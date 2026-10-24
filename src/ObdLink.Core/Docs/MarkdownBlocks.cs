using System.Text.RegularExpressions;

namespace ObdLink.Core.Docs;

public enum MdKind { Heading, Paragraph, Bullet, Numbered, Table, Code, Rule }

/// <summary>One block of the Guide. Text keeps inline **bold** / `code` markers; <see cref="MarkdownBlocks.Inline"/> splits them.</summary>
public sealed record MdBlock(MdKind Kind, string Text, int Level = 0, IReadOnlyList<IReadOnlyList<string>>? Rows = null);

/// <summary>Minimal Markdown block parser for docs/GUIDE.md (headings, lists, pipe tables, code fences, paragraphs).</summary>
public static class MarkdownBlocks
{
    private static readonly Regex Num = new(@"^\s*(\d+)[.)]\s+(.*)$"), Bul = new(@"^\s*[-*+]\s+(.*)$"), Sep = new(@"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$");

    public static IReadOnlyList<MdBlock> Parse(string md)
    {
        var lines = md.Replace("\r\n", "\n").Split('\n');
        var res = new List<MdBlock>();
        var para = new List<string>();
        void Flush() { if (para.Count > 0) { res.Add(new(MdKind.Paragraph, string.Join(" ", para))); para.Clear(); } }
        for (int i = 0; i < lines.Length; i++)
        {
            var l = lines[i];
            var t = l.Trim();
            if (t.StartsWith("```"))
            {
                Flush(); var code = new List<string>();
                for (i++; i < lines.Length && !lines[i].Trim().StartsWith("```"); i++) code.Add(lines[i]);
                res.Add(new(MdKind.Code, string.Join("\n", code))); continue;
            }
            if (t.Length == 0) { Flush(); continue; }
            if (t.StartsWith('#')) { Flush(); var lvl = t.TakeWhile(c => c == '#').Count(); res.Add(new(MdKind.Heading, t[lvl..].Trim(), Math.Min(lvl, 4))); continue; }
            if (t is "---" or "***" or "___") { Flush(); res.Add(new(MdKind.Rule, "")); continue; }
            if (t.StartsWith('|'))
            {
                Flush(); var rows = new List<IReadOnlyList<string>>();
                for (; i < lines.Length && lines[i].Trim().StartsWith('|'); i++)
                {
                    if (Sep.IsMatch(lines[i])) continue;
                    var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToList();
                    rows.Add(cells);
                }
                i--; res.Add(new(MdKind.Table, "", Rows: rows)); continue;
            }
            if (Num.Match(l) is { Success: true } n) { Flush(); res.Add(new(MdKind.Numbered, n.Groups[2].Value.Trim(), int.Parse(n.Groups[1].Value))); continue; }
            if (Bul.Match(l) is { Success: true } b) { Flush(); res.Add(new(MdKind.Bullet, b.Groups[1].Value.Trim(), (l.Length - l.TrimStart().Length) / 2)); continue; }
            para.Add(t);
        }
        Flush();
        return res;
    }

    /// <summary>Splits inline text into (text, bold, code) runs for **bold** and `code`.</summary>
    public static IReadOnlyList<(string Text, bool Bold, bool Code)> Inline(string s)
    {
        var res = new List<(string, bool, bool)>();
        foreach (Match m in Regex.Matches(s, @"\*\*(.+?)\*\*|`([^`]+)`|([^*`]+|[*`])"))
            if (m.Groups[1].Success) res.Add((m.Groups[1].Value, true, false));
            else if (m.Groups[2].Success) res.Add((m.Groups[2].Value, false, true));
            else res.Add((m.Value, false, false));
        return res;
    }
}
