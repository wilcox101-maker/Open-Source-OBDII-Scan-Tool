using ObdLink.Core.Uds;

namespace ObdLink.Core.Service;

/// <summary>Outcome of one procedure run: what each step answered, in order.</summary>
public sealed record ProcedureRun(ServiceProcedure Procedure, IReadOnlyList<string> Log, byte[]? LastStatus)
{
    public string Summary => Log.Count == 0 ? "" : Log[^1];
}

/// <summary>
/// Runs a <see cref="ServiceProcedure"/> on one module. The caller has already shown <see cref="ServiceProcedure.ConfirmSteps"/>,
/// holds the adapter lock, and passes a client whose write gate applies (31 is state-changing). Session keep-alive is the
/// caller's session host (the App's <c>UdsSessionHost</c>).
/// <list type="bullet">
/// <item>A negative response to the start is definite (nothing ran): reported with the NRC's meaning, never retried.</item>
/// <item>A module that can't report results (31 03 → 0x12 / 0x31 / 0x11 / 0x24) is normal for one-shot resets: the run still
/// counts as started, and the procedure's own confirmation (a lamp, a horn) is what to watch.</item>
/// </list>
/// </summary>
public static class ServiceProcedureRunner
{
    public const int ResultPolls = 5;
    public static readonly TimeSpan ResultPollDelay = TimeSpan.FromSeconds(1);

    public static async Task<ProcedureRun> RunAsync(UdsClient uds, ServiceProcedure p, IProgress<string>? progress = null,
        Func<TimeSpan, Task>? delay = null, CancellationToken ct = default)
    {
        if (!p.Runnable) throw new InvalidOperationException($"{p.Name}: {p.NotRunnableReason}");
        if (uds.TargetTxId != p.TxId) throw new ArgumentException($"Client targets 0x{uds.TargetTxId:X3}; {p.Name} is for {p.Module} 0x{p.TxId:X3}", nameof(uds));
        delay ??= t => Task.Delay(t, ct);
        var log = new List<string>();
        byte[]? status = null;
        void Say(string m) { log.Add(m); progress?.Report(m); }

        bool started = false;
        try
        {
        foreach (var step in p.Steps)
        {
            ct.ThrowIfCancellationRequested();
            switch (step.Kind)
            {
                case ProcedureStepKind.Session:
                    await uds.DiagnosticSessionControlAsync(step.Session!.Value, ct);
                    Say($"{step.Text}: OK");
                    break;
                case ProcedureStepKind.StartRoutine:
                    try
                    {
                        var r = await uds.StartRoutineAsync(step.RoutineId!.Value, step.Option, ct);
                        status = r.StatusRecord;
                        started = true;
                        Say($"{step.Text}: accepted" + (r.StatusRecord.Length > 0 ? $" (status {Convert.ToHexString(r.StatusRecord)})" : ""));
                    }
                    catch (UdsNegativeResponseException e)
                    {
                        throw new InvalidOperationException($"{p.Name}: the {p.Module} refused routine 0x{step.RoutineId:X4} (NRC 0x{e.Nrc:X2} {UdsNrc.Describe(e.Nrc)}); nothing ran. " + Hint(e.Nrc, p), e);
                    }
                    break;
                case ProcedureStepKind.RequestResults:
                    status = await PollAsync(uds, step, delay, Say, ct) ?? status;
                    break;
                case ProcedureStepKind.Wait:
                    Say($"{step.Text}: waiting {step.Time!.Value.TotalSeconds:0} s");
                    await delay(step.Time.Value);
                    break;
            }
        }
        }
        finally
        {
            // X2: back to the default session, whatever happened. After an accepted start, wait ExitSettle first: leaving the session
            // stops a routine that is still running. Best-effort: a failed 10 01 is reported, not thrown (S3 ends the session anyway).
            if (p.LeavesSession && uds.ActiveSession is UdsSession s && s != UdsSession.Default)
            {
                if (started && p.ExitSettle is { } t && t > TimeSpan.Zero)
                {
                    Say($"waiting {t.TotalSeconds:0} s before leaving the extended session");
                    await SettleInSessionAsync(uds, t, delay, Say);
                }
                try { await uds.DiagnosticSessionControlAsync(UdsSession.Default, CancellationToken.None); Say("default session (10 01): OK"); }
                catch (Exception ex) { Say($"default session (10 01) failed: {ex.Message}; the module leaves the extended session by itself in a few seconds"); }
            }
        }
        Say($"{p.Name}: done. {string.Join(" ", p.After)}");
        return new ProcedureRun(p, log, status);
    }

    /// <summary>Keep-alive interval during the settle wait (below S3 = 5 s).</summary>
    public static readonly TimeSpan SettleKeepAlive = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Waits <paramref name="total"/> in the session, sending 3E 80 every <see cref="SettleKeepAlive"/>: the caller holds the adapter
    /// lock, so the session host's keep-alive can't run and the module would otherwise drop the session (and the routine) at S3.
    /// Cancellation or a failed keep-alive ends the wait early; the caller then sends 10 01.
    /// </summary>
    private static async Task SettleInSessionAsync(UdsClient uds, TimeSpan total, Func<TimeSpan, Task> delay, Action<string> say)
    {
        for (var left = total; left > TimeSpan.Zero; left -= SettleKeepAlive)
        {
            try
            {
                await delay(left < SettleKeepAlive ? left : SettleKeepAlive);
                await uds.TesterPresentAsync(suppressResponse: true, CancellationToken.None);
            }
            catch (OperationCanceledException) { say("wait cancelled"); return; }
            catch (Exception ex) { say($"keep-alive during the wait failed: {ex.Message}"); return; }
        }
    }

    private static async Task<byte[]?> PollAsync(UdsClient uds, ProcedureStep step, Func<TimeSpan, Task> delay, Action<string> say, CancellationToken ct)
    {
        for (int i = 0; i < ResultPolls; i++)
        {
            await delay(ResultPollDelay);
            try
            {
                var r = await uds.RequestRoutineResultsAsync(step.RoutineId!.Value, ct);
                say($"{step.Text}: status {(r.StatusRecord.Length == 0 ? "(none)" : Convert.ToHexString(r.StatusRecord))}");
                return r.StatusRecord;
            }
            catch (UdsNegativeResponseException e) when (e.Nrc is 0x21 or 0x78) { /* still running */ }
            catch (UdsNegativeResponseException e) when (e.Nrc is 0x11 or 0x12 or 0x24 or 0x31)
            {
                say($"{step.Text}: the module does not report results (NRC 0x{e.Nrc:X2}); watch the vehicle's own confirmation.");
                return null;
            }
        }
        say($"{step.Text}: no result after {ResultPolls} tries; watch the vehicle's own confirmation.");
        return null;
    }

    private static string Hint(byte nrc, ServiceProcedure p) => nrc switch
    {
        0x22 => "Conditions not correct: check: " + string.Join(" ", p.Conditions),
        0x31 => "Routine not supported by this module: the identifier from the source does not apply to this model year.",
        0x33 => "The module wants security access first; this procedure has no security step (identifiers incomplete).",
        0x7E or 0x7F => "Not allowed in this session.",
        _ => "",
    };
}
