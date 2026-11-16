using System.IO;
using System.Collections.ObjectModel;
using ObdLink.Core.Dashboard;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using ObdLink.Core.LiveData;
using ObdLink.Core.Logging;
using ObdLink.Core.Obd;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// v0.10.0 Phase 2: the per-signal live-data browser. Pick any supported OBD-II PID or Ford enhanced DID and watch it live,
/// with running min / max / average and optional CSV logging. Read-only: it drives the same <see cref="DashboardEngine"/>
/// as the dashboard, which sends only services 01 and 22. Rows are built on the UI thread before polling starts; the loop
/// only updates their values, so no collection is mutated off-thread (same approach as the dashboard).
/// </summary>
public sealed class LiveDataViewModel : ObservableObject
{
    private readonly IEventLog _log; private readonly Func<AppSettings> _settings; private readonly Func<ElmDiagChannel?> _ch;
    private readonly SemaphoreSlim _bus; private readonly string _logsDir; private readonly string _pidDir;
    private readonly LiveReadings _readings = new();
    private DashboardEngine? _engine; private CancellationTokenSource? _cts; private DashboardCsvRecorder? _rec;
    private readonly TriggerMonitor _triggers = new();
    private readonly List<DateTime> _markers = new();
    private ReplayCursor? _replay; private LiveCsvLog? _replayLog; private System.Windows.Threading.DispatcherTimer? _replayTimer;
    private DateTime _replayTick;

    /// <summary>v0.12.0: every sample of every polled or replayed signal, for the chart.</summary>
    public SignalHistory History { get; } = new();
    public ObservableCollection<string> TriggerHits { get; } = new();
    public ObservableCollection<SignalTrigger> TriggerList { get; } = new();
    private IReadOnlyList<ChartSeries> _plotted = Array.Empty<ChartSeries>();
    public IReadOnlyList<ChartSeries> Plotted { get => _plotted; private set => Set(ref _plotted, value); }
    public IReadOnlyList<DateTime> Markers => _markers.ToList();
    private int _revision; public int Revision { get => _revision; private set => Set(ref _revision, value); }
    public double[] WindowChoices { get; } = { 10, 30, 60, 300, 900 };
    private double _window = 60; public double ChartWindowSeconds { get => _window; set { if (Set(ref _window, value)) Revision++; } }
    private DateTime? _chartEnd; public DateTime? ChartEnd { get => _chartEnd; private set => Set(ref _chartEnd, value); }
    private string _triggerText = ""; public string TriggerText { get => _triggerText; set => Set(ref _triggerText, value); }
    public double[] SpeedChoices { get; } = { 0.5, 1, 2, 4, 8, 16 };
    private double _speed = 1; public double ReplaySpeed { get => _speed; set { if (Set(ref _speed, value) && _replay != null) _replay.Speed = value; } }
    public bool Replaying => _replayTimer?.IsEnabled == true;
    public bool HasReplay => _replay != null;
    public string ReplayText => _replay is null ? "" : $"Replay {_replayLog!.Duration.TotalSeconds * _replay.Fraction:0}/{_replayLog.Duration.TotalSeconds:0} s{(Replaying ? "" : " (paused)")}";

    public ObservableCollection<SignalPick> Available { get; } = new();
    public ObservableCollection<LiveRowVm> Rows { get; } = new();

    public AsyncCommand DiscoverCommand { get; }
    public AsyncCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RecordCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }
    public RelayCommand AddTriggerCommand { get; }
    public RelayCommand ClearTriggersCommand { get; }
    public RelayCommand OpenReplayCommand { get; }
    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand CloseReplayCommand { get; }
    public RelayCommand ClearChartCommand { get; }

    public LiveDataViewModel(IEventLog log, Func<AppSettings> settings, Func<ElmDiagChannel?> ch, SemaphoreSlim bus, string logsDir, string pidDir)
    {
        _log = log; _settings = settings; _ch = ch; _bus = bus; _logsDir = logsDir; _pidDir = pidDir;
        DiscoverCommand = new AsyncCommand("Live data: list signals", log, DiscoverAsync, Err);
        StartCommand = new AsyncCommand("Live data: start", log, StartAsync, Err);
        StopCommand = new RelayCommand("Live data: stop", log, _ => Stop());
        RecordCommand = new RelayCommand("Live data: record CSV", log, _ => ToggleRecord());
        ResetCommand = new RelayCommand("Live data: reset min/max", log, _ => { _readings.Reset(); foreach (var r in Rows) r.Refresh(); });
        SelectAllCommand = new RelayCommand("Live data: select all", log, _ => { foreach (var a in Available) a.Selected = true; });
        SelectNoneCommand = new RelayCommand("Live data: select none", log, _ => { foreach (var a in Available) a.Selected = false; });
        AddTriggerCommand = new RelayCommand("Live data: add trigger", log, _ => AddTrigger());
        ClearTriggersCommand = new RelayCommand("Live data: clear triggers", log, _ => { _triggers.Clear(); TriggerList.Clear(); TriggerHits.Clear(); _markers.Clear(); OnPropertyChanged(nameof(Markers)); Revision++; });
        OpenReplayCommand = new RelayCommand("Live data: open recording", log, _ => OpenReplay());
        PlayPauseCommand = new RelayCommand("Live data: play / pause replay", log, _ => PlayPause());
        CloseReplayCommand = new RelayCommand("Live data: close replay", log, _ => CloseReplay());
        ClearChartCommand = new RelayCommand("Live data: clear chart", log, _ => { History.Clear(); _markers.Clear(); OnPropertyChanged(nameof(Markers)); Revision++; });
    }

    private void Err(string what, Exception ex) { Info = what + ": " + ex.Message; _log.Message(LogEventLevel.Warning, "Live data", what, ex); }

    private string _info = "Connect, then click “List signals” to see what this vehicle supports.";
    public string Info { get => _info; private set => Set(ref _info, value); }
    private bool _active; public bool Active { get => _active; private set { Set(ref _active, value); OnPropertyChanged(nameof(CanEditSelection)); } }
    public bool CanEditSelection => !_active;
    public bool Recording => _rec != null;
    public string RecordText => _rec is null ? "Record CSV" : "Stop recording";

    private List<LiveSignal> _catalog = new();

    /// <summary>Discovers supported Mode 01 PIDs and loads the enhanced DID table, then builds the selectable list.</summary>
    /// <summary>Set by the main view model: a reason adapter commands are refused right now (the bus monitor runs), or null.</summary>
    public Func<string?>? Blocked { get; set; }

    private async Task DiscoverAsync()
    {
        if (Blocked?.Invoke() is string why) throw new InvalidOperationException(why);
        var ch = _ch() ?? throw new InvalidOperationException("Connect first (a DEMO port works without a vehicle).");
        var engine = new DashboardEngine(ch);
        await _bus.WaitAsync();
        try { await engine.DiscoverAsync(); } finally { _bus.Release(); }
        var (table, _, err) = EnhancedPidTable.LoadOrCreate(_pidDir);
        if (err != null) _log.Message(LogEventLevel.Warning, "Live data", "enhanced PID table: " + err);
        _catalog = LiveSignal.Catalog(engine.Supported, EnhancedPidTable.WithBuiltIns(table)).ToList();
        Available.Clear();
        foreach (var s in _catalog) Available.Add(new SignalPick(s));
        Info = $"{engine.Supported.Count} OBD-II PID(s) supported; {_catalog.Count} signal(s) available. Tick some and press Start.";
        _log.Message(LogEventLevel.Information, "Live data", Info);
    }

    private async Task StartAsync()
    {
        if (Active) return;
        if (Blocked?.Invoke() is string why) throw new InvalidOperationException(why);
        CloseReplay();
        if (_catalog.Count == 0) await DiscoverAsync();
        var picked = Available.Where(a => a.Selected).Select(a => a.Signal).ToList();
        if (picked.Count == 0) { Info = "Tick at least one signal first."; return; }
        var ch = _ch() ?? throw new InvalidOperationException("Connect first.");

        BuildRows(picked);

        _engine = new DashboardEngine(ch);
        _engine.Scheduler.BaseMs = Math.Max(50, _settings().Dashboard.RefreshMs);
        await _bus.WaitAsync();
        try { await _engine.DiscoverAsync(); } finally { _bus.Release(); }
        _engine.Configure(picked.Where(s => s.Kind == LiveKind.ObdPid).Select(s => s.Key),
                          EnhancedPidTable.WithBuiltIns(EnhancedPidTable.LoadOrCreate(_pidDir).Table).Pids
                              .Where(e => picked.Any(s => s.Kind == LiveKind.Enhanced && s.Key == e.Key))
                              .Select(e => { e.Enabled = true; return e; }));
        _cts = new CancellationTokenSource();
        Active = true;
        Info = $"Polling {picked.Count} signal(s).";
        _ = RunAsync(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            var refresh = Math.Max(50, _settings().Dashboard.RefreshMs / 2);
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(refresh, ct);
                if (!await _bus.WaitAsync(0, ct)) continue; // a command is using the adapter; try next tick
                Dictionary<string, double> v;
                try { v = await _engine!.PollOnceAsync(ct); }
                finally { _bus.Release(); }
                if (v.Count == 0) continue;
                var now = DateTime.Now;
                Ingest(v, now);
                _rec?.Write(now, v);
            }
        }
        catch (OperationCanceledException) { /* normal stop */ }
        catch (Exception ex) { Info = "Live data stopped: " + ex.Message; _log.Message(LogEventLevel.Warning, "Live data", "Polling stopped", ex); }
        finally { Active = false; }
    }

    public void Stop()
    {
        _cts?.Cancel(); _cts = null;
        if (_rec != null) { _log.Message(LogEventLevel.Information, "Live data", "Recording saved " + _rec.Path); _rec.Dispose(); _rec = null; OnPropertyChanged(nameof(Recording)); OnPropertyChanged(nameof(RecordText)); }
        Info = "Stopped.";
    }

    private void ToggleRecord()
    {
        if (_rec != null) { var p = _rec.Path; _rec.Dispose(); _rec = null; _log.Message(LogEventLevel.Information, "Live data", "Recording saved " + p); }
        else
        {
            if (Rows.Count == 0) { Info = "Start polling before recording."; return; }
            _rec = new DashboardCsvRecorder(_logsDir, Rows.Select(r => r.Key).ToList(), DateTime.Now);
            _log.Message(LogEventLevel.Information, "Live data", "Recording to " + _rec.Path + " (base units)");
        }
        OnPropertyChanged(nameof(Recording)); OnPropertyChanged(nameof(RecordText));
    }

    /// <summary>
    /// Rows for a new run (live or replay): fresh readings, fresh chart history, triggers re-armed. The first eight rows are
    /// plotted by default; each row keeps its colour slot (its position) whatever else is plotted.
    /// </summary>
    private void BuildRows(IReadOnlyList<LiveSignal> signals)
    {
        foreach (var r in Rows) r.PropertyChanged -= RowChanged;
        _readings.Reset();
        _readings.Track(signals);
        Rows.Clear();
        History.Clear(); _markers.Clear(); OnPropertyChanged(nameof(Markers));
        _triggers.Rearm();
        for (int i = 0; i < signals.Count; i++)
        {
            var row = new LiveRowVm(signals[i]) { Source = _readings.Rows.First(r => r.Key == signals[i].Key), Slot = i, Plot = i < ChartPalette.Slots };
            row.PropertyChanged += RowChanged;
            Rows.Add(row);
        }
        UpdatePlotted();
    }

    private void RowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LiveRowVm.Plot) || sender is not LiveRowVm row) return;
        if (row.Plot && Rows.Count(r => r.Plot) > ChartPalette.Slots)
        {
            row.Plot = false;
            Info = $"The chart shows at most {ChartPalette.Slots} signals at once; untick one first.";
            return;
        }
        UpdatePlotted();
    }

    /// <summary>Colour slot = the row's position mod 8, so a signal keeps its colour when others are ticked or unticked.</summary>
    private void UpdatePlotted()
    {
        Plotted = Rows.Where(r => r.Plot).Select(r => new ChartSeries(r.Key, r.Name, r.Unit, r.Slot % ChartPalette.Slots)).ToList();
        Revision++;
    }

    /// <summary>One cycle, live or replayed: grid statistics, chart history, triggers.</summary>
    private void Ingest(IReadOnlyDictionary<string, double> v, DateTime t)
    {
        _readings.Apply(v, t);
        foreach (var row in Rows) if (v.ContainsKey(row.Key)) row.Refresh();
        History.Add(v, t);
        foreach (var hit in _triggers.Apply(v, t))
        {
            var line = $"{t:HH:mm:ss.fff}  {hit.Trigger.Text}  value {hit.Value:0.##}";
            TriggerHits.Insert(0, line);
            while (TriggerHits.Count > 200) TriggerHits.RemoveAt(TriggerHits.Count - 1);
            _markers.Add(t);
            if (_markers.Count > 500) _markers.RemoveAt(0);
            OnPropertyChanged(nameof(Markers));
            _log.Message(LogEventLevel.Information, "Live data", "Trigger: " + line);
        }
        Revision++;
    }

    private void AddTrigger()
    {
        SignalTrigger t;
        try { t = SignalTrigger.Parse(TriggerText); }
        catch (FormatException ex) { Info = ex.Message; return; }
        if (Rows.Count > 0 && !Rows.Any(r => string.Equals(r.Key, t.Key, StringComparison.OrdinalIgnoreCase)))
            Info = $"Note: '{t.Key}' is not one of the signals in the grid ({string.Join(", ", Rows.Select(r => r.Key))}); it will fire only if it appears.";
        _triggers.Add(t); TriggerList.Add(t); TriggerText = "";
    }

    // ---- Replay of a recorded CSV (no adapter traffic) -------------------------------------------------------------

    private void OpenReplay()
    {
        if (Active) { Info = "Stop live polling before opening a recording."; return; }
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Live-data / dashboard recording|*.csv", InitialDirectory = ObdLink.Core.Storage.AppPaths.Ensure(_logsDir), RestoreDirectory = true };
        if (DialogService.ShowFileDialog(dlg) != true) throw new UserCancelledException("No recording chosen");
        var log = LiveCsvLog.Load(dlg.FileName);
        if (log.Cycles.Count == 0) { Info = "That recording has no readable samples."; return; }
        CloseReplay();
        var known = _catalog.ToDictionary(c => c.Key, StringComparer.OrdinalIgnoreCase);
        BuildRows(log.Keys.Select(k => known.TryGetValue(k, out var s) ? s : new LiveSignal(k, k, "", LiveKind.ObdPid, 9)).ToList());
        _replayLog = log;
        _replay = new ReplayCursor(log) { Speed = ReplaySpeed };
        Info = $"Loaded {Path.GetFileName(dlg.FileName)}: {log.Cycles.Count} cycles, {log.Duration.TotalSeconds:0} s" + (log.SkippedLines > 0 ? $", {log.SkippedLines} unreadable line(s) skipped" : "") + ". Press Play.";
        _log.Message(LogEventLevel.Information, "Live data", Info);
        OnPropertyChanged(nameof(HasReplay)); OnPropertyChanged(nameof(ReplayText));
    }

    private void PlayPause()
    {
        if (_replay is null) { Info = "Open a recording first."; return; }
        if (Replaying) { _replayTimer!.Stop(); }
        else
        {
            if (_replay.Finished) { _replay.Seek(0); BuildRows(Rows.Select(r => r.Signal).ToList()); }
            _replayTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _replayTimer.Tick -= ReplayTick; _replayTimer.Tick += ReplayTick;
            _replayTick = DateTime.UtcNow;
            _replayTimer.Start();
        }
        OnPropertyChanged(nameof(Replaying)); OnPropertyChanged(nameof(ReplayText));
    }

    private void ReplayTick(object? sender, EventArgs e)
    {
        if (_replay is null) return;
        var now = DateTime.UtcNow; var dt = now - _replayTick; _replayTick = now;
        foreach (var c in _replay.Advance(dt)) { Ingest(c.Values, c.T); ChartEnd = c.T; }
        if (_replay.Finished) { _replayTimer!.Stop(); OnPropertyChanged(nameof(Replaying)); Info = "Replay finished. Play starts it again."; }
        OnPropertyChanged(nameof(ReplayText));
    }

    private void CloseReplay()
    {
        _replayTimer?.Stop();
        _replay = null; _replayLog = null; ChartEnd = null;
        OnPropertyChanged(nameof(HasReplay)); OnPropertyChanged(nameof(Replaying)); OnPropertyChanged(nameof(ReplayText));
    }

    /// <summary>LiveReadings row this VM mirrors, so current/min/max/avg come from the Core aggregator, not a second copy.</summary>
    public LiveRowVm RowFor(string key) => Rows.First(r => r.Key == key);
    internal LiveReadings Readings => _readings;
}

/// <summary>A tickable catalog entry.</summary>
public sealed class SignalPick : ObservableObject
{
    public LiveSignal Signal { get; }
    public SignalPick(LiveSignal s) { Signal = s; }
    public string Display => Signal.Display + (Signal.Verified ? "" : "  (unverified)");
    public string Kind => Signal.Kind == LiveKind.ObdPid ? "OBD-II" : "Ford";
    private bool _sel; public bool Selected { get => _sel; set => Set(ref _sel, value); }
}

/// <summary>One live grid row. Values are read back from the shared <see cref="LiveReadings"/> via the owning view model.</summary>
public sealed class LiveRowVm : ObservableObject
{
    private readonly LiveSignal _s;
    public LiveRowVm(LiveSignal s) { _s = s; }
    public LiveSignal Signal => _s;
    /// <summary>Fixed colour slot for the chart (row position).</summary>
    public int Slot { get; init; }
    private bool _plot; public bool Plot { get => _plot; set => Set(ref _plot, value); }
    public string Key => _s.Key;
    public string Name => _s.Name;
    public string Unit => _s.Unit;
    public string Kind => _s.Kind == LiveKind.ObdPid ? "OBD-II" : "Ford";
    public LiveReadings.Row? Source { get; set; }
    public string Current => Fmt(Source?.Current);
    public string Min => Source is { } r && !double.IsNaN(r.Stats.Min) ? Fmt(r.Stats.Min) : "";
    public string Max => Source is { } r && !double.IsNaN(r.Stats.Max) ? Fmt(r.Stats.Max) : "";
    public string Avg => Source is { } r && r.Stats.Count > 0 ? Fmt(r.Stats.Avg) : "";
    public long Samples => Source?.Samples ?? 0;
    private static string Fmt(double? v) => v is { } d ? d.ToString("0.##") : "—";
    public void Refresh() { OnPropertyChanged(nameof(Current)); OnPropertyChanged(nameof(Min)); OnPropertyChanged(nameof(Max)); OnPropertyChanged(nameof(Avg)); OnPropertyChanged(nameof(Samples)); }
}
