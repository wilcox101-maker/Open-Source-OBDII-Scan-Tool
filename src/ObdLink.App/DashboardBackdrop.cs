using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ObdLink.Core.Dashboard;

namespace ObdLink.App;

/// <summary>Drawing brushes for the built-in dashboard backdrops, plus a file under Data\backgrounds for Custom.</summary>
public static class DashboardBackdrop
{
    public static Brush For(string? id, string? customPath)
    {
        Brush b = id switch
        {
            DashboardBackgrounds.Wood => Wood(),
            DashboardBackgrounds.Steel => Steel(),
            DashboardBackgrounds.Mirror => Mirror(),
            DashboardBackgrounds.Onyx => Onyx(),
            DashboardBackgrounds.Leather => Leather(),
            DashboardBackgrounds.Carbon => Carbon(),
            DashboardBackgrounds.Brushed => Brushed(),
            DashboardBackgrounds.Custom => Image(customPath) ?? Brushes.Transparent,
            _ => Brushes.Transparent,
        };
        if (b.CanFreeze) b.Freeze();
        return b;
    }

    private static Brush? Image(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            img.UriSource = new Uri(path, UriKind.Absolute);
            img.EndInit();
            img.Freeze();
            return new ImageBrush(img) { Stretch = Stretch.UniformToFill, AlignmentX = AlignmentX.Center, AlignmentY = AlignmentY.Center };
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Dashboard background image could not be loaded");
            return null;
        }
    }

    private static DrawingBrush Tile(Drawing drawing, double w, double h) => new()
    {
        Drawing = drawing,
        TileMode = TileMode.Tile,
        Viewport = new Rect(0, 0, w, h),
        ViewportUnits = BrushMappingMode.Absolute,
        Stretch = Stretch.None,
    };

    private static Brush Wood()
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(BrushOf(0x6B, 0x3E, 0x22), null, new RectangleGeometry(new Rect(0, 0, 140, 56))));
        var grain = new GeometryGroup();
        var pen = new Pen(BrushOf(0x3A, 0x22, 0x12, 110), 1.1);
        for (int y = 3; y < 56; y += 6)
            grain.Children.Add(new LineGeometry(new Point(0, y), new Point(140, y + (y % 12 == 3 ? 2 : -1))));
        g.Children.Add(new GeometryDrawing(null, pen, grain));
        g.Children.Add(new GeometryDrawing(BrushOf(0x2C, 0x16, 0x0A, 90), null, new EllipseGeometry(new Point(96, 24), 9, 5)));
        return Tile(g, 140, 56);
    }

    private static Brush Steel()
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(new LinearGradientBrush(ColorOf(0x9A, 0xA1, 0xA8), ColorOf(0x5A, 0x61, 0x68), 90), null, new RectangleGeometry(new Rect(0, 0, 180, 90))));
        g.Children.Add(new GeometryDrawing(new LinearGradientBrush(ColorOf(0xE6, 0xEA, 0xEE, 150), ColorOf(0xE6, 0xEA, 0xEE, 0), 90), null, new RectangleGeometry(new Rect(0, 8, 180, 12))));
        var lines = new GeometryGroup();
        var pen = new Pen(BrushOf(0xFF, 0xFF, 0xFF, 28), 0.6);
        for (int y = 0; y < 90; y += 3) lines.Children.Add(new LineGeometry(new Point(0, y), new Point(180, y)));
        g.Children.Add(new GeometryDrawing(null, pen, lines));
        foreach (var p in new[] { new Point(14, 14), new Point(166, 14), new Point(14, 76), new Point(166, 76) })
            g.Children.Add(new GeometryDrawing(BrushOf(0x6A, 0x70, 0x76), new Pen(BrushOf(0x3E, 0x44, 0x4A), 1), new EllipseGeometry(p, 4.5, 4.5)));
        return Tile(g, 180, 90);
    }

    private static Brush Mirror()
    {
        var g = new DrawingGroup();
        var sheen = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        sheen.GradientStops.Add(new GradientStop(ColorOf(0xF4, 0xF8, 0xFC), 0));
        sheen.GradientStops.Add(new GradientStop(ColorOf(0xC5, 0xD4, 0xE0), 0.45));
        sheen.GradientStops.Add(new GradientStop(ColorOf(0xF7, 0xFB, 0xFF), 0.62));
        sheen.GradientStops.Add(new GradientStop(ColorOf(0x8E, 0xA4, 0xB8), 1));
        g.Children.Add(new GeometryDrawing(sheen, null, new RectangleGeometry(new Rect(0, 0, 200, 120))));
        return Tile(g, 200, 120);
    }

    private static Brush Onyx()
    {
        var g = new DrawingGroup();
        var fill = new RadialGradientBrush(ColorOf(0x2C, 0x2C, 0x2E), ColorOf(0x08, 0x08, 0x0A)) { RadiusX = 0.75, RadiusY = 0.75 };
        g.Children.Add(new GeometryDrawing(fill, null, new RectangleGeometry(new Rect(0, 0, 200, 140))));
        return Tile(g, 200, 140);
    }

    private static Brush Leather()
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(BrushOf(0x3A, 0x18, 0x16), null, new RectangleGeometry(new Rect(0, 0, 120, 72))));
        var stitch = new GeometryGroup();
        var pen = new Pen(BrushOf(0x8A, 0x5A, 0x48), 1) { DashStyle = DashStyles.Dash };
        stitch.Children.Add(new LineGeometry(new Point(8, 10), new Point(112, 10)));
        stitch.Children.Add(new LineGeometry(new Point(8, 62), new Point(112, 62)));
        stitch.Children.Add(new LineGeometry(new Point(10, 8), new Point(10, 64)));
        stitch.Children.Add(new LineGeometry(new Point(110, 8), new Point(110, 64)));
        g.Children.Add(new GeometryDrawing(null, pen, stitch));
        g.Children.Add(new GeometryDrawing(BrushOf(0x2A, 0x10, 0x0E, 80), null, new RectangleGeometry(new Rect(18, 18, 84, 36))));
        return Tile(g, 120, 72);
    }

    private static Brush Carbon()
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(BrushOf(0x14, 0x14, 0x16), null, new RectangleGeometry(new Rect(0, 0, 16, 16))));
        var weave = new GeometryGroup();
        for (int i = -16; i < 32; i += 4)
        {
            weave.Children.Add(new LineGeometry(new Point(i, 0), new Point(i + 16, 16)));
            weave.Children.Add(new LineGeometry(new Point(i + 2, 16), new Point(i + 18, 0)));
        }
        g.Children.Add(new GeometryDrawing(null, new Pen(BrushOf(0x3A, 0x3A, 0x40), 1.4), weave));
        return Tile(g, 16, 16);
    }

    private static Brush Brushed()
    {
        var g = new DrawingGroup();
        g.Children.Add(new GeometryDrawing(BrushOf(0xC5, 0xC9, 0xCE), null, new RectangleGeometry(new Rect(0, 0, 80, 40))));
        var lightLines = new GeometryGroup();
        var darkLines = new GeometryGroup();
        for (int y = 0; y < 40; y += 2)
            (y % 4 == 0 ? lightLines : darkLines).Children.Add(new LineGeometry(new Point(0, y), new Point(80, y)));
        g.Children.Add(new GeometryDrawing(null, new Pen(BrushOf(0xE8, 0xEB, 0xEE, 180), 0.7), lightLines));
        g.Children.Add(new GeometryDrawing(null, new Pen(BrushOf(0x8E, 0x94, 0x9A, 160), 0.7), darkLines));
        return Tile(g, 80, 40);
    }

    private static SolidColorBrush BrushOf(byte r, byte g, byte b, byte a = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Color ColorOf(byte r, byte g, byte b, byte a = 255) => Color.FromArgb(a, r, g, b);
}
