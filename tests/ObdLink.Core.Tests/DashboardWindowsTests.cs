using ObdLink.Core.Dashboard;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.20.0: three dashboard pop-out windows, write cards, layout files.</summary>
public class DashboardWindowsTests
{
    private static string TempSettings(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "dash3-" + Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Old_single_window_settings_become_window_1_and_windows_2_3_get_their_own_layouts()
    {
        var path = TempSettings("""{"Dashboard":{"ActiveLayout":"Towing","Background":"WoodGrain","ArrangeWidth":900,"WindowPlacementSaved":true,"WindowLeft":10,"WindowTop":20,"WindowWidth":1200,"WindowHeight":800,"Layouts":[{"Name":"Default","Items":[{"Key":"rpm"}]},{"Name":"Towing","Items":[{"Key":"tft"}]}]}}""");
        try
        {
            var s = AppSettings.Load(path);
            var d = s.Dashboard;
            Assert.Equal(new[] { 1, 2, 3 }, d.Windows.Select(w => w.Slot));
            var w1 = d.Window(1);
            Assert.Equal("Towing", w1.Layout);
            Assert.Equal(DashboardBackgrounds.WoodGrain, w1.Background);
            Assert.Equal(900, w1.ArrangeWidth);
            Assert.True(w1.WindowPlacementSaved);
            Assert.Equal(1200, w1.WindowWidth);
            Assert.Equal("tft", d.LayoutFor(1).Items[0].Key);
            Assert.Equal("Dashboard 2", d.Window(2).Layout);
            Assert.Equal("Dashboard 3", d.Window(3).Layout);
            Assert.Equal(DashboardLayout.Default().Items.Count, d.LayoutFor(2).Items.Count);
            Assert.NotSame(d.LayoutFor(2), d.LayoutFor(3));

            d.SetLayout(2, "Towing");
            d.SetLayout(1, "Default");
            Assert.Equal("Default", d.ActiveLayout); // window 1 is the legacy active layout
            s.Save(path);
            var r = AppSettings.Load(path).Dashboard;
            Assert.Equal("Default", r.Window(1).Layout);
            Assert.Equal("Towing", r.Window(2).Layout);
            Assert.Equal("Default", r.Current().Name);
            Assert.Throws<ArgumentException>(() => r.SetLayout(3, "nope"));
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void Broken_window_entries_are_repaired()
    {
        var path = TempSettings("""{"Dashboard":{"Windows":[{"Slot":2,"Layout":"gone","Background":"Custom","ArrangeWidth":-5},{"Slot":2,"Layout":"x"},{"Slot":9}],"Layouts":[{"Name":"Default","Items":[{"Key":"rpm"}]}]}}""");
        try
        {
            var d = AppSettings.Load(path).Dashboard;
            Assert.Equal(3, d.Windows.Count);
            var w2 = d.Window(2);
            Assert.Equal("Default", w2.Layout); // missing layout -> first layout
            Assert.Equal(DashboardBackgrounds.None, w2.Background); // Custom without a file
            Assert.Equal(0, w2.ArrangeWidth);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void Write_cards_validate_and_are_never_polled()
    {
        var l = DashboardLayout.Default();
        var key = l.AddAction(new DashAction { Kind = DashActionKind.Routine, Module = "7E0", Id = "1234", Label = "Self test" });
        Assert.True(DashActions.IsActionKey(key));
        Assert.Equal(DisplayKind.Action, l.Items[^1].Display);
        Assert.DoesNotContain(key, DashCatalog.PollKeys(l, true));
        Assert.Equal("Self test", l.Clone().Items[^1].Action!.Label);

        Assert.Throws<RoutineRefusedException>(() => l.AddAction(new DashAction { Kind = DashActionKind.Routine, Module = "737", Id = "E201" })); // airbag deployment
        Assert.Throws<RoutineRefusedException>(() => DashActions.Plan(new DashAction { Kind = DashActionKind.Routine, Module = "7E0", Id = "FF00" })); // flashing only via the flash engine
        Assert.Throws<FormatException>(() => DashActions.Plan(new DashAction { Kind = DashActionKind.WriteDid, Module = "726", Id = "DE01", Data = "00" })); // As-Built tab
        Assert.Throws<FormatException>(() => DashActions.Plan(new DashAction { Kind = DashActionKind.WriteDid, Module = "7E0", Id = "F190", Data = "00" })); // VIN
        Assert.Throws<FormatException>(() => DashActions.Plan(new DashAction { Kind = DashActionKind.OutputHold, Module = "726", Id = "D100", Data = "" }));
        Assert.Throws<FormatException>(() => DashActions.Plan(new DashAction { Kind = DashActionKind.Routine, Module = "18DA10F1", Id = "1234" }));
        Assert.Throws<FormatException>(() => DashActions.Plan(new DashAction { Kind = DashActionKind.Routine, Module = "7E0", Id = "12" }));

        var hold = DashActions.Plan(new DashAction { Kind = DashActionKind.OutputHold, Module = "0x726", Id = "D100", Data = "01" });
        Assert.Equal(0x726u, hold.TxId);
        Assert.Equal("2F D100 03 01", hold.RequestHex);
        Assert.Equal("HOLD", hold.Phrase);
        var write = DashActions.Plan(new DashAction { Kind = DashActionKind.WriteDid, Module = "7E0", Id = "0100", Data = "0A 0B" });
        var steps = DashActions.Steps(write, "My write", "PCM (7E0)", "0102");
        Assert.Contains("0102 → 0A0B", steps[0].Message);
        Assert.Equal("WRITE", steps[^1].TypedPhrase);
        Assert.Equal("RUN", DashActions.Steps(DashActions.Plan(new DashAction { Kind = DashActionKind.Routine, Module = "7E0", Id = "1234" }), "r", "PCM")[^1].TypedPhrase);
    }

    [Fact]
    public void Write_cards_survive_a_settings_round_trip_and_cards_without_an_action_are_dropped()
    {
        var path = TempSettings("""{"Dashboard":{"Layouts":[{"Name":"Default","Items":[{"Key":"rpm"},{"Key":"action:dead","Display":"Action"}]}]}}""");
        try
        {
            var s = AppSettings.Load(path);
            Assert.DoesNotContain(s.Dashboard.Current().Items, i => i.Key == "action:dead");
            s.Dashboard.Current().AddAction(new DashAction { Kind = DashActionKind.OutputHold, Module = "726", Id = "D100", Data = "01", Label = "Horn" });
            s.Save(path);
            Assert.Contains("\"OutputHold\"", File.ReadAllText(path));
            var card = AppSettings.Load(path).Dashboard.Current().Items.Single(i => i.Display == DisplayKind.Action);
            Assert.Equal("Horn", card.Action!.Label);
            Assert.Equal(DashActionKind.OutputHold, card.Action.Kind);
        }
        finally { Directory.Delete(Path.GetDirectoryName(path)!, true); }
    }

    [Fact]
    public void Layout_files_round_trip_and_reject_refused_cards()
    {
        var l = DashboardLayout.Default().Clone("Towing");
        l.AddAction(new DashAction { Kind = DashActionKind.Routine, Module = "7E0", Id = "1234", Label = "Test" });
        var json = DashboardLayoutFile.Export(l);
        Assert.Contains(DashboardLayoutFile.Format, json);
        var back = DashboardLayoutFile.Import(json, new[] { "Default", "Towing" });
        Assert.Equal("Towing (2)", back.Name);
        Assert.Equal(l.Items.Count, back.Items.Count);
        Assert.Equal("Test", back.Items[^1].Action!.Label);

        var evil = json.Replace("\"1234\"", "\"E201\"");
        var ex = Assert.Throws<FormatException>(() => DashboardLayoutFile.Import(evil, Array.Empty<string>()));
        Assert.Contains("not allowed", ex.Message);
        Assert.Throws<FormatException>(() => DashboardLayoutFile.Import("{\"Format\":\"something-else\"}", Array.Empty<string>()));
        Assert.Throws<FormatException>(() => DashboardLayoutFile.Import("not json", Array.Empty<string>()));
        var newer = json.Replace("\"Version\": 1", "\"Version\": 99");
        Assert.Throws<FormatException>(() => DashboardLayoutFile.Import(newer, Array.Empty<string>()));

        var dup = DashboardLayoutFile.Import("""{"Format":"obdlink-dashboard-layout","Version":1,"Layout":{"Name":"","Items":[{"Key":"rpm"},{"Key":"rpm"},{"Key":"tpms_spare"},{"Key":"speed","Face":"weird"}]}}""", Array.Empty<string>());
        Assert.Equal("Imported", dup.Name);
        Assert.Equal(new[] { "rpm", "speed" }, dup.Items.Select(i => i.Key));
        Assert.Equal(GaugeFaces.Analog, dup.Items[1].Face);
    }
}
