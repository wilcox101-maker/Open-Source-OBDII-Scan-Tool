using System.IO.Compression;
using ObdLink.Core.Adapters;
using ObdLink.Core.Diagnostics;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Modules;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Serilog.Events;
using Xunit;

namespace ObdLink.Core.Tests;

public class LoggingTests
{
    // Synthetic test VIN only (never a real vehicle).
    private const string FakeVin = "1FTFW1E5XJFA00001";
    private static readonly string FakeVinHex = Convert.ToHexString(System.Text.Encoding.ASCII.GetBytes(FakeVin));

    [Fact]
    public void Begin_succeeded_failed_produce_outcomes()
    {
        var log = new EventLogService();
        log.Begin("Read VIN", ("module", "0x7E0")).Succeeded("ok");
        log.Begin("Write block").Failed(new InvalidOperationException("boom"));
        log.UserAction("Tab changed", ("tab", "DTCs"));
        var ev = log.Snapshot(LogChannel.Events);
        Assert.Contains(ev, e => e.Action == "Read VIN" && e.Outcome == EventOutcome.Started);
        Assert.Contains(ev, e => e.Action == "Read VIN" && e.Outcome == EventOutcome.Succeeded && e.ParamText.Contains("module=0x7E0"));
        var f = Assert.Single(ev, e => e.Outcome == EventOutcome.Failed);
        Assert.Contains("boom", f.Error);
        Assert.Equal(LogEventLevel.Error, f.Level);
    }

    [Fact]
    public void Ring_buffer_caps_and_clear()
    {
        var log = new EventLogService(capacity: 10);
        for (int i = 0; i < 25; i++) log.Traffic(">>", "0100 #" + i);
        var t = log.Snapshot(LogChannel.Traffic);
        Assert.Equal(10, t.Count);
        Assert.Contains("#24", t[^1].Text);
        log.Clear(LogChannel.Traffic);
        Assert.Empty(log.Snapshot(LogChannel.Traffic));
    }

    [Fact]
    public void File_logs_created_and_messages_ingested()
    {
        var dir = Path.Combine(Path.GetTempPath(), "evlog_" + Guid.NewGuid());
        var log = LogSetup.CreateEventLog(dir);
        log.UserAction("Click", ("button", "Connect"));
        log.Message(LogEventLevel.Warning, "Test", "low voltage");
        log.Traffic("<<", "41 00 BE");
        Serilog.Log.CloseAndFlush();
        Assert.Contains(log.Snapshot(LogChannel.Messages), e => e.Text.Contains("low voltage") && e.Level == LogEventLevel.Warning);
        var files = Directory.GetFiles(dir).Select(Path.GetFileName).ToList();
        Assert.Contains(files, f => f!.StartsWith("events-"));
        Assert.Contains(files, f => f!.StartsWith("app-"));
        Assert.Contains(files, f => f!.StartsWith("traffic-"));
        Assert.NotEmpty(LogSetup.ReadRecentLogs(dir));
    }

    [Fact]
    public void Filter_by_level_and_search()
    {
        var log = new EventLogService();
        log.Message(LogEventLevel.Information, "A", "hello world");
        log.Message(LogEventLevel.Error, "B", "fatal NRC 0x31");
        var m = log.Snapshot(LogChannel.Messages);
        Assert.Single(LogFilter.Apply(m, LogEventLevel.Warning, null));
        Assert.Single(LogFilter.Apply(m, LogEventLevel.Verbose, "nrc"));
        Assert.Equal(2, LogFilter.Apply(m, LogEventLevel.Verbose, "").Count());
    }

    [Fact]
    public void Export_csv_quotes_guards_formulas_and_redacts()
    {
        var log = new EventLogService();
        log.Message(LogEventLevel.Information, "VIN", $"VIN is {FakeVin}, ok");
        log.Message(LogEventLevel.Information, "X", "=HYPERLINK(\"evil\")");
        var csv = LogExporter.ToCsv(log.Snapshot(LogChannel.Messages));
        Assert.DoesNotContain(FakeVin, csv);
        Assert.Contains("XXXXXXXXXXXA00001", csv);
        Assert.Contains("\"VIN is XXXXXXXXXXXA00001, ok\"", csv);
        Assert.DoesNotContain(",=HYPERLINK", csv);
        Assert.Contains(FakeVin, LogExporter.ToText(log.Snapshot(LogChannel.Messages), redactVins: false));
        var p = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
        LogExporter.Export(log.Snapshot(LogChannel.Messages), p);
        Assert.DoesNotContain(FakeVin, File.ReadAllText(p));
    }

    [Fact]
    public void Vin_redaction_plain_and_hex()
    {
        Assert.Equal("XXXXXXXXXXXA00001", VinRedactor.Redact(FakeVin));
        var hex = VinRedactor.Redact("62F190" + FakeVinHex);
        Assert.DoesNotContain(FakeVinHex, hex);
        Assert.StartsWith("62F190", hex);
        Assert.Equal("ATSP6 0100 41 00 BE 3F A8 13", VinRedactor.Redact("ATSP6 0100 41 00 BE 3F A8 13"));
    }

    private static async Task<(SimulatedAdapter, AdapterSession, ElmDiagChannel)> Setup()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, s, new ElmDiagChannel(s));
    }

    [Fact]
    public async Task Simulator_serves_demo_DE00_and_NRC_is_decoded()
    {
        var (_, _, ch) = await Setup();
        var uds = new UdsClient(ch, 0x7E0);
        var r = await uds.RequestAsync(new byte[] { 0x22, 0xDE, 0x00 });
        Assert.Equal(0x62, r[0]);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.RequestAsync(new byte[] { 0x22, 0xBE, 0xEF }));
        Assert.Contains("0x31", ex.Message);
        Assert.Contains("requestOutOfRange", ex.Message);
        Assert.Contains("ReadDataByIdentifier", ex.Message);
        Assert.Contains("7E0", ex.Message);
    }

    [Fact]
    public void Connection_status_labels_consistent()
    {
        var port = new PortInfo("SIM-MX", "Simulated MX+", PortKind.Virtual);
        var c = ConnectionStatus.Connected(port, new AdapterInfo("ELM327 v1.4b", "STN2255", "OBDLink MX+", 12.6), AdapterProfiles.ObdLinkMxPlus);
        Assert.Equal("OBDLink MX+ (simulated)", c.AdapterName);
        Assert.Equal("12.6 V", c.Voltage);
        Assert.StartsWith("Connected", c.StatusText);
        Assert.Equal("-", ConnectionStatus.Disconnected.Voltage);
        Assert.Equal("-", ConnectionStatus.Failed("x").Voltage);
    }

    private sealed class Answer(bool v) : IConfirmationProvider { public Task<bool> ConfirmAsync(ConfirmationStep s, CancellationToken ct = default) => Task.FromResult(v); }

    [Fact]
    public async Task Confirmation_dialogs_are_logged()
    {
        var log = new EventLogService();
        await new LoggingConfirmationProvider(new Answer(true), log).ConfirmAsync(new ConfirmationStep("Write", "sure?"));
        await new LoggingConfirmationProvider(new Answer(false), log).ConfirmAsync(new ConfirmationStep("Clear", "sure?"));
        var all = string.Join("\n", log.Snapshot(LogChannel.Events).Select(e => e.Render()));
        Assert.Contains("Write", all); Assert.Contains("Clear", all);
        Assert.Contains(log.Snapshot(LogChannel.Events), e => e.Outcome == EventOutcome.Cancelled || e.Text.Contains("cancel", StringComparison.OrdinalIgnoreCase) || e.ParamText.Contains("False"));
    }

    [Fact]
    public async Task Capture_includes_logs_and_redacts()
    {
        var (_, s, ch) = await Setup();
        var dir = Path.Combine(Path.GetTempPath(), "cap_" + Guid.NewGuid());
        var path = await new DiagnosticsCapture(s, ch).RunAsync(new("2017 Ford F-150", FordF150_2017.Modules, StnProtocol.HsCan11_500, '6', null, "test",
            ExtraFiles: new[] { ("events-20261023.log", $"read VIN {FakeVin}") }, RedactVins: true), dir);
        using var z = ZipFile.OpenRead(path);
        var e = z.GetEntry("logs/events-20261023.log");
        Assert.NotNull(e);
        using var r = new StreamReader(e!.Open());
        var text = r.ReadToEnd();
        Assert.DoesNotContain(FakeVin, text);
        Assert.Contains("XXXXXXXXXXXA00001", text);
    }
}
