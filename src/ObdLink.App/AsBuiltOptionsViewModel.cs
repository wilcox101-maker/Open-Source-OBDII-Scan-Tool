using System.Collections.ObjectModel;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Logging;
using Serilog.Events;

namespace ObdLink.App;

/// <summary>
/// v0.15.0 As-Built options: the community sheet's options for the lines in the As-Built grid, shown by name with the current
/// value and its meaning. Pick a new value (or type raw digits) and "Apply to grid": the grid lines change and their checksums
/// are recomputed. Nothing is sent to the module here. The normal "Write + verify" path still does the backup, the
/// confirmations and the read-back. CommunityReported: the meanings are not from Ford.
/// v0.23.16 (W4): the sheet tab is picked from the module's F188/F111 part number (<see cref="DecodeTabResolver"/>), with the
/// reason in Info; a tab the user picks stays picked for that module. Options whose address has no grid line are listed as "not read".
/// </summary>
public sealed class AsBuiltOptionsViewModel : ObservableObject
{
    private readonly IEventLog _log;
    private readonly Func<IReadOnlyList<AsBuiltRow>> _rows;
    private readonly Func<ushort, IReadOnlyDictionary<string, string>?> _dids;
    /// <summary>Tabs the user picked, by module. Sticky across Refresh while the tab still applies.</summary>
    private readonly Dictionary<ushort, string> _manualTab = new();
    private ushort? _module;
    private TabChoice? _choice;
    private IReadOnlyList<FeatureOption>? _sheet;
    /// <summary>Pending new values by option key, kept while rows are filtered out of view.</summary>
    private readonly Dictionary<string, (FeatureOption Option, string Digits)> _pending = new();

    public ObservableCollection<OptionRowVm> Options { get; } = new();
    public ObservableCollection<string> Tabs { get; } = new();
    private string? _tab;
    /// <summary>Set by the dropdown (the user). Refresh sets the field directly, so only the user's picks become sticky.</summary>
    public string? SelectedTab
    {
        get => _tab;
        set
        {
            if (!Set(ref _tab, value)) return;
            if (value is not null && _module is ushort m)
            {
                _manualTab[m] = value;
                _choice = new TabChoice(value, TabChoiceReason.Manual, $"tab '{value}' picked by you");
                _log.Message(LogEventLevel.Information, "As-Built options", $"Sheet tab for {m:X3} set by user: '{value}'");
            }
            Rebuild();
        }
    }
    /// <summary>Why the current tab was chosen (W4). Also the first part of <see cref="Info"/>.</summary>
    public string TabReason => _choice?.Why ?? "";
    private string _search = ""; public string Search { get => _search; set { if (Set(ref _search, value)) Rebuild(); } }
    private bool _changedOnly; public bool ChangedOnly { get => _changedOnly; set { if (Set(ref _changedOnly, value)) Rebuild(); } }
    private string _info = "Read a block (or import an .abt), then Refresh to list its options.";
    public string Info { get => _info; private set => Set(ref _info, value); }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ApplyCommand { get; }
    public RelayCommand ResetCommand { get; }

    /// <param name="dids">The module's DIDs (F188, F111, …) from Identify vehicle, by TX id; null when not identified.</param>
    public AsBuiltOptionsViewModel(IEventLog log, Func<IReadOnlyList<AsBuiltRow>> rows, Func<ushort, IReadOnlyDictionary<string, string>?>? dids = null)
    {
        _log = log; _rows = rows; _dids = dids ?? (_ => null);
        RefreshCommand = new RelayCommand("As-Built options: refresh", log, _ => Refresh());
        ApplyCommand = new RelayCommand("As-Built options: apply to grid", log, _ => Apply());
        ResetCommand = new RelayCommand("As-Built options: discard pending values", log, _ => { _pending.Clear(); Rebuild(); });
    }

    private List<AsBuiltLine> GridLines()
    {
        var list = new List<AsBuiltLine>();
        foreach (var r in _rows())
        {
            try { list.Add(AsBuiltLine.Create(r.Addr, Convert.FromHexString(r.Data.Replace(" ", "")))); }
            catch (FormatException) { throw new InvalidOperationException($"{r.Address}: the grid data '{r.Data}' is not whole hex bytes. Fix it first."); }
        }
        return list;
    }

    /// <summary>Re-reads the grid and lists the options for its module(s). Pending values are kept where the option is still listed.</summary>
    public void Refresh()
    {
        _sheet ??= FeatureOption.LoadF150_2015_2020();
        var lines = GridLines();
        if (lines.Count == 0) { Options.Clear(); Tabs.Clear(); Info = "The As-Built grid is empty. Read a block or import an .abt first."; return; }
        var addrs = lines.Select(l => l.Address.ToString()).ToHashSet();
        var tabs = _sheet.Where(o => addrs.Contains(o.Address)).Select(o => o.Tab).Distinct().OrderBy(t => t).ToList();
        var module = lines[0].Address.Module;
        _module = null; // the dropdown resets while Tabs is rebuilt: that is not a user pick
        Tabs.Clear(); foreach (var t in tabs) Tabs.Add(t);
        _choice = DecodeTabResolver.Pick(module, tabs, _dids(module), _manualTab.GetValueOrDefault(module));
        _tab = _choice.Tab;
        _module = module;
        _log.Message(LogEventLevel.Information, "As-Built options", $"Sheet tab for {module:X3}: {_choice.Why}");
        OnPropertyChanged(nameof(SelectedTab)); OnPropertyChanged(nameof(TabReason));
        Rebuild();
    }

    private void Rebuild()
    {
        if (_sheet is null) return;
        List<AsBuiltLine> lines;
        try { lines = GridLines(); } catch (InvalidOperationException ex) { Info = ex.Message; return; }
        var byAddr = lines.ToDictionary(l => l.Address.ToString());
        var modules = lines.Select(l => l.Address.ToString()[..3]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Options.Clear();
        int read = 0, notRead = 0;
        // W4: every option of the tab for the grid's module(s); an address with no grid line is listed as "not read".
        foreach (var opt in _sheet.Where(o => o.Tab == SelectedTab && o.Address.Length >= 3 && modules.Contains(o.Address[..3])))
        {
            var row = new OptionRowVm(opt, byAddr.GetValueOrDefault(opt.Address), OnRowChanged);
            if (row.HasLine) read++; else notRead++;
            if (_pending.TryGetValue(row.Key, out var v)) row.NewValue = v.Digits;
            if (Search.Length > 0 && !(opt.Description.Contains(Search, StringComparison.OrdinalIgnoreCase) || opt.Address.Contains(Search, StringComparison.OrdinalIgnoreCase))) continue;
            if (ChangedOnly && !row.IsPending) continue;
            Options.Add(row);
        }
        var why = _choice is null ? "" : (_choice.NeedsConfirm ? "⚠ " : "") + char.ToUpperInvariant(_choice.Why[0]) + _choice.Why[1..] + ". ";
        Info = $"{why}{Options.Count} option(s) shown from tab '{SelectedTab}' (community sheet, UNVERIFIED): {read} read, {notRead} not read (no grid line), {_pending.Count} changed. Pick a value, Apply to grid, then Write + verify.";
    }

    private void OnRowChanged(OptionRowVm row)
    {
        if (row.IsPending) _pending[row.Key] = (row.Option, row.NewValue); else _pending.Remove(row.Key);
    }

    private void Apply()
    {
        var edits = _pending.Values.Select(p => new OptionEdit(p.Option, p.Digits)).ToList();
        if (edits.Count == 0) { Info = "No option has a new value."; return; }
        var lines = GridLines();
        var result = AsBuiltEditor.ApplyAll(lines, edits);
        var rows = _rows();
        int changed = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (ReferenceEquals(lines[i], result[i])) continue;
            var row = rows.First(r => r.Addr.Equals(result[i].Address));
            row.Data = Convert.ToHexString(result[i].Data);
            row.EnteredChecksum = result[i].Checksum.ToString("X2"); // generated, not typed: the change itself is checked by the editor
            changed++;
        }
        var summary = string.Join("; ", edits.Select(e => $"{e.Option.Address} '{e.Option.Description}' {e.Option.Current(lines.First(l => l.Address.ToString() == e.Option.Address))} -> {e.Digits.ToUpperInvariant()}"));
        _log.Message(LogEventLevel.Information, "As-Built options", $"Applied {edits.Count} option change(s) to {changed} grid line(s): {summary}");
        _pending.Clear();
        Rebuild();
        Info = $"Applied {edits.Count} change(s) to {changed} line(s) in the grid. Nothing has been written to the module yet: use Write + verify for each changed block.";
    }
}

/// <summary>One option in the grid: current digits and meaning, and an editable new value (pick from the list or type digits).</summary>
public sealed class OptionRowVm : ObservableObject
{
    public FeatureOption Option { get; }
    private readonly Action<OptionRowVm> _changed;
    /// <param name="line">The grid line for this address, or null when that block hasn't been read (state "not read", no editing).</param>
    public OptionRowVm(FeatureOption o, AsBuiltLine? line, Action<OptionRowVm> changed)
    {
        Option = o; _changed = changed;
        HasLine = line is not null;
        CurrentDigits = line is null ? null : o.Current(line);
        _new = CurrentDigits ?? "";
    }
    public string Key => $"{Option.Tab}|{Option.Address}|{Option.Pattern}|{Option.Description}";
    public string Address => Option.Address;
    public string Pattern => Option.Pattern;
    public string Description => Option.Description;
    public string? CurrentDigits { get; }
    /// <summary>False when the grid has no line for this address: the New cell is disabled.</summary>
    public bool HasLine { get; }
    public string CurrentMeaning => !HasLine ? "(not read)" : CurrentDigits is null ? "(line too short)" : Option.MeaningOf(CurrentDigits) ?? "(not in the sheet)";
    public IReadOnlyList<string> Choices => Option.Values.Select(v => v.Digits).ToList();
    public string ChoicesText => string.Join("\n", Option.Values.Select(v => v.Display));
    private string _new; public string NewValue { get => _new; set { if (Set(ref _new, (value ?? "").Trim().ToUpperInvariant())) { OnPropertyChanged(nameof(IsPending)); OnPropertyChanged(nameof(NewMeaning)); OnPropertyChanged(nameof(State)); _changed(this); } } }
    public string NewMeaning => Option.MeaningOf(NewValue) ?? (NewValue.Length == Option.DigitCount && NewValue.All(Uri.IsHexDigit) ? "(raw value)" : "(needs " + Option.DigitCount + " hex digit(s))");
    public bool IsPending => CurrentDigits is not null && !string.Equals(NewValue, CurrentDigits, StringComparison.OrdinalIgnoreCase);
    public string State => OptionStates.Text(OptionStates.Of(HasLine, CurrentDigits, NewValue));
}
