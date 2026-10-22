using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ObdLink.Core.AsBuilt;

/// <summary>Ford As-Built address "MMM-BB-LL": module request id, block, line.</summary>
public readonly record struct AsBuiltAddress(ushort Module, byte Block, byte Line)
{
    public override string ToString() => $"{Module:X3}-{Block:X2}-{Line:X2}";
    public static AsBuiltAddress Parse(string s)
    {
        var p = s.Trim().Split('-');
        if (p.Length != 3) throw new FormatException($"Bad As-Built address '{s}'");
        return new(ushort.Parse(p[0], NumberStyles.HexNumber), byte.Parse(p[1], NumberStyles.HexNumber), byte.Parse(p[2], NumberStyles.HexNumber));
    }
}

/// <summary>
/// One As-Built line: data bytes + trailing 1-byte checksum.
/// Checksum (community-documented, NOT an official Ford spec): low 8 bits of the sum of the
/// address bytes [Module>>8, Module&amp;0xFF, Block, Line] plus all data bytes.
/// Sources: FORScan forum t=2012 ("760-01-04 3733 D6": 07+60+01+04+37+33=D6),
/// f150forum FORScan thread p.13 ("7D0-01-01 2A2A 0502 2054"), mk4-wiki.denkdose.de "Aufbau der ASBUILT Datei" (note: that page's 770-01-03 example adds line byte 01 instead of 03, so it is internally inconsistent and is not used as a test vector).
/// </summary>
public sealed record AsBuiltLine(AsBuiltAddress Address, byte[] Data, byte Checksum)
{
    public bool ChecksumValid => Checksum == ComputeChecksum(Address, Data);

    public static byte ComputeChecksum(AsBuiltAddress a, ReadOnlySpan<byte> data)
    {
        int sum = (a.Module >> 8) + (a.Module & 0xFF) + a.Block + a.Line;
        foreach (var b in data) sum += b;
        return (byte)sum;
    }

    public static AsBuiltLine Create(AsBuiltAddress a, byte[] data) => new(a, data, ComputeChecksum(a, data));

    /// <summary>Parses "726-01-01 xxxx xxxx xxCC" (FORScan-style display; groups of 4 hex digits, last byte = checksum).</summary>
    public static AsBuiltLine Parse(string line)
    {
        var m = Regex.Match(line.Trim(), @"^([0-9A-Fa-f]{3}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2})\s*[:=]?\s*([0-9A-Fa-f\s]+)$");
        if (!m.Success) throw new FormatException($"Unrecognised As-Built line '{line}'");
        var hex = Regex.Replace(m.Groups[2].Value, @"\s", "");
        if (hex.Length < 4 || hex.Length % 2 != 0) throw new FormatException("As-Built data must be whole bytes incl. checksum");
        var bytes = Convert.FromHexString(hex);
        return new(AsBuiltAddress.Parse(m.Groups[1].Value), bytes[..^1], bytes[^1]);
    }

    public string Format()
    {
        var hex = Convert.ToHexString(Data.Append(Checksum).ToArray());
        var sb = new StringBuilder(Address.ToString()).Append(' ');
        for (int i = 0; i < hex.Length; i += 4) { if (i > 0) sb.Append(' '); sb.Append(hex.AsSpan(i, Math.Min(4, hex.Length - i))); }
        return sb.ToString();
    }
}

/// <summary>A full As-Built block (all lines) mapped to a UDS DID.</summary>
public sealed class AsBuiltBlock
{
    public ushort Module { get; init; }
    public byte Block { get; init; }
    public byte[] Data { get; init; } = Array.Empty<byte>();

    /// <summary>[UNVERIFIED] Data bytes per display line. Community dumps show 5 data bytes (3 groups incl. checksum) for most modules; final line may be shorter.</summary>
    public int BytesPerLine { get; init; } = 5;

    /// <summary>
    /// [UNVERIFIED] Community convention: block N is read/written via UDS DID 0xDE00 + (N-1)
    /// (so block 01 = DE00). Not from a Ford-published spec; confirm with a capture before writing.
    /// </summary>
    public static ushort DidForBlock(byte block) => (ushort)(0xDE00 + block - 1);
    public ushort Did => DidForBlock(Block);

    public IEnumerable<AsBuiltLine> Lines()
    {
        byte n = 1;
        for (int i = 0; i < Data.Length; i += BytesPerLine, n++)
            yield return AsBuiltLine.Create(new AsBuiltAddress(Module, Block, n), Data[i..Math.Min(Data.Length, i + BytesPerLine)]);
    }

    public static AsBuiltBlock FromLines(IEnumerable<AsBuiltLine> lines)
    {
        var l = lines.OrderBy(x => x.Address.Line).ToList();
        if (l.Count == 0) throw new ArgumentException("no lines");
        var bad = l.FirstOrDefault(x => !x.ChecksumValid);
        if (bad is not null) throw new InvalidDataException($"Checksum mismatch at {bad.Address}");
        if (l.Select(x => (x.Address.Module, x.Address.Block)).Distinct().Count() != 1) throw new ArgumentException("lines span multiple blocks");
        return new AsBuiltBlock { Module = l[0].Address.Module, Block = l[0].Address.Block, Data = l.SelectMany(x => x.Data).ToArray(), BytesPerLine = l[0].Data.Length };
    }

    public static List<AsBuiltBlock> ParseText(string text) =>
        text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#'))
            .Select(AsBuiltLine.Parse).GroupBy(x => (x.Address.Module, x.Address.Block)).Select(FromLines).ToList();
}

public sealed record AsBuiltBackup(string Vin, DateTimeOffset TakenAt, ushort Module, Dictionary<string, string> Blocks)
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public string Serialize() => JsonSerializer.Serialize(this, Json);
    public static AsBuiltBackup Deserialize(string s) => JsonSerializer.Deserialize<AsBuiltBackup>(s, Json)!;
}
