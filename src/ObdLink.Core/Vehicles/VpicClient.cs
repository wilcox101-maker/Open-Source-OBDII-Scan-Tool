using System.Net.Http;
using System.Text.Json;

namespace ObdLink.Core.Vehicles;

/// <summary>
/// NHTSA vPIC online decoder (free, no key): GET https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValuesExtended/{vin}?format=json.
/// Opt-in only; the caller logs the request. Returns null on any network/parse failure (offline-safe).
/// </summary>
public sealed class VpicClient : IDisposable
{
    public const string BaseUrl = "https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValuesExtended/";
    public static readonly string[] Fields = { "Make", "Model", "ModelYear", "Trim", "Series", "BodyClass", "DriveType", "EngineModel", "EngineConfiguration",
        "EngineCylinders", "DisplacementL", "FuelTypePrimary", "TransmissionStyle", "GVWR", "PlantCity", "PlantState", "PlantCountry", "Manufacturer", "ErrorCode", "ErrorText" };
    private readonly HttpClient _http;
    public VpicClient(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = timeout ?? TimeSpan.FromSeconds(10) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("OBDLinkDiag (open-source)");
    }

    public void Dispose() => _http.Dispose();

    public static Uri UrlFor(string vin) => new(BaseUrl + Uri.EscapeDataString(VinDecoder.Normalize(vin)) + "?format=json");

    public async Task<IReadOnlyDictionary<string, string>?> DecodeAsync(string vin, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(UrlFor(vin), ct);
            if (!resp.IsSuccessStatusCode) return null;
            return Parse(await resp.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException) { return null; }
    }

    public static IReadOnlyDictionary<string, string>? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Results", out var res) || res.GetArrayLength() == 0) return null;
        var d = new Dictionary<string, string>();
        foreach (var p in res[0].EnumerateObject())
            if (Fields.Contains(p.Name) && p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()) && p.Value.GetString() != "Not Applicable")
                d[p.Name] = p.Value.GetString()!.Trim();
        return d;
    }
}
