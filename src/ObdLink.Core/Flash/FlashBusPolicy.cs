namespace ObdLink.Core.Flash;

/// <summary>
/// KB-R1: whether a flash quiets the bus (<see cref="FlashEngine.QuietBus"/>: functional 10 83 / 85 82 / 28 83 01).
/// FORScan's module firmware update guide says some SYNC3 modules are bricked with "Stop activity on buses" enabled and tells
/// users to disable it for SYNC (https://forum.forscan.org/viewtopic.php?p=70209, troubleshooting). Which SYNC generations are
/// affected isn't stated, so every APIM (SYNC host, 0x7D0) flash leaves the bus talking [judgment: conservative]. Every other
/// module keeps the FORScan default (quiet).
/// </summary>
public static class FlashBusPolicy
{
    /// <summary>APIM (SYNC) request ID on Ford HS/MS-CAN.</summary>
    public const uint ApimTxId = 0x7D0;

    public const string Source = "https://forum.forscan.org/viewtopic.php?p=70209";

    /// <summary>True when the flash should quiet the bus; false for the APIM / SYNC module.</summary>
    public static bool QuietBus(uint txId, string? acronym = null) =>
        !(txId == ApimTxId || string.Equals(acronym, "APIM", StringComparison.OrdinalIgnoreCase)
          || (acronym?.Contains("SYNC", StringComparison.OrdinalIgnoreCase) ?? false));

    /// <summary>Why the bus is left alone, for the confirmation and the log.</summary>
    public static string NotQuietReason(uint txId) =>
        $"Module 0x{txId:X3} is the SYNC module (APIM). FORScan reports SYNC modules can be bricked when the other modules are told to stop sending, " +
        "so this flash leaves the bus as it is: other modules keep talking and may log U-codes during the flash. Clear them afterwards.";
}
