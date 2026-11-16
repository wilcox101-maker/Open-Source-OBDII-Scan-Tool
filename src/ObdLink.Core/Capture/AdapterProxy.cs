using System.Globalization;
using System.Text;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Capture;

/// <summary>
/// Pass-through between another diagnostic program (connected over TCP, like a Wi-Fi ELM adapter) and the real adapter, logging
/// every command and every line the adapter prints. Used to record what a working tool sends for a service procedure on the
/// user's own truck, so its identifiers can be added to the catalog with a source ("captured on the truck").
/// <list type="bullet">
/// <item>Text is relayed unchanged in both directions, except: a request for a pyrotechnic deployment routine (31 xx E200-E2FF)
/// is never forwarded; the client gets "?" (the ELM "unknown command" answer) and the log says BLOCKED.</item>
/// <item>The log holds raw replies (including the VIN): keep it private. Share the output of <see cref="CaptureSummary"/>, which
/// masks VINs and leaves out security keys.</item>
/// </list>
/// Log format, one line each: <c>hh:mm:ss.fff &gt; COMMAND</c>, <c>hh:mm:ss.fff &lt; ADAPTER LINE</c>, <c>hh:mm:ss.fff ! NOTE</c>.
/// </summary>
public sealed class AdapterProxy
{
    private readonly ITransport _adapter;
    private readonly TextWriter _log;
    private readonly Func<DateTimeOffset> _now;
    private readonly object _gate = new();

    public int Forwarded { get; private set; }
    public int Blocked { get; private set; }

    public AdapterProxy(ITransport adapter, TextWriter log, Func<DateTimeOffset>? now = null)
    { _adapter = adapter; _log = log; _now = now ?? (() => DateTimeOffset.Now); }

    /// <summary>Relays until the client disconnects or <paramref name="ct"/> is cancelled. The adapter must be open.</summary>
    public async Task RunAsync(Stream client, CancellationToken ct = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Note("client connected");
        var fromAdapter = PumpAdapterAsync(client, stop.Token);
        try { await PumpClientAsync(client, stop.Token); }
        finally
        {
            stop.Cancel();
            try { await fromAdapter; } catch (OperationCanceledException) { /* stopped with the client */ } catch (IOException) { /* client gone */ }
            Note($"client disconnected ({Forwarded} commands forwarded, {Blocked} blocked)");
        }
    }

    private async Task PumpClientAsync(Stream client, CancellationToken ct)
    {
        var buf = new byte[1024];
        var line = new StringBuilder();
        while (!ct.IsCancellationRequested)
        {
            int n = await client.ReadAsync(buf, ct);
            if (n == 0) return;
            foreach (char ch in Encoding.ASCII.GetString(buf, 0, n))
            {
                if (ch is not ('\r' or '\n')) { line.Append(ch); continue; }
                var cmd = line.ToString().Trim();
                line.Clear();
                if (cmd.Length == 0) continue;
                if (IsBlocked(cmd))
                {
                    Blocked++;
                    Write('!', $"BLOCKED {cmd} (pyrotechnic deployment routine; never sent)");
                    await client.WriteAsync(Encoding.ASCII.GetBytes("?\r\r>"), ct);
                    continue;
                }
                Write('>', cmd);
                Forwarded++;
                await _adapter.WriteAsync(cmd + "\r", ct);
            }
        }
    }

    private async Task PumpAdapterAsync(Stream client, CancellationToken ct)
    {
        var line = new StringBuilder();
        while (!ct.IsCancellationRequested)
        {
            var text = await _adapter.ReadAsync(ct);
            if (text.Length == 0) continue;
            await client.WriteAsync(Encoding.ASCII.GetBytes(text), ct);
            await client.FlushAsync(ct);
            foreach (char ch in text)
            {
                if (ch is '\r' or '\n' or '>')
                {
                    var l = line.ToString().Trim();
                    line.Clear();
                    if (l.Length > 0) Write('<', l);
                    continue;
                }
                line.Append(ch);
            }
        }
    }

    /// <summary>True for a request payload that starts a pyrotechnic routine, in any of the forms an ELM/STN client sends it.</summary>
    public static bool IsBlocked(string command)
    {
        foreach (var payload in CaptureParser.PayloadCandidates(command))
            if (UdsRoutines.IsPyrotechnicRequest(payload)) return true;
        return false;
    }

    private void Note(string text) => Write('!', text);

    private void Write(char dir, string text)
    {
        lock (_gate)
        {
            _log.WriteLine($"{_now().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)} {dir} {text}");
            _log.Flush();
        }
    }
}
