using System.Collections.ObjectModel;
using ObdLink.Core.Dashboard;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
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
    private string _note = "";
    public string Note { get => _note; set { if (Set(ref _note, value)) OnPropertyChanged(nameof(TileFooter)); } }
    private string _subtitle = "";
    public string Subtitle { get => _subtitle; set { if (Set(ref _subtitle, value)) { OnPropertyChanged(nameof(HasSubtitle)); OnPropertyChanged(nameof(TileFooter)); } } }
    public bool HasSubtitle => _subtitle.Length > 0;
    /// <summary>Tile footer. An UNVERIFIED source line replaces min/max/avg so the warning is the thing you read.</summary>
    public string TileFooter => HasSubtitle ? Subtitle : StatsText;
    public GaugeStats Stats { get; } = new();
    internal UnitPrefs Units = new();

    private double _base = double.NaN;
    public double Value => Conv(_base);
    public double Minimum => Conv(BaseMin);
    public double Maximum => Conv(BaseMax);
    public double WarnHigh => BaseWarnHigh is { } h ? Conv(h) : double.NaN;
    public double WarnLow => BaseWarnLow is { } l ? Conv(l) : double.NaN;
    public string Unit => ObdLink.Core.Obd.Units.Convert(0, Quantity, BaseUnit, Units).Unit;
    public string? TextOverride { get; set; }
    private bool _unsupported; public bool Unsupported { get => _unsupported; set { if (Set(ref _unsupported, value)) Refresh(); } }
    public string ValueText => TextOverride ?? (double.IsNaN(_base) ? (Unsupported ? "unsupported" : "--") : Fmt(Value));
    public bool ShowAnalog { get; set; } = true;
    public bool HasValue => !double.IsNaN(_base);
    public string StatsText => Stats.Count == 0 ? "min -- · max -- · avg --" : $"min {Fmt(Conv(Stats.Min))} · max {Fmt(Conv(Stats.Max))} · avg {Fmt(Conv(Stats.Avg))}";
    public bool IsLow => !double.IsNaN(_base) && BaseWarnLow is { } lo && _base < lo;
    public bool IsWarn => !double.IsNaN(_base) && Thresholds.Evaluate(_base, BaseWarnLow, BaseWarnHigh) == Level.Warn;
    private double Conv(double v) => double.IsNaN(v) ? v : ObdLink.Core.Obd.Units.ConvertOnly(v, Quantity, Units);
    private string Fmt(double v) => Quantity is Quantity.Rpm or Quantity.Speed ? v.ToString("0") : Math.Abs(v) < 100 ? v.ToString("0.0") : v.ToString("0");

    public void Update(double baseValue) { _base = baseValue; Stats.Add(baseValue); Refresh(); }
    public void Refresh()
    {
        foreach (var p in new[] { nameof(Value), nameof(ValueText), nameof(HasValue), nameof(IsLow), nameof(StatsText), nameof(TileFooter), nameof(Subtitle), nameof(HasSubtitle), nameof(Note), nameof(IsWarn), nameof(Unit), nameof(Minimum), nameof(Maximum), nameof(WarnHigh), nameof(WarnLow), nameof(ShowAnalog) }) OnPropertyChanged(p);
    }
    public void Clear() { _base = double.NaN; Stats.Reset(); Refresh(); }
}

/// <summary>One item on the dashboard (a gauge in some display style, or the TPMS widget).</summary>
public sealed class DashItemVm : ObservableObject
{
    private readonly DashboardViewModel _owner;
    public DashItem Item { get; }
    public GaugeVm Gauge { get; }
    public CatalogEntry? Entry { get; }
    public TpmsVm? Tpms { get; init; }
    /// <summary>Set for write cards (<see cref="DisplayKind.Action"/>).</summary>
    public DashActionCardVm? Action { get; init; }
    public RelayCommand MoveLeftCommand { get; }
    public RelayCommand MoveRightCommand { get; }
    public DashItemVm(DashboardViewModel owner, DashItem item, GaugeVm gauge, CatalogEntry? entry)
    {
        _owner = owner; Item = item; Gauge = gauge; Entry = entry;
        _flowWidth = DashboardFlow.Metrics(item.Display, item.Size).Width;
        // Parameterless commands: the button's DataContext is this card, so the click cannot lose the item
        // the way a parent-ItemsControl CommandParameter binding can.
        MoveLeftCommand = new("Dashboard move item left", null, _ => _owner.MoveLeftCommand.Execute(this));
        MoveRightCommand = new("Dashboard move item right", null, _ => _owner.MoveRightCommand.Execute(this));
        SyncFace();
    }
    private double _flowWidth;
    /// <summary>Pre-transform card width. Saved size, or the stacked width when the window is narrower than the widest card.</summary>
    public double FlowWidth { get => _flowWidth; internal set => Set(ref _flowWidth, value); }
    public string Key => Item.Key;
    public string Title => Gauge.Title;
    public DisplayKind[] Displays => Entry?.Displays ?? new[] { Item.Display };
    public DisplayKind Display { get => Item.Display; set { if (Item.Display == value) return; Item.Display = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLarge)); SyncFace(); _owner.Changed("display " + Key + " = " + value); } }
    public string[] Faces { get; } = { GaugeFaces.Analog, GaugeFaces.Digital };
    public bool ShowFaceChoice => Entry?.Displays.Contains(DisplayKind.Dial) == true;
    public string Face
    {
        get => GaugeFaces.Normalize(Item.Face);
        set
        {
            var face = GaugeFaces.Normalize(value);
            if (Item.Face == face) { SyncFace(); return; }
            Item.Face = face;
            SyncFace();
            OnPropertyChanged();
            _owner.Changed("face " + Key + " = " + face);
        }
    }
    public void SyncFace()
    {
        Gauge.ShowAnalog = GaugeFaces.ShowsNeedle(Item.Display, Item.Face);
        Gauge.Refresh();
    }
    public bool IsLarge { get => Item.Size is ItemSize.Large or ItemSize.Wide; set => Size = value ? ItemSize.Large : ItemSize.Small; }
    private static readonly ItemSize[] AllSizes = { ItemSize.Small, ItemSize.Medium, ItemSize.Large, ItemSize.Wide }; public ItemSize[] Sizes => AllSizes;
    public ItemSize Size { get => Item.Size; set { if (Item.Size == value) return; Item.Size = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLarge)); _owner.Changed("size " + Key + " = " + value); } }
    public string MoveHint => $"{Title}: Ctrl+Left/Right moves one place, Ctrl+Up/Down one row, Ctrl+Home/End to first/last";
    public bool CanChangeDisplay => Displays.Length > 1;
}

/// <summary>Top-view TPMS widget: LF/RF/RR/LR (no spare since v0.3.0). Values in kPa (base), converted per Settings.</summary>
public sealed class TpmsVm : ObservableObject
{
    public GaugeVm LF { get; init; } = null!; public GaugeVm RF { get; init; } = null!; public GaugeVm RR { get; init; } = null!; public GaugeVm LR { get; init; } = null!;
    public IEnumerable<GaugeVm> All => new[] { LF, RF, RR, LR };
    public bool AnyLow => All.Any(g => g.IsLow);
    public void Refresh() => OnPropertyChanged(nameof(AnyLow));
}


/// <summary>
/// v0.20.0: the one poll loop behind all three dashboard windows. Polls the union of the items shown in the open windows
/// (all three windows' items when Keep live runs with every window closed), so three windows never mean three loops on one
/// adapter. Owns the shared gauges (one <see cref="GaugeVm"/> per key, bound in every window that shows it), pause, CSV recording,
/// the lamp row and the state text. Sends services 01 and 22 only, and only when no other command holds the bus lock.
/// Write cards do not go through here: they run through <see cref="DashActionCardVm"/> under the write gate.
/// </summary>
public sealed class DashboardPoller : ObservableObject
{
    private readonly IEventLog _log; private readonly Func<AppSettings> _settings; private readonly Func<ElmDiagChannel?> _ch; private readonly Action _save;
    private readonly Func<Task<double?>> _readAtrv; private readonly SemaphoreSlim _bus; private readonly string _logsDir; private readonly string _pidDir;
    private CancellationTokenSource? _cts; private DashboardEngine? _engine; private DashboardCsvRecorder? _rec; private volatile bool _reconfigure;
    private bool _connected; private bool _loopFailed; private bool _stopping;
    private EnhancedPidTable _enhanced = new(); private readonly GearEstimator _gearDefault = new();
    private readonly Dictionary<string, GaugeVm> _gauges = new();
    private readonly List<DashboardViewModel> _slots = new();

    public RelayCommand PauseCommand { get; }
    public RelayCommand RecordCommand { get; }
    public RelayCommand ResetStatsCommand { get; }
    public RelayCommand OpenPidTableCommand { get; }

    public DashboardPoller(IEventLog log, Func<AppSettings> settings, Func<ElmDiagChannel?> ch, Func<Task<double?>> readAtrv, SemaphoreSlim bus, string logsDir, string pidDir, Action save)
    {
        _log = log; _settings = settings; _ch = ch; _readAtrv = readAtrv; _bus = bus; _logsDir = logsDir; _pidDir = pidDir; _save = save;
        PauseCommand = new("Dashboard pause/resume", log, _ => { Paused = !Paused; });
        RecordCommand = new("Dashboard record CSV", log, _ => ToggleRecord());
        ResetStatsCommand = new("Dashboard reset min/max/avg", log, _ => { foreach (var g in _gauges.Values) { g.Stats.Reset(); g.Refresh(); } });
        OpenPidTableCommand = new("Open Ford enhanced PID table", log, _ =>
        {
            var (_, path, _) = EnhancedPidTable.LoadOrCreate(_pidDir);
            DialogService.OpenExternal(path);
        });
        ReloadEnhanced();
    }

    internal void Register(DashboardViewModel slot) => _slots.Add(slot);
    public IReadOnlyList<DashboardViewModel> Slots => _slots;
    internal EnhancedPidTable Enhanced => _enhanced;
    internal ISet<byte>? Supported => _engine?.Supported is { Count: > 0 } s ? s : null;

    internal void ReloadEnhanced()
    {
        (_enhanced, _, var err) = EnhancedPidTable.LoadOrCreate(_pidDir);
        if (err != null) _log.Message(LogEventLevel.Warning, "Dashboard", $"Ford enhanced PID table unreadable ({err}); using built-in UNVERIFIED defaults");
    }

    /// <summary>A layout changed in some window: other windows showing the same layout rebuild, and the poller re-reads its keys.</summary>
    internal void LayoutChanged(DashboardViewModel from, string layoutName)
    {
        foreach (var s in _slots) if (!ReferenceEquals(s, from) && s.ActiveLayout == layoutName) s.BuildGauges();
        _reconfigure = true;
    }
    internal void Reconfigure() => _reconfigure = true;

    /// <summary>The shared gauge for a key (one instance per key across all windows).</summary>
    internal GaugeVm Gauge(string key)
    {
        if (_gauges.TryGetValue(key, out var have)) return have;
        var s = _settings();
        GaugeVm g;
        var tpmsLow = s.Dashboard.TpmsLowKpa;
        if (J1979.Get(key) is { } d)
            g = new GaugeVm { Key = d.Key, Title = d.Name, Kind = d.Kind, Quantity = d.Quantity, BaseUnit = d.Unit, BaseMin = d.Min, BaseMax = d.Max,
                BaseWarnHigh = key == "coolant" ? s.Dashboard.OverheatC : key == "voltage" ? 15.2 : d.WarnHigh, BaseWarnLow = key == "voltage" ? s.Dashboard.LowVoltage : d.WarnLow, Note = $"Mode 01 PID {d.Pid:X2}", Units = s.Units };
        else if (key == "atrv")
            g = new GaugeVm { Key = "atrv", Title = "Battery (adapter ATRV)", Kind = GaugeKind.Bar, Quantity = Quantity.Voltage, BaseUnit = "V", BaseMin = 8, BaseMax = 16, BaseWarnLow = s.Dashboard.LowVoltage, BaseWarnHigh = 15.2, Note = "ATRV", Units = s.Units };
        else if (key == "gear")
            g = new GaugeVm { Key = "gear", Title = UnverifiedLabels.GearTitle, Kind = GaugeKind.Tile, Note = "Ford DID 1E12 (UNVERIFIED) or estimate", Subtitle = UnverifiedLabels.GearTitle, Units = s.Units, TextOverride = "--" };
        else if (key == "mil")
            g = new GaugeVm { Key = "mil", Title = "Check engine (MIL)", Kind = GaugeKind.Tile, BaseMin = 0, BaseMax = 1, BaseWarnHigh = 0.5, Note = "Mode 01 PID 01 bit 7", Units = s.Units };
        else if (key == "dtcCount")
            g = new GaugeVm { Key = "dtcCount", Title = "Stored DTCs", Kind = GaugeKind.Tile, BaseMin = 0, BaseMax = 20, BaseWarnHigh = 0.5, Note = "Mode 01 PID 01", Units = s.Units };
        else if (_enhanced.Pids.FirstOrDefault(p => p.Key == key) is { } e)
            g = new GaugeVm { Key = e.Key, Title = UnverifiedLabels.Mark(e.Name, e.Verified), Kind = GaugeKind.Bar, Quantity = e.QuantityValue, BaseUnit = e.Unit, BaseMin = e.Min, BaseMax = e.Max,
                BaseWarnHigh = e.WarnHigh, BaseWarnLow = key.StartsWith("tpms_") ? tpmsLow : e.WarnLow,
                Note = e.Key == "oss" && uint.TryParse(e.Header, System.Globalization.NumberStyles.HexNumber, null, out var ossTx0) ? UnverifiedLabels.OssSource(ossTx0, e.Did) : $"Mode 22 {e.Header} DID {e.Did}" + (e.Verified ? "" : " UNVERIFIED"),
                Subtitle = e.Verified ? "" : "UNVERIFIED", Units = s.Units };
        else
            g = new GaugeVm { Key = key, Title = key + " (unknown)", Kind = GaugeKind.Tile, Note = "Not in the PID list or the Ford PID table", Units = s.Units, Unsupported = true };
        return _gauges[key] = g;
    }

    private IEnumerable<GaugeVm> Shown => _slots.SelectMany(s => s.ShownGauges).Distinct();
    public void ApplyUnits() { var u = _settings().Units; foreach (var g in _gauges.Values) { g.Units = u; g.Refresh(); } }
    /// <summary>Threshold changes from the Settings page (TPMS low, overheat, low voltage): every window rebuilds its gauges.</summary>
    public void ApplyThresholds() { _gauges.Clear(); foreach (var s in _slots) s.BuildGauges(); }

    private bool _paused; public bool Paused { get => _paused; set { if (Set(ref _paused, value)) { OnPropertyChanged(nameof(StateText)); _log.UserAction("Dashboard " + (value ? "paused" : "resumed")); } } }
    private bool _active; public bool Active { get => _active; private set { if (Set(ref _active, value)) OnPropertyChanged(nameof(StateText)); } }
    public bool AnyWindowOpen => _slots.Any(s => s.WindowOpen);
    public bool Engaged { get; private set; }
    public bool KeepLiveWhileClosed
    {
        get => _settings().Dashboard.KeepLiveWhileClosed;
        set
        {
            var d = _settings().Dashboard;
            if (d.KeepLiveWhileClosed == value) return;
            d.KeepLiveWhileClosed = value;
            OnPropertyChanged();
            _save();
            Update(_connected);
        }
    }
    private string _info = "Connect (a DEMO port works), then open a dashboard window."; public string Info { get => _info; private set => Set(ref _info, value); }
    public string StateText => DashboardSession.StateText(_connected, AnyWindowOpen, KeepLiveWhileClosed, Paused, Engaged, _loopFailed);
    public bool Recording => _rec != null;
    public string RecordText => _rec is null ? "Record CSV" : "Stop recording";
    private string _gear = "--"; public string Gear { get => _gear; private set { if (Set(ref _gear, value) && _gauges.TryGetValue("gear", out var g)) { g.TextOverride = value; g.Refresh(); } } }
    private string _gearSource = ""; public string GearSource { get => _gearSource; private set => Set(ref _gearSource, value); }
    private bool _mil; public bool MilOn { get => _mil; private set => Set(ref _mil, value); }
    private int _dtc; public int DtcCount { get => _dtc; private set => Set(ref _dtc, value); }
    private bool _lowV; public bool LowVoltage { get => _lowV; private set => Set(ref _lowV, value); }
    private bool _hot; public bool Overheat { get => _hot; private set => Set(ref _hot, value); }
    private bool _lowTire; public bool LowTire { get => _lowTire; private set => Set(ref _lowTire, value); }
    private bool _link; public bool LinkOk { get => _link; private set => Set(ref _link, value); }
    private string _rate = ""; public string RateText { get => _rate; private set => Set(ref _rate, value); }

    /// <summary>A window opened or closed: re-evaluate whether to poll and which keys.</summary>
    internal void WindowChanged(bool opened)
    {
        if (opened) Engaged = true;
        _reconfigure = true;
        Update(_connected);
    }

    /// <summary>Process exit. Cancels polling even when Keep live is on, and closes an open CSV recorder.</summary>
    public void Stop()
    {
        _stopping = true;
        if (_cts != null) { _cts.Cancel(); _cts = null; }
        if (_rec != null) { _rec.Dispose(); _rec = null; OnPropertyChanged(nameof(Recording)); OnPropertyChanged(nameof(RecordText)); }
    }

    /// <summary>Called whenever the connection changes and when a window opens or closes.</summary>
    public void Update(bool connected)
    {
        if (_stopping) return;
        _connected = connected;
        LinkOk = connected;
        var run = DashboardSession.ShouldRun(connected, AnyWindowOpen, KeepLiveWhileClosed);
        if (run && _cts is null)
        {
            Engaged = true;
            _loopFailed = false;
            _cts = new();
            _run = RunAfterAsync(_run, _cts.Token);
        }
        else if (!run && _cts != null) { _cts.Cancel(); _cts = null; }
        OnPropertyChanged(nameof(StateText));
    }

    /// <summary>Layouts to poll: the open windows', or every window's while Keep live runs with all of them closed.</summary>
    private IEnumerable<DashboardLayout> PolledLayouts()
    {
        var open = _slots.Where(s => s.WindowOpen).ToList();
        return (open.Count > 0 ? open : _slots).Select(s => s.Layout);
    }

    private void Configure(AppSettings s)
    {
        var keys = new HashSet<string>();
        foreach (var l in PolledLayouts()) keys.UnionWith(DashCatalog.PollKeys(l, s.Dashboard.EstimateGear));
        var enh = s.Dashboard.FordEnhancedPids ? _enhanced.Pids.Where(p => keys.Contains(p.Key)) : Enumerable.Empty<EnhancedPid>();
        _engine!.Configure(keys, enh);
        foreach (var g in Shown)
            g.Unsupported = g.Key is not ("gear" or "atrv" or "mil" or "dtcCount" or "tpms") && !DashActions.IsActionKey(g.Key) && _engine.Scheduler.Items.All(i => i.Key != g.Key);
        Info = $"{_engine.Supported.Count} Mode 01 PIDs supported; polling {_engine.Scheduler.Items.Count} items for {_slots.Count(x => x.WindowOpen)} open window(s) (read-only: services 01 and 22).";
        _log.Message(LogEventLevel.Information, "Dashboard", Info + " Polling: " + string.Join(" ", _engine.Scheduler.Items.Select(i => i.Key)) + "; supported: " + string.Join(" ", _engine.Supported.Select(p => p.ToString("X2"))));
        _reconfigure = false;
    }

    private Task _run = Task.CompletedTask;
    /// <summary>v0.3.4 audit: a quick hide/show (or reconnect) used to start a second polling loop while the old one was still
    /// winding down; both shared _engine and the old loop's finally cleared Active for the new one. Now the new loop waits for the old.</summary>
    private async Task RunAfterAsync(Task previous, CancellationToken ct)
    {
        try { await previous; } catch { /* the previous loop already logged its own failure */ }
        if (!ct.IsCancellationRequested) await RunAsync(ct);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var ch = _ch(); if (ch is null) return;
        Active = true; var s = _settings();
        try
        {
            _engine = new DashboardEngine(ch) { BatchObd = s.Dashboard.BatchObdRequests };
            _engine.Scheduler.BaseMs = s.Dashboard.RefreshMs;
            await _bus.WaitAsync(ct);
            try { await _engine.DiscoverAsync(ct); } finally { _bus.Release(); }
            Configure(s);
            var gearEst = new GearEstimator { Axle = s.Dashboard.AxleRatio, TireCircumferenceM = s.Dashboard.TireCircumferenceM, Ratios = _gearDefault.Ratios };
            long cycles = 0; var atrvDue = DateTime.MinValue; var good = new HashSet<string>();
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(Math.Max(50, s.Dashboard.RefreshMs / 2), ct);
                if (_reconfigure) { Configure(s); good.Clear(); }
                if (Paused) continue;
                if (!await _bus.WaitAsync(0, ct)) continue; // another command (or a write card) is using the adapter
                Dictionary<string, double> v;
                try
                {
                    v = await _engine.PollOnceAsync(ct);
                    if (DateTime.Now >= atrvDue && Shown.Any(g => g.Key is "atrv")) { atrvDue = DateTime.Now.AddSeconds(2); if (await _readAtrv() is { } atrv) v["atrv"] = atrv; }
                }
                finally { _bus.Release(); }
                foreach (var k in v.Keys) good.Add(k);
                foreach (var i in _engine.Scheduler.Items.Where(i => i.Disabled && !good.Contains(i.Key)))
                    if (_gauges.TryGetValue(i.Key, out var g) && !g.Unsupported) { g.Unsupported = true; _log.Message(LogEventLevel.Information, "Dashboard", $"{i.Key}: no response after {i.Failures} tries - shown as unsupported"); }
                Apply(v, gearEst);
                if (++cycles % 20 == 0) RateText = $"×{_engine.Scheduler.Factor:0.0} rate factor · fast PIDs every {_engine.Scheduler.BaseMs * _engine.Scheduler.Factor:0} ms";
            }
        }
        catch (OperationCanceledException) { /* normal stop (windows closed or disconnected) */ }
        catch (Exception ex)
        {
            _loopFailed = true;
            Info = "Dashboard stopped: " + ex.Message;
            _log.Message(LogEventLevel.Warning, "Dashboard", "Polling stopped", ex);
        }
        finally
        {
            Active = false;
            if (_cts is not null && _cts.Token == ct) _cts = null;
            OnPropertyChanged(nameof(StateText));
        }
    }

    private void Apply(Dictionary<string, double> v, GearEstimator est)
    {
        if (v.Count == 0) return;
        var s = _settings();
        foreach (var (k, x) in v) if (_gauges.TryGetValue(k, out var g)) g.Update(x);
        foreach (var t in _slots.SelectMany(sl => sl.Items).Select(i => i.Tpms).OfType<TpmsVm>()) t.Refresh();
        if (v.TryGetValue("mil", out var mil)) MilOn = mil > 0;
        if (v.TryGetValue("dtcCount", out var n)) DtcCount = (int)n;
        if (v.TryGetValue("coolant", out var ct)) Overheat = ct >= s.Dashboard.OverheatC;
        if (v.TryGetValue("voltage", out var vv) || v.TryGetValue("atrv", out vv)) LowVoltage = vv < s.Dashboard.LowVoltage;
        LowTire = TpmsKeys.Tires.Any(k => _gauges.TryGetValue(k, out var g) && g.IsLow);
        var gearItem = _engine?.Scheduler.Items.FirstOrDefault(i => i.Key == "gear");
        if (v.TryGetValue("gear", out var gr) && _enhanced.Pids.FirstOrDefault(p => p.Key == "gear") is { } gp && (gearItem == null || gearItem.Failures == 0))
        {
            Gear = gp.Label(gr);
            var src = UnverifiedLabels.GearFromDid(gp.Header, gp.Did);
            GearSource = src;
            if (_gauges.TryGetValue("gear", out var gg)) { gg.Note = src; gg.Subtitle = src; }
        }
        else if (s.Dashboard.EstimateGear && v.TryGetValue("speed", out var kmh) && v.TryGetValue("rpm", out var rpm)
                 && (_engine?.Enhanced.ContainsKey("gear") != true || _engine.Scheduler.Items.First(i => i.Key == "gear").Failures > 0))
        {
            var e = est.Estimate(rpm, kmh); Gear = e?.ToString() ?? (kmh < 5 ? "N/P" : "--");
            var src = UnverifiedLabels.GearFromEstimate();
            GearSource = src;
            if (_gauges.TryGetValue("gear", out var gg)) { gg.Note = src; gg.Subtitle = src; }
        }
        if (_enhanced.Pids.FirstOrDefault(p => p.Key == "oss") is { } oss && _gauges.TryGetValue("oss", out var og)
            && _engine?.AnsweredBy.TryGetValue("oss", out var ossTx) == true)
        {
            var src = UnverifiedLabels.OssSource(ossTx, oss.Did);
            og.Note = src; og.Subtitle = src;
        }
        if (_rec != null) _rec.Write(DateTime.Now, v);
    }

    private void ToggleRecord()
    {
        if (_rec != null) { var p = _rec.Path; _rec.Dispose(); _rec = null; _log.Message(LogEventLevel.Information, "Dashboard", $"Recording saved {p}"); }
        else
        {
            var cols = PolledLayouts().SelectMany(l => DashCatalog.PollKeys(l, true)).Concat(new[] { "atrv", "gear", "mil", "dtcCount" }).Distinct().ToList();
            _rec = new DashboardCsvRecorder(_logsDir, cols, DateTime.Now);
            _log.Message(LogEventLevel.Information, "Dashboard", $"Recording to {_rec.Path} (values in metric/base units)");
        }
        OnPropertyChanged(nameof(Recording)); OnPropertyChanged(nameof(RecordText));
    }
}

/// <summary>What a write card needs from the app: run code on a module under the bus lock (bus routing and session included), the safety guard, and module names.</summary>
public sealed record DashActionHost(Func<uint, Func<UdsClient, Task>, Task> OnModule, Func<SafetyGuard> Safety, Func<uint, string> ModuleName, Action<string, Exception> Err);

/// <summary>
/// One dashboard pop-out window (slot 1-3): its layout (shared pool, chosen per window), background, arrange width, placement,
/// Customize mode, layout file import/export and write cards. Live values, pause, Keep live, recording and the lamp row come
/// from the shared <see cref="DashboardPoller"/> and are re-exposed here so the window binds to one object.
/// </summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly DashboardPoller _poller; private readonly IEventLog _log; private readonly Func<AppSettings> _settings; private readonly Action _save;
    private readonly string _pidDir; private readonly DashActionHost? _host;
    private static readonly string[] Relayed =
    {
        nameof(StateText), nameof(Info), nameof(RateText), nameof(MilOn), nameof(DtcCount), nameof(LowVoltage), nameof(Overheat), nameof(LowTire),
        nameof(LinkOk), nameof(Recording), nameof(RecordText), nameof(Gear), nameof(GearSource), nameof(Paused), nameof(KeepLiveWhileClosed), nameof(Active),
    };

    public int Slot { get; }
    public string Title => Slot == 1 ? "Dashboard" : $"Dashboard {Slot}";
    public string WindowTitle => $"{Title} ({ActiveLayout}) - OBDLink Diag";
    public DashboardPoller Poller => _poller;
    public ObservableCollection<DashItemVm> Items { get; } = new();
    public ObservableCollection<CatalogEntry> Catalog { get; } = new();
    public ObservableCollection<string> LayoutNames { get; } = new();
    public RelayCommand PauseCommand => _poller.PauseCommand;
    public RelayCommand RecordCommand => _poller.RecordCommand;
    public RelayCommand ResetStatsCommand => _poller.ResetStatsCommand;
    public RelayCommand OpenPidTableCommand => _poller.OpenPidTableCommand;
    public RelayCommand CustomizeCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand ResetLayoutCommand { get; }
    public RelayCommand MoveLeftCommand { get; }
    public RelayCommand MoveRightCommand { get; }
    public RelayCommand SaveLayoutAsCommand { get; }
    public RelayCommand DeleteLayoutCommand { get; }
    public RelayCommand RenameLayoutCommand { get; }
    public RelayCommand ExportLayoutCommand { get; }
    public RelayCommand ImportLayoutCommand { get; }
    public RelayCommand AddActionCommand { get; }

    public DashboardViewModel(DashboardPoller poller, int slot, IEventLog log, Func<AppSettings> settings, string pidDir, Action save, DashActionHost? host = null)
    {
        _poller = poller; Slot = slot; _log = log; _settings = settings; _pidDir = pidDir; _save = save; _host = host;
        poller.Register(this);
        poller.PropertyChanged += (_, e) => { if (e.PropertyName is string n && Relayed.Contains(n)) OnPropertyChanged(n); };
        string L(string what) => $"Dashboard {Slot} {what}";
        CustomizeCommand = new(L("customize on/off"), log, _ => Customizing = !Customizing);
        MoveUpCommand = new(L("move item up"), log, p => Move(p as DashItemVm, -1));
        MoveDownCommand = new(L("move item down"), log, p => Move(p as DashItemVm, +1));
        MoveLeftCommand = new(L("move item left"), log, p => Move(p as DashItemVm, -1));
        MoveRightCommand = new(L("move item right"), log, p => Move(p as DashItemVm, +1));
        RemoveCommand = new(L("remove item"), log, p => { if (p is DashItemVm it && Layout.Remove(it.Key)) { _ = it.Action?.ReleaseAsync("card removed"); BuildGauges(); Changed("removed " + it.Key); } });
        AddCommand = new(L("add item"), log, p =>
        {
            if ((p as CatalogEntry ?? SelectedCatalog) is { } c && Layout.Add(c.Key, c.DefaultDisplay)) { BuildGauges(); Changed("added " + c.Key); }
        });
        ResetLayoutCommand = new(L("reset layout to default"), log, _ =>
        {
            var d = DashboardLayout.Default(); Layout.Items = d.Items; BuildGauges(); Changed("reset layout " + Layout.Name);
        });
        SaveLayoutAsCommand = new(L("save layout as"), log, _ =>
        {
            var name = (NewLayoutName ?? "").Trim(); if (name.Length == 0) { Hint = "Type a name for the new layout first."; return; }
            var d = _settings().Dashboard; var copy = Layout.Clone(name);
            d.Layouts.RemoveAll(l => l.Name == name); d.Layouts.Add(copy); d.SetLayout(Slot, name);
            NewLayoutName = ""; RefreshAllLayoutNames(); BuildGauges(); Changed("saved layout " + name);
        });
        DeleteLayoutCommand = new(L("delete layout"), log, _ =>
        {
            var d = _settings().Dashboard; if (d.Layouts.Count <= 1) { Hint = "This is the only layout; use Reset to go back to the default."; return; }
            var gone = Layout.Name;
            d.Layouts.Remove(Layout);
            foreach (var w in d.Windows.Where(w => w.Layout == gone)) d.SetLayout(w.Slot, d.Layouts[0].Name);
            RefreshAllLayoutNames(); foreach (var s in _poller.Slots) s.BuildGauges(); Changed("deleted layout " + gone);
        });
        RenameLayoutCommand = new(L("rename layout"), log, _ =>
        {
            var name = (NewLayoutName ?? "").Trim(); var d = _settings().Dashboard;
            if (name.Length == 0) { Hint = "Type the new name first."; return; }
            if (d.Layouts.Any(l => l.Name == name)) { Hint = $"A layout named '{name}' already exists."; return; }
            var old = Layout.Name; Layout.Name = name;
            foreach (var w in d.Windows.Where(w => w.Layout == old)) d.SetLayout(w.Slot, name);
            NewLayoutName = ""; RefreshAllLayoutNames(); Changed($"renamed layout {old} to {name}");
        });
        ExportLayoutCommand = new(L("export layout"), log, _ => ExportLayout());
        ImportLayoutCommand = new(L("import layout"), log, _ => ImportLayout());
        AddActionCommand = new(L("add write card"), log, _ => AddAction());
        PickBackgroundCommand = new(L("pick background image"), log, _ => PickBackground());
        RefreshLayoutNames();
        BuildGauges();
    }

    // ---- shared state, re-exposed for the window ----
    public string StateText => _poller.StateText;
    public string Info => _poller.Info;
    public string RateText => _poller.RateText;
    public bool MilOn => _poller.MilOn;
    public int DtcCount => _poller.DtcCount;
    public bool LowVoltage => _poller.LowVoltage;
    public bool Overheat => _poller.Overheat;
    public bool LowTire => _poller.LowTire;
    public bool LinkOk => _poller.LinkOk;
    public bool Recording => _poller.Recording;
    public string RecordText => _poller.RecordText;
    public string Gear => _poller.Gear;
    public string GearSource => _poller.GearSource;
    public bool Active => _poller.Active;
    public bool Paused { get => _poller.Paused; set => _poller.Paused = value; }
    public bool KeepLiveWhileClosed { get => _poller.KeepLiveWhileClosed; set => _poller.KeepLiveWhileClosed = value; }
    public void ApplyUnits() => _poller.ApplyUnits();
    public void ApplyThresholds() => _poller.ApplyThresholds();

    // ---- per-window background ----
    private DashboardWindowState Win => _settings().Dashboard.Window(Slot);
    public RelayCommand PickBackgroundCommand { get; }
    public IReadOnlyList<DashboardBackgroundChoice> BackgroundChoices => DashboardBackgrounds.Choices;
    public string BackgroundId
    {
        get => Win.Background;
        set
        {
            var id = DashboardBackgrounds.Normalize(value);
            var w = Win;
            if (id == DashboardBackgrounds.Custom && string.IsNullOrEmpty(w.BackgroundFile))
            {
                OnPropertyChanged();
                PickBackground();
                return;
            }
            if (id == w.Background) return;
            w.Background = id;
            if (Slot == 1) _settings().Dashboard.Background = id; // older builds read the single-window field
            _save();
            NotifyBackground();
            _log.UserAction($"Dashboard {Slot} background", ("id", id));
        }
    }
    public string? BackgroundFile => Win.BackgroundFile;
    public string? BackgroundFullPath => DashboardBackgroundFiles.FullPath(_pidDir, BackgroundFile);
    private double _hostMax = double.PositiveInfinity;
    public double HostMaxWidth { get => _hostMax; private set => Set(ref _hostMax, value); }
    private double _viewport;
    private bool _stacking;
    /// <summary>View-time layout. Does not rewrite the saved item list. Pins the wrap to the width last used in Customize when the window is at least that wide.</summary>
    public void ApplyViewport(double availableWidth)
    {
        if (availableWidth > 1) _viewport = availableWidth;
        var plan = DashboardFlow.Plan(Items.Select(i => (i.Display, i.Size)).ToList(), _viewport, Win.ArrangeWidth, _stacking);
        if (availableWidth > 1) _stacking = plan.Stacking;
        for (int i = 0; i < Items.Count && i < plan.Widths.Length; i++) Items[i].FlowWidth = plan.Widths[i];
        HostMaxWidth = plan.HostMaxWidth;
    }
    private void NotifyBackground()
    {
        OnPropertyChanged(nameof(BackgroundId));
        OnPropertyChanged(nameof(BackgroundFile));
        OnPropertyChanged(nameof(BackgroundFullPath));
    }
    private void PickBackground()
    {
        var dir = ObdLink.Core.Storage.AppPaths.Ensure(System.IO.Path.Combine(_pidDir, DashboardBackgroundFiles.Folder));
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif", InitialDirectory = dir, Title = $"{Title} background image" };
        if (DialogService.ShowFileDialog(dlg) != true || string.IsNullOrEmpty(dlg.FileName)) return;
        var name = DashboardBackgroundFiles.Import(_pidDir, dlg.FileName);
        if (name is null)
        {
            DialogService.Info("Choose a PNG, JPEG, BMP or GIF image, 15 MB or smaller. The copy is stored in Data\\backgrounds.", "Dashboard background");
            OnPropertyChanged(nameof(BackgroundId));
            return;
        }
        var w = Win;
        w.BackgroundFile = name;
        w.Background = DashboardBackgrounds.Custom;
        if (Slot == 1) { _settings().Dashboard.BackgroundFile = name; _settings().Dashboard.Background = DashboardBackgrounds.Custom; }
        _save();
        NotifyBackground();
        _log.UserAction($"Dashboard {Slot} background image", ("file", name));
    }

    // ---- layout ----
    internal DashboardLayout Layout => _settings().Dashboard.LayoutFor(Slot);
    private bool _customizing; public bool Customizing
    {
        get => _customizing;
        set
        {
            if (!Set(ref _customizing, value)) return;
            if (value) RefreshCatalog();
            else if (_viewport > 1) { Win.ArrangeWidth = _viewport; _save(); }
            OnPropertyChanged(nameof(CustomizeText));
        }
    }
    public string CustomizeText => Customizing ? "Done" : "Customize";
    private CatalogEntry? _selCat; public CatalogEntry? SelectedCatalog { get => _selCat; set => Set(ref _selCat, value); }
    private string? _newName; public string? NewLayoutName { get => _newName; set => Set(ref _newName, value); }
    private string _hint = ""; public string Hint { get => _hint; set => Set(ref _hint, value); }
    public string ActiveLayout
    {
        get => Win.Layout;
        set
        {
            if (value is null || value == ActiveLayout) return;
            _settings().Dashboard.SetLayout(Slot, value);
            BuildGauges(); Changed("active layout " + value);
            OnPropertyChanged(); OnPropertyChanged(nameof(WindowTitle));
        }
    }
    private void RefreshLayoutNames()
    {
        LayoutNames.Clear(); foreach (var l in _settings().Dashboard.Layouts) LayoutNames.Add(l.Name);
        OnPropertyChanged(nameof(ActiveLayout)); OnPropertyChanged(nameof(WindowTitle));
    }
    private void RefreshAllLayoutNames() { foreach (var s in _poller.Slots) s.RefreshLayoutNames(); }

    /// <summary>Called after any layout edit: persists settings (including the width the user arranged at) and makes the poller pick up the new item list.</summary>
    internal void Changed(string what)
    {
        RememberArrangeWidth();
        _save(); _poller.LayoutChanged(this, Layout.Name); Hint = ""; _log.UserAction($"Dashboard {Slot} layout: " + what);
        if (Customizing) RefreshCatalog();
        ApplyViewport(_viewport);
    }
    private void RememberArrangeWidth()
    {
        if (_viewport > 1) Win.ArrangeWidth = _viewport;
    }
    private void Move(DashItemVm? it, int delta) => MoveBy(it, delta);
    /// <summary>Keyboard / button move. No-op while Customize is off, so the saved layout stays put.</summary>
    public bool MoveBy(DashItemVm? it, int delta)
    {
        if (it is null) return false;
        var i = Items.IndexOf(it);
        var to = DashReorder.Move(Layout, i, delta, Customizing);
        if (to < 0) return false;
        Items.Move(i, to);
        Changed($"moved {it.Key} to position {to + 1}");
        return true;
    }
    /// <summary>Drag-and-drop reorder by item key. The view-model object is not an OLE-safe drag payload.</summary>
    public bool MoveItemByKey(string? srcKey, string? dstKey)
    {
        if (!Customizing) return false;
        int a = IndexOfKey(srcKey), b = IndexOfKey(dstKey);
        if (a < 0 || b < 0 || a == b) return false;
        if (!DashReorder.Drop(Layout, srcKey, dstKey, true)) return false;
        Items.Move(a, b);
        Changed($"moved {srcKey} to position {b + 1}");
        return true;
    }
    private int IndexOfKey(string? key)
    {
        if (key is null) return -1;
        for (int i = 0; i < Items.Count; i++) if (Items[i].Key == key) return i;
        return -1;
    }
    private void RefreshCatalog()
    {
        Catalog.Clear();
        foreach (var c in DashCatalog.Build(_poller.Enhanced, _poller.Supported).Where(c => Layout.Items.All(i => i.Key != c.Key))) Catalog.Add(c);
        SelectedCatalog = Catalog.FirstOrDefault();
    }

    /// <summary>Gauges shown in this window (the TPMS widget counts as its four tires). Write cards are not gauges.</summary>
    internal IEnumerable<GaugeVm> ShownGauges => Items.Where(i => i.Action is null).SelectMany(i => i.Tpms?.All ?? new[] { i.Gauge });

    /// <summary>(Re)builds the visible items from this window's layout. Gauges are shared, so min/max stats survive.</summary>
    public void BuildGauges()
    {
        _poller.ReloadEnhanced();
        var s = _settings();
        var catalog = DashCatalog.Build(_poller.Enhanced).ToDictionary(c => c.Key);
        foreach (var old in Items) if (old.Action is { } card && Layout.Items.All(i => i.Key != old.Key)) _ = card.ReleaseAsync("card no longer on the layout");
        var keep = Items.Where(i => i.Action is not null).ToDictionary(i => i.Key, i => i.Action!);
        Items.Clear();
        foreach (var it in Layout.Items)
        {
            if (it.Display == DisplayKind.Action || it.Action is not null)
            {
                var card = keep.TryGetValue(it.Key, out var existing) && ReferenceEquals(existing.Item, it) ? existing : new DashActionCardVm(it, _host, _log);
                var holder = new GaugeVm { Key = it.Key, Title = card.Label, Kind = GaugeKind.Tile, Note = card.Description, Units = s.Units };
                Items.Add(new DashItemVm(this, it, holder, null) { Action = card });
                continue;
            }
            catalog.TryGetValue(it.Key, out var entry);
            if (entry != null && !entry.Displays.Contains(it.Display)) it.Display = entry.DefaultDisplay;
            if (it.Key == TpmsKeys.Widget)
            {
                var t = new TpmsVm { LF = _poller.Gauge("tpms_lf"), RF = _poller.Gauge("tpms_rf"), RR = _poller.Gauge("tpms_rr"), LR = _poller.Gauge("tpms_lr") };
                var verified = entry?.Verified == true;
                var holder = new GaugeVm
                {
                    Key = "tpms", Title = verified ? "Tire pressures" : "Tire pressures (UNVERIFIED)", Kind = GaugeKind.Tile,
                    Note = verified ? "BCM 726 DIDs 2813-2816 (verified)" : "BCM 726 DIDs 2813-2816 (UNVERIFIED)", Units = s.Units,
                };
                Items.Add(new DashItemVm(this, it, holder, entry) { Tpms = t });
            }
            else Items.Add(new DashItemVm(this, it, _poller.Gauge(it.Key), entry));
        }
        _poller.Reconfigure();
        ApplyViewport(_viewport);
    }

    // ---- layout files ----
    private void ExportLayout()
    {
        var dir = ObdLink.Core.Storage.AppPaths.Ensure(System.IO.Path.Combine(_pidDir, "exports"));
        var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Dashboard layout (*.dashboard.json)|*.dashboard.json", InitialDirectory = dir, FileName = ObdLink.Core.Security.SafePaths.Token(Layout.Name) + ".dashboard.json", Title = "Export dashboard layout" };
        if (DialogService.ShowFileDialog(dlg) != true || string.IsNullOrEmpty(dlg.FileName)) return;
        System.IO.File.WriteAllText(dlg.FileName, DashboardLayoutFile.Export(Layout));
        Hint = $"Exported '{Layout.Name}' to {System.IO.Path.GetFileName(dlg.FileName)}.";
        _log.UserAction($"Dashboard {Slot} exported layout", ("layout", Layout.Name), ("file", System.IO.Path.GetFileName(dlg.FileName)));
    }

    private void ImportLayout()
    {
        var dir = ObdLink.Core.Storage.AppPaths.Ensure(System.IO.Path.Combine(_pidDir, "exports"));
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Dashboard layout (*.dashboard.json;*.json)|*.dashboard.json;*.json", InitialDirectory = dir, Title = "Import dashboard layout" };
        if (DialogService.ShowFileDialog(dlg) != true || string.IsNullOrEmpty(dlg.FileName)) return;
        var info = new System.IO.FileInfo(dlg.FileName);
        if (info.Length > 1_000_000) { Hint = "That file is larger than 1 MB; a layout file is a few KB."; return; }
        DashboardLayout l;
        try { l = DashboardLayoutFile.Import(System.IO.File.ReadAllText(dlg.FileName), _settings().Dashboard.Layouts.Select(x => x.Name)); }
        catch (FormatException ex) { Hint = ex.Message; return; }
        var d = _settings().Dashboard;
        d.Layouts.Add(l); d.SetLayout(Slot, l.Name);
        RefreshAllLayoutNames(); BuildGauges();
        int cards = l.Items.Count(i => i.Action is not null);
        Changed($"imported layout {l.Name} ({l.Items.Count} items, {cards} write card(s))");
        Hint = $"Imported '{l.Name}'." + (cards > 0 ? $" It has {cards} write card(s): check what each one sends before using it." : "");
    }

    // ---- write cards ----
    public DashActionKind[] ActionKinds { get; } = Enum.GetValues<DashActionKind>();
    private DashActionKind _newKind = DashActionKind.Routine; public DashActionKind NewActionKind { get => _newKind; set => Set(ref _newKind, value); }
    private string _newModule = "7E0"; public string NewActionModule { get => _newModule; set => Set(ref _newModule, value); }
    private string _newId = ""; public string NewActionId { get => _newId; set => Set(ref _newId, value); }
    private string _newData = ""; public string NewActionData { get => _newData; set => Set(ref _newData, value); }
    private string _newLabel = ""; public string NewActionLabel { get => _newLabel; set => Set(ref _newLabel, value); }

    private void AddAction()
    {
        var a = new DashAction { Kind = NewActionKind, Module = NewActionModule, Id = NewActionId, Data = NewActionData, Label = NewActionLabel.Trim() };
        try
        {
            var plan = DashActions.Plan(a);
            if (a.Label.Length == 0) a.Label = plan.Summary;
            Layout.AddAction(a);
        }
        catch (Exception ex) when (ex is FormatException or RoutineRefusedException) { Hint = ex.Message; return; }
        NewActionId = ""; NewActionData = ""; NewActionLabel = "";
        BuildGauges(); Changed($"added write card {a.Kind} {a.Module} {a.Id}");
    }

    // ---- window ----
    public bool WindowOpen { get; private set; }

    /// <summary>Called when this window opens or closes. Closing releases any output this window holds.</summary>
    public void SetWindowOpen(bool open)
    {
        WindowOpen = open;
        if (!open) foreach (var c in Items.Select(i => i.Action).OfType<DashActionCardVm>()) _ = c.ReleaseAsync("dashboard window closed");
        _poller.WindowChanged(open);
        OnPropertyChanged(nameof(StateText));
    }

    public DashboardWindowPlacement.PlacementRequest SavedPlacement
    {
        get
        {
            var w = Win;
            return new(w.WindowPlacementSaved, w.WindowLeft, w.WindowTop, w.WindowWidth, w.WindowHeight, w.WindowMaximized);
        }
    }

    public void CapturePlacement(double left, double top, double width, double height, bool maximized)
    {
        var w = Win;
        w.WindowLeft = left;
        w.WindowTop = top;
        w.WindowWidth = width;
        w.WindowHeight = height;
        w.WindowMaximized = maximized;
        w.WindowPlacementSaved = true;
        _save();
    }
}
