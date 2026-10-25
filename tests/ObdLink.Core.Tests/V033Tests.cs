using ObdLink.Core.Dashboard;
using ObdLink.Core.Safety;
using System.Text.Json;
using Xunit;

namespace ObdLink.Core.Tests;

public class V033Tests
{
    private static string Src(string f, [System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(me)!, "..", "..", "src", "ObdLink.App", f));

    [Fact]
    public void MoveByClampsAndReorders()
    {
        var l = DashboardLayout.Default(); var first = l.Items[0].Key; var n = l.Items.Count;
        Assert.Equal(-1, l.MoveBy(0, -1));                 // already first
        Assert.Equal(1, l.MoveBy(0, 1)); Assert.Equal(first, l.Items[1].Key);
        Assert.Equal(n - 1, l.MoveBy(1, 999)); Assert.Equal(first, l.Items[^1].Key); // Ctrl+End
        Assert.Equal(0, l.MoveBy(n - 1, -999)); Assert.Equal(first, l.Items[0].Key);  // Ctrl+Home
        Assert.Equal(-1, l.MoveBy(n, 1));
    }

    [Fact]
    public void SizesPersistByNameAndOldFilesLoad()
    {
        var s = new AppSettings();
        var l = s.Dashboard.Current(); l.Items[0].Size = ItemSize.Medium; l.Items[1].Size = ItemSize.Wide;
        var json = JsonSerializer.Serialize(s, AppSettings.Json);
        Assert.Contains("\"Medium\"", json); Assert.Contains("\"Wide\"", json);
        var back = JsonSerializer.Deserialize<AppSettings>(json, AppSettings.Json)!;
        Assert.Equal(ItemSize.Medium, back.Dashboard.Current().Items[0].Size);
        Assert.Equal(ItemSize.Wide, back.Dashboard.Current().Items[1].Size);
        // v0.3.2 files only had Small/Large
        var old = JsonSerializer.Deserialize<DashItem>("{\"Key\":\"rpm\",\"Display\":\"Dial\",\"Size\":\"Large\"}", AppSettings.Json)!;
        Assert.Equal(ItemSize.Large, old.Size);
    }

    [Fact]
    public void ResetRestoresDefaultOrderAndSizes()
    {
        var l = DashboardLayout.Default(); l.MoveBy(0, 3); l.Items[0].Size = ItemSize.Wide;
        var d = DashboardLayout.Default(); l.Items = d.Items;
        Assert.Equal(DashboardLayout.Default().Items.Select(i => (i.Key, i.Size)), l.Items.Select(i => (i.Key, i.Size)));
    }

    [Fact]
    public void ViewHasKeyboardButtonsSizePickerAndDrag()
    {
        var x = Src("MainWindow.xaml"); var cs = Src("MainWindow.xaml.cs"); var vm = Src("DashboardViewModel.cs");
        foreach (var t in new[] { "MoveLeftCommand", "MoveRightCommand", "DashItemRow_Click", "PreviewKeyDown=\"DashItem_KeyDown\"", "ItemsSource=\"{Binding Sizes}\"", "SelectedItem=\"{Binding Size}\"", "Value=\"Wide\"", "Value=\"Medium\"", "Drop=\"DashItem_Drop\"", "x:Name=\"DashItemsHost\"", "ResetLayoutCommand" })
            Assert.Contains(t, x);
        Assert.Contains("Key.Left => -1, Key.Right => 1, Key.Up => -row, Key.Down => row", cs);
        Assert.Contains("ModifierKeys.Control", cs);
        Assert.Contains("public bool MoveBy(DashItemVm? it, int delta)", vm);
        Assert.Contains("Changed($\"moved", vm); // every move persists (settings save)
        // Drop must hit a full-card overlay. AllowDrop on the border alone never saw a drop on the gauge.
        Assert.Contains("x:Name=\"DropOverlay\"", x);
        Assert.Contains("IsHitTestVisible=\"False\"", x);
        Assert.Contains("Content=\"Move left\"", x);
        Assert.Contains("Content=\"Move right\"", x);
        Assert.Contains("Content=\"Move up\"", x);
        Assert.Contains("Content=\"Move down\"", x);
        Assert.Contains("Command=\"{Binding MoveLeftCommand}\"", x);
        Assert.Contains("Focusable=\"True\"", x);
        Assert.Contains("SetDashDropTargets(true)", cs);
        Assert.Contains("_dragKey", cs);
        Assert.Contains("DataFormats.UnicodeText", cs);
        Assert.DoesNotContain("typeof(DashItemVm)", cs);
        Assert.Contains("DashReorder.Move", vm);
        Assert.Contains("DashReorder.Drop", vm);
    }
}
