using ObdLink.Core.Elm;
using ObdLink.Core.Transport;
using Serilog;

namespace ObdLink.Core.Adapters;

public enum AdapterKind { Unknown, ObdLinkEx, ObdLinkMxPlus, OtherStn, GenericElm }
public enum PortKind { Unknown, Usb, Bluetooth, Virtual }

[Flags]
public enum AdapterCapabilities
{
    None = 0,
    HsCan = 1,         // STP 31-36
    MsCan = 2,         // STP 51-54, MS-CAN transceiver (FRPM: "typically pins 3 and 11")
    SwCan = 4,         // STP 61-64, pin 1 (GMLAN) - not used for Ford here
    J1850Pwm = 8,      // Ford SCP
    J1850Vpw = 16,     // GM/Chrysler class 2/PCI (not used by this Ford-only app)
    StnCommands = 32,  // ST* command set
    HighUartBaud = 64, // physical UART/USB link where the host baud rate matters
}

/// <summary>
/// Per-adapter settings. Provenance of each fact:
/// - Chip IDs and product names: the OBDLink FRPM device table lists STN2230/2231/2232 as "OBDLink EX, USB w/ support for MS CAN"
///   and STN2255/2256 as "OBDLink MX+, Bluetooth w/ MS CAN and SW CAN" [VERIFIED-DOC].
/// - The EX defaulting to 115200 bps: ScanTool support article ("Baud rate is set to 115.2 Kbps" + FORScan "Auto-increase") [VERIFIED-DOC].
/// - 2,000,000 bps for the EX: seen in Dale's FORScan log after FORScan's auto-increase [VERIFIED-FROM-LOG]. It is not the power-on default.
/// - J1850 PWM/VPW on the EX and MX+: the FORScan compatibility table lists "Ford SCP/J1850 PWM: Yes" for both.
///   VPW is [UNVERIFIED] on the EX (not used by this app).
/// - Timeouts are engineering estimates [UNVERIFIED]; tune them using the logged round-trip times.
/// </summary>
public sealed record AdapterProfile(
    AdapterKind Kind, string DisplayName, PortKind Port, int[] BaudCandidates,
    TimeSpan CommandTimeout, TimeSpan ResetTimeout, AdapterCapabilities Capabilities, string Notes = "")
{
    public bool Has(AdapterCapabilities c) => (Capabilities & c) == c;
    public string CapabilityText => string.Join(", ", Enum.GetValues<AdapterCapabilities>().Where(c => c != 0 && Has(c)).Select(c => c switch
    {
        AdapterCapabilities.HsCan => "HS-CAN", AdapterCapabilities.MsCan => "MS-CAN", AdapterCapabilities.SwCan => "SW-CAN",
        AdapterCapabilities.J1850Pwm => "J1850 PWM", AdapterCapabilities.J1850Vpw => "J1850 VPW", AdapterCapabilities.StnCommands => "ST cmds",
        _ => c.ToString(),
    }));
}

public static class AdapterProfiles
{
    const AdapterCapabilities StnBase = AdapterCapabilities.HsCan | AdapterCapabilities.StnCommands;

    public static readonly AdapterProfile ObdLinkEx = new(AdapterKind.ObdLinkEx, "OBDLink EX (USB)", PortKind.Usb,
        new[] { 115200, 2000000, 500000, 1000000, 230400, 38400, 9600 },
        TimeSpan.FromMilliseconds(1500), TimeSpan.FromSeconds(3),
        StnBase | AdapterCapabilities.MsCan | AdapterCapabilities.J1850Pwm | AdapterCapabilities.HighUartBaud,
        "MS-CAN via STP 53 [VERIFIED-DOC: FRPM device table]; J1850 VPW [UNVERIFIED]; 2 Mbps seen in log only after auto-increase");

    public static readonly AdapterProfile ObdLinkMxPlus = new(AdapterKind.ObdLinkMxPlus, "OBDLink MX+ (Bluetooth)", PortKind.Bluetooth,
        new[] { 115200 }, // SPP virtual COM: the host baud is nominal
        TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6),
        StnBase | AdapterCapabilities.MsCan | AdapterCapabilities.SwCan | AdapterCapabilities.J1850Pwm | AdapterCapabilities.J1850Vpw,
        "Bluetooth SPP: the baud setting has no effect; higher latency, so timeouts are longer. J1850 VPW is from the product listing");

    public static readonly AdapterProfile OtherStn = new(AdapterKind.OtherStn, "Other STN adapter", PortKind.Unknown,
        new[] { 115200, 500000, 38400, 9600 }, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), StnBase,
        "Unknown STN device: MS-CAN not assumed");

    public static readonly AdapterProfile GenericElm = new(AdapterKind.GenericElm, "Generic ELM327", PortKind.Unknown,
        new[] { 38400, 115200, 9600 }, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5), AdapterCapabilities.HsCan,
        "No STP; MS-CAN unavailable");

    /// <summary>Port-kind default used before identification (USB = fast, BT = slow).</summary>
    public static AdapterProfile ForPort(PortKind k) => k == PortKind.Bluetooth ? ObdLinkMxPlus : ObdLinkEx;

    /// <summary>Classifies the adapter from its ATI / STI / STDI replies.</summary>
    public static AdapterProfile Detect(AdapterInfo info)
    {
        string dev = info.DeviceId ?? "", stn = info.StnId ?? "";
        if (dev.Contains("OBDLink EX", StringComparison.OrdinalIgnoreCase) || stn.StartsWith("STN223", StringComparison.OrdinalIgnoreCase)) return ObdLinkEx;
        if (dev.Contains("MX+", StringComparison.OrdinalIgnoreCase) || stn.StartsWith("STN2255", StringComparison.OrdinalIgnoreCase) || stn.StartsWith("STN2256", StringComparison.OrdinalIgnoreCase)) return ObdLinkMxPlus;
        if (info.StnId is not null) return OtherStn;
        return GenericElm;
    }
}

/// <summary>Finds a working host baud rate by sending ATI at each candidate rate until a reply contains "ELM327".</summary>
public static class BaudProber
{
    public static async Task<(int Baud, ITransport Transport)?> ProbeAsync(Func<int, ITransport> factory, IEnumerable<int> candidates, TimeSpan perTry, CancellationToken ct = default)
    {
        foreach (var b in candidates)
        {
            var t = factory(b);
            try
            {
                await t.OpenAsync(ct);
                var link = new ElmLink(t) { DefaultTimeout = perTry };
                var r = await link.SendAsync("ATI", perTry, ct);
                if (r.Lines.Any(l => l.Contains("ELM327", StringComparison.OrdinalIgnoreCase)))
                {
                    Log.Information("Adapter answered at {Baud} bps", b);
                    return (b, t);
                }
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                Log.Debug("No adapter at {Baud}: {Err}", b, ex.Message);
            }
            await t.CloseAsync();
        }
        return null;
    }
}
