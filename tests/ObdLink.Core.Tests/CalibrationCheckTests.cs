using ObdLink.Core.AsBuilt;
using ObdLink.Core.Logs;
using ObdLink.Core.Vehicles;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// W5 v1: installed module IDs vs the factory .AB record, offline. Differences only: the check must never say "newer" or
/// "update available" (Dale's truck: factory record BDC, truck BDB, reason UNVERIFIED), and it must make no network call.
/// </summary>
public class CalibrationCheckTests
{
    static string Fx(params string[] p) => File.ReadAllText(Path.Combine(new[] { AppContext.BaseDirectory, "Fixtures" }.Concat(p).ToArray()));
    static ForscanProfile Profile() => ForscanProfile.Parse(Fx("forscan", "s2-vehicle.redacted.profile"));
    static FordAbFile Ab() => FordAbFile.Parse(Fx("ab", "dale-f150-2017.redacted.ab"));

    [Fact]
    public void Dales_truck_shows_one_difference_PCM_strategy_BDB_vs_record_BDC_without_a_direction()
    {
        var findings = CalibrationCheck.Compare(Profile(), Ab());
        var diff = Assert.Single(findings, f => f.Status == CalibrationStatus.Differs);
        Assert.Equal(((ushort)0x7E0, "F188", "HL3A-14C204-BDB", "HL3A-14C204-BDC"), (diff.Module, diff.Did, diff.Installed, diff.Factory));
        Assert.EndsWith("installed software / strategy (F188) differs from the factory record (installed HL3A-14C204-BDB, record BDC).", diff.Line);
        Assert.True(findings.Count(f => f.Status == CalibrationStatus.Same) > 10);

        var report = CalibrationCheck.Report(findings);
        Assert.Contains("1 differ", report);
        Assert.Contains("doesn't show which version came later", report);
        foreach (var word in new[] { "newer calibration", "update available", "is newer", "is older", "outdated", "out of date" })
            Assert.DoesNotContain(word, report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Live_identification_is_compared_by_module_address_and_reports_missing_entries()
    {
        static ModuleIdentity M(uint tx, string acr, params (string, string)[] dids) => new(acr, acr, "HS", tx, tx + 8, true, dids.ToDictionary(d => d.Item1, d => d.Item2), false);
        var installed = new[]
        {
            M(0x7E0, "PCM", ("F188", "HL3A-14C204-BDB"), ("F113", "FL3A-12A650-CGA"), ("F18C", "123456")),
            M(0x726, "BCM", ("F113", "HU5T-14B476-DAJ")),
            M(0x799, "XYZ", ("F188", "AAAA-11111-AA")),
        };
        var f = CalibrationCheck.Compare(installed, Ab());
        Assert.Contains(f, x => x is { Module: 0x7E0, Did: "F188", Status: CalibrationStatus.Differs });
        Assert.Contains(f, x => x is { Module: 0x7E0, Did: "F113", Status: CalibrationStatus.Same });
        Assert.Contains(f, x => x is { Module: 0x726, Did: "F113", Status: CalibrationStatus.Same });
        Assert.Contains(f, x => x is { Module: 0x799, Status: CalibrationStatus.NotInFactoryRecord });
        Assert.Contains(f, x => x is { Module: 0x720, Status: CalibrationStatus.NotRead });   // IPC is in the record, wasn't read
        Assert.DoesNotContain(f, x => x.Did == "F18C");                                        // serials are never compared
    }

    [Theory]
    [InlineData("HL3A-14C204-BDC", "HL3A-14C204-BDB", "BDC")]
    [InlineData("HL3A-14C204-BDC", "JL3A-14C204-AA", "HL3A-14C204-BDC")]
    public void The_record_value_is_shortened_only_when_the_base_part_matches(string value, string other, string shown) =>
        Assert.Equal(shown, CalibrationCheck.Short(value, other));

    [Fact]
    public void The_check_is_offline_and_the_app_shows_it_with_the_factory_record()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.Core", "Vehicles", "CalibrationCheck.cs"));
        foreach (var net in new[] { "HttpClient", "WebClient", "System.Net", "Socket", "https://", "http://" })
            Assert.DoesNotContain(net, src);
        var main = File.ReadAllText(Path.Combine(dir.FullName, "src", "ObdLink.App", "MainViewModel.cs"));
        Assert.Contains("CalibrationCheck.Report(CalibrationCheck.Compare(p.Modules, factory))", main);
    }
}
