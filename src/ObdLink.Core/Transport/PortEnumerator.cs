using System.IO.Ports;
using System.Text.RegularExpressions;
using ObdLink.Core.Adapters;

namespace ObdLink.Core.Transport;

public sealed record PortInfo(string Name, string? FriendlyName, PortKind Kind, string? PnpId = null)
{
    public string Label => Kind switch { PortKind.Usb => "USB", PortKind.Bluetooth => "BT", PortKind.Virtual => "SIM", _ => "?" } + $"  {Name}" + (FriendlyName is null ? "" : $"  {FriendlyName}");
    public override string ToString() => Label;
}

/// <summary>Lists COM ports. On Windows it uses WMI Win32_PnPEntity to get friendly names and tell USB ports from Bluetooth ones.</summary>
public static class PortEnumerator
{
    public static List<PortInfo> List()
    {
        var names = SerialPort.GetPortNames().Distinct().ToList();
        var wmi = OperatingSystem.IsWindows() ? QueryWmi() : new();
        return names.OrderBy(n => n).Select(n => wmi.TryGetValue(n, out var w) ? Classify(n, w.Name, w.Pnp) : new PortInfo(n, null, PortKind.Unknown)).ToList();
    }

    /// <summary>Classifies a port by its PnP device ID and friendly name. Pure function, so it is unit-testable off Windows.</summary>
    public static PortInfo Classify(string port, string? friendly, string? pnpId)
    {
        var p = pnpId?.ToUpperInvariant() ?? ""; var f = friendly ?? "";
        PortKind k = p.StartsWith("BTHENUM") || f.Contains("Bluetooth", StringComparison.OrdinalIgnoreCase) ? PortKind.Bluetooth
                   : p.StartsWith("FTDIBUS") || p.StartsWith("USB") || f.Contains("USB", StringComparison.OrdinalIgnoreCase) ? PortKind.Usb
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
