using System.Windows;
using System.Windows.Controls;
using ObdLink.Core.Ui;

namespace ObdLink.App.Controls;

/// <summary>Left-rail TabControl. Scrolls when the window is short and drops captions when it is tighter still.</summary>
public class NavTabControl : TabControl
{
    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(NavTabControl), new FrameworkPropertyMetadata(false));
    public bool IsCompact { get => (bool)GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    public static readonly DependencyProperty RailWidthProperty =
        DependencyProperty.Register(nameof(RailWidth), typeof(double), typeof(NavTabControl), new FrameworkPropertyMetadata(NavRailLayout.LabeledWidth));
    public double RailWidth { get => (double)GetValue(RailWidthProperty); set => SetValue(RailWidthProperty, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild("PART_NavScroll") is ScrollViewer sv)
        {
            sv.SizeChanged += (_, _) => Update(sv);
            sv.ScrollChanged += (_, _) => Update(sv);
            Update(sv);
        }
    }

    private void Update(ScrollViewer sv)
    {
        var h = sv.ViewportHeight;
        if (double.IsNaN(h) || h < 1) h = sv.ActualHeight;
        var d = NavRailLayout.Decide(h, Items.Count);
        IsCompact = d.Compact;
        RailWidth = d.Width;
    }
}
