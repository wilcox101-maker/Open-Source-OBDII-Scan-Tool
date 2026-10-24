using System.Text;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Modules;
using Xunit;
namespace ObdLink.Core.Tests;

public class FordAbTests
{
    static string Fx(params string[] p) => Path.Combine(new[] { AppContext.BaseDirectory, "Fixtures" }.Concat(p).ToArray());
    static FordAbFile Ab() => FordAbFile.Load(Fx("ab", "dale-f150-2017.redacted.ab"));
    static readonly ushort[] AbModules = { 0x701, 0x716, 0x720, 0x724, 0x726, 0x727, 0x730, 0x737, 0x740, 0x741, 0x751, 0x760, 0x7A5, 0x7A7, 0x7D0 };
    static readonly ushort[] NodeOnly = { 0x756, 0x761, 0x765, 0x7E0 };

    [Fact]
    public void Parses_redacted_fixture()
    {
        var f = Ab();
        Assert.Equal("XXXXXXXXXXX000000", f.Vin);
        Assert.Equal(295, f.Lines.Count);
        Assert.Equal(9, f.PcmLines.Count);
        Assert.Equal(19, f.Nodes.Count);
        Assert.Equal(("614", "CCC DATA NOT FOUND"), Assert.Single(f.Errors));
    }

    [Fact]
    public void Every_line_checksum_valid()
    {
        var f = Ab();
        Assert.All(f.Lines, l => Assert.True(l.ChecksumValid, l.Format()));
        Assert.All(f.PcmLines, p => Assert.True(FordAbFile.PcmChecksumValid(p.Label, p.Bytes), p.Label));
    }

    [Fact]
    public void Module_set()
    {
        var f = Ab();
        Assert.Equal(AbModules, f.AsBuiltModules);
        Assert.Equal(AbModules.Concat(NodeOnly).OrderBy(x => x), f.AllModules);
    }

    [Theory]
    [InlineData(0x726, 70, 148)] [InlineData(0x720, 9, 18)] [InlineData(0x740, 22, 27)] [InlineData(0x716, 15, 15)] [InlineData(0x7D0, 3, 5)]
    public void Blocks_and_lines_per_module(int m, int blocks, int lines)
    {
        var f = Ab();
        Assert.Equal(blocks, f.Blocks((ushort)m).Count());
        Assert.Equal(lines, f.Lines.Count(l => l.Address.Module == m));
    }

    [Fact]
    public void Block_numbers_are_hex_and_skip_0A_to_0F()
    {
        // The labels go 09 then 10. The checksums only validate when "10" is read as 0x10, so the labels are hex.
        var b = Ab().Blocks(0x726).ToList();
        Assert.Contains((byte)0x10, b); Assert.DoesNotContain((byte)0x0A, b);
    }

    [Fact]
    public void Redaction_removes_vin_from_blocks_and_keeps_checksums()
    {
        var text = File.ReadAllText(Fx("ab", "dale-f150-2017.redacted.ab"));
        Assert.DoesNotContain("1FTEX1", text);
        var f = Ab();
        var vinBlock = Encoding.ASCII.GetString(f.Lines.Where(l => l.Address.Module == 0x737 && l.Address.Block == 1).SelectMany(l => l.Data).ToArray());
        Assert.Equal("XXXXXXXXXXX000000", vinBlock);
        Assert.All(f.Nodes.Values, n => Assert.All(FordAbFile.SerialDids, d => Assert.True(!n.ContainsKey(d) || n[d] == "REDACTED")));
        var again = f.Redacted();
        Assert.All(again.Lines, l => Assert.True(l.ChecksumValid));
    }

    [Fact]
    public void Round_trips_through_xml()
    {
        var f = Ab(); var g = FordAbFile.Parse(f.ToXml());
        Assert.Equal(f.Lines.Select(l => l.Format()), g.Lines.Select(l => l.Format()));
        Assert.Equal(f.PcmLines.Count, g.PcmLines.Count);
        Assert.Equal(f.Nodes.Keys, g.Nodes.Keys);
    }

    [Fact]
    public void Decodes_against_community_db()
    {
        var d = FeatureDecoder.Decode(Ab().Lines, File.ReadAllText(Fx("community", "features.json")));
        Assert.Equal("3.5L PFI", d.First(x => x.Address == "760-02-01" && x.Tab == "ABS (2015-2017)" && x.Description.StartsWith("Engine")).Meaning);
        Assert.Equal("3.73", d.First(x => x.Address == "726-12-01" && x.Tab == "BCM (BdyCM)-old" && x.Description.StartsWith("Axle Ratio")).Meaning);
        Assert.Equal("6R80 [MFAL TR-C3 (6spd Auto Trans)]", d.First(x => x.Address == "760-02-02" && x.Description.StartsWith("Transmission")).Meaning);
    }

    [Fact]
    public void Compare_to_factory()
    {
        var f = Ab();
        var raw = f.Lines.Where(l => l.Address.Module == 0x720 && l.Address.Block == 1).SelectMany(l => l.Data).ToArray();
        raw[0] ^= 0x80;
        var cur = f.SplitLike(0x720, 1, raw.Concat(new byte[] { 0xAA }).ToArray());
        Assert.Equal(new[] { 5, 3, 1 }, cur.Select(l => l.Data.Length));
        var diff = AsBuiltDiff.Compare(f.Lines.Where(l => l.Address.Block == 1), cur, 0x720);
        Assert.Equal(new[] { DiffKind.Changed, DiffKind.Same, DiffKind.NotInFactory }, diff.Select(x => x.Kind));
        Assert.Equal(DiffKind.MissingInCurrent, AsBuiltDiff.Compare(f.Lines, cur.Take(1), 0x720).Last().Kind);
    }

    [Fact]
    public void Module_table_marks_factory_ab_modules()
    {
        foreach (var id in AbModules.Concat(NodeOnly))
            Assert.Contains(FordF150_2017.Modules, m => m.TxId == id && m.InFactoryAb);
        Assert.DoesNotContain(FordF150_2017.Modules, m => m.Acronym == "4X4");
        Assert.DoesNotContain(FordF150_2017.Modules, m => m.Acronym is "HVAC" or "PAM" or "IPMA" or "TRM" && m.InFactoryAb);
        Assert.True(FordF150_2017.Modules.Single(m => m.Acronym == "BCM").SeenInLog);
    }
}
