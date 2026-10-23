using ObdLink.Core.AsBuilt;
using ObdLink.Core.Logs;
using ObdLink.Core.Modules;
using Xunit;
namespace ObdLink.Core.Tests;

/// <summary>Dale's second FORScan session (2026-10): connect/scan, DTC report, Mode 6, config report, and the vehicle .profile.</summary>
public class Session2Tests
{
    static string Fx(params string[] p) => File.ReadAllText(Path.Combine(new[] { AppContext.BaseDirectory, "Fixtures" }.Concat(p).ToArray()));
    static ForscanProfile Profile() => ForscanProfile.Parse(Fx("forscan", "s2-vehicle.redacted.profile"));
    static FordAbFile Ab() => FordAbFile.Parse(Fx("ab", "dale-f150-2017.redacted.ab"));

    [Fact]
    public void Scan_adapter_and_modules()
    {
        var log = Fx("forscan", "s2-connect-and-scan.txt");
        Assert.Equal("OBDLink EX r2.2.1 STN2231 v5.13.0 (ELM327 v1.4b)", ForscanLogParser.AdapterId(log));
        Assert.Equal(20, ForscanLogParser.FoundModules(log).Count);
    }

    [Fact]
    public void Dtc_report()
    {
        var d = ForscanLogParser.ParseDtcs(Fx("forscan", "s2-dtc-report.txt"));
        Assert.Contains(d, x => x.Module == "PCM" && x.Code == "P0722");
        Assert.Contains(d, x => x.Module == "PBM" && x.Code == "C2005");
        Assert.Contains(d, x => x.Module == "SCCM" && x.Code == "U2101");
        Assert.Contains(d, x => x.Module == "PSCM" && x.Code == "U3003");
    }

    [Fact]
    public void Profile_parses_and_is_redacted()
    {
        var p = Profile();
        Assert.Equal(19, p.Modules.Count);
        Assert.Equal("21E7", p.PcmKey);
        Assert.DoesNotContain("1FTEX1", Fx("forscan", "s2-vehicle.redacted.profile"));
        Assert.All(p.Modules, m => Assert.True(m.Get(3) is "" or "REDACTED"));
    }

    [Fact]
    public void Profile_maps_to_ab_addresses_and_identifies_pbm_ocs()
    {
        var map = Profile().MapToAb(Ab());
        Assert.Equal(19, map.Count);
        Assert.Equal(0x756, map["25E4"]); // PBM FL34-2C496-AK
        Assert.Equal(0x765, map["2DD6"]); // OCS HL3B-604A17-AA
        Assert.Equal(0x7E0, map["21E7"]);
        Assert.Equal(0x761, map["365C"]); // TCCM
        Assert.Equal(19, map.Values.Distinct().Count());
    }

    [Fact]
    public void Only_pcm_strategy_differs_from_factory()
    {
        var d = Assert.Single(Profile().CompareSoftware(Ab()));
        Assert.Equal((ushort)0x7E0, d.Module); Assert.Equal("F188", d.Did);
        Assert.Equal("HL3A-14C204-BDC", d.Factory); Assert.Equal("HL3A-14C204-BDB", d.Current);
    }

    [Fact]
    public void Module_table_has_pbm_and_ocs_addresses()
    {
        var m = FordF150_2017.Modules;
        Assert.Equal(0x756u, (uint)m.Single(x => x.Acronym == "PBM").TxId);
        Assert.Equal(0x765u, (uint)m.Single(x => x.Acronym == "OCS").TxId);
        Assert.DoesNotContain(m, x => x.Acronym.StartsWith("NODE"));
    }
}

public class Session2SimTests
{
    static string Fx(string n) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "forscan", n));

    [Fact]
    public async Task Simulator_replays_session2_scan_and_dtcs()
    {
        var sim = ObdLink.Core.Transport.SimulatedAdapter.FromForscanLogs(Fx("s2-connect-and-scan.txt"), Fx("s2-dtc-report.txt"));
        var s = new ObdLink.Core.Elm.AdapterSession(new ObdLink.Core.Elm.ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        var ch = new ObdLink.Core.IsoTp.ElmDiagChannel(s);
        var res = await new ModuleScanner(s, ch).ScanAsync(FordF150_2017.Modules);
        Assert.True(res.Single(r => r.Module.Acronym == "OCS").Responded);
        Assert.True(res.Single(r => r.Module.Acronym == "PBM").Responded);
        Assert.False(res.Single(r => r.Module.Acronym == "HVAC").Responded);
    }
}
