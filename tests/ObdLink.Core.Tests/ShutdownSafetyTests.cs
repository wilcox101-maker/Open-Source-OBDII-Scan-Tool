using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.22.3: the app can't be closed mid-flash, and held dashboard outputs are returned before it exits. Source scans (WPF).</summary>
public class ShutdownSafetyTests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string f = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));
    private static string App(string file) => File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", file));

    [Fact]
    public void Closing_is_refused_while_flashing_and_waits_for_held_outputs()
    {
        var w = App("MainWindow.xaml.cs");
        int closing = w.IndexOf("protected override void OnClosing", StringComparison.Ordinal);
        Assert.True(closing > 0);
        var body = w[closing..w.IndexOf("private async Task ReleaseHoldsThenCloseAsync", closing, StringComparison.Ordinal)];
        int flash = body.IndexOf("_vm.Flash.Running", StringComparison.Ordinal), cancel = body.IndexOf("e.Cancel = true", StringComparison.Ordinal);
        int holds = body.IndexOf("HoldingOutputs", StringComparison.Ordinal), stop = body.IndexOf("DashboardPoller.Stop()", StringComparison.Ordinal);
        Assert.True(flash > 0 && cancel > flash, "the flash check must come first and cancel the close");
        Assert.True(holds > flash && stop > holds, "held outputs are handled before the windows close and polling stops");
        Assert.Contains("ReleaseAllAsync(\"app closing\")", w);
        Assert.Contains("Task.Delay(TimeSpan.FromSeconds(3))", w); // bounded: never hangs the exit
        Assert.Contains("Task.WhenAll(Items.Select(i => i.Action).OfType<DashActionCardVm>().Where(c => c.Holding).Select(c => c.ReleaseAsync(why)))", App("DashboardViewModel.cs"));
    }

    [Fact]
    public void Windows_shutdown_is_refused_while_flashing()
    {
        var a = App("App.xaml.cs");
        Assert.Contains("protected override void OnSessionEnding", a);
        Assert.Contains("Flash.Running: true", a);
        Assert.Contains("e.Cancel = true", a);
    }
}
