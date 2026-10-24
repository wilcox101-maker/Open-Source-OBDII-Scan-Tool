namespace ObdLink.Core.ConsoleCmds;

/// <summary>Console command reference. Printed by the console "help" command; mirrored in docs/GUIDE.md ("Console command reference").</summary>
public static class ConsoleHelp
{
    public static readonly IReadOnlyList<(string Cmd, string What)> Examples = new[]
    {
        ("help  (or ?)", "Show this list (built-in, nothing is sent)"),
        ("cls", "Clear the console output (built-in)"),
        ("ATZ", "Reset the adapter"),
        ("ATI", "ELM327 version string"),
        ("STI", "STN firmware id (OBDLink only)"),
        ("STDI", "OBDLink device/hardware id"),
        ("ATRV", "Battery voltage at the OBD port"),
        ("ATSP6", "ELM protocol 6 = ISO 15765-4 CAN 11-bit 500k (HS-CAN)"),
        ("STP33 / STP53", "STN protocol: 33 = HS-CAN 500k (pins 6/14), 53 = Ford MS-CAN 125k (pins 3/11, MX+ only)"),
        ("ATSH7E0", "Set the request header (target module). 7DF = OBD broadcast, 7E0 = PCM, 726 = BCM, 720 = IPC"),
        ("ATDP", "Show the current protocol"),
        ("0100", "OBD mode 01: supported PIDs (needs ATSH7DF or 7E0)"),
        ("0902", "OBD mode 09: VIN"),
        ("22F190", "UDS ReadDataByIdentifier F190 = VIN (send after ATSH to a module)"),
        ("22F188 / 22F111 / 22F113", "UDS: software part number / hardware part / assembly part"),
        ("22DE00", "UDS: read As-Built block DE00 (read-only)"),
        ("1003", "UDS DiagnosticSessionControl: extended session (allowed, changes session only)"),
        ("3E00", "UDS TesterPresent (keeps a session alive; module presence check)"),
        ("19 02 FF", "UDS ReadDTCInformation, all DTCs by status mask FF (spaces are ignored)"),
    };

    /// <summary>UDS/OBD service ids blocked unless "Allow writes" is on, and always blocked in read-only test mode.</summary>
    public static readonly IReadOnlyList<(string Sid, string Name)> Blocked = new[]
    {
        ("04", "OBD clear DTCs"), ("11", "ECUReset"), ("14", "ClearDiagnosticInformation"), ("27", "SecurityAccess"),
        ("2E", "WriteDataByIdentifier (As-Built write)"), ("2F", "InputOutputControl"), ("31", "RoutineControl"),
        ("34-37", "Request download/upload/transfer (flashing)"), ("3B", "WriteDataByLocalIdentifier"),
    };

    public static readonly IReadOnlyList<(string Nrc, string Meaning)> Nrcs = new[]
    {
        ("10", "generalReject"), ("11", "serviceNotSupported"), ("12", "subFunctionNotSupported"), ("13", "incorrect message length/format"),
        ("22", "conditionsNotCorrect (e.g. engine running, wrong session)"), ("24", "requestSequenceError"), ("31", "requestOutOfRange (DID not supported)"),
        ("33", "securityAccessDenied"), ("35", "invalidKey"), ("36", "exceededNumberOfAttempts"), ("37", "requiredTimeDelayNotExpired"),
        ("72", "generalProgrammingFailure"), ("78", "responsePending (module is busy; app waits)"), ("7E", "subFunctionNotSupportedInActiveSession"),
        ("7F", "serviceNotSupportedInActiveSession (try 1003 first)"),
    };

    public static string Text()
    {
        var nl = Environment.NewLine;
        return "Console syntax: one command per line. Lines starting with AT or ST go to the adapter as-is; anything else is sent as hex bytes" + nl
             + "to the current header (ATSH). Spaces are optional. Replies are shown raw (header + data). 7F xx yy = negative response (NRC yy)." + nl + nl
             + "Examples:" + nl + string.Join(nl, Examples.Select(e => $"  {e.Cmd,-26} {e.What}")) + nl + nl
             + "Blocked unless 'Allow writes' is ticked (and always blocked while Read-only test mode is on):" + nl
             + string.Join(nl, Blocked.Select(b => $"  {b.Sid,-6} {b.Name}")) + nl + nl
             + "Common NRCs (7F <sid> <nrc>):" + nl + string.Join(nl, Nrcs.Select(n => $"  {n.Nrc}  {n.Meaning}"));
    }
}
