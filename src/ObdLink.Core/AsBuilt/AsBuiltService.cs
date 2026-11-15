using ObdLink.Core.Safety;
using ObdLink.Core.Security;
using ObdLink.Core.Uds;
using Serilog;

namespace ObdLink.Core.AsBuilt;

public sealed class AsBuiltService
{
    private readonly Func<uint, UdsClient> _clientFactory;
    private readonly SafetyGuard _safety;
    private readonly string _backupDir;
    public byte? SecurityLevel { get; set; } // null = don't attempt 0x27 [whether As-Built writes need it is module-dependent, UNVERIFIED]

    public AsBuiltService(Func<uint, UdsClient> clientFactory, SafetyGuard safety, string backupDir)
    { _clientFactory = clientFactory; _safety = safety; _backupDir = backupDir; Directory.CreateDirectory(backupDir); }

    public async Task<AsBuiltBlock> ReadBlockAsync(ushort module, byte block, CancellationToken ct = default)
    {
        var did = AsBuiltBlock.DidForBlock(block);
        try
        {
            var data = await _clientFactory(module).ReadDataByIdentifierAsync(did, ct);
            return new AsBuiltBlock { Module = module, Block = block, Data = data };
        }
        catch (UdsNegativeResponseException ex) when (ex.Service == 0x22 && ex.Nrc == 0x31)
        {
            // NRC 0x31 on an As-Built DID means the module has no As-Built data. Do not wrap the UDS exception:
            // ErrorText walks InnerException and would put the raw rejection text back on screen.
            throw new AsBuiltNotSupportedException(module, did);
        }
    }

    public async Task<string> BackupAsync(string vin, ushort module, IEnumerable<byte> blocks, CancellationToken ct = default)
    {
        var read = new List<AsBuiltBlock>();
        foreach (var b in blocks) read.Add(await ReadBlockAsync(module, b, ct));
        return await SaveBackupAsync(vin, module, read, "", ct);
    }

    private async Task<string> SaveBackupAsync(string vin, ushort module, IEnumerable<AsBuiltBlock> blocks, string tag, CancellationToken ct)
    {
        var dict = blocks.ToDictionary(b => b.Block.ToString("X2"), b => Convert.ToHexString(b.Data));
        var bk = new AsBuiltBackup(vin, DateTimeOffset.Now, module, dict);
        var name = $"{SafePaths.Token(vin)}_{module:X3}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{tag}.asbuilt.json";
        var path = Path.Combine(_backupDir, name);
        if (!SafePaths.IsUnder(_backupDir, path)) throw new InvalidOperationException("Backup path escaped the backup folder");
        await File.WriteAllTextAsync(path, bk.Serialize(), ct);
        Log.Information("As-Built backup {Path}", path);
        return path;
    }

    /// <summary>
    /// Voltage check → read the current block (length must match) → confirmations (with what changes) → backup of what was read
    /// → extended session → (optional 0x27) → 0x2E → read-back verify.
    /// </summary>
    public async Task<AsBuiltWriteResult> WriteBlockAsync(string vin, AsBuiltBlock newBlock, CancellationToken ct = default)
    {
        _safety.Gate.Require("As-Built write");
        await _safety.EnsureVoltageAsync(ct);
        var where = $"{newBlock.Module:X3}-{newBlock.Block:X2}";
        var current = await ReadBlockAsync(newBlock.Module, newBlock.Block, ct);
        CheckLength(where, current.Data, newBlock.Data);
        int changed = AsBuiltWritePlan.CountChanges(current.Data, newBlock.Data);
        if (changed == 0) return new AsBuiltWriteResult(null, 0, false, "No bytes differ from the module; nothing was written.");
        await _safety.RequireAsync(SafetyGuard.WriteSteps($"As-Built {where} ({changed} of {newBlock.Data.Length} bytes change)"), ct);
        var backup = await SaveBackupAsync(vin, newBlock.Module, new[] { current }, "", ct);
        var note = await WriteVerifyAsync(newBlock, backup, ct);
        return new AsBuiltWriteResult(backup, changed, true, note);
    }

    private static void CheckLength(string where, byte[] current, byte[] wanted)
    {
        if (current.Length != wanted.Length)
            throw new SafetyException($"{where}: the module holds {current.Length} bytes but the new data is {wanted.Length} bytes. A different length is never written. Nothing was written.");
    }

    /// <summary>Delay before the read-back that settles a write the module answered with "response pending" (0x78), timed out on, or lost to a link drop.</summary>
    public TimeSpan PendingSettle { get; set; } = TimeSpan.FromSeconds(2);

    /// <returns>A note for the log (empty for a plain positive response).</returns>
    private async Task<string> WriteVerifyAsync(AsBuiltBlock b, string backupPath, CancellationToken ct)
    {
        var uds = _clientFactory(b.Module);
        var where = $"{b.Module:X3}-{b.Block:X2}";
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended, ct);
        if (SecurityLevel is byte lvl && !await uds.SecurityAccessAsync(lvl, ct))
            throw new SafetyException("Security access required but no seed-key provider produced a key");
        string note = "";
        try { await uds.WriteDataByIdentifierAsync(b.Did, b.Data, ct); }
        catch (Exception ex) when (IsUncertainWriteOutcome(ex))
        {
            // v0.6.1 audit: these do NOT mean the write failed. The ELM interface returns on the first reply, so a module that answers
            // 0x78 and then 6E is reported as 0x78. Read back to find out what is actually stored, and say so.
            Log.Warning(ex, "As-Built {Where}: write outcome uncertain; settling, then reading back", where);
            await Task.Delay(PendingSettle, ct);
            note = $"The module did not give a final answer to the write ({ex.Message}); the read-back below decided the result.";
        }
        byte[] back;
        try { back = await uds.ReadDataByIdentifierAsync(b.Did, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidDataException($"{where}: could not read the block back after writing, so the result is UNKNOWN. {ex.Message} Read the block again before doing anything else. Backup: {backupPath}", ex);
        }
        if (!back.AsSpan().SequenceEqual(b.Data))
            throw new InvalidDataException($"Read-back mismatch on {where}: wrote {Convert.ToHexString(b.Data)}, read {Convert.ToHexString(back)}. Backup: {backupPath}");
        Log.Information("As-Built {Mod:X3}-{Blk:X2} written and verified{Note}", b.Module, b.Block, note.Length > 0 ? " (by read-back after an uncertain reply)" : "");
        return note;
    }

    /// <summary>Errors after which the write may or may not have been stored: response pending, busy, no reply, or a link drop mid-request.</summary>
    public static bool IsUncertainWriteOutcome(Exception ex) => ex switch
    {
        UdsNegativeResponseException n => n.Nrc is 0x78 or 0x21,
        TimeoutException => true,
        Elm.LinkDroppedException => true,
        _ => false,
    };

    /// <summary>
    /// Restores a backup. The backup must belong to the connected vehicle (VIN), every block must have the module's current length,
    /// and a fresh backup of the current contents is saved before anything is written. Blocks that already match are skipped.
    /// </summary>
    public async Task<AsBuiltWriteResult> RestoreAsync(string backupPath, string connectedVin, CancellationToken ct = default)
    {
        _safety.Gate.Require("As-Built restore");
        var bk = AsBuiltBackup.Deserialize(await File.ReadAllTextAsync(backupPath, ct));
        if (!VinMatch.IsKnown(connectedVin))
            throw new SafetyException("The connected vehicle's VIN is not known. Use Read VIN or Identify vehicle first so the backup can be matched to this truck. Nothing was written.");
        if (!VinMatch.Same(bk.Vin, connectedVin))
            throw new SafetyException($"This backup is from VIN …{Tail(bk.Vin)}, but the connected vehicle is …{Tail(connectedVin)}. Restoring another vehicle's As-Built is refused. Nothing was written.");
        await _safety.EnsureVoltageAsync(ct);
        var wanted = bk.Blocks.Select(kv => new AsBuiltBlock { Module = bk.Module, Block = Convert.ToByte(kv.Key, 16), Data = Convert.FromHexString(kv.Value) }).OrderBy(b => b.Block).ToList();
        var current = new List<AsBuiltBlock>();
        foreach (var w in wanted)
        {
            var c = await ReadBlockAsync(w.Module, w.Block, ct);
            CheckLength($"{w.Module:X3}-{w.Block:X2}", c.Data, w.Data);
            current.Add(c);
        }
        var todo = wanted.Where((w, i) => !w.Data.AsSpan().SequenceEqual(current[i].Data)).ToList();
        int changed = wanted.Select((w, i) => AsBuiltWritePlan.CountChanges(current[i].Data, w.Data)).Sum();
        if (todo.Count == 0) return new AsBuiltWriteResult(null, 0, false, "The module already matches this backup; nothing was written.");
        await _safety.RequireAsync(SafetyGuard.WriteSteps($"RESTORE of {bk.Module:X3} from {Path.GetFileName(backupPath)} ({todo.Count} block(s), {changed} byte(s) change)"), ct);
        var pre = await SaveBackupAsync(connectedVin, bk.Module, current, "_pre-restore", ct);
        var notes = new List<string>();
        foreach (var b in todo)
        {
            var n = await WriteVerifyAsync(b, pre, ct);
            if (n.Length > 0) notes.Add(n);
        }
        return new AsBuiltWriteResult(pre, changed, true, string.Join(" ", notes));
    }

    private static string Tail(string? v) => v is { Length: >= 6 } ? v[^6..] : "?";
}

/// <summary>Outcome of a write or restore. <see cref="BackupPath"/> is the backup of what was in the module before (null if nothing was written).</summary>
public sealed record AsBuiltWriteResult(string? BackupPath, int BytesChanged, bool Written, string Note);
