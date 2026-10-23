using System.Text;
using System.Text.RegularExpressions;

namespace ObdLink.Core.AsBuilt;

/// <summary>
/// FORScan .abt As-Built backup (one module per file). The layout was reverse-read from 6 of Dale's own files; FORScan does not document it.
/// <code>
/// ;Block 1                 comment = sequential block INDEX (1..n). It is not the block number.
/// 737G1G13146544558A8      MMM + block(2) + line(2) + data hex + checksum byte
/// </code>
/// Block and line numbers use a 2-char code holding the ORDINAL: first char 'G'+ordinal/16 (G=0, H=1, I=2…), second char = ordinal%16 in hex.
/// Examples: "G9"=9, "GA"=10, "H0"=16, "J4"=52.
/// The address byte used in the checksum (and in .ab labels) is BCD(ordinal), so "GA" gives 0x10. All 337 lines in Dale's 6 files validate this way; plain hex fails on every line ≥ 10.
/// Comments: ";Block 7" precedes "737G8…", so comments are a running count, and the code carries the real block number (RCM has no block 7).
/// </summary>
public sealed class ForscanAbtFile
{
    public List<AsBuiltLine> Lines { get; } = new();
    /// <summary>Comment index ("Block n") each line appeared under. Kept so export round-trips byte-identically.</summary>
    public Dictionary<AsBuiltAddress, int> BlockIndex { get; } = new();
    public IEnumerable<ushort> Modules => Lines.Select(l => l.Address.Module).Distinct();
    public ushort Module => Modules.Single();

    public static byte DecodeCode(string c)
    {
        if (c.Length != 2 || c[0] < 'G' || c[0] > 'V') throw new FormatException($"Bad .abt code '{c}'");
        return AsBuiltAddress.ToBcd(((c[0] - 'G') << 4) | Convert.ToByte(c[1].ToString(), 16));
    }
    public static string EncodeCode(byte bcd) { var n = AsBuiltAddress.FromBcd(bcd); return $"{(char)('G' + (n >> 4))}{n & 0xF:X}"; }

    public static ForscanAbtFile Parse(string text)
    {
        var f = new ForscanAbtFile(); int idx = 0;
        foreach (var raw in text.Split('\n'))
        {
            var l = raw.Trim();
            if (l.Length == 0) continue;
            var c = Regex.Match(l, @"^;\s*Block\s+(\d+)", RegexOptions.IgnoreCase);
            if (c.Success) { idx = int.Parse(c.Groups[1].Value); continue; }
            if (l.StartsWith(';')) continue;
            var m = Regex.Match(l, @"^([0-9A-Fa-f]{3})([G-V][0-9A-F])([G-V][0-9A-F])((?:[0-9A-Fa-f]{2})+)$");
            if (!m.Success) throw new FormatException($"Unrecognised .abt line '{l}'");
            var a = new AsBuiltAddress(Convert.ToUInt16(m.Groups[1].Value, 16), DecodeCode(m.Groups[2].Value), DecodeCode(m.Groups[3].Value));
            var b = Convert.FromHexString(m.Groups[4].Value);
            var line = new AsBuiltLine(a, b[..^1], b[^1]);
            f.Lines.Add(line); f.BlockIndex[a] = idx;
        }
        return f;
    }
    public static ForscanAbtFile Load(string path) => Parse(File.ReadAllText(path));

    /// <summary>Export in FORScan layout (CRLF). Block comments are sequential when no index is known.</summary>
    public static string Format(IEnumerable<AsBuiltLine> lines)
    {
        var sb = new StringBuilder(); byte? last = null; int idx = 0;
        foreach (var l in lines.OrderBy(l => l.Address.Module).ThenBy(l => l.Address.Block).ThenBy(l => l.Address.Line))
        {
            if (l.Address.Block != last) { sb.Append($";Block {++idx}\r\n"); last = l.Address.Block; }
            sb.Append($"{l.Address.Module:X3}{EncodeCode(l.Address.Block)}{EncodeCode(l.Address.Line)}{Convert.ToHexString(l.Data)}{l.Checksum:X2}\r\n");
        }
        return sb.ToString();
    }
    public string ToText() => Format(Lines);

    /// <summary>Replaces an ASCII VIN embedded anywhere in the data, even across blocks, with X's (keeping the last 6) and recomputes checksums.</summary>
    public ForscanAbtFile Redacted(string vin, int keep = 6)
    {
        var masked = Encoding.ASCII.GetBytes(new string('X', vin.Length - keep) + vin[^keep..]);
        var vinB = Encoding.ASCII.GetBytes(vin);
        var r = new ForscanAbtFile();
        // Scan the whole file: the BCM VIN crosses block boundaries (09→10→11).
        {
            var ls = Lines; var data = ls.SelectMany(l => l.Data).ToArray();
            for (int i = 0; i + vinB.Length <= data.Length; i++)
                if (data.AsSpan(i, vinB.Length).SequenceEqual(vinB)) masked.CopyTo(data, i);
            int o = 0;
            foreach (var l in ls) { var nl = AsBuiltLine.Create(l.Address, data[o..(o + l.Data.Length)]); o += l.Data.Length; r.Lines.Add(nl); r.BlockIndex[nl.Address] = BlockIndex[l.Address]; }
        }
        return r;
    }
}
