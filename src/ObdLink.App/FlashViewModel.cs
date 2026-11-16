using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using ObdLink.Core.Adapters;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Elm;
using ObdLink.Core.Flash;
using ObdLink.Core.Logging;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// Flash tab (Phase 5): pick a VBF file, run the preflight, confirm the warnings (typed FLASH), flash with live progress, and
/// resume or abandon an interrupted job. All protocol work is in <see cref="FlashEngine"/>; this class only wires it to the
/// adapter, the bus lock, the backup and the dialogs. The whole job holds the shared bus lock, like every adapter command.
/// On a DEMO port the target is a SYNTHETIC bootloader module (<see cref="SimulatedFlashEcu"/>) so the full flow can be tried
/// without a vehicle.
/// </summary>
public sealed class FlashViewModel : ObservableObject
{
    private readonly IEventLog _log;
    private readonly Func<UdsSessionHost?> _host;
    private readonly Func<ModuleRow?> _module;
    private readonly Func<ModuleRow, Func<Task>, bool, Task> _onBus;
    private readonly SemaphoreSlim _bus;
    private readonly Func<SafetyGuard> _safety;
    private readonly Func<AdapterSession?> _session;
    private readonly Func<PortInfo?> _port;
    private readonly Func<AsBuiltService?> _asBuilt;
    private readonly Func<string> _vin;
    private readonly string _dir;
    private readonly IHostPower _power;
    private readonly IProgress<FlashProgress> _progress;
    private FlashImage? _image;
    private CancellationTokenSource? _stop;
    private SimulatedFlashEcu? _demo;

    public ObservableCollection<PreflightItem> Preflight { get; } = new();
    public ObservableCollection<string> Lines { get; } = new();

    private string _imageText = "No file loaded."; public string ImageText { get => _imageText; private set => Set(ref _imageText, value); }
    private string _preflightText = "Run the checks after loading a file."; public string PreflightText { get => _preflightText; private set => Set(ref _preflightText, value); }
    private string _status = "Idle."; public string Status { get => _status; private set => Set(ref _status, value); }
    private string _bytesText = ""; public string BytesText { get => _bytesText; private set => Set(ref _bytesText, value); }
    private double _percent; public double Percent { get => _percent; private set => Set(ref _percent, value); }
    private int _retries; public int Retries { get => _retries; private set => Set(ref _retries, value); }
    private bool _running; public bool Running { get => _running; private set { if (Set(ref _running, value)) { OnPropertyChanged(nameof(Idle)); OnPropertyChanged(nameof(Banner)); OnPropertyChanged(nameof(BannerVisible)); } } }
    public bool Idle => !Running;
    private string _jobText = ""; public string JobText { get => _jobText; private set => Set(ref _jobText, value); }
    private bool _hasOpenJob; public bool HasOpenJob { get => _hasOpenJob; private set { if (Set(ref _hasOpenJob, value)) { OnPropertyChanged(nameof(Banner)); OnPropertyChanged(nameof(BannerVisible)); } } }
    private string _adapterNote = "Not connected."; public string AdapterNote { get => _adapterNote; private set => Set(ref _adapterNote, value); }
    private string _level = "01";
    /// <summary>SecurityAccess seed level for programming (hex, odd). [UNVERIFIED for Ford modules]</summary>
    public string SecurityLevel { get => _level; set => Set(ref _level, value); }
    public bool BannerVisible => Running || HasOpenJob;
    public string Banner => Running ? "FLASHING: DO NOT DISCONNECT, DO NOT TURN THE KEY, KEEP THE CHARGER ON"
        : HasOpenJob ? "OPEN FLASH JOB: the module may be in its bootloader. Resume (same file) or abandon." : "";

    public AsyncCommand OpenFileCommand { get; }
    public AsyncCommand DemoImageCommand { get; }
    public AsyncCommand PreflightCommand { get; }
    public AsyncCommand FlashCommand { get; }
    public AsyncCommand ResumeCommand { get; }
    public RelayCommand StopCommand { get; }
    public AsyncCommand AbandonCommand { get; }
    public RelayCommand ClearCommand { get; }

    public FlashViewModel(IEventLog log, Func<UdsSessionHost?> host, Func<ModuleRow?> module, Func<ModuleRow, Func<Task>, bool, Task> onBus,
        SemaphoreSlim bus, Func<SafetyGuard> safety, Func<AdapterSession?> session, Func<PortInfo?> port, Func<AsBuiltService?> asBuilt,
        Func<string> vin, string flashDir, IHostPower power, Action<string, Exception> err, Func<string?>? blocked = null)
    {
        _log = log; _host = host; _module = module; _onBus = onBus; _bus = bus; _safety = safety; _session = session; _port = port;
        _asBuilt = asBuilt; _vin = vin; _dir = flashDir; _power = power;
        _progress = new Progress<FlashProgress>(OnProgress); // created on the UI thread: reports land there
        (string, object?)[] P() => new (string, object?)[] { ("module", _module()?.Label), ("addr", _module()?.Address), ("file", _image?.Name) };
        AsyncCommand C(string name, Func<Task> f, bool needsBus = true) => new(name, log, async () =>
        {
            if (needsBus && blocked?.Invoke() is string why) throw new InvalidOperationException(why);
            try { await f(); }
            finally { Refresh(); }
        }, err, P, () => !Running);

        OpenFileCommand = C("Flash: open VBF file", OpenFileAsync, needsBus: false);
        DemoImageCommand = C("Flash: load DEMO image", DemoImageAsync, needsBus: false);
        PreflightCommand = C("Flash: preflight", PreflightAsync);
        FlashCommand = C("Flash: start", () => RunAsync(resume: false));
        ResumeCommand = C("Flash: resume", () => RunAsync(resume: true));
        AbandonCommand = C("Flash: abandon job", AbandonAsync, needsBus: false);
        StopCommand = new RelayCommand("Flash: stop after this segment", log, _ => { _stop?.Cancel(); Add("Stop requested: the flash stops after the current segment (the job stays open for resume)."); });
        ClearCommand = new RelayCommand("Flash: clear log", log, _ => Lines.Clear());
    }

    /// <summary>Updates adapter note and open-job state. Call on the UI thread (after connect / module change).</summary>
    public void Refresh()
    {
        var s = _session();
        AdapterNote = s is null ? "Not connected." : s.Profile.Kind switch
        {
            AdapterKind.ObdLinkEx => "OBDLink EX (USB): wired link, recommended for flashing.",
            AdapterKind.ObdLinkMxPlus => "OBDLink MX+ (Bluetooth): flashing is allowed with an extra warning. Keep the PC close; a wired OBDLink EX is recommended.",
            _ => $"{s.Profile.DisplayName}: not an OBDLink EX/MX+. Flashing is allowed, but MS-CAN modules can't be reached.",
        } + (_port() is { IsSimulated: true } ? " DEMO: flashing targets a simulated bootloader module." : "");
        var m = _module();
        var st = m is null ? FlashJournalState.None : FlashJournalState.From(Journal(m).ReadAll());
        HasOpenJob = st.Open;
        JobText = !st.Exists ? "No flash history for this module."
            : st.Open ? $"Open job for 0x{st.TxId:X3}: {st.LastPhase ?? "started"}, {st.SegmentsDone} segment(s) done, {st.BytesAcked:N0} bytes acknowledged, attempt {st.Attempts}. Last fault: {st.LastFault?.Detail ?? "none"}. Backup: {Path.GetFileName(st.BackupRef ?? "-")}. Open the same file, then Resume."
            : "Last job finished or was abandoned.";
        foreach (var c in new[] { OpenFileCommand, DemoImageCommand, PreflightCommand, FlashCommand, ResumeCommand, AbandonCommand }) c.RaiseCanExecuteChanged();
    }

    private IFlashJournal Journal(ModuleRow m) => new FileFlashJournal(Path.Combine(_dir, $"flash-{m.Def.TxId:X3}.jsonl"));

    private Task OpenFileAsync()
    {
        var dlg = new OpenFileDialog { Filter = "VBF flash files (*.vbf)|*.vbf|All files (*.*)|*.*", Title = "Open flash file", InitialDirectory = ObdLink.Core.Storage.AppPaths.Ensure(_dir) };
        if (DialogService.ShowFileDialog(dlg) != true) return Task.CompletedTask;
        SetImage(VbfReader.Read(dlg.FileName));
        return Task.CompletedTask;
    }

    private Task DemoImageAsync()
    {
        if (_port() is not { IsSimulated: true }) throw new InvalidOperationException("The DEMO image is only for DEMO ports (the simulated module). Open a real VBF file for a vehicle.");
        var m = _module() ?? throw new InvalidOperationException("Select a module first");
        var segs = Enumerable.Range(0, 2).Select(i => new FlashSegment(0x00010000u + (uint)i * 0x10000, Enumerable.Range(0, 6000).Select(b => (byte)(b * 13 + i)).ToArray())).ToList();
        SetImage(VbfReader.Parse(VbfReader.Write(segs, "DEMO-14C204-SIM", m.Def.TxId), "DEMO (synthetic).vbf"));
        return Task.CompletedTask;
    }

    private void SetImage(FlashImage img)
    {
        _image = img;
        ImageText = $"{img.Name}: {img.Segments.Count} segment(s), {img.TotalBytes:N0} bytes" + (img.SoftwarePartNumber is string p ? $", part {p}" : "")
            + (img.EcuTxId is uint t ? $", for module 0x{t:X3}" : "") + $". SHA-256 {img.Sha256[..8]}…{img.Sha256[^4..]}"
            + (img.Warnings.Count > 0 ? "\nNote: " + string.Join("; ", img.Warnings) : "");
        Preflight.Clear(); PreflightText = "Run the checks.";
        Add($"Loaded {img.Name} ({img.TotalBytes:N0} bytes)");
        _log.Message(LogEventLevel.Information, "Flash", ImageText);
    }

    private (ModuleRow M, FlashPlan Plan) Target()
    {
        var m = _module() ?? throw new InvalidOperationException("Select a module first");
        var img = _image ?? throw new InvalidOperationException("Open a flash file first");
        if (!byte.TryParse(SecurityLevel, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var lvl) || lvl % 2 == 0)
            throw new InvalidOperationException($"Security level '{SecurityLevel}' must be an odd hex byte (e.g. 01)");
        return (m, new FlashPlan { TxId = m.Def.TxId, Image = img, SecurityLevel = lvl });
    }

    private bool Demo => _port() is { IsSimulated: true };

    private UdsClient Client(ModuleRow m, FlashPlan plan)
    {
        if (Demo)
        {
            if (_demo is null || _demo.TxId != m.Def.TxId) _demo = new SimulatedFlashEcu(m.Def.TxId);
            _demo.ExpectedImage = plan.Image;
            return new UdsClient(_demo, m.Def.TxId) { SeedKey = new DemoKey(), Gate = new WriteGate(() => _safety().Gate.ReadOnly) };
        }
        var host = _host() ?? throw new InvalidOperationException("Not connected - click Connect first (use a DEMO port to try without a vehicle)");
        return host.Client(m);
    }

    private Task<double?> Volts(CancellationToken ct) => Demo ? Task.FromResult<double?>(_demo?.Volts ?? 13.4)
        : (_session() ?? throw new InvalidOperationException("Not connected")).ReadBatteryVoltageAsync(ct);

    private async Task Locked(ModuleRow m, Func<Task> f)
    {
        await _bus.WaitAsync();
        try { if (Demo) await f(); else await _onBus(m, f, false); }
        finally { _bus.Release(); }
    }

    private async Task PreflightAsync()
    {
        var (m, plan) = Target();
        var uds = Client(m, plan);
        FlashPreflightReport? rep = null;
        await Locked(m, async () => rep = await FlashPreflight.RunAsync(uds, plan, Volts, _session()?.Profile, _power, ReadPart));
        Preflight.Clear();
        foreach (var i in rep!.Items) Preflight.Add(i);
        int warn = rep.Warnings.Count(), fail = rep.Items.Count(i => i.Status == PreflightStatus.Fail);
        PreflightText = fail > 0 ? $"{fail} check(s) failed: flashing is blocked until they pass." : warn > 0 ? $"Ready, with {warn} warning(s) to acknowledge." : "All checks passed.";
        Add("Preflight: " + PreflightText);
    }

    private static async Task<string?> ReadPart(UdsClient uds, CancellationToken ct)
        => System.Text.Encoding.ASCII.GetString(await uds.ReadDataByIdentifierAsync(0xF188, ct)).Trim('\0', ' ');

    private async Task RunAsync(bool resume)
    {
        var (m, plan) = Target();
        var journal = Journal(m);
        var st = FlashJournalState.From(journal.ReadAll());
        if (resume && !st.Open) throw new InvalidOperationException("There is no open flash job for this module.");
        if (!resume && st.Open) throw new InvalidOperationException("This module has an open flash job. Resume it (same file) or abandon it first.");
        if (resume && st.ImageSha256 != plan.Image.Sha256) throw new InvalidOperationException("The loaded file is not the one the open job started with. Open the same file to resume.");

        // Hard preflight checks run again right before the confirmation, so a stale report can't be used.
        var uds = Client(m, plan);
        FlashPreflightReport? rep = null;
        await Locked(m, async () => rep = await FlashPreflight.RunAsync(uds, plan, Volts, _session()?.Profile, _power, ReadPart));
        Preflight.Clear(); foreach (var i in rep!.Items) Preflight.Add(i);
        if (!rep.CanFlash) throw new FlashPreflightException(rep);
        await _safety().RequireAsync(FlashWarnings.For(plan, _session()?.Profile));

        var engine = new FlashEngine(plan, journal)
        {
            ReadVoltage = Volts,
            Backup = ct => BackupAsync(m, ct),
            ReadModulePart = ReadPart,
            Host = _power,
            Progress = _progress,
        };
        _stop = new CancellationTokenSource();
        Running = true; Retries = 0; Percent = 0;
        Add(resume ? $"Resuming flash of {m.Label} (0x{m.Address})" : $"Flashing {m.Label} (0x{m.Address}) with {plan.Image.Name}");
        try
        {
            FlashResult? r = null;
            await Locked(m, async () => r = await Task.Run(() => engine.RunAsync(uds, _stop.Token)));
            Status = $"Done in {r!.Elapsed:m\\:ss}. {r.Message}" + (r.Retries > 0 ? $" ({r.Retries} retries)" : "");
            Add(Status);
            DialogService.Info(Status, "Flash finished");
        }
        catch (FlashFailedException ex)
        {
            Status = (ex.ModuleMayBeInBootloader ? "Stopped: the module may be in its bootloader. Use Resume. " : "Stopped before anything changed. ") + ex.Message;
            Add(Status);
            throw;
        }
        finally { Running = false; _stop.Dispose(); _stop = null; }
    }

    private async Task<string> BackupAsync(ModuleRow m, CancellationToken ct)
    {
        if (Demo) return "DEMO: simulated module, no backup file";
        var svc = _asBuilt() ?? throw new InvalidOperationException("Not connected");
        var path = await svc.BackupAvailableAsync(_vin(), (ushort)m.Def.TxId, ct: ct);
        if (path is not null) { Add($"Backup saved: {Path.GetFileName(path)}"); return path; }
        Add($"{m.Label} has no As-Built blocks; nothing to back up besides the journal.");
        return $"no As-Built blocks on 0x{m.Def.TxId:X3}";
    }

    private Task AbandonAsync()
    {
        var m = _module() ?? throw new InvalidOperationException("Select a module first");
        var journal = Journal(m);
        if (!FlashJournalState.From(journal.ReadAll()).Open) throw new InvalidOperationException("There is no open flash job for this module.");
        if (!DialogService.YesNo($"Abandon the open flash job for {m.Label}?\n\nThe module may still be in its bootloader and may not work until it is flashed. The journal and backup are kept.",
            "Abandon flash job", System.Windows.MessageBoxImage.Warning)) return Task.CompletedTask;
        journal.Append(new FlashJournalEntry(DateTimeOffset.UtcNow, FlashJournalKind.Abandoned, Detail: "abandoned by the operator"));
        Add($"Flash job for {m.Label} abandoned by the operator.");
        return Task.CompletedTask;
    }

    private void OnProgress(FlashProgress p)
    {
        Status = $"{p.Phase}: {p.Message}";
        Percent = Math.Round(p.Fraction * 100, 1);
        BytesText = $"{p.BytesDone:N0} / {p.BytesTotal:N0} bytes" + (p.SegmentCount > 0 && p.Segment > 0 ? $" · segment {p.Segment}/{p.SegmentCount}" : "");
        Retries = p.Retries;
        if (p.Phase != FlashPhase.Download || p.Message.Contains("retry", StringComparison.OrdinalIgnoreCase) || p.Message.Contains("waiting", StringComparison.OrdinalIgnoreCase))
            Add($"{p.Phase}: {p.Message}");
    }

    private void Add(string line)
    {
        Lines.Add($"{DateTimeOffset.Now:HH:mm:ss} {line}");
        while (Lines.Count > 500) Lines.RemoveAt(0);
    }

    /// <summary>DEMO only: the simulated bootloader's key is the seed reversed.</summary>
    private sealed class DemoKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(seed.Reverse().ToArray());
    }
}
