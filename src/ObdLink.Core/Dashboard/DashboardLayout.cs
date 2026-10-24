using ObdLink.Core.Obd;

namespace ObdLink.Core.Dashboard;

/// <summary>How one dashboard item is drawn. Tpms = the 4-tire top-view widget.</summary>
public enum DisplayKind { Dial, Bar, Tile, Lamp, Tpms }
public enum ItemSize { Small, Large }

/// <summary>One item on a saved dashboard layout.</summary>
public sealed class DashItem
{
    public string Key { get; set; } = "";
    public DisplayKind Display { get; set; } = DisplayKind.Bar;
    public ItemSize Size { get; set; } = ItemSize.Small;
    public DashItem Clone() => new() { Key = Key, Display = Display, Size = Size };
}

public sealed class DashboardLayout
{
    public string Name { get; set; } = "Default";
    public List<DashItem> Items { get; set; } = new();
    public DashboardLayout Clone(string? name = null) => new() { Name = name ?? Name, Items = Items.Select(i => i.Clone()).ToList() };

    public const string DefaultName = "Default";
    /// <summary>The v0.2.1 default: RPM + speed dials, gear tile, TPMS widget, then the usual bars.</summary>
    public static DashboardLayout Default() => new()
    {
        Name = DefaultName,
        Items = new List<DashItem>
        {
            new() { Key = "rpm", Display = DisplayKind.Dial, Size = ItemSize.Large },
            new() { Key = "speed", Display = DisplayKind.Dial, Size = ItemSize.Large },
            new() { Key = "gear", Display = DisplayKind.Tile },
            new() { Key = TpmsKeys.Widget, Display = DisplayKind.Tpms },
            new() { Key = "coolant", Display = DisplayKind.Bar }, new() { Key = "iat", Display = DisplayKind.Bar },
            new() { Key = "load", Display = DisplayKind.Bar }, new() { Key = "throttle", Display = DisplayKind.Bar },
            new() { Key = "fuel", Display = DisplayKind.Bar }, new() { Key = "tft", Display = DisplayKind.Bar },
            new() { Key = "atrv", Display = DisplayKind.Bar }, new() { Key = "maf", Display = DisplayKind.Tile },
            new() { Key = "stft1", Display = DisplayKind.Tile }, new() { Key = "ltft1", Display = DisplayKind.Tile },
        },
    };

    /// <summary>Builds a layout from a v0.2.0 plain gauge list (dials for RPM/speed, bars for the rest).</summary>
    public static DashboardLayout FromLegacy(IEnumerable<string> keys) => new()
    {
        Name = DefaultName,
        Items = keys.Distinct().Select(k => new DashItem { Key = k, Display = k is "rpm" or "speed" ? DisplayKind.Dial : DisplayKind.Bar, Size = k is "rpm" or "speed" ? ItemSize.Large : ItemSize.Small }).ToList(),
    };

    public bool Move(int index, int delta)
    {
        var j = index + delta;
        if (index < 0 || index >= Items.Count || j < 0 || j >= Items.Count) return false;
        (Items[index], Items[j]) = (Items[j], Items[index]); return true;
    }
    public bool MoveTo(int from, int to)
    {
        if (from < 0 || from >= Items.Count || to < 0 || to >= Items.Count || from == to) return false;
        var it = Items[from]; Items.RemoveAt(from); Items.Insert(to, it); return true;
    }
    public bool Add(string key, DisplayKind display)
    {
        if (Items.Any(i => i.Key == key)) return false;
        Items.Add(new DashItem { Key = key, Display = display }); return true;
    }
    public bool Remove(string key) => Items.RemoveAll(i => i.Key == key) > 0;
}

public static class TpmsKeys
{
    public const string Widget = "tpms";
    public static readonly string[] Tires = { "tpms_lf", "tpms_rf", "tpms_rr", "tpms_lr" };
    /// <summary>v0.3.0: the spare (guessed DID 2817) was removed. Kept here so old saved layouts / user-edited PID tables never show or poll it.</summary>
    public static readonly string[] Retired = { "tpms_spare" };
}

/// <summary>Everything that can be put on the dashboard.</summary>
public sealed record CatalogEntry(string Key, string Name, string Source, DisplayKind[] Displays, bool Verified = true)
{
    public DisplayKind DefaultDisplay => Displays[0];
    public string Label => $"{Name}  [{Source}]" + (Verified ? "" : "  UNVERIFIED");
}

public static class DashCatalog
{
    private static readonly DisplayKind[] Numeric = { DisplayKind.Bar, DisplayKind.Dial, DisplayKind.Tile, DisplayKind.Lamp };

    /// <summary>Mode 01 PIDs (only the supported ones when <paramref name="supported"/> is known), Ford DIDs, TPMS, gear, ATRV, MIL/DTC lamps.</summary>
    public static List<CatalogEntry> Build(EnhancedPidTable enhanced, ISet<byte>? supported = null)
    {
        var list = new List<CatalogEntry>
        {
            new("gear", "Transmission gear (Ford DID or estimate)", "Mode 22 / estimate", new[] { DisplayKind.Tile }, false),
            new(TpmsKeys.Widget, "Tire pressures (top view, 4 tires)", "BCM 726 Mode 22", new[] { DisplayKind.Tpms }, false),
            new("atrv", "Battery voltage (adapter ATRV)", "Adapter", Numeric),
            new("mil", "Check engine lamp (MIL)", "Mode 01 PID 01", new[] { DisplayKind.Lamp, DisplayKind.Tile }),
            new("dtcCount", "Stored DTC count", "Mode 01 PID 01", new[] { DisplayKind.Tile, DisplayKind.Lamp }),
        };
        foreach (var d in J1979.All.Where(d => d.Key != "mil" && (supported is null || supported.Contains(d.Pid))))
        {
            var disp = d.Kind == GaugeKind.Circular ? new[] { DisplayKind.Dial, DisplayKind.Bar, DisplayKind.Tile, DisplayKind.Lamp } : d.Kind == GaugeKind.Tile ? new[] { DisplayKind.Tile, DisplayKind.Bar, DisplayKind.Dial, DisplayKind.Lamp } : Numeric;
            list.Add(new(d.Key, d.Name, $"Mode 01 PID {d.Pid:X2}", disp));
        }
        foreach (var e in enhanced.Pids.Where(e => e.Key != "gear" && !TpmsKeys.Retired.Contains(e.Key) && list.All(x => x.Key != e.Key)))
            list.Add(new(e.Key, e.Name, $"Mode 22 {e.Header} DID {e.Did}", e.Map != null ? new[] { DisplayKind.Tile } : Numeric, e.Verified));
        return list;
    }

    /// <summary>
    /// Keys the engine must poll for a layout: only displayed items. The TPMS widget expands to its tire DIDs; "gear" adds the gear DID
    /// plus RPM and speed for the estimate. "mil"/"dtcCount" both come from PID 01, which is always polled for the lamp row.
    /// </summary>
    public static HashSet<string> PollKeys(DashboardLayout layout, bool estimateGear)
    {
        var keys = new HashSet<string> { "mil" };
        foreach (var it in layout.Items)
        {
            if (it.Key == TpmsKeys.Widget) foreach (var t in TpmsKeys.Tires) keys.Add(t);
            else if (it.Key == "gear") { keys.Add("gear"); if (estimateGear) { keys.Add("rpm"); keys.Add("speed"); } }
            else if (it.Key == "dtcCount" || TpmsKeys.Retired.Contains(it.Key)) { }
            else keys.Add(it.Key);
        }
        return keys;
    }
}
