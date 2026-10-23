using ObdLink.Core.Adapters;
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

namespace ObdLink.App;

public sealed class ModuleRow : ObservableObject
{
    public required ModuleDefinition Def { get; init; }
    public string Acronym => Def.Acronym;
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
    private AdapterSession? _session; private ElmDiagChannel? _ch; private RawConsoleService? _console; private AsBuiltService? _asBuilt;
    private string _vin = "UNKNOWNVIN";

    public ObservableCollection<PortInfo> Ports { get; } = new();
    public ObservableCollection<string> Vehicles { get; } = new() { "2017 Ford F-150", "2008 Dodge Ram 1500" };
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
    public ObservableCollection<string> LogLines { get; } = new();

    private PortInfo? _port; public PortInfo? SelectedPort { get => _port; set => Set(ref _port, value); }
    private string _vehicle = "2017 Ford F-150"; public string SelectedVehicle { get => _vehicle; set { Set(ref _vehicle, value); LoadModules(); } }
    private ModuleRow? _mod; public ModuleRow? SelectedModule { get => _mod; set => Set(ref _mod, value); }
    private string _adapterName = "—"; public string AdapterName { get => _adapterName; set => Set(ref _adapterName, value); }
    private string _caps = ""; public string Capabilities { get => _caps; set => Set(ref _caps, value); }
    private string _baud = ""; public string Baud { get => _baud; set => Set(ref _baud, value); }
    private bool _ms; public bool MsCanAvailable { get => _ms; set => Set(ref _ms, value); }
    private string _status = "Disconnected"; public string Status { get => _status; set => Set(ref _status, value); }
    private string _voltage = "-"; public string Voltage { get => _voltage; set => Set(ref _voltage, value); }
    private string _blk = "1"; public string AsBuiltBlockNo { get => _blk; set => Set(ref _blk, value); }
    private string _ci = ""; public string ConsoleInput { get => _ci; set => Set(ref _ci, value); }
    private string _co = ""; public string ConsoleOutput { get => _co; set => Set(ref _co, value); }
    private bool _allow; public bool ConsoleAllowWrites { get => _allow; set { Set(ref _allow, value); if (_console != null) _console.AllowWrites = value; } }

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

    public MainViewModel(IConfirmationProvider confirm)
    {
        _confirm = confirm;
        LogSetup.Create(Path.Combine(_dataDir, "logs"), e => Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            LogLines.Add($"{e.Timestamp:HH:mm:ss.fff} [{e.Level}] {e.RenderMessage()}");
            if (LogLines.Count > 2000) LogLines.RemoveAt(0);
        }));
        void Err(Exception ex) { Log.Error(ex, "Operation failed"); MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        AsyncCommand C(Func<Task> f) => new(f, Err);
        RefreshPortsCommand = C(() => { Ports.Clear(); Ports.Add(DemoMx); Ports.Add(DemoEx); foreach (var p in PortEnumerator.List()) Ports.Add(p); SelectedPort ??= Ports[0]; return Task.CompletedTask; });
        ConnectCommand = C(ConnectAsync);
        DisconnectCommand = C(async () => { if (_session != null) await _session.Link.DisposeAsync(); _session = null; Status = "Disconnected"; });
        ScanCommand = C(ScanAsync);
        ReadObdDtcsCommand = C(async () => { Dtcs.Clear(); foreach (var (ecu, code) in await new ObdService(Ch).ReadStoredDtcsAsync()) Dtcs.Add(new($"{ecu:X3}", code, "stored", _dtcDb.Describe(code))); });
        ReadModuleDtcsCommand = C(async () => { Dtcs.Clear(); foreach (var d in await Uds().ReadDtcsByStatusAsync()) Dtcs.Add(new(SelectedModule!.Acronym, d.Display, $"0x{d.Status:X2}", _dtcDb.Describe(d.Display))); });
        ClearModuleDtcsCommand = C(async () =>
        {
            await Safety().RequireAsync(new[] { new ConfirmationStep("Clear DTCs", $"Clear all DTCs in {SelectedModule?.Acronym}? Freeze-frame data will be lost.") });
            await Uds().ClearDtcsAsync();
        });
        ReadVinCommand = C(async () => { _vin = await new ObdService(Ch).ReadVinAsync() ?? _vin; Log.Information("VIN {Vin}", _vin); });
        ReadAsBuiltCommand = C(ReadAsBuiltAsync);
        BackupAsBuiltCommand = C(async () => { var p = await AsBuilt.BackupAsync(_vin, Module, new[] { Block }); MessageBox.Show($"Saved {p}"); });
        WriteAsBuiltCommand = C(async () =>
        {
            var data = AsBuiltRows.SelectMany(r => Convert.FromHexString(r.Data.Replace(" ", ""))).ToArray();
            await AsBuilt.WriteBlockAsync(_vin, new AsBuiltBlock { Module = Module, Block = Block, Data = data });
            MessageBox.Show("Written and verified by read-back.");
        });
        RestoreAsBuiltCommand = C(async () =>
        {
            var dlg = new OpenFileDialog { Filter = "As-Built backup|*.asbuilt.json", InitialDirectory = Path.Combine(_dataDir, "backups") };
            if (dlg.ShowDialog() == true) await AsBuilt.RestoreAsync(dlg.FileName);
        });
        ImportFactoryAbCommand = C(() =>
        {
            var dlg = new OpenFileDialog { Filter = "Ford As-Built|*.ab|All files|*.*" };
            if (dlg.ShowDialog() != true) return Task.CompletedTask;
            _factory = FordAbFile.Load(dlg.FileName);
            var bad = _factory.Lines.Count(l => !l.ChecksumValid);
            FactoryInfo = $"Factory .ab: VIN …{(_factory.Vin.Length >= 6 ? _factory.Vin[^6..] : _factory.Vin)}, {_factory.AsBuiltModules.Count()} modules, {_factory.Lines.Count} lines, {bad} bad checksums";
            return Task.CompletedTask;
        });
        ExportFactoryAbCommand = C(() =>
        {
            if (_factory is null) throw new InvalidOperationException("Import a factory .ab first");
            var dlg = new SaveFileDialog { Filter = "Ford As-Built|*.ab", FileName = "factory-redacted.ab" };
            if (dlg.ShowDialog() == true)
            {
                var redact = MessageBox.Show("Redact VIN (keep last 6) and module serials?", "Export", MessageBoxButton.YesNo) == MessageBoxResult.Yes;
                File.WriteAllText(dlg.FileName, (redact ? _factory.Redacted() : _factory).ToXml());
            }
            return Task.CompletedTask;
        });
        ImportAbtCommand = C(() =>
        {
            var dlg = new OpenFileDialog { Filter = "FORScan As-Built backup|*.abt|All files|*.*" };
            if (dlg.ShowDialog() != true) return Task.CompletedTask;
            var abt = ForscanAbtFile.Load(dlg.FileName);
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
            return Task.CompletedTask;
        });
        ExportAbtCommand = C(() =>
        {
            if (AsBuiltRows.Count == 0) throw new InvalidOperationException("Read or import As-Built rows first");
            var lines = AsBuiltRows.Select(r => AsBuiltLine.Create(r.Addr, Convert.FromHexString(r.Data.Replace(" ", "")))).ToList();
            var dlg = new SaveFileDialog { Filter = "FORScan As-Built backup|*.abt", FileName = $"{lines[0].Address.Module:X3}.abt" };
            if (dlg.ShowDialog() != true) return Task.CompletedTask;
            var abt = ForscanAbtFile.Parse(ForscanAbtFile.Format(lines));
            if (_factory is { Vin.Length: 17 } && MessageBox.Show("Redact VIN (keep last 6) in block data?", "Export", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                abt = abt.Redacted(_factory.Vin);
            File.WriteAllText(dlg.FileName, abt.ToText());
            return Task.CompletedTask;
        });
        CompareToFactoryCommand = C(async () =>
        {
            if (_factory is null) throw new InvalidOperationException("Import a factory .ab first");
            var current = new List<AsBuiltLine>();
            foreach (var b in _factory.Blocks(Module))
                current.AddRange(_factory.SplitLike(Module, b, (await AsBuilt.ReadBlockAsync(Module, b)).Data));
            FactoryDiff.Clear();
            foreach (var d in AsBuiltDiff.Compare(_factory.Lines, current, Module)) FactoryDiff.Add(d);
        });
        ConsoleSendCommand = C(async () =>
        {
            if (_console is null) throw new InvalidOperationException("Not connected");
            var input = ConsoleInput; ConsoleInput = "";
            ConsoleOutput += $"> {input}{Environment.NewLine}{await _console.ExecuteAsync(input)}{Environment.NewLine}";
        });
        RefreshPortsCommand.Execute(null);
        LoadModules();
    }

    private ElmDiagChannel Ch => _ch ?? throw new InvalidOperationException("Not connected");
    private ushort Module => (ushort)(SelectedModule?.Def.TxId ?? throw new InvalidOperationException("Select a module"));
    private byte Block => Convert.ToByte(AsBuiltBlockNo, 16);
    private UdsClient Uds() => new(Ch, Module);
    private SafetyGuard Safety() => new(_session ?? throw new InvalidOperationException("Not connected"), _confirm);
    private AsBuiltService AsBuilt => _asBuilt ?? throw new InvalidOperationException("Not connected");

    private void LoadModules()
    {
        Modules.Clear();
        var list = SelectedVehicle.Contains("Ford") ? FordF150_2017.Modules : DodgeRam1500_2008.Modules;
        foreach (var m in list) Modules.Add(new ModuleRow { Def = m });
    }

    private async Task ConnectAsync()
    {
        var sel = SelectedPort ?? DemoMx;
        ITransport t;
        if (sel.Kind == PortKind.Virtual) t = new SimulatedAdapter { Profile = sel == DemoEx ? SimProfile.Ex : SimProfile.MxPlus }.CreateTransport();
        else
        {
            var guess = AdapterProfiles.ForPort(sel.Kind);
            Status = $"Probing {sel.Name}…";
            var found = await BaudProber.ProbeAsync(b => new SerialPortTransport(sel.Name, b), guess.BaudCandidates, TimeSpan.FromSeconds(sel.Kind == PortKind.Bluetooth ? 5 : 1))
                        ?? throw new InvalidOperationException($"No ELM327/STN adapter answered on {sel.Name}");
            t = found.Transport;
            Baud = sel.Kind == PortKind.Bluetooth ? "n/a (BT SPP)" : $"{found.Baud} bps";
        }
        var link = new ElmLink(t);
        link.Traffic += (d, s) => Log.Debug("{Dir} {Text}", d, s);
        _session = new AdapterSession(link, initial: AdapterProfiles.ForPort(sel.Kind));
        Status = "Initializing…";
        var info = await _session.InitializeAsync();
        Voltage = info.BatteryVolts is double v ? $"{v:0.0} V" : "?";
        Status = $"{info.DeviceId ?? info.ElmId} | {info.StnId ?? "not STN"}";
        AdapterName = _session.Profile.DisplayName;
        Capabilities = _session.Profile.CapabilityText;
        MsCanAvailable = _session.Profile.Has(AdapterCapabilities.MsCan);
        _ch = new ElmDiagChannel(_session);
        _console = new RawConsoleService(link) { AllowWrites = ConsoleAllowWrites };
        _asBuilt = new AsBuiltService(m => new UdsClient(Ch, m), Safety(), Path.Combine(_dataDir, "backups"));
        if (_session.IsStn) await _session.SelectProtocolAsync(StnProtocol.HsCan11_500);
    }

    private async Task ScanAsync()
    {
        if (_session is null) throw new InvalidOperationException("Not connected");
        var rows = Modules.ToDictionary(r => r.Def);
        await new ModuleScanner(_session, Ch).ScanAsync(Modules.Select(r => r.Def).ToList(),
            new Progress<ScanResult>(r => rows[r.Module].Found = r.Responded ? "YES" : "no"));
        await _session.SelectProtocolAsync(StnProtocol.HsCan11_500);
    }

    private async Task ReadAsBuiltAsync()
    {
        var b = await AsBuilt.ReadBlockAsync(Module, Block);
        AsBuiltRows.Clear();
        foreach (var l in b.Lines()) AsBuiltRows.Add(new AsBuiltRow { Addr = l.Address, Data = Convert.ToHexString(l.Data) });
    }
}
