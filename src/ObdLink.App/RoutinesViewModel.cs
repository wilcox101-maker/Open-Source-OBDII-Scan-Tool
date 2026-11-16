using System.Collections.ObjectModel;
using ObdLink.Core.Logging;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// Routines tab (UDS 0x31): enter the extended session, start / stop a routine by identifier, read its results.
/// The module's client comes from <see cref="UdsSessionHost"/>, so the session survives between clicks and the host keeps it
/// alive. Start needs write mode (the client's gate), a battery check and a typed RUN; Stop needs write mode and a confirmation;
/// Results needs write mode (31 is state-changing as a service). Session control works in read-only mode.
/// Pyrotechnic (E200-E2FF) and programming (FF00/FF01) identifiers are refused before anything is sent.
/// Every command holds the shared bus lock, like every other adapter command.
/// </summary>
public sealed class RoutinesViewModel : ObservableObject
{
    private readonly IEventLog _log;
    private readonly Func<UdsSessionHost?> _host;
    private readonly Func<ModuleRow?> _module;
    private readonly Func<ModuleRow, Func<Task>, bool, Task> _onBus;
    private readonly SemaphoreSlim _bus;
    private readonly Func<SafetyGuard> _safety;
    private readonly Action<string, Exception> _err;

    public ObservableCollection<string> Results { get; } = new();

    private string _routineId = "";
    public string RoutineId { get => _routineId; set => Set(ref _routineId, value); }
    private string _option = "";
    public string Option { get => _option; set => Set(ref _option, value); }
    private string _sessionText = "Not connected.";
    public string SessionText { get => _sessionText; private set => Set(ref _sessionText, value); }

    public AsyncCommand ExtendedSessionCommand { get; }
    public AsyncCommand DefaultSessionCommand { get; }
    public AsyncCommand StartCommand { get; }
    public AsyncCommand StopCommand { get; }
    public AsyncCommand ResultsCommand { get; }
    public RelayCommand ClearCommand { get; }

    public RoutinesViewModel(IEventLog log, Func<UdsSessionHost?> host, Func<ModuleRow?> module, Func<ModuleRow, Func<Task>, bool, Task> onBus,
        SemaphoreSlim bus, Func<SafetyGuard> safety, Action<string, Exception> err)
    {
        _log = log; _host = host; _module = module; _onBus = onBus; _bus = bus; _safety = safety; _err = err;
        (string, object?)[] P() => new (string, object?)[] { ("module", _module()?.Label), ("addr", _module()?.Address), ("routine", RoutineId), ("option", Option) };
        AsyncCommand C(string name, Func<Task> f) => new(name, log, async () =>
        {
            await _bus.WaitAsync();
            try { await f(); }
            finally { _bus.Release(); Refresh(); }
        }, _err, P);

        ExtendedSessionCommand = C("Routines: extended session (10 03)", () => Session(UdsSession.Extended));
        DefaultSessionCommand = C("Routines: default session (10 01)", () => Session(UdsSession.Default));
        StartCommand = C("Routines: start (31 01)", StartAsync);
        StopCommand = C("Routines: stop (31 02)", StopAsync);
        ResultsCommand = C("Routines: results (31 03)", ResultsAsync);
        ClearCommand = new RelayCommand("Routines: clear results", log, _ => Results.Clear());
    }

    /// <summary>Updates the session line. Call on the UI thread.</summary>
    public void Refresh()
    {
        var host = _host();
        if (host is null) { SessionText = "Not connected."; return; }
        var open = host.OpenSessions;
        SessionText = open.Count == 0
            ? "No module is in a non-default session."
            : "Kept alive (3E 80 every 2 s): " + string.Join(", ", open.Select(o => $"0x{o.TxId:X3} {o.Session}"));
    }

    /// <summary>A keep-alive failed: show it in the results list. Call on the UI thread.</summary>
    public void NoteSessionLost(uint tx, string why)
    {
        Add($"{DateTimeOffset.Now:HH:mm:ss} 0x{tx:X3} session lost (keep-alive failed: {why}). Enter the extended session again before the next routine.");
        Refresh();
    }

    private (ModuleRow M, UdsClient Uds) Target()
    {
        var host = _host() ?? throw new InvalidOperationException("Not connected - click Connect first (use a DEMO port to try without a vehicle)");
        var m = _module() ?? throw new InvalidOperationException("Select a module first");
        return (m, host.Client(m));
    }

    private async Task Session(UdsSession s)
    {
        var (m, uds) = Target();
        await _onBus(m, () => uds.DiagnosticSessionControlAsync(s), s == UdsSession.Extended);
        Add($"{DateTimeOffset.Now:HH:mm:ss} {m.Label} session {s} confirmed (P2 {uds.ServerTiming.P2.TotalMilliseconds:0} ms, P2* {uds.ServerTiming.P2Star.TotalMilliseconds:0} ms)");
    }

    private async Task StartAsync()
    {
        var cmd = RoutineCommand.Parse(RoutineId, Option);
        var (m, uds) = Target();
        var safety = _safety();
        await safety.EnsureVoltageAsync();
        await safety.RequireAsync(cmd.StartSteps($"{m.Label} (0x{m.Address})"));
        await Run(m, () => uds.StartRoutineAsync(cmd.RoutineId, cmd.Option), cmd);
    }

    private async Task StopAsync()
    {
        var cmd = RoutineCommand.Parse(RoutineId, Option);
        var (m, uds) = Target();
        await _safety().RequireAsync(cmd.StopSteps($"{m.Label} (0x{m.Address})"));
        await Run(m, () => uds.StopRoutineAsync(cmd.RoutineId, cmd.Option), cmd);
    }

    private async Task ResultsAsync()
    {
        var cmd = RoutineCommand.Parse(RoutineId, null);
        var (m, uds) = Target();
        await Run(m, () => uds.RequestRoutineResultsAsync(cmd.RoutineId), cmd);
    }

    /// <summary>Routines never fall back to another bus. A negative response is also written to the results list.</summary>
    private async Task Run(ModuleRow m, Func<Task<RoutineResult>> f, RoutineCommand cmd)
    {
        RoutineResult? r = null;
        try { await _onBus(m, async () => r = await f(), false); }
        catch (UdsNegativeResponseException ex)
        {
            Add($"{DateTimeOffset.Now:HH:mm:ss} {m.Label} {cmd.IdText}: NRC 0x{ex.Nrc:X2} {UdsNrc.Describe(ex.Nrc)} - {UdsNrc.Explain(ex.Nrc, ex.Service)}");
            throw;
        }
        var line = RoutineCommand.Describe(DateTimeOffset.Now, m.Label, r!);
        Add(line);
        _log.Message(LogEventLevel.Information, "Routines", line);
    }

    private void Add(string line)
    {
        Results.Insert(0, line);
        while (Results.Count > 500) Results.RemoveAt(Results.Count - 1);
    }
}
