using System.Text;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Dtc;

/// <summary>One DTC in a snapshot: its 19 02 record, and its 19 03/04/06 detail or why that could not be read.</summary>
public sealed record DtcSnapshotEntry(DtcRecord Dtc, DtcDetail? Detail, string? Error);

/// <summary>
/// C1 (write recovery plan): a clear (14 FF FF FF) deletes freeze frames and DTC history in the module for good, so the app reads
/// every DTC with its freeze-frame (19 04) and extended data (19 06) and saves it to a file BEFORE clearing. Reads only.
/// A snapshot that could not be read completely says so (<see cref="Complete"/>); the caller then asks for an extra confirmation.
/// </summary>
public sealed record DtcSnapshot(string Module, uint TxId, DateTimeOffset Taken, IReadOnlyList<DtcSnapshotEntry> Entries, string? ListError)
{
    /// <summary>True when the DTC list and every DTC's detail were read.</summary>
    public bool Complete => ListError is null && Entries.All(e => e.Error is null);

    /// <summary>Short text for the confirmation: how many DTCs were saved and what is missing.</summary>
    public string Summary => ListError is not null ? $"DTC list could not be read ({ListError})"
        : Entries.Count == 0 ? "no DTCs stored"
        : $"{Entries.Count} DTC(s), {Entries.Sum(e => e.Detail?.Snapshots.Count ?? 0)} freeze frame(s)"
          + (Entries.Count(e => e.Error is not null) is int n and > 0 ? $", details missing for {n}" : "");

    /// <summary>Reads 19 02 (all statuses), then 19 03/04/06 per DTC. A failing read is recorded, not thrown (except cancellation).</summary>
    public static async Task<DtcSnapshot> TakeAsync(UdsClient uds, string module, uint txId, CancellationToken ct = default)
    {
        List<DtcRecord> list;
        try { list = await uds.ReadDtcsByStatusAsync(ct: ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DtcSnapshot(module, txId, DateTimeOffset.Now, Array.Empty<DtcSnapshotEntry>(), $"19 02: {ex.Message}");
        }
        var reader = new UdsDtcReader(uds);
        var entries = new List<DtcSnapshotEntry>();
        foreach (var d in list)
        {
            try { entries.Add(new DtcSnapshotEntry(d, await reader.DetailAsync(d.Code, d.Status, ct), null)); }
            catch (Exception ex) when (ex is not OperationCanceledException) { entries.Add(new DtcSnapshotEntry(d, null, ex.Message)); }
        }
        return new DtcSnapshot(module, txId, DateTimeOffset.Now, entries, null);
    }

    /// <summary>The file text: header, then each DTC as the DTC details dialog shows it.</summary>
    public string Format(Func<string, string>? describe = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DTC snapshot before clear: {Module} (0x{TxId:X3})");
        sb.AppendLine($"Taken {Taken:yyyy-MM-dd HH:mm:ss zzz}. Read with 19 02 FF, 19 03, 19 04, 19 06 (reads only).");
        sb.AppendLine($"Contents: {Summary}{(Complete ? "" : " - INCOMPLETE")}");
        sb.AppendLine();
        if (ListError is not null) sb.AppendLine($"The DTC list could not be read: {ListError}");
        foreach (var e in Entries)
        {
            var code = e.Dtc.Display;
            if (describe?.Invoke(code) is { Length: > 0 } text) sb.AppendLine(text);
            if (e.Detail is { } d) sb.AppendLine(UdsDtcReader.Format(code, d).TrimEnd());
            else sb.AppendLine($"{code}   status 0x{e.Dtc.Status:X2}   details not read: {e.Error}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>
    /// Writes the snapshot to <paramref name="dir"/> and flushes it to disk before returning, so it survives a crash during the clear.
    /// Returns the full path. Never overwrites: a second snapshot in the same second gets a suffix.
    /// </summary>
    public string Save(string dir, Func<string, string>? describe = null)
    {
        Directory.CreateDirectory(dir);
        var safe = new string(Module.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        var stem = $"dtc-snapshot-{safe}-{TxId:X3}-{Taken:yyyyMMdd-HHmmss}";
        var path = Path.Combine(dir, stem + ".txt");
        for (int i = 2; File.Exists(path); i++) path = Path.Combine(dir, $"{stem}-{i}.txt");
        var bytes = new UTF8Encoding(false).GetBytes(Format(describe));
        using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        {
            fs.Write(bytes);
            fs.Flush(true);
        }
        return path;
    }
}
