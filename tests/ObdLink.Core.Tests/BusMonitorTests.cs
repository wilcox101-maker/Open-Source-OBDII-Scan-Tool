using ObdLink.Core.Elm;
using ObdLink.Core.Monitor;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using ObdLink.Core.IsoTp;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.13.0: passive bus monitor - line parsing, filters, statistics, CSV log, ElmLink monitor mode, simulator end to end.</summary>
public class BusMonitorTests
{
    private static readonly DateTime T0 = new(2026, 11, 15, 12, 0, 0);

    // ---- Line parsing ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("7E80641000000000000", 0x7E8u, false, "0641000000000000")]
    [InlineData("201 0C 80 1F 40", 0x201u, false, "0C801F40")]
    [InlineData("080", 0x080u, false, "")]
    [InlineData("18DAF11003410C1A", 0x18DAF110u, true, "03410C1A")]
    [InlineData("18 DA F1 10 03 41 0C 1A", 0x18DAF110u, true, "03410C1A")]
    public void Parses_monitor_lines(string line, uint id, bool ext, string data)
    {
        Assert.True(MonitorLine.TryParse(line, T0, out var f));
        Assert.Equal((id, ext, data), (f.Id, f.Extended, f.DataText));
        Assert.Equal(T0, f.T);
    }

    [Theory]
    [InlineData("CAN ERROR")]
    [InlineData("<RX ERROR")]
    [InlineData("7E8064100<DATA ERROR")]
    [InlineData("7E")]
    [InlineData("7E80102030405060708090A")]   // 9 data bytes
    [InlineData("FFF0102")]                   // 11-bit id above 7FF
    [InlineData("SEARCHING...")]
    [InlineData("")]
    public void Rejects_non_frames(string line) => Assert.False(MonitorLine.TryParse(line, T0, out _));

    // ---- Filters ---------------------------------------------------------------------------------------------------

    private static CanFrame F(uint id, bool ext = false, params byte[] d) => new(T0, id, ext, d);

    [Fact]
    public void Filters()
    {
        Assert.True(IdFilter.Parse("").Matches(F(0x201)));
        Assert.True(IdFilter.Parse("ALL").Matches(F(0x18DAF110, true)));
        var diag = IdFilter.Parse("diag");
        Assert.True(diag.Matches(F(0x7E8))); Assert.True(diag.Matches(F(0x726))); Assert.False(diag.Matches(F(0x201)));
        Assert.False(diag.Matches(F(0x18DAF110, true)));
        var one = IdFilter.Parse("7e8");
        Assert.True(one.Matches(F(0x7E8))); Assert.False(one.Matches(F(0x7E0)));
        var masked = IdFilter.Parse("7E0/7F0");
        Assert.True(masked.Matches(F(0x7E8))); Assert.True(masked.Matches(F(0x7E0))); Assert.False(masked.Matches(F(0x726)));
        var ext = IdFilter.Parse("18DAF110");
        Assert.True(ext.Matches(F(0x18DAF110, true))); Assert.False(ext.Matches(F(0x110)));
    }

    [Theory]
    [InlineData("7E")]
    [InlineData("xyz")]
    [InlineData("700/780/7FF")]
    [InlineData("7E8 7E0")]
    public void Filter_rejects(string text) => Assert.Throws<FormatException>(() => IdFilter.Parse(text));

    // ---- Stats -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Stats_count_rate_and_changed_bits()
    {
        var s = new MonitorStats();
        for (int i = 0; i < 11; i++)
            s.Add(new CanFrame(T0.AddMilliseconds(100 * i), 0x201, false, new byte[] { 0x10, (byte)(i % 2 == 0 ? 0x00 : 0x81), 0x55 }));
        s.Add(new CanFrame(T0.AddSeconds(1), 0x080, false, new byte[] { 1 }));
        var rows = s.Snapshot();
        Assert.Equal(new[] { 0x080u, 0x201u }, rows.Select(r => r.Id));
        var r201 = rows[1];
        Assert.Equal(11, r201.Count);
        Assert.Equal(10.0, r201.RateHz, 3);
        Assert.Equal("·· 81 ··", r201.ChangedText);
        Assert.Equal("10 00 55", r201.DataText);
        Assert.Equal(12, s.Frames);
        Assert.Equal(11.0, s.TotalRate, 3);
        s.Reset();
        Assert.Empty(s.Snapshot());
        Assert.Equal(0, s.Frames);
    }

    [Fact]
    public void Csv_log_writes_frames()
    {
        var dir = Path.Combine(Path.GetTempPath(), "obd-bus-" + Guid.NewGuid().ToString("N"));
        string path;
        using (var log = new CanCsvLog(dir, "HS-CAN", T0))
        {
            log.Write(new CanFrame(T0, 0x201, false, new byte[] { 0x0C, 0x80 }));
            log.Write(new CanFrame(T0.AddMilliseconds(5), 0x18DAF110, true, Array.Empty<byte>()));
            path = log.Path;
            Assert.Equal(2, log.Written);
        }
        var lines = File.ReadAllLines(path);
        Assert.Equal("time,id,ext,dlc,data", lines[0]);
        Assert.Equal("2026-11-15T12:00:00.000,201,0,2,0C80", lines[1]);
        Assert.Equal("2026-11-15T12:00:00.005,18DAF110,1,0,", lines[2]);
        Directory.Delete(dir, true);
    }

    // ---- ElmLink monitor mode --------------------------------------------------------------------------------------

    [Fact]
    public async Task Link_monitor_streams_lines_and_stops_on_cancel()
    {
        var sim = new SimulatedAdapter { MonitorIntervalMs = 1 };
        var link = new ElmLink(sim.CreateTransport());
        var lines = new List<string>();
        using var cts = new CancellationTokenSource();
        var stop = await link.MonitorAsync("ATMA", l => { lines.Add(l); if (lines.Count == 20) cts.Cancel(); }, cts.Token);
        Assert.Equal(MonitorStop.Cancelled, stop);
        Assert.True(lines.Count >= 20);
        Assert.False(sim.Monitoring);
        // Back in command mode: the next command gets its own reply.
        Assert.Equal("OK", (await link.SendAsync("ATE0")).Lines.Single());
    }

    [Fact]
    public async Task Link_monitor_reports_buffer_full()
    {
        var sim = new SimulatedAdapter { MonitorIntervalMs = 1, MonitorBufferFullAfter = 5 };
        var link = new ElmLink(sim.CreateTransport());
        var lines = new List<string>();
        var stop = await link.MonitorAsync("ATMA", lines.Add, CancellationToken.None);
        Assert.Equal(MonitorStop.BufferFull, stop);
        Assert.Equal(5, lines.Count);
    }

    // ---- BusMonitor end to end -------------------------------------------------------------------------------------

    private static async Task<(SimulatedAdapter, AdapterSession)> Connect(Action<SimulatedAdapter>? setup = null)
    {
        var sim = new SimulatedAdapter { MonitorIntervalMs = 1 };
        setup?.Invoke(sim);
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, s);
    }

    [Fact]
    public async Task Monitor_hs_can_is_passive_and_restores_the_session()
    {
        var (sim, s) = await Connect();
        var stats = new MonitorStats();
        int seen = 0;
        using var cts = new CancellationTokenSource();
        var before = sim.Sent.Count;
        var stop = await new BusMonitor(s).RunAsync(Bus.HsCan, IdFilter.All, stats, _ => { if (++seen == 40) cts.Cancel(); }, () => DateTime.Now, cts.Token);
        Assert.Equal(MonitorStop.Cancelled, stop);
        Assert.Equal(before, sim.Sent.Count); // no vehicle request was transmitted
        Assert.Contains(stats.Snapshot(), r => r.Id == 0x201);
        Assert.Contains(stats.Snapshot(), r => r.Id == 0x3B3);
        Assert.True(s.IsStale);

        // A normal request afterwards works (header + receive filter re-applied).
        var uds = new UdsClient(new ElmDiagChannel(s), 0x7E0);
        Assert.Equal(new byte[] { 0x7E, 0x00 }, await uds.TesterPresentAsync());
    }

    [Fact]
    public async Task Monitor_filter_drops_other_ids()
    {
        var (_, s) = await Connect();
        var stats = new MonitorStats();
        using var cts = new CancellationTokenSource();
        int seen = 0;
        await new BusMonitor(s).RunAsync(Bus.HsCan, IdFilter.Parse("201"), stats, _ => { if (++seen == 10) cts.Cancel(); }, () => DateTime.Now, cts.Token);
        Assert.Equal(new[] { 0x201u }, stats.Snapshot().Select(r => r.Id));
        Assert.True(stats.Filtered > 0);
    }

    [Fact]
    public async Task Monitor_restarts_after_buffer_full()
    {
        var (_, s) = await Connect(sim => sim.MonitorBufferFullAfter = 8);
        var stats = new MonitorStats();
        using var cts = new CancellationTokenSource();
        var mon = new BusMonitor(s);
        int seen = 0;
        await mon.RunAsync(Bus.HsCan, IdFilter.All, stats, _ => { if (++seen == 30) cts.Cancel(); }, () => DateTime.Now, cts.Token);
        Assert.True(mon.Overflows >= 3);
        Assert.True(stats.Frames >= 30);
    }

    [Fact]
    public async Task Monitor_ms_can_shows_ms_traffic()
    {
        var (_, s) = await Connect();
        var stats = new MonitorStats();
        using var cts = new CancellationTokenSource();
        int seen = 0;
        await new BusMonitor(s).RunAsync(Bus.MsCan, IdFilter.All, stats, _ => { if (++seen == 12) cts.Cancel(); }, () => DateTime.Now, cts.Token);
        Assert.Contains(stats.Snapshot(), r => r.Id == 0x3B2);
        Assert.DoesNotContain(stats.Snapshot(), r => r.Id == 0x201);
        Assert.Equal(Bus.MsCan, s.CurrentBus);
    }

    /// <summary>Like a Windows SerialStream: a pending read ignores cancellation and returns only when the adapter sends something.</summary>
    private sealed class DeafToCancelTransport : ITransport
    {
        private readonly System.Threading.Channels.Channel<string> _rx = System.Threading.Channels.Channel.CreateUnbounded<string>();
        public List<string> Writes { get; } = new();
        public string Name => "DEAF";
        public bool IsOpen { get; private set; }
        public Task OpenAsync(CancellationToken ct = default) { IsOpen = true; return Task.CompletedTask; }
        public Task CloseAsync() { IsOpen = false; return Task.CompletedTask; }
        public Task WriteAsync(string text, CancellationToken ct = default)
        {
            lock (Writes) Writes.Add(text);
            if (text == "ATMA\r") _rx.Writer.TryWrite("201AABB\r");          // one frame, then a silent bus
            else if (text == "\r") _rx.Writer.TryWrite("\r>");               // any character stops the monitor
            return Task.CompletedTask;
        }
        public async Task<string> ReadAsync(CancellationToken ct = default) => await _rx.Reader.ReadAsync(CancellationToken.None);
        public void DiscardInput() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Stop_works_when_the_serial_read_ignores_cancellation()
    {
        var t = new DeafToCancelTransport();
        var link = new ElmLink(t);
        var lines = new List<string>();
        using var cts = new CancellationTokenSource();
        var run = link.MonitorAsync("ATMA", lines.Add, cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        var stop = await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(MonitorStop.Cancelled, stop);
        Assert.Equal(new[] { "201AABB" }, lines);
        Assert.Equal(1, t.Writes.Count(w => w == "\r")); // exactly one stop CR (a second would repeat ATMA)
    }

    [Fact]
    public async Task No_stray_cr_after_the_adapter_stopped_by_itself()
    {
        var sim = new SimulatedAdapter { MonitorIntervalMs = 1, MonitorBufferFullAfter = 3 };
        var t = sim.CreateTransport();
        var link = new ElmLink(t);
        using var cts = new CancellationTokenSource();
        Assert.Equal(MonitorStop.BufferFull, await link.MonitorAsync("ATMA", _ => { }, cts.Token));
        cts.Cancel(); // after return: the registration is gone, nothing is written
        await Task.Delay(20);
        Assert.Equal(new[] { "ATMA" }, t.Sent);
    }
}
