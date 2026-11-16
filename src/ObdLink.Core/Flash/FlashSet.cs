namespace ObdLink.Core.Flash;

/// <summary>What <see cref="FlashSet.Check"/> found for a set of VBF files meant for one module.</summary>
public sealed record FlashSetResult(FlashImage? Sbl, IReadOnlyList<FlashImage> Images, uint? TxId, IReadOnlyList<string> Problems, IReadOnlyList<string> Notes)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// v0.22.5: checks a Ford software set (secondary bootloader + strategy/EXE + calibration/DATA) before anything is flashed: one SBL at
/// most, every file for the same module, no image overlapping another, and every image's layout valid together with the SBL (the
/// same checks the engine runs). Offline; used by <c>obdcli vbf-info --file a.vbf --file b.vbf …</c>.
/// </summary>
public static class FlashSet
{
    public static FlashSetResult Check(IReadOnlyList<FlashImage> files)
    {
        var problems = new List<string>();
        var notes = new List<string>();
        var sbls = files.Where(f => f.IsSbl).ToList();
        var images = files.Where(f => !f.IsSbl).ToList();
        if (sbls.Count > 1) problems.Add($"{sbls.Count} SBL files ({string.Join(", ", sbls.Select(s => s.Name))}); a set has one secondary bootloader");
        var sbl = sbls.FirstOrDefault();
        if (images.Count == 0) problems.Add("No strategy or calibration file: only an SBL was given");

        var ids = files.Where(f => f.EcuTxId is not null).Select(f => f.EcuTxId!.Value).Distinct().ToList();
        uint? tx = ids.Count == 1 ? ids[0] : null;
        if (ids.Count > 1)
            problems.Add("The files are for different modules: " + string.Join(", ", files.Where(f => f.EcuTxId is not null).Select(f => $"{f.Name} → 0x{f.EcuTxId:X3}")));
        else if (ids.Count == 0) notes.Add("No file names its module (ecu_address); choose the module by hand and double-check it");

        if (sbl is null && images.Any(i => i.PartType is not null))
            notes.Add("No SBL in the set. Ford modules usually refuse the erase without one (the app then resets the module back; nothing is changed)");
        else if (sbl is not null) notes.Add($"SBL {sbl.Name} starts at 0x{sbl.CallAddress:X8}" + (sbl.CallAddress is null ? " (missing call address)" : ""));

        for (int i = 0; i < images.Count; i++)
            for (int j = i + 1; j < images.Count; j++)
                foreach (var a in images[i].Segments)
                    foreach (var b in images[j].Segments)
                        if (a.Address < (ulong)b.Address + b.Length && b.Address < (ulong)a.Address + a.Length)
                            problems.Add($"{images[i].Name} and {images[j].Name} both write 0x{Math.Max(a.Address, b.Address):X8}");

        foreach (var img in images)
            if (FlashImageChecks.Layout(new FlashPlan { TxId = tx ?? img.EcuTxId ?? 0x7E0, Image = img, Sbl = sbl }) is string bad)
                problems.Add($"{img.Name}: {bad}");

        // Community Ford flashers load the SBL once, then program the strategy (EXE) and then the calibration (DATA).
        var order = images.OrderBy(i => string.Equals(i.PartType, "EXE", StringComparison.OrdinalIgnoreCase) ? 0 : string.Equals(i.PartType, "DATA", StringComparison.OrdinalIgnoreCase) ? 1 : 2).ToList();
        if (order.Count > 1) notes.Add("Flash order: " + string.Join(" → ", order.Select(i => $"{i.Name} ({i.PartType ?? "?"})")) + ". The app flashes one image per job, each with the SBL");
        return new FlashSetResult(sbl, order, tx, problems.Distinct().ToList(), notes);
    }
}
