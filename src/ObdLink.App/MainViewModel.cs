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
using ObdLink.Core.Firmware;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using ObdLink.Core.Vehicles;
using System.Windows.Threading;
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
    private string _found = "not scanned"; public string Found { get => _found; set { if (Set(ref _found, value)) OnPropertyChanged(nameof(PickerText)); } }
    /// <summary>Known / likely to have As-Built (DE..) blocks: factory .ab evidence, a backup, or a Ford module that normally has them.</summary>
    private bool _hasAb; public bool HasAsBuilt { get => _hasAb; set => Set(ref _hasAb, value); }
    private DateTime? _backup; public DateTime? LastBackup { get => _backup; set { if (Set(ref _backup, value)) { OnPropertyChanged(nameof(BackupText)); OnPropertyChanged(nameof(PickerText)); } } }
    public string? BackupPath { get; set; }
    public string BackupText => LastBackup is { } d ? "backup " + d.ToString("yyyy-MM-dd HH:mm") : "no backup";
    /// <summary>Picker line: "BCM - Body Control Module · 0x726 · HS-CAN · found · backup 2026-10-24 09:30".</summary>
    public string PickerText => $"{Acronym} - {FullName}  ·  0x{Address}  ·  {(Bus == Bus.MsCan ? "MS-CAN" : Bus == Bus.HsCan ? "HS-CAN" : Bus.ToString())}  ·  {(Found == "YES" ? "found" : Found == "no" ? "no reply" : "not scanned")}  ·  {BackupText}";
    public static readonly HashSet<string> TypicalAsBuilt = new(StringComparer.OrdinalIgnoreCase)
    { "PCM", "TCM", "BCM", "IPC", "APIM", "ABS", "PSCM", "RCM", "PAM", "GWM", "ACM", "SCCM", "DDM", "PDM", "HVAC", "FCIM", "IPMA", "IPMB", "CCM", "TBM", "SOBDM", "OCSM", "DSM", "GPSM", "HCM", "TRM", "RFA", "ATCM" };
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
    private static ObdLink.Core.Storage.AppPaths Paths => ObdLink.Core.Storage.AppPaths.Current;
    private string LogDir => Paths.LogsDir;
    public string DataDir => Paths.DataDir;
    public string DataDirShort => (Paths.IsPortable ? "Data: " : "Data (LocalAppData): ") + Paths.DataDir;
    public string DataDirTooltip => Paths.IsPortable ? $"Portable data folder next to the exe:\n{Paths.DataDir}" : $"Not portable: {Paths.FallbackReason}\nUsing {Paths.DataDir}";
    public RelayCommand OpenDataFolderCommand { get; }
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
    private ModuleRow? _mod; public ModuleRow? SelectedModule
    {
        get => _mod;
        set
        {
            if (!Set(ref _mod, value) || value is null) return;
            EventLog.UserAction("Module selected", ("module", value.Label), ("addr", value.Address));
            ModuleHint = "";
            if (!_initializing && _settings.LastModule != value.Address) { _settings.LastModule = value.Address; SaveSettings(); }
        }
    }
    /// <summary>As-Built page picker: modules with As-Built blocks (or all, if the toggle is on). Selection is shared with Modules / DTCs.</summary>
    public ObservableCollection<ModuleRow> AsBuiltModules { get; } = new();
    public bool AsBuiltShowAll { get => _settings?.AsBuiltShowAllModules == true; set { _settings.AsBuiltShowAllModules = value; SaveSettings(); OnPropertyChanged(); RefreshAsBuiltModules(); } }
    private string _modHint = ""; public string ModuleHint { get => _modHint; set => Set(ref _modHint, value); }
    private bool _batchRunning; public bool BatchRunning { get => _batchRunning; private set => Set(ref _batchRunning, value); }
    private double _batchPct; public double BatchPercent { get => _batchPct; private set => Set(ref _batchPct, value); }
    private string _batchText = ""; public string BatchText { get => _batchText; private set => Set(ref _batchText, value); }
    private CancellationTokenSource? _batchCts;
    private string _tab = "Dashboard"; public string SelectedTab { get => _tab; set { if (Set(ref _tab, value) && !string.IsNullOrEmpty(value)) { EventLog.UserAction("Tab changed", ("tab", value)); SyncDashboard(); } } }
    private readonly SemaphoreSlim _bus = new(1, 1);
    public DashboardViewModel Dashboard { get; }
    private void SyncDashboard() => Dashboard?.Update(SelectedTab == "Dashboard", IsConnected && _ch != null);
    private string _bottomTab = "Events"; public string SelectedLogTab { get => _bottomTab; set { if (Set(ref _bottomTab, value) && !string.IsNullOrEmpty(value)) EventLog.UserAction("Log tab changed", ("tab", value)); } }

    private ConnectionStatus _conn = ConnectionStatus.Disconnected;
    public ConnectionStatus Connection { get => _conn; private set { _conn = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(Voltage)); OnPropertyChanged(nameof(AdapterName)); OnPropertyChanged(nameof(Capabilities)); OnPropertyChanged(nameof(IsConnected)); OnPropertyChanged(nameof(ConnState)); SyncDashboard(); } }
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
    private string SettingsPath => Paths.SettingsFile;
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
            OnPropertyChanged(); OnPropertyChanged(nameof(HeaderIconUri));
        }
    }
    public string ConnState => Connection.State.ToString();
    // ---- App logo (Dale's artwork, assets/icons). Auto = black variant 1 on light, yellow variant 2 on dark. ----
    public string[] HeaderIcons { get; } = { "Auto", "1", "2", "3" };
    public string SelectedHeaderIcon
    {
        get => _settings.HeaderIcon;
        set { if (string.IsNullOrEmpty(value) || value == _settings.HeaderIcon) return; _settings.HeaderIcon = value; SaveSettings(); EventLog.UserAction("Header icon changed", ("icon", value)); OnPropertyChanged(); OnPropertyChanged(nameof(HeaderIconUri)); }
    }
    public static string IconFor(string choice, string effectiveTheme) => choice is "1" or "2" or "3" ? choice : effectiveTheme == "Dark" ? "2" : "1";
    public string HeaderIconUri => $"pack://application:,,,/OBDLinkDiag;component/Assets/icon-{IconFor(_settings.HeaderIcon, ThemeManager.Effective)}.png";
    public string AppVersion => "v" + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "?");
    public string ModeBanner => ReadOnlyTestMode ? "READ-ONLY TEST MODE (writes/clears disabled)" : "WRITES ENABLED";
    public string DiscoveryStart { get => _settings.Discovery.StartId.ToString("X3"); set { _settings.Discovery.StartId = Convert.ToUInt32(value, 16); SaveSettings(); EventLog.UserAction("Discovery start ID entered", ("id", value)); OnPropertyChanged(); } }
    public string DiscoveryEnd { get => _settings.Discovery.EndId.ToString("X3"); set { _settings.Discovery.EndId = Convert.ToUInt32(value, 16); SaveSettings(); EventLog.UserAction("Discovery end ID entered", ("id", value)); OnPropertyChanged(); } }
    public bool DiscoveryTesterPresent { get => _settings.Discovery.TesterPresent; set { _settings.Discovery.TesterPresent = value; SaveSettings(); EventLog.UserAction("Discovery option toggled", ("TesterPresent", value)); } }
    public bool DiscoveryF190 { get => _settings.Discovery.ReadVinF190; set { _settings.Discovery.ReadVinF190 = value; SaveSettings(); EventLog.UserAction("Discovery option toggled", ("ReadVinF190", value)); } }
    public bool DiscoveryKwp1A { get => _settings.Discovery.Kwp1A; set { _settings.Discovery.Kwp1A = value; SaveSettings(); EventLog.UserAction("Discovery option toggled", ("Kwp1A", value)); } }
    private bool _capDisc; public bool CaptureIncludesDiscovery { get => _capDisc; set { if (Set(ref _capDisc, value)) EventLog.UserAction("Capture option toggled", ("includeDiscovery", value)); } }
    private bool _capRedact; public bool CaptureRedactVins { get => _capRedact; set { if (Set(ref _capRedact, value)) EventLog.UserAction("Capture option toggled", ("redactVins", value)); } }
    // ---- Settings page (v0.2.0) ----
    private void S(Action a, string what, object? v, string? prop = null) { a(); _settings.Normalize(); SaveSettings(); EventLog.UserAction("Setting changed: " + what, ("value", v)); OnPropertyChanged(prop); }
    public bool UpdateCheckEnabled { get => _settings.UpdateCheck; set => S(() => _settings.UpdateCheck = value, "update check (GitHub)", value, nameof(UpdateCheckEnabled)); }
    public bool RedactVinsDefault { get => _settings.RedactVinsDefault; set { S(() => _settings.RedactVinsDefault = value, "redact VINs by default", value, nameof(RedactVinsDefault)); CaptureRedactVins = value; foreach (var lp in new[] { EventsPanel, MessagesPanel, TrafficPanel }) lp.RedactOnExport = value; } }
    public bool SimulatorEnabled { get => _settings.SimulatorEnabled; set { S(() => _settings.SimulatorEnabled = value, "simulator ports", value, nameof(SimulatorEnabled)); RefreshPortsCommand.Execute(null); } }
    public string[] SpeedUnits { get; } = { "mph", "km/h" };
    public string[] TempUnits { get; } = { "°F", "°C" };
    public string[] PressureUnits { get; } = { "psi", "kPa" };
    public string[] LogLevels { get; } = { "Debug", "Info", "Warning", "Error" };
    public string[] AdapterKinds { get; } = { "Auto", "MX+", "EX", "Other STN/ELM" };
    public int[] RefreshRates { get; } = { 100, 250, 500, 1000, 2000 };
    public string SpeedUnit { get => _settings.Units.Speed; set { S(() => _settings.Units.Speed = value, "speed unit", value, nameof(SpeedUnit)); Dashboard.ApplyUnits(); } }
    public string TempUnit { get => _settings.Units.Temperature; set { S(() => _settings.Units.Temperature = value, "temperature unit", value, nameof(TempUnit)); Dashboard.ApplyUnits(); } }
    public string PressureUnit { get => _settings.Units.Pressure; set { S(() => _settings.Units.Pressure = value, "pressure unit", value, nameof(PressureUnit)); Dashboard.ApplyUnits(); OnPropertyChanged(nameof(TpmsLowThreshold)); } }
    public string LogLevelSetting { get => _settings.LogLevel; set => S(() => _settings.LogLevel = value, "log level (applies on restart)", value, nameof(LogLevelSetting)); }
    public int LogRetentionDays { get => _settings.LogRetentionDays; set => S(() => _settings.LogRetentionDays = value, "log retention days", value, nameof(LogRetentionDays)); }
    public string AdapterDefaultPort { get => _settings.Adapter.Port ?? ""; set => S(() => _settings.Adapter.Port = string.IsNullOrWhiteSpace(value) ? null : value.Trim(), "default port", value, nameof(AdapterDefaultPort)); }
    public string AdapterKind { get => _settings.Adapter.Kind; set => S(() => _settings.Adapter.Kind = value, "adapter kind", value, nameof(AdapterKind)); }
    public int AdapterTimeoutMs { get => _settings.Adapter.TimeoutMs; set => S(() => _settings.Adapter.TimeoutMs = value, "adapter timeout ms", value, nameof(AdapterTimeoutMs)); }
    public int DashboardRefreshMs { get => _settings.Dashboard.RefreshMs; set => S(() => _settings.Dashboard.RefreshMs = value, "dashboard refresh ms (applies when the dashboard restarts)", value, nameof(DashboardRefreshMs)); }
    public bool DashboardFordPids { get => _settings.Dashboard.FordEnhancedPids; set => S(() => _settings.Dashboard.FordEnhancedPids = value, "Ford enhanced PIDs (Mode 22, UNVERIFIED)", value, nameof(DashboardFordPids)); }
    public bool DashboardEstimateGear { get => _settings.Dashboard.EstimateGear; set => S(() => _settings.Dashboard.EstimateGear = value, "estimate gear from RPM/speed", value, nameof(DashboardEstimateGear)); }
    /// <summary>Low tyre pressure threshold shown in the Settings pressure unit; stored in kPa.</summary>
    public double TpmsLowThreshold
    {
        get => Math.Round(Units.ConvertOnly(_settings.Dashboard.TpmsLowKpa, Quantity.Pressure, _settings.Units), 1);
        set { var kpa = _settings.Units.Pressure == "psi" ? value / 0.1450377 : value; S(() => _settings.Dashboard.TpmsLowKpa = kpa, "TPMS low threshold", value, nameof(TpmsLowThreshold)); Dashboard.ApplyThresholds(); }
    }
    public AsyncCommand CheckUpdatesCommand { get; private set; } = null!;
    /// <summary>Opt-in GitHub release check. Manual (Help menu) always asks first unless the setting is on; never downloads anything.</summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        if (!_settings.UpdateCheck && (!manual || !Ask("Check for updates", $"Contact api.github.com once to read the latest release of {UpdateChecker.Repo}?\n\nOnly the app version is sent (User-Agent). Nothing is downloaded or installed.", MessageBoxImage.Question))) return;
        var v = typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0, 0); var ver = v.ToString(3);
        var r = await new UpdateChecker().CheckAsync(v);
        if (r is null) { EventLog.Message(LogEventLevel.Warning, "Update check", "Could not reach GitHub (offline or rate-limited)"); if (manual) Info("Check for updates", "Could not reach GitHub. Releases: " + UpdateChecker.ReleasesUrl); return; }
        var text = r.Newer ? $"A newer release is available: {r.Tag} ({r.Name}).\n{r.Url}" : $"You are up to date (v{ver}; latest {r.Tag}).";
        EventLog.Message(LogEventLevel.Information, "Update check", text);
        if (manual || r.Newer) Info("Check for updates", text);
    }
    public string AboutText => $"OBDLink Diag {AppVersion}-test (Ford-only, open source, MIT)\n\nOBDLink EX / MX+ diagnostics for Ford vehicles: dashboard (read-only Mode 01 / 22), module scan, DTCs, As-Built backup/compare, raw console.\n\nData folder: {DataDir}\nhttps://github.com/{UpdateChecker.Repo}\n\nTEST BUILD: use at your own risk. Read-only mode is ON by default.";

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
    public AsyncCommand ReadAllBlocksCommand { get; }
    public AsyncCommand ReadAllModulesAsBuiltCommand { get; }
    public AsyncCommand ReadAllModuleDtcsCommand { get; }
    public RelayCommand CancelBatchCommand { get; }
    public AsyncCommand BackupAsBuiltCommand { get; }
    public AsyncCommand WriteAsBuiltCommand { get; }
    public AsyncCommand RestoreAsBuiltCommand { get; }
    public AsyncCommand ConsoleSendCommand { get; }
    public AsyncCommand IdentifyVehicleCommand { get; }
    public AsyncCommand OpenMotorcraftCommand { get; }
    public AsyncCommand ImportVehicleAbCommand { get; }

    // ---- TX/RX/ERR/connection LEDs (v0.1.5). Traffic only stamps times; a 30 ms timer samples them, so bursts never flood the UI. ----
    public ActivityLeds Leds { get; } = new();
    private bool _txOn, _rxOn, _errOn;
    public bool TxOn { get => _txOn; private set => Set(ref _txOn, value); }
    public bool RxOn { get => _rxOn; private set => Set(ref _rxOn, value); }
    public bool ErrOn { get => _errOn; private set => Set(ref _errOn, value); }
    private DispatcherTimer? _ledTimer;
    private void StartLedTimer()
    {
        if (Application.Current is null) return;
        _ledTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(30) };
        _ledTimer.Tick += (_, _) => { var st = Leds.Sample(); if (st.Tx != _txOn) TxOn = st.Tx; if (st.Rx != _rxOn) RxOn = st.Rx; if (st.Error != _errOn) ErrOn = st.Error; };
        _ledTimer.Start();
    }

    // ---- Identify vehicle / saved vehicle profiles (v0.1.5) ----
    private readonly Dictionary<string, VehicleProfile> _profiles = new();
    private VehicleProfile? _current;
    private string _vehInfo = "No vehicle identified yet. Connect, then click Identify vehicle (read-only)."; public string VehicleInfo { get => _vehInfo; set => Set(ref _vehInfo, value); }
    public bool OnlineVinDecode { get => _settings.OnlineVinDecode; set { _settings.OnlineVinDecode = value; SaveSettings(); EventLog.UserAction("Online VIN decode (NHTSA vPIC) toggled", ("on", value)); OnPropertyChanged(); } }
    private void LoadSavedProfiles()
    {
        foreach (var p in VehicleProfile.LoadAll(Paths.VehiclesDir)) AddProfile(p);
    }
    private void AddProfile(VehicleProfile p)
    {
        var had = _profiles.ContainsKey(p.Label);
        _profiles[p.Label] = p;
        if (!had) Vehicles.Add(p.Label);
    }
    public const string MotorcraftAsBuiltUrl = "https://www.motorcraftservice.com/AsBuilt";
    private static string DescribeProfile(VehicleProfile p)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{p.DisplayName}   VIN {p.VinRedacted}");
        if (p.Offline is { } o) sb.AppendLine($"Offline: {o.Manufacturer}, {o.Country}, MY {o.ModelYear}, plant {o.PlantCode} = {o.Plant}, check digit {(o.CheckDigitOk ? "OK" : "MISMATCH")}");
        if (p.Online is { Count: > 0 } on) sb.AppendLine("NHTSA vPIC: " + string.Join(", ", on.Where(k => k.Key is not ("ErrorCode" or "ErrorText")).Select(k => $"{k.Key}={k.Value}")));
        sb.AppendLine($"{p.Modules.Count} module(s):");
        foreach (var m in p.Modules)
            sb.AppendLine($"  {m.Bus,-5} 0x{m.TxId:X3} {m.Acronym,-6} {m.Name}" + string.Concat(m.Dids.Where(d => d.Key != "F190").Select(d => $"  {d.Key}={d.Value}")) + (m.HasAsBuiltDe00 ? "  [As-Built]" : ""));
        if (p.FactoryAbFile != null) sb.AppendLine($"Factory .ab: {p.FactoryAbFile}");
        return sb.ToString();
    }

    /// <summary>Yes/No dialog that is recorded in the event log (shown / accepted / cancelled).</summary>
    private bool Ask(string title, string message, MessageBoxImage icon = MessageBoxImage.Question)
    {
        var op = EventLog.Begin("Confirmation dialog", ("title", title));
        var ok = MessageBox.Show(message, title, MessageBoxButton.YesNo, icon) == MessageBoxResult.Yes;
        if (ok) op.Succeeded("Accepted: " + title); else op.Cancelled("User answered No");
        return ok;
    }
    private void Info(string title, string message) { EventLog.UserAction("Info dialog shown", ("title", title)); MessageBox.Show(message, title); }

    // v0.1.5: every dialog starts in a Data\<subfolder> (never the working directory) and logs the full chosen path.
    private string OpenFile(string purpose, string filter, string initialDir)
    {
        var dlg = new OpenFileDialog { Filter = filter, InitialDirectory = ObdLink.Core.Storage.AppPaths.Ensure(initialDir), RestoreDirectory = true };
        if (dlg.ShowDialog() != true) throw new UserCancelledException($"{purpose}: open dialog cancelled");
        EventLog.UserAction("File chosen for import", ("purpose", purpose), ("path", dlg.FileName));
        return dlg.FileName;
    }
    private string SaveFile(string purpose, string filter, string name, string initialDir)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = name, InitialDirectory = ObdLink.Core.Storage.AppPaths.Ensure(initialDir), RestoreDirectory = true };
        if (dlg.ShowDialog() != true) throw new UserCancelledException($"{purpose}: save dialog cancelled");
        EventLog.UserAction("File chosen for export", ("purpose", purpose), ("path", dlg.FileName));
        return dlg.FileName;
    }

    public MainViewModel(IConfirmationProvider confirm)
    {
        EventLogService log;
        try { log = LogSetup.CreateEventLog(LogDir); }
        catch (Exception ex) { log = LogSetup.CreateEventLog(Path.Combine(Path.GetTempPath(), "OBDLinkDiag", "logs")); log.Message(LogEventLevel.Warning, "App", $"Log dir {LogDir} unusable; logging to temp", ex); }
        EventLog = log;
        OpenDataFolderCommand = new("Open data folder", log, _ =>
        {
            Directory.CreateDirectory(Paths.DataDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{Paths.DataDir}\"") { UseShellExecute = true });
        });
        _confirm = new LoggingConfirmationProvider(confirm, EventLog);
        EventsPanel = new("Events", LogChannel.Events, EventLog, LogEventLevel.Verbose);
        MessagesPanel = new("Messages", LogChannel.Messages, EventLog, LogEventLevel.Information);
        TrafficPanel = new("Adapter traffic", LogChannel.Traffic, EventLog, LogEventLevel.Verbose);
        EventLog.EntryAdded += e => Application.Current?.Dispatcher.BeginInvoke(() =>
            (e.Channel switch { LogChannel.Events => EventsPanel, LogChannel.Messages => MessagesPanel, _ => TrafficPanel }).Add(e));
        EventLog.Message(LogEventLevel.Information, "App", $"Started v{typeof(MainViewModel).Assembly.GetName().Version}; data folder {Paths.DataDir} ({(Paths.IsPortable ? "portable" : "LocalAppData fallback: " + Paths.FallbackReason)})");
        if (App.Migration is { Ran: true } mig)
            EventLog.Message(mig.KeptUnverified > 0 ? LogEventLevel.Warning : LogEventLevel.Information, "Data migration",
                $"Moved {mig.Moved} file(s) from {ObdLink.Core.Storage.AppPaths.DefaultLegacyDir}; {mig.SkippedNewer} left (newer copy here); {mig.KeptUnverified} left (not verified). " + string.Join("; ", mig.Notes));

        void Err(string op, Exception ex)
        {
            var text = ErrorText.ForOperation(op, ex);
            EventLog.Message(LogEventLevel.Error, op, text, ex);
            MessageBox.Show(text, op + " - error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        // Every adapter command holds the shared bus lock so the dashboard poller never interleaves with it.
        AsyncCommand C(string name, Func<Task> f, Func<(string, object?)[]>? p = null) => new(name, EventLog, async () => { await _bus.WaitAsync(); try { await f(); } finally { _bus.Release(); } }, Err, p);
        (string, object?)[] Mod() => new (string, object?)[] { ("module", SelectedModule?.Label), ("addr", SelectedModule?.Address) };
        (string, object?)[] ModBlk() => new (string, object?)[] { ("module", SelectedModule?.Label), ("addr", SelectedModule?.Address), ("block", AsBuiltBlockNo) };

        try { _settings = AppSettings.Load(SettingsPath); }
        catch (Exception ex) { _settings = new(); EventLog.Message(LogEventLevel.Warning, "Settings", "settings.json unreadable; using defaults", ex); }
        _capRedact = _settings.RedactVinsDefault;
        foreach (var lp in new[] { EventsPanel, MessagesPanel, TrafficPanel }) if (lp != null) lp.RedactOnExport = _settings.RedactVinsDefault;
        try { ThemeManager.Apply(_settings.Theme); }
        catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Theme", "Theme load failed; using built-in default", ex); }
        Dashboard = new DashboardViewModel(EventLog, () => _settings, () => _ch, async () => _session is null ? null : await _session.ReadBatteryVoltageAsync(), _bus, LogDir, Paths.DataDir, SaveSettings);
        InitFirmwareCommands(Err);
        CheckUpdatesCommand = new AsyncCommand("Check for updates", EventLog, () => CheckForUpdatesAsync(true), Err);
        RefreshPortsCommand = C("Refresh ports", () =>
        {
            Ports.Clear(); if (_settings.SimulatorEnabled) { Ports.Add(DemoMx); Ports.Add(DemoEx); } foreach (var p in PortEnumerator.List()) Ports.Add(p);
            SelectedPort = Ports.FirstOrDefault(p => p.Name == SelectedPort?.Name) ?? Ports.FirstOrDefault(p => p.Name == _settings.Adapter.Port) ?? Ports.FirstOrDefault();
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
        ReadModuleDtcsCommand = C("Read module DTCs", async () =>
        {
            if (await EnsureModuleAsync(false) is not { } m) return;
            Dtcs.Clear();
            await OnBus(m, async () => { foreach (var d in await new UdsClient(Ch, (ushort)m.Def.TxId).ReadDtcsByStatusAsync()) Dtcs.Add(new(m.Label, d.Display, $"0x{d.Status:X2}", _dtcDb.Describe(d.Display))); });
            EventLog.Message(LogEventLevel.Information, "Read module DTCs", $"{m.Label}: {Dtcs.Count} DTCs");
        }, Mod);
        ReadAllModuleDtcsCommand = C("Read DTCs from all modules", ReadAllModuleDtcsAsync);
        ReadAllBlocksCommand = C("Read all As-Built blocks", ReadAllBlocksAsync, Mod);
        ReadAllModulesAsBuiltCommand = C("Back up As-Built of all modules", ReadAllModulesAsBuiltAsync);
        CancelBatchCommand = new RelayCommand("Cancel batch", EventLog, _ => { _batchCts?.Cancel(); BatchText = "Cancelling after the current module…"; });
        ClearModuleDtcsCommand = C("Clear module DTCs", async () =>
        {
            await Safety().RequireAsync(new[] { new ConfirmationStep("Clear DTCs", $"Clear all DTCs in {SelectedModule?.Label}? Freeze-frame data will be lost.") });
            await Uds().ClearDtcsAsync();
        }, Mod);
        ReadVinCommand = C("Read VIN", async () => { _vin = await new ObdService(Ch).ReadVinAsync() ?? _vin; EventLog.Message(LogEventLevel.Information, "Read VIN", $"VIN {_vin}"); });
        ReadAsBuiltCommand = C("Read As-Built block", ReadAsBuiltAsync, ModBlk);
        BackupAsBuiltCommand = C("Backup As-Built block", async () =>
        {
            if (await EnsureModuleAsync(true) is not { } m) return;
            string p = ""; await OnBus(m, async () => p = await AsBuilt.BackupAsync(_vin, (ushort)m.Def.TxId, new[] { Block }));
            RefreshBackups(); Info("Backup", $"Saved {p}");
        }, ModBlk);
        WriteAsBuiltCommand = C("Write + verify As-Built block", async () =>
        {
            var data = AsBuiltRows.SelectMany(r => Convert.FromHexString(r.Data.Replace(" ", ""))).ToArray();
            await AsBuilt.WriteBlockAsync(_vin, new AsBuiltBlock { Module = Module, Block = Block, Data = data });
            Info("Write", "Written and verified by read-back.");
        }, ModBlk);
        RestoreAsBuiltCommand = C("Restore As-Built from backup", async () =>
        {
            var f = OpenFile("As-Built backup", "As-Built backup|*.asbuilt.json", Paths.BackupsDir);
            await AsBuilt.RestoreAsync(f);
        });
        ImportFactoryAbCommand = C("Import factory .ab", () =>
        {
            _factory = FordAbFile.Load(OpenFile("Factory .ab", "Ford As-Built|*.ab|All files|*.*", Paths.AsBuiltDir)); RefreshBackups();
            var bad = _factory.Lines.Count(l => !l.ChecksumValid);
            FactoryInfo = $"Factory .ab: VIN …{(_factory.Vin.Length >= 6 ? _factory.Vin[^6..] : _factory.Vin)}, {_factory.AsBuiltModules.Count()} modules, {_factory.Lines.Count} lines, {bad} bad checksums";
            EventLog.Message(bad > 0 ? LogEventLevel.Warning : LogEventLevel.Information, "Import factory .ab", FactoryInfo);
            return Task.CompletedTask;
        });
        ExportFactoryAbCommand = C("Export .ab", () =>
        {
            if (_factory is null) throw new InvalidOperationException("Import a factory .ab first");
            var f = SaveFile("Factory .ab", "Ford As-Built|*.ab", "factory-redacted.ab", Paths.AsBuiltDir);
            var redact = Ask("Export", "Redact VIN (keep last 6) and module serials?");
            File.WriteAllText(f, (redact ? _factory.Redacted() : _factory).ToXml());
            EventLog.Message(LogEventLevel.Information, "Export .ab", $"Saved {f} (redacted: {redact})");
            return Task.CompletedTask;
        });
        ImportAbtCommand = C("Import .abt", () =>
        {
            var abt = ForscanAbtFile.Load(OpenFile("FORScan .abt", "FORScan As-Built backup|*.abt|All files|*.*", Paths.AsBuiltDir));
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
            var f = SaveFile("FORScan .abt", "FORScan As-Built backup|*.abt", $"{lines[0].Address.Module:X3}.abt", Paths.AsBuiltDir);
            var abt = ForscanAbtFile.Parse(ForscanAbtFile.Format(lines));
            if (_factory is { Vin.Length: 17 } && Ask("Export", "Redact VIN (keep last 6) in block data?"))
                abt = abt.Redacted(_factory.Vin);
            File.WriteAllText(f, abt.ToText());
            EventLog.Message(LogEventLevel.Information, "Export .abt", $"Saved {f}");
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
        IdentifyVehicleCommand = C("Identify vehicle", async () =>
        {
            if (_session is null) throw new InvalidOperationException("Connect first (use a DEMO port to try it without a vehicle)");
            var vpic = OnlineVinDecode ? new VpicClient() : null;
            VehicleProfile prof;
            try { prof = await new VehicleIdentifier(_session, Ch, vpic).RunAsync(new IdentifyOptions { OnlineDecode = OnlineVinDecode, SweepMsCan = MsCanAvailable },
                new Progress<string>(m => EventLog.Message(LogEventLevel.Information, "Identify vehicle", m))); }
            catch (NotFordVehicleException nf)
            {
                EventLog.Message(LogEventLevel.Warning, "Identify vehicle", nf.Message);
                VehicleInfo = nf.Message; Info("Ford products only", nf.Message); return;
            }
            if (prof.Vin.Length == 17) _vin = prof.Vin;
            _current = prof;
            if (prof.Vin.Length == 17)
            {
                var path = prof.Save(Paths.VehiclesDir);
                EventLog.Message(LogEventLevel.Information, "Identify vehicle", $"Vehicle profile saved to {path}");
                AddProfile(prof); SelectedVehicle = prof.Label;
            }
            VehicleInfo = DescribeProfile(prof);
        }, () => new (string, object?)[] { ("online", OnlineVinDecode), ("msCan", MsCanAvailable) });
        OpenMotorcraftCommand = C("Open Motorcraft As-Built site", () =>
        {
            var vin = _current?.Vin is { Length: 17 } cv ? cv : _vin.Length == 17 ? _vin : null;
            if (vin != null) { try { Clipboard.SetText(vin); } catch { } }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(MotorcraftAsBuiltUrl) { UseShellExecute = true });
            EventLog.Message(LogEventLevel.Information, "Motorcraft As-Built", $"Opened {MotorcraftAsBuiltUrl} in the browser{(vin != null ? "; VIN copied to clipboard" : "")}. Sign in, pass the captcha, download the .ab, then use Import .ab here (it is copied into {Paths.AsBuiltDir}).");
            return Task.CompletedTask;
        });
        ImportVehicleAbCommand = C("Import downloaded .ab", () =>
        {
            var src = OpenFile("Motorcraft .ab", "Ford As-Built|*.ab|All files|*.*", Paths.AsBuiltDir);
            var ab = FordAbFile.Load(src);
            var dest = Path.Combine(ObdLink.Core.Storage.AppPaths.Ensure(Paths.AsBuiltDir), Path.GetFileName(src));
            if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) File.Copy(src, dest, overwrite: true);
            _factory = ab; RefreshBackups();
            if (_current is null && ab.Vin.Length == 17) _current = _profiles.Values.FirstOrDefault(p => p.Vin == ab.Vin);
            if (_current is { } cur && (cur.Vin.Length != 17 || cur.Vin == ab.Vin))
            {
                cur.FactoryAbFile = Path.GetFileName(dest);
                if (cur.Vin.Length == 17) cur.Save(Paths.VehiclesDir);
                VehicleInfo = DescribeProfile(cur);
            }
            FactoryInfo = $"Factory .ab: VIN …{(ab.Vin.Length >= 6 ? ab.Vin[^6..] : ab.Vin)}, {ab.AsBuiltModules.Count()} modules, {ab.Lines.Count} lines (copied to {dest})";
            EventLog.Message(LogEventLevel.Information, "Import .ab", FactoryInfo);
            return Task.CompletedTask;
        });
        ConsoleSendCommand = C("Console send", async () =>
        {
            var input = ConsoleInput; ConsoleInput = "";
            var t = input.Trim().ToUpperInvariant();
            if (t is "CLS" or "CLEAR") { ConsoleOutput = ""; return; }
            if (t is "HELP" or "?" or "/?" or "/HELP" && _console is null) { ConsoleOutput += $"> {input}{Environment.NewLine}{ObdLink.Core.ConsoleCmds.ConsoleHelp.Text()}{Environment.NewLine}"; return; }
            if (_console is null) throw new InvalidOperationException("Not connected (type help for the command list)");
            var result = await _console.ExecuteAsync(input);
            ConsoleOutput += $"> {input}{Environment.NewLine}{result}{Environment.NewLine}";
            if (result.StartsWith("BLOCKED")) EventLog.Message(LogEventLevel.Warning, "Console", $"{input}: {result}");
        }, () => new (string, object?)[] { ("command", ConsoleInput) });
        _ = RefreshPortsCommand.ExecuteAsync();
        try { LoadSavedProfiles(); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Vehicles", "Could not load saved vehicle profiles", ex); }
        LoadModules();
        StartLedTimer();
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
            Paths.CapturesDir, new Progress<string>(m => EventLog.Message(LogEventLevel.Information, "Capture", m)));
        await SelectDiagProtocol();
        EventLog.Message(LogEventLevel.Information, "Capture", $"Saved {path} ({logs.Count} log files included)");
        try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
        Info("Diagnostics capture", $"Capture saved:\n{path}\n\nSend this zip back. " + (CaptureRedactVins ? "VINs were redacted." : "(It is NOT redacted: it may include your VIN and adapter serial.)"));
    }
    private AsBuiltService AsBuilt => _asBuilt ?? throw new InvalidOperationException("Not connected - click Connect first");

    private void LoadModules()
    {
        Modules.Clear();
        var defs = _profiles.TryGetValue(SelectedVehicle, out var p) && p.Modules.Count > 0 ? p.ToModuleDefinitions() : VehicleProfiles.ModulesFor(SelectedVehicle);
        if (p != null) { _current = p; if (p.Vin.Length == 17) _vin = p.Vin; VehicleInfo = DescribeProfile(p); }
        foreach (var m in defs) Modules.Add(new ModuleRow { Def = m });
        RefreshBackups();
        if (SelectedModule is null || !Modules.Contains(SelectedModule)) { _mod = null; OnPropertyChanged(nameof(SelectedModule)); if (Modules.FirstOrDefault(r => r.Address == _settings?.LastModule) is { } last) SelectedModule = last; }
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
            link.Traffic += (d, s) => { Leds.OnTraffic(d, s); EventLog.Traffic(d, s); };
            _session = new AdapterSession(link, initial: sel.Kind == PortKind.Virtual ? AdapterProfiles.OtherStn : AdapterProfiles.ForPort(sel.Kind));
            Connection = ConnectionStatus.Connecting(sel, "Initializing");
            var info = await _session.InitializeAsync();
            MsCanAvailable = _session.Profile.Has(AdapterCapabilities.MsCan);
            _ch = new ElmDiagChannel(_session);
            _console = new RawConsoleService(link) { AllowWrites = ConsoleAllowWrites, ReadOnlyMode = ReadOnlyTestMode };
            _asBuilt = new AsBuiltService(m => new UdsClient(Ch, m), Safety(), Paths.BackupsDir);
            if (_session.IsStn) await _session.SelectProtocolAsync(StnProtocol.HsCan11_500);
            Connection = ConnectionStatus.Connected(sel, info, _session.Profile);
            EventLog.Message(LogEventLevel.Information, "Connect", $"{Connection.StatusText}; adapter {AdapterName}; battery {Voltage}");
            _ = RunFirmwareCheckAsync(info.StnId);
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
        RefreshAsBuiltModules();
    }

    // ---------------- v0.2.2 adapter firmware check ----------------
    public bool FirmwareCheckEnabled { get => _settings.FirmwareCheck; set => S(() => _settings.FirmwareCheck = value, "automatic adapter firmware check", value, nameof(FirmwareCheckEnabled)); }
    private FirmwareCheckResult? _fw; private string _fwBanner = ""; private bool _fwVisible, _fwBusy; private string? _fwFolder;
    public string FirmwareBanner { get => _fwBanner; private set => Set(ref _fwBanner, value); }
    public bool FirmwareBannerVisible { get => _fwVisible; private set => Set(ref _fwVisible, value); }
    public bool FirmwareDownloading { get => _fwBusy; private set => Set(ref _fwBusy, value); }
    public bool FirmwareCanDownload => _fw?.Status == FirmwareStatus.UpdateAvailable && FirmwareFeed.IsOfficialUrl(_fw.Latest?.PackageUrl) && !_fwBusy;
    public bool FirmwareHasFolder => _fwFolder is not null;
    public string FirmwarePage => _fw?.OfficialPage ?? FirmwareFeed.FirmwareIndex;
    public string FirmwareRoot => Path.Combine(Paths.DataDir, "firmware");
    public AsyncCommand DownloadFirmwareCommand { get; private set; } = null!;
    public RelayCommand OpenFirmwareFolderCommand { get; private set; } = null!;
    public RelayCommand OpenFirmwarePageCommand { get; private set; } = null!;
    public RelayCommand DismissFirmwareCommand { get; private set; } = null!;
    private void InitFirmwareCommands(Action<string, Exception> Err)
    {
        DownloadFirmwareCommand = new AsyncCommand("Download firmware", EventLog, DownloadFirmwareAsync, Err);
        OpenFirmwareFolderCommand = new RelayCommand("Open firmware folder", EventLog, _ => { var d = _fwFolder ?? ObdLink.Core.Storage.AppPaths.Ensure(FirmwareRoot); try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{d}\"") { UseShellExecute = true }); } catch { } });
        OpenFirmwarePageCommand = new RelayCommand("Open firmware page", EventLog, _ => { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(FirmwarePage) { UseShellExecute = true }); } catch { } });
        DismissFirmwareCommand = new RelayCommand("Dismiss firmware notice", EventLog, _ => FirmwareBannerVisible = false);
    }
    private void FwChanged() { foreach (var n in new[] { nameof(FirmwareCanDownload), nameof(FirmwareHasFolder), nameof(FirmwarePage) }) OnPropertyChanged(n); }

    /// <summary>Runs after connect, in the background. Silent when offline; never blocks or flashes anything.</summary>
    private async Task RunFirmwareCheckAsync(string? sti)
    {
        if (!_settings.FirmwareCheck) return;
        try
        {
            var r = await new FirmwareChecker().CheckAsync(sti);
            _fw = r; _fwFolder = null;
            switch (r.Status)
            {
                case FirmwareStatus.UpdateAvailable:
                    var dir = FirmwareDownloader.FolderFor(FirmwareRoot, r.Installed!, r.Latest!);
                    if (File.Exists(Path.Combine(dir, "README.txt"))) _fwFolder = dir;
                    FirmwareBanner = $"Adapter firmware update available: {r.Latest!.DeviceName} installed v{r.Installed!.Version}, latest stable v{r.Latest.Version} ({r.Latest.ReleaseDate:yyyy-MM-dd}). {r.Latest.Summary}"
                        + (_fwFolder is not null ? " (package already downloaded)" : "") + " Update with OBDLink's official updater; this app never flashes firmware.";
                    FirmwareBannerVisible = true;
                    EventLog.Message(LogEventLevel.Warning, "Firmware", $"{r.Message}. Official page: {r.OfficialPage}");
                    break;
                case FirmwareStatus.Current: EventLog.Message(LogEventLevel.Information, "Firmware", r.Message); FirmwareBannerVisible = false; break;
                case FirmwareStatus.Offline: EventLog.Message(LogEventLevel.Debug, "Firmware", r.Message); break;
                default: EventLog.Message(LogEventLevel.Debug, "Firmware", r.Message); break;
            }
        }
        catch (Exception ex) { EventLog.Message(LogEventLevel.Debug, "Firmware", "Firmware check skipped: " + ex.Message); }
        FwChanged();
    }

    private async Task DownloadFirmwareAsync()
    {
        if (_fw is not { Status: FirmwareStatus.UpdateAvailable, Installed: { } inst, Latest: { } rel }) return;
        if (!FirmwareFeed.IsOfficialUrl(rel.PackageUrl)) throw new InvalidOperationException("The feed did not give an official scantool.net package URL");
        FirmwareDownloading = true; FwChanged();
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OBDLinkDiag-firmware-download");
            var d = await FirmwareDownloader.DownloadAsync(http, FirmwareRoot, inst, rel);
            _fwFolder = d.Folder;
            EventLog.Message(LogEventLevel.Information, "Firmware", $"Downloaded and verified {Path.GetFileName(d.ZipPath)} ({d.Size:N0} bytes, {d.BinName} has STNFWv header) to {d.Folder}");
            FirmwareBanner = $"Firmware v{rel.Version} package downloaded and verified: {d.Folder}. Use OBDLink's official updater to install it; this app never flashes firmware.";
        }
        finally { FirmwareDownloading = false; FwChanged(); }
    }

    // ---------------- v0.2.1 module workflow ----------------
    private string MaskedVin => _vin is { Length: 17 } v ? VinRedactor.MaskVin(v) : "NOVIN";

    /// <summary>Recomputes As-Built flags + newest backup per module, then refreshes the As-Built picker.</summary>
    private void RefreshBackups()
    {
        var found = AsBuiltBatch.FindBackups(Paths.AsBuiltDir, Paths.BackupsDir);
        var fac = _factory?.AsBuiltModules.Select(x => (uint)x).ToHashSet() ?? new();
        foreach (var r in Modules)
        {
            if (found.TryGetValue(r.Def.TxId, out var b)) { r.LastBackup = b.When; r.BackupPath = b.Path; } else { r.LastBackup = null; r.BackupPath = null; }
            r.HasAsBuilt = r.Def.InFactoryAb || fac.Contains(r.Def.TxId) || r.LastBackup != null || ModuleRow.TypicalAsBuilt.Contains(r.Acronym);
        }
        RefreshAsBuiltModules();
    }
    private void RefreshAsBuiltModules()
    {
        var keep = SelectedModule;
        AsBuiltModules.Clear();
        foreach (var r in Modules.Where(r => (AsBuiltShowAll || r.HasAsBuilt) && r.Found != "no")) AsBuiltModules.Add(r);
        if (keep != null) { _mod = null; SelectedModule = keep; }
    }
    private ModuleRow? DefaultModule(bool asBuilt)
    {
        var pool = (asBuilt ? AsBuiltModules.AsEnumerable() : Modules).ToList();
        return pool.FirstOrDefault(r => r.Address == _settings.LastModule && r.Found != "no")
            ?? pool.FirstOrDefault(r => r.Found == "YES") ?? pool.FirstOrDefault();
    }

    /// <summary>
    /// Gets everything ready for a module read instead of failing: offers to connect, scans if nothing was scanned yet,
    /// and picks the last-used (or first found) module when none is selected. Returns null (with an inline hint) if the user declines.
    /// </summary>
    private async Task<ModuleRow?> EnsureModuleAsync(bool asBuilt)
    {
        if (!await EnsureConnectedAsync()) return null;
        if (Modules.All(r => r.Found == "not scanned"))
        {
            ModuleHint = "Scanning modules first (one-time, read-only)…";
            await ScanAsync(); RefreshBackups();
        }
        if (SelectedModule is null || (asBuilt && !AsBuiltModules.Contains(SelectedModule) && !AsBuiltShowAll))
        {
            var m = DefaultModule(asBuilt);
            if (m is null) { ModuleHint = "No module answered the scan. Check the ignition is ON, then Scan again."; return null; }
            SelectedModule = m;
            ModuleHint = $"Using {m.Acronym} ({m.FullName}) - pick another module in the list above to change.";
        }
        return SelectedModule;
    }
    private async Task<bool> EnsureConnectedAsync()
    {
        if (IsConnected && _ch != null) return true;
        var port = SelectedPort?.Name ?? DemoMx.Name;
        if (MessageBox.Show($"Not connected. Connect to {port} now?", "Connect", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        { ModuleHint = "Connect first (toolbar Connect, or F5). A DEMO port works without a vehicle."; EventLog.UserAction("Auto-connect declined"); return false; }
        EventLog.UserAction("Auto-connect accepted", ("port", port));
        await ConnectAsync();
        return true;
    }
    /// <summary>Runs <paramref name="f"/> on the module's bus: MS-CAN modules switch the adapter to MS-CAN 125k (MX+ / EX) and back.</summary>
    private async Task OnBus(ModuleRow m, Func<Task> f)
    {
        if (m.Bus != Bus.MsCan) { await f(); return; }
        if (!MsCanAvailable || _session is not { IsStn: true }) throw new InvalidOperationException($"{m.Acronym} is on MS-CAN; this adapter cannot switch to MS-CAN (OBDLink MX+ / EX can).");
        await _session.SelectProtocolAsync(StnProtocol.MsCan11_125);
        try { await f(); } finally { await SelectDiagProtocol(); }
    }
    private void ShowBlocks(IEnumerable<AsBuiltBlock> blocks)
    {
        AsBuiltRows.Clear();
        foreach (var b in blocks) foreach (var l in b.Lines()) AsBuiltRows.Add(new AsBuiltRow { Addr = l.Address, Data = Convert.ToHexString(l.Data) });
    }

    private async Task ReadAllBlocksAsync()
    {
        if (await EnsureModuleAsync(true) is not { } m) return;
        List<AsBuiltBlock> blocks = new();
        await OnBus(m, async () => blocks = await AsBuiltBatch.ReadAllBlocksAsync((b, ct) => AsBuilt.ReadBlockAsync((ushort)m.Def.TxId, b, ct)));
        ShowBlocks(blocks);
        var path = await AsBuiltBatch.SaveAsync(AsBuiltBatch.DateFolder(Paths.AsBuiltDir, DateTime.Now), MaskedVin, (ushort)m.Def.TxId, blocks, DateTime.Now);
        RefreshBackups();
        ModuleHint = $"{m.Acronym}: {blocks.Count} block(s), {AsBuiltRows.Count} lines - saved {path}";
        EventLog.Message(LogEventLevel.Information, "Read all As-Built blocks", ModuleHint);
    }

    private List<ModuleRow> BatchModules(bool asBuilt) =>
        (asBuilt ? AsBuiltModules.AsEnumerable() : Modules).Where(r => r.Found == "YES" && (r.Bus != Bus.MsCan || MsCanAvailable)).ToList();

    private async Task<List<BatchItemResult>> RunBatch(string what, List<ModuleRow> mods, Func<ModuleRow, CancellationToken, Task<BatchItemResult>> work)
    {
        _batchCts = new(); BatchRunning = true; BatchPercent = 0;
        var rows = mods.ToDictionary(m => m.Def);
        try
        {
            return await AsBuiltBatch.RunAsync(mods.Select(m => m.Def).ToList(), (d, ct) => work(rows[d], ct),
                new Progress<BatchProgress>(p => { BatchPercent = p.Percent; BatchText = $"{what}: {p.Text}"; }), _batchCts.Token);
        }
        catch (OperationCanceledException) { BatchText = $"{what}: cancelled"; EventLog.Message(LogEventLevel.Warning, what, "Cancelled by user"); return new(); }
        finally { BatchRunning = false; _batchCts.Dispose(); _batchCts = null; }
    }

    private async Task ReadAllModulesAsBuiltAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        if (Modules.All(r => r.Found == "not scanned")) { BatchText = "Scanning modules first…"; await ScanAsync(); RefreshBackups(); }
        var mods = BatchModules(true);
        if (mods.Count == 0) { ModuleHint = "No As-Built module answered the scan (ignition ON?)."; return; }
        var now = DateTime.Now; var dir = AsBuiltBatch.DateFolder(Paths.AsBuiltDir, now);
        var res = await RunBatch("As-Built backup", mods, async (m, ct) =>
        {
            List<AsBuiltBlock> blocks = new();
            await OnBus(m, async () => blocks = await AsBuiltBatch.ReadAllBlocksAsync((b, c) => AsBuilt.ReadBlockAsync((ushort)m.Def.TxId, b, c), ct: ct));
            var p = await AsBuiltBatch.SaveAsync(dir, MaskedVin, (ushort)m.Def.TxId, blocks, now, ct);
            return new BatchItemResult(m.Def, true, $"{blocks.Count} block(s)", p);
        });
        RefreshBackups();
        if (res.Count == 0) return;
        var ok = res.Count(r => r.Ok);
        BatchText = $"As-Built backup: {ok} of {res.Count} modules saved to {dir}";
        EventLog.Message(ok == res.Count ? LogEventLevel.Information : LogEventLevel.Warning, "Back up As-Built of all modules",
            BatchText + "; " + string.Join("; ", res.Select(r => $"{r.Module.Acronym}: {(r.Ok ? "OK " : "FAILED ")}{r.Detail}")));
        Info("As-Built backup", BatchText + "\n\n" + string.Join("\n", res.Select(r => $"{r.Module.Acronym} 0x{r.Module.TxId:X3}: {(r.Ok ? "OK" : "failed")} - {r.Detail}")));
    }

    private async Task ReadAllModuleDtcsAsync()
    {
        if (!await EnsureConnectedAsync()) return;
        if (Modules.All(r => r.Found == "not scanned")) { BatchText = "Scanning modules first…"; await ScanAsync(); RefreshBackups(); }
        var mods = BatchModules(false);
        if (mods.Count == 0) { ModuleHint = "No module answered the scan (ignition ON?)."; return; }
        Dtcs.Clear();
        var res = await RunBatch("Module DTCs", mods, async (m, ct) =>
        {
            List<DtcRecord> list = new();
            await OnBus(m, async () => list = await new UdsClient(Ch, (ushort)m.Def.TxId).ReadDtcsByStatusAsync(ct: ct));
            foreach (var d in list) Dtcs.Add(new(m.Label, d.Display, $"0x{d.Status:X2}", _dtcDb.Describe(d.Display)));
            return new BatchItemResult(m.Def, true, $"{list.Count} DTC(s)");
        });
        if (res.Count == 0) return;
        BatchText = $"Module DTCs: {res.Count(r => r.Ok)} of {res.Count} modules read, {Dtcs.Count} DTC(s)" + (res.Any(r => !r.Ok) ? " - failed: " + string.Join(", ", res.Where(r => !r.Ok).Select(r => r.Module.Acronym)) : "");
        EventLog.Message(LogEventLevel.Information, "Read DTCs from all modules", BatchText);
    }

    private async Task ReadAsBuiltAsync()
    {
        if (await EnsureModuleAsync(true) is not { } m) return;
        var did = AsBuiltBlock.DidForBlock(Block);
        AsBuiltBlock b = null!;
        await OnBus(m, async () => b = await AsBuilt.ReadBlockAsync((ushort)m.Def.TxId, Block));
        AsBuiltRows.Clear();
        foreach (var l in b.Lines()) AsBuiltRows.Add(new AsBuiltRow { Addr = l.Address, Data = Convert.ToHexString(l.Data) });
        EventLog.Message(LogEventLevel.Information, "Read As-Built block", $"{SelectedModule?.Label} block {AsBuiltBlockNo} (DID 0x{did:X4}): {b.Data.Length} bytes, {AsBuiltRows.Count} lines");
    }
}
