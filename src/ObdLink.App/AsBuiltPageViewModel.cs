using System.Collections.ObjectModel;
using System.IO;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Logging;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>One line of the As-Built change history.</summary>
public sealed record AsBuiltHistoryRow(AsBuiltChange Change)
{
    public string When => Change.At.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    public string Where => Change.Where;
    public string Kind => Change.Kind;
    public int Bytes => Change.BytesChanged;
    public string Before => Change.Before;
    public string After => Change.After;
    public string Vin => "…" + Change.VinTail;
    public string Backup => Change.BackupFile;
    public string Note => Change.Note;
}

/// <summary>
/// As-Built page state and the offline file commands (no adapter traffic): the editable grid, the block number, the factory .ab
/// reference with its diff, the change history, and import / export of .ab and .abt files.
/// Split out of <see cref="MainViewModel"/>, step 1. Module reads, backup, write, restore, undo and compare stay in MainViewModel
/// for now (they need the module list, the bus router and the safety guard) and use this page's state.
/// The factory .ab is a read-only reference: nothing here writes to a module.
/// </summary>
public sealed class AsBuiltPageViewModel : ObservableObject
{
    /// <summary>Builds a logged command: (name, body).</summary>
    public delegate AsyncCommand CommandFactory(string name, Func<Task> body);
    /// <summary>Open dialog: (purpose, filter, initial folder) → chosen path; throws UserCancelledException on cancel.</summary>
    public delegate string OpenFileDialogFn(string purpose, string filter, string initialDir);
    /// <summary>Save dialog: (purpose, filter, suggested name, initial folder) → chosen path; throws UserCancelledException on cancel.</summary>
    public delegate string SaveFileDialogFn(string purpose, string filter, string name, string initialDir);

    private readonly IEventLog _log;
    private readonly OpenFileDialogFn _open;
    private readonly SaveFileDialogFn _save;
    private readonly Func<string, string, bool> _ask;
    private readonly Func<string> _asBuiltDir;
    private readonly Func<string> _backupsDir;
    private readonly Action _factoryChanged;

    public ObservableCollection<AsBuiltRow> AsBuiltRows { get; } = new();
    public ObservableCollection<AsBuiltDiff> FactoryDiff { get; } = new();
    public ObservableCollection<AsBuiltHistoryRow> AsBuiltHistory { get; } = new();

    private string _blk = "1";
    public string AsBuiltBlockNo { get => _blk; set { if (Set(ref _blk, value)) _log.UserAction("As-Built block entered", ("block", value)); } }

    /// <summary>The block number as a byte (hex, as FORScan shows it); throws a user-readable error when it is not one.</summary>
    public byte Block
    {
        get
        {
            try { return Convert.ToByte(AsBuiltBlockNo.Trim(), 16); }
            catch (Exception) { throw new InvalidOperationException($"Block '{AsBuiltBlockNo}' is not a valid block number (1-99, as in FORScan)"); }
        }
    }

    /// <summary>Factory reference baseline (Ford motorcraftservice.com .ab). Read-only reference. It is never written to a module automatically.</summary>
    public FordAbFile? Factory { get; private set; }
    private string _factoryInfo = "No factory .ab loaded"; public string FactoryInfo { get => _factoryInfo; set => Set(ref _factoryInfo, value); }
    /// <summary>Module ids that the factory .ab has As-Built data for (empty when none is loaded).</summary>
    public HashSet<uint> FactoryModules => Factory?.AsBuiltModules.Select(x => (uint)x).ToHashSet() ?? new();

    public AsyncCommand ImportFactoryAbCommand { get; }
    public AsyncCommand ExportFactoryAbCommand { get; }
    public AsyncCommand ImportAbtCommand { get; }
    public AsyncCommand ExportAbtCommand { get; }

    /// <param name="factoryChanged">Called after the factory .ab changes (the main view model refreshes module flags and backups).</param>
    public AsBuiltPageViewModel(IEventLog log, CommandFactory cmd, OpenFileDialogFn open, SaveFileDialogFn save, Func<string, string, bool> ask,
        Func<string> asBuiltDir, Func<string> backupsDir, Action factoryChanged)
    {
        _log = log; _open = open; _save = save; _ask = ask; _asBuiltDir = asBuiltDir; _backupsDir = backupsDir; _factoryChanged = factoryChanged;
        ImportFactoryAbCommand = cmd("Import factory .ab", ImportFactoryAb);
        ExportFactoryAbCommand = cmd("Export .ab", ExportFactoryAb);
        ImportAbtCommand = cmd("Import .abt", ImportAbt);
        ExportAbtCommand = cmd("Export .abt", ExportAbt);
    }

    /// <summary>Sets the factory reference (also used by the vehicle page's "Import downloaded .ab") and refreshes dependants.</summary>
    public void SetFactory(FordAbFile ab, string? info = null)
    {
        Factory = ab;
        _factoryChanged();
        if (info != null) FactoryInfo = info;
    }

    public static string VinTail(string vin) => vin.Length >= 6 ? vin[^6..] : vin;

    /// <summary>One-line count per diff kind, e.g. "Changed=2, Missing=1".</summary>
    public static string DiffSummary(IEnumerable<AsBuiltDiff> diff) => string.Join(", ", diff.GroupBy(d => d.Kind).Select(g => $"{g.Key}={g.Count()}"));

    /// <summary>Re-reads the change history from the backups folder (newest first).</summary>
    public void RefreshHistory()
    {
        AsBuiltHistory.Clear();
        try { foreach (var c in new AsBuiltHistory(_backupsDir()).Load().Reverse()) AsBuiltHistory.Add(new AsBuiltHistoryRow(c)); }
        catch (Exception ex) { _log.Message(LogEventLevel.Warning, "As-Built history", "Could not read the change history", ex); }
    }

    /// <summary>Replaces the grid with <paramref name="lines"/>.</summary>
    public void ShowLines(IEnumerable<AsBuiltLine> lines)
    {
        AsBuiltRows.Clear();
        foreach (var l in lines) AsBuiltRows.Add(AsBuiltRow.From(l));
    }

    /// <summary>Replaces the factory diff with <paramref name="diff"/>.</summary>
    public void ShowDiff(IEnumerable<AsBuiltDiff> diff)
    {
        FactoryDiff.Clear();
        foreach (var d in diff) FactoryDiff.Add(d);
    }

    private Task ImportFactoryAb()
    {
        var ab = FordAbFile.Load(_open("Factory .ab", "Ford As-Built|*.ab|All files|*.*", _asBuiltDir()));
        var bad = ab.Lines.Count(l => !l.ChecksumValid);
        SetFactory(ab, $"Factory .ab: VIN …{VinTail(ab.Vin)}, {ab.AsBuiltModules.Count()} modules, {ab.Lines.Count} lines, {bad} bad checksums");
        _log.Message(bad > 0 ? LogEventLevel.Warning : LogEventLevel.Information, "Import factory .ab", FactoryInfo);
        return Task.CompletedTask;
    }

    private Task ExportFactoryAb()
    {
        if (Factory is null) throw new InvalidOperationException("Import a factory .ab first");
        var f = _save("Factory .ab", "Ford As-Built|*.ab", "factory-redacted.ab", _asBuiltDir());
        var redact = _ask("Export", "Redact VIN (keep last 6) and module serials?");
        File.WriteAllText(f, (redact ? Factory.Redacted() : Factory).ToXml());
        _log.Message(LogEventLevel.Information, "Export .ab", $"Saved {f} (redacted: {redact})");
        return Task.CompletedTask;
    }

    private Task ImportAbt()
    {
        var abt = ForscanAbtFile.Load(_open("FORScan .abt", "FORScan As-Built backup|*.abt|All files|*.*", _asBuiltDir()));
        ShowLines(abt.Lines);
        var bad = abt.Lines.Count(l => !l.ChecksumValid);
        FactoryDiff.Clear();
        var info = $".abt: {string.Join(",", abt.Modules.Select(m => m.ToString("X3")))}, {abt.Lines.Count} lines, {bad} bad checksums";
        if (Factory is not null)
        {
            ShowDiff(abt.Modules.SelectMany(m => AsBuiltDiff.Compare(Factory.Lines, abt.Lines, m)).ToList());
            info += " | vs factory: " + DiffSummary(FactoryDiff);
        }
        FactoryInfo = info;
        _log.Message(bad > 0 ? LogEventLevel.Warning : LogEventLevel.Information, "Import .abt", info);
        return Task.CompletedTask;
    }

    private Task ExportAbt()
    {
        if (AsBuiltRows.Count == 0) throw new InvalidOperationException("Read or import As-Built rows first");
        var lines = AsBuiltRows.Select(r => AsBuiltLine.Create(r.Addr, Convert.FromHexString(r.Data.Replace(" ", "")))).ToList();
        var f = _save("FORScan .abt", "FORScan As-Built backup|*.abt", $"{lines[0].Address.Module:X3}.abt", _asBuiltDir());
        var abt = ForscanAbtFile.Parse(ForscanAbtFile.Format(lines));
        if (Factory is { Vin.Length: 17 } && _ask("Export", "Redact VIN (keep last 6) in block data?"))
            abt = abt.Redacted(Factory.Vin);
        File.WriteAllText(f, abt.ToText());
        _log.Message(LogEventLevel.Information, "Export .abt", $"Saved {f}");
        return Task.CompletedTask;
    }
}
