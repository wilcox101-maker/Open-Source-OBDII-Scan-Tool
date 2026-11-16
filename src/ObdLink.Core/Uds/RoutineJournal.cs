using System.Text;
using System.Text.Json;

namespace ObdLink.Core.Uds;

/// <summary>Outcome values of a <see cref="RoutineEntry"/>.</summary>
public static class RoutineOutcome
{
    /// <summary>Written before the request; still the latest line after a crash or a pulled adapter.</summary>
    public const string Pending = "pending";
    /// <summary>The module answered positively (71 xx).</summary>
    public const string Accepted = "accepted";
    /// <summary>The module refused (7F 31 nrc): definite, nothing ran.</summary>
    public const string Refused = "refused";
    /// <summary>No final answer (link drop, timeout): the routine may or may not be running.</summary>
    public const string Unknown = "unknown";
    /// <summary>An unfinished entry was looked at later with 31 03.</summary>
    public const string Checked = "checked";
}

/// <summary>
/// One line of the routine journal. Lines with the same <see cref="Id"/> are one attempt; the newest line is its state.
/// <see cref="Action"/> is "start", "stop", "results" or "procedure:&lt;key&gt;" (a service procedure run).
/// </summary>
public sealed record RoutineEntry(DateTimeOffset At, string Id, string Action, uint TxId, string Module, ushort RoutineId, string OptionHex,
    string Outcome, string Detail)
{
    public string Where => $"{Module} (0x{TxId:X3}) routine 0x{RoutineId:X4}";
    public bool Unfinished => Outcome is RoutineOutcome.Pending or RoutineOutcome.Unknown;
}

/// <summary>
/// R1 (write recovery plan): RoutineControl start / stop / results are journaled. Each attempt is appended (flushed to disk)
/// BEFORE the request and resolved after it, so an attempt cut short by a crash, a pulled adapter or a timeout stays visible as
/// <see cref="RoutineOutcome.Pending"/> / <see cref="RoutineOutcome.Unknown"/>. Its effect can't be undone in general (the module
/// defines it), but it can be checked with 31 03 after reconnecting. Append-only JSON lines; a torn last line is skipped.
/// </summary>
public sealed class RoutineJournal
{
    public const string FileName = "routine-journal.jsonl";
    private static readonly JsonSerializerOptions Json = new();
    private readonly object _sync = new();
    public string Path { get; }

    public RoutineJournal(string dir) { Directory.CreateDirectory(dir); Path = System.IO.Path.Combine(dir, FileName); }

    public RoutineEntry Append(RoutineEntry e)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e, Json) + "\n");
        lock (_sync)
        {
            using var fs = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }
        return e;
    }

    public RoutineEntry Resolve(RoutineEntry attempt, string outcome, string detail) =>
        Append(attempt with { At = DateTimeOffset.Now, Outcome = outcome, Detail = detail });

    /// <summary>Every line in file order. Unreadable lines (a torn write at a crash) are skipped.</summary>
    public IReadOnlyList<RoutineEntry> ReadAll()
    {
        if (!File.Exists(Path)) return Array.Empty<RoutineEntry>();
        string[] lines;
        lock (_sync) lines = File.ReadAllLines(Path);
        var list = new List<RoutineEntry>();
        foreach (var l in lines)
        {
            if (l.Length == 0) continue;
            try { if (JsonSerializer.Deserialize<RoutineEntry>(l, Json) is { } e) list.Add(e); }
            catch (JsonException) { /* torn last line after a crash: skipped, the earlier lines still count */ }
        }
        return list;
    }

    /// <summary>Attempts whose newest line is pending or unknown, newest first.</summary>
    public IReadOnlyList<RoutineEntry> Unfinished() => ReadAll()
        .GroupBy(e => e.Id).Select(g => g.Last())
        .Where(e => e.Unfinished)
        .OrderByDescending(e => e.At).ToList();

    /// <summary>
    /// Journals one request (or one service-procedure run) around <paramref name="send"/>: pending first, then accepted, refused
    /// (a negative response, also when wrapped, e.g. by the procedure runner: definite, nothing ran), or unknown (any other
    /// failure, including cancellation). Exceptions are rethrown unchanged.
    /// </summary>
    public async Task<T> RecordAsync<T>(string action, uint txId, string module, ushort routineId, byte[]? option, Func<Task<T>> send, Func<T, string>? describe = null)
    {
        var e = Append(new RoutineEntry(DateTimeOffset.Now, Guid.NewGuid().ToString("N"), action, txId, module, routineId,
            Convert.ToHexString(option ?? Array.Empty<byte>()), RoutineOutcome.Pending, ""));
        try
        {
            var r = await send();
            Resolve(e, RoutineOutcome.Accepted, describe?.Invoke(r) ?? (r is RoutineResult rr && rr.StatusRecord.Length > 0 ? "status " + Convert.ToHexString(rr.StatusRecord) : ""));
            return r;
        }
        catch (Exception ex) when ((ex as UdsNegativeResponseException ?? ex.InnerException as UdsNegativeResponseException) is { } nrc)
        {
            Resolve(e, RoutineOutcome.Refused, $"NRC 0x{nrc.Nrc:X2} {UdsNrc.Describe(nrc.Nrc)}");
            throw;
        }
        catch (Exception ex)
        {
            Resolve(e, RoutineOutcome.Unknown, ex is OperationCanceledException ? "cancelled before an answer" : "no final answer: " + ex.Message);
            throw;
        }
    }

    /// <summary>Marks every unfinished attempt for this module + routine as checked with what 31 03 (or its refusal) said.</summary>
    public int MarkChecked(uint txId, ushort routineId, string detail)
    {
        int n = 0;
        foreach (var e in Unfinished().Where(e => e.TxId == txId && e.RoutineId == routineId)) { Resolve(e, RoutineOutcome.Checked, detail); n++; }
        return n;
    }

    /// <summary>The line shown for an unfinished attempt (Routines tab, event log).</summary>
    public static string Describe(RoutineEntry e) =>
        $"UNFINISHED: {e.Action} of {e.Where} at {e.At.LocalDateTime:yyyy-MM-dd HH:mm:ss} got no final answer ({(e.Outcome == RoutineOutcome.Pending ? "the app or link stopped first" : e.Detail)}). "
        + "It may have run. Select the module, enter the routine ID and press Results (31 03) to check; the module defines what the routine did.";
}
