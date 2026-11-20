using System.IO.Ports;
using System.Text;

namespace ObdLink.Core.Transport;

/// <summary>
/// Transport over a (virtual) serial port. On Windows, pairing the OBDLink MX+ creates an
/// outgoing Bluetooth SPP COM port; the baud rate is nominal for SPP virtual ports.
/// v0.23.17: reads go through <see cref="SerialReadPump"/> (blocking reads with a short timeout on a background thread), and
/// writes are synchronous with <see cref="SerialPort.WriteTimeout"/>, because the port's async stream methods ignore cancellation
/// and timeouts on Windows. A silent or half-dropped adapter now ends in a timeout instead of a hang.
/// </summary>
public sealed class SerialPortTransport : ITransport
{
    private readonly string _portName;
    private readonly int _baud;
    private SerialPort? _port;
    private SerialReadPump? _pump;
    /// <summary>How long one blocking read waits for data before the pump checks whether it should stop.</summary>
    public const int PumpReadTimeoutMs = 50;

    public SerialPortTransport(string portName, int baud = 115200) { _portName = portName; _baud = baud; }

    public string Name => _portName;
    public bool IsOpen => _port?.IsOpen == true;

    public static string[] ListPorts() => SerialPort.GetPortNames();

    public Task OpenAsync(CancellationToken ct = default)
    {
        // v0.3.4 audit: re-open must not leak the previous handle, and a failed Open must not leave a half-built port behind
        CloseCore();
        var port = new SerialPort(_portName, _baud, Parity.None, 8, StopBits.One)
        {
            Encoding = Encoding.ASCII,
            ReadTimeout = PumpReadTimeoutMs,
            WriteTimeout = 2000,
            NewLine = "\r",
            DtrEnable = true,
            RtsEnable = true,
        };
        try { port.Open(); }
        catch { port.Dispose(); throw; }
        _port = port;
        _pump = new SerialReadPump(buf =>
        {
            try { return port.Read(buf, 0, buf.Length); }
            catch (TimeoutException) { return 0; } // no data within PumpReadTimeoutMs
        }, "serial-read " + _portName);
        return Task.CompletedTask;
    }

    public Task CloseAsync() { CloseCore(); return Task.CompletedTask; }

    private void CloseCore()
    {
        var pump = _pump; _pump = null;
        pump?.Stop(); // before Close: the read the port's close interrupts is then not reported as a failure
        try { _port?.Close(); } catch { /* port may already be gone (BT dropout) */ }
        _port?.Dispose();
        _port = null;
        pump?.Join(TimeSpan.FromSeconds(1));
    }

    public async Task WriteAsync(string text, CancellationToken ct = default)
    {
        var p = _port ?? throw new InvalidOperationException("Port not open");
        var bytes = Encoding.ASCII.GetBytes(text);
        ct.ThrowIfCancellationRequested();
        // Synchronous write: honours WriteTimeout (the async stream write does not on Windows). Commands are a few bytes.
        try { await Task.Run(() => p.Write(bytes, 0, bytes.Length), CancellationToken.None).ConfigureAwait(false); }
        catch (TimeoutException ex) { throw new IOException($"Write to {_portName} timed out after {p.WriteTimeout} ms", ex); }
    }

    public async Task<string> ReadAsync(CancellationToken ct = default)
    {
        var pump = _pump ?? throw new InvalidOperationException("Port not open");
        return await pump.ReadAsync(ct).ConfigureAwait(false);
    }

    public void DiscardInput()
    {
        try { _port?.DiscardInBuffer(); } catch { /* port closing or gone; nothing to discard */ }
        _pump?.Discard();
    }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
