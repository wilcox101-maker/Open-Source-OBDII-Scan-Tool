using ObdLink.Core.AsBuilt;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logs;
using ObdLink.Core.Modules;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Replays Dale's real FORScan logs from his 2017 F-150 (VIN redacted, last 6 kept).</summary>
public class ForscanLogTests
{
    static string Fx(string n) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "forscan", n));
    static readonly string[] All = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "forscan"));
    static readonly string[] DtcReports = { "dtc-report-a.txt", "dtc-report-b.txt", "dtc-report-c.txt" };

    [Fact]
    public void Fixtures_have_no_full_vin_or_device_ids()
    {
        foreach (var f in All)
        {
            var t = File.ReadAllText(f);
            Assert.DoesNotMatch(@"1FT[A-Z0-9]{8}25268", t);
            Assert.DoesNotContain("223130114921", t);
            Assert.DoesNotContain("B5:E1:AC", t);
        }
    }

    [Fact]
    public void Scan_log_lists_20_modules_and_EX_adapter()
    {
        var log = Fx("connect-and-scan.txt");
        var found = ForscanLogParser.FoundModules(log);
        Assert.Equal(20, found.Count);
        Assert.Equal(new[] { "OBD2_PCM", "PCM", "APIM", "BdyCM", "FCIM", "FCDIM", "OCS", "TCCM", "ABS", "PBM", "RTM", "PDM", "DDM", "RCM", "PSCM", "ACM", "SCCM", "IPC", "GWM", "GPSM" }, found.ToArray());
        Assert.StartsWith("OBDLink EX r2.2.1 STN2231 v5.10.3", ForscanLogParser.AdapterId(log));
        Assert.Contains(ForscanLogParser.ParseEvents(log), e => e.Level == "ERR" && e.Message.Contains("ignition key is ON"));
    }

    [Fact]
    public void Every_logged_module_is_in_table_and_marked_verified()
    {
        foreach (var ac in ForscanLogParser.FoundModules(Fx("connect-and-scan.txt")).Distinct())
        {
            var a = FordF150_2017.ForscanAlias.GetValueOrDefault(ac, ac);
            var m = FordF150_2017.Modules.SingleOrDefault(x => x.Acronym == a);
            Assert.True(m is not null, $"missing {ac}");
            Assert.True(m!.SeenInLog, $"{ac} not marked verified-from-log");
        }
        Assert.DoesNotContain(FordF150_2017.Modules, m => m.Acronym is "TCM" or "PAM" or "HVAC" && m.SeenInLog);
    }

    [Fact]
    public void Config_report_parses_part_numbers()
    {
        var mods = ForscanLogParser.ParseConfigReport(Fx("config-report.txt"));
        var pcm = mods.Single(m => m.Acronym == "PCM");
        Assert.Equal("FL3A-12A650-CGA", pcm.Fields["Part number"]);
        Assert.Equal("XXXXXXXXXXXE25268", pcm.Fields["VIN"]);
        Assert.Contains(mods, m => m.Acronym == "BdyCM" && m.Fields["Strategy"] == "HU5T-14C184-AAJ");
    }

    [Fact]
    public void Dtc_reports_parse()
    {
        var d = DtcReports.SelectMany(n => ForscanLogParser.ParseDtcs(Fx(n))).ToList();
        Assert.Contains(d, x => x is { Module: "PCM", Code: "P0504", Symptom: "00", Status: "6C" });
        Assert.Contains(d, x => x is { Module: "BdyCM", Code: "U3006" } && x.Title.Contains("Input Power 'A'"));
        Assert.All(d, x => Assert.Matches("^[PCBU][0-9A-F]{4}$", x.Code));
    }

    [Fact]
    public void No_asbuilt_lines_in_logs_but_any_present_must_checksum()
    {
        // These logs contain no As-Built data. If a future export (.abt) adds any, every line must pass the checksum.
        var lines = All.SelectMany(f => ForscanLogParser.AsBuiltLines(File.ReadAllText(f))).ToList();
        Assert.All(lines, l => Assert.True(AsBuiltLine.Parse(l).ChecksumValid, l));
        Assert.Empty(lines);
    }

    [Fact]
    public async Task Simulator_built_from_logs_replays_scan_and_dtcs()
    {
        var sim = SimulatedAdapter.FromForscanLogs(Fx("connect-and-scan.txt"), DtcReports.Select(Fx).ToArray());
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        var ch = new ElmDiagChannel(s);
        var res = await new ModuleScanner(s, ch).ScanAsync(FordF150_2017.Modules);
        Assert.True(res.Single(r => r.Module.Acronym == "BCM").Responded);
        Assert.False(res.Single(r => r.Module.Acronym == "PAM").Responded);
        Assert.Contains("skipped", res.Single(r => r.Module.Acronym == "PBM").Detail);

        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var dtcs = await new UdsClient(ch, 0x726).ReadDtcsByStatusAsync();
        Assert.Contains(dtcs, x => x.Display == "U3006-16" && x.Status == 0x08);
        Assert.Contains(dtcs, x => x.Display == "B1438-03");
    }

    [Theory]  // examples from Livnitup's FORScan guide (docx Dale uploaded)
    [InlineData("726-16-02 0101 0101 0049")]
    [InlineData("740-13-01 005B")]
    public void Guide_checksum_examples_valid(string l) => Assert.True(AsBuiltLine.Parse(l).ChecksumValid);

    [Fact]
    public void Spreadsheet_complete_lines_checksum()
    {
        var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "community", "complete-lines.json")));
        var lines = json.RootElement.GetProperty("lines").EnumerateArray().Select(e => e.GetProperty("line").GetString()!).ToList();
        var bad = lines.Where(l => !AsBuiltLine.Parse(l).ChecksumValid).Distinct().ToList();
        Assert.True(lines.Count > 200);
        // 10 of the 249 rows (6 unique lines) fail. They're hand-edited forum examples where someone changed the data and didn't fix the checksum (FORScan recalculates it on write). Pin them so any new failure shows up.
        Assert.Equal(new[] { "727-08-01 749A CB55 84EF", "7D0-01-02 828B 0204 8BF6", "7D0-02-02 1212 8000 006F", "720-07-01 8409 8E50 E08A", "720-07-01 8409 8E50 204A", "7D0-02-02 1212 9000 00B1" }.OrderBy(x => x), bad.OrderBy(x => x));
    }
}
