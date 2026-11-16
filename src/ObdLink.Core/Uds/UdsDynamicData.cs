namespace ObdLink.Core.Uds;

/// <summary>Part of a source DID copied into a dynamic DID: <paramref name="Position"/> is 1-based, <paramref name="Size"/> in bytes.</summary>
public readonly record struct DidSource(ushort SourceDid, byte Position, byte Size);

/// <summary>
/// 0x2C DynamicallyDefineDataIdentifier: builds a new DID (F200-F3FF) out of parts of other DIDs or memory ranges, so several values
/// can be read with one 22 request or streamed with 2A. Defining a DID changes module state (it is in
/// <see cref="UdsServices.StateChanging"/>), so <see cref="UdsClient.Gate"/> applies. Definitions are normally lost when the module
/// leaves the session or resets. Support on Ford modules is [UNVERIFIED].
/// </summary>
public static class UdsDynamicData
{
    public static bool IsDynamicDid(ushort did) => did is >= 0xF200 and <= 0xF3FF;

    /// <summary>2C 01: define <paramref name="dddi"/> from parts of existing DIDs.</summary>
    public static async Task DefineDynamicDidByIdentifierAsync(this UdsClient uds, ushort dddi, IReadOnlyList<DidSource> sources, CancellationToken ct = default)
    {
        CheckDddi(dddi);
        if (sources.Count == 0) throw new ArgumentException("At least one source DID is needed", nameof(sources));
        if (sources.Any(s => s.Position == 0 || s.Size == 0)) throw new ArgumentOutOfRangeException(nameof(sources), "Position is 1-based and size must be above zero");
        var req = new List<byte> { 0x2C, 0x01, (byte)(dddi >> 8), (byte)dddi };
        foreach (var s in sources) req.AddRange(new[] { (byte)(s.SourceDid >> 8), (byte)s.SourceDid, s.Position, s.Size });
        CheckEcho(await uds.RequestAsync(req.ToArray(), ct), 0x01, dddi);
    }

    /// <summary>2C 02: define <paramref name="dddi"/> from memory ranges (one ALFID for all of them).</summary>
    public static async Task DefineDynamicDidByMemoryAsync(this UdsClient uds, ushort dddi, IReadOnlyList<MemoryBlock> blocks, int addressBytes = 4, int sizeBytes = 2, CancellationToken ct = default)
    {
        CheckDddi(dddi);
        if (blocks.Count == 0) throw new ArgumentException("At least one memory block is needed", nameof(blocks));
        var req = new List<byte> { 0x2C, 0x02, (byte)(dddi >> 8), (byte)dddi, UdsMemory.AddressAndLengthFormat(addressBytes, sizeBytes) };
        foreach (var b in blocks) req.AddRange(UdsMemory.EncodeBlock(b, addressBytes, sizeBytes));
        CheckEcho(await uds.RequestAsync(req.ToArray(), ct), 0x02, dddi);
    }

    /// <summary>2C 03: clear one dynamic DID, or all of them when <paramref name="dddi"/> is null.</summary>
    public static async Task ClearDynamicDidAsync(this UdsClient uds, ushort? dddi = null, CancellationToken ct = default)
    {
        byte[] req;
        if (dddi is ushort d) { CheckDddi(d); req = new byte[] { 0x2C, 0x03, (byte)(d >> 8), (byte)d }; }
        else req = new byte[] { 0x2C, 0x03 };
        var r = await uds.RequestAsync(req, ct);
        if (r.Length < 2 || r[1] != 0x03) throw new InvalidDataException($"Malformed 2C 03 reply: {Convert.ToHexString(r)}");
        if (dddi is ushort e && r.Length >= 4 && (r[2] << 8 | r[3]) != e) throw new InvalidDataException($"2C 03 reply names DID 0x{r[2]:X2}{r[3]:X2}, not 0x{e:X4}");
    }

    private static void CheckDddi(ushort dddi)
    {
        if (!IsDynamicDid(dddi)) throw new ArgumentOutOfRangeException(nameof(dddi), $"0x{dddi:X4} is outside the dynamic DID range F200-F3FF");
    }

    private static void CheckEcho(byte[] r, byte sub, ushort dddi)
    {
        if (r.Length < 4 || r[1] != sub || (r[2] << 8 | r[3]) != dddi)
            throw new InvalidDataException($"2C {sub:X2} reply does not confirm DID 0x{dddi:X4}: {Convert.ToHexString(r)}");
    }
}
