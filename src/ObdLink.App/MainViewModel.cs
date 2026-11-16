using ObdLink.Core.Adapters;
using ObdLink.Core.Diagnostics;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Dashboard;
using ObdLink.Core.Dtc;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Firmware;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Security;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using ObdLink.Core.Vehicles;
using System.Windows.Threading;
using Serilog;
using Serilog.Events;

namespace ObdLink.App;

public sealed class ModuleRow : ObservableObject, ObdLink.Core.Modules.IBusTarget
{
    public required ModuleDefinition Def { get; init; }
    // IBusTarget (v0.9.1): lets BusRouter route to this module without depending on the view model.
    Bus ObdLink.Core.Modules.IBusTarget.DefaultBus => Def.Bus;
    uint ObdLink.Core.Modules.IBusTarget.TxId => Def.TxId;
    bool ObdLink.Core.Modules.IBusTarget.FoundConfirmed => Found == "YES";
    public string Acronym => Def.Acronym;
    public string FullName => ModuleAcronyms.FullName(Def.Acronym, Def.Name);
    public string Label => ModuleAcronyms.Label(Def.Acronym, Def.Name);
    public string Tooltip => ModuleAcronyms.Tooltip(Def.Acronym, Def.Name) + $"\nCAN 0x{Def.TxId:X3}/0x{Def.RxId:X3} ({Bus}{(BusOverride is null ? "" : ", found by scan")}), {Def.Confidence}";
    public string Address => Def.TxId.ToString("X3");
    /// <summary>v0.3.2: bus where the module actually answered (scan or alternate-bus retry), if it differs from the table.</summary>
    private Bus? _busOverride; public Bus? BusOverride { get => _busOverride; set { if (Set(ref _busOverride, value)) { OnPropertyChanged(nameof(Bus)); OnPropertyChanged(nameof(PickerText)); OnPropertyChanged(nameof(Tooltip)); } } }
    public Bus Bus => BusOverride ?? Def.Bus;
    /// <summary>v0.3.2: the selection box of a ComboBox showed "ObdLink.App.ModuleRow"; never fall back to the type name.</summary>
    public override string ToString() => PickerText;
    public Confidence Confidence => Def.Confidence;
    private string _found = "not scanned"; public string Found { get => _found; set { if (Set(ref _found, value)) OnPropertyChanged(nameof(PickerText)); } }
    /// <summary>Known / likely to have As-Built (DE..) blocks: factory .ab evidence, a backup, or a Ford module that normally has them.</summary>
    private bool _hasAb; public bool HasAsBuilt { get => _hasAb; set => Set(ref _hasAb, value); }
    /// <summary>v0.5.0: DE00 answered, NRC 0x31, or not checked yet this session.</summary>
    private AsBuiltSupport _abSupport = AsBuiltSupport.Unknown;
    public AsBuiltSupport AsBuiltSupport => _abSupport;
    public void SetAsBuiltSupport(AsBuiltSupport value) { if (Set(ref _abSupport, value)) OnPropertyChanged(nameof(AsBuiltSupport)); }
    private DateTime? _backup; public DateTime? LastBackup { get => _backup; set { if (Set(ref _backup, value)) { OnPropertyChanged(nameof(BackupText)); OnPropertyChanged(nameof(PickerText)); } } }
    public string? BackupPath { get; set; }
    public string BackupText => LastBackup is { } d ? "backup " + d.ToString("yyyy-MM-dd HH:mm") : "no backup";
    /// <summary>Picker line: "BCM - Body Control Module · 0x726 · HS-CAN · found · backup 2026-10-24 09:30".</summary>
    public string PickerText => $"{Acronym} - {FullName}  ·  0x{Address}  ·  {ModuleReplies.BusName(Bus)}  ·  {(Found == "YES" ? "found" : Found == "no" ? "no reply" : "not scanned")}  ·  {BackupText}";
    public static readonly HashSet<string> TypicalAsBuilt = new(StringComparer.OrdinalIgnoreCase)
    { "PCM", "TCM", "BCM", "IPC", "APIM", "ABS", "PSCM", "RCM", "PAM", "GWM", "ACM", "SCCM", "DDM", "PDM", "HVAC", "FCIM", "IPMA", "IPMB", "CCM", "TBM", "SOBDM", "OCSM", "DSM", "GPSM", "HCM", "TRM", "RFA", "ATCM" };
}

public sealed class AsBuiltRow : ObservableObject
{
    public required AsBuiltAddress Addr { get; init; }
    public string Address => Addr.ToString();
    private string _data = ""; public string Data { get => _data; set { Set(ref _data, value); OnChecksum(); } }
    /// <summary>Checksum the data needs (computed).</summary>
    private string _cs = ""; public string Checksum { get => _cs; private set => Set(ref _cs, value); }
    /// <summary>
    /// v0.6.1 audit: checksum as typed by the user (as in FORScan). Filled from the module or file when the row is loaded and NOT
    /// updated when Data is edited, so a typo in Data shows as a mismatch and blocks the write until the checksum is typed too.
    /// </summary>
    private string _entered = ""; public string EnteredChecksum { get => _entered; set { Set(ref _entered, value); OnPropertyChanged(nameof(ChecksumOk)); } }
    public bool ChecksumOk => AsBuiltWritePlan.TryParseByte(_entered, out var e) && e.ToString("X2") == _cs;
    private void OnChecksum()
    {
        try { Checksum = AsBuiltLine.ComputeChecksum(Addr, Convert.FromHexString(_data.Replace(" ", ""))).ToString("X2"); }
        catch { Checksum = "ERR"; } // partially typed hex: shown as ERR in the grid
        OnPropertyChanged(nameof(ChecksumOk));
    }
    /// <summary>A row loaded from the module or a file: the typed checksum starts equal to the line's own checksum.</summary>
    public static AsBuiltRow From(AsBuiltLine l) { var r = new AsBuiltRow { Addr = l.Address, Data = Convert.ToHexString(l.Data) }; r.EnteredChecksum = l.Checksum.ToString("X2"); return r; }
    public AsBuiltEditLine ToEdit() => new(Addr, Data, EnteredChecksum);
}

public sealed class MainViewModel : ObservableObject, IAsBuiltHost
{
    private static readonly PortInfo DemoMx = new("DEMO-MX+", "Simulated OBDLink MX+", PortKind.Virtual);
    private static readonly PortInfo DemoEx = new("DEMO-EX", "Simulated OBDLink EX", PortKind.Virtual);
    private readonly IConfirmationProvider _confirm;
    private static ObdLink.Core.Storage.AppPaths Paths => ObdLink.Core.Storage.AppPaths.Current;
    private string LogDir => Paths.LogsDir;
    public string DataDir => Paths.DataDir;
    public string DataDirShort => (Paths.IsPortable ? "Data: " : "Data (LocalAppData): ") + Paths.DataDir;
    public string DataDirTooltip => Paths.IsPortable ? $"Portable data folder next to the exe:\n{Paths.DataDir}" : $"Not portable: {Paths.FallbackReason}\nUsing {Paths.DataDir}";
    public RelayCommand OpenDataFolderCommand { get; }
    private AdapterSession? _session; private ElmDiagChannel? _ch; private RawConsoleService? _console; private AsBuiltService? _asBuilt; private BusRouter? _router;
    private string _vin = "UNKNOWNVIN";

    /// <summary>Central user-interaction / app-message / traffic log.</summary>
    public IEventLog EventLog { get; }
    public LogPanelViewModel EventsPanel { get; }
    public LogPanelViewModel MessagesPanel { get; }
    public LogPanelViewModel TrafficPanel { get; }

    public ObservableCollection<PortInfo> Ports { get; } = new();
    public ObservableCollection<string> Vehicles { get; } = new(VehicleProfiles.All);
    public ObservableCollection<ModuleRow> Modules { get; } = new();

    private PortInfo? _port; private bool _refreshingPorts, _simExplicit;
    public PortInfo? SelectedPort
    {
        get => _port;
        set
        {
            if (!Set(ref _port, value)) return;
            if (!_refreshingPorts) _simExplicit = value?.IsSimulated == true;
            if (_initializing || _refreshingPorts) return;
            EventLog.UserAction("Port selected", ("port", value?.Name), ("kind", value?.Kind));
            if (value is { IsSimulated: false }) DropStaleDemoProfile();
        }
    }
    /// <summary>v0.3.2: shown next to the port list when no real COM port was found.</summary>
    private string _portHint = ""; public string PortHint { get => _portHint; private set { Set(ref _portHint, value); OnPropertyChanged(nameof(PortHintVisible)); } }
    public bool PortHintVisible => PortHint.Length > 0;
    private string _manualPort = ""; public string ManualPort { get => _manualPort; set => Set(ref _manualPort, value); }
    /// <summary>v0.3.2: true only while connected to a DEMO (simulated) adapter; drives the "DEMO - simulated data" banner.</summary>
    public bool IsDemo => IsConnected && _connectedSim;
    private bool _connectedSim;
    private string _vehicle = VehicleProfiles.F150_2017; public string SelectedVehicle { get => _vehicle; set { if (Set(ref _vehicle, value)) { EventLog.UserAction("Vehicle selected", ("vehicle", value)); LoadModules(); } } }
    private ModuleRow? _mod; public ModuleRow? SelectedModule
    {
        get => _mod;
        set
        {
            if (!Set(ref _mod, value)) return;
            OnPropertyChanged(nameof(AsBuiltActionsEnabled));
            AsBuiltPage?.RaiseModuleCommands();
            Flash?.Refresh();
            if (value is null) return;
            EventLog.UserAction("Module selected", ("module", value.Label), ("addr", value.Address));
            ModuleHint = value.AsBuiltSupport == AsBuiltSupport.Unsupported
                ? AsBuiltNotSupportedException.Describe(value.Acronym, value.Def.TxId, 0xDE00)
                : "";
            if (!_initializing && _settings.LastModule != value.Address) { _settings.LastModule = value.Address; SaveSettings(); }
        }
    }
    /// <summary>Read, backup, and write stay available until a module has answered DE00 with NRC 0x31.</summary>
    public bool AsBuiltActionsEnabled => AsBuiltSupportPolicy.ActionsEnabled(SelectedModule?.AsBuiltSupport ?? AsBuiltSupport.Unknown);
    /// <summary>As-Built page picker: modules with As-Built blocks (or all, if the toggle is on). Selection is shared with Modules / DTCs.</summary>
    public ObservableCollection<ModuleRow> AsBuiltModules { get; } = new();
    public bool AsBuiltShowAll { get => _settings?.AsBuiltShowAllModules == true; set { _settings.AsBuiltShowAllModules = value; SaveSettings(); OnPropertyChanged(); RefreshAsBuiltModules(); } }
    private string _modHint = ""; public string ModuleHint { get => _modHint; set => Set(ref _modHint, value); }
    private bool _batchRunning; public bool BatchRunning { get => _batchRunning; private set => Set(ref _batchRunning, value); }
    private double _batchPct; public double BatchPercent { get => _batchPct; private set => Set(ref _batchPct, value); }
    private string _batchText = ""; public string BatchText { get => _batchText; private set => Set(ref _batchText, value); }
    private CancellationTokenSource? _batchCts;
    private string _tab = "Modules"; public string SelectedTab { get => _tab; set { if (value == "Dashboard") value = "Modules"; if (Set(ref _tab, value) && !string.IsNullOrEmpty(value)) { EventLog.UserAction("Tab changed", ("tab", value)); SyncDashboard(); } } }
    private readonly SemaphoreSlim _bus = new(1, 1);
    /// <summary>Dashboard window 1 (the Settings page and View menu bind here).</summary>
    public DashboardViewModel Dashboard { get; }
    /// <summary>v0.20.0: the three dashboard pop-out windows (1-3), fed by one <see cref="DashboardPoller"/>.</summary>
    public IReadOnlyList<DashboardViewModel> Dashboards { get; }
    public DashboardPoller DashboardPoller { get; }
    public LiveDataViewModel LiveData { get; }
    public RoutinesViewModel Routines { get; }
    /// <summary>Flash tab (Phase 5): module flashing through <see cref="ObdLink.Core.Flash.FlashEngine"/>.</summary>
    public FlashViewModel Flash { get; }
    /// <summary>DTCs page (split out of this class).</summary>
    public DtcViewModel Dtc { get; }
    public BusMonitorViewModel Monitor { get; }
    /// <summary>As-Built page state + offline file commands (split out of this class, step 1).</summary>
    public AsBuiltPageViewModel AsBuiltPage { get; }
    /// <summary>Raw console page (split out of this class).</summary>
    public ConsoleViewModel RawConsole { get; }
    public AsBuiltOptionsViewModel AsBuiltOptions { get; }
    /// <summary>Why adapter commands are refused right now (the bus monitor owns the adapter), or null.</summary>
    private string? AdapterBlocked() => Monitor is { Running: true } ? "The bus monitor is using the adapter. Press Stop on the Bus Monitor tab first." : null;
    /// <summary>Per-module UDS clients for this connection; keeps non-default sessions alive (3E 80) under <see cref="_bus"/>.</summary>
    private UdsSessionHost? _sessions;
    private CancellationTokenSource? _keepAlive;
    private void SyncDashboard() => DashboardPoller?.Update(IsConnected && _ch != null);
    public RelayCommand OpenDashboardCommand { get; }
    public RelayCommand OpenDashboard2Command { get; }
    public RelayCommand OpenDashboard3Command { get; }
    /// <summary>Asks the main window to open (or bring to front) dashboard window 1, 2 or 3.</summary>
    public event Action<int>? DashboardOpenRequested;

    /// <summary>
    /// v0.20.0 dashboard write cards: runs <paramref name="f"/> on module <paramref name="tx"/> holding the shared adapter lock, on the
    /// module's bus, with the connection's per-module UDS client (write gate, seed/key, session keep-alive). No bus fallback for writes.
    /// </summary>
    private async Task RunOnModuleAsync(uint tx, Func<UdsClient, Task> f)
    {
        if (AdapterBlocked() is string why) throw new InvalidOperationException(why);
        var host = _sessions ?? throw new InvalidOperationException("Not connected - click Connect first (use a DEMO port to try without a vehicle)");
        var m = Modules.FirstOrDefault(r => r.Def.TxId == tx)
            ?? throw new InvalidOperationException($"Module 0x{tx:X3} is not in this vehicle's module list. Pick the vehicle or scan first.");
        await _bus.WaitAsync();
        try { await OnBus(m, () => f(host.Client(m)), false); }
        finally { _bus.Release(); }
    }
    private string ModuleName(uint tx) => Modules.FirstOrDefault(r => r.Def.TxId == tx) is { } m ? $"{m.Label} (0x{tx:X3})" : $"module 0x{tx:X3}";
    private string _bottomTab = "Events"; public string SelectedLogTab { get => _bottomTab; set { if (Set(ref _bottomTab, value) && !string.IsNullOrEmpty(value)) EventLog.UserAction("Log tab changed", ("tab", value)); } }

    private ConnectionStatus _conn = ConnectionStatus.Disconnected;
    public ConnectionStatus Connection { get => _conn; private set { _conn = value; OnPropertyChanged(); OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(Voltage)); OnPropertyChanged(nameof(AdapterName)); OnPropertyChanged(nameof(Capabilities)); OnPropertyChanged(nameof(IsConnected)); OnPropertyChanged(nameof(IsDemo)); OnPropertyChanged(nameof(ConnState)); SyncDashboard(); } }
    public string Status => Connection.StatusText;
    public string Voltage => Connection.Voltage;
    public string AdapterName => Connection.AdapterName;
    public string Capabilities => Connection.Capabilities;
    public bool IsConnected => Connection.State == ConnectionState.Connected;
    private string _baud = ""; public string Baud { get => _baud; set => Set(ref _baud, value); }
    private bool _ms; public bool MsCanAvailable { get => _ms; set => Set(ref _ms, value); }
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
    public string AdapterDefaultPort { get => _settings.Adapter.Port ?? ""; set => S(() => _settings.Adapter.Port = string.IsNullOrWhiteSpace(value) ? "" : value.Trim(), "default port", value, nameof(AdapterDefaultPort)); }
    public string AdapterKind { get => _settings.Adapter.Kind; set => S(() => _settings.Adapter.Kind = value, "adapter kind", value, nameof(AdapterKind)); }
    public int AdapterTimeoutMs { get => _settings.Adapter.TimeoutMs; set => S(() => _settings.Adapter.TimeoutMs = value, "adapter timeout ms", value, nameof(AdapterTimeoutMs)); }
    public bool BatchObdRequests { get => _settings.Dashboard.BatchObdRequests; set => S(() => _settings.Dashboard.BatchObdRequests = value, "multi-PID OBD-II requests (applies when polling restarts)", value, nameof(BatchObdRequests)); }
    public int DashboardRefreshMs { get => _settings.Dashboard.RefreshMs; set => S(() => _settings.Dashboard.RefreshMs = value, "dashboard refresh ms (applies when the dashboard restarts)", value, nameof(DashboardRefreshMs)); }
    public bool DashboardFordPids { get => _settings.Dashboard.FordEnhancedPids; set => S(() => _settings.Dashboard.FordEnhancedPids = value, "Ford enhanced PIDs (Mode 22, UNVERIFIED)", value, nameof(DashboardFordPids)); }
    public bool DashboardEstimateGear { get => _settings.Dashboard.EstimateGear; set => S(() => _settings.Dashboard.EstimateGear = value, "estimate gear from RPM/speed", value, nameof(DashboardEstimateGear)); }
    /// <summary>Low tire pressure threshold shown in the Settings pressure unit; stored in kPa.</summary>
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
    public static string InformationalVersion => typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "?";
    public static string Revision { get { var v = InformationalVersion; var i = v.IndexOf('+'); return i < 0 ? "unknown" : v[(i + 1)..]; } }
    public static string BuildDate => typeof(MainViewModel).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false).OfType<System.Reflection.AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "BuildDateUtc")?.Value is { } d ? d + " UTC" : "unknown";
    public string AboutText => $"OBDLink Diag {AppVersion}\nRevision {Revision}, built {BuildDate}\n\nOBDLink EX / MX+ Scan / Diagnose / Repair (SDR) for Ford vehicles (Ford-only), for personal use on your own vehicle: dashboard (read-only Mode 01 / 22), module scan, DTCs, As-Built backup/compare, raw console, firmware check.\n\nLicense: PolyForm Noncommercial License 1.0.0 (source-available; personal and other noncommercial use). Portable, no installer.\nSupported OS: Windows 10 1607+ / Windows 11 (x64 and ARM64). Windows 7 / 8.1 not supported.\n\nCredits:\n  Built by OBDLink MX+ Windows App (Grok Bot) for Dale Wilcox.\n  As-Built / PID community data: Livnitup, f150forum.com, jdunk54nl.\n\nhttps://github.com/{UpdateChecker.Repo}\nData folder: {DataDir}\n\nUse at your own risk. Read-only mode is ON by default. The exe is unsigned.";

    private void SaveSettings() { try { _settings.Save(SettingsPath); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Settings", "Could not save settings", ex); } }
    private WriteGate Gate => new(() => _settings.ReadOnlyTestMode);
    public AsyncCommand DiagnosticsCaptureCommand { get; }
    public AsyncCommand DiscoveryCommand { get; }

    public AsyncCommand RefreshPortsCommand { get; }
    public AsyncCommand UseManualPortCommand { get; }
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand DisconnectCommand { get; }
    public AsyncCommand ScanCommand { get; }
    public AsyncCommand ReadVinCommand { get; }
    public RelayCommand CancelBatchCommand { get; }
    /// <summary>v0.16.0: restores the backup taken before the last verified write of the selected module.</summary>
    /// <summary>v0.16.0: verified As-Built changes (newest first), from asbuilt-history.jsonl in the backups folder.</summary>
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
    private string _vehInfo = "No vehicle identified yet. Connect, then click Identify vehicle (read-only)."; public string VehicleInfo { get => _vehInfo; set { Set(ref _vehInfo, value); RefreshModuleIds(); } }

    // ---------------- v0.9.0 Phase 1: module identification grid + Mode 6 ----------------
    public ObservableCollection<ModuleIdRow> ModuleIds { get; } = new();
    private void RefreshModuleIds()
    {
        ModuleIds.Clear();
        if (_current is null) return;
        foreach (var r in ObdLink.Core.Vehicles.ModuleIds.FromProfile(_current)) ModuleIds.Add(r);
    }
    public RelayCommand ExportModuleIdsCommand { get; private set; } = null!;
    public AsyncCommand ReadMode6Command { get; private set; } = null!;

    private void InitPhase1(Action<string, Exception> Err, Func<string, Func<Task>, AsyncCommand> c)
    {
        ExportModuleIdsCommand = new RelayCommand("Export module IDs", EventLog, _ =>
        {
            if (ModuleIds.Count == 0) { Info("Export module IDs", "Identify the vehicle first (Vehicle page)."); return; }
            var f = SaveFile("Module IDs", "CSV|*.csv", $"modules-{(_current is null ? "vehicle" : SafePaths.Token(_current.VinRedacted))}.csv", Paths.ExportsDir);
            var redact = !Ask("Export module IDs", "Keep module serial numbers (F18C) in the file? Choose No to blank them (recommended if you will share it).");
            File.WriteAllText(f, ObdLink.Core.Vehicles.ModuleIds.Csv(ModuleIds, redact));
            EventLog.Message(LogEventLevel.Information, "Export module IDs", $"Saved {f} ({ModuleIds.Count} modules, serials {(redact ? "blanked" : "kept")})");
        });
        ReadMode6Command = c("Read Mode 6 test results", async () =>
        {
            if (_session is not null && _session.CurrentBus != Bus.HsCan) await SelectDiagProtocol();
            var res = await Mode6.ReadAllAsync(Ch, new Progress<string>(m => EventLog.Message(LogEventLevel.Debug, "Mode 6", m)));
            var text = Mode6.Report(res);
            EventLog.Message(res.Any(r => !r.Pass) ? LogEventLevel.Warning : LogEventLevel.Information, "Mode 6", text);
            Info("Mode 6 test results (06)", text);
        });
    }
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
    private string DescribeProfile(VehicleProfile p) => DescribeProfile(p, AsBuiltPage.Factory);
    private static string DescribeProfile(VehicleProfile p, FordAbFile? factory)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{p.DisplayName}   VIN {p.VinRedacted}");
        if (p.Offline is { } o) sb.AppendLine($"Offline: {o.Manufacturer}, {o.Country}, MY {o.ModelYear}, plant {o.PlantCode} = {o.Plant}, check digit {(o.CheckDigitOk ? "OK" : "MISMATCH")}");
        if (p.Online is { Count: > 0 } on) sb.AppendLine("NHTSA vPIC: " + string.Join(", ", on.Where(k => k.Key is not ("ErrorCode" or "ErrorText")).Select(k => $"{k.Key}={k.Value}")));
        sb.AppendLine($"{p.Modules.Count} module(s):");
        foreach (var m in p.Modules)
            sb.AppendLine($"  {m.Bus,-5} 0x{m.TxId:X3} {m.Acronym,-6} {m.Name}" + string.Concat(m.Dids.Where(d => d.Key != "F190").Select(d => $"  {d.Key}={d.Value}")) + (m.HasAsBuiltDe00 ? "  [As-Built]" : ""));
        if (p.FactoryAbFile != null) sb.AppendLine($"Factory .ab: {p.FactoryAbFile}");
        // W5 v1: installed module IDs vs the factory record, offline; differences only, never "newer".
        if (factory is not null && p.Modules.Any(m => m.Dids.Count > 0) && (factory.Vin.Length != 17 || p.Vin.Length != 17 || factory.Vin == p.Vin))
            sb.AppendLine(CalibrationCheck.Report(CalibrationCheck.Compare(p.Modules, factory)));
        return sb.ToString();
    }

    /// <summary>Yes/No dialog that is recorded in the event log (shown / accepted / canceled).</summary>
    private bool Ask(string title, string message, MessageBoxImage icon = MessageBoxImage.Question)
    {
        var op = EventLog.Begin("Confirmation dialog", ("title", title));
        var ok = DialogService.YesNo(message, title, icon);
        if (ok) op.Succeeded("Accepted: " + title); else op.Cancelled("User answered No");
        return ok;
    }
    private void Info(string title, string message) { EventLog.UserAction("Info dialog shown", ("title", title)); DialogService.Info(message, title); }

    // v0.1.5: every dialog starts in a Data\<subfolder> (never the working directory) and logs the full chosen path.
    private string OpenFile(string purpose, string filter, string initialDir)
    {
        var dlg = new OpenFileDialog { Filter = filter, InitialDirectory = ObdLink.Core.Storage.AppPaths.Ensure(initialDir), RestoreDirectory = true };
        if (DialogService.ShowFileDialog(dlg) != true) throw new UserCancelledException($"{purpose}: open dialog canceled");
        EventLog.UserAction("File chosen for import", ("purpose", purpose), ("path", dlg.FileName));
        return dlg.FileName;
    }
    private string SaveFile(string purpose, string filter, string name, string initialDir)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = name, InitialDirectory = ObdLink.Core.Storage.AppPaths.Ensure(initialDir), RestoreDirectory = true };
        if (DialogService.ShowFileDialog(dlg) != true) throw new UserCancelledException($"{purpose}: save dialog canceled");
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
            DialogService.OpenExternal("explorer.exe", $"\"{Paths.DataDir}\"");
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
            DialogService.Error(text, op + " - error");
        }
        // Every adapter command holds the shared bus lock so the dashboard poller never interleaves with it.
        // NRC 0x31 on an As-Built DID is "no As-Built": the dialog is shown after the lock is released, and the command is not logged as Failed.
        AsyncCommand C(string name, Func<Task> f, Func<(string, object?)[]>? p = null, Func<bool>? can = null) => new(name, EventLog, async () =>
        {
            AsBuiltNotSupportedException? none = null;
            // v0.13.0: the bus monitor holds the adapter. Connect / Disconnect stop it first; anything else is refused instead of waiting.
            if (name is "Connect" or "Disconnect") await Monitor.StopAsync();
            else if (AdapterBlocked() is string why) throw new InvalidOperationException(why);
            await _bus.WaitAsync();
            try { await f(); }
            catch (AsBuiltNotSupportedException ex) { none = ex; }
            finally { _bus.Release(); }
            if (none != null) NoticeNoAsBuilt(none);
        }, Err, p, can);
        bool AsBuiltCan() => AsBuiltActionsEnabled;

        try { _settings = AppSettings.Load(SettingsPath); }
        catch (Exception ex) { _settings = new(); EventLog.Message(LogEventLevel.Warning, "Settings", "settings.json unreadable; using defaults", ex); }
        if (_settings.ApplyStartupSafety()) { EventLog.Message(LogEventLevel.Warning, "Settings", "Read-only test mode was OFF in settings.json; turned back ON at startup (writes are enabled per session only)"); try { SaveSettings(); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Settings", "Could not save settings.json", ex); } }
        _capRedact = _settings.RedactVinsDefault;
        foreach (var lp in new[] { EventsPanel, MessagesPanel, TrafficPanel }) if (lp != null) lp.RedactOnExport = _settings.RedactVinsDefault;
        try { ThemeManager.Apply(_settings.Theme); }
        catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Theme", "Theme load failed; using built-in default", ex); }
        DashboardPoller = new DashboardPoller(EventLog, () => _settings, () => _ch, async () => _session is null ? null : await _session.ReadBatteryVoltageAsync(), _bus, LogDir, Paths.DataDir, SaveSettings);
        var actionHost = new DashActionHost(RunOnModuleAsync, Safety, ModuleName, Err);
        Dashboards = Enumerable.Range(1, DashboardWindows.Count)
            .Select(slot => new DashboardViewModel(DashboardPoller, slot, EventLog, () => _settings, Paths.DataDir, SaveSettings, actionHost)).ToList();
        Dashboard = Dashboards[0];
        LiveData = new LiveDataViewModel(EventLog, () => _settings, () => _ch, _bus, LogDir, Paths.DataDir);
        Routines = new RoutinesViewModel(EventLog, () => _sessions, () => SelectedModule, (m, f, fallback) => OnBus(m, f, fallback), _bus, Safety, Err, AdapterBlocked,
            tx => { if (Modules.FirstOrDefault(r => r.Def.TxId == tx) is { } row) SelectedModule = row; },
            new RoutineJournal(Path.Combine(Paths.DataDir, "routines")),
            tx => Modules.FirstOrDefault(r => r.Def.TxId == tx) is not { } row ? false   // not in this vehicle's module list
                : row.Found == "YES" ? true : row.Found == "no" ? false : null);          // KB-R3: null = not scanned yet
        Flash = new FlashViewModel(EventLog, () => _sessions, () => SelectedModule, (m, f, fallback) => OnBus(m, f, fallback), _bus, Safety, () => _session,
            () => SelectedPort, () => _asBuilt, () => _vin, Path.Combine(Paths.DataDir, "flash"), new WindowsHostPower(), Err, AdapterBlocked);
        Monitor = new BusMonitorViewModel(EventLog, () => _session, _bus, () => MsCanAvailable && _session is { IsStn: true }, LogDir, MonitorStarting, Err);
        Dtc = new DtcViewModel(DtcDatabase.LoadEmbedded(), EventLog, (n, f, p) => C(n, f, p), () => Ch,
            async () => { if (_session is not null && _session.CurrentBus != Bus.HsCan) await SelectDiagProtocol(); },
            () => SelectedModule, EnsureModuleAsync, tx => Modules.FirstOrDefault(r => r.Def.TxId == tx),
            (m, f, fallback) => OnBus(m, f, fallback), Safety, (what, work) => ModuleBatchAsync(what, false, work), t => BatchText = t, Info,
            Path.Combine(Paths.DataDir, "dtc-snapshots"));
        LiveData.Blocked = AdapterBlocked;
        RawConsole = new ConsoleViewModel(EventLog, (n, f, p) => C(n, f, p), () => _console);
        AsBuiltPage = new AsBuiltPageViewModel(EventLog, this, (n, f, p, can) => C(n, f, p, can), OpenFile, SaveFile, (t, m) => Ask(t, m),
            () => Paths.AsBuiltDir, () => Paths.BackupsDir, RefreshBackups, AsBuiltCan);
        AsBuiltOptions = new AsBuiltOptionsViewModel(EventLog, () => AsBuiltPage.AsBuiltRows.ToList());
        OpenDashboardCommand = new("Open dashboard", EventLog, _ => DashboardOpenRequested?.Invoke(1));
        OpenDashboard2Command = new("Open dashboard 2", EventLog, _ => DashboardOpenRequested?.Invoke(2));
        OpenDashboard3Command = new("Open dashboard 3", EventLog, _ => DashboardOpenRequested?.Invoke(3));
        Firmware = new FirmwareViewModel(EventLog, () => _settings, Paths.DataDir, () => IsConnected, () => _connectedSim, DisconnectAsync, Info, Err, SaveSettings);
        InitSeedKey(Err);
        CheckUpdatesCommand = new AsyncCommand("Check for updates", EventLog, () => CheckForUpdatesAsync(true), Err);
        RefreshPortsCommand = C("Refresh ports", () =>
        {
            // v0.3.2: real ports first, DEMO last; the simulator is never picked automatically (it used to be first and won FirstOrDefault).
            var prev = SelectedPort?.Name; var real = PortEnumerator.List();
            var manual = Ports.Where(p => p.FriendlyName == "Manual entry" && !real.Any(r => r.Name.Equals(p.Name, StringComparison.OrdinalIgnoreCase))).ToList();
            _refreshingPorts = true;
            try
            {
                Ports.Clear(); foreach (var p in real.Concat(manual)) Ports.Add(p);
                if (_settings.SimulatorEnabled) { Ports.Add(DemoMx); Ports.Add(DemoEx); }
                SelectedPort = PortEnumerator.ChooseDefault(Ports, prev, string.IsNullOrWhiteSpace(_settings.Adapter.Port) ? _settings.Adapter.LastPort : _settings.Adapter.Port, _simExplicit);
            }
            finally { _refreshingPorts = false; }
            if (!_initializing && SelectedPort is { IsSimulated: false }) DropStaleDemoProfile();
            PortHint = real.Count == 0 && manual.Count == 0 ? PortEnumerator.NoPortsHint : "";
            EventLog.Message(LogEventLevel.Information, "Ports", $"{real.Count} real COM port(s): {(real.Count == 0 ? "none" : string.Join(", ", real.Select(p => p.Label)))}; selected {SelectedPort?.Name ?? "(none)"}");
            return Task.CompletedTask;
        });
        UseManualPortCommand = C("Use manual COM port", () =>
        {
            var n = PortEnumerator.NormalizePort(ManualPort) ?? throw new InvalidOperationException($"\"{ManualPort}\" is not a COM port. Type e.g. COM5 (see Device Manager > Ports (COM & LPT)).");
            var p = Ports.FirstOrDefault(x => x.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
            if (p is null) { p = new PortInfo(n, "Manual entry", PortKind.Unknown); Ports.Insert(Ports.TakeWhile(x => !x.IsSimulated).Count(), p); }
            SelectedPort = p; ManualPort = n; PortHint = "";
            return Task.CompletedTask;
        }, () => new (string, object?)[] { ("port", ManualPort) });
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
        InitPhase1(Err, (n, f) => C(n, f));
        CancelBatchCommand = new RelayCommand("Cancel batch", EventLog, _ => { _batchCts?.Cancel(); BatchText = "Canceling after the current module…"; });
        ReadVinCommand = C("Read VIN", async () => { _vin = await new ObdService(Ch).ReadVinAsync() ?? _vin; EventLog.Message(LogEventLevel.Information, "Read VIN", $"VIN {_vin}"); });
        IdentifyVehicleCommand = C("Identify vehicle", async () =>
        {
            if (_session is null) throw new InvalidOperationException("Connect first (use a DEMO port to try it without a vehicle)");
            using var vpic = OnlineVinDecode ? new VpicClient() : null;
            VehicleProfile prof;
            try { prof = await new VehicleIdentifier(_session, Ch, vpic).RunAsync(new IdentifyOptions { OnlineDecode = OnlineVinDecode, SweepMsCan = MsCanAvailable },
                new Progress<string>(m => EventLog.Message(LogEventLevel.Information, "Identify vehicle", m))); }
            catch (NotFordVehicleException nf)
            {
                EventLog.Message(LogEventLevel.Warning, "Identify vehicle", nf.Message);
                VehicleInfo = nf.Message; Info("Ford products only", nf.Message); return;
            }
            prof.IsDemo = _connectedSim;
            if (prof.Vin.Length == 17) _vin = prof.Vin;
            _current = prof;
            var apply = VehicleSelection.AfterIdentify(SelectedVehicle, prof.Label, vinRead: prof.Vin.Length == 17, moduleCount: prof.Modules.Count);
            if (apply.SaveProfile)
            {
                var path = prof.Save(Paths.VehiclesDir);
                EventLog.Message(LogEventLevel.Information, "Identify vehicle", $"Vehicle profile saved to {path}");
                AddProfile(prof);
            }
            if (apply.SelectLabel is { } label)
            {
                if (SelectedVehicle != label) SelectedVehicle = label;
                else if (apply.RebuildModules) LoadModules();
            }
            else if (apply.RebuildModules)
            {
                Modules.Clear();
                foreach (var def in prof.ToModuleDefinitions()) Modules.Add(MakeRow(def));
                ApplyKnownAsBuilt(prof);
                RefreshBackups();
                if (SelectedModule is null || !Modules.Contains(SelectedModule)) { _mod = null; OnPropertyChanged(nameof(SelectedModule)); }
            }
            VehicleInfo = DescribeProfile(prof);
        }, () => new (string, object?)[] { ("online", OnlineVinDecode), ("msCan", MsCanAvailable) });
        OpenMotorcraftCommand = C("Open Motorcraft As-Built site", () =>
        {
            var vin = _current?.Vin is { Length: 17 } cv ? cv : _vin.Length == 17 ? _vin : null;
            if (vin != null) { try { Clipboard.SetText(vin); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Vehicle", "Could not copy VIN to the clipboard: " + ex.Message); } }
            DialogService.OpenExternal(MotorcraftAsBuiltUrl);
            EventLog.Message(LogEventLevel.Information, "Motorcraft As-Built", $"Opened {MotorcraftAsBuiltUrl} in the browser{(vin != null ? "; VIN copied to clipboard" : "")}. Sign in, pass the captcha, download the .ab, then use Import .ab here (it is copied into {Paths.AsBuiltDir}).");
            return Task.CompletedTask;
        });
        ImportVehicleAbCommand = C("Import downloaded .ab", () =>
        {
            var src = OpenFile("Motorcraft .ab", "Ford As-Built|*.ab|All files|*.*", Paths.AsBuiltDir);
            var ab = FordAbFile.Load(src);
            var dest = Path.Combine(ObdLink.Core.Storage.AppPaths.Ensure(Paths.AsBuiltDir), Path.GetFileName(src));
            if (!string.Equals(Path.GetFullPath(src), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase)) File.Copy(src, dest, overwrite: true);
            AsBuiltPage.SetFactory(ab);
            if (_current is null && ab.Vin.Length == 17) _current = _profiles.Values.FirstOrDefault(p => p.Vin == ab.Vin);
            if (_current is { } cur && (cur.Vin.Length != 17 || cur.Vin == ab.Vin))
            {
                cur.FactoryAbFile = Path.GetFileName(dest);
                if (cur.Vin.Length == 17) cur.Save(Paths.VehiclesDir);
                VehicleInfo = DescribeProfile(cur, ab);
            }
            AsBuiltPage.FactoryInfo = $"Factory .ab: VIN …{AsBuiltPageViewModel.VinTail(ab.Vin)}, {ab.AsBuiltModules.Count()} modules, {ab.Lines.Count} lines (copied to {dest})";
            EventLog.Message(LogEventLevel.Information, "Import .ab", AsBuiltPage.FactoryInfo);
            return Task.CompletedTask;
        });
        _ = RefreshPortsCommand.ExecuteAsync();
        try { LoadSavedProfiles(); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Vehicles", "Could not load saved vehicle profiles", ex); }
        LoadModules();
        StartLedTimer();
        _initializing = false;
        if (SelectedPort is { IsSimulated: false }) DropStaleDemoProfile();
    }

    private ElmDiagChannel Ch => _ch ?? throw new InvalidOperationException("Not connected - click Connect first (use a DEMO port to try without a vehicle)");
    private ushort Module => (ushort)(SelectedModule?.Def.TxId ?? throw new InvalidOperationException("Select a module in the module list first"));
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
        try { DialogService.OpenExternal("explorer.exe", $"/select,\"{path}\""); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Explorer", "Could not open Explorer: " + ex.Message); }
        Info("Diagnostics capture", $"Capture saved:\n{path}\n\nSend this zip back. " + (CaptureRedactVins ? "VINs were redacted." : "(It is NOT redacted: it may include your VIN and adapter serial.)"));
    }
    private AsBuiltService AsBuilt => _asBuilt ?? throw new InvalidOperationException("Not connected - click Connect first");

    /// <summary>
    /// v0.3.2: on every connect the module list is rebuilt (scan results and bus findings from the previous adapter are dropped).
    /// A DEMO vehicle profile is never kept for a real adapter: the selection falls back to the built-in Ford table, so the real truck
    /// is scanned with real addresses, not the 4 simulated modules.
    /// </summary>
    private void ResetVehicleForAdapter(bool simulated)
    {
        if (VehicleSelection.DropDemoProfile(realConnectPath: !simulated, profileLooksDemo: _profiles.TryGetValue(SelectedVehicle, out var p) && p.LooksDemo))
        {
            EventLog.Message(LogEventLevel.Warning, "Vehicles", $"Real adapter connected: not using DEMO profile '{p!.Label}'. Run Identify vehicle to build this truck's module list.");
            _current = null; _vin = "UNKNOWNVIN"; VehicleInfo = "Real adapter connected. Click Identify vehicle (read-only) to read this truck's VIN and modules.";
            if (SelectedVehicle != VehicleProfiles.F150_2017) { SelectedVehicle = VehicleProfiles.F150_2017; return; } // setter rebuilds
        }
        LoadModules();
    }
    /// <summary>A real COM port must not keep a profile captured on the simulator, even before Connect.</summary>
    private void DropStaleDemoProfile()
    {
        if (!_profiles.TryGetValue(SelectedVehicle, out var p)) return;
        if (!VehicleSelection.DropDemoProfile(realConnectPath: true, profileLooksDemo: p.LooksDemo)) return;
        EventLog.Message(LogEventLevel.Warning, "Vehicles", $"Real port selected: not using DEMO profile '{p.Label}'. Connect, then Identify vehicle to build this truck's module list.");
        _current = null; _vin = "UNKNOWNVIN";
        VehicleInfo = "A real COM port is selected. Connect, then click Identify vehicle (read-only) to rebuild this truck's module list. The DEMO profile was cleared.";
        if (SelectedVehicle != VehicleProfiles.F150_2017) SelectedVehicle = VehicleProfiles.F150_2017;
        else LoadModules();
    }
    private ModuleRow MakeRow(ModuleDefinition def)
    {
        var row = new ModuleRow { Def = def };
        if (ModuleBusMemory.ShouldPersist(_connectedSim) && ModuleBusMemory.Recall(_settings.ModuleBuses, def.TxId) is { } bus && bus != def.Bus)
            row.BusOverride = bus;
        return row;
    }
    /// <summary>Trust HasAsBuiltDe00 only when this profile recorded at least one readable DE00. Missing JSON bools are false and must not hide every module.</summary>
    private void ApplyKnownAsBuilt(VehicleProfile? profile)
    {
        if (profile == null || !profile.Modules.Any(m => m.HasAsBuiltDe00)) return;
        foreach (var row in Modules)
        {
            var id = profile.Modules.FirstOrDefault(m => m.TxId == row.Def.TxId);
            if (id != null) row.SetAsBuiltSupport(AsBuiltSupportPolicy.FromDe00(id.HasAsBuiltDe00));
        }
    }
    private void LoadModules()
    {
        Modules.Clear();
        var profile = _profiles.TryGetValue(SelectedVehicle, out var p) && p.Modules.Count > 0 ? p : null;
        var defs = profile != null ? profile.ToModuleDefinitions() : VehicleProfiles.ModulesFor(SelectedVehicle);
        if (profile != null) { _current = profile; if (profile.Vin.Length == 17) _vin = profile.Vin; VehicleInfo = DescribeProfile(profile); }
        foreach (var m in defs) Modules.Add(MakeRow(m));
        ApplyKnownAsBuilt(profile);
        RefreshBackups();
        if (SelectedModule is null || !Modules.Contains(SelectedModule)) { _mod = null; OnPropertyChanged(nameof(SelectedModule)); if (Modules.FirstOrDefault(r => r.Address == _settings?.LastModule) is { } last) SelectedModule = last; }
    }

    private async Task DisconnectAsync()
    {
        await Monitor.StopAsync();
        LiveData.Stop();
        StopSessions();
        if (_session != null) await _session.Link.DisposeAsync();
        _session = null; _ch = null; _console = null; _asBuilt = null; _router = null;
        Connection = ConnectionStatus.Disconnected; Baud = ""; MsCanAvailable = false;
        EventLog.Message(LogEventLevel.Information, "Disconnect", "Disconnected");
    }

    private async Task ConnectAsync()
    {
        if (_session != null) await DisconnectAsync();
        // v0.3.2: never fall back to the simulator; DEMO must be chosen in the port list.
        var sel = SelectedPort ?? throw new InvalidOperationException(Ports.Any(p => !p.IsSimulated) ? "Pick a COM port first." : PortEnumerator.NoPortsHint);
        _connectedSim = sel.IsSimulated;
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
            _console = new RawConsoleService(link, _session.Invalidate) { AllowWrites = RawConsole.ConsoleAllowWrites, ReadOnlyMode = ReadOnlyTestMode };
            _asBuilt = new AsBuiltService(m => new UdsClient(Ch, m) { SeedKey = _seedKey }, Safety(), Paths.BackupsDir);
            _router = new BusRouter(_session, () => CanSwitchBus, (lvl, ch, msg) => EventLog.Message(lvl, ch, msg));
            StartSessions();
            ApplySeedKey();
            if (_session.IsStn) await _session.SelectProtocolAsync(StnProtocol.HsCan11_500);
            Connection = ConnectionStatus.Connected(sel, info, _session.Profile);
            EventLog.Message(LogEventLevel.Information, "Connect", $"{Connection.StatusText}; adapter {AdapterName}; battery {Voltage}");
            // v0.4.0: DEMO (including a first-run simulated session) never queries the firmware feed.
            _ = Firmware.OnConnectedAsync(info.StnId);
            ResetVehicleForAdapter(sel.IsSimulated);
            if (!sel.IsSimulated && _settings.Adapter.LastPort != sel.Name) { _settings.Adapter.LastPort = sel.Name; SaveSettings(); }
        }
        catch (Exception ex)
        {
            StopSessions();
            if (_session != null) { try { await _session.Link.DisposeAsync(); } catch (Exception closeEx) { EventLog.Message(LogEventLevel.Debug, "Adapter", "Closing the old link failed: " + closeEx.Message); } }
            _session = null; _ch = null; _console = null; _asBuilt = null; _router = null;
            Connection = ConnectionStatus.Failed(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// New connection: per-module clients share the adapter channel, the write gate and the seed/key providers, and route through
    /// the bus router without fallback. The keep-alive loop runs in the background and takes <see cref="_bus"/> only when a
    /// module in a non-default session is due. Called with the bus lock held (from Connect).
    /// </summary>
    private void StartSessions()
    {
        StopSessions();
        var router = _router!;
        var host = new UdsSessionHost(tx => new UdsClient(Ch, tx) { SeedKey = _seedKey, Gate = Gate }, _bus,
            (t, work, ct) => router.RunAsync(t, work, allowFallback: false, ct: ct));
        host.SessionLost += (tx, why) =>
        {
            EventLog.Message(LogEventLevel.Warning, "Session", $"0x{tx:X3}: keep-alive failed ({why}); the module is back in its default session");
            Application.Current?.Dispatcher.BeginInvoke(() => Routines.NoteSessionLost(tx, why));
        };
        _sessions = host;
        _keepAlive = new CancellationTokenSource();
        _ = host.RunAsync(TimeProvider.System, _keepAlive.Token,
            onError: ex => EventLog.Message(LogEventLevel.Warning, "Session", "Keep-alive loop error", ex));
        Application.Current?.Dispatcher.BeginInvoke(() => { Routines.Refresh(); Flash.Refresh(); Monitor.AdapterChanged(); });
    }

    /// <summary>
    /// The bus monitor is about to take the adapter for as long as it runs: stop live-data polling, and release kept-alive UDS
    /// sessions (no keep-alive can be sent while monitoring, so the modules drop to their default session after 5 s).
    /// </summary>
    private void MonitorStarting()
    {
        LiveData.Stop();
        if (_sessions is { } h && h.OpenSessions.Count > 0)
            EventLog.Message(LogEventLevel.Warning, "Bus monitor", "Released kept-alive sessions (" + string.Join(", ", h.OpenSessions.Select(o => $"0x{o.TxId:X3} {o.Session}")) + "): modules return to their default session while the monitor runs");
        _sessions?.Clear();
        Routines.Refresh();
        Flash.Refresh();
    }

    /// <summary>
    /// Stops the keep-alive loop and forgets the clients without traffic: the link is about to close, and each module returns to
    /// its default session on its own after S3 (5 s) without TesterPresent.
    /// </summary>
    private void StopSessions()
    {
        _keepAlive?.Cancel(); _keepAlive?.Dispose(); _keepAlive = null;
        _sessions?.Clear(); _sessions = null;
        Application.Current?.Dispatcher.BeginInvoke(() => { Routines.Refresh(); Flash.Refresh(); Monitor.AdapterChanged(); });
    }

    private async Task ScanAsync()
    {
        if (_session is null) throw new InvalidOperationException("Not connected");
        var session = _session;
        var byTx = Modules.ToDictionary(r => r.Def.TxId);
        // v0.17.0: scan + alternate-bus retries live in Core (ModuleScanFlow, tested); this method only updates the grid.
        var outcomes = await ModuleScanFlow.RunAsync((mods, p, ct) => new ModuleScanner(session, Ch).ScanAsync(mods, p, ct),
            Modules.Select(r => new ScanTarget(r.Def, r.Bus)).ToList(), CanSwitchBus,
            new Progress<ScanResult>(r => { if (byTx.TryGetValue(r.Module.TxId, out var row)) row.Found = r.Responded ? "YES" : "no"; }),
            msg => EventLog.Message(LogEventLevel.Information, "Scan modules", msg));
        foreach (var o in outcomes)
        {
            if (!byTx.TryGetValue(o.Table.TxId, out var row)) continue;
            row.Found = o.Responded ? "YES" : "no";
            if (o.FoundByRetry && o.AnsweredOn is Bus alt) row.BusOverride = alt == row.Def.Bus ? null : alt;
        }
        foreach (var row in Modules.Where(r => r.Found == "YES" && r.Def.HasAddress))
            NoteAnsweredBus(row, row.Bus);
        await ProbeAsBuiltAsync();
        await SelectDiagProtocol();
        EventLog.Message(LogEventLevel.Information, "Scan modules", $"{outcomes.Count(r => r.Responded)} of {outcomes.Count} modules responded");
        RefreshBackups();
        Routines.VehicleChanged(); // KB-R3: a procedure's target can change once the scan says which modules are there
    }

    // ---------------- v0.2.2 adapter firmware check (logic in FirmwareViewModel since v0.22.3) ----------------
    public bool FirmwareCheckEnabled { get => _settings.FirmwareCheck; set => S(() => _settings.FirmwareCheck = value, "automatic adapter firmware check", value, nameof(FirmwareCheckEnabled)); }
    public string FirmwareUpdaterPath { get => _settings.FirmwareUpdaterPath ?? ""; set => S(() => _settings.FirmwareUpdaterPath = string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"'), "firmware updater path override", value, nameof(FirmwareUpdaterPath)); }
    /// <summary>v0.22.3: adapter firmware check, package download and official updater (moved out of this class).</summary>
    public FirmwareViewModel Firmware { get; private set; } = null!;

    // ---------------- v0.7.0 user-supplied SecurityAccess (seed/key) plug-ins ----------------
    private CompositeSeedKeyProvider _seedKey = new(Array.Empty<ISeedKeyProvider>());
    public AsyncCommand AddSeedKeyPluginCommand { get; private set; } = null!;
    public RelayCommand OpenSeedKeyFolderCommand { get; private set; } = null!;
    public RelayCommand ReloadSeedKeyPluginsCommand { get; private set; } = null!;
    private string _seedKeyStatus = "";
    public string SeedKeyStatus { get => _seedKeyStatus; private set => Set(ref _seedKeyStatus, value); }

    private void InitSeedKey(Action<string, Exception> Err)
    {
        AddSeedKeyPluginCommand = new AsyncCommand("Add seed/key plug-in", EventLog, AddSeedKeyPluginAsync, Err);
        OpenSeedKeyFolderCommand = new RelayCommand("Open seed/key plug-in folder", EventLog, _ =>
        {
            var d = ObdLink.Core.Storage.AppPaths.Ensure(Paths.SeedKeyPluginsDir);
            try { DialogService.OpenExternal("explorer.exe", $"\"{d}\""); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Explorer", "Could not open the plug-in folder: " + ex.Message); }
        });
        ReloadSeedKeyPluginsCommand = new RelayCommand("Reload seed/key plug-ins", EventLog, _ => ReloadSeedKeyPlugins());
        ReloadSeedKeyPlugins();
    }

    /// <summary>Loads approved plug-ins (only when enabled) and applies the As-Built security level to the As-Built service.</summary>
    private void ReloadSeedKeyPlugins()
    {
        var (p, files) = SeedKeyPlugins.Load(Paths.SeedKeyPluginsDir, _settings.SeedKey, m => EventLog.Message(LogEventLevel.Information, "Seed/key", m));
        _seedKey = p;
        ApplySeedKey();
        SeedKeyStatus = !_settings.SeedKey.Enabled ? "Off. No plug-in is loaded."
            : files.Count == 0 ? $"On. No plug-ins in {Paths.SeedKeyPluginsDir}."
            : $"On. {p.Providers.Count} provider(s) loaded.\n" + string.Join("\n", files.Select(f => f.Summary));
    }

    private void ApplySeedKey()
    {
        if (_asBuilt != null) _asBuilt.SecurityLevel = _settings.SeedKey.Enabled && _seedKey.Providers.Count > 0 ? _settings.SeedKey.AsBuiltLevel : null;
    }

    public bool SeedKeyEnabled
    {
        get => _settings.SeedKey.Enabled;
        set
        {
            if (value == _settings.SeedKey.Enabled) return;
            if (value && !Ask("Security access plug-ins",
                    "Turn on seed/key plug-ins?\n\nOnly plug-in files you approve (by SHA-256) are loaded. A plug-in runs with this app's full rights, so only add ones you built yourself or fully trust. This project ships no algorithms or keys.\n\nUnlocking a module lets writes reach settings it normally protects. Back up first.", MessageBoxImage.Warning))
            { OnPropertyChanged(); return; }
            _settings.SeedKey.Enabled = value; SaveSettings();
            EventLog.UserAction("Seed/key plug-ins toggled", ("on", value));
            OnPropertyChanged(); ReloadSeedKeyPlugins();
        }
    }

    /// <summary>Hex level (odd, 01..7F) requested before As-Built writes; empty = no unlock.</summary>
    public string SeedKeyAsBuiltLevel
    {
        get => _settings.SeedKey.AsBuiltLevel is byte b ? b.ToString("X2") : "";
        set
        {
            var t = (value ?? "").Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            byte? lvl = null;
            if (t.Length > 0)
            {
                if (AsBuiltWritePlan.TryParseByte(t, out var b) && b % 2 == 1 && b <= 0x7F) lvl = b;
                else { EventLog.Message(LogEventLevel.Warning, "Seed/key", $"'{value}' is not an odd level 01..7F; kept {SeedKeyAsBuiltLevel}"); OnPropertyChanged(); return; }
            }
            _settings.SeedKey.AsBuiltLevel = lvl; SaveSettings(); ApplySeedKey();
            EventLog.UserAction("As-Built security level set", ("level", lvl?.ToString("X2") ?? "none"));
            OnPropertyChanged();
        }
    }

    private async Task AddSeedKeyPluginAsync()
    {
        if (!_settings.SeedKey.Enabled) throw new InvalidOperationException("Turn on \"Use seed/key plug-ins\" first.");
        var src = OpenFile("Seed/key plug-in", "Plug-in (*.dll)|*.dll", Paths.SeedKeyPluginsDir);
        var sha = SeedKeyPlugins.Sha256Of(src);
        if (!await _confirm.ConfirmAsync(new ConfirmationStep("Approve plug-in",
                $"{Path.GetFileName(src)}\nSHA-256 {sha}\n\nThis code will run inside the app with your user's rights and will be asked for SecurityAccess keys. Approve it only if you built it or trust its author. Type TRUST to approve.", "TRUST")))
            throw new UserCancelledException("Seed/key plug-in not approved");
        var (dest, destSha) = SeedKeyPlugins.Install(src, Paths.SeedKeyPluginsDir);
        if (!destSha.Equals(sha, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The file changed while it was being copied. Not approved.");
        if (!_settings.SeedKey.TrustedSha256.Contains(sha)) _settings.SeedKey.TrustedSha256.Add(sha);
        SaveSettings();
        EventLog.Message(LogEventLevel.Warning, "Seed/key", $"Plug-in approved: {dest} (SHA-256 {sha})");
        ReloadSeedKeyPlugins();
    }

    // ---------------- v0.2.1 module workflow ----------------
    private string MaskedVin => _vin is { Length: 17 } v ? VinRedactor.MaskVin(v) : "NOVIN";

    private static bool GuessedAsBuilt(ModuleRow r, HashSet<uint> factory) =>
        r.Def.InFactoryAb || factory.Contains(r.Def.TxId) || r.LastBackup != null || ModuleRow.TypicalAsBuilt.Contains(r.Acronym);

    /// <summary>Recomputes As-Built flags + newest backup per module, then refreshes the As-Built picker.</summary>
    private void RefreshBackups()
    {
        AsBuiltPage.RefreshHistory();
        var found = AsBuiltBatch.FindBackups(Paths.AsBuiltDir, Paths.BackupsDir);
        var fac = AsBuiltPage.FactoryModules;
        foreach (var r in Modules)
        {
            if (found.TryGetValue(r.Def.TxId, out var b)) { r.LastBackup = b.When; r.BackupPath = b.Path; } else { r.LastBackup = null; r.BackupPath = null; }
            r.HasAsBuilt = AsBuiltSupportPolicy.ShowInList(r.AsBuiltSupport, GuessedAsBuilt(r, fac), showAll: false, found: "YES");
        }
        RefreshAsBuiltModules();
    }
    private void RefreshAsBuiltModules()
    {
        var keep = SelectedModule;
        var fac = AsBuiltPage.FactoryModules;
        AsBuiltModules.Clear();
        foreach (var r in Modules.Where(r => AsBuiltSupportPolicy.ShowInList(r.AsBuiltSupport, GuessedAsBuilt(r, fac), AsBuiltShowAll, r.Found))) AsBuiltModules.Add(r);
        if (keep != null) { _mod = null; SelectedModule = keep; }
    }
    private void MarkAsBuilt(ModuleRow row, AsBuiltSupport support)
    {
        row.SetAsBuiltSupport(support);
        if (!ReferenceEquals(SelectedModule, row)) return;
        if (support == AsBuiltSupport.Unsupported)
            ModuleHint = AsBuiltNotSupportedException.Describe(row.Acronym, row.Def.TxId, 0xDE00);
        OnPropertyChanged(nameof(AsBuiltActionsEnabled));
        AsBuiltPage?.RaiseModuleCommands();
    }
    private void NoticeNoAsBuilt(AsBuiltNotSupportedException ex)
    {
        if (Modules.FirstOrDefault(r => r.Def.TxId == ex.Module) is { } row) MarkAsBuilt(row, AsBuiltSupport.Unsupported);
        else if (SelectedModule is { } sel && sel.Def.TxId == ex.Module) MarkAsBuilt(sel, AsBuiltSupport.Unsupported);
        ModuleHint = ex.Message;
        RefreshBackups();
        EventLog.Message(LogEventLevel.Information, "As-Built", ex.Message);
        DialogService.Info(ex.Message, "No As-Built");
    }
    /// <summary>One DE00 read per module that answered. NRC 0x31 hides the module; a timeout leaves it unknown so a glitch does not hide it.</summary>
    private async Task ProbeAsBuiltAsync()
    {
        if (_asBuilt is null) return;
        foreach (var row in Modules.Where(r => r.Found == "YES" && r.Def.HasAddress).ToList())
        {
            try
            {
                await OnBus(row, async () => { await AsBuilt.ReadBlockAsync((ushort)row.Def.TxId, 0x01); });
                row.SetAsBuiltSupport(AsBuiltSupport.Supported);
            }
            catch (AsBuiltNotSupportedException)
            {
                row.SetAsBuiltSupport(AsBuiltSupport.Unsupported);
            }
            catch (Exception ex)
            {
                EventLog.Message(LogEventLevel.Debug, "As-Built", $"{row.Acronym}: As-Built check skipped ({ex.Message})");
            }
        }
    }
    private void NoteAnsweredBus(ModuleRow m, Bus bus)
    {
        m.BusOverride = bus == m.Def.Bus ? null : bus;
        if (!ModuleBusMemory.ShouldPersist(_connectedSim) || !ModuleBusMemory.TryParse(bus.ToString(), out _)) return;
        if (ModuleBusMemory.Recall(_settings.ModuleBuses, m.Def.TxId) == bus) return;
        ModuleBusMemory.Remember(_settings.ModuleBuses, m.Def.TxId, bus);
        SaveSettings();
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
            if (m.AsBuiltSupport != AsBuiltSupport.Unsupported)
                ModuleHint = $"Using {m.Acronym} ({m.FullName}) - pick another module in the list above to change.";
        }
        return SelectedModule;
    }
    private async Task<bool> EnsureConnectedAsync()
    {
        if (IsConnected && _ch != null) return true;
        if (SelectedPort is null) { ModuleHint = "Pick a COM port first (or DEMO for simulated data)."; DialogService.Info(Ports.Any(p => !p.IsSimulated) ? "Pick a COM port first." : PortEnumerator.NoPortsHint, "Connect"); return false; }
        var port = SelectedPort.Name;
        if (!DialogService.YesNo($"Not connected. Connect to {port} now?", "Connect"))
        { ModuleHint = "Connect first (toolbar Connect, or F5). A DEMO port works without a vehicle."; EventLog.UserAction("Auto-connect declined"); return false; }
        EventLog.UserAction("Auto-connect accepted", ("port", port));
        await ConnectAsync();
        return true;
    }
    /// <summary>Runs <paramref name="f"/> on the module's bus: MS-CAN modules switch the adapter to MS-CAN 125k (MX+ / EX) and back.</summary>
    private bool CanSwitchBus => MsCanAvailable && _session is { IsStn: true };
    /// <summary>
    /// v0.3.2: runs <paramref name="f"/> on the module's bus. If the module does not answer there and the adapter can switch buses
    /// (EX / MX+), it tries the other Ford bus once and remembers where the module answered. A final no-reply becomes a friendly message.
    /// </summary>
    private async Task OnBus(ModuleRow m, Func<Task> f, bool allowFallback = true)
    {
        var router = _router ?? throw new InvalidOperationException("Not connected");
        await router.RunAsync(m, f, allowFallback, answered =>
        {
            // Preserves OnBus's side effects: remember the bus that answered. On a first-try success this re-sets the
            // same value (no-op); after a fallback it records the new override.
            m.BusOverride = answered == m.Def.Bus ? null : answered;
            NoteAnsweredBus(m, answered);
        });
    }
    private List<ModuleRow> BatchModules(bool asBuilt) =>
        (asBuilt ? AsBuiltModules.AsEnumerable() : Modules).Where(r => r.Found == "YES" && (r.Bus == Bus.HsCan || MsCanAvailable)).ToList();

    private async Task<List<BatchItemResult>> RunBatch(string what, List<ModuleRow> mods, Func<ModuleRow, CancellationToken, Task<BatchItemResult>> work)
    {
        _batchCts = new(); BatchRunning = true; BatchPercent = 0;
        var rows = mods.ToDictionary(m => m.Def);
        try
        {
            return await AsBuiltBatch.RunAsync(mods.Select(m => m.Def).ToList(), (d, ct) => work(rows[d], ct),
                new Progress<BatchProgress>(p => { BatchPercent = p.Percent; BatchText = $"{what}: {p.Text}"; }), _batchCts.Token);
        }
        catch (OperationCanceledException) { BatchText = $"{what}: canceled"; EventLog.Message(LogEventLevel.Warning, what, "Cancelled by user"); return new(); }
        finally { BatchRunning = false; _batchCts.Dispose(); _batchCts = null; }
    }

    /// <summary>Scanned-module batch for child pages (DTCs, As-Built): connect, scan if needed, then run <paramref name="work"/> per module. Null = nothing ran.</summary>
    private async Task<List<BatchItemResult>?> ModuleBatchAsync(string what, bool asBuilt, Func<ModuleRow, CancellationToken, Task<BatchItemResult>> work)
    {
        if (!await EnsureConnectedAsync()) return null;
        if (Modules.All(r => r.Found == "not scanned")) { BatchText = "Scanning modules first…"; await ScanAsync(); RefreshBackups(); }
        var mods = BatchModules(asBuilt);
        if (mods.Count == 0) { ModuleHint = asBuilt ? "No As-Built module answered the scan (ignition ON?)." : "No module answered the scan (ignition ON?)."; return null; }
        return await RunBatch(what, mods, work);
    }

    // IAsBuiltHost: the As-Built page reaches the vehicle, the module list and the dialogs only through these.
    AsBuiltService IAsBuiltHost.AsBuilt => AsBuilt;
    string IAsBuiltHost.Vin => _vin;
    string IAsBuiltHost.MaskedVin => MaskedVin;
    ModuleRow? IAsBuiltHost.FindModule(uint tx) => Modules.FirstOrDefault(r => r.Def.TxId == tx);
    Task<ModuleRow?> IAsBuiltHost.EnsureModuleAsync(bool asBuilt) => EnsureModuleAsync(asBuilt);
    Task IAsBuiltHost.OnBus(ModuleRow m, Func<Task> work, bool allowFallback) => OnBus(m, work, allowFallback);
    SafetyGuard IAsBuiltHost.Safety() => Safety();
    void IAsBuiltHost.MarkAsBuilt(ModuleRow row, AsBuiltSupport support) => MarkAsBuilt(row, support);
    void IAsBuiltHost.RefreshBackups() => RefreshBackups();
    void IAsBuiltHost.Info(string title, string message) => Info(title, message);
    string IAsBuiltHost.BatchText { set => BatchText = value; }
    Task<List<BatchItemResult>?> IAsBuiltHost.ModuleBatchAsync(string what, bool asBuilt, Func<ModuleRow, CancellationToken, Task<BatchItemResult>> work) => ModuleBatchAsync(what, asBuilt, work);

}
