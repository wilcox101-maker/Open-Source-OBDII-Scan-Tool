namespace ObdLink.Core.Ui;

/// <summary>
/// v0.4.0 left rail. Labels stay on while the strip fits. A short window scrolls (the view hosts the tabs in a
/// vertical ScrollViewer). When fewer than <see cref="MinVisibleLabeled"/> labeled tabs would be on screen,
/// the rail collapses to icons. Labels come back as soon as that many fit again.
/// Strides are the laid-out height of one tab (padding, icon, and caption), used only to choose the mode.
/// The ScrollViewer still uses the real height, so a tab is never clipped just because this estimate is short.
/// </summary>
public static class NavRailLayout
{
    public const int MinVisibleLabeled = 4;
    /// <summary>Icon plus caption, matching NavTabStyle (padding 8+8, icon 20, caption ~16, margins).</summary>
    public const double LabeledStride = 60;
    /// <summary>Icon only, caption collapsed.</summary>
    public const double IconStride = 40;
    public const double Pad = 16;
    public const double LabeledWidth = 112;
    public const double IconWidth = 56;

    public readonly record struct Decision(bool Compact, bool Scrolls, double Width);

    public static Decision Decide(double availableHeight, int tabCount)
    {
        if (tabCount <= 0 || availableHeight <= 1)
            return new(false, false, LabeledWidth);
        var labeled = tabCount * LabeledStride + Pad;
        if (availableHeight >= labeled)
            return new(false, false, LabeledWidth);
        var visible = (availableHeight - Pad) / LabeledStride;
        if (visible >= MinVisibleLabeled)
            return new(false, true, LabeledWidth);
        var icons = tabCount * IconStride + Pad;
        return new(true, availableHeight + 0.5 < icons, IconWidth);
    }
}
