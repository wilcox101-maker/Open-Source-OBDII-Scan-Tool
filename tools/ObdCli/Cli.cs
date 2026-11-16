using System.Globalization;
using System.Text.Json;
using ObdLink.Core.Adapters;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Dashboard;
using ObdLink.Core.Dtc;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.J2534;
using ObdLink.Core.LiveData;
using ObdLink.Core.Modules;
using ObdLink.Core.Monitor;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;

namespace ObdLink.Cli;

/// <summary>
/// v0.17.0: scriptable command line over Core (Phase 5). Read-only toward the vehicle: every UDS client it creates has a closed
/// write gate, so a state-changing request is refused before it is sent. Works against a real adapter (--port) or the
/// simulator (--demo), so contributors can script and test without a truck. Output is plain text, or JSON with --json.
/// Exit codes: 0 OK, 1 error, 2 usage.
/// </summary>
public static class Cli
{
    public const string Usage = """
        obdcli - open-source Ford diagnostics, command line (read-only toward the vehicle)

        Connection (one of):
          --port COM5 [--baud 115200]   real ELM327/STN adapter (baud is probed when omitted)
          --demo [mx|ex]                built-in simulator (no vehicle needed)
          --j2534 NAME|DLL              J2534 pass-thru device (registered name, or the driver DLL path)
          --j2534-demo                  simulated J2534 device (same vehicle as --demo)
        Common: --json   machine-readable output

        Commands:
          j2534-list                                  installed J2534 devices and whether this build can load each driver
          info                                        adapter identity and battery voltage
          scan                                        2017 F-150 module scan with alternate-bus retries
          dtcs [--module 7E0|all]                     UDS DTCs (19 02) with status; 'all' scans first
          live --signals rpm,speed [--seconds 5] [--csv out.csv]
                                                      OBD-II Mode 01 live data (multi-PID requests)
          monitor [--bus hs|ms|hs2] [--seconds 5] [--filter all|diag|7E8|700/780] [--csv out.csv]
                                                      passive bus monitor, per-ID summary
          asbuilt-read --module 726 [--out file.abt]  read every As-Built block of a module
          options --abt file.abt [--tab NAME] [--search TEXT] [--set "ADDR PATTERN=DIGITS"]... [--out new.abt]
                                                      offline: list or edit As-Built options of a FORScan .abt file
        """;

    public static async Task<int> RunAsync(string[] args, TextWriter o, TextWriter e, Func<string, int, ITransport>? serial = null, CancellationToken ct = default)
    {
        Args a;
        try { a = Args.Parse(args); }
        catch (FormatException ex) { e.WriteLine(ex.Message); e.WriteLine(); e.WriteLine(Usage); return 2; }
        if (a.Command is null or "help" or "--help" or "-h") { o.WriteLine(Usage); return a.Command is null ? 2 : 0; }
        try
        {
            return a.Command switch
            {
                "options" => Options(a, o),
                "j2534-list" => J2534List(a, o),
                "info" or "scan" or "dtcs" or "live" or "monitor" or "asbuilt-read" => await WithAdapterAsync(a, o, e, serial, ct),
                _ => Bad(e, $"Unknown command '{a.Command}'."),
            };
        }
        catch (OperationCanceledException) { e.WriteLine("Cancelled."); return 1; }
        catch (Exception ex) when (ex is FormatException or ArgumentException) { e.WriteLine(ex.Message); return 2; }
        catch (Exception ex) { e.WriteLine("Error: " + ex.Message); return 1; }
    }

    private static int Bad(TextWriter e, string msg) { e.WriteLine(msg); e.WriteLine(); e.WriteLine(Usage); return 2; }

    // ---- Connection -----------------------------------------------------------------------------------------------

    /// <summary>An open adapter: ELM/STN (Session set) or J2534 (Session null). SelectBus puts the channel on a Ford bus.</summary>
    private sealed class Conn
    {
        public required IDiagChannel Channel { get; init; }
        public AdapterSession? Session { get; init; }
        public required Func<Bus, CancellationToken, Task> SelectBus { get; init; }
        public required bool CanSwitchBus { get; init; }
        public required object InfoJson { get; init; }
        public required string[] InfoText { get; init; }
    }

    private static int J2534List(Args a, TextWriter o)
    {
        var devs = J2534Registry.Enumerate(new WindowsJ2534Registry());
        var rows = devs.Select(d =>
        {
            var m = File.Exists(d.FunctionLibrary) ? DllInfo.Machine(d.FunctionLibrary) : DllMachine.Unknown;
            return new { name = d.Name, vendor = d.Vendor, dll = d.FunctionLibrary, exists = File.Exists(d.FunctionLibrary), machine = m.ToString(), loadsHere = DllInfo.LoadsInProcess(m), can = d.Can, iso15765 = d.Iso15765 };
        }).ToList();
        if (a.Has("json")) Json(o, rows);
        else
        {
            foreach (var r in rows)
                o.WriteLine($"{r.name} ({r.vendor})  {r.dll}  {(r.exists ? r.machine : "missing")}{(r.exists && !r.loadsHere ? $" - needs the {(r.machine == "X86" ? "x86" : r.machine)} build" : "")}{(r.iso15765 ? "" : " - no ISO15765")}");
            o.WriteLine(rows.Count == 0 ? "No J2534 v04.04 devices are registered on this computer." : $"{rows.Count} device(s). This build runs as {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}.");
        }
        return 0;
    }

    private static async Task<int> WithJ2534Async(Args a, TextWriter o, TextWriter e, CancellationToken ct)
    {
        IJ2534Api api;
        string label;
        if (a.Has("j2534-demo"))
        {
            api = new SimulatedJ2534Api { Responder = SimulatedJ2534Api.FromElmSimulator(new SimulatedAdapter()) };
            label = "Simulated J2534 device";
        }
        else
        {
            var want = a.Get("j2534")!;
            var dev = J2534Registry.Enumerate(new WindowsJ2534Registry()).FirstOrDefault(d => d.Name.Contains(want, StringComparison.OrdinalIgnoreCase));
            var dll = dev?.FunctionLibrary ?? want;
            if (!File.Exists(dll)) throw new InvalidOperationException($"No registered J2534 device matches '{want}' and no such DLL exists. Run 'obdcli j2534-list'.");
            api = new NativeJ2534Api(dll);
            label = dev?.Display ?? Path.GetFileName(dll);
        }
        using (api)
        {
            var devId = await Task.Run(() => api.Open(null), ct);
            try
            {
                var ver = api.ReadVersion(devId);
                double? volts = null;
                try { volts = api.ReadVbattMillivolts(devId) / 1000.0; } catch (J2534Exception) { /* optional */ }
                await using var ch = new J2534DiagChannel(api, devId);
                if (a.Has("j2534-demo")) ch.DefaultTimeout = TimeSpan.FromMilliseconds(100); // the simulator answers at once
                await ch.SelectBusAsync(Bus.HsCan, ct);
                bool ms;
                try { await ch.SelectBusAsync(Bus.MsCan, ct); ms = true; } catch (J2534Exception) { ms = false; }
                await ch.SelectBusAsync(Bus.HsCan, ct);
                var conn = new Conn
                {
                    Channel = ch, Session = null, SelectBus = ch.SelectBusAsync, CanSwitchBus = ms,
                    InfoJson = new { adapter = label, firmware = ver.Firmware, dll = ver.Dll, api = ver.Api, battery = volts, msCan = ms, transport = "J2534" },
                    InfoText = new[]
                    {
                        $"Adapter : {label} (J2534 {ver.Api})",
                        $"Versions: firmware {ver.Firmware}, driver {ver.Dll}",
                        $"Buses   : HS-CAN{(ms ? ", MS-CAN / HS-CAN2 (J2534-2 pin selection)" : " only (no J2534-2 pin selection)")}",
                        $"Battery : {(volts is double v ? v.ToString("0.0", CultureInfo.InvariantCulture) + " V" : "?")}",
                    },
                };
                return await DispatchAsync(a, o, conn, ct);
            }
            finally { try { api.Close(devId); } catch (J2534Exception) { /* already closed */ } }
        }
    }

    private static Task<int> DispatchAsync(Args a, TextWriter o, Conn conn, CancellationToken ct) => a.Command switch
    {
        "info" => Task.FromResult(Info(a, o, conn)),
        "scan" => ScanAsync(a, o, conn, ct),
        "dtcs" => DtcsAsync(a, o, conn, ct),
        "live" => LiveAsync(a, o, conn, ct),
        "monitor" => MonitorAsync(a, o, conn, ct),
        "asbuilt-read" => AsBuiltReadAsync(a, o, conn, ct),
        _ => Task.FromResult(2),
    };

    private static async Task<int> WithAdapterAsync(Args a, TextWriter o, TextWriter e, Func<string, int, ITransport>? serial, CancellationToken ct)
    {
        if (a.Has("j2534") || a.Has("j2534-demo")) return await WithJ2534Async(a, o, e, ct);
        ITransport t;
        AdapterProfile initial;
        if (a.Has("demo"))
        {
            var kind = (a.Get("demo") ?? "mx").ToLowerInvariant();
            var sim = new SimulatedAdapter { Profile = kind == "ex" ? SimProfile.Ex : SimProfile.MxPlus, MonitorIntervalMs = 2 };
            t = sim.CreateTransport(); initial = AdapterProfiles.OtherStn;
        }
        else if (a.Get("port") is string port)
        {
            var make = serial ?? ((p, b) => new SerialPortTransport(p, b));
            initial = AdapterProfiles.ForPort(PortKind.Unknown);
            if (a.Get("baud") is string bs) t = make(port, int.Parse(bs, CultureInfo.InvariantCulture));
            else
            {
                var found = await BaudProber.ProbeAsync(b => make(port, b), initial.BaudCandidates, TimeSpan.FromSeconds(1), ct)
                    ?? throw new InvalidOperationException($"No ELM327/STN adapter answered on {port}.");
                t = found.Transport;
            }
        }
        else return Bad(e, "Give --port COMx, --demo, --j2534 NAME or --j2534-demo.");

        var link = new ElmLink(t);
        try
        {
            var session = new AdapterSession(link, initial: initial);
            var info = await session.InitializeAsync(ct);
            if (session.IsStn) await session.SelectProtocolAsync(StnProtocol.HsCan11_500, ct); else await session.SelectElmProtocolAsync('6', ct);
            bool msCan = session.IsStn && session.Profile.Has(AdapterCapabilities.MsCan);
            var conn = new Conn
            {
                Channel = new ElmDiagChannel(session), Session = session, SelectBus = session.SelectBusAsync, CanSwitchBus = msCan,
                InfoJson = new { elm = info.ElmId, stn = info.StnId, device = info.DeviceId, battery = info.BatteryVolts, profile = session.Profile.DisplayName, msCan, transport = "ELM/STN" },
                InfoText = new[]
                {
                    $"Adapter : {info.ElmId}{(info.StnId is null ? "" : " / " + info.StnId)}{(info.DeviceId is null ? "" : " / " + info.DeviceId)}",
                    $"Profile : {session.Profile.DisplayName}{(msCan ? " (MS-CAN capable)" : "")}",
                    $"Battery : {(info.BatteryVolts is double v ? v.ToString("0.0", CultureInfo.InvariantCulture) + " V" : "?")}",
                },
            };
            return await DispatchAsync(a, o, conn, ct);
        }
        finally { await link.DisposeAsync(); }
    }

    /// <summary>Every client the CLI makes refuses state-changing services before sending (read-only tool).</summary>
    private static UdsClient Uds(Conn c, uint tx) => new(c.Channel, tx) { Gate = new WriteGate(() => true) };

    private static void Json(TextWriter o, object value) => o.WriteLine(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));

    // ---- Commands -------------------------------------------------------------------------------------------------

    private static int Info(Args a, TextWriter o, Conn c)
    {
        if (a.Has("json")) Json(o, c.InfoJson); else foreach (var l in c.InfoText) o.WriteLine(l);
        return 0;
    }

    private static async Task<IReadOnlyList<ScanOutcome>> Scan(Conn c, CancellationToken ct)
    {
        var targets = FordF150_2017.Modules.Where(m => m.HasAddress).Select(m => new ScanTarget(m, m.Bus)).ToList();
        var res = await ModuleScanFlow.RunAsync((mods, p, t) => (c.Session is { } s ? new ModuleScanner(s, c.Channel) : new ModuleScanner(c.SelectBus, c.Channel)).ScanAsync(mods, p, t), targets, c.CanSwitchBus, ct: ct);
        await c.SelectBus(Bus.HsCan, ct);
        return res;
    }

    private static async Task<int> ScanAsync(Args a, TextWriter o, Conn c, CancellationToken ct)
    {
        var res = await Scan(c, ct);
        if (a.Has("json")) Json(o, res.Select(r => new { module = r.Table.Acronym, tx = r.Table.TxId.ToString("X3"), responded = r.Responded, bus = r.AnsweredOn?.ToString(), byRetry = r.FoundByRetry }));
        else
        {
            foreach (var r in res) o.WriteLine($"{r.Table.TxId:X3}  {r.Table.Acronym,-6} {(r.Responded ? "YES" : "no "),-4} {(r.AnsweredOn is Bus b ? ModuleReplies.BusName(b) : "")}{(r.FoundByRetry ? " (found by retry)" : "")}");
            o.WriteLine($"{res.Count(r => r.Responded)} of {res.Count} modules responded");
        }
        return 0;
    }

    private static uint ParseTx(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        if (t.Length != 3 || !uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) || v > 0x7FF)
            throw new FormatException($"'{s}' is not an 11-bit module address like 7E0.");
        return v;
    }

    private static ModuleDefinition DefFor(uint tx) =>
        FordF150_2017.Modules.FirstOrDefault(m => m.TxId == tx) ?? new ModuleDefinition($"0x{tx:X3}", "", Bus.HsCan, tx, tx + 8, Confidence.Unverified);

    /// <summary>Runs <paramref name="work"/> on the module's bus (MS-CAN/HS-CAN2 need an MX+ / EX), then back to HS-CAN.</summary>
    private static async Task OnBus(Conn c, ModuleDefinition m, Bus? bus, Func<Task> work, CancellationToken ct)
    {
        var b = bus ?? m.Bus;
        if (b != Bus.HsCan && !c.CanSwitchBus) throw new InvalidOperationException($"{m.Acronym} is on {ModuleReplies.BusName(b)}; this adapter cannot switch buses.");
        await c.SelectBus(b, ct);
        try { await work(); }
        finally { if (b != Bus.HsCan) await c.SelectBus(Bus.HsCan, ct); }
    }

    private static async Task<int> DtcsAsync(Args a, TextWriter o, Conn c, CancellationToken ct)
    {
        var which = a.Get("module") ?? "all";
        var mods = new List<(ModuleDefinition Def, Bus? Bus)>();
        if (which.Equals("all", StringComparison.OrdinalIgnoreCase)) mods.AddRange((await Scan(c, ct)).Where(r => r.Responded).Select(r => (r.Table, r.AnsweredOn)));
        else mods.Add((DefFor(ParseTx(which)), null));
        var db = DtcDatabase.LoadEmbedded();
        var rows = new List<object>(); int failed = 0;
        foreach (var (def, bus) in mods)
        {
            try
            {
                List<DtcRecord> list = new();
                await OnBus(c, def, bus, async () => list = await Uds(c, def.TxId).ReadDtcsByStatusAsync(ct: ct), ct);
                foreach (var d in list)
                {
                    rows.Add(new { module = def.Acronym, tx = def.TxId.ToString("X3"), code = d.Display, status = d.Status.ToString("X2"), state = DtcStatusBits.Summary(d.Status), description = db.Describe(d.Display) });
                    if (!a.Has("json")) o.WriteLine($"{def.TxId:X3} {def.Acronym,-6} {d.Display,-10} status {d.Status:X2} ({DtcStatusBits.Summary(d.Status)}) {db.Describe(d.Display)}");
                }
                if (!a.Has("json") && list.Count == 0) o.WriteLine($"{def.TxId:X3} {def.Acronym,-6} no DTCs");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                if (!a.Has("json")) o.WriteLine($"{def.TxId:X3} {def.Acronym,-6} not read: {ex.Message}");
            }
        }
        if (a.Has("json")) Json(o, rows);
        return failed > 0 && failed == mods.Count ? 1 : 0;
    }

    private static async Task<int> LiveAsync(Args a, TextWriter o, Conn c, CancellationToken ct)
    {
        var keys = (a.Get("signals") ?? "rpm,speed").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var unknown = keys.Where(k => J1979.Get(k) is null).ToList();
        if (unknown.Count > 0) throw new FormatException($"Unknown signal(s): {string.Join(", ", unknown)}. Known: {string.Join(", ", J1979.All.Select(p => p.Key))}.");
        var seconds = double.Parse(a.Get("seconds") ?? "5", CultureInfo.InvariantCulture);
        var engine = new DashboardEngine(c.Channel);
        await engine.DiscoverAsync(ct);
        engine.Configure(keys, Array.Empty<EnhancedPid>());
        using var rec = a.Get("csv") is string path ? new DashboardCsvRecorder(Path.GetDirectoryName(Path.GetFullPath(path))!, keys, DateTime.Now) : null;
        var readings = new LiveReadings();
        readings.Track(keys.Select(k => J1979.Get(k)!).Select(p => new LiveSignal(p.Key, p.Name, p.Unit, LiveKind.ObdPid, p.Priority)));
        var until = DateTime.UtcNow.AddSeconds(seconds);
        if (!a.Has("json")) o.WriteLine("time         " + string.Join(" ", keys.Select(k => k.PadLeft(10))));
        while (DateTime.UtcNow < until)
        {
            ct.ThrowIfCancellationRequested();
            var v = await engine.PollOnceAsync(ct);
            var now = DateTime.Now;
            if (v.Count > 0)
            {
                readings.Apply(v, now);
                rec?.Write(now, v);
                if (!a.Has("json")) o.WriteLine($"{now:HH:mm:ss.fff} " + string.Join(" ", keys.Select(k => (v.TryGetValue(k, out var x) ? x.ToString("0.##", CultureInfo.InvariantCulture) : "").PadLeft(10))));
            }
            await Task.Delay(50, ct);
        }
        var summary = readings.Rows.Select(r => new { key = r.Key, unit = r.Unit, samples = r.Samples, min = r.Stats.Count > 0 ? r.Stats.Min : (double?)null, max = r.Stats.Count > 0 ? r.Stats.Max : (double?)null, avg = r.Stats.Count > 0 ? r.Stats.Avg : (double?)null });
        if (a.Has("json")) Json(o, new { requests = engine.Mode01Requests, batched = engine.BatchObd, signals = summary });
        else o.WriteLine($"{engine.Mode01Requests} Mode 01 request(s){(engine.BatchObd ? " (multi-PID)" : "")}" + (rec is null ? "" : $"; CSV: {rec.Path}"));
        return 0;
    }

    private static async Task<int> MonitorAsync(Args a, TextWriter o, Conn c, CancellationToken ct)
    {
        var bus = (a.Get("bus") ?? "hs").ToLowerInvariant() switch
        {
            "hs" => Bus.HsCan, "ms" => Bus.MsCan, "hs2" => Bus.HsCan2,
            var x => throw new FormatException($"--bus must be hs, ms or hs2 (got '{x}')."),
        };
        if (c.Session is null) throw new InvalidOperationException("monitor needs an ELM327/STN adapter (--port or --demo); a raw-CAN J2534 monitor is not implemented yet.");
        if (bus != Bus.HsCan && !c.CanSwitchBus) throw new InvalidOperationException($"{ModuleReplies.BusName(bus)} needs an OBDLink MX+ / EX.");
        var filter = IdFilter.Parse(a.Get("filter"));
        var seconds = double.Parse(a.Get("seconds") ?? "5", CultureInfo.InvariantCulture);
        var stats = new MonitorStats();
        using var log = a.Get("csv") is string path ? new CanCsvLog(Path.GetDirectoryName(Path.GetFullPath(path))!, bus.ToString(), DateTime.Now) : null;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stop.CancelAfter(TimeSpan.FromSeconds(seconds));
        var mon = new BusMonitor(c.Session);
        var end = await mon.RunAsync(bus, filter, stats, f => log?.Write(f), () => DateTime.Now, stop.Token);
        if (ct.IsCancellationRequested) throw new OperationCanceledException();
        var rows = stats.Snapshot();
        if (a.Has("json")) Json(o, new { bus = bus.ToString(), frames = stats.Frames, rate = stats.TotalRate, overflows = mon.Overflows, ids = rows.Select(r => new { id = r.IdText, count = r.Count, hz = r.RateHz, data = r.DataText, changed = r.ChangedText }) });
        else
        {
            foreach (var r in rows) o.WriteLine($"{r.IdText,-8} {r.Count,8} {r.RateHz,7:0.0} Hz  {r.DataText,-24} changed {r.ChangedText}");
            o.WriteLine($"{stats.Frames} frames, {stats.TotalRate:0} frames/s, {rows.Count} IDs, stop: {end}{(mon.Overflows > 0 ? $", {mon.Overflows} overflow(s)" : "")}" + (log is null ? "" : $"; CSV: {log.Path}"));
        }
        return 0;
    }

    private static async Task<int> AsBuiltReadAsync(Args a, TextWriter o, Conn c, CancellationToken ct)
    {
        var tx = ParseTx(a.Get("module") ?? throw new FormatException("asbuilt-read needs --module, e.g. --module 726"));
        var def = DefFor(tx);
        // Read-only: 22 DExx through a gated client (no AsBuiltService, which also carries the write path).
        async Task<AsBuiltBlock> Read(byte block, CancellationToken t) =>
            new() { Module = (ushort)tx, Block = block, Data = await Uds(c, tx).ReadDataByIdentifierAsync(AsBuiltBlock.DidForBlock(block), t) };
        List<AsBuiltBlock> blocks = new();
        await OnBus(c, def, null, async () => blocks = await AsBuiltBatch.ReadAllBlocksAsync(Read, ct: ct), ct);
        var lines = blocks.SelectMany(b => b.Lines()).ToList();
        if (a.Get("out") is string outPath) { File.WriteAllText(outPath, ForscanAbtFile.Format(lines)); }
        if (a.Has("json")) Json(o, new { module = def.Acronym, tx = tx.ToString("X3"), blocks = blocks.Count, lines = lines.Select(l => l.Format()) });
        else { foreach (var l in lines) o.WriteLine(l.Format()); o.WriteLine($"{blocks.Count} block(s), {lines.Count} line(s){(a.Get("out") is string p ? "; saved " + p : "")}"); }
        return 0;
    }

    /// <summary>Offline: lists the community options for an .abt file, or applies --set edits and writes a new .abt.</summary>
    private static int Options(Args a, TextWriter o)
    {
        var path = a.Get("abt") ?? throw new FormatException("options needs --abt file.abt");
        var abt = ForscanAbtFile.Load(path);
        var addrs = abt.Lines.Select(l => l.Address.ToString()).ToHashSet();
        var all = FeatureOption.LoadF150_2015_2020().Where(x => addrs.Contains(x.Address)).ToList();
        var tabs = all.Select(x => x.Tab).Distinct().OrderBy(x => x).ToList();
        var module = abt.Lines[0].Address.Module;
        var tab = a.Get("tab") ?? (DecodeProfile.DaleF150_2017.TabByModule.TryGetValue(module, out var pt) && tabs.Contains(pt) ? pt : tabs.FirstOrDefault());
        if (tab is not null && !tabs.Contains(tab)) throw new FormatException($"Tab '{tab}' has no options for this file. Tabs: {string.Join(" | ", tabs)}");
        var opts = all.Where(x => x.Tab == tab).ToList();
        var byAddr = abt.Lines.ToDictionary(l => l.Address.ToString());

        var sets = a.All("set");
        if (sets.Count == 0)
        {
            var search = a.Get("search");
            var shown = opts.Where(x => search is null || x.Description.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            if (a.Has("json")) Json(o, new { tab, tabs, options = shown.Select(x => new { address = x.Address, pattern = x.Pattern, description = x.Description, now = x.Current(byAddr[x.Address]), meaning = x.Current(byAddr[x.Address]) is string d ? x.MeaningOf(d) : null, values = x.Values.Select(v => v.Display) }) });
            else
            {
                o.WriteLine($"Tab: {tab}  (other tabs: {string.Join(" | ", tabs.Where(t => t != tab))})");
                foreach (var x in shown)
                {
                    var now = x.Current(byAddr[x.Address]);
                    o.WriteLine($"{x.Address} {x.Pattern}  now {now ?? "?"}  {x.Description.Replace('\n', ' ')}{(now is not null && x.MeaningOf(now) is string m ? "  = " + m.Replace('\n', ' ') : "")}");
                }
                o.WriteLine($"{shown.Count} option(s). CommunityReported / UNVERIFIED meanings.");
            }
            return 0;
        }

        var edits = new List<OptionEdit>();
        foreach (var s in sets)
        {
            var m = System.Text.RegularExpressions.Regex.Match(s.Trim(), @"^([0-9A-Fa-f]{3}-[0-9A-Fa-f]{2}-[0-9A-Fa-f]{2})\s+([*xX0-9A-Fa-f\-]+)\s*=\s*([0-9A-Fa-f]+)$");
            if (!m.Success) throw new FormatException($"--set \"{s}\": use \"ADDR PATTERN=DIGITS\", e.g. \"7D0-01-01 *xxxxxxxxx--=1\".");
            var opt = opts.FirstOrDefault(x => x.Address.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase) && x.Pattern.Equals(m.Groups[2].Value, StringComparison.OrdinalIgnoreCase))
                ?? throw new FormatException($"--set \"{s}\": no option with that address and pattern in tab '{tab}'.");
            edits.Add(new OptionEdit(opt, m.Groups[3].Value));
        }
        var result = AsBuiltEditor.ApplyAll(abt.Lines, edits);
        var outPath = a.Get("out") ?? throw new FormatException("--set needs --out new.abt (the input file is never overwritten).");
        if (Path.GetFullPath(outPath) == Path.GetFullPath(path)) throw new FormatException("--out must be a different file from --abt.");
        File.WriteAllText(outPath, ForscanAbtFile.Format(result));
        var changed = abt.Lines.Zip(result).Where(p => !ReferenceEquals(p.First, p.Second)).ToList();
        if (a.Has("json")) Json(o, new { written = outPath, changed = changed.Select(p => new { before = p.First.Format(), after = p.Second.Format() }) });
        else { foreach (var (b, n) in changed) o.WriteLine($"{b.Format()}  ->  {n.Format()}"); o.WriteLine($"{changed.Count} line(s) changed; wrote {outPath}"); }
        return 0;
    }
}

/// <summary>Minimal argument parser: first non-option is the command; "--name value" options (a value may not start with "--"); flags without a value.</summary>
internal sealed class Args
{
    private readonly List<(string Name, string? Value)> _opts = new();
    public string? Command { get; private set; }

    /// <summary>Options that never take a value.</summary>
    private static readonly HashSet<string> Flags = new() { "json", "j2534-demo" };

    public static Args Parse(string[] args)
    {
        var a = new Args();
        for (int i = 0; i < args.Length; i++)
        {
            var s = args[i];
            if (s.StartsWith("--", StringComparison.Ordinal) && s.Length > 2)
            {
                var name = s[2..].ToLowerInvariant();
                bool hasNext = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                string? value = null;
                if (name == "demo") { if (hasNext && args[i + 1].ToLowerInvariant() is "mx" or "ex") value = args[++i].ToLowerInvariant(); }
                else if (!Flags.Contains(name))
                {
                    if (!hasNext) throw new FormatException($"--{name} needs a value.");
                    value = args[++i];
                }
                a._opts.Add((name, value));
            }
            else if (a.Command is null) a.Command = s.ToLowerInvariant();
            else throw new FormatException($"Unexpected argument '{s}'.");
        }
        return a;
    }

    public bool Has(string name) => _opts.Any(o => o.Name == name);
    public string? Get(string name) => _opts.LastOrDefault(o => o.Name == name).Value;
    public IReadOnlyList<string> All(string name) => _opts.Where(o => o.Name == name && o.Value is not null).Select(o => o.Value!).ToList();
}
