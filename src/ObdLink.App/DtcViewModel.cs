using System.Collections.ObjectModel;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Dtc;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Serilog.Events;

namespace ObdLink.App;

public sealed record DtcRow(string Source, string Code, string Status, string Description)
{
    /// <summary>v0.8.0: plain-English status (confirmed / pending / history, failing now, lamp).</summary>
    public string State { get; init; } = "";
    /// <summary>Module transmit id for UDS DTCs (null for OBD-II rows). Hidden from the grid; used by Details.</summary>
    [System.ComponentModel.Browsable(false)] public uint? ModuleTx { get; init; }
    [System.ComponentModel.Browsable(false)] public uint? RawDtc { get; init; }
    [System.ComponentModel.Browsable(false)] public byte RawStatus { get; init; }
    public static DtcRow FromUds(string label, uint tx, DtcRecord d, string description) =>
        new(label, d.Display, $"0x{d.Status:X2}", description) { State = DtcStatusBits.Summary(d.Status), ModuleTx = tx, RawDtc = d.Code, RawStatus = d.Status };
}

/// <summary>
/// DTCs page: OBD-II stored / pending / permanent DTCs and freeze frame, UDS module DTCs (one module or all), details, and clear.
/// Split out of <see cref="MainViewModel"/>. Every command is built by the main view model's command factory, so it holds the shared
/// bus lock and is refused while the bus monitor owns the adapter, exactly as before. Clear is the only state-changing command:
/// it goes through the safety guard (read-only gate + confirmation) and never hunts another bus.
/// </summary>
public sealed class DtcViewModel : ObservableObject
{
    /// <summary>Builds an adapter command: (name, body, log parameters).</summary>
    public delegate AsyncCommand CommandFactory(string name, Func<Task> body, Func<(string, object?)[]>? parameters = null);
    /// <summary>Runs <paramref name="work"/> over every scanned module as a cancellable batch; null when there was nothing to run.</summary>
    public delegate Task<List<BatchItemResult>?> ModuleBatch(string what, Func<ModuleRow, CancellationToken, Task<BatchItemResult>> work);

    private readonly DtcDatabase _db;
    private readonly IEventLog _log;
    private readonly Func<ElmDiagChannel> _ch;
    private readonly Func<Task> _toObdBus;
    private readonly Func<ModuleRow?> _selected;
    private readonly Func<bool, Task<ModuleRow?>> _ensureModule;
    private readonly Func<uint, ModuleRow?> _findModule;
    private readonly Func<ModuleRow, Func<Task>, bool, Task> _onBus;
    private readonly Func<SafetyGuard> _safety;
    private readonly ModuleBatch _batch;
    private readonly Action<string> _batchText;
    private readonly Action<string, string> _info;

    public ObservableCollection<DtcRow> Dtcs { get; } = new();
    private DtcRow? _selectedDtc; public DtcRow? SelectedDtc { get => _selectedDtc; set => Set(ref _selectedDtc, value); }

    public AsyncCommand ReadObdDtcsCommand { get; }
    public AsyncCommand ReadPendingDtcsCommand { get; }
    public AsyncCommand ReadPermanentDtcsCommand { get; }
    public AsyncCommand ReadFreezeFrameCommand { get; }
    public AsyncCommand ReadModuleDtcsCommand { get; }
    public AsyncCommand ReadAllModuleDtcsCommand { get; }
    public AsyncCommand DtcDetailsCommand { get; }
    public AsyncCommand ClearModuleDtcsCommand { get; }

    /// <param name="toObdBus">Switches the adapter to HS-CAN for OBD-II requests when it is on another bus.</param>
    /// <param name="ensureModule">The selected module, after asking the user to scan/pick one; null = cancelled.</param>
    public DtcViewModel(DtcDatabase db, IEventLog log, CommandFactory cmd, Func<ElmDiagChannel> ch, Func<Task> toObdBus,
        Func<ModuleRow?> selected, Func<bool, Task<ModuleRow?>> ensureModule, Func<uint, ModuleRow?> findModule,
        Func<ModuleRow, Func<Task>, bool, Task> onBus, Func<SafetyGuard> safety, ModuleBatch batch, Action<string> batchText, Action<string, string> info)
    {
        _db = db; _log = log; _ch = ch; _toObdBus = toObdBus; _selected = selected; _ensureModule = ensureModule; _findModule = findModule;
        _onBus = onBus; _safety = safety; _batch = batch; _batchText = batchText; _info = info;
        (string, object?)[] Mod() => new (string, object?)[] { ("module", _selected()?.Label), ("addr", _selected()?.Address) };

        ReadObdDtcsCommand = cmd("Read OBD-II DTCs", ReadStoredAsync);
        // v0.8.0 Phase 1 (read-only): pending / permanent OBD-II DTCs, freeze frame, and UDS DTC details.
        ReadPendingDtcsCommand = cmd("Read pending OBD-II DTCs", () => ReadObdListAsync(0x07, "pending (07)"));
        ReadPermanentDtcsCommand = cmd("Read permanent OBD-II DTCs", () => ReadObdListAsync(0x0A, "permanent (0A)"));
        ReadFreezeFrameCommand = cmd("Read OBD-II freeze frame", ReadFreezeFrameAsync);
        ReadModuleDtcsCommand = cmd("Read module DTCs", ReadModuleAsync, Mod);
        ReadAllModuleDtcsCommand = cmd("Read DTCs from all modules", ReadAllModulesAsync);
        DtcDetailsCommand = cmd("DTC details", DetailsAsync);
        ClearModuleDtcsCommand = cmd("Clear module DTCs", ClearAsync, Mod);
    }

    private ElmDiagChannel Ch => _ch();

    private async Task ReadStoredAsync()
    {
        Dtcs.Clear();
        foreach (var (ecu, code) in await new ObdService(Ch).ReadStoredDtcsAsync()) Dtcs.Add(new($"{ecu:X3}", code, "stored", _db.Describe(code)) { State = "stored (03)" });
        _log.Message(LogEventLevel.Information, "Read OBD-II DTCs", $"{Dtcs.Count} DTCs");
    }

    private async Task ReadObdListAsync(byte service, string label)
    {
        await _toObdBus();
        Dtcs.Clear();
        foreach (var (ecu, code) in await ObdDtcExtras.ReadListAsync(Ch, service))
            Dtcs.Add(new($"{ecu:X3}", code, label, _db.Describe(code)) { State = label });
        _log.Message(LogEventLevel.Information, $"Read {label} DTCs", $"{Dtcs.Count} DTCs");
    }

    private async Task ReadFreezeFrameAsync()
    {
        await _toObdBus();
        var ff = await ObdDtcExtras.ReadFreezeFrameAsync(Ch);
        var text = ff.Count == 0 ? "No ECU returned a freeze frame (mode 02). Nothing is stored, or the ECU does not support it."
            : string.Join(Environment.NewLine, ff.Select(v => $"ECU 0x{v.Ecu:X3}  PID {v.Pid:X2}  {v.Name}: {v.Value}"));
        _log.Message(LogEventLevel.Information, "Freeze frame", text);
        _info("Freeze frame (mode 02, frame 0)", text);
    }

    private async Task ReadModuleAsync()
    {
        if (await _ensureModule(false) is not { } m) return;
        Dtcs.Clear();
        await _onBus(m, async () => { foreach (var d in await new UdsClient(Ch, (ushort)m.Def.TxId).ReadDtcsByStatusAsync()) Dtcs.Add(DtcRow.FromUds(m.Label, m.Def.TxId, d, _db.Describe(d.Display))); }, true);
        _log.Message(LogEventLevel.Information, "Read module DTCs", $"{m.Label}: {Dtcs.Count} DTCs");
    }

    private async Task ReadAllModulesAsync()
    {
        Dtcs.Clear();
        var res = await _batch("Module DTCs", async (m, ct) =>
        {
            List<DtcRecord> list = new();
            await _onBus(m, async () => list = await new UdsClient(Ch, (ushort)m.Def.TxId).ReadDtcsByStatusAsync(ct: ct), true);
            foreach (var d in list) Dtcs.Add(DtcRow.FromUds(m.Label, m.Def.TxId, d, _db.Describe(d.Display)));
            return new BatchItemResult(m.Def, true, $"{list.Count} DTC(s)");
        });
        if (res is null || res.Count == 0) return;
        var text = BatchSummary(res, Dtcs.Count);
        _batchText(text);
        _log.Message(LogEventLevel.Information, "Read DTCs from all modules", text);
    }

    /// <summary>One line for the batch status: modules read, DTC count, failed module acronyms.</summary>
    public static string BatchSummary(IReadOnlyCollection<BatchItemResult> res, int dtcCount) =>
        $"Module DTCs: {res.Count(r => r.Ok)} of {res.Count} modules read, {dtcCount} DTC(s)"
        + (res.Any(r => !r.Ok) ? " - failed: " + string.Join(", ", res.Where(r => !r.Ok).Select(r => r.Module.Acronym)) : "");

    private async Task DetailsAsync()
    {
        if (SelectedDtc is not { ModuleTx: uint tx, RawDtc: uint raw } row) throw new InvalidOperationException("Select a module DTC (read with 19 02) first. OBD-II rows have no UDS details; use Freeze frame.");
        var m = _findModule(tx) ?? throw new InvalidOperationException($"Module 0x{tx:X3} is not in the module list.");
        DtcDetail d = null!;
        await _onBus(m, async () => d = await new UdsDtcReader(new UdsClient(Ch, (ushort)tx)).DetailAsync(raw, row.RawStatus), true);
        var text = $"{row.Source}{Environment.NewLine}{_db.Describe(row.Code)}{Environment.NewLine}{Environment.NewLine}" + UdsDtcReader.Format(row.Code, d);
        _log.Message(LogEventLevel.Information, "DTC details", text);
        _info($"DTC {row.Code} details", text);
    }

    private async Task ClearAsync()
    {
        if (_selected() is not { } m) return;
        await _safety().RequireAsync(new[] { new ConfirmationStep("Clear DTCs", $"Clear all DTCs in {m.Label} on {ModuleReplies.BusName(m.Bus)}? Freeze-frame data will be lost. Other buses are not tried.") });
        await _onBus(m, () => new UdsClient(Ch, (ushort)m.Def.TxId).ClearDtcsAsync(), false); // state-changing: allowFallback = false, never hunt another bus
    }
}
