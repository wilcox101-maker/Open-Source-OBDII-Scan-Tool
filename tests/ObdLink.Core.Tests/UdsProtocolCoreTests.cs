using ObdLink.Core.ConsoleCmds;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>UDS protocol core (ISO 14229-1/-2): service table, NRCs, server timing, response pending, suppressed responses, multi-DID 22, TesterPresent keep-alive, service gate.</summary>
public class UdsProtocolCoreTests
{
    private const uint Tx = 0x726, Rx = 0x72E;
    private static DiagResponse R(params byte[] p) => new(Rx, p);

    /// <summary>Scripted channel: one reply set per request; optional extra reply sets for ReceiveAsync (listen without sending).</summary>
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
        public List<TimeSpan> Waits { get; } = new();
        public Listening(DiagResponse[] first, params DiagResponse[][] later) : base(first) => _later = new(later);
        public Task<IReadOnlyList<DiagResponse>> ReceiveAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            Waits.Add(timeout);
            return Task.FromResult<IReadOnlyList<DiagResponse>>(_later.Count > 0 ? _later.Dequeue() : Array.Empty<DiagResponse>());
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // ---- v0.23.19: a negative response for another service is never this request's outcome ---------------------

    [Fact]
    public async Task Negative_response_for_another_service_is_not_reported_as_this_requests_NRC()
    {
        // A late "7F 31 22" (answer to an earlier routine) arrives while 2E is outstanding: the write's outcome is unknown, not "conditionsNotCorrect".
        var ch = new Scripted(new[] { R(0x7F, 0x31, 0x22) });
        var uds = new UdsClient(ch, Tx);
        var ex = await Assert.ThrowsAsync<UdsStrayResponseException>(() => uds.RequestAsync(new byte[] { 0x2E, 0xDE, 0x00, 0x01 }));
        Assert.Contains("negative response for service 0x31", ex.Message);
        Assert.Contains("outcome of 2EDE0001 is unknown", ex.Message);
    }

    [Fact]
    public async Task Stray_pending_for_another_service_is_not_taken_as_pending_for_this_request()
    {
        var ch = new Listening(new[] { R(0x7F, 0x31, 0x78) }, new[] { R(0x6E, 0xDE, 0x00) });
        var uds = new UdsClient(ch, Tx);
        await Assert.ThrowsAsync<UdsStrayResponseException>(() => uds.RequestAsync(new byte[] { 0x2E, 0xDE, 0x00, 0x01 }));
        Assert.Empty(ch.Waits); // did not start listening on someone else's "pending"
    }

    [Fact]
    public async Task Stray_busy_for_another_service_does_not_trigger_a_resend()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x19, 0x21) }, new[] { R(0x62, 0xF1, 0x90, 0x41) });
        var uds = new UdsClient(ch, Tx) { PendingRetryDelay = TimeSpan.Zero };
        await Assert.ThrowsAsync<UdsStrayResponseException>(() => uds.RequestAsync(new byte[] { 0x22, 0xF1, 0x90 }));
        Assert.Single(ch.Sent);
    }

    [Fact]
    public async Task Negative_response_for_this_service_is_still_reported()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x2E, 0x22) });
        var uds = new UdsClient(ch, Tx);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.RequestAsync(new byte[] { 0x2E, 0xDE, 0x00, 0x01 }));
        Assert.Equal(0x22, ex.Nrc);
        Assert.Equal(0x2E, ex.Service);
    }

    // ---- Service table -------------------------------------------------------------------------------------------

    [Fact]
    public void Console_guard_uses_the_same_state_changing_services_as_before()
    {
        // Byte-for-byte the set ConsoleGuard had before the table moved to UdsServices (no behaviour change for the console).
        byte[] before = { 0x04, 0x08, 0x11, 0x14, 0x27, 0x28, 0x2C, 0x2E, 0x2F, 0x31, 0x34, 0x35, 0x36, 0x37, 0x38, 0x3B, 0x3D, 0x85, 0x86, 0x87 };
        Assert.Equal(before.OrderBy(b => b), ConsoleGuard.StateChanging.OrderBy(b => b));
        Assert.Equal(before.OrderBy(b => b), UdsServices.StateChanging.OrderBy(b => b));
    }

    [Theory]
    [InlineData(0x22, false)]
    [InlineData(0x19, false)]
    [InlineData(0x10, false)]
    [InlineData(0x3E, false)]
    [InlineData(0x23, false)]
    [InlineData(0x2A, false)]
    [InlineData(0x2E, true)]
    [InlineData(0x31, true)]
    [InlineData(0x2F, true)]
    [InlineData(0x14, true)]
    [InlineData(0x27, true)]
    [InlineData(0x36, true)]
    public void State_changing_classification(byte sid, bool changing) => Assert.Equal(changing, UdsServices.IsStateChanging(sid));

    [Theory]
    [InlineData(0x10, true)]
    [InlineData(0x11, true)]
    [InlineData(0x19, true)]
    [InlineData(0x27, true)]
    [InlineData(0x28, true)]
    [InlineData(0x31, true)]
    [InlineData(0x3E, true)]
    [InlineData(0x85, true)]
    [InlineData(0x22, false)]
    [InlineData(0x2E, false)]
    [InlineData(0x2F, false)]
    [InlineData(0x14, false)]
    public void Sub_function_services(byte sid, bool hasSub) => Assert.Equal(hasSub, UdsServices.HasSubFunction(sid));

    [Fact]
    public void Suppress_bit_only_counts_on_services_with_a_sub_function()
    {
        Assert.True(UdsServices.SuppressesPositiveResponse(new byte[] { 0x3E, 0x80 }));
        Assert.True(UdsServices.SuppressesPositiveResponse(new byte[] { 0x10, 0x83 }));
        Assert.False(UdsServices.SuppressesPositiveResponse(new byte[] { 0x3E, 0x00 }));
        Assert.False(UdsServices.SuppressesPositiveResponse(new byte[] { 0x22, 0xF1, 0x90 })); // 0xF1 is a DID byte, not a sub-function
        Assert.False(UdsServices.SuppressesPositiveResponse(new byte[] { 0x3E }));
    }

    [Theory]
    [InlineData(0x23, "ReadMemoryByAddress")]
    [InlineData(0x2A, "ReadDataByPeriodicIdentifier")]
    [InlineData(0x2C, "DynamicallyDefineDataIdentifier")]
    [InlineData(0x28, "CommunicationControl")]
    [InlineData(0x85, "ControlDTCSetting")]
    [InlineData(0x34, "RequestDownload")]
    [InlineData(0x22, "ReadDataByIdentifier")]
    public void Service_names(byte sid, string name) => Assert.Equal(name, UdsNrc.ServiceName(sid));

    // ---- NRC table ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0x26, "failurePreventsExecutionOfRequestedAction")]
    [InlineData(0x73, "wrongBlockSequenceCounter")]
    [InlineData(0x81, "rpmTooHigh")]
    [InlineData(0x83, "engineIsRunning")]
    [InlineData(0x88, "vehicleSpeedTooHigh")]
    [InlineData(0x8D, "transmissionRangeNotInGear")]
    [InlineData(0x90, "shifterLeverNotInPark")]
    [InlineData(0x92, "voltageTooHigh")]
    [InlineData(0x93, "voltageTooLow")]
    [InlineData(0x31, "requestOutOfRange")]
    [InlineData(0x78, "requestCorrectlyReceived-ResponsePending")]
    public void Nrc_names_per_iso_14229_1(byte nrc, string name) => Assert.Equal(name, UdsNrc.Describe(nrc));

    [Fact]
    public void Manufacturer_condition_range_is_named_not_unknown()
    {
        Assert.Equal("vehicleManufacturerSpecificConditionsNotCorrect", UdsNrc.Describe(0xF3));
        Assert.Equal("reservedForSpecificConditionsNotCorrect", UdsNrc.Describe(0xA0));
        Assert.Contains("engine", UdsNrc.Explain(0x83));
    }

    // ---- Server timing ------------------------------------------------------------------------------------------

    [Fact]
    public void Session_reply_carries_p2_and_p2_star()
    {
        // 50 03 | P2max 0x0032 = 50 ms | P2*max 0x01F4 = 500 x 10 ms = 5000 ms
        Assert.True(UdsServerTiming.TryParse(new byte[] { 0x50, 0x03, 0x00, 0x32, 0x01, 0xF4 }, out var t));
        Assert.Equal(TimeSpan.FromMilliseconds(50), t.P2);
        Assert.Equal(TimeSpan.FromMilliseconds(5000), t.P2Star);
    }

    [Fact]
    public void Short_session_reply_keeps_defaults()
    {
        Assert.False(UdsServerTiming.TryParse(new byte[] { 0x50, 0x03 }, out _));
        Assert.Equal(TimeSpan.FromMilliseconds(50), UdsServerTiming.Default.P2);
        Assert.Equal(TimeSpan.FromMilliseconds(5000), UdsServerTiming.Default.P2Star);
    }

    [Fact]
    public async Task Session_control_stores_server_timing()
    {
        var ch = new Scripted(new[] { R(0x50, 0x03, 0x00, 0x19, 0x00, 0xC8) }); // 25 ms / 2000 ms
        var uds = new UdsClient(ch, Tx);
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        Assert.Equal(TimeSpan.FromMilliseconds(25), uds.ServerTiming.P2);
        Assert.Equal(TimeSpan.FromMilliseconds(2000), uds.ServerTiming.P2Star);
    }

    // ---- Response pending (0x78) ----------------------------------------------------------------------------------

    [Fact]
    public async Task Pending_then_final_on_a_listening_channel_waits_without_resending()
    {
        var ch = new Listening(new[] { R(0x7F, 0x2E, 0x78) }, new[] { R(0x7F, 0x2E, 0x78) }, new[] { R(0x6E, 0xDE, 0x00) });
        var uds = new UdsClient(ch, Tx);
        var r = await uds.RequestAsync(new byte[] { 0x2E, 0xDE, 0x00, 0x01 });
        Assert.Equal(new byte[] { 0x6E, 0xDE, 0x00 }, r);
        Assert.Single(ch.Sent); // a non-idempotent write is never sent twice
        Assert.Equal(2, ch.Waits.Count);
        Assert.All(ch.Waits, w => Assert.Equal(UdsServerTiming.Default.P2Star, w));
    }

    [Fact]
    public async Task Listening_channel_uses_the_server_p2_star()
    {
        var ch = new Listening(new[] { R(0x50, 0x03, 0x00, 0x32, 0x00, 0x64) }, // P2* = 1000 ms
            new[] { R(0x71, 0x01, 0x02, 0x02) });
        var uds = new UdsClient(ch, Tx);
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        ch.Queue(R(0x7F, 0x31, 0x78));
        await uds.RequestAsync(new byte[] { 0x31, 0x01, 0x02, 0x02 });
        Assert.Equal(TimeSpan.FromMilliseconds(1000), Assert.Single(ch.Waits));
    }

    [Fact]
    public async Task Pending_with_no_final_answer_on_a_listening_channel_times_out()
    {
        var ch = new Listening(new[] { R(0x7F, 0x2E, 0x78) }); // nothing more ever arrives
        var uds = new UdsClient(ch, Tx);
        var ex = await Assert.ThrowsAsync<TimeoutException>(() => uds.RequestAsync(new byte[] { 0x2E, 0xDE, 0x00, 0x01 }));
        Assert.Contains("response pending", ex.Message);
        Assert.Single(ch.Sent);
    }

    [Fact]
    public async Task Pending_forever_on_a_listening_channel_is_bounded()
    {
        var pend = new[] { R(0x7F, 0x31, 0x78) };
        var ch = new Listening(pend, Enumerable.Repeat(pend, 100).ToArray());
        var uds = new UdsClient(ch, Tx) { MaxPendingReplies = 5 };
        await Assert.ThrowsAsync<TimeoutException>(() => uds.RequestAsync(new byte[] { 0x31, 0x01, 0x02, 0x02 }));
        Assert.Equal(5, ch.Waits.Count);
    }

    [Fact]
    public async Task Elm_style_channel_still_reports_pending_on_a_write_unchanged()
    {
        // No receive capability: a write that only got 0x78 is NOT re-sent and surfaces as NRC 0x78, so the As-Built read-back path decides.
        var ch = new Scripted(new[] { R(0x7F, 0x2E, 0x78) });
        var uds = new UdsClient(ch, Tx);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.RequestAsync(new byte[] { 0x2E, 0xDE, 0x00, 0x01 }));
        Assert.Equal(0x78, ex.Nrc);
        Assert.Single(ch.Sent);
    }

    [Fact]
    public async Task Elm_style_channel_still_resends_an_idempotent_read_after_pending()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x22, 0x78) }, new[] { R(0x62, 0xF1, 0x90, 0x41) });
        var uds = new UdsClient(ch, Tx) { PendingRetryDelay = TimeSpan.Zero };
        Assert.Equal(new byte[] { 0x41 }, await uds.ReadDataByIdentifierAsync(0xF190));
        Assert.Equal(2, ch.Sent.Count);
    }

    // ---- Suppressed positive response -------------------------------------------------------------------------------

    [Fact]
    public async Task Suppressed_tester_present_with_no_reply_is_success()
    {
        var ch = new Scripted(Array.Empty<DiagResponse>());
        var uds = new UdsClient(ch, Tx);
        var r = await uds.TesterPresentAsync(suppressResponse: true);
        Assert.Empty(r);
        Assert.Equal(new byte[] { 0x3E, 0x80 }, Assert.Single(ch.Sent));
    }

    [Fact]
    public async Task Suppressed_request_still_throws_on_a_negative_response()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x3E, 0x12) });
        var uds = new UdsClient(ch, Tx);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.TesterPresentAsync(suppressResponse: true));
        Assert.Equal(0x12, ex.Nrc);
    }

    [Fact]
    public async Task Unsuppressed_request_with_no_reply_still_times_out()
    {
        var uds = new UdsClient(new Scripted(Array.Empty<DiagResponse>()), Tx);
        await Assert.ThrowsAsync<TimeoutException>(() => uds.TesterPresentAsync());
    }

    // ---- Multi-DID ReadDataByIdentifier ------------------------------------------------------------------------------

    [Fact]
    public void Multi_did_reply_is_split_by_known_lengths()
    {
        var resp = new byte[] { 0x62, 0x1E, 0x1C, 0x04, 0x60, 0xF4, 0x05, 0x7B };
        var v = UdsClient.ParseMultiDid(resp, new[] { new DidRequest(0x1E1C, 2), new DidRequest(0xF405, 1) });
        Assert.Equal(2, v.Count);
        Assert.Equal((ushort)0x1E1C, v[0].Did); Assert.Equal(new byte[] { 0x04, 0x60 }, v[0].Data);
        Assert.Equal((ushort)0xF405, v[1].Did); Assert.Equal(new byte[] { 0x7B }, v[1].Data);
    }

    [Theory]
    [InlineData(new byte[] { 0x62, 0x1E, 0x1C, 0x04, 0x60, 0xF4, 0x06, 0x7B })] // second DID echo wrong
    [InlineData(new byte[] { 0x62, 0x1E, 0x1C, 0x04, 0x60, 0xF4, 0x05 })]       // short
    [InlineData(new byte[] { 0x62, 0x1E, 0x1C, 0x04, 0x60, 0xF4, 0x05, 0x7B, 0x00 })] // trailing byte
    public void Multi_did_reply_that_does_not_match_is_rejected(byte[] resp)
        => Assert.Throws<InvalidDataException>(() => UdsClient.ParseMultiDid(resp, new[] { new DidRequest(0x1E1C, 2), new DidRequest(0xF405, 1) }));

    [Fact]
    public async Task Multi_did_read_sends_one_request_with_every_did()
    {
        var ch = new Scripted(new[] { R(0x62, 0x1E, 0x1C, 0x04, 0x60, 0xF4, 0x05, 0x7B) });
        var uds = new UdsClient(ch, Tx);
        var v = await uds.ReadDataByIdentifiersAsync(new[] { new DidRequest(0x1E1C, 2), new DidRequest(0xF405, 1) });
        Assert.Equal(new byte[] { 0x22, 0x1E, 0x1C, 0xF4, 0x05 }, Assert.Single(ch.Sent));
        Assert.Equal(2, v.Count);
    }

    [Fact]
    public async Task Multi_did_read_rejects_empty_and_duplicate_lists()
    {
        var uds = new UdsClient(new Scripted(), Tx);
        await Assert.ThrowsAsync<ArgumentException>(() => uds.ReadDataByIdentifiersAsync(Array.Empty<DidRequest>()));
        await Assert.ThrowsAsync<ArgumentException>(() => uds.ReadDataByIdentifiersAsync(new[] { new DidRequest(0x1E1C, 2), new DidRequest(0x1E1C, 2) }));
    }

    // ---- TesterPresent keep-alive ----------------------------------------------------------------------------------

    [Fact]
    public async Task Keeper_sends_suppressed_tester_present_only_when_the_module_has_been_idle()
    {
        var clock = new ManualClock();
        var ch = new Scripted(new[] { R(0x50, 0x03) });
        var uds = new UdsClient(ch, Tx) { Clock = clock };
        var keeper = new TesterPresentKeeper(uds, TimeSpan.FromSeconds(2));

        Assert.False(keeper.IsDue); // nothing sent yet: no session to keep
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        clock.Now += TimeSpan.FromSeconds(1.9);
        Assert.False(await keeper.TickAsync());
        clock.Now += TimeSpan.FromSeconds(0.2);
        Assert.True(await keeper.TickAsync());
        Assert.Equal(new byte[] { 0x3E, 0x80 }, ch.Sent[^1]);
        Assert.False(await keeper.TickAsync()); // the TesterPresent itself counts as activity
    }

    [Fact]
    public async Task Keeper_stops_after_returning_to_the_default_session()
    {
        var clock = new ManualClock();
        var ch = new Scripted(new[] { R(0x50, 0x03) }, new[] { R(0x50, 0x01) });
        var uds = new UdsClient(ch, Tx) { Clock = clock };
        var keeper = new TesterPresentKeeper(uds, TimeSpan.FromSeconds(2));
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        await uds.DiagnosticSessionControlAsync(UdsSession.Default);
        clock.Now += TimeSpan.FromSeconds(10);
        Assert.False(keeper.IsDue);
        Assert.False(await keeper.TickAsync());
    }

    [Fact]
    public void Keeper_interval_must_be_inside_the_server_s3_window()
    {
        var uds = new UdsClient(new Scripted(), Tx);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TesterPresentKeeper(uds, TimeSpan.FromSeconds(5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TesterPresentKeeper(uds, TimeSpan.Zero));
    }

    // ---- Service gate -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Gate_blocks_state_changing_services_before_anything_is_sent()
    {
        var ch = new Scripted(new[] { R(0x62, 0xF1, 0x90, 0x41) });
        var uds = new UdsClient(ch, Tx) { Gate = new WriteGate(() => true) };
        var ex = await Assert.ThrowsAsync<ReadOnlyModeException>(() => uds.RequestAsync(new byte[] { 0x31, 0x01, 0x02, 0x02 }));
        Assert.Contains("RoutineControl", ex.Message);
        Assert.Empty(ch.Sent);
        Assert.Equal(new byte[] { 0x41 }, await uds.ReadDataByIdentifierAsync(0xF190)); // reads still pass
    }

    [Fact]
    public async Task No_gate_keeps_the_old_behaviour()
    {
        var ch = new Scripted(new[] { R(0x6E, 0xDE, 0x00) });
        var uds = new UdsClient(ch, Tx);
        await uds.WriteDataByIdentifierAsync(0xDE00, new byte[] { 1 });
        Assert.Single(ch.Sent);
    }
}
