using ObdLink.Core.Elm;

namespace ObdLink.Core.Modules;

/// <summary>A module to scan: its table entry and the bus to try first (a remembered bus, or the table bus).</summary>
public sealed record ScanTarget(ModuleDefinition Table, Bus FirstBus);

/// <summary>
/// Scan outcome for one module. <see cref="AnsweredOn"/> is the bus it answered on (null if it did not). <see cref="FoundByRetry"/>
/// is true when it answered only on another bus, so the caller should remember that bus.
/// </summary>
public sealed record ScanOutcome(ModuleDefinition Table, bool Responded, Bus? AnsweredOn, bool FoundByRetry);

/// <summary>
/// v0.17.0: the module scan with alternate-bus retries, moved out of the view model so it is tested (and usable from a CLI).
/// Behaviour, unchanged from v0.3.2:
///  - Pass 1 scans every module on its first bus (remembered bus, else the table bus).
///  - If the adapter can switch buses (EX / MX+), each Ford-bus module that did not answer gets one try on each other Ford bus:
///    the table bus first, then the rest (<see cref="ModuleBusMemory.Remaining"/>, falling back to <see cref="ModuleReplies.Alternates"/>).
///    Retries are grouped by bus (HS-CAN, HS-CAN2, MS-CAN) so the adapter switches at most three times; a module is not tried
///    again once it has answered.
///  - Modules without an address, or on a non-Ford bus, are never retried.
/// </summary>
public static class ModuleScanFlow
{
    public delegate Task<List<ScanResult>> ScanFn(IReadOnlyList<ModuleDefinition> modules, IProgress<ScanResult>? progress, CancellationToken ct);

    private static readonly Bus[] RetryOrder = { Bus.HsCan, Bus.HsCan2, Bus.MsCan };
    private static bool IsFordBus(Bus b) => b is Bus.HsCan or Bus.MsCan or Bus.HsCan2;

    public static async Task<IReadOnlyList<ScanOutcome>> RunAsync(ScanFn scan, IReadOnlyList<ScanTarget> targets, bool canSwitchBus,
        IProgress<ScanResult>? firstPassProgress = null, Action<string>? log = null, CancellationToken ct = default)
    {
        var byTx = new Dictionary<uint, ScanTarget>();
        foreach (var t in targets) byTx.TryAdd(t.Table.TxId, t);
        var answered = new Dictionary<uint, (Bus Bus, bool Retry)>();

        var first = await scan(targets.Select(t => ModuleBusMemory.ForScan(t.Table, t.FirstBus)).ToList(), firstPassProgress, ct);
        foreach (var r in first.Where(r => r.Responded)) answered.TryAdd(r.Module.TxId, (r.Module.Bus, false));

        if (canSwitchBus)
        {
            var jobs = new List<(uint Tx, Bus Alt, ModuleDefinition Def)>();
            foreach (var miss in first.Where(r => !r.Responded && r.Module.HasAddress && IsFordBus(r.Module.Bus)))
            {
                if (!byTx.TryGetValue(miss.Module.TxId, out var t)) continue;
                var next = ModuleBusMemory.Remaining(miss.Module.Bus, t.Table.Bus);
                if (next.Length == 0) next = ModuleReplies.Alternates(miss.Module.Bus);
                foreach (var alt in next) jobs.Add((miss.Module.TxId, alt, miss.Module with { Bus = alt }));
            }
            foreach (var bus in RetryOrder)
            {
                var retry = jobs.Where(j => j.Alt == bus && !answered.ContainsKey(j.Tx)).Select(j => j.Def).ToList();
                if (retry.Count == 0) continue;
                foreach (var r in await scan(retry, null, ct))
                    if (r.Responded && answered.TryAdd(r.Module.TxId, (r.Module.Bus, true)))
                        log?.Invoke($"{r.Module.Acronym} 0x{r.Module.TxId:X3} answered on {ModuleReplies.BusName(r.Module.Bus)}");
            }
        }

        return targets.Select(t => answered.TryGetValue(t.Table.TxId, out var a)
            ? new ScanOutcome(t.Table, true, a.Bus, a.Retry)
            : new ScanOutcome(t.Table, false, null, false)).ToList();
    }
}
