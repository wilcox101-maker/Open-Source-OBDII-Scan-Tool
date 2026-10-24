namespace ObdLink.Core.Security;

/// <summary>
/// v0.3.4 audit: allow-list for anything handed to the shell (browser/Explorer). Web links must be https and
/// point at a known host; local targets must be explorer.exe or an absolute local path. Everything else is refused.
/// </summary>
public static class ExternalTargets
{
    public static readonly IReadOnlyCollection<string> AllowedHosts = new[]
    {
        "github.com", "www.motorcraftservice.com", "www.scantool.net", "scantool.net", "www.obdlink.com", "obdlink.com", "docs.google.com",
    };

    public static bool IsAllowed(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        if (string.Equals(target, "explorer.exe", StringComparison.OrdinalIgnoreCase)) return true;
        if (Uri.TryCreate(target, UriKind.Absolute, out var u))
        {
            if (u.IsFile) return !u.IsUnc && (IsDrivePath(target) || Path.IsPathFullyQualified(target));
            return u.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(u.UserInfo) && u.IsDefaultPort
                && AllowedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool IsDrivePath(string t) => t.Length > 2 && char.IsAsciiLetter(t[0]) && t[1] == ':' && (t[2] == '\\' || t[2] == '/');
}
