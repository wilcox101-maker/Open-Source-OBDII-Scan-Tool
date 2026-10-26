using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ObdLink.Core.Firmware;

/// <summary>Adapter firmware parsed from the STI reply, e.g. "STN2231 v5.13.0" -> chip 2231, 5.13.0.</summary>
public sealed record StnFirmware(int Chip, Version Version, string Raw)
{
    private static readonly Regex Rx = new(@"STN\s*(\d{4})\s+v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
    public static StnFirmware? Parse(string? sti)
    {
        if (string.IsNullOrWhiteSpace(sti)) return null;
        var m = Rx.Match(sti); if (!m.Success) return null;
        return Version.TryParse(m.Groups[2].Value, out var v) ? new(int.Parse(m.Groups[1].Value), Norm(v), sti.Trim()) : null;
    }
    internal static Version Norm(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
    /// <summary>Folder name for Data\firmware\&lt;model&gt;.</summary>
    public string Model => FirmwareFeed.ModelOf(Chip);
}

public sealed record FirmwareRelease(int DeviceId, string DeviceName, Version Version, DateTime ReleaseDate, string? PackageUrl, string Summary, IReadOnlyList<string> Changelog)
{
    public string ChangelogText => string.Join(Environment.NewLine, Changelog.Select(c => "- " + c));
}

public enum FirmwareStatus { Current, UpdateAvailable, Offline, Unknown, Unsupported }

public sealed record FirmwareCheckResult(FirmwareStatus Status, StnFirmware? Installed, FirmwareRelease? Latest, string Message)
{
    public string OfficialPage => Installed is null ? FirmwareFeed.FirmwareIndex : FirmwareFeed.OfficialPage(Installed.Chip);
}

/// <summary>
/// Public (undocumented) OBDLink firmware feed that OBDLink's own web page uses. Read-only GET; nothing about the vehicle or user is sent.
/// The restdb.io API key is the public key embedded in scantool.net's page, and the service only answers requests with that site's Origin header.
/// </summary>
public static class FirmwareFeed
{
    public const string Host = "obdlinkappregs-58d8.restdb.io";
    public const string ApiKey = "63256e7c0282ae17e4905db8";
    public const string Origin = "https://www.scantool.net";
    public const string FirmwareIndex = "https://www.scantool.net/downloads/updates/";
    public const string ExPage = "https://www.scantool.net/downloads/updates/obdlink_ex/";
    public const string MxpPage = "https://www.scantool.net/downloads/updates/obdlink_mxp/";

    public static bool IsSupportedChip(int chip) => chip is 2230 or 2231 or 2255;
    public static string OfficialPage(int chip) => chip switch { 2230 or 2231 => ExPage, 2255 => MxpPage, _ => FirmwareIndex };
    public static string ModelOf(int chip) => chip switch { 2230 or 2231 => $"OBDLink-EX-STN{chip}", 2255 => "OBDLink-MXplus-STN2255", _ => $"STN{chip}" };

    public static string BuildUrl(int deviceId)
    {
        var q = Uri.EscapeDataString($"{{\"device\":{{\"device_id\":{deviceId}}},\"release_channel\":\"stable\"}}");
        var h = Uri.EscapeDataString("{\"$orderby\":{\"release_date\":-1}}");
        return $"https://{Host}/rest/firmware?q={q}&h={h}&max=5";
    }

    /// <summary>Parses the feed JSON; keeps only stable releases for <paramref name="deviceId"/>, newest version first. Throws on malformed JSON.</summary>
    public static IReadOnlyList<FirmwareRelease> Parse(string json, int deviceId)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new FormatException("Firmware feed did not return a list");
        var list = new List<FirmwareRelease>();
        foreach (var r in doc.RootElement.EnumerateArray())
        {
            if (Str(r, "release_channel") is { } ch && !ch.Equals("stable", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Version.TryParse(Str(r, "version") ?? "", out var v)) continue;
            int id = deviceId; string name = "";
            if (r.TryGetProperty("device", out var d))
            {
                var dev = d.ValueKind == JsonValueKind.Array ? d.EnumerateArray().FirstOrDefault() : d;
                if (dev.ValueKind == JsonValueKind.Object)
                {
                    if (dev.TryGetProperty("device_id", out var di) && di.TryGetInt32(out var x)) id = x;
                    name = Str(dev, "name") ?? "";
                }
            }
            if (id != deviceId) continue;
            DateTime.TryParse(Str(r, "release_date"), null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var date);
            var log = new List<string>();
            if (r.TryGetProperty("changelog", out var cl) && cl.ValueKind == JsonValueKind.Array)
                foreach (var c in cl.EnumerateArray())
                    if ((c.ValueKind == JsonValueKind.Object ? Str(c, "long") ?? Str(c, "short") : c.ValueKind == JsonValueKind.String ? c.GetString() : null) is { Length: > 0 } s)
                        log.Add(c.ValueKind == JsonValueKind.Object && Str(c, "type") is { } t ? $"[{t}] {s}" : s);
            list.Add(new(id, name, StnFirmware.Norm(v), date, Str(r, "package_url"), Str(r, "summary") ?? "", log));
        }
        return list.OrderByDescending(x => x.Version).ThenByDescending(x => x.ReleaseDate).ToList();
    }
    private static string? Str(JsonElement e, string p) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Only https downloads from scantool.net are accepted. Default port, no user-info, path must end in .zip and must not climb out with "..".</summary>
    public static bool IsOfficialUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        if (u.Scheme != Uri.UriSchemeHttps || !u.IsDefaultPort || !string.IsNullOrEmpty(u.UserInfo)) return false;
        if (!u.Host.Equals("www.scantool.net", StringComparison.OrdinalIgnoreCase) && !u.Host.Equals("scantool.net", StringComparison.OrdinalIgnoreCase)) return false;
        if (!u.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || u.AbsolutePath.Contains('\\')) return false;
        return !u.AbsolutePath.Split('/').Any(s => s is "..");
    }

    /// <summary>Last path segment of an official zip URL, or null when it is not a single safe file name.</summary>
    public static string? SafeZipFileName(string? url)
    {
        if (!IsOfficialUrl(url) || !Uri.TryCreate(url, UriKind.Absolute, out var u)) return null;
        var name = Path.GetFileName(u.AbsolutePath.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        if (name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0) return null;
        return name;
    }
}

/// <summary>Automatic, silent-when-offline firmware check. Never flashes anything.</summary>
public sealed class FirmwareChecker
{
    private readonly HttpClient _http;
    private readonly TimeSpan _probe;
    public FirmwareChecker(HttpMessageHandler? handler = null, TimeSpan? timeout = null, TimeSpan? probeTimeout = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = timeout ?? TimeSpan.FromSeconds(8);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("OBDLinkDiag-firmware-check");
        _probe = probeTimeout ?? TimeSpan.FromSeconds(3);
    }

    /// <summary>Quick reachability check (HEAD to the feed host, short timeout). Any HTTP answer counts as online.</summary>
    public async Task<bool> IsOnlineAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct); cts.CancelAfter(_probe);
        try { using var r = await _http.SendAsync(new HttpRequestMessage(HttpMethod.Head, $"https://{FirmwareFeed.Host}/"), cts.Token); return true; }
        catch { return false; } // offline is an expected answer, reported by the caller
    }

    public async Task<FirmwareCheckResult> CheckAsync(string? sti, CancellationToken ct = default)
    {
        var fw = StnFirmware.Parse(sti);
        if (fw is null) return new(FirmwareStatus.Unsupported, null, null, "Adapter firmware not reported (STI); not an OBDLink STN adapter");
        if (!FirmwareFeed.IsSupportedChip(fw.Chip)) return new(FirmwareStatus.Unsupported, fw, null, $"STN{fw.Chip} is not an OBDLink EX / MX+; no firmware check");
        if (!await IsOnlineAsync(ct)) return new(FirmwareStatus.Offline, fw, null, "Offline; firmware check skipped");
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, FirmwareFeed.BuildUrl(fw.Chip));
            req.Headers.Add("x-apikey", FirmwareFeed.ApiKey); req.Headers.Add("Origin", FirmwareFeed.Origin);
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new(FirmwareStatus.Unknown, fw, null, $"Firmware feed answered HTTP {(int)resp.StatusCode}");
            return Evaluate(fw, FirmwareFeed.Parse(await resp.Content.ReadAsStringAsync(ct), fw.Chip));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException) { return new(FirmwareStatus.Offline, fw, null, "Firmware feed not reachable; check skipped"); }
        catch (Exception ex) { return new(FirmwareStatus.Unknown, fw, null, "Firmware feed could not be read: " + ex.Message); }
    }

    public static FirmwareCheckResult Evaluate(StnFirmware fw, IReadOnlyList<FirmwareRelease> releases)
    {
        var latest = releases.FirstOrDefault();
        if (latest is null) return new(FirmwareStatus.Unknown, fw, null, "No stable firmware listed for STN" + fw.Chip);
        return fw.Version >= latest.Version
            ? new(FirmwareStatus.Current, fw, latest, $"Adapter firmware v{fw.Version} is current (latest stable v{latest.Version})")
            : new(FirmwareStatus.UpdateAvailable, fw, latest, $"Adapter firmware update available: installed v{fw.Version}, latest stable v{latest.Version} ({latest.ReleaseDate:yyyy-MM-dd})");
    }
}

public sealed record FirmwareDownload(string Folder, string ZipPath, string? ChangelogPath, long Size, string BinName);

/// <summary>Downloads the official package zip into Data\firmware\&lt;model&gt;\&lt;version&gt;\ and verifies it. Does NOT flash; use OBDLink's own updater.</summary>
public static class FirmwareDownloader
{
    public const long MaxBytes = 20 * 1024 * 1024;
    /// <summary>Cap on bytes read out of a zip entry. The package itself is at most <see cref="MaxBytes"/> compressed.</summary>
    public const long MaxUncompressed = 64L * 1024 * 1024;
    public static string FolderFor(string firmwareRoot, StnFirmware fw, FirmwareRelease r) => Path.Combine(firmwareRoot, fw.Model, r.Version.ToString(3));

    public static async Task<FirmwareDownload> DownloadAsync(HttpClient http, string firmwareRoot, StnFirmware fw, FirmwareRelease r, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (!FirmwareFeed.IsOfficialUrl(r.PackageUrl)) throw new InvalidOperationException($"Refusing to download from a non-official URL: {r.PackageUrl}");
        var dir = FolderFor(firmwareRoot, fw, r); Directory.CreateDirectory(dir);
        var url = r.PackageUrl!;
        var name = FirmwareFeed.SafeZipFileName(url) ?? "firmware.zip";
        var zip = Path.Combine(dir, name); var tmp = zip + ".part";
        HttpResponseMessage? resp = null;
        try
        {
            for (int hop = 0; ; hop++)
            {
                resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                if ((int)resp.StatusCode is >= 300 and < 400 && resp.Headers.Location is { } loc)
                {
                    if (hop >= OfficialUpdater.MaxRedirects) throw new InvalidOperationException("Too many redirects");
                    var next = new Uri(new Uri(url), loc).ToString();
                    resp.Dispose(); resp = null;
                    if (!FirmwareFeed.IsOfficialUrl(next)) throw new InvalidOperationException("Download was redirected off scantool.net: " + next);
                    url = next;
                    name = FirmwareFeed.SafeZipFileName(url) ?? name;
                    zip = Path.Combine(dir, name); tmp = zip + ".part";
                    continue;
                }
                break;
            }
            resp!.EnsureSuccessStatusCode();
            var final = resp.RequestMessage?.RequestUri?.ToString();
            if (string.IsNullOrEmpty(final) || !FirmwareFeed.IsOfficialUrl(final))
                throw new InvalidOperationException("Download was redirected off scantool.net: " + (final ?? "(unknown)"));
            var len = resp.Content.Headers.ContentLength;
            if (len > MaxBytes) throw new InvalidOperationException($"Package is too large ({len} bytes)");
            await using (var src = await resp.Content.ReadAsStreamAsync(ct))
            await using (var dst = File.Create(tmp))
            {
                var buf = new byte[81920]; long total = 0; int n;
                while ((n = await src.ReadAsync(buf, ct)) > 0)
                {
                    total += n; if (total > MaxBytes) throw new InvalidOperationException("Package is too large");
                    await dst.WriteAsync(buf.AsMemory(0, n), ct);
                    if (len > 0) progress?.Report(100.0 * total / len.Value);
                }
                if (len is not null && total != len) throw new InvalidDataException($"Size mismatch: got {total} of {len} bytes");
            }
            var (bin, log) = Verify(tmp);
            File.Move(tmp, zip, true);
            string? clPath = null;
            var leaf = log is null ? null : SafeEntryName(log);
            using (var z = ZipFile.OpenRead(zip))
            {
                if (leaf is not null)
                {
                    clPath = Path.Combine(dir, leaf);
                    if (!ObdLink.Core.Security.SafePaths.IsUnder(dir, clPath)) throw new InvalidDataException("Changelog path escapes the firmware folder");
                    z.GetEntry(log!)!.ExtractToFile(clPath, true);
                }
            }
            if (clPath is null && r.Changelog.Count > 0) { clPath = Path.Combine(dir, "changelog.txt"); File.WriteAllText(clPath, $"{r.DeviceName} v{r.Version} ({r.ReleaseDate:yyyy-MM-dd})\n{r.Summary}\n\n{r.ChangelogText}\n"); }
            File.WriteAllText(Path.Combine(dir, "README.txt"),
                $"Official OBDLink firmware package downloaded by OBDLink Diag on {DateTime.Now:yyyy-MM-dd HH:mm}.\nSource: {url}\nAdapter: STN{fw.Chip}, installed v{fw.Version}; this package v{r.Version}.\n\nOBDLink Diag does NOT flash firmware. Use OBDLink's official firmware updater:\n{FirmwareFeed.OfficialPage(fw.Chip)}\n");
            return new(dir, zip, clPath, new FileInfo(zip).Length, bin);
        }
        catch { try { File.Delete(tmp); } catch { /* temp cleanup only; the original error is rethrown */ } throw; }
        finally { resp?.Dispose(); }
    }

    /// <summary>Zip entry leaf name. Slashes and backslashes never become a path.</summary>
    public static string? SafeEntryName(string entry)
    {
        var leaf = entry.Replace('\\', '/').Split('/').Last();
        if (string.IsNullOrWhiteSpace(leaf) || leaf is "." or ".." || leaf.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
        return leaf;
    }

    /// <summary>Checks that the zip opens, every entry reads (CRC), and exactly one .bin starts with "STNFWv". Returns (bin entry, changelog entry).</summary>
    public static (string Bin, string? Changelog) Verify(string zipPath)
    {
        using var z = ZipFile.OpenRead(zipPath);
        string? bin = null, log = null;
        foreach (var e in z.Entries)
        {
            using (var s = e.Open())
            {
                var head = new byte[6]; int got = s.ReadAtLeast(head, 6, false); var rest = new byte[81920]; long expanded = got;
                int n; while ((n = s.Read(rest)) > 0) { expanded += n; if (expanded > MaxUncompressed) throw new InvalidDataException($"{e.Name} expands past {MaxUncompressed} bytes"); }
                if (e.Name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase))
                {
                    if (got < 6 || Encoding.ASCII.GetString(head) != "STNFWv") throw new InvalidDataException($"{e.Name} is not an STN firmware image (missing STNFWv header)");
                    if (bin is not null) throw new InvalidDataException("Package contains more than one firmware image");
                    bin = e.FullName;
                }
            }
            if (e.Name.Contains("changelog", StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) log = e.FullName;
        }
        return (bin ?? throw new InvalidDataException("Package contains no .bin firmware image"), log);
    }
}
