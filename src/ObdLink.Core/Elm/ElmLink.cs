using System.Text;
using Serilog;
using ObdLink.Core.Transport;

namespace ObdLink.Core.Elm;

public enum ElmError { None, Unknown, NoData, CanError, BufferFull, Stopped, UnableToConnect, BusInit, BusError, RxError, DataError, LowVoltageReset, ActivityAlert, Error }

public sealed record ElmResponse(string Command, IReadOnlyList<string> Lines, ElmError Error, string Raw)
{
    public bool Ok => Error == ElmError.None;
}

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
                    if (!_transport.IsOpen) await _transport.OpenAsync(ct).ConfigureAwait(false);
                    return await SendCoreAsync(command, timeout ?? DefaultTimeout, ct).ConfigureAwait(false);
                }
                catch (IOException ex) when (attempt < MaxReconnectAttempts)
                {
                    _log.Warning(ex, "Link I/O error on {Cmd}; reconnecting (attempt {N})", command, attempt + 1);
                    await _transport.CloseAsync().ConfigureAwait(false);
                    await Task.Delay(500, ct).ConfigureAwait(false);
                }
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<ElmResponse> SendCoreAsync(string command, TimeSpan timeout, CancellationToken ct)
    {
        _transport.DiscardInput();
        Traffic?.Invoke(">>", command);
        _log.Debug(">> {Cmd}", command);
        await _transport.WriteAsync(command + "\r", ct).ConfigureAwait(false);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        var sb = new StringBuilder();
        try
        {
            while (!sb.ToString().Contains('>'))
                sb.Append(await _transport.ReadAsync(cts.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Recovery: a stray CR aborts most in-progress ELM operations; we then resync on next command.
            _log.Warning("Timeout waiting for prompt on {Cmd}; partial={Partial}", command, sb.ToString());
            try { await _transport.WriteAsync("\r", CancellationToken.None).ConfigureAwait(false); } catch { /* best-effort abort of a timed-out command; the link may already be gone */ }
            Traffic?.Invoke("!!", $"TIMEOUT {command}");
            throw new ElmTimeoutException(command, timeout);
        }
        var raw = sb.ToString();
        Traffic?.Invoke("<<", raw.TrimEnd('>', '\r', '\n'));
        _log.Debug("<< {Raw}", raw.Replace("\r", "\\r"));
        return Parse(command, raw);
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
