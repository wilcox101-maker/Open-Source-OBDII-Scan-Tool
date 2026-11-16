using ObdLink.Core.Dashboard;
using ObdLink.Core.LiveData;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.12.0: signal history and decimation, CSV log read-back and replay, threshold triggers.</summary>
public class ChartsReplayTests
{
    private static readonly DateTime T0 = new(2026, 11, 15, 12, 0, 0);
    private static Dictionary<string, double> C(params (string K, double V)[] kv) => kv.ToDictionary(x => x.K, x => x.V);

    // ---- SignalHistory ---------------------------------------------------------------------------------------------

    [Fact]
    public void Ring_keeps_the_newest_samples()
    {
        var h = new SignalHistory(capacityPerSignal: 3);
        for (int i = 0; i < 5; i++) h.Add(C(("rpm", i)), T0.AddSeconds(i));
        var all = h.Window("rpm", DateTime.MinValue, DateTime.MaxValue);
        Assert.Equal(new double[] { 2, 3, 4 }, all.Select(p => p.V));
        Assert.Equal(3, h.Count("rpm"));
    }

    [Fact]
    public void Non_finite_and_backwards_samples_are_dropped()
    {
        var h = new SignalHistory();
        h.Add(C(("a", 1)), T0.AddSeconds(2));
        h.Add(C(("a", double.NaN), ("b", double.PositiveInfinity)), T0.AddSeconds(3));
        h.Add(C(("a", 5)), T0.AddSeconds(1));
        Assert.Equal(1, h.Count("a"));
        Assert.Equal(0, h.Count("b"));
    }

    [Fact]
    public void Window_includes_one_neighbour_each_side()
    {
        var h = new SignalHistory();
        for (int i = 0; i < 10; i++) h.Add(C(("v", i)), T0.AddSeconds(i));
        var w = h.Window("v", T0.AddSeconds(3.5), T0.AddSeconds(6));
        Assert.Equal(new double[] { 3, 4, 5, 6, 7 }, w.Select(p => p.V));
        Assert.Empty(h.Window("nope", T0, T0.AddSeconds(1)));
    }

    [Fact]
    public void Span_and_keys_are_case_insensitive()
    {
        var h = new SignalHistory();
        Assert.Null(h.Span);
        h.Add(C(("RPM", 1)), T0);
        h.Add(C(("rpm", 2), ("ect", 80)), T0.AddSeconds(4));
        Assert.Equal((T0, T0.AddSeconds(4)), h.Span);
        Assert.Equal(2, h.Count("Rpm"));
        Assert.Equal(2, h.Keys.Count);
    }

    [Fact]
    public void Decimation_keeps_peaks_ends_and_order()
    {
        var pts = new List<SamplePoint>();
        for (int i = 0; i < 10_000; i++) pts.Add(new SamplePoint(T0.AddMilliseconds(i), i == 5_000 ? 999 : i == 7_777 ? -999 : 0));
        var d = SignalHistory.Decimate(pts, T0, T0.AddMilliseconds(10_000), 100);
        Assert.True(d.Count <= 2 * 100 + 2);
        Assert.Equal(pts[0], d[0]);
        Assert.Equal(pts[^1], d[^1]);
        Assert.Contains(d, p => p.V == 999);
        Assert.Contains(d, p => p.V == -999);
        Assert.True(d.Zip(d.Skip(1)).All(x => x.First.T <= x.Second.T));
    }

    [Fact]
    public void Sparse_points_are_not_decimated()
    {
        var pts = Enumerable.Range(0, 10).Select(i => new SamplePoint(T0.AddSeconds(i), i)).ToList();
        Assert.Same(pts, SignalHistory.Decimate(pts, T0, T0.AddSeconds(10), 100));
    }

    // ---- LiveCsvLog ------------------------------------------------------------------------------------------------

    [Fact]
    public void Recorder_output_reads_back()
    {
        var dir = Path.Combine(Path.GetTempPath(), "obd-csv-" + Guid.NewGuid().ToString("N"));
        string path;
        using (var rec = new DashboardCsvRecorder(dir, new[] { "rpm", "ect" }, T0))
        {
            rec.Write(T0, C(("rpm", 800), ("ect", 85.5)));
            rec.Write(T0.AddMilliseconds(250), C(("rpm", 1200.125)));
            path = rec.Path;
        }
        var log = LiveCsvLog.Load(path);
        Assert.Equal(new[] { "rpm", "ect" }, log.Keys);
        Assert.Equal(2, log.Cycles.Count);
        Assert.Equal(85.5, log.Cycles[0].Values["ect"]);
        Assert.False(log.Cycles[1].Values.ContainsKey("ect"));
        Assert.Equal(1200.125, log.Cycles[1].Values["rpm"]);
        Assert.Equal(TimeSpan.FromMilliseconds(250), log.Duration);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Bad_lines_are_skipped_and_counted()
    {
        var text = "time,rpm\n2026-11-15T12:00:00.000,800\nnot a line\n2026-11-15T12:00:01.000,abc\n2026-11-15T11:00:00.000,900\n\n2026-11-15T12:00:02.000,1000\n";
        var log = LiveCsvLog.Parse(new StringReader(text));
        Assert.Equal(2, log.Cycles.Count);
        Assert.Equal(3, log.SkippedLines);
    }

    [Theory]
    [InlineData("")]
    [InlineData("rpm,ect\n")]
    [InlineData("time\n")]
    [InlineData("time,rpm,RPM\n")]
    public void Header_is_checked(string text) => Assert.Throws<InvalidDataException>(() => LiveCsvLog.Parse(new StringReader(text)));

    // ---- ReplayCursor ----------------------------------------------------------------------------------------------

    private static LiveCsvLog Log10() => LiveCsvLog.Parse(new StringReader("time,v\n" + string.Join("\n",
        Enumerable.Range(0, 10).Select(i => $"{T0.AddSeconds(i):yyyy-MM-ddTHH:mm:ss.fff},{i}"))));

    [Fact]
    public void Replay_follows_the_recorded_time_base()
    {
        var c = new ReplayCursor(Log10());
        Assert.Single(c.Advance(TimeSpan.Zero));             // t=0
        Assert.Empty(c.Advance(TimeSpan.FromMilliseconds(900)));
        Assert.Single(c.Advance(TimeSpan.FromMilliseconds(100)));
        c.Speed = 4;
        Assert.Equal(new double[] { 2, 3, 4, 5 }, c.Advance(TimeSpan.FromSeconds(1)).Select(x => x.Values["v"]));
        Assert.False(c.Finished);
        Assert.Equal(4, c.Advance(TimeSpan.FromSeconds(10)).Count);
        Assert.True(c.Finished);
        Assert.Equal(1, c.Fraction);
    }

    [Fact]
    public void Replay_seek_and_speed_limits()
    {
        var c = new ReplayCursor(Log10());
        c.Seek(0.5);
        Assert.Equal(4.5, c.Position.TotalSeconds, 3);
        Assert.Equal(5, c.Advance(TimeSpan.FromMilliseconds(500))[0].Values["v"]);
        c.Seek(0);
        Assert.Equal(0, c.Advance(TimeSpan.Zero)[0].Values["v"]);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Speed = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Speed = 100);
        Assert.Throws<ArgumentOutOfRangeException>(() => c.Advance(TimeSpan.FromSeconds(-1)));
    }

    // ---- Triggers --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("rpm > 3000", "rpm", TriggerOp.Above, 3000, 0)]
    [InlineData(" ect<70 hys 2 ", "ect", TriggerOp.Below, 70, 2)]
    [InlineData("tft > -10.5 hysteresis 0.5", "tft", TriggerOp.Above, -10.5, 0.5)]
    public void Trigger_parse(string text, string key, TriggerOp op, double th, double hy)
    {
        var t = SignalTrigger.Parse(text);
        Assert.Equal((key, op, th, hy), (t.Key, t.Op, t.Threshold, t.Hysteresis));
    }

    [Theory]
    [InlineData("rpm >= 3000")]
    [InlineData("> 3000")]
    [InlineData("rpm > lots")]
    [InlineData("")]
    public void Trigger_parse_rejects(string text) => Assert.Throws<FormatException>(() => SignalTrigger.Parse(text));

    [Fact]
    public void Trigger_fires_once_per_crossing_with_hysteresis()
    {
        var m = new TriggerMonitor();
        m.Add(SignalTrigger.Parse("rpm > 3000 hys 100"));
        double[] seq = { 2000, 3100, 3200, 2950, 3050, 2800, 3001 };
        var fired = new List<double>();
        for (int i = 0; i < seq.Length; i++)
            fired.AddRange(m.Apply(C(("rpm", seq[i])), T0.AddSeconds(i)).Select(h => h.Value));
        Assert.Equal(new double[] { 3100, 3001 }, fired); // 2950 is inside the hysteresis band, 2800 re-arms
    }

    [Fact]
    public void Below_trigger_and_missing_signal()
    {
        var m = new TriggerMonitor();
        m.Add(SignalTrigger.Parse("ECT < 70"));
        Assert.Empty(m.Apply(C(("rpm", 1)), T0));
        Assert.Single(m.Apply(C(("ect", 60)), T0));
        Assert.Empty(m.Apply(C(("ect", 50)), T0));
        m.Rearm();
        Assert.Single(m.Apply(C(("ect", 50)), T0));
    }
}
