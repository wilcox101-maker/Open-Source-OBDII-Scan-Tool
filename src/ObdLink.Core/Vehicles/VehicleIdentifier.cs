using ObdLink.Core.Adapters;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Uds;
using Serilog;

namespace ObdLink.Core.Vehicles;

public sealed record IdentifyOptions
{
    public uint StartId { get; init; } = 0x700;
    public uint EndId { get; init; } = 0x7E7;
    public bool SweepHsCan { get; init; } = true;
    public bool SweepMsCan { get; init; } = true;
    public int ProbeTimeoutMs { get; init; } = 100;
    public bool OnlineDecode { get; init; }
    /// <summary>Identification DIDs read from each responding module (all ReadDataByIdentifier, read-only).</summary>
    public IReadOnlyList<ushort> Dids { get; init; } = new ushort[] { 0xF190, 0xF188, 0xF111, 0xF113, 0xF124, 0xF18C, 0xF17F };
}

/// <summary>
/// "Identify vehicle": read VIN (OBD 09 02, fallback UDS 22 F190 on 7E0), decode it offline (and online via NHTSA vPIC if allowed),
/// sweep HS-CAN and MS-CAN for responding modules and read their identification DIDs. READ-ONLY: only 09, 3E and 22 are sent.
/// </summary>
public sealed class NotFordVehicleException(string wmi) : Exception($"{VinDecoder.FordOnlyMessage} (WMI {wmi})") { public string Wmi { get; } = wmi; }

public sealed class VehicleIdentifier
{
    public static readonly byte[] AllowedServices = { 0x09, 0x3E, 0x22 };
    private readonly AdapterSession _s; private readonly IDiagChannel _ch; private readonly VpicClient? _vpic;
    public VehicleIdentifier(AdapterSession s, IDiagChannel ch, VpicClient? vpic = null) { _s = s; _ch = ch; _vpic = vpic; }

    public static string DidName(ushort did) => did switch
    {
        0xF190 => "VIN", 0xF188 => "ECU software (strategy) part", 0xF111 => "ECU core assembly part", 0xF113 => "ECU delivery assembly part",
        0xF124 => "Calibration part", 0xF18C => "ECU serial", 0xF17F => "Serial / extra id", 0xDE00 => "As-Built block DE00", _ => $"DID {did:X4}",
    };

    public async Task<string?> ReadVinAsync(CancellationToken ct = default)
    {
        await _ch.SetTargetAsync(0x7DF, null, ct);
        try { var v = await new ObdService(_ch).ReadVinAsync(); if (v is { Length: 17 }) return v; } catch (Exception ex) when (ex is not OperationCanceledException) { Log.Debug("0902 failed: {E}", ex.Message); }
        try { var b = await new UdsClient(_ch, 0x7E0).ReadDataByIdentifierAsync(0xF190, ct); var v = AsciiOf(b); if (v.Length == 17) return v; }
        catch (Exception ex) when (ex is not OperationCanceledException) { Log.Debug("22F190 failed: {E}", ex.Message); }
        return null;
    }

    public static string AsciiOf(byte[] b) => new string(b.Where(x => x >= 0x20 && x < 0x7F).Select(x => (char)x).ToArray()).Trim();

    public static (string Acronym, string Name, bool Known) NameFor(uint tx)
    {
        var m = FordF150_2017.Modules.FirstOrDefault(x => x.TxId == tx);
        if (m != null) return (m.Acronym, ModuleAcronyms.FullName(m.Acronym, m.Name), true);
        return ($"0x{tx:X3}", "Unknown module (not in the address table)", false);
    }

    public async Task<VehicleProfile> RunAsync(IdentifyOptions o, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (o.EndId < o.StartId || o.EndId > 0x7FF) throw new ArgumentException("Range must be 11-bit and Start <= End");
        var p = new VehicleProfile();
        progress?.Report("Reading VIN (09 02, fallback 22 F190)...");
        if (_s.IsStn) await _s.SelectProtocolAsync(StnProtocol.HsCan11_500, ct); else await _s.SelectElmProtocolAsync('6', ct);
        p.Vin = await ReadVinAsync(ct) ?? "";
        if (p.Vin.Length == 17)
        {
            p.Offline = VinDecoder.Decode(p.Vin);
            if (!VinDecoder.IsFordWmi(p.Vin))
            {
                progress?.Report($"{VinDecoder.FordOnlyMessage} (WMI {p.Offline.Wmi}: {p.Offline.Manufacturer})");
                throw new NotFordVehicleException(p.Offline.Wmi);
            }
            progress?.Report($"VIN read; offline decode: {p.Offline.ModelYear} {p.Offline.Manufacturer}, plant {p.Offline.Plant}{(p.Offline.CheckDigitOk ? "" : " (check digit mismatch)")}");
            if (o.OnlineDecode && _vpic != null)
            {
                progress?.Report($"Online decode: GET {VpicClient.BaseUrl}<VIN> (NHTSA vPIC)...");
                p.Online = (await _vpic.DecodeAsync(p.Vin, ct))?.ToDictionary(k => k.Key, k => k.Value);
                progress?.Report(p.Online is null ? "Online decode unavailable (offline or timed out); offline decode kept" : $"Online decode: {string.Join(", ", new[] { "ModelYear", "Make", "Model", "Trim", "EngineModel", "DisplacementL" }.Where(p.Online.ContainsKey).Select(k => p.Online[k]))}");
            }
        }
        else progress?.Report("VIN not readable (ignition on? engine-off key-on required); continuing with module sweep");
        p.DisplayName = p.Online is { } on && on.TryGetValue("Model", out var model)
            ? $"{on.GetValueOrDefault("ModelYear")} {on.GetValueOrDefault("Make")} {model}".Trim()
            : p.Offline is { Valid: true } off ? $"{off.ModelYear} {off.Manufacturer}" : "Unidentified vehicle";

        var buses = new List<(Bus Bus, StnProtocol P)>();
        if (o.SweepHsCan) buses.Add((Bus.HsCan, StnProtocol.HsCan11_500));
        if (o.SweepMsCan && _s.IsStn && _s.Profile.Has(AdapterCapabilities.MsCan)) buses.Add((Bus.MsCan, StnProtocol.MsCan11_125));
        else if (o.SweepMsCan) progress?.Report("MS-CAN sweep skipped: adapter has no MS-CAN (OBDLink MX+ needed)");
        foreach (var (bus, proto) in buses)
        {
            if (_s.IsStn) await _s.SelectProtocolAsync(proto, ct); else await _s.SelectElmProtocolAsync('6', ct);
            await _s.Link.SendAsync($"ATST{Math.Clamp(o.ProbeTimeoutMs / 4, 1, 255):X2}", ct: ct);
            progress?.Report($"Sweeping {bus} 0x{o.StartId:X3}-0x{o.EndId:X3} with 3E 00...");
            try
            {
                for (uint id = o.StartId; id <= o.EndId; id++)
                {
                    ct.ThrowIfCancellationRequested();
                    if (id == 0x7DF || (id >= 0x7E8 && id <= 0x7EF)) continue;
                    if (p.Modules.Any(m => m.TxId == id)) continue; // already found on HS (gateway-forwarded)
                    var uds = new UdsClient(_ch, id);
                    bool present;
                    try { await uds.TesterPresentAsync(ct); present = true; }
                    catch (UdsNegativeResponseException) { present = true; }
                    catch (OperationCanceledException) { throw; }
                    catch { present = false; }
                    if (!present) continue;
                    var (ac, name, known) = NameFor(id);
                    var dids = new Dictionary<string, string>();
                    foreach (var did in o.Dids)
                    {
                        try { var b = await uds.ReadDataByIdentifierAsync(did, ct); var a = AsciiOf(b); dids[$"{did:X4}"] = a.Length >= Math.Max(4, b.Length / 2) ? a : Convert.ToHexString(b); }
                        catch (OperationCanceledException) { throw; }
                        catch { }
                    }
                    bool de00;
                    try { await uds.ReadDataByIdentifierAsync(0xDE00, ct); de00 = true; } catch (OperationCanceledException) { throw; } catch { de00 = false; }
                    p.Modules.Add(new(ac, name, bus.ToString(), id, id + 8, known, dids, de00));
                    progress?.Report($"FOUND {bus} 0x{id:X3} {ac} - {name}{(dids.TryGetValue("F188", out var sw) ? $"  F188={sw}" : "")}{(de00 ? "  (As-Built DE00 readable)" : "")}");
                }
            }
            finally { await _s.Link.SendAsync("ATST32", ct: CancellationToken.None); }
        }
        if (_s.IsStn) await _s.SelectProtocolAsync(StnProtocol.HsCan11_500, CancellationToken.None);
        progress?.Report($"Identify done: {p.Modules.Count} module(s) responded");
        return p;
    }
}
