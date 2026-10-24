using System.Security.Cryptography;
using System.Text;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Vehicles;
using Serilog;
using Serilog.Events;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.3.5: synthetic demo VIN everywhere, and empty catch blocks either log or explain themselves.</summary>
[Collection("GlobalSerilog")]
public class V035Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    // SHA-256 (first 16 hex) of every 5-character run of the real VIN, so the real VIN is not written in the repo.
    private static readonly HashSet<string> RealVinRuns = new()
    {
        "5a92f562119b4af8", "6202394f37eaba77", "71b3d092748860f1", "8404ee5a4784ad1e", "97f55d508921c148", "c34c64ffbabb267c", "d3d050926383efbc",
        "d755c1c653c681c3", "e7a9c39245cd0761", "e871a2bcc9f237f3", "ed79e6ecacf0a383", "f6256e958444db3b", "fdb7cc08ac36eb08",
    };
    private static string H5(string s) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(s)))[..16].ToLowerInvariant();

    // The old tail, built from character codes so the literal never appears in the source tree.
    private static string OldTail => new(new[] { (char)69, (char)50, (char)53, (char)50, (char)54, (char)56 });

    [Fact]
    public void Demo_vin_shares_no_five_char_run_with_the_real_vin()
    {
        foreach (var vin in new[] { SimulatedAdapter.DemoVin, new SimulatedAdapter().Vin, SimulatedAdapter.FromForscanLogs("").Vin, "XXXXXXXXXXX000000" })
            for (int i = 0; i + 5 <= vin.Length; i++)
                Assert.DoesNotContain(H5(vin.Substring(i, 5)), RealVinRuns);
        Assert.Contains(H5(OldTail[..5]), RealVinRuns); // the check itself works
    }

    [Fact]
    public void Log_based_simulator_reports_the_synthetic_vin()
        => Assert.Equal(SimulatedAdapter.DemoVin, SimulatedAdapter.FromForscanLogs("").Vin);

    [Fact]
    public void Old_profiles_with_the_previous_demo_vin_still_show_demo()
    {
        var old = new VehicleProfile { Vin = "XXXXXXXXXXX" + OldTail, DisplayName = "2017 Ford" };
        Assert.True(old.LooksDemo); Assert.StartsWith("[DEMO] ", old.Label);
        Assert.False(new VehicleProfile { Vin = "XXXXXXXXXXX000000" }.LooksDemo);
        Assert.True(new VehicleProfile { Vin = SimulatedAdapter.DemoVin }.LooksDemo);
    }

    [Fact]
    public void Old_vin_tail_is_nowhere_in_the_repo()
    {
        var hits = new List<string>();
        var bad = new[] { Encoding.ASCII.GetBytes(OldTail), Encoding.Unicode.GetBytes(OldTail), Encoding.ASCII.GetBytes(Convert.ToHexString(Encoding.ASCII.GetBytes(OldTail))) };
        foreach (var f in Directory.EnumerateFiles(Root(), "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(Root(), f).Replace('\\', '/');
            if (rel.Split('/').Any(p => p is ".git" or "bin" or "obj" or "real-logs" or "captures")) continue;
            var b = File.ReadAllBytes(f);
            if (bad.Any(x => b.AsSpan().IndexOf(x) >= 0)) hits.Add(rel);
        }
        Assert.Empty(hits);
    }

    [Fact]
    public void No_unexplained_empty_catch_blocks_in_src()
    {
        var bad = new List<string>();
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
            foreach (var (l, i) in File.ReadAllLines(f).Select((l, i) => (l, i)))
                if (System.Text.RegularExpressions.Regex.IsMatch(l, @"catch(\s*\([^)]*\))?\s*\{\s*\}")) bad.Add($"{Path.GetFileName(f)}:{i + 1}");
        Assert.Empty(bad);
    }

    private static List<LogEvent> Capture(Action a)
    {
        var events = new List<LogEvent>(); var prev = Log.Logger;
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(new ListSink(events)).CreateLogger();
        try { a(); } finally { Log.Logger = prev; }
        return events;
    }
    private sealed class ListSink(List<LogEvent> l) : Serilog.Core.ILogEventSink { public void Emit(LogEvent e) { lock (l) l.Add(e); } }

    [Fact]
    public void Unreadable_vehicle_profile_is_logged_not_hidden()
    {
        var d = Directory.CreateTempSubdirectory("v035").FullName;
        File.WriteAllText(Path.Combine(d, "broken.json"), "{ not json");
        List<VehicleProfile> got = null!;
        var ev = Capture(() => got = VehicleProfile.LoadAll(d));
        Assert.Empty(got);
        Assert.Contains(ev, e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("broken.json"));
    }

    [Fact]
    public void Corrupt_settings_are_logged_and_stay_read_only()
    {
        var p = Path.Combine(Directory.CreateTempSubdirectory("v035").FullName, "settings.json");
        File.WriteAllText(p, "{{{");
        AppSettings s = null!;
        var ev = Capture(() => s = AppSettings.Load(p));
        Assert.True(s.ReadOnlyTestMode);
        Assert.Contains(ev, e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("settings.json"));
    }

    [Fact]
    public void Trusted_site_refusals_and_explorer_failures_are_logged()
    {
        var vm = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainViewModel.cs"));
        Assert.DoesNotContain("OpenExternal(FirmwarePage); } catch { }", vm);
        Assert.Contains("Could not open the firmware page", vm);
        Assert.Equal(3, System.Text.RegularExpressions.Regex.Matches(vm, @"OpenExternal\(""explorer\.exe"".*?catch \(Exception ex\) \{ EventLog\.Message\(LogEventLevel\.Warning").Count);
    }
}

[CollectionDefinition("GlobalSerilog", DisableParallelization = true)]
public class GlobalSerilogCollection { }
