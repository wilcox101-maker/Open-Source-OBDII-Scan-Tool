using ObdLink.Core.AsBuilt;
using ObdLink.Core.Dtc;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

public class AsBuiltTests
{
    // Public worked examples (see AsBuiltLine doc comment for sources).
    [Theory]
    [InlineData("760-01-04 3733 D6")]
    [InlineData("7D0-01-01 2A2A 0502 2054")]
    [InlineData("726-02-02 0001 0164 0097")]
    public void Checksum_matches_public_examples(string line) => Assert.True(AsBuiltLine.Parse(line).ChecksumValid);

    [Fact]
    public void Checksum_detects_bad_line() => Assert.False(AsBuiltLine.Parse("7D0-01-01 2A2A 0502 2F2C").ChecksumValid);

    [Fact]
    public void Format_roundtrip() => Assert.Equal("7D0-01-01 2A2A 0502 2054", AsBuiltLine.Parse("7d0-01-01 2a2a05022054").Format());

    [Fact]
    public void Block_lines_and_did()
    {
        var b = new AsBuiltBlock { Module = 0x726, Block = 1, Data = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray() };
        var lines = b.Lines().ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(2, lines[2].Data.Length);
        Assert.Equal(0xDE00, b.Did);
        var re = AsBuiltBlock.FromLines(lines);
        Assert.Equal(b.Data, re.Data);
    }
}

public class ElmTests
{
    [Fact]
    public void Parse_strips_echo_and_prompt()
    {
        var r = ElmLink.Parse("0100", "0100\r7E8064100BE3EA813\r\r>");
        Assert.True(r.Ok);
        Assert.Single(r.Lines);
    }

    [Theory]
    [InlineData("NO DATA", ElmError.NoData)]
    [InlineData("CAN ERROR", ElmError.CanError)]
    [InlineData("?", ElmError.Unknown)]
    [InlineData("BUFFER FULL", ElmError.BufferFull)]
    [InlineData("SEARCHING...\rUNABLE TO CONNECT", ElmError.UnableToConnect)]
    public void Classifies_errors(string body, ElmError e) => Assert.Equal(e, ElmLink.Parse("x", body + "\r>").Error);

    [Fact]
    public async Task Init_sequence_and_info()
    {
        var sim = new SimulatedAdapter();
        var t = sim.CreateTransport();
        var s = new AdapterSession(new ElmLink(t));
        var info = await s.InitializeAsync();
        Assert.Equal(new[] { "ATZ", "ATE0", "ATL0", "ATS0", "ATH1", "STI", "STDI", "ATRV" }, t.Sent);
        Assert.True(s.IsStn);
        Assert.Equal(12.6, info.BatteryVolts);
    }

    [Fact]
    public async Task Timeout_throws_and_recovers()
    {
        int n = 0;
        var t = new MockTransport(c => c == "ATRV" && n++ == 0 ? null : "OK");
        var link = new ElmLink(t) { DefaultTimeout = TimeSpan.FromMilliseconds(150) };
        await Assert.ThrowsAsync<ElmTimeoutException>(() => link.SendAsync("ATRV"));
        Assert.True((await link.SendAsync("ATRV")).Ok);
    }

    [Fact]
    public async Task Reconnects_after_io_error()
    {
        var t = new MockTransport(_ => "OK");
        var link = new ElmLink(t);
        await t.OpenAsync(); await t.CloseAsync();            // simulate BT drop
        Assert.True((await link.SendAsync("ATE0")).Ok);
        Assert.Equal(2, t.OpenCount);
    }

    [Fact]
    public async Task Protocol_presets_are_fr_pm_values()
    {
        Assert.Equal(33, (int)StnProtocol.HsCan11_500);
        Assert.Equal(53, (int)StnProtocol.MsCan11_125);
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.MsCan11_125);
        Assert.Equal("53", sim.Protocol);
    }

    [Theory]
    [InlineData("12.6V", 12.6)]
    [InlineData(" 9.8V", 9.8)]
    [InlineData("junk", null)]
    public void Voltage_parse(string s, double? v) => Assert.Equal(v, AdapterSession.ParseVoltage(s));
}

public class IsoTpTests
{
    [Fact]
    public void Segment_and_reassemble_long()
    {
        var p = Enumerable.Range(0, 40).Select(i => (byte)i).ToArray();
        var frames = IsoTpCodec.Segment(p);
        Assert.Equal(FrameType.First, IsoTpCodec.TypeOf(frames[0]));
        Assert.Equal(0x21, frames[1][0]);
        var a = new IsoTpReassembler(); byte[]? res = null;
        foreach (var f in frames) res = a.Feed(f) ?? res;
        Assert.Equal(p, res);
    }

    [Fact]
    public void Sequence_error()
    {
        var f = IsoTpCodec.Segment(new byte[20]);
        var a = new IsoTpReassembler(); a.Feed(f[0]);
        Assert.Throws<IsoTpException>(() => a.Feed(f[2]));
    }

    [Fact]
    public void Flow_control_frame() => Assert.Equal(new byte[] { 0x30, 0, 0, 0, 0, 0, 0, 0 }, IsoTpCodec.FlowControl());

    [Fact]
    public void Parse_elm_multiframe_lines()
    {
        var lines = new[] { "7E81014490201544553", "7E82154564930303030", "7E82230303030303031" };
        var r = ElmDiagChannel.ParseFrames(lines, false).Single();
        Assert.Equal(0x7E8u, r.SourceId);
        Assert.Equal(20, r.Payload.Length);
    }
}

public class ServiceTests
{
    private static async Task<(SimulatedAdapter sim, AdapterSession s, ElmDiagChannel ch)> Setup()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, s, new ElmDiagChannel(s));
    }

    [Fact]
    public async Task Obd_vin_pids_dtcs()
    {
        var (sim, _, ch) = await Setup();
        var obd = new ObdService(ch);
        Assert.Equal(sim.Vin, await obd.ReadVinAsync());
        Assert.Equal(1726.0, (await obd.ReadPidAsync(0x0C))!.Value);
        Assert.Contains((byte)0x0C, await obd.GetSupportedPidsAsync());
        var dtcs = await obd.ReadStoredDtcsAsync();
        Assert.Contains(dtcs, d => d.Code == "P0301");
        Assert.Contains(dtcs, d => d.Code == "P0101");
        Assert.True(await obd.ClearDtcsAsync());
    }

    [Fact]
    public void Dtc_encoding() { Assert.Equal("P0301", DtcFormat.FromTwoBytes(0x0301)); Assert.Equal("U0100", DtcFormat.FromTwoBytes(0xC100)); Assert.Equal("B1234", DtcFormat.FromTwoBytes(0x9234)); }

    [Fact]
    public void Dtc_db_lookup()
    {
        var db = DtcDatabase.LoadEmbedded();
        Assert.True(db.Count > 30);
        Assert.Contains("Misfire", db.Describe("P0300"));
        Assert.Contains("Manufacturer", db.Describe("P1234"));
    }

    [Fact]
    public async Task Uds_services_and_nrc()
    {
        var (_, _, ch) = await Setup();
        var u = new UdsClient(ch, 0x726);
        await u.TesterPresentAsync();
        await u.DiagnosticSessionControlAsync(UdsSession.Extended);
        var dtcs = await u.ReadDtcsByStatusAsync();
        Assert.Equal("B1123-45", dtcs.Single().Display);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => u.ReadDataByIdentifierAsync(0x1234));
        Assert.Equal(0x31, ex.Nrc);
        Assert.False(await u.SecurityAccessAsync(1)); // null provider -> no key
    }

    private sealed class FakeKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] s, CancellationToken ct = default) => Task.FromResult<byte[]?>(s.Reverse().ToArray());
    }

    [Fact]
    public async Task Security_access_uses_provider()
    {
        var (_, _, ch) = await Setup();
        Assert.True(await new UdsClient(ch, 0x726) { SeedKey = new FakeKey() }.SecurityAccessAsync(1));
    }

    [Fact]
    public async Task Module_scan_both_buses()
    {
        var (sim, s, ch) = await Setup();
        var res = await new ModuleScanner(s, ch).ScanAsync(FordF150_2017.Modules);
        Assert.Contains(res, r => r.Module.Acronym == "BCM" && r.Responded);
        Assert.Contains(res, r => r.Module.Acronym == "APIM" && !r.Responded);
        Assert.Equal("53", sim.Protocol); // last bus scanned was MS-CAN
    }

    private static async Task<(SimulatedAdapter, AsBuiltService, string)> AsBuiltSetup(bool confirm, double volts = 12.6)
    {
        var (sim, s, ch) = await Setup();
        sim.Voltage = volts;
        sim.Dids[0x726] = new() { [0xDE00] = new byte[] { 1, 2, 3, 4, 5, 6, 7 } };
        var dir = Path.Combine(Path.GetTempPath(), "abtest_" + Guid.NewGuid());
        var svc = new AsBuiltService(m => new UdsClient(ch, m), new SafetyGuard(s, new AutoConfirm(confirm)), dir);
        return (sim, svc, dir);
    }

    [Fact]
    public async Task AsBuilt_write_backup_verify_restore()
    {
        var (sim, svc, _) = await AsBuiltSetup(true);
        var nb = new AsBuiltBlock { Module = 0x726, Block = 1, Data = new byte[] { 9, 9, 9, 9, 9, 9, 9 } };
        var backup = await svc.WriteBlockAsync("TESTVIN0000000001", nb);
        Assert.True(File.Exists(backup));
        Assert.Equal(nb.Data, sim.Dids[0x726][0xDE00]);
        await svc.RestoreAsync(backup);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task AsBuilt_readback_mismatch_detected()
    {
        var (sim, svc, _) = await AsBuiltSetup(true);
        sim.CorruptWrites = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => svc.WriteBlockAsync("V", new AsBuiltBlock { Module = 0x726, Block = 1, Data = new byte[7] }));
    }

    [Fact]
    public async Task AsBuilt_blocked_by_low_voltage_or_decline()
    {
        var (sim, svc, _) = await AsBuiltSetup(true, 11.9);
        await Assert.ThrowsAsync<SafetyException>(() => svc.WriteBlockAsync("V", new AsBuiltBlock { Module = 0x726, Block = 1, Data = new byte[7] }));
        var (sim2, svc2, _) = await AsBuiltSetup(false);
        await Assert.ThrowsAsync<UserDeclinedException>(() => svc2.WriteBlockAsync("V", new AsBuiltBlock { Module = 0x726, Block = 1, Data = new byte[7] }));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, sim2.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Console_blocks_writes_by_default()
    {
        var (_, s, _) = await Setup();
        var c = new RawConsoleService(s.Link);
        Assert.StartsWith("BLOCKED", await c.ExecuteAsync("2E DE00 01"));
        Assert.Equal("12.6V", await c.ExecuteAsync("ATRV"));
    }
}
