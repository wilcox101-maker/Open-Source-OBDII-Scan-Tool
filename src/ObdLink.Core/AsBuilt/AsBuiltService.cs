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
        var dict = new Dictionary<string, string>();
        foreach (var b in blocks) dict[b.ToString("X2")] = Convert.ToHexString((await ReadBlockAsync(module, b, ct)).Data);
        var bk = new AsBuiltBackup(vin, DateTimeOffset.Now, module, dict);
        var name = $"{SafePaths.Token(vin)}_{module:X3}_{DateTime.Now:yyyyMMdd_HHmmss}.asbuilt.json";
        var path = Path.Combine(_backupDir, name);
        if (!SafePaths.IsUnder(_backupDir, path)) throw new InvalidOperationException("Backup path escaped the backup folder");
        await File.WriteAllTextAsync(path, bk.Serialize(), ct);
        Log.Information("As-Built backup {Path}", path);
        return path;
    }

    /// <summary>Voltage check → confirmations → backup → extended session → (optional 0x27) → 0x2E → read-back verify.</summary>
    public async Task<string> WriteBlockAsync(string vin, AsBuiltBlock newBlock, CancellationToken ct = default)
    {
        await _safety.EnsureVoltageAsync(ct);
        await _safety.RequireAsync(SafetyGuard.WriteSteps($"As-Built {newBlock.Module:X3}-{newBlock.Block:X2}"), ct);
        var backup = await BackupAsync(vin, newBlock.Module, new[] { newBlock.Block }, ct);
        await WriteVerifyAsync(newBlock, ct);
        return backup;
    }

    private async Task WriteVerifyAsync(AsBuiltBlock b, CancellationToken ct)
    {
        var uds = _clientFactory(b.Module);
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended, ct);
        if (SecurityLevel is byte lvl && !await uds.SecurityAccessAsync(lvl, ct))
            throw new SafetyException("Security access required but no seed-key provider produced a key");
        await uds.WriteDataByIdentifierAsync(b.Did, b.Data, ct);
        var back = await uds.ReadDataByIdentifierAsync(b.Did, ct);
        if (!back.AsSpan().SequenceEqual(b.Data))
            throw new InvalidDataException($"Read-back mismatch on {b.Module:X3}-{b.Block:X2}: wrote {Convert.ToHexString(b.Data)}, read {Convert.ToHexString(back)}");
        Log.Information("As-Built {Mod:X3}-{Blk:X2} written and verified", b.Module, b.Block);
    }

    public async Task RestoreAsync(string backupPath, CancellationToken ct = default)
    {
        var bk = AsBuiltBackup.Deserialize(await File.ReadAllTextAsync(backupPath, ct));
        await _safety.EnsureVoltageAsync(ct);
        await _safety.RequireAsync(SafetyGuard.WriteSteps($"RESTORE of {bk.Module:X3} from {Path.GetFileName(backupPath)}"), ct);
        foreach (var (blk, hex) in bk.Blocks)
            await WriteVerifyAsync(new AsBuiltBlock { Module = bk.Module, Block = Convert.ToByte(blk, 16), Data = Convert.FromHexString(hex) }, ct);
    }
}
