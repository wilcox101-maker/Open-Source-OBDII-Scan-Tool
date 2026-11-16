using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObdLink.Core.Flash;

public enum FlashJournalKind
{
    Started, Backup, Phase, SegmentStarted, BlockAcked, SegmentDone, Fault, Interrupted, Resumed, Completed, Abandoned,
    /// <summary>A SecurityAccess outcome (Detail: one of <see cref="FlashSecurity"/>'s constants). Kept across jobs: lockout belongs to the module.</summary>
    Security,
    /// <summary>The module left the programming session mid-job; the engine started again from the session and erase in the same run.</summary>
    Restarted,
}

/// <summary>SecurityAccess outcomes recorded in the journal, and the rule that keeps resumes from locking a module out.</summary>
public static class FlashSecurity
{
    public const string Ok = "ok", WrongKey = "wrong-key", Locked = "locked", Delay = "delay", NoKey = "no-key", HoldCleared = "hold-cleared";

    /// <summary>
    /// Why another key must not be sent now, or null when it may. After a rejected key (NRC 0x35) the provider is presumed wrong:
    /// sending it again only spends the module's attempt counter, so the operator must clear the hold. After a lockout (NRC 0x36,
    /// or 0x37 that did not clear) no seed is requested until <paramref name="lockoutHold"/> has passed.
    /// </summary>
    public static string? Hold(FlashJournalState st, TimeSpan lockoutHold, DateTimeOffset now)
    {
        if (st.WrongKeys > 0)
            return $"The module rejected the security key on an earlier attempt (NRC 0x35, invalid key). Another try with the same key would count toward its lockout (NRC 0x36). Fix the seed/key provider, then clear the security hold.";
        if (st.LockedAt is DateTimeOffset at && now - at < lockoutHold)
        {
            var left = lockoutHold - (now - at);
            return $"The module locked security access (too many attempts or a delay that did not clear). Leave the ignition on and wait {Math.Ceiling(left.TotalMinutes):0} more minute(s) before trying again.";
        }
        return null;
    }
}

/// <summary>One line of the flash journal. Only the fields relevant to <see cref="Kind"/> are set.</summary>
public sealed record FlashJournalEntry(
    DateTimeOffset At, FlashJournalKind Kind,
    string? Phase = null, int? Segment = null, int? Block = null, byte? Counter = null, long? Offset = null,
    string? Detail = null, string? ImageSha256 = null, uint? TxId = null)
{
    [JsonIgnore] public string Summary => Kind switch
    {
        FlashJournalKind.BlockAcked => $"segment {Segment} block {Block} (counter 0x{Counter:X2}) at offset {Offset}",
        _ => string.Join(" ", new[] { Kind.ToString(), Phase, Detail }.Where(s => !string.IsNullOrEmpty(s))),
    };
}

/// <summary>What the journal says about a job: whether one is open, for which image and module, and where it stopped.</summary>
public sealed record FlashJournalState(
    bool Exists, bool Open, string? ImageSha256, uint? TxId, string? BackupRef, string? LastPhase,
    int SegmentsDone, long BytesAcked, FlashJournalEntry? LastFault, int Attempts, int WrongKeys = 0, DateTimeOffset? LockedAt = null)
{
    public static readonly FlashJournalState None = new(false, false, null, null, null, null, 0, 0, null, 0);

    public static FlashJournalState From(IEnumerable<FlashJournalEntry> entries)
    {
        bool exists = false, open = false;
        string? sha = null, backup = null, phase = null;
        uint? tx = null;
        int segsDone = 0, attempts = 0;
        long bytes = 0;
        FlashJournalEntry? fault = null;
        int wrongKeys = 0;
        DateTimeOffset? lockedAt = null;
        foreach (var e in entries)
        {
            exists = true;
            switch (e.Kind)
            {
                case FlashJournalKind.Started: open = true; sha = e.ImageSha256; tx = e.TxId; attempts = 1; segsDone = 0; bytes = 0; fault = null; break;
                case FlashJournalKind.Resumed: attempts++; segsDone = 0; bytes = 0; break; // a resume restarts the download from the erase
                case FlashJournalKind.Restarted: segsDone = 0; bytes = 0; break;
                case FlashJournalKind.Security:
                    switch (e.Detail)
                    {
                        case FlashSecurity.Ok or FlashSecurity.HoldCleared: wrongKeys = 0; lockedAt = null; break;
                        case FlashSecurity.WrongKey: wrongKeys++; break;
                        case FlashSecurity.Locked: lockedAt = e.At; break;
                    }
                    break;
                case FlashJournalKind.Backup: backup = e.Detail; break;
                case FlashJournalKind.Phase: phase = e.Phase; break;
                case FlashJournalKind.SegmentDone: segsDone++; break;
                case FlashJournalKind.BlockAcked: if (e.Offset is long o) bytes = Math.Max(bytes, o); break;
                case FlashJournalKind.Fault or FlashJournalKind.Interrupted: fault = e; break;
                case FlashJournalKind.Completed or FlashJournalKind.Abandoned: open = false; break;
            }
        }
        return new(exists, open, sha, tx, backup, phase, segsDone, bytes, fault, attempts, wrongKeys, lockedAt);
    }
}

/// <summary>Append-only record of a flash job. Every entry must be durable before the next request is sent.</summary>
public interface IFlashJournal
{
    void Append(FlashJournalEntry entry);
    IReadOnlyList<FlashJournalEntry> ReadAll();
}

public sealed class MemoryFlashJournal : IFlashJournal
{
    private readonly List<FlashJournalEntry> _entries = new();
    public void Append(FlashJournalEntry entry) { lock (_entries) _entries.Add(entry); }
    public IReadOnlyList<FlashJournalEntry> ReadAll() { lock (_entries) return _entries.ToList(); }
}

/// <summary>
/// JSON-lines journal file. Each append opens the file with write-through and flushes to disk before returning, so a PC crash or
/// power loss leaves every acknowledged step on disk. A torn final line (crash mid-write) is skipped when reading.
/// </summary>
public sealed class FileFlashJournal(string path) : IFlashJournal
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() }, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly object _lock = new();
    public string Path { get; } = path;

    public void Append(FlashJournalEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, Json) + "\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(line);
        lock (_lock)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path))!);
            using var fs = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            fs.Write(bytes);
            fs.Flush(flushToDisk: true);
        }
    }

    public IReadOnlyList<FlashJournalEntry> ReadAll()
    {
        lock (_lock)
        {
            if (!File.Exists(Path)) return Array.Empty<FlashJournalEntry>();
            var list = new List<FlashJournalEntry>();
            foreach (var l in File.ReadAllLines(Path))
            {
                if (string.IsNullOrWhiteSpace(l)) continue;
                try { if (JsonSerializer.Deserialize<FlashJournalEntry>(l, Json) is { } e) list.Add(e); }
                catch (JsonException) { /* torn last line after a crash: everything before it is intact */ }
            }
            return list;
        }
    }
}
