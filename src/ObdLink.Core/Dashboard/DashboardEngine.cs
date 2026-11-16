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

    /// <summary>
    /// v0.14.0: ask for up to six Mode 01 PIDs in one request (J1979 on CAN). PIDs missing from a batch are read one by one in
    /// the same cycle. Turned off for the rest of the engine's life after <see cref="MaxBatchMisses"/> incomplete batches in a
    /// row (an ECU without multi-PID support answers nothing or only the first PID), so it never costs more than single requests
    /// for long.
    /// </summary>
    public bool BatchObd { get; set; } = true;
    public int MaxBatchMisses { get; set; } = 3;
    private int _batchMisses;
    /// <summary>Mode 01 requests sent (single or batched), for diagnostics and tests.</summary>
    public long Mode01Requests { get; private set; }

    private static byte? ObdPid(string key) => key == "mil" ? (byte)0x01 : J1979.Get(key)?.Pid;

    private static void Store(Dictionary<string, double> res, string key, byte[] data)
    {
        if (key == "mil") { var (mil, n) = J1979.DecodeMonitorStatus(data); res["mil"] = mil ? 1 : 0; res["dtcCount"] = n; }
        else res[key] = J1979.Get(key)!.Decode(data);
    }

    /// <summary>Sends batches for the due OBD-II items; returns the items answered (already completed in the scheduler).</summary>
    private async Task<HashSet<PollScheduler.Item>> PollBatchedAsync(List<PollScheduler.Item> obd, Dictionary<string, double> res, CancellationToken ct)
    {
        var done = new HashSet<PollScheduler.Item>();
        foreach (var chunk in obd.Chunk(J1979.MaxPidsPerRequest))
        {
            if (!BatchObd || chunk.Length < 2) break;
            var req = new byte[] { 0x01 }.Concat(chunk.Select(i => ObdPid(i.Key)!.Value)).ToArray();
            var got = new Dictionary<byte, byte[]>();
            try
            {
                Mode01Requests++;
                foreach (var r in (await Send(ObdService.Functional11, null, req, ct)).OrderBy(x => x.SourceId))
                    foreach (var (pid, data) in J1979.ParseMultiPid(r.Payload)) got.TryAdd(pid, data);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Serilog.Log.Debug("Dashboard batch {Req} failed: {Err}", Convert.ToHexString(req), ex.Message); }
            foreach (var item in chunk)
                if (got.TryGetValue(ObdPid(item.Key)!.Value, out var data))
                {
                    try { Store(res, item.Key, data); Scheduler.Completed(item, _now(), true); done.Add(item); }
                    catch (ArgumentException ex) { Serilog.Log.Debug("Dashboard batch decode {Item}: {Err}", item.Key, ex.Message); }
                }
            // A batch that leaves out any requested PID costs a batch plus single requests, so it counts as a miss.
            bool complete = chunk.All(i => got.ContainsKey(ObdPid(i.Key)!.Value));
            if (complete) _batchMisses = 0;
            else if (++_batchMisses >= MaxBatchMisses)
            {
                BatchObd = false;
                Serilog.Log.Information("Multi-PID Mode 01 requests came back incomplete {N} times in a row; polling PIDs one at a time from now on", _batchMisses);
            }
        }
        return done;
    }

    /// <summary>One cycle: polls whatever is due and returns key -> value (base units). "mil" = 1/0, "dtcCount" = n.</summary>
    public async Task<Dictionary<string, double>> PollOnceAsync(CancellationToken ct = default)
    {
        var start = _now(); var res = new Dictionary<string, double>();
        var due = Scheduler.Due(start, BatchObd ? 2 * J1979.MaxPidsPerRequest : 6);
        var batched = BatchObd ? await PollBatchedAsync(due.Where(i => ObdPid(i.Key) is not null).ToList(), res, ct) : new();
        foreach (var item in due.Where(i => !batched.Contains(i)))
        {
            bool ok = false;
            try
            {
                if (item.Key == "mil")
                {
                    Mode01Requests++;
                    var r = (await Send(ObdService.Functional11, null, new byte[] { 0x01, 0x01 }, ct)).FirstOrDefault(x => x.Payload.Length >= 3 && x.Payload[0] == 0x41 && x.Payload[1] == 0x01);
                    if (r != null) { var (mil, n) = J1979.DecodeMonitorStatus(r.Payload[2..]); res["mil"] = mil ? 1 : 0; res["dtcCount"] = n; ok = true; }
                }
                else if (J1979.Get(item.Key) is { } d)
                {
                    Mode01Requests++;
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
