using ObdLink.Core.AsBuilt;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.15.0: As-Built editing by option name (community sheet rows with '*' digits), checksum recompute, conflicts.</summary>
public class AsBuiltOptionEditorTests
{
    private const string Json = """
    {"entries":[
      {"sheet":"APIM Sync 1","address":"7D0-01-01","pattern":["*xxx","xxxx","xx--"],"description":"Brand, ICAN","values":["0=Ford, ICAN Off","1=Ford, ICAN On","8=Lincoln, ICAN Off"]},
      {"sheet":"APIM Sync 1","address":"7D0-01-01","pattern":["xx**","xxxx","xxxx"],"description":"Clock: 00=12h, 01=24h","values":[]},
      {"sheet":"APIM Sync 1","address":"7D0-01-01","pattern":["*x**","xxxx","xxxx"],"description":"Combined","values":["000=a","1FF=b","12=wrong length"]},
      {"sheet":"APIM Sync 1","address":"7D0-01-01","pattern":["xxxx","xxxx","xx*x"],"description":"checksum digit","values":["0=x"]},
      {"sheet":"APIM Sync 1","address":"7D0-01-02","pattern":["xxx7","xxxx","xxxx"],"description":"recipe","values":[]},
      {"sheet":"BCM","address":"726-01-01","pattern":["x*xx","xxxx","xxxx"],"description":"Something","values":["0=off","1=on"]},
      {"sheet":"BCM","address":"726-01-01","pattern":["x*xx","xxxx","xxxx"],"description":"Something","values":["0=off"]},
      {"sheet":"Bad","address":"not-an-address","pattern":["*"],"description":"","values":[]}
    ]}
    """;

    private static AsBuiltLine L(string addr, string dataHex) => AsBuiltLine.Create(AsBuiltAddress.Parse(addr), Convert.FromHexString(dataHex));

    [Fact]
    public void Loads_only_editable_rows()
    {
        var opts = FeatureOption.Load(Json);
        Assert.Equal(4, opts.Count); // 7D0 brand, clock, combined, BCM once (duplicate row dropped); recipe, checksum and bad address skipped
        var brand = opts.Single(o => o.Description == "Brand, ICAN");
        Assert.Equal(new[] { 0 }, brand.Positions);
        Assert.Equal(new[] { "0", "1", "8" }, brand.Values.Select(v => v.Digits));
        var clock = opts.Single(o => o.Description.StartsWith("Clock"));
        Assert.Equal(new[] { "00", "01" }, clock.Values.Select(v => v.Digits)); // from the description
        var combined = opts.Single(o => o.Description == "Combined");
        Assert.Equal(new[] { "000", "1FF" }, combined.Values.Select(v => v.Digits)); // "12" has the wrong length
    }

    [Fact]
    public void Current_value_and_meaning()
    {
        var brand = FeatureOption.Load(Json).Single(o => o.Description == "Brand, ICAN");
        var line = L("7D0-01-01", "8A2A050220");
        Assert.Equal("8", brand.Current(line));
        Assert.Equal("Lincoln, ICAN Off", brand.MeaningOf("8"));
        Assert.Null(brand.MeaningOf("F"));
    }

    [Fact]
    public void Apply_changes_only_the_option_digits_and_fixes_the_checksum()
    {
        var opts = FeatureOption.Load(Json);
        var line = L("7D0-01-01", "2A2A050220");
        var changed = AsBuiltEditor.Apply(line, opts.Single(o => o.Description == "Brand, ICAN"), "1");
        Assert.Equal("1A2A050220", Convert.ToHexString(changed.Data));
        Assert.True(changed.ChecksumValid);
        Assert.NotEqual(line.Checksum, changed.Checksum);
    }

    [Fact]
    public void Several_edits_on_one_line_and_untouched_lines_are_returned_as_is()
    {
        var opts = FeatureOption.Load(Json);
        var a = L("7D0-01-01", "2A2A050220");
        var b = L("7D0-01-02", "0000000000");
        var res = AsBuiltEditor.ApplyAll(new[] { a, b }, new[]
        {
            new OptionEdit(opts.Single(o => o.Description == "Brand, ICAN"), "8"),
            new OptionEdit(opts.Single(o => o.Description.StartsWith("Clock")), "01"),
        });
        Assert.Equal("8A01050220", Convert.ToHexString(res[0].Data));
        Assert.Same(b, res[1]);
    }

    [Fact]
    public void Same_digit_set_twice_the_same_way_is_fine_but_a_conflict_is_refused()
    {
        var opts = FeatureOption.Load(Json);
        var line = L("7D0-01-01", "2A2A050220");
        var brand = opts.Single(o => o.Description == "Brand, ICAN");
        var combined = opts.Single(o => o.Description == "Combined");
        AsBuiltEditor.ApplyAll(new[] { line }, new[] { new OptionEdit(brand, "1"), new OptionEdit(combined, "1FF") });
        var ex = Assert.Throws<InvalidOperationException>(() =>
            AsBuiltEditor.ApplyAll(new[] { line }, new[] { new OptionEdit(brand, "8"), new OptionEdit(combined, "1FF") }));
        Assert.Contains("Conflicting", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12")]
    [InlineData("G")]
    public void Bad_values_are_refused(string digits)
    {
        var brand = FeatureOption.Load(Json).Single(o => o.Description == "Brand, ICAN");
        Assert.Throws<FormatException>(() => AsBuiltEditor.Apply(L("7D0-01-01", "2A2A050220"), brand, digits));
    }

    [Fact]
    public void Missing_or_short_lines_are_refused()
    {
        var opts = FeatureOption.Load(Json);
        var clock = opts.Single(o => o.Description.StartsWith("Clock"));
        Assert.Throws<InvalidOperationException>(() => AsBuiltEditor.ApplyAll(new[] { L("7D0-01-02", "0000") }, new[] { new OptionEdit(clock, "01") }));
        Assert.Throws<InvalidDataException>(() => AsBuiltEditor.Apply(L("7D0-01-01", "2A"), clock, "01"));
    }

    [Fact]
    public void Edited_lines_round_trip_through_the_write_plan()
    {
        var opts = FeatureOption.Load(Json);
        var block = new AsBuiltBlock { Module = 0x7D0, Block = 0x01, Data = Convert.FromHexString("2A2A05022011223344550A0B0C0D0E") };
        var edited = AsBuiltEditor.ApplyAll(block.Lines().ToList(), new[] { new OptionEdit(opts.Single(o => o.Description == "Brand, ICAN"), "1") });
        var plan = AsBuiltWritePlan.BuildBlock(0x7D0, 0x01, edited.Select(l => new AsBuiltEditLine(l.Address, Convert.ToHexString(l.Data), l.Checksum.ToString("X2"))));
        Assert.Equal("1A2A05022011223344550A0B0C0D0E", Convert.ToHexString(plan.Data));
        Assert.Equal(1, AsBuiltWritePlan.CountChanges(block.Data, plan.Data));
    }

    [Fact]
    public void Shipped_sheet_loads_and_decodes_a_known_apim_line()
    {
        var opts = FeatureOption.LoadF150_2015_2020();
        Assert.True(opts.Count > 1000);
        Assert.All(opts, o => Assert.True(o.Positions.Count > 0));
        // Same community example as AsBuiltLine's checksum notes: 7D0-01-01 2A2A 0502 20 54.
        var line = AsBuiltLine.Parse("7D0-01-01 2A2A 0502 2054");
        Assert.True(line.ChecksumValid);
        var first = opts.First(o => o.Address == "7D0-01-01" && o.Positions.SequenceEqual(new[] { 0 }) && o.Values.Count > 0);
        Assert.Equal("2", first.Current(line));
        var changed = AsBuiltEditor.Apply(line, first, first.Values[0].Digits);
        Assert.True(changed.ChecksumValid);
    }
}
