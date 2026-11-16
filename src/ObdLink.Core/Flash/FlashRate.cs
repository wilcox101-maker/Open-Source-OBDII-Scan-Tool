namespace ObdLink.Core.Flash;

/// <summary>
/// Download throughput and time left from progress samples. The rate is measured from the first acknowledged byte, so the
/// erase and session set-up do not count. When the byte count goes down (a session re-entry or resume restarts from the
/// erase), measuring starts again.
/// </summary>
public sealed class FlashRate
{
    private DateTimeOffset? _t0;
    private long _b0, _last;

    /// <summary>Bytes per second, or null until there is a measurable interval.</summary>
    public double? BytesPerSecond { get; private set; }
    /// <summary>Estimated time left for the remaining bytes, or null while unknown.</summary>
    public TimeSpan? Remaining { get; private set; }

    public void Update(DateTimeOffset now, long bytesDone, long bytesTotal)
    {
        if (bytesDone < _last || _t0 is null)
        {
            _last = bytesDone;
            if (bytesDone > 0) { _t0 = now; _b0 = bytesDone; }
            else _t0 = null;
            BytesPerSecond = null; Remaining = null;
            return;
        }
        _last = bytesDone;
        var dt = (now - _t0.Value).TotalSeconds;
        if (dt < 1 || bytesDone <= _b0) return;
        BytesPerSecond = (bytesDone - _b0) / dt;
        Remaining = TimeSpan.FromSeconds(Math.Max(0, bytesTotal - bytesDone) / BytesPerSecond.Value);
    }

    public string Describe() => BytesPerSecond is double r
        ? $"{Format(r)}" + (Remaining is TimeSpan t ? $", about {Duration(t)} left" : "")
        : "measuring speed";

    public static string Format(double bytesPerSecond) => bytesPerSecond >= 1024 ? $"{bytesPerSecond / 1024:0.0} KB/s" : $"{bytesPerSecond:0} B/s";

    public static string Duration(TimeSpan t) => t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes:00} min" : t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} min {t.Seconds:00} s" : $"{t.Seconds} s";
}
