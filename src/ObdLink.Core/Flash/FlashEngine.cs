using System.Diagnostics;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Uds;
using Serilog;

namespace ObdLink.Core.Flash;

public enum FlashPhase
{
    Preflight, Backup, ExtendedSession, ProgrammingSession, SecurityAccess, Erase, Download, TransferExit,
    CheckDependencies, Reset, Verify, Done,
}

public sealed record FlashProgress(FlashPhase Phase, int Segment, int SegmentCount, long BytesDone, long BytesTotal, int Retries, string Message)
{
    public double Fraction => BytesTotal == 0 ? 0 : (double)BytesDone / BytesTotal;
}

public sealed record FlashResult(bool PartNumberConfirmed, string? ModulePartAfter, int Retries, TimeSpan Elapsed, int Attempt, string Message);

/// <summary>
/// Module flashing (ISO 14229-1 download): 10 03 → 10 02 → 27 → 31 01 FF00 (erase) → per segment 34 / 36×N / 37 → 31 01 FF01
/// (check) → 11 → verify. Designed to survive faults:
/// <list type="bullet">
/// <item>Every acknowledged step is written to an <see cref="IFlashJournal"/> before the next request, so a PC crash leaves an open job.</item>
/// <item>A TransferData block that gets no answer is sent again with the SAME block sequence counter (a server answers a repeat of
/// the last accepted counter positively without writing twice, ISO 14229-1), up to <see cref="FlashRetryPolicy.BlockRetries"/>.</item>
/// <item>Each failure is classified (<see cref="FlashFaults"/>): wait on "response pending", pause on voltage NRCs, stop on
/// programming failures and security errors, interrupt on link loss.</item>
/// <item>Once the programming session was requested, the engine never sends ECUReset or leaves the session on its own after a
/// failure: a module left in its bootloader can usually be flashed again, and a reset can make that harder.</item>
/// <item>An interrupted job is resumed by calling <see cref="RunAsync"/> again with a new <see cref="UdsClient"/> (same or other
/// adapter). A resume always restarts from the erase: bootloaders generally cannot continue mid-segment.</item>
/// <item>The <c>stop</c> token is only honored at safe points: before the programming session (nothing changed) and between
/// segments (the job stays open for recovery). A transfer in progress is never cut mid-block.</item>
/// </list>
/// The caller must open the write gate and supply a backup step; the engine refuses to start a new job without one.
/// </summary>
public sealed class FlashEngine
{
    private readonly FlashPlan _plan;
    private readonly IFlashJournal _journal;
    private int _retries;
    private FlashPhase _phase;
    private int _segment;
    private long _bytesDone;

    public FlashEngine(FlashPlan plan, IFlashJournal journal) { _plan = plan; _journal = journal; }

    /// <summary>Battery voltage (ATRV). Required: the flash is refused when the voltage cannot be read.</summary>
    public Func<CancellationToken, Task<double?>>? ReadVoltage { get; init; }
    /// <summary>Saves the module's configuration (As-Built etc.) and returns a reference to the backup (e.g. its file name). Required for a new job.</summary>
    public Func<CancellationToken, Task<string>>? Backup { get; init; }
    /// <summary>Reads the module's software part number after the reset, to confirm the new software runs. Optional.</summary>
    public Func<UdsClient, CancellationToken, Task<string?>>? ReadModulePart { get; init; }
    public IHostPower Host { get; init; } = UnknownHostPower.Instance;
    public IProgress<FlashProgress>? Progress { get; init; }
    /// <summary>Delay hook (tests replace it to run fast).</summary>
    public Func<TimeSpan, Task> Delay { get; init; } = t => Task.Delay(t);
    /// <summary>Blocks between in-flight voltage checks.</summary>
    public int VoltageCheckEveryBlocks { get; init; } = 16;
    /// <summary>Longest silence while the module is in the programming session: every wait sends 3E 80 at least this often (S3 is 5 s).</summary>
    public TimeSpan KeepAliveInterval { get; init; } = Uds.TesterPresentKeeper.DefaultInterval;
    /// <summary>Automatic re-entries into programming (from the erase) after the session or security was lost. Then the job stays open for resume.</summary>
    public int MaxSessionRecoveries { get; init; } = 1;

    private UdsClient? _uds;
    private bool _inSession, _unlocked;

    public FlashJournalState State => FlashJournalState.From(_journal.ReadAll());

    /// <summary>Marks an open job as abandoned by the operator (the journal keeps everything). The backup is never deleted.</summary>
    public void Abandon(string reason) => Write(FlashJournalKind.Abandoned, detail: reason);

    public async Task<FlashResult> RunAsync(UdsClient uds, CancellationToken stop = default)
    {
        var none = CancellationToken.None; // I/O is never cut mid-step; `stop` is checked at safe points only
        var sw = Stopwatch.StartNew();
        var st = State;
        bool resume = st.Open;
        if (resume && (st.ImageSha256 != _plan.Image.Sha256 || st.TxId != _plan.TxId))
            throw new FlashFailedException($"An open flash job exists for module 0x{st.TxId:X3} with a different image. Recover or abandon it first.", "Start", true);
        if (ReadVoltage is null) throw new FlashFailedException("Flashing needs a battery voltage reading; none is available.", "Start", false);
        if (!resume && Backup is null) throw new FlashFailedException("Flashing needs a backup step; none was supplied.", "Start", false);
        if (uds.Gate is { ReadOnly: true }) throw new FlashFailedException("Read-only mode is on. Turn on writes for this session before flashing.", "Start", false);
        if (uds.TargetTxId != _plan.TxId) throw new ArgumentException($"Client targets 0x{uds.TargetTxId:X3}, plan targets 0x{_plan.TxId:X3}", nameof(uds));

        using var awake = Host.KeepAwake();
        _retries = 0; _segment = 0; _bytesDone = 0; _uds = uds; _inSession = false; _unlocked = false;
        bool inBootloader = resume;
        try
        {
            SetPhase(FlashPhase.Preflight, resume ? $"resuming open job (attempt {st.Attempts + 1})" : "checking battery");
            await EnsureVoltageAsync(waitIfLow: resume);
            if (resume) Write(FlashJournalKind.Resumed, detail: $"attempt {st.Attempts + 1}");
            else
            {
                Write(FlashJournalKind.Started, detail: _plan.Image.Name, sha: _plan.Image.Sha256, tx: _plan.TxId);
                SetPhase(FlashPhase.Backup, "saving backup");
                var backupRef = await Backup!(none);
                Write(FlashJournalKind.Backup, detail: backupRef);
            }
            if (stop.IsCancellationRequested)
            {
                if (!resume) Write(FlashJournalKind.Abandoned, detail: "stopped by operator before the programming session; module unchanged");
                stop.ThrowIfCancellationRequested();
            }

            inBootloader = true; // from the first programming-session request on, the module may be in its bootloader
            for (int round = 0; ; round++)
            {
                try { await ProgramAsync(uds, stop); break; }
                catch (FlashSessionLostException ex) when (round < MaxSessionRecoveries)
                {
                    _retries++; _inSession = false; _unlocked = false;
                    Write(FlashJournalKind.Phase, phase: _phase.ToString(), detail: $"session lost ({ex.Fault?.Reason}); re-entering programming from the erase");
                    Report("programming session lost; re-entering from the erase");
                }
            }

            SetPhase(FlashPhase.Reset, "resetting module");
            _inSession = false; _unlocked = false; // the reset ends the session: no keep-alive during the post-reset wait
            try { await uds.EcuResetAsync(_plan.Reset, none); }
            catch (Exception ex) when (ex is TimeoutException or UdsNegativeResponseException) { Log.Warning("Flash: no clean reply to ECU reset ({Err}); the module may already be rebooting", ex.Message); }
            await Delay(_plan.Retry.PostResetDelay);

            SetPhase(FlashPhase.Verify, "reading back the software part number");
            string? after = null;
            if (ReadModulePart is not null)
                try { after = await ReadModulePart(uds, none); } catch (Exception ex) when (ex is not OperationCanceledException) { Log.Warning("Flash: part number read-back failed: {Err}", ex.Message); }
            bool confirmed = after is not null && _plan.Image.SoftwarePartNumber is string want && Normalize(after).Contains(Normalize(want), StringComparison.Ordinal);
            string msg = confirmed ? $"Module reports {after}." : after is null ? "Flash finished; the part number could not be read back." : $"Flash finished; the module reports {after}, the file says {_plan.Image.SoftwarePartNumber ?? "(none)"}.";
            Write(FlashJournalKind.Completed, detail: msg);
            SetPhase(FlashPhase.Done, msg);
            return new FlashResult(confirmed, after, _retries, sw.Elapsed, resume ? st.Attempts + 1 : 1, msg);
        }
        catch (OperationCanceledException) when (_phase < FlashPhase.ExtendedSession) { throw; } // stopped before anything was sent
        catch (FlashInterruptedException ex) { Write(FlashJournalKind.Interrupted, detail: ex.Message); Report(ex.Message); throw; }
        catch (FlashFailedException ex) { Record(ex, inBootloader); throw; }
        catch (Exception ex)
        {
            var fault = FlashFaults.Classify(ex);
            FlashFailedException wrapped = fault.Action == FlashFaultAction.LinkLost
                ? new FlashInterruptedException($"{_phase}: {fault.Reason}", _phase.ToString(), fault, ex)
                : new FlashFailedException($"{_phase}: {fault.Reason}", _phase.ToString(), inBootloader, fault, ex);
            if (wrapped is FlashInterruptedException) Write(FlashJournalKind.Interrupted, detail: wrapped.Message); else Record(wrapped, inBootloader);
            Report(wrapped.Message);
            throw wrapped;
        }
    }

    /// <summary>Programming session → security → erase → download → check. Run again from the top after a lost session.</summary>
    private async Task ProgramAsync(UdsClient uds, CancellationToken stop)
    {
        SetPhase(FlashPhase.ExtendedSession, "extended session");
        await TryOptionalAsync(uds, new byte[] { 0x10, 0x03 }, "extended session");

        SetPhase(FlashPhase.ProgrammingSession, "programming session");
        await StepAsync(uds, "DiagnosticSessionControl programming", new byte[] { 0x10, 0x02 }, r => r[0] == 0x50);
        _inSession = true;

        SetPhase(FlashPhase.SecurityAccess, $"security access level 0x{_plan.SecurityLevel:X2}");
        await SecurityAsync(uds);
        _unlocked = true;

        if (_plan.Erase)
            foreach (var region in _plan.EraseRegions)
            {
                SetPhase(FlashPhase.Erase, $"erasing 0x{region.Address:X8} (+{region.Length:N0} bytes)");
                await RoutineAsync(uds, "erase", _plan.EraseRoutineId, _plan.BuildEraseOption(region), _plan.Retry.EraseTimeout);
            }

        for (int i = 0; i < _plan.Image.Segments.Count; i++)
        {
            if (i > 0 && stop.IsCancellationRequested)
                throw new FlashInterruptedException($"Stopped by the operator after segment {i} of {_plan.Image.Segments.Count}. The module is in its bootloader; resume to finish.",
                    _phase.ToString(), new FlashFault(FlashFaultAction.LinkLost, "operator stop"));
            _segment = i + 1;
            await DownloadSegmentAsync(uds, i, _plan.Image.Segments[i]);
        }

        if (_plan.CheckDependencies)
        {
            SetPhase(FlashPhase.CheckDependencies, "checking programming dependencies");
            var status = await RoutineAsync(uds, "check", _plan.CheckRoutineId, null, _plan.Retry.EraseTimeout);
            if (_plan.CheckPassed is { } ok && !ok(status))
                throw new FlashFailedException($"The module's check routine reported a problem (status {Convert.ToHexString(status)}).", _phase.ToString(), true,
                    new FlashFault(FlashFaultAction.Fatal, "check routine failed"));
        }
    }

    /// <summary>
    /// Waits <paramref name="t"/>. While the module is in the programming session, sends TesterPresent (3E 80, no reply) at least every
    /// <see cref="KeepAliveInterval"/> so a pause (low battery, security delay, back-off) never lets S3 end the session.
    /// </summary>
    private async Task WaitAsync(TimeSpan t)
    {
        if (!_inSession || _uds is null) { await Delay(t); return; }
        for (var left = t; left > TimeSpan.Zero; left -= KeepAliveInterval)
        {
            await Delay(left < KeepAliveInterval ? left : KeepAliveInterval);
            try { await _uds.TesterPresentAsync(true, CancellationToken.None); }
            catch (Exception ex) when (ex is TimeoutException or UdsNegativeResponseException or IsoTpException or DiagLinkException or InvalidDataException)
            { /* keep-alive only; the next real step reports real problems */ }
        }
    }

    /// <summary>Context-aware fault: NRCs that mean "the session or security is gone" once we had them.</summary>
    private FlashFault Contextual(FlashFault f, byte sid) => f.Nrc switch
    {
        0x7E or 0x7F when _inSession => new(FlashFaultAction.SessionLost, $"NRC 0x{f.Nrc:X2} {UdsNrc.Describe(f.Nrc!.Value)}: the programming session ended", f.Nrc),
        0x33 when _unlocked => new(FlashFaultAction.SessionLost, "NRC 0x33 security access denied after unlocking: security was lost", f.Nrc),
        0x24 when _unlocked && sid == 0x36 => new(FlashFaultAction.SessionLost, "NRC 0x24 on TransferData: the module dropped the download", f.Nrc),
        _ => f,
    };

    private void Record(FlashFailedException ex, bool inBootloader)
    {
        if (inBootloader) Write(FlashJournalKind.Fault, detail: ex.Message);
        else if (State.Open) Write(FlashJournalKind.Abandoned, detail: "failed before the programming session; module unchanged: " + ex.Message);
    }

    private async Task DownloadSegmentAsync(UdsClient uds, int index, FlashSegment seg)
    {
        int n = index + 1, count = _plan.Image.Segments.Count;
        SetPhase(FlashPhase.Download, $"segment {n}/{count}: request download 0x{seg.Address:X8} ({seg.Length:N0} bytes)");
        Write(FlashJournalKind.SegmentStarted, segment: n, detail: $"0x{seg.Address:X8} +{seg.Length}");
        var req = FlashMessages.RequestDownload(_plan.Image.DataFormat, _plan.AddressAndLengthFormat, seg.Address, seg.Length);
        // 0x22/0x70/0x21 right after an erase that could not be polled usually means "not ready yet": retry with a longer pause.
        var resp = await StepAsync(uds, "RequestDownload", req, r => r[0] == 0x74, slowNrcs: new byte[] { 0x21, 0x22, 0x70 });
        int maxMsg = Math.Min(FlashMessages.ParseMaxBlockLength(resp), _plan.MaxBlockLength);
        int chunk = maxMsg - 2;

        byte counter = 1;
        int block = 0;
        long offset = 0;
        while (offset < seg.Length)
        {
            int len = (int)Math.Min(chunk, seg.Length - offset);
            var td = FlashMessages.TransferData(counter, seg.Data.AsSpan((int)offset, len));
            byte expect = counter;
            await StepAsync(uds, $"TransferData block {block + 1} (counter 0x{counter:X2})", td, r => r.Length >= 2 && r[0] == 0x76 && r[1] == expect,
                retries: _plan.Retry.BlockRetries);
            offset += len; block++; _bytesDone += len;
            Write(FlashJournalKind.BlockAcked, segment: n, block: block, counter: counter, offset: offset);
            Report($"segment {n}/{count}: {offset:N0}/{seg.Length:N0} bytes");
            counter = unchecked((byte)(counter + 1)); // 0xFF wraps to 0x00 (ISO 14229-1)
            if (block % VoltageCheckEveryBlocks == 0) await EnsureVoltageAsync(waitIfLow: true);
        }

        SetPhase(FlashPhase.TransferExit, $"segment {n}/{count}: transfer exit");
        await StepAsync(uds, "RequestTransferExit", new byte[] { 0x37 }, r => r[0] == 0x77, settle: async () =>
        {
            // The exit may have been accepted while its answer was lost. A second 37 then gets 0x24 (sequence error): that means done.
            try { return await uds.RequestAsync(new byte[] { 0x37 }, CancellationToken.None); }
            catch (UdsNegativeResponseException e) when (e.Nrc == 0x24) { Log.Information("Flash: second transfer exit answered 0x24; the first one was accepted"); return new byte[] { 0x77 }; }
        });
        Write(FlashJournalKind.SegmentDone, segment: n);
    }

    /// <summary>Starts a long routine (erase / check). If the channel cannot wait out "response pending", polls 31 03 for the result.</summary>
    private async Task<byte[]> RoutineAsync(UdsClient uds, string what, ushort rid, byte[]? option, TimeSpan timeout)
    {
        var start = FlashMessages.Routine(RoutineControlType.Start, rid, option);
        var resp = await StepAsync(uds, $"{what} routine 0x{rid:X4}", start, r => r.Length >= 4 && r[0] == 0x71 && (r[2] << 8 | r[3]) == rid,
            settle: () => PollRoutineAsync(uds, what, rid, timeout));
        return resp.Length > 4 ? resp[4..] : Array.Empty<byte>();
    }

    private async Task<byte[]> PollRoutineAsync(UdsClient uds, string what, ushort rid, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        var results = FlashMessages.Routine(RoutineControlType.RequestResults, rid);
        while (sw.Elapsed < timeout)
        {
            await WaitAsync(_plan.Retry.PendingSettle);
            try
            {
                var r = await uds.RequestAsync(results, CancellationToken.None);
                if (r.Length >= 4 && r[0] == 0x71) { Log.Information("Flash: {What} routine results: {Hex}", what, Convert.ToHexString(r)); return r; }
            }
            catch (UdsNegativeResponseException e) when (e.Nrc is 0x78 or 0x21) { /* still running: poll again */ }
            catch (TimeoutException) { /* a lost poll answer: poll again */ }
            catch (UdsNegativeResponseException e) when (e.Nrc is 0x12 or 0x24 or 0x31 or 0x7E or 0x7F or 0x11)
            {
                // The module can't report results. Wait a fixed time, keeping the session alive, and let the next step find out.
                Log.Warning("Flash: {What} routine results not available (NRC 0x{Nrc:X2}); waiting blind", what, e.Nrc);
                var blind = TimeSpan.FromTicks(Math.Min(timeout.Ticks - sw.Elapsed.Ticks, BlindWait.Ticks));
                await WaitAsync(blind); // keeps the session alive
                return new byte[] { 0x71, 0x01, (byte)(rid >> 8), (byte)rid };
            }
        }
        throw new FlashFailedException($"The {what} routine did not finish within {timeout.TotalSeconds:0} s.", _phase.ToString(), true,
            new FlashFault(FlashFaultAction.Fatal, $"{what} timeout"));
    }

    /// <summary>Longest fixed wait used when a module cannot report a routine's results.</summary>
    public TimeSpan BlindWait { get; init; } = TimeSpan.FromSeconds(20);

    private async Task SecurityAsync(UdsClient uds)
    {
        for (int attempt = 0; ; attempt++)
        {
            bool ok;
            try { ok = await uds.SecurityAccessAsync(_plan.SecurityLevel, CancellationToken.None); }
            catch (UdsNegativeResponseException e) when (e.Nrc == 0x37 && attempt == 0)
            {
                Report($"security delay active; waiting {_plan.Retry.SecurityDelay.TotalSeconds:0} s (session kept alive)");
                await WaitAsync(_plan.Retry.SecurityDelay);
                continue;
            }
            catch (UdsNegativeResponseException e) when (Contextual(FlashFaults.FromNrc(e.Nrc), 0x27).Action == FlashFaultAction.SessionLost)
            {
                throw new FlashSessionLostException($"Security access: NRC 0x{e.Nrc:X2} {UdsNrc.Describe(e.Nrc)}: the programming session ended.", _phase.ToString(),
                    new FlashFault(FlashFaultAction.SessionLost, "session ended during security access", e.Nrc), e);
            }
            catch (Exception ex) when (FlashFaults.Classify(ex).Action == FlashFaultAction.Retry && attempt < _plan.Retry.StepRetries)
            {
                _retries++;
                await WaitAsync(_plan.Retry.Backoff);
                continue;
            }
            if (!ok) throw new FlashFailedException($"No security key for module 0x{_plan.TxId:X3} level 0x{_plan.SecurityLevel:X2}. Install a seed/key provider for this module.",
                FlashPhase.SecurityAccess.ToString(), true, new FlashFault(FlashFaultAction.Security, "no key"));
            return;
        }
    }

    /// <summary>One request with the fault policy applied. <paramref name="settle"/> replaces a plain resend after "response pending".</summary>
    private async Task<byte[]> StepAsync(UdsClient uds, string what, byte[] req, Func<byte[], bool> valid, int? retries = null,
        Func<Task<byte[]>>? settle = null, byte[]? slowNrcs = null)
    {
        int max = retries ?? _plan.Retry.StepRetries;
        for (int attempt = 0; ; attempt++)
        {
            FlashFault fault;
            Exception err;
            try
            {
                var r = await uds.RequestAsync(req, CancellationToken.None);
                if (r.Length > 0 && valid(r)) return r;
                throw new InvalidDataException($"Unexpected reply to {what}: {Convert.ToHexString(r)}");
            }
            catch (Exception ex) when (ex is not FlashFailedException and not OperationCanceledException)
            {
                err = ex;
                fault = ex is UdsNegativeResponseException n && slowNrcs is not null && slowNrcs.Contains(n.Nrc)
                    ? new FlashFault(FlashFaultAction.Retry, $"NRC 0x{n.Nrc:X2} {UdsNrc.Describe(n.Nrc)} (module not ready)", n.Nrc)
                    : FlashFaults.Classify(ex);
                fault = Contextual(fault, req[0]);
            }
            Log.Warning("Flash {What}: {Action} - {Reason} (attempt {Attempt})", what, fault.Action, fault.Reason, attempt + 1);
            switch (fault.Action)
            {
                case FlashFaultAction.Retry when attempt < max:
                    _retries++;
                    Report($"{what}: {fault.Reason}; retrying ({attempt + 1}/{max})");
                    await WaitAsync(TimeSpan.FromTicks(_plan.Retry.Backoff.Ticks * (attempt + 1) * (fault.Nrc is null ? 1 : 4)));
                    continue;
                case FlashFaultAction.WaitPending when attempt < max:
                    _retries++;
                    Report($"{what}: module busy (response pending); waiting");
                    if (settle is not null)
                    {
                        try
                        {
                            var r = await settle();
                            if (r.Length > 0 && valid(r)) return r;
                        }
                        catch (Exception ex) when (ex is not FlashFailedException && FlashFaults.Classify(ex).Action is FlashFaultAction.Retry or FlashFaultAction.WaitPending) { /* settle failed transiently: the loop resends */ }
                        continue;
                    }
                    await WaitAsync(_plan.Retry.PendingSettle);
                    continue; // resend: TransferData keeps its counter, so a repeat is safe
                case FlashFaultAction.WaitVoltage when attempt < max:
                    _retries++;
                    await EnsureVoltageAsync(waitIfLow: true);
                    continue;
                case FlashFaultAction.Retry or FlashFaultAction.WaitPending or FlashFaultAction.WaitVoltage:
                    throw new FlashInterruptedException($"{what}: gave up after {max + 1} attempts ({fault.Reason}). Check the adapter and battery, then resume.",
                        _phase.ToString(), fault, err);
                case FlashFaultAction.LinkLost:
                    throw new FlashInterruptedException($"{what}: {fault.Reason}. Reconnect (OBDLink EX or MX+) and resume.", _phase.ToString(), fault, err);
                case FlashFaultAction.SessionLost:
                    throw new FlashSessionLostException($"{what}: {fault.Reason}. Resume re-enters programming from the erase.", _phase.ToString(), fault, err);
                case FlashFaultAction.Security:
                    throw new FlashFailedException($"{what}: {fault.Reason}. Security must be re-done by the operator.", _phase.ToString(), _phase >= FlashPhase.ProgrammingSession, fault, err);
                default:
                    throw new FlashFailedException($"{what}: {fault.Reason}", _phase.ToString(), _phase >= FlashPhase.ProgrammingSession, fault, err);
            }
        }
    }

    /// <summary>Sends a request whose rejection is acceptable (e.g. a bootloader without an extended session).</summary>
    private async Task TryOptionalAsync(UdsClient uds, byte[] req, string what)
    {
        try { await StepAsync(uds, what, req, r => r[0] == req[0] + 0x40); }
        catch (FlashFailedException ex) when (ex.Fault?.Nrc is not null && ex.Fault.Action == FlashFaultAction.Fatal)
        { Log.Information("Flash: {What} refused ({Reason}); continuing", what, ex.Fault.Reason); }
    }

    private async Task EnsureVoltageAsync(bool waitIfLow)
    {
        var sw = Stopwatch.StartNew();
        while (true)
        {
            var v = await ReadVoltage!(CancellationToken.None);
            if (v is double ok && ok >= _plan.MinVoltage && ok <= _plan.MaxVoltage) return;
            string what = v is double x ? $"{x:0.0} V (allowed {_plan.MinVoltage:0.0}-{_plan.MaxVoltage:0.0} V)" : "unreadable";
            if (!waitIfLow)
                throw new FlashFailedException($"Battery voltage {what}. Connect a charger/maintainer.", _phase.ToString(), false,
                    new FlashFault(FlashFaultAction.WaitVoltage, "voltage out of range"));
            if (sw.Elapsed >= _plan.Retry.VoltageWait)
                throw new FlashInterruptedException($"Battery voltage {what} for {_plan.Retry.VoltageWait.TotalSeconds:0} s. Fix the supply, then resume.",
                    _phase.ToString(), new FlashFault(FlashFaultAction.WaitVoltage, "voltage out of range"));
            Report($"paused: battery {what}" + (_inSession ? " (session kept alive)" : ""));
            await WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    private static string Normalize(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    private void SetPhase(FlashPhase p, string msg)
    {
        _phase = p;
        if (p is not FlashPhase.Preflight and not FlashPhase.Backup and not FlashPhase.Done) Write(FlashJournalKind.Phase, phase: p.ToString(), detail: msg);
        Log.Information("Flash 0x{Tx:X3}: {Phase} - {Msg}", _plan.TxId, p, msg);
        Report(msg);
    }

    private void Report(string msg) => Progress?.Report(new FlashProgress(_phase, _segment, _plan.Image.Segments.Count, _bytesDone, _plan.Image.TotalBytes, _retries, msg));

    private void Write(FlashJournalKind kind, string? phase = null, int? segment = null, int? block = null, byte? counter = null, long? offset = null,
        string? detail = null, string? sha = null, uint? tx = null)
        => _journal.Append(new FlashJournalEntry(DateTimeOffset.UtcNow, kind, phase, segment, block, counter, offset, detail, sha, tx));
}
