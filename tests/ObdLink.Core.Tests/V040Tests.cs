using ObdLink.Core.Dashboard;
using ObdLink.Core.Firmware;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Ui;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.4.0: scrolling/collapsing nav, no firmware check in DEMO, dashboard stack + backgrounds, TPMS verified, version 0.4.0.</summary>
public class V040Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    private const int NavTabs = 9;

    [Fact]
    public void Nav_keeps_labels_when_the_rail_fits()
    {
        var tall = NavRailLayout.Decide(800, NavTabs);
        Assert.False(tall.Compact); Assert.False(tall.Scrolls); Assert.Equal(NavRailLayout.LabeledWidth, tall.Width);
        var exact = NavRailLayout.Decide(NavTabs * NavRailLayout.LabeledStride + NavRailLayout.Pad, NavTabs);
        Assert.False(exact.Compact); Assert.False(exact.Scrolls);
    }

    [Fact]
    public void Nav_scrolls_with_labels_before_it_collapses()
    {
        var d = NavRailLayout.Decide(400, NavTabs);
        Assert.False(d.Compact); Assert.True(d.Scrolls); Assert.Equal(NavRailLayout.LabeledWidth, d.Width);
    }

    [Fact]
    public void Nav_collapses_to_icons_when_fewer_than_four_labeled_tabs_fit_and_restores_labels()
    {
        var tight = NavRailLayout.Decide(250, NavTabs);
        Assert.True(tight.Compact); Assert.True(tight.Scrolls); Assert.Equal(NavRailLayout.IconWidth, tight.Width);
        var room = NavRailLayout.Decide(400, NavTabs);
        Assert.False(room.Compact); Assert.Equal(NavRailLayout.LabeledWidth, room.Width);
        Assert.False(NavRailLayout.Decide(0, NavTabs).Compact);
        Assert.Equal(NavTabs, Shortcuts.Pages.Length);
        Assert.Equal("page:1", Shortcuts.Resolve("Alt+1", false));
        Assert.Equal("page:9", Shortcuts.Resolve("Alt+9", false));
    }

    [Fact]
    public void Nav_xaml_scrolls_and_hides_the_caption_when_compact()
    {
        var controls = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "Themes", "Controls.xaml"));
        Assert.Contains("x:Name=\"PART_NavScroll\"", controls);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", controls);
        Assert.Contains("IsItemsHost=\"True\"", controls);
        Assert.Contains("TargetName=\"Label\" Property=\"Visibility\" Value=\"Collapsed\"", controls);
        var window = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainWindow.xaml"));
        Assert.Contains("c:NavTabControl", window);
        Assert.DoesNotContain("-test", window);
        Assert.Contains("BackgroundId", window);
        Assert.Contains("FlowWidth", window);
        Assert.DoesNotContain("TIRE PRESSURES (UNVERIFIED)", window);
    }

    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    public void Demo_never_queries_the_firmware_feed(bool simulated, bool enabled, bool force)
        => Assert.False(FirmwareCheckPolicy.ShouldQuery(simulated, enabled, force));

    [Fact]
    public void Real_adapter_still_checks_when_enabled_or_asked()
    {
        Assert.True(FirmwareCheckPolicy.ShouldQuery(false, true, false));
        Assert.True(FirmwareCheckPolicy.ShouldQuery(false, false, true));
        Assert.False(FirmwareCheckPolicy.ShouldQuery(false, false, false));
        var src = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainViewModel.cs"));
        var start = src.IndexOf("private async Task RunFirmwareCheckAsync", StringComparison.Ordinal);
        var end = src.IndexOf("private async Task DownloadFirmwareAsync", StringComparison.Ordinal);
        var body = src[start..end];
        Assert.Contains("FirmwareCheckPolicy.ShouldQuery", body);
        Assert.True(body.IndexOf("ShouldQuery", StringComparison.Ordinal) < body.IndexOf("new FirmwareChecker", StringComparison.Ordinal));
    }

    [Fact]
    public void Saved_card_sizes_stay_until_a_card_cannot_fit()
    {
        var items = new (DisplayKind Display, ItemSize Size)[]
        {
            (DisplayKind.Bar, ItemSize.Small),
            (DisplayKind.Bar, ItemSize.Wide),
            (DisplayKind.Dial, ItemSize.Large),
            (DisplayKind.Tile, ItemSize.Small),
        };
        var wide = DashboardFlow.Plan(items, 900, arrangeWidth: 0);
        Assert.False(wide.Stacking);
        Assert.Equal(new[] { 300d, 620d, 290d, 180d }, wide.Widths);
        Assert.False(wide.ConstrainHost);

        var pinned = DashboardFlow.Plan(items, 1000, arrangeWidth: 720);
        Assert.False(pinned.Stacking); Assert.True(pinned.ConstrainHost); Assert.Equal(720, pinned.HostMaxWidth);
        Assert.Equal(620, pinned.Widths[1]);
        var arrangedWhileStacked = DashboardFlow.Plan(items, 1000, arrangeWidth: 400);
        Assert.False(arrangedWhileStacked.Stacking); Assert.False(arrangedWhileStacked.ConstrainHost);
        Assert.Equal(620, arrangedWhileStacked.Widths[1]);

        var narrowButFits = DashboardFlow.Plan(items, 640, arrangeWidth: 720);
        Assert.False(narrowButFits.Stacking); Assert.False(narrowButFits.ConstrainHost);
        Assert.Equal(300, narrowButFits.Widths[0]);
    }

    [Fact]
    public void Gauge_height_cards_stack_only_when_the_saved_layout_cannot_fit()
    {
        var items = new (DisplayKind Display, ItemSize Size)[]
        {
            (DisplayKind.Bar, ItemSize.Small),
            (DisplayKind.Bar, ItemSize.Medium),
            (DisplayKind.Bar, ItemSize.Wide),
            (DisplayKind.Tile, ItemSize.Small),
            (DisplayKind.Lamp, ItemSize.Small),
            (DisplayKind.Dial, ItemSize.Small),
            (DisplayKind.Tpms, ItemSize.Small),
        };
        Assert.True(DashboardFlow.IsGaugeHeight(DisplayKind.Bar, ItemSize.Wide));
        Assert.True(DashboardFlow.IsGaugeHeight(DisplayKind.Tile, ItemSize.Large));
        Assert.False(DashboardFlow.IsGaugeHeight(DisplayKind.Dial, ItemSize.Small));
        Assert.False(DashboardFlow.IsGaugeHeight(DisplayKind.Tpms, ItemSize.Small));

        var plan = DashboardFlow.Plan(items, 360, arrangeWidth: 800);
        Assert.True(plan.Stacking);
        var fit = 360 - DashboardFlow.CardMargin;
        Assert.Equal(fit, plan.Widths[0], 3);
        Assert.Equal(fit / 1.2, plan.Widths[1], 3);
        Assert.Equal(fit, plan.Widths[2], 3);
        Assert.Equal(fit, plan.Widths[3], 3);
        Assert.Equal(fit, plan.Widths[4], 3);
        Assert.Equal(230, plan.Widths[5]);
        Assert.Equal(300, plan.Widths[6]);
    }

    [Fact]
    public void Flow_does_not_change_widths_before_the_dashboard_is_measured()
    {
        var plan = DashboardFlow.Plan(new[] { (DisplayKind.Bar, ItemSize.Wide) }, 0, 500);
        Assert.False(plan.Stacking); Assert.Equal(620, plan.Widths[0]);
    }

    [Fact]
    public void Background_choice_persists_and_custom_images_stay_inside_data()
    {
        var path = Path.Combine(Path.GetTempPath(), "v040-" + Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{\"ReadOnlyTestMode\":false,\"Dashboard\":{\"Gauges\":[\"rpm\"]}}");
        var old = AppSettings.Load(path);
        Assert.True(old.ApplyStartupSafety());
        Assert.True(old.ReadOnlyTestMode);
        Assert.Equal(DashboardBackgrounds.None, old.Dashboard.Background);
        Assert.Null(old.Dashboard.BackgroundFile);
        Assert.Equal(0, old.Dashboard.ArrangeWidth);
        Assert.Equal(new[] { "rpm" }, old.Dashboard.Current().Items.Select(i => i.Key));

        old.Dashboard.Background = "wood";
        old.Dashboard.ArrangeWidth = 640;
        old.Dashboard.BackgroundFile = "..\\settings.json";
        old.Normalize();
        Assert.Equal(DashboardBackgrounds.None, old.Dashboard.Background);
        Assert.Null(old.Dashboard.BackgroundFile);
        Assert.Equal(640, old.Dashboard.ArrangeWidth);
        old.Save(path);
        var back = AppSettings.Load(path);
        Assert.Equal(DashboardBackgrounds.None, back.Dashboard.Background);
        Assert.Equal(640, back.Dashboard.ArrangeWidth);

        var data = Path.Combine(Path.GetTempPath(), "v040-img-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(data, "picked.png");
        Directory.CreateDirectory(data);
        File.WriteAllBytes(src, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFCSjAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="));
        var name = DashboardBackgroundFiles.Import(data, src);
        Assert.Equal("custom.png", name);
        var full = DashboardBackgroundFiles.FullPath(data, name);
        Assert.NotNull(full);
        Assert.StartsWith(Path.GetFullPath(Path.Combine(data, "backgrounds")), full);
        Assert.Null(DashboardBackgroundFiles.FullPath(data, "..\\settings.json"));
        Assert.Null(DashboardBackgroundFiles.SanitizeFileName("C:\\outside.png"));
        var exe = Path.Combine(data, "nope.exe");
        File.WriteAllBytes(exe, File.ReadAllBytes(src));
        Assert.Null(DashboardBackgroundFiles.Import(data, exe));
        var big = Path.Combine(data, "big.png");
        using (var f = File.Create(big)) f.SetLength(DashboardBackgroundFiles.MaxBytes + 1);
        Assert.Null(DashboardBackgroundFiles.Import(data, big));

        back.Dashboard.Background = DashboardBackgrounds.Custom;
        back.Dashboard.BackgroundFile = name;
        back.Save(path);
        Assert.Equal("custom.png", AppSettings.Load(path).Dashboard.BackgroundFile);
        Assert.Contains(DashboardBackgrounds.Choices, c => c.Id == DashboardBackgrounds.SportsLeather && c.Name.Contains("leather"));
        Assert.Contains(DashboardBackgrounds.Choices, c => c.Id == DashboardBackgrounds.GlossBlack);
    }

    [Fact]
    public void Tpms_stock_dids_are_verified_and_oss_1e13_stays_unverified()
    {
        var dir = Path.Combine(Path.GetTempPath(), "v040-pids-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, EnhancedPidTable.FileName), """
            {"pids":[
              {"key":"tpms_lf","header":"726","did":"2813","bytes":2,"scale":0.333333,"offset":7.333333,"verified":false},
              {"key":"tpms_rf","header":"726","did":"9999","bytes":2,"verified":false},
              {"key":"oss","header":"7E0","did":"1E13","bytes":2,"scale":0.25,"verified":false}
            ]}
            """);
        var t = EnhancedPidTable.LoadOrCreate(dir).Table;
        Assert.True(t.Pids.Single(p => p.Key == "tpms_lf").Verified);
        Assert.False(t.Pids.Single(p => p.Key == "tpms_rf").Verified);
        var oss = t.Pids.Single(p => p.Key == "oss");
        Assert.Equal("1E13", oss.Did); Assert.False(oss.Verified);
        Assert.Contains(DashCatalog.Build(EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson())), c => c.Key == "oss" && !c.Verified);
        Assert.Contains(DashCatalog.Build(t), c => c.Key == "tpms" && !c.Verified);
        Assert.Contains(DashCatalog.Build(EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson())), c => c.Key == "tpms" && c.Verified);
        Directory.Delete(dir, true);
    }

    [Fact]
    public void Version_is_0_4_0_and_notes_record_windows_tested_v03()
    {
        var notes = File.ReadAllText(Path.Combine(Root(), "docs", "RELEASE-NOTES.md"));
        Assert.Contains("## v0.4.0", notes);
        Assert.DoesNotContain("v0.4.0-test", notes);
        Assert.Contains("Windows-tested", notes);
        Assert.Contains("2813", notes);
        Assert.Contains("1E13", notes);
        var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
        Assert.Contains("Windows-tested", readme);
        var guide = File.ReadAllText(Path.Combine(Root(), "docs", "GUIDE.md"));
        Assert.Contains("DEMO", guide);
        Assert.Contains("verified", guide, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2813-2816 (**unverified**", guide);
    }
}
