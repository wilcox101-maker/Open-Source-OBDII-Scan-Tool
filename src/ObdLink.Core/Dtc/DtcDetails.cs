using ObdLink.Core.IsoTp;
using ObdLink.Core.Obd;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Dtc;

/// <summary>ISO 14229-1 DTC status byte (bit 0 = testFailed ... bit 7 = warningIndicatorRequested).</summary>
public static class DtcStatusBits
{
    public static readonly string[] Names =
    {
        "testFailed", "testFailedThisOperationCycle", "pendingDTC", "confirmedDTC",
        "testNotCompletedSinceLastClear", "testFailedSinceLastClear", "testNotCompletedThisOperationCycle", "warningIndicatorRequested",
    };

    /// <summary>Plain-English labels for the grid / details.</summary>
    public static readonly string[] Plain =
    {
        "failing now", "failed this drive cycle", "pending", "confirmed (stored)",
        "not tested since clear", "failed since clear", "not tested this cycle", "warning lamp requested",
    };

    public static IEnumerable<string> Set(byte status, bool plain = true)
    {
        for (int i = 0; i < 8; i++) if ((status & (1 << i)) != 0) yield return (plain ? Plain : Names)[i];
    }

    /// <summary>Short summary for a grid column, e.g. "confirmed, failing now, MIL".</summary>
    public static string Summary(byte status)
    {
        var parts = new List<string>();
        if ((status & 0x08) != 0) parts.Add("confirmed");
        else if ((status & 0x04) != 0) parts.Add("pending");
        else if ((status & 0x20) != 0) parts.Add("history");
        if ((status & 0x01) != 0) parts.Add("failing now");
        if ((status & 0x80) != 0) parts.Add("lamp");
        return parts.Count == 0 ? "-" : string.Join(", ", parts);
    }
}

/// <summary>One record of 19 04 (snapshot) or 19 06 (extended data). Data layout is module-specific, so it is kept raw.</summary>
public sealed record DtcDataRecord(byte Number, byte? IdentifierCount, ushort? FirstDid, byte[] Data)
{
    public string Hex => Convert.ToHexString(Data);
}

/// <summary>19 0B-0E: which single DTC to report.</summary>
public enum DtcOccurrence : byte { FirstFailed = 0x0B, FirstConfirmed = 0x0C, MostRecentFailed = 0x0D, MostRecentConfirmed = 0x0E }

/// <summary>19 14 entry: DTC and its fault detection counter (-128..+127).</summary>
public sealed record DtcFdc(uint Code, sbyte Counter)
{
    public string Display => new DtcRecord(Code, 0).Display;
}

/// <summary>19 05 reply: the DTC that stored the snapshot, and the snapshot (layout module-specific, kept raw).</summary>
public sealed record DtcSnapshotByRecord(DtcRecord Dtc, DtcDataRecord Snapshot);

public sealed record DtcDetail(DtcDataRecord Dtc3, byte Status, IReadOnlyList<DtcDataRecord> Snapshots, IReadOnlyList<DtcDataRecord> Extended, IReadOnlyList<string> Notes);

/// <summary>
/// Read-only DTC detail over UDS service 0x19: count (01), snapshot identification (03), snapshot by DTC (04) and by record (05),
/// extended data by DTC (06), supported DTCs (0A), first/most recent (0B-0E), fault detection counters (14), permanent (15).
/// Every request here is a read; nothing is cleared or changed.
/// </summary>
public sealed class UdsDtcReader(UdsClient uds)
{
    /// <summary>19 01: number of DTCs matching <paramref name="mask"/>.</summary>
    public async Task<int> CountAsync(byte mask = 0xFF, CancellationToken ct = default)
    {
        var r = await uds.RequestAsync(new byte[] { 0x19, 0x01, mask }, ct);
        // 59 01 [availabilityMask] [formatId] [countHi] [countLo]
        if (r.Length < 6) throw new InvalidDataException($"Short 19 01 reply: {Convert.ToHexString(r)}");
        return r[4] << 8 | r[5];
    }

    /// <summary>19 03: (DTC, snapshot record number) pairs the module holds.</summary>
    public async Task<List<(uint Dtc, byte Record)>> SnapshotIdsAsync(CancellationToken ct = default)
    {
        var r = await uds.RequestAsync(new byte[] { 0x19, 0x03 }, ct);
        var list = new List<(uint, byte)>();
        for (int i = 2; i + 4 <= r.Length; i += 4) list.Add(((uint)(r[i] << 16 | r[i + 1] << 8 | r[i + 2]), r[i + 3]));
        return list;
    }

    /// <summary>
    /// 19 04 for one record. Reply: 59 04 DTC(3) status [record# identifierCount (DID data)...]. When the module sends one identifier
    /// the DID is split out; otherwise the record is kept raw because data lengths are module-specific.
    /// </summary>
    public static DtcDataRecord? ParseSnapshot(byte[] r)
    {
        if (r.Length < 7) return null; // no record stored
        byte num = r[6];
        if (r.Length < 8) return new(num, null, null, Array.Empty<byte>());
        byte count = r[7];
        var rest = r[8..];
        if (count == 1 && rest.Length >= 2) return new(num, count, (ushort)(rest[0] << 8 | rest[1]), rest[2..]);
        return new(num, count, null, rest);
    }

    /// <summary>19 06 reply: 59 06 DTC(3) status [record# data...]. Without per-module definitions all records after the first stay in one blob.</summary>
    public static DtcDataRecord? ParseExtended(byte[] r) => r.Length < 7 ? null : new(r[6], null, null, r[7..]);

    public async Task<DtcDetail> DetailAsync(uint dtc, byte status, CancellationToken ct = default)
    {
        var notes = new List<string>();
        var snaps = new List<DtcDataRecord>();
        var ext = new List<DtcDataRecord>();
        byte[] D(uint d) => new[] { (byte)(d >> 16), (byte)(d >> 8), (byte)d };

        IEnumerable<byte> records = new byte[] { 0xFF };
        try
        {
            var ids = await SnapshotIdsAsync(ct);
            var mine = ids.Where(x => x.Dtc == dtc).Select(x => x.Record).Distinct().ToList();
            records = mine;
            if (mine.Count == 0) notes.Add("No freeze-frame (snapshot) records for this DTC.");
        }
        catch (UdsNegativeResponseException ex) { notes.Add($"Snapshot list (19 03) not supported: NRC 0x{ex.Nrc:X2}; asking for all records."); }

        foreach (var rec in records)
        {
            try
            {
                var r = await uds.RequestAsync(new byte[] { 0x19, 0x04 }.Concat(D(dtc)).Append(rec).ToArray(), ct);
                if (ParseSnapshot(r) is { } s) snaps.Add(s);
            }
            catch (UdsNegativeResponseException ex) { notes.Add($"Snapshot 0x{rec:X2} (19 04): NRC 0x{ex.Nrc:X2} {UdsNrc.Describe(ex.Nrc)}"); }
        }
        try
        {
            var r = await uds.RequestAsync(new byte[] { 0x19, 0x06 }.Concat(D(dtc)).Append((byte)0xFF).ToArray(), ct);
            if (ParseExtended(r) is { } e) ext.Add(e); else notes.Add("No extended data records for this DTC.");
        }
        catch (UdsNegativeResponseException ex) { notes.Add($"Extended data (19 06): NRC 0x{ex.Nrc:X2} {UdsNrc.Describe(ex.Nrc)}"); }

        return new DtcDetail(new DtcDataRecord(0, null, null, D(dtc)), status, snaps, ext, notes);
    }

    /// <summary>19 0A: every DTC the module supports, with its current status.</summary>
    public async Task<List<DtcRecord>> SupportedAsync(CancellationToken ct = default)
        => ParseDtcStatusList(await uds.RequestAsync(new byte[] { 0x19, 0x0A }, ct), 0x0A);

    /// <summary>19 15: DTCs with permanent status (cannot be cleared by 14; they clear when the monitor passes).</summary>
    public async Task<List<DtcRecord>> PermanentAsync(CancellationToken ct = default)
        => ParseDtcStatusList(await uds.RequestAsync(new byte[] { 0x19, 0x15 }, ct), 0x15);

    /// <summary>19 0B / 0C / 0D / 0E: the first or most recent failed or confirmed DTC, or null when the module has none.</summary>
    public async Task<DtcRecord?> OccurrenceAsync(DtcOccurrence which, CancellationToken ct = default)
    {
        byte sub = (byte)which;
        return ParseDtcStatusList(await uds.RequestAsync(new byte[] { 0x19, sub }, ct), sub).FirstOrDefault();
    }

    /// <summary>
    /// 19 14: fault detection counter per DTC (signed: +127 = failed, -128 = passed, between = maturing). Shows which faults are
    /// close to setting before they do.
    /// </summary>
    public async Task<List<DtcFdc>> FaultDetectionCountersAsync(CancellationToken ct = default)
    {
        var r = await uds.RequestAsync(new byte[] { 0x19, 0x14 }, ct);
        if (r.Length < 2 || r[0] != 0x59 || r[1] != 0x14 || (r.Length - 2) % 4 != 0)
            throw new InvalidDataException($"Malformed 19 14 reply: {Convert.ToHexString(r)}");
        var list = new List<DtcFdc>();
        for (int i = 2; i + 4 <= r.Length; i += 4) list.Add(new DtcFdc((uint)(r[i] << 16 | r[i + 1] << 8 | r[i + 2]), unchecked((sbyte)r[i + 3])));
        return list;
    }

    /// <summary>19 05: one snapshot record by number, with the DTC that stored it. Null when the record is empty.</summary>
    public async Task<DtcSnapshotByRecord?> SnapshotByRecordAsync(byte record, CancellationToken ct = default)
    {
        var r = await uds.RequestAsync(new byte[] { 0x19, 0x05, record }, ct);
        if (r.Length < 2 || r[0] != 0x59 || r[1] != 0x05) throw new InvalidDataException($"Malformed 19 05 reply: {Convert.ToHexString(r)}");
        if (r.Length < 7) return null;
        var dtc = new DtcRecord((uint)(r[3] << 16 | r[4] << 8 | r[5]), r[6]);
        byte? count = r.Length >= 8 ? r[7] : null;
        var rest = r.Length > 8 ? r[8..] : Array.Empty<byte>();
        var snap = count == 1 && rest.Length >= 2
            ? new DtcDataRecord(r[2], count, (ushort)(rest[0] << 8 | rest[1]), rest[2..])
            : new DtcDataRecord(r[2], count, null, rest);
        return new DtcSnapshotByRecord(dtc, snap);
    }

    /// <summary>Parses 59 [sub] [availabilityMask] then (DTC, status) records. The sub-function must be echoed and records must be whole.</summary>
    public static List<DtcRecord> ParseDtcStatusList(byte[] r, byte sub)
    {
        if (r.Length < 3 || r[0] != 0x59 || r[1] != sub || (r.Length - 3) % 4 != 0)
            throw new InvalidDataException($"Malformed 19 {sub:X2} reply: {Convert.ToHexString(r)}");
        var list = new List<DtcRecord>();
        for (int i = 3; i + 4 <= r.Length; i += 4) list.Add(new DtcRecord((uint)(r[i] << 16 | r[i + 1] << 8 | r[i + 2]), r[i + 3]));
        return list;
    }

    public static string Format(string display, DtcDetail d)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{display}   status 0x{d.Status:X2}");
        for (int i = 0; i < 8; i++) sb.AppendLine($"  [{((d.Status & (1 << i)) != 0 ? 'x' : ' ')}] bit {i} {DtcStatusBits.Names[i]} ({DtcStatusBits.Plain[i]})");
        sb.AppendLine();
        sb.AppendLine($"Freeze-frame / snapshot records: {d.Snapshots.Count}");
        foreach (var s in d.Snapshots)
            sb.AppendLine($"  record 0x{s.Number:X2}" + (s.IdentifierCount is byte c ? $", {c} identifier(s)" : "") + (s.FirstDid is ushort did ? $", DID 0x{did:X4}" : "") + $": {s.Hex}");
        sb.AppendLine($"Extended data records: {d.Extended.Count}");
        foreach (var e in d.Extended) sb.AppendLine($"  record 0x{e.Number:X2}: {e.Hex}");
        if (d.Notes.Count > 0) { sb.AppendLine(); foreach (var n in d.Notes) sb.AppendLine("Note: " + n); }
        sb.AppendLine();
        sb.AppendLine("Snapshot and extended-data layouts are module-specific; values are shown raw.");
        return sb.ToString();
    }
}

/// <summary>Phase 1: OBD-II pending (07), permanent (0A) and freeze frame (02) reads. All read-only.</summary>
public static class ObdDtcExtras
{
    /// <summary>CAN reply for 03 / 07 / 0A: [service+0x40] [count] then 2-byte DTCs.</summary>
    public static IEnumerable<string> ParseDtcList(byte service, byte[] p)
    {
        if (p.Length < 2 || p[0] != (byte)(service + 0x40)) yield break;
        for (int i = 2; i + 1 < p.Length; i += 2)
        {
            ushort v = (ushort)(p[i] << 8 | p[i + 1]);
            if (v != 0) yield return DtcFormat.FromTwoBytes(v);
        }
    }

    public static async Task<List<(uint Ecu, string Code)>> ReadListAsync(IDiagChannel ch, byte service, CancellationToken ct = default)
    {
        if (service is not (0x03 or 0x07 or 0x0A)) throw new ArgumentOutOfRangeException(nameof(service));
        await ch.SetTargetAsync(ObdService.Functional11, null, ct);
        var list = new List<(uint, string)>();
        foreach (var r in await ch.RequestAsync(new[] { service }, ct: ct))
            list.AddRange(ParseDtcList(service, r.Payload).Select(c => (r.SourceId, c)));
        return list;
    }

    public sealed record FreezeFrameValue(uint Ecu, byte Pid, string Name, string Value);

    /// <summary>
    /// Mode 02 frame 0: PID 02 (the DTC that stored the frame), then every Mode 01 PID this app can decode that the ECU reports
    /// as supported in the frame (02 00 / 20 / 40 bitmaps).
    /// </summary>
    public static async Task<List<FreezeFrameValue>> ReadFreezeFrameAsync(IDiagChannel ch, CancellationToken ct = default)
    {
        await ch.SetTargetAsync(ObdService.Functional11, null, ct);
        var result = new List<FreezeFrameValue>();
        var supported = new Dictionary<uint, HashSet<byte>>();
        foreach (byte b in new byte[] { 0x00, 0x20, 0x40 })
        {
            foreach (var r in await ch.RequestAsync(new byte[] { 0x02, b, 0x00 }, ct: ct))
            {
                var p = r.Payload;
                if (p.Length < 7 || p[0] != 0x42 || p[1] != b) continue;
                if (!supported.TryGetValue(r.SourceId, out var set)) supported[r.SourceId] = set = new();
                foreach (var pid in J1979.ParseSupported(b, p[3..7])) set.Add(pid);
            }
        }
        foreach (var (ecu, pids) in supported.OrderBy(k => k.Key))
        {
            foreach (var pid in pids.Where(p => p == 0x02 || J1979.Get(p) is not null).OrderBy(p => p))
            {
                var r = (await ch.RequestAsync(new byte[] { 0x02, pid, 0x00 }, ct: ct))
                    .FirstOrDefault(x => x.SourceId == ecu && x.Payload.Length >= 4 && x.Payload[0] == 0x42 && x.Payload[1] == pid);
                if (r is null) continue;
                var data = r.Payload[3..];
                if (pid == 0x02)
                {
                    var v = data.Length >= 2 ? (ushort)(data[0] << 8 | data[1]) : (ushort)0;
                    result.Add(new(ecu, pid, "DTC that stored the frame", v == 0 ? "none (no freeze frame)" : DtcFormat.FromTwoBytes(v)));
                    continue;
                }
                var def = J1979.Get(pid)!;
                try { result.Add(new(ecu, pid, def.Name, $"{def.Decode(data):0.##} {def.Unit}".Trim())); }
                catch (ArgumentException) { result.Add(new(ecu, pid, def.Name, "raw " + Convert.ToHexString(data))); }
            }
        }
        return result;
    }
}
