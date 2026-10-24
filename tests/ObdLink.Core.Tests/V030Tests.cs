using System.Text.RegularExpressions;
using ObdLink.Core.Dashboard;
using ObdLink.Core.Ui;
using Xunit;

namespace ObdLink.Core.Tests;

public class V030Tests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "src", "ObdLink.App"))) d = d.Parent;
        return d?.FullName ?? throw new DirectoryNotFoundException("repo root");
    }
    private static IEnumerable<(string File, string Text)> AppSources() =>
        Directory.EnumerateFiles(Path.Combine(Root(), "src", "ObdLink.App"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(f => (f, File.ReadAllText(f)));

    [Fact]
    public void No_ownerless_dialogs_outside_DialogService()
    {
        var bad = new List<string>();
        foreach (var (f, t) in AppSources())
        {
            if (Path.GetFileName(f) == "DialogService.cs") continue;
            foreach (var pat in new[] { @"MessageBox\.Show\(", @"\.ShowDialog\(\s*\)", @"Interaction\.InputBox", @"Process\.Start\(" })
                foreach (Match m in Regex.Matches(t, pat)) bad.Add($"{Path.GetFileName(f)}: {m.Value}");
        }
        Assert.True(bad.Count == 0, "Use DialogService instead: " + string.Join("; ", bad));
    }

    [Fact]
    public void Shortcuts_are_unique_and_documented()
    {
        Assert.Equal(Shortcuts.All.Count, Shortcuts.All.Select(s => s.Gesture).Distinct().Count());
        Assert.Equal(Shortcuts.All.Count, Shortcuts.All.Select(s => s.Id).Distinct().Count());
        var guide = File.ReadAllText(Path.Combine(Root(), "docs", "GUIDE.md"));
        foreach (var s in Shortcuts.All) Assert.Contains($"| {s.Gesture} |", guide);
        var xaml = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainWindow.xaml"));
        foreach (var id in new[] { "identify", "scan", "dtcs", "dtcsAll", "clearDtcs", "readAb", "readAllAb", "backup", "write", "open", "export", "capture", "exportLogs", "firmware", "shortcuts" })
            Assert.Contains($"InputGestureText=\"{Shortcuts.Gesture(id)}\"", xaml);
        Assert.True(Regex.Count(xaml, "Command=\"\\{Binding IdentifyVehicleCommand\\}\" Style") >= 2); // header + dashboard empty state
    }

    [Theory]
    [InlineData("Ctrl+D", false, "dtcs")]
    [InlineData("Ctrl+Shift+D", false, "dtcsAll")]
    [InlineData("Ctrl+Alt+C", false, "capture")]
    [InlineData("Alt+3", false, "page:3")]
    [InlineData("Ctrl+I", true, null)]
    [InlineData("Ctrl+W", true, null)]
    [InlineData("Ctrl+Shift+Delete", true, null)]
    [InlineData("Ctrl+F", true, "find")]
    [InlineData("Alt+F4", false, null)]
    [InlineData("Ctrl+Q", false, null)]
    public void Resolve_respects_text_focus(string g, bool inText, string? expected) => Assert.Equal(expected, Shortcuts.Resolve(g, inText));

    [Fact]
    public void Spare_tire_is_gone()
    {
        Assert.DoesNotContain("tpms_spare", TpmsKeys.Tires);
        var table = ObdLink.Core.Obd.EnhancedPidTable.Parse(ObdLink.Core.Obd.EnhancedPidTable.DefaultJson());
        Assert.DoesNotContain(table.Pids, p => p.Key == "tpms_spare" || p.Did == "2817");
        var layout = new DashboardLayout { Name = "x" }; layout.Items.Add(new DashItem { Key = TpmsKeys.Widget }); layout.Items.Add(new DashItem { Key = "tpms_spare" });
        Assert.DoesNotContain("tpms_spare", DashCatalog.PollKeys(layout, false));
    }
}
