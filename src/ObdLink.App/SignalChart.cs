using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ObdLink.Core.LiveData;

namespace ObdLink.App;

/// <summary>A plotted signal: <see cref="Slot"/> is its fixed categorical colour slot (follows the signal, not its rank).</summary>
public sealed record ChartSeries(string Key, string Name, string Unit, int Slot);

/// <summary>
/// Validated categorical palette (dataviz reference palette, 8 slots, fixed order). Checked with validate_palette.js against
/// this app's card surfaces: dark #26282C all checks PASS; light #FFFFFF passes CVD / normal-vision floors, with slots 3, 4, 5
/// under 3:1 contrast, so every lane also carries a visible text title (identity is never colour alone) and the grid is the
/// table view.
/// </summary>
public static class ChartPalette
{
    public static readonly string[] Light = { "#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6", "#e34948" };
    public static readonly string[] Dark = { "#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#0ca30c", "#9085e9", "#e66767" };
    public const int Slots = 8;
    public static Color For(int slot) => (Color)ColorConverter.ConvertFromString((ThemeManager.Effective == "Light" ? Light : Dark)[slot % Slots]);
}

/// <summary>
/// v0.12.0: live / replay chart as small multiples. One lane per signal, each with its own y-scale (never a second axis in one
/// plot), a shared time axis, min/max decimation per pixel column so peaks survive, trigger markers, and a hover crosshair that
/// reads every lane's value at that time. Drawn in OnRender; re-rendered when <see cref="Revision"/> changes.
/// </summary>
public sealed class SignalChart : FrameworkElement
{
    public static readonly DependencyProperty HistoryProperty = Dp(nameof(History), typeof(SignalHistory), null);
    public static readonly DependencyProperty SeriesProperty = Dp(nameof(Series), typeof(IReadOnlyList<ChartSeries>), null);
    public static readonly DependencyProperty WindowSecondsProperty = Dp(nameof(WindowSeconds), typeof(double), 60.0);
    public static readonly DependencyProperty EndProperty = Dp(nameof(End), typeof(DateTime?), null);
    public static readonly DependencyProperty MarkersProperty = Dp(nameof(Markers), typeof(IReadOnlyList<DateTime>), null);
    public static readonly DependencyProperty RevisionProperty = Dp(nameof(Revision), typeof(int), 0);

    private static DependencyProperty Dp(string name, Type t, object? def) =>
        DependencyProperty.Register(name, t, typeof(SignalChart), new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

    public SignalHistory? History { get => (SignalHistory?)GetValue(HistoryProperty); set => SetValue(HistoryProperty, value); }
    public IReadOnlyList<ChartSeries>? Series { get => (IReadOnlyList<ChartSeries>?)GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public double WindowSeconds { get => (double)GetValue(WindowSecondsProperty); set => SetValue(WindowSecondsProperty, value); }
    /// <summary>Right edge of the window; null = the newest sample.</summary>
    public DateTime? End { get => (DateTime?)GetValue(EndProperty); set => SetValue(EndProperty, value); }
    public IReadOnlyList<DateTime>? Markers { get => (IReadOnlyList<DateTime>?)GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }
    public int Revision { get => (int)GetValue(RevisionProperty); set => SetValue(RevisionProperty, value); }

    private Point? _hover;
    private const double AxisH = 18, Gap = 8, TitleH = 16;

    public SignalChart() { ClipToBounds = true; Focusable = false; }

    protected override void OnMouseMove(MouseEventArgs e) { _hover = e.GetPosition(this); InvalidateVisual(); }
    protected override void OnMouseLeave(MouseEventArgs e) { _hover = null; InvalidateVisual(); }

    private Brush Res(string key, Color fallback) => TryFindResource(key) as Brush ?? new SolidColorBrush(fallback);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // hit-testable for hover
        var fg = Res("Fg", Colors.Gray); var dim = Res("FgDim", Colors.Gray); var grid = Res("Border", Colors.DimGray);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        FormattedText T(string s, Brush b, double size = 11) =>
            new(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, b, dpi);

        var hist = History; var series = Series;
        if (hist is null || series is null || series.Count == 0 || w < 80 || h < 60)
        {
            dc.DrawText(T("Tick “Plot” on rows in the grid to chart them.", dim, 12), new Point(8, 8));
            return;
        }
        var span = hist.Span;
        if (span is null) { dc.DrawText(T("Waiting for data…", dim, 12), new Point(8, 8)); return; }
        var end = End ?? span.Value.To;
        var start = end - TimeSpan.FromSeconds(Math.Max(1, WindowSeconds));
        double plotW = w - 2, laneH = Math.Max(24, (h - AxisH - Gap * (series.Count - 1)) / series.Count);
        double X(DateTime t) => 1 + (t - start).Ticks / (double)(end - start).Ticks * plotW;
        DateTime? hoverT = _hover is { } hp && hp.Y < h - AxisH ? start + TimeSpan.FromTicks((long)((hp.X - 1) / plotW * (end - start).Ticks)) : null;

        var gridPen = new Pen(grid, 1); gridPen.Freeze();
        var markerPen = new Pen(dim, 1) { DashStyle = DashStyles.Dash }; markerPen.Freeze();

        for (int i = 0; i < series.Count; i++)
        {
            var s = series[i];
            double top = i * (laneH + Gap), plotTop = top + TitleH, plotH = laneH - TitleH - 2;
            var colour = new SolidColorBrush(ChartPalette.For(s.Slot)); colour.Freeze();
            var pts = hist.Window(s.Key, start, end);

            // Lane title: swatch + name (text in text ink, never the series colour) + latest value.
            dc.DrawRoundedRectangle(colour, null, new Rect(2, top + 4, 8, 8), 2, 2);
            var title = T(s.Name + (s.Unit.Length > 0 ? $" ({s.Unit})" : ""), fg);
            dc.DrawText(title, new Point(14, top));
            if (pts.Count > 0) { var cur = T(Fmt(pts[^1].V), fg); dc.DrawText(cur, new Point(w - cur.Width - 4, top)); }
            dc.DrawLine(gridPen, new Point(0, plotTop + plotH), new Point(w, plotTop + plotH)); // recessive baseline

            var inView = pts.Where(p => p.T >= start && p.T <= end).ToList();
            if (inView.Count == 0 && pts.Count == 0) { dc.DrawText(T("no samples in this window", dim, 10), new Point(14, plotTop + 2)); continue; }
            var scaleSrc = inView.Count > 0 ? inView : pts.ToList();
            double lo = scaleSrc.Min(p => p.V), hi = scaleSrc.Max(p => p.V);
            if (hi - lo < 1e-9) { lo -= 1; hi += 1; }
            double pad = (hi - lo) * 0.08; lo -= pad; hi += pad;
            double Y(double v) => plotTop + plotH - (v - lo) / (hi - lo) * plotH;
            var hiT = T(Fmt(hi - pad), dim, 9); var loT = T(Fmt(lo + pad), dim, 9);
            dc.DrawText(hiT, new Point(w - hiT.Width - 4, plotTop));
            dc.DrawText(loT, new Point(w - loT.Width - 4, plotTop + plotH - loT.Height));

            var draw = SignalHistory.Decimate(pts, start, end, Math.Max(1, (int)plotW));
            if (draw.Count >= 2)
            {
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(X(draw[0].T), Y(draw[0].V)), false, false);
                    for (int k = 1; k < draw.Count; k++) ctx.LineTo(new Point(X(draw[k].T), Y(draw[k].V)), true, false);
                }
                g.Freeze();
                var pen = new Pen(colour, 2) { LineJoin = PenLineJoin.Round }; pen.Freeze();
                dc.PushClip(new RectangleGeometry(new Rect(0, plotTop - 1, w, plotH + 2)));
                dc.DrawGeometry(null, pen, g);
                dc.Pop();
            }
            else if (draw.Count == 1)
                dc.DrawEllipse(colour, null, new Point(X(draw[0].T), Y(draw[0].V)), 4, 4);

            if (hoverT is { } ht && pts.Count > 0)
            {
                var near = pts.MinBy(p => Math.Abs((p.T - ht).Ticks));
                if (near.T >= start && near.T <= end)
                {
                    var c = new Point(X(near.T), Y(near.V));
                    dc.DrawEllipse(colour, new Pen(Res("Bg", Colors.Black), 2), c, 4, 4);
                    var lab = T($"{Fmt(near.V)} {s.Unit}".Trim(), fg);
                    double lx = c.X + 8 + lab.Width > w ? c.X - 8 - lab.Width : c.X + 8;
                    var bg = Res("Bg", Colors.Black);
                    dc.DrawRoundedRectangle(bg, gridPen, new Rect(lx - 3, Math.Clamp(c.Y - 9, plotTop, plotTop + plotH - 16), lab.Width + 6, 16), 3, 3);
                    dc.DrawText(lab, new Point(lx, Math.Clamp(c.Y - 8, plotTop + 1, plotTop + plotH - 15)));
                }
            }
        }

        double lanesBottom = series.Count * (laneH + Gap) - Gap;
        if (Markers is { } marks)
            foreach (var m in marks)
                if (m >= start && m <= end) dc.DrawLine(markerPen, new Point(X(m), 0), new Point(X(m), lanesBottom));

        // Time axis: window length on the left, right edge clock time.
        var left = T($"−{WindowSeconds:0} s", dim, 10); var right = T(end.ToString("HH:mm:ss"), dim, 10);
        dc.DrawText(left, new Point(2, h - AxisH + 2));
        dc.DrawText(right, new Point(w - right.Width - 2, h - AxisH + 2));
        if (hoverT is { } t0)
        {
            double x = Math.Clamp(_hover!.Value.X, 1, w - 1);
            dc.DrawLine(gridPen, new Point(x, 0), new Point(x, lanesBottom));
            var tl = T(t0.ToString("HH:mm:ss.f"), fg, 10);
            dc.DrawText(tl, new Point(Math.Clamp(x - tl.Width / 2, 0, w - tl.Width), h - AxisH + 2));
        }
    }

    private static string Fmt(double v) => Math.Abs(v) >= 1000 ? v.ToString("0") : Math.Abs(v) >= 10 ? v.ToString("0.#") : v.ToString("0.##");
}
