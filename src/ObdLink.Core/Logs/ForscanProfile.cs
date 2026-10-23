using System.Text.RegularExpressions;
using ObdLink.Core.AsBuilt;

namespace ObdLink.Core.Logs;

/// <summary>One MODNS entry of a FORScan vehicle .profile. The Key is FORScan's internal module id; it is NOT a CAN address.</summary>
public sealed record ForscanProfileModule(string Key, string[] Fields)
{
    /// <summary>
    /// Field index → Ford DID. Inferred by matching Dale's profile against his Ford .ab NODEID values: every index listed here matched the .ab for all modules that have it.
    /// This is provenance "inferred-profile-vs-ab". Indices 5/6 (PBM only) and 23 (short tag) have unknown meaning.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> FieldDid = new Dictionary<int, string>
    { [0] = "F113", [1] = "F188", [2] = "F124", [3] = "F18C", [9] = "F125", [13] = "F10A", [14] = "F16B", [15] = "F16C", [16] = "F16D", [17] = "F16E", [18] = "F17D", [21] = "F111" };
    public const int VinField = 20;
    public string Get(int i) => i < Fields.Length ? Fields[i] : "";
    public string PartNumber => Get(0);
    public string Strategy => Get(1);
    public Dictionary<string, string> Dids => FieldDid.Where(kv => Get(kv.Key) != "").ToDictionary(kv => kv.Value, kv => Get(kv.Key));
}

public sealed record SoftwareDiff(ushort Module, string Did, string Factory, string Current);

public sealed class ForscanProfile
{
    public Dictionary<string, string> Values { get; } = new();
    public List<ForscanProfileModule> Modules { get; } = new();
    public string? PcmKey => Values.GetValueOrDefault("PCMID")?.ToUpperInvariant();

    public static ForscanProfile Parse(string text)
    {
        var p = new ForscanProfile();
        foreach (var raw in text.Split('\n'))
        {
            var l = raw.TrimEnd('\r'); var i = l.IndexOf('=');
            if (i <= 0) continue;
            var k = l[..i]; var v = l[(i + 1)..];
            p.Values[k] = v;
            if (k.StartsWith("MODNS")) p.Modules.Add(new(k[5..].ToUpperInvariant(), v.Split(';')));
        }
        return p;
    }

    /// <summary>Maps profile modules to CAN request IDs by matching F113 (part number) and F18C (serial) against a Ford .ab. Both must match where both are present.</summary>
    public Dictionary<string, ushort> MapToAb(FordAbFile ab)
    {
        var res = new Dictionary<string, ushort>();
        foreach (var m in Modules)
            foreach (var (id, dids) in ab.Nodes)
            {
                bool Eq(string d) => !dids.TryGetValue(d, out var v) || m.Dids.GetValueOrDefault(d) is not { } c || v == c || v == "REDACTED" || c == "REDACTED";
                if (dids.GetValueOrDefault("F113") == m.PartNumber && Eq("F18C") && Eq("F188")) { res[m.Key] = id; break; }
                if (dids.GetValueOrDefault("F113") == m.PartNumber && !res.ContainsKey(m.Key)) res[m.Key] = id;
            }
        return res;
    }

    /// <summary>Compares software/hardware IDs (current FORScan profile vs factory .ab), excluding serial and VIN fields.</summary>
    public List<SoftwareDiff> CompareSoftware(FordAbFile ab)
    {
        var map = MapToAb(ab); var res = new List<SoftwareDiff>();
        foreach (var m in Modules)
        {
            if (!map.TryGetValue(m.Key, out var id)) continue;
            var f = ab.Nodes[id];
            foreach (var (did, cur) in m.Dids)
                if (did != "F18C" && f.TryGetValue(did, out var fac) && fac != cur) res.Add(new(id, did, fac, cur));
        }
        return res;
    }

    /// <summary>Masks VIN (keep last 6) and module serials (field 3 / F18C).</summary>
    public static string Redact(string text, int keep = 6) =>
        string.Join("\n", text.Split('\n').Select(raw =>
        {
            var l = Regex.Replace(raw, @"\b[A-HJ-NPR-Z0-9]{11}([A-HJ-NPR-Z0-9]{6})\b", m => new string('X', 11) + m.Groups[1].Value);
            if (!l.StartsWith("MODNS")) return l;
            var i = l.IndexOf('='); var f = l[(i + 1)..].Split(';');
            if (f.Length > 3 && f[3].TrimEnd('\r') != "") f[3] = "REDACTED";
            return l[..(i + 1)] + string.Join(';', f);
        }));
}
