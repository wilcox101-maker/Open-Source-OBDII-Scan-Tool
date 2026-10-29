using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.5.2: cabin photographs replace tiled drawing brushes. Analog and digital clusters are both built in.</summary>
public class V052Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    [Fact]
    public void Version_is_0_6_0()
    {
        Assert.Contains("<Version>0.6.0</Version>", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "ObdLink.App.csproj")));
        Assert.Contains("<Version>0.6.0</Version>", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.Core", "ObdLink.Core.csproj")));
        var notes = File.ReadAllText(Path.Combine(Root(), "docs", "RELEASE-NOTES.md"));
        Assert.StartsWith("## v0.6.0", notes.TrimStart());
        Assert.Contains("## v0.5.2", notes);
        var v060 = notes[..notes.IndexOf("## v0.5.2", StringComparison.Ordinal)];
        Assert.Contains("ObdLinkApp-win-x64-v0.6.0.zip", v060);
        Assert.Contains("ObdLinkApp-ARM64-only-v0.6.0.zip", v060);
        Assert.Contains("Keep live", v060);
        Assert.Contains("Fine wood grain", v060);
        Assert.Contains("not tiled", v060);
        Assert.Contains("generated", v060, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MIT", v060);
        Assert.Contains("ARM64 zip was built and was not run on ARM64 hardware", v060);
        Assert.DoesNotContain("v0.6.0-test", notes);
        var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
        Assert.Contains("v0.6.0", readme);
        var about = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainViewModel.cs"));
        Assert.Contains("Assembly.GetName().Version", about);
    }

}
