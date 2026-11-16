using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ObdLink.Core.Flash;

/// <summary>One contiguous block of module memory to download (RequestDownload + TransferData + RequestTransferExit).</summary>
public sealed record FlashSegment(uint Address, byte[] Data)
{
    public uint Length => (uint)Data.Length;
}

/// <summary>A memory range the bootloader erases before download (erase routine option record).</summary>
public readonly record struct EraseRegion(uint Address, uint Length);

/// <summary>
/// A flash file in memory: the segments to download, the regions to erase, and identifying header fields. The file bytes are
/// hashed (SHA-256) so the journal can tell whether a recovery is resuming the same image.
/// </summary>
public sealed class FlashImage
{
    public required string Name { get; init; }
    public required IReadOnlyList<FlashSegment> Segments { get; init; }
    public IReadOnlyList<EraseRegion> Erase { get; init; } = Array.Empty<EraseRegion>();
    /// <summary>Software part number from the file header (VBF <c>sw_part_number</c>), when present.</summary>
    public string? SoftwarePartNumber { get; init; }
    /// <summary>Module request CAN ID from the file header (VBF <c>ecu_address</c>), when present.</summary>
    public uint? EcuTxId { get; init; }
    /// <summary>dataFormatIdentifier for RequestDownload (VBF <c>data_format_identifier</c>; 0x00 = uncompressed, unencrypted).</summary>
    public byte DataFormat { get; init; }
    public required string Sha256 { get; init; }
    /// <summary>VBF <c>sw_part_type</c>: SBL (secondary bootloader, loaded to RAM), EXE (strategy), DATA (calibration), ... Null when absent.</summary>
    public string? PartType { get; init; }
    /// <summary>VBF <c>call</c>: where the bootloader starts an SBL (the address sent with routine 0x0301). Null when absent.</summary>
    public uint? CallAddress { get; init; }
    /// <summary>True for a secondary bootloader file (<c>sw_part_type = SBL</c>).</summary>
    public bool IsSbl => string.Equals(PartType, "SBL", StringComparison.OrdinalIgnoreCase);
    /// <summary>Checks that could not be confirmed (e.g. a file checksum whose algorithm is not known). Shown to the operator; not fatal.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    public long TotalBytes => Segments.Sum(s => (long)s.Data.Length);

    /// <summary>Builds an image from raw segments (tests, DEMO, and callers that parsed another format).</summary>
    public static FlashImage FromSegments(string name, IReadOnlyList<FlashSegment> segments, IReadOnlyList<EraseRegion>? erase = null,
        string? partNumber = null, uint? ecuTxId = null, byte dataFormat = 0)
    {
        if (segments.Count == 0) throw new FlashImageException("The image has no data segments");
        if (segments.Any(s => s.Data.Length == 0)) throw new FlashImageException("The image has an empty segment");
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> hdr = stackalloc byte[8];
        foreach (var s in segments)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(hdr, s.Address);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(hdr[4..], s.Length);
            sha.AppendData(hdr); sha.AppendData(s.Data);
        }
        return new FlashImage
        {
            Name = name, Segments = segments, Erase = erase ?? Array.Empty<EraseRegion>(), SoftwarePartNumber = partNumber,
            EcuTxId = ecuTxId, DataFormat = dataFormat, Sha256 = Convert.ToHexString(sha.GetHashAndReset()),
        };
    }
}

public sealed class FlashImageException(string message) : Exception(message);

/// <summary>One VBF block as found in the file: the stored checksum, the CRC-16/CCITT-FALSE this reader expects, and which other common checksum matches when that one does not.</summary>
public sealed record VbfBlockInfo(uint Address, uint Length, ushort StoredChecksum, ushort ExpectedCcitt, bool Matches, string? AlternativeMatch);

public sealed record VbfInspection(IReadOnlyDictionary<string, string> Header, IReadOnlyList<VbfBlockInfo> Blocks, string? Error)
{
    /// <summary>True when <see cref="VbfReader.Parse"/> would accept the file's blocks.</summary>
    public bool ReaderAccepts => Error is null && Blocks.Count > 0 && Blocks.All(b => b.Matches);
}

/// <summary>
/// Reader for VBF ("Versatile Binary Format") flash files: an ASCII header <c>vbf_version = x.y; header { key = value; ... }</c>
/// followed by binary blocks of <c>[start address u32 BE][length u32 BE][data][checksum u16 BE]</c>.
/// <para>
/// [UNVERIFIED against a real Ford VBF] The layout and the per-block checksum (CRC-16/CCITT-FALSE: poly 0x1021, init 0xFFFF)
/// come from community descriptions of the format, not from Ford documentation, and no real file has been parsed by this
/// code yet. A block checksum mismatch rejects the file (fail safe). The header <c>file_checksum</c> (CRC-32) is checked
/// against two plausible coverages; if neither matches it is reported as a warning, not an error, because its exact coverage
/// is not confirmed.
/// </para>
/// </summary>
public static class VbfReader
{
    public static FlashImage Read(string path) => Parse(File.ReadAllBytes(path), Path.GetFileName(path));

    public static FlashImage Parse(byte[] file, string name = "image.vbf")
    {
        int headerEnd = FindHeaderEnd(file);
        var header = Encoding.ASCII.GetString(file, 0, headerEnd);
        var fields = ParseHeader(header);
        var segments = new List<FlashSegment>();
        int pos = headerEnd;
        while (pos < file.Length)
        {
            if (file.Length - pos < 10) throw new FlashImageException($"Truncated VBF block header at byte {pos} ({file.Length - pos} bytes left)");
            uint addr = ReadU32(file, pos), len = ReadU32(file, pos + 4);
            pos += 8;
            if (len == 0) throw new FlashImageException($"VBF block at 0x{addr:X8} has length 0");
            if ((long)pos + len + 2 > file.Length) throw new FlashImageException($"VBF block at 0x{addr:X8} claims {len} bytes but the file ends first");
            var data = file.AsSpan(pos, (int)len).ToArray();
            pos += (int)len;
            ushort stored = (ushort)(file[pos] << 8 | file[pos + 1]);
            pos += 2;
            ushort calc = Crc16Ccitt(data);
            if (stored != calc)
                throw new FlashImageException($"VBF block at 0x{addr:X8}: checksum 0x{stored:X4} does not match the computed CRC-16/CCITT 0x{calc:X4}. The file is damaged, or this reader's checksum assumption is wrong for this file - do not flash it.");
            segments.Add(new FlashSegment(addr, data));
        }
        if (segments.Count == 0) throw new FlashImageException("The VBF file has a header but no data blocks");

        var warnings = new List<string>();
        if (fields.TryGetValue("file_checksum", out var fc) && TryParseUInt(fc, out uint expected))
        {
            var dataOnly = Crc32(segments.SelectMany(s => s.Data));
            var whole = Crc32(file.Skip(headerEnd));
            if (expected != dataOnly && expected != whole)
                warnings.Add($"file_checksum 0x{expected:X8} could not be confirmed (computed 0x{dataOnly:X8} over block data, 0x{whole:X8} over the binary section); block checksums all passed");
        }
        else warnings.Add("The header has no file_checksum; only per-block checksums were checked");

        uint? ecu = fields.TryGetValue("ecu_address", out var ea) && TryParseUInt(ea, out uint e) ? e : null;
        byte fmt = fields.TryGetValue("data_format_identifier", out var df) && TryParseUInt(df, out uint d) ? (byte)d : (byte)0;
        string? part = fields.TryGetValue("sw_part_number", out var pn) ? pn.Trim('"') : null;
        var erase = fields.TryGetValue("erase", out var er) ? ParseErase(er) : new List<EraseRegion>();
        string? type = fields.TryGetValue("sw_part_type", out var pt) ? pt.Trim('"', ' ') : null;
        uint? call = fields.TryGetValue("call", out var cl) && TryParseUInt(cl, out uint c) ? c : null;
        var img = FlashImage.FromSegments(name, segments, erase, part, ecu, fmt);
        return new FlashImage
        {
            Name = img.Name, Segments = img.Segments, Erase = img.Erase, SoftwarePartNumber = img.SoftwarePartNumber, EcuTxId = img.EcuTxId,
            DataFormat = img.DataFormat, Sha256 = Convert.ToHexString(SHA256.HashData(file)), Warnings = warnings, PartType = type, CallAddress = call,
        };
    }

    /// <summary>
    /// Reads a VBF without refusing it: header fields, and for each block the stored checksum next to the CRC-16/CCITT-FALSE this
    /// reader expects and the first of several other common 16-bit checksums that matches. Used by <c>obdcli vbf-info</c> to check
    /// this reader's assumptions against real files before anything is flashed. <see cref="VbfInspection.Error"/> is set when the
    /// file structure itself is broken.
    /// </summary>
    public static VbfInspection Inspect(byte[] file)
    {
        int headerEnd;
        try { headerEnd = FindHeaderEnd(file); }
        catch (FlashImageException ex) { return new VbfInspection(new Dictionary<string, string>(), Array.Empty<VbfBlockInfo>(), ex.Message); }
        var fields = ParseHeader(Encoding.ASCII.GetString(file, 0, headerEnd));
        var blocks = new List<VbfBlockInfo>();
        int pos = headerEnd;
        string? error = null;
        while (pos < file.Length)
        {
            if (file.Length - pos < 10) { error = $"Truncated block header at byte {pos}"; break; }
            uint addr = ReadU32(file, pos), len = ReadU32(file, pos + 4);
            if (len == 0 || (long)pos + 8 + len + 2 > file.Length) { error = $"Block at 0x{addr:X8} claims {len} bytes but the file ends first (byte {pos})"; break; }
            var data = file.AsSpan(pos + 8, (int)len).ToArray();
            int c = pos + 8 + (int)len;
            ushort stored = (ushort)(file[c] << 8 | file[c + 1]);
            ushort ccitt = Crc16Ccitt(data);
            string? alt = stored == ccitt ? null : Alternatives.FirstOrDefault(a => a.Fn(data) == stored).Name;
            blocks.Add(new VbfBlockInfo(addr, len, stored, ccitt, stored == ccitt, alt));
            pos = c + 2;
        }
        return new VbfInspection(fields, blocks, error);
    }

    private static readonly (string Name, Func<byte[], ushort> Fn)[] Alternatives =
    {
        ("CRC-16/XMODEM (init 0x0000)", d => Crc16(d, 0x1021, 0x0000, false)),
        ("CRC-16/ARC (reflected, init 0x0000)", d => Crc16(d, 0x8005, 0x0000, true)),
        ("CRC-16/MODBUS (reflected, init 0xFFFF)", d => Crc16(d, 0x8005, 0xFFFF, true)),
        ("CRC-16/KERMIT (reflected CCITT, init 0x0000)", d => Crc16(d, 0x1021, 0x0000, true)),
        ("byte sum mod 65536", d => (ushort)d.Sum(b => (long)b)),
    };

    /// <summary>Generic CRC-16 (no final XOR). Reflected variants use the bit-reversed polynomial internally.</summary>
    private static ushort Crc16(ReadOnlySpan<byte> data, ushort poly, ushort init, bool reflected)
    {
        if (!reflected)
        {
            ushort crc = init;
            foreach (var b in data) { crc ^= (ushort)(b << 8); for (int i = 0; i < 8; i++) crc = (crc & 0x8000) != 0 ? (ushort)(crc << 1 ^ poly) : (ushort)(crc << 1); }
            return crc;
        }
        ushort rp = 0;
        for (int i = 0; i < 16; i++) if ((poly & (1 << i)) != 0) rp |= (ushort)(1 << (15 - i));
        ushort r = init;
        foreach (var b in data) { r ^= b; for (int i = 0; i < 8; i++) r = (r & 1) != 0 ? (ushort)(r >> 1 ^ rp) : (ushort)(r >> 1); }
        return r;
    }

    /// <summary>Writes a VBF in the layout <see cref="Parse"/> reads. For tests and DEMO images only.</summary>
    public static byte[] Write(IReadOnlyList<FlashSegment> segments, string? partNumber = null, uint? ecuTxId = null, IReadOnlyList<EraseRegion>? erase = null,
        string? partType = null, uint? call = null)
    {
        var bin = new MemoryStream();
        foreach (var s in segments)
        {
            bin.Write(U32(s.Address)); bin.Write(U32(s.Length)); bin.Write(s.Data);
            ushort c = Crc16Ccitt(s.Data); bin.WriteByte((byte)(c >> 8)); bin.WriteByte((byte)c);
        }
        var sb = new StringBuilder("vbf_version = 2.2;\r\nheader {\r\n    // synthetic test image\r\n");
        if (partNumber is not null) sb.Append($"    sw_part_number = \"{partNumber}\";\r\n");
        if (partType is not null) sb.Append($"    sw_part_type = {partType};\r\n");
        if (call is uint ca) sb.Append($"    call = 0x{ca:X8};\r\n");
        if (ecuTxId is uint id) sb.Append($"    ecu_address = 0x{id:X3};\r\n");
        if (erase is { Count: > 0 }) sb.Append("    erase = { ").Append(string.Join(", ", erase.Select(r => $"{{ 0x{r.Address:X8}, 0x{r.Length:X8} }}"))).Append(" };\r\n");
        sb.Append($"    file_checksum = 0x{Crc32(segments.SelectMany(s => s.Data)):X8};\r\n}}");
        return Encoding.ASCII.GetBytes(sb.ToString()).Concat(bin.ToArray()).ToArray();
    }

    /// <summary>Index just after the closing brace of <c>header { ... }</c>, honoring nesting, strings and comments.</summary>
    private static int FindHeaderEnd(byte[] f)
    {
        int i = IndexOf(f, "header"u8);
        if (i < 0) throw new FlashImageException("Not a VBF file: no 'header' section");
        int depth = 0; bool started = false, inStr = false;
        for (; i < f.Length; i++)
        {
            byte c = f[i];
            if (inStr) { if (c == '"') inStr = false; continue; }
            if (c == '"') { inStr = true; continue; }
            if (c == '/' && i + 1 < f.Length && f[i + 1] == '/') { while (i < f.Length && f[i] != '\n') i++; continue; }
            if (c == '/' && i + 1 < f.Length && f[i + 1] == '*') { i += 2; while (i + 1 < f.Length && !(f[i] == '*' && f[i + 1] == '/')) i++; i++; continue; }
            if (c == '{') { depth++; started = true; }
            else if (c == '}' && --depth == 0 && started) return i + 1;
            if (c > 0x7E && !started) throw new FlashImageException("Not a VBF file: binary data before the header");
        }
        throw new FlashImageException("Not a VBF file: the header section is not closed");
    }

    private static Dictionary<string, string> ParseHeader(string h)
    {
        h = Regex.Replace(h, @"/\*.*?\*/", "", RegexOptions.Singleline);
        h = Regex.Replace(h, @"//[^\n]*", "");
        int open = h.IndexOf('{');
        var body = open >= 0 ? h[(open + 1)..h.LastIndexOf('}')] : h;
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(body, @"(\w+)\s*=\s*((?:\{(?:[^{}]|\{[^{}]*\})*\})|""[^""]*""|[^;]+)\s*;"))
            dict[m.Groups[1].Value] = m.Groups[2].Value.Trim();
        return dict;
    }

    private static List<EraseRegion> ParseErase(string v)
    {
        var list = new List<EraseRegion>();
        foreach (Match m in Regex.Matches(v, @"\{\s*(0x[0-9A-Fa-f]+|\d+)\s*,\s*(0x[0-9A-Fa-f]+|\d+)\s*\}"))
            if (TryParseUInt(m.Groups[1].Value, out var a) && TryParseUInt(m.Groups[2].Value, out var l)) list.Add(new EraseRegion(a, l));
        return list;
    }

    private static bool TryParseUInt(string s, out uint v)
    {
        s = s.Trim().Trim('"');
        return s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? uint.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)
            : uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
    }

    private static int IndexOf(byte[] f, ReadOnlySpan<byte> needle) => f.AsSpan().IndexOf(needle);
    private static uint ReadU32(byte[] f, int p) => (uint)(f[p] << 24 | f[p + 1] << 16 | f[p + 2] << 8 | f[p + 3]);
    private static byte[] U32(uint v) => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };

    /// <summary>CRC-16/CCITT-FALSE: poly 0x1021, init 0xFFFF, no reflection, no final XOR. Check value for "123456789" is 0x29B1.</summary>
    public static ushort Crc16Ccitt(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++) crc = (crc & 0x8000) != 0 ? (ushort)(crc << 1 ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }

    /// <summary>CRC-32 (IEEE 802.3, reflected, poly 0xEDB88320). Check value for "123456789" is 0xCBF43926.</summary>
    public static uint Crc32(IEnumerable<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++) crc = (crc & 1) != 0 ? crc >> 1 ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
