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

    public ObservableCollection<SignalPick> Available { get; } = new();
    public ObservableCollection<LiveRowVm> Rows { get; } = new();

    public AsyncCommand DiscoverCommand { get; }
    public AsyncCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RecordCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand SelectAllCommand { get; }
    public RelayCommand SelectNoneCommand { get; }

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
    private async Task DiscoverAsync()
    {
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
        if (_catalog.Count == 0) await DiscoverAsync();
        var picked = Available.Where(a => a.Selected).Select(a => a.Signal).ToList();
        if (picked.Count == 0) { Info = "Tick at least one signal first."; return; }
        var ch = _ch() ?? throw new InvalidOperationException("Connect first.");

        _readings.Reset();
        _readings.Track(picked);
        Rows.Clear();
        foreach (var s in picked)
        {
            var row = new LiveRowVm(s) { Source = _readings.Rows.First(r => r.Key == s.Key) };
            Rows.Add(row);
        }

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
                _readings.Apply(v, DateTime.Now);
                foreach (var row in Rows) if (v.ContainsKey(row.Key)) row.Refresh();
                _rec?.Write(DateTime.Now, v);
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
