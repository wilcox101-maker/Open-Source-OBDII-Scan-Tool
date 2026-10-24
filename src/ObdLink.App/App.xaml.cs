using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ObdLink.App;

/// <summary>Startup with global crash handlers: any failure writes crash files and shows a MessageBox (never silent).</summary>
public partial class App : Application
{
    private static int _reported;

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report("AppDomain.UnhandledException", e.ExceptionObject as Exception, fatal: true);
        TaskScheduler.UnobservedTaskException += (_, e) => { Report("TaskScheduler.UnobservedTaskException", e.Exception, fatal: false); e.SetObserved(); };
        DispatcherUnhandledException += OnDispatcherUnhandled;
    }

    private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        bool started = MainWindow is { IsLoaded: true };
        Report("DispatcherUnhandledException", e.Exception, fatal: !started);
        e.Handled = started; // keep running once the window is up; startup failures are fatal
        if (!started) Shutdown(1);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var paths = ObdLink.Core.Storage.AppPaths.Current;
            try { Migration = paths.MigrateFrom(ObdLink.Core.Storage.AppPaths.DefaultLegacyDir); }
            catch (Exception ex) { Report("Data folder migration", ex, fatal: false); }
            if (paths.ShouldShowFallbackNotice())
                DialogService.Show($"The folder this app runs from is not writable:\n{paths.FallbackReason}\n\nLogs, captures, backups and settings will be stored in:\n{paths.DataDir}\n\nTo keep everything next to the app, move the app folder somewhere writable (e.g. Documents or a USB drive).",
                    "OBDLinkDiag data folder", MessageBoxButton.OK, MessageBoxImage.Information);
            // .NET 8 needs Windows 10 1607 (build 14393) or Windows 11; older Windows normally cannot start the exe at all.
            if (OperatingSystem.IsWindows() && !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
                DialogService.Show($"This Windows version ({Environment.OSVersion.VersionString}) is not supported.\n\nOBDLink Diag needs Windows 10 version 1607 or later, or Windows 11 (x64). Windows 7 and 8.1 are not supported. The app may not work correctly.",
                    "OBDLinkDiag - unsupported Windows", MessageBoxButton.OK, MessageBoxImage.Warning);
            var w = new MainWindow();
            MainWindow = w;
            w.Show();
        }
        catch (Exception ex)
        {
            Report("App startup (MainWindow)", ex, fatal: true);
            Shutdown(1);
        }
    }

    public static ObdLink.Core.Storage.AppPaths.MigrationResult? Migration { get; private set; }

    public static string CrashDir
    {
        get { try { return ObdLink.Core.Storage.AppPaths.Current.LogsDir; } catch { return Path.Combine(ObdLink.Core.Storage.AppPaths.DefaultLegacyDir, "logs"); } }
    }

    /// <summary>Writes &lt;data folder&gt;\logs\crash-*.txt (crash.txt next to the exe only if that fails), then shows a MessageBox.</summary>
    public static void Report(string source, Exception? ex, bool fatal)
    {
        var text = new StringBuilder()
            .AppendLine($"OBDLinkDiag v{typeof(App).Assembly.GetName().Version} crash report")
            .AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}")
            .AppendLine($"Source: {source} (fatal={fatal})")
            .AppendLine($"OS: {Environment.OSVersion}; .NET {Environment.Version}; 64-bit={Environment.Is64BitProcess}")
            .AppendLine($"Exe dir: {AppContext.BaseDirectory}")
            .AppendLine().AppendLine(ex?.ToString() ?? "(no exception object)").ToString();
        var paths = new List<string>();
        try { Directory.CreateDirectory(CrashDir); var p = Path.Combine(CrashDir, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt"); File.WriteAllText(p, text); paths.Add(p); } catch { }
        // Last resort only (v0.1.5: crash files live in Data\logs; the exe folder is written only if that failed).
        if (paths.Count == 0) try { var p = Path.Combine(AppContext.BaseDirectory, "crash.txt"); File.WriteAllText(p, text); paths.Add(p); } catch { }
        try { Serilog.Log.Fatal(ex, "Unhandled exception from {Source}", source); Serilog.Log.CloseAndFlush(); } catch { }
        if (!fatal && Interlocked.Exchange(ref _reported, 1) == 1) return;
        try
        {
            var inner = ex; while (inner?.InnerException != null) inner = inner.InnerException;
            DialogService.Show($"{(fatal ? "OBDLinkDiag failed to start / crashed." : "OBDLinkDiag hit an unexpected error.")}\n\n{inner?.GetType().Name}: {inner?.Message}\n\nDetails saved to:\n{(paths.Count > 0 ? string.Join("\n", paths) : "(could not write crash file)")}",
                "OBDLinkDiag error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }
}
