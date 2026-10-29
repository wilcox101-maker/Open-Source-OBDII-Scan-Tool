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
    public bool HasValue => !double.IsNaN(_base);
    public string StatsText => Stats.Count == 0 ? "min -- · max -- · avg --" : $"min {Fmt(Conv(Stats.Min))} · max {Fmt(Conv(Stats.Max))} · avg {Fmt(Conv(Stats.Avg))}";
    public bool IsLow => !double.IsNaN(_base) && BaseWarnLow is { } lo && _base < lo;
    public bool IsWarn => !double.IsNaN(_base) && Thresholds.Evaluate(_base, BaseWarnLow, BaseWarnHigh) == Level.Warn;
    private double Conv(double v) => double.IsNaN(v) ? v : ObdLink.Core.Obd.Units.ConvertOnly(v, Quantity, Units);
    private string Fmt(double v) => Quantity is Quantity.Rpm or Quantity.Speed ? v.ToString("0") : Math.Abs(v) < 100 ? v.ToString("0.0") : v.ToString("0");

    public void Update(double baseValue) { _base = baseValue; Stats.Add(baseValue); Refresh(); }
    public void Refresh()
    {
        foreach (var p in new[] { nameof(Value), nameof(ValueText), nameof(HasValue), nameof(IsLow), nameof(StatsText), nameof(TileFooter), nameof(Subtitle), nameof(HasSubtitle), nameof(Note), nameof(IsWarn), nameof(Unit), nameof(Minimum), nameof(Maximum), nameof(WarnHigh), nameof(WarnLow) }) OnPropertyChanged(p);
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
    }
    private double _flowWidth;
    /// <summary>Pre-transform card width. Saved size, or the stacked width when the window is narrower than the widest card.</summary>
    public double FlowWidth { get => _flowWidth; internal set => Set(ref _flowWidth, value); }
    public string Key => Item.Key;
    public string Title => Gauge.Title;
    public DisplayKind[] Displays => Entry?.Displays ?? new[] { Item.Display };
    public DisplayKind Display { get => Item.Display; set { if (Item.Display == value) return; Item.Display = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLarge)); _owner.Changed("display " + Key + " = " + value); } }
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
/// Read-only live dashboard driven by a saved layout (Data\settings.json, Dashboard.Layouts). Polls ONLY the displayed items
/// while the dashboard window is open (or Keep live is on), the adapter is connected, and the user pause flag is off,
/// and only when no other command holds the bus lock. Sends services 01 and 22 only.
/// </summary>
public sealed class DashboardViewModel : ObservableObject
{
    private readonly IEventLog _log; private readonly Func<AppSettings> _settings; private readonly Func<ElmDiagChannel?> _ch; private readonly Action _save;
    private readonly Func<Task<double?>> _readAtrv; private readonly SemaphoreSlim _bus; private readonly string _logsDir; private readonly string _pidDir;
    private CancellationTokenSource? _cts; private DashboardEngine? _engine; private DashboardCsvRecorder? _rec; private volatile bool _reconfigure;
    private bool _connected; private bool _loopFailed; private bool _stopping;
    private EnhancedPidTable _enhanced = new(); private readonly GearEstimator _gearDefault = new();
    private readonly Dictionary<string, GaugeVm> _gauges = new();

    public ObservableCollection<DashItemVm> Items { get; } = new();
    public ObservableCollection<CatalogEntry> Catalog { get; } = new();
    public ObservableCollection<string> LayoutNames { get; } = new();
    public RelayCommand PauseCommand { get; }
    public RelayCommand RecordCommand { get; }
    public RelayCommand ResetStatsCommand { get; }
    public RelayCommand OpenPidTableCommand { get; }
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

    public DashboardViewModel(IEventLog log, Func<AppSettings> settings, Func<ElmDiagChannel?> ch, Func<Task<double?>> readAtrv, SemaphoreSlim bus, string logsDir, string pidDir, Action save)
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
        CustomizeCommand = new("Dashboard customize on/off", log, _ => Customizing = !Customizing);
        MoveUpCommand = new("Dashboard move item up", log, p => Move(p as DashItemVm, -1));
        MoveDownCommand = new("Dashboard move item down", log, p => Move(p as DashItemVm, +1));
        MoveLeftCommand = new("Dashboard move item left", log, p => Move(p as DashItemVm, -1));
        MoveRightCommand = new("Dashboard move item right", log, p => Move(p as DashItemVm, +1));
        RemoveCommand = new("Dashboard remove item", log, p => { if (p is DashItemVm it && Layout.Remove(it.Key)) { BuildGauges(); Changed("removed " + it.Key); } });
        AddCommand = new("Dashboard add item", log, p =>
        {
            if ((p as CatalogEntry ?? SelectedCatalog) is { } c && Layout.Add(c.Key, c.DefaultDisplay)) { BuildGauges(); Changed("added " + c.Key); }
        });
        ResetLayoutCommand = new("Dashboard reset layout to default", log, _ =>
        {
            var d = DashboardLayout.Default(); Layout.Items = d.Items; BuildGauges(); Changed("reset layout " + Layout.Name);
        });
        SaveLayoutAsCommand = new("Dashboard save layout as", log, _ =>
        {
            var name = (NewLayoutName ?? "").Trim(); if (name.Length == 0) { Hint = "Type a name for the new layout first."; return; }
            var d = _settings().Dashboard; d.Layouts.RemoveAll(l => l.Name == name); d.Layouts.Add(Layout.Clone(name)); d.ActiveLayout = name;
            NewLayoutName = ""; RefreshLayoutNames(); BuildGauges(); Changed("saved layout " + name);
        });
        DeleteLayoutCommand = new("Dashboard delete layout", log, _ =>
        {
            var d = _settings().Dashboard; if (d.Layouts.Count <= 1) { Hint = "This is the only layout; use Reset to go back to the default."; return; }
            d.Layouts.Remove(Layout); d.ActiveLayout = d.Layouts[0].Name; RefreshLayoutNames(); BuildGauges(); Changed("deleted layout");
        });
        PickBackgroundCommand = new("Dashboard pick background image", log, _ => PickBackground());
        RefreshLayoutNames();
        BuildGauges();
    }

    public RelayCommand PickBackgroundCommand { get; }
    public IReadOnlyList<DashboardBackgroundChoice> BackgroundChoices => DashboardBackgrounds.Choices;
    public string BackgroundId
    {
        get => _settings().Dashboard.Background;
        set
        {
            var id = DashboardBackgrounds.Normalize(value);
            var d = _settings().Dashboard;
            if (id == DashboardBackgrounds.Custom && string.IsNullOrEmpty(d.BackgroundFile))
            {
                OnPropertyChanged();
                PickBackground();
                return;
            }
            if (id == d.Background) return;
            d.Background = id;
            _save();
            NotifyBackground();
            _log.UserAction("Dashboard background", ("id", id));
        }
    }
    public string? BackgroundFile => _settings().Dashboard.BackgroundFile;
    public string? BackgroundFullPath => DashboardBackgroundFiles.FullPath(_pidDir, BackgroundFile);
    private double _hostMax = double.PositiveInfinity;
    public double HostMaxWidth { get => _hostMax; private set => Set(ref _hostMax, value); }
    private double _viewport;
    private bool _stacking;
    /// <summary>View-time layout. Does not rewrite the saved item list. Pins the wrap to the width last used in Customize when the window is at least that wide.</summary>
    public void ApplyViewport(double availableWidth)
    {
        if (availableWidth > 1) _viewport = availableWidth;
        var plan = DashboardFlow.Plan(Items.Select(i => (i.Display, i.Size)).ToList(), _viewport, _settings().Dashboard.ArrangeWidth, _stacking);
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
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif", InitialDirectory = dir, Title = "Dashboard background image" };
        if (DialogService.ShowFileDialog(dlg) != true || string.IsNullOrEmpty(dlg.FileName)) return;
        var name = DashboardBackgroundFiles.Import(_pidDir, dlg.FileName);
        if (name is null)
        {
            DialogService.Info("Choose a PNG, JPEG, BMP or GIF image, 15 MB or smaller. The copy is stored in Data\\backgrounds.", "Dashboard background");
            OnPropertyChanged(nameof(BackgroundId));
            return;
        }
        var d = _settings().Dashboard;
        d.BackgroundFile = name;
        d.Background = DashboardBackgrounds.Custom;
        _save();
        NotifyBackground();
        _log.UserAction("Dashboard background image", ("file", name));
    }

    private DashboardLayout Layout => _settings().Dashboard.Current();
    private bool _customizing; public bool Customizing
    {
        get => _customizing;
        set
        {
            if (!Set(ref _customizing, value)) return;
            if (value) RefreshCatalog();
            else if (_viewport > 1) { _settings().Dashboard.ArrangeWidth = _viewport; _save(); }
            OnPropertyChanged(nameof(CustomizeText));
        }
    }
    public string CustomizeText => Customizing ? "Done" : "Customize";
    private CatalogEntry? _selCat; public CatalogEntry? SelectedCatalog { get => _selCat; set => Set(ref _selCat, value); }
    private string? _newName; public string? NewLayoutName { get => _newName; set => Set(ref _newName, value); }
    private string _hint = ""; public string Hint { get => _hint; set => Set(ref _hint, value); }
    public string ActiveLayout
    {
        get => _settings().Dashboard.ActiveLayout;
        set { if (value is null || value == ActiveLayout) return; _settings().Dashboard.ActiveLayout = value; BuildGauges(); Changed("active layout " + value); OnPropertyChanged(); }
    }
    private void RefreshLayoutNames() { LayoutNames.Clear(); foreach (var l in _settings().Dashboard.Layouts) LayoutNames.Add(l.Name); OnPropertyChanged(nameof(ActiveLayout)); }

    /// <summary>Called after any layout edit: persists settings (including the width the user arranged at) and makes the poller pick up the new item list.</summary>
    internal void Changed(string what)
    {
        RememberArrangeWidth();
        _save(); _reconfigure = true; Hint = ""; _log.UserAction("Dashboard layout: " + what);
        if (Customizing) RefreshCatalog();
        ApplyViewport(_viewport);
    }
    private void RememberArrangeWidth()
    {
        if (_viewport > 1) _settings().Dashboard.ArrangeWidth = _viewport;
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
        var sup = _engine?.Supported is { Count: > 0 } s ? s : null;
        foreach (var c in DashCatalog.Build(_enhanced, sup).Where(c => Layout.Items.All(i => i.Key != c.Key))) Catalog.Add(c);
        SelectedCatalog = Catalog.FirstOrDefault();
    }

    private GaugeVm MakeGauge(string key, AppSettings s)
    {
        if (_gauges.TryGetValue(key, out var have)) return have;
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

    /// <summary>(Re)builds the visible items from the active layout. Keeps min/max stats of gauges that stay.</summary>
    public void BuildGauges()
    {
        var s = _settings();
        (_enhanced, _, var err) = EnhancedPidTable.LoadOrCreate(_pidDir);
        if (err != null) _log.Message(LogEventLevel.Warning, "Dashboard", $"Ford enhanced PID table unreadable ({err}); using built-in UNVERIFIED defaults");
        var catalog = DashCatalog.Build(_enhanced).ToDictionary(c => c.Key);
        Items.Clear();
        foreach (var it in Layout.Items)
        {
            catalog.TryGetValue(it.Key, out var entry);
            if (entry != null && !entry.Displays.Contains(it.Display)) it.Display = entry.DefaultDisplay;
            if (it.Key == TpmsKeys.Widget)
            {
                var t = new TpmsVm { LF = MakeGauge("tpms_lf", s), RF = MakeGauge("tpms_rf", s), RR = MakeGauge("tpms_rr", s), LR = MakeGauge("tpms_lr", s) };
                var verified = entry?.Verified == true;
                var holder = new GaugeVm
                {
                    Key = "tpms", Title = verified ? "Tire pressures" : "Tire pressures (UNVERIFIED)", Kind = GaugeKind.Tile,
                    Note = verified ? "BCM 726 DIDs 2813-2816 (verified)" : "BCM 726 DIDs 2813-2816 (UNVERIFIED)", Units = s.Units,
                };
                Items.Add(new DashItemVm(this, it, holder, entry) { Tpms = t });
            }
            else Items.Add(new DashItemVm(this, it, MakeGauge(it.Key, s), entry));
        }
        _reconfigure = true;
        ApplyViewport(_viewport);
    }
    private IEnumerable<GaugeVm> Shown => Items.SelectMany(i => i.Tpms?.All ?? new[] { i.Gauge });
    public void ApplyUnits() { var u = _settings().Units; foreach (var g in _gauges.Values) { g.Units = u; g.Refresh(); } }
    /// <summary>Threshold changes from the Settings page (TPMS low, overheat, low voltage).</summary>
    public void ApplyThresholds() { _gauges.Clear(); BuildGauges(); }

    private bool _paused; public bool Paused { get => _paused; set { if (Set(ref _paused, value)) { OnPropertyChanged(nameof(StateText)); _log.UserAction("Dashboard " + (value ? "paused" : "resumed")); } } }
    private bool _active; public bool Active { get => _active; private set { if (Set(ref _active, value)) OnPropertyChanged(nameof(StateText)); } }
    public bool WindowOpen { get; private set; }
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
            Update(WindowOpen, _connected);
        }
    }
    private string _info = "Connect (a DEMO port works), then open the dashboard window."; public string Info { get => _info; private set => Set(ref _info, value); }
    public string StateText => DashboardSession.StateText(_connected, WindowOpen, KeepLiveWhileClosed, Paused, Engaged, _loopFailed);
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

    /// <summary>Called when the dashboard window opens or closes, and whenever the connection changes.</summary>
    public void SetWindowOpen(bool open)
    {
        WindowOpen = open;
        if (open) Engaged = true;
        Update(open, _connected);
    }

    /// <summary>Process exit. Cancels polling even when Keep live is on, and closes an open CSV recorder.</summary>
    public void Stop()
    {
        _stopping = true;
        if (_cts != null) { _cts.Cancel(); _cts = null; }
        if (_rec != null) { _rec.Dispose(); _rec = null; OnPropertyChanged(nameof(Recording)); OnPropertyChanged(nameof(RecordText)); }
    }

    public void Update(bool windowOpen, bool connected)
    {
        if (_stopping) return;
        WindowOpen = windowOpen;
        _connected = connected;
        LinkOk = connected;
        var run = DashboardSession.ShouldRun(connected, windowOpen, KeepLiveWhileClosed);
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

    private void Configure(AppSettings s)
    {
        var keys = DashCatalog.PollKeys(Layout, s.Dashboard.EstimateGear);
        if (keys.Contains("voltage") || keys.Contains("atrv")) { } // atrv is read separately below
        var enh = s.Dashboard.FordEnhancedPids ? _enhanced.Pids.Where(p => keys.Contains(p.Key)) : Enumerable.Empty<EnhancedPid>();
        _engine!.Configure(keys, enh);
        foreach (var g in Shown)
            g.Unsupported = g.Key is not ("gear" or "atrv" or "mil" or "dtcCount" or "tpms") && _engine.Scheduler.Items.All(i => i.Key != g.Key);
        Info = $"{_engine.Supported.Count} Mode 01 PIDs supported; polling {_engine.Scheduler.Items.Count} displayed items (read-only: services 01 and 22).";
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
            _engine = new DashboardEngine(ch);
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
                if (!await _bus.WaitAsync(0, ct)) continue; // another command is using the adapter
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
        catch (OperationCanceledException) { /* normal stop (window closed or disconnected) */ }
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
        foreach (var t in Items.Select(i => i.Tpms).OfType<TpmsVm>()) t.Refresh();
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
            var cols = DashCatalog.PollKeys(Layout, true).Concat(new[] { "atrv", "gear", "mil", "dtcCount" }).Distinct().ToList();
            _rec = new DashboardCsvRecorder(_logsDir, cols, DateTime.Now);
            _log.Message(LogEventLevel.Information, "Dashboard", $"Recording to {_rec.Path} (values in metric/base units)");
        }
        OnPropertyChanged(nameof(Recording)); OnPropertyChanged(nameof(RecordText));
    }
}
