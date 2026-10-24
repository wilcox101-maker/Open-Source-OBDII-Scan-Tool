namespace ObdLink.Core.Dashboard;

/// <summary>Running min / max / average for a numeric tile.</summary>
public sealed class GaugeStats
{
    public double Min { get; private set; } = double.NaN;
    public double Max { get; private set; } = double.NaN;
    public double Avg => Count == 0 ? double.NaN : _sum / Count;
    public long Count { get; private set; }
    private double _sum;
    public void Add(double v)
    {
        if (double.IsNaN(v)) return;
        Min = Count == 0 ? v : Math.Min(Min, v); Max = Count == 0 ? v : Math.Max(Max, v); _sum += v; Count++;
    }
    public void Reset() { Min = Max = double.NaN; _sum = 0; Count = 0; }
}

public enum Level { Normal, Warn }
public static class Thresholds
{
    public static Level Evaluate(double v, double? low, double? high) => (high is { } h && v >= h) || (low is { } l && v <= l) ? Level.Warn : Level.Normal;
}

/// <summary>Writes dashboard samples as CSV (one row per poll cycle; columns fixed at start). Invariant culture.</summary>
public sealed class DashboardCsvRecorder : IDisposable
{
    private readonly StreamWriter _w; private readonly IReadOnlyList<string> _cols;
    public string Path { get; }
    public DashboardCsvRecorder(string dir, IReadOnlyList<string> columns, DateTime now)
    {
        Directory.CreateDirectory(dir);
        Path = System.IO.Path.Combine(dir, $"dashboard-{now:yyyyMMdd-HHmmss}.csv");
        _cols = columns; _w = new StreamWriter(Path) { AutoFlush = true };
        _w.WriteLine("time," + string.Join(",", columns));
    }
    public void Write(DateTime t, IReadOnlyDictionary<string, double> values) =>
        _w.WriteLine(t.ToString("yyyy-MM-ddTHH:mm:ss.fff", System.Globalization.CultureInfo.InvariantCulture) + "," +
            string.Join(",", _cols.Select(c => values.TryGetValue(c, out var v) ? v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : "")));
    public void Dispose() => _w.Dispose();
}
