using ObdLink.Core.Elm;
using ObdLink.Core.Transport;

namespace ObdLink.Core.Adapters;

public enum ConnectionState { Disconnected, Connecting, Connected, Failed }

/// <summary>
/// Single source of truth for the top-bar status, battery and adapter labels so they can't contradict each other
/// (e.g. "Disconnected" next to a live battery reading, or an EX label while the simulated MX+ is connected).
/// </summary>
public sealed record ConnectionStatus(ConnectionState State, string StatusText, string Voltage, string AdapterName, string Capabilities)
{
    public static ConnectionStatus Disconnected { get; } = new(ConnectionState.Disconnected, "Disconnected", "-", "—", "");
    public static ConnectionStatus Connecting(PortInfo port, string step = "Connecting") => new(ConnectionState.Connecting, $"{step} {port.Name}…", "-", "—", "");
    public static ConnectionStatus Failed(string error) => new(ConnectionState.Failed, "Connect failed: " + error, "-", "—", "");

    /// <summary>Status after a successful init. The adapter label comes from the profile detected from ATI/STI/STDI, marked "(simulated)" on demo ports.</summary>
    public static ConnectionStatus Connected(PortInfo port, AdapterInfo info, AdapterProfile detected) => new(
        ConnectionState.Connected,
        $"Connected: {info.DeviceId ?? info.ElmId} | {info.StnId ?? "not STN"}",
        info.BatteryVolts is double v ? $"{v:0.0} V" : "?",
        AdapterLabel(port, detected),
        detected.CapabilityText);

    public static string AdapterLabel(PortInfo port, AdapterProfile detected)
    {
        if (port.Kind != PortKind.Virtual) return detected.DisplayName;
        var name = detected.Kind switch { AdapterKind.ObdLinkEx => "OBDLink EX", AdapterKind.ObdLinkMxPlus => "OBDLink MX+", _ => detected.DisplayName };
        return name + " (simulated)";
    }
}
