using ObdLink.Core.IsoTp;
using ObdLink.Core.Obd;

namespace ObdLink.Core.Dashboard;

/// <summary>
/// Read-only live data poller: discovers supported Mode 01 PIDs (0100/0120/0140...), then polls the chosen PIDs plus Ford Mode 22
/// enhanced PIDs through <see cref="PollScheduler"/>. Only services 01 and 22 are ever sent.
/// Values are base units (km/h, °C, kPa); conversion is the UI's job.
/// </summary>
public sealed class DashboardEngine
{
    public static readonly byte[] AllowedServices = { 0x01, 0x22 };
    private readonly IDiagChannel _ch; private readonly Func<long> _now;
    public PollScheduler Scheduler { get; } = new();
    public HashSet<byte> Supported { get; private set; } = new();
    public Dictionary<string, EnhancedPid> Enhanced { get; } = new();
    private readonly Dictionary<string, uint> _answered = new();
    /// <summary>Module (request id) that answered each enhanced key, once known.</summary>
    public IReadOnlyDictionary<string, uint> AnsweredBy => _answered;
    public event Action<string>? Sent;

    public DashboardEngine(IDiagChannel ch, Func<long>? nowMs = null)
    { _ch = ch; var sw = System.Diagnostics.Stopwatch.StartNew(); _now = nowMs ?? (() => sw.ElapsedMilliseconds); }

    private async Task<IReadOnlyList<DiagResponse>> Send(uint tx, uint? rx, byte[] req, CancellationToken ct)
    {
        if (!AllowedServices.Contains(req[0])) throw new InvalidOperationException($"Dashboard may only send services 01/22 (got {req[0]:X2})");
        Sent?.Invoke(Convert.ToHexString(req));
        await _ch.SetTargetAsync(tx, rx, ct);
        return await _ch.RequestAsync(req, TimeSpan.FromMilliseconds(300), ct);
    }

    public async Task<HashSet<byte>> DiscoverAsync(CancellationToken ct = default)
    {
        var set = new HashSet<byte>();
        for (int b = 0x00; b <= 0xC0; b += 0x20)
        {
            var r = (await Send(ObdService.Functional11, null, new byte[] { 0x01, (byte)b }, ct)).FirstOrDefault(x => x.Payload.Length >= 6 && x.Payload[0] == 0x41 && x.Payload[1] == b);
            if (r is null) break;
            foreach (var p in J1979.ParseSupported((byte)b, r.Payload[2..6])) set.Add(p);
            if (!set.Contains((byte)(b + 0x20))) break;
        }
        return Supported = set;
    }

    /// <summary>Sets up the schedule: selected J1979 keys that are supported, PID 01 (MIL) always if supported, plus enabled enhanced PIDs.</summary>
    public void Configure(IEnumerable<string> keys, IEnumerable<EnhancedPid> enhanced)
    {
        Scheduler.Clear(); Enhanced.Clear();
        if (Supported.Contains(0x01)) Scheduler.Add("mil", 2);
        foreach (var k in keys)
            if (J1979.Get(k) is { } d && Supported.Contains(d.Pid)) Scheduler.Add(k, d.Priority);
        foreach (var e in enhanced.Where(e => e.Enabled)) { Enhanced[e.Key] = e; Scheduler.Add(e.Key, e.Key == "gear" ? 0 : 2); }
    }

    /// <summary>One cycle: polls whatever is due and returns key -> value (base units). "mil" = 1/0, "dtcCount" = n.</summary>
    public async Task<Dictionary<string, double>> PollOnceAsync(CancellationToken ct = default)
    {
        var start = _now(); var res = new Dictionary<string, double>();
        foreach (var item in Scheduler.Due(start))
        {
            bool ok = false;
            try
            {
                if (item.Key == "mil")
                {
                    var r = (await Send(ObdService.Functional11, null, new byte[] { 0x01, 0x01 }, ct)).FirstOrDefault(x => x.Payload.Length >= 3 && x.Payload[0] == 0x41 && x.Payload[1] == 0x01);
                    if (r != null) { var (mil, n) = J1979.DecodeMonitorStatus(r.Payload[2..]); res["mil"] = mil ? 1 : 0; res["dtcCount"] = n; ok = true; }
                }
                else if (J1979.Get(item.Key) is { } d)
                {
                    var r = (await Send(ObdService.Functional11, null, new byte[] { 0x01, d.Pid }, ct)).FirstOrDefault(x => x.Payload.Length >= 2 + d.Bytes && x.Payload[0] == 0x41 && x.Payload[1] == d.Pid);
                    if (r != null) { res[item.Key] = d.Decode(r.Payload[2..]); ok = true; }
                }
                else if (Enhanced.TryGetValue(item.Key, out var e))
                {
                    // v0.3.4: try the module that answered last time first, then the others (PCM 7E0 / TCM 7E1 for OSS)
                    var order = _answered.TryGetValue(e.Key, out var known) ? new[] { known }.Concat(e.Candidates.Where(c => c != known)) : e.Candidates;
                    foreach (var tx in order)
                    {
                        var r = (await Send(tx, tx + 8, e.Request, ct)).FirstOrDefault(x => x.Payload.Length >= 3 + e.Bytes && x.Payload[0] == 0x62);
                        if (r != null) { res[item.Key] = e.Decode(r.Payload); _answered[e.Key] = tx; ok = true; break; }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { ok = false; Serilog.Log.Debug("Dashboard read {Item} failed: {Err}", item, ex.Message); }
            Scheduler.Completed(item, _now(), ok);
        }
        Scheduler.CycleTook(_now() - start);
        return res;
    }
}
