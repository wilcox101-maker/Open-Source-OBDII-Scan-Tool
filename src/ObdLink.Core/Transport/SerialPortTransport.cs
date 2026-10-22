using System.IO.Ports;
using System.Text;

namespace ObdLink.Core.Transport;

/// <summary>
/// Transport over a (virtual) serial port. On Windows, pairing the OBDLink MX+ creates an
/// outgoing Bluetooth SPP COM port; the baud rate is nominal for SPP virtual ports.
/// </summary>
public sealed class SerialPortTransport : ITransport
{
    private readonly string _portName;
    private readonly int _baud;
    private SerialPort? _port;

    public SerialPortTransport(string portName, int baud = 115200) { _portName = portName; _baud = baud; }

    public string Name => _portName;
    public bool IsOpen => _port?.IsOpen == true;

    public static string[] ListPorts() => SerialPort.GetPortNames();

    public Task OpenAsync(CancellationToken ct = default)
    {
        _port = new SerialPort(_portName, _baud, Parity.None, 8, StopBits.One)
        {
            Encoding = Encoding.ASCII,
            ReadTimeout = SerialPort.InfiniteTimeout,
            WriteTimeout = 2000,
            NewLine = "\r",
            DtrEnable = true,
            RtsEnable = true,
        };
        _port.Open();
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        try { _port?.Close(); } catch { /* port may already be gone (BT dropout) */ }
        _port?.Dispose();
        _port = null;
        return Task.CompletedTask;
    }

    public async Task WriteAsync(string text, CancellationToken ct = default)
    {
        var p = _port ?? throw new InvalidOperationException("Port not open");
        var bytes = Encoding.ASCII.GetBytes(text);
        await p.BaseStream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await p.BaseStream.FlushAsync(ct).ConfigureAwait(false);
    }

    public async Task<string> ReadAsync(CancellationToken ct = default)
    {
        var p = _port ?? throw new InvalidOperationException("Port not open");
        var buf = new byte[512];
        int n = await p.BaseStream.ReadAsync(buf, ct).ConfigureAwait(false);
        if (n == 0) throw new IOException("Serial stream closed");
        return Encoding.ASCII.GetString(buf, 0, n);
    }

    public void DiscardInput() { try { _port?.DiscardInBuffer(); } catch { } }

    public async ValueTask DisposeAsync() => await CloseAsync();
}
