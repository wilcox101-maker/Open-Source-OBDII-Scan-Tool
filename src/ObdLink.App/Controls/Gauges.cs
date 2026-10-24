using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ObdLink.App.Controls;

/// <summary>
/// Shared gauge properties. Gauges are plain FrameworkElements drawn in OnRender (no third-party libraries).
/// Brushes are dependency properties set from Controls.xaml with DynamicResource so Light/Dark switch live.
/// </summary>
public abstract class GaugeBase : FrameworkElement
{
    private static FrameworkPropertyMetadata M(object v) => new(v, FrameworkPropertyMetadataOptions.AffectsRender);
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(GaugeBase), M(double.NaN));
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(GaugeBase), M(0.0));
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(GaugeBase), M(100.0));
    public static readonly DependencyProperty WarnHighProperty = DependencyProperty.Register(nameof(WarnHigh), typeof(double), typeof(GaugeBase), M(double.NaN));
    public static readonly DependencyProperty WarnLowProperty = DependencyProperty.Register(nameof(WarnLow), typeof(double), typeof(GaugeBase), M(double.NaN));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(GaugeBase), M(""));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(GaugeBase), M(""));
    public static readonly DependencyProperty ValueTextProperty = DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(GaugeBase), M("--"));
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(GaugeBase), M(Brushes.Gray));
    public static readonly DependencyProperty FillBrushProperty = DependencyProperty.Register(nameof(FillBrush), typeof(Brush), typeof(GaugeBase), M(Brushes.DodgerBlue));
    public static readonly DependencyProperty WarnBrushProperty = DependencyProperty.Register(nameof(WarnBrush), typeof(Brush), typeof(GaugeBase), M(Brushes.OrangeRed));
    public static readonly DependencyProperty NeedleBrushProperty = DependencyProperty.Register(nameof(NeedleBrush), typeof(Brush), typeof(GaugeBase), M(Brushes.OrangeRed));
    public static readonly DependencyProperty TickBrushProperty = DependencyProperty.Register(nameof(TickBrush), typeof(Brush), typeof(GaugeBase), M(Brushes.Gray));
    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(GaugeBase), M(Brushes.Black));
    public static readonly DependencyProperty DimBrushProperty = DependencyProperty.Register(nameof(DimBrush), typeof(Brush), typeof(GaugeBase), M(Brushes.Gray));

    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double WarnHigh { get => (double)GetValue(WarnHighProperty); set => SetValue(WarnHighProperty, value); }
    public double WarnLow { get => (double)GetValue(WarnLowProperty); set => SetValue(WarnLowProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string ValueText { get => (string)GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush FillBrush { get => (Brush)GetValue(FillBrushProperty); set => SetValue(FillBrushProperty, value); }
    public Brush WarnBrush { get => (Brush)GetValue(WarnBrushProperty); set => SetValue(WarnBrushProperty, value); }
    public Brush NeedleBrush { get => (Brush)GetValue(NeedleBrushProperty); set => SetValue(NeedleBrushProperty, value); }
    public Brush TickBrush { get => (Brush)GetValue(TickBrushProperty); set => SetValue(TickBrushProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Brush DimBrush { get => (Brush)GetValue(DimBrushProperty); set => SetValue(DimBrushProperty, value); }

    protected double Fraction(double v) => Maximum <= Minimum || double.IsNaN(v) ? 0 : Math.Clamp((v - Minimum) / (Maximum - Minimum), 0, 1);
    protected bool IsWarn => !double.IsNaN(Value) && ((!double.IsNaN(WarnHigh) && Value >= WarnHigh) || (!double.IsNaN(WarnLow) && Value <= WarnLow));
    protected FormattedText Text(string s, double size, Brush b, FontWeight? w = null) =>
        new(s ?? "", CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, w ?? FontWeights.Normal, FontStretches.Normal), size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    protected static string Num(double v) => Math.Abs(v) >= 1000 ? (v / 1000).ToString("0.#") + "k" : v.ToString("0.#");
}

/// <summary>270° circular gauge with ticks, a warning arc, a value arc, a needle and a big numeric readout (RPM, speed).</summary>
public sealed class CircularGauge : GaugeBase
{
    private const double Start = 135, Sweep = 270;
    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 220 : a.Width, double.IsInfinity(a.Height) ? 220 : a.Height);
    private static Point P(Point c, double r, double deg) { var a = deg * Math.PI / 180; return new(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)); }
    private static Geometry Arc(Point c, double r, double from, double sweep)
    {
        if (sweep <= 0.01) return Geometry.Empty;
        var g = new StreamGeometry();
        using (var ctx = g.Open()) { ctx.BeginFigure(P(c, r, from), false, false); ctx.ArcTo(P(c, r, from + sweep), new Size(r, r), 0, sweep > 180, SweepDirection.Clockwise, true, false); }
        g.Freeze(); return g;
    }
    protected override void OnRender(DrawingContext dc)
    {
        var size = Math.Min(ActualWidth, ActualHeight); if (size < 40) return;
        var c = new Point(ActualWidth / 2, ActualHeight / 2 + size * 0.04); var r = size * 0.40; var th = size * 0.065;
        dc.DrawGeometry(null, new Pen(TrackBrush, th) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Arc(c, r, Start, Sweep));
        if (!double.IsNaN(WarnHigh)) dc.DrawGeometry(null, new Pen(WarnBrush, th * 0.35), Arc(c, r + th * 0.75, Start + Sweep * Fraction(WarnHigh), Sweep * (1 - Fraction(WarnHigh))));
        var f = Fraction(Value);
        dc.DrawGeometry(null, new Pen(IsWarn ? WarnBrush : FillBrush, th) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, Arc(c, r, Start, Sweep * f));
        var tick = new Pen(TickBrush, 1.2);
        for (int i = 0; i <= 10; i++)
        {
            var a = Start + Sweep * i / 10.0;
            dc.DrawLine(tick, P(c, r - th * 0.9, a), P(c, r - th * (i % 5 == 0 ? 1.9 : 1.4), a));
            if (i % 2 == 0) { var t = Text(Num(Minimum + (Maximum - Minimum) * i / 10), size * 0.05, DimBrush); var p = P(c, r - th * 2.7, a); dc.DrawText(t, new Point(p.X - t.Width / 2, p.Y - t.Height / 2)); }
        }
        if (!double.IsNaN(Value))
        {
            var a = Start + Sweep * f;
            dc.DrawLine(new Pen(NeedleBrush, size * 0.018) { EndLineCap = PenLineCap.Triangle, StartLineCap = PenLineCap.Round }, P(c, -r * 0.12, a), P(c, r - th * 1.2, a));
        }
        dc.DrawEllipse(NeedleBrush, null, c, size * 0.03, size * 0.03);
        var v = Text(ValueText, size * 0.15, IsWarn ? WarnBrush : Foreground, FontWeights.SemiBold);
        dc.DrawText(v, new Point(c.X - v.Width / 2, c.Y + r * 0.22));
        var u = Text(Unit, size * 0.06, DimBrush); dc.DrawText(u, new Point(c.X - u.Width / 2, c.Y + r * 0.22 + v.Height));
        var ti = Text(Title.ToUpperInvariant(), size * 0.055, DimBrush, FontWeights.SemiBold); dc.DrawText(ti, new Point(c.X - ti.Width / 2, c.Y - r * 0.45));
    }
}

/// <summary>Horizontal bar gauge with warning zones (fuel, temps, trims, voltage...).</summary>
public sealed class LinearGauge : GaugeBase
{
    protected override Size MeasureOverride(Size a) => new(double.IsInfinity(a.Width) ? 200 : a.Width, 46);
    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth; if (w < 30) return;
        var title = Text(Title, 12, DimBrush); dc.DrawText(title, new Point(0, 0));
        var val = Text(ValueText + (string.IsNullOrEmpty(Unit) ? "" : " " + Unit), 14, IsWarn ? WarnBrush : Foreground, FontWeights.SemiBold);
        dc.DrawText(val, new Point(w - val.Width, -1));
        var bar = new Rect(0, 26, w, 10);
        dc.DrawRoundedRectangle(TrackBrush, null, bar, 5, 5);
        if (!double.IsNaN(WarnHigh)) { var x = w * Fraction(WarnHigh); dc.DrawRectangle(WarnBrush, null, new Rect(x, 38, Math.Max(0, w - x), 2)); }
        if (!double.IsNaN(WarnLow)) dc.DrawRectangle(WarnBrush, null, new Rect(0, 38, w * Fraction(WarnLow), 2));
        // centered bars for signed ranges (fuel trims)
        var zero = Minimum < 0 && Maximum > 0 ? Fraction(0) : 0; var f = Fraction(Value);
        var x0 = w * Math.Min(zero, f); var x1 = w * Math.Max(zero, f);
        if (!double.IsNaN(Value) && x1 - x0 > 0.5) dc.DrawRoundedRectangle(IsWarn ? WarnBrush : FillBrush, null, new Rect(x0, 26, x1 - x0, 10), 5, 5);
        if (zero > 0) dc.DrawRectangle(TickBrush, null, new Rect(w * zero - 0.5, 24, 1, 14));
    }
}
