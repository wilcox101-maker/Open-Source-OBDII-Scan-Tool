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

}
