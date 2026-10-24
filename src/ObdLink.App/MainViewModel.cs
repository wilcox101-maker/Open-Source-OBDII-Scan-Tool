using ObdLink.Core.Adapters;
using ObdLink.Core.Diagnostics;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Dtc;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Serilog;
using Serilog.Events;

namespace ObdLink.App;

public sealed class ModuleRow : ObservableObject
{
    public required ModuleDefinition Def { get; init; }
    public string Acronym => Def.Acronym;
    public string FullName => ModuleAcronyms.FullName(Def.Acronym, Def.Name);
    public string Label => ModuleAcronyms.Label(Def.Acronym, Def.Name);
    public string Tooltip => ModuleAcronyms.Tooltip(Def.Acronym, Def.Name) + $"\nCAN 0x{Def.TxId:X3}/0x{Def.RxId:X3} ({Def.Bus}), {Def.Confidence}";
    public string Address => Def.TxId.ToString("X3");
    public Bus Bus => Def.Bus;
    public Confidence Confidence => Def.Confidence;
    private string _found = "?"; public string Found { get => _found; set => Set(ref _found, value); }
}

public sealed class AsBuiltRow : ObservableObject
{
    public required AsBuiltAddress Addr { get; init; }
    public string Address => Addr.ToString();
    private string _data = ""; public string Data { get => _data; set { Set(ref _data, value); OnChecksum(); } }
    private string _cs = ""; public string Checksum { get => _cs; private set => Set(ref _cs, value); }
    private void OnChecksum()
    {
        try { Checksum = AsBuiltLine.ComputeChecksum(Addr, Convert.FromHexString(_data.Replace(" ", ""))).ToString("X2"); }
        catch { Checksum = "ERR"; }
    }
}

public sealed record DtcRow(string Source, string Code, string Status, string Description);

public sealed class MainViewModel : ObservableObject
{
    private static readonly PortInfo DemoMx = new("DEMO-MX+", "Simulated OBDLink MX+", PortKind.Virtual);
    private static readonly PortInfo DemoEx = new("DEMO-EX", "Simulated OBDLink EX", PortKind.Virtual);
    private readonly IConfirmationProvider _confirm;
    private readonly DtcDatabase _dtcDb = DtcDatabase.LoadEmbedded();
    private readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OBDLinkDiag");
    private string LogDir => Path.Combine(_dataDir, "logs");
    private AdapterSession? _session; private ElmDiagChannel? _ch; private RawConsoleService? _console; private AsBuiltService? _asBuilt;
    private string _vin = "UNKNOWNVIN";

    /// <summary>Central user-interaction / app-message / traffic log.</summary>
    public IEventLog EventLog { get; }
    public LogPanelViewModel EventsPanel { get; }
    public LogPanelViewModel MessagesPanel { get; }
    public LogPanelViewModel TrafficPanel { get; }

    public ObservableCollection<PortInfo> Ports { get; } = new();
    public ObservableCollection<string> Vehicles { get; } = new(VehicleProfiles.All);
    public ObservableCollection<ModuleRow> Modules { get; } = new();
    public ObservableCollection<DtcRow> Dtcs { get; } = new();
    public ObservableCollection<AsBuiltRow> AsBuiltRows { get; } = new();
    /// <summary>Factory reference baseline (Ford motorcraftservice.com .ab). Read-only reference. It is never written to a module automatically.</summary>
    private FordAbFile? _factory;
    private string _factoryInfo = "No factory .ab loaded"; public string FactoryInfo { get => _factoryInfo; set => Set(ref _factoryInfo, value); }
    public ObservableCollection<AsBuiltDiff> FactoryDiff { get; } = new();
    public AsyncCommand ImportFactoryAbCommand { get; }
    public AsyncCommand ExportFactoryAbCommand { get; }
    public AsyncCommand CompareToFactoryCommand { get; }
    public AsyncCommand ImportAbtCommand { get; }
    public AsyncCommand ExportAbtCommand { get; }

    private PortInfo? _port; public PortInfo? SelectedPort { get => _port; set { if (Set(ref _port, value) && !_initializing) EventLog.UserAction("Port selected", ("port", value?.Name), ("kind", value?.Kind)); } }
    private string _vehicle = VehicleProfiles.F150_2017; public string SelectedVehicle { get => _vehicle; set { if (Set(ref _vehicle, value)) { EventLog.UserAction("Vehicle selected", ("vehicle", value)); LoadModules(); } } }
    private ModuleRow? _mod; public ModuleRow? SelectedModule { get => _mod; set { if (Set(ref _mod, value) && value is not null) EventLog.UserAction("Module selected", ("module", value.Label), ("addr", value.Address)); } }
    private string _tab = "DTCs"; public string SelectedTab { get => _tab; set { if (Set(ref _tab, value) && !string.IsNullOrEmpty(value)) EventLog.UserAction("Tab changed", ("tab", value)); } }
    private string _bottomTab = "Events"; public string SelectedLogTab { get => _bottomTab; set { if (Set(ref _bottomTab, value) && !string.IsNullOrEmpty(value)) EventLog.UserAction("Log tab changed", ("tab", value)); } }

    private ConnectionStatus _conn = ConnectionStatus.Disconnected;
    public ConnectionStatus Connection { get => _conn; private set { _conn = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(Voltage)); OnPropertyChanged(nameof(AdapterName)); OnPropertyChanged(nameof(Capabilities)); OnPropertyChanged(nameof(IsConnected)); OnPropertyChanged(nameof(ConnState)); } }
    public string Status => Connection.StatusText;
    public string Voltage => Connection.Voltage;
    public string AdapterName => Connection.AdapterName;
    public string Capabilities => Connection.Capabilities;
    public bool IsConnected => Connection.State == ConnectionState.Connected;
    private string _baud = ""; public string Baud { get => _baud; set => Set(ref _baud, value); }
    private bool _ms; public bool MsCanAvailable { get => _ms; set => Set(ref _ms, value); }
    private string _blk = "1"; public string AsBuiltBlockNo { get => _blk; set { if (Set(ref _blk, value)) EventLog.UserAction("As-Built block entered", ("block", value)); } }
    private string _ci = ""; public string ConsoleInput { get => _ci; set => Set(ref _ci, value); }
    private string _co = ""; public string ConsoleOutput { get => _co; set => Set(ref _co, value); }
    private bool _allow; public bool ConsoleAllowWrites { get => _allow; set { if (Set(ref _allow, value)) { EventLog.UserAction("Console 'Allow writes' toggled", ("on", value)); if (_console != null) _console.AllowWrites = value; } } }
    private bool _initializing = true;

    // ---- Settings: read-only test mode (default ON) ----
    private string SettingsPath => Path.Combine(_dataDir, "settings.json");
    private AppSettings _settings = new();
    public bool ReadOnlyTestMode
    {
        get => _settings.ReadOnlyTestMode;
        set
        {
            if (value == _settings.ReadOnlyTestMode) return;
            EventLog.UserAction("Read-only test mode toggled", ("requested", value));
            if (!value && !Ask("Enable writes", "Turn OFF read-only test mode?\n\nThis re-enables As-Built writes/restores, DTC clears and console write services. These can change module configuration.", MessageBoxImage.Warning))
            { OnPropertyChanged(nameof(ReadOnlyTestMode)); return; }
            _settings.ReadOnlyTestMode = value; SaveSettings();
            if (_console != null) _console.ReadOnlyMode = value;
            EventLog.Message(LogEventLevel.Warning, "Settings", $"Read-only test mode {(value ? "ON" : "OFF")}");
            OnPropertyChanged(); OnPropertyChanged(nameof(ModeBanner));
        }
    }
    public string GuideText { get; } = LoadGuide();
    private static string LoadGuide()
    {
        try { using var st = typeof(MainViewModel).Assembly.GetManifestResourceStream("GUIDE.md"); return st == null ? "docs/GUIDE.md not embedded." : new StreamReader(st).ReadToEnd(); }
        catch (Exception ex) { return "Guide unavailable: " + ex.Message; }
    }
    public string[] Themes => ThemeManager.Modes;
    public string SelectedTheme
    {
        get => _settings.Theme;
        set
        {
            if (value == _settings.Theme || string.IsNullOrEmpty(value)) return;
            _settings.Theme = value; SaveSettings();
            var eff = ThemeManager.Apply(value);
            EventLog.UserAction("Theme changed", ("theme", value), ("effective", eff));
            OnPropertyChanged();
        }
    }
    public string ConnState => Connection.State.ToString();
    public string AppVersion => "v" + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "?");
    public string ModeBanner => ReadOnlyTestMode ? "READ-ONLY TEST MODE (writes/clears disabled)" : "WRITES ENABLED";
    public string DiscoveryStart { get => _settings.Discovery.StartId.ToString("X3"); set { _settings.Discovery.StartId = Convert.ToUInt32(value, 16); SaveSettings(); EventLog.UserAction("Discovery start ID entered", ("id", value)); OnPropertyChanged(); } }
    public string DiscoveryEnd { get => _settings.Discovery.EndId.ToString("X3"); set { _settings.Discovery.EndId = Convert.ToUInt32(value, 16); SaveSettings(); EventLog.UserAction("Discovery end ID entered", ("id", value)); OnPropertyChanged(); } }
    public bool DiscoveryTesterPresent { get => _settings.Discovery.TesterPresent; set { _settings.Discovery.TesterPresent = value; SaveSettings(); EventLog.UserAction("Discovery option toggled", ("TesterPresent", value)); } }
    public bool DiscoveryF190 { get => _settings.Discovery.ReadVinF190; set { _settings.Discovery.ReadVinF190 = value; SaveSettings(); EventLog.UserAction("Discovery option toggled", ("ReadVinF190", value)); } }
    public bool DiscoveryKwp1A { get => _settings.Discovery.Kwp1A; set { _settings.Discovery.Kwp1A = value; SaveSettings(); EventLog.UserAction("Discovery option toggled", ("Kwp1A", value)); } }
    private bool _capDisc; public bool CaptureIncludesDiscovery { get => _capDisc; set { if (Set(ref _capDisc, value)) EventLog.UserAction("Capture option toggled", ("includeDiscovery", value)); } }
    private bool _capRedact; public bool CaptureRedactVins { get => _capRedact; set { if (Set(ref _capRedact, value)) EventLog.UserAction("Capture option toggled", ("redactVins", value)); } }
    private void SaveSettings() { try { _settings.Save(SettingsPath); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Settings", "Could not save settings", ex); } }
    private WriteGate Gate => new(() => _settings.ReadOnlyTestMode);
    public AsyncCommand DiagnosticsCaptureCommand { get; }
    public AsyncCommand DiscoveryCommand { get; }

    public AsyncCommand RefreshPortsCommand { get; }
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand DisconnectCommand { get; }
    public AsyncCommand ScanCommand { get; }
    public AsyncCommand ReadObdDtcsCommand { get; }
    public AsyncCommand ReadModuleDtcsCommand { get; }
    public AsyncCommand ClearModuleDtcsCommand { get; }
    public AsyncCommand ReadVinCommand { get; }
    public AsyncCommand ReadAsBuiltCommand { get; }
    public AsyncCommand BackupAsBuiltCommand { get; }
    public AsyncCommand WriteAsBuiltCommand { get; }
    public AsyncCommand RestoreAsBuiltCommand { get; }
    public AsyncCommand ConsoleSendCommand { get; }

    /// <summary>Yes/No dialog that is recorded in the event log (shown / accepted / cancelled).</summary>
    private bool Ask(string title, string message, MessageBoxImage icon = MessageBoxImage.Question)
    {
        var op = EventLog.Begin("Confirmation dialog", ("title", title));
        var ok = MessageBox.Show(message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;
        if (ok) op.Succeeded("Accepted: " + title); else op.Cancelled("User answered No");
        return ok;
    }
    private void Info(string title, string message) { EventLog.UserAction("Info dialog shown", ("title", title)); MessageBox.Show(message, title); }

    private string OpenFile(string purpose, string filter, string? dir = null)
    {
        var dlg = new OpenFileDialog { Filter = filter, InitialDirectory = dir ?? "" };
        if (dlg.ShowDialog() != true) throw new UserCancelledException($"{purpose}: open dialog cancelled");
        EventLog.UserAction("File chosen for import", ("purpose", purpose), ("file", Path.GetFileName(dlg.FileName)));
        return dlg.FileName;
    }
    private string SaveFile(string purpose, string filter, string name)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = name };
        if (dlg.ShowDialog() != true) throw new UserCancelledException($"{purpose}: save dialog cancelled");
        EventLog.UserAction("File chosen for export", ("purpose", purpose), ("file", Path.GetFileName(dlg.FileName)));
        return dlg.FileName;
    }

    public MainViewModel(IConfirmationProvider confirm)
    {
        EventLog = LogSetup.CreateEventLog(LogDir);
        _confirm = new LoggingConfirmationProvider(confirm, EventLog);
        EventsPanel = new("Events", LogChannel.Events, EventLog, LogEventLevel.Verbose);
        MessagesPanel = new("Messages", LogChannel.Messages, EventLog, LogEventLevel.Information);
        TrafficPanel = new("Adapter traffic", LogChannel.Traffic, EventLog, LogEventLevel.Verbose);
        EventLog.EntryAdded += e => Application.Current?.Dispatcher.BeginInvoke(() =>
            (e.Channel switch { LogChannel.Events => EventsPanel, LogChannel.Messages => MessagesPanel, _ => TrafficPanel }).Add(e));
        EventLog.Message(LogEventLevel.Information, "App", $"Started v{typeof(MainViewModel).Assembly.GetName().Version}; logs in {LogDir}");

        void Err(string op, Exception ex)
        {
            var text = ErrorText.ForOperation(op, ex);
            EventLog.Message(LogEventLevel.Error, op, text, ex);
            MessageBox.Show(text, op + " - error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        AsyncCommand C(string name, Func<Task> f, Func<(string, object?)[]>? p = null) => new(name, EventLog, f, Err, p);
        (string, object?)[] Mod() => new (string, object?)[] { ("module", SelectedModule?.Label), ("addr", SelectedModule?.Address) };
        (string, object?)[] ModBlk() => new (string, object?)[] { ("module", SelectedModule?.Label), ("addr", SelectedModule?.Address), ("block", AsBuiltBlockNo) };

        _settings = AppSettings.Load(SettingsPath);
        ThemeManager.Apply(_settings.Theme);
        RefreshPortsCommand = C("Refresh ports", () =>
        {
            Ports.Clear(); Ports.Add(DemoMx); Ports.Add(DemoEx); foreach (var p in PortEnumerator.List()) Ports.Add(p);
            SelectedPort ??= Ports[0];
            EventLog.Message(LogEventLevel.Information, "Ports", $"{Ports.Count} ports: {string.Join(", ", Ports.Select(p => p.Name))}");
            return Task.CompletedTask;
        });
        ConnectCommand = C("Connect", ConnectAsync, () => new (string, object?)[] { ("port", SelectedPort?.Name), ("vehicle", SelectedVehicle) });
        DisconnectCommand = C("Disconnect", DisconnectAsync);
        ScanCommand = C("Scan modules", ScanAsync, () => new (string, object?)[] { ("vehicle", SelectedVehicle), ("modules", Modules.Count) });
        DiagnosticsCaptureCommand = C("Diagnostics capture", CaptureAsync, () => new (string, object?)[] { ("includeDiscovery", CaptureIncludesDiscovery), ("redactVins", CaptureRedactVins) });
        DiscoveryCommand = C("Discovery sweep", async () =>
        {
            if (_session is null) throw new InvalidOperationException("Not connected");
            await SelectDiagProtocol();
            var hits = await new DiscoverySweep(_session, Ch).RunAsync(_settings.Discovery, new Progress<string>(m => EventLog.Message(LogEventLevel.Information, "Discovery", m)));
            EventLog.Message(LogEventLevel.Information, "Discovery", $"Done: {hits.Select(h => h.RequestId).Distinct().Count()} responding IDs\n{DiscoverySweep.Summarize(hits)}");
        }, () => new (string, object?)[] { ("from", DiscoveryStart), ("to", DiscoveryEnd) });
        ReadObdDtcsCommand = C("Read OBD-II DTCs", async () => { Dtcs.Clear(); foreach (var (ecu, code) in await new ObdService(Ch).ReadStoredDtcsAsync()) Dtcs.Add(new($"{ecu:X3}", code, "stored", _dtcDb.Describe(code))); EventLog.Message(LogEventLevel.Information, "Read OBD-II DTCs", $"{Dtcs.Count} DTCs"); });
        ReadModuleDtcsCommand = C("Read module DTCs", async () => { Dtcs.Clear(); foreach (var d in await Uds().ReadDtcsByStatusAsync()) Dtcs.Add(new(SelectedModule!.Label, d.Display, $"0x{d.Status:X2}", _dtcDb.Describe(d.Display))); EventLog.Message(LogEventLevel.Information, "Read module DTCs", $"{SelectedModule?.Label}: {Dtcs.Count} DTCs"); }, Mod);
        ClearModuleDtcsCommand = C("Clear module DTCs", async () =>
        {
            await Safety().RequireAsync(new[] { new ConfirmationStep("Clear DTCs", $"Clear all DTCs in {SelectedModule?.Label}? Freeze-frame data will be lost.") });
            await Uds().ClearDtcsAsync();
        }, Mod);
        ReadVinCommand = C("Read VIN", async () => { _vin = await new ObdService(Ch).ReadVinAsync() ?? _vin; EventLog.Message(LogEventLevel.Information, "Read VIN", $"VIN {_vin}"); });
        ReadAsBuiltCommand = C("Read As-Built block", ReadAsBuiltAsync, ModBlk);
        BackupAsBuiltCommand = C("Backup As-Built block", async () => { var p = await AsBuilt.BackupAsync(_vin, Module, new[] { Block }); Info("Backup", $"Saved {p}"); }, ModBlk);
        WriteAsBuiltCommand = C("Write + verify As-Built block", async () =>
        {
            var data = AsBuiltRows.SelectMany(r => Convert.FromHexString(r.Data.Replace(" ", ""))).ToArray();
            await AsBuilt.WriteBlockAsync(_vin, new AsBuiltBlock { Module = Module, Block = Block, Data = data });
            Info("Write", "Written and verified by read-back.");
        }, ModBlk);
        RestoreAsBuiltCommand = C("Restore As-Built from backup", async () =>
        {
            var f = OpenFile("As-Built backup", "As-Built backup|*.asbuilt.json", Path.Combine(_dataDir, "backups"));
            await AsBuilt.RestoreAsync(f);
        });
        ImportFactoryAbCommand = C("Import factory .ab", () =>
        {
            _factory = FordAbFile.Load(OpenFile("Factory .ab", "Ford As-Built|*.ab|All files|*.*"));
            var bad = _factory.Lines.Count(l => !l.ChecksumValid);
            FactoryInfo = $"Factory .ab: VIN …{(_factory.Vin.Length >= 6 ? _factory.Vin[^6..] : _factory.Vin)}, {_factory.AsBuiltModules.Count()} modules, {_factory.Lines.Count} lines, {bad} bad checksums";
            EventLog.Message(bad > 0 ? LogEventLevel.Warning : LogEventLevel.Information, "Import factory .ab", FactoryInfo);
            return Task.CompletedTask;
        });
        ExportFactoryAbCommand = C("Export .ab", () =>
        {
            if (_factory is null) throw new InvalidOperationException("Import a factory .ab first");
            var f = SaveFile("Factory .ab", "Ford As-Built|*.ab", "factory-redacted.ab");
            var redact = Ask("Export", "Redact VIN (keep last 6) and module serials?");
            File.WriteAllText(f, (redact ? _factory.Redacted() : _factory).ToXml());
            EventLog.Message(LogEventLevel.Information, "Export .ab", $"Saved {Path.GetFileName(f)} (redacted: {redact})");
            return Task.CompletedTask;
        });
        ImportAbtCommand = C("Import .abt", () =>
        {
            var abt = ForscanAbtFile.Load(OpenFile("FORScan .abt", "FORScan As-Built backup|*.abt|All files|*.*"));
            AsBuiltRows.Clear();
            foreach (var l in abt.Lines) AsBuiltRows.Add(new AsBuiltRow { Addr = l.Address, Data = Convert.ToHexString(l.Data) });
            var bad = abt.Lines.Count(l => !l.ChecksumValid);
            FactoryDiff.Clear();
            var info = $".abt: {string.Join(",", abt.Modules.Select(m => m.ToString("X3")))}, {abt.Lines.Count} lines, {bad} bad checksums";
            if (_factory is not null)
            {
                foreach (var m in abt.Modules) foreach (var d in AsBuiltDiff.Compare(_factory.Lines, abt.Lines, m)) FactoryDiff.Add(d);
                info += " | vs factory: " + string.Join(", ", FactoryDiff.GroupBy(d => d.Kind).Select(g => $"{g.Key}={g.Count()}"));
            }
            FactoryInfo = info;
            EventLog.Message(bad > 0 ? LogEventLevel.Warning : LogEventLevel.Information, "Import .abt", info);
            return Task.CompletedTask;
        });
        ExportAbtCommand = C("Export .abt", () =>
        {
            if (AsBuiltRows.Count == 0) throw new InvalidOperationException("Read or import As-Built rows first");
            var lines = AsBuiltRows.Select(r => AsBuiltLine.Create(r.Addr, Convert.FromHexString(r.Data.Replace(" ", "")))).ToList();
            var f = SaveFile("FORScan .abt", "FORScan As-Built backup|*.abt", $"{lines[0].Address.Module:X3}.abt");
            var abt = ForscanAbtFile.Parse(ForscanAbtFile.Format(lines));
            if (_factory is { Vin.Length: 17 } && Ask("Export", "Redact VIN (keep last 6) in block data?"))
                abt = abt.Redacted(_factory.Vin);
            File.WriteAllText(f, abt.ToText());
            return Task.CompletedTask;
        });
        CompareToFactoryCommand = C("Compare to factory", async () =>
        {
            if (_factory is null) throw new InvalidOperationException("Import a factory .ab first");
            var current = new List<AsBuiltLine>();
            foreach (var b in _factory.Blocks(Module))
                current.AddRange(_factory.SplitLike(Module, b, (await AsBuilt.ReadBlockAsync(Module, b)).Data));
            FactoryDiff.Clear();
            foreach (var d in AsBuiltDiff.Compare(_factory.Lines, current, Module)) FactoryDiff.Add(d);
            EventLog.Message(LogEventLevel.Information, "Compare to factory", string.Join(", ", FactoryDiff.GroupBy(d => d.Kind).Select(g => $"{g.Key}={g.Count()}")));
        }, Mod);
        ConsoleSendCommand = C("Console send", async () =>
        {
            if (_console is null) throw new InvalidOperationException("Not connected");
            var input = ConsoleInput; ConsoleInput = "";
            var result = await _console.ExecuteAsync(input);
            ConsoleOutput += $"> {input}{Environment.NewLine}{result}{Environment.NewLine}";
            if (result.StartsWith("BLOCKED")) EventLog.Message(LogEventLevel.Warning, "Console", $"{input}: {result}");
        }, () => new (string, object?)[] { ("command", ConsoleInput) });
        _ = RefreshPortsCommand.ExecuteAsync();
        LoadModules();
        _initializing = false;
    }

    private ElmDiagChannel Ch => _ch ?? throw new InvalidOperationException("Not connected - click Connect first (use a DEMO port to try without a vehicle)");
    private ushort Module => (ushort)(SelectedModule?.Def.TxId ?? throw new InvalidOperationException("Select a module in the module list first"));
    private byte Block
    {
        get
        {
            try { return Convert.ToByte(AsBuiltBlockNo.Trim(), 16); }
            catch (Exception) { throw new InvalidOperationException($"Block '{AsBuiltBlockNo}' is not a valid block number (1-99, as in FORScan)"); }
        }
    }
    private UdsClient Uds() => new(Ch, Module);
    private SafetyGuard Safety() => new(_session ?? throw new InvalidOperationException("Not connected"), _confirm, gate: Gate);

    private async Task SelectDiagProtocol()
    {   // HS-CAN 500k 11-bit (STP 33, or ATSP6 on plain ELM).
        if (_session!.IsStn) await _session.SelectProtocolAsync(StnProtocol.HsCan11_500); else await _session.SelectElmProtocolAsync('6');
    }

    private async Task CaptureAsync()
    {
        if (_session is null) throw new InvalidOperationException("Connect first (use a Demo port to try it without a vehicle)");
        var ver = typeof(MainViewModel).Assembly.GetName().Version?.ToString() ?? "?";
        var logs = LogSetup.ReadRecentLogs(LogDir).ToList();
        var path = await new DiagnosticsCapture(_session, Ch).RunAsync(new(SelectedVehicle, Modules.Select(r => r.Def).ToList(), StnProtocol.HsCan11_500, '6',
            CaptureIncludesDiscovery ? _settings.Discovery : null, ver, $"Port: {SelectedPort?.Name}; adapter: {AdapterName}; read-only mode: {ReadOnlyTestMode}",
            logs, CaptureRedactVins),
            Path.Combine(_dataDir, "captures"), new Progress<string>(m => EventLog.Message(LogEventLevel.Information, "Capture", m)));
        await SelectDiagProtocol();
        EventLog.Message(LogEventLevel.Information, "Capture", $"Saved {path} ({logs.Count} log files included)");
        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
        Info("Diagnostics capture", $"Capture saved:\n{path}\n\nSend this zip back. " + (CaptureRedactVins ? "VINs were redacted." : "(It is NOT redacted: it may include your VIN and adapter serial.)"));
    }
    private AsBuiltService AsBuilt => _asBuilt ?? throw new InvalidOperationException("Not connected - click Connect first");

    private void LoadModules()
    {
        Modules.Clear();
        foreach (var m in VehicleProfiles.ModulesFor(SelectedVehicle)) Modules.Add(new ModuleRow { Def = m });
    }

    private async Task DisconnectAsync()
    {
        if (_session != null) await _session.Link.DisposeAsync();
        _session = null; _ch = null; _console = null; _asBuilt = null;
        Connection = ConnectionStatus.Disconnected; Baud = ""; MsCanAvailable = false;
        EventLog.Message(LogEventLevel.Information, "Disconnect", "Disconnected");
    }

    private async Task ConnectAsync()
    {
        if (_session != null) await DisconnectAsync();
        var sel = SelectedPort ?? DemoMx;
        try
        {
            ITransport t;
            Connection = ConnectionStatus.Connecting(sel);
            if (sel.Kind == PortKind.Virtual) t = new SimulatedAdapter { Profile = sel.Name == DemoEx.Name ? SimProfile.Ex : SimProfile.MxPlus }.CreateTransport();
            else
            {
                var guess = AdapterProfiles.ForPort(sel.Kind);
                Connection = ConnectionStatus.Connecting(sel, "Probing");
                var found = await BaudProber.ProbeAsync(b => new SerialPortTransport(sel.Name, b), guess.BaudCandidates, TimeSpan.FromSeconds(sel.Kind == PortKind.Bluetooth ? 5 : 1))
                            ?? throw new InvalidOperationException($"No ELM327/STN adapter answered on {sel.Name}");
                t = found.Transport;
                Baud = sel.Kind == PortKind.Bluetooth ? "n/a (BT SPP)" : $"{found.Baud} bps";
            }
            var link = new ElmLink(t);
            link.Traffic += (d, s) => EventLog.Traffic(d, s);
            _session = new AdapterSession(link, initial: sel.Kind == PortKind.Virtual ? AdapterProfiles.OtherStn : AdapterProfiles.ForPort(sel.Kind));
            Connection = ConnectionStatus.Connecting(sel, "Initializing");
            var info = await _session.InitializeAsync();
            MsCanAvailable = _session.Profile.Has(AdapterCapabilities.MsCan);
            _ch = new ElmDiagChannel(_session);
            _console = new RawConsoleService(link) { AllowWrites = ConsoleAllowWrites, ReadOnlyMode = ReadOnlyTestMode };
            _asBuilt = new AsBuiltService(m => new UdsClient(Ch, m), Safety(), Path.Combine(_dataDir, "backups"));
            if (_session.IsStn) await _session.SelectProtocolAsync(StnProtocol.HsCan11_500);
            Connection = ConnectionStatus.Connected(sel, info, _session.Profile);
            EventLog.Message(LogEventLevel.Information, "Connect", $"{Connection.StatusText}; adapter {AdapterName}; battery {Voltage}");
        }
        catch (Exception ex)
        {
            if (_session != null) { try { await _session.Link.DisposeAsync(); } catch { } }
            _session = null; _ch = null; _console = null; _asBuilt = null;
            Connection = ConnectionStatus.Failed(ex.Message);
            throw;
        }
    }

    private async Task ScanAsync()
    {
        if (_session is null) throw new InvalidOperationException("Not connected");
        var rows = Modules.ToDictionary(r => r.Def);
        var res = await new ModuleScanner(_session, Ch).ScanAsync(Modules.Select(r => r.Def).ToList(),
            new Progress<ScanResult>(r => rows[r.Module].Found = r.Responded ? "YES" : "no"));
        await SelectDiagProtocol();
        EventLog.Message(LogEventLevel.Information, "Scan modules", $"{res.Count(r => r.Responded)} of {res.Count} modules responded");
    }

    private async Task ReadAsBuiltAsync()
    {
        var did = AsBuiltBlock.DidForBlock(Block);
        var b = await AsBuilt.ReadBlockAsync(Module, Block);
        AsBuiltRows.Clear();
        foreach (var l in b.Lines()) AsBuiltRows.Add(new AsBuiltRow { Addr = l.Address, Data = Convert.ToHexString(l.Data) });
        EventLog.Message(LogEventLevel.Information, "Read As-Built block", $"{SelectedModule?.Label} block {AsBuiltBlockNo} (DID 0x{did:X4}): {b.Data.Length} bytes, {AsBuiltRows.Count} lines");
    }
}
