using ObdLink.Core.Elm;
using Serilog;
using Serilog.Events;

namespace ObdLink.Core.Logging;

public static class LogSetup
{
    /// <summary>Legacy single-logger setup (session-*.log). Prefer <see cref="CreateEventLog"/>.</summary>
    public static ILogger Create(string logDir, Action<LogEvent>? sink = null)
    {
        Directory.CreateDirectory(logDir);
        var cfg = new LoggerConfiguration().MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(logDir, "session-.log"), rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}");
        if (sink is not null) cfg = cfg.WriteTo.Sink(new DelegateSink(sink));
        return Log.Logger = cfg.CreateLogger();
    }

    public const string EventsPrefix = "events-", AppPrefix = "app-", TrafficPrefix = "traffic-";

    /// <summary>
    /// Creates the three rolling daily files under <paramref name="logDir"/>: events-YYYYMMDD.log (user actions),
    /// app-YYYYMMDD.log (application messages; also becomes the global Serilog logger) and traffic-YYYYMMDD.log (raw adapter traffic).
    /// </summary>
    public static EventLogService CreateEventLog(string logDir, int retainDays = 14)
    {
        Directory.CreateDirectory(logDir);
        const string lineOnly = "{Message:l}{NewLine}";
        ILogger File(string prefix, string template) => new LoggerConfiguration().MinimumLevel.Verbose()
            .WriteTo.File(Path.Combine(logDir, prefix + ".log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: retainDays,
                outputTemplate: template, shared: true, flushToDiskInterval: TimeSpan.FromSeconds(1))
            .CreateLogger();
        var svc = new EventLogService(File(EventsPrefix, lineOnly), File(TrafficPrefix, lineOnly));
        var app = new LoggerConfiguration().MinimumLevel.Debug().Enrich.FromLogContext()
            .WriteTo.File(Path.Combine(logDir, AppPrefix + ".log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: retainDays, shared: true,
                flushToDiskInterval: TimeSpan.FromSeconds(1),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.Sink(new DelegateSink(svc.Ingest))
            .CreateLogger();
        svc.AppLog = app;
        Log.Logger = app;
        return svc;
    }

    /// <summary>Today's (and optionally recent) log files, read with sharing so open Serilog files can be included in a capture.</summary>
    public static IEnumerable<(string Name, string Text)> ReadRecentLogs(string logDir, int days = 2)
    {
        if (!Directory.Exists(logDir)) yield break;
        var cutoff = DateTime.Now.Date.AddDays(-(days - 1));
        foreach (var f in Directory.GetFiles(logDir, "*.log").Where(f => System.IO.File.GetLastWriteTime(f) >= cutoff).OrderBy(f => f))
        {
            string text;
            try { using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); text = new StreamReader(fs).ReadToEnd(); }
            catch (IOException) { continue; }
            yield return (Path.GetFileName(f), text);
        }
    }

    private sealed class DelegateSink(Action<LogEvent> a) : Serilog.Core.ILogEventSink { public void Emit(LogEvent e) => a(e); }
}

/// <summary>Raw command console: passes user text straight to the adapter, with a guard against write-type services.</summary>
public sealed class RawConsoleService
{
    private readonly ElmLink _link;
    public List<string> History { get; } = new();
    public bool AllowWrites { get; set; }
    /// <summary>When true (read-only test mode), state-changing services are blocked even if AllowWrites is set.</summary>
    public bool ReadOnlyMode { get; set; }
    /// <summary>ELM CAN auto-formatting (ATCAF). True after ATZ. While writes are locked, ATCAF0 is refused.</summary>
    public bool CanAutoFormat { get; private set; } = true;
    public RawConsoleService(ElmLink link) => _link = link;

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var cmd = input.Trim();
        History.Add(cmd);
        var compact = cmd.Replace(" ", "").ToUpperInvariant();
        if (compact is "HELP" or "?" or "/?" or "/HELP") return ObdLink.Core.ConsoleCmds.ConsoleHelp.Text();
        var decision = ObdLink.Core.ConsoleCmds.ConsoleGuard.Evaluate(cmd, ReadOnlyMode, AllowWrites, CanAutoFormat);
        if (!decision.Allowed) return decision.Reason!;
        var r = await _link.SendAsync(cmd, ct: ct);
        CanAutoFormat = ObdLink.Core.ConsoleCmds.ConsoleGuard.NextAutoFormat(CanAutoFormat, cmd);
        return string.Join(Environment.NewLine, r.Lines) + (r.Ok ? "" : $"  [{r.Error}]");
    }
}
