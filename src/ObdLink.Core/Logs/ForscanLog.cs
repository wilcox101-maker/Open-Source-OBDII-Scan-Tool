using System.Text.RegularExpressions;

namespace ObdLink.Core.Logs;

public sealed record ForscanEvent(string Level, TimeSpan Time, string Message);
public sealed record ForscanDtc(string Module, string Code, string Symptom, string Status, string Title);
public sealed record ForscanModuleInfo(string Acronym, string Name, Dictionary<string, string> Fields);

/// <summary>
/// Parser for FORScan's human-readable text exports: the event log ("(OK) [hh:mm:ss.fff] ..."),
/// DTC report blocks ("===MOD DTC Cxxxx:ss-SS===") and the configuration/"module info" report.
/// These exports contain NO bus-level data (no CAN IDs, AT/ST commands or raw frames).
/// </summary>
public static class ForscanLogParser
{
    private static readonly Regex Ev = new(@"^\((OK|WARN|ERR)\) \[(\d\d:\d\d:\d\d\.\d{3})\] (.*)$");
    private static readonly Regex DtcHdr = new(@"^===(\w+) DTC ([PCBU][0-9A-F]{4}):([0-9A-F]{2})-([0-9A-F]{2})===$");
    private static readonly Regex Mod = new(@"^(\w+) - (.+)$");

    public static List<ForscanEvent> ParseEvents(string text)
    {
        var list = new List<ForscanEvent>();
        foreach (var l in text.Split('\n'))
        {
            var m = Ev.Match(l.TrimEnd('\r'));
            if (m.Success) list.Add(new(m.Groups[1].Value, TimeSpan.Parse(m.Groups[2].Value), m.Groups[3].Value));
            else if (list.Count > 0 && l.Trim().Length > 0 && !l.StartsWith("===")) // wrapped continuation
                list[^1] = list[^1] with { Message = list[^1].Message + " " + l.Trim() };
        }
        return list;
    }

    public static List<string> FoundModules(string text) =>
        ParseEvents(text).Select(e => Regex.Match(e.Message, @"^Found module:\s+(\w+) - ")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();

    public static string? AdapterId(string text)
    {
        var m = Regex.Match(text, @"Adapter:\s+(.+)$", RegexOptions.Multiline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public static List<ForscanDtc> ParseDtcs(string text)
    {
        var list = new List<ForscanDtc>();
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        for (int i = 0; i < lines.Length; i++)
        {
            var m = DtcHdr.Match(lines[i]);
            if (!m.Success) continue;
            var title = lines.Skip(i + 1).Take(3).Select(l => Regex.Match(l, @"^Code: \S+ - (.+)$")).FirstOrDefault(x => x.Success)?.Groups[1].Value ?? "";
            list.Add(new(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, title));
        }
        return list;
    }

    /// <summary>Configuration report: blocks "ACR - Name" followed by "Key: value" lines.</summary>
    public static List<ForscanModuleInfo> ParseConfigReport(string text)
    {
        var list = new List<ForscanModuleInfo>();
        ForscanModuleInfo? cur = null;
        foreach (var raw in text.Split('\n'))
        {
            var l = raw.TrimEnd('\r');
            if (l.Length == 0) { cur = null; continue; }
            var m = Mod.Match(l);
            if (cur is null && m.Success && !l.Contains(':')) { cur = new(m.Groups[1].Value, m.Groups[2].Value, new()); list.Add(cur); continue; }
            if (cur is not null && l.IndexOf(':') is int k and > 0) cur.Fields[l[..k].Trim()] = l[(k + 1)..].Trim();
        }
        return list;
    }

    /// <summary>Lines in FORScan As-Built format, if any (for checksum validation).</summary>
    public static IEnumerable<string> AsBuiltLines(string text) =>
        text.Split('\n').Select(l => l.Trim()).Where(l => Regex.IsMatch(l, @"^[0-9A-F]{3}-[0-9A-F]{2}-[0-9A-F]{2}\s*[:=]?\s*[0-9A-F ]+$"));
}
