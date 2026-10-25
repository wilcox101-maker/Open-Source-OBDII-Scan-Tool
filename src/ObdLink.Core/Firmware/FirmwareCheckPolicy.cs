namespace ObdLink.Core.Firmware;

/// <summary>
/// v0.4.0: DEMO / simulated sessions never query OBDLink's firmware feed. That includes first-run demo and
/// Ctrl+Alt+F. A real adapter still checks when the setting is on, or when the user asks. The app only downloads;
/// it never installs firmware and never bundles a firmware file.
/// </summary>
public static class FirmwareCheckPolicy
{
    public static bool ShouldQuery(bool simulatedAdapter, bool settingEnabled, bool force) =>
        !simulatedAdapter && (settingEnabled || force);
}
