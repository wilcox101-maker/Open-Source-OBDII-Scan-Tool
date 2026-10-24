using System.IO.Ports;
using System.Text.RegularExpressions;
using ObdLink.Core.Adapters;

namespace ObdLink.Core.Transport;

public sealed record PortInfo(string Name, string? FriendlyName, PortKind Kind, string? PnpId = null)
{
    /// <summary>v0.3.2: FTDI USB-serial (VID 0403), which is what the OBDLink EX uses.</summary>
    public bool IsFtdi => PortEnumerator.IsFtdiPnp(PnpId);
    public bool IsSimulated => Kind == PortKind.Virtual;
    public string Label => IsSimulated ? $"DEMO (simulated)  {Name}  {FriendlyName}"
        : (Kind switch { PortKind.Usb => "USB", PortKind.Bluetooth => "BT", _ => "COM" }) + $"  {Name}" + (IsFtdi ? "  OBDLink EX / FTDI" : "") + (FriendlyName is null ? "" : $"  ({FriendlyName})");
    public override string ToString() => Label;
}

/// <summary>Lists COM ports. On Windows it uses WMI Win32_PnPEntity to get friendly names and tell USB ports from Bluetooth ones.</summary>
public static class PortEnumerator
{
    /// <summary>
    /// v0.3.2: every COM port Windows knows about: the union of SerialPort.GetPortNames (registry SERIALCOMM) and WMI Win32_PnPEntity,
    /// so a port is listed even when one source misses it. Never throws.
    /// </summary>
    public static List<PortInfo> List()
    {
        string[] names;
        try { names = SerialPort.GetPortNames(); } catch (Exception ex) { Serilog.Log.Warning("GetPortNames failed: {Err}", ex.Message); names = Array.Empty<string>(); }
        var wmi = OperatingSystem.IsWindows() ? QueryWmi() : new();
        return Merge(names, wmi.Select(kv => (kv.Key, (string?)kv.Value.Name, (string?)kv.Value.Pnp)));
    }

    /// <summary>Pure merge of port names and (port, friendly, pnp) rows: de-duplicated (case-insensitive, trimmed), sorted by COM number.</summary>
    public static List<PortInfo> Merge(IEnumerable<string> names, IEnumerable<(string Port, string? Friendly, string? Pnp)> pnp)
    {
        var info = new Dictionary<string, (string? F, string? P)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (port, f, p) in pnp) if (NormalizePort(port) is { } n) info[n] = (f, p);
        var all = names.Select(NormalizePort).Concat(info.Keys).Where(n => n != null).Select(n => n!).Distinct(StringComparer.OrdinalIgnoreCase);
        return all.OrderBy(PortNumber).ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                  .Select(n => info.TryGetValue(n, out var w) ? Classify(n, w.F, w.P) : new PortInfo(n, null, PortKind.Unknown)).ToList();
    }

    /// <summary>"com7", " COM7 ", "7", "\\.\COM7" -> "COM7"; anything else -> null.</summary>
    public static string? NormalizePort(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var m = Regex.Match(s.Trim(), @"^(?:\\\\\.\\)?(?:COM)?(\d{1,3})$", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var n = int.Parse(m.Groups[1].Value);
        return n is >= 1 and <= 256 ? $"COM{n}" : null;
    }

    private static int PortNumber(string n) => int.TryParse(n.AsSpan(3), out var i) ? i : int.MaxValue;

    public static bool IsFtdiPnp(string? pnpId)
    {
        var p = pnpId?.ToUpperInvariant() ?? "";
        return p.StartsWith("FTDIBUS") || p.Contains("VID_0403");
    }

    public const string NoPortsHint = "No COM ports found. Plug in the OBDLink EX (USB) and click Refresh. If it still does not show: install the FTDI VCP driver (ftdichip.com, \"VCP Drivers\"), then check Device Manager > Ports (COM & LPT) for \"USB Serial Port (COMx)\". You can also type the COM port by hand.";

    /// <summary>
    /// v0.3.2 default-selection rule. Real ports always win over the simulator. The simulator is only kept when the user picked it
    /// explicitly (<paramref name="simExplicit"/>) and it is still listed. Order for real ports: previous selection, saved default,
    /// FTDI (OBDLink EX), other USB, Bluetooth, anything else. Returns null when there is no real port and no explicit simulator choice.
    /// </summary>
    public static PortInfo? ChooseDefault(IReadOnlyList<PortInfo> ports, string? previous, string? savedDefault, bool simExplicit)
    {
        PortInfo? Find(string? n) => n is null ? null : ports.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase));
        var prev = Find(previous);
        if (prev is { IsSimulated: true } && simExplicit) return prev;
        if (prev is { IsSimulated: false }) return prev;
        if (Find(savedDefault) is { IsSimulated: false } saved) return saved;
        var real = ports.Where(p => !p.IsSimulated).ToList();
        return real.FirstOrDefault(p => p.IsFtdi) ?? real.FirstOrDefault(p => p.Kind == PortKind.Usb)
            ?? real.FirstOrDefault(p => p.Kind == PortKind.Bluetooth) ?? real.FirstOrDefault();
    }

    /// <summary>Classifies a port by its PnP device ID and friendly name. Pure function, so it is unit-testable off Windows.</summary>
    public static PortInfo Classify(string port, string? friendly, string? pnpId)
    {
        var p = pnpId?.ToUpperInvariant() ?? ""; var f = friendly ?? "";
        PortKind k = p.StartsWith("BTHENUM") || f.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ? PortKind.Bluetooth
                   : IsFtdiPnp(p) || p.StartsWith("USB") || f.Contains("USB", StringComparison.OrdinalIgnoreCase) ? PortKind.Usb
                   : PortKind.Unknown;
        return new PortInfo(port, friendly, k, pnpId);
    }

    private static Dictionary<string, (string Name, string Pnp)> QueryWmi()
    {
        var map = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        if (!OperatingSystem.IsWindows()) return map;
        try
        {
            using var s = new System.Management.ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (var o in s.Get())
            {
                var name = o["Name"]?.ToString() ?? ""; var pnp = o["PNPDeviceID"]?.ToString() ?? "";
                var m = Regex.Match(name, @"\((COM\d+)\)");
                if (m.Success) map[m.Groups[1].Value] = (name, pnp);
            }
        }
        catch (Exception ex) { Serilog.Log.Warning("WMI port query failed: {Err}", ex.Message); }
        return map;
    }
}
