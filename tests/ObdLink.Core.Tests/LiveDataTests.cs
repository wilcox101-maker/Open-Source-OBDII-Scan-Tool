using ObdLink.Core.Dashboard;
using ObdLink.Core.Elm;
using ObdLink.Core.LiveData;
using ObdLink.Core.Obd;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.10.0 Phase 2: live-data catalog and the per-signal aggregator.</summary>
public class LiveDataTests
{
    private static EnhancedPidTable Enhanced() => new()
    {
        Pids =
        {
            new EnhancedPid { Key = "tpms_fl", Name = "TPMS front left", Unit = "kPa", Header = "726", Did = "2813", Bytes = 1, Verified = true },
            new EnhancedPid { Key = "oss", Name = "Output shaft speed", Unit = "rpm", Header = "7E0", Did = "1E13", Bytes = 2, Verified = false },
            new EnhancedPid { Key = "rpm", Name = "Engine speed (enhanced dup)", Unit = "rpm", Header = "7E0", Did = "1000", Bytes = 2 },
        },
    };

    [Fact]
    public void Catalog_lists_supported_obd_pids_then_enhanced_and_obd_wins_key_collisions()
    {
        var supported = new HashSet<byte> { 0x0C, 0x05 }; // rpm, coolant
        var cat = LiveSignal.Catalog(supported, Enhanced());
        Assert.Contains(cat, s => s.Key == "rpm" && s.Kind == LiveKind.ObdPid);          // the standard PID 0C, not the enhanced dup
        Assert.Single(cat, s => s.Key == "rpm");                                          // the enhanced "rpm" was dropped
        Assert.Contains(cat, s => s.Key == "coolant" && s.Kind == LiveKind.ObdPid);
        Assert.DoesNotContain(cat, s => s.Key == "speed");                                // PID 0D not supported
        Assert.Contains(cat, s => s.Key == "tpms_fl" && s.Kind == LiveKind.Enhanced && s.Verified);
        Assert.Contains(cat, s => s.Key == "oss" && !s.Verified && s.Note!.Contains("UNVERIFIED"));
    }

    [Fact]
    public void Unsupported_vehicle_still_offers_enhanced_dids()
    {
        var cat = LiveSignal.Catalog(new HashSet<byte>(), Enhanced());
        Assert.All(cat, s => Assert.Equal(LiveKind.Enhanced, s.Kind));
        Assert.Equal(3, cat.Count);
    }

    [Fact]
    public void Readings_track_current_min_max_and_sample_count()
    {
        var r = new LiveReadings();
        r.Track(LiveSignal.Catalog(new HashSet<byte> { 0x0C }, new EnhancedPidTable()));
        var t = new DateTime(2026, 11, 15, 12, 0, 0);
        r.Apply(new Dictionary<string, double> { ["rpm"] = 800 }, t);
        r.Apply(new Dictionary<string, double> { ["rpm"] = 2400 }, t.AddSeconds(1));
        r.Apply(new Dictionary<string, double> { ["rpm"] = 1500 }, t.AddSeconds(2));
        var row = Assert.Single(r.Rows);
        Assert.Equal(1500, row.Current);
        Assert.Equal(800, row.Stats.Min);
        Assert.Equal(2400, row.Stats.Max);
        Assert.Equal(3, row.Samples);
    }

    [Fact]
    public void Missing_signal_keeps_its_value_and_ages()
    {
        var r = new LiveReadings();
        r.Track(new[] { new LiveSignal("rpm", "Engine speed", "rpm", LiveKind.ObdPid, 0) });
        var t = new DateTime(2026, 11, 15, 12, 0, 0);
        r.Apply(new Dictionary<string, double> { ["rpm"] = 800 }, t);
        r.Apply(new Dictionary<string, double>(), t.AddSeconds(3)); // this cycle didn't read rpm
        var row = Assert.Single(r.Rows);
        Assert.Equal(800, row.Current);          // last value kept
        Assert.Equal(1, row.Samples);            // not counted again
        Assert.True(r.IsStale(row, t.AddSeconds(3), TimeSpan.FromSeconds(2)));
        Assert.False(r.IsStale(row, t, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Untracked_keys_and_non_finite_values_are_ignored()
    {
        var r = new LiveReadings();
        r.Track(new[] { new LiveSignal("rpm", "Engine speed", "rpm", LiveKind.ObdPid, 0) });
        var t = new DateTime(2026, 11, 15, 12, 0, 0);
        r.Apply(new Dictionary<string, double> { ["mil"] = 1, ["rpm"] = double.NaN }, t); // mil untracked, rpm NaN
        var row = Assert.Single(r.Rows);
        Assert.Null(row.Current);
        Assert.Equal(0, row.Samples);
    }

    [Fact]
    public void Reset_clears_values_but_keeps_rows()
    {
        var r = new LiveReadings();
        r.Track(new[] { new LiveSignal("rpm", "Engine speed", "rpm", LiveKind.ObdPid, 0) });
        r.Apply(new Dictionary<string, double> { ["rpm"] = 800 }, DateTime.Now);
        r.Reset();
        var row = Assert.Single(r.Rows);
        Assert.Null(row.Current);
        Assert.Equal(0, row.Samples);
        Assert.True(double.IsNaN(row.Stats.Min));
    }

    [Fact]
    public async Task End_to_end_reads_supported_pids_from_the_simulator_into_readings()
    {
        var (_, s2, ch) = await AsBuiltServiceSafetyTests.Connect();
        var engine = new DashboardEngine(ch);
        await engine.DiscoverAsync();
        var cat = LiveSignal.Catalog(engine.Supported, new EnhancedPidTable());
        Assert.NotEmpty(cat); // the simulated ECU reports supported PIDs
        var readings = new LiveReadings();
        readings.Track(cat);
        engine.Configure(cat.Select(c => c.Key), System.Array.Empty<EnhancedPid>());
        for (int i = 0; i < 5; i++) readings.Apply(await engine.PollOnceAsync(), System.DateTime.Now);
        Assert.Contains(readings.Rows, r => r.Samples > 0 && r.Current is not null);
        _ = s2;
    }
}
