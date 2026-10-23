using System.Text.Json;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Modules;
using Xunit;
namespace ObdLink.Core.Tests;
public class CommunitySheet0914Tests
{
    static JsonElement Load(string f) => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "community-0914", f))).RootElement;

    [Fact]
    public void All_complete_lines_checksum_valid()
    {
        var lines = Load("complete-lines.json").GetProperty("lines").EnumerateArray().Select(e => e.GetProperty("line").GetString()!).ToList();
        Assert.Equal(116, lines.Count);
        Assert.All(lines, l => Assert.True(AsBuiltLine.Parse(l).ChecksumValid, l));
    }

    [Theory]
    [InlineData("features.json")]
    [InlineData("complete-lines.json")]
    [InlineData("module-addresses.json")]
    public void Has_provenance(string f)
    {
        var r = Load(f);
        Assert.Equal("CommunityReported", r.GetProperty("confidence").GetString());
        Assert.Contains("15MQ8CEh7eSXUJYjPg-J8uUqpLwP_xyqbfQHB0rAnXNs", r.GetProperty("source_url").GetString());
        Assert.Contains("VINs", r.GetProperty("skipped_tabs").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact]
    public void Every_record_has_tab()
    {
        foreach (var e in Load("features.json").GetProperty("entries").EnumerateArray())
            Assert.False(string.IsNullOrEmpty(e.GetProperty("tab").GetString()));
    }

    [Fact]
    public void No_full_vins_committed()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "community-0914");
        foreach (var f in Directory.GetFiles(dir, "*.json"))
            Assert.DoesNotMatch(@"\b[1-5][A-HJ-NPR-Z0-9]{16}\b", File.ReadAllText(f));
    }

    [Fact]
    public void Tccm_and_rtm_addresses_from_community_remain_unverified()
    {
        var t = FordF150_2017.Modules.Single(m => m.Acronym == "TCCM");
        Assert.Equal(0x761u, t.TxId); Assert.Equal(Confidence.Unverified, t.Confidence);
        Assert.Equal(0x751u, FordF150_2017.Modules.Single(m => m.Acronym == "RTM").TxId);
    }
}
