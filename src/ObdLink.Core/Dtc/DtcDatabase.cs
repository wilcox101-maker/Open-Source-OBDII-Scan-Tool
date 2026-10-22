using System.Reflection;
using System.Text.Json;

namespace ObdLink.Core.Dtc;

public sealed record DtcDefinition(string Code, string Description, string Source);

/// <summary>
/// DTC lookup stub. Ships a small set of SAE J2012 *generic* (P0xxx/U0xxx) titles, which are
/// publicly reproduced (e.g. Wikipedia/obd-codes.com listings of J2012 generic codes). Manufacturer codes
/// (P1xxx, Bxxxx, etc.) are intentionally NOT included.
/// </summary>
public sealed class DtcDatabase
{
    private readonly Dictionary<string, DtcDefinition> _map;
    public DtcDatabase(IEnumerable<DtcDefinition> defs) => _map = defs.ToDictionary(d => d.Code.ToUpperInvariant());
    public static DtcDatabase LoadEmbedded()
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("ObdLink.Core.Data.dtc_generic.json")!;
        var defs = JsonSerializer.Deserialize<List<DtcDefinition>>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        return new DtcDatabase(defs);
    }
    public int Count => _map.Count;
    public string Describe(string code)
    {
        var key = code.Split('-')[0].ToUpperInvariant();
        if (_map.TryGetValue(key, out var d)) return d.Description;
        return key.Length > 1 && key[1] != '0' ? "Manufacturer-specific code (no description in open DB)" : "Unknown (not in stub DB)";
    }
}
