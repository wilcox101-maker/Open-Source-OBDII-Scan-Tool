using ObdLink.Core.Dashboard;
using ObdLink.Core.Safety;
using Xunit;

namespace ObdLink.Core.Tests;

public class V060Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nope")]
    [InlineData("Analog")]
    [InlineData("wood")]
    [InlineData("STEEL")]
    [InlineData("Mirror")]
    [InlineData("onyx")]
    [InlineData("Leather")]
    [InlineData("Carbon")]
    [InlineData("Brushed")]
    public void Removed_cabin_ids_and_unknown_ids_normalize_to_none(string? id) =>
        Assert.Equal(DashboardBackgrounds.None, DashboardBackgrounds.Normalize(id));

    [Fact]
    public void Material_ids_round_trip_and_custom_stays_custom()
    {
        Assert.Equal(DashboardBackgrounds.WoodGrain, DashboardBackgrounds.Normalize("woodgrain"));
        Assert.Equal(DashboardBackgrounds.BrushedAluminum, DashboardBackgrounds.Normalize("brushedaluminum"));
        Assert.Equal(DashboardBackgrounds.GlossBlack, DashboardBackgrounds.Normalize("glossblack"));
        Assert.Equal(DashboardBackgrounds.DiamondPlate, DashboardBackgrounds.Normalize("diamondplate"));
        Assert.Equal(DashboardBackgrounds.CarbonFiber, DashboardBackgrounds.Normalize("carbonfiber"));
        Assert.Equal(DashboardBackgrounds.SportsLeather, DashboardBackgrounds.Normalize("sportsleather"));
        Assert.Equal(DashboardBackgrounds.Custom, DashboardBackgrounds.Normalize("custom"));
        Assert.Equal(DashboardBackgrounds.None, DashboardBackgrounds.Normalize("none"));
    }

    [Fact]
    public void Saved_analog_background_loads_as_none_and_keeps_layouts()
    {
        var path = Path.Combine(Path.GetTempPath(), "v060-" + Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"Dashboard":{"Background":"Analog","Layouts":[{"Name":"Default","Items":[{"Key":"rpm","Display":"Dial","Size":"Large"}]}]}}""");
        var loaded = AppSettings.Load(path);
        Assert.Equal(DashboardBackgrounds.None, loaded.Dashboard.Background);
        Assert.Equal("rpm", loaded.Dashboard.Current().Items[0].Key);
    }

    [Fact]
    public void Material_jpegs_cover_and_cabin_files_are_gone()
    {
        Assert.False(DashboardBackdropPolicy.TileBuiltIns);
        Assert.Equal("UniformToFill", DashboardBackdropPolicy.CoverStretch);
        Assert.Equal((byte)0x48, DashboardBackdropPolicy.ScrimAlpha);
        Assert.Null(DashboardBackdropPolicy.FileFor(DashboardBackgrounds.None));
        Assert.Null(DashboardBackdropPolicy.FileFor(DashboardBackgrounds.Custom));
        Assert.Equal("wood-grain.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.WoodGrain));
        Assert.Equal("brushed-aluminum.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.BrushedAluminum));
        Assert.Equal("gloss-black.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.GlossBlack));
        Assert.Equal("diamond-plate.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.DiamondPlate));
        Assert.Equal("carbon-fiber.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.CarbonFiber));
        Assert.Equal("sports-leather.jpg", DashboardBackdropPolicy.FileFor(DashboardBackgrounds.SportsLeather));

        var names = DashboardBackgrounds.Choices.Select(c => c.Name).ToList();
        Assert.Equal(new[] { "None (app theme)", "Fine wood grain", "Brushed aluminum", "High-gloss black", "Diamond-plate metal", "Carbon fiber", "Sports-car leather", "Custom image" }, names);

        var dir = Path.Combine(Root(), "assets", "backgrounds");
        foreach (var gone in new[] { "analog.jpg", "digital-dash.jpg", "digital-cluster.jpg", "highway.jpg", "night.jpg", "leather.jpg", "dusk.jpg", "center-stack.jpg" })
            Assert.False(File.Exists(Path.Combine(dir, gone)), gone);
        foreach (var file in new[] { "wood-grain.jpg", "brushed-aluminum.jpg", "gloss-black.jpg", "diamond-plate.jpg", "carbon-fiber.jpg", "sports-leather.jpg" })
        {
            var path = Path.Combine(dir, file);
            Assert.True(File.Exists(path), path);
            var (w, h) = JpegSize(path);
            Assert.True(w >= 1920 && h >= 1080, $"{file} is {w}x{h}");
            Assert.InRange(new FileInfo(path).Length, 40_000, 1_500_000);
        }
        var prov = File.ReadAllText(Path.Combine(dir, "PROVENANCE.txt"));
        Assert.Contains("generated", prov, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT", prov);
        Assert.Contains("wood-grain.jpg", prov);
        Assert.Contains("brushed-aluminum.jpg", prov);
        Assert.Contains("gloss-black.jpg", prov);
        Assert.Contains("diamond-plate.jpg", prov);
        Assert.Contains("carbon-fiber.jpg", prov);
        Assert.Contains("sports-leather.jpg", prov);
        Assert.Contains("material surfaces", prov);
        Assert.Contains("not photographs of a vehicle cabin", prov);
        var backdrop = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "DashboardBackdrop.cs"));
        Assert.Contains("TileMode.None", backdrop);
        Assert.Contains("UniformToFill", backdrop);
        Assert.DoesNotContain("TileMode.Tile", backdrop);
    }

    [Fact]
    public void Face_defaults_to_analog_and_unknown_values_do_not_drop_the_settings_file()
    {
        var path = Path.Combine(Path.GetTempPath(), "v060-face-" + Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, """{"Dashboard":{"Layouts":[{"Name":"Default","Items":[{"Key":"rpm","Display":"Dial","Size":"Large"},{"Key":"speed","Display":"Dial","Size":"Large","Face":"Digital"},{"Key":"fuel","Display":"Bar","Face":"nope"}]}]}}""");
        var loaded = AppSettings.Load(path);
        var items = loaded.Dashboard.Current().Items;
        Assert.Equal(GaugeFaces.Analog, items[0].Face);
        Assert.Equal(GaugeFaces.Digital, items[1].Face);
        Assert.Equal(GaugeFaces.Analog, items[2].Face);
        Assert.Equal("rpm", items[0].Key);
        var clone = items[1].Clone();
        Assert.Equal(GaugeFaces.Digital, clone.Face);
        Assert.True(GaugeFaces.ShowsNeedle(DisplayKind.Dial, GaugeFaces.Analog));
        Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Dial, GaugeFaces.Digital));
        Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Bar, GaugeFaces.Analog));
        Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Tile, GaugeFaces.Digital));
        Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Lamp, null));
        Assert.False(GaugeFaces.ShowsNeedle(DisplayKind.Tpms, GaugeFaces.Analog));
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
            if (marker is 0xD8 or 0xD9 or (>= 0xD0 and <= 0xD7)) continue;
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
