namespace ObdLink.Core.Security;

/// <summary>Keeps app-written files inside a chosen folder. VIN and module text are not path-safe.</summary>
public static class SafePaths
{
    /// <summary>Letters, digits, underscore and hyphen only. Empty or all-stripped input becomes <paramref name="fallback"/>.</summary>
    public static string Token(string? value, string fallback = "NOVIN")
    {
        if (string.IsNullOrEmpty(value)) return fallback;
        var s = new string(value.Where(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-').ToArray());
        if (s.Length == 0) return fallback;
        return s.Length <= 40 ? s : s[^40..];
    }

    public static bool IsUnder(string root, string candidate)
    {
        var baseDir = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(candidate).StartsWith(baseDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A single file name with no directory, or null when <paramref name="name"/> would leave <paramref name="dir"/>.</summary>
    public static string? ConfineFile(string dir, string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return null;
        if (name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        var full = Path.GetFullPath(Path.Combine(dir, name));
        return IsUnder(dir, full) ? full : null;
    }
}
