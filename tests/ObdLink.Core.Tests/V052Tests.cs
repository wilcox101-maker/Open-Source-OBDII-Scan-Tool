using ObdLink.Core.Dashboard;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.5.2: cabin photographs replace tiled drawing brushes. Analog and digital clusters are both built in.</summary>
public class V052Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    [Fact]
    public void Version_is_0_5_2()
    {
        Assert.Contains("<Version>0.5.2</Version>", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "ObdLink.App.csproj")));
        Assert.Contains("<Version>0.5.2</Version>", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.Core", "ObdLink.Core.csproj")));
        var notes = File.ReadAllText(Path.Combine(Root(), "docs", "RELEASE-NOTES.md"));
        Assert.StartsWith("## v0.5.2", notes.TrimStart());
        Assert.Contains("Analog gauges", notes);
        Assert.Contains("Digital dash", notes);
        Assert.Contains("Digital cluster", notes);
        Assert.Contains("not tiled", notes);
        Assert.Contains("generated", notes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT", notes);
        Assert.DoesNotContain("v0.5.2-test", notes);
        Assert.Contains("## v0.5.1", notes);
        var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
        Assert.Contains("v0.5.2", readme);
        var about = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainViewModel.cs"));
        Assert.Contains("Assembly.GetName().Version", about);
        var guide = File.ReadAllText(Path.Combine(Root(), "docs", "GUIDE.md"));
        Assert.Contains("Analog gauges", guide);
        Assert.Contains("Digital dash", guide);
    }

    [Fact]
    public void Built_in_backgrounds_are_cover_photos_not_tiles()
    {
        Assert.False(DashboardBackdropPolicy.TileBuiltIns);
        Assert.Equal("UniformToFill", DashboardBackdropPolicy.CoverStretch);
        Assert.InRange(DashboardBackdropPolicy.ScrimAlpha, (byte)32, (byte)96);

        Assert.Equal(DashboardBackgrounds.Analog, DashboardBackgrounds.Normalize("analog"));
        Assert.Equal(DashboardBackgrounds.Wood, DashboardBackgrounds.Normalize("wood"));
        Assert.Equal(DashboardBackgrounds.None, DashboardBackgrounds.Normalize("nope"));
        Assert.Null(DashboardBackdropPolicy.FileFor(DashboardBackgrounds.None));
        Assert.Null(DashboardBackdropPolicy.FileFor(DashboardBackgrounds.Custom));

        var photos = DashboardBackgrounds.Choices.Where(c => c.Id is not (DashboardBackgrounds.None or DashboardBackgrounds.Custom)).ToList();
        Assert.Contains(photos, c => c.Id == DashboardBackgrounds.Analog && c.Name.Contains("analog", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(photos, c => c.Name.Contains("digital", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(photos, c => c.Id == DashboardBackgrounds.Steel && c.Name.Contains("digital", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(photos, c => c.Id == DashboardBackgrounds.Leather && c.Name.Contains("leather", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(photos, c => c.Id == DashboardBackgrounds.Onyx);
        Assert.DoesNotContain(photos, c => c.Name.Contains("grain", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(photos, c => c.Name.Contains("carbon fiber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(photos, c => c.Name.Contains("steel plate", StringComparison.OrdinalIgnoreCase));

        var files = photos.Select(c => DashboardBackdropPolicy.FileFor(c.Id)).ToList();
        Assert.DoesNotContain(files, string.IsNullOrEmpty);
        Assert.Equal(files.Count, files.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("analog.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.Analog));
        Assert.Equal("digital-dash.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.Wood));
        Assert.Equal("digital-cluster.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.Steel));

        var dir = Path.Combine(Root(), "assets", "backgrounds");
        foreach (var file in files)
        {
            var path = Path.Combine(dir, file!);
            Assert.True(File.Exists(path), path);
            var (w, h) = JpegSize(path);
            Assert.True(w >= 1600 && h >= 900, $"{file} is {w}x{h}");
            Assert.InRange(new FileInfo(path).Length, 40_000, 1_500_000);
        }

        var prov = File.ReadAllText(Path.Combine(dir, "PROVENANCE.txt"));
        Assert.Contains("generated", prov, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT", prov);
        Assert.Contains("analog", prov, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("digital", prov, StringComparison.OrdinalIgnoreCase);

        var backdrop = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardBackdrop.cs"));
        Assert.DoesNotContain("TileMode.Tile", backdrop);
        Assert.Contains("TileMode.None", backdrop);
        Assert.Contains("UniformToFill", backdrop);
        Assert.Contains("DashboardBackdropPolicy.ScrimAlpha", backdrop);
        Assert.Contains("component/assets/backgrounds/", backdrop);
        var csproj = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "ObdLink.App.csproj"));
        Assert.Contains(@"assets\backgrounds\*.jpg", csproj);
    }

    private static (int Width, int Height) JpegSize(string path)
    {
        using var fs = File.OpenRead(path);
        Span<byte> buf = stackalloc byte[8];
        if (fs.Read(buf[..2]) != 2 || buf[0] != 0xFF || buf[1] != 0xD8)
            throw new InvalidDataException(path + " is not a JPEG");
        while (fs.Position + 4 < fs.Length)
        {
            int b = fs.ReadByte();
            if (b != 0xFF) continue;
            int marker;
            do { marker = fs.ReadByte(); } while (marker == 0xFF);
            if (marker is 0xD8 or 0xD9 or ( >= 0xD0 and <= 0xD7)) continue;
            if (fs.Read(buf[..2]) != 2) break;
            int len = (buf[0] << 8) | buf[1];
            if (len < 2) break;
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                if (fs.Read(buf) != 8) break;
                int h = (buf[1] << 8) | buf[2];
                int w = (buf[3] << 8) | buf[4];
                return (w, h);
            }
            fs.Seek(len - 2, SeekOrigin.Current);
        }
        throw new InvalidDataException("No JPEG frame in " + path);
    }
}
