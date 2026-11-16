using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using ObdLink.Core.Elm;
using ObdLink.Core.Logging;
using ObdLink.Core.Monitor;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// v0.13.0 Bus Monitor tab: passive capture (ATMA, silent) on HS-CAN, MS-CAN or HS-CAN2 with a per-ID grid (count, rate,
/// latest data, bits that changed), a software ID filter and an optional CSV log. Read-only: nothing is transmitted.
/// The monitor owns the adapter (shared bus lock) while it runs; other adapter commands are refused until Stop.
/// </summary>
public sealed class BusMonitorViewModel : ObservableObject
{
    private readonly IEventLog _log;
    private readonly Func<AdapterSession?> _session;
    private readonly SemaphoreSlim _bus;
    private readonly Func<bool> _msCan;
    private readonly string _logsDir;
    private readonly Action _starting;
    private readonly DispatcherTimer _ui;
    private readonly Dictionary<(uint, bool), MonitorRowVm> _byId = new();
    private CancellationTokenSource? _cts;
    private Task? _run;
    private MonitorStats _stats = new();
    private CanCsvLog? _rec;
    private readonly object _recSync = new();
    private BusMonitor? _mon;

    public ObservableCollection<MonitorRowVm> Rows { get; } = new();
    public IReadOnlyList<string> BusChoices => _msCan() ? new[] { "HS-CAN", "MS-CAN", "HS-CAN2" } : new[] { "HS-CAN" };
    private string _busChoice = "HS-CAN"; public string BusChoice { get => _busChoice; set => Set(ref _busChoice, value); }
    private string _filter = "all"; public string FilterText { get => _filter; set => Set(ref _filter, value); }
    private string _status = "Connect, pick a bus and press Start. Nothing is transmitted: the adapter listens only.";
    public string Status { get => _status; private set => Set(ref _status, value); }
    private bool _running; public bool Running { get => _running; private set { if (Set(ref _running, value)) { OnPropertyChanged(nameof(Idle)); } } }
    public bool Idle => !_running;
    public string RecordText => _rec is null ? "Record CSV" : "Stop recording";

    public AsyncCommand StartCommand { get; }
    public AsyncCommand StopCommand { get; }
    public RelayCommand RecordCommand { get; }
    public RelayCommand ResetCommand { get; }

    /// <param name="starting">Called on the UI thread just before the monitor takes the adapter (stop pollers, release sessions).</param>
    public BusMonitorViewModel(IEventLog log, Func<AdapterSession?> session, SemaphoreSlim bus, Func<bool> msCanAvailable, string logsDir, Action starting, Action<string, Exception> err)
    {
        _log = log; _session = session; _bus = bus; _msCan = msCanAvailable; _logsDir = logsDir; _starting = starting;
        StartCommand = new AsyncCommand("Bus monitor: start", log, StartAsync, err, () => new (string, object?)[] { ("bus", BusChoice), ("filter", FilterText) });
        StopCommand = new AsyncCommand("Bus monitor: stop", log, StopAsync, err);
        RecordCommand = new RelayCommand("Bus monitor: record CSV", log, _ => ToggleRecord());
        ResetCommand = new RelayCommand("Bus monitor: reset stats", log, _ => { _stats.Reset(); _byId.Clear(); Rows.Clear(); });
        _ui = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _ui.Tick += (_, _) => RefreshRows();
    }

    /// <summary>Called when the adapter connection changes, so the bus list matches the adapter.</summary>
    public void AdapterChanged() { OnPropertyChanged(nameof(BusChoices)); if (!BusChoices.Contains(BusChoice)) BusChoice = "HS-CAN"; }

    private static Bus ToBus(string s) => s switch { "MS-CAN" => Bus.MsCan, "HS-CAN2" => Bus.HsCan2, _ => Bus.HsCan };

    private async Task StartAsync()
    {
        if (Running) return;
        var session = _session() ?? throw new InvalidOperationException("Not connected - click Connect first (a DEMO port works without a vehicle)");
        var filter = IdFilter.Parse(FilterText);
        var bus = ToBus(BusChoice);
        _starting();
        _stats = new MonitorStats(); _byId.Clear(); Rows.Clear();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        await _bus.WaitAsync(ct);
        Running = true;
        Status = $"Monitoring {BusChoice} (filter: {filter.Text}). Other adapter commands wait until you press Stop.";
        _log.Message(LogEventLevel.Information, "Bus monitor", Status);
        _mon = new BusMonitor(session);
        _ui.Start();
        var stats = _stats;
        _run = Task.Run(async () =>
        {
            MonitorStop stop = MonitorStop.Cancelled; Exception? error = null;
            try { stop = await _mon.RunAsync(bus, filter, stats, f => { lock (_recSync) _rec?.Write(f); }, () => DateTime.Now, ct); }
            catch (OperationCanceledException) { /* Stop pressed while waiting to start: a normal end */ }
            catch (Exception ex) { error = ex; }
            finally { _bus.Release(); }
            Application.Current?.Dispatcher.BeginInvoke(() => Finished(stop, error, stats));
        });
    }

    private void Finished(MonitorStop stop, Exception? error, MonitorStats stats)
    {
        _ui.Stop();
        RefreshRows();
        Running = false;
        StopRecording();
        var overflow = _mon?.Overflows is int o && o > 0 ? $"; adapter buffer overflowed {o} time(s) (some frames lost, monitor restarted)" : "";
        Status = error is not null
            ? $"Monitor stopped: {error.Message}"
            : $"Stopped ({stop}). {stats.Frames} frames, {stats.TotalRate:0} frames/s, {Rows.Count} IDs{overflow}.";
        _log.Message(error is null ? LogEventLevel.Information : LogEventLevel.Warning, "Bus monitor", Status, error);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_run is { } r) { try { await r; } catch { /* reported in Finished */ } }
        _cts = null; _run = null;
    }

    private void RefreshRows()
    {
        foreach (var r in _stats.Snapshot())
        {
            if (!_byId.TryGetValue((r.Id, r.Extended), out var vm))
            {
                vm = new MonitorRowVm(r);
                _byId[(r.Id, r.Extended)] = vm;
                int i = 0; while (i < Rows.Count && (Rows[i].Extended, Rows[i].Id).CompareTo((r.Extended, r.Id)) < 0) i++;
                Rows.Insert(i, vm);
            }
            else vm.Update(r);
        }
        if (Running) Status = $"Monitoring {BusChoice}: {_stats.Frames} frames, {_stats.TotalRate:0} frames/s, {Rows.Count} IDs, {_stats.Filtered} filtered out, {_stats.NonFrameLines} non-frame lines" + (_rec is null ? "" : $", recording {_rec.Written}");
    }

    private void ToggleRecord()
    {
        lock (_recSync)
        {
            if (_rec is null)
            {
                _rec = new CanCsvLog(_logsDir, BusChoice, DateTime.Now);
                _log.Message(LogEventLevel.Information, "Bus monitor", "Recording frames to " + _rec.Path);
            }
            else StopRecordingLocked();
        }
        OnPropertyChanged(nameof(RecordText));
    }

    private void StopRecording() { lock (_recSync) StopRecordingLocked(); OnPropertyChanged(nameof(RecordText)); }
    private void StopRecordingLocked()
    {
        if (_rec is null) return;
        _log.Message(LogEventLevel.Information, "Bus monitor", $"Recording saved: {_rec.Path} ({_rec.Written} frames)");
        _rec.Dispose(); _rec = null;
    }
}

/// <summary>One ID in the monitor grid, updated in place.</summary>
public sealed class MonitorRowVm : ObservableObject
{
    private MonitorRow _r;
    public MonitorRowVm(MonitorRow r) => _r = r;
    public uint Id => _r.Id;
    public bool Extended => _r.Extended;
    public string IdText => _r.IdText;
    public long Count => _r.Count;
    public string Rate => _r.RateHz >= 10 ? _r.RateHz.ToString("0") : _r.RateHz.ToString("0.0");
    public int Dlc => _r.LastData.Length;
    public string Data => _r.DataText;
    public string Changed => _r.ChangedText;
    public string LastSeen => _r.LastSeen.ToString("HH:mm:ss.fff");
    public void Update(MonitorRow r)
    {
        _r = r;
        foreach (var p in new[] { nameof(Count), nameof(Rate), nameof(Dlc), nameof(Data), nameof(Changed), nameof(LastSeen) }) OnPropertyChanged(p);
    }
}
