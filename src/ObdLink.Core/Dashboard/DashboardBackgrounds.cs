namespace ObdLink.Core.Dashboard;

/// <summary>Built-in dashboard backdrops plus a custom image stored under Data\backgrounds (never an absolute path).</summary>
public static class DashboardBackgrounds
{
    public const string None = "None";
    /// <summary>Saved-settings id. The picture is the digital dash (the old wood-grain texture is gone).</summary>
    public const string Wood = "Wood";
    /// <summary>Saved-settings id. The picture is the digital cluster close-up.</summary>
    public const string Steel = "Steel";
    /// <summary>Saved-settings id. The picture is the highway cabin.</summary>
    public const string Mirror = "Mirror";
    /// <summary>Saved-settings id. The picture is the night cabin.</summary>
    public const string Onyx = "Onyx";
    public const string Leather = "Leather";
    /// <summary>Saved-settings id. The picture is the dusk cabin.</summary>
    public const string Carbon = "Carbon";
    /// <summary>Saved-settings id. The picture is the center stack.</summary>
    public const string Brushed = "Brushed";
    /// <summary>Physical analog instrument cluster (late-model truck). New in v0.5.2.</summary>
    public const string Analog = "Analog";
    public const string Custom = "Custom";

    public static readonly IReadOnlyList<DashboardBackgroundChoice> Choices = new DashboardBackgroundChoice[]
    {
        new(None, "None (app theme)"),
        new(Analog, "Analog gauges"),
        new(Wood, "Digital dash"),
        new(Steel, "Digital cluster"),
        new(Onyx, "Night cabin"),
        new(Mirror, "Highway view"),
        new(Carbon, "Dusk cabin"),
        new(Leather, "Black leather cabin"),
        new(Brushed, "Center stack"),
        new(Custom, "Custom image"),
    };

    public static string Normalize(string? id) =>
        Choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) is { } hit ? hit.Id : None;
}

public sealed record DashboardBackgroundChoice(string Id, string Name);

/// <summary>
/// Built-in cabin photos cover the dashboard once. v0.5.1 and earlier drew small
/// <c>DrawingBrush</c> tiles (<c>TileMode.Tile</c> plus a tiny absolute viewport), which is why
/// those backdrops looked repeated and low resolution.
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
        new(DashboardBackgrounds.Analog, "analog.jpg"),
        new(DashboardBackgrounds.Wood, "digital-dash.jpg"),
        new(DashboardBackgrounds.Steel, "digital-cluster.jpg"),
        new(DashboardBackgrounds.Mirror, "highway.jpg"),
        new(DashboardBackgrounds.Onyx, "night.jpg"),
        new(DashboardBackgrounds.Leather, "leather.jpg"),
        new(DashboardBackgrounds.Carbon, "dusk.jpg"),
        new(DashboardBackgrounds.Brushed, "center-stack.jpg"),
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
