namespace ObdLink.Core.Vehicles;

/// <summary>When a DEMO profile must be dropped, and when Identify has to rebuild the module list.</summary>
public static class VehicleSelection
{
    public readonly record struct IdentifyApply(bool SaveProfile, bool RebuildModules, string? SelectLabel);

    /// <summary>A real port or a real adapter must not keep a profile captured on the simulator.</summary>
    public static bool DropDemoProfile(bool realConnectPath, bool profileLooksDemo) => realConnectPath && profileLooksDemo;

    public static IdentifyApply AfterIdentify(string currentLabel, string profileLabel, bool vinRead, int moduleCount)
    {
        if (vinRead) return new(true, true, profileLabel);
        if (moduleCount > 0) return new(false, true, null);
        return new(false, currentLabel.Length < 0, null);
    }
}
