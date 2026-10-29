namespace ObdLink.Core.Dashboard;

/// <summary>Built-in dashboard backdrops plus a custom image stored under Data\backgrounds (never an absolute path).</summary>
public static class DashboardBackgrounds
{
    public const string None = "None";
    public const string WoodGrain = "WoodGrain";
    public const string BrushedAluminum = "BrushedAluminum";
    public const string GlossBlack = "GlossBlack";
    public const string DiamondPlate = "DiamondPlate";
    public const string CarbonFiber = "CarbonFiber";
    public const string SportsLeather = "SportsLeather";
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<DashboardBackgroundChoice> Choices = new DashboardBackgroundChoice[]
    {
        new(None, "None (app theme)"),
        new(WoodGrain, "Fine wood grain"),
        new(BrushedAluminum, "Brushed aluminum"),
        new(GlossBlack, "High-gloss black"),
        new(DiamondPlate, "Diamond-plate metal"),
        new(CarbonFiber, "Carbon fiber"),
        new(SportsLeather, "Sports-car leather"),
        new(Custom, "Custom image"),
    };

    public static string Normalize(string? id) =>
        Choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) is { } hit ? hit.Id : None;
}

public sealed record DashboardBackgroundChoice(string Id, string Name);

/// <summary>
/// Built-in material photos cover the dashboard once. v0.5.1 and earlier drew small
/// <c>DrawingBrush</c> tiles (<c>TileMode.Tile</c> plus a tiny absolute viewport), which is why
/// those backdrops looked repeated and low resolution. v0.5.2 cabin ids normalize to None.
/// </summary>
public static class DashboardBackdropPolicy
{
    public const bool TileBuiltIns = false;
    public const string CoverStretch = "UniformToFill";
    /// <summary>Black overlay on photos so gauge cards stay readable in light and dark themes. 0-255.</summary>
    public const byte ScrimAlpha = 0x48;

    public sealed record BackdropAsset(string Id, string FileName);

    public static readonly IReadOnlyList<BackdropAsset> Assets = new BackdropAsset[]
    {
        new(DashboardBackgrounds.WoodGrain, "wood-grain.jpg"),
        new(DashboardBackgrounds.BrushedAluminum, "brushed-aluminum.jpg"),
        new(DashboardBackgrounds.GlossBlack, "gloss-black.jpg"),
        new(DashboardBackgrounds.DiamondPlate, "diamond-plate.jpg"),
        new(DashboardBackgrounds.CarbonFiber, "carbon-fiber.jpg"),
        new(DashboardBackgrounds.SportsLeather, "sports-leather.jpg"),
    };

    public static string? FileFor(string? id)
    {
        var norm = DashboardBackgrounds.Normalize(id);
        foreach (var asset in Assets)
            if (string.Equals(asset.Id, norm, StringComparison.Ordinal)) return asset.FileName;
        return null;
    }
}

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
