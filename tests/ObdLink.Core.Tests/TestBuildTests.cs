using System.IO.Compression;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Diagnostics;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Modules;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

public class TestBuildTests
{
    private sealed class Yes : IConfirmationProvider { public Task<bool> ConfirmAsync(ConfirmationStep s, CancellationToken ct = default) => Task.FromResult(true); }

    private static async Task<(SimulatedAdapter, AdapterSession, ElmDiagChannel)> Setup()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, s, new ElmDiagChannel(s));
    }

    [Fact]
    public void Settings_default_is_read_only_and_corrupt_file_falls_back()
    {
        Assert.True(new AppSettings().ReadOnlyTestMode);
        var p = Path.GetTempFileName(); File.WriteAllText(p, "{not json");
        Assert.True(AppSettings.Load(p).ReadOnlyTestMode);
        new AppSettings { ReadOnlyTestMode = false }.Save(p);
        Assert.False(AppSettings.Load(p).ReadOnlyTestMode);
    }

    [Fact]
    public async Task Read_only_gate_blocks_asbuilt_write_before_any_traffic()
    {
        var (sim, s, ch) = await Setup();
        sim.Dids[0x726] = new() { [0xDE00] = new byte[] { 1, 2, 3, 4, 5, 6, 7 } };
        var guard = new SafetyGuard(s, new Yes(), gate: new WriteGate(() => true));
        var svc = new AsBuiltService(m => new ObdLink.Core.Uds.UdsClient(ch, m), guard, Path.Combine(Path.GetTempPath(), "ro_" + Guid.NewGuid()));
        var sent = new List<string>(); s.Link.Traffic += (d, t) => { if (d == ">>") sent.Add(t); };
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => svc.WriteBlockAsync("V", new AsBuiltBlock { Module = 0x726, Block = 1, Data = new byte[] { 9, 9, 9, 9, 9, 9, 9 } }));
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => guard.RequireAsync(SafetyGuard.WriteSteps("clear DTCs")));
        Assert.DoesNotContain(sent, c => c.Replace(" ", "").StartsWith("2E"));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Console_read_only_overrides_allow_writes()
    {
        var (_, s, _) = await Setup();
        var con = new RawConsoleService(s.Link) { AllowWrites = true, ReadOnlyMode = true };
        foreach (var c in new[] { "2E DE00 01", "14 FF FF FF", "04", "11 01", "31 01 00" })
            Assert.StartsWith("BLOCKED: read-only", await con.ExecuteAsync(c));
        Assert.DoesNotContain("BLOCKED", await con.ExecuteAsync("ATRV"));
    }

    [Fact]
    public void Ram_profile_sweeps_7E0_to_7EF_on_hs_can()
    {
        var m = DodgeRam1500_2008.Modules;
        Assert.Equal(Enumerable.Range(0x7E0, 16).Select(i => (uint)i), m.Select(x => x.TxId));
        Assert.All(m, x => Assert.Equal(Bus.HsCan, x.Bus));
        Assert.All(m.Skip(1), x => Assert.Equal(Confidence.Unverified, x.Confidence));
    }

    [Fact]
    public async Task Discovery_sweep_logs_responding_ids()
    {
        var (sim, s, ch) = await Setup();
        sim.PresentModules.Clear(); sim.PresentModules.Add(0x620); sim.PresentModules.Add(0x7E0);
        var hits = await new DiscoverySweep(s, ch).RunAsync(new DiscoveryOptions { StartId = 0x61E, EndId = 0x622 });
        Assert.Equal(new uint[] { 0x620 }, hits.Select(h => h.RequestId).Distinct());
        Assert.Contains(hits, h => h.Probe.StartsWith("3E00") && h.PayloadHex == "7E00" && h.ResponseId == 0x628);
        Assert.Contains(hits, h => h.Probe.StartsWith("1A87") && h.PayloadHex.StartsWith("7F1A"));
        await Assert.ThrowsAsync<ArgumentException>(() => new DiscoverySweep(s, ch).RunAsync(new DiscoveryOptions { StartId = 0x700, EndId = 0x800 }));
    }

    [Fact]
    public async Task Capture_writes_zip_with_raw_log_and_probes()
    {
        var (_, s, ch) = await Setup();
        var dir = Path.Combine(Path.GetTempPath(), "cap_" + Guid.NewGuid());
        var path = await new DiagnosticsCapture(s, ch).RunAsync(new("2008 Dodge Ram 1500", DodgeRam1500_2008.Modules, StnProtocol.HsCan11_500, '6',
            new DiscoveryOptions { StartId = 0x7E0, EndId = 0x7E1 }, "test"), dir);
        using var z = ZipFile.OpenRead(path);
        Assert.Equal(new[] { "options.json", "raw-traffic.log", "summary.txt" }, z.Entries.Select(e => e.Name).OrderBy(x => x));
        var raw = new StreamReader(z.GetEntry("raw-traffic.log")!.Open()).ReadToEnd();
        foreach (var c in new[] { ">> ATI", ">> STI", ">> STDI", ">> ATRV", ">> ATSP6", ">> ATSP2", ">> STP53", ">> 0100", ">> 3E00" }) Assert.Contains(c, raw);
        var sum = new StreamReader(z.GetEntry("summary.txt")!.Open()).ReadToEnd();
        Assert.Contains("PCM      0x7E0 RESPONDED", sum);
    }
}
