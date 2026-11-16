using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>UdsSessionHost: per-module clients, keep-alive under the shared bus lock, lost sessions, close-all.</summary>
public class UdsSessionHostTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Target(uint TxId, Bus Bus = Bus.HsCan, string Acronym = "M") : IBusTarget
    {
        public Bus DefaultBus => Bus;
        public bool FoundConfirmed => true;
    }

    /// <summary>Answers session control positively, stays silent to 3E 80; modules in <see cref="Dead"/> never answer.</summary>
    private sealed class Ecus : IDiagChannel
    {
        private uint _tx;
        public HashSet<uint> Dead { get; } = new();
        public List<(uint Tx, byte[] Req)> Sent { get; } = new();
        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) { _tx = txId; return Task.CompletedTask; }
        public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] req, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Sent.Add((_tx, req));
            IReadOnlyList<DiagResponse> none = Array.Empty<DiagResponse>();
            if (Dead.Contains(_tx) || (req[0] == 0x3E && (req[1] & 0x80) != 0)) return Task.FromResult(none);
            var resp = req[0] == 0x10 ? new byte[] { 0x50, req[1] } : new byte[] { (byte)(req[0] + 0x40) };
            return Task.FromResult<IReadOnlyList<DiagResponse>>(new[] { new DiagResponse(_tx + 8, resp) });
        }
    }

    private sealed class Fixture
    {
        public ManualClock Clock { get; } = new();
        public Ecus Ch { get; } = new();
        public SemaphoreSlim Bus { get; } = new(1, 1);
        public List<(uint Tx, Bus Bus)> Routed { get; } = new();
        public bool LockHeldDuringWork { get; private set; } = true;
        public UdsSessionHost Host { get; }

        public Fixture(bool readOnly = false)
        {
            Host = new UdsSessionHost(tx => new UdsClient(Ch, tx) { Clock = Clock, Gate = new WriteGate(() => readOnly) }, Bus,
                async (t, work, ct) => { Routed.Add((t.TxId, t.Bus)); if (Bus.CurrentCount != 0) LockHeldDuringWork = false; await work(); });
        }
    }

    [Fact]
    public void Client_is_cached_per_module()
    {
        var f = new Fixture();
        var a = f.Host.Client(new Target(0x7E0));
        Assert.Same(a, f.Host.Client(new Target(0x7E0)));
        Assert.NotSame(a, f.Host.Client(new Target(0x726)));
    }

    [Fact]
    public void Factory_must_return_a_client_for_the_requested_module()
    {
        var host = new UdsSessionHost(_ => new UdsClient(new Ecus(), 0x7E0), new SemaphoreSlim(1, 1), (_, w, _) => w());
        Assert.Throws<InvalidOperationException>(() => host.Client(new Target(0x726)));
    }

    [Fact]
    public async Task Nothing_is_sent_while_no_module_is_in_a_non_default_session()
    {
        var f = new Fixture();
        await f.Host.Client(new Target(0x7E0)).DiagnosticSessionControlAsync(UdsSession.Default);
        f.Clock.Now += TimeSpan.FromSeconds(30);
        Assert.Equal(0, await f.Host.TickAsync());
        Assert.Empty(f.Host.OpenSessions);
        Assert.Single(f.Ch.Sent);
    }

    [Fact]
    public async Task Keep_alive_is_sent_when_due_under_the_bus_lock_and_on_the_module_bus()
    {
        var f = new Fixture();
        var ms = new Target(0x726, Bus.MsCan);
        await f.Host.Client(ms).DiagnosticSessionControlAsync(UdsSession.Extended);
        Assert.Equal(new[] { (0x726u, UdsSession.Extended) }, f.Host.OpenSessions);

        f.Clock.Now += TimeSpan.FromSeconds(1.5);
        Assert.Equal(0, await f.Host.TickAsync());
        f.Clock.Now += TimeSpan.FromSeconds(0.5);
        Assert.Equal(1, await f.Host.TickAsync());

        Assert.Equal(0x726u, f.Ch.Sent[^1].Tx);
        Assert.Equal(new byte[] { 0x3E, 0x80 }, f.Ch.Sent[^1].Req);
        Assert.Equal((0x726u, Bus.MsCan), Assert.Single(f.Routed));
        Assert.True(f.LockHeldDuringWork);
        Assert.Equal(1, f.Bus.CurrentCount); // released
    }

    [Fact]
    public async Task Any_request_through_the_cached_client_postpones_the_keep_alive()
    {
        var f = new Fixture();
        var uds = f.Host.Client(new Target(0x7E0));
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Clock.Now += TimeSpan.FromSeconds(1.9);
        await uds.TesterPresentAsync();
        f.Clock.Now += TimeSpan.FromSeconds(1.9);
        Assert.Equal(0, await f.Host.TickAsync());
    }

    [Fact]
    public async Task Only_due_modules_get_a_keep_alive()
    {
        var f = new Fixture();
        var a = f.Host.Client(new Target(0x7E0));
        var b = f.Host.Client(new Target(0x726));
        await a.DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Clock.Now += TimeSpan.FromSeconds(1.5);
        await b.DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Clock.Now += TimeSpan.FromSeconds(0.6);
        Assert.Equal(1, await f.Host.TickAsync());
        Assert.Equal(0x7E0u, f.Ch.Sent[^1].Tx);
    }

    [Fact]
    public async Task Tick_does_not_take_the_lock_when_nothing_is_due()
    {
        var f = new Fixture();
        await f.Bus.WaitAsync(); // another command holds the adapter
        var t = f.Host.TickAsync();
        Assert.True(t.IsCompleted); // would deadlock / block if it waited for the lock
        Assert.Equal(0, await t);
        f.Bus.Release();
    }

    [Fact]
    public async Task Tick_waits_for_a_running_command_to_finish()
    {
        var f = new Fixture();
        await f.Host.Client(new Target(0x7E0)).DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Clock.Now += TimeSpan.FromSeconds(3);
        await f.Bus.WaitAsync();
        var t = f.Host.TickAsync();
        Assert.False(t.IsCompleted);
        int before = f.Ch.Sent.Count;
        f.Bus.Release();
        Assert.Equal(1, await t);
        Assert.Equal(before + 1, f.Ch.Sent.Count);
    }

    [Fact]
    public async Task A_failed_keep_alive_drops_the_module_and_reports_it()
    {
        var f = new Fixture();
        var lost = new List<uint>();
        f.Host.SessionLost += (tx, _) => lost.Add(tx);
        var old = f.Host.Client(new Target(0x7E0));
        await old.DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Clock.Now += TimeSpan.FromSeconds(3);

        // The bus router turns "no reply" into an exception; simulate that by failing the routed work.
        var failing = new UdsSessionHost(_ => old, f.Bus, (_, _, _) => throw new TimeoutException("No response from 0x7E0"));
        failing.SessionLost += (tx, _) => lost.Add(tx);
        failing.Client(new Target(0x7E0));
        Assert.Equal(0, await failing.TickAsync());
        Assert.Equal(new[] { 0x7E0u }, lost);
        Assert.Empty(failing.OpenSessions);
        Assert.Equal(1, f.Bus.CurrentCount);
    }

    [Fact]
    public async Task A_silent_module_does_not_count_as_lost_because_3E_80_expects_silence()
    {
        // 3E 80 is suppressed: silence is success at this layer. Loss is detected by the router/transport (no reply on the bus)
        // or by the next real request. This pins the behaviour so it is not mistaken for detection.
        var f = new Fixture();
        await f.Host.Client(new Target(0x7E0)).DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Ch.Dead.Add(0x7E0);
        f.Clock.Now += TimeSpan.FromSeconds(3);
        Assert.Equal(1, await f.Host.TickAsync());
        Assert.Single(f.Host.OpenSessions);
    }

    [Fact]
    public async Task Keep_alive_works_in_read_only_mode()
    {
        var f = new Fixture(readOnly: true);
        await f.Host.Client(new Target(0x7E0)).DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Clock.Now += TimeSpan.FromSeconds(3);
        Assert.Equal(1, await f.Host.TickAsync());
    }

    [Fact]
    public async Task Close_all_returns_open_sessions_to_default_and_forgets_clients()
    {
        var f = new Fixture();
        var a = f.Host.Client(new Target(0x7E0));
        var b = f.Host.Client(new Target(0x726, Bus.MsCan));
        f.Host.Client(new Target(0x760)); // never opened a session: no traffic for it
        await a.DiagnosticSessionControlAsync(UdsSession.Extended);
        await b.DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Ch.Dead.Add(0x726);
        int before = f.Ch.Sent.Count;

        var errors = await f.Host.CloseAllAsync();

        var sent = f.Ch.Sent.Skip(before).ToList();
        Assert.Equal(2, sent.Count);
        Assert.All(sent, s => Assert.Equal(new byte[] { 0x10, 0x01 }, s.Req));
        Assert.Equal(0x726u, Assert.Single(errors).TxId);
        Assert.Empty(f.Host.OpenSessions);
        Assert.NotSame(a, f.Host.Client(new Target(0x7E0)));
        Assert.Equal(1, f.Bus.CurrentCount);
    }

    [Fact]
    public async Task Client_uses_the_latest_bus_for_a_module()
    {
        var f = new Fixture();
        var first = new Target(0x726, Bus.HsCan);
        await f.Host.Client(first).DiagnosticSessionControlAsync(UdsSession.Extended);
        f.Host.Client(first with { Bus = Bus.MsCan });
        f.Clock.Now += TimeSpan.FromSeconds(3);
        await f.Host.TickAsync();
        Assert.Equal(Bus.MsCan, Assert.Single(f.Routed).Bus);
    }

    [Fact]
    public async Task Run_loop_stops_on_cancel()
    {
        var f = new Fixture();
        using var cts = new CancellationTokenSource();
        var run = f.Host.RunAsync(TimeProvider.System, cts.Token, TimeSpan.FromMilliseconds(5));
        await Task.Delay(30);
        cts.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Interval_must_be_below_s3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UdsSessionHost(tx => null!, new SemaphoreSlim(1, 1), (_, w, _) => w(), TimeSpan.FromSeconds(5)));
    }
}
