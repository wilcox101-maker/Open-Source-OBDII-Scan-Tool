using System.Reflection;
using System.Text.Json;

namespace ObdLink.Core.Modules;

/// <summary>
/// Ford/FORScan module acronym → full name, from the community "FORScan Programming Modules Acronyms-Abbreviations"
/// list (updated May-20-2019, see community-db/ford-module-acronyms). Names/descriptions only: this never changes a
/// module's CAN address. UNVERIFIED community data.
/// </summary>
public static class ModuleAcronyms
{
    public sealed record Entry(string Code, string Name, bool SeenIn2019F150Log, bool Ambiguous);

    private static readonly Lazy<IReadOnlyDictionary<string, Entry>> _map = new(Load);
    public static IReadOnlyDictionary<string, Entry> All => _map.Value;

    public static Entry? Find(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : All.TryGetValue(code.Trim(), out var e) ? e : null;

    /// <summary>Full name: the module table's own name wins; the community list is the fallback.</summary>
    public static string FullName(string acronym, string? tableName = null) =>
        !string.IsNullOrWhiteSpace(tableName) ? tableName! : Find(acronym)?.Name ?? acronym;

    /// <summary>"BCM – Body Control Module" style label used in grids and logs.</summary>
    public static string Label(string acronym, string? tableName = null)
    {
        var n = FullName(acronym, tableName);
        return n == acronym ? acronym : $"{acronym} – {n}";
    }

    /// <summary>Tooltip text: table name plus the community-list wording when it differs.</summary>
    public static string Tooltip(string acronym, string? tableName = null)
    {
        var e = Find(acronym);
        var lines = new List<string> { Label(acronym, tableName) };
        if (e != null && !string.Equals(e.Name, tableName, StringComparison.OrdinalIgnoreCase))
            lines.Add($"Community acronym list: {e.Name}");
        if (e?.Ambiguous == true) lines.Add("Acronym has more than one meaning in the community list [UNVERIFIED]");
        return string.Join("\n", lines);
    }

    private static IReadOnlyDictionary<string, Entry> Load()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ObdLink.Core.Data.ford_module_acronyms.json");
        var d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        if (s == null) return d;
        using var doc = JsonDocument.Parse(s);
        foreach (var a in doc.RootElement.GetProperty("acronyms").EnumerateArray())
        {
            var e = new Entry(a.GetProperty("code").GetString()!, a.GetProperty("name").GetString()!,
                a.GetProperty("seenIn2019F150Log").GetBoolean(), a.TryGetProperty("ambiguous", out var am) && am.GetBoolean());
            d.TryAdd(e.Code, e);
        }
        return d;
    }
}
