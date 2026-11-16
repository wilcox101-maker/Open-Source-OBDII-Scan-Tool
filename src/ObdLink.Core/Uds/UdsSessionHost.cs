using ObdLink.Core.Modules;

namespace ObdLink.Core.Uds;

/// <summary>
/// Owns one <see cref="UdsClient"/> per module for the life of a connection, so the session a module confirmed (and its
/// P2/P2* timing) is remembered between commands, and keeps every non-default session alive with a suppressed TesterPresent.
/// <para>
/// Concurrency: <see cref="TickAsync"/> and <see cref="CloseAllAsync"/> take <c>bus</c>, the same exclusive lock every other
/// adapter command holds, and route through <c>runOnBus</c> (the App passes <see cref="BusRouter"/>), so a keep-alive for an
/// MS-CAN module switches buses the same way any other request does and never interleaves with another module's request.
/// Do not call them while already holding that lock. <see cref="Client"/> itself takes no lock; call it from inside the
/// command that holds the lock.
/// </para>
/// <para>
/// Limitation: a single command that holds the lock for longer than S3 (5 s) against a different module (for example reading
/// every As-Built block of another module) can let an idle module's session time out. When a keep-alive then fails, the module
/// is dropped (<see cref="SessionLost"/>) and its next client starts again from "no session confirmed".
/// </para>
/// </summary>
public sealed class UdsSessionHost
{
    private readonly Func<uint, UdsClient> _create;
    private readonly SemaphoreSlim _bus;
    private readonly Func<IBusTarget, Func<Task>, CancellationToken, Task> _runOnBus;
    private readonly TimeSpan _interval;
    private readonly Dictionary<uint, Entry> _entries = new();
    private readonly object _sync = new();

    private sealed record Entry(IBusTarget Target, UdsClient Client, TesterPresentKeeper Keeper);

    /// <summary>A module's session ended without the app asking (keep-alive failed): its TxId and why.</summary>
    public event Action<uint, string>? SessionLost;

    /// <param name="create">Builds a client for a module TxId (the App sets Gate and SeedKey here).</param>
    /// <param name="bus">The adapter lock shared with every other command.</param>
    /// <param name="runOnBus">Runs work with the adapter on the module's bus; no fallback to other buses.</param>
    /// <param name="interval">Keep-alive interval, below S3 (default 2 s).</param>
    public UdsSessionHost(Func<uint, UdsClient> create, SemaphoreSlim bus, Func<IBusTarget, Func<Task>, CancellationToken, Task> runOnBus, TimeSpan? interval = null)
    {
        _create = create; _bus = bus; _runOnBus = runOnBus;
        _interval = interval ?? TimeSpan.FromSeconds(2);
        if (_interval <= TimeSpan.Zero || _interval >= TesterPresentKeeper.S3Server)
            throw new ArgumentOutOfRangeException(nameof(interval), _interval, "Keep-alive interval must be above zero and below S3");
    }

    /// <summary>The cached client for this module (created on first use). The target is refreshed so a later bus override is used.</summary>
    public UdsClient Client(IBusTarget m)
    {
        lock (_sync)
        {
            if (_entries.TryGetValue(m.TxId, out var e))
            {
                if (!ReferenceEquals(e.Target, m)) _entries[m.TxId] = e with { Target = m };
                return e.Client;
            }
            var c = _create(m.TxId);
            if (c.TargetTxId != m.TxId) throw new InvalidOperationException($"Client factory returned 0x{c.TargetTxId:X3} for module 0x{m.TxId:X3}");
            _entries[m.TxId] = new Entry(m, c, new TesterPresentKeeper(c, _interval));
            return c;
        }
    }

    /// <summary>Modules whose last confirmed session is not the default session.</summary>
    public IReadOnlyList<(uint TxId, UdsSession Session)> OpenSessions
    {
        get
        {
            lock (_sync)
                return _entries.Values
                    .Where(e => e.Client.ActiveSession is UdsSession s && s != UdsSession.Default)
                    .Select(e => (e.Target.TxId, e.Client.ActiveSession!.Value))
                    .OrderBy(x => x.TxId).ToList();
        }
    }

    /// <summary>
    /// Sends 3E 80 to every module whose keep-alive is due. Takes the bus lock only when at least one is due.
    /// A module whose keep-alive fails is forgotten and reported through <see cref="SessionLost"/>. Returns how many were sent.
    /// </summary>
    public async Task<int> TickAsync(CancellationToken ct = default)
    {
        if (!Snapshot().Any(e => e.Keeper.IsDue)) return 0;
        await _bus.WaitAsync(ct);
        int sent = 0;
        var lost = new List<(uint, string)>();
        try
        {
            foreach (var e in Snapshot())
            {
                if (!e.Keeper.IsDue) continue; // re-checked under the lock: a command may just have talked to it
                try
                {
                    bool did = false;
                    await _runOnBus(e.Target, async () => did = await e.Keeper.TickAsync(ct), ct);
                    if (did) sent++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    Forget(e.Target.TxId);
                    lost.Add((e.Target.TxId, ex.Message));
                }
            }
        }
        finally { _bus.Release(); }
        foreach (var (tx, why) in lost) SessionLost?.Invoke(tx, why);
        return sent;
    }

    /// <summary>Calls <see cref="TickAsync"/> every <paramref name="period"/> (default 250 ms) until cancelled. Errors other than cancellation are passed to <paramref name="onError"/>.</summary>
    public async Task RunAsync(TimeProvider clock, CancellationToken ct, TimeSpan? period = null, Action<Exception>? onError = null)
    {
        var p = period ?? TimeSpan.FromMilliseconds(250);
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(p, clock, ct); await TickAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex) { onError?.Invoke(ex); }
        }
    }

    /// <summary>
    /// Best effort: asks every module in a non-default session to return to the default session (10 01), then forgets all
    /// clients. Used before disconnecting. Failures are returned, not thrown. Takes the bus lock.
    /// </summary>
    public async Task<IReadOnlyList<(uint TxId, string Error)>> CloseAllAsync(CancellationToken ct = default)
    {
        var errors = new List<(uint, string)>();
        await _bus.WaitAsync(ct);
        try
        {
            foreach (var e in Snapshot().Where(e => e.Client.ActiveSession is UdsSession s && s != UdsSession.Default))
            {
                try { await _runOnBus(e.Target, () => e.Client.DiagnosticSessionControlAsync(UdsSession.Default, ct), ct); }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { errors.Add((e.Target.TxId, ex.Message)); }
            }
        }
        finally { _bus.Release(); Clear(); }
        return errors;
    }

    /// <summary>Drops one module's client without any traffic (its next <see cref="Client"/> starts fresh).</summary>
    public void Forget(uint txId) { lock (_sync) _entries.Remove(txId); }

    /// <summary>Drops every client without any traffic (the link is already gone).</summary>
    public void Clear() { lock (_sync) _entries.Clear(); }

    private List<Entry> Snapshot() { lock (_sync) return _entries.Values.OrderBy(e => e.Target.TxId).ToList(); }
}
