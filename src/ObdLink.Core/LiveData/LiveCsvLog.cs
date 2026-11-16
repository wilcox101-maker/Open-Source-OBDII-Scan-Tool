using System.Globalization;

namespace ObdLink.Core.LiveData;

/// <summary>One recorded poll cycle.</summary>
public sealed record RecordedCycle(DateTime T, IReadOnlyDictionary<string, double> Values);

/// <summary>
/// v0.12.0: a CSV written by <see cref="Dashboard.DashboardCsvRecorder"/> (header "time,key1,key2,...", time as
/// yyyy-MM-ddTHH:mm:ss.fff, invariant numbers, empty = no value that cycle), read back for replay and charts.
/// Lines that do not parse, or whose time goes backwards, are skipped and counted.
/// </summary>
public sealed class LiveCsvLog
{
    public IReadOnlyList<string> Keys { get; }
    public IReadOnlyList<RecordedCycle> Cycles { get; }
    public int SkippedLines { get; }

    private LiveCsvLog(IReadOnlyList<string> keys, IReadOnlyList<RecordedCycle> cycles, int skipped) { Keys = keys; Cycles = cycles; SkippedLines = skipped; }

    public TimeSpan Duration => Cycles.Count < 2 ? TimeSpan.Zero : Cycles[^1].T - Cycles[0].T;

    public static LiveCsvLog Load(string path) { using var r = new StreamReader(path); return Parse(r); }

    public static LiveCsvLog Parse(TextReader r)
    {
        var header = r.ReadLine() ?? throw new InvalidDataException("The file is empty.");
        var cols = header.Trim().TrimStart('﻿').Split(',').Select(c => c.Trim()).ToArray();
        if (cols.Length < 2 || !cols[0].Equals("time", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Not a live-data / dashboard recording: the first column must be 'time'.");
        var keys = cols.Skip(1).ToArray();
        if (keys.Any(k => k.Length == 0) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
            throw new InvalidDataException("The header has an empty or repeated column name.");

        var cycles = new List<RecordedCycle>();
        int skipped = 0;
        DateTime? prev = null;
        for (string? line; (line = r.ReadLine()) != null;)
        {
            if (line.Trim().Length == 0) continue;
            var f = line.Split(',');
            if (f.Length != cols.Length
                || !DateTime.TryParseExact(f[0].Trim(), "yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
                || (prev is { } p && t < p))
            { skipped++; continue; }
            var values = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            bool bad = false;
            for (int i = 1; i < f.Length; i++)
            {
                var s = f[i].Trim();
                if (s.Length == 0) continue;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v)) values[keys[i - 1]] = v;
                else { bad = true; break; }
            }
            if (bad) { skipped++; continue; }
            cycles.Add(new RecordedCycle(t, values));
            prev = t;
        }
        return new LiveCsvLog(keys, cycles, skipped);
    }
}

/// <summary>
/// Plays a recording back on its own time base: <see cref="Advance"/> takes real elapsed time, scales it by
/// <see cref="Speed"/>, and returns the cycles that became due. Pure (no timers), so the UI drives it from a timer and tests
/// drive it directly.
/// </summary>
public sealed class ReplayCursor
{
    private readonly LiveCsvLog _log;
    private int _next;
    private TimeSpan _pos;
    private double _speed = 1;

    public ReplayCursor(LiveCsvLog log) => _log = log;

    /// <summary>Playback rate, 0.1x to 64x.</summary>
    public double Speed { get => _speed; set => _speed = value is >= 0.1 and <= 64 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Speed must be 0.1 to 64"); }
    /// <summary>Position from the first cycle.</summary>
    public TimeSpan Position => _pos;
    public bool Finished => _next >= _log.Cycles.Count;
    public double Fraction => _log.Duration <= TimeSpan.Zero ? (Finished ? 1 : 0) : Math.Min(1, _pos / _log.Duration);

    public IReadOnlyList<RecordedCycle> Advance(TimeSpan realElapsed)
    {
        if (realElapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(realElapsed));
        if (_log.Cycles.Count == 0) return Array.Empty<RecordedCycle>();
        _pos += realElapsed * _speed;
        var t0 = _log.Cycles[0].T;
        var due = new List<RecordedCycle>();
        while (_next < _log.Cycles.Count && _log.Cycles[_next].T - t0 <= _pos) due.Add(_log.Cycles[_next++]);
        return due;
    }

    /// <summary>Jumps to a fraction of the recording (0..1). The next <see cref="Advance"/> returns cycles from there on.</summary>
    public void Seek(double fraction)
    {
        fraction = Math.Clamp(fraction, 0, 1);
        _pos = _log.Duration * fraction;
        var t0 = _log.Cycles.Count > 0 ? _log.Cycles[0].T : default;
        _next = 0;
        while (_next < _log.Cycles.Count && _log.Cycles[_next].T - t0 < _pos) _next++;
    }
}
