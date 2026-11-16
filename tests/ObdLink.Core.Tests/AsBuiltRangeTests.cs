using ObdLink.Core.AsBuilt;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// KB-R2: As-Built blocks above 99 (FORScan shows PCM 7E0-162-01 exists on some vehicles) can't be stored as BCD bytes and their
/// checksum rule is unknown. They must fail with a clear, typed error, and file imports skip and report them instead of aborting.
/// </summary>
public class AsBuiltRangeTests
{
    [Fact]
    public void A_three_digit_block_is_a_clear_range_error_not_an_overflow()
    {
        var ex = Assert.Throws<AsBuiltRangeException>(() => AsBuiltAddress.Parse("7E0-162-01"));
        Assert.Contains("above 99", ex.Message);
        Assert.Contains("7E0-162-01", ex.Message);
        Assert.IsAssignableFrom<FormatException>(ex); // existing "bad input" handling still catches it
        Assert.Throws<AsBuiltRangeException>(() => AsBuiltLine.Parse("7E0-162-01 0102 0304 05FF"));
        Assert.Throws<AsBuiltRangeException>(() => AsBuiltAddress.ToBcd(100));
        Assert.Throws<ArgumentOutOfRangeException>(() => AsBuiltAddress.ToBcd(-1));
        Assert.Equal(0x99, AsBuiltAddress.ToBcd(99));
        Assert.Equal(new AsBuiltAddress(0x7E0, 0x16, 0x01), AsBuiltAddress.Parse("7E0-16-01")); // two digits still work
    }

    [Fact]
    public void An_abt_with_a_block_above_99_imports_the_rest_and_reports_the_skipped_line()
    {
        var good = AsBuiltLine.Create(new AsBuiltAddress(0x7E0, 0x01, 0x01), new byte[] { 1, 2, 3, 4, 5 });
        var text = ForscanAbtFile.Format(new[] { good })
                 + ";Block 2\r\n7E0M4G10102AA\r\n";       // M4 = ordinal 100: can't be represented
        var abt = ForscanAbtFile.Parse(text);
        var line = Assert.Single(abt.Lines);
        Assert.Equal(good.Address, line.Address);
        var skipped = Assert.Single(abt.Skipped);
        Assert.StartsWith("7E0M4G10102AA", skipped);
        Assert.Contains("above 99", skipped);
    }

    [Fact]
    public void A_factory_ab_with_a_block_above_99_imports_the_rest_and_reports_the_skipped_line()
    {
        const string Xml = "<AS_BUILT_DATA><VEHICLE><VIN>1FTEW1EP7HFA00000</VIN>"
            + "<BCE_MODULE>"
            + "<DATA LABEL=\"7E0-01-01\"><CODE>0102</CODE><CODE>0304</CODE><CODE>05FF</CODE></DATA>"
            + "<DATA LABEL=\"7E0-162-01\"><CODE>0102</CODE><CODE>0304</CODE><CODE>05FF</CODE></DATA>"
            + "</BCE_MODULE></VEHICLE></AS_BUILT_DATA>";
        var ab = FordAbFile.Parse(Xml);
        Assert.Equal(new AsBuiltAddress(0x7E0, 0x01, 0x01), Assert.Single(ab.Lines).Address);
        Assert.Contains("7E0-162-01", Assert.Single(ab.Skipped));
    }

    [Fact]
    public void The_app_reports_skipped_lines_on_both_imports()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var vm = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.App", "AsBuiltPageViewModel.cs"));
        Assert.Contains("foreach (var skip in ab.Skipped)", vm);
        Assert.Contains("foreach (var skip in abt.Skipped)", vm);
    }
}
