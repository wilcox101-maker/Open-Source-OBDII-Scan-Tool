namespace ObdLink.Core.Uds;

/// <summary>One memory range: start address and size in bytes.</summary>
public readonly record struct MemoryBlock(uint Address, uint Size);

/// <summary>
/// ISO 14229-1 memory addressing. The addressAndLengthFormatIdentifier (ALFID) byte says how many bytes the size (high nibble)
/// and the address (low nibble) take in the request. This client supports 1-4 bytes for each.
/// </summary>
public static class UdsMemory
{
    /// <summary>Largest read that fits one classic ISO-TP message (4095 bytes) after the 0x63 response byte.</summary>
    public const int MaxReadSize = 4094;

    public static byte AddressAndLengthFormat(int addressBytes, int sizeBytes)
    {
        if (addressBytes is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(addressBytes), addressBytes, "Address length must be 1-4 bytes");
        if (sizeBytes is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(sizeBytes), sizeBytes, "Size length must be 1-4 bytes");
        return (byte)(sizeBytes << 4 | addressBytes);
    }

    /// <summary>Address then size, big-endian, each in its configured number of bytes. Throws if either value does not fit.</summary>
    public static byte[] EncodeBlock(MemoryBlock b, int addressBytes, int sizeBytes)
    {
        AddressAndLengthFormat(addressBytes, sizeBytes);
        if (!Fits(b.Address, addressBytes)) throw new ArgumentOutOfRangeException(nameof(b), $"Address 0x{b.Address:X} does not fit in {addressBytes} byte(s)");
        if (b.Size == 0 || !Fits(b.Size, sizeBytes)) throw new ArgumentOutOfRangeException(nameof(b), $"Size {b.Size} must be above zero and fit in {sizeBytes} byte(s)");
        var o = new byte[addressBytes + sizeBytes];
        for (int i = 0; i < addressBytes; i++) o[i] = (byte)(b.Address >> (8 * (addressBytes - 1 - i)));
        for (int i = 0; i < sizeBytes; i++) o[addressBytes + i] = (byte)(b.Size >> (8 * (sizeBytes - 1 - i)));
        return o;
    }

    private static bool Fits(uint v, int bytes) => bytes >= 4 || v < 1u << (8 * bytes);

    /// <summary>
    /// 0x23 ReadMemoryByAddress. Read-only. The reply must carry exactly <paramref name="size"/> bytes. Which addresses a module
    /// exposes (and in which session or security level) is module-specific; expect NRC 0x31 or 0x33 outside them.
    /// </summary>
    public static async Task<byte[]> ReadMemoryByAddressAsync(this UdsClient uds, uint address, uint size, int addressBytes = 4, int sizeBytes = 2, CancellationToken ct = default)
    {
        if (size > MaxReadSize) throw new ArgumentOutOfRangeException(nameof(size), size, $"At most {MaxReadSize} bytes per read");
        var block = EncodeBlock(new MemoryBlock(address, size), addressBytes, sizeBytes);
        var req = new byte[2 + block.Length];
        req[0] = 0x23; req[1] = AddressAndLengthFormat(addressBytes, sizeBytes); block.CopyTo(req, 2);
        var r = await uds.RequestAsync(req, ct);
        if (r.Length - 1 != size)
            throw new InvalidDataException($"ReadMemoryByAddress 0x{address:X} asked for {size} byte(s) but the module returned {r.Length - 1}: {Convert.ToHexString(r)}");
        return r[1..];
    }
}
