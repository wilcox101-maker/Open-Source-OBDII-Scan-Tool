using System.Text.Json;
using ObdLink.Core.Logging;
using ObdLink.Core.Elm;
using ObdLink.Core.Modules;

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

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string FileNameFor(string vin) => VinRedactor.MaskVin(vin) + ".json";
    public string Save(string dir) { Directory.CreateDirectory(dir); var p = Path.Combine(dir, FileNameFor(Vin)); File.WriteAllText(p, JsonSerializer.Serialize(this, Json)); return p; }
    public static VehicleProfile Load(string path) => JsonSerializer.Deserialize<VehicleProfile>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Empty vehicle profile");

    public static List<VehicleProfile> LoadAll(string dir)
    {
        var res = new List<VehicleProfile>();
        if (!Directory.Exists(dir)) return res;
        foreach (var f in Directory.GetFiles(dir, "*.json")) try { res.Add(Load(f)); } catch { }
        return res;
    }

    /// <summary>Label for the Vehicle dropdown, e.g. "2017 FORD F-150 (XXXXXXXXXXX025268)".</summary>
    public string Label => $"{(string.IsNullOrWhiteSpace(DisplayName) ? "Vehicle" : DisplayName)} ({VinRedacted})";

    public IReadOnlyList<ModuleDefinition> ToModuleDefinitions() => Modules.Select(m => new ModuleDefinition(m.Acronym, m.Name,
        m.Bus == "MsCan" ? Bus.MsCan : Bus.HsCan, m.TxId, m.RxId, m.Known ? Confidence.CommunityReported : Confidence.Unverified,
        "Found by Identify vehicle" + (m.Dids.TryGetValue("F188", out var sw) ? $"; F188 {sw}" : ""))).ToList();
}
