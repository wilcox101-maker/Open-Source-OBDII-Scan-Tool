using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObdLink.Core.Flash;

public enum FlashJournalKind
{
    Started, Backup, Phase, SegmentStarted, BlockAcked, SegmentDone, Fault, Interrupted, Resumed, Completed, Abandoned,
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
    int SegmentsDone, long BytesAcked, FlashJournalEntry? LastFault, int Attempts, bool ReachedProgramming = false, bool ReachedReset = false)
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
        bool programming = false, reset = false;
        foreach (var e in entries)
        {
            exists = true;
            switch (e.Kind)
            {
                case FlashJournalKind.Started: open = true; sha = e.ImageSha256; tx = e.TxId; attempts = 1; segsDone = 0; bytes = 0; fault = null; backup = null; programming = reset = false; break;
                case FlashJournalKind.Resumed: attempts++; segsDone = 0; bytes = 0; break; // a resume restarts the download from the erase
                case FlashJournalKind.Backup: backup = e.Detail; break;
                case FlashJournalKind.Phase:
                    phase = e.Phase;
                    if (Enum.TryParse<FlashPhase>(e.Phase, out var p))
                    {
                        if (p >= FlashPhase.ProgrammingSession) programming = true;   // from the first 10 02 on, the module may be in its bootloader
                        if (p >= FlashPhase.Reset) reset = true;                      // download and check finished in some attempt
                    }
                    break;
                case FlashJournalKind.SegmentDone: segsDone++; break;
                case FlashJournalKind.BlockAcked: if (e.Offset is long o) bytes = Math.Max(bytes, o); break;
                case FlashJournalKind.Fault or FlashJournalKind.Interrupted: fault = e; break;
                case FlashJournalKind.Completed or FlashJournalKind.Abandoned: open = false; break;
            }
        }
        return new(exists, open, sha, tx, backup, phase, segsDone, bytes, fault, attempts, programming, reset);
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
