using ObdLink.Core.AsBuilt;
using ObdLink.Core.Dashboard;
using ObdLink.Core.Diagnostics;
using ObdLink.Core.Elm;
using ObdLink.Core.Obd;
using ObdLink.Core.Modules;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

public class V021Tests
{
    private static EnhancedPidTable Table() => EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson());

    [Fact]
    public void Tpms_entries_are_unverified_bcm_dids()
    {
        var t = Table();
        foreach (var (k, did) in new[] { ("tpms_lf", "2813"), ("tpms_rf", "2814"), ("tpms_rr", "2815"), ("tpms_lr", "2816") })
        {
            var e = t.Pids.Single(p => p.Key == k);
            Assert.Equal("726", e.Header); Assert.Equal(did, e.Did); Assert.False(e.Verified); Assert.True(e.Enabled);
            Assert.Equal("Pressure", e.Quantity.ToString());
        }
        // raw 0x02DA = 730 -> 730/3 + 22/3 = 250.67 kPa = 36.4 psi
        var lf = t.Pids.Single(p => p.Key == "tpms_lf");
        Assert.Equal(250.67, lf.Decode(new byte[] { 0x62, 0x28, 0x13, 0x02, 0xDA }), 1);
        Assert.Equal(36.4, Units.ConvertOnly(250.67, Quantity.Pressure, new UnitPrefs()), 1);
    }

    [Fact]
    public async Task Simulator_serves_tpms_and_no_spare()
    {
        var sim = new SimulatedAdapter { Clock = () => 0 };
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync(); await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var eng = new DashboardEngine(new ElmDiagChannel(s), () => 0);
        await eng.DiscoverAsync();
        var keys = DashCatalog.PollKeys(new DashboardLayout { Items = { new() { Key = TpmsKeys.Widget, Display = DisplayKind.Tpms } } }, true);
        eng.Configure(keys, Table().Pids.Where(p => keys.Contains(p.Key)));
        var v = await eng.PollOnceAsync(); foreach (var kv in await eng.PollOnceAsync()) v[kv.Key] = kv.Value;
        Assert.Equal(SimulatedAdapter.TireBaseKpa[0], v["tpms_lf"], 0);
        Assert.Equal(SimulatedAdapter.TireBaseKpa[2], v["tpms_rr"], 0);
        Assert.False(v.ContainsKey("tpms_spare"));
        Assert.DoesNotContain(eng.Scheduler.Items, i => i.Key is "gear" or "tft" or "rpm"); // only displayed items polled
    }

    [Fact]
    public void Layout_ops_and_poll_keys()
    {
        var l = DashboardLayout.Default();
        Assert.Equal("rpm", l.Items[0].Key);
        Assert.True(l.Move(0, 1)); Assert.Equal("speed", l.Items[0].Key);
        Assert.False(l.Move(0, -1));
        Assert.True(l.MoveTo(0, l.Items.Count - 1)); Assert.Equal("speed", l.Items[^1].Key);
        Assert.False(l.Add("rpm", DisplayKind.Bar)); Assert.True(l.Add("baro", DisplayKind.Tile));
        Assert.True(l.Remove("coolant")); Assert.False(l.Remove("coolant"));
        var k = DashCatalog.PollKeys(l, true);
        Assert.Contains("mil", k); Assert.DoesNotContain("tpms_spare", k); Assert.Contains("tpms_lf", k); Assert.Contains("baro", k); Assert.DoesNotContain("coolant", k);
        Assert.DoesNotContain("tpms", k);
        var c = l.Clone("Towing"); c.Items.Clear(); Assert.NotEmpty(l.Items);
    }

    [Fact]
    public void Catalog_filters_unsupported_and_includes_ford()
    {
        var cat = DashCatalog.Build(Table(), new HashSet<byte> { 0x0C });
        Assert.Contains(cat, c => c.Key == "rpm"); Assert.DoesNotContain(cat, c => c.Key == "speed");
        Assert.Contains(cat, c => c.Key == "tft"); Assert.Contains(cat, c => c.Key == "tpms" && !c.Verified);
        Assert.Contains(cat, c => c.Key == "gear"); Assert.Single(cat, c => c.Key == "gear");
        Assert.Contains(cat, c => c.Key == "tpms_lf"); // single tires can also be tiles
    }

    [Fact]
    public void Settings_migrate_layouts()
    {
        var path = Path.Combine(Path.GetTempPath(), "set-" + Guid.NewGuid(), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"Dashboard\":{\"Gauges\":[\"rpm\",\"coolant\"]}}");
        var o = AppSettings.Load(path);
        Assert.Equal(new[] { "rpm", "coolant" }, o.Dashboard.Current().Items.Select(i => i.Key));
        Assert.Equal(207, o.Dashboard.TpmsLowKpa);
        o.Dashboard.Layouts.Add(o.Dashboard.Current().Clone("Towing")); o.Dashboard.ActiveLayout = "Towing";
        o.Dashboard.Current().Items[0].Display = DisplayKind.Tile; o.LastModule = "726";
        o.Save(path);
        var l = AppSettings.Load(path);
        Assert.Equal(DisplayKind.Tile, l.Dashboard.Current().Items[0].Display); Assert.Equal("726", l.LastModule);
        Assert.Contains("\"Tile\"", File.ReadAllText(path));
        Assert.Equal(DashboardLayout.Default().Items.Count, new AppSettings().Normalize().Dashboard.Current().Items.Count);
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    private static AsBuiltBlock Blk(byte b) => new() { Module = 0x726, Block = b, Data = new byte[] { 1, 2, 3, 4, 5 } };

    [Fact]
    public async Task Read_all_blocks_stops_at_first_missing()
    {
        var r = await AsBuiltBatch.ReadAllBlocksAsync((b, _) => AsBuiltAddress.FromBcd(b) <= 3 ? Task.FromResult(Blk(b)) : throw new IOException("NRC 31"), 16, default);
        Assert.Equal(3, r.Count);
        await Assert.ThrowsAsync<IOException>(() => AsBuiltBatch.ReadAllBlocksAsync((b, _) => throw new IOException("no"), 16, default));
    }

    [Fact]
    public async Task Batch_continues_on_failure_saves_and_cancels()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ab-" + Guid.NewGuid());
        var now = new DateTime(2026, 10, 24, 9, 30, 0);
        var day = AsBuiltBatch.DateFolder(dir, now); Assert.EndsWith("2026-10-24", day);
        var mods = new[] { 0x726, 0x7E0, 0x720 }.Select(a => new ModuleDefinition("M" + a.ToString("X"), "m", Bus.HsCan, (uint)a, (uint)a + 8, Confidence.Standard)).ToList();
        var prog = new List<BatchProgress>();
        var res = await AsBuiltBatch.RunAsync(mods, async (m, ct) =>
        {
            if (m.TxId == 0x7E0) throw new IOException("no response");
            var p = await AsBuiltBatch.SaveAsync(day, "XXXXXXXXXXX123456", (ushort)m.TxId, new[] { Blk(0x01) }, now, ct);
            return new BatchItemResult(m, true, "1 block", p);
        }, new SyncProgress(prog), default);
        Assert.Equal(new[] { true, false, true }, res.Select(r => r.Ok));
        Assert.Equal(3, prog[^1].Done);
        var found = AsBuiltBatch.FindBackups(dir);
        Assert.True(found.ContainsKey(0x726)); Assert.True(found.ContainsKey(0x720)); Assert.False(found.ContainsKey(0x7E0));
        Assert.DoesNotContain("1FTEX", File.ReadAllText(found[0x726].Path));
        using var cts = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AsBuiltBatch.RunAsync(mods, (m, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.FromResult(new BatchItemResult(m, true, "")); }, null, cts.Token));
        Directory.Delete(dir, true);
    }

    private sealed class SyncProgress(List<BatchProgress> l) : IProgress<BatchProgress> { public void Report(BatchProgress v) => l.Add(v); }
}
