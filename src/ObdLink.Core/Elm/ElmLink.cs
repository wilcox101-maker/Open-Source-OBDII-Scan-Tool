using System.Text;
using Serilog;
using ObdLink.Core.Transport;

namespace ObdLink.Core.Elm;

public enum ElmError { None, Unknown, NoData, CanError, BufferFull, Stopped, UnableToConnect, BusInit, BusError, RxError, DataError, LowVoltageReset, ActivityAlert, Error }

/// <summary>Why <see cref="ElmLink.MonitorAsync"/> returned.</summary>
public enum MonitorStop { Cancelled, BufferFull, AdapterStopped }

public sealed record ElmResponse(string Command, IReadOnlyList<string> Lines, ElmError Error, string Raw)
{
    public bool Ok => Error == ElmError.None;
}

/// <summary>The link dropped during a vehicle request. The request was NOT resent; it may or may not have reached the vehicle.</summary>
public sealed class LinkDroppedException(string cmd, Exception inner)
    : IOException($"The adapter link dropped during '{cmd}'. The request was not resent; it may or may not have reached the vehicle. Check the result before retrying.", inner);

public sealed class ElmTimeoutException(string cmd, TimeSpan t) : TimeoutException($"No '>' prompt for '{cmd}' within {t.TotalMilliseconds} ms");

/// <summary>
/// ELM327 / STN command layer: one command in flight at a time, reads until the '>' prompt,
/// strips echo/blank lines, classifies standard ELM327 error strings.
/// </summary>
public sealed class ElmLink : IAsyncDisposable
{
    private readonly ITransport _transport;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _log;
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(3);
    public int MaxReconnectAttempts { get; set; } = 2;
    public event Action<string, string>? Traffic; // (direction ">>"/"<<", text)
    /// <summary>v0.6.1 audit: raised (inside the command gate) when the transport is reopened after it had been open. The adapter may have reset.</summary>
    public event Action? Reopened;
    private bool _everOpened;

    public ElmLink(ITransport transport, ILogger? log = null)
    {
        _transport = transport;
        _log = (log ?? Log.Logger).ForContext<ElmLink>();
    }

    public ITransport Transport => _transport;

    public async Task<ElmResponse> SendAsync(string command, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    await EnsureOpenAsync(ct).ConfigureAwait(false);
                    return await SendCoreAsync(command, timeout ?? DefaultTimeout, ct).ConfigureAwait(false);
                }
                catch (IOException ex) when (attempt < MaxReconnectAttempts)
                {
                    _log.Warning(ex, "Link I/O error on {Cmd}; reconnecting (attempt {N})", command, attempt + 1);
                    await _transport.CloseAsync().ConfigureAwait(false);
                    await Task.Delay(500, ct).ConfigureAwait(false);
                    if (!IsResendSafe(command))
                    {
                        // v0.6.1 audit: a vehicle request may already have reached the bus, and a reset adapter would send a resend
                        // to the default header (7DF, every module). Reopen so the session can re-apply its state, but never resend.
                        try { await EnsureOpenAsync(ct).ConfigureAwait(false); }
                        catch (Exception reopenEx) when (reopenEx is not OperationCanceledException) { _log.Warning(reopenEx, "Reopen after link drop failed"); }
                        throw new LinkDroppedException(command, ex);
                    }
                }
            }
        }
        finally { _gate.Release(); }
    }

    private async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (_transport.IsOpen) { _everOpened = true; return; }
        await _transport.OpenAsync(ct).ConfigureAwait(false);
        if (_everOpened) Reopened?.Invoke();
        _everOpened = true;
    }

    /// <summary>
    /// Only adapter-local AT/ST commands are resent after a reconnect. Anything else is a vehicle request (OBD/UDS hex),
    /// and so are the pass-through/wakeup commands that put a frame on the bus.
    /// </summary>
    public static bool IsResendSafe(string command)
    {
        var c = command.Replace(" ", "").ToUpperInvariant();
        if (!(c.StartsWith("AT", StringComparison.Ordinal) || c.StartsWith("ST", StringComparison.Ordinal))) return false;
        return !(c.StartsWith("STPX", StringComparison.Ordinal) || c.StartsWith("ATWM", StringComparison.Ordinal));
    }

    /// <summary>
    /// STN data-entry command (e.g. <c>STPX l:n</c>): sends <paramref name="command"/>, and only if the adapter answers with its
    /// <c>DATA&gt;</c> prompt sends <paramref name="data"/> as the next line. Both happen inside one hold of the command gate, so no
    /// other command can land in between. Returns the prompt reply when the adapter refused the command (<c>?</c>, <c>OUT OF
    /// MEMORY</c>): nothing reached the bus then. Never resent after a link drop (the data line is a vehicle request).
    /// </summary>
    public async Task<ElmResponse> SendWithDataAsync(string command, string data, TimeSpan timeout, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureOpenAsync(ct).ConfigureAwait(false);
            var prompt = await SendCoreAsync(command, TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            if (!IsDataPrompt(prompt)) return prompt;
            string shown = data.Length > 48 ? $"{data[..48]}… ({data.Length / 2} bytes)" : data;
            return await SendCoreAsync(data, timeout, ct, shown).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>True when a reply is the STN <c>DATA&gt;</c> data-entry prompt.</summary>
    public static bool IsDataPrompt(ElmResponse r) => r.Lines.Count > 0 && string.Equals(r.Lines[^1], "DATA", StringComparison.OrdinalIgnoreCase);

    private async Task<ElmResponse> SendCoreAsync(string command, TimeSpan timeout, CancellationToken ct, string? shown = null)
    {
        _transport.DiscardInput();
        Traffic?.Invoke(">>", shown ?? command);
        _log.Debug(">> {Cmd}", command);
        await _transport.WriteAsync(command + "\r", ct).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        var sb = new StringBuilder();
        try
        {
            while (true)
            {
                var chunk = await _transport.ReadAsync(cts.Token).ConfigureAwait(false);
                sb.Append(chunk);
                if (chunk.Contains('>')) break;
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Recovery: a stray CR aborts most in-progress ELM operations; we then resync on next command.
            _log.Warning("Timeout waiting for prompt on {Cmd}; partial={Partial}", command, sb.ToString());
            try { await _transport.WriteAsync("\r", CancellationToken.None).ConfigureAwait(false); } catch { /* best-effort abort of a timed-out command; the link may already be gone */ }
            await DrainToPromptAsync(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            Traffic?.Invoke("!!", $"TIMEOUT {command}");
            throw new ElmTimeoutException(command, timeout);
        }
        var raw = sb.ToString();
        Traffic?.Invoke("<<", raw.TrimEnd('>', '\r', '\n'));
        _log.Debug("<< {Raw}", raw.Replace("\r", "\\r"));
        return Parse(command, raw);
    }

    /// <summary>
    /// v0.13.0: runs a monitor command (ATMA / STMA) that keeps printing lines until any character is sent. Each received line
    /// (not the echo, not the prompt) goes to <paramref name="onLine"/>. Cancelling <paramref name="ct"/> sends a CR to stop the
    /// adapter and waits for its prompt, so the link is back in command mode on return. Holds the command gate for the whole
    /// time: other commands wait. Individual lines are not written to <see cref="Traffic"/> (that would flood the log); the
    /// start and the stop are.
    /// </summary>
    public async Task<MonitorStop> MonitorAsync(string command, Action<string> onLine, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await EnsureOpenAsync(ct).ConfigureAwait(false);
            _transport.DiscardInput();
            Traffic?.Invoke(">>", command);
            await _transport.WriteAsync(command + "\r", ct).ConfigureAwait(false);
            var line = new StringBuilder();
            bool bufferFull = false, first = true;
            // Stop = one CR, sent from the cancellation callback so the adapter ends a pending read by printing its prompt
            // (a serial read that ignores cancellation still returns). Exactly once: a CR at the prompt would repeat ATMA.
            int stopSent = 0;
            void SendStop() { if (Interlocked.Exchange(ref stopSent, 1) == 0) _ = WriteQuietAsync("\r"); }
            using var reg = ct.Register(SendStop);
            void Flush()
            {
                var l = line.ToString().Trim(); line.Clear();
                if (l.Length == 0) return;
                if (first && string.Equals(l.Replace(" ", ""), command.Replace(" ", ""), StringComparison.OrdinalIgnoreCase)) { first = false; return; }
                first = false;
                if (l.StartsWith("BUFFER FULL", StringComparison.OrdinalIgnoreCase)) { bufferFull = true; return; }
                onLine(l);
            }
            try
            {
                while (true)
                {
                    var chunk = await _transport.ReadAsync(ct).ConfigureAwait(false);
                    foreach (var c in chunk)
                    {
                        if (c == '>')
                        {
                            Flush();
                            if (Volatile.Read(ref stopSent) == 1) { Traffic?.Invoke("<<", "(monitor stopped)"); return MonitorStop.Cancelled; }
                            Interlocked.Exchange(ref stopSent, 1); // the adapter stopped on its own: never send a stray CR now
                            Traffic?.Invoke("<<", bufferFull ? "(monitor stopped: BUFFER FULL)" : "(monitor stopped by the adapter)");
                            return bufferFull ? MonitorStop.BufferFull : MonitorStop.AdapterStopped;
                        }
                        if (c is '\r' or '\n') Flush(); else line.Append(c);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                SendStop(); // no-op: the callback already sent it
                await DrainToPromptAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
                Traffic?.Invoke("<<", "(monitor stopped)");
                return MonitorStop.Cancelled;
            }
        }
        finally { _gate.Release(); }
    }

    private async Task WriteQuietAsync(string text)
    {
        try { await _transport.WriteAsync(text, CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { _log.Debug("Monitor stop write failed: {Err}", ex.Message); }
    }

    /// <summary>
    /// v0.6.1 audit: after an abort the adapter still prints its '&gt;' prompt. If that late prompt reached the next command,
    /// the next command would return early with this command's leftovers. Wait briefly for it and throw it away.
    /// </summary>
    private async Task DrainToPromptAsync(TimeSpan max)
    {
        using var cts = new CancellationTokenSource(max);
        try { while (!(await _transport.ReadAsync(cts.Token).ConfigureAwait(false)).Contains('>')) { } }
        catch (OperationCanceledException) { /* no prompt in time; the next command still discards input first */ }
        catch (IOException) { /* link gone; the next command reconnects */ }
        catch (InvalidOperationException) { /* port closed */ }
    }

    public static ElmResponse Parse(string command, string raw)
    {
        int i = raw.LastIndexOf('>');
        var body = i >= 0 ? raw[..i] : raw;
        var lines = body.Split('\r', '\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();
        if (lines.Count > 0 && string.Equals(lines[0].Replace(" ", ""), command.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
            lines.RemoveAt(0); // echo (ATE1 / before ATE0 takes effect)
        lines.RemoveAll(l => l.StartsWith("SEARCHING", StringComparison.OrdinalIgnoreCase));
        return new ElmResponse(command, lines, Classify(lines), raw);
    }

    public static ElmError Classify(IReadOnlyList<string> lines)
    {
        foreach (var l in lines)
        {
            var u = l.ToUpperInvariant();
            if (u == "?") return ElmError.Unknown;
            if (u.StartsWith("NO DATA")) return ElmError.NoData;
            if (u.StartsWith("CAN ERROR")) return ElmError.CanError;
            if (u.StartsWith("BUFFER FULL")) return ElmError.BufferFull;
            if (u.StartsWith("STOPPED")) return ElmError.Stopped;
            if (u.StartsWith("UNABLE TO CONNECT")) return ElmError.UnableToConnect;
            if (u.StartsWith("BUS INIT")&& u.Contains("ERROR")) return ElmError.BusInit;
            if (u.StartsWith("BUS ERROR")) return ElmError.BusError;
            if (u.StartsWith("<RX ERROR")) return ElmError.RxError;
            if (u.StartsWith("DATA ERROR") || u.EndsWith("<DATA ERROR")) return ElmError.DataError;
            if (u.StartsWith("LV RESET")) return ElmError.LowVoltageReset;
            if (u.StartsWith("ACT ALERT")) return ElmError.ActivityAlert;
            if (u.StartsWith("ERR") || u == "ERROR") return ElmError.Error;
        }
        return ElmError.None;
    }

    public ValueTask DisposeAsync() => _transport.DisposeAsync();
}
