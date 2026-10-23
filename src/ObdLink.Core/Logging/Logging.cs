using ObdLink.Core.Elm;
using Serilog;
using Serilog.Events;

namespace ObdLink.Core.Logging;

public static class LogSetup
{
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
    public RawConsoleService(ElmLink link) => _link = link;

    // UDS services that change ECU state; blocked unless AllowWrites (0x2E write DID, 0x31 routine, 0x34-0x37 download, 0x11 reset, 0x14 clear, 0x27 security, 0x3B/0x2F IO).
    private static readonly string[] Risky = { "2E", "2F", "31", "34", "35", "36", "37", "3B", "11", "14", "27", "04" };

    public async Task<string> ExecuteAsync(string input, CancellationToken ct = default)
    {
        var cmd = input.Trim();
        History.Add(cmd);
        var compact = cmd.Replace(" ", "").ToUpperInvariant();
        if (ReadOnlyMode && !compact.StartsWith("AT") && !compact.StartsWith("ST") && Risky.Any(compact.StartsWith))
            return "BLOCKED: read-only test mode is ON (Settings) - state-changing services disabled";
        if (!AllowWrites && !compact.StartsWith("AT") && !compact.StartsWith("ST") && Risky.Any(compact.StartsWith))
            return "BLOCKED: state-changing service; enable writes in console settings first";
        var r = await _link.SendAsync(cmd, ct: ct);
        return string.Join(Environment.NewLine, r.Lines) + (r.Ok ? "" : $"  [{r.Error}]");
    }
}
