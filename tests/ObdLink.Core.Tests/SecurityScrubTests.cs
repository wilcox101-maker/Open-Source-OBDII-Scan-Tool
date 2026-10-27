using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.ConsoleCmds;
using ObdLink.Core.Elm;
using ObdLink.Core.Firmware;
using ObdLink.Core.Logging;
using ObdLink.Core.Safety;
using ObdLink.Core.Security;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using ObdLink.Core.Vehicles;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Post-v0.5.0 security scrub: console write bypass, path confinement, firmware URL checks.</summary>
public class SecurityScrubTests
{
    private static string Src(string f, [System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(me)!, "..", "..", "src", f));

    private static async Task<(SimulatedAdapter Sim, AdapterSession Session, string Dir)> Link()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        var dir = Path.Combine(Path.GetTempPath(), "sec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return (sim, s, dir);
    }

    [Theory]
    [InlineData("2E DE00 01")]
    [InlineData("3D 01 02 03")]
    [InlineData("08 01")]
    [InlineData("38 01")]
    [InlineData("STPX h:7E0,d:2EDE0001")]
    [InlineData("ATWM 2EDE0001")]
    [InlineData("ATCAF0")]
    [InlineData("ATRV\r2EDE0001")]
    public void Locked_console_refuses_hidden_writes(string cmd)
    {
        Assert.False(ConsoleGuard.Evaluate(cmd, readOnly: true, allowWrites: true).Allowed);
        Assert.False(ConsoleGuard.Evaluate(cmd, readOnly: false, allowWrites: false).Allowed);
        Assert.StartsWith("BLOCKED: read-only", ConsoleGuard.Evaluate(cmd, true, true).Reason);
    }

    [Theory]
    [InlineData("ATRV")]
    [InlineData("ATI")]
    [InlineData("STI")]
    [InlineData("STP33")]
    [InlineData("ATSH7E0")]
    [InlineData("0100")]
    [InlineData("0902")]
    [InlineData("22F190")]
    [InlineData("1003")]
    [InlineData("3E00")]
    [InlineData("19 02 FF")]
    [InlineData("012E")]
    public void Locked_console_still_allows_reads_and_adapter_setup(string cmd)
    {
        Assert.True(ConsoleGuard.Evaluate(cmd, readOnly: true, allowWrites: false, canAutoFormat: true).Allowed);
    }

    [Fact]
    public void Formatting_off_sees_the_service_behind_the_pci()
    {
        Assert.False(ConsoleGuard.Evaluate("06 2E DE 00 01 02", readOnly: true, allowWrites: true, canAutoFormat: false).Allowed);
        Assert.False(ConsoleGuard.Evaluate("10 0A 2E DE 00 01 02 03 04 05", readOnly: true, allowWrites: true, canAutoFormat: false).Allowed);
        Assert.True(ConsoleGuard.Evaluate("012E", readOnly: true, allowWrites: false, canAutoFormat: true).Allowed);
        Assert.False(ConsoleGuard.Evaluate("012E", readOnly: true, allowWrites: false, canAutoFormat: false).Allowed);
        Assert.True(ConsoleGuard.NextAutoFormat(true, "ATCAF0") == false);
        Assert.True(ConsoleGuard.NextAutoFormat(false, "ATZ"));
    }

    [Fact]
    public void Writes_enabled_session_may_send_a_write()
    {
        Assert.True(ConsoleGuard.Evaluate("2E DE00 01", readOnly: false, allowWrites: true).Allowed);
    }

    [Fact]
    public async Task Blocked_console_line_is_not_sent()
    {
        var (_, s, dir) = await Link();
        try
        {
            var sent = new List<string>();
            s.Link.Traffic += (d, t) => { if (d == ">>") sent.Add(t); };
            var con = new RawConsoleService(s.Link) { AllowWrites = true, ReadOnlyMode = true };
            Assert.StartsWith("BLOCKED: read-only", await con.ExecuteAsync("STPX h:7E0,d:2EDE0001"));
            Assert.StartsWith("BLOCKED: read-only", await con.ExecuteAsync("ATCAF0"));
            Assert.DoesNotContain(sent, c => c.Contains("STPX", StringComparison.OrdinalIgnoreCase) || c.Contains("ATCAF0", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("BLOCKED", await con.ExecuteAsync("22F190"));
            Assert.DoesNotContain("BLOCKED", await con.ExecuteAsync("012E"));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Backup_filename_cannot_leave_the_backup_folder()
    {
        var (sim, s, dir) = await Link();
        try
        {
            sim.Dids[0x7E0] = new() { [0xDE00] = new byte[] { 1, 2, 3, 4, 5 } };
            var svc = new AsBuiltService(m => new UdsClient(new ObdLink.Core.IsoTp.ElmDiagChannel(s), m), new SafetyGuard(s, new AutoConfirm(false)), dir);
            var path = await svc.BackupAsync(@"1FT..\..\..\..\xy", 0x7E0, new byte[] { 0x01 });
            Assert.True(SafePaths.IsUnder(dir, path));
            Assert.DoesNotContain("..", Path.GetFileName(path));
            var saved = new VehicleProfile { Vin = @"1FT..\..\..\..\xy" }.Save(dir);
            Assert.True(SafePaths.IsUnder(dir, saved));
            Assert.DoesNotContain("\\", Path.GetFileName(saved));
            Assert.DoesNotContain("/", Path.GetFileName(saved));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Profile_name_strips_path_characters_from_a_masked_vin()
    {
        var name = VehicleProfile.FileNameFor(@"1FT..\..\..\..\xy");
        Assert.EndsWith(".json", name);
        Assert.DoesNotContain("\\", name);
        Assert.DoesNotContain("/", name);
        Assert.Equal("XXXXXXXXXXX000001.json", VehicleProfile.FileNameFor("1FTDEMX95HF000001"));
    }

    [Theory]
    [InlineData("https://www.scantool.net/a/obdlink.zip", true)]
    [InlineData("https://user:pw@www.scantool.net/a/obdlink.zip", false)]
    [InlineData("https://www.scantool.net:8443/a/obdlink.zip", false)]
    [InlineData("https://www.scantool.net.evil.example/a.zip", false)]
    [InlineData("http://www.scantool.net/a.zip", false)]
    public void Firmware_url_requires_https_scantool_default_port(string url, bool ok) =>
        Assert.Equal(ok, FirmwareFeed.IsOfficialUrl(url));

    [Fact]
    public void Zip_entry_name_cannot_climb_out()
    {
        Assert.Equal("changelog.txt", FirmwareDownloader.SafeEntryName("../../changelog.txt"));
        Assert.Equal("changelog.txt", FirmwareDownloader.SafeEntryName("..\\..\\changelog.txt"));
        Assert.Null(FirmwareDownloader.SafeEntryName(".."));
        Assert.Null(OfficialUpdater.ConfinedFile(Path.GetTempPath(), "..\\..\\payload.exe"));
    }

    [Fact]
    public async Task Firmware_redirect_off_scantool_is_refused_before_saving()
    {
        var root = Path.Combine(Path.GetTempPath(), "fwred-" + Guid.NewGuid().ToString("N"));
        var h = new RedirectHandler();
        var fw = StnFirmware.Parse("STN2231 v5.13.0")!;
        var rel = new FirmwareRelease(2231, "EX", new Version(5, 13, 1), DateTime.UtcNow, "https://www.scantool.net/a/obdlink.zip", "", Array.Empty<string>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => FirmwareDownloader.DownloadAsync(new HttpClient(h), root, fw, rel));
        Assert.Contains("evil.example", ex.Message);
        Assert.Single(h.Seen);
        Assert.False(Directory.Exists(root) && Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void State_changing_commands_do_not_hunt_another_bus()
    {
        var src = Src("ObdLink.App/MainViewModel.cs");
        Assert.Contains("await OnBus(m, () => new UdsClient(Ch, (ushort)m.Def.TxId).ClearDtcsAsync(), allowFallback: false);", src);
        Assert.Contains("await OnBus(m, () => AsBuilt.WriteBlockAsync(", src);
        Assert.Contains("await OnBus(row, () => AsBuilt.RestoreAsync(f), allowFallback: false);", src);
        Assert.Equal(3, Count(src, "allowFallback: false"));
    }

    private static int Count(string hay, string needle)
    {
        int n = 0, i = 0;
        while ((i = hay.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    private sealed class RedirectHandler : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen.Add(request.RequestUri!);
            if (Seen.Count == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://evil.example/x.zip") } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("nope")) });
        }
    }
}
