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

/// <summary>
/// KB-R4: shared vehicle configuration ("ECC", Enhanced Central Configuration) in the BdyCM. Other modules (PCM, ABS, PSCM, IPC,
/// …) learn these values; after a change, Ford's "Module Initialization / Relearn Vehicle Data" must run on them, or they set
/// U2100 / U2101 and keep using the old values (speedometer, ABS, steering assist). Source: FORScan "How to manage module
/// configuration" (https://forum.forscan.org/viewtopic.php?p=59999). This app has no relearn yet, so a write or restore that
/// changes one of these lines needs a typed RELEARN: the user confirms they'll run the relearn with another tool right after.
/// <para>Which lines: the BdyCM lines both community-sheet BCM tabs (BdyCM-old for HU5T, JU5T) name as VIN, axle ratio / tire
/// circumference and steering side / gearbox (community-db/f150-asbuilt). [UNVERIFIED for other model years; 2015-2020 F-150.]</para>
/// </summary>
public static class AsBuiltEccPolicy
{
    public const ushort BdyCm = 0x726;
    public const string Phrase = "RELEARN";
    public const string Source = "https://forum.forscan.org/viewtopic.php?p=59999";

    /// <summary>ECC lines in the BdyCM, as BCD block/line bytes, with what they hold.</summary>
    public static readonly IReadOnlyDictionary<(byte Block, byte Line), string> Lines = new Dictionary<(byte, byte), string>
    {
        [(0x09, 0x01)] = "VIN", [(0x09, 0x02)] = "VIN", [(0x10, 0x01)] = "VIN", [(0x10, 0x02)] = "VIN", [(0x11, 0x01)] = "VIN",
        [(0x12, 0x01)] = "axle ratio / tire circumference",
        [(0x13, 0x01)] = "steering side / gearbox type",
    };

    /// <summary>The ECC lines whose bytes differ between <paramref name="before"/> and <paramref name="after"/> (same module/block).</summary>
    public static IReadOnlyList<(AsBuiltAddress Address, string What)> Changed(AsBuiltBlock before, AsBuiltBlock after)
    {
        if (before.Module != BdyCm) return Array.Empty<(AsBuiltAddress, string)>();
        var old = before.Lines().ToDictionary(l => l.Address);
        return after.Lines()
            .Where(l => Lines.ContainsKey((l.Address.Block, l.Address.Line)) && (!old.TryGetValue(l.Address, out var o) || !o.Data.AsSpan().SequenceEqual(l.Data)))
            .Select(l => (l.Address, Lines[(l.Address.Block, l.Address.Line)]))
            .ToList();
    }

    /// <summary>The extra typed step when ECC lines change; none otherwise.</summary>
    public static IEnumerable<ConfirmationStep> Steps(IReadOnlyList<(AsBuiltAddress Address, string What)> changed)
    {
        if (changed.Count == 0) yield break;
        var list = string.Join(", ", changed.Select(c => $"{c.Address} ({c.What})"));
        yield return new ConfirmationStep("Shared vehicle configuration (ECC)",
            $"This changes shared vehicle configuration in the body control module: {list}.\n\n"
            + "Other modules (PCM, ABS, steering, cluster) learn these values. Without Ford's \"Module Initialization / Relearn Vehicle Data\" "
            + "they set U2100 / U2101 and keep using the old values (speedometer, ABS, steering assist). This app can't run that relearn yet.\n\n"
            + $"Type {Phrase} only if you will run the relearn with FORScan or a Ford tool right after this. Source: {Source}", Phrase);
    }
}
