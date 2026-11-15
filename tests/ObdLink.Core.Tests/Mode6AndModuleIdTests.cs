using ObdLink.Core.Obd;
using ObdLink.Core.Vehicles;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.9.0 Phase 1: Mode 6 test results and the module identification grid.</summary>
public class Mode6Tests
{
    [Fact]
    public void Parses_records_and_decides_pass_on_raw_limits()
    {
        var r = Mode6.Parse(0x7E8, Convert.FromHexString("46" + "01871000" + "1F00000190" + "21812400030000" + "0002"));
        Assert.Equal(2, r.Count);
        Assert.True(r[0].Pass);
        Assert.Equal("31 ms", r[0].Show(r[0].Value));
        Assert.Equal("O2 sensor bank 1 sensor 1", r[0].MidName);
        Assert.Equal("Manufacturer test 0x87", r[0].TidName);
        Assert.False(r[1].Pass);
        Assert.Contains("FAIL", r[1].Line);
    }

    [Fact]
    public void Signed_uasid_reads_two_s_complement()
    {
        // UASID 0x8B (signed volts, 0.001): value -0.5 V (FE0C), limits -1 V .. +1 V
        var r = Mode6.Parse(0x7E8, Convert.FromHexString("46" + "21" + "90" + "8B" + "FE0C" + "FC18" + "03E8")).Single();
        Assert.Equal(-500, r.Value);
        Assert.True(r.Pass);
        Assert.Equal("-0.5 V", r.Show(r.Value));
    }

    [Fact]
    public void Unknown_uasid_is_shown_raw_but_still_judged()
    {
        var r = Mode6.Parse(0x7E8, Convert.FromHexString("46" + "A2" + "0B" + "7A" + "0005" + "0000" + "0004")).Single();
        Assert.Null(r.Uas);
        Assert.Equal("5 (UASID 0x7A)", r.Show(r.Value));
        Assert.False(r.Pass);
        Assert.Equal("Misfire cylinder 1", r.MidName);
        Assert.StartsWith("Misfire count", r.TidName);
    }

    [Theory]
    [InlineData(0x05, "O2 sensor bank 2 sensor 1")]
    [InlineData(0x21, "Catalyst bank 1")]
    [InlineData(0x35, "Variable valve timing bank 1")]
    [InlineData(0x3A, "EVAP leak 0.090\"")]
    [InlineData(0x82, "Fuel system bank 2")]
    [InlineData(0x41, "O2 sensor heater bank 1 sensor 1")]
    public void Mid_names_match_j1979(byte mid, string name) => Assert.Equal(name, Mode6.MidName(mid));

    [Fact]
    public void Temperature_uasid_has_offset() => Assert.Equal("20 °C", new Mode6Result(0x7E8, 0x21, 1, 0x16, 600, 0, 0).Show(600));

    [Fact]
    public void Short_tail_is_ignored() => Assert.Single(Mode6.Parse(0x7E8, Convert.FromHexString("46" + "01871000" + "1F00000190" + "0188")));

    [Fact]
    public async Task Reads_all_supported_mids_from_simulator_with_only_service_06()
    {
        var (sim, _, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.Sent.Clear();
        var res = await Mode6.ReadAllAsync(ch);
        Assert.Equal(4, res.Count);
        Assert.Equal(new byte[] { 0x01, 0x01, 0x21, 0x21 }, res.Select(r => r.Mid));
        Assert.Single(res, r => !r.Pass);
        Assert.All(sim.Sent, x => Assert.StartsWith("06", x.Request));
        var report = Mode6.Report(res);
        Assert.Contains("4 test(s), 1 FAIL", report);
        Assert.Contains("[ECU 0x7E8] Catalyst bank 1 (MID 21)", report);
        Assert.Contains("0.25 :1", report);
    }
}

public class ModuleIdTests
{
    private static VehicleProfile Profile() => new()
    {
        Vin = "1FTDEMX95HF000001",
        Modules =
        {
            new("PCM", "Powertrain control", "HS", 0x7E0, 0x7E8, true, new() { ["F188"] = "HL3A-14C204-ABC", ["F18C"] = "SERIAL1" }, true),
            new("ACM", "Audio, \"front\"", "MS", 0x727, 0x72F, true, new(), false),
            new("BCM", "Body control", "HS", 0x726, 0x72E, true, new() { ["F111"] = "HU5T-14F642-AA" }, true),
        },
    };

    [Fact]
    public void Rows_sorted_by_bus_then_address()
    {
        var rows = ModuleIds.FromProfile(Profile());
        Assert.Equal(new[] { "726", "7E0", "727" }, rows.Select(r => r.Address));
        Assert.Equal("HL3A-14C204-ABC", rows[1].Software);
        Assert.Equal("yes", rows[1].AsBuilt);
    }

    [Fact]
    public void Csv_quotes_and_redacts_serials()
    {
        var csv = ModuleIds.Csv(ModuleIds.FromProfile(Profile()), redactSerials: true);
        Assert.Contains("REDACTED", csv);
        Assert.DoesNotContain("SERIAL1", csv);
        Assert.Contains("\"Audio, \"\"front\"\"\"", csv);
        Assert.Contains("SERIAL1", ModuleIds.Csv(ModuleIds.FromProfile(Profile()), redactSerials: false));
    }
}
