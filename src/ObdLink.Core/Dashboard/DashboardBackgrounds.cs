namespace ObdLink.Core.Dashboard;

/// <summary>Built-in dashboard backdrops plus a custom image stored under Data\backgrounds (never an absolute path).</summary>
public static class DashboardBackgrounds
{
    public const string None = "None";
    public const string Wood = "Wood";
    public const string Steel = "Steel";
    public const string Mirror = "Mirror";
    public const string Onyx = "Onyx";
    public const string Leather = "Leather";
    public const string Carbon = "Carbon";
    public const string Brushed = "Brushed";
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<DashboardBackgroundChoice> Choices = new DashboardBackgroundChoice[]
    {
        new(None, "None (app theme)"),
        new(Wood, "Wood grain"),
        new(Steel, "Steel plate"),
        new(Mirror, "Mirror"),
        new(Onyx, "Onyx black"),
        new(Leather, "Sports-car leather"),
        new(Carbon, "Carbon fiber"),
        new(Brushed, "Brushed aluminum"),
        new(Custom, "Custom image"),
    };

    public static string Normalize(string? id) =>
        Choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) is { } hit ? hit.Id : None;
}

public sealed record DashboardBackgroundChoice(string Id, string Name);

/// <summary>Copies a user-picked image into Data\backgrounds and refuses anything that is not a file name under that folder.</summary>
public static class DashboardBackgroundFiles
{
    public const string Folder = "backgrounds";
    public const long MaxBytes = 15 * 1024 * 1024;
    public static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    public static string? SanitizeFileName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (name.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0) return null;
        var file = Path.GetFileName(name);
        if (!string.Equals(file, name, StringComparison.Ordinal)) return null;
        if (!Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) return null;
        if (file.Length > 80) return null;
        return file;
    }

    /// <summary>Full path of a stored image, or null when the name is unsafe or the file is missing.</summary>
    public static string? FullPath(string dataDir, string? fileName)
    {
        var safe = SanitizeFileName(fileName);
        if (safe is null) return null;
        var root = Path.GetFullPath(Path.Combine(dataDir, Folder));
        var full = Path.GetFullPath(Path.Combine(root, safe));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        return File.Exists(full) ? full : null;
    }

    /// <summary>Copies <paramref name="sourcePath"/> to Data\backgrounds\custom.ext. Returns the file name, or null if rejected.</summary>
    public static string? Import(string dataDir, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return null;
        var ext = Path.GetExtension(sourcePath);
        if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) return null;
        var len = new FileInfo(sourcePath).Length;
        if (len <= 0 || len > MaxBytes) return null;
        var dir = Path.Combine(dataDir, Folder);
        Directory.CreateDirectory(dir);
        var name = "custom" + ext.ToLowerInvariant();
        var dest = Path.Combine(dir, name);
        if (!SamePath(sourcePath, dest))
        {
            File.Copy(sourcePath, dest, overwrite: true);
            foreach (var old in Directory.GetFiles(dir, "custom.*"))
                if (!SamePath(old, dest)) try { File.Delete(old); } catch (Exception ex) { Serilog.Log.Warning("Could not remove previous dashboard image {File}: {Err}", old, ex.Message); }
        }
        return name;
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
}
