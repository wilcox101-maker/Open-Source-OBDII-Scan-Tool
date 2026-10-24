using ObdLink.Core.Dashboard;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Obd;
using ObdLink.Core.Security;
using ObdLink.Core.Transport;
using ObdLink.Core.Vehicles;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.3.4: code audit fixes and the OSS widget.</summary>
public class V034Tests
{
    private static string Src(string f, [System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(me)!, "..", "..", "src", f));

    [Theory]
    [InlineData("https://github.com/wilcox101-maker/Open-Source-OBDII-Scan-Tool", true)]
    [InlineData("https://www.motorcraftservice.com/AsBuilt", true)]
    [InlineData("https://www.scantool.net/downloads/updates/", true)]
    [InlineData("explorer.exe", true)]
    [InlineData(@"C:\Tools\OBDLinkDiag\Data", true)]
    [InlineData("http://github.com/", false)]               // not https
    [InlineData("https://github.com.evil.example/", false)]  // look-alike host
    [InlineData("https://user@github.com/", false)]
    [InlineData("https://github.com:8443/", false)]
    [InlineData("cmd.exe", false)]
    [InlineData("powershell", false)]
    [InlineData(@"\\server\share\x.exe", false)]             // UNC
    [InlineData("file://server/share/x.exe", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    public void ExternalTargetsAllowList(string target, bool ok) => Assert.Equal(ok, ExternalTargets.IsAllowed(target));

    [Fact]
    public void OpenExternalChecksAllowList() => Assert.Contains("ExternalTargets.IsAllowed(target)", Src("ObdLink.App/DialogService.cs"));

    [Fact]
    public void VpicClientIsDisposedAfterIdentify()
    {
        Assert.True(typeof(IDisposable).IsAssignableFrom(typeof(VpicClient)));
        new VpicClient().Dispose();
        Assert.Contains("using var vpic = OnlineVinDecode ? new VpicClient() : null;", Src("ObdLink.App/MainViewModel.cs"));
    }

    [Fact]
    public void AsyncVoidExecuteCannotThrow()
    {
        var s = Src("ObdLink.App/Mvvm.cs");
        Assert.DoesNotContain("public async void Execute(object? p) => await ExecuteAsync();", s);
        Assert.Contains("try { await ExecuteAsync(); } catch (Exception ex)", s);
    }

    [Fact]
    public void DashboardLoopsDoNotOverlap()
    {
        var s = Src("ObdLink.App/DashboardViewModel.cs");
        Assert.Contains("_run = RunAfterAsync(_run, _cts.Token)", s);
        Assert.DoesNotContain("_ = RunAsync(_cts.Token)", s);
    }

    [Fact]
    public void DefaultPortIsNeverNull()
    {
        Assert.Contains("string.IsNullOrWhiteSpace(value) ? \"\" : value.Trim()", Src("ObdLink.App/MainViewModel.cs"));
    }

    [Fact]
    public void WParamIsNotTruncated() => Assert.Contains("wParam.ToInt64() is", Src("ObdLink.App/MainWindow.xaml.cs"));

    [Fact]
    public async Task SerialPortFailedOpenLeavesNothingOpen()
    {
        var t = new SerialPortTransport("COM_DOES_NOT_EXIST_99");
        await Assert.ThrowsAnyAsync<Exception>(() => t.OpenAsync());
        Assert.False(t.IsOpen);
        await Assert.ThrowsAsync<InvalidOperationException>(() => t.WriteAsync("ATZ\r")); // no half-built port kept
        await t.DisposeAsync();
    }

    // ---- OSS widget ----

    [Fact]
    public void OssIsUnverifiedReadOnlyAndInCatalogButNotDefault()
    {
        var t = EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson());
        var oss = t.Pids.Single(p => p.Key == "oss");
        Assert.False(oss.Verified); Assert.Contains("UNVERIFIED", oss.Source);
        Assert.Equal("rpm", oss.Unit); Assert.Equal(0x22, oss.Request[0]);
        Assert.Equal(new uint[] { 0x7E0, 0x7E1 }, oss.Candidates.ToArray());
        Assert.Equal(1000, oss.Decode(new byte[] { 0x62, 0x1E, 0x13, 0x0F, 0xA0 }), 3); // 4000/4
        Assert.Contains(DashCatalog.Build(t), c => c.Key == "oss" && !c.Verified);
        Assert.DoesNotContain(DashboardLayout.Default().Items, i => i.Key == "oss"); // off by default; add it via Customize
    }

    [Fact]
    public void OlderPidFileGainsOss()
    {
        var old = EnhancedPidTable.Parse("{\"pids\":[{\"key\":\"tft\",\"did\":\"1E1C\",\"scale\":2}]}");
        var merged = EnhancedPidTable.WithBuiltIns(old);
        Assert.Equal(2, merged.Pids.Single(p => p.Key == "tft").Scale); // user edit kept
        Assert.Contains(merged.Pids, p => p.Key == "oss");
    }

    [Fact]
    public async Task DemoOssFallsBackFromPcmToTcm()
    {
        var sim = new SimulatedAdapter { Clock = () => 60 };
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync(); await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var eng = new DashboardEngine(new ElmDiagChannel(s)); var sent = new List<string>(); eng.Sent += sent.Add;
        await eng.DiscoverAsync();
        eng.Configure(new[] { "oss" }, EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson()).Pids.Where(p => p.Key == "oss"));
        var v = await eng.PollOnceAsync();
        Assert.InRange(v["oss"] - SimulatedAdapter.OssRpm(DriveCycle.At(60).SpeedKmh), -0.25, 0.25);
        Assert.Equal(0x7E1u, eng.AnsweredBy["oss"]);
        Assert.All(sent, x => Assert.True(x.StartsWith("01") || x.StartsWith("22"), x)); // read-only
    }
}
