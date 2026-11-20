using System.Text;
using System.Threading.Channels;

namespace ObdLink.Core.Transport;

/// <summary>
/// v0.23.17: turns a blocking serial read with a short timeout into an async read that really honours cancellation.
/// <para>
/// Why: on Windows, <c>SerialPort.BaseStream.ReadAsync</c> ignores both its CancellationToken and <c>ReadTimeout</c>
/// (dotnet/corefx#36040, dotnet/runtime#28968). If the adapter went silent (Bluetooth half-dropped, adapter hung) the ELM
/// command timeout could never fire, so <see cref="Elm.ElmLink"/> kept its command gate and every later request waited forever.
/// </para>
/// <para>
/// How: one background thread calls the blocking read in a loop. The read returns 0 when its short timeout passes with no data,
/// so the thread checks for stop at least every timeout. Received text and a read failure are queued in order;
/// <see cref="ReadAsync"/> takes the next item and can be cancelled at any time without losing bytes (they stay queued).
/// </para>
/// </summary>
public sealed class SerialReadPump : IDisposable
{
    private readonly Func<byte[], int> _read;
    private readonly Channel<object> _queue = Channel.CreateUnbounded<object>(new UnboundedChannelOptions { SingleWriter = true });
    private readonly Thread _thread;
    private volatile bool _stop;

    /// <param name="blockingRead">Reads into the buffer and returns the byte count; returns 0 when its timeout passed with no data;
    /// throws when the port failed or closed.</param>
    /// <param name="name">Thread name for diagnostics.</param>
    public SerialReadPump(Func<byte[], int> blockingRead, string name = "serial-read")
    {
        _read = blockingRead;
        _thread = new Thread(Run) { IsBackground = true, Name = name };
        _thread.Start();
    }

    private void Run()
    {
        var buf = new byte[512];
        try
        {
            while (!_stop)
            {
                int n;
                try { n = _read(buf); }
                catch (Exception ex)
                {
                    if (!_stop) _queue.Writer.TryWrite(ex); // closing on purpose is not a failure
                    return;
                }
                if (n > 0 && !_stop) _queue.Writer.TryWrite(Encoding.ASCII.GetString(buf, 0, n));
            }
        }
        finally { _queue.Writer.TryComplete(); }
    }

    /// <summary>Next received text. Throws <see cref="IOException"/> when the read failed or the pump stopped; honours <paramref name="ct"/>.</summary>
    public async Task<string> ReadAsync(CancellationToken ct = default)
    {
        while (await _queue.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            if (!_queue.Reader.TryRead(out var item)) continue;
            if (item is Exception ex) throw ex as IOException ?? new IOException("Serial read failed: " + ex.Message, ex);
            return (string)item;
        }
        throw new IOException("Serial stream closed");
    }

    /// <summary>Drops received text not yet read. A queued read failure is kept so the next read still reports it.</summary>
    public void Discard()
    {
        var keep = new List<Exception>();
        while (_queue.Reader.TryRead(out var item)) if (item is Exception ex) keep.Add(ex);
        foreach (var ex in keep) _queue.Writer.TryWrite(ex); // completed writer: TryWrite is a no-op and the reader reports "closed"
    }

    /// <summary>True while the background thread is running.</summary>
    public bool IsRunning => _thread.IsAlive;

    /// <summary>Asks the thread to stop. The thread ends within one read timeout (or when the port's close makes the read throw).</summary>
    public void Stop() => _stop = true;

    /// <summary>Stops and waits up to <paramref name="wait"/> for the thread to end.</summary>
    public bool Join(TimeSpan wait) { Stop(); return _thread.Join(wait); }

    public void Dispose() { Stop(); _thread.Join(TimeSpan.FromSeconds(1)); }
}
