namespace ObdLink.Core.AsBuilt;

/// <summary>
/// Picks which community-sheet tab to use for each module when decoding a specific vehicle. Different tabs describe the same address differently; e.g. APIM 7D0-01-01 digit 1 is "Smart DSP" on the Sync 3 tabs but "Brand / ICAN" on Sync 1.
/// </summary>
public sealed record DecodeProfile(string Name, IReadOnlyDictionary<ushort, string> TabByModule)
{
    /// <summary>
    /// Dale's 2017 F-150. APIM = SYNC 1 (confirmed by owner 2026-10-23). BCM HU5T → "BCM (BdyCM)-old". ABS tab = 2015-2017.
    /// </summary>
    public static readonly DecodeProfile DaleF150_2017 = new("Dale 2017 F-150 (SYNC 1)", new Dictionary<ushort, string>
    {
        [0x7D0] = "APIM Sync 1",
        [0x726] = "BCM (BdyCM)-old",
        [0x760] = "ABS (2015-2017)",
    });

    /// <summary>Returns decodes that are for modules with a configured tab and come from that tab. Modules with no configured tab pass through unchanged.</summary>
    public IEnumerable<DecodedFeature> Filter(IEnumerable<DecodedFeature> decoded) =>
        decoded.Where(d => !TabByModule.TryGetValue(Convert.ToUInt16(d.Address[..3], 16), out var tab) || d.Tab == tab);
}
