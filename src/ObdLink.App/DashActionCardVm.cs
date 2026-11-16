using ObdLink.Core.Dashboard;
using ObdLink.Core.Logging;
using ObdLink.Core.Uds;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// A write card on a dashboard (v0.20.0). Every press re-validates the card (<see cref="DashActions.Plan"/>), then, holding the
/// shared adapter lock on the module's bus: write gate + battery check (<c>EnsureVoltageAsync</c>), the confirmation with a typed
/// phrase (RUN / HOLD / WRITE), extended session, and the request.
/// <list type="bullet">
/// <item>Routine: 31 01; the status bytes are shown on the card.</item>
/// <item>Output hold: 2F did 03 state with a <see cref="ControlLease"/>. Pressing again, <see cref="DashActions.HoldSeconds"/> s
/// passing, removing the card or closing the window sends 2F did 00 (allowed even after writes are switched off).</item>
/// <item>DID write: reads the current value first (shown as old → new and logged so it can be written back), writes, reads back
/// and says whether the module now holds the new value.</item>
/// </list>
/// </summary>
public sealed class DashActionCardVm : ObservableObject
{
    private readonly DashActionHost? _host;
    private readonly IEventLog _log;
    private ControlLease? _lease;
    private uint _leaseTx;

    public DashItem Item { get; }
    public DashAction Spec => Item.Action!;
    public AsyncCommand RunCommand { get; }

    public DashActionCardVm(DashItem item, DashActionHost? host, IEventLog log)
    {
        Item = item; _host = host; _log = log;
        RunCommand = new AsyncCommand($"Dashboard write card: {Label}", log, RunAsync, (n, e) => { Status = "Failed: " + e.Message; _host?.Err(n, e); },
            () => new (string, object?)[] { ("card", Label), ("kind", Spec.Kind), ("module", Spec.Module), ("id", Spec.Id) });
    }

    public string Label => string.IsNullOrWhiteSpace(Spec.Label) ? Spec.Kind.ToString() : Spec.Label;
    public string Description
    {
        get
        {
            try { var p = DashActions.Plan(Spec); return $"{p.Summary}: {p.RequestHex}"; }
            catch (Exception ex) when (ex is FormatException or RoutineRefusedException) { return "Not usable: " + ex.Message; }
        }
    }
    public bool Valid
    {
        get { try { DashActions.Plan(Spec); return true; } catch (Exception ex) when (ex is FormatException or RoutineRefusedException) { return false; } }
    }
    private bool _holding; public bool Holding { get => _holding; private set { if (Set(ref _holding, value)) OnPropertyChanged(nameof(ButtonText)); } }
    public string ButtonText => Spec.Kind switch
    {
        DashActionKind.Routine => "Run…",
        DashActionKind.OutputHold => Holding ? "Release" : "Hold…",
        _ => "Write…",
    };
    private string _status = "Not run yet."; public string Status { get => _status; private set => Set(ref _status, value); }

    private DashActionHost Host => _host ?? throw new InvalidOperationException("Write cards need a connected adapter (a DEMO port works).");

    private async Task RunAsync()
    {
        if (_lease is not null) { await ReleaseAsync("released by the user"); return; }
        var plan = DashActions.Plan(Spec);
        var module = Host.ModuleName(plan.TxId);
        string? before = null;
        await Host.OnModule(plan.TxId, async uds =>
        {
            var safety = Host.Safety();
            await safety.EnsureVoltageAsync(); // write gate + battery window
            if (plan.Kind == DashActionKind.WriteDid)
            {
                try { before = Convert.ToHexString(await uds.ReadDataByIdentifierAsync(plan.Id)); }
                catch (UdsNegativeResponseException ex) { _log.Message(LogEventLevel.Warning, "Dashboard", $"{Label}: current value of DID 0x{plan.Id:X4} unreadable before the write", ex); }
            }
            await safety.RequireAsync(DashActions.Steps(plan, Label, module, before));
            await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
            switch (plan.Kind)
            {
                case DashActionKind.Routine:
                    var r = await uds.StartRoutineAsync(plan.Id, plan.Data);
                    Status = RoutineCommand.Describe(DateTimeOffset.Now, module, r);
                    break;
                case DashActionKind.OutputHold:
                    var (res, lease) = await uds.AdjustOutputAsync(plan.Id, plan.Data);
                    _lease = lease; _leaseTx = plan.TxId; Holding = true;
                    Status = $"{DateTimeOffset.Now:HH:mm:ss} holding DID 0x{plan.Id:X4} (status {Convert.ToHexString(res.ControlStatus)}); releases in {DashActions.HoldSeconds} s or on the next press.";
                    break;
                default:
                    _log.Message(LogEventLevel.Information, "Dashboard", $"{Label}: DID 0x{plan.Id:X4} on 0x{plan.TxId:X3} was {before ?? "(unreadable)"} before the write");
                    await uds.WriteDataByIdentifierAsync(plan.Id, plan.Data);
                    var back = await uds.ReadDataByIdentifierAsync(plan.Id);
                    var ok = back.AsSpan().SequenceEqual(plan.Data);
                    Status = $"{DateTimeOffset.Now:HH:mm:ss} wrote {Convert.ToHexString(plan.Data)} (was {before ?? "unreadable"}); read back {Convert.ToHexString(back)}: " + (ok ? "verified." : "DIFFERENT - the module did not keep the value.");
                    if (!ok) _log.Message(LogEventLevel.Warning, "Dashboard", $"{Label}: read-back {Convert.ToHexString(back)} differs from written {Convert.ToHexString(plan.Data)}");
                    break;
            }
        });
        if (_lease is { } held) _ = AutoReleaseAsync(held);
    }

    private async Task AutoReleaseAsync(ControlLease lease)
    {
        await Task.Delay(TimeSpan.FromSeconds(DashActions.HoldSeconds));
        if (ReferenceEquals(_lease, lease)) await ReleaseAsync($"released after {DashActions.HoldSeconds} s");
    }

    /// <summary>Returns a held output to the module (2F did 00). Safe to call when nothing is held. Never throws.</summary>
    public async Task ReleaseAsync(string why)
    {
        var lease = _lease;
        if (lease is null) return;
        _lease = null;
        try
        {
            if (_host is null) await lease.RestoreAsync();
            else await _host.OnModule(_leaseTx, _ => lease.RestoreAsync());
            Status = $"{DateTimeOffset.Now:HH:mm:ss} output returned to the module ({why}).";
        }
        catch (Exception ex)
        {
            // The module also returns control when its extended session times out (ISO 14229 S3), the backstop if this fails.
            Status = $"Could not return the output ({ex.Message}). It returns when the module's session times out; cycle the ignition if unsure.";
            _log.Message(LogEventLevel.Error, "Dashboard", $"{Label}: output restore failed ({why})", ex);
        }
        finally { Holding = false; }
    }
}
