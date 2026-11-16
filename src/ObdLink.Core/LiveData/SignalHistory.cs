namespace ObdLink.Core.LiveData;

/// <summary>One sample of one signal.</summary>
public readonly record struct SamplePoint(DateTime T, double V);

/// <summary>
/// v0.12.0: time history of every polled signal, for charts. A fixed-size ring per signal (default 36 000 samples, one hour at
/// 10 Hz), so memory is bounded however long polling runs. Non-finite values are ignored. Thread-safe: the poll loop adds while
/// the chart reads.
/// </summary>
public sealed class SignalHistory
{
    private sealed class Ring
    {
        public readonly SamplePoint[] Buf; public int Start, Count;
        public Ring(int cap) => Buf = new SamplePoint[cap];
        public SamplePoint this[int i] => Buf[(Start + i) % Buf.Length];
        public void Add(SamplePoint p)
        {
            if (Count < Buf.Length) Buf[(Start + Count++) % Buf.Length] = p;
            else { Buf[Start] = p; Start = (Start + 1) % Buf.Length; }
        }
    }

    private readonly Dictionary<string, Ring> _rings = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();
    public int CapacityPerSignal { get; }

    public SignalHistory(int capacityPerSignal = 36_000)
    {
        if (capacityPerSignal < 2) throw new ArgumentOutOfRangeException(nameof(capacityPerSignal));
        CapacityPerSignal = capacityPerSignal;
    }

    /// <summary>Adds one poll cycle at time <paramref name="t"/>. A sample older than the signal's last one is dropped (time must not go backwards).</summary>
    public void Add(IReadOnlyDictionary<string, double> cycle, DateTime t)
    {
        lock (_sync)
            foreach (var (k, v) in cycle)
            {
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                if (!_rings.TryGetValue(k, out var r)) _rings[k] = r = new Ring(CapacityPerSignal);
                if (r.Count > 0 && r[r.Count - 1].T > t) continue;
                r.Add(new SamplePoint(t, v));
            }
    }

    public IReadOnlyList<string> Keys { get { lock (_sync) return _rings.Keys.ToList(); } }

    public int Count(string key) { lock (_sync) return _rings.TryGetValue(key, out var r) ? r.Count : 0; }

    /// <summary>Earliest and latest sample time across all signals, or null when empty.</summary>
    public (DateTime From, DateTime To)? Span
    {
        get
        {
            lock (_sync)
            {
                var live = _rings.Values.Where(r => r.Count > 0).ToList();
                if (live.Count == 0) return null;
                return (live.Min(r => r[0].T), live.Max(r => r[r.Count - 1].T));
            }
        }
    }

    /// <summary>
    /// Samples of <paramref name="key"/> inside [from, to], plus the nearest sample on each side (so a line can be drawn to the
    /// window edges). In time order.
    /// </summary>
    public IReadOnlyList<SamplePoint> Window(string key, DateTime from, DateTime to)
    {
        lock (_sync)
        {
            if (!_rings.TryGetValue(key, out var r) || r.Count == 0) return Array.Empty<SamplePoint>();
            int lo = LowerBound(r, from), hi = LowerBound(r, to);
            while (hi < r.Count && r[hi].T <= to) hi++;
            int a = Math.Max(0, lo - 1), b = Math.Min(r.Count, hi + 1);
            var list = new List<SamplePoint>(b - a);
            for (int i = a; i < b; i++) list.Add(r[i]);
            return list;
        }
    }

    public void Clear() { lock (_sync) _rings.Clear(); }

    private static int LowerBound(Ring r, DateTime t)
    {
        int lo = 0, hi = r.Count;
        while (lo < hi) { int mid = (lo + hi) / 2; if (r[mid].T < t) lo = mid + 1; else hi = mid; }
        return lo;
    }

    /// <summary>
    /// Reduces points for drawing in <paramref name="buckets"/> columns: per column the minimum and the maximum, in time order,
    /// so peaks and dips survive however many samples share a pixel. First and last points are always kept. Points already
    /// sparse enough are returned unchanged.
    /// </summary>
    public static IReadOnlyList<SamplePoint> Decimate(IReadOnlyList<SamplePoint> pts, DateTime from, DateTime to, int buckets)
    {
        if (buckets < 1) throw new ArgumentOutOfRangeException(nameof(buckets));
        if (pts.Count <= 2 * buckets + 2 || to <= from) return pts;
        double width = (to - from).Ticks / (double)buckets;
        var outp = new List<SamplePoint>(2 * buckets + 2) { pts[0] };
        int i = 1, last = pts.Count - 1;
        while (i < last)
        {
            int b = Math.Clamp((int)((pts[i].T - from).Ticks / width), -1, buckets);
            SamplePoint mn = pts[i], mx = pts[i];
            int j = i + 1;
            for (; j < last && Math.Clamp((int)((pts[j].T - from).Ticks / width), -1, buckets) == b; j++)
            {
                if (pts[j].V < mn.V) mn = pts[j];
                if (pts[j].V > mx.V) mx = pts[j];
            }
            if (mn.T <= mx.T) { outp.Add(mn); if (mx != mn) outp.Add(mx); }
            else { outp.Add(mx); outp.Add(mn); }
            i = j;
        }
        outp.Add(pts[last]);
        return outp;
    }
}
