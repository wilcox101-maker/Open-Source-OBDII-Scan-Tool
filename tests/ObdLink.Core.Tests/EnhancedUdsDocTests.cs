using ObdLink.Core.Docs;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>docs/ENHANCED-UDS.md carries tables generated from the code. They must match; UPDATE_DOCS=1 rewrites them.</summary>
public class EnhancedUdsDocTests
{
    private static string DocPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "docs", "ENHANCED-UDS.md");
    }

    [Fact]
    public void Generated_tables_match_the_code()
    {
        var path = DocPath();
        var doc = File.ReadAllText(path).Replace("\r\n", "\n");
        var gen = EnhancedUdsTables.Markdown();
        int b = doc.IndexOf(EnhancedUdsTables.Begin, StringComparison.Ordinal);
        int e = doc.IndexOf(EnhancedUdsTables.End, StringComparison.Ordinal);
        string current = b >= 0 && e > b ? doc[b..(e + EnhancedUdsTables.End.Length)] + "\n" : "";
        if (Environment.GetEnvironmentVariable("UPDATE_DOCS") == "1" && current != gen)
        {
            doc = b >= 0 && e > b ? doc[..b] + gen + doc[(e + EnhancedUdsTables.End.Length + 1)..] : doc.TrimEnd('\n') + "\n\n" + gen;
            File.WriteAllText(path, doc);
            return;
        }
        Assert.True(current == gen, "docs/ENHANCED-UDS.md is out of date with the code. Run: UPDATE_DOCS=1 dotnet test tests/ObdLink.Core.Tests --filter EnhancedUdsDocTests");
    }

    [Fact]
    public void Tables_carry_the_safety_facts()
    {
        var md = EnhancedUdsTables.Markdown();
        Assert.Contains("| 0xE200-0xE2FF | PyrotechnicDeploy | **refused**", md);
        Assert.Contains("| 0xFF00 | Programming | **refused**", md);
        Assert.Contains("| 0x2E | WriteDataByIdentifier | yes |", md);
        Assert.Contains("| 0x22 | ReadDataByIdentifier |  |", md);
        Assert.Contains("| 0x78 | requestCorrectlyReceived-ResponsePending |", md);
        Assert.Contains("| BCM | Body Control Module | HS-CAN | 0x726 | 0x72E |", md);
        Assert.Contains("`27 LL+1 <key>`", md);                       // security access steps
        Assert.Contains("| 0x08 | confirmedDTC | confirmed (stored) |", md);
        Assert.Contains("| `85 01` | DTC setting ON (restore) |", md);
        Assert.Contains("| `10 02` | Programming |", md);
        Assert.Contains("| S3server | 5000 ms |", md);
        Assert.DoesNotContain("1FT", md); // no VIN fragments in a public doc
    }
}
