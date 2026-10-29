namespace ObdLink.Core.Dashboard;

public static class DashboardWindowPlacement
{
    public const double DefaultWidth = 1100;
    public const double DefaultHeight = 720;
    public const double MinWidth = 640;
    public const double MinHeight = 480;
    public const double MaxWidth = 4000;
    public const double MaxHeight = 3000;
    public const double MinVisible = 64;

    public readonly record struct MonitorArea(double Left, double Top, double Width, double Height);
    public readonly record struct PlacementRequest(bool PlacementSaved, double Left, double Top, double Width, double Height, bool Maximized);
    public readonly record struct PlacementResult(double Left, double Top, double Width, double Height, bool Maximize);

    public static PlacementResult Resolve(PlacementRequest saved, MonitorArea target, IReadOnlyList<MonitorArea> monitors)
    {
        var sizeOk = saved.PlacementSaved
            && double.IsFinite(saved.Width) && double.IsFinite(saved.Height)
            && saved.Width >= MinWidth && saved.Width <= MaxWidth
            && saved.Height >= MinHeight && saved.Height <= MaxHeight;
        var width = sizeOk ? saved.Width : DefaultWidth;
        var height = sizeOk ? saved.Height : DefaultHeight;
        var centeredLeft = target.Left + (target.Width - width) / 2;
        var centeredTop = target.Top + (target.Height - height) / 2;
        if (!sizeOk)
            return new(centeredLeft, centeredTop, width, height, false);
        if (!monitors.Any(m => Overlaps(saved.Left, saved.Top, width, height, m)))
            return new(centeredLeft, centeredTop, width, height, false);
        return new(saved.Left, saved.Top, width, height, saved.Maximized);
    }

    private static bool Overlaps(double left, double top, double width, double height, MonitorArea m)
    {
        var x1 = Math.Max(left, m.Left);
        var y1 = Math.Max(top, m.Top);
        var x2 = Math.Min(left + width, m.Left + m.Width);
        var y2 = Math.Min(top + height, m.Top + m.Height);
        return x2 - x1 >= MinVisible && y2 - y1 >= MinVisible;
    }
}
