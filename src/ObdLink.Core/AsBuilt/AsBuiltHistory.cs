using System.Text;
using System.Text.Json;

namespace ObdLink.Core.AsBuilt;

/// <summary>What happened to one As-Built write or restore of a block.</summary>
public static class AsBuiltOutcome
{
    /// <summary>Recorded before the 0x2E was sent; no answer recorded yet. After a crash or a pulled adapter this is what remains.</summary>
    public const string Pending = "pending";
    /// <summary>The read-back matched the new data.</summary>
    public const string Verified = "verified";
    /// <summary>The read-back differed from the new data: the block holds something else (neither old nor new, or old).</summary>
    public const string Mismatch = "mismatch";
    /// <summary>No usable answer and no read-back: the block may hold the old data, the new data, or neither.</summary>
    public const string Unknown = "unknown";
    /// <summary>The module refused the write with a definite negative response: nothing was stored.</summary>
    public const string Rejected = "rejected";
    /// <summary>A later check read the block and found the old data: the write never took effect.</summary>
    public const string NotWritten = "not-written";
    /// <summary>The backup taken before this write was restored and verified.</summary>
    public const string Recovered = "recovered";

    /// <summary>Outcomes that leave the block in an uncertain or unwanted state and need the operator.</summary>
    public static bool NeedsAttention(string outcome) => outcome is Pending or Mismatch or Unknown;
    /// <summary>Outcomes after which the module may hold something other than the backup (so restoring the backup undoes it).</summary>
    public static bool MayHaveChanged(string outcome) => outcome is not (Rejected or NotWritten);
}

/// <summary>
/// One As-Built block change. <see cref="BackupFile"/> (a file name in the backups folder) holds what the module had
/// before, so it can be undone. Stored as a name, not a full path, so the history still works when the portable Data folder moves.
/// <see cref="AttemptId"/> ties a <see cref="AsBuiltOutcome.Pending"/> line written before the request to its outcome line;
/// lines written before v0.21 have neither and were all verified.
/// </summary>
public sealed record AsBuiltChange(DateTimeOffset At, string Kind, ushort Module, byte Block, string Before, string After, int BytesChanged, string BackupFile, string VinTail, string Note = "",
    string Outcome = AsBuiltOutcome.Verified, string? AttemptId = null)
{
    public string Where => $"{Module:X3}-{Block:X2}";
}

/// <summary>
/// Append-only journal of As-Built writes and restores next to the backups (asbuilt-history.jsonl, one JSON object per line).
/// v0.16.0 recorded only verified changes. Now every write is journaled: a <see cref="AsBuiltOutcome.Pending"/> line is flushed to
/// disk before the 0x2E is sent and an outcome line follows, so a write interrupted by a crash, a pulled adapter or a failed
/// read-back is never lost (<see cref="Unresolved"/>). <see cref="Load"/> folds the lines into one change per attempt.
/// The VIN is kept as its last 6 characters. Unreadable lines (a crash mid-append, a hand edit) are skipped. It lives in the backups
/// folder, which already holds full VINs (in backup names and contents); it is local data and is not part of the diagnostics capture.
/// </summary>
public sealed class AsBuiltHistory
{
    public const string FileName = "asbuilt-history.jsonl";
    private static readonly JsonSerializerOptions Json = new();
    private readonly object _sync = new();
    public string Path { get; }
    public string Dir { get; }

    public AsBuiltHistory(string dir) { Directory.CreateDirectory(dir); Dir = dir; Path = System.IO.Path.Combine(dir, FileName); }

    /// <summary>Full path of a change's backup file in this history's folder.</summary>
    public string BackupPathOf(AsBuiltChange c) => System.IO.Path.Combine(Dir, System.IO.Path.GetFileName(c.BackupFile));

    /// <summary>Appends one line and flushes it to disk before returning (the pending line must survive a crash right after the request).</summary>
    public void Append(AsBuiltChange c)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(c, Json) + Environment.NewLine);
        lock (_sync)
        {
            using var fs = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }
    }

    /// <summary>Records the outcome of an attempt (same attempt id, newer time).</summary>
    public AsBuiltChange Resolve(AsBuiltChange attempt, string outcome, string note = "", string? after = null)
    {
        var line = attempt with
        {
            At = DateTimeOffset.Now, Outcome = outcome, After = after ?? attempt.After,
            Note = string.IsNullOrEmpty(note) ? attempt.Note : (string.IsNullOrEmpty(attempt.Note) ? note : attempt.Note + "; " + note),
        };
        Append(line);
        return line;
    }

    /// <summary>Every line as written, oldest first (pending and outcome lines separately).</summary>
    public IReadOnlyList<AsBuiltChange> LoadLines()
    {
        lock (_sync)
        {
            if (!File.Exists(Path)) return Array.Empty<AsBuiltChange>();
            var list = new List<AsBuiltChange>();
            foreach (var l in File.ReadLines(Path))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                try { if (JsonSerializer.Deserialize<AsBuiltChange>(l, Json) is { } c) list.Add(c); }
                catch (JsonException) { /* a torn or hand-edited line: skip it, keep the rest */ }
            }
            return list;
        }
    }

    /// <summary>One change per attempt (its latest line), ordered by when the attempt started, oldest first.</summary>
    public IReadOnlyList<AsBuiltChange> Load()
    {
        var lines = LoadLines();
        var order = new List<string>();
        var latest = new Dictionary<string, AsBuiltChange>();
        var started = new Dictionary<string, DateTimeOffset>();
        for (int i = 0; i < lines.Count; i++)
        {
            var c = lines[i];
            var key = c.AttemptId ?? "#" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!latest.ContainsKey(key)) { order.Add(key); started[key] = c.At; }
            latest[key] = c;
        }
        return order.Select(k => latest[k] with { At = started[k] }).ToList();
    }

    /// <summary>
    /// The newest change of <paramref name="module"/> that can be undone: it may have changed the block and its backup file still
    /// exists. Null if none. Undo = restore that backup (the normal restore path: VIN match, fresh backup, confirmations, read-back).
    /// A write that ended mismatched, unknown or interrupted counts: restoring the backup taken before it is how it is recovered.
    /// </summary>
    public AsBuiltChange? LastUndoable(ushort module) =>
        Load().Where(c => c.Module == module && AsBuiltOutcome.MayHaveChanged(c.Outcome) && c.Outcome != AsBuiltOutcome.Recovered && File.Exists(BackupPathOf(c)))
            .LastOrDefault();

    /// <summary>
    /// Writes that did not end cleanly (pending, mismatch, unknown) and have not been settled since: per module and block, the newest
    /// attempt, if it needs attention. Shown as "a write did not finish" so the operator checks or restores it.
    /// </summary>
    public IReadOnlyList<AsBuiltChange> Unresolved() =>
        Load().GroupBy(c => (c.Module, c.Block)).Select(g => g.Last()).Where(c => AsBuiltOutcome.NeedsAttention(c.Outcome)).ToList();

    /// <summary>VIN shown in the history list: last 6 characters.</summary>
    public static string VinTail(string? vin) => vin is { Length: >= 6 } ? vin[^6..] : "?";
}
