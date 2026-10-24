using System.Security.Cryptography;
using System.Text;

namespace ObdLink.Core.Storage;

/// <summary>
/// Single source of truth for where the app keeps its files. Portable by default: &lt;exe dir&gt;\Data\{logs,captures,backups}\, settings.json.
/// Uses AppContext.BaseDirectory (correct for single-file publish, unlike Assembly.Location). Falls back to %LOCALAPPDATA%\OBDLinkDiag
/// when the exe folder is not writable (e.g. Program Files).
/// </summary>
public sealed class AppPaths
{
    public const string DataFolderName = "Data", MigratedNote = "MIGRATED.txt", FallbackNoticeFlag = ".fallback-notice-shown";

    public string DataDir { get; }
    public bool IsPortable { get; }
    /// <summary>Why the portable folder was not used (null when portable).</summary>
    public string? FallbackReason { get; }
    public string LogsDir => Path.Combine(DataDir, "logs");
    public string CapturesDir => Path.Combine(DataDir, "captures");
    public string BackupsDir => Path.Combine(DataDir, "backups");
    public string ExportsDir => Path.Combine(DataDir, "exports");
    public string AsBuiltDir => Path.Combine(DataDir, "asbuilt");
    public string VehiclesDir => Path.Combine(DataDir, "vehicles");
    public string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static readonly string[] SubFolders = { "logs", "captures", "backups", "exports", "asbuilt", "vehicles" };
    /// <summary>Returns <paramref name="dir"/> after making sure it exists (for Save/Open dialog InitialDirectory).</summary>
    public static string Ensure(string dir) { try { Directory.CreateDirectory(dir); } catch (Exception ex) { Serilog.Log.Warning("Could not create folder {Dir}: {Err}", dir, ex.Message); } return dir; }

    private AppPaths(string dataDir, bool portable, string? reason) { DataDir = dataDir; IsPortable = portable; FallbackReason = reason; }

    public static string DefaultLegacyDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OBDLinkDiag");

    private static AppPaths? _current;
    /// <summary>Process-wide paths (resolved once from AppContext.BaseDirectory).</summary>
    public static AppPaths Current => _current ??= Resolve(AppContext.BaseDirectory, DefaultLegacyDir);

    public static AppPaths Resolve(string exeDir, string legacyDir)
    {
        var portable = Path.Combine(exeDir, DataFolderName);
        string? reason;
        try
        {
            Directory.CreateDirectory(portable);
            var probe = Path.Combine(portable, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok"); File.Delete(probe);
            foreach (var d in SubFolders) Directory.CreateDirectory(Path.Combine(portable, d));
            return new AppPaths(portable, true, null);
        }
        catch (Exception ex) { reason = $"{exeDir} is not writable ({ex.GetType().Name}: {ex.Message})"; }
        Directory.CreateDirectory(legacyDir);
        foreach (var d in SubFolders) try { Directory.CreateDirectory(Path.Combine(legacyDir, d)); } catch { /* probe only: failure means the folder is not writable, checked next */ }
        return new AppPaths(legacyDir, false, reason);
    }

    /// <summary>True once per data folder: whether to show the "not portable, using LocalAppData" notice.</summary>
    public bool ShouldShowFallbackNotice()
    {
        if (IsPortable) return false;
        var flag = Path.Combine(DataDir, FallbackNoticeFlag);
        if (File.Exists(flag)) return false;
        try { File.WriteAllText(flag, DateTime.Now.ToString("O")); } catch { /* marker file is optional */ }
        return true;
    }

    public sealed record MigrationResult(bool Ran, int Moved, int SkippedNewer, int KeptUnverified, IReadOnlyList<string> Notes);

    /// <summary>
    /// One-time migration of an old %LOCALAPPDATA%\OBDLinkDiag folder into <see cref="DataDir"/>. Each file is copied unless the
    /// destination already has a newer (or same-age) copy; the source is deleted only after the copy is verified (size + SHA-256).
    /// Unverified or skipped files stay in place. A MIGRATED.txt note is left in the old folder; its presence disables re-running.
    /// </summary>
    public MigrationResult MigrateFrom(string legacyDir)
    {
        var notes = new List<string>();
        if (!IsPortable || !Directory.Exists(legacyDir) || File.Exists(Path.Combine(legacyDir, MigratedNote))
            || string.Equals(Path.GetFullPath(legacyDir).TrimEnd('\\', '/'), Path.GetFullPath(DataDir).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            return new(false, 0, 0, 0, notes);
        int moved = 0, skipped = 0, kept = 0;
        foreach (var src in Directory.GetFiles(legacyDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(legacyDir, src);
            if (rel == MigratedNote || Path.GetFileName(rel) == FallbackNoticeFlag) continue;
            var dst = Path.Combine(DataDir, rel);
            try
            {
                if (File.Exists(dst) && File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src))
                { skipped++; notes.Add($"kept old copy (destination is newer): {rel}"); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                File.Copy(src, dst, overwrite: true);
                File.SetLastWriteTimeUtc(dst, File.GetLastWriteTimeUtc(src));
                if (SameContent(src, dst)) { File.Delete(src); moved++; }
                else { kept++; notes.Add($"copy NOT verified, original kept: {rel}"); }
            }
            catch (Exception ex) { kept++; notes.Add($"could not move {rel}: {ex.Message}"); }
        }
        foreach (var d in Directory.GetDirectories(legacyDir, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
            try { if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } catch { /* tidy-up of empty legacy folders only */ }
        var sb = new StringBuilder()
            .AppendLine($"OBDLinkDiag data moved on {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz} to:")
            .AppendLine(DataDir).AppendLine()
            .AppendLine($"Moved (copy verified, original removed): {moved}")
            .AppendLine($"Left here because the new folder already had a newer copy: {skipped}")
            .AppendLine($"Left here because the copy could not be verified: {kept}");
        foreach (var n in notes) sb.AppendLine(" - " + n);
        sb.AppendLine().AppendLine("This note stops the app from migrating again. Anything still in this folder is safe to keep or delete by hand.");
        try { File.WriteAllText(Path.Combine(legacyDir, MigratedNote), sb.ToString()); } catch (Exception ex) { notes.Add("could not write MIGRATED.txt: " + ex.Message); }
        return new(true, moved, skipped, kept, notes);
    }

    private static bool SameContent(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        using var fa = File.OpenRead(a); using var fb = File.OpenRead(b);
        return SHA256.HashData(fa).AsSpan().SequenceEqual(SHA256.HashData(fb));
    }
}
