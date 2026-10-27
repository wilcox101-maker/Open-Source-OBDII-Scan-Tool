using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ObdLink.Core.Dashboard;

namespace ObdLink.App;

/// <summary>
/// One cabin photograph per built-in backdrop, drawn with cover stretch (UniformToFill) and a light scrim.
/// Custom images use the same cover path. None leaves the app theme showing through.
/// </summary>
public static class DashboardBackdrop
{
    public static Brush For(string? id, string? customPath)
    {
        var norm = DashboardBackgrounds.Normalize(id);
        Brush b = norm switch
        {
            DashboardBackgrounds.Custom => CoverFile(customPath) ?? Brushes.Transparent,
            _ when DashboardBackdropPolicy.FileFor(norm) is { } file => CoverResource(file) ?? Brushes.Transparent,
            _ => Brushes.Transparent,
        };
        if (b.CanFreeze) b.Freeze();
        return b;
    }

    private static Brush? CoverResource(string fileName)
    {
        // Resource names are lowercased by the WPF compiler (assets/backgrounds/…), same as the header icons.
        var uri = new Uri($"pack://application:,,,/OBDLinkDiag;component/assets/backgrounds/{fileName}", UriKind.Absolute);
        return Cover(uri);
    }

    private static Brush? CoverFile(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return Cover(new Uri(path, UriKind.Absolute));
    }

    private static Brush? Cover(Uri uri)
    {
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            img.UriSource = uri;
            img.EndInit();
            img.Freeze();
            return WithScrim(img);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Dashboard background image could not be loaded");
            return null;
        }
    }

    /// <summary>Scales the photo to cover the dashboard once. TileMode.None is required; a small tiled viewport was the v0.5.1 look.</summary>
    private static Brush WithScrim(BitmapSource img)
    {
        var rect = new Rect(0, 0, img.PixelWidth, img.PixelHeight);
        var scrim = new SolidColorBrush(Color.FromArgb(DashboardBackdropPolicy.ScrimAlpha, 0, 0, 0));
        scrim.Freeze();
        var group = new DrawingGroup();
        group.Children.Add(new ImageDrawing(img, rect));
        group.Children.Add(new GeometryDrawing(scrim, null, new RectangleGeometry(rect)));
        group.Freeze();
        var brush = new DrawingBrush(group)
        {
            Stretch = Stretch.UniformToFill,
            TileMode = TileMode.None,
            AlignmentX = AlignmentX.Center,
            AlignmentY = AlignmentY.Center,
            Viewport = new Rect(0, 0, 1, 1),
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = new Rect(0, 0, 1, 1),
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
        };
        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.HighQuality);
        return brush;
    }
}
