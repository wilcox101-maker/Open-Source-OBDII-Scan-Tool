using System.Text;
using System.Threading.Channels;

namespace ObdLink.Core.Transport;

/// <summary>
/// Simulated adapter. Each command (text up to CR) is passed to a responder that returns the
/// adapter reply text (without the prompt); the mock appends "\r\r>" like an ELM327/STN.
/// Returning null simulates a hang (no reply) for timeout tests.
/// </summary>
public sealed class MockTransport : ITransport
{
    private readonly Func<string, string?> _responder;
    private readonly Channel<string> _rx = Channel.CreateUnbounded<string>();
    private readonly StringBuilder _pending = new();
    public List<string> Sent { get; } = new();
    public bool Echo { get; set; }
    public int OpenCount { get; private set; }

    public MockTransport(Func<string, string?> responder) => _responder = responder;

    public string Name => "MOCK";
    public bool IsOpen { get; private set; }

    public Task OpenAsync(CancellationToken ct = default) { IsOpen = true; OpenCount++; return Task.CompletedTask; }
    public Task CloseAsync() { IsOpen = false; return Task.CompletedTask; }

    public Task WriteAsync(string text, CancellationToken ct = default)
    {
        if (!IsOpen) throw new IOException("mock closed");
        _pending.Append(text);
        int idx;
        while ((idx = _pending.ToString().IndexOf('\r')) >= 0)
        {
            var cmd = _pending.ToString(0, idx).Trim();
            _pending.Remove(0, idx + 1);
            Sent.Add(cmd);
            var reply = _responder(cmd);
            if (reply is null) continue;
            var sb = new StringBuilder();
            if (Echo) sb.Append(cmd).Append('\r');
            sb.Append(reply.Replace("\n", "\r")).Append("\r\r>");
            _rx.Writer.TryWrite(sb.ToString());
        }
        return Task.CompletedTask;
    }

    public async Task<string> ReadAsync(CancellationToken ct = default) => await _rx.Reader.ReadAsync(ct);

    public void DiscardInput() { while (_rx.Reader.TryRead(out _)) { } }

    public ValueTask DisposeAsync() { IsOpen = false; return ValueTask.CompletedTask; }
}
