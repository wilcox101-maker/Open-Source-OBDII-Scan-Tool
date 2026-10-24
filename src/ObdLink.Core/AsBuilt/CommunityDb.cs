using System.Text.Json;

namespace ObdLink.Core.AsBuilt;

/// <summary>One community As-Built feature row (schema of community-db/*/features.json).</summary>
public sealed record CommunityFeature(string Source, string Address, IReadOnlyList<string> Pattern, string Description, IReadOnlyList<string> Values)
{
    /// <summary>Nibble positions (0-based, across the joined pattern) that the row changes ('*' or a concrete hex digit).</summary>
    public IReadOnlyList<int> ChangedPositions => string.Concat(Pattern).Replace("-", "")
        .Select((c, i) => (c, i)).Where(t => t.c is not ('x' or 'X')).Select(t => t.i).ToList();
}

public enum MergeStatus { New, Merged, Conflict }
public sealed record MergeResult(CommunityFeature Incoming, MergeStatus Status, CommunityFeature? Existing, string Note);

/// <summary>Loads community-db feature files and cross-references them without overwriting anything.</summary>
public static class CommunityDb
{
    public static IReadOnlyList<CommunityFeature> LoadFeatures(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var list = new List<CommunityFeature>();
        foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            string S(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
            IReadOnlyList<string> A(string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : Array.Empty<string>();
            list.Add(new(S("sheet") is { Length: > 0 } sh ? sh : S("tab"), S("address"), A("pattern"), S("description"), A("values")));
        }
        return list;
    }

    /// <summary>
    /// New = address/positions unknown in the existing set. Merged = same address and same changed nibble positions,
    /// and every concrete incoming value appears in the existing value list (or the existing row lists none).
    /// Conflict = same address+positions but the incoming value is not among the existing documented values.
    /// </summary>
    public static IReadOnlyList<MergeResult> Merge(IEnumerable<CommunityFeature> existing, IEnumerable<CommunityFeature> incoming)
    {
        var ex = existing.ToList();
        var res = new List<MergeResult>();
        foreach (var f in incoming)
        {
            var pos = f.ChangedPositions;
            var match = ex.FirstOrDefault(o => string.Equals(o.Address, f.Address, StringComparison.OrdinalIgnoreCase) && o.ChangedPositions.SequenceEqual(pos));
            if (match is null) { res.Add(new(f, MergeStatus.New, null, "not in existing set")); continue; }
            var digits = string.Concat(f.Pattern).Replace("-", "").Where(c => c is not ('x' or 'X' or '*')).Select(char.ToUpperInvariant).ToList();
            bool documented = match.Values.Count == 0 || digits.All(d => match.Values.Any(v => v.TrimStart().StartsWith(d + "=", StringComparison.OrdinalIgnoreCase) || v.Contains(", " + d + "=", StringComparison.OrdinalIgnoreCase)));
            res.Add(new(f, documented ? MergeStatus.Merged : MergeStatus.Conflict, match, documented ? "same address+position; value documented" : "value not among existing documented values"));
        }
        return res;
    }
}
