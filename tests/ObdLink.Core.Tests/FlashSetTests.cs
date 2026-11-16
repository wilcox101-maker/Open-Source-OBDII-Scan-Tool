using ObdLink.Core.Flash;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.23.15: Ford software set check (<see cref="FlashSet"/>), <c>obdcli vbf-info</c> with several files, and <c>obdcli selftest</c>.</summary>
public class FlashSetTests
{
    private static byte[] Pattern(int n, int seed) => Enumerable.Range(0, n).Select(i => (byte)(i * 7 + seed)).ToArray();

    private static FlashImage Vbf(string name, uint addr, int len, string? type, uint? ecu = 0x7E0, uint? call = null) =>
        VbfReader.Parse(VbfReader.Write(new[] { new FlashSegment(addr, Pattern(len, (int)(addr >> 8))) }, name, ecu, null, partType: type, call: call), name + ".vbf");

    [Fact]
    public void AcceptsSblExeData_AndOrdersExeBeforeData()
    {
        var r = FlashSet.Check(new[]
        {
            Vbf("JL3A-14C204-DATA", 0x80000, 64, "DATA"), Vbf("E3B1-14C039-AA", 0xE00000, 64, "SBL", call: 0xE00000), Vbf("JL3A-14C204-EXE", 0x10000, 64, "EXE"),
        });
        Assert.True(r.Ok, string.Join("; ", r.Problems));
        Assert.Equal(0x7E0u, r.TxId);
        Assert.Equal("E3B1-14C039-AA.vbf", r.Sbl!.Name);
        Assert.Equal(new[] { "JL3A-14C204-EXE.vbf", "JL3A-14C204-DATA.vbf" }, r.Images.Select(i => i.Name));
        Assert.Contains(r.Notes, n => n.StartsWith("Flash order", StringComparison.Ordinal));
        Assert.Contains(r.Notes, n => n.Contains("starts at 0x00E00000"));
    }

    [Fact]
    public void RefusesFilesForDifferentModules()
    {
        var r = FlashSet.Check(new[] { Vbf("A", 0x10000, 64, "EXE", ecu: 0x7E0), Vbf("B", 0x80000, 64, "DATA", ecu: 0x7E1) });
        Assert.False(r.Ok);
        Assert.Null(r.TxId);
        Assert.Contains(r.Problems, p => p.Contains("different modules") && p.Contains("0x7E1"));
    }

    [Fact]
    public void RefusesOverlappingImages_ButNotTouchingOnes()
    {
        var overlap = FlashSet.Check(new[] { Vbf("A", 0x10000, 0x100, "EXE"), Vbf("B", 0x10080, 0x100, "DATA") });
        Assert.Contains(overlap.Problems, p => p.Contains("both write 0x00010080"));
        var adjacent = FlashSet.Check(new[] { Vbf("A", 0x10000, 0x100, "EXE"), Vbf("B", 0x10100, 0x100, "DATA") });
        Assert.True(adjacent.Ok, string.Join("; ", adjacent.Problems));
    }

    [Fact]
    public void RefusesTwoSbls_AndSblOnly()
    {
        var two = FlashSet.Check(new[] { Vbf("S1", 0xE00000, 16, "SBL", call: 0xE00000), Vbf("S2", 0xE00000, 16, "SBL", call: 0xE00000), Vbf("A", 0x10000, 16, "EXE") });
        Assert.Contains(two.Problems, p => p.Contains("2 SBL files"));
        var only = FlashSet.Check(new[] { Vbf("S1", 0xE00000, 16, "SBL", call: 0xE00000) });
        Assert.Contains(only.Problems, p => p.Contains("only an SBL"));
    }

    [Fact]
    public void NoSbl_IsAllowedWithANote_AndNoModuleGetsANote()
    {
        var noSbl = FlashSet.Check(new[] { Vbf("A", 0x10000, 16, "EXE") });
        Assert.True(noSbl.Ok);
        Assert.Contains(noSbl.Notes, n => n.Contains("No SBL"));
        var noEcu = FlashSet.Check(new[] { Vbf("A", 0x10000, 16, "EXE", ecu: null) });
        Assert.Null(noEcu.TxId);
        Assert.Contains(noEcu.Notes, n => n.Contains("No file names its module"));
    }

    [Fact]
    public void BadSbl_IsReportedThroughTheEnginesLayoutCheck_ForEachImage()
    {
        // Call address outside the SBL's own data: the same check FlashEngine runs before touching a module.
        var r = FlashSet.Check(new[] { Vbf("S", 0xE00000, 0x40, "SBL", call: 0xF00000), Vbf("A", 0x10000, 0x40, "EXE"), Vbf("B", 0x80000, 0x40, "DATA") });
        Assert.False(r.Ok);
        Assert.Contains(r.Problems, p => p.StartsWith("A.vbf: ", StringComparison.Ordinal) && p.Contains("outside the SBL's own data"));
        Assert.Contains(r.Problems, p => p.StartsWith("B.vbf: ", StringComparison.Ordinal) && p.Contains("outside the SBL's own data"));
    }

    [Fact]
    public async Task Cli_VbfInfo_ChecksASet()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vbfset-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string W(string n, byte[] b) { var p = Path.Combine(dir, n); File.WriteAllBytes(p, b); return p; }
            var sbl = W("sbl.vbf", VbfReader.Write(new[] { new FlashSegment(0xE00000, Pattern(64, 1)) }, "E3B1-14C039-AA", 0x7E0, partType: "SBL", call: 0xE00000));
            var exe = W("exe.vbf", VbfReader.Write(new[] { new FlashSegment(0x10000, Pattern(64, 2)) }, "JL3A-14C204-AA", 0x7E0, partType: "EXE"));
            var other = W("other.vbf", VbfReader.Write(new[] { new FlashSegment(0x80000, Pattern(64, 3)) }, "JL3A-14C204-AB", 0x7E1, partType: "DATA"));
            var o = new StringWriter(); var e = new StringWriter();
            Assert.Equal(0, await ObdLink.Cli.Cli.RunAsync(new[] { "vbf-info", "--file", sbl, "--file", exe }, o, e));
            Assert.Contains("Set verdict: the flash engine would accept this set.", o.ToString());
            o = new StringWriter();
            Assert.Equal(3, await ObdLink.Cli.Cli.RunAsync(new[] { "vbf-info", "--file", sbl, "--file", exe, "--file", other }, o, e));
            Assert.Contains("different modules", o.ToString());
            o = new StringWriter();
            Assert.Equal(0, await ObdLink.Cli.Cli.RunAsync(new[] { "vbf-info", "--file", sbl, "--file", exe, "--json" }, o, e));
            var root = System.Text.Json.JsonDocument.Parse(o.ToString()).RootElement;
            Assert.Equal(2, root.GetProperty("files").GetArrayLength());
            Assert.True(root.GetProperty("set").GetProperty("ok").GetBoolean());
            Assert.Equal("0x7E0", root.GetProperty("set").GetProperty("module").GetString());
            // One file still behaves as before (single-file JSON object, no "set").
            o = new StringWriter();
            Assert.Equal(0, await ObdLink.Cli.Cli.RunAsync(new[] { "vbf-info", "--file", exe, "--json" }, o, e));
            Assert.False(System.Text.Json.JsonDocument.Parse(o.ToString()).RootElement.TryGetProperty("set", out _));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Cli_SelfTest_AllChecksPass()
    {
        var o = new StringWriter(); var e = new StringWriter();
        Assert.Equal(0, await ObdLink.Cli.Cli.RunAsync(new[] { "selftest" }, o, e));
        var text = o.ToString();
        Assert.DoesNotContain("FAIL", text);
        Assert.Matches(@"All \d+ checks passed\.", text);
        Assert.Contains("no COM port, no vehicle", text);
    }
}
