using ObdLink.Core.IsoTp;

namespace ObdLink.Core.Uds;

/// <summary>2A transmission modes (ISO 14229-1). Actual periods are module-defined.</summary>
public enum PeriodicRate : byte { Slow = 0x01, Medium = 0x02, Fast = 0x03 }

/// <summary>
/// 0x2A ReadDataByPeriodicIdentifier. The module answers 6A once and then streams frames of the form [periodic id][data] on its
/// response ID until told to stop. Periodic DIDs are F200-F2FF (the request carries only the low byte), usually defined first with 2C.
/// <para>
/// The ELM/STN text interface cannot consume a stream (each command transmits and returns on the first reply), so starting is
/// refused unless the channel implements <see cref="IDiagReceiveChannel"/>. Stopping is always allowed. Support on Ford modules is
/// [UNVERIFIED].
/// </para>
/// </summary>
public static class UdsPeriodic
{
    public static byte ToPeriodicId(ushort did)
    {
        if (did is < 0xF200 or > 0xF2FF) throw new ArgumentOutOfRangeException(nameof(did), $"0x{did:X4} is not a periodic DID (F200-F2FF)");
        return (byte)did;
    }

    public static async Task StartPeriodicAsync(this UdsClient uds, PeriodicRate rate, IReadOnlyList<ushort> dids, CancellationToken ct = default)
    {
        if (uds.Channel is not IDiagReceiveChannel)
            throw new NotSupportedException("Periodic data (2A) needs a transport that can receive without transmitting; the ELM/STN interface cannot, so nothing was sent.");
        if (dids.Count == 0) throw new ArgumentException("At least one periodic DID is needed", nameof(dids));
        if (dids.Distinct().Count() != dids.Count) throw new ArgumentException("A DID is listed more than once", nameof(dids));
        var req = new List<byte> { 0x2A, (byte)rate };
        req.AddRange(dids.Select(ToPeriodicId));
        await uds.RequestAsync(req.ToArray(), ct);
    }

    /// <summary>2A 04: stop the listed periodic DIDs, or all of them when <paramref name="dids"/> is null or empty.</summary>
    public static async Task StopPeriodicAsync(this UdsClient uds, IReadOnlyList<ushort>? dids = null, CancellationToken ct = default)
    {
        var req = new List<byte> { 0x2A, 0x04 };
        if (dids is not null) req.AddRange(dids.Select(ToPeriodicId));
        await uds.RequestAsync(req.ToArray(), ct);
    }

    /// <summary>
    /// Collects the periodic frames that arrive within <paramref name="timeout"/> from this module. <paramref name="lengths"/> maps each
    /// periodic DID to its data length; frames from other modules or for DIDs not in the map are ignored.
    /// </summary>
    public static async Task<IReadOnlyList<DidValue>> ReceivePeriodicAsync(this UdsClient uds, IReadOnlyDictionary<ushort, int> lengths, TimeSpan timeout, CancellationToken ct = default)
    {
        if (uds.Channel is not IDiagReceiveChannel rx) throw new NotSupportedException("Periodic data (2A) needs a transport that can receive without transmitting.");
        var list = new List<DidValue>();
        foreach (var r in await rx.ReceiveAsync(timeout, ct))
            if (r.SourceId == uds.ResponseId && ParseFrame(r.Payload, lengths) is { } v) list.Add(v);
        return list;
    }

    /// <summary>One periodic frame: [periodic id][data]. Null for a DID not in <paramref name="lengths"/>; throws when the length is wrong.</summary>
    public static DidValue? ParseFrame(byte[] payload, IReadOnlyDictionary<ushort, int> lengths)
    {
        if (payload.Length == 0) return null;
        ushort did = (ushort)(0xF200 | payload[0]);
        if (!lengths.TryGetValue(did, out int len)) return null;
        if (payload.Length - 1 != len)
            throw new InvalidDataException($"Periodic DID 0x{did:X4} frame has {payload.Length - 1} data byte(s), expected {len}: {Convert.ToHexString(payload)}");
        return new DidValue(did, payload[1..]);
    }
}
