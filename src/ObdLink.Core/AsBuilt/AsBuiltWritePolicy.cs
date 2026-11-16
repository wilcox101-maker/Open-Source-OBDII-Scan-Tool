using ObdLink.Core.Safety;

namespace ObdLink.Core.AsBuilt;

/// <summary>
/// Extra rules for As-Built writes that FORScan handles with dedicated functions instead of raw As-Built editing.
/// <list type="bullet">
/// <item>Powertrain modules (PCM 0x7E0, TCM 0x7E1): their configuration includes the checksum-protected vehicle ID block
/// (VIN, tire size, axle ratio) and transmission characterization data. FORScan recalculates the VID checksum in its PCM VID
/// function and advises strongly against editing transmission characterization through As-Built. This app has neither function,
/// so a raw write to these modules needs an extra typed confirmation.</item>
/// <item>After a verified write: most modules apply As-Built after an ignition cycle, and shared (central) parameters can make
/// other modules report U2100/U2101 until they are relearned. <see cref="AfterWriteAdvice"/> says so.</item>
/// </list>
/// Source of the FORScan behaviour: forum.forscan.org "How to manage module configuration in FORScan" (sections 2, 4.3, 4.4).
/// </summary>
public static class AsBuiltWritePolicy
{
    public const ushort Pcm = 0x7E0, Tcm = 0x7E1;
    public const string PowertrainPhrase = "POWERTRAIN";

    public static bool IsPowertrain(ushort module) => module is Pcm or Tcm;

    /// <summary>Extra confirmations for a write or restore of <paramref name="module"/>; empty for most modules.</summary>
    public static IEnumerable<ConfirmationStep> ExtraSteps(ushort module, string what)
    {
        if (!IsPowertrain(module)) yield break;
        yield return new ConfirmationStep("Powertrain module",
            $"{what} targets the {(module == Pcm ? "PCM" : "TCM")}. Its configuration can hold checksum-protected vehicle ID data (VIN, tire size, axle ratio) "
            + "and transmission characterization. FORScan changes those only through dedicated functions that recalculate the checksums; a wrong byte here "
            + "can cause drivability or shifting faults. This app writes the raw bytes you entered (a backup is taken first). "
            + $"Type {PowertrainPhrase} to continue.", PowertrainPhrase);
    }

    /// <summary>What to do after a verified write or restore of <paramref name="module"/>.</summary>
    public static string AfterWriteAdvice(ushort module) =>
        "Next: turn the ignition OFF for at least 30 s, then ON, and Read the block again to confirm the module kept it. "
        + "If other modules then report U2100 / U2101 (configuration not programmed / incompatible), they need a relearn of the shared settings "
        + "(not available in this app yet) or restore this backup."
        + (IsPowertrain(module) ? " For the PCM / TCM also check for new powertrain DTCs before driving." : "");
}
