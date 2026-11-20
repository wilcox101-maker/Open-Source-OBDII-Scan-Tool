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
    /// <summary>v0.16.0: verified changes are recorded here (next to the backups).</summary>
    public AsBuiltHistory History { get; }

    public AsBuiltService(Func<uint, UdsClient> clientFactory, SafetyGuard safety, string backupDir)
    { _clientFactory = clientFactory; _safety = safety; _backupDir = backupDir; Directory.CreateDirectory(backupDir); History = new AsBuiltHistory(backupDir); }

    /// <summary>Journals a line; a failure to journal is logged, never thrown (it must not hide the write's own result).</summary>
    private void Journal(Action append)
    {
        try { append(); }
        catch (Exception ex) { Log.Warning(ex, "Could not append to the As-Built change history"); }
    }

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

    /// <summary>
    /// Backup before a flash: reads As-Built blocks 1, 2, 3, ... until the module answers "no such block" (NRC 0x31) and saves
    /// what was read. Returns null when the module has no As-Built block 1.
    /// </summary>
    public async Task<string?> BackupAvailableAsync(string vin, ushort module, int maxBlocks = 20, CancellationToken ct = default)
    {
        var read = new List<AsBuiltBlock>();
        for (int n = 1; n <= maxBlocks; n++)
        {
            try { read.Add(await ReadBlockAsync(module, AsBuiltAddress.ToBcd(n), ct)); }
            catch (AsBuiltNotSupportedException) { break; }
        }
        return read.Count == 0 ? null : await SaveBackupAsync(vin, module, read, "_preflash", ct);
    }

    private async Task<string> SaveBackupAsync(string vin, ushort module, IEnumerable<AsBuiltBlock> blocks, string tag, CancellationToken ct)
    {
        var dict = blocks.ToDictionary(b => b.Block.ToString("X2"), b => Convert.ToHexString(b.Data));
        var bk = new AsBuiltBackup(vin, DateTimeOffset.Now, module, dict);
        var name = $"{SafePaths.Token(vin)}_{module:X3}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{tag}.asbuilt.json";
        var path = Path.Combine(_backupDir, name);
        if (!SafePaths.IsUnder(_backupDir, path)) throw new InvalidOperationException("Backup path escaped the backup folder");
        path = Storage.DurableFile.Unique(path, ".asbuilt.json");
        // v0.23.18: flushed to disk, renamed into place and read back before the module is touched (a power loss mid-write must not
        // leave an empty backup of the data being replaced).
        await Storage.DurableFile.WriteNewAsync(path, bk.Serialize(), ct);
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
        await _safety.RequireAsync(SafetyGuard.WriteSteps($"As-Built {where} ({changed} of {newBlock.Data.Length} bytes change)")
            .Concat(AsBuiltWritePolicy.ExtraSteps(newBlock.Module, $"Writing As-Built {where}"))
            .Concat(AsBuiltEccPolicy.Steps(AsBuiltEccPolicy.Changed(current, newBlock))), ct); // KB-R4
        var backup = await SaveBackupAsync(vin, newBlock.Module, new[] { current }, "", ct);
        var note = await WriteVerifyAsync(newBlock, current, backup, "write", vin, "", ct);
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
    private async Task<string> WriteVerifyAsync(AsBuiltBlock b, AsBuiltBlock before, string backupPath, string kind, string vin, string context, CancellationToken ct)
    {
        var uds = _clientFactory(b.Module);
        var where = $"{b.Module:X3}-{b.Block:X2}";
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended, ct);
        // X2 (write recovery plan): back to the default session when done, whatever the outcome, so a lingering extended session
        // (kept alive by the session host) can't hold the module unlocked or block other tools. Best-effort: never hides the result.
        try { return await WriteVerifyInSessionAsync(uds, b, before, backupPath, kind, vin, context, where, ct); }
        finally { await LeaveSessionAsync(uds, where); }
    }

    /// <summary>10 01 after a write. A failure here is only logged: the module drops to default by itself at the S3 timeout.</summary>
    internal static async Task LeaveSessionAsync(UdsClient uds, string where)
    {
        try { await uds.DiagnosticSessionControlAsync(UdsSession.Default, CancellationToken.None); }
        catch (Exception ex) { Log.Warning(ex, "As-Built {Where}: could not return to the default session (10 01); the module leaves it at its S3 timeout", where); }
    }

    private async Task<string> WriteVerifyInSessionAsync(UdsClient uds, AsBuiltBlock b, AsBuiltBlock before, string backupPath, string kind, string vin, string context, string where, CancellationToken ct)
    {
        if (SecurityLevel is byte lvl && !await uds.SecurityAccessAsync(lvl, ct))
            throw new SafetyException($"{where}: security access level 0x{lvl:X2} is set, but no approved seed/key plug-in produced a key for module 0x{b.Module:X3}. Nothing was written.");
        string note = "";
        // Journal before the request: if the PC crashes or the adapter is pulled from here on, the pending line says which block,
        // what was there before, what was being written and which backup holds the old data.
        var attempt = new AsBuiltChange(DateTimeOffset.Now, kind, b.Module, b.Block, Convert.ToHexString(before.Data), Convert.ToHexString(b.Data),
            AsBuiltWritePlan.CountChanges(before.Data, b.Data), Path.GetFileName(backupPath), AsBuiltHistory.VinTail(vin), context,
            AsBuiltOutcome.Pending, Guid.NewGuid().ToString("N"));
        Journal(() => History.Append(attempt));
        try { await uds.WriteDataByIdentifierAsync(b.Did, b.Data, ct); }
        catch (UdsNegativeResponseException ex) when (ex.Nrc == 0x33)
        {
            Journal(() => History.Resolve(attempt, AsBuiltOutcome.Rejected, "NRC 0x33 security access denied"));
            throw new SafetyException($"{where}: the module requires security access (NRC 0x33) for this write. "
                + (SecurityLevel is null
                    ? "Settings → Security access: enable an approved seed/key plug-in and set the As-Built security level, then try again."
                    : $"Unlock at level 0x{SecurityLevel:X2} succeeded but was not enough; this module may need another level.")
                + " Nothing was written.");
        }
        catch (UdsNegativeResponseException ex) when (!IsUncertainWriteOutcome(ex))
        {
            // A definite refusal (e.g. 0x22 conditions not correct, 0x31 out of range): the module stored nothing.
            Journal(() => History.Resolve(attempt, AsBuiltOutcome.Rejected, $"NRC 0x{ex.Nrc:X2} {UdsNrc.Describe(ex.Nrc)}"));
            throw;
        }
        catch (Exception ex) when (IsUncertainWriteOutcome(ex))
        {
            // v0.6.1 audit: these do NOT mean the write failed. The ELM interface returns on the first reply, so a module that answers
            // 0x78 and then 6E is reported as 0x78. Read back to find out what is actually stored, and say so.
            Log.Warning(ex, "As-Built {Where}: write outcome uncertain; settling, then reading back", where);
            await Task.Delay(PendingSettle, ct);
            note = $"The module did not give a final answer to the write ({ex.Message}); the read-back below decided the result.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Anything else (adapter gone, port closed): the request may have reached the module.
            Journal(() => History.Resolve(attempt, AsBuiltOutcome.Unknown, "no answer to the write: " + ex.Message));
            throw new InvalidDataException($"{where}: the write was sent but the link failed ({ex.Message}), so the result is UNKNOWN. Reconnect, then use Check unfinished write or restore the backup: {backupPath}", ex);
        }
        byte[] back;
        try { back = await uds.ReadDataByIdentifierAsync(b.Did, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Journal(() => History.Resolve(attempt, AsBuiltOutcome.Unknown, "read-back failed: " + ex.Message));
            throw new InvalidDataException($"{where}: could not read the block back after writing, so the result is UNKNOWN. {ex.Message} Read the block again before doing anything else. Backup: {backupPath}", ex);
        }
        if (!back.AsSpan().SequenceEqual(b.Data))
        {
            var what = back.AsSpan().SequenceEqual(before.Data) ? AsBuiltOutcome.NotWritten : AsBuiltOutcome.Mismatch;
            Journal(() => History.Resolve(attempt, what, $"read back {Convert.ToHexString(back)}"));
            throw new InvalidDataException($"Read-back mismatch on {where}: wrote {Convert.ToHexString(b.Data)}, read {Convert.ToHexString(back)}"
                + (what == AsBuiltOutcome.NotWritten ? " (the old data: the write did not take effect)." : ". Restore the backup or check the block.") + $" Backup: {backupPath}");
        }
        Journal(() => History.Resolve(attempt, AsBuiltOutcome.Verified, note));
        Log.Information("As-Built {Mod:X3}-{Blk:X2} written and verified{Note}", b.Module, b.Block, note.Length > 0 ? " (by read-back after an uncertain reply)" : "");
        return note;
    }

    /// <summary>
    /// Settles an unfinished write by reading the block (read-only): new data → <see cref="AsBuiltOutcome.Verified"/>, old data →
    /// <see cref="AsBuiltOutcome.NotWritten"/>, anything else → <see cref="AsBuiltOutcome.Mismatch"/> (restore the backup).
    /// The outcome is journaled and returned with what the block holds now.
    /// </summary>
    public async Task<(string Outcome, byte[] Now)> CheckAttemptAsync(AsBuiltChange attempt, CancellationToken ct = default)
    {
        var now = (await ReadBlockAsync(attempt.Module, attempt.Block, ct)).Data;
        var hex = Convert.ToHexString(now);
        var outcome = hex == attempt.After ? AsBuiltOutcome.Verified : hex == attempt.Before ? AsBuiltOutcome.NotWritten : AsBuiltOutcome.Mismatch;
        Journal(() => History.Resolve(attempt, outcome, $"checked later: block holds {hex}"));
        return (outcome, now);
    }

    /// <summary>
    /// Recovers an attempt by restoring the backup taken before it (the normal restore: VIN match, fresh backup, confirmations,
    /// read-back). The attempt is then marked <see cref="AsBuiltOutcome.Recovered"/>, or <see cref="AsBuiltOutcome.NotWritten"/> when
    /// the module already held the backup's data.
    /// </summary>
    public async Task<AsBuiltWriteResult> RecoverAttemptAsync(AsBuiltChange attempt, string connectedVin, CancellationToken ct = default)
    {
        var path = History.BackupPathOf(attempt);
        if (!File.Exists(path)) throw new SafetyException($"The backup taken before this write ({attempt.BackupFile}) is not in the backups folder. Nothing was written.");
        var r = await RestoreAsync(path, connectedVin, ct);
        Journal(() => History.Resolve(attempt, r.Written ? AsBuiltOutcome.Recovered : AsBuiltOutcome.NotWritten,
            r.Written ? $"restored {attempt.BackupFile}" : "the module already held the backup's data"));
        return r;
    }

    /// <summary>Errors after which the write may or may not have been stored: response pending, busy, no reply, or a link drop mid-request.</summary>
    public static bool IsUncertainWriteOutcome(Exception ex) => ex switch
    {
        UdsNegativeResponseException n => n.Nrc is 0x78 or 0x21,
        TimeoutException => true,
        Elm.LinkDroppedException => true,
        UdsStrayResponseException => true, // v0.23.20: a late answer to something else; our write may or may not have been stored
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
        await _safety.RequireAsync(SafetyGuard.WriteSteps($"RESTORE of {bk.Module:X3} from {Path.GetFileName(backupPath)} ({todo.Count} block(s), {changed} byte(s) change)")
            .Concat(AsBuiltWritePolicy.ExtraSteps(bk.Module, $"Restoring {bk.Module:X3}"))
            .Concat(AsBuiltEccPolicy.Steps(todo.SelectMany(w => AsBuiltEccPolicy.Changed(current[wanted.IndexOf(w)], w)).ToList())), ct); // KB-R4
        var pre = await SaveBackupAsync(connectedVin, bk.Module, current, "_pre-restore", ct);
        var notes = new List<string>();
        int done = 0;
        foreach (var b in todo)
        {
            string n;
            try { n = await WriteVerifyAsync(b, current[wanted.IndexOf(b)], pre, "restore", connectedVin, $"from {Path.GetFileName(backupPath)}", ct); }
            catch (Exception ex) when (done > 0 && ex is not OperationCanceledException)
            {
                throw new PartialRestoreException(done, todo.Count, b, pre, ex);
            }
            done++;
            if (n.Length > 0) notes.Add(n);
        }
        return new AsBuiltWriteResult(pre, changed, true, string.Join(" ", notes));
    }

    private static string Tail(string? v) => v is { Length: >= 6 } ? v[^6..] : "?";
}

/// <summary>A multi-block restore stopped after some blocks were written and verified. The module now holds a mix.</summary>
public sealed class PartialRestoreException(int done, int total, AsBuiltBlock failed, string preRestoreBackup, Exception inner)
    : Exception($"Restore stopped after {done} of {total} block(s): block {failed.Block:X2} failed ({inner.Message}). Blocks before it hold the backup's data; "
        + $"block {failed.Block:X2} and later still need restoring. Run the restore again, or restore {Path.GetFileName(preRestoreBackup)} to go back to how the module was before.", inner)
{
    public int Done { get; } = done;
    public int Total { get; } = total;
    public string PreRestoreBackup { get; } = preRestoreBackup;
}

/// <summary>Outcome of a write or restore. <see cref="BackupPath"/> is the backup of what was in the module before (null if nothing was written).</summary>
public sealed record AsBuiltWriteResult(string? BackupPath, int BytesChanged, bool Written, string Note);
