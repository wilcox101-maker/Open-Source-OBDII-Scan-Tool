using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.17.0: module scan with alternate-bus retries (moved from MainViewModel.ScanAsync).</summary>
public class ModuleScanFlowTests
{
    private static ModuleDefinition M(string a, uint tx, Bus bus) => new(a, a, bus, tx, tx + 8, Confidence.Standard);

    /// <summary>Fake vehicle: each module answers only on the buses listed for it. Records every scan call.</summary>
    private sealed class Vehicle
    {
        public Dictionary<uint, Bus[]> AnswersOn { get; } = new();
        public List<(Bus Bus, uint[] Tx)> Calls { get; } = new();
        public Task<List<ScanResult>> Scan(IReadOnlyList<ModuleDefinition> mods, IProgress<ScanResult>? p, CancellationToken ct)
        {
            foreach (var g in mods.GroupBy(m => m.Bus)) Calls.Add((g.Key, g.Select(m => m.TxId).ToArray()));
            var res = mods.Select(m => new ScanResult(m, AnswersOn.TryGetValue(m.TxId, out var b) && b.Contains(m.Bus), null)).ToList();
            foreach (var r in res) p?.Report(r);
            return Task.FromResult(res);
        }
    }

    [Fact]
    public async Task Modules_found_on_the_first_pass_are_not_retried()
    {
        var v = new Vehicle(); v.AnswersOn[0x7E0] = new[] { Bus.HsCan }; v.AnswersOn[0x726] = new[] { Bus.HsCan };
        var res = await ModuleScanFlow.RunAsync(v.Scan, new[] { new ScanTarget(M("PCM", 0x7E0, Bus.HsCan), Bus.HsCan), new ScanTarget(M("BCM", 0x726, Bus.HsCan), Bus.HsCan) }, canSwitchBus: true);
        Assert.All(res, r => Assert.True(r.Responded && !r.FoundByRetry && r.AnsweredOn == Bus.HsCan));
        Assert.Single(v.Calls);
    }

    [Fact]
    public async Task A_miss_is_retried_on_the_table_bus_first_then_the_others_grouped_by_bus()
    {
        var v = new Vehicle();
        v.AnswersOn[0x720] = new[] { Bus.MsCan };   // IPC: table MS, remembered HS (wrong)
        v.AnswersOn[0x7D0] = new[] { Bus.HsCan2 };  // APIM: table HS, really HS2
        var ipc = M("IPC", 0x720, Bus.MsCan); var apim = M("APIM", 0x7D0, Bus.HsCan);
        var logs = new List<string>();
        var res = await ModuleScanFlow.RunAsync(v.Scan, new[] { new ScanTarget(ipc, Bus.HsCan), new ScanTarget(apim, Bus.HsCan) }, true, log: logs.Add);
        Assert.Equal((true, (Bus?)Bus.MsCan, true), (res[0].Responded, res[0].AnsweredOn, res[0].FoundByRetry));
        Assert.Equal((true, (Bus?)Bus.HsCan2, true), (res[1].Responded, res[1].AnsweredOn, res[1].FoundByRetry));
        // Pass 1 on HS for both; retries grouped: HS2 then MS (the HS group is empty: both already tried there).
        Assert.Equal(new[] { Bus.HsCan, Bus.HsCan2, Bus.MsCan }, v.Calls.Select(c => c.Bus));
        Assert.DoesNotContain(v.Calls.Skip(2), c => c.Tx.Contains(0x7D0u)); // APIM found on HS2: not tried again on MS
        Assert.Equal(2, logs.Count);
    }

    [Fact]
    public async Task No_retries_when_the_adapter_cannot_switch_buses()
    {
        var v = new Vehicle(); v.AnswersOn[0x720] = new[] { Bus.MsCan };
        var res = await ModuleScanFlow.RunAsync(v.Scan, new[] { new ScanTarget(M("IPC", 0x720, Bus.MsCan), Bus.HsCan) }, canSwitchBus: false);
        Assert.False(res[0].Responded);
        Assert.Single(v.Calls);
    }

    [Fact]
    public async Task Modules_without_an_address_or_on_a_non_Ford_bus_are_not_retried()
    {
        var v = new Vehicle();
        var res = await ModuleScanFlow.RunAsync(v.Scan, new[]
        {
            new ScanTarget(M("X", 0, Bus.HsCan), Bus.HsCan),
            new ScanTarget(M("Y", 0x7A0, Bus.J1850Pwm), Bus.J1850Pwm),
        }, true);
        Assert.All(res, r => Assert.False(r.Responded));
        Assert.Equal(2, v.Calls.Count); // pass 1 only (two bus groups)
    }

    [Fact]
    public async Task First_pass_progress_is_reported()
    {
        var v = new Vehicle(); v.AnswersOn[0x7E0] = new[] { Bus.HsCan };
        var seen = new List<ScanResult>();
        var p = new SyncProgress(seen.Add);
        await ModuleScanFlow.RunAsync(v.Scan, new[] { new ScanTarget(M("PCM", 0x7E0, Bus.HsCan), Bus.HsCan), new ScanTarget(M("TCM", 0x7E1, Bus.HsCan), Bus.HsCan) }, true, p);
        Assert.Equal(2, seen.Count);
    }

    private sealed class SyncProgress(Action<ScanResult> a) : IProgress<ScanResult> { public void Report(ScanResult v) => a(v); }

    [Fact]
    public async Task Simulator_end_to_end_with_the_real_scanner()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()), initial: ObdLink.Core.Adapters.AdapterProfiles.OtherStn);
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var ch = new ElmDiagChannel(s);
        var targets = FordF150_2017.Modules.Where(m => m.HasAddress).Select(m => new ScanTarget(m, m.Bus)).ToList();
        var res = await ModuleScanFlow.RunAsync((mods, p, ct) => new ModuleScanner(s, ch).ScanAsync(mods, p, ct), targets, canSwitchBus: s.Profile.Has(ObdLink.Core.Adapters.AdapterCapabilities.MsCan));
        Assert.Contains(res, r => r.Table.TxId == 0x7E0 && r.Responded && r.AnsweredOn == Bus.HsCan);
        Assert.Equal(targets.Count, res.Count);
    }
}
