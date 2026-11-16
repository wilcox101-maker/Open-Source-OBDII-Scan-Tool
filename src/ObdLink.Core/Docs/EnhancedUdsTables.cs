using System.Globalization;
using System.Text;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Flash;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Uds;
using ObdLink.Core.Vehicles;
using ObdLink.Core.Dtc;
using ObdLink.Core.Elm;
using ObdLink.Core.Security;

namespace ObdLink.Core.Docs;

/// <summary>
/// Builds the reference tables in docs/ENHANCED-UDS.md from the data the tool actually uses (module table, service gate,
/// identification DIDs, the built-in enhanced PID table, routine ranges, NRC texts, flash fault policy). The doc is generated
/// so it cannot drift from the code: <c>EnhancedUdsDocTests</c> fails when the committed tables differ from this output
/// (set UPDATE_DOCS=1 when running the tests to rewrite them).
/// </summary>
public static class EnhancedUdsTables
{
    public const string Begin = "<!-- BEGIN GENERATED: EnhancedUdsTables (do not edit by hand; run the tests with UPDATE_DOCS=1) -->";
    public const string End = "<!-- END GENERATED -->";

    /// <summary>ISO 14229-1 service names (and the two OBD-II services in the write gate) with where the tool uses each one.</summary>
    private static readonly (byte Sid, string Name, string Where)[] Services =
    {
        (0x04, "OBD-II clear DTCs (SAE J1979 mode 04)", "console only"),
        (0x08, "OBD-II on-board control (SAE J1979 mode 08)", "console only"),
        (0x10, "DiagnosticSessionControl", "Routines tab (10 01 / 10 03), session host, flash (10 02)"),
        (0x11, "ECUReset", "flash engine only (after programming)"),
        (0x14, "ClearDiagnosticInformation", "DTCs tab: Clear module DTCs"),
        (0x19, "ReadDTCInformation", "DTCs tab (19 02 by status; details 19 04 / 19 06)"),
        (0x22, "ReadDataByIdentifier", "identify, As-Built read, enhanced PIDs (Live Data, dashboards)"),
        (0x23, "ReadMemoryByAddress", "Core only (UdsMemory)"),
        (0x27, "SecurityAccess", "seed/key plug-ins; flash engine"),
        (0x28, "CommunicationControl", "Core only (ControlLease, restore 28 00); bench-first"),
        (0x2A, "ReadDataByPeriodicIdentifier", "Core only (UdsPeriodic)"),
        (0x2C, "DynamicallyDefineDataIdentifier", "Core only (UdsDynamicData)"),
        (0x2E, "WriteDataByIdentifier", "As-Built write / restore / undo; dashboard write cards"),
        (0x2F, "InputOutputControlByIdentifier", "dashboard output-hold cards (lease + auto-release); Core ControlLease"),
        (0x31, "RoutineControl", "Routines tab, dashboard routine cards, flash engine (FF00/FF01)"),
        (0x34, "RequestDownload", "flash engine only"),
        (0x35, "RequestUpload", "console only"),
        (0x36, "TransferData", "flash engine only"),
        (0x37, "RequestTransferExit", "flash engine only"),
        (0x38, "RequestFileTransfer", "console only"),
        (0x3B, "WriteDataByLocalIdentifier (KWP2000)", "console only"),
        (0x3D, "WriteMemoryByAddress", "console only"),
        (0x3E, "TesterPresent", "module scan, keep-alive (3E 80), flash preflight"),
        (0x85, "ControlDTCSetting", "Core only (ControlLease, restore 85 01); bench-first"),
        (0x86, "ResponseOnEvent", "console only"),
        (0x87, "LinkControl", "console only"),
    };

    /// <summary>Representative routine identifiers, one per ISO 14229-1 Annex F range, plus the edges of the refused ranges.</summary>
    private static readonly (ushort From, ushort To)[] RoutineRanges =
    {
        (0x0000, 0x00FF), (0x0100, 0x01FF), (0x0200, 0xDFFF), (0xE000, 0xE1FF), (0xE200, 0xE2FF), (0xE300, 0xEFFF),
        (0xF000, 0xFEFF), (0xFF00, 0xFF00), (0xFF01, 0xFF01), (0xFF02, 0xFF02), (0xFF03, 0xFFFF),
    };

    public static string Markdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine(Begin).AppendLine();
        Buses(sb);
        Modules(sb);
        ServicesTable(sb);
        PhaseGates(sb);
        Sessions(sb);
        Timing(sb);
        Security(sb);
        IdentificationDids(sb);
        AsBuiltDids(sb);
        EnhancedPids(sb);
        DtcServices(sb);
        Controls(sb);
        Routines(sb);
        ReadExtras(sb);
        Programming(sb);
        Refusals(sb);
        Nrcs(sb);
        sb.AppendLine(End);
        return sb.ToString().Replace("\r\n", "\n");
    }

    private static void Modules(StringBuilder sb)
    {
        sb.AppendLine("### Ford modules (2017 F-150 table)").AppendLine();
        sb.AppendLine("Source: `FordF150_2017.Modules`. Response ID = request ID + 8 (Ford convention). \"Log\" = FORScan found the module on Dale's truck (presence only). \".ab\" = Ford's As-Built file for Dale's VIN lists the module id (confirms the address, not the bus). Bus is a starting guess: the scanner tries both buses.").AppendLine();
        Row(sb, "Module", "Name", "Bus", "Request", "Response", "Confidence", "Log", ".ab", "Note");
        Rule(sb, 9);
        foreach (var m in FordF150_2017.Modules)
            Row(sb, m.Acronym, m.Name, BusName(m.Bus), m.HasAddress ? $"0x{m.TxId:X3}" : "unknown", m.HasAddress ? $"0x{m.RxId:X3}" : "",
                m.Confidence.ToString(), m.SeenInLog ? "yes" : "", m.InFactoryAb ? "yes" : "", m.Note);
        sb.AppendLine();
    }

    private static void ServicesTable(StringBuilder sb)
    {
        sb.AppendLine("### Services").AppendLine();
        sb.AppendLine("Source: `UdsServices`. \"Gated\" = state-changing: refused in read-only mode and, in the console, unless \"Allow writes\" is on (one table for the client, console and write gate). \"Sub-fn\" = byte 2 is a sub-function (bit 7 suppresses the positive response).").AppendLine();
        Row(sb, "SID", "Service", "Gated", "Sub-fn", "Used by");
        Rule(sb, 5);
        foreach (var (sid, name, where) in Services)
            Row(sb, $"0x{sid:X2}", name, UdsServices.IsStateChanging(sid) ? "yes" : "", UdsServices.HasSubFunction(sid) ? "yes" : "", where);
        sb.AppendLine();
        sb.AppendLine("Restore requests let through a closed gate (`UdsServices.IsRestoreRequest`): `85 01` (DTC setting on), `28 00 xx` (enable Rx/Tx), `2F did 00` (return control to ECU).").AppendLine();
    }

    private static void IdentificationDids(StringBuilder sb)
    {
        sb.AppendLine("### Identification DIDs read by Identify vehicle").AppendLine();
        sb.AppendLine("Source: `IdentifyOptions.DefaultDids` (ISO 14229-1 Annex C identification range). Read-only: Identify sends only services 0x09, 0x3E and 0x22.").AppendLine();
        Row(sb, "DID", "Meaning in this tool");
        Rule(sb, 2);
        foreach (var d in IdentifyOptions.DefaultDids) Row(sb, $"0x{d:X4}", VehicleIdentifier.DidName(d));
        sb.AppendLine();
    }

    private static void AsBuiltDids(StringBuilder sb)
    {
        sb.AppendLine("### As-Built blocks").AppendLine();
        sb.AppendLine("Source: `AsBuiltBlock.DidForBlock`. **[UNVERIFIED community convention]**: block N (FORScan numbering, BCD) is DID 0xDE00 + (N - 1). Writes go only through the As-Built tab (length and checksum checks, backup, read-back, history).").AppendLine();
        Row(sb, "Block", "DID");
        Rule(sb, 2);
        foreach (var b in new byte[] { 0x01, 0x02, 0x03, 0x09, 0x10, 0x11 })
            Row(sb, b.ToString("X2"), $"0x{AsBuiltBlock.DidForBlock(b):X4}");
        sb.AppendLine();
    }

    private static void EnhancedPids(StringBuilder sb)
    {
        sb.AppendLine("### Enhanced (Mode 22) PIDs built in").AppendLine();
        sb.AppendLine("Source: `Data/ford-enhanced-pids.json` (the built-in default; users can edit their copy). Value = raw x scale + offset, raw = big-endian data bytes after `62 DID DID` (two's complement when signed). Only rows marked verified were checked on a truck.").AppendLine();
        Row(sb, "Key", "Name", "Module", "DID", "Bytes", "Signed", "Scale", "Offset", "Unit", "Verified", "Source");
        Rule(sb, 11);
        foreach (var p in EnhancedPidTable.WithBuiltIns(EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson())).Pids)
        {
            var mod = string.Join(" / ", p.Candidates.Select(c => $"0x{c:X3}"));
            Row(sb, p.Key, p.Name, mod, $"0x{p.Did.ToUpperInvariant()}", p.Bytes.ToString(CultureInfo.InvariantCulture), p.Signed ? "yes" : "",
                p.Scale.ToString("0.######", CultureInfo.InvariantCulture), p.Offset.ToString("0.######", CultureInfo.InvariantCulture), p.Unit,
                p.Verified ? "**yes**" : "no", p.Source ?? "");
        }
        sb.AppendLine();
    }

    private static void Routines(StringBuilder sb)
    {
        sb.AppendLine("### Routine identifier ranges (0x31)").AppendLine();
        sb.AppendLine("Source: `UdsRoutines.Classify` / `RefusalReason` (ISO 14229-1 Annex F). Ford routine identifiers themselves are catalog data and are not shipped.").AppendLine();
        Row(sb, "Range", "ISO class", "Policy");
        Rule(sb, 3);
        foreach (var (from, to) in RoutineRanges)
        {
            var range = from == to ? $"0x{from:X4}" : $"0x{from:X4}-0x{to:X4}";
            Row(sb, range, UdsRoutines.Classify(from).ToString(), UdsRoutines.RefusalReason(from) is string why ? "**refused**: " + why : "allowed, behind the write gate (typed RUN in the app)");
        }
        sb.AppendLine();
    }

    private static void Refusals(StringBuilder sb)
    {
        sb.AppendLine("### Never sent / write refusals").AppendLine();
        Row(sb, "What", "Where enforced", "Why");
        Rule(sb, 3);
        Row(sb, "RoutineControl 0xE200-0xE2FF (pyrotechnic deployment)", "`UdsClient.RequestAsync`, `UdsRoutines`, `ConsoleGuard` (also inside raw ISO-TP frames)", "can fire airbags or pretensioners; never sent whatever the settings");
        Row(sb, "RoutineControl 0xFF00 / 0xFF01 (erase / check dependencies)", "`UdsRoutines.BuildRequest` (Routines tab, console helpers, dashboard cards)", "only the flash engine sends them, inside a journaled job");
        Row(sb, "WriteDataByIdentifier 0xDE00-0xDEFF from a dashboard card", "`DashAction.Validate`", "As-Built writes go through the As-Built tab (checksums, backup, history)");
        Row(sb, "WriteDataByIdentifier 0xF180-0xF19F from a dashboard card", "`DashAction.Validate`", "identification DIDs (VIN, part numbers)");
        Row(sb, "Console `STPX`, `ATWM`, `ATCAF0` while writes are locked", "`ConsoleGuard`", "can send arbitrary frames or hide the service id");
        Row(sb, "Immobilizer / anti-theft bypass", "not implemented", "project limit (Dale, 2026-11-15)");
        sb.AppendLine();
    }

    private static void Nrcs(StringBuilder sb)
    {
        sb.AppendLine("### Negative response codes").AppendLine();
        sb.AppendLine("Source: `UdsNrc.Describe` / `Explain` (ISO 14229-1 Annex A) and `FlashFaults.FromNrc` (what the flash engine does). Response format: `7F <SID> <NRC>`. 0x94-0xEF are reserved for specific conditions; 0xF0-0xFE are manufacturer-specific.").AppendLine();
        Row(sb, "NRC", "ISO name", "Meaning shown to the user", "Flash engine action");
        Rule(sb, 4);
        for (int n = 0x10; n <= 0x93; n++)
        {
            var b = (byte)n;
            var name = UdsNrc.Describe(b);
            if (name == "unknown") continue;
            Row(sb, $"0x{n:X2}", name, UdsNrc.Explain(b), FlashFaults.FromNrc(b).Action.ToString());
        }
        sb.AppendLine();
    }

    private static void Buses(StringBuilder sb)
    {
        sb.AppendLine("### Buses and transport").AppendLine();
        sb.AppendLine("UDS runs on ISO 15765-2 (ISO-TP) over CAN with 11-bit IDs. Physical requests go to one module (`0x7xx`, reply on request + 8). OBD-II requests use the functional ID " +
            $"`0x{ObdService.Functional11:X3}` (every emissions ECU answers). ISO-TP: single frame up to 7 data bytes; longer messages are a first frame plus consecutive frames, paced by the receiver's flow control (block size, STmin). " +
            "Source: `Elm/StnProtocol.cs`, `Elm/BusProfile.cs`, `IsoTp/`.").AppendLine();
        var bp = new BusProfile();
        Row(sb, "Bus", "OBD connector pins", "Bit rate", "STN protocol (STP)", "Used for");
        Rule(sb, 5);
        Row(sb, "HS-CAN", "6 / 14", "500 kbps", $"{bp.HsCanPreset} (ISO 15765, 11-bit)", "OBD-II, PCM, ABS, BCM, most 2017 F-150 modules");
        Row(sb, "MS-CAN", "3 / 11", "125 kbps", $"{bp.MsCanPreset} (ISO 15765, 11-bit)", "Body / comfort modules on older Fords (APIM, ACM, door modules on some years) [bus per module UNVERIFIED for 2017]");
        Row(sb, "HS-CAN2", "3 / 11", "500 kbps", $"{bp.MsCanPreset} at 500k (STPBR)", "2015+ F-150: the 3/11 pair carries a 500k bus (MS-CAN at 125k answers CAN ERROR there)");
        Row(sb, "HS-CAN3", "—", "500 kbps", "—", "2021+ F-150; not supported yet");
        sb.AppendLine();
        sb.AppendLine("J2534 pass-thru (obdcli, v0.18.0): HS uses ISO15765 at 500k; MS / HS2 use ISO15765_PS with J1962 pin selection 0x030B (pins 3 / 11).").AppendLine();
    }

    private static void PhaseGates(StringBuilder sb)
    {
        sb.AppendLine("### When each write service may run on the truck").AppendLine();
        sb.AppendLine("The app enforces the write gate; *which phase* a service is allowed on the real truck is the project's verification plan (Phase 0 DEMO, 1 truck read-only, 2 one As-Built block, 3 As-Built any module + sessions + allow-listed routines, 4 bench-then-truck 2F/85/28, 5 programming / keys). A phase failure stops new work in that area.").AppendLine();
        Row(sb, "Service", "Earliest phase on the truck", "Notes");
        Rule(sb, 3);
        Row(sb, "0x22 / 0x19 / 0x3E / 0x09 / 0x01", "1", "read-only");
        Row(sb, "0x2E on As-Built DE00-DEFF", "2 (one block), 3 (any module)", "backup, journal, read-back, undo");
        Row(sb, "0x10 03 sessions, 0x27 for As-Built", "3", "seed/key from a user plug-in");
        Row(sb, "0x31 routines", "3", "allow-listed routine IDs only (list starts empty)");
        Row(sb, "0x14 clear DTCs", "3", "freeze frames are lost");
        Row(sb, "0x2F / 0x85 / 0x28", "4", "bench module first, then truck; restore leases");
        Row(sb, "0x10 02, 0x34 / 0x36 / 0x37, 0x31 FF00 / FF01, 0x11", "5", "flashing: bench flash and recovery of a spare module first");
        Row(sb, "PATS key programming", "5", "Ford's add-a-key procedure only; no bypass");
        sb.AppendLine();
    }

    private static void Sessions(StringBuilder sb)
    {
        sb.AppendLine("### Diagnostic sessions (0x10) and reset (0x11)").AppendLine();
        sb.AppendLine("Source: `UdsSession`, `ResetType`. A module starts in the default session; writes, routines and controls usually need the extended session; flashing needs the programming session. " +
            "Leaving a non-default session (timeout or `10 01`) drops security access and ends 0x28 / 0x85 / 0x2F effects. A positive `50` reply carries the module's P2 and P2* (the client uses them).").AppendLine();
        Row(sb, "Request", "Session / reset");
        Rule(sb, 2);
        foreach (var v in Enum.GetValues<UdsSession>()) Row(sb, $"`10 {(byte)v:X2}`", v.ToString());
        foreach (var v in Enum.GetValues<ResetType>()) Row(sb, $"`11 {(byte)v:X2}`", v + " reset");
        sb.AppendLine();
        sb.AppendLine("Suppress-positive-response: setting bit 7 of the sub-function (e.g. `3E 80`, `10 83`) asks the module to stay silent on success; silence is then the expected answer.").AppendLine();
    }

    private static void Timing(StringBuilder sb)
    {
        sb.AppendLine("### Timing, keep-alive and response pending").AppendLine();
        sb.AppendLine("Source: `UdsServerTiming`, `TesterPresentKeeper`, `UdsClient`.").AppendLine();
        Row(sb, "Parameter", "Value", "Meaning");
        Rule(sb, 3);
        Row(sb, "P2server_max", $"{UdsServerTiming.Default.P2.TotalMilliseconds:0} ms", "first response (ISO 14229-2 default; the module may report its own)");
        Row(sb, "P2*server_max", $"{UdsServerTiming.Default.P2Star.TotalMilliseconds:0} ms", "final response after NRC 0x78 response pending");
        Row(sb, "S3server", $"{TesterPresentKeeper.S3Server.TotalMilliseconds:0} ms", "a non-default session ends after this long without a request");
        Row(sb, "Keep-alive", $"`3E 80` after {TesterPresentKeeper.DefaultInterval.TotalSeconds:0} s idle", "sent by the session host under the shared adapter lock, on the module's own bus");
        Row(sb, "0x78 / 0x21 on an ELM-style link", $"up to {UdsClient.DefaultPendingRetries} resends, {UdsClient.DefaultPendingRetryDelay.TotalMilliseconds:0} ms apart", "only for requests that are safe to repeat (below); others report the NRC so the caller can settle the outcome by reading back");
        Row(sb, "0x78 on a listening link (J2534)", $"wait up to P2* per reply, at most {UdsClient.DefaultMaxPendingReplies} pending replies", "no resend");
        sb.AppendLine();
        sb.Append("Requests the client may resend after 0x78 / 0x21: ");
        sb.AppendLine(string.Join(", ", new[] { new byte[] { 0x10, 0x03 }, new byte[] { 0x19, 0x02 }, new byte[] { 0x22, 0xF1, 0x90 }, new byte[] { 0x23 }, new byte[] { 0x3E, 0x00 }, new byte[] { 0x31, 0x03, 0x12, 0x34 }, new byte[] { 0x2E, 0xDE, 0x00 }, new byte[] { 0x31, 0x01, 0x12, 0x34 } }
            .Where(UdsClient.IsIdempotent).Select(r => $"`{r[0]:X2}" + (r[0] == 0x31 ? " 03`" : "`")).Distinct()) + " (reads, session control, tester present, routine results). Writes and routine starts are never resent blindly.");
        sb.AppendLine();
    }

    private static void Security(StringBuilder sb)
    {
        sb.AppendLine("### Security access (0x27)").AppendLine();
        sb.AppendLine("Source: `UdsClient.SecurityAccessAsync`, `ISeedKeyProvider`, `SeedKeyOptions`, `FlashPlan.SecurityLevel`.").AppendLine();
        sb.AppendLine("1. Request a seed: `27 LL` with an **odd** level LL (0x01-0x7F). Reply `67 LL <seed>`.");
        sb.AppendLine("2. A seed of all zero bytes means the level is already unlocked (ISO 14229 convention): nothing more is sent.");
        sb.AppendLine("3. Otherwise the key comes from a seed/key **plug-in** (`ISeedKeyProvider.ComputeKeyAsync(moduleTxId, level, seed)`); the app ships none. No key → the operation stops before anything is written.");
        sb.AppendLine("4. Send the key: `27 LL+1 <key>` (the even level). Reply `67 LL+1` = unlocked until the session ends.");
        sb.AppendLine("5. A wrong key (NRC 0x35) is sent once and stops the operation; repeated wrong keys give 0x36 (exceeded attempts) and then 0x37 (wait, typically after power-up or a lockout).").AppendLine();
        Row(sb, "Where", "Level", "Notes");
        Rule(sb, 3);
        Row(sb, "As-Built writes", "Settings → Security access → As-Built level (odd hex; empty = never unlock)", "invalid (even or > 0x7F) values are ignored");
        Row(sb, "Flash engine", $"default 0x{new FlashPlan { TxId = 0x7E0, Image = FlashImage.FromSegments("x", new[] { new FlashSegment(0, new byte[] { 0 }) }) }.SecurityLevel:X2} per plan", "[UNVERIFIED for Ford] some modules use another odd level; one wait on 0x37 then stop");
        Row(sb, "Routines / dashboard cards", "none requested by the app", "a module answering 0x33 needs a session + level the operator supplies");
        sb.AppendLine();
        sb.AppendLine("**Per-module Ford levels and algorithms are not known to this project** (not public; no verified source). Plug-ins are .NET 8 DLLs approved by SHA-256 (`Data\\plugins\\seedkey\\`, typed TRUST); see `tools/SeedKeyPluginSample/README.md`.").AppendLine();
    }

    private static void DtcServices(StringBuilder sb)
    {
        sb.AppendLine("### DTC services (0x19, 0x14) and status bits").AppendLine();
        sb.AppendLine("Source: `UdsClient.ReadDtcsByStatusAsync`, `Dtc/DtcDetails.cs`. A UDS DTC is 3 bytes (shown as P/C/B/U + 4 hex digits + a 2-hex failure type) plus one status byte.").AppendLine();
        Row(sb, "Request", "ISO name", "Used for");
        Rule(sb, 3);
        Row(sb, "`19 01 mask`", "reportNumberOfDTCByStatusMask", "count");
        Row(sb, "`19 02 mask`", "reportDTCByStatusMask", "DTC list (DTCs tab, mask 0xFF)");
        Row(sb, "`19 03`", "reportDTCSnapshotIdentification", "which snapshots exist");
        Row(sb, "`19 04 DTC rec`", "reportDTCSnapshotRecordByDTCNumber", "freeze-frame style snapshot");
        Row(sb, "`19 06 DTC FF`", "reportDTCExtDataRecordByDTCNumber", "extended data (counters, raw)");
        Row(sb, "`19 0A`", "reportSupportedDTC", "every DTC the module supports");
        Row(sb, "`19 15`", "reportDTCWithPermanentStatus", "permanent DTCs");
        Row(sb, "`14 FF FF FF`", "ClearDiagnosticInformation (all groups)", "Clear module DTCs (gated, own bus only)");
        sb.AppendLine();
        Row(sb, "Status bit", "ISO name", "Shown as");
        Rule(sb, 3);
        for (int i = 0; i < 8; i++) Row(sb, $"0x{1 << i:X2}", DtcStatusBits.Names[i], DtcStatusBits.Plain[i]);
        sb.AppendLine();
    }

    private static void Controls(StringBuilder sb)
    {
        sb.AppendLine("### Control services (0x85, 0x28, 0x2F)").AppendLine();
        sb.AppendLine("Source: `Uds/UdsControl.cs`. Each disabling request returns a `ControlLease` whose restore (retried, ignores cancel) runs when the job ends; restore requests pass a closed write gate. All three end when the session ends.").AppendLine();
        Row(sb, "Request", "Meaning");
        Rule(sb, 2);
        Row(sb, "`85 01`", "DTC setting ON (restore)");
        Row(sb, "`85 02`", "DTC setting OFF");
        foreach (var t in Enum.GetValues<CommunicationControlType>())
            foreach (var g in new[] { CommunicationGroups.Normal, CommunicationGroups.NetworkManagement, CommunicationGroups.Both })
                if (t == CommunicationControlType.EnableRxAndTx || g == CommunicationGroups.Normal)
                    Row(sb, $"`28 {(byte)t:X2} {(byte)g:X2}`", $"{t}, {g} messages" + (t == CommunicationControlType.EnableRxAndTx ? " (restore)" : ""));
        foreach (var p in Enum.GetValues<IoControlParameter>())
            Row(sb, $"`2F DID DID {(byte)p:X2}" + (p == IoControlParameter.ShortTermAdjustment ? " state`" : "`"), p + (p == IoControlParameter.ReturnControlToEcu ? " (restore)" : p == IoControlParameter.ShortTermAdjustment ? " (dashboard hold card: released after 10 s, on the next press or when the window closes)" : ""));
        sb.AppendLine();
    }

    private static void ReadExtras(StringBuilder sb)
    {
        sb.AppendLine("### Other read services (Core, not in the app UI)").AppendLine();
        sb.AppendLine("Source: `UdsMemory`, `UdsDynamicData`, `UdsPeriodic`.").AppendLine();
        Row(sb, "Request", "Meaning");
        Rule(sb, 2);
        Row(sb, $"`23 {UdsMemory.AddressAndLengthFormat(4, 2):X2} addr(4) size(2)`", "ReadMemoryByAddress; the format byte = (size bytes << 4) | address bytes");
        Row(sb, "`2C 01 DDDI src...`", "define a dynamic DID from other DIDs (DDDI in 0xF200-0xF3FF)");
        Row(sb, "`2C 02 DDDI ALFID addr size...`", "define a dynamic DID from memory");
        Row(sb, "`2C 03 [DDDI]`", "clear one or all dynamic DIDs");
        foreach (var r in Enum.GetValues<PeriodicRate>()) Row(sb, $"`2A {(byte)r:X2} PDID...`", $"periodic DIDs at the {r.ToString().ToLowerInvariant()} rate (module-defined)");
        Row(sb, "`2A 04 [PDID...]`", "stop periodic DIDs");
        Row(sb, "`22 DID DID DID DID ...`", "several DIDs in one request (lengths must be known to split the reply)");
        sb.AppendLine();
    }

    private static void Programming(StringBuilder sb)
    {
        var plan = new FlashPlan { TxId = 0x7E0, Image = FlashImage.FromSegments("x", new[] { new FlashSegment(0, new byte[] { 0 }) }) };
        var rp = new FlashRetryPolicy();
        sb.AppendLine("### Programming sequence (flash engine)").AppendLine();
        sb.AppendLine("Source: `Flash/FlashEngine.cs`, `FlashPlan`, `FlashRetryPolicy` (OUIE). **Run only against the simulated module so far.** Ford jobs normally load a secondary bootloader (SBL) first; the engine does not model that yet (see `docs/FLASHING.md`).").AppendLine();
        sb.AppendLine("`10 03` (optional) → `10 02` → `27 LL / LL+1` → `31 01 " + $"{plan.EraseRoutineId:X4}" + " <region>` per erase region → per segment: `34 00 " + $"{plan.AddressAndLengthFormat:X2}" + " addr size` → `36 counter data` × N (counter 01..FF, wraps to 00) → `37` → `31 01 " + $"{plan.CheckRoutineId:X4}" + "` → `11 " + $"{(byte)plan.Reset:X2}" + "` → read part number (`22 F188`).").AppendLine();
        Row(sb, "Setting", "Default");
        Rule(sb, 2);
        Row(sb, "Battery window", $"{plan.MinVoltage:0.0}-{plan.MaxVoltage:0.0} V");
        Row(sb, "Max block length (capped by the 74 reply)", $"{plan.MaxBlockLength} bytes");
        Row(sb, "TransferData resends (same counter)", rp.BlockRetries.ToString(CultureInfo.InvariantCulture));
        Row(sb, "Other step resends", rp.StepRetries.ToString(CultureInfo.InvariantCulture));
        Row(sb, "Erase / check routine timeout", $"{rp.EraseTimeout.TotalSeconds:0} s");
        Row(sb, "Security delay wait (0x37)", $"{rp.SecurityDelay.TotalSeconds:0} s");
        Row(sb, "Wait after reset", $"{rp.PostResetDelay.TotalSeconds:0} s");
        sb.AppendLine();
    }

    private static string BusName(Elm.Bus b) => b switch { Elm.Bus.HsCan => "HS-CAN", Elm.Bus.MsCan => "MS-CAN", Elm.Bus.HsCan2 => "HS-CAN2", _ => b.ToString() };

    private static void Row(StringBuilder sb, params string[] cells) => sb.Append("| ").Append(string.Join(" | ", cells.Select(Cell))).AppendLine(" |");
    private static void Rule(StringBuilder sb, int n) => sb.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", n))).AppendLine();
    private static string Cell(string s) => s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();
}
