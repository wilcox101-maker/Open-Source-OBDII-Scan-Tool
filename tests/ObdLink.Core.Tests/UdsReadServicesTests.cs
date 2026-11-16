using ObdLink.Core.Dtc;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Read-side UDS services (ISO 14229-1): remaining 19 sub-functions, 23 ReadMemoryByAddress, 2C dynamic DIDs, 2A periodic DIDs.</summary>
public class UdsReadServicesTests
{
    private const uint Tx = 0x7E0, Rx = 0x7E8;
    private static DiagResponse R(params byte[] p) => new(Rx, p);

    private class Scripted : IDiagChannel
    {
        private readonly Queue<DiagResponse[]> _replies;
        public List<byte[]> Sent { get; } = new();
        public Scripted(params DiagResponse[][] replies) => _replies = new(replies);
        public void Queue(params DiagResponse[] reply) => _replies.Enqueue(reply);
        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] request, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Sent.Add(request);
            return Task.FromResult<IReadOnlyList<DiagResponse>>(_replies.Count > 0 ? _replies.Dequeue() : Array.Empty<DiagResponse>());
        }
    }

    private sealed class Listening : Scripted, IDiagReceiveChannel
    {
        private readonly Queue<DiagResponse[]> _later;
        public Listening(DiagResponse[] first, params DiagResponse[][] later) : base(first) => _later = new(later);
        public Task<IReadOnlyList<DiagResponse>> ReceiveAsync(TimeSpan timeout, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DiagResponse>>(_later.Count > 0 ? _later.Dequeue() : Array.Empty<DiagResponse>());
    }

    // ---- 19 sub-functions ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Supported_dtcs_19_0A()
    {
        var ch = new Scripted(new[] { R(0x59, 0x0A, 0xFF, 0x01, 0x71, 0x00, 0x50, 0xC1, 0x23, 0x00, 0x00) });
        var list = await new UdsDtcReader(new UdsClient(ch, Tx)).SupportedAsync();
        Assert.Equal(new byte[] { 0x19, 0x0A }, Assert.Single(ch.Sent));
        Assert.Equal(2, list.Count);
        Assert.Equal(0x017100u, list[0].Code); Assert.Equal(0x50, list[0].Status);
        Assert.Equal(0xC12300u, list[1].Code); Assert.Equal(0x00, list[1].Status);
    }

    [Fact]
    public async Task Permanent_dtcs_19_15()
    {
        var ch = new Scripted(new[] { R(0x59, 0x15, 0xFF, 0x03, 0x01, 0x00, 0x08) });
        var list = await new UdsDtcReader(new UdsClient(ch, Tx)).PermanentAsync();
        Assert.Equal(new byte[] { 0x19, 0x15 }, Assert.Single(ch.Sent));
        Assert.Equal(0x030100u, Assert.Single(list).Code);
    }

    [Theory]
    [InlineData(DtcOccurrence.FirstFailed, 0x0B)]
    [InlineData(DtcOccurrence.FirstConfirmed, 0x0C)]
    [InlineData(DtcOccurrence.MostRecentFailed, 0x0D)]
    [InlineData(DtcOccurrence.MostRecentConfirmed, 0x0E)]
    public async Task First_and_most_recent_dtc(DtcOccurrence which, byte sub)
    {
        var ch = new Scripted(new[] { R(0x59, sub, 0xFF, 0x03, 0x01, 0x00, 0x2F) });
        var d = await new UdsDtcReader(new UdsClient(ch, Tx)).OccurrenceAsync(which);
        Assert.Equal(new byte[] { 0x19, sub }, Assert.Single(ch.Sent));
        Assert.Equal(0x030100u, d!.Code);
        Assert.Equal(0x2F, d.Status);
    }

    [Fact]
    public async Task No_first_or_most_recent_dtc_is_null()
    {
        var ch = new Scripted(new[] { R(0x59, 0x0E, 0xFF) });
        Assert.Null(await new UdsDtcReader(new UdsClient(ch, Tx)).OccurrenceAsync(DtcOccurrence.MostRecentConfirmed));
    }

    [Fact]
    public async Task Fault_detection_counters_19_14_are_signed()
    {
        // 59 14 then (DTC, FDC) pairs; no availability mask. FDC 0x7F = +127 (failed), 0x80 = -128 (passed), 0x10 = +16 (maturing).
        var ch = new Scripted(new[] { R(0x59, 0x14, 0x01, 0x71, 0x00, 0x7F, 0x03, 0x01, 0x00, 0x80, 0xC1, 0x23, 0x00, 0x10) });
        var list = await new UdsDtcReader(new UdsClient(ch, Tx)).FaultDetectionCountersAsync();
        Assert.Equal(new byte[] { 0x19, 0x14 }, Assert.Single(ch.Sent));
        Assert.Equal(new sbyte[] { 127, -128, 16 }, list.Select(x => x.Counter));
        Assert.Equal(0xC12300u, list[2].Code);
    }

    [Fact]
    public async Task Snapshot_by_record_number_19_05()
    {
        // 59 05 rec DTC(3) status idCount DID(2) data
        var ch = new Scripted(new[] { R(0x59, 0x05, 0x01, 0x03, 0x01, 0x00, 0x2F, 0x01, 0xDD, 0x00, 0x12, 0x34) });
        var s = await new UdsDtcReader(new UdsClient(ch, Tx)).SnapshotByRecordAsync(0x01);
        Assert.Equal(new byte[] { 0x19, 0x05, 0x01 }, Assert.Single(ch.Sent));
        Assert.Equal(0x030100u, s!.Dtc.Code);
        Assert.Equal(0x2F, s.Dtc.Status);
        Assert.Equal((byte)0x01, s.Snapshot.Number);
        Assert.Equal((ushort)0xDD00, s.Snapshot.FirstDid);
        Assert.Equal(new byte[] { 0x12, 0x34 }, s.Snapshot.Data);
    }

    [Fact]
    public async Task Empty_snapshot_record_19_05_is_null()
    {
        var ch = new Scripted(new[] { R(0x59, 0x05, 0x01) });
        Assert.Null(await new UdsDtcReader(new UdsClient(ch, Tx)).SnapshotByRecordAsync(0x01));
    }

    [Theory]
    [InlineData(new byte[] { 0x59, 0x02, 0xFF, 0x01, 0x71, 0x00, 0x50 })]          // wrong sub-function echoed
    [InlineData(new byte[] { 0x59, 0x0A, 0xFF, 0x01, 0x71, 0x00 })]                // half a record
    public void Dtc_list_reply_that_does_not_match_is_rejected(byte[] resp)
        => Assert.Throws<InvalidDataException>(() => UdsDtcReader.ParseDtcStatusList(resp, 0x0A));

    // ---- 23 ReadMemoryByAddress -------------------------------------------------------------------------------------

    [Fact]
    public void Address_and_length_format_identifier()
    {
        Assert.Equal(0x24, UdsMemory.AddressAndLengthFormat(addressBytes: 4, sizeBytes: 2));
        Assert.Equal(0x13, UdsMemory.AddressAndLengthFormat(addressBytes: 3, sizeBytes: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => UdsMemory.AddressAndLengthFormat(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => UdsMemory.AddressAndLengthFormat(4, 5));
    }

    [Fact]
    public async Task Read_memory_builds_request_and_checks_length()
    {
        var ch = new Scripted(new[] { R(0x63, 0xAA, 0xBB, 0xCC, 0xDD) });
        var data = await new UdsClient(ch, Tx).ReadMemoryByAddressAsync(0x00FF8000, 4);
        Assert.Equal(new byte[] { 0x23, 0x24, 0x00, 0xFF, 0x80, 0x00, 0x00, 0x04 }, Assert.Single(ch.Sent));
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, data);
    }

    [Fact]
    public async Task Read_memory_with_wrong_length_reply_is_rejected()
    {
        var ch = new Scripted(new[] { R(0x63, 0xAA, 0xBB) });
        await Assert.ThrowsAsync<InvalidDataException>(() => new UdsClient(ch, Tx).ReadMemoryByAddressAsync(0x00FF8000, 4));
    }

    [Fact]
    public async Task Read_memory_rejects_values_that_do_not_fit()
    {
        var uds = new UdsClient(new Scripted(), Tx);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.ReadMemoryByAddressAsync(0x01000000, 4, addressBytes: 3));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.ReadMemoryByAddressAsync(0x1000, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.ReadMemoryByAddressAsync(0x1000, 300, sizeBytes: 1));
    }

    [Fact]
    public void Read_memory_is_a_read() => Assert.False(UdsServices.IsStateChanging(0x23));

    // ---- 2C DynamicallyDefineDataIdentifier ---------------------------------------------------------------------------

    [Fact]
    public async Task Define_dynamic_did_by_identifier()
    {
        var ch = new Scripted(new[] { R(0x6C, 0x01, 0xF3, 0x00) });
        await new UdsClient(ch, Tx).DefineDynamicDidByIdentifierAsync(0xF300, new[] { new DidSource(0x1E1C, 1, 2), new DidSource(0xF405, 1, 1) });
        Assert.Equal(new byte[] { 0x2C, 0x01, 0xF3, 0x00, 0x1E, 0x1C, 0x01, 0x02, 0xF4, 0x05, 0x01, 0x01 }, Assert.Single(ch.Sent));
    }

    [Fact]
    public async Task Define_dynamic_did_by_memory_address()
    {
        var ch = new Scripted(new[] { R(0x6C, 0x02, 0xF2, 0x01) });
        await new UdsClient(ch, Tx).DefineDynamicDidByMemoryAsync(0xF201, new[] { new MemoryBlock(0x00FF8000, 2) });
        Assert.Equal(new byte[] { 0x2C, 0x02, 0xF2, 0x01, 0x24, 0x00, 0xFF, 0x80, 0x00, 0x00, 0x02 }, Assert.Single(ch.Sent));
    }

    [Fact]
    public async Task Clear_dynamic_did()
    {
        var ch = new Scripted(new[] { R(0x6C, 0x03, 0xF3, 0x00) }, new[] { R(0x6C, 0x03) });
        var uds = new UdsClient(ch, Tx);
        await uds.ClearDynamicDidAsync(0xF300);
        await uds.ClearDynamicDidAsync();
        Assert.Equal(new byte[] { 0x2C, 0x03, 0xF3, 0x00 }, ch.Sent[0]);
        Assert.Equal(new byte[] { 0x2C, 0x03 }, ch.Sent[1]);
    }

    [Fact]
    public async Task Dynamic_did_must_be_in_the_iso_range_and_sources_valid()
    {
        var uds = new UdsClient(new Scripted(), Tx);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.DefineDynamicDidByIdentifierAsync(0xF190, new[] { new DidSource(0x1E1C, 1, 2) }));
        await Assert.ThrowsAsync<ArgumentException>(() => uds.DefineDynamicDidByIdentifierAsync(0xF300, Array.Empty<DidSource>()));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.DefineDynamicDidByIdentifierAsync(0xF300, new[] { new DidSource(0x1E1C, 0, 2) }));
    }

    [Fact]
    public async Task Define_reply_echo_mismatch_is_rejected()
    {
        var ch = new Scripted(new[] { R(0x6C, 0x01, 0xF3, 0x01) });
        await Assert.ThrowsAsync<InvalidDataException>(() => new UdsClient(ch, Tx).DefineDynamicDidByIdentifierAsync(0xF300, new[] { new DidSource(0x1E1C, 1, 2) }));
    }

    [Fact]
    public async Task Dynamic_did_definition_is_gated_in_read_only_mode()
    {
        var ch = new Scripted(new[] { R(0x6C, 0x01, 0xF3, 0x00) });
        var uds = new UdsClient(ch, Tx) { Gate = new WriteGate(() => true) };
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => uds.DefineDynamicDidByIdentifierAsync(0xF300, new[] { new DidSource(0x1E1C, 1, 2) }));
        Assert.Empty(ch.Sent);
    }

    // ---- 2A ReadDataByPeriodicIdentifier ------------------------------------------------------------------------------

    [Fact]
    public async Task Periodic_start_is_refused_on_a_channel_that_cannot_keep_receiving()
    {
        var ch = new Scripted(new[] { R(0x6A) });
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => new UdsClient(ch, Tx).StartPeriodicAsync(PeriodicRate.Medium, new ushort[] { 0xF201 }));
        Assert.Empty(ch.Sent); // nothing streamed onto an ELM/STN link
        Assert.Contains("receive", ex.Message);
    }

    [Fact]
    public async Task Periodic_start_receive_and_stop()
    {
        var ch = new Listening(new[] { R(0x6A) },
            new[] { R(0x01, 0x04, 0x60), R(0x02, 0x7B), new DiagResponse(0x7E9, new byte[] { 0x01, 0xFF, 0xFF }) },
            new[] { R(0x01, 0x04, 0x61) });
        ch.Queue(R(0x6A)); // reply to the stop request
        var uds = new UdsClient(ch, Tx);
        await uds.StartPeriodicAsync(PeriodicRate.Fast, new ushort[] { 0xF201, 0xF202 });
        Assert.Equal(new byte[] { 0x2A, 0x03, 0x01, 0x02 }, ch.Sent[0]);

        var lengths = new Dictionary<ushort, int> { [0xF201] = 2, [0xF202] = 1 };
        var first = await uds.ReceivePeriodicAsync(lengths, TimeSpan.FromMilliseconds(100));
        Assert.Equal(2, first.Count); // the 0x7E9 frame is another module's and is ignored
        Assert.Equal((ushort)0xF201, first[0].Did); Assert.Equal(new byte[] { 0x04, 0x60 }, first[0].Data);
        Assert.Equal((ushort)0xF202, first[1].Did); Assert.Equal(new byte[] { 0x7B }, first[1].Data);

        var second = await uds.ReceivePeriodicAsync(lengths, TimeSpan.FromMilliseconds(100));
        Assert.Equal(new byte[] { 0x04, 0x61 }, Assert.Single(second).Data);

        await uds.StopPeriodicAsync();
        Assert.Equal(new byte[] { 0x2A, 0x04 }, ch.Sent[^1]);
    }

    [Fact]
    public void Periodic_ids_must_be_F2xx()
    {
        Assert.Equal(0x01, UdsPeriodic.ToPeriodicId(0xF201));
        Assert.Throws<ArgumentOutOfRangeException>(() => UdsPeriodic.ToPeriodicId(0xF300));
        Assert.Throws<ArgumentOutOfRangeException>(() => UdsPeriodic.ToPeriodicId(0x1E1C));
    }

    [Fact]
    public void Periodic_frame_with_wrong_length_is_rejected()
        => Assert.Throws<InvalidDataException>(() => UdsPeriodic.ParseFrame(new byte[] { 0x01, 0x04 }, new Dictionary<ushort, int> { [0xF201] = 2 }));
}
