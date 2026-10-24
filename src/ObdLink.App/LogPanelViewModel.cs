using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using ObdLink.Core.Logging;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>One tab of the bottom log panel (Events / Messages / Adapter traffic): level filter, search, clear, copy, export, auto-scroll.</summary>
public sealed class LogPanelViewModel : ObservableObject
{
    private readonly IEventLog _log; private readonly LogChannel _channel;
    private readonly List<LogEntry> _all = new();
    public const int MaxRows = 5000;
    public string Title { get; }
    public ObservableCollection<LogEntry> Entries { get; } = new();
    public IReadOnlyList<LogEventLevel> Levels { get; } = new[] { LogEventLevel.Verbose, LogEventLevel.Debug, LogEventLevel.Information, LogEventLevel.Warning, LogEventLevel.Error };
    private LogEventLevel _min; public LogEventLevel MinLevel { get => _min; set { if (Set(ref _min, value)) { Refilter(); _log.UserAction($"{Title} log: level filter", ("level", value)); } } }
    private string _search = ""; public string Search { get => _search; set { if (Set(ref _search, value)) Refilter(); } }
    private bool _auto = true; public bool AutoScroll { get => _auto; set { if (Set(ref _auto, value)) _log.UserAction($"{Title} log: auto-scroll", ("on", value)); } }
    private bool _redact = true; public bool RedactOnExport { get => _redact; set { if (Set(ref _redact, value)) _log.UserAction($"{Title} log: redact VINs on export", ("on", value)); } }
    public RelayCommand ClearCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand ExportCommand { get; }

    public LogPanelViewModel(string title, LogChannel channel, IEventLog log, LogEventLevel defaultLevel)
    {
        Title = title; _channel = channel; _log = log; _min = defaultLevel;
        ClearCommand = new($"{title} log: Clear", channel == LogChannel.Events ? null : log, _ => { _all.Clear(); Entries.Clear(); _log.Clear(_channel); });
        CopyCommand = new($"{title} log: Copy selected", log, p =>
        {
            var sel = (p as IList)?.OfType<LogEntry>().OrderBy(e => e.Time).ToList() ?? new();
            if (sel.Count == 0) sel = Entries.ToList();
            Clipboard.SetText(LogExporter.ToText(sel, RedactOnExport));
        });
        ExportCommand = new($"{title} log: Export", log, _ =>
        {
            var dlg = new SaveFileDialog { Filter = "Text|*.txt|CSV|*.csv", FileName = $"{channel.ToString().ToLowerInvariant()}-{DateTime.Now:yyyyMMdd-HHmmss}.txt" };
            if (dlg.ShowDialog() != true) throw new UserCancelledException("Save dialog cancelled");
            LogExporter.Export(Entries.ToList(), dlg.FileName, RedactOnExport);
            _log.Message(LogEventLevel.Information, "Export log", $"{Entries.Count} {title} entries exported to {Path.GetFileName(dlg.FileName)} (VIN redaction {(RedactOnExport ? "on" : "off")})");
        });
    }

    /// <summary>Called on the UI thread for each new entry of this channel.</summary>
    public void Add(LogEntry e)
    {
        _all.Add(e);
        if (_all.Count > MaxRows) { var old = _all[0]; _all.RemoveAt(0); Entries.Remove(old); }
        if (LogFilter.Matches(e, MinLevel, Search)) Entries.Add(e);
    }

    private void Refilter()
    {
        Entries.Clear();
        foreach (var e in LogFilter.Apply(_all, MinLevel, Search)) Entries.Add(e);
    }
}
