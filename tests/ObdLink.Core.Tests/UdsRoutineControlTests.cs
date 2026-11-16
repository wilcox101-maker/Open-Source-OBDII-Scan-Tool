using ObdLink.Core.ConsoleCmds;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>0x31 RoutineControl (ISO 14229-1): request building, echo checks, write gate, refused ranges, pending handling.</summary>
public class UdsRoutineControlTests
{
    private const uint Tx = 0x7E0, Rx = 0x7E8;
    private static DiagResponse R(params byte[] p) => new(Rx, p);

    private class Scripted : IDiagChannel
    {
        private readonly Queue<DiagResponse[]> _replies;
        public List<byte[]> Sent { get; } = new();
        public Scripted(params DiagResponse[][] replies) => _replies = new(replies);
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

    private static UdsClient Client(IDiagChannel ch) => new(ch, Tx) { PendingRetryDelay = TimeSpan.Zero };

    [Fact]
    public async Task Start_sends_31_01_rid_option_and_returns_status()
    {
        var ch = new Scripted(new[] { R(0x71, 0x01, 0x12, 0x34, 0x20, 0xAA) });
        var r = await Client(ch).StartRoutineAsync(0x1234, new byte[] { 0x05 });
        Assert.Equal(new byte[] { 0x31, 0x01, 0x12, 0x34, 0x05 }, Assert.Single(ch.Sent));
        Assert.Equal(0x1234, r.RoutineId);
        Assert.Equal(RoutineControlType.Start, r.Type);
        Assert.Equal(new byte[] { 0x20, 0xAA }, r.StatusRecord);
    }

    [Fact]
    public async Task Stop_and_results_use_sub_functions_02_and_03()
    {
        var ch = new Scripted(new[] { R(0x71, 0x02, 0x12, 0x34) }, new[] { R(0x71, 0x03, 0x12, 0x34, 0x00, 0x01) });
        var uds = Client(ch);
        Assert.Empty((await uds.StopRoutineAsync(0x1234)).StatusRecord);
        Assert.Equal(new byte[] { 0x00, 0x01 }, (await uds.RequestRoutineResultsAsync(0x1234)).StatusRecord);
        Assert.Equal(new byte[] { 0x31, 0x02, 0x12, 0x34 }, ch.Sent[0]);
        Assert.Equal(new byte[] { 0x31, 0x03, 0x12, 0x34 }, ch.Sent[1]);
    }

    [Fact]
    public async Task Wrong_routine_echo_is_rejected()
    {
        var ch = new Scripted(new[] { R(0x71, 0x01, 0x12, 0x35) });
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => Client(ch).StartRoutineAsync(0x1234));
        Assert.Contains("0x1235", ex.Message);
    }

    [Fact]
    public async Task Wrong_sub_function_echo_is_rejected()
    {
        var ch = new Scripted(new[] { R(0x71, 0x02, 0x12, 0x34) });
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(ch).StartRoutineAsync(0x1234));
    }

    [Fact]
    public async Task Short_reply_is_rejected()
    {
        var ch = new Scripted(new[] { R(0x71, 0x01, 0x12) });
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(ch).StartRoutineAsync(0x1234));
    }

    [Fact]
    public async Task Negative_response_surfaces_nrc()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x31, 0x22) });
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => Client(ch).StartRoutineAsync(0x1234));
        Assert.Equal(0x31, ex.Service);
        Assert.Equal(0x22, ex.Nrc);
        Assert.Contains("RoutineControl", ex.Message);
    }

    [Fact]
    public void Results_with_an_option_record_is_refused()
    {
        Assert.Throws<ArgumentException>(() => UdsRoutines.BuildRequest(RoutineControlType.RequestResults, 0x1234, new byte[] { 1 }));
    }

    [Fact]
    public void Undefined_sub_function_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UdsRoutines.BuildRequest((RoutineControlType)0x04, 0x1234));
    }

    // ---- Write gate --------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(RoutineControlType.Start)]
    [InlineData(RoutineControlType.Stop)]
    [InlineData(RoutineControlType.RequestResults)]
    public async Task Read_only_mode_blocks_every_sub_function_before_sending(RoutineControlType type)
    {
        var ch = new Scripted();
        var uds = Client(ch);
        uds.Gate = new WriteGate(() => true);
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => uds.RequestAsync(UdsRoutines.BuildRequest(type, 0x1234)));
        Assert.Empty(ch.Sent);
    }

    [Fact]
    public async Task Open_gate_lets_routines_through()
    {
        var ch = new Scripted(new[] { R(0x71, 0x01, 0x12, 0x34) });
        var uds = Client(ch);
        uds.Gate = new WriteGate(() => false);
        await uds.StartRoutineAsync(0x1234);
        Assert.Single(ch.Sent);
    }

    // ---- Refused ranges ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0xE200)]
    [InlineData(0xE201)]
    [InlineData(0xE2FF)]
    public async Task Pyrotechnic_routines_are_refused_by_the_helpers(int rid)
    {
        var ch = new Scripted();
        var uds = Client(ch);
        uds.Gate = new WriteGate(() => false);
        await Assert.ThrowsAsync<RoutineRefusedException>(() => uds.StartRoutineAsync((ushort)rid));
        await Assert.ThrowsAsync<RoutineRefusedException>(() => uds.RequestRoutineResultsAsync((ushort)rid));
        Assert.Empty(ch.Sent);
    }

    [Theory]
    [InlineData(0x01)]
    [InlineData(0x81)] // suppress bit set
    [InlineData(0x03)]
    public async Task Pyrotechnic_routines_are_refused_on_the_raw_request_path(byte sub)
    {
        var ch = new Scripted();
        var uds = Client(ch); // no gate at all
        var ex = await Assert.ThrowsAsync<RoutineRefusedException>(() => uds.RequestAsync(new byte[] { 0x31, sub, 0xE2, 0x00 }));
        Assert.Equal(0xE200, ex.RoutineId);
        Assert.Empty(ch.Sent);
    }

    [Theory]
    [InlineData(0xFF00)]
    [InlineData(0xFF01)]
    public async Task Programming_routines_are_refused_by_the_helpers(int rid)
    {
        var ch = new Scripted();
        await Assert.ThrowsAsync<RoutineRefusedException>(() => Client(ch).StartRoutineAsync((ushort)rid));
        Assert.Empty(ch.Sent);
    }

    [Theory]
    [InlineData(0x0000, RoutineIdRange.IsoReserved)]
    [InlineData(0x0100, RoutineIdRange.Tachograph)]
    [InlineData(0x0202, RoutineIdRange.VehicleManufacturer)]
    [InlineData(0xDFFF, RoutineIdRange.VehicleManufacturer)]
    [InlineData(0xE000, RoutineIdRange.ObdTest)]
    [InlineData(0xE200, RoutineIdRange.PyrotechnicDeploy)]
    [InlineData(0xE2FF, RoutineIdRange.PyrotechnicDeploy)]
    [InlineData(0xE300, RoutineIdRange.IsoReserved)]
    [InlineData(0xF000, RoutineIdRange.SystemSupplier)]
    [InlineData(0xFF00, RoutineIdRange.Programming)]
    [InlineData(0xFF02, RoutineIdRange.EraseMirrorMemoryDtcs)]
    [InlineData(0xFFFF, RoutineIdRange.IsoReserved)]
    public void Annex_F_ranges(int rid, RoutineIdRange expected) => Assert.Equal(expected, UdsRoutines.Classify((ushort)rid));

    [Theory]
    [InlineData("3101E200", true, true)]
    [InlineData("31 01 E2 05", true, true)]
    [InlineData("0431 01E2 00", false, true)]     // ISO-TP single frame, formatting off
    [InlineData("100A3101E2000102", false, true)] // first frame, formatting off
    [InlineData("3101E300", true, false)]         // not pyrotechnic: normal write rules
    public void Console_blocks_pyrotechnic_even_with_writes_allowed(string line, bool autoFormat, bool blocked)
    {
        var d = ConsoleGuard.Evaluate(line, readOnly: false, allowWrites: true, canAutoFormat: autoFormat);
        Assert.Equal(!blocked, d.Allowed);
        if (blocked) Assert.Contains("pyrotechnic", d.Reason);
    }

    // ---- Pending / retry ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Elm_does_not_resend_start_after_pending()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x31, 0x78) }, new[] { R(0x71, 0x01, 0x12, 0x34) });
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => Client(ch).StartRoutineAsync(0x1234));
        Assert.Equal(0x78, ex.Nrc);
        Assert.Single(ch.Sent); // starting a routine twice could run it twice
    }

    [Fact]
    public async Task Elm_resends_results_request_after_pending()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x31, 0x78) }, new[] { R(0x71, 0x03, 0x12, 0x34, 0x00) });
        var r = await Client(ch).RequestRoutineResultsAsync(0x1234);
        Assert.Equal(new byte[] { 0x00 }, r.StatusRecord);
        Assert.Equal(2, ch.Sent.Count);
    }

    [Fact]
    public async Task Receive_channel_waits_for_final_start_reply_without_resending()
    {
        var ch = new Listening(new[] { R(0x7F, 0x31, 0x78) }, new[] { R(0x7F, 0x31, 0x78) }, new[] { R(0x71, 0x01, 0x12, 0x34, 0x10) });
        var r = await Client(ch).StartRoutineAsync(0x1234);
        Assert.Single(ch.Sent);
        Assert.Equal(new byte[] { 0x10 }, r.StatusRecord);
    }

    [Theory]
    [InlineData(new byte[] { 0x31, 0x03, 0x12, 0x34 }, true)]
    [InlineData(new byte[] { 0x31, 0x83, 0x12, 0x34 }, true)]
    [InlineData(new byte[] { 0x31, 0x01, 0x12, 0x34 }, false)]
    [InlineData(new byte[] { 0x31, 0x02, 0x12, 0x34 }, false)]
    [InlineData(new byte[] { 0x31 }, false)]
    [InlineData(new byte[] { 0x22, 0xF1, 0x90 }, true)]
    [InlineData(new byte[] { 0x2E, 0xF1, 0x90 }, false)]
    public void Idempotency(byte[] req, bool expected) => Assert.Equal(expected, UdsClient.IsIdempotent(req));
}
