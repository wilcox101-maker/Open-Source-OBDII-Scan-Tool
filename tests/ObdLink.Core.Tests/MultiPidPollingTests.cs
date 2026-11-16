using ObdLink.Core.Dashboard;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Obd;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.14.0: multi-PID Mode 01 requests (J1979, up to 6 PIDs per request) in the live-data / dashboard poller.</summary>
public class MultiPidPollingTests
{
    [Fact]
    public void Parses_a_multi_pid_reply()
    {
        // 41 | 0C 0F A0 | 0D 32 | 05 5A | 01 82 07 E5 00
        var m = J1979.ParseMultiPid(new byte[] { 0x41, 0x0C, 0x0F, 0xA0, 0x0D, 0x32, 0x05, 0x5A, 0x01, 0x82, 0x07, 0xE5, 0x00 });
        Assert.Equal(new byte[] { 0x0C, 0x0D, 0x05, 0x01 }, m.Keys);
        Assert.Equal(new byte[] { 0x0F, 0xA0 }, m[0x0C]);
        Assert.Equal(new byte[] { 0x82, 0x07, 0xE5, 0x00 }, m[0x01]);
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x0C, 0x0F, 0xA0, 0x99, 0x01, 0x0D, 0x32 }, new byte[] { 0x0C })]   // unknown PID stops parsing
    [InlineData(new byte[] { 0x41, 0x0D, 0x32, 0x0C, 0x0F }, new byte[] { 0x0D })]                     // cut-short data
    [InlineData(new byte[] { 0x41, 0x0D, 0x32, 0x00, 0x00, 0x00 }, new byte[] { 0x0D })]               // padding
    [InlineData(new byte[] { 0x7F, 0x01, 0x12 }, new byte[0])]
    [InlineData(new byte[] { 0x41 }, new byte[0])]
    public void Partial_or_bad_replies(byte[] payload, byte[] pids) => Assert.Equal(pids, J1979.ParseMultiPid(payload).Keys);

    private static async Task<(SimulatedAdapter, DashboardEngine)> Engine(bool multi = true)
    {
        var sim = new SimulatedAdapter { Clock = () => 30, MultiPidSupported = multi };
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        long t = 0;
        var e = new DashboardEngine(new ElmDiagChannel(s), () => t);
        await e.DiscoverAsync();
        e.Configure(new[] { "rpm", "speed", "coolant", "throttle", "maf", "iat", "load" }, Array.Empty<EnhancedPid>());
        return (sim, e);
    }

    [Fact]
    public async Task Batches_up_to_six_pids_per_request_and_decodes_all()
    {
        var (sim, e) = await Engine();
        int before = sim.Sent.Count;
        var v = await e.PollOnceAsync();
        Assert.True(e.BatchObd);
        foreach (var k in new[] { "rpm", "speed", "coolant", "throttle", "maf", "iat", "load", "mil", "dtcCount" }) Assert.True(v.ContainsKey(k), k);
        // 8 items (7 PIDs + MIL) -> one batch of 6 and one of 2.
        Assert.Equal(2, e.Mode01Requests);
        Assert.Equal(2, sim.Sent.Count - before);
        Assert.Equal(1, v["mil"]);
    }

    [Fact]
    public async Task Batched_values_match_single_requests()
    {
        var (_, batched) = await Engine();
        var (_, single) = await Engine();
        single.BatchObd = false;
        var a = await batched.PollOnceAsync();
        var b = await single.PollOnceAsync();
        foreach (var k in b.Keys) Assert.Equal(b[k], a[k], 6);
    }

    [Fact]
    public async Task Ecu_without_multi_pid_support_falls_back_after_three_incomplete_batches()
    {
        var (_, e) = await Engine(multi: false);
        // 8 due items = two batches per cycle; this ECU answers only the first PID of each, so each batch is a miss.
        var first = await e.PollOnceAsync();
        Assert.Equal(9, first.Count);                    // every item still read (missing PIDs singly), + dtcCount
        Assert.True(e.BatchObd);                         // 2 misses so far
        foreach (var i in e.Scheduler.Items) i.NextDueMs = 0;
        await e.PollOnceAsync();                         // third miss in this cycle's first batch
        Assert.False(e.BatchObd);
        foreach (var i in e.Scheduler.Items) i.NextDueMs = 0;
        long before = e.Mode01Requests;
        var last = await e.PollOnceAsync();
        Assert.Equal(6, e.Mode01Requests - before);      // single requests only, scheduler cap of 6 per cycle
        Assert.True(last.ContainsKey("rpm"));
    }

    [Fact]
    public void Setting_defaults_on_and_old_settings_files_keep_it_on()
    {
        Assert.True(new ObdLink.Core.Safety.AppSettings().Dashboard.BatchObdRequests);
        var path = Path.Combine(Path.GetTempPath(), "obd-settings-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{\"Dashboard\":{\"RefreshMs\":250}}");
        try { Assert.True(ObdLink.Core.Safety.AppSettings.Load(path).Dashboard.BatchObdRequests); }
        finally { File.Delete(path); }
    }
}
