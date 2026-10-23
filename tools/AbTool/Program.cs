using ObdLink.Core.AsBuilt;
// Usage: AbTool redact <in.ab> <out.ab> | AbTool summary <in.ab> | AbTool decode <in.ab> <features.json>
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
