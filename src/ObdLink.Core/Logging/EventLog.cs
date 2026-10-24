using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Events;

namespace ObdLink.Core.Logging;

/// <summary>Which bottom-panel log an entry belongs to.</summary>
public enum LogChannel { Events, Messages, Traffic }
public enum EventOutcome { None, Started, Succeeded, Failed, Cancelled }

/// <summary>One log line. <see cref="Action"/> is the user action / message source, <see cref="Text"/> the human-readable text.</summary>
public sealed record LogEntry(DateTimeOffset Time, LogChannel Channel, LogEventLevel Level, string Action, string Text,
    IReadOnlyList<KeyValuePair<string, string>> Parameters, EventOutcome Outcome = EventOutcome.None, string? Error = null)
{
    /// <summary>Text column for the grid: blank when it would just repeat <see cref="Action"/> (e.g. "Read VIN" started/succeeded).</summary>
    public string DisplayText => string.Equals(Text, Action, StringComparison.Ordinal) ? "" : Text;
    public string ParamText => string.Join(", ", Parameters.Select(p => $"{p.Key}={p.Value}"));
    public string OutcomeText => Outcome == EventOutcome.None ? "" : Outcome.ToString();
    public string LevelText => Level switch { LogEventLevel.Verbose => "VRB", LogEventLevel.Debug => "DBG", LogEventLevel.Information => "INF", LogEventLevel.Warning => "WRN", LogEventLevel.Error => "ERR", _ => "FTL" };
    /// <summary>Single-line rendering used for files, clipboard and .txt export.</summary>
    public string Render()
    {
        var sb = new StringBuilder();
        sb.Append(Time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(" [").Append(LevelText).Append("] ");
        if (Channel == LogChannel.Events)
        {
            sb.Append(Action);
            if (Outcome != EventOutcome.None) sb.Append(" -> ").Append(Outcome);
            if (Parameters.Count > 0) sb.Append(" (").Append(ParamText).Append(')');
            if (!string.IsNullOrEmpty(Error)) sb.Append(": ").Append(Error);
            if (!string.IsNullOrEmpty(Text) && Text != Action) sb.Append(" | ").Append(Text);
        }
        else
        {
            if (!string.IsNullOrEmpty(Action)) sb.Append(Action).Append(": ");
            sb.Append(Text);
            if (!string.IsNullOrEmpty(Error)) sb.Append(" | ").Append(Error);
        }
        return sb.ToString().Replace("\r", "\\r").Replace("\n", " | ");
    }
}

/// <summary>A user action in progress: call <see cref="Succeeded"/>, <see cref="Failed"/> or <see cref="Cancelled"/> once.</summary>
public interface IEventOperation
{
    string Action { get; }
    void Succeeded(string? detail = null);
    void Failed(Exception ex);
    void Failed(string error);
    void Cancelled(string? reason = null);
}

/// <summary>
/// Central log of user interaction (Events), application messages (Messages, Info/Warn/Error) and raw adapter traffic (Traffic).
/// The UI subscribes to <see cref="EntryAdded"/>; files are written through Serilog (see <see cref="LogSetup"/>).
/// </summary>
public interface IEventLog
{
    event Action<LogEntry>? EntryAdded;
    /// <summary>Instant user action (checkbox, selection, tab change...).</summary>
    void UserAction(string action, params (string Key, object? Value)[] parameters);
    /// <summary>Starts a long-running user action; logs "Started" now and the outcome later.</summary>
    IEventOperation Begin(string action, params (string Key, object? Value)[] parameters);
    void Message(LogEventLevel level, string source, string text, Exception? ex = null);
    void Traffic(string direction, string text);
    IReadOnlyList<LogEntry> Snapshot(LogChannel channel);
    void Clear(LogChannel channel);
}

public sealed class EventLogService : IEventLog
{
    private readonly Dictionary<LogChannel, LinkedList<LogEntry>> _buf = Enum.GetValues<LogChannel>().ToDictionary(c => c, _ => new LinkedList<LogEntry>());
    private readonly object _lock = new();
    private readonly ILogger? _eventsFile, _trafficFile, _appLog;
    private readonly Func<DateTimeOffset> _clock;
    public int Capacity { get; }
    public event Action<LogEntry>? EntryAdded;

    /// <param name="eventsFile">Serilog logger writing events-*.log (optional).</param>
    /// <param name="trafficFile">Serilog logger writing traffic-*.log (optional).</param>
    /// <param name="appLog">Application logger (app-*.log). When given, <see cref="Message"/> goes through it and comes back via <see cref="Ingest"/>; otherwise messages are added directly.</param>
    public EventLogService(ILogger? eventsFile = null, ILogger? trafficFile = null, ILogger? appLog = null, int capacity = 5000, Func<DateTimeOffset>? clock = null)
    { _eventsFile = eventsFile; _trafficFile = trafficFile; _appLog = appLog; Capacity = capacity; _clock = clock ?? (() => DateTimeOffset.Now); }

    /// <summary>Sets the app logger after construction (the logger's sink needs this instance first).</summary>
    public ILogger? AppLog { get => _appLogLate ?? _appLog; set => _appLogLate = value; }
    private ILogger? _appLogLate;

    private static IReadOnlyList<KeyValuePair<string, string>> P((string Key, object? Value)[] p) =>
        p.Select(x => new KeyValuePair<string, string>(x.Key, Format(x.Value))).ToList();
    private static string Format(object? v) => v switch { null => "(none)", bool b => b ? "on" : "off", IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => v.ToString() ?? "" };

    private void Add(LogEntry e)
    {
        lock (_lock)
        {
            var l = _buf[e.Channel];
            l.AddLast(e);
            while (l.Count > Capacity) l.RemoveFirst();
        }
        if (e.Channel == LogChannel.Events) _eventsFile?.Write(e.Level, "{Line}", e.Render());
        else if (e.Channel == LogChannel.Traffic) _trafficFile?.Write(e.Level, "{Line}", e.Render());
        EntryAdded?.Invoke(e);
    }

    public void UserAction(string action, params (string Key, object? Value)[] parameters) =>
        Add(new LogEntry(_clock(), LogChannel.Events, LogEventLevel.Information, action, action, P(parameters)));

    public IEventOperation Begin(string action, params (string Key, object? Value)[] parameters)
    {
        var ps = P(parameters);
        Add(new LogEntry(_clock(), LogChannel.Events, LogEventLevel.Information, action, action, ps, EventOutcome.Started));
        return new Op(this, action, ps);
    }

    private sealed class Op(EventLogService log, string action, IReadOnlyList<KeyValuePair<string, string>> ps) : IEventOperation
    {
        private int _done;
        public string Action => action;
        private void End(LogEventLevel lvl, EventOutcome o, string? err, string? detail)
        {
            if (Interlocked.Exchange(ref _done, 1) == 1) return;
            log.Add(new LogEntry(log._clock(), LogChannel.Events, lvl, action, detail ?? action, ps, o, err));
        }
        public void Succeeded(string? detail = null) => End(LogEventLevel.Information, EventOutcome.Succeeded, null, detail);
        public void Failed(Exception ex) => End(LogEventLevel.Error, EventOutcome.Failed, ErrorText.Describe(ex), null);
        public void Failed(string error) => End(LogEventLevel.Error, EventOutcome.Failed, error, null);
        public void Cancelled(string? reason = null) => End(LogEventLevel.Warning, EventOutcome.Cancelled, reason, null);
    }

    public void Message(LogEventLevel level, string source, string text, Exception? ex = null)
    {
        var app = AppLog;
        if (app is not null) { app.ForContext("Op", source).Write(level, ex, "{Op}: {Text}", source, text); return; }
        Add(new LogEntry(_clock(), LogChannel.Messages, level, source, text, Array.Empty<KeyValuePair<string, string>>(), Error: ex is null ? null : ErrorText.Describe(ex)));
    }

    /// <summary>Adds a Serilog event (from the app logger's sink) to the Messages channel.</summary>
    public void Ingest(LogEvent e)
    {
        string source = "", text;
        if (e.Properties.TryGetValue("Op", out var op) && e.Properties.TryGetValue("Text", out var t))
        { source = Unquote(op); text = Unquote(t); }
        else
        {
            text = e.RenderMessage(CultureInfo.InvariantCulture);
            if (e.Properties.TryGetValue("SourceContext", out var sc)) source = Unquote(sc).Split('.').Last();
        }
        Add(new LogEntry(e.Timestamp, LogChannel.Messages, e.Level, source, text, Array.Empty<KeyValuePair<string, string>>(),
            Error: e.Exception is null ? null : ErrorText.Describe(e.Exception)));
    }
    private static string Unquote(LogEventPropertyValue v) => v is ScalarValue { Value: string s } ? s : v.ToString();

    public void Traffic(string direction, string text) =>
        Add(new LogEntry(_clock(), LogChannel.Traffic, LogEventLevel.Debug, direction, text.Replace("\r", "\\r").Replace("\n", "\\n"), Array.Empty<KeyValuePair<string, string>>()));

    public IReadOnlyList<LogEntry> Snapshot(LogChannel channel) { lock (_lock) return _buf[channel].ToList(); }
    public void Clear(LogChannel channel) { lock (_lock) _buf[channel].Clear(); }
}

/// <summary>Turns exceptions into meaningful one-line text (type, message, inner causes; UDS NRCs are decoded by the exception itself).</summary>
public static class ErrorText
{
    public static string Describe(Exception ex)
    {
        if (ex is AggregateException { InnerExceptions.Count: 1 } ag) ex = ag.InnerException!;
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
            parts.Add(e is InvalidOperationException or Uds.UdsNegativeResponseException ? e.Message : $"{e.GetType().Name}: {e.Message}");
        return string.Join(" <- ", parts.Distinct());
    }
    /// <summary>"Read As-Built block failed: ..." text for the Messages log and error dialogs.</summary>
    public static string ForOperation(string operation, Exception ex) => $"{operation} failed: {Describe(ex)}";
}

/// <summary>Level + text filter for the log panels.</summary>
public static class LogFilter
{
    public static bool Matches(LogEntry e, LogEventLevel minLevel, string? search)
    {
        if (e.Level < minLevel) return false;
        if (string.IsNullOrWhiteSpace(search)) return true;
        var s = search.Trim();
        return e.Render().Contains(s, StringComparison.OrdinalIgnoreCase);
    }
    public static IEnumerable<LogEntry> Apply(IEnumerable<LogEntry> entries, LogEventLevel minLevel, string? search) => entries.Where(e => Matches(e, minLevel, search));
}

/// <summary>Plain-text and CSV export of log entries, with optional VIN redaction.</summary>
public static class LogExporter
{
    public static string ToText(IEnumerable<LogEntry> entries, bool redactVins = true)
    {
        var s = string.Join(Environment.NewLine, entries.Select(e => e.Render())) + Environment.NewLine;
        return redactVins ? VinRedactor.Redact(s) : s;
    }

    public static string ToCsv(IEnumerable<LogEntry> entries, bool redactVins = true)
    {
        var sb = new StringBuilder("Time,Channel,Level,Action,Outcome,Parameters,Text,Error\r\n");
        foreach (var e in entries)
            sb.AppendJoin(',', new[] { e.Time.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture), e.Channel.ToString(), e.Level.ToString(), e.Action, e.OutcomeText, e.ParamText, e.Text, e.Error ?? "" }.Select(Csv)).Append("\r\n");
        var s = sb.ToString();
        return redactVins ? VinRedactor.Redact(s) : s;
    }

    private static string Csv(string v)
    {
        if (v.Length > 0 && "=+-@".Contains(v[0])) v = "'" + v; // no spreadsheet formula injection
        return v.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
    }

    public static string Export(IEnumerable<LogEntry> entries, string path, bool redactVins = true)
    {
        var text = Path.GetExtension(path).Equals(".csv", StringComparison.OrdinalIgnoreCase) ? ToCsv(entries, redactVins) : ToText(entries, redactVins);
        File.WriteAllText(path, text);
        return text;
    }
}

/// <summary>
/// Masks 17-character VINs (ISO 3779 alphabet, no I/O/Q) as "XXXXXXXXXXX" + last 6, both as ASCII text and as the hex-encoded ASCII
/// that appears in adapter traffic (e.g. a 0902 / 22F190 response).
/// </summary>
public static class VinRedactor
{
    private static readonly Regex Plain = new(@"(?<![A-Za-z0-9])(?=[A-HJ-NPR-Z0-9]{17}(?![A-Za-z0-9]))(?=[A-HJ-NPR-Z0-9]*[A-HJ-NPR-Z])(?=[A-HJ-NPR-Z0-9]*[0-9])[A-HJ-NPR-Z0-9]{17}", RegexOptions.Compiled);
    private static readonly Regex HexRun = new(@"[0-9A-Fa-f]{34,}", RegexOptions.Compiled);

    public static string MaskVin(string vin) => new string('X', 11) + vin[^6..];

    public static string Redact(string text)
    {
        var s = Plain.Replace(text, m => m.Value.StartsWith("XXXXXXXXXXX") ? m.Value : MaskVin(m.Value));
        return HexRun.Replace(s, m => RedactHex(m.Value));
    }

    private static string RedactHex(string run)
    {
        // Try both nibble alignments; replace any 34-hex-char window that decodes to a VIN.
        var chars = run.ToCharArray();
        for (int i = 0; i + 34 <= chars.Length; i++)
        {
            var ascii = new char[17]; bool ok = true;
            for (int k = 0; k < 17 && ok; k++)
            {
                int b = Convert.ToInt32(new string(chars, i + 2 * k, 2), 16);
                ascii[k] = (char)b; ok = b is >= '0' and <= '9' or >= 'A' and <= 'Z' && b is not ('I' or 'O' or 'Q');
            }
            var v = new string(ascii);
            if (!ok || !v.Any(char.IsDigit) || !v.Any(char.IsLetter) || v.StartsWith("XXXXXXXXXXX")) continue;
            var masked = Convert.ToHexString(Encoding.ASCII.GetBytes(MaskVin(v)));
            masked.CopyTo(0, chars, i, 34);
            i += 33;
        }
        return new string(chars);
    }
}
