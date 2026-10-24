namespace ObdLink.Core.Diagnostics;

public readonly record struct LedState(bool Tx, bool Rx, bool Error);

/// <summary>
/// Thread-safe TX/RX/error activity tracker for the header LEDs. <see cref="OnTraffic"/> is called from the link thread
/// (cheap: only stores a timestamp); the UI polls <see cref="Sample"/> on a timer (~30 ms), so bursts never flood the dispatcher.
/// Each LED stays lit for <see cref="Hold"/> after the last event so single commands are visible.
/// </summary>
public sealed class ActivityLeds
{
    private long _tx = long.MinValue / 2, _rx = long.MinValue / 2, _err = long.MinValue / 2;
    private readonly Func<long> _nowMs;
    public TimeSpan Hold { get; set; } = TimeSpan.FromMilliseconds(120);
    public TimeSpan ErrorHold { get; set; } = TimeSpan.FromMilliseconds(600);
    public ActivityLeds(Func<long>? nowMs = null) => _nowMs = nowMs ?? (() => Environment.TickCount64);

    /// <summary>Direction as raised by ElmLink.Traffic: "&gt;&gt;" sent, "&lt;&lt;" received, "!!" timeout.</summary>
    public void OnTraffic(string dir, string text)
    {
        var now = _nowMs();
        if (dir == ">>") Interlocked.Exchange(ref _tx, now);
        else if (dir == "<<") { Interlocked.Exchange(ref _rx, now); if (IsError(text)) Interlocked.Exchange(ref _err, now); }
        else Interlocked.Exchange(ref _err, now);
    }

    public static bool IsError(string reply)
    {
        var u = reply.ToUpperInvariant();
        return u.Contains("NO DATA") || u.Contains("ERROR") || u.Contains("UNABLE TO CONNECT") || u.Contains("STOPPED") || u.Trim() == "?"
            || u.Split('\r', '\n').Select(l => l.Replace(" ", "")).Any(l => l.StartsWith("7F") || (l.Length >= 7 && l.Substring(5, 2) == "7F"));
    }

    public LedState Sample()
    {
        var now = _nowMs();
        return new(now - Interlocked.Read(ref _tx) < Hold.TotalMilliseconds,
                   now - Interlocked.Read(ref _rx) < Hold.TotalMilliseconds,
                   now - Interlocked.Read(ref _err) < ErrorHold.TotalMilliseconds);
    }
}
