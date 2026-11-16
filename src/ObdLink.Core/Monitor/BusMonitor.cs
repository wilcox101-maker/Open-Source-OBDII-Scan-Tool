using System.Globalization;
using ObdLink.Core.Elm;

namespace ObdLink.Core.Monitor;

/// <summary>One CAN frame seen on the bus.</summary>
public readonly record struct CanFrame(DateTime T, uint Id, bool Extended, byte[] Data)
{
    public string IdText => Extended ? Id.ToString("X8") : Id.ToString("X3");
    public string DataText => Convert.ToHexString(Data);
}

/// <summary>
/// Parses an ELM/STN monitor line printed with headers on (ATH1), auto-formatting off (ATCAF0) and DLC off (ATD0), spaces on
/// or off. 11-bit: 3 hex header digits; 29-bit: 8. Without spaces the length tells them apart: 3 + 2n is odd, 8 + 2n is even.
/// Anything else (CAN ERROR, &lt;RX ERROR, a line ending in &lt;DATA ERROR, more than 8 data bytes) is not a frame.
/// </summary>
public static class MonitorLine
{
    public static bool TryParse(string line, DateTime t, out CanFrame frame)
    {
        frame = default;
        var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        string id, data;
        if (parts.Length > 1)
        {
            if (parts[0].Length == 3) { id = parts[0]; data = string.Concat(parts.Skip(1)); }
            else if (parts[0].Length == 2 && parts.Length >= 4 && parts.Take(4).All(p => p.Length == 2)) { id = string.Concat(parts.Take(4)); data = string.Concat(parts.Skip(4)); }
            else return false;
        }
        else
        {
            var s = parts[0];
            if (s.Length % 2 == 1) { if (s.Length < 3) return false; id = s[..3]; data = s[3..]; }
            else { if (s.Length < 8) return false; id = s[..8]; data = s[8..]; }
        }
        if (!id.All(Uri.IsHexDigit) || !data.All(Uri.IsHexDigit) || data.Length % 2 != 0 || data.Length > 16) return false;
        uint v = uint.Parse(id, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        bool ext = id.Length == 8;
        if (!ext && v > 0x7FF || ext && v > 0x1FFFFFFF) return false;
        frame = new CanFrame(t, v, ext, Convert.FromHexString(data));
        return true;
    }
}

/// <summary>
/// Which IDs the monitor keeps. "" or "all" = everything; "diag" = 11-bit 700-7FF (diagnostic requests and replies);
/// "7E8" = that ID; "700/780" = id/mask (bits set in the mask must match). Applied in software, after the adapter: the
/// adapter's own receive filters are left alone so normal requests afterwards are not affected.
/// </summary>
public sealed record IdFilter(uint Value, uint Mask, bool? Extended, string Text)
{
    public static readonly IdFilter All = new(0, 0, null, "all");

    public bool Matches(CanFrame f) => (Extended is null || Extended == f.Extended) && (f.Id & Mask) == (Value & Mask);

    public static IdFilter Parse(string? text)
    {
        var t = (text ?? "").Trim();
        if (t.Length == 0 || t.Equals("all", StringComparison.OrdinalIgnoreCase)) return All;
        if (t.Equals("diag", StringComparison.OrdinalIgnoreCase)) return new IdFilter(0x700, 0x700, false, "diag (700-7FF)");
        var parts = t.Split('/');
        if (parts.Length > 2 || parts.Any(p => p.Trim().Length is not (3 or 8) || !p.Trim().All(Uri.IsHexDigit)))
            throw new FormatException($"'{text}' is not a filter. Use all, diag, an ID (7E8 or 18DAF110), or id/mask (700/780).");
        bool ext = parts[0].Trim().Length == 8;
        uint value = uint.Parse(parts[0].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        uint full = ext ? 0x1FFFFFFFu : 0x7FFu;
        uint mask = parts.Length == 2 ? uint.Parse(parts[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture) & full : full;
        if (value > full) throw new FormatException($"ID {parts[0].Trim()} is out of range");
        return new IdFilter(value, mask, ext, t);
    }
}

/// <summary>Per-ID summary for the monitor grid.</summary>
public sealed record MonitorRow(uint Id, bool Extended, long Count, double RateHz, byte[] LastData, byte[] ChangedBits, DateTime LastSeen)
{
    public string IdText => Extended ? Id.ToString("X8") : Id.ToString("X3");
    public string DataText => string.Join(" ", LastData.Select(b => b.ToString("X2")));
    /// <summary>Per byte: "··" unchanged since stats were reset, otherwise the hex mask of bits that have changed.</summary>
    public string ChangedText => string.Join(" ", ChangedBits.Select(b => b == 0 ? "··" : b.ToString("X2")));
}

/// <summary>
/// Running per-ID statistics: count, rate (frames over the time between the first and the latest frame), latest data, and
/// which bits have changed since the stats were reset (OR of every frame-to-frame XOR), so signals stand out in the grid.
/// Thread-safe: the monitor thread adds while the UI takes snapshots.
/// </summary>
public sealed class MonitorStats
{
    private sealed class Entry { public long Count; public DateTime First, Last; public byte[] Data = Array.Empty<byte>(); public byte[] Changed = Array.Empty<byte>(); }
    private readonly Dictionary<(uint, bool), Entry> _ids = new();
    private readonly object _sync = new();
    public long Frames { get; private set; }
    public long Filtered { get; private set; }
    public long NonFrameLines { get; private set; }
    public DateTime? FirstFrame { get; private set; }
    public DateTime? LastFrame { get; private set; }

    public void Add(CanFrame f)
    {
        lock (_sync)
        {
            Frames++; FirstFrame ??= f.T; LastFrame = f.T;
            if (!_ids.TryGetValue((f.Id, f.Extended), out var e)) { _ids[(f.Id, f.Extended)] = e = new Entry { First = f.T, Data = f.Data, Changed = new byte[f.Data.Length] }; }
            else
            {
                if (e.Changed.Length < f.Data.Length) Array.Resize(ref e.Changed, f.Data.Length);
                for (int i = 0; i < f.Data.Length; i++) e.Changed[i] |= (byte)(f.Data[i] ^ (i < e.Data.Length ? e.Data[i] : 0));
                e.Data = f.Data;
            }
            e.Count++; e.Last = f.T;
        }
    }

    public void CountFiltered() { lock (_sync) Filtered++; }
    public void CountNonFrame() { lock (_sync) NonFrameLines++; }

    /// <summary>Frames per second over the whole run.</summary>
    public double TotalRate { get { lock (_sync) return Rate(Frames, FirstFrame, LastFrame); } }

    public IReadOnlyList<MonitorRow> Snapshot()
    {
        lock (_sync)
            return _ids.OrderBy(k => k.Key.Item2).ThenBy(k => k.Key.Item1)
                .Select(k => new MonitorRow(k.Key.Item1, k.Key.Item2, k.Value.Count, Rate(k.Value.Count, k.Value.First, k.Value.Last),
                    (byte[])k.Value.Data.Clone(), (byte[])k.Value.Changed.Clone(), k.Value.Last)).ToList();
    }

    public void Reset() { lock (_sync) { _ids.Clear(); Frames = Filtered = NonFrameLines = 0; FirstFrame = LastFrame = null; } }

    private static double Rate(long n, DateTime? a, DateTime? b) => a is { } x && b is { } y && y > x && n > 1 ? (n - 1) / (y - x).TotalSeconds : 0;
}

/// <summary>CSV log of monitored frames: time,id,ext,dlc,data. Kept open for the run; flushed every frame.</summary>
public sealed class CanCsvLog : IDisposable
{
    private readonly StreamWriter _w;
    public string Path { get; }
    public long Written { get; private set; }
    public CanCsvLog(string dir, string busName, DateTime now)
    {
        Directory.CreateDirectory(dir);
        Path = System.IO.Path.Combine(dir, $"bus-{busName}-{now:yyyyMMdd-HHmmss}.csv");
        _w = new StreamWriter(Path) { AutoFlush = true };
        _w.WriteLine("time,id,ext,dlc,data");
    }
    public void Write(CanFrame f)
    {
        _w.WriteLine($"{f.T.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture)},{f.IdText},{(f.Extended ? 1 : 0)},{f.Data.Length},{f.DataText}");
        Written++;
    }
    public void Dispose() => _w.Dispose();
}

/// <summary>
/// v0.13.0: passive bus monitor. Puts the adapter on the requested bus, clears the receive filter (ATCRA), shows raw frames
/// (ATCAF0), asks for silent monitoring (ATCSM1: the adapter does not acknowledge frames) and runs ATMA until cancelled.
/// Sends nothing onto the bus. If the adapter's buffer overflows (BUFFER FULL) it restarts ATMA and counts the overflow.
/// Afterwards it turns formatting back on and marks the session stale, so the next request re-applies header and receive
/// filter. The caller must hold the shared adapter lock for the whole run.
/// </summary>
public sealed class BusMonitor
{
    private readonly AdapterSession _session;
    public BusMonitor(AdapterSession session) => _session = session;

    public int Overflows { get; private set; }
    public int MaxRestarts { get; set; } = 1000;

    public async Task<MonitorStop> RunAsync(Bus bus, IdFilter filter, MonitorStats stats, Action<CanFrame>? onFrame, Func<DateTime> now, CancellationToken ct)
    {
        await _session.EnsureReadyAsync(ct);
        await _session.SelectBusAsync(bus, ct);
        var link = _session.Link;
        try
        {
            await Expect(link, "ATCRA", ct);
            await Expect(link, "ATCAF0", ct);
            try { await link.SendAsync("ATCSM1", ct: ct); } catch (OperationCanceledException) { throw; } catch { /* older clones lack it; ELM v1.4+ monitors silently by default */ }
            for (int restarts = 0; ; restarts++)
            {
                var stop = await link.MonitorAsync("ATMA", line =>
                {
                    var t = now();
                    if (!MonitorLine.TryParse(line, t, out var f)) { stats.CountNonFrame(); return; }
                    if (!filter.Matches(f)) { stats.CountFiltered(); return; }
                    stats.Add(f);
                    onFrame?.Invoke(f);
                }, ct);
                if (stop != MonitorStop.BufferFull || ct.IsCancellationRequested || restarts >= MaxRestarts) return stop;
                Overflows++;
            }
        }
        finally
        {
            _session.Invalidate();
            try { await link.SendAsync("ATCAF1", ct: CancellationToken.None); } catch { /* re-asserted by EnsureReadyAsync on the next request */ }
        }
    }

    private static async Task Expect(ElmLink link, string cmd, CancellationToken ct)
    {
        var r = await link.SendAsync(cmd, ct: ct);
        if (!r.Ok) throw new IsoTp.DiagLinkException(r.Error, $"{cmd}: {string.Join(" ", r.Lines)}");
    }
}
