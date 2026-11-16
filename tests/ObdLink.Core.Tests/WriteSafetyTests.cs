using ObdLink.Core.AsBuilt;
using ObdLink.Core.Elm;
using ObdLink.Core.Firmware;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Logging;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.6.1 audit: one test (or more) per finding. Grouped by feature, not by version.</summary>
public class AsBuiltWritePlanTests
{
    private static AsBuiltEditLine L(string addr, string data, string? cs = null)
    {
        var a = AsBuiltAddress.Parse(addr);
        var d = Convert.FromHexString(data);
        return new(a, data, cs ?? AsBuiltLine.ComputeChecksum(a, d).ToString("X2"));
    }

    [Fact]
    public void Uses_only_the_selected_module_and_block()
    {
        // Grid after "Read all blocks" / "Import .abt": several blocks and another module.
        var rows = new[]
        {
            L("726-01-01", "0102030405"), L("726-01-02", "0607"),
            L("726-02-01", "AAAAAAAAAA"), L("720-01-01", "BBBBBBBBBB"),
        };
        var b = AsBuiltWritePlan.BuildBlock(0x726, 0x01, rows);
        Assert.Equal(Convert.FromHexString("01020304050607"), b.Data);
        Assert.Equal(0xDE00, b.Did);
    }

    [Fact]
    public void No_lines_for_the_block_is_refused() =>
        Assert.Throws<InvalidOperationException>(() => AsBuiltWritePlan.BuildBlock(0x726, 0x03, new[] { L("726-01-01", "01") }));

    [Fact]
    public void Gap_or_duplicate_line_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => AsBuiltWritePlan.BuildBlock(0x726, 1, new[] { L("726-01-01", "01"), L("726-01-03", "02") }));
        Assert.Throws<InvalidDataException>(() => AsBuiltWritePlan.BuildBlock(0x726, 1, new[] { L("726-01-01", "01"), L("726-01-01", "02") }));
    }

    [Fact]
    public void Typo_in_data_is_caught_by_the_typed_checksum()
    {
        var good = L("726-01-01", "0102030405");
        var typo = good with { DataHex = "0102030406" }; // data edited, checksum not retyped
        var ex = Assert.Throws<InvalidDataException>(() => AsBuiltWritePlan.BuildBlock(0x726, 1, new[] { typo }));
        Assert.Contains("checksum", ex.Message);
        Assert.Throws<InvalidDataException>(() => AsBuiltWritePlan.BuildBlock(0x726, 1, new[] { good with { DataHex = "01020" } }));
    }

    [Fact]
    public void Line_ten_is_bcd_0x10()
    {
        var rows = Enumerable.Range(1, 10).Select(n => L($"726-01-{AsBuiltAddress.ToBcd(n):X2}", "01")).ToArray();
        Assert.Equal(10, AsBuiltWritePlan.BuildBlock(0x726, 1, rows).Data.Length);
    }

    [Theory]
    [InlineData("1FTFW1E80HFA00001", "1FTFW1E80HFA00001", true)]
    [InlineData("1FTFW1E80HFA00001", "XXXXXXXXXXXA00001", true)]
    [InlineData("1FTFW1E80HFA00001", "1FTFW1E80HFB99999", false)]
    [InlineData("UNKNOWNVIN", "UNKNOWNVIN", false)]
    [InlineData("", "1FTFW1E80HFA00001", false)]
    public void Vin_match(string a, string b, bool same) => Assert.Equal(same, VinMatch.Same(a, b));
}

public class AsBuiltServiceSafetyTests
{
    private const string Vin = "1FTFW1E80HFA00001";
    private static readonly byte[] Original = { 1, 2, 3, 4, 5, 6, 7 };

    internal static async Task<(SimulatedAdapter sim, AdapterSession s, ElmDiagChannel ch)> Connect()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, s, new ElmDiagChannel(s));
    }

    private static async Task<(SimulatedAdapter sim, AsBuiltService svc, AutoConfirm confirm, string dir)> Setup()
    {
        var (sim, s, ch) = await Connect();
        sim.Dids[0x726] = new() { [0xDE00] = (byte[])Original.Clone() };
        var dir = Path.Combine(Path.GetTempPath(), "abt61_" + Guid.NewGuid());
        var confirm = new AutoConfirm(true);
        var svc = new AsBuiltService(m => new UdsClient(ch, m), new SafetyGuard(s, confirm), dir) { PendingSettle = TimeSpan.Zero };
        return (sim, svc, confirm, dir);
    }

    private static AsBuiltBlock Block(params byte[] d) => new() { Module = 0x726, Block = 1, Data = d };

    [Fact]
    public async Task Different_length_is_never_written()
    {
        var (sim, svc, confirm, dir) = await Setup();
        var ex = await Assert.ThrowsAsync<SafetyException>(() => svc.WriteBlockAsync(Vin, Block(9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9)));
        Assert.Contains("7 bytes", ex.Message);
        Assert.Equal(Original, sim.Dids[0x726][0xDE00]);
        Assert.Empty(confirm.Asked);                     // refused before any "write now?" question
        Assert.DoesNotContain(sim.Sent, r => r.Request.StartsWith("2E") || r.Request.StartsWith("10"));
        Assert.Empty(Directory.GetFiles(dir));
    }

    [Fact]
    public async Task Unchanged_data_writes_nothing()
    {
        var (sim, svc, _, _) = await Setup();
        var r = await svc.WriteBlockAsync(Vin, Block(Original));
        Assert.False(r.Written);
        Assert.DoesNotContain(sim.Sent, x => x.Request.Contains("2EDE00") || x.Request.Contains("2EDE"));
    }

    [Fact]
    public async Task Confirmation_says_how_many_bytes_change_and_backup_holds_old_data()
    {
        var (_, svc, confirm, _) = await Setup();
        var r = await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9));
        Assert.Contains(confirm.Asked, a => a.Message.Contains("1 of 7 bytes change"));
        var bk = AsBuiltBackup.Deserialize(File.ReadAllText(r.BackupPath!));
        Assert.Equal(Convert.ToHexString(Original), bk.Blocks["01"]);
    }

    [Theory]
    [InlineData(SimWriteReply.PendingThenPositive)]
    [InlineData(SimWriteReply.PendingOnly)]
    [InlineData(SimWriteReply.NoReply)]
    public async Task Uncertain_write_reply_is_settled_by_read_back(SimWriteReply reply)
    {
        var (sim, svc, _, _) = await Setup();
        sim.WriteReply = reply;
        var r = await svc.WriteBlockAsync(Vin, Block(9, 9, 9, 9, 9, 9, 9));
        Assert.True(r.Written);
        Assert.Equal(new byte[] { 9, 9, 9, 9, 9, 9, 9 }, sim.Dids[0x726][0xDE00]);
        if (reply != SimWriteReply.PendingThenPositive) Assert.NotEqual("", r.Note);
    }

    [Fact]
    public async Task Real_rejection_is_still_an_error()
    {
        var (sim, svc, _, _) = await Setup();
        sim.WriteReply = SimWriteReply.RejectNotStored;
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => svc.WriteBlockAsync(Vin, Block(9, 9, 9, 9, 9, 9, 9)));
        Assert.Equal(0x22, ex.Nrc);
        Assert.Equal(Original, sim.Dids[0x726][0xDE00]);
    }

    private static async Task<string> SaveBackup(string dir, string vin, byte[] data)
    {
        Directory.CreateDirectory(dir);
        var p = Path.Combine(dir, "test.asbuilt.json");
        await File.WriteAllTextAsync(p, new AsBuiltBackup(vin, DateTimeOffset.Now, 0x726, new() { ["01"] = Convert.ToHexString(data) }).Serialize());
        return p;
    }

    [Fact]
    public async Task Restore_refuses_another_vehicles_backup()
    {
        var (sim, svc, _, dir) = await Setup();
        var p = await SaveBackup(dir, "1FTFW1E80HFB99999", new byte[7]);
        await Assert.ThrowsAsync<SafetyException>(() => svc.RestoreAsync(p, Vin));
        await Assert.ThrowsAsync<SafetyException>(() => svc.RestoreAsync(p, "UNKNOWNVIN"));
        Assert.Equal(Original, sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Restore_backs_up_current_contents_first_and_accepts_masked_vin()
    {
        var (sim, svc, _, dir) = await Setup();
        var p = await SaveBackup(dir, "XXXXXXXXXXXA00001", new byte[] { 7, 7, 7, 7, 7, 7, 7 });
        var r = await svc.RestoreAsync(p, Vin);
        Assert.True(r.Written);
        Assert.Contains("_pre-restore", r.BackupPath);
        Assert.Equal(Convert.ToHexString(Original), AsBuiltBackup.Deserialize(File.ReadAllText(r.BackupPath!)).Blocks["01"]);
        Assert.Equal(new byte[] { 7, 7, 7, 7, 7, 7, 7 }, sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Restore_of_wrong_length_or_matching_data_writes_nothing()
    {
        var (sim, svc, _, dir) = await Setup();
        var p = await SaveBackup(dir, Vin, new byte[] { 7, 7 });
        await Assert.ThrowsAsync<SafetyException>(() => svc.RestoreAsync(p, Vin));
        p = await SaveBackup(dir, Vin, Original);
        Assert.False((await svc.RestoreAsync(p, Vin)).Written);
        Assert.DoesNotContain(sim.Sent, x => x.Request.Contains("2EDE"));
    }
}

public class AdapterStateTests
{
    [Fact]
    public async Task Console_ATSH_does_not_redirect_the_next_module_request()
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        await new UdsClient(ch, 0x726).TesterPresentAsync();
        var con = new RawConsoleService(s.Link, s.Invalidate);
        await con.ExecuteAsync("ATSH7E0");                      // allowed in read-only mode
        sim.Sent.Clear();
        await new UdsClient(ch, 0x726).TesterPresentAsync();
        Assert.Equal(0x726u, sim.Sent.Single().Header);          // before v0.6.1 this went to 0x7E0
    }

    [Fact]
    public async Task Console_ATZ_is_followed_by_reinit_and_protocol()
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        var con = new RawConsoleService(s.Link, s.Invalidate);
        await con.ExecuteAsync("ATZ");
        Assert.Null(sim.Protocol);
        var sent = new List<string>(); s.Link.Traffic += (d, t) => { if (d == ">>") sent.Add(t); };
        await new UdsClient(ch, 0x726).TesterPresentAsync();
        Assert.Equal("33", sim.Protocol);
        Assert.Contains("ATH1", sent);
        Assert.Contains("ATSH726", sent);
    }

    [Fact]
    public async Task Pure_query_in_console_does_not_cost_extra_traffic()
    {
        var (_, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        await new UdsClient(ch, 0x726).TesterPresentAsync();
        await new RawConsoleService(s.Link, s.Invalidate).ExecuteAsync("ATRV");
        Assert.False(s.IsStale);
    }

    [Fact]
    public async Task Vehicle_request_is_not_resent_after_link_drop_and_state_is_reapplied()
    {
        var sim = new SimulatedAdapter();
        var t = sim.CreateTransport();
        var s = new AdapterSession(new ElmLink(t));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var ch = new ElmDiagChannel(s);
        await new UdsClient(ch, 0x726).TesterPresentAsync();
        sim.Sent.Clear();
        t.DropAfterSend = c => c == "3E00";
        await Assert.ThrowsAsync<LinkDroppedException>(() => new UdsClient(ch, 0x726).TesterPresentAsync());
        Assert.Single(sim.Sent);                                  // reached the vehicle once, never resent
        Assert.True(s.IsStale);
        await new UdsClient(ch, 0x726).TesterPresentAsync();      // next request re-applies ATH1/protocol/header first
        Assert.Equal(0x726u, sim.Sent[^1].Header);
    }

    [Fact]
    public async Task Adapter_command_is_retried_after_link_drop()
    {
        var sim = new SimulatedAdapter();
        var t = sim.CreateTransport();
        var link = new ElmLink(t);
        await link.SendAsync("ATI");
        t.DropAfterSend = c => c == "ATRV";
        var r = await link.SendAsync("ATRV");
        Assert.Equal("12.6V", r.Lines.Single());
    }

    [Theory]
    [InlineData("ATRV", true)]
    [InlineData("AT SH 7E0", true)]
    [InlineData("STP 33", true)]
    [InlineData("2EDE000102", false)]
    [InlineData("22F190", false)]
    [InlineData("STPX h:7E0,d:2EDE00", false)]
    [InlineData("ATWM 8110F13E", false)]
    public void Resend_safety(string cmd, bool safe) => Assert.Equal(safe, ElmLink.IsResendSafe(cmd));

    [Fact]
    public async Task Hs_can2_is_tracked_as_its_own_bus()
    {
        var (_, s, _) = await AsBuiltServiceSafetyTests.Connect();
        await s.SelectBusAsync(Bus.HsCan2);
        Assert.Equal(Bus.HsCan2, s.CurrentBus);
        Assert.Null(s.CurrentProtocol);
        await s.SelectBusAsync(Bus.MsCan);
        Assert.Equal(StnProtocol.MsCan11_125, s.CurrentProtocol);
    }
}

public class IsoTpTransmitTests
{
    private static async Task<(SimulatedAdapter sim, UdsClient uds)> Bcm()
    {
        var (sim, _, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.StpxSupported = false; // these tests cover host-side ISO-TP flow control (v0.22.5: STN adapters use STPX first, StpxTests)
        sim.Dids[0x726] = new() { [0xDE00] = new byte[14] };
        return (sim, new UdsClient(ch, 0x726));
    }

    [Fact]
    public async Task Flow_control_wait_alone_stops_the_transfer()
    {
        var (sim, uds) = await Bcm();
        sim.FlowControlReply = "72E310000";
        sim.Sent.Clear();
        await Assert.ThrowsAsync<IsoTpException>(() => uds.WriteDataByIdentifierAsync(0xDE00, Enumerable.Repeat((byte)9, 14).ToArray()));
        Assert.Single(sim.Sent, x => x.Request.StartsWith("1"));   // first frame only
        Assert.DoesNotContain(sim.Sent, x => x.Request.StartsWith("2"));
        Assert.Equal(new byte[14], sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Wait_then_clear_to_send_in_one_reply_proceeds()
    {
        var (sim, uds) = await Bcm();
        sim.FlowControlReply = "72E310000\r72E300000";
        await uds.WriteDataByIdentifierAsync(0xDE00, Enumerable.Repeat((byte)9, 14).ToArray());
        Assert.Equal(Enumerable.Repeat((byte)9, 14).ToArray(), sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Overflow_aborts()
    {
        var (sim, uds) = await Bcm();
        sim.FlowControlReply = "72E320000";
        await Assert.ThrowsAsync<IsoTpException>(() => uds.WriteDataByIdentifierAsync(0xDE00, new byte[14]));
    }

    [Fact]
    public async Task Block_size_requires_a_new_flow_control()
    {
        var (sim, uds) = await Bcm();
        sim.FlowControlReply = "72E300100"; // BS = 1: a new FC is due after every frame; the simulator never sends one
        await Assert.ThrowsAsync<IsoTpException>(() => uds.WriteDataByIdentifierAsync(0xDE00, Enumerable.Repeat((byte)9, 14).ToArray()));
        Assert.Equal(new byte[14], sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public void St_min_mapping()
    {
        var f = ElmDiagChannel.ParseFlowControls(new[] { "72E3000F1", "72E300080", "72E300014" }, false);
        Assert.Equal(new[] { 1, 127, 20 }, f.Select(x => x.StMinMs));
    }

    [Fact]
    public async Task Adapter_is_back_in_auto_format_after_a_failed_transfer()
    {
        var (sim, uds) = await Bcm();
        sim.FlowControlReply = "72E320000";
        await Assert.ThrowsAnyAsync<Exception>(() => uds.WriteDataByIdentifierAsync(0xDE00, new byte[14]));
        sim.FlowControlReply = null;
        Assert.Equal(new byte[14], await uds.ReadDataByIdentifierAsync(0xDE00)); // single-frame request works again (ATCAF1 restored)
    }
}

public class UdsClientEdgeTests
{
    private sealed class Scripted(params DiagResponse[][] replies) : IDiagChannel
    {
        private int _i;
        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] request, TimeSpan? timeout = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DiagResponse>>(replies[Math.Min(_i++, replies.Length - 1)]);
    }

    [Fact]
    public void Final_reply_beats_pending_and_other_modules_are_ignored()
    {
        var r = UdsClient.PickFinal(new[]
        {
            new DiagResponse(0x7E8, new byte[] { 0x6E, 0xDE, 0x00 }),  // another module
            new DiagResponse(0x72E, new byte[] { 0x7F, 0x2E, 0x78 }),
            new DiagResponse(0x72E, new byte[] { 0x6E, 0xDE, 0x00 }),
        }, 0x72E);
        Assert.Equal(new byte[] { 0x6E, 0xDE, 0x00 }, r);
        Assert.Null(UdsClient.PickFinal(new[] { new DiagResponse(0x7E8, new byte[] { 0x62 }) }, 0x72E));
    }

    [Fact]
    public async Task Empty_seed_is_not_treated_as_unlocked()
    {
        var uds = new UdsClient(new Scripted(new[] { new DiagResponse(0x72E, new byte[] { 0x67, 0x01 }) }), 0x726);
        await Assert.ThrowsAsync<InvalidDataException>(() => uds.SecurityAccessAsync(1));
    }

    [Fact]
    public async Task All_zero_seed_still_means_unlocked()
    {
        var uds = new UdsClient(new Scripted(new[] { new DiagResponse(0x72E, new byte[] { 0x67, 0x01, 0, 0 }) }), 0x726);
        Assert.True(await uds.SecurityAccessAsync(1));
    }
}

public class UpdaterPublisherTests
{
    [Theory]
    [InlineData("OBD Solutions LLC", true)]
    [InlineData("OBD Solutions, LLC", true)]
    [InlineData("ScanTool.net, LLC", true)]
    [InlineData("SCANTOOL", true)]
    [InlineData("Not ScanTool", false)]
    [InlineData("ScanTool Fake Inc", false)]
    [InlineData("OBD Solutions Reseller LLC", false)]
    [InlineData(null, false)]
    public void Publisher_must_be_an_exact_name(string? cn, bool ok) => Assert.Equal(ok, OfficialUpdater.PublisherOk(cn));
}
