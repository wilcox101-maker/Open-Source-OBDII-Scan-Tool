using System.Text;
using ObdLink.Core.Firmware;
using Xunit;

namespace ObdLink.Core.Tests;

public class V031Tests
{
    [Theory]
    [InlineData("https://www.scantool.net/scantool/downloads/740/Install-StnFirmwareUpdater.exe", true)]
    [InlineData("https://scantool.net/x/Install-StnFirmwareUpdater.exe", true)]
    [InlineData("https://WWW.SCANTOOL.NET/a.EXE", true)]
    [InlineData("http://www.scantool.net/a.exe", false)]
    [InlineData("https://www.scantool.net.evil.com/a.exe", false)]
    [InlineData("https://evilscantool.net/a.exe", false)]
    [InlineData("https://cdn.scantool.net/a.exe", false)]
    [InlineData("https://www.scantool.net@evil.com/a.exe", false)]
    [InlineData("https://user@www.scantool.net/a.exe", false)]
    [InlineData("https://www.scantool.net:8443/a.exe", false)]
    [InlineData("https://www.scantool.net/a.zip", false)]
    [InlineData("ftp://www.scantool.net/a.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void InstallerUrlHostValidation(string? url, bool ok) => Assert.Equal(ok, OfficialUpdater.IsAllowedInstallerUrl(url));

    [Fact]
    public void ParsesInstallerLinkAndRejectsForeignHosts()
    {
        Assert.Equal(OfficialUpdater.KnownInstallerUrl, OfficialUpdater.ParseInstallerUrl("<a href=\"/scantool/downloads/740/Install-StnFirmwareUpdater.exe\">Download</a>"));
        Assert.Null(OfficialUpdater.ParseInstallerUrl("<a href=\"https://evil.com/Install-StnFirmwareUpdater.exe\">x</a>"));
        Assert.Null(OfficialUpdater.ParseInstallerUrl("<a href=\"http://www.scantool.net/Install-StnFirmwareUpdater.exe\">x</a>"));
        Assert.True(OfficialUpdater.IsAllowedInstallerUrl(OfficialUpdater.KnownInstallerUrl));
    }

    [Fact]
    public void TrustPolicy()
    {
        var pinned = OfficialUpdater.KnownGoodSha256.Keys.First();
        Assert.Equal(UpdaterTrust.TrustedSignature, OfficialUpdater.Evaluate(new(true, true, "OBD Solutions LLC"), "00"));
        Assert.Equal(UpdaterTrust.Rejected, OfficialUpdater.Evaluate(new(true, true, "Evil Corp"), pinned));
        Assert.Equal(UpdaterTrust.Rejected, OfficialUpdater.Evaluate(new(true, false, "OBD Solutions LLC"), "00"));
        Assert.Equal(UpdaterTrust.KnownHash, OfficialUpdater.Evaluate(new(false, false, null), pinned.ToUpperInvariant()));
        Assert.Equal(UpdaterTrust.Unverified, OfficialUpdater.Evaluate(new(false, false, null), "00"));
    }

    [Fact]
    public void ReadsVersionResourceAndInstallerMarkers()
    {
        var b = new List<byte> { (byte)'M', (byte)'Z' };
        b.AddRange(new byte[62]); b.AddRange(Encoding.ASCII.GetBytes("Inno Setup Setup Data (6.1.0)"));
        while (b.Count % 4 != 0) b.Add(0);
        b.AddRange(Encoding.Unicode.GetBytes("ProductVersion\0")); while (b.Count % 4 != 0) b.Add(0);
        b.AddRange(Encoding.Unicode.GetBytes("2.4.2.0\0"));
        var a = b.ToArray();
        Assert.Equal("2.4.2.0", OfficialUpdater.ReadVersion(a));
        Assert.True(OfficialUpdater.LooksLikeInstaller(a));
        Assert.False(OfficialUpdater.HasAuthenticodeBlob(a));
        Assert.False(OfficialUpdater.LooksLikeInstaller(Encoding.ASCII.GetBytes("<html>Inno Setup</html>")));
    }

    [Fact]
    public void FindInstalledPrefersRegistryThenProgramFiles()
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine("C:/Inst", OfficialUpdater.AppExeName), Path.Combine("C:/PF86", "OBD Solutions", "StnFirmwareUpdater", OfficialUpdater.AppExeName) };
        var reg = new (string?, string?, string?)[] { ("Other", "C:/Inst", null), ("StnFirmwareUpdater version 2.4.2.0", "\"C:/Inst\"", null) };
        Assert.Equal(Path.Combine("C:/Inst", OfficialUpdater.AppExeName), OfficialUpdater.FindInstalled(reg, new[] { "C:/PF86" }, files.Contains));
        Assert.Equal(Path.Combine("C:/PF86", "OBD Solutions", "StnFirmwareUpdater", OfficialUpdater.AppExeName), OfficialUpdater.FindInstalled(new (string?, string?, string?)[] { ("Other", "C:/Inst", null) }, new[] { "", "C:/PF86" }, files.Contains));
        var icon = Path.Combine("D:/X", OfficialUpdater.AppExeName); files.Add(icon);
        Assert.Equal(icon, OfficialUpdater.FindInstalled(new (string?, string?, string?)[] { ("STN Firmware Update Utility", "D:/missing", icon + ",0") }, Array.Empty<string>(), files.Contains));
        Assert.Null(OfficialUpdater.FindInstalled(Array.Empty<(string?, string?, string?)>(), new[] { "C:/none" }, files.Contains));
    }

    [Fact]
    public void ResolveLaunchOrder()
    {
        Func<string, bool> ex = p => p != "missing";
        Assert.Equal("override", OfficialUpdater.ResolveLaunch("o.exe", "i.exe", "d.exe", ex).Kind);
        Assert.Equal("installed", OfficialUpdater.ResolveLaunch("missing", "i.exe", "d.exe", ex).Kind);
        Assert.Equal("installer", OfficialUpdater.ResolveLaunch(null, null, "d.exe", ex).Kind);
        Assert.Equal("none", OfficialUpdater.ResolveLaunch("", null, "missing", ex).Kind);
    }

    [Fact]
    public void NewerVersionDetection()
    {
        var m = new UpdaterManifest(OfficialUpdater.KnownInstallerUrl, "a.exe", 100, "x", "2.4.2.0", "Sat, 24 Oct 2026 15:41:42 GMT", null, false, false, null, true, DateTime.UtcNow);
        Assert.False(OfficialUpdater.ServerIsNewer(m, OfficialUpdater.KnownInstallerUrl, 100, m.LastModified, null));
        Assert.False(OfficialUpdater.ServerIsNewer(m, OfficialUpdater.KnownInstallerUrl, null, null, null));
        Assert.True(OfficialUpdater.ServerIsNewer(m, OfficialUpdater.KnownInstallerUrl, 200, m.LastModified, null));
        Assert.True(OfficialUpdater.ServerIsNewer(m, "https://www.scantool.net/scantool/downloads/999/Install-StnFirmwareUpdater.exe", 100, null, null));
    }

    [Fact]
    public async Task RedirectToForeignHostIsRefused()
    {
        using var http = OfficialUpdater.CreateClient(new Redirector("https://evil.example.com/a.exe"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => OfficialUpdater.SendCheckedAsync(http, HttpMethod.Get, OfficialUpdater.KnownInstallerUrl, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => OfficialUpdater.SendCheckedAsync(http, HttpMethod.Get, "http://www.scantool.net/a.exe", default));
    }
    private sealed class Redirector(string to) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        { var m = new HttpResponseMessage(System.Net.HttpStatusCode.Found); m.Headers.Location = new Uri(to); return Task.FromResult(m); }
    }
}
