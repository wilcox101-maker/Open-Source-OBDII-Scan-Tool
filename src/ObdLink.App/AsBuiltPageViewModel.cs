using System.Collections.ObjectModel;
using System.IO;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Logging;
using ObdLink.Core.Modules;
using ObdLink.Core.Safety;
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
/// What the As-Built page needs from the main view model: the connection, the module list and its As-Built flags,
/// bus routing, the safety guard and the shared batch runner. Implemented explicitly by <see cref="MainViewModel"/>.
/// </summary>
public interface IAsBuiltHost
{
    /// <summary>As-Built service of the current connection; throws "Not connected" when there is none.</summary>
    AsBuiltService AsBuilt { get; }
    /// <summary>The vehicle's VIN as read (used for backups and the restore VIN check).</summary>
    string Vin { get; }
    /// <summary>VIN masked for file names, or NOVIN.</summary>
    string MaskedVin { get; }
    ModuleRow? SelectedModule { get; }
    ModuleRow? FindModule(uint tx);
    /// <summary>The selected module, after offering to connect / scan / pick one; null = the user declined (hint already shown).</summary>
    Task<ModuleRow?> EnsureModuleAsync(bool asBuilt);
    /// <summary>Runs <paramref name="work"/> on the module's bus. State-changing work passes <paramref name="allowFallback"/> = false.</summary>
    Task OnBus(ModuleRow m, Func<Task> work, bool allowFallback);
    SafetyGuard Safety();
    /// <summary>Records whether the module has As-Built data and refreshes the page's enabled state.</summary>
    void MarkAsBuilt(ModuleRow row, AsBuiltSupport support);
    /// <summary>Re-scans backup files, history and As-Built flags.</summary>
    void RefreshBackups();
    void Info(string title, string message);
    string ModuleHint { set; }
    string BatchText { set; }
    /// <summary>Connect, scan if needed, then run <paramref name="work"/> over the scanned (As-Built) modules. Null = nothing ran (hint shown).</summary>
    Task<List<BatchItemResult>?> ModuleBatchAsync(string what, bool asBuilt, Func<ModuleRow, CancellationToken, Task<BatchItemResult>> work);
}

/// <summary>
/// As-Built page state and the offline file commands (no adapter traffic): the editable grid, the block number, the factory .ab
/// reference with its diff, the change history, and import / export of .ab and .abt files.
/// Split out of <see cref="MainViewModel"/>. Step 1 moved the state and file commands; step 2 the module commands (read, read all,
/// back up all modules, backup, write, restore, undo, compare), which reach the vehicle only through <see cref="IAsBuiltHost"/>.
/// Write, restore and undo go through the safety guard (read-only gate, confirmation, typed WRITE, battery) and never hunt
/// another bus. The factory .ab is a read-only reference: nothing writes it to a module.
/// </summary>
public sealed class AsBuiltPageViewModel : ObservableObject
{
    /// <summary>Builds a logged adapter command: (name, body, log parameters, can-execute).</summary>
    public delegate AsyncCommand CommandFactory(string name, Func<Task> body, Func<(string, object?)[]>? parameters = null, Func<bool>? canExecute = null);
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
    private readonly IAsBuiltHost _host;

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
    public AsyncCommand ReadAsBuiltCommand { get; }
    public AsyncCommand ReadAllBlocksCommand { get; }
    public AsyncCommand ReadAllModulesAsBuiltCommand { get; }
    public AsyncCommand BackupAsBuiltCommand { get; }
    public AsyncCommand WriteAsBuiltCommand { get; }
    public AsyncCommand RestoreAsBuiltCommand { get; }
    public AsyncCommand UndoAsBuiltCommand { get; }
    public AsyncCommand CompareToFactoryCommand { get; }

    /// <param name="factoryChanged">Called after the factory .ab changes (the main view model refreshes module flags and backups).</param>
    /// <param name="actionsEnabled">Whether the selected module may have As-Built (can-execute of the module commands).</param>
    public AsBuiltPageViewModel(IEventLog log, IAsBuiltHost host, CommandFactory cmd, OpenFileDialogFn open, SaveFileDialogFn save, Func<string, string, bool> ask,
        Func<string> asBuiltDir, Func<string> backupsDir, Action factoryChanged, Func<bool> actionsEnabled)
    {
        _log = log; _host = host; _open = open; _save = save; _ask = ask; _asBuiltDir = asBuiltDir; _backupsDir = backupsDir; _factoryChanged = factoryChanged;
        (string, object?)[] Mod() => new (string, object?)[] { ("module", _host.SelectedModule?.Label), ("addr", _host.SelectedModule?.Address) };
        (string, object?)[] ModBlk() => new (string, object?)[] { ("module", _host.SelectedModule?.Label), ("addr", _host.SelectedModule?.Address), ("block", AsBuiltBlockNo) };
        ImportFactoryAbCommand = cmd("Import factory .ab", ImportFactoryAb);
        ExportFactoryAbCommand = cmd("Export .ab", ExportFactoryAb);
        ImportAbtCommand = cmd("Import .abt", ImportAbt);
        ExportAbtCommand = cmd("Export .abt", ExportAbt);
        ReadAllBlocksCommand = cmd("Read all As-Built blocks", ReadAllBlocksAsync, Mod, actionsEnabled);
        ReadAllModulesAsBuiltCommand = cmd("Back up As-Built of all modules", ReadAllModulesAsync);
        ReadAsBuiltCommand = cmd("Read As-Built block", ReadBlockAsync, ModBlk, actionsEnabled);
        BackupAsBuiltCommand = cmd("Backup As-Built block", BackupAsync, ModBlk, actionsEnabled);
        WriteAsBuiltCommand = cmd("Write + verify As-Built block", WriteAsync, ModBlk, actionsEnabled);
        RestoreAsBuiltCommand = cmd("Restore As-Built from backup", RestoreAsync, null, actionsEnabled);
        UndoAsBuiltCommand = cmd("Undo last As-Built write", UndoAsync, Mod, actionsEnabled);
        CompareToFactoryCommand = cmd("Compare to factory", CompareAsync, Mod, actionsEnabled);
    }

    /// <summary>Re-evaluates can-execute of the module commands (after the selected module or its As-Built support changed).</summary>
    public void RaiseModuleCommands()
    {
        ReadAllBlocksCommand.RaiseCanExecuteChanged();
        ReadAsBuiltCommand.RaiseCanExecuteChanged();
        BackupAsBuiltCommand.RaiseCanExecuteChanged();
        WriteAsBuiltCommand.RaiseCanExecuteChanged();
        CompareToFactoryCommand.RaiseCanExecuteChanged();
        RestoreAsBuiltCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Refuses a module known to have no As-Built (NRC 0x31 on DE00) before anything is sent.</summary>
    public static void RequireAsBuilt(ModuleRow m)
    {
        if (m.AsBuiltSupport == AsBuiltSupport.Unsupported)
            throw new AsBuiltNotSupportedException(m.Def.TxId, 0xDE00, m.Acronym);
    }

    private AsBuiltService AsBuilt => _host.AsBuilt;
    private string _vin => _host.Vin;   // same name as in MainViewModel so the write-path source scans read the same
    private Task OnBus(ModuleRow m, Func<Task> work, bool allowFallback = true) => _host.OnBus(m, work, allowFallback);

    private async Task ReadBlockAsync()
    {
        if (await _host.EnsureModuleAsync(true) is not { } m) return;
        RequireAsBuilt(m);
        var did = AsBuiltBlock.DidForBlock(Block);
        AsBuiltBlock b = null!;
        await OnBus(m, async () => b = await AsBuilt.ReadBlockAsync((ushort)m.Def.TxId, Block));
        _host.MarkAsBuilt(m, AsBuiltSupport.Supported);
        ShowLines(b.Lines());
        _log.Message(LogEventLevel.Information, "Read As-Built block", $"{_host.SelectedModule?.Label} block {AsBuiltBlockNo} (DID 0x{did:X4}): {b.Data.Length} bytes, {AsBuiltRows.Count} lines");
    }

    private async Task ReadAllBlocksAsync()
    {
        if (await _host.EnsureModuleAsync(true) is not { } m) return;
        RequireAsBuilt(m);
        List<AsBuiltBlock> blocks = new();
        await OnBus(m, async () => blocks = await AsBuiltBatch.ReadAllBlocksAsync((b, ct) => AsBuilt.ReadBlockAsync((ushort)m.Def.TxId, b, ct)));
        _host.MarkAsBuilt(m, AsBuiltSupport.Supported);
        ShowLines(blocks.SelectMany(b => b.Lines()));
        var path = await AsBuiltBatch.SaveAsync(AsBuiltBatch.DateFolder(_asBuiltDir(), DateTime.Now), _host.MaskedVin, (ushort)m.Def.TxId, blocks, DateTime.Now);
        _host.RefreshBackups();
        var hint = $"{m.Acronym}: {blocks.Count} block(s), {AsBuiltRows.Count} lines - saved {path}";
        _host.ModuleHint = hint;
        _log.Message(LogEventLevel.Information, "Read all As-Built blocks", hint);
    }

    private async Task ReadAllModulesAsync()
    {
        var now = DateTime.Now; var dir = AsBuiltBatch.DateFolder(_asBuiltDir(), now);
        var res = await _host.ModuleBatchAsync("As-Built backup", true, async (m, ct) =>
        {
            if (m.AsBuiltSupport == AsBuiltSupport.Unsupported)
                return new BatchItemResult(m.Def, false, AsBuiltNotSupportedException.Describe(m.Acronym, m.Def.TxId, 0xDE00), NoAsBuilt: true);
            try
            {
                List<AsBuiltBlock> blocks = new();
                await OnBus(m, async () => blocks = await AsBuiltBatch.ReadAllBlocksAsync((b, c) => AsBuilt.ReadBlockAsync((ushort)m.Def.TxId, b, c), ct: ct));
                var p = await AsBuiltBatch.SaveAsync(dir, _host.MaskedVin, (ushort)m.Def.TxId, blocks, now, ct);
                m.SetAsBuiltSupport(AsBuiltSupport.Supported);
                return new BatchItemResult(m.Def, true, $"{blocks.Count} block(s)", p);
            }
            catch (AsBuiltNotSupportedException ex)
            {
                m.SetAsBuiltSupport(AsBuiltSupport.Unsupported);
                return new BatchItemResult(m.Def, false, ex.Message, NoAsBuilt: true);
            }
        });
        if (res is null) return;
        _host.RefreshBackups();
        if (res.Count == 0) return;
        var text = AsBuiltBatch.BackupSummary(res, dir);
        _host.BatchText = text;
        _log.Message(AsBuiltBatch.HasHardFailure(res) ? LogEventLevel.Warning : LogEventLevel.Information, "Back up As-Built of all modules",
            text + "; " + string.Join("; ", res.Select(AsBuiltBatch.Line)));
        _host.Info("As-Built backup", text + "\n\n" + string.Join("\n", res.Select(AsBuiltBatch.Line)));
    }

    private async Task BackupAsync()
    {
        if (await _host.EnsureModuleAsync(true) is not { } m) return;
        RequireAsBuilt(m);
        string p = ""; await OnBus(m, async () => p = await AsBuilt.BackupAsync(_vin, (ushort)m.Def.TxId, new[] { Block }));
        _host.MarkAsBuilt(m, AsBuiltSupport.Supported);
        _host.RefreshBackups(); _host.Info("Backup", $"Saved {p}");
    }

    private async Task WriteAsync()
    {
        if (_host.SelectedModule is not { } m) return;
        RequireAsBuilt(m);
        // v0.6.1 audit: only this module's lines for this block are written (the grid may hold other blocks or modules),
        // lines must be complete and every typed checksum must match. Length is checked against the module in AsBuiltService.
        var newBlock = AsBuiltWritePlan.BuildBlock((ushort)m.Def.TxId, Block, AsBuiltRows.Select(r => r.ToEdit()));
        await _host.Safety().RequireAsync(new[] { new ConfirmationStep("Target", $"{m.Label} (0x{m.Def.TxId:X3}) block {Block:X2} on {ModuleReplies.BusName(m.Bus)}, {newBlock.Data.Length} bytes. A write does not try another bus.") });
        AsBuiltWriteResult res = null!;
        await OnBus(m, async () => res = await AsBuilt.WriteBlockAsync(_vin, newBlock), allowFallback: false);
        _host.MarkAsBuilt(m, AsBuiltSupport.Supported);
        _host.RefreshBackups();
        if (!res.Written) { _host.Info("Write", res.Note); return; }
        _host.Info("Write", $"{res.BytesChanged} byte(s) written and verified by read-back on {ModuleReplies.BusName(m.Bus)}.{(res.Note.Length > 0 ? " " + res.Note : "")}\nBackup of the previous contents: {res.BackupPath}");
    }

    private async Task RestoreAsync()
    {
        var f = _open("As-Built backup", "As-Built backup|*.asbuilt.json", _backupsDir());
        var bk = AsBuiltBackup.Deserialize(File.ReadAllText(f));
        var row = _host.FindModule(bk.Module)
            ?? throw new InvalidOperationException($"Backup is for module 0x{bk.Module:X3}, which is not in this vehicle's module list. Nothing was written.");
        RequireAsBuilt(row);
        await _host.Safety().RequireAsync(new[] { new ConfirmationStep("Target", $"Restore 0x{row.Def.TxId:X3} ({row.Label}) on {ModuleReplies.BusName(row.Bus)} from the backup of VIN …{VinTail(bk.Vin)}. A restore does not try another bus.") });
        AsBuiltWriteResult res = null!;
        await OnBus(row, async () => res = await AsBuilt.RestoreAsync(f, _vin), allowFallback: false);
        _host.RefreshBackups();
        if (!res.Written) { _host.Info("Restore", res.Note); return; }
        _host.Info("Restore", $"Restored and verified by read-back ({res.BytesChanged} byte(s) changed).{(res.Note.Length > 0 ? " " + res.Note : "")}\nBackup taken before the restore: {res.BackupPath}");
    }

    private async Task UndoAsync()
    {
        if (_host.SelectedModule is not { } m) throw new InvalidOperationException("Select a module first");
        RequireAsBuilt(m);
        var hist = new AsBuiltHistory(_backupsDir());
        var last = hist.LastUndoable((ushort)m.Def.TxId)
            ?? throw new InvalidOperationException($"No recorded change of {m.Label} with its backup still on disk. Nothing was written.");
        await _host.Safety().RequireAsync(new[] { new ConfirmationStep("Undo", $"Undo the {last.Kind} of {last.Where} on {last.At.LocalDateTime:yyyy-MM-dd HH:mm} ({last.BytesChanged} byte(s)) by restoring {last.BackupFile}? The normal restore checks follow (VIN match, fresh backup, typed WRITE, read-back).") });
        AsBuiltWriteResult res = null!;
        await OnBus(m, async () => res = await AsBuilt.RestoreAsync(hist.BackupPathOf(last), _vin), allowFallback: false);
        _host.RefreshBackups();
        if (!res.Written) { _host.Info("Undo", res.Note); return; }
        _host.Info("Undo", $"Restored and verified by read-back ({res.BytesChanged} byte(s) changed).{(res.Note.Length > 0 ? " " + res.Note : "")}\nBackup taken before the undo: {res.BackupPath}");
    }

    private async Task CompareAsync()
    {
        if (Factory is not { } factory) throw new InvalidOperationException("Import a factory .ab first");
        if (_host.SelectedModule is { } m) RequireAsBuilt(m);
        var module = (ushort)(_host.SelectedModule?.Def.TxId ?? throw new InvalidOperationException("Select a module in the module list first"));
        var current = new List<AsBuiltLine>();
        foreach (var b in factory.Blocks(module))
            current.AddRange(factory.SplitLike(module, b, (await AsBuilt.ReadBlockAsync(module, b)).Data));
        if (_host.SelectedModule is { } ok) _host.MarkAsBuilt(ok, AsBuiltSupport.Supported);
        ShowDiff(AsBuiltDiff.Compare(factory.Lines, current, module));
        _log.Message(LogEventLevel.Information, "Compare to factory", DiffSummary(FactoryDiff));
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
