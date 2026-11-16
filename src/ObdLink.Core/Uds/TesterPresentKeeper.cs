namespace ObdLink.Core.Uds;

/// <summary>
/// Keeps a non-default diagnostic session open. A module drops back to the default session when it hears no request for
/// S3server (5000 ms, ISO 14229-2), which would cancel a long routine or an unlocked state halfway through.
/// The keeper sends a suppressed TesterPresent (3E 80) only when the module has been idle for <see cref="Interval"/>;
/// any other request to the module counts as activity.
/// <para>
/// Call <see cref="TickAsync"/> from whatever owns the adapter (the same exclusive gate as every other command): a UdsClient
/// request sets the CAN header and then transmits, so it must not interleave with requests to another module.
/// </para>
/// </summary>
public sealed class TesterPresentKeeper
{
    /// <summary>ISO 14229-2 S3server. The keep-alive interval has to stay below it.</summary>
    public static readonly TimeSpan S3Server = TimeSpan.FromMilliseconds(5000);
    /// <summary>Default idle time before a keep-alive 3E 80 (well inside S3).</summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(2);

    private readonly UdsClient _uds;
    public TimeSpan Interval { get; }

    public TesterPresentKeeper(UdsClient uds, TimeSpan? interval = null)
    {
        _uds = uds;
        Interval = interval ?? DefaultInterval;
        if (Interval <= TimeSpan.Zero || Interval >= S3Server)
            throw new ArgumentOutOfRangeException(nameof(interval), Interval, $"Keep-alive interval must be above zero and below S3 ({S3Server.TotalMilliseconds} ms)");
    }

    /// <summary>True when the module is in a non-default session and has heard nothing for at least <see cref="Interval"/>.</summary>
    public bool IsDue =>
        _uds.ActiveSession is UdsSession s && s != UdsSession.Default
        && _uds.LastRequestAt is DateTimeOffset last
        && _uds.Clock.GetUtcNow() - last >= Interval;

    /// <summary>Sends 3E 80 if due. Returns true when it sent one.</summary>
    public async Task<bool> TickAsync(CancellationToken ct = default)
    {
        if (!IsDue) return false;
        await _uds.TesterPresentAsync(suppressResponse: true, ct);
        return true;
    }
}
