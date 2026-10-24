using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using ObdLink.Core.Firmware;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

public class V022FirmwareTests
{
    static string Feed => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "firmware-feed-2231.json"));

    sealed class Fake : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> F = _ => throw new HttpRequestException("offline");
        public List<HttpRequestMessage> Seen = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Seen.Add(r); return Task.FromResult(F(r)); }
    }

    [Theory]
    [InlineData("STN2231 v5.13.0", 2231, "5.13.0")]
    [InlineData("STN2255 v5.10.3 (SIMULATED)", 2255, "5.10.3")]
    [InlineData("stn2230 v5.8", 2230, "5.8.0")]
    public void ParsesSti(string sti, int chip, string ver)
    {
        var f = StnFirmware.Parse(sti)!; Assert.Equal(chip, f.Chip); Assert.Equal(Version.Parse(ver), f.Version);
    }
    [Theory][InlineData(null)][InlineData("?")][InlineData("ELM327 v1.5")] public void RejectsNonStn(string? s) => Assert.Null(StnFirmware.Parse(s));

    [Theory]
    [InlineData("5.13.0", "5.13.1", FirmwareStatus.UpdateAvailable)]
    [InlineData("5.9.10", "5.10.3", FirmwareStatus.UpdateAvailable)]   // numeric, not string compare
    [InlineData("5.13.1", "5.13.1", FirmwareStatus.Current)]
    [InlineData("5.14", "5.13.1", FirmwareStatus.Current)]
    public void VersionCompare(string inst, string latest, FirmwareStatus st)
    {
        var fw = StnFirmware.Parse("STN2231 v" + inst)!;
        var r = new FirmwareRelease(2231, "EX", Version.Parse(latest), DateTime.UtcNow, null, "", Array.Empty<string>());
        Assert.Equal(st, FirmwareChecker.Evaluate(fw, new[] { r }).Status);
    }

    [Fact]
    public void ParsesFeedStableOnlyForDevice()
    {
        var l = FirmwareFeed.Parse(Feed, 2231);
        Assert.Equal(new Version(5, 13, 1), l[0].Version);            // beta 5.14.0 and other-device 9.9.9 ignored
        Assert.DoesNotContain(l, x => x.Version == new Version(5, 14, 0) || x.Version == new Version(9, 9, 9));
        Assert.Equal("OBDLink EX (2231)", l[0].DeviceName);
        Assert.StartsWith("https://www.scantool.net/", l[0].PackageUrl);
        Assert.NotEmpty(l[0].Changelog);
        Assert.Equal(new DateTime(2026, 7, 8), l[0].ReleaseDate.Date);
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => FirmwareFeed.Parse("not json", 2231)); Assert.Throws<FormatException>(() => FirmwareFeed.Parse("{}", 2231));
    }

    [Fact]
    public void UrlAndPages()
    {
        var u = FirmwareFeed.BuildUrl(2255);
        Assert.Contains("obdlinkappregs-58d8.restdb.io/rest/firmware", u); Assert.Contains(Uri.EscapeDataString("\"device_id\":2255"), u); Assert.Contains("stable", Uri.UnescapeDataString(u));
        Assert.Equal(FirmwareFeed.ExPage, FirmwareFeed.OfficialPage(2231));
        Assert.Equal(FirmwareFeed.MxpPage, FirmwareFeed.OfficialPage(2255));
        Assert.True(FirmwareFeed.IsOfficialUrl("https://www.scantool.net/scantool/downloads/769/obdlink_ex-5.13.1-stn2231.zip"));
        Assert.False(FirmwareFeed.IsOfficialUrl("http://www.scantool.net/x.zip"));
        Assert.False(FirmwareFeed.IsOfficialUrl("https://www.scantool.net.evil.com/x.zip"));
        Assert.False(FirmwareFeed.IsOfficialUrl("https://example.com/x.zip"));
    }

    [Fact]
    public async Task OfflineIsSilentFallback()
    {
        var h = new Fake();
        var r = await new FirmwareChecker(h, probeTimeout: TimeSpan.FromMilliseconds(200)).CheckAsync("STN2231 v5.13.0");
        Assert.Equal(FirmwareStatus.Offline, r.Status); Assert.Single(h.Seen); Assert.Equal(HttpMethod.Head, h.Seen[0].Method);
    }

    [Fact]
    public async Task FeedFailureAfterProbeIsOffline()
    {
        var h = new Fake(); int n = 0;
        h.F = _ => ++n == 1 ? new HttpResponseMessage(HttpStatusCode.NotFound) : throw new HttpRequestException("dropped");
        Assert.Equal(FirmwareStatus.Offline, (await new FirmwareChecker(h).CheckAsync("STN2231 v5.13.0")).Status);
    }

    [Fact]
    public async Task OnlineReportsUpdateWithHeaders()
    {
        var h = new Fake();
        h.F = r => r.Method == HttpMethod.Head ? new HttpResponseMessage(HttpStatusCode.OK) : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Feed) };
        var res = await new FirmwareChecker(h).CheckAsync("STN2231 v5.13.0");
        Assert.Equal(FirmwareStatus.UpdateAvailable, res.Status);
        Assert.Equal(new Version(5, 13, 1), res.Latest!.Version); Assert.Equal(FirmwareFeed.ExPage, res.OfficialPage);
        var get = h.Seen[1]; Assert.True(get.Headers.Contains("x-apikey")); Assert.Contains("5.13.0", res.Message); Assert.Contains("5.13.1", res.Message);
    }

    [Fact]
    public async Task UnsupportedAdapterSkipsNetwork()
    {
        var h = new Fake();
        Assert.Equal(FirmwareStatus.Unsupported, (await new FirmwareChecker(h).CheckAsync("?")).Status);
        Assert.Empty(h.Seen);
    }

    [Fact]
    public void SimulatorReportsOlderFirmware()
    {
        foreach (var p in new[] { SimProfile.Ex, SimProfile.MxPlus })
        {
            var sti = new SimulatedAdapter { Profile = p }.Respond("STI");
            var fw = StnFirmware.Parse(sti)!; Assert.True(fw.Version < new Version(5, 13, 1)); Assert.True(FirmwareFeed.IsSupportedChip(fw.Chip));
        }
    }

    static byte[] Pkg(bool goodHeader = true, bool withBin = true)
    {
        var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            if (withBin) using (var s = z.CreateEntry("obdlink_ex-5.13.1-stn2231.bin").Open()) s.Write(Encoding.ASCII.GetBytes((goodHeader ? "STNFWv05" : "XXXXXXXX") + new string('\0', 100)));
            using (var s = new StreamWriter(z.CreateEntry("obdlink_ex-stn2231-changelog.txt").Open())) s.Write("OBDLink EX changelog");
        }
        return ms.ToArray();
    }
    static FirmwareRelease Rel(string url) => new(2231, "OBDLink EX (2231)", new Version(5, 13, 1), new DateTime(2026, 7, 8), url, "s", new[] { "fix" });

    [Fact]
    public async Task DownloadVerifiesAndKeepsChangelog()
    {
        var root = Path.Combine(Path.GetTempPath(), "fwt" + Guid.NewGuid().ToString("N"));
        var bytes = Pkg(); var h = new Fake();
        h.F = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = r };
        var fw = StnFirmware.Parse("STN2231 v5.13.0")!;
        var d = await FirmwareDownloader.DownloadAsync(new HttpClient(h), root, fw, Rel("https://www.scantool.net/scantool/downloads/769/obdlink_ex-5.13.1-stn2231.zip"));
        Assert.Equal(Path.Combine(root, "OBDLink-EX-STN2231", "5.13.1"), d.Folder);
        Assert.True(File.Exists(d.ZipPath)); Assert.Equal(bytes.Length, d.Size); Assert.True(File.Exists(d.ChangelogPath));
        Assert.True(File.Exists(Path.Combine(d.Folder, "README.txt")));
        Assert.Empty(Directory.GetFiles(d.Folder, "*.bin"));                  // the image stays inside the zip
        Directory.Delete(root, true);
    }

    [Fact]
    public async Task DownloadRejectsBadPackagesAndUrls()
    {
        var root = Path.Combine(Path.GetTempPath(), "fwt" + Guid.NewGuid().ToString("N")); var fw = StnFirmware.Parse("STN2231 v5.13.0")!;
        const string url = "https://www.scantool.net/a/obdlink_ex-5.13.1-stn2231.zip";
        foreach (var bad in new[] { Pkg(goodHeader: false), Pkg(withBin: false), Encoding.ASCII.GetBytes("not a zip") })
        {
            var h = new Fake(); h.F = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bad), RequestMessage = r };
            await Assert.ThrowsAnyAsync<Exception>(() => FirmwareDownloader.DownloadAsync(new HttpClient(h), root, fw, Rel(url)));
            Assert.Empty(Directory.GetFiles(root, "*.zip*", SearchOption.AllDirectories));
        }
        var h2 = new Fake();
        await Assert.ThrowsAsync<InvalidOperationException>(() => FirmwareDownloader.DownloadAsync(new HttpClient(h2), root, fw, Rel("https://evil.example/x.zip")));
        Assert.Empty(h2.Seen);
        var h3 = new Fake(); h3.F = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Pkg()), RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://evil.example/x.zip") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => FirmwareDownloader.DownloadAsync(new HttpClient(h3), root, fw, Rel(url)));
        Directory.Delete(root, true);
    }

    [Fact]
    public void VerifiesRealPackageShape()
    {
        var p = Path.GetTempFileName(); File.WriteAllBytes(p, Pkg());
        var (bin, log) = FirmwareDownloader.Verify(p); Assert.EndsWith(".bin", bin); Assert.NotNull(log); File.Delete(p);
    }
}
