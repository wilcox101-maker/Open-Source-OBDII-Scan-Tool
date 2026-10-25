namespace ObdLink.Core.Dashboard;

/// <summary>
/// v0.4.0 dashboard flow. Saved sizes and order are left alone. Cards keep those sizes, and when the window is
/// wider than the width last used in Customize the wrap is pinned to that width so the rows stay put.
/// Gauge-height cards (bars, tiles, lamps — about one gauge tall, not dials or the tire widget) are stretched
/// to one per row only when the viewport is narrower than the widest saved card, so the saved layout cannot fit.
/// </summary>
public static class DashboardFlow
{
    /// <summary>CardStyle horizontal margin (6 + 6).</summary>
    public const double CardMargin = 12;
    /// <summary>Standard bar height. Cards at or under 1.5× this (180) are the ones that stack.</summary>
    public const double GaugeHeight = 120;
    public const double GaugeComparableHeight = 180;

    public readonly record struct CardBox(double Width, double Height, double Scale)
    {
        public double LayoutWidth => Width * Scale;
        public double LayoutHeight => Height * Scale;
    }

    public readonly record struct FlowPlan(double[] Widths, bool Stacking, double HostMaxWidth)
    {
        public bool ConstrainHost => !double.IsPositiveInfinity(HostMaxWidth);
    }

    /// <summary>Pre-transform card size. Matches the dashboard card triggers (Medium is a 1.2 layout scale; Wide is 620 wide).</summary>
    public static CardBox Metrics(DisplayKind display, ItemSize size)
    {
        double w = 300, h = GaugeHeight, scale = 1;
        switch (display)
        {
            case DisplayKind.Dial: w = 230; h = 250; break;
            case DisplayKind.Tile: w = 180; h = 130; break;
            case DisplayKind.Lamp: w = 200; h = 80; break;
            case DisplayKind.Tpms: w = 300; h = 290; break;
        }
        if (size is ItemSize.Large or ItemSize.Wide)
        {
            switch (display)
            {
                case DisplayKind.Dial: w = 290; h = 310; break;
                case DisplayKind.Bar: w = 440; break;
                case DisplayKind.Tile: w = 250; h = 180; break;
                case DisplayKind.Lamp: w = 280; break;
                case DisplayKind.Tpms: w = 380; h = 320; break;
            }
        }
        if (size == ItemSize.Medium) scale = 1.2;
        if (size == ItemSize.Wide) w = 620;
        return new(w, h, scale);
    }

    public static bool IsGaugeHeight(DisplayKind display, ItemSize size)
    {
        if (display is DisplayKind.Dial or DisplayKind.Tpms) return false;
        return Metrics(display, size).LayoutHeight <= GaugeComparableHeight;
    }

    public static FlowPlan Plan(IReadOnlyList<(DisplayKind Display, ItemSize Size)> items, double availableWidth, double arrangeWidth)
    {
        var boxes = new CardBox[items.Count];
        var widths = new double[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            boxes[i] = Metrics(items[i].Display, items[i].Size);
            widths[i] = boxes[i].Width;
        }
        if (items.Count == 0 || availableWidth <= 1)
            return new(widths, false, double.PositiveInfinity);

        var widest = 0.0;
        for (int i = 0; i < boxes.Length; i++) widest = Math.Max(widest, boxes[i].LayoutWidth + CardMargin);
        if (widest > availableWidth + 0.5)
        {
            var fit = Math.Max(80, availableWidth - CardMargin);
            for (int i = 0; i < items.Count; i++)
            {
                var scale = boxes[i].Scale <= 0 ? 1 : boxes[i].Scale;
                var room = fit / scale;
                widths[i] = IsGaugeHeight(items[i].Display, items[i].Size) ? room : Math.Min(boxes[i].Width, room);
            }
            return new(widths, true, double.PositiveInfinity);
        }
        // Pin only when the saved width can actually show every card. A width saved while stacked is narrower
        // than the widest card, and pinning to it would clip that card once the window is wide again.
        var pin = arrangeWidth > 1 && availableWidth + 0.5 >= arrangeWidth && arrangeWidth + 0.5 >= widest;
        return new(widths, false, pin ? arrangeWidth : double.PositiveInfinity);
    }
}
