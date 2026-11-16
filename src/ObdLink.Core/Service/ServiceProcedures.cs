using System.Text;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Service;

/// <summary>How much the identifiers in a procedure are backed by evidence.</summary>
public enum ProcedureConfidence
{
    /// <summary>From Ford service information or a captured run on this vehicle model, and run on the truck (Phase 3 D2).</summary>
    Verified,
    /// <summary>From a public community source (forum post quoting a Ford tool, etc.). Not run on a 2017 F-150 by this project.</summary>
    Community,
    /// <summary>Inferred by this project from related evidence (e.g. the same routine on another model year). Never sent without the UNVERIFIED phrase.</summary>
    Synthesized,
    /// <summary>The procedure is known to exist, but no source gives its identifiers. It cannot be run until they are captured.</summary>
    NeedsCapture,
}

public enum ProcedureStepKind
{
    /// <summary>10 xx.</summary>
    Session,
    /// <summary>31 01 rid [option].</summary>
    StartRoutine,
    /// <summary>31 03 rid, repeated: reports the module's status bytes. A module that can't report results is not a failure.</summary>
    RequestResults,
    /// <summary>A pause (the session is kept alive by the caller's session host).</summary>
    Wait,
}

public sealed record ProcedureStep(ProcedureStepKind Kind, string Text, ushort? RoutineId = null, byte[]? Option = null, UdsSession? Session = null, TimeSpan? Time = null);

/// <summary>Where a procedure's identifiers or conditions come from. Quotes stay short; the link is the reference.</summary>
public sealed record ProcedureSource(string What, string Url);

/// <summary>
/// One service procedure (FORScan "Service Procedures" / Ford IDS "Toolbox" equivalent). Everything a user needs to decide is
/// on the record: what it does, when to use it and when not, the conditions, what to do afterwards, a tool-free method where
/// Ford documents one, and where every identifier comes from.
/// </summary>
public sealed record ServiceProcedure(
    string Key, string Name, string Module, uint TxId, string Purpose,
    IReadOnlyList<string> UseWhen, IReadOnlyList<string> Conditions, IReadOnlyList<string> After, IReadOnlyList<string> Warnings,
    IReadOnlyList<ProcedureStep> Steps, ProcedureConfidence Confidence, IReadOnlyList<ProcedureSource> Sources,
    string AppliesTo, IReadOnlyList<string>? ManualMethod = null, string? Gate = null)
{
    /// <summary>True when the steps contain a routine to send and its identifiers are not missing.</summary>
    public bool Runnable => Confidence != ProcedureConfidence.NeedsCapture && Steps.Any(s => s.Kind == ProcedureStepKind.StartRoutine);

    /// <summary>The phrase typed to send it: RUN for verified procedures, UNVERIFIED for everything else.</summary>
    public string Phrase => Confidence == ProcedureConfidence.Verified ? "RUN" : "UNVERIFIED";

    /// <summary>The requests the procedure sends, in order (for the confirmation and the docs).</summary>
    public string RequestSummary => string.Join(", ", Steps.Select(s => s.Kind switch
    {
        ProcedureStepKind.Session => $"10 {(byte)s.Session!.Value:X2}",
        ProcedureStepKind.StartRoutine => $"31 01 {s.RoutineId!.Value >> 8:X2} {s.RoutineId.Value & 0xFF:X2}" + (s.Option is { Length: > 0 } o ? " " + Convert.ToHexString(o) : ""),
        ProcedureStepKind.RequestResults => $"31 03 {s.RoutineId!.Value >> 8:X2} {s.RoutineId.Value & 0xFF:X2}",
        _ => $"wait {s.Time!.Value.TotalSeconds:0} s",
    }));

    /// <summary>Confirmation steps: what and when, conditions, provenance, then the typed phrase.</summary>
    public IReadOnlyList<ConfirmationStep> ConfirmSteps(string moduleName)
    {
        if (!Runnable) throw new InvalidOperationException($"{Name}: {NotRunnableReason}");
        var why = new StringBuilder($"{Purpose}\n\nUse only when:\n");
        foreach (var u in UseWhen) why.Append("• ").Append(u).Append('\n');
        foreach (var w in Warnings) why.Append("⚠ ").Append(w).Append('\n');
        var cond = new StringBuilder();
        foreach (var c in Conditions) cond.Append("• ").Append(c).Append('\n');
        string src = Confidence switch
        {
            ProcedureConfidence.Verified => "The identifiers were verified on this vehicle model.",
            ProcedureConfidence.Community => "The identifiers come from a public community source and have NOT been verified on your model year by this project. The module may refuse them (nothing happens) or, rarely, do something else.",
            _ => "The identifiers were INFERRED by this project, not taken from a source. Only for a bench module or if you accept the risk.",
        };
        var steps = new List<ConfirmationStep>();
        // The gate is a plain yes/no question asked first: No (or closing the dialog) stops before anything else is shown or sent.
        if (Gate is { Length: > 0 } g)
            steps.Add(new ConfirmationStep($"{Name}: check", $"{g}\n\nYes continues. No stops here: {Name} is only for the case above."));
        steps.AddRange(new[]
        {
            new ConfirmationStep($"{Name}: when", why.ToString().TrimEnd()),
            new ConfirmationStep($"{Name}: conditions", cond.ToString().TrimEnd()),
            new ConfirmationStep($"{Name}: send", $"{src}\nSources: {string.Join("; ", Sources.Select(s => s.Url))}\n\n{RequestSummary} to {moduleName}. Type {Phrase} to send it.", Phrase),
        });
        return steps;
    }

    public string NotRunnableReason => Confidence == ProcedureConfidence.NeedsCapture
        ? "no public source gives this procedure's identifiers yet. It can be added after a capture on the truck (see docs/SERVICE-PROCEDURES.md)."
        : "it has no routine to send.";
}

/// <summary>
/// The service procedure catalog. Generated into docs/SERVICE-PROCEDURES.md. Rules:
/// <list type="bullet">
/// <item>An identifier comes from a cited source, a capture on the truck, or is marked <see cref="ProcedureConfidence.Synthesized"/>.
/// Nothing is guessed silently.</item>
/// <item>A procedure without identifiers is still listed (what it is, when it's needed, a documented tool-free method if any)
/// as <see cref="ProcedureConfidence.NeedsCapture"/>: it can't be run.</item>
/// <item>Pyrotechnic (E200-E2FF) and programming (FF00/FF01) routines are refused here as everywhere (<see cref="UdsRoutines.RefusalReason"/>).</item>
/// </list>
/// </summary>
public static class ServiceProcedures
{
    private static readonly ProcedureStep Extended = new(ProcedureStepKind.Session, "extended diagnostic session", Session: UdsSession.Extended);

    private const string F150BmsDet = "https://www.f150forum.com/f38/battery-management-system-reset-109656/";
    private const string FocusBmsDet = "https://www.focusfanatics.com/threads/low-battery-audio-off-message.673337/";
    private const string BmsManualF150 = "https://www.f150forum.com/f118/forscan-reset-bms-code-437277/";
    private const string BmsTsbCharger = "https://static.nhtsa.gov/odi/tsbs/2024/MC-10249755-0001.pdf";
    private const string BmsResetOnlyNewBattery = "https://forum.forscan.org/viewtopic.php?p=84356";
    private const string TpmsTraining = "https://www.brakeandfrontend.com/ford-tpms-relearn-training-procedures/";
    private const string TcmKamTsb = "https://www.vehicleservicepros.com/tsbs/article/53092343/ford-trans-relearn-2014-11-10";
    private const string KamPced = "https://fordraptorforum.com/threads/pcm.23710";
    private const string SasForum = "https://www.fordtremor.com/threads/steering-wheel-angle-sensor-calibration.20048/";
    private const string AbsBleedForum = "https://www.ford-trucks.com/forums/1695552-no-more-abs-service-bleed-function-in-forscan.html";

    public static readonly IReadOnlyList<ServiceProcedure> All = new[]
    {
        new ServiceProcedure("bcm-bms-reset", "Battery Monitoring System (BMS) reset", "BCM", 0x726,
            "Tells the BCM a new battery is installed: battery state of charge back to its default (about 85 %), drain timers and learned battery data reset.",
            UseWhen: new[] { "A new battery was just installed (fully charged), or Ford service information tells you to." },
            Conditions: new[]
            {
                "New battery fully charged; charger DISCONNECTED before the reset (Ford TSB wording).",
                "Ignition ON, engine OFF, doors closed.",
                "Afterwards leave the truck undisturbed for about 8 hours if possible.",
            },
            After: new[] { "The battery light may flash to confirm. Check for load-shed messages over the next few days." },
            Warnings: new[]
            {
                "Not after a simple charge: the reset clears the battery's learned aging data, which is only right for a new battery.",
                "Battery type (flooded / AGM / EFB) is NOT set by this procedure. If you changed type, check the BCM As-Built battery type first [UNVERIFIED location].",
            },
            Steps: new[] { Extended, new ProcedureStep(ProcedureStepKind.StartRoutine, "start BMS reset", RoutineId: 0x201A) },
            Confidence: ProcedureConfidence.Community,
            Sources: new[]
            {
                new ProcedureSource("Ford engineer's post: BCM 726, extended session, routine 201A with Ford's Diagnostic Engineering Tool (2011 F-150)", F150BmsDet),
                new ProcedureSource("Same procedure quoted for other Ford models", FocusBmsDet),
                new ProcedureSource("Ford TSB: disconnect the charger, then perform the BMS reset", BmsTsbCharger),
                new ProcedureSource("Ford service note: reset only for a new battery", BmsResetOnlyNewBattery),
                new ProcedureSource("Tool-free method (high beams 5x, brake 3x)", BmsManualF150),
            },
            AppliesTo: "Ford with BMS (2011+). Routine reported for the 2011 F-150 BCM; not verified on the 2017 BCM.",
            Gate: "Did you disconnect and re-connect battery recently?",
            ManualMethod: new[]
            {
                "Ignition ON, engine OFF.",
                "Within 10 seconds: flash the high beams 5 times (ending with them off), then press and release the brake pedal 3 times.",
                "The battery warning light flashes 3 times within about 15 seconds when the reset worked.",
            }),

        new ServiceProcedure("bcm-tpms-training", "TPMS sensor training", "BCM", 0x726,
            "Teaches the BCM which tire pressure sensor sits at which wheel.",
            UseWhen: new[] { "After a tire rotation, a sensor or wheel replacement, or a BCM replacement." },
            Conditions: new[] { "Parked, doors closed, no RF noise nearby. Vehicle-speed DTCs fixed first." },
            After: new[] { "The horn sounds twice when all wheels are trained." },
            Warnings: Array.Empty<string>(),
            Steps: Array.Empty<ProcedureStep>(),
            Confidence: ProcedureConfidence.NeedsCapture,
            Sources: new[] { new ProcedureSource("Ford tool-free training procedure", TpmsTraining) },
            AppliesTo: "Ford with TPMS (BCM-hosted).",
            ManualMethod: new[]
            {
                "Steps vary by model year: your owner's manual is authoritative.",
                "Ignition OFF, then OFF → RUN three times, ending in RUN (no more than 2 minutes between cycles).",
                "Press and release the brake pedal, then ignition OFF.",
                "OFF → RUN three times again, ending in RUN: the horn sounds once and the TPMS light flashes (training mode).",
                "Starting at the left front and going clockwise, let air out of each tire (or use a TPMS activation tool) until the horn sounds.",
                "After the last tire the horn sounds twice. Re-inflate to the placard pressure.",
            }),

        new ServiceProcedure("pcm-kam-reset", "PCM Keep Alive Memory (KAM) reset", "PCM", 0x7E0,
            "Returns the PCM's adaptive learning (fuel trims, idle air, adaptive airflow) to defaults.",
            UseWhen: new[] { "After repairs that invalidate learned values (injectors, MAF, intake leaks), when service information says so." },
            Conditions: new[] { "Ignition ON, engine OFF." },
            After: new[] { "Idle at operating temperature with the A/C off for about 2 minutes, then drive normally; driveability may be odd until it relearns." },
            Warnings: new[] { "Clearing DTCs does not reset KAM; a KAM reset also clears the idle air trim table." },
            Steps: Array.Empty<ProcedureStep>(),
            Confidence: ProcedureConfidence.NeedsCapture,
            Sources: new[] { new ProcedureSource("Ford PCED text on KAM reset (quoted)", KamPced) },
            AppliesTo: "Ford PCM."),

        new ServiceProcedure("tcm-kam-reset", "TCM Keep Alive Memory reset / clear adaptive tables", "TCM", 0x7E1,
            "Clears the transmission's learned shift adaptations.",
            UseWhen: new[] { "After transmission repair, valve body or TCM replacement, fluid service where Ford asks for it, or a shift-quality TSB." },
            Conditions: new[] { "Ignition ON, engine OFF." },
            After: new[] { "Key OFF for at least 2 minutes, then the adaptive learning drive cycle on a level road (fluid ≥ 175 °F)." },
            Warnings: new[] { "TCM KAM reset and \"clear adaptive tables\" are separate functions in Ford's tools; a TSB requires TCM KAM first. Disconnecting the battery does not clear TCM KAM. Do not clear PCM KAM for this." },
            Steps: Array.Empty<ProcedureStep>(),
            Confidence: ProcedureConfidence.NeedsCapture,
            Sources: new[] { new ProcedureSource("Ford TSB (transmission relearn): TCM KAM vs adaptive tables", TcmKamTsb) },
            AppliesTo: "6R80 / 10R80. On the 2017 F-150 3.5L NA (6R80) the TCM may be inside the PCM: the address is [UNVERIFIED]."),

        new ServiceProcedure("sas-calibration", "Steering angle sensor calibration", "ABS", 0x760,
            "Zeroes the steering angle so 0° means straight ahead (stability control, lane keeping).",
            UseWhen: new[] { "After an alignment, steering column or clock spring work, or an ABS/PSCM replacement; DTC C0051-type steering angle faults." },
            Conditions: new[] { "Alignment correct, wheels and steering wheel straight, vehicle level and stopped, engine running or ignition ON per procedure." },
            After: new[] { "Drive straight and check the steering wheel and stability-control light." },
            Warnings: new[] { "Some trucks host this in the PSCM (0x730) or SCCM instead of the ABS." },
            Steps: Array.Empty<ProcedureStep>(),
            Confidence: ProcedureConfidence.NeedsCapture,
            Sources: new[] { new ProcedureSource("Community description (ABS/SCCM and PSCM procedures)", SasForum) },
            AppliesTo: "2015+ F-150 with EPAS."),

        new ServiceProcedure("pscm-pull-drift-reset", "PSCM pull-drift compensation reset", "PSCM", 0x730,
            "Clears the steering's learned pull-drift compensation.",
            UseWhen: new[] { "After an alignment or suspension change, when the truck pulls because the PSCM learned the old fault." },
            Conditions: new[] { "Alignment correct first. Ignition ON, engine OFF unless the procedure says otherwise." },
            After: new[] { "Drive on a straight road so it relearns." },
            Warnings: Array.Empty<string>(),
            Steps: Array.Empty<ProcedureStep>(),
            Confidence: ProcedureConfidence.NeedsCapture,
            Sources: new[] { new ProcedureSource("Community description", SasForum) },
            AppliesTo: "2015+ F-150 with EPAS."),

        new ServiceProcedure("abs-service-bleed", "ABS service bleed", "ABS", 0x760,
            "Cycles the ABS valves and pump so trapped air leaves the hydraulic unit during a brake bleed.",
            UseWhen: new[] { "After the ABS hydraulic unit was opened or air reached it." },
            Conditions: new[] { "Brake bleeding set up (second person or pressure bleeder), reservoir full, ignition ON, engine OFF." },
            After: new[] { "Bleed the brakes again; firm pedal before driving." },
            Warnings: new[] { "Reported missing from FORScan for some 2015+ F-150 ABS modules." },
            Steps: Array.Empty<ProcedureStep>(),
            Confidence: ProcedureConfidence.NeedsCapture,
            Sources: new[] { new ProcedureSource("Community report", AbsBleedForum) },
            AppliesTo: "Ford ABS."),
    };

    public static ServiceProcedure? Find(string key) => All.FirstOrDefault(p => p.Key == key);

    /// <summary>Catalog checks (also run by the tests): unique keys, no refused routine, identifiers only where a source is cited.</summary>
    public static IEnumerable<string> Problems(IEnumerable<ServiceProcedure>? catalog = null)
    {
        var list = (catalog ?? All).ToList();
        foreach (var dup in list.GroupBy(p => p.Key).Where(g => g.Count() > 1)) yield return $"duplicate key {dup.Key}";
        foreach (var p in list)
        {
            if (p.Sources.Count == 0) yield return $"{p.Key}: no source";
            if (p.Sources.Any(s => !s.Url.StartsWith("https://", StringComparison.Ordinal))) yield return $"{p.Key}: source is not an https link";
            foreach (var s in p.Steps.Where(s => s.RoutineId is ushort))
                if (UdsRoutines.RefusalReason(s.RoutineId!.Value) is string why) yield return $"{p.Key}: routine 0x{s.RoutineId:X4} is refused ({why})";
            if (p.Confidence == ProcedureConfidence.NeedsCapture && p.Steps.Count > 0) yield return $"{p.Key}: needs capture but has steps";
            if (p.Confidence != ProcedureConfidence.NeedsCapture && !p.Runnable) yield return $"{p.Key}: has identifiers but no routine";
        }
    }
}
