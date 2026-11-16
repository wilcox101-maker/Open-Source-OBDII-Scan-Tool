using System.Globalization;
using System.Text;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Flash;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Uds;
using ObdLink.Core.Vehicles;

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
        Modules(sb);
        ServicesTable(sb);
        IdentificationDids(sb);
        AsBuiltDids(sb);
        EnhancedPids(sb);
        Routines(sb);
        Refusals(sb);
        Nrcs(sb);
        Timing(sb);
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

    private static void Timing(StringBuilder sb)
    {
        sb.AppendLine("### Timing defaults").AppendLine();
        sb.AppendLine("Source: `UdsServerTiming.Default` (ISO 14229-2); a module reports its own P2/P2* in the `50 ss` reply.").AppendLine();
        Row(sb, "Parameter", "Default");
        Rule(sb, 2);
        Row(sb, "P2server_max (first response)", $"{UdsServerTiming.Default.P2.TotalMilliseconds:0} ms");
        Row(sb, "P2*server_max (after NRC 0x78)", $"{UdsServerTiming.Default.P2Star.TotalMilliseconds:0} ms");
        sb.AppendLine();
    }

    private static string BusName(Elm.Bus b) => b switch { Elm.Bus.HsCan => "HS-CAN", Elm.Bus.MsCan => "MS-CAN", Elm.Bus.HsCan2 => "HS-CAN2", _ => b.ToString() };

    private static void Row(StringBuilder sb, params string[] cells) => sb.Append("| ").Append(string.Join(" | ", cells.Select(Cell))).AppendLine(" |");
    private static void Rule(StringBuilder sb, int n) => sb.Append('|').Append(string.Concat(Enumerable.Repeat(" --- |", n))).AppendLine();
    private static string Cell(string s) => s.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Trim();
}
