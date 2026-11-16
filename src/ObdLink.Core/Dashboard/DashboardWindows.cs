using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObdLink.Core.Dashboard;

/// <summary>
/// One dashboard pop-out window (slot 1-3): which saved layout it shows, its own background and arrange width, and where it was
/// on screen. The layouts themselves are shared (<see cref="Safety.DashboardOptions.Layouts"/>), so a layout can be shown in more
/// than one window.
/// </summary>
public sealed class DashboardWindowState
{
    public int Slot { get; set; }
    public string Layout { get; set; } = DashboardLayout.DefaultName;
    public string Background { get; set; } = DashboardBackgrounds.None;
    public string? BackgroundFile { get; set; }
    public double ArrangeWidth { get; set; }
    public bool WindowPlacementSaved { get; set; }
    public double WindowLeft { get; set; }
    public double WindowTop { get; set; }
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
}

public static class DashboardWindows
{
    public const int Count = 3;

    /// <summary>
    /// Makes sure there are exactly <see cref="Count"/> window states. Slot 1 is migrated from the pre-v0.20 single-window fields
    /// (active layout, background, arrange width, placement); afterwards window 1's layout stays equal to <c>ActiveLayout</c>. Slots 2 and 3 get their own copy of the default layout the first time.
    /// Every window points at an existing layout and has sane numbers.
    /// </summary>
    public static void Normalize(Safety.DashboardOptions d)
    {
        d.Windows ??= new();
        d.Windows.RemoveAll(w => w is null || w.Slot is < 1 or > Count);
        d.Windows = d.Windows.GroupBy(w => w.Slot).Select(g => g.First()).OrderBy(w => w.Slot).ToList();
        for (int slot = 1; slot <= Count; slot++)
        {
            if (d.Windows.Any(w => w.Slot == slot)) continue;
            DashboardWindowState w;
            if (slot == 1)
                w = new DashboardWindowState
                {
                    Slot = 1, Layout = d.ActiveLayout, Background = d.Background, BackgroundFile = d.BackgroundFile, ArrangeWidth = d.ArrangeWidth,
                    WindowPlacementSaved = d.WindowPlacementSaved, WindowLeft = d.WindowLeft, WindowTop = d.WindowTop,
                    WindowWidth = d.WindowWidth, WindowHeight = d.WindowHeight, WindowMaximized = d.WindowMaximized,
                };
            else
            {
                var name = $"Dashboard {slot}";
                if (d.Layouts.All(l => l.Name != name)) d.Layouts.Add(DashboardLayout.Default().Clone(name));
                w = new DashboardWindowState { Slot = slot, Layout = name };
            }
            d.Windows.Add(w);
        }
        d.Windows = d.Windows.OrderBy(w => w.Slot).ToList();
        foreach (var w in d.Windows)
        {
            if (d.Layouts.All(l => l.Name != w.Layout)) w.Layout = d.Layouts[0].Name;
            w.Background = DashboardBackgrounds.Normalize(w.Background);
            w.BackgroundFile = DashboardBackgroundFiles.SanitizeFileName(w.BackgroundFile);
            if (w.Background == DashboardBackgrounds.Custom && w.BackgroundFile is null) w.Background = DashboardBackgrounds.None;
            if (!double.IsFinite(w.ArrangeWidth) || w.ArrangeWidth < 0) w.ArrangeWidth = 0;
            if (!double.IsFinite(w.WindowLeft) || !double.IsFinite(w.WindowTop) || !double.IsFinite(w.WindowWidth) || !double.IsFinite(w.WindowHeight))
                w.WindowPlacementSaved = false;
        }
        // Window 1's layout IS the legacy ActiveLayout (older builds and Current() read it), so it wins over the copy here.
        if (d.Layouts.Any(l => l.Name == d.ActiveLayout)) d.Windows[0].Layout = d.ActiveLayout; else d.ActiveLayout = d.Windows[0].Layout;
    }
}

/// <summary>
/// A dashboard layout as a file, to keep, share or move between PCs. Import re-validates everything: unknown or retired keys are
/// dropped, faces normalized, and write cards checked with <see cref="DashActions.Plan"/> (an invalid card rejects the file, so
/// a shared layout can't smuggle in a refused routine).
/// </summary>
public static class DashboardLayoutFile
{
    public const string Format = "obdlink-dashboard-layout";
    public const int Version = 1;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    private sealed class Envelope
    {
        public string? Format { get; set; }
        public int Version { get; set; }
        public DashboardLayout? Layout { get; set; }
    }

    public static string Export(DashboardLayout layout) => JsonSerializer.Serialize(new Envelope { Format = Format, Version = Version, Layout = layout.Clone() }, Json);

    /// <summary>Parses and validates a layout file. The name is made unique against <paramref name="existingNames"/> ("Name (2)").</summary>
    public static DashboardLayout Import(string json, IEnumerable<string> existingNames)
    {
        Envelope? env;
        try { env = JsonSerializer.Deserialize<Envelope>(json, Json); }
        catch (JsonException ex) { throw new FormatException("Not a dashboard layout file: " + ex.Message); }
        if (env?.Format != Format) throw new FormatException($"Not a dashboard layout file (expected format '{Format}').");
        if (env.Version > Version) throw new FormatException($"Layout file version {env.Version} is newer than this app understands ({Version}).");
        var l = env.Layout ?? throw new FormatException("The file has no layout.");
        var items = new List<DashItem>();
        foreach (var it in l.Items ?? new())
        {
            if (it is null || string.IsNullOrWhiteSpace(it.Key) || TpmsKeys.Retired.Contains(it.Key) || items.Any(x => x.Key == it.Key)) continue;
            it.Face = GaugeFaces.Normalize(it.Face);
            if (DashActions.IsActionKey(it.Key) || it.Display == DisplayKind.Action || it.Action is not null)
            {
                if (it.Action is null) throw new FormatException($"Write card '{it.Key}' has no action.");
                try { DashActions.Plan(it.Action); }
                catch (Exception ex) when (ex is FormatException or Uds.RoutineRefusedException)
                { throw new FormatException($"Write card '{(it.Action.Label.Length > 0 ? it.Action.Label : it.Key)}' is not allowed: {ex.Message}"); }
                it.Key = DashActions.IsActionKey(it.Key) ? it.Key : DashActions.NewKey();
                it.Display = DisplayKind.Action;
            }
            items.Add(it);
        }
        var names = existingNames.ToHashSet();
        var baseName = string.IsNullOrWhiteSpace(l.Name) ? "Imported" : l.Name.Trim();
        var name = baseName;
        for (int n = 2; names.Contains(name); n++) name = $"{baseName} ({n})";
        return new DashboardLayout { Name = name, Items = items };
    }
}
