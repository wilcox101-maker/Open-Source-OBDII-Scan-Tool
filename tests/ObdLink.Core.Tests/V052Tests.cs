using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.5.2: cabin photographs replace tiled drawing brushes. Analog and digital clusters are both built in.</summary>
public class V052Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    /// <summary>Version-agnostic since v0.6.1: App and Core agree, and the release notes and README lead with that version.</summary>
    [Fact]
    public void Versions_agree_and_notes_lead_with_current()
    {
        static string Ver(string csproj) => System.Text.RegularExpressions.Regex.Match(File.ReadAllText(csproj), "<Version>([^<]+)</Version>").Groups[1].Value;
        var app = Ver(Path.Combine(Root(), "src", "ObdLink.App", "ObdLink.App.csproj"));
        Assert.Matches(@"^\d+\.\d+\.\d+$", app);
        Assert.Equal(app, Ver(Path.Combine(Root(), "src", "ObdLink.Core", "ObdLink.Core.csproj")));
        var notes = File.ReadAllText(Path.Combine(Root(), "docs", "RELEASE-NOTES.md"));
        Assert.StartsWith($"## v{app}", notes.TrimStart());
        Assert.DoesNotContain($"v{app}-test", notes);
        Assert.Contains($"v{app}", File.ReadAllText(Path.Combine(Root(), "README.md")));
        Assert.Contains("Assembly.GetName().Version", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainViewModel.cs")));
    }

    [Fact]
    public void V060_notes_are_kept()
    {
        var notes = File.ReadAllText(Path.Combine(Root(), "docs", "RELEASE-NOTES.md"));
        int a = notes.IndexOf("## v0.6.0", StringComparison.Ordinal), b = notes.IndexOf("## v0.5.2", StringComparison.Ordinal);
        Assert.True(a >= 0 && b > a);
        var v060 = notes[a..b];
        Assert.Contains("ObdLinkApp-win-x64-v0.6.0.zip", v060);
        Assert.Contains("ObdLinkApp-ARM64-only-v0.6.0.zip", v060);
        Assert.Contains("Keep live", v060);
        Assert.Contains("Fine wood grain", v060);
        Assert.Contains("not tiled", v060);
        Assert.Contains("generated", v060, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT", v060);
        Assert.Contains("ARM64 zip was built and was not run on ARM64 hardware", v060);
    }
}
