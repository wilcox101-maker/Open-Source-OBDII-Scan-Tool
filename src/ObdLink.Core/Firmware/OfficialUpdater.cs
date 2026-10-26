using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ObdLink.Core.Security;

namespace ObdLink.Core.Firmware;

/// <summary>Result of the Authenticode check (done by the Windows app with WinVerifyTrust; Core only holds the policy).</summary>
public sealed record SignatureInfo(bool HasSignature, bool TrustValid, string? Publisher);

/// <summary>What is known about the downloaded official installer; saved as updater.json next to it.</summary>
public sealed record UpdaterManifest(string Url, string FileName, long Size, string Sha256, string? Version, string? LastModified, string? ETag,
    bool Signed, bool SignatureValid, string? Publisher, bool PinnedHash, DateTime DownloadedUtc);

public enum UpdaterTrust { TrustedSignature, KnownHash, Unverified, Rejected }

/// <summary>
/// v0.3.1: OBDLink's official "STN Firmware Update Utility" (an Inno Setup installer from scantool.net).
/// The app downloads it on request into Data\firmware\updater\ and launches it; it never bundles or replaces it,
/// and it never flashes firmware itself.
/// </summary>
public static class OfficialUpdater
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };
    public const string PageUrl = "https://www.scantool.net/downloads/updates/firmware_updater/";
    /// <summary>Direct installer link found on the page on 2026-10-24 (v2.4.2.0, 15,613,628 bytes, UNSIGNED Inno Setup 6.1).</summary>
    public const string KnownInstallerUrl = "https://www.scantool.net/scantool/downloads/740/Install-StnFirmwareUpdater.exe";
    public const string InstallerFileName = "Install-StnFirmwareUpdater.exe";
    public const string AppExeName = "StnFirmwareUpdater.exe";
    public const string ManifestName = "updater.json";
    public const long MaxBytes = 60L * 1024 * 1024;
    public const int MaxRedirects = 5;

    /// <summary>SHA-256 of official installers checked by hand. The current installer has no Authenticode signature, so this pin is what proves it is the official file.</summary>
    public static readonly IReadOnlyDictionary<string, string> KnownGoodSha256 = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["2bc969d3ad639171449d3451e458e00587e591d3da8c9aaee3d2236f32a29fcf"] = "2.4.2.0",
    };

    /// <summary>Publisher names accepted in an Authenticode certificate subject (OBD Solutions LLC = ScanTool.net / OBDLink).</summary>
    public static readonly string[] Publishers = { "OBD Solutions", "ScanTool" };

    /// <summary>https only, host scantool.net or www.scantool.net only (checked on every redirect hop).</summary>
    public static bool IsAllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && u.IsDefaultPort && string.IsNullOrEmpty(u.UserInfo)
        && (u.Host.Equals("www.scantool.net", StringComparison.OrdinalIgnoreCase) || u.Host.Equals("scantool.net", StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowedInstallerUrl(string? url) => IsAllowedUrl(url) && new Uri(url!).AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>Finds the installer link on the updater page (relative links resolved against the page). Null if none is official.</summary>
    public static string? ParseInstallerUrl(string html, string pageUrl = PageUrl)
    {
        foreach (Match m in Regex.Matches(html, "href\\s*=\\s*[\"']([^\"']*Install-?StnFirmwareUpdater[^\"']*\\.exe)[\"']", RegexOptions.IgnoreCase))
            if (Uri.TryCreate(new Uri(pageUrl), WebUtility.HtmlDecode(m.Groups[1].Value), out var u) && IsAllowedInstallerUrl(u.ToString())) return u.ToString();
        return null;
    }

    public static bool PublisherOk(string? subject) => subject is not null && Publishers.Any(p => subject.Contains(p, StringComparison.OrdinalIgnoreCase));

    /// <summary>Trust decision: a valid signature from OBD Solutions/ScanTool, or else a hash we have pinned. A signature from anyone else is rejected.</summary>
    public static UpdaterTrust Evaluate(SignatureInfo sig, string sha256)
    {
        if (sig.HasSignature && sig.TrustValid && PublisherOk(sig.Publisher)) return UpdaterTrust.TrustedSignature;
        if (sig.HasSignature && sig.TrustValid) return UpdaterTrust.Rejected;           // signed, but not by OBD Solutions
        if (KnownGoodSha256.ContainsKey(sha256)) return UpdaterTrust.KnownHash;
        return sig.HasSignature ? UpdaterTrust.Rejected : UpdaterTrust.Unverified;    // broken signature = reject; unsigned + unknown hash = ask the user
    }

    /// <summary>True when the PE has an Authenticode certificate table (security data directory).</summary>
    public static bool HasAuthenticodeBlob(ReadOnlySpan<byte> pe)
    {
        if (pe.Length < 0x40 || pe[0] != 'M' || pe[1] != 'Z') return false;
        int h = BitConverter.ToInt32(pe[0x3C..]); if (h <= 0 || h + 24 + 2 > pe.Length) return false;
        int magic = BitConverter.ToUInt16(pe[(h + 24)..]);
        int dd = h + 24 + (magic == 0x10B ? 96 : 112) + 4 * 8;
        return dd + 8 <= pe.Length && BitConverter.ToInt32(pe[(dd + 4)..]) > 0;
    }

    public static bool LooksLikeInstaller(ReadOnlySpan<byte> pe) =>
        pe.Length > 2 && pe[0] == 'M' && pe[1] == 'Z' && pe.IndexOf(Encoding.ASCII.GetBytes("Inno Setup")) >= 0;

    /// <summary>Reads ProductVersion (or FileVersion) from the PE version resource (UTF-16 strings).</summary>
    public static string? ReadVersion(ReadOnlySpan<byte> pe)
    {
        foreach (var key in new[] { "ProductVersion", "FileVersion" })
        {
            var k = Encoding.Unicode.GetBytes(key + "\0");
            int i = pe.IndexOf(k); if (i < 0) continue;
            i += k.Length; if (i % 4 != 0) i += 2;                       // value is DWORD-aligned
            var sb = new StringBuilder();
            for (; i + 1 < pe.Length && sb.Length < 40; i += 2) { char c = (char)BitConverter.ToUInt16(pe[i..]); if (c == '\0') { if (sb.Length > 0) break; continue; } sb.Append(c); }
            var v = sb.ToString().Trim();
            if (Regex.IsMatch(v, @"^\d+(\.\d+){1,3}$")) return v;
        }
        return null;
    }

    public static string Sha256(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant(); }

    public static UpdaterManifest? LoadManifest(string dir)
    {
        try { var p = Path.Combine(dir, ManifestName); return File.Exists(p) ? JsonSerializer.Deserialize<UpdaterManifest>(File.ReadAllText(p)) : null; } catch (Exception ex) { Serilog.Log.Warning("Updater manifest unreadable, will download again: {Err}", ex.Message); return null; }
    }

    /// <summary>Manifest file names stay inside <paramref name="dir"/>. A name with a directory is refused.</summary>
    public static string? ConfinedFile(string dir, string? name) => SafePaths.ConfineFile(dir, name);

    /// <summary>Existing download still matches its manifest (size + SHA-256) and the file name stays in <paramref name="dir"/>.</summary>
    public static bool ExistingValid(string dir, out UpdaterManifest? m)
    {
        m = LoadManifest(dir);
        if (m is null) return false;
        var f = ConfinedFile(dir, m.FileName);
        return f is not null && File.Exists(f) && new FileInfo(f).Length == m.Size && Sha256(f) == m.Sha256;
    }

    /// <summary>Server copy differs from ours (different URL, size, Last-Modified or ETag). Unknown headers = treat as same.</summary>
    public static bool ServerIsNewer(UpdaterManifest m, string url, long? len, string? lastModified, string? etag) =>
        !string.Equals(m.Url, url, StringComparison.OrdinalIgnoreCase)
        || (len is > 0 && len != m.Size)
        || (etag is not null && m.ETag is not null && etag != m.ETag)
        || (lastModified is not null && m.LastModified is not null && lastModified != m.LastModified && len != m.Size);

    /// <summary>Installed updater: uninstall entries (DisplayName, InstallLocation, DisplayIcon) first, then known Program Files folders.</summary>
    public static string? FindInstalled(IEnumerable<(string? Name, string? Location, string? Icon)> uninstall, IEnumerable<string> programFiles, Func<string, bool> exists)
    {
        foreach (var (name, loc, icon) in uninstall)
        {
            if (name is null || !(name.Contains("StnFirmwareUpdater", StringComparison.OrdinalIgnoreCase) || name.Contains("STN Firmware", StringComparison.OrdinalIgnoreCase))) continue;
            if (!string.IsNullOrWhiteSpace(loc) && exists(Path.Combine(loc.Trim('"'), AppExeName))) return Path.Combine(loc.Trim('"'), AppExeName);
            var ic = icon?.Split(',')[0].Trim('"');
            if (ic is not null && ic.EndsWith(AppExeName, StringComparison.OrdinalIgnoreCase) && exists(ic)) return ic;
        }
        foreach (var pf in programFiles.Where(p => !string.IsNullOrEmpty(p)))
            foreach (var sub in new[] { new[] { "StnFirmwareUpdater" }, new[] { "OBD Solutions", "StnFirmwareUpdater" }, new[] { "OBDLink", "StnFirmwareUpdater" }, new[] { "ScanTool.net", "StnFirmwareUpdater" }, new[] { "OBD Solutions" }, new[] { "OBDLink" } })
            { var p = Path.Combine(new[] { pf }.Concat(sub).Append(AppExeName).ToArray()); if (exists(p)) return p; }
        return null;
    }

    /// <summary>Which exe "Run official updater" starts: a valid Settings override wins, then an installed copy, then the downloaded installer.</summary>
    public static (string? Path, string Kind) ResolveLaunch(string? overridePath, string? installed, string? downloaded, Func<string, bool> exists) =>
        !string.IsNullOrWhiteSpace(overridePath) && exists(overridePath) ? (overridePath, "override")
        : installed is not null ? (installed, "installed")
        : downloaded is not null && exists(downloaded) ? (downloaded, "installer")
        : (null, "none");

    /// <summary>GET with manual redirects; every hop must pass <see cref="IsAllowedUrl"/>. Caller disposes the response.</summary>
    public static async Task<HttpResponseMessage> SendCheckedAsync(HttpClient http, HttpMethod method, string url, CancellationToken ct)
    {
        for (int hop = 0; ; hop++)
        {
            if (!IsAllowedUrl(url)) throw new InvalidOperationException("Refusing non-official URL (https scantool.net only): " + url);
            var resp = await http.SendAsync(new HttpRequestMessage(method, url), HttpCompletionOption.ResponseHeadersRead, ct);
            if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location is { } loc)
            {
                resp.Dispose();
                if (hop >= MaxRedirects) throw new InvalidOperationException("Too many redirects");
                url = new Uri(new Uri(url), loc).ToString();
                continue;
            }
            return resp;
        }
    }

    /// <summary>HttpClient that does NOT follow redirects by itself (so each hop is host-checked).</summary>
    public static HttpClient CreateClient(HttpMessageHandler? handler = null)
    {
        var http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("OBDLinkDiag-updater-download");
        return http;
    }

    /// <summary>Looks up the current installer URL on the official page; falls back to the known link if the page cannot be read.</summary>
    public static async Task<string> FindInstallerUrlAsync(HttpClient http, CancellationToken ct = default)
    {
        try
        {
            using var r = await SendCheckedAsync(http, HttpMethod.Get, PageUrl, ct);
            if (r.IsSuccessStatusCode && ParseInstallerUrl(await r.Content.ReadAsStringAsync(ct)) is { } u) return u;
        }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex) { Serilog.Log.Information("Updater page not readable, using the known installer link: {Err}", ex.Message); }
        return KnownInstallerUrl;
    }

    /// <summary>
    /// Downloads the installer into <paramref name="dir"/> unless a valid copy that matches the server is already there.
    /// <paramref name="verifySignature"/> runs WinVerifyTrust on Windows. Throws if the trust decision is Rejected; Unverified is returned for the caller to confirm.
    /// </summary>
    public static async Task<(UpdaterManifest Manifest, UpdaterTrust Trust, bool Reused)> DownloadAsync(HttpClient http, string dir, Func<string, SignatureInfo> verifySignature, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(dir);
        var url = await FindInstallerUrlAsync(http, ct);
        long? len = null; string? lm = null, etag = null;
        if (ExistingValid(dir, out var old) && old is not null)
        {
            try
            {
                using var h = await SendCheckedAsync(http, HttpMethod.Head, url, ct);
                len = h.Content.Headers.ContentLength; lm = h.Content.Headers.LastModified?.ToString("R"); etag = h.Headers.ETag?.Tag;
            }
            catch (InvalidOperationException) { throw; }
            catch (Exception ex) { Serilog.Log.Information("Updater HEAD check failed, comparing with the saved copy only: {Err}", ex.Message); }
            if (!ServerIsNewer(old, url, len, lm, etag) && ConfinedFile(dir, old.FileName) is { } p)
            {
                var trust = Evaluate(verifySignature(p), old.Sha256);
                if (trust != UpdaterTrust.Rejected) return (old, trust, true);
            }
        }
        var rawName = Path.GetFileName(new Uri(url).AbsolutePath);
        var name = ConfinedFile(dir, rawName) is null ? InstallerFileName : rawName;
        var file = ConfinedFile(dir, name) ?? Path.Combine(dir, InstallerFileName);
        var tmp = file + ".part";
        try
        {
            using (var resp = await SendCheckedAsync(http, HttpMethod.Get, url, ct))
            {
                resp.EnsureSuccessStatusCode();
                len = resp.Content.Headers.ContentLength; lm = resp.Content.Headers.LastModified?.ToString("R"); etag = resp.Headers.ETag?.Tag;
                if (len > MaxBytes) throw new InvalidOperationException($"Installer is too large ({len} bytes)");
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(tmp);
                var buf = new byte[81920]; long total = 0; int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    total += n; if (total > MaxBytes) throw new InvalidOperationException("Installer is too large");
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    if (len > 0) progress?.Report(100.0 * total / len.Value);
                }
                if (len is not null && total != len) throw new InvalidDataException($"Size mismatch: got {total} of {len} bytes");
            }
            var bytes = await File.ReadAllBytesAsync(tmp, ct);
            if (!LooksLikeInstaller(bytes)) throw new InvalidDataException("Downloaded file is not a Windows installer (no MZ / Inno Setup header)");
            var sha = Sha256(tmp);
            var sig = verifySignature(tmp);
            var t = Evaluate(sig, sha);
            if (t == UpdaterTrust.Rejected) throw new InvalidDataException($"Installer signature is not from OBD Solutions/ScanTool (publisher: {sig.Publisher ?? "none"}, valid: {sig.TrustValid}). Not saved.");
            File.Move(tmp, file, true);
            var m = new UpdaterManifest(url, name, bytes.LongLength, sha, ReadVersion(bytes), lm, etag, sig.HasSignature, sig.TrustValid, sig.Publisher, KnownGoodSha256.ContainsKey(sha), DateTime.UtcNow);
            File.WriteAllText(Path.Combine(dir, ManifestName), JsonSerializer.Serialize(m, IndentedJson));
            return (m, t, false);
        }
        catch { try { File.Delete(tmp); } catch { /* temp cleanup only; the original error is rethrown */ } throw; }
    }
}
