using ObdLink.Core.AsBuilt;
using Xunit;
namespace ObdLink.Core.Tests;

public class AbtTests
{
    static string Fx(params string[] p) => Path.Combine(new[] { AppContext.BaseDirectory, "Fixtures" }.Concat(p).ToArray());
    static ForscanAbtFile Abt(string n) => ForscanAbtFile.Load(Fx("abt", $"dale-{n}.redacted.abt"));
    static FordAbFile Ab() => FordAbFile.Load(Fx("ab", "dale-f150-2017.redacted.ab"));

    [Theory]
    [InlineData("G1", 0x01)] [InlineData("G9", 0x09)] [InlineData("GA", 0x10)] [InlineData("GF", 0x15)]
    [InlineData("H0", 0x16)] [InlineData("HA", 0x26)] [InlineData("J4", 0x52)]
    public void Code_is_ordinal_mapped_to_bcd(string code, byte bcd)
    {
        Assert.Equal(bcd, ForscanAbtFile.DecodeCode(code));
        Assert.Equal(code, ForscanAbtFile.EncodeCode(bcd));
    }

    [Fact]
    public void Bcd_helpers_and_did_mapping()
    {
        Assert.Equal(0x10, AsBuiltAddress.ToBcd(10));
        Assert.Equal(52, AsBuiltAddress.FromBcd(0x52));
        Assert.Throws<FormatException>(() => AsBuiltAddress.FromBcd(0x0A));
        Assert.Equal(0xDE00, AsBuiltBlock.DidForBlock(0x01));
        Assert.Equal(0xDE09, AsBuiltBlock.DidForBlock(0x10)); // 10th block
        var b = new AsBuiltBlock { Module = 0x7E0, Block = 1, Data = new byte[60], BytesPerLine = 5 };
        Assert.Equal(0x12, b.Lines().Last().Address.Line);
    }

    [Theory]
    [InlineData("737-rcm", 0x737, 12)] [InlineData("7d0-apim", 0x7D0, 5)] [InlineData("726-bcm", 0x726, 148)]
    [InlineData("7a5-fcdim", 0x7A5, 4)] [InlineData("7a7-fcim", 0x7A7, 8)] [InlineData("7e0-pcm", 0x7E0, 101)]
    public void Parses_with_valid_checksums_and_round_trips(string n, int module, int lines)
    {
        var f = Abt(n);
        Assert.Equal(module, f.Module);
        Assert.Equal(lines, f.Lines.Count);
        Assert.All(f.Lines, l => Assert.True(l.ChecksumValid, l.Format()));
        var text = File.ReadAllText(Fx("abt", $"dale-{n}.redacted.abt"));
        Assert.Equal(text.Replace("\r\n", "\n").TrimEnd(), f.ToText().Replace("\r\n", "\n").TrimEnd());
        Assert.DoesNotContain("1FTEX", System.Text.Encoding.ASCII.GetString(f.Lines.SelectMany(l => l.Data).ToArray()));
    }

    [Fact]
    public void Rcm_skips_block_7_and_has_blocks_missing_from_factory()
    {
        var f = Abt("737-rcm");
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 8, 9 }, f.Lines.Select(l => l.Address.Block).Distinct());
        Assert.Equal(7, f.BlockIndex[new AsBuiltAddress(0x737, 8, 1)]);
        var d = AsBuiltDiff.Compare(Ab().Lines, f.Lines, 0x737);
        Assert.Equal(10, d.Count(x => x.Kind == DiffKind.Same));
        Assert.Equal(new[] { "737-08-01", "737-09-01" }, d.Where(x => x.Kind == DiffKind.NotInFactory).Select(x => x.Address.ToString()));
    }

    [Fact]
    public void Redaction_masks_vin_across_blocks()
    {
        var bcm = Abt("726-bcm");
        var s = System.Text.Encoding.ASCII.GetString(bcm.Lines.SelectMany(l => l.Data).ToArray());
        Assert.Contains("XXXXXXXXXXXE25268", s);
        var fake = ForscanAbtFile.Parse(ForscanAbtFile.Format(new[] { AsBuiltLine.Create(new(0x7A7, 1, 1), "1ABCD"u8.ToArray()), AsBuiltLine.Create(new(0x7A7, 2, 1), "EFGH01E25268"u8.ToArray()) }));
        var r = fake.Redacted("1ABCDEFGH01E25268");
        Assert.Equal("XXXXXXXXXXXE25268", System.Text.Encoding.ASCII.GetString(r.Lines.SelectMany(l => l.Data).ToArray()));
        Assert.All(r.Lines, l => Assert.True(l.ChecksumValid));
    }

    [Theory]
    [InlineData("7a7-fcim", 0x7A7)] [InlineData("7a5-fcdim", 0x7A5)]
    public void Unmodified_modules_match_factory(string n, ushort m) =>
        Assert.All(AsBuiltDiff.Compare(Ab().Lines, Abt(n).Lines, m), x => Assert.Equal(DiffKind.Same, x.Kind));

    [Fact]
    public void Apim_and_bcm_changes_decode()
    {
        var ab = Ab(); var json = File.ReadAllText(Fx("community", "features.json"));
        var apim = AsBuiltDiff.Compare(ab.Lines, Abt("7d0-apim").Lines, 0x7D0).Where(x => x.Kind != DiffKind.Same).ToList();
        Assert.Equal("7D0-01-01", Assert.Single(apim).Address.ToString());
        var bcm = Assert.Single(AsBuiltDiff.Compare(ab.Lines, Abt("726-bcm").Lines, 0x726), x => x.Kind != DiffKind.Same);
        Assert.Equal("726-18-02", bcm.Address.ToString());
        string Meaning(IEnumerable<AsBuiltLine> l) => FeatureDecoder.Decode(l.Where(x => x.Address.ToString() == "726-18-02"), json)
            .First(x => x.Tab == "BCM (BdyCM)-old" && x.Description.Contains("Stage Unlock") && x.Pattern == "xxxxxxxxx*xx").Meaning;
        Assert.Equal("two stage", Meaning(ab.Lines));
        Assert.Equal("one stage", Meaning(Abt("726-bcm").Lines));
        string Dsp(IEnumerable<AsBuiltLine> l) => FeatureDecoder.Decode(l.Where(x => x.Address.ToString() == "7D0-01-01"), json)
            .First(x => x.Tab == "APIM Sync 3" && x.Description.StartsWith("Smart DSP")).Meaning;
        Assert.StartsWith("DSP Disabled", Dsp(ab.Lines));
        Assert.StartsWith("DSP Enabled", Dsp(Abt("7d0-apim").Lines));
    }

    [Fact]
    public void Pcm_has_no_addressed_factory_counterpart()
    {
        var d = AsBuiltDiff.Compare(Ab().Lines, Abt("7e0-pcm").Lines, 0x7E0);
        Assert.All(d, x => Assert.Equal(DiffKind.NotInFactory, x.Kind));
        Assert.Equal(new[] { 26, 52, 18, 5 }, Abt("7e0-pcm").Lines.GroupBy(l => l.Address.Block).Select(g => g.Count()));
    }

    [Fact]
    public void Factory_fixture_bcm_vin_split_across_blocks_is_masked()
    {
        var data = Ab().Lines.Where(l => l.Address.Module == 0x726).OrderBy(l => l.Address.Block).ThenBy(l => l.Address.Line).SelectMany(l => l.Data).ToArray();
        var s = System.Text.Encoding.ASCII.GetString(data);
        Assert.Contains("XXXXXXXXXXXE25268", s);
        Assert.DoesNotContain("1FTEX", s);
    }
}
