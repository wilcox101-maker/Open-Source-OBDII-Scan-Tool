using ObdLink.Core.Elm;
using Serilog.Events;

namespace ObdLink.Core.Modules;

/// <summary>What <see cref="BusRouter"/> needs to know about a module to send a request to it and report back.</summary>
public interface IBusTarget
{
    /// <summary>The bus to try first (an earlier override, or the table bus).</summary>
    Bus Bus { get; }
    /// <summary>The module's bus in the definition table.</summary>
    Bus DefaultBus { get; }
    uint TxId { get; }
    string Acronym { get; }
    /// <summary>True when a scan confirmed the module answered (affects the no-reply message only).</summary>
    bool FoundConfirmed { get; }
}

/// <summary>
/// v0.9.1: routes a diagnostic request to a module's bus, with the no-reply fallback across the Ford buses.
/// Extracted verbatim from MainViewModel.OnBus/OnBusCore so it can be tested against the simulator; the view model
/// now delegates here and keeps only the UI side effects (persisting the chosen bus, updating the grid).
///
/// Behaviour, unchanged:
///  - HS-CAN: re-selects the HS-CAN preset only if the adapter is not already on it, then runs the work. No restore.
///  - MS-CAN / HS-CAN2: selects the bus, runs the work, and always restores the HS-CAN default afterwards.
///  - A non-HS bus on an adapter that cannot switch buses throws before any traffic.
///  - On a no-reply error, when fallback is allowed and the adapter can switch buses, tries the remaining Ford buses
///    (remembered bus first, then the table bus, then the others) and reports the first that answers.
/// </summary>
public sealed class BusRouter(AdapterSession session, Func<bool> canSwitchBus, Action<LogEventLevel, string, string>? log = null)
{
    private void Log(LogEventLevel level, string text) => log?.Invoke(level, "Bus", text);

    /// <summary>
    /// Runs <paramref name="work"/> with the adapter on <paramref name="m"/>'s bus. <paramref name="onBusAnswered"/>
    /// fires with the bus that answered (on the first try or after fallback). Returns that bus.
    /// Throws <see cref="InvalidOperationException"/> with a user-facing message when no bus answers.
    /// </summary>
    public async Task<Bus> RunAsync(IBusTarget m, Func<Task> work, bool allowFallback = true, Action<Bus>? onBusAnswered = null, CancellationToken ct = default)
    {
        try
        {
            await OnBusCore(m.Bus, m, work, ct);
            onBusAnswered?.Invoke(m.Bus);
            return m.Bus;
        }
        catch (Exception ex) when (allowFallback && ModuleReplies.IsNoReply(ex) && canSwitchBus() && m.Bus is Bus.HsCan or Bus.MsCan or Bus.HsCan2)
        {
            Log(LogEventLevel.Warning, $"{m.Acronym} 0x{m.TxId:X3}: no reply on {ModuleReplies.BusName(m.Bus)} ({ex.Message}); trying the other buses");
            var next = ModuleBusMemory.Remaining(m.Bus, m.DefaultBus);
            if (next.Length == 0) next = ModuleReplies.Alternates(m.Bus);
            foreach (var alt in next)
            {
                try { await OnBusCore(alt, m, work, ct); }
                catch (Exception ex2) when (ModuleReplies.IsNoReply(ex2)) { Log(LogEventLevel.Information, $"{m.Acronym}: no reply on {ModuleReplies.BusName(alt)} ({ex2.Message})"); continue; }
                onBusAnswered?.Invoke(alt);
                Log(LogEventLevel.Information, $"{m.Acronym} 0x{m.TxId:X3} answered on {ModuleReplies.BusName(alt)}; using that bus from now on");
                return alt;
            }
            throw new InvalidOperationException(ModuleReplies.NoReplyMessage(m.Acronym, m.TxId, m.Bus, m.FoundConfirmed, triedBoth: true));
        }
        catch (Exception ex) when (ModuleReplies.IsNoReply(ex))
        {
            throw new InvalidOperationException(ModuleReplies.NoReplyMessage(m.Acronym, m.TxId, m.Bus, m.FoundConfirmed, triedBoth: false));
        }
    }

    private async Task OnBusCore(Bus bus, IBusTarget m, Func<Task> work, CancellationToken ct)
    {
        if (bus != Bus.HsCan && !canSwitchBus())
            throw new InvalidOperationException($"{m.Acronym} is on {ModuleReplies.BusName(bus)}; this adapter cannot use the MS-CAN pins (OBDLink MX+ / EX can).");
        // HS-CAN reads used to assume the adapter was already there. A previous MS-CAN / HS-CAN2 session, or the console, may have left another protocol selected.
        if (bus == Bus.HsCan)
        {
            if (session.CurrentProtocol != StnProtocol.HsCan11_500) await session.SelectBusAsync(Bus.HsCan, ct);
            await work();
            return;
        }
        await session.SelectBusAsync(bus, ct);
        try { await work(); }
        finally { await RestoreDefaultAsync(ct); }
    }

    /// <summary>HS-CAN 500k 11-bit (STP 33, or ATSP6 on plain ELM). Same as MainViewModel.SelectDiagProtocol.</summary>
    private async Task RestoreDefaultAsync(CancellationToken ct)
    {
        if (session.IsStn) await session.SelectProtocolAsync(StnProtocol.HsCan11_500, ct);
        else await session.SelectElmProtocolAsync('6', ct);
    }
}
