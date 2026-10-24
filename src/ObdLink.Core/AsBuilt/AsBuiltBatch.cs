using ObdLink.Core.Modules;

namespace ObdLink.Core.AsBuilt;

/// <summary>Outcome for one module in a batch run (As-Built backup or DTC read).</summary>
public sealed record BatchItemResult(ModuleDefinition Module, bool Ok, string Detail, string? Path = null);

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
        $"{(string.IsNullOrWhiteSpace(vinMasked) ? "NOVIN" : vinMasked)}_{module:X3}_{now:yyyyMMdd_HHmmss}.asbuilt.json";

    public static async Task<string> SaveAsync(string dir, string vinMasked, ushort module, IReadOnlyList<AsBuiltBlock> blocks, DateTime now, CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var bk = new AsBuiltBackup(vinMasked, new DateTimeOffset(now), module, blocks.ToDictionary(b => b.Block.ToString("X2"), b => Convert.ToHexString(b.Data)));
        var path = Path.Combine(dir, FileName(vinMasked, module, now));
        await File.WriteAllTextAsync(path, bk.Serialize(), ct);
        return path;
    }

    /// <summary>Newest backup per module address found under the given folders (recursive), from file names "*_XXX_yyyyMMdd_HHmmss.asbuilt.json".</summary>
    public static Dictionary<uint, (DateTime When, string Path)> FindBackups(params string[] dirs)
    {
        var res = new Dictionary<uint, (DateTime, string)>();
        foreach (var dir in dirs.Where(Directory.Exists))
            foreach (var f in Directory.EnumerateFiles(dir, "*.asbuilt.json", SearchOption.AllDirectories))
            {
                var parts = Path.GetFileName(f)[..^".asbuilt.json".Length].Split('_');
                if (parts.Length < 4) continue;
                if (!uint.TryParse(parts[^3], System.Globalization.NumberStyles.HexNumber, null, out var m)) continue;
                if (!DateTime.TryParseExact(parts[^2] + parts[^1], "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var when)) continue;
                if (!res.TryGetValue(m, out var cur) || when > cur.Item1) res[m] = (when, f);
            }
        return res;
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
            catch (Exception ex) { res.Add(new(m, false, ex.Message)); }
        }
        progress?.Report(new(modules.Count, modules.Count, $"Done: {res.Count(r => r.Ok)} of {modules.Count} OK"));
        return res;
    }
}
