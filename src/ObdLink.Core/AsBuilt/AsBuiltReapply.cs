namespace ObdLink.Core.AsBuilt;

/// <summary>Where a block stands against the user's last verified write of it.</summary>
public enum ReapplyState
{
    /// <summary>The module still holds the user's value: nothing to do.</summary>
    Kept,
    /// <summary>The module holds the value from before the user's change again (what a dealer update or OTA reload does).</summary>
    Reverted,
    /// <summary>The module holds something else: neither the user's value nor the one before it (e.g. a new factory default).</summary>
    Different,
}

/// <summary>One block: the user's last verified write, what the module holds now, and where that leaves it.</summary>
public sealed record ReapplyItem(AsBuiltChange Change, byte[] Now, ReapplyState State)
{
    public byte[] Wanted => Convert.FromHexString(Change.After);
    public string Line => State switch
    {
        ReapplyState.Kept => $"{Change.Where}: still as you set it on {Change.At.LocalDateTime:yyyy-MM-dd}.",
        ReapplyState.Reverted => $"{Change.Where}: back to the value before your change of {Change.At.LocalDateTime:yyyy-MM-dd} (a dealer update or OTA reload does this). Re-apply {Change.After}.",
        _ => $"{Change.Where}: now holds {Convert.ToHexString(Now)}, neither your value ({Change.After}) nor the one before it ({Change.Before}). Check before re-applying.",
    };
}

/// <summary>
/// KB-R5: Ford dealer software updates (FDRS) and OTA reloads re-apply the server's As-Built at the end, wiping user changes
/// (f150gen14 / FORScan forum reports, see claude scope doc R5). "Re-apply my changes" finds, per block of a module, the user's
/// last verified <b>write</b> for this vehicle and compares it with what the module holds now. A block whose newest verified change
/// is a restore (undo, recover, restore) is left alone: the user chose to go back. Writing goes through the normal As-Built write
/// path (backup, confirmations incl. ECC RELEARN, read-back); this class only plans.
/// </summary>
public static class AsBuiltReapply
{
    /// <summary>Per block, the newest verified change; only those that are writes for this vehicle (VIN tail) are returned.</summary>
    public static IReadOnlyList<AsBuiltChange> LastWrites(AsBuiltHistory history, ushort module, string vin)
    {
        var tail = AsBuiltHistory.VinTail(vin);
        return history.Load()
            .Where(c => c.Module == module && c.Outcome == AsBuiltOutcome.Verified)
            .GroupBy(c => c.Block)
            .Select(g => g.Last())
            .Where(c => c.Kind == "write" && c.VinTail == tail && c.After.Length > 0)
            .OrderBy(c => c.Block)
            .ToList();
    }

    public static ReapplyState Classify(AsBuiltChange c, byte[] now)
    {
        var hex = Convert.ToHexString(now);
        return hex == c.After ? ReapplyState.Kept : hex == c.Before ? ReapplyState.Reverted : ReapplyState.Different;
    }

    /// <summary>Reads each block that has a user write (reads only) and classifies it.</summary>
    public static async Task<IReadOnlyList<ReapplyItem>> PlanAsync(AsBuiltHistory history, ushort module, string vin,
        Func<ushort, byte, CancellationToken, Task<AsBuiltBlock>> read, CancellationToken ct = default)
    {
        var items = new List<ReapplyItem>();
        foreach (var c in LastWrites(history, module, vin))
        {
            var now = (await read(module, c.Block, ct)).Data;
            items.Add(new ReapplyItem(c, now, Classify(c, now)));
        }
        return items;
    }
}
