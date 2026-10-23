using ObdLink.Core.Logs;
using ObdLink.Core.AsBuilt;
// Usage: AbTool redact <in.ab> <out.ab> | AbTool summary <in.ab> | AbTool decode <in.ab> <features.json>
// AbTool redact-profile <in.profile> <out> | AbTool profile-vs-ab <in.profile> <in.ab>
if (args[0] == "redact-abt") { var a = ForscanAbtFile.Load(args[1]); File.WriteAllText(args[3], a.Redacted(args[2]).ToText()); return; }
if (args[0] == "redact-profile") { File.WriteAllText(args[2], ForscanProfile.Redact(File.ReadAllText(args[1]))); return; }
if (args[0] == "profile-vs-ab")
{
    var pr = ForscanProfile.Parse(File.ReadAllText(args[1])); var ab = FordAbFile.Load(args[2]);
    foreach (var (k, id) in pr.MapToAb(ab)) Console.WriteLine($"map {k} -> {id:X3} {pr.Modules.First(m => m.Key == k).PartNumber}");
    foreach (var d in pr.CompareSoftware(ab)) Console.WriteLine($"diff {d.Module:X3} {d.Did} factory={d.Factory} current={d.Current}");
    return;
}
if (args[0] == "abt")
{   // AbTool abt <factory.ab> <features.json> <file.abt>...
    var ab = FordAbFile.Load(args[1]); var feats = File.ReadAllText(args[2]);
    foreach (var path in args.Skip(3))
    {
        var t = ForscanAbtFile.Load(path); var m = t.Module;
        Console.WriteLine($"== {Path.GetFileName(path)} module {m:X3} lines {t.Lines.Count} bad {t.Lines.Count(l => !l.ChecksumValid)} blocks {string.Join(",", t.Lines.Select(l => l.Address.Block.ToString("X2")).Distinct())} lens {string.Join(",", t.Lines.Select(l => l.Data.Length))}");
        var fac = m == 0x7E0 ? new List<AsBuiltLine>() : ab.Lines.Where(l => l.Address.Module == m).ToList();
        Console.WriteLine($"   factory blocks {string.Join(",", fac.Select(l => l.Address.Block.ToString("X2")).Distinct())} lens {string.Join(",", fac.Select(l => l.Data.Length))}");
        var diffs = AsBuiltDiff.Compare(fac, t.Lines, m);
        Console.WriteLine("   " + string.Join(" ", diffs.GroupBy(d => d.Kind).Select(g => $"{g.Key}={g.Count()}")));
        foreach (var d in diffs.Where(d => d.Kind != DiffKind.Same)) Console.WriteLine($"   {d.Kind} {d.Address} F={d.Factory} C={d.Current}");
        var pr = DecodeProfile.DaleF150_2017; var cur = pr.Filter(FeatureDecoder.Decode(t.Lines, feats)).ToList(); var f0 = pr.Filter(FeatureDecoder.Decode(fac, feats)).ToList();
        foreach (var c in cur)
        {
            var o = f0.FirstOrDefault(x => x.Address == c.Address && x.Tab == c.Tab && x.Pattern == c.Pattern);
            if (o != null && o.Digits != c.Digits) Console.WriteLine($"   FEAT {c.Address} [{c.Tab}] {c.Pattern} {o.Digits}->{c.Digits} {c.Description.Replace('\n',' ')} :: {o.Meaning} -> {c.Meaning}");
        }
    }
    return;
}
var f = FordAbFile.Load(args[1]);
switch (args[0])
{
    case "redact": File.WriteAllText(args[2], f.Redacted().ToXml()); break;
    case "summary":
        Console.WriteLine($"lines {f.Lines.Count} bad {f.Lines.Count(l => !l.ChecksumValid)} pcm {f.PcmLines.Count} pcmbad {f.PcmLines.Count(p => !FordAbFile.PcmChecksumValid(p.Label, p.Bytes))}");
        foreach (var m in f.AllModules)
        {
            var ls = f.Lines.Where(l => l.Address.Module == m).ToList();
            f.Nodes.TryGetValue(m, out var n);
            Console.WriteLine($"{m:X3}|{ls.Select(l => l.Address.Block).Distinct().Count()}|{ls.Count}|{string.Join(",", ls.Select(l => l.Data.Length).Distinct())}|{n?.GetValueOrDefault("F188")}|{n?.GetValueOrDefault("F113")}");
        }
        break;
    case "decode":
        foreach (var d in FeatureDecoder.Decode(f.Lines, File.ReadAllText(args[2])))
            Console.WriteLine($"{d.Address}|{d.Tab}|{d.Pattern}|{d.Digits}|{d.Description.Replace('\n', ' ')}|{d.Meaning?.Replace('\n', ' ') ?? "?"}");
        break;
}
