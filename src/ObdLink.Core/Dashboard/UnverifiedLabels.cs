namespace ObdLink.Core.Dashboard;

/// <summary>
/// Visible "UNVERIFIED" wording for Ford Mode 22 values that Dale has not confirmed on the truck.
/// Tire-pressure DIDs 2813-2816 are verified and must not go through <see cref="Mark"/> with verified false.
/// Gear DID 1E12 and OSS DID 1E13 stay unverified; do not invent a replacement DID and call it verified.
/// </summary>
public static class UnverifiedLabels
{
    public const string MarkWord = "UNVERIFIED";

    public static string GearTitle => "Gear (UNVERIFIED)";

    public static string Mark(string name, bool verified) => verified ? name : $"{name} ({MarkWord})";

    public static string GearFromDid(string header, string did) =>
        $"PCM {header} DID {did} — {MarkWord}. Compare with the cluster (P, R, N, D and the gears) before trusting it.";

    public static string GearFromEstimate() =>
        $"Estimated from RPM/speed (6R80 ratios). Gear DID 1E12 is {MarkWord} and did not answer.";

    /// <summary>OSS source line. <paramref name="did"/> is the table value (1E13 today) and is still unverified.</summary>
    public static string OssSource(uint tx, string did)
    {
        var who = tx == 0x7E1 ? "TCM" : tx == 0x7E0 ? "PCM" : "Module";
        return $"{who} 0x{tx:X3} DID {did} — {MarkWord}. PCM is asked first, then TCM.";
    }
}
