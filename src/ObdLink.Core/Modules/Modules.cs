using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Uds;
using Serilog;

namespace ObdLink.Core.Modules;

public enum Confidence { Standard, CommunityReported, Unverified }

public sealed record ModuleDefinition(string Acronym, string Name, Bus Bus, uint TxId, uint RxId, Confidence Confidence, string Note = "", string? Evidence = null)
{
    public const string VerifiedFromLog = "verified-from-log";
    public const string VerifiedFactoryAb = "verified-factory-ab";
    /// <summary>True if the module appeared as "Found module" in Dale's FORScan log (presence only; the log carries no CAN IDs or bus).</summary>
    public bool SeenInLog => HasEvidence(VerifiedFromLog);
    /// <summary>True if Ford's official As-Built (.ab) for Dale's VIN lists this module id (as BCE_MODULE lines and/or a NODEID). This confirms the address. It does not confirm the bus.</summary>
    public bool InFactoryAb => HasEvidence(VerifiedFactoryAb);
    public bool HasEvidence(string tag) => (Evidence ?? "").Split(',').Contains(tag);
    public bool HasAddress => TxId != 0;
}

/// <summary>
/// 2017 Ford F-150 (P552) module addresses. Physical request IDs follow the Ford convention
/// response = request + 8. Addresses are as commonly reported in public FORScan / f150forum
/// As-Built threads (e.g. "726" BCM, "720" IPC, "7D0" APIM). Bus assignment (HS vs MS) for
/// 2017 is [UNVERIFIED] for several modules — the scanner probes BOTH buses regardless.
/// </summary>
public static class FordF150_2017
{
    /// <summary>Maps FORScan acronyms used in logs to the acronyms in this table.</summary>
    public static readonly IReadOnlyDictionary<string,string> ForscanAlias = new Dictionary<string,string> { ["BdyCM"] = "BCM", ["OBD2_PCM"] = "PCM" };

    public static readonly IReadOnlyList<ModuleDefinition> Modules = new List<ModuleDefinition>
    {
        new("PCM",  "Powertrain Control Module",          Bus.HsCan, 0x7E0, 0x7E8, Confidence.Standard, "J1979 standard ECU #1 address", Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("TCM",  "Transmission Control Module",        Bus.HsCan, 0x7E1, 0x7E9, Confidence.Unverified, "May be integrated in PCM on some powertrains; not present in Dale's factory .ab"),
        new("ABS",  "Anti-lock Brake System",             Bus.HsCan, 0x760, 0x768, Confidence.CommunityReported, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("RCM",  "Restraint Control Module",           Bus.HsCan, 0x737, 0x73F, Confidence.CommunityReported, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("PSCM", "Power Steering Control Module",      Bus.HsCan, 0x730, 0x738, Confidence.CommunityReported, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("BCM",  "Body Control Module",                Bus.HsCan, 0x726, 0x72E, Confidence.CommunityReported, "Reported reachable on HS and MS on some models [UNVERIFIED]", Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("IPC",  "Instrument Panel Cluster",           Bus.HsCan, 0x720, 0x728, Confidence.CommunityReported, "Bus [UNVERIFIED] for 2017", Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("APIM", "SYNC Accessory Protocol Interface",  Bus.MsCan, 0x7D0, 0x7D8, Confidence.CommunityReported, "Bus [UNVERIFIED]", Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("ACM",  "Audio Control Module",               Bus.MsCan, 0x727, 0x72F, Confidence.CommunityReported, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("HVAC", "Climate Control Module",             Bus.MsCan, 0x733, 0x73B, Confidence.CommunityReported, "Not present in Dale's factory .ab"),
        new("PAM",  "Parking Aid Module",                 Bus.HsCan, 0x736, 0x73E, Confidence.CommunityReported, "Not present in Dale's factory .ab"),
        new("IPMA", "Image Processing Module A (camera)", Bus.HsCan, 0x706, 0x70E, Confidence.CommunityReported, "Not present in Dale's factory .ab"),
        new("GWM",  "Gateway Module",                     Bus.HsCan, 0x716, 0x71E, Confidence.Unverified, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("DDM",  "Driver Door Module",                 Bus.MsCan, 0x740, 0x748, Confidence.CommunityReported, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("PDM",  "Passenger Door Module",              Bus.MsCan, 0x741, 0x749, Confidence.CommunityReported, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("SCCM", "Steering Column Control Module",     Bus.HsCan, 0x724, 0x72C, Confidence.CommunityReported, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("TRM",  "Trailer Module",                     Bus.HsCan, 0x791, 0x799, Confidence.CommunityReported, "Was 0x775; changed to 0x791 per the f150forum As-Built spreadsheet (TRM sheet). Not found on Dale's truck; not present in Dale's factory .ab"),
        new("FCIM", "Front Controls Interface Module",    Bus.MsCan, 0x7A7, 0x7AF, Confidence.Unverified, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("DSM",  "Driver Seat Module",                 Bus.MsCan, 0x744, 0x74C, Confidence.Unverified, "Not present in Dale's factory .ab"),
        new("GPSM", "GPS Module",                         Bus.MsCan, 0x701, 0x709, Confidence.Unverified, Evidence: ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("SODL", "Side Obstacle Detection Left",       Bus.HsCan, 0x7C4, 0x7CC, Confidence.Unverified, "Not present in Dale's factory .ab"),
        new("SODR", "Side Obstacle Detection Right",      Bus.HsCan, 0x7C6, 0x7CE, Confidence.Unverified, "Not present in Dale's factory .ab"),
        // Modules FORScan found on Dale's 2017 F-150 that weren't in the original table. The log has no CAN IDs, so the address is 0 (unknown) and the scanner skips them.
        new("TCCM",  "Transfer Case Control Module",            Bus.HsCan, 0x761, 0x769, Confidence.Unverified, "0x761 confirmed by Ford .ab NODEID 761 (part base 7H417); replaces the old 4X4 row", ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("RTM",   "Radio Transceiver Module",                Bus.HsCan, 0x751, 0x759, Confidence.Unverified, "0x751 confirmed by Ford .ab (751-01-01 + NODEID; part base 15K619); bus not confirmed", ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("PBM",   "Park Brake Control Module",               Bus.HsCan /* bus unknown */, 0x756, 0x75E, Confidence.Unverified, "0x756 identified: FORScan profile/config report part FL34-2C496-AK + serial match .ab NODEID 756. Response ID 0x75E assumed (+8) [UNVERIFIED]; bus [UNVERIFIED]", ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("OCS",   "Occupant Classification System Module",   Bus.HsCan /* bus unknown */, 0x765, 0x76D, Confidence.Unverified, "0x765 identified: FORScan profile/config report part HL3B-604A17-AA + serial match .ab NODEID 765. Response ID 0x76D assumed (+8) [UNVERIFIED]; bus [UNVERIFIED]", ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
        new("FCDIM", "Front Control/Display Interface Module",  Bus.MsCan, 0x7A5, 0x7AD, Confidence.CommunityReported, "0x7A5 confirmed by Ford .ab (3 blocks); bus [UNVERIFIED]", ModuleDefinition.VerifiedFromLog + "," + ModuleDefinition.VerifiedFactoryAb),
    };
}

/// <summary>
/// 2008 Dodge Ram 1500 (DR/DH), per docs/CHRYSLER-NETWORKS.md:
/// - Diagnostic CAN C, 500 kbps, 11-bit, on DLC pins 6/14, through the TIPM/CGW gateway.
///   Bus/pins are HIGH confidence; the 11-bit format is MED. STP 33 / ATSP6 is MED confidence.
/// - CAN B is not reachable at the DLC.
/// - PCM 0x7E0/0x7E8 is generic ISO 15765-4.
/// Every other ID in 0x7E1-0x7EF is a SWEEP SLOT, not a known module [UNVERIFIED].
/// Chrysler enhanced IDs (e.g. 0x620 reported for a BCM, LOW confidence) are found with <see cref="ObdLink.Core.Diagnostics.DiscoverySweep"/>.
/// </summary>
public static class DodgeRam1500_2008
{
    public const string ProfileNote = "Ram 2008 profile [UNVERIFIED]: diag CAN C 500k 11-bit, pins 6/14, STP 33 / ATSP6";
    public static readonly IReadOnlyList<ModuleDefinition> Modules =
        new[] { new ModuleDefinition("PCM", "Powertrain Control Module (OBD-II)", Bus.HsCan, 0x7E0, 0x7E8, Confidence.Standard, "ISO 15765-4 7E0/7E8; CAN C 6/14 (CHRYSLER-NETWORKS.md)") }
        .Concat(Enumerable.Range(0x7E1, 0x7EF - 0x7E1 + 1).Select(id => new ModuleDefinition(
            $"ID{id:X3}", $"Sweep slot 0x{id:X3}", Bus.HsCan, (uint)id, (uint)id + 8, Confidence.Unverified,
            id <= 0x7E7 ? "ISO 15765-4 OBD slot; module identity unknown [UNVERIFIED]" : "Physical ID 0x7E8-0x7EF are normally RESPONSE IDs; probed anyway for discovery [UNVERIFIED]")))
        .ToList();
}

public sealed record ScanResult(ModuleDefinition Module, bool Responded, string? Detail);

/// <summary>Probes each CAN module with UDS TesterPresent (0x3E 00), falling back to 0x22 F190 (VIN DID, ISO 14229).</summary>
public sealed class ModuleScanner
{
    private readonly AdapterSession _session;
    private readonly IDiagChannel _ch;
    private readonly BusProfile _profile;
    public ModuleScanner(AdapterSession s, IDiagChannel ch, BusProfile? profile = null) { _session = s; _ch = ch; _profile = profile ?? new BusProfile(); }

    public async Task<List<ScanResult>> ScanAsync(IEnumerable<ModuleDefinition> modules, IProgress<ScanResult>? progress = null, CancellationToken ct = default)
    {
        var results = new List<ScanResult>();
        foreach (var grp in modules.GroupBy(m => m.Bus))
        {
            if (grp.Key is not (Bus.HsCan or Bus.MsCan))
            {
                foreach (var m in grp) { var r = new ScanResult(m, false, "Bus not implemented [STUB]"); results.Add(r); progress?.Report(r); }
                continue;
            }
            await _session.SelectProtocolAsync((StnProtocol)(grp.Key == Bus.HsCan ? _profile.HsCanPreset : _profile.MsCanPreset), ct);
            if (grp.Key == Bus.MsCan && _profile.MsCanBaudOverride is int b) await _session.Link.SendAsync($"STPBR {b}", ct: ct);
            foreach (var m in grp)
            {
                ct.ThrowIfCancellationRequested();
                if (!m.HasAddress) { var skip = new ScanResult(m, false, "No known CAN address [UNVERIFIED]; skipped"); results.Add(skip); progress?.Report(skip); continue; }
                ScanResult res;
                try
                {
                    var uds = new UdsClient(_ch, m.TxId);
                    await uds.TesterPresentAsync(ct);
                    res = new ScanResult(m, true, "3E OK");
                }
                catch (UdsNegativeResponseException ex) { res = new ScanResult(m, true, ex.Message); }
                catch (Exception ex) { res = new ScanResult(m, false, ex.Message); }
                Log.Information("Scan {Mod} 0x{Tx:X3} on {Bus}: {Ok} {Detail}", m.Acronym, m.TxId, m.Bus, res.Responded, res.Detail);
                results.Add(res); progress?.Report(res);
            }
        }
        return results;
    }
}
