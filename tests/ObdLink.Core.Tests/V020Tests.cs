using System.Net;
using System.Net.Http;
using ObdLink.Core.Dashboard;
using ObdLink.Core.Diagnostics;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

public class V020Tests
{
    private static double D(string key, params byte[] b) => J1979.Get(key)!.Decode(b);

    [Theory]
    [InlineData("rpm", new byte[] { 0x1A, 0xF8 }, 1726)]
    [InlineData("speed", new byte[] { 100 }, 100)]
    [InlineData("coolant", new byte[] { 0x82 }, 90)]
    [InlineData("iat", new byte[] { 0 }, -40)]
    [InlineData("load", new byte[] { 255 }, 100)]
    [InlineData("throttle", new byte[] { 0 }, 0)]
    [InlineData("fuel", new byte[] { 128 }, 50.196)]
    [InlineData("stft1", new byte[] { 128 }, 0)]
    [InlineData("stft1", new byte[] { 0 }, -100)]
    [InlineData("ltft2", new byte[] { 255 }, 99.219)]
    [InlineData("timing", new byte[] { 128 }, 0)]
    [InlineData("timing", new byte[] { 0 }, -64)]
    [InlineData("maf", new byte[] { 0x01, 0xF4 }, 5)]
    [InlineData("baro", new byte[] { 101 }, 101)]
    [InlineData("voltage", new byte[] { 0x37, 0x6E }, 14.19)]
    [InlineData("ambient", new byte[] { 61 }, 21)]
    [InlineData("oiltemp", new byte[] { 140 }, 100)]
    public void J1979_formulas(string key, byte[] data, double expected) => Assert.Equal(expected, D(key, data), 2);

    [Fact] public void Short_data_throws() => Assert.Throws<ArgumentException>(() => D("rpm", 0x10));

    [Fact]
    public void Monitor_status_mil_and_count()
    {
        Assert.Equal((true, 2), J1979.DecodeMonitorStatus(new byte[] { 0x82, 0, 0, 0 }));
        Assert.Equal((false, 0), J1979.DecodeMonitorStatus(new byte[] { 0x00 }));
        Assert.Equal((false, 127), J1979.DecodeMonitorStatus(new byte[] { 0x7F }));
    }

    [Fact]
    public void Supported_bitmap_parsing()
    {
        // BE 1F A8 13 = classic J1979 example: 01,03,04,05,06,07,0C,0D,0E,0F,10,11,13,15,1C,1F,20
        var s = J1979.ParseSupported(0x00, new byte[] { 0xBE, 0x1F, 0xA8, 0x13 }).ToArray();
        Assert.Equal(new byte[] { 0x01, 0x03, 0x04, 0x05, 0x06, 0x07, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x13, 0x15, 0x1C, 0x1F, 0x20 }, s);
        Assert.Equal(new byte[] { 0x21, 0x40 }, J1979.ParseSupported(0x20, new byte[] { 0x80, 0, 0, 0x01 }).ToArray());
    }

    [Fact]
    public void Drive_cycle_bitmap_roundtrips()
    {
        var all = new[] { (byte)0, (byte)0x20, (byte)0x40 }.SelectMany(b => J1979.ParseSupported(b, DriveCycle.Bitmap(b))).ToArray();
        Assert.Equal(DriveCycle.SupportedPids, all);
    }

    [Theory]
    [InlineData(100, 62.137)] [InlineData(0, 0)]
    public void Kmh_to_mph(double kmh, double mph) => Assert.Equal(mph, Units.KmhToMph(kmh), 3);

    [Fact]
    public void Unit_conversion_follows_prefs()
    {
        var us = new UnitPrefs(); var metric = new UnitPrefs { Speed = "km/h", Temperature = "°C", Pressure = "kPa" };
        Assert.Equal((212.0, "°F"), Units.Convert(100, Quantity.Temperature, "°C", us));
        Assert.Equal(-40, Units.CToF(-40), 6);
        Assert.Equal(14.6488, Units.Convert(101, Quantity.Pressure, "kPa", us).Value, 3);
        Assert.Equal((101.0, "kPa"), Units.Convert(101, Quantity.Pressure, "kPa", metric));
        Assert.Equal((50.0, "km/h"), Units.Convert(50, Quantity.Speed, "km/h", metric));
        Assert.Equal((42.0, "%"), Units.Convert(42, Quantity.Percent, "%", us));
    }

    [Fact]
    public void Enhanced_pid_table_default_is_unverified_and_decodes()
    {
        var t = EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson());
        Assert.All(t.Pids, p => Assert.False(p.Verified));
        var tft = t.Pids.Single(p => p.Key == "tft");
        Assert.Equal(0x7E0u, tft.TxId); Assert.Equal(new byte[] { 0x22, 0x1E, 0x1C }, tft.Request);
        Assert.Equal(87.5, tft.Decode(new byte[] { 0x62, 0x1E, 0x1C, 0x05, 0x78 }), 3); // 1400/16
        Assert.Equal(-10, tft.Decode(new byte[] { 0xFF, 0x60 }), 3);                     // signed -160/16
        var gear = t.Pids.Single(p => p.Key == "gear");
        Assert.Equal("3", gear.Label(gear.Decode(new byte[] { 0x62, 0x1E, 0x12, 0x03 })));
        Assert.False(t.Pids.Single(p => p.Key == "gearActual").Enabled);
    }

    [Fact]
    public void Enhanced_pid_file_is_created_and_user_edits_win()
    {
        var dir = Path.Combine(Path.GetTempPath(), "pids-" + Guid.NewGuid());
        var (t, path, err) = EnhancedPidTable.LoadOrCreate(dir);
        Assert.Null(err); Assert.True(File.Exists(path)); Assert.NotEmpty(t.Pids);
        File.WriteAllText(path, "{\"pids\":[{\"key\":\"x\",\"did\":\"1234\",\"bytes\":2,\"scale\":0.5,\"offset\":-10}]}");
        var t2 = EnhancedPidTable.LoadOrCreate(dir).Table;
        Assert.Equal(-10 + 0x0102 * 0.5, t2.Pids.Single(p => p.Key == "x").Decode(new byte[] { 1, 2 }));
        File.WriteAllText(path, "{ broken");
        var (t3, _, err3) = EnhancedPidTable.LoadOrCreate(dir);
        Assert.NotNull(err3); Assert.Contains(t3.Pids, p => p.Key == "gear");
        Directory.Delete(dir, true);
    }

    [Theory]
    [InlineData(1, 15)] [InlineData(2, 25)] [InlineData(3, 45)] [InlineData(4, 60)] [InlineData(5, 80)] [InlineData(6, 105)]
    public void Gear_estimator_inverts_rpm_for(int gear, double kmh)
    {
        var g = new GearEstimator(); Assert.Equal(gear, g.Estimate(g.RpmFor(gear, kmh), kmh));
    }
    [Fact]
    public void Gear_estimator_null_when_stopped_or_slipping()
    {
        var g = new GearEstimator(); Assert.Null(g.Estimate(700, 0)); Assert.Null(g.Estimate(8000, 100));
    }

    [Fact]
    public void Scheduler_priorities_and_intervals()
    {
        var s = new PollScheduler { BaseMs = 100 };
        s.Add("coolant", 2); s.Add("rpm", 0); s.Add("load", 1); s.Add("rpm", 0);
        Assert.Equal(3, s.Items.Count);
        Assert.Equal(new[] { "rpm", "load", "coolant" }, s.Due(0).Select(i => i.Key));
        foreach (var i in s.Due(0)) s.Completed(i, 0, true);
        Assert.Empty(s.Due(50));
        Assert.Equal(new[] { "rpm" }, s.Due(100).Select(i => i.Key));
        Assert.Equal(new[] { "rpm", "load" }, s.Due(200).Select(i => i.Key));
        Assert.Contains("coolant", s.Due(800).Select(i => i.Key));
        Assert.Single(s.Due(800, max: 1));
    }

    [Fact]
    public void Scheduler_adapts_and_backs_off()
    {
        var s = new PollScheduler { BaseMs = 100 };
        for (int k = 0; k < 20; k++) s.CycleTook(500);
        Assert.Equal(s.MaxFactor, s.Factor);
        for (int k = 0; k < 100; k++) s.CycleTook(10);
        Assert.Equal(1.0, s.Factor);
        s.Add("x", 0); var it = s.Items[0];
        s.Completed(it, 0, false); Assert.Equal(200, it.NextDueMs);
        s.Completed(it, 0, false); Assert.Equal(400, it.NextDueMs);
        for (int k = 0; k < 10; k++) s.Completed(it, 0, false);
        Assert.True(it.Disabled); Assert.Empty(s.Due(long.MaxValue));
    }

    [Fact]
    public void Stats_and_thresholds()
    {
        var st = new GaugeStats(); Assert.True(double.IsNaN(st.Avg));
        foreach (var v in new[] { 3.0, 1, 5, double.NaN }) st.Add(v);
        Assert.Equal((1.0, 5.0, 3.0, 3L), (st.Min, st.Max, st.Avg, st.Count));
        Assert.Equal(Level.Warn, Thresholds.Evaluate(111, null, 110));
        Assert.Equal(Level.Warn, Thresholds.Evaluate(11, 11.8, 15.2));
        Assert.Equal(Level.Normal, Thresholds.Evaluate(14, 11.8, 15.2));
    }

    [Fact]
    public void Csv_recorder_writes_invariant_rows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dash-" + Guid.NewGuid());
        string path;
        using (var r = new DashboardCsvRecorder(dir, new[] { "rpm", "speed" }, new DateTime(2026, 10, 24, 9, 0, 0)))
        { path = r.Path; r.Write(new DateTime(2026, 10, 24, 9, 0, 1), new Dictionary<string, double> { ["rpm"] = 1726.25 }); }
        Assert.EndsWith("dashboard-20261024-090000.csv", path);
        Assert.Equal(new[] { "time,rpm,speed", "2026-10-24T09:00:01.000,1726.25," }, File.ReadAllLines(path));
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Drive_cycle_is_realistic()
    {
        var idle = DriveCycle.At(5); Assert.Equal(0, idle.SpeedKmh); Assert.InRange(idle.Rpm, 600, 800); Assert.Equal(0, idle.Gear);
        var cruise = DriveCycle.At(60); Assert.InRange(cruise.SpeedKmh, 95, 115); Assert.Equal(6, cruise.Gear); Assert.InRange(cruise.Rpm, 1200, 2200);
        double max = Enumerable.Range(0, 1200).Max(i => DriveCycle.At(i / 10.0).Rpm); Assert.InRange(max, 2000, 6000);
        var gears = Enumerable.Range(0, 1200).Select(i => DriveCycle.At(i / 10.0).Gear).Distinct().ToList();
        Assert.Equal(7, gears.Count); // N + 1..6
    }

    private static async Task<(SimulatedAdapter, ElmDiagChannel)> Setup(double t)
    {
        var sim = new SimulatedAdapter { Clock = () => t };
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync(); await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, new ElmDiagChannel(s));
    }

    [Fact]
    public async Task Dashboard_engine_polls_simulator_read_only()
    {
        var (_, ch) = await Setup(60);
        long now = 0; var eng = new DashboardEngine(ch, () => now); var sent = new List<string>(); eng.Sent += sent.Add;
        var sup = await eng.DiscoverAsync();
        Assert.Contains((byte)0x0C, sup); Assert.Contains((byte)0x5C, sup); Assert.DoesNotContain((byte)0x02, sup);
        eng.Configure(new[] { "rpm", "speed", "coolant", "voltage", "notapid" }, EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson()).Pids);
        Assert.DoesNotContain(eng.Scheduler.Items, i => i.Key is "notapid" or "gearActual");
        var v = await eng.PollOnceAsync(); foreach (var kv in await eng.PollOnceAsync()) v[kv.Key] = kv.Value; // max 6 per cycle
        var c = DriveCycle.At(60);
        Assert.Equal(c.Rpm, v["rpm"], 0); Assert.Equal(Math.Round(c.SpeedKmh), v["speed"]);
        Assert.Equal(1, v["mil"]); Assert.Equal(2, v["dtcCount"]);
        Assert.Equal(6, v["gear"]); Assert.Equal(c.TftC, v["tft"], 1);
        Assert.All(sent, s => Assert.True(s.StartsWith("01") || s.StartsWith("22"), s));
    }

    [Fact]
    public void Settings_roundtrip_and_normalize()
    {
        var path = Path.Combine(Path.GetTempPath(), "set-" + Guid.NewGuid(), "settings.json");
        var s = new AppSettings { UpdateCheck = true, LogRetentionDays = 7, SimulatorEnabled = false };
        s.Units.Speed = "km/h"; s.Adapter.Baud = 500000; s.Dashboard.RefreshMs = 500; s.Dashboard.Gauges = new() { "rpm" };
        s.Save(path);
        var l = AppSettings.Load(path);
        Assert.True(l.UpdateCheck); Assert.Equal(7, l.LogRetentionDays); Assert.False(l.SimulatorEnabled);
        Assert.Equal("km/h", l.Units.Speed); Assert.Equal(500000, l.Adapter.Baud); Assert.Equal(500, l.Dashboard.RefreshMs);
        Assert.Equal(new[] { "rpm" }, l.Dashboard.Gauges); Assert.True(l.ReadOnlyTestMode);
        // old v0.1.5 file without new sections + bad values
        File.WriteAllText(path, "{\"ReadOnlyTestMode\":false,\"Theme\":\"Dark\",\"Units\":{\"Speed\":\"furlongs\"},\"Dashboard\":{\"RefreshMs\":1}}");
        var o = AppSettings.Load(path);
        Assert.False(o.ReadOnlyTestMode); Assert.Equal("mph", o.Units.Speed); Assert.Equal(100, o.Dashboard.RefreshMs);
        Assert.False(o.UpdateCheck); Assert.True(o.RedactVinsDefault); Assert.NotNull(o.Adapter);
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    private sealed class Fake(string body) : HttpMessageHandler
    {
        public Uri? Seen;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        { Seen = r.RequestUri; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }); }
    }

    [Fact]
    public async Task Update_checker_picks_newest()
    {
        var h = new Fake("[{\"tag_name\":\"v0.1.5-test\",\"html_url\":\"u1\",\"prerelease\":true},{\"tag_name\":\"v0.3.0-test\",\"name\":\"n\",\"html_url\":\"u3\",\"prerelease\":true},{\"tag_name\":\"v9.0.0\",\"draft\":true,\"html_url\":\"x\"}]");
        var u = await new UpdateChecker(h).CheckAsync(new Version(0, 2, 0));
        Assert.Equal("v0.3.0-test", u!.Tag); Assert.True(u.Newer); Assert.Equal("u3", u.Url);
        Assert.Equal("api.github.com", h.Seen!.Host);
        Assert.False((await new UpdateChecker(new Fake("[{\"tag_name\":\"v0.2.0-test\",\"html_url\":\"u\"}]")).CheckAsync(new Version(0, 2, 0, 0)))!.Newer);
        Assert.Null(await new UpdateChecker(new Fake("not json")).CheckAsync(new Version(0, 2, 0)));
        Assert.Equal(new Version(0, 2, 0), UpdateChecker.ParseTag("v0.2.0-test"));
    }
}
