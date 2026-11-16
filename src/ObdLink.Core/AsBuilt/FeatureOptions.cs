using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ObdLink.Core.AsBuilt;

/// <summary>One documented value of an option: the hex digits to write and what the community sheet says they mean.</summary>
public sealed record OptionValue(string Digits, string Meaning)
{
    public string Display => $"{Digits} = {Meaning}";
}

/// <summary>
/// v0.15.0: an editable As-Built option from a community sheet row whose pattern marks the digits it controls with '*'.
/// <see cref="Positions"/> are indexes into the line's data hex (checksum digits are never part of an option). Rows whose
/// pattern holds literal digits instead of '*' are recipes with sheet-specific meaning and are not offered for editing.
/// CommunityReported: these meanings are not from Ford.
/// </summary>
public sealed record FeatureOption(string Address, string Tab, string Pattern, string Description, IReadOnlyList<OptionValue> Values, IReadOnlyList<int> Positions)
{
    public AsBuiltAddress Addr => AsBuiltAddress.Parse(Address);
    public int DigitCount => Positions.Count;

    /// <summary>The option's digits in <paramref name="line"/>, or null when the line is too short for the pattern.</summary>
    public string? Current(AsBuiltLine line)
    {
        var hex = Convert.ToHexString(line.Data);
        return Positions.Max() < hex.Length ? string.Concat(Positions.Select(i => hex[i])) : null;
    }

    public string? MeaningOf(string digits) => Values.FirstOrDefault(v => v.Digits.Equals(digits, StringComparison.OrdinalIgnoreCase))?.Meaning;

    private static readonly Regex ValueRx = new(@"^\s*([0-9A-Fa-f]+)\s*=\s*(.+?)\s*$", RegexOptions.Singleline);

    /// <summary>
    /// Values from the sheet's value list, plus "d=meaning" items inside the description (some tabs put them there). Only items
    /// with exactly <paramref name="digits"/> hex digits count; the first meaning for a given value wins.
    /// </summary>
    public static IReadOnlyList<OptionValue> ParseValues(IEnumerable<string> values, string description, int digits)
    {
        var list = new List<OptionValue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Take(string s)
        {
            var m = ValueRx.Match(s);
            if (m.Success && m.Groups[1].Value.Length == digits && seen.Add(m.Groups[1].Value.ToUpperInvariant()))
                list.Add(new OptionValue(m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value));
        }
        foreach (var v in values) Take(v);
        if (description.Contains('='))
            foreach (var part in description.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                Take(part.Contains(':') ? part[(part.IndexOf(':') + 1)..] : part);
        return list.OrderBy(v => Convert.ToInt64(v.Digits, 16)).ToList();
    }

    /// <summary>Editable options from a community-db features.json ({entries:[{sheet|tab, address, pattern[], description, values[]}]}).</summary>
    public static IReadOnlyList<FeatureOption> Load(string featuresJson)
    {
        var res = new List<FeatureOption>();
        var seen = new HashSet<string>();
        foreach (var e in JsonDocument.Parse(featuresJson).RootElement.GetProperty("entries").EnumerateArray())
        {
            var addr = e.GetProperty("address").GetString() ?? "";
            if (!Regex.IsMatch(addr, @"^[0-9A-Fa-f]{3}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2}$")) continue;
            var tab = (e.TryGetProperty("sheet", out var s) ? s : e.GetProperty("tab")).GetString() ?? "";
            var pat = string.Concat(e.GetProperty("pattern").EnumerateArray().Select(p => p.GetString()));
            var pos = Enumerable.Range(0, pat.Length).Where(i => pat[i] == '*').ToList();
            if (pos.Count == 0) continue;
            // A full 3-group pattern (12 digits) ends with the checksum byte; a row that marks it is not an option.
            if (pat.Length == 12 && pos.Max() >= 10) continue;
            var desc = e.GetProperty("description").GetString() ?? "";
            if (!seen.Add($"{addr}|{tab}|{pat}|{desc}")) continue;
            var values = e.TryGetProperty("values", out var v) ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : new List<string>();
            res.Add(new FeatureOption(addr.ToUpperInvariant(), tab, pat, desc, ParseValues(values, desc, pos.Count), pos));
        }
        return res;
    }

    /// <summary>The 2015-2020 F-150 community sheet (Livnitup), shipped inside Core. CommunityReported / UNVERIFIED.</summary>
    public static IReadOnlyList<FeatureOption> LoadF150_2015_2020()
    {
        using var s = typeof(FeatureOption).Assembly.GetManifestResourceStream("community-f150-2015-2020-features.json")
            ?? throw new InvalidOperationException("The community As-Built sheet is missing from this build.");
        using var r = new StreamReader(s);
        return Load(r.ReadToEnd());
    }
}

/// <summary>A pending change: set <see cref="Option"/> to <see cref="Digits"/>.</summary>
public sealed record OptionEdit(FeatureOption Option, string Digits);

/// <summary>
/// v0.15.0: applies option edits to As-Built lines. Each edit replaces exactly the option's digits; the line checksum is
/// recomputed. Two edits that set the same digit to different values are refused, and so is a value with the wrong number of
/// digits or a non-hex digit. Lines that no edit touches are returned unchanged. Writing still goes through the normal As-Built
/// write path (backup, confirmation, read-back).
/// </summary>
public static class AsBuiltEditor
{
    public static AsBuiltLine Apply(AsBuiltLine line, FeatureOption option, string digits) => ApplyAll(new[] { line }, new[] { new OptionEdit(option, digits) })[0];

    public static IReadOnlyList<AsBuiltLine> ApplyAll(IReadOnlyList<AsBuiltLine> lines, IEnumerable<OptionEdit> edits)
    {
        var byAddr = lines.Select((l, i) => (l, i)).ToDictionary(x => x.l.Address.ToString(), x => x.i);
        var hex = lines.Select(l => Convert.ToHexString(l.Data).ToCharArray()).ToArray();
        var setBy = new Dictionary<(int Line, int Pos), (char Digit, OptionEdit Edit)>();
        foreach (var e in edits)
        {
            var d = (e.Digits ?? "").Trim().ToUpperInvariant();
            if (d.Length != e.Option.DigitCount || !d.All(Uri.IsHexDigit))
                throw new FormatException($"{e.Option.Address} '{Short(e.Option.Description)}': '{e.Digits}' must be {e.Option.DigitCount} hex digit(s).");
            if (!byAddr.TryGetValue(e.Option.Address, out var li))
                throw new InvalidOperationException($"Line {e.Option.Address} is not loaded. Read that block first.");
            if (e.Option.Positions.Max() >= hex[li].Length)
                throw new InvalidDataException($"Line {e.Option.Address} is shorter than the option's pattern ({e.Option.Pattern}); this sheet row does not fit this module.");
            for (int k = 0; k < d.Length; k++)
            {
                var key = (li, e.Option.Positions[k]);
                if (setBy.TryGetValue(key, out var prev) && prev.Digit != d[k])
                    throw new InvalidOperationException($"Conflicting changes to {e.Option.Address} digit {e.Option.Positions[k] + 1}: '{Short(prev.Edit.Option.Description)}' sets {prev.Digit}, '{Short(e.Option.Description)}' sets {d[k]}.");
                setBy[key] = (d[k], e);
                hex[li][e.Option.Positions[k]] = d[k];
            }
        }
        return lines.Select((l, i) =>
        {
            var data = Convert.FromHexString(new string(hex[i]));
            return data.AsSpan().SequenceEqual(l.Data) ? l : AsBuiltLine.Create(l.Address, data);
        }).ToList();
    }

    private static string Short(string s) => s.Length <= 40 ? s : s[..40] + "…";
}
