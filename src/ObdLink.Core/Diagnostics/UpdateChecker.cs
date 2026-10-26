using System.Net.Http;
using System.Text.Json;

namespace ObdLink.Core.Diagnostics;

public sealed record UpdateInfo(string Tag, string Name, string Url, bool Prerelease, bool Newer);

/// <summary>Opt-in update check against the public GitHub releases API (no token, no telemetry; only a GET to api.github.com).</summary>
public sealed class UpdateChecker
{
    public const string Repo = "wilcox101-maker/Open-Source-OBDII-Scan-Tool";
    public const string ReleasesUrl = "https://github.com/" + Repo + "/releases";
    public static string ApiUrl => $"https://api.github.com/repos/{Repo}/releases?per_page=10";

    /// <summary>Release pages are shown to the user. Only https://github.com is kept; anything else becomes <see cref="ReleasesUrl"/>.</summary>
    public static string SafeReleaseUrl(string? url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.IsDefaultPort && string.IsNullOrEmpty(u.UserInfo)
            && u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return u.AbsoluteUri;
        return ReleasesUrl;
    }
    private readonly HttpClient _http;
    public UpdateChecker(HttpMessageHandler? handler = null, TimeSpan? timeout = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = timeout ?? TimeSpan.FromSeconds(10);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("OBDLinkDiag-update-check");
    }

    /// <summary>"v0.2.0-test" -> 0.2.0.</summary>
    public static Version? ParseTag(string tag)
    {
        var t = tag.TrimStart('v', 'V'); var cut = t.IndexOfAny(new[] { '-', '+', ' ' }); if (cut > 0) t = t[..cut];
        return Version.TryParse(t, out var v) ? v : null;
    }

    /// <summary>Newest release (including pre-releases, since all builds are test builds). Null on any failure.</summary>
    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken ct = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(ApiUrl, ct));
            UpdateInfo? best = null; Version? bestV = null;
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.TryGetProperty("draft", out var dr) && dr.GetBoolean()) continue;
                var tag = r.GetProperty("tag_name").GetString() ?? ""; var v = ParseTag(tag); if (v is null) continue;
                if (bestV is null || v > bestV) { bestV = v; best = new(tag, r.TryGetProperty("name", out var n) ? n.GetString() ?? tag : tag, SafeReleaseUrl(r.GetProperty("html_url").GetString()), r.TryGetProperty("prerelease", out var p) && p.GetBoolean(), false); }
            }
            return best is null ? null : best with { Newer = bestV! > new Version(current.Major, current.Minor, Math.Max(0, current.Build)) };
        }
        catch (Exception ex) { Serilog.Log.Warning("Update check failed: {Err}", ex.Message); return null; }
    }
}
