using System.Text.Json;

namespace ObdLink.Core.AsBuilt;

/// <summary>
/// One verified As-Built block change. <see cref="BackupFile"/> (a file name in the backups folder) holds what the module had
/// before, so it can be undone. Stored as a name, not a full path, so the history still works when the portable Data folder moves.
/// </summary>
public sealed record AsBuiltChange(DateTimeOffset At, string Kind, ushort Module, byte Block, string Before, string After, int BytesChanged, string BackupFile, string VinTail, string Note = "")
{
    public string Where => $"{Module:X3}-{Block:X2}";
}

/// <summary>
/// v0.16.0: append-only change history next to the backups (asbuilt-history.jsonl, one JSON object per line). A line is added
/// only after a write or restore of a block was verified by read-back. The VIN is kept as its last 6 characters. Unreadable
/// lines (a crash mid-append, a hand edit) are skipped when reading. It lives in the backups folder, which already holds full
/// VINs (in backup names and contents); it is local data and is not part of the diagnostics capture.
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

    public void Append(AsBuiltChange c)
    {
        var line = JsonSerializer.Serialize(c, Json);
        lock (_sync) File.AppendAllText(Path, line + Environment.NewLine);
    }

    /// <summary>All entries, oldest first.</summary>
    public IReadOnlyList<AsBuiltChange> Load()
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

    /// <summary>
    /// The newest change of <paramref name="module"/> that can be undone: its backup file still exists. Null if none.
    /// Undo = restore that backup (the normal restore path: VIN match, fresh backup, confirmations, read-back).
    /// </summary>
    public AsBuiltChange? LastUndoable(ushort module) =>
        Load().Where(c => c.Module == module && File.Exists(BackupPathOf(c))).OrderBy(c => c.At).LastOrDefault();

    /// <summary>VIN shown in the history list: last 6 characters.</summary>
    public static string VinTail(string? vin) => vin is { Length: >= 6 } ? vin[^6..] : "?";
}
