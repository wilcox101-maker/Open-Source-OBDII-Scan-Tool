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
/// and says whether the module now holds the new value (<see cref="DashWrite"/>: a lost or pending reply is settled by the read-back,
/// a refusal says what to do next, a different value suggests an ignition cycle or Undo). <see cref="UndoCommand"/> then writes the old value back (same gate,
/// battery check and typed WRITE), but only while the module still holds what the card wrote.</item>
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
    public AsyncCommand UndoCommand { get; }
    private DashWriteUndo? _undo;
    /// <summary>True after a DID write whose old value was read: the card offers Undo.</summary>
    public bool CanUndo => _undo is not null;
    public string UndoTip => _undo?.Summary ?? "";

    public DashActionCardVm(DashItem item, DashActionHost? host, IEventLog log)
    {
        Item = item; _host = host; _log = log;
        RunCommand = new AsyncCommand($"Dashboard write card: {Label}", log, RunAsync, (n, e) => { Status = "Failed: " + e.Message; _host?.Err(n, e); },
            () => new (string, object?)[] { ("card", Label), ("kind", Spec.Kind), ("module", Spec.Module), ("id", Spec.Id) });
        UndoCommand = new AsyncCommand($"Dashboard write card undo: {Label}", log, UndoAsync, (n, e) => { Status = "Undo failed: " + e.Message; _host?.Err(n, e); },
            () => new (string, object?)[] { ("card", Label), ("module", Spec.Module), ("id", Spec.Id), ("old", _undo is null ? null : Convert.ToHexString(_undo.Before)) });
    }

    private void SetUndo(DashWriteUndo? u) { _undo = u; OnPropertyChanged(nameof(CanUndo)); OnPropertyChanged(nameof(UndoTip)); }

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
                    // Undo is offered before the write is sent, so a write whose answer is lost can still be undone. Undo re-reads
                    // the DID first: if the write never landed it finds the old value and has nothing to do.
                    byte[]? old = before is null ? null : Convert.FromHexString(before);
                    SetUndo(DashActions.UndoFor(Spec, old, plan.Data, plan.Data));
                    // A lost or "pending" reply is settled by reading back; a refusal is definite (nothing changed). See DashWrite.
                    var o = await DashWrite.WriteAndSettleAsync(uds, plan.Id, plan.Data, old);
                    // Not applied: the module still holds the old value, nothing to undo. Unknown: Undo stays (it re-reads before writing).
                    SetUndo(o.Verdict == DashWriteVerdict.NotApplied ? null : DashActions.UndoFor(Spec, old, plan.Data, o.ReadBack ?? plan.Data));
                    Status = $"{DateTimeOffset.Now:HH:mm:ss} {o.Message}" + (before is null ? " (old value was unreadable: no undo)" : "");
                    if (!o.Verified) _log.Message(LogEventLevel.Warning, "Dashboard", $"{Label}: {o.Message}");
                    break;
            }
        });
        if (_lease is { } held) _ = AutoReleaseAsync(held);
    }

    private async Task UndoAsync()
    {
        var undo = _undo ?? throw new InvalidOperationException("Nothing to undo: write with this card first (its old value must have been readable).");
        var module = Host.ModuleName(undo.Plan.TxId);
        await Host.OnModule(undo.Plan.TxId, async uds =>
        {
            var safety = Host.Safety();
            await safety.EnsureVoltageAsync(); // write gate + battery window
            var now = await uds.ReadDataByIdentifierAsync(undo.Plan.Id);
            if (undo.Refusal(now) is string no)
            {
                if (now.AsSpan().SequenceEqual(undo.Before)) SetUndo(null);
                throw new InvalidOperationException(no);
            }
            await safety.RequireAsync(DashActions.Steps(undo.Plan, Label + " (undo)", module, Convert.ToHexString(now)));
            await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
            _log.Message(LogEventLevel.Information, "Dashboard", $"{Label}: undo, DID 0x{undo.Plan.Id:X4} on 0x{undo.Plan.TxId:X3} {Convert.ToHexString(now)} → {Convert.ToHexString(undo.Before)}");
            var o = await DashWrite.WriteAndSettleAsync(uds, undo.Plan.Id, undo.Before, now);
            if (o.Verified) SetUndo(null);
            Status = $"{DateTimeOffset.Now:HH:mm:ss} undo: {o.Message}";
            if (!o.Verified) _log.Message(LogEventLevel.Warning, "Dashboard", $"{Label}: undo: {o.Message}");
        });
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
