namespace ObdLink.Core.Transport;

/// <summary>Byte-stream link to the adapter (Bluetooth SPP COM port, mock, replay...).</summary>
public interface ITransport : IAsyncDisposable
{
    string Name { get; }
    bool IsOpen { get; }
    Task OpenAsync(CancellationToken ct = default);
    Task CloseAsync();
    Task WriteAsync(string text, CancellationToken ct = default);
    /// <summary>Reads whatever is available (may block until data or cancellation).</summary>
    Task<string> ReadAsync(CancellationToken ct = default);
    void DiscardInput();
}
