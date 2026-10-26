using System.Text.Json;
using ObdLink.Core.Logging;
using ObdLink.Core.Elm;
using ObdLink.Core.Modules;
using ObdLink.Core.Security;

namespace ObdLink.Core.Vehicles;

public sealed record ModuleIdentity(string Acronym, string Name, string Bus, uint TxId, uint RxId, bool Known,
    Dictionary<string, string> Dids, bool HasAsBuiltDe00, string? Note = null);

/// <summary>Saved result of "Identify vehicle". Stored in Data\vehicles\&lt;redacted VIN&gt;.json (the full VIN stays inside the file on this PC).</summary>
public sealed class VehicleProfile
{
    public string Vin { get; set; } = "";
    public string VinRedacted => VinRedactor.MaskVin(Vin.Length == 17 ? Vin : Vin.PadLeft(17, 'X'));
    public string DisplayName { get; set; } = "";
    public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
    public VinInfo? Offline { get; set; }
    public Dictionary<string, string>? Online { get; set; }
    public List<ModuleIdentity> Modules { get; set; } = new();
    public string? FactoryAbFile { get; set; }
    /// <summary>v0.3.2: true when identified on a DEMO (simulated) adapter. Demo profiles are labeled and never used on a real adapter.</summary>
    public bool IsDemo { get; set; }
    /// <summary>Also catches profiles saved by v0.3.1 and earlier (no IsDemo flag): the simulator's synthetic VIN or DEMO part numbers.</summary>
    public bool LooksDemo => IsDemo || Vin.EndsWith("000001", StringComparison.Ordinal) && Vin.StartsWith("1FTDEMX", StringComparison.Ordinal) || IsLegacyLogDemoVin(Vin)
        || Modules.Any(m => m.Dids != null && m.Dids.TryGetValue("F188", out var v) && v.StartsWith("DEMO", StringComparison.OrdinalIgnoreCase));

    // v0.3.4 and earlier, the log-based DEMO simulator reported a masked VIN whose tail came from Dale's truck.
    // Only its SHA-256 is kept so the tail no longer appears in the source, but those old profiles still show [DEMO].
    private const string LegacyLogDemoVinSha256 = "3769afa8825ce091acffb966eb0021e318402571c8d7aaf6ae93e5ed3de479c2";
    public static bool IsLegacyLogDemoVin(string vin) => vin.Length == 17 && vin.StartsWith("XXXXXXXXXXX", StringComparison.Ordinal)
        && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(vin))).Equals(LegacyLogDemoVinSha256, StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string FileNameFor(string vin) => SafePaths.Token(VinRedactor.MaskVin(vin)) + ".json";
    public string Save(string dir) { Directory.CreateDirectory(dir); var p = Path.Combine(dir, FileNameFor(Vin)); File.WriteAllText(p, JsonSerializer.Serialize(this, Json)); return p; }
    public static VehicleProfile Load(string path) => JsonSerializer.Deserialize<VehicleProfile>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Empty vehicle profile");

    public static List<VehicleProfile> LoadAll(string dir)
    {
        var res = new List<VehicleProfile>();
        if (!Directory.Exists(dir)) return res;
        foreach (var f in Directory.GetFiles(dir, "*.json")) try { res.Add(Load(f)); } catch (Exception ex) { Serilog.Log.Warning(ex, "Skipped unreadable vehicle profile {File}", Path.GetFileName(f)); }
        return res;
    }

    /// <summary>Label for the Vehicle dropdown, e.g. "2017 FORD F-150 (XXXXXXXXXXX025268)".</summary>
    public string Label => (LooksDemo ? "[DEMO] " : "") + $"{(string.IsNullOrWhiteSpace(DisplayName) ? "Vehicle" : DisplayName)} ({VinRedacted})";

    public IReadOnlyList<ModuleDefinition> ToModuleDefinitions() => Modules.Select(m => new ModuleDefinition(m.Acronym, m.Name,
        Enum.TryParse<Bus>(m.Bus, out var b) ? b : Bus.HsCan, m.TxId, m.RxId, m.Known ? Confidence.CommunityReported : Confidence.Unverified,
        "Found by Identify vehicle" + (m.Dids.TryGetValue("F188", out var sw) ? $"; F188 {sw}" : ""))).ToList();
}
