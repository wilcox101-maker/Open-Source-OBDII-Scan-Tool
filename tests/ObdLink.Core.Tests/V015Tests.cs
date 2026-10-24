using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using ObdLink.Core.ConsoleCmds;
using ObdLink.Core.Diagnostics;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Storage;
using ObdLink.Core.Transport;
using ObdLink.Core.Vehicles;
using Xunit;

namespace ObdLink.Core.Tests;

public class V015Tests
{
    private static string RepoRoot([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));
    private static IEnumerable<string> SrcFiles() => Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static async Task<(SimulatedAdapter, AdapterSession, ElmDiagChannel)> Setup()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, s, new ElmDiagChannel(s));
    }

    // ---------- file locations ----------
    [Fact]
    public void Static_scan_no_working_directory_and_every_dialog_has_initial_directory()
    {
        foreach (var f in SrcFiles())
        {
            var t = File.ReadAllText(f);
            Assert.False(t.Contains("Environment.CurrentDirectory") || t.Contains("Directory.GetCurrentDirectory") || t.Contains("SetCurrentDirectory"), $"{f} uses the working directory");
            foreach (Match m in Regex.Matches(t, @"new\s+(SaveFileDialog|OpenFileDialog)\s*\{.*?\};", RegexOptions.Singleline))
                Assert.True(m.Value.Contains("InitialDirectory") && !m.Value.Contains("InitialDirectory = \"\""), $"{Path.GetFileName(f)}: dialog without InitialDirectory: {m.Value}");
            // File writes must be built from a directory (Path.Combine/AppPaths/parameter), never a bare relative literal like File.WriteAllText("x.txt", ...)
            Assert.DoesNotMatch(new Regex(@"File\.(WriteAll\w*|Create|AppendAll\w*|Copy|Move)\(\s*""(?![A-Za-z]:\\|\\\\)"), t);
            Assert.DoesNotMatch(new Regex(@"new\s+(FileStream|StreamWriter)\(\s*""(?![A-Za-z]:\\|\\\\)"), t);
        }
    }

    [Fact]
    public void Dialog_count_matches_dir_aware_helpers()
    {
        var vm = File.ReadAllText(Path.Combine(RepoRoot(), "src", "ObdLink.App", "MainViewModel.cs"));
        foreach (Match m in Regex.Matches(vm, @"(?<![\w.])(OpenFile|SaveFile)\(""[^\n]*"))
            Assert.Contains("Paths.", m.Value);
    }

    [Fact]
    public void AppPaths_creates_exports_asbuilt_vehicles_subfolders()
    {
        var exe = Path.Combine(Path.GetTempPath(), "obd-v015-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(exe);
        try
        {
            var p = AppPaths.Resolve(exe, Path.Combine(exe, "legacy"));
            Assert.True(p.IsPortable);
            foreach (var d in new[] { p.ExportsDir, p.AsBuiltDir, p.VehiclesDir, p.LogsDir, p.CapturesDir, p.BackupsDir })
            {
                Assert.True(Directory.Exists(d), d);
                Assert.StartsWith(Path.Combine(exe, "Data"), d);
            }
            Assert.EndsWith("exports", p.ExportsDir); Assert.EndsWith("asbuilt", p.AsBuiltDir);
        }
        finally { Directory.Delete(exe, true); }
    }

    // ---------- console help ----------
    [Fact]
    public async Task Console_help_lists_commands_without_sending()
    {
        var (_, s, _) = await Setup();
        var sent = 0; s.Link.Traffic += (d, _) => { if (d == ">>") sent++; };
        var con = new RawConsoleService(s.Link);
        foreach (var h in new[] { "help", "?", "HELP" })
        {
            var r = await con.ExecuteAsync(h);
            Assert.Contains("ATSH7E0", r); Assert.Contains("19 02 FF", r); Assert.Contains("requestOutOfRange", r); Assert.Contains("2E", r);
        }
        Assert.Equal(0, sent);
    }

    [Fact]
    public void Guide_contains_every_console_example_blocked_sid_and_nrc()
    {
        var g = File.ReadAllText(Path.Combine(RepoRoot(), "docs", "GUIDE.md"));
        Assert.Contains("## Console command reference", g);
        foreach (var (c, _) in ConsoleHelp.Examples) Assert.Contains($"`{c}`", g);
        foreach (var (c, _) in ConsoleHelp.Blocked) Assert.Contains($"`{c}`", g);
        foreach (var (c, _) in ConsoleHelp.Nrcs) Assert.Contains($"`{c}`", g);
        foreach (var c in new[] { "ATZ", "ATI", "STI", "STDI", "ATRV", "ATSP6", "ATSH7E0", "STP33", "0100", "0902", "22F190", "1003", "3E00", "19 02 FF" }) Assert.Contains(c, g);
    }

    [Fact]
    public void Help_blocked_list_matches_console_risky_services()
    {
        foreach (var sid in new[] { "2E", "2F", "31", "11", "14", "27", "04", "3B" }) Assert.Contains(ConsoleHelp.Blocked, b => b.Sid == sid);
    }

    // ---------- LEDs ----------
    [Fact]
    public void Leds_hold_for_120ms_then_turn_off()
    {
        long now = 1000; var l = new ActivityLeds(() => now);
        Assert.Equal(new LedState(false, false, false), l.Sample());
        l.OnTraffic(">>", "0100");
        Assert.True(l.Sample().Tx); now += 100; Assert.True(l.Sample().Tx); now += 25; Assert.False(l.Sample().Tx);
        l.OnTraffic("<<", "7E8064100BE3EA813"); Assert.True(l.Sample().Rx); Assert.False(l.Sample().Error);
        l.OnTraffic("<<", "7E8037F2231"); Assert.True(l.Sample().Error);
        now += 700; Assert.False(l.Sample().Error);
        l.OnTraffic("!!", "TIMEOUT 0100"); Assert.True(l.Sample().Error);
    }

    [Theory]
    [InlineData("NO DATA", true)] [InlineData("?", true)] [InlineData("CAN ERROR", true)] [InlineData("7F 22 31", true)] [InlineData("7E8 03 7F 19 11", true)]
    [InlineData("7E8064100BE3EA813", false)] [InlineData("OK", false)] [InlineData("12.6V", false)] [InlineData("7E810146290", false)]
    public void Led_error_detection(string reply, bool err) => Assert.Equal(err, ActivityLeds.IsError(reply));

    [Fact]
    public async Task Elm_timeout_raises_error_traffic()
    {
        var link = new ElmLink(new MockTransport(_ => null!));
        var dirs = new List<string>(); link.Traffic += (d, _) => dirs.Add(d);
        await Assert.ThrowsAnyAsync<Exception>(() => link.SendAsync("0100", TimeSpan.FromMilliseconds(50)));
        Assert.Contains("!!", dirs);
    }

    // ---------- VIN decode ----------
    [Theory]
    [InlineData("1FTDEMX95HF000001", true, 2017)]
    [InlineData("1M8GDM9AXKP042788", true, 1989)]   // NHTSA example VIN (check digit X); digit in pos 7 => 1980-2009 cycle for this light-vehicle rule
    [InlineData("11111111111111111", true, 2001)]  // all-ones VIN: check digit 1 is valid
    public void Vin_check_digit_and_year(string vin, bool ok, int year)
    {
        var d = VinDecoder.Decode(vin);
        Assert.True(d.Valid); Assert.Equal(ok, d.CheckDigitOk); Assert.Equal(year, d.ModelYear);
    }

    [Fact]
    public void Vin_decode_ford_fields_and_bad_vins()
    {
        var d = VinDecoder.Decode(" 1ftdemx95hf000001 ");
        Assert.Equal("1FTDEMX95HF000001", d.Vin); Assert.Equal("1FT", d.Wmi); Assert.StartsWith("Ford", d.Manufacturer); Assert.Equal("USA", d.Country);
        Assert.Equal("F", d.PlantCode); Assert.Contains("Dearborn", d.Plant); Assert.Equal("000001", d.Serial);
        Assert.False(VinDecoder.Decode("1FTDEMX96HF000001").CheckDigitOk);
        Assert.False(VinDecoder.Decode("1FTDEMX95HF00000").Valid);
        Assert.False(VinDecoder.Decode("1FTDEMX95HF00000I").Valid); // I never used
    }

    // ---------- vPIC ----------
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> f) : HttpMessageHandler
    {
        public List<Uri> Seen { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { Seen.Add(r.RequestUri!); return Task.FromResult(f(r)); }
    }
    private const string VpicJson = """{"Count":1,"Message":"ok","Results":[{"Make":"FORD","Model":"F-150","ModelYear":"2017","Trim":"XLT","BodyClass":"Pickup","DisplacementL":"3.5","EngineModel":"","PlantCity":"DEARBORN","Series":"Not Applicable","ErrorCode":"0","Junk":"x"}]}""";

    [Fact]
    public async Task Vpic_parses_and_uses_documented_url()
    {
        var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(VpicJson) });
        var r = await new VpicClient(h).DecodeAsync("1FTDEMX95HF000001");
        Assert.NotNull(r);
        Assert.Equal("F-150", r!["Model"]); Assert.Equal("3.5", r["DisplacementL"]); Assert.False(r.ContainsKey("EngineModel")); Assert.False(r.ContainsKey("Series")); Assert.False(r.ContainsKey("Junk"));
        Assert.Equal("https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValuesExtended/1FTDEMX95HF000001?format=json", h.Seen.Single().ToString());
    }

    [Fact]
    public async Task Vpic_is_offline_safe()
    {
        Assert.Null(await new VpicClient(new FakeHandler(_ => throw new HttpRequestException("offline"))).DecodeAsync("1FTDEMX95HF000001"));
        Assert.Null(await new VpicClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))).DecodeAsync("1FTDEMX95HF000001"));
        Assert.Null(await new VpicClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>") })).DecodeAsync("1FTDEMX95HF000001"));
        var slow = new SlowHandler();
        Assert.Null(await new VpicClient(slow, TimeSpan.FromMilliseconds(100)).DecodeAsync("1FTDEMX95HF000001"));
    }
    private sealed class SlowHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) { await Task.Delay(5000, ct); return new(HttpStatusCode.OK); }
    }

    // ---------- Identify vehicle on the simulator ----------
    [Fact]
    public async Task Identify_vehicle_on_simulator_is_read_only_and_finds_modules()
    {
        var (sim, s, ch) = await Setup();
        var sids = new List<string>();
        s.Link.Traffic += (d, t) => { if (d == ">>" && Regex.IsMatch(t, "^[0-9A-F]{4,}$")) sids.Add(t[..2]); };
        var h = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(VpicJson) });
        var progress = new List<string>();
        var p = await new VehicleIdentifier(s, ch, new VpicClient(h)).RunAsync(new IdentifyOptions { OnlineDecode = true, ProbeTimeoutMs = 20 }, new SyncProgress(progress.Add));
        Assert.Equal(sim.Vin, p.Vin);
        Assert.True(p.Offline!.CheckDigitOk); Assert.Equal(2017, p.Offline.ModelYear);
        Assert.Equal("F-150", p.Online!["Model"]);
        Assert.Contains("F-150", p.DisplayName);
        var ids = p.Modules.Select(m => m.TxId).ToList();
        Assert.Contains(0x7E0u, ids); Assert.Contains(0x726u, ids); Assert.Contains(0x720u, ids);
        var pcm = p.Modules.Single(m => m.TxId == 0x7E0);
        Assert.Equal("PCM", pcm.Acronym); Assert.Equal("DEMO-14C204-AA", pcm.Dids["F188"]); Assert.True(pcm.HasAsBuiltDe00);
        if (s.Profile.Has(Adapters.AdapterCapabilities.MsCan))
            Assert.Contains(p.Modules, m => m.TxId == 0x727 && m.Bus == "MsCan");
        Assert.All(sids, sid => Assert.Contains(Convert.ToByte(sid, 16), new byte[] { 0x01, 0x09, 0x3E, 0x22 }));
        Assert.DoesNotContain(progress, m => m.Contains(sim.Vin)); // progress (shown in the log) never contains the full VIN
    }

    private sealed class SyncProgress(Action<string> a) : IProgress<string> { public void Report(string v) => a(v); }

    [Fact]
    public async Task Vehicle_profile_saves_with_masked_name_and_reloads()
    {
        var (_, s, ch) = await Setup();
        var p = await new VehicleIdentifier(s, ch).RunAsync(new IdentifyOptions { SweepMsCan = false, ProbeTimeoutMs = 20 });
        var dir = Path.Combine(Path.GetTempPath(), "obd-veh-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = p.Save(dir);
            Assert.Equal("XXXXXXXXXXX000001.json", Path.GetFileName(path));
            var back = VehicleProfile.LoadAll(dir).Single();
            Assert.Equal(p.Vin, back.Vin); Assert.Equal(p.Modules.Count, back.Modules.Count);
            Assert.Contains("XXXXXXXXXXX000001", back.Label);
            var defs = back.ToModuleDefinitions();
            Assert.Contains(defs, d => d.Acronym == "PCM" && d.TxId == 0x7E0 && d.RxId == 0x7E8);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData("1FTEW1EP5HFA00001", true)] [InlineData("1LNHL9DK5CG000001", true)] [InlineData("1MEFM50U01A000001", true)] [InlineData("3FADP4BJ5HM000001", true)]
    [InlineData("2FMDK3JC5BB000001", true)] [InlineData("WF0XXXGCDX5000001", true)] [InlineData("6FPAAAJGSW8000001", true)] [InlineData("5LMJJ2LT5HE000001", true)]
    [InlineData("1G1ZT51816F000001", false)] [InlineData("1C6RR7LT5ES000001", false)] [InlineData("JTDKB20U093000001", false)] [InlineData("2T1BURHE0JC000001", false)] [InlineData("1M8GDM9AXKP042788", false)]
    public void Ford_only_wmi_check(string vin, bool ford) => Assert.Equal(ford, VinDecoder.IsFordWmi(vin));

    [Fact]
    public async Task Identify_refuses_non_ford_vin_and_scans_nothing()
    {
        var sim = new SimulatedAdapter { Vin = "1G1ZT51806F000001" };
        var s = new AdapterSession(new ElmLink(sim.CreateTransport())); await s.InitializeAsync(); await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var sent = new List<string>(); s.Link.Traffic += (d, t) => { if (d == ">>") sent.Add(t); };
        var progress = new List<string>();
        var ex = await Assert.ThrowsAsync<NotFordVehicleException>(() => new VehicleIdentifier(s, new ElmDiagChannel(s)).RunAsync(new IdentifyOptions { ProbeTimeoutMs = 20 }, new SyncProgress(progress.Add)));
        Assert.Contains("Ford products only", ex.Message); Assert.Contains(progress, m => m.Contains("Ford products only"));
        Assert.DoesNotContain(sent, t => t.StartsWith("3E"));
    }

    [Fact]
    public void Identify_only_uses_read_services() => Assert.Equal(new byte[] { 0x09, 0x3E, 0x22 }, VehicleIdentifier.AllowedServices);
}

public class IconAssetTests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));
    [Fact]
    public void Icons_exist_and_are_wired()
    {
        foreach (var i in new[] { 1, 2, 3 })
        {
            Assert.True(File.Exists(Path.Combine(Root(), "assets", "icons", $"icon-{i}.png")));
            var ico = File.ReadAllBytes(Path.Combine(Root(), "assets", "icons", $"icon-{i}.ico"));
            Assert.Equal(new byte[] { 0, 0, 1, 0 }, ico[..4]); Assert.Equal(7, BitConverter.ToUInt16(ico, 4)); // 16,24,32,48,64,128,256
        }
        var csproj = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "ObdLink.App.csproj"));
        Assert.Contains("<ApplicationIcon>", csproj);
        Assert.Contains("HeaderIconUri", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainWindow.xaml")));
    }
}
