using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ObdLink.Core.AsBuilt;

/// <summary>
/// Ford "motorcraftservice.com" As-Built download (.ab). It is XML even though it has no declaration. Structure observed in a real file (Dale's 2017 F-150):
/// AS_BUILT_DATA/VEHICLE/{VIN, VEHICLE_DATA/DATA, PCM_MODULE/DATA[LABEL="PCM n"] (x2, identical), BCE_MODULE/DATA[LABEL="MMM-BB-LL"]/CODE x3,
/// NODEID (text = module id, children F1xx = DID values: part numbers/serials), ERROR/{ERRORCODE,ERRORMSG}}.
/// The format is not publicly documented by Ford; everything here was reverse-read from that one file.
/// </summary>
public sealed class FordAbFile
{
    public string Vin { get; set; } = "";
    public List<AsBuiltLine> Lines { get; } = new();
    /// <summary>PCM rows are labelled "PCM n" with no module address.</summary>
    public List<(string Label, byte[] Bytes)> PcmLines { get; } = new();
    public byte[] VehicleData { get; set; } = Array.Empty<byte>();
    /// <summary>Module id -> DID (e.g. "F188") -> value. Includes modules that have no As-Built lines (e.g. PCM 7E0).</summary>
    public SortedDictionary<ushort, SortedDictionary<string, string>> Nodes { get; } = new();
    public List<(string Code, string Message)> Errors { get; } = new();

    public IEnumerable<ushort> AsBuiltModules => Lines.Select(l => l.Address.Module).Distinct().OrderBy(x => x);
    public IEnumerable<ushort> AllModules => AsBuiltModules.Union(Nodes.Keys).OrderBy(x => x);

    /// <summary>
    /// [UNVERIFIED, empirical] PCM line checksum. In Dale's file every "PCM n" row satisfies last = (sum(data) + 0x10 + n) mod 256.
    /// That is consistent with the BCE formula using a pseudo-address of 0x00-0x10-n. This holds for 9 of 9 rows, but it comes from one file only.
    /// </summary>
    public static bool PcmChecksumValid(string label, byte[] b)
    {
        var m = Regex.Match(label, @"PCM\s*(\d+)"); if (!m.Success || b.Length < 2) return false;
        int sum = 0x10 + int.Parse(m.Groups[1].Value); for (int i = 0; i < b.Length - 1; i++) sum += b[i];
        return (byte)sum == b[^1];
    }

    static byte[] Codes(XElement data) => Convert.FromHexString(string.Concat(data.Elements("CODE").Select(c => c.Value.Trim())));

    public static FordAbFile Parse(string text)
    {
        var x = XDocument.Parse(text);
        var v = x.Root?.Element("VEHICLE") ?? throw new FormatException("Not a Ford .ab file: no AS_BUILT_DATA/VEHICLE");
        var f = new FordAbFile { Vin = v.Element("VIN")?.Value.Trim() ?? "" };
        var vd = v.Element("VEHICLE_DATA")?.Element("DATA"); if (vd != null) f.VehicleData = Codes(vd);
        var pcm = v.Elements("PCM_MODULE").FirstOrDefault();
        if (pcm != null) foreach (var d in pcm.Elements("DATA")) f.PcmLines.Add(((string?)d.Attribute("LABEL") ?? "", Codes(d)));
        foreach (var d in v.Elements("BCE_MODULE").Elements("DATA"))
        {
            var b = Codes(d);
            if (b.Length < 2) throw new FormatException($"Short line {(string?)d.Attribute("LABEL")}");
            f.Lines.Add(new AsBuiltLine(AsBuiltAddress.Parse((string?)d.Attribute("LABEL") ?? ""), b[..^1], b[^1]));
        }
        foreach (var n in v.Elements("NODEID"))
        {
            var id = Convert.ToUInt16(string.Concat(n.Nodes().OfType<XText>().Select(t => t.Value)).Trim(), 16);
            var dict = f.Nodes.TryGetValue(id, out var e) ? e : f.Nodes[id] = new();
            foreach (var c in n.Elements()) dict[c.Name.LocalName] = c.Value;
        }
        foreach (var e in v.Elements("ERROR")) f.Errors.Add(((string?)e.Element("ERRORCODE") ?? "", (string?)e.Element("ERRORMSG") ?? ""));
        return f;
    }

    public static FordAbFile Load(string path) => Parse(File.ReadAllText(path));

    static XElement Data(string label, byte[] b)
    {
        var hex = Convert.ToHexString(b); var el = new XElement("DATA", new XAttribute("LABEL", label));
        for (int i = 0; i < 12; i += 4) el.Add(new XElement("CODE", i < hex.Length ? hex.Substring(i, Math.Min(4, hex.Length - i)) : ""));
        return el;
    }

    /// <summary>Serialises back to the same layout. PCM_MODULE is written twice, as in the original. The output is compact like the original, so the CODE grouping round-trips.</summary>
    public string ToXml()
    {
        XElement Pcm() => new("PCM_MODULE", PcmLines.Select(p => Data(p.Label, p.Bytes)));
        var v = new XElement("VEHICLE", new XElement("VIN", Vin), new XElement("VEHICLE_DATA", Data("", VehicleData)), Pcm(),
            new XElement("BCE_MODULE", Lines.Select(l => Data(l.Address.ToString(), l.Data.Append(l.Checksum).ToArray()))), Pcm());
        foreach (var (id, d) in Nodes) { var n = new XElement("NODEID", id.ToString("X3")); foreach (var (k, val) in d) n.Add(new XElement(k, val)); v.Add(n); }
        foreach (var (c, m) in Errors) v.Add(new XElement("ERROR", new XElement("ERRORCODE", c), new XElement("ERRORMSG", m)));
        return new XElement("AS_BUILT_DATA", v).ToString(SaveOptions.DisableFormatting);
    }

    public IEnumerable<byte> Blocks(ushort module) => Lines.Where(l => l.Address.Module == module).Select(l => l.Address.Block).Distinct().OrderBy(b => b);

    /// <summary>
    /// Splits raw block data from a module read using this file's line lengths for that block. Factory lines vary in length (1 to 5 data bytes), so a fixed 5-byte split would misalign the comparison.
    /// Bytes beyond the factory layout are returned as extra 5-byte lines.
    /// </summary>
    public List<AsBuiltLine> SplitLike(ushort module, byte block, byte[] data)
    {
        var res = new List<AsBuiltLine>(); int o = 0; byte n = 1;
        foreach (var l in Lines.Where(l => l.Address.Module == module && l.Address.Block == block).OrderBy(l => l.Address.Line))
        { if (o >= data.Length) break; var len = Math.Min(l.Data.Length, data.Length - o); res.Add(AsBuiltLine.Create(l.Address, data[o..(o + len)])); o += len; n = (byte)(l.Address.Line + 1); }
        for (; o < data.Length; o += 5, n++) res.Add(AsBuiltLine.Create(new AsBuiltAddress(module, block, n), data[o..Math.Min(data.Length, o + 5)]));
        return res;
    }

    /// <summary>Default DIDs treated as serials or identifiers when redacting: ECU serial (F18C), F1D0 (looks like a MAC), F141.</summary>
    public static readonly string[] SerialDids = { "F18C", "F1D0", "F141" };

    /// <summary>
    /// Copy with the VIN reduced to its last <paramref name="keep"/> characters (X-padded). The ASCII copies of the VIN stored inside module blocks (e.g. ABS/RCM/FCIM) are redacted the same way, and checksums are recomputed so the redacted file stays valid. Serial DIDs are replaced.
    /// </summary>
    public FordAbFile Redacted(int keep = 6)
    {
        var red = Vin.Length > keep ? new string('X', Vin.Length - keep) + Vin[^keep..] : Vin;
        var r = new FordAbFile { Vin = red, VehicleData = VehicleData };
        r.PcmLines.AddRange(PcmLines); r.Errors.AddRange(Errors);
        foreach (var (id, d) in Nodes) r.Nodes[id] = new(d.ToDictionary(kv => kv.Key, kv => SerialDids.Contains(kv.Key) ? "REDACTED" : kv.Value));
        var from = Encoding.ASCII.GetBytes(Vin); var to = Encoding.ASCII.GetBytes(red);
        foreach (var g in Lines.GroupBy(l => (l.Address.Module, l.Address.Block)))
        {
            var ls = g.OrderBy(l => l.Address.Line).ToList(); var all = ls.SelectMany(l => l.Data).ToArray();
            if (from.Length > 0) for (int i = 0; i + from.Length <= all.Length; i++) if (all.AsSpan(i, from.Length).SequenceEqual(from)) to.CopyTo(all, i);
            int o = 0; foreach (var l in ls) { r.Lines.Add(AsBuiltLine.Create(l.Address, all[o..(o + l.Data.Length)])); o += l.Data.Length; }
        }
        return r;
    }
}

public enum DiffKind { Same, Changed, MissingInCurrent, NotInFactory }
public sealed record AsBuiltDiff(AsBuiltAddress Address, string? Factory, string? Current, DiffKind Kind)
{
    /// <summary>Compares the lines read from a module with the factory .ab baseline. Lines are matched by address and data bytes are compared; the checksum follows from the data. Pass a module to limit the comparison to that module.</summary>
    public static List<AsBuiltDiff> Compare(IEnumerable<AsBuiltLine> factory, IEnumerable<AsBuiltLine> current, ushort? module = null)
    {
        var f = factory.Where(l => module is null || l.Address.Module == module).ToDictionary(l => l.Address);
        var c = current.Where(l => module is null || l.Address.Module == module).ToDictionary(l => l.Address);
        static string H(AsBuiltLine l) => l.Format()[10..];
        return f.Keys.Union(c.Keys).OrderBy(a => a.Module).ThenBy(a => a.Block).ThenBy(a => a.Line).Select(a =>
            f.TryGetValue(a, out var fl) ? c.TryGetValue(a, out var cl)
                ? new AsBuiltDiff(a, H(fl), H(cl), fl.Data.AsSpan().SequenceEqual(cl.Data) ? DiffKind.Same : DiffKind.Changed)
                : new AsBuiltDiff(a, H(fl), null, DiffKind.MissingInCurrent)
            : new AsBuiltDiff(a, null, H(c[a]), DiffKind.NotInFactory)).ToList();
    }
}

/// <summary>One community-db feature decoded from a line value. CommunityReported: these meanings are not from Ford.</summary>
public sealed record DecodedFeature(string Address, string Tab, string Pattern, string Digits, string Description, string? Meaning);

public static class FeatureDecoder
{
    /// <summary>Decodes against community-db features.json entries {sheet|tab, address, pattern[], description, values[]}. Each '*' in the pattern selects a hex digit of the data. Positions that fall on the checksum byte are skipped.</summary>
    public static List<DecodedFeature> Decode(IEnumerable<AsBuiltLine> lines, string featuresJson, Func<string, string, bool>? tabFilter = null)
    {
        var byAddr = lines.ToDictionary(l => l.Address.ToString());
        var res = new List<DecodedFeature>(); var seen = new HashSet<string>();
        foreach (var e in JsonDocument.Parse(featuresJson).RootElement.GetProperty("entries").EnumerateArray())
        {
            var addr = e.GetProperty("address").GetString()!;
            var tab = (e.TryGetProperty("sheet", out var s) ? s : e.GetProperty("tab")).GetString()!;
            if (!byAddr.TryGetValue(addr, out var line) || (tabFilter != null && !tabFilter(addr, tab))) continue;
            var pat = string.Concat(e.GetProperty("pattern").EnumerateArray().Select(p => p.GetString()));
            var hex = Convert.ToHexString(line.Data);
            var pos = Enumerable.Range(0, pat.Length).Where(i => pat[i] == '*').ToList();
            if (pos.Count == 0 || pos.Max() >= hex.Length) continue;
            var digits = string.Concat(pos.Select(i => hex[i]));
            var desc = e.GetProperty("description").GetString() ?? "";
            if (!seen.Add($"{addr}|{tab}|{pat}|{desc}")) continue;
            string? meaning = null;
            var vre = new Regex($@"^\s*{Regex.Escape(digits)}\s*=\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (var v in e.GetProperty("values").EnumerateArray())
            { var m = vre.Match(v.GetString() ?? ""); if (m.Success) { meaning = m.Groups[1].Value.Trim(); break; } }
            if (meaning is null && desc.Contains(':'))
                meaning = desc[(desc.IndexOf(':') + 1)..].Split(',').Select(t => vre.Match(t)).FirstOrDefault(m => m.Success)?.Groups[1].Value.Trim();
            res.Add(new DecodedFeature(addr, tab, pat, digits, desc, meaning));
        }
        return res;
    }
}
