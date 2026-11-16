namespace ObdLink.Core.Uds;

/// <summary>
/// Service-level facts used by the client, the console guard and the write gate. One table, so a service cannot be
/// "state-changing" in one place and harmless in another.
/// </summary>
public static class UdsServices
{
    /// <summary>
    /// Services that change module or vehicle state: OBD-II clear (04) and control (08), ECU reset, clear DTCs, security access,
    /// communication control, dynamic DIDs, writes (2E, 3B KWP, 3D), I/O control, routines, transfer (34-38), DTC setting,
    /// event response and link control. Read-only test mode blocks all of them.
    /// </summary>
    public static readonly byte[] StateChanging =
    {
        0x04, 0x08, 0x11, 0x14, 0x27, 0x28, 0x2C, 0x2E, 0x2F,
        0x31, 0x34, 0x35, 0x36, 0x37, 0x38, 0x3B, 0x3D, 0x85, 0x86, 0x87,
    };

    /// <summary>ISO 14229-1 services whose second byte is a sub-function (bit 7 = suppressPosRspMsgIndicationBit).</summary>
    private static readonly byte[] WithSubFunction = { 0x10, 0x11, 0x19, 0x27, 0x28, 0x29, 0x2C, 0x31, 0x3E, 0x83, 0x84, 0x85, 0x86, 0x87 };

    public static bool IsStateChanging(byte sid) => Array.IndexOf(StateChanging, sid) >= 0;
    public static bool HasSubFunction(byte sid) => Array.IndexOf(WithSubFunction, sid) >= 0;

    /// <summary>True when the request sets the suppress-positive-response bit, so silence is the expected answer to success.</summary>
    public static bool SuppressesPositiveResponse(byte[] req) => req.Length >= 2 && HasSubFunction(req[0]) && (req[1] & 0x80) != 0;
}

/// <summary>
/// Server timing (ISO 14229-2). P2: time to the first response. P2*: time to the final response after a "response pending"
/// (NRC 0x78). DiagnosticSessionControl's positive response reports both: 50 ss P2hi P2lo P2*hi P2*lo, P2 in 1 ms and
/// P2* in 10 ms units.
/// </summary>
public readonly record struct UdsServerTiming(TimeSpan P2, TimeSpan P2Star)
{
    /// <summary>ISO 14229-2 default-session values: P2server_max 50 ms, P2*server_max 5000 ms.</summary>
    public static readonly UdsServerTiming Default = new(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(5000));

    public static bool TryParse(byte[] sessionResponse, out UdsServerTiming timing)
    {
        timing = Default;
        if (sessionResponse.Length < 6 || sessionResponse[0] != 0x50) return false;
        int p2 = sessionResponse[2] << 8 | sessionResponse[3];
        int p2Star = (sessionResponse[4] << 8 | sessionResponse[5]) * 10;
        timing = new UdsServerTiming(TimeSpan.FromMilliseconds(p2), TimeSpan.FromMilliseconds(p2Star));
        return true;
    }
}

/// <summary>One DID in a multi-DID ReadDataByIdentifier request; <paramref name="Length"/> is the data length the module returns for it.</summary>
public readonly record struct DidRequest(ushort Did, int Length);

/// <summary>One DID's data from a ReadDataByIdentifier reply.</summary>
public sealed record DidValue(ushort Did, byte[] Data);
