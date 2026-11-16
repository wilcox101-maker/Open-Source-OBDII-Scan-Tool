using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Routines and session keep-alive end to end: UdsSessionHost + UdsClient + ElmDiagChannel + SimulatedAdapter.</summary>
public class RoutineEndToEndTests
{
    private sealed record Target(uint TxId, Bus Bus = Bus.HsCan, string Acronym = "PCM") : IBusTarget
    {
        public Bus DefaultBus => Bus;
        public bool FoundConfirmed => true;
    }

    private static async Task<(SimulatedAdapter Sim, UdsSessionHost Host, SemaphoreSlim Bus)> Connect(bool readOnly = false)
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var ch = new ElmDiagChannel(s);
        var bus = new SemaphoreSlim(1, 1);
        var router = new BusRouter(s, () => true);
        var host = new UdsSessionHost(tx => new UdsClient(ch, tx) { Gate = new WriteGate(() => readOnly), PendingRetryDelay = TimeSpan.Zero }, bus,
            (t, w, ct) => router.RunAsync(t, w, allowFallback: false, ct: ct));
        return (sim, host, bus);
    }

    [Fact]
    public async Task Demo_routine_needs_the_extended_session()
    {
        var (_, host, _) = await Connect();
        var uds = host.Client(new Target(0x7E0));
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.StartRoutineAsync(SimulatedAdapter.DemoRoutineId));
        Assert.Equal(0x7F, ex.Nrc);
    }

    [Fact]
    public async Task Demo_routine_start_poll_results_stop()
    {
        var (_, host, _) = await Connect();
        var uds = host.Client(new Target(0x7E0));
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        Assert.Equal(UdsServerTiming.TryParse(new byte[] { 0x50, 0x03, 0x00, 0x32, 0x01, 0xF4 }, out var t) ? t : default, uds.ServerTiming);

        Assert.Equal(new byte[] { 0x01 }, (await uds.StartRoutineAsync(SimulatedAdapter.DemoRoutineId)).StatusRecord);
        Assert.Equal(new byte[] { 0x01 }, (await uds.RequestRoutineResultsAsync(SimulatedAdapter.DemoRoutineId)).StatusRecord);
        Assert.Equal(new byte[] { 0x00, 0x55 }, (await uds.RequestRoutineResultsAsync(SimulatedAdapter.DemoRoutineId)).StatusRecord);
        Assert.Empty((await uds.StopRoutineAsync(SimulatedAdapter.DemoRoutineId)).StatusRecord);
    }

    [Fact]
    public async Task Results_before_start_is_a_sequence_error()
    {
        var (_, host, _) = await Connect();
        var uds = host.Client(new Target(0x7E0));
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.RequestRoutineResultsAsync(SimulatedAdapter.DemoRoutineId));
        Assert.Equal(0x24, ex.Nrc);
    }

    [Fact]
    public async Task Unknown_routine_is_out_of_range()
    {
        var (_, host, _) = await Connect();
        var uds = host.Client(new Target(0x7E0));
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.StartRoutineAsync(0x0202));
        Assert.Equal(0x31, ex.Nrc);
    }

    [Fact]
    public async Task Read_only_mode_blocks_the_routine_but_not_the_session_or_keep_alive()
    {
        var (_, host, _) = await Connect(readOnly: true);
        var uds = host.Client(new Target(0x7E0));
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => uds.StartRoutineAsync(SimulatedAdapter.DemoRoutineId));
        await uds.TesterPresentAsync(suppressResponse: true); // silent on the simulator, as on a real module
    }

    [Fact]
    public async Task Suppressed_tester_present_is_silent_and_counts_as_success()
    {
        var (_, host, _) = await Connect();
        var uds = host.Client(new Target(0x7E0));
        Assert.Empty(await uds.TesterPresentAsync(suppressResponse: true));
        Assert.Equal(new byte[] { 0x7E, 0x00 }, await uds.TesterPresentAsync());
    }

    [Fact]
    public async Task Keep_alive_goes_through_the_router_and_lock()
    {
        var (_, host, bus) = await Connect();
        var clock = new ManualClock();
        var uds = host.Client(new Target(0x7E0));
        uds.Clock = clock;
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        clock.Now += TimeSpan.FromSeconds(3);
        Assert.Equal(1, await host.TickAsync());
        Assert.Equal(1, bus.CurrentCount);
        Assert.Single(host.OpenSessions);
    }

    [Fact]
    public async Task Ecu_reset_returns_the_demo_module_to_default()
    {
        var (_, host, _) = await Connect();
        var uds = host.Client(new Target(0x7E0));
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        await uds.EcuResetAsync(ResetType.Soft);
        Assert.Empty(host.OpenSessions);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.StartRoutineAsync(SimulatedAdapter.DemoRoutineId));
        Assert.Equal(0x7F, ex.Nrc);
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

public class RoutineCommandTests
{
    [Theory]
    [InlineData("0202", 0x0202)]
    [InlineData("0x1234", 0x1234)]
    [InlineData(" 12 34 ", 0x1234)]
    [InlineData("ab-cd", 0xABCD)]
    public void Parses_routine_ids(string text, int expected) => Assert.Equal(expected, RoutineCommand.Parse(text, "").RoutineId);

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("123")]
    [InlineData("12345678")]
    [InlineData("zz00")]
    public void Rejects_bad_ids(string text) => Assert.Throws<FormatException>(() => RoutineCommand.Parse(text, null));

    [Fact]
    public void Parses_option_record()
    {
        var c = RoutineCommand.Parse("1234", "01 02 0A");
        Assert.Equal(new byte[] { 1, 2, 0x0A }, c.Option);
        Assert.Empty(RoutineCommand.Parse("1234", "  ").Option);
        Assert.Throws<FormatException>(() => RoutineCommand.Parse("1234", "1"));
    }

    [Theory]
    [InlineData("E200")]
    [InlineData("e2ff")]
    [InlineData("FF00")]
    [InlineData("FF01")]
    public void Refused_ranges_fail_at_parse_time(string text) => Assert.Throws<RoutineRefusedException>(() => RoutineCommand.Parse(text, ""));

    [Fact]
    public void Start_needs_a_typed_run()
    {
        var steps = RoutineCommand.Parse("1234", "05").StartSteps("PCM 0x7E0").ToList();
        Assert.Equal("RUN", steps[^1].TypedPhrase);
        Assert.Contains("31 01 12 34 05", steps[^1].Message);
        Assert.Contains("does not know what this routine does", steps[0].Message);
    }

    [Fact]
    public void Describe_shows_status_bytes()
    {
        var at = new DateTimeOffset(2026, 11, 15, 12, 0, 1, TimeSpan.Zero);
        var line = RoutineCommand.Describe(at, "PCM", new RoutineResult(0x1234, RoutineControlType.RequestResults, new byte[] { 0x00, 0x55 }));
        Assert.EndsWith("PCM results 0x1234: status 0055", line);
        Assert.EndsWith("accepted (no status bytes)", RoutineCommand.Describe(at, "PCM", new RoutineResult(0x1234, RoutineControlType.Stop, Array.Empty<byte>())));
    }
}
