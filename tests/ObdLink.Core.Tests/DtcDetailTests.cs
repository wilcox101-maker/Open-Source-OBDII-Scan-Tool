using ObdLink.Core.Dtc;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.8.0 Phase 1: read-only DTC detail (UDS 19 01/03/04/06, OBD 02/07/0A).</summary>
public class DtcDetailTests
{
    [Theory]
    [InlineData(0x2F, "confirmed, failing now")]
    [InlineData(0x24, "pending")]
    [InlineData(0x20, "history")]
    [InlineData(0x89, "confirmed, failing now, lamp")]
    [InlineData(0x00, "-")]
    public void Status_summary(byte status, string text) => Assert.Equal(text, DtcStatusBits.Summary(status));

    [Fact]
    public void Status_bits_named_per_iso_14229()
    {
        Assert.Equal(new[] { "testFailed", "pendingDTC", "confirmedDTC" }, DtcStatusBits.Set(0x0D, plain: false));
        Assert.Equal("warningIndicatorRequested", DtcStatusBits.Names[7]);
    }

    [Fact]
    public void Snapshot_with_one_identifier_splits_the_did()
    {
        var s = UdsDtcReader.ParseSnapshot(Convert.FromHexString("5904912345" + "2F" + "01" + "01" + "DD00" + "00012345"))!;
        Assert.Equal(0x01, s.Number);
        Assert.Equal((ushort)0xDD00, s.FirstDid);
        Assert.Equal("00012345", s.Hex);
        Assert.Null(UdsDtcReader.ParseSnapshot(Convert.FromHexString("59049123452F"))); // no record stored
    }

    [Fact]
    public void Snapshot_with_several_identifiers_stays_raw()
    {
        var s = UdsDtcReader.ParseSnapshot(Convert.FromHexString("5904912345" + "2F" + "01" + "02" + "DD00AABB" + "DD01CC"))!;
        Assert.Null(s.FirstDid);
        Assert.Equal("DD00AABBDD01CC", s.Hex);
    }

    [Fact]
    public async Task Count_and_details_from_simulator()
    {
        var (_, _, ch) = await AsBuiltServiceSafetyTests.Connect();
        var r = new UdsDtcReader(new UdsClient(ch, 0x726));
        Assert.Equal(1, await r.CountAsync());
        var d = await r.DetailAsync(0x912345, 0x2F);
        var snap = Assert.Single(d.Snapshots);
        Assert.Equal((ushort)0xDD00, snap.FirstDid);
        Assert.Equal("05", Assert.Single(d.Extended).Hex);
        var text = UdsDtcReader.Format("B1123-45", d);
        Assert.Contains("[x] bit 3 confirmedDTC", text);
        Assert.Contains("[ ] bit 7 warningIndicatorRequested", text);
        Assert.Contains("DID 0xDD00", text);
    }

    [Fact]
    public async Task Details_for_a_dtc_the_module_does_not_hold_report_notes_not_errors()
    {
        var (_, _, ch) = await AsBuiltServiceSafetyTests.Connect();
        var d = await new UdsDtcReader(new UdsClient(ch, 0x726)).DetailAsync(0x111111, 0x08);
        Assert.Empty(d.Snapshots);
        Assert.Contains(d.Notes, n => n.Contains("No freeze-frame"));
        Assert.Contains(d.Notes, n => n.Contains("19 06") && n.Contains("0x31"));
    }

    [Fact]
    public async Task Pending_and_permanent_obd_dtcs()
    {
        var (_, _, ch) = await AsBuiltServiceSafetyTests.Connect();
        Assert.Equal(new[] { (0x7E8u, "P0171") }, await ObdDtcExtras.ReadListAsync(ch, 0x07));
        Assert.Equal(new[] { (0x7E8u, "P0301") }, await ObdDtcExtras.ReadListAsync(ch, 0x0A));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ObdDtcExtras.ReadListAsync(ch, 0x04)); // 04 would CLEAR codes
    }

    [Fact]
    public async Task Freeze_frame_decodes_supported_pids()
    {
        var (sim, _, ch) = await AsBuiltServiceSafetyTests.Connect();
        var ff = await ObdDtcExtras.ReadFreezeFrameAsync(ch);
        Assert.Equal("P0301", ff.Single(v => v.Pid == 0x02).Value);
        Assert.Equal("1000 rpm", ff.Single(v => v.Pid == 0x0C).Value);
        Assert.Equal("50 °C", ff.Single(v => v.Pid == 0x05).Value);
        Assert.All(sim.Sent, x => Assert.True(x.Request[..2] is "02" or "01" or "3E" or "22" or "10", x.Request)); // read-only services only
    }

    [Fact]
    public void Dtc_list_parse_checks_the_service()
    {
        Assert.Equal(new[] { "P0171" }, ObdDtcExtras.ParseDtcList(0x07, new byte[] { 0x47, 0x01, 0x01, 0x71 }));
        Assert.Empty(ObdDtcExtras.ParseDtcList(0x0A, new byte[] { 0x47, 0x01, 0x01, 0x71 }));
    }
}
