using System.Collections.ObjectModel;
using ObdLink.Core.Dashboard;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>One gauge / tile on the dashboard. Values are kept in base units and converted for display.</summary>
public sealed class GaugeVm : ObservableObject
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required GaugeKind Kind { get; init; }
    public Quantity Quantity { get; init; }
    public string BaseUnit { get; init; } = "";
    public double BaseMin { get; init; } public double BaseMax { get; init; } = 100;
    public double? BaseWarnHigh { get; init; } public double? BaseWarnLow { get; init; }
    public string Note { get; init; } = "";
    public GaugeStats Stats { get; } = new();
    internal UnitPrefs Units = new();

    private double _base = double.NaN;
    public double Value => Conv(_base);
    public double Minimum => Conv(BaseMin);
    public double Maximum => Conv(BaseMax);
    public double WarnHigh => BaseWarnHigh is { } h ? Conv(h) : double.NaN;
    public double WarnLow => BaseWarnLow is { } l ? Conv(l) : double.NaN;
    public string Unit => ObdLink.Core.Obd.Units.Convert(0, Quantity, BaseUnit, Units).Unit;
    public string ValueText => double.IsNaN(_base) ? "--" : Fmt(Value);
    public string StatsText => Stats.Count == 0 ? "min -- · max -- · avg --" : $"min {Fmt(Conv(Stats.Min))} · max {Fmt(Conv(Stats.Max))} · avg {Fmt(Conv(Stats.Avg))}";
    public bool IsWarn => !double.IsNaN(_base) && Thresholds.Evaluate(_base, BaseWarnLow, BaseWarnHigh) == Level.Warn;
    private double Conv(double v) => double.IsNaN(v) ? v : ObdLink.Core.Obd.Units.ConvertOnly(v, Quantity, Units);
    private string Fmt(double v) => Quantity is Quantity.Rpm or Quantity.Speed ? v.ToString("0") : Math.Abs(v) < 100 ? v.ToString("0.0") : v.ToString("0");

    public void Update(double baseValue) { _base = baseValue; Stats.Add(baseValue); Refresh(); }
    public void Refresh()
    {
        foreach (var p in new[] { nameof(Value), nameof(ValueText), nameof(StatsText), nameof(IsWarn), nameof(Unit), nameof(Minimum), nameof(Maximum), nameof(WarnHigh), nameof(WarnLow) }) OnPropertyChanged(p);
    }
    public void Clear() { _base = double.NaN; Stats.Reset(); Refresh(); }
}

/// <summary>
/// Read-only live dashboard. Polls only while the Dashboard page is visible, connected and not paused, and only when no other
/// command holds the bus lock (so it never interleaves with a scan / As-Built read). Sends services 01 and 22 only.
/// </summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly IEventLog _log; private readonly Func<AppSettings> _settings; private readonly Func<ElmDiagChannel?> _ch;
    private readonly Func<Task<double?>> _readAtrv; private readonly SemaphoreSlim _bus; private readonly string _logsDir; private readonly string _pidDir;
    private CancellationTokenSource? _cts; private DashboardEngine? _engine; private DashboardCsvRecorder? _rec;
    private EnhancedPidTable _enhanced = new(); private readonly GearEstimator _gearDefault = new();

    public ObservableCollection<GaugeVm> Dials { get; } = new();
    public ObservableCollection<GaugeVm> Bars { get; } = new();
    public RelayCommand PauseCommand { get; }
    public RelayCommand RecordCommand { get; }
    public RelayCommand ResetStatsCommand { get; }
    public RelayCommand OpenPidTableCommand { get; }

    public DashboardViewModel(IEventLog log, Func<AppSettings> settings, Func<ElmDiagChannel?> ch, Func<Task<double?>> readAtrv, SemaphoreSlim bus, string logsDir, string pidDir)
    {
        _log = log; _settings = settings; _ch = ch; _readAtrv = readAtrv; _bus = bus; _logsDir = logsDir; _pidDir = pidDir;
        PauseCommand = new("Dashboard pause/resume", log, _ => { Paused = !Paused; });
        RecordCommand = new("Dashboard record CSV", log, _ => ToggleRecord());
        ResetStatsCommand = new("Dashboard reset min/max/avg", log, _ => { foreach (var g in All) g.Stats.Reset(); foreach (var g in All) g.Refresh(); });
        OpenPidTableCommand = new("Open Ford enhanced PID table", log, _ =>
        {
            var (_, path, _) = EnhancedPidTable.LoadOrCreate(_pidDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        });
        BuildGauges();
    }
    private IEnumerable<GaugeVm> All => Dials.Concat(Bars);

    public void BuildGauges()
    {
        var s = _settings(); Dials.Clear(); Bars.Clear();
        foreach (var key in s.Dashboard.Gauges.Distinct())
        {
            if (J1979.Get(key) is not { } d) continue;
            var g = new GaugeVm { Key = d.Key, Title = d.Name, Kind = d.Kind, Quantity = d.Quantity, BaseUnit = d.Unit, BaseMin = d.Min, BaseMax = d.Max, BaseWarnHigh = d.WarnHigh, BaseWarnLow = d.WarnLow, Note = $"Mode 01 PID {d.Pid:X2}", Units = s.Units };
            if (key == "coolant") g = new GaugeVm { Key = g.Key, Title = g.Title, Kind = g.Kind, Quantity = g.Quantity, BaseUnit = g.BaseUnit, BaseMin = g.BaseMin, BaseMax = g.BaseMax, BaseWarnHigh = s.Dashboard.OverheatC, Note = g.Note, Units = s.Units };
            if (key == "voltage") g = new GaugeVm { Key = g.Key, Title = g.Title, Kind = g.Kind, Quantity = g.Quantity, BaseUnit = g.BaseUnit, BaseMin = g.BaseMin, BaseMax = g.BaseMax, BaseWarnHigh = 15.2, BaseWarnLow = s.Dashboard.LowVoltage, Note = g.Note, Units = s.Units };
            (d.Kind == GaugeKind.Circular ? Dials : Bars).Add(g);
        }
        Bars.Add(new GaugeVm { Key = "atrv", Title = "Battery (adapter ATRV)", Kind = GaugeKind.Bar, Quantity = Quantity.Voltage, BaseUnit = "V", BaseMin = 8, BaseMax = 16, BaseWarnLow = s.Dashboard.LowVoltage, BaseWarnHigh = 15.2, Note = "ATRV", Units = s.Units });
        (_enhanced, _, var err) = EnhancedPidTable.LoadOrCreate(_pidDir);
        if (err != null) _log.Message(LogEventLevel.Warning, "Dashboard", $"Ford enhanced PID table unreadable ({err}); using built-in UNVERIFIED defaults");
        foreach (var e in _enhanced.Pids.Where(e => e.Enabled && e.Key != "gear"))
            Bars.Add(new GaugeVm { Key = e.Key, Title = e.Name + (e.Verified ? "" : " (UNVERIFIED)"), Kind = GaugeKind.Bar, Quantity = e.QuantityValue, BaseUnit = e.Unit, BaseMin = e.Min, BaseMax = e.Max, BaseWarnHigh = e.WarnHigh, BaseWarnLow = e.WarnLow, Note = $"Mode 22 {e.Header} DID {e.Did} UNVERIFIED", Units = s.Units });
    }
    public void ApplyUnits() { var u = _settings().Units; foreach (var g in All) { g.Units = u; g.Refresh(); } }

    private bool _paused; public bool Paused { get => _paused; set { if (Set(ref _paused, value)) { OnPropertyChanged(nameof(StateText)); _log.UserAction("Dashboard " + (value ? "paused" : "resumed")); } } }
    private bool _active; public bool Active { get => _active; private set { if (Set(ref _active, value)) OnPropertyChanged(nameof(StateText)); } }
    private string _info = "Connect (a DEMO port works), then open this page."; public string Info { get => _info; private set => Set(ref _info, value); }
    public string StateText => !Active ? "Stopped" : Paused ? "Paused" : "Live";
    public bool Recording => _rec != null;
    public string RecordText => _rec is null ? "Record CSV" : "Stop recording";
    private string _gear = "--"; public string Gear { get => _gear; private set => Set(ref _gear, value); }
    private string _gearSource = ""; public string GearSource { get => _gearSource; private set => Set(ref _gearSource, value); }
    private bool _mil; public bool MilOn { get => _mil; private set => Set(ref _mil, value); }
    private int _dtc; public int DtcCount { get => _dtc; private set => Set(ref _dtc, value); }
    private bool _lowV; public bool LowVoltage { get => _lowV; private set => Set(ref _lowV, value); }
    private bool _hot; public bool Overheat { get => _hot; private set => Set(ref _hot, value); }
    private bool _link; public bool LinkOk { get => _link; private set => Set(ref _link, value); }
    private string _rate = ""; public string RateText { get => _rate; private set => Set(ref _rate, value); }

    /// <summary>Called by the main VM whenever the page, connection or settings change.</summary>
    public void Update(bool pageVisible, bool connected)
    {
        LinkOk = connected;
        if (pageVisible && connected && _cts is null) { _cts = new(); _ = RunAsync(_cts.Token); }
        else if ((!pageVisible || !connected) && _cts != null) { _cts.Cancel(); _cts = null; }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var ch = _ch(); if (ch is null) return;
        Active = true; var s = _settings();
        try
        {
            _engine = new DashboardEngine(ch);
            _engine.Scheduler.BaseMs = s.Dashboard.RefreshMs;
            await _bus.WaitAsync(ct);
            try { await _engine.DiscoverAsync(ct); } finally { _bus.Release(); }
            _engine.Configure(All.Select(g => g.Key), s.Dashboard.FordEnhancedPids ? _enhanced.Pids : Enumerable.Empty<EnhancedPid>());
            if (s.Dashboard.FordEnhancedPids && _enhanced.Pids.FirstOrDefault(p => p.Key == "gear" && p.Enabled) is null) { }
            Info = $"{_engine.Supported.Count} Mode 01 PIDs supported; polling {_engine.Scheduler.Items.Count} items (read-only: services 01 and 22). Unsupported gauges show --.";
            _log.Message(LogEventLevel.Information, "Dashboard", Info + " Supported: " + string.Join(" ", _engine.Supported.Select(p => p.ToString("X2"))));
            var gearEst = new GearEstimator { Axle = s.Dashboard.AxleRatio, TireCircumferenceM = s.Dashboard.TireCircumferenceM, Ratios = _gearDefault.Ratios };
            long cycles = 0; var atrvDue = DateTime.MinValue;
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(Math.Max(50, s.Dashboard.RefreshMs / 2), ct);
                if (Paused) continue;
                if (!await _bus.WaitAsync(0, ct)) continue; // another command is using the adapter
                Dictionary<string, double> v;
                try
                {
                    v = await _engine.PollOnceAsync(ct);
                    if (DateTime.Now >= atrvDue) { atrvDue = DateTime.Now.AddSeconds(2); if (await _readAtrv() is { } atrv) v["atrv"] = atrv; }
                }
                finally { _bus.Release(); }
                Apply(v, gearEst);
                if (++cycles % 20 == 0) RateText = $"×{_engine.Scheduler.Factor:0.0} rate factor · fast PIDs every {_engine.Scheduler.BaseMs * _engine.Scheduler.Factor:0} ms";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Info = "Dashboard stopped: " + ex.Message; _log.Message(LogEventLevel.Warning, "Dashboard", "Polling stopped", ex); }
        finally { Active = false; }
    }

    private void Apply(Dictionary<string, double> v, GearEstimator est)
    {
        if (v.Count == 0) return;
        var s = _settings();
        foreach (var g in All.Distinct()) if (v.TryGetValue(g.Key, out var x)) g.Update(x);
        if (v.TryGetValue("mil", out var mil)) MilOn = mil > 0;
        if (v.TryGetValue("dtcCount", out var n)) DtcCount = (int)n;
        if (v.TryGetValue("coolant", out var ct)) Overheat = ct >= s.Dashboard.OverheatC;
        if (v.TryGetValue("voltage", out var vv) || v.TryGetValue("atrv", out vv)) LowVoltage = vv < s.Dashboard.LowVoltage;
        if (v.TryGetValue("gear", out var gr) && _enhanced.Pids.FirstOrDefault(p => p.Key == "gear") is { } gp)
        { Gear = gp.Label(gr); GearSource = $"PCM {gp.Header} DID {gp.Did} (UNVERIFIED)"; }
        else if (s.Dashboard.EstimateGear && Dials.Concat(Bars).FirstOrDefault(g => g.Key == "rpm") is { } r && Dials.Concat(Bars).FirstOrDefault(g => g.Key == "speed") is { } sp
                 && v.ContainsKey("speed") && (_engine?.Enhanced.ContainsKey("gear") != true || _engine.Scheduler.Items.First(i => i.Key == "gear").Failures > 0))
        {
            var rpmBase = r.Stats.Count > 0 ? v.GetValueOrDefault("rpm", double.NaN) : double.NaN; var kmh = v["speed"];
            if (!double.IsNaN(rpmBase)) { var e = est.Estimate(rpmBase, kmh); Gear = e?.ToString() ?? (kmh < 5 ? "N/P" : "--"); GearSource = "estimated from RPM/speed (6R80 ratios)"; }
        }
        if (_rec != null) _rec.Write(DateTime.Now, v);
    }

    private void ToggleRecord()
    {
        if (_rec != null) { var p = _rec.Path; _rec.Dispose(); _rec = null; _log.Message(LogEventLevel.Information, "Dashboard", $"Recording saved {p}"); }
        else
        {
            var cols = All.Select(g => g.Key).Distinct().Concat(new[] { "gear", "mil", "dtcCount" }).Distinct().ToList();
            _rec = new DashboardCsvRecorder(_logsDir, cols, DateTime.Now);
            _log.Message(LogEventLevel.Information, "Dashboard", $"Recording to {_rec.Path} (values in metric/base units)");
        }
        OnPropertyChanged(nameof(Recording)); OnPropertyChanged(nameof(RecordText));
    }
}
