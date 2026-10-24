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

    public static string CrashDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OBDLinkDiag", "logs");

    /// <summary>Writes %LOCALAPPDATA%\OBDLinkDiag\logs\crash-*.txt and crash.txt next to the exe, then shows a MessageBox.</summary>
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
        try { var p = Path.Combine(AppContext.BaseDirectory, "crash.txt"); File.WriteAllText(p, text); paths.Add(p); } catch { }
        try { Serilog.Log.Fatal(ex, "Unhandled exception from {Source}", source); Serilog.Log.CloseAndFlush(); } catch { }
        if (!fatal && Interlocked.Exchange(ref _reported, 1) == 1) return;
        try
        {
            var inner = ex; while (inner?.InnerException != null) inner = inner.InnerException;
            MessageBox.Show($"{(fatal ? "OBDLinkDiag failed to start / crashed." : "OBDLinkDiag hit an unexpected error.")}\n\n{inner?.GetType().Name}: {inner?.Message}\n\nDetails saved to:\n{(paths.Count > 0 ? string.Join("\n", paths) : "(could not write crash file)")}",
                "OBDLinkDiag error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }
}
