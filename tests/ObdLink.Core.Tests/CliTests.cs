using System.Text.Json;
using CliApp = ObdLink.Cli.Cli;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.17.0: obdcli against the simulator, argument handling, and the read-only guarantee.</summary>
public class CliTests
{
    private static async Task<(int Code, string Out, string Err)> Run(params string[] args)
    {
        var o = new StringWriter(); var e = new StringWriter();
        var code = await CliApp.RunAsync(args, o, e);
        return (code, o.ToString(), e.ToString());
    }

    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    [Fact]
    public async Task Info_json()
    {
        var (code, o, _) = await Run("info", "--demo", "--json");
        Assert.Equal(0, code);
        var j = JsonDocument.Parse(o).RootElement;
        Assert.Contains("STN", j.GetProperty("stn").GetString());
        Assert.True(j.GetProperty("msCan").GetBoolean());
    }

    [Fact]
    public async Task Ex_profile_and_flag_before_command()
    {
        var (code, o, _) = await Run("--json", "--demo", "ex", "info");
        Assert.Equal(0, code);
        Assert.Contains("EX", JsonDocument.Parse(o).RootElement.GetProperty("device").GetString());
    }

    [Fact]
    public async Task Scan_json_lists_responding_modules()
    {
        var (code, o, _) = await Run("scan", "--demo", "--json");
        Assert.Equal(0, code);
        var rows = JsonDocument.Parse(o).RootElement.EnumerateArray().ToList();
        Assert.Contains(rows, r => r.GetProperty("tx").GetString() == "7E0" && r.GetProperty("responded").GetBoolean());
    }

    [Fact]
    public async Task Dtcs_for_one_module()
    {
        var (code, o, _) = await Run("dtcs", "--demo", "--module", "726", "--json");
        Assert.Equal(0, code);
        var row = Assert.Single(JsonDocument.Parse(o).RootElement.EnumerateArray());
        Assert.Equal("726", row.GetProperty("tx").GetString());
        Assert.Equal("2F", row.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Live_uses_multi_pid_requests()
    {
        var (code, o, _) = await Run("live", "--demo", "--signals", "rpm,speed,throttle", "--seconds", "0.5", "--json");
        Assert.Equal(0, code);
        var j = JsonDocument.Parse(o).RootElement;
        Assert.True(j.GetProperty("batched").GetBoolean());
        Assert.True(j.GetProperty("requests").GetInt64() > 0);
        Assert.Contains(j.GetProperty("signals").EnumerateArray(), s => s.GetProperty("key").GetString() == "rpm" && s.GetProperty("samples").GetInt64() > 0);
    }

    [Fact]
    public async Task Monitor_summary_and_csv()
    {
        var dir = Path.Combine(Path.GetTempPath(), "obdcli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (code, o, _) = await Run("monitor", "--demo", "--bus", "ms", "--seconds", "0.5", "--csv", Path.Combine(dir, "x.csv"), "--json");
        Assert.Equal(0, code);
        var j = JsonDocument.Parse(o).RootElement;
        Assert.Equal("MsCan", j.GetProperty("bus").GetString());
        Assert.True(j.GetProperty("frames").GetInt64() > 0);
        Assert.Single(Directory.GetFiles(dir, "bus-MsCan-*.csv"));
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task Asbuilt_read_then_offline_options_edit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "obdcli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var abt = Path.Combine(dir, "726.abt");
        var (code, _, _) = await Run("asbuilt-read", "--demo", "--module", "726", "--out", abt);
        Assert.Equal(0, code);
        Assert.True(File.Exists(abt));

        var (lc, lo, _) = await Run("options", "--abt", abt, "--json");
        Assert.Equal(0, lc);
        var opt = JsonDocument.Parse(lo).RootElement.GetProperty("options").EnumerateArray().First(x => x.GetProperty("values").GetArrayLength() > 1);
        var addr = opt.GetProperty("address").GetString()!; var pat = opt.GetProperty("pattern").GetString()!; var now = opt.GetProperty("now").GetString()!;
        var other = opt.GetProperty("values").EnumerateArray().Select(v => v.GetString()!.Split(' ')[0]).First(d => d != now);

        var outAbt = Path.Combine(dir, "726-new.abt");
        var (sc, so, se) = await Run("options", "--abt", abt, "--set", $"{addr} {pat}={other}", "--out", outAbt);
        Assert.True(sc == 0, se);
        Assert.Contains("1 line(s) changed", so);
        var before = ObdLink.Core.AsBuilt.ForscanAbtFile.Load(abt).Lines; var after = ObdLink.Core.AsBuilt.ForscanAbtFile.Load(outAbt).Lines;
        Assert.All(after, l => Assert.True(l.ChecksumValid));
        Assert.Equal(1, before.Zip(after).Count(p => !p.First.Data.SequenceEqual(p.Second.Data)));

        var (oc, _, oe) = await Run("options", "--abt", abt, "--set", $"{addr} {pat}={other}", "--out", abt);
        Assert.Equal(2, oc);
        Assert.Contains("different file", oe);
        Directory.Delete(dir, true);
    }

    [Theory]
    [InlineData(new string[0], 2)]
    [InlineData(new[] { "help" }, 0)]
    [InlineData(new[] { "bogus", "--demo" }, 2)]
    [InlineData(new[] { "scan" }, 2)]                       // no connection given
    [InlineData(new[] { "dtcs", "--demo", "--module", "7E" }, 2)]
    [InlineData(new[] { "live", "--demo", "--signals", "warp" }, 2)]
    [InlineData(new[] { "monitor", "--demo", "--bus", "xx" }, 2)]
    [InlineData(new[] { "scan", "--port" }, 2)]             // option without its value
    [InlineData(new[] { "scan", "extra", "--demo" }, 2)]
    public async Task Usage_errors(string[] args, int expected) => Assert.Equal(expected, (await Run(args)).Code);

    [Fact]
    public void Cli_never_calls_a_vehicle_write_api()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "tools", "ObdCli", "Cli.cs"));
        foreach (var api in new[] { "ClearDtcsAsync", "WriteDataByIdentifierAsync", "WriteBlockAsync", "RestoreAsync(", "StartRoutineAsync", "StopRoutineAsync",
                                    "EcuResetAsync", "SecurityAccessAsync", "DisableDtcSettingAsync", "DisableCommunicationAsync", "AdjustOutputAsync", "FreezeOutputAsync" })
            Assert.DoesNotContain(api, src);
        Assert.Contains("Gate = new WriteGate(() => true)", src); // and every UDS client it makes refuses state-changing services
    }

    // ---- J2534 ----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task J2534_demo_info()
    {
        var (code, o, _) = await Run("info", "--j2534-demo", "--json");
        Assert.Equal(0, code);
        var j = JsonDocument.Parse(o).RootElement;
        Assert.Equal("J2534", j.GetProperty("transport").GetString());
        Assert.True(j.GetProperty("msCan").GetBoolean());
        Assert.Equal("04.04", j.GetProperty("api").GetString());
    }

    [Fact]
    public async Task J2534_demo_scan_reaches_ms_can_by_pin_selection()
    {
        var (code, o, _) = await Run("--j2534-demo", "scan", "--json");
        Assert.Equal(0, code);
        var rows = JsonDocument.Parse(o).RootElement.EnumerateArray().ToList();
        var acm = rows.Single(r => r.GetProperty("tx").GetString() == "727");
        Assert.True(acm.GetProperty("responded").GetBoolean());
        Assert.Equal("MsCan", acm.GetProperty("bus").GetString());
        Assert.Contains(rows, r => r.GetProperty("tx").GetString() == "7E0" && r.GetProperty("responded").GetBoolean());
    }

    [Fact]
    public async Task J2534_demo_dtcs_and_asbuilt()
    {
        var (dc, dout, _) = await Run("dtcs", "--j2534-demo", "--module", "727", "--json");
        Assert.Equal(0, dc);
        Assert.Equal("727", Assert.Single(JsonDocument.Parse(dout).RootElement.EnumerateArray()).GetProperty("tx").GetString());
        var (ac, aout, _) = await Run("asbuilt-read", "--j2534-demo", "--module", "726", "--json");
        Assert.Equal(0, ac);
        Assert.Equal(8, JsonDocument.Parse(aout).RootElement.GetProperty("blocks").GetInt32());
    }

    [Fact]
    public async Task J2534_monitor_is_not_available_yet()
    {
        var (code, _, e) = await Run("monitor", "--j2534-demo");
        Assert.Equal(1, code);
        Assert.Contains("ELM327/STN", e);
    }

    [Fact]
    public async Task J2534_list_runs_anywhere_and_an_unknown_device_is_an_error()
    {
        Assert.Equal(0, (await Run("j2534-list")).Code);
        var (code, _, e) = await Run("info", "--j2534", "no-such-device-xyz");
        Assert.Equal(1, code);
        Assert.Contains("j2534-list", e);
    }
}
