using System.IO;
using ObdLink.Core.Safety;
using ObdLink.Core.Diagnostics;
using ObdLink.Core.Firmware;
using ObdLink.Core.Logging;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// Adapter firmware: the automatic check against OBDLink's public feed after connect (never in DEMO), the verified package
/// download, and launching OBDLink's official updater. This app never flashes adapter firmware. Moved out of MainViewModel in
/// v0.22.2 (bindings: <c>Firmware.*</c>); the two settings (check on/off, updater path) stay on the Settings page.
/// </summary>
public sealed class FirmwareViewModel : ObservableObject
{
    private readonly IEventLog EventLog;
    private readonly Func<AppSettings> _settings;
    private readonly string _dataDir;
    private readonly Func<bool> _isConnected, _simulated;
    private readonly Func<Task> _disconnect;
    private readonly Action<string, string> Info;
    private readonly Action<string, Exception> _err;
    private readonly Action _save;

    public FirmwareViewModel(IEventLog log, Func<AppSettings> settings, string dataDir, Func<bool> isConnected, Func<bool> simulated,
        Func<Task> disconnect, Action<string, string> info, Action<string, Exception> err, Action save)
    {
        EventLog = log; _settings = settings; _dataDir = dataDir; _isConnected = isConnected; _simulated = simulated;
        _disconnect = disconnect; Info = info; _err = err; _save = save;
        InitCommands(err);
    }

    /// <summary>Called after a successful connect with the adapter's STN id (null on a plain ELM).</summary>
    public Task OnConnectedAsync(string? sti) { _lastSti = sti; return RunFirmwareCheckAsync(sti); }

    // ---------------- v0.2.2 adapter firmware check ----------------
    private FirmwareCheckResult? _fw; private string _fwBanner = ""; private bool _fwVisible, _fwBusy; private string? _fwFolder;
    public string FirmwareBanner { get => _fwBanner; private set => Set(ref _fwBanner, value); }
    public bool FirmwareBannerVisible { get => _fwVisible; private set => Set(ref _fwVisible, value); }
    public bool FirmwareDownloading { get => _fwBusy; private set => Set(ref _fwBusy, value); }
    public bool FirmwareCanDownload => _fw?.Status == FirmwareStatus.UpdateAvailable && FirmwareFeed.IsOfficialUrl(_fw.Latest?.PackageUrl) && !_fwBusy;
    public bool FirmwareHasFolder => _fwFolder is not null;
    public string FirmwarePage => _fw?.OfficialPage ?? FirmwareFeed.FirmwareIndex;
    public string FirmwareRoot => Path.Combine(_dataDir, "firmware");
    public AsyncCommand DownloadFirmwareCommand { get; private set; } = null!;
    public RelayCommand OpenFirmwareFolderCommand { get; private set; } = null!;
    public RelayCommand OpenFirmwarePageCommand { get; private set; } = null!;
    public RelayCommand DismissFirmwareCommand { get; private set; } = null!;
    private void InitCommands(Action<string, Exception> Err)
    {
        DownloadFirmwareCommand = new AsyncCommand("Download firmware", EventLog, DownloadFirmwareAsync, Err);
        OpenFirmwareFolderCommand = new RelayCommand("Open firmware folder", EventLog, _ => { var d = _fwFolder ?? ObdLink.Core.Storage.AppPaths.Ensure(FirmwareRoot); try { DialogService.OpenExternal("explorer.exe", $"\"{d}\""); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Explorer", "Could not open the firmware folder: " + ex.Message); } });
        OpenFirmwarePageCommand = new RelayCommand("Open firmware page", EventLog, _ => { try { DialogService.OpenExternal(FirmwarePage); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Firmware", "Could not open the firmware page (blocked or no browser): " + ex.Message); } });
        DismissFirmwareCommand = new RelayCommand("Dismiss firmware notice", EventLog, _ => FirmwareBannerVisible = false);
        GetUpdaterCommand = new AsyncCommand("Get official updater", EventLog, GetUpdaterAsync, Err);
        RunUpdaterCommand = new AsyncCommand("Run official updater", EventLog, RunUpdaterAsync, Err);
        BrowseUpdaterPathCommand = new RelayCommand("Browse for updater exe", EventLog, _ =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Updater (*.exe)|*.exe", FileName = OfficialUpdater.AppExeName };
            if (DialogService.ShowFileDialog(dlg) == true) { _settings().FirmwareUpdaterPath = dlg.FileName; _save(); }
        });
    }

    // ---------------- v0.3.1 official firmware updater (downloaded on request, never bundled) ----------------
    public string UpdaterDir => Path.Combine(FirmwareRoot, "updater");
    private bool _updBusy;
    public bool UpdaterBusy { get => _updBusy; private set => Set(ref _updBusy, value); }
    public AsyncCommand GetUpdaterCommand { get; private set; } = null!;
    public AsyncCommand RunUpdaterCommand { get; private set; } = null!;
    public RelayCommand BrowseUpdaterPathCommand { get; private set; } = null!;

    private static string TrustText(UpdaterTrust t, UpdaterManifest m) => t switch
    {
        UpdaterTrust.TrustedSignature => $"Authenticode signature valid, publisher \"{m.Publisher}\"",
        UpdaterTrust.KnownHash => "no Authenticode signature (OBDLink ships it unsigned), but the SHA-256 matches the known official build",
        _ => "UNSIGNED and SHA-256 not on the known-good list",
    };

    private async Task<(UpdaterManifest m, UpdaterTrust t)?> GetUpdaterCoreAsync(bool interactive)
    {
        UpdaterBusy = true;
        try
        {
            var dir = ObdLink.Core.Storage.AppPaths.Ensure(UpdaterDir);
            using var http = OfficialUpdater.CreateClient();
            EventLog.Message(LogEventLevel.Information, "Updater", $"Checking {OfficialUpdater.PageUrl} for the official installer");
            var (m, t, reused) = await OfficialUpdater.DownloadAsync(http, dir, UpdaterWindows.VerifySignature);
            var file = OfficialUpdater.ConfinedFile(dir, m.FileName) ?? throw new InvalidDataException("Updater file name is not inside the updater folder");
            EventLog.Message(LogEventLevel.Information, "Updater", $"{(reused ? "Up to date, kept existing" : "Downloaded")} {m.FileName} v{m.Version ?? "?"} ({m.Size:N0} bytes) SHA-256 {m.Sha256}; {TrustText(t, m)}; from {m.Url}");
            if (t == UpdaterTrust.Unverified)
            {
                EventLog.Message(LogEventLevel.Warning, "Updater", "Installer is unsigned and its hash is not pinned; asking the user");
                if (!DialogService.YesNo($"The downloaded updater came from {m.Url} (https, scantool.net) and looks like an installer, but it is NOT digitally signed and its SHA-256 is not one this app knows:\n\n{m.Sha256}\n\nOBDLink may simply have released a new version. Keep and use it?", "Unverified updater", System.Windows.MessageBoxImage.Warning))
                {
                    try { File.Delete(file); File.Delete(Path.Combine(dir, OfficialUpdater.ManifestName)); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Updater", "Could not delete the declined installer: " + ex.Message); }
                    EventLog.Message(LogEventLevel.Warning, "Updater", "User declined the unverified installer; deleted");
                    return null;
                }
            }
            if (interactive)
                DialogService.Info($"{(reused ? "You already have the latest official updater" : "Official updater downloaded")}:\n{file}\n\nVersion: {m.Version ?? "unknown"}\nSHA-256: {m.Sha256}\nCheck: {TrustText(t, m)}\n\nUse \"Run official updater\" to start it.", "Official firmware updater");
            return (m, t);
        }
        finally { UpdaterBusy = false; }
    }
    private Task GetUpdaterAsync() => GetUpdaterCoreAsync(true);

    private async Task RunUpdaterAsync()
    {
        var ovr = _settings().FirmwareUpdaterPath;
        if (!string.IsNullOrWhiteSpace(ovr) && !File.Exists(ovr)) EventLog.Message(LogEventLevel.Warning, "Updater", $"Settings override not found: {ovr}; auto-detecting");
        var installed = UpdaterWindows.FindInstalled();
        string? downloaded = null;
        if (OfficialUpdater.ExistingValid(UpdaterDir, out var man) && man is not null) downloaded = OfficialUpdater.ConfinedFile(UpdaterDir, man.FileName);
        var (exe, kind) = OfficialUpdater.ResolveLaunch(ovr, installed, downloaded, File.Exists);
        if (kind == "installed" && downloaded is not null)
        {
            var r = DialogService.Show($"The official updater is already installed:\n{installed}\n\nYes = launch the installed StnFirmwareUpdater\nNo = run the downloaded installer again (repair / upgrade)", "Run official updater", System.Windows.MessageBoxButton.YesNoCancel, System.Windows.MessageBoxImage.Question);
            if (r == System.Windows.MessageBoxResult.Cancel) return;
            if (r == System.Windows.MessageBoxResult.No) { exe = downloaded; kind = "installer"; }
        }
        if (exe is null)
        {
            if (!DialogService.YesNo("The official updater is not installed and has not been downloaded yet. Download it now from scantool.net?", "Run official updater")) return;
            var g = await GetUpdaterCoreAsync(false); if (g is null) return;
            exe = OfficialUpdater.ConfinedFile(UpdaterDir, g.Value.m.FileName); kind = "installer";
        }
        else if (kind == "installer")
        {
            var t = OfficialUpdater.Evaluate(UpdaterWindows.VerifySignature(exe), OfficialUpdater.Sha256(exe));
            if (t == UpdaterTrust.Rejected) throw new InvalidDataException("The downloaded installer failed its signature check; use Get official updater again.");
            if (t == UpdaterTrust.Unverified && !DialogService.YesNo("This installer is unsigned and its SHA-256 is not on the known-good list. Run it anyway?", "Unverified updater", System.Windows.MessageBoxImage.Warning)) return;
        }
        var fwNote = _fwFolder ?? (Directory.Exists(FirmwareRoot) && Directory.EnumerateFiles(FirmwareRoot, "*.zip", SearchOption.AllDirectories).Any() ? FirmwareRoot : null);
        var msg = (kind == "installer" ? "This runs OBDLink's official installer (Windows will ask for administrator permission). After it installs, start StnFirmwareUpdater from it or from here.\n\n" : "")
            + "Before updating:\n"
            + (_isConnected() ? "\u2022 This app will DISCONNECT the adapter now so its COM port / Bluetooth link is free for the updater.\n" : "\u2022 The adapter is not connected in this app, so the port is already free.\n")
            + "\u2022 Keep USB and vehicle power stable (engine off is fine, battery charger recommended). Do not unplug during the update.\n"
            + "\u2022 OBDLink MX+ over Bluetooth: updating wirelessly carries a risk if the link drops; stay close to the adapter.\n"
            + "\u2022 This app never flashes firmware; the official updater does all of it.\n"
            + (fwNote is not null ? $"\nAlready-downloaded firmware package(s): {fwNote}\n(The official updater normally fetches firmware itself; this folder is there if it asks for a file.)\n" : "")
            + $"\nStart:\n{exe}\n\nContinue?";
        if (!DialogService.YesNo(msg, "Run official updater", System.Windows.MessageBoxImage.Warning)) { EventLog.Message(LogEventLevel.Information, "Updater", "User canceled the updater launch"); return; }
        if (_isConnected()) { EventLog.Message(LogEventLevel.Information, "Updater", "Auto-disconnecting the adapter before the official updater"); await _disconnect(); }
        EventLog.Message(LogEventLevel.Information, "Updater", $"Launching official updater ({kind}): {exe}");
        if (!DialogService.RunUpdater(exe!)) EventLog.Message(LogEventLevel.Warning, "Updater", "Launch canceled (UAC declined)");
        if (fwNote is not null) { try { DialogService.OpenExternal("explorer.exe", $"\"{fwNote}\""); } catch (Exception ex) { EventLog.Message(LogEventLevel.Warning, "Explorer", "Could not open Explorer: " + ex.Message); } }
    }

    private void FwChanged() { foreach (var n in new[] { nameof(FirmwareCanDownload), nameof(FirmwareHasFolder), nameof(FirmwarePage) }) OnPropertyChanged(n); }

    /// <summary>Runs after connect, in the background. Silent when offline; never blocks or flashes anything.</summary>
    private string? _lastSti;
    public AsyncCommand CheckFirmwareCommand => _checkFw ??= new("Check adapter firmware", EventLog, async () =>
    {
        if (!_isConnected() || _lastSti is null) { Info("Firmware check", "Connect to the adapter first (F5); the check needs its STN chip id."); return; }
        await RunFirmwareCheckAsync(_lastSti, force: true);
        if (_fw is { } r) Info("Firmware check", r.Message);
    }, _err);
    private AsyncCommand? _checkFw;
    private async Task RunFirmwareCheckAsync(string? sti, bool force = false)
    {
        if (!FirmwareCheckPolicy.ShouldQuery(_simulated(), _settings().FirmwareCheck, force))
        {
            if (_simulated())
            {
                _fw = null; FirmwareBannerVisible = false; FwChanged();
                EventLog.Message(LogEventLevel.Information, "Firmware", "Firmware check skipped: DEMO / simulated adapter. No request is sent to OBDLink.");
                if (force) Info("Firmware check", "DEMO mode does not check adapter firmware, including on a first-run simulated session. Connect a real OBDLink adapter to compare with OBDLink's public feed. This app never installs firmware.");
            }
            return;
        }
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
            using var http = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(2) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("OBDLinkDiag-firmware-download");
            var d = await FirmwareDownloader.DownloadAsync(http, FirmwareRoot, inst, rel);
            _fwFolder = d.Folder;
            EventLog.Message(LogEventLevel.Information, "Firmware", $"Downloaded and verified {Path.GetFileName(d.ZipPath)} ({d.Size:N0} bytes, {d.BinName} has STNFWv header) to {d.Folder}");
            FirmwareBanner = $"Firmware v{rel.Version} package downloaded and verified: {d.Folder}. Use OBDLink's official updater to install it; this app never flashes firmware.";
        }
        finally { FirmwareDownloading = false; FwChanged(); }
    }
}
