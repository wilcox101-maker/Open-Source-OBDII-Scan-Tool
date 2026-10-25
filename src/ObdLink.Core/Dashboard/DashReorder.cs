namespace ObdLink.Core.Dashboard;

/// <summary>
/// Reorder rules for the dashboard wrap layout. The WPF view calls these so Customize-off,
/// drag-drop, and Up/Down can be tested without a window.
/// </summary>
public static class DashReorder
{
    /// <summary>Top-left or center of one card, in the same coordinates for every card.</summary>
    public readonly record struct Slot(double X, double Y);

    /// <summary>Moves one place when Customize is on. Returns the new index, or -1 when the layout stays put.</summary>
    public static int Move(DashboardLayout layout, int from, int delta, bool customizing)
    {
        if (!customizing || layout is null) return -1;
        return layout.MoveBy(from, delta);
    }

    /// <summary>Drops the card <paramref name="srcKey"/> onto <paramref name="dstKey"/>. False when nothing should change.</summary>
    public static bool Drop(DashboardLayout layout, string? srcKey, string? dstKey, bool customizing)
    {
        if (!customizing || layout is null || string.IsNullOrEmpty(srcKey) || string.IsNullOrEmpty(dstKey) || srcKey == dstKey) return false;
        int a = layout.Items.FindIndex(i => i.Key == srcKey);
        int b = layout.Items.FindIndex(i => i.Key == dstKey);
        return a >= 0 && layout.MoveTo(a, b);
    }

    public static bool IsMeasured(IReadOnlyList<Slot> slots)
    {
        if (slots is null || slots.Count <= 1) return false;
        double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
        foreach (var s in slots)
        {
            if (s.X < minX) minX = s.X;
            if (s.X > maxX) maxX = s.X;
            if (s.Y < minY) minY = s.Y;
            if (s.Y > maxY) maxY = s.Y;
        }
        return maxX - minX > 1 || maxY - minY > 1;
    }

    /// <summary>How many wrap rows the measured slots form. Unmeasured slots count as one row. Empty is zero.</summary>
    public static int RowCount(IReadOnlyList<Slot> slots)
    {
        if (slots is null || slots.Count == 0) return 0;
        if (!IsMeasured(slots)) return 1;
        return slots.Select(s => RowKey(s.Y)).Distinct().Count();
    }

    /// <summary>
    /// Places to move so the card changes row. Positive is down. Zero means it is already at that end
    /// of a multi-row layout. A single row steps one place. Unmeasured layouts use <paramref name="unmeasuredStride"/>.
    /// </summary>
    public static int VerticalDelta(IReadOnlyList<Slot> slots, int index, int direction, int unmeasuredStride)
    {
        if (slots is null || direction == 0 || index < 0 || index >= slots.Count) return 0;
        int sign = Math.Sign(direction);
        if (!IsMeasured(slots)) return sign * Math.Max(1, unmeasuredStride);
        int delta = RowDelta(slots, index, sign);
        if (delta == 0 && RowCount(slots) < 2) return sign;
        return delta;
    }

    /// <summary>Index of the slot closest to the drop point, or -1 when there are no slots.</summary>
    public static int NearestIndex(IReadOnlyList<Slot> slots, double x, double y)
    {
        if (slots is null || slots.Count == 0) return -1;
        int best = 0;
        double bestD = double.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            double dx = slots[i].X - x, dy = slots[i].Y - y, d = dx * dx + dy * dy;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    private static int RowKey(double y) => (int)Math.Round(y / 8.0);

    private static int RowDelta(IReadOnlyList<Slot> slots, int index, int sign)
    {
        var rows = slots.Select((s, i) => (s, i))
            .GroupBy(t => RowKey(t.s.Y))
            .OrderBy(g => g.Key)
            .Select(g => g.Select(t => t.i).OrderBy(i => slots[i].X).ToList())
            .ToList();
        int r = rows.FindIndex(row => row.Contains(index));
        if (r < 0) return 0;
        int nr = r + sign;
        if (nr < 0 || nr >= rows.Count) return 0;
        double x = slots[index].X;
        int best = rows[nr][0];
        foreach (var i in rows[nr])
            if (Math.Abs(slots[i].X - x) < Math.Abs(slots[best].X - x)) best = i;
        return best - index;
    }
}
