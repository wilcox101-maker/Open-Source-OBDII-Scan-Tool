using ObdLink.Core.Modules;
using ObdLink.Core.Security;

namespace ObdLink.Core.AsBuilt;

/// <summary>Outcome for one module in a batch run (As-Built backup or DTC read).</summary>
public sealed record BatchItemResult(ModuleDefinition Module, bool Ok, string Detail, string? Path = null, bool NoAsBuilt = false);

/// <summary>Progress report for a batch: modules done so far, total, and what is happening now.</summary>
public readonly record struct BatchProgress(int Done, int Total, string Text)
{
    public double Percent => Total == 0 ? 0 : 100.0 * Done / Total;
}

/// <summary>
/// Read-only helpers for the As-Built page (v0.2.1): read every block of a module, back up every module into
/// Data\asbuilt\&lt;yyyy-MM-dd&gt;\, and look up which modules already have a backup. Nothing here writes to a vehicle.
/// </summary>
public static class AsBuiltBatch
{
    /// <summary>FORScan numbers blocks 1.. (DID DE00..). Real Ford modules use up to about 8; 16 is a safety stop.</summary>
    public const int MaxBlocks = 16;

    /// <summary>
    /// Reads block 1, 2, 3... until a block fails (normally NRC 0x31 requestOutOfRange) or <paramref name="max"/>.
    /// If block 1 fails the exception is rethrown (the module has no As-Built or did not answer).
    /// </summary>
    public static async Task<List<AsBuiltBlock>> ReadAllBlocksAsync(Func<byte, CancellationToken, Task<AsBuiltBlock>> read, int max = MaxBlocks, CancellationToken ct = default)
    {
        var list = new List<AsBuiltBlock>();
        for (int i = 1; i <= max; i++)
        {
            ct.ThrowIfCancellationRequested();
            var blk = AsBuiltAddress.ToBcd(i);
            try { list.Add(await read(blk, ct)); }
            catch (OperationCanceledException) { throw; }
            catch when (list.Count > 0) { break; }
        }
        return list;
    }

    /// <summary>Data\asbuilt\yyyy-MM-dd</summary>
    public static string DateFolder(string asBuiltDir, DateTime now) => Path.Combine(asBuiltDir, now.ToString("yyyy-MM-dd"));

    /// <summary>File name used for a module backup; the VIN is masked unless <paramref name="vin"/> is empty.</summary>
    public static string FileName(string vinMasked, uint module, DateTime now) =>
        $"{SafePaths.Token(vinMasked)}_{module:X3}_{now:yyyyMMdd_HHmmss}.asbuilt.json";

    public static async Task<string> SaveAsync(string dir, string vinMasked, ushort module, IReadOnlyList<AsBuiltBlock> blocks, DateTime now, CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var bk = new AsBuiltBackup(vinMasked, new DateTimeOffset(now), module, blocks.ToDictionary(b => b.Block.ToString("X2"), b => Convert.ToHexString(b.Data)));
        var path = Path.Combine(dir, FileName(vinMasked, module, now));
        if (!SafePaths.IsUnder(dir, path)) throw new InvalidOperationException("As-Built backup path escaped the destination folder");
        path = Storage.DurableFile.Unique(path, ".asbuilt.json"); // v0.23.18: two backups in the same second no longer overwrite each other
        await Storage.DurableFile.WriteNewAsync(path, bk.Serialize(), ct);
        return path;
    }

    /// <summary>
    /// Newest backup per module address found under the given folders (recursive). v0.23.18: recognises every name the app writes:
    /// batch "*_XXX_yyyyMMdd_HHmmss.asbuilt.json", pre-write "*_XXX_yyyyMMdd_HHmmss_fff.asbuilt.json", pre-flash / pre-restore "…_fff_preflash…" / "…_fff_pre-restore…",
    /// and a "_2"/"_3" suffix for same-second files. Before, the pre-write and pre-flash backups were never found.
    /// </summary>
    public static Dictionary<uint, (DateTime When, string Path)> FindBackups(params string[] dirs)
    {
        var res = new Dictionary<uint, (DateTime, string)>();
        foreach (var dir in dirs.Where(Directory.Exists))
            foreach (var f in Directory.EnumerateFiles(dir, "*.asbuilt.json", SearchOption.AllDirectories))
            {
                if (!TryParseBackupName(Path.GetFileName(f), out var m, out var when)) continue;
                if (!res.TryGetValue(m, out var cur) || when > cur.Item1) res[m] = (when, f);
            }
        return res;
    }

    private static readonly System.Text.RegularExpressions.Regex BackupName = new(
        @"_(?<m>[0-9A-Fa-f]{3})_(?<d>\d{8})_(?<t>\d{6})(?:_(?<ms>\d{3}))?(?:_[A-Za-z][A-Za-z-]*)?(?:_\d+)?\.asbuilt\.json$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Module address and time from a backup file name written by this app; false for anything else.</summary>
    public static bool TryParseBackupName(string fileName, out uint module, out DateTime when)
    {
        module = 0; when = default;
        var m = BackupName.Match(fileName);
        if (!m.Success) return false;
        module = Convert.ToUInt32(m.Groups["m"].Value, 16);
        var ms = m.Groups["ms"].Success ? m.Groups["ms"].Value : "000";
        return DateTime.TryParseExact(m.Groups["d"].Value + m.Groups["t"].Value + ms, "yyyyMMddHHmmssfff",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out when);
    }

    /// <summary>
    /// Runs <paramref name="work"/> for each module in order, reporting progress. A failing module is recorded and the batch goes on;
    /// cancellation stops after the current module and is rethrown.
    /// </summary>
    public static async Task<List<BatchItemResult>> RunAsync(IReadOnlyList<ModuleDefinition> modules, Func<ModuleDefinition, CancellationToken, Task<BatchItemResult>> work,
        IProgress<BatchProgress>? progress, CancellationToken ct)
    {
        var res = new List<BatchItemResult>();
        for (int i = 0; i < modules.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var m = modules[i];
            progress?.Report(new(i, modules.Count, $"{m.Acronym} (0x{m.TxId:X3}) {i + 1}/{modules.Count}"));
            try { res.Add(await work(m, ct)); }
            catch (OperationCanceledException) { throw; }
            catch (AsBuiltNotSupportedException ex) { res.Add(new(m, false, ex.Message, NoAsBuilt: true)); }
            catch (Exception ex) { res.Add(new(m, false, ex.Message)); }
        }
        progress?.Report(new(modules.Count, modules.Count, $"Done: {res.Count(r => r.Ok)} of {modules.Count} OK"));
        return res;
    }

    /// <summary>A module with no As-Built is not a failed backup. Timeouts and other errors still are.</summary>
    public static bool HasHardFailure(IReadOnlyList<BatchItemResult> results) => results.Any(r => !r.Ok && !r.NoAsBuilt);

    public static string Line(BatchItemResult r)
    {
        var head = $"{r.Module.Acronym} 0x{r.Module.TxId:X3}: ";
        if (r.NoAsBuilt) return head + "no As-Built - " + r.Detail;
        return head + (r.Ok ? "OK - " : "failed - ") + r.Detail;
    }

    public static string BackupSummary(IReadOnlyList<BatchItemResult> results, string dir)
    {
        var saved = results.Count(r => r.Ok);
        var none = results.Count(r => r.NoAsBuilt);
        var failed = results.Count(r => !r.Ok && !r.NoAsBuilt);
        return $"As-Built backup: {saved} saved, {none} with no As-Built, {failed} failed to {dir}";
    }
}
