using ObdLink.Core.Adapters;
using ObdLink.Core.Flash;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>KB-R1: no bus quieting when flashing the SYNC module (FORScan MFU: SYNC3 modules can be bricked with it).</summary>
public class FlashBusPolicyTests
{
    [Theory]
    [InlineData(0x7D0u, "APIM", false)]   // SYNC host
    [InlineData(0x7D0u, null, false)]
    [InlineData(0x7A0u, "SYNC", false)]   // any module the table calls SYNC
    [InlineData(0x726u, "BCM", true)]
    [InlineData(0x7E0u, "PCM", true)]
    [InlineData(0x760u, "ABS", true)]
    public void Quiet_bus_except_for_SYNC(uint tx, string? acronym, bool quiet) => Assert.Equal(quiet, FlashBusPolicy.QuietBus(tx, acronym));

    private static FlashPlan Plan(uint tx) => new()
    {
        TxId = tx,
        Image = FlashImage.FromSegments("t.vbf", new List<FlashSegment> { new(0x00010000u, new byte[16]) }),
    };

    [Fact]
    public void The_confirmation_matches_what_the_engine_will_do()
    {
        var plan = Plan(0x7D0);
        var off = string.Join("\n", FlashWarnings.For(plan, AdapterProfiles.ObdLinkEx, quietBus: false).Select(s => s.Message));
        Assert.Contains("SYNC module (APIM)", off);
        Assert.DoesNotContain("told to stop sending (and to stop logging faults)", off);
        var on = string.Join("\n", FlashWarnings.For(Plan(0x726), AdapterProfiles.ObdLinkEx).Select(s => s.Message));
        Assert.Contains("told to stop sending", on);
        Assert.StartsWith("https://", FlashBusPolicy.Source);
    }

    [Fact]
    public void The_Flash_tab_takes_the_policy_for_both_the_warning_and_the_engine()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var vm = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.App", "FlashViewModel.cs"));
        Assert.Contains("var quiet = FlashBusPolicy.QuietBus(m.Def.TxId, m.Def.Acronym);", vm);
        Assert.Contains("FlashWarnings.For(plan, _session()?.Profile, quiet)", vm);
        Assert.Contains("QuietBus = quiet,", vm);
        Assert.DoesNotContain("QuietBus = true", vm);
    }
}
