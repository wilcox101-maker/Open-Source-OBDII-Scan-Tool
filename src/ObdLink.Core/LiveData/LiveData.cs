using ObdLink.Core.Dashboard;
using ObdLink.Core.Obd;

namespace ObdLink.Core.LiveData;

public enum LiveKind { ObdPid, Enhanced }

/// <summary>A signal the live-data browser can offer: a Mode 01 OBD-II PID, or a Ford Mode 22 enhanced DID.</summary>
public sealed record LiveSignal(string Key, string Name, string Unit, LiveKind Kind, int Priority, bool Verified = true, string? Note = null)
{
    public string Display => Name + (Unit.Length > 0 ? $" ({Unit})" : "");

    /// <summary>
    /// Selectable signals for a vehicle: every J1979 PID the ECU reported as supported, then the Ford enhanced DIDs from the
    /// editable table. OBD-II wins a key collision (it is standardized). Both reuse the decoders the dashboard already uses.
    /// </summary>
    public static IReadOnlyList<LiveSignal> Catalog(IReadOnlySet<byte> supportedPids, EnhancedPidTable enhanced)
    {
        var list = new List<LiveSignal>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in J1979.All)
            if (supportedPids.Contains(d.Pid) && seen.Add(d.Key))
                list.Add(new LiveSignal(d.Key, d.Name, d.Unit, LiveKind.ObdPid, d.Priority));
        foreach (var e in enhanced.Pids)
            if (seen.Add(e.Key))
                list.Add(new LiveSignal(e.Key, e.Name, e.Unit, LiveKind.Enhanced, e.Key == "gear" ? 0 : 2, e.Verified,
                    e.Verified ? null : $"UNVERIFIED (Mode 22 {e.Header} DID {e.Did})"));
        return list;
    }
}

/// <summary>
/// v0.10.0: accumulates successive poll cycles into per-signal current/min/max/avg, sample counts and last-updated times,
/// for the live-data grid. Pure (no polling, no clock of its own); the view model feeds it <see cref="DashboardEngine.PollOnceAsync"/>
/// results. A signal missing from a cycle keeps its last value and ages (goes stale); it is not zeroed.
/// </summary>
public sealed class LiveReadings
{
    public sealed class Row
    {
        public required string Key { get; init; }
        public required string Name { get; init; }
        public required string Unit { get; init; }
        public double? Current { get; internal set; }
        public GaugeStats Stats { get; } = new();
        public long Samples { get; internal set; }
        public DateTime? Updated { get; internal set; }
    }

    private readonly Dictionary<string, Row> _rows = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Row> _order = new();

    /// <summary>Signals the grid should show, in a stable order. Call before polling; unknown keys from a cycle are ignored.</summary>
    public void Track(IEnumerable<LiveSignal> signals)
    {
        foreach (var s in signals)
            if (!_rows.ContainsKey(s.Key))
            {
                var row = new Row { Key = s.Key, Name = s.Name, Unit = s.Unit };
                _rows[s.Key] = row; _order.Add(row);
            }
    }

    public IReadOnlyList<Row> Rows => _order;

    /// <summary>Folds one poll cycle in. Only tracked keys are recorded; a non-finite value is ignored.</summary>
    public void Apply(IReadOnlyDictionary<string, double> cycle, DateTime now)
    {
        foreach (var (key, value) in cycle)
        {
            if (!_rows.TryGetValue(key, out var row) || double.IsNaN(value) || double.IsInfinity(value)) continue;
            row.Current = value; row.Stats.Add(value); row.Samples++; row.Updated = now;
        }
    }

    public bool IsStale(Row row, DateTime now, TimeSpan after) => row.Updated is { } u && now - u > after;

    public void Reset()
    {
        foreach (var r in _order) { r.Current = null; r.Samples = 0; r.Updated = null; r.Stats.Reset(); }
    }
}
