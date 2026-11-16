using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>85 ControlDTCSetting, 28 CommunicationControl, 2F InputOutputControl, and the restore leases.</summary>
public class UdsControlTests
{
    private const uint Tx = 0x7E0, Rx = 0x7E8;
    private static DiagResponse R(params byte[] p) => new(Rx, p);

    private sealed class Scripted : IDiagChannel
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

    private static UdsClient Client(IDiagChannel ch, bool readOnly = false) =>
        new(ch, Tx) { Gate = new WriteGate(() => readOnly), PendingRetryDelay = TimeSpan.Zero };

    // ---- Restore request table ---------------------------------------------------------------------------------------

    [Theory]
    [InlineData(new byte[] { 0x85, 0x01 }, true)]
    [InlineData(new byte[] { 0x85, 0x81 }, true)]
    [InlineData(new byte[] { 0x85, 0x02 }, false)]
    [InlineData(new byte[] { 0x85, 0x01, 0xFF }, false)]
    [InlineData(new byte[] { 0x28, 0x00, 0x01 }, true)]
    [InlineData(new byte[] { 0x28, 0x03, 0x01 }, false)]
    [InlineData(new byte[] { 0x28, 0x00 }, false)]
    [InlineData(new byte[] { 0x2F, 0xD1, 0x00, 0x00 }, true)]
    [InlineData(new byte[] { 0x2F, 0xD1, 0x00, 0x00, 0xFF }, true)]
    [InlineData(new byte[] { 0x2F, 0xD1, 0x00, 0x03, 0x01 }, false)]
    [InlineData(new byte[] { 0x2E, 0xD1, 0x00, 0x00 }, false)]
    [InlineData(new byte[] { 0x31, 0x02, 0x12, 0x34 }, false)]
    public void Restore_requests(byte[] req, bool expected) => Assert.Equal(expected, UdsServices.IsRestoreRequest(req));

    [Fact]
    public async Task Restore_path_refuses_anything_else()
    {
        var ch = new Scripted();
        await Assert.ThrowsAsync<ArgumentException>(() => Client(ch).RestoreAsync(new byte[] { 0x2E, 0xDE, 0x00, 0x01 }));
        Assert.Empty(ch.Sent);
    }

    // ---- 85 --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Disable_dtc_setting_then_lease_restores_once()
    {
        var ch = new Scripted(new[] { R(0xC5, 0x02) }, new[] { R(0xC5, 0x01) });
        await using (var lease = await Client(ch).DisableDtcSettingAsync())
        {
            Assert.False(lease.Restored);
            await lease.RestoreAsync();
            Assert.True(lease.Restored);
        }
        Assert.Equal(new[] { new byte[] { 0x85, 0x02 }, new byte[] { 0x85, 0x01 } }, ch.Sent);
    }

    [Fact]
    public async Task Dispose_restores_when_the_caller_did_not()
    {
        var ch = new Scripted(new[] { R(0xC5, 0x02) }, new[] { R(0xC5, 0x01) });
        var lease = await Client(ch).DisableDtcSettingAsync();
        await lease.DisposeAsync();
        await lease.DisposeAsync();
        Assert.True(lease.Restored);
        Assert.Equal(2, ch.Sent.Count);
    }

    [Fact]
    public async Task Read_only_blocks_the_take_over_before_sending()
    {
        var ch = new Scripted();
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => Client(ch, readOnly: true).DisableDtcSettingAsync());
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => Client(ch, readOnly: true).DisableCommunicationAsync(CommunicationControlType.DisableRxAndTx));
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => Client(ch, readOnly: true).AdjustOutputAsync(0xD100, new byte[] { 1 }));
        Assert.Empty(ch.Sent);
    }

    [Fact]
    public async Task Restore_goes_through_after_read_only_is_switched_on_mid_job()
    {
        bool readOnly = false;
        var ch = new Scripted(new[] { R(0xC5, 0x02) }, new[] { R(0xC5, 0x01) });
        var uds = new UdsClient(ch, Tx) { Gate = new WriteGate(() => readOnly) };
        var lease = await uds.DisableDtcSettingAsync();
        readOnly = true;
        await lease.RestoreAsync();
        Assert.True(lease.Restored);
        Assert.Equal(new byte[] { 0x85, 0x01 }, ch.Sent[^1]);
        // ...but taking over again is blocked.
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => uds.DisableDtcSettingAsync());
    }

    [Fact]
    public async Task Restore_retries_then_succeeds()
    {
        var ch = new Scripted(new[] { R(0xC5, 0x02) }, Array.Empty<DiagResponse>(), new[] { R(0x7F, 0x85, 0x22) }, new[] { R(0xC5, 0x01) });
        var lease = await Client(ch).DisableDtcSettingAsync();
        lease = Retry(lease);
        await lease.RestoreAsync();
        Assert.True(lease.Restored);
        Assert.Equal(4, ch.Sent.Count);
    }

    [Fact]
    public async Task Restore_failure_is_kept_and_reported_by_dispose_without_throwing()
    {
        var ch = new Scripted(new[] { R(0xC5, 0x02) }); // nothing answers the restores
        var lease = Retry(await Client(ch).DisableDtcSettingAsync());
        Exception? reported = null;
        lease.RestoreFailed += (_, ex) => reported = ex;
        await lease.DisposeAsync();
        Assert.False(lease.Restored);
        Assert.NotNull(lease.RestoreError);
        Assert.NotNull(reported);
        Assert.Contains("leaves the diagnostic session", reported!.Message);
        Assert.Equal(1 + 3, ch.Sent.Count);
    }

    [Fact]
    public async Task Explicit_restore_failure_throws()
    {
        var ch = new Scripted(new[] { R(0xC5, 0x02) });
        var lease = Retry(await Client(ch).DisableDtcSettingAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => lease.RestoreAsync());
        Assert.NotNull(lease.RestoreError);
    }

    [Fact]
    public async Task Wrong_85_echo_is_rejected()
    {
        var ch = new Scripted(new[] { R(0xC5, 0x01) });
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(ch).DisableDtcSettingAsync());
    }

    // ---- 28 --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Disable_communication_and_restore()
    {
        var ch = new Scripted(new[] { R(0x68, 0x03) }, new[] { R(0x68, 0x00) });
        var lease = await Client(ch).DisableCommunicationAsync(CommunicationControlType.DisableRxAndTx, CommunicationGroups.Both);
        await lease.RestoreAsync();
        Assert.Equal(new byte[] { 0x28, 0x03, 0x03 }, ch.Sent[0]);
        Assert.Equal(new byte[] { 0x28, 0x00, 0x03 }, ch.Sent[1]);
    }

    [Fact]
    public async Task Communication_arguments_are_checked()
    {
        var uds = Client(new Scripted());
        await Assert.ThrowsAsync<ArgumentException>(() => uds.DisableCommunicationAsync(CommunicationControlType.EnableRxAndTx));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.SetCommunicationAsync((CommunicationControlType)0x04));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.SetCommunicationAsync(CommunicationControlType.EnableRxDisableTx, (CommunicationGroups)0x10));
    }

    [Fact]
    public async Task Wrong_28_echo_is_rejected()
    {
        var ch = new Scripted(new[] { R(0x68, 0x01) });
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(ch).DisableCommunicationAsync(CommunicationControlType.DisableRxAndTx));
    }

    // ---- 2F --------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Adjust_output_with_mask_then_return_control()
    {
        var ch = new Scripted(new[] { R(0x6F, 0xD1, 0x00, 0x03, 0x01) }, new[] { R(0x6F, 0xD1, 0x00, 0x00, 0x00) });
        var (res, lease) = await Client(ch).AdjustOutputAsync(0xD100, new byte[] { 0x01 }, new byte[] { 0x80 });
        Assert.Equal(new byte[] { 0x01 }, res.ControlStatus);
        await lease.RestoreAsync();
        Assert.Equal(new byte[] { 0x2F, 0xD1, 0x00, 0x03, 0x01, 0x80 }, ch.Sent[0]);
        Assert.Equal(new byte[] { 0x2F, 0xD1, 0x00, 0x00, 0x80 }, ch.Sent[1]);
    }

    [Fact]
    public async Task Freeze_output()
    {
        var ch = new Scripted(new[] { R(0x6F, 0xD1, 0x00, 0x02, 0x00) });
        var (res, _) = await Client(ch).FreezeOutputAsync(0xD100);
        Assert.Equal(IoControlParameter.FreezeCurrentState, res.Parameter);
        Assert.Equal(new byte[] { 0x2F, 0xD1, 0x00, 0x02 }, ch.Sent[0]);
    }

    [Fact]
    public async Task Io_control_echo_and_arguments_are_checked()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(new Scripted(new[] { R(0x6F, 0xD1, 0x01, 0x03, 0x01) })).AdjustOutputAsync(0xD100, new byte[] { 1 }));
        await Assert.ThrowsAsync<InvalidDataException>(() => Client(new Scripted(new[] { R(0x6F, 0xD1, 0x00, 0x02, 0x01) })).AdjustOutputAsync(0xD100, new byte[] { 1 }));
        var uds = Client(new Scripted());
        await Assert.ThrowsAsync<ArgumentException>(() => uds.AdjustOutputAsync(0xD100, Array.Empty<byte>()));
        await Assert.ThrowsAsync<ArgumentException>(() => uds.IoControlAsync(0xD100, IoControlParameter.FreezeCurrentState, new byte[] { 1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => uds.IoControlAsync(0xD100, (IoControlParameter)0x07, Array.Empty<byte>()));
    }

    [Fact]
    public async Task Return_control_works_in_read_only_mode()
    {
        var ch = new Scripted(new[] { R(0x6F, 0xD1, 0x00, 0x00) });
        await Client(ch, readOnly: true).ReturnControlToEcuAsync(0xD100);
        Assert.Single(ch.Sent);
    }

    [Fact]
    public async Task Elm_does_not_resend_take_over_requests_after_pending()
    {
        var ch = new Scripted(new[] { R(0x7F, 0x2F, 0x78) }, new[] { R(0x6F, 0xD1, 0x00, 0x03, 0x01) });
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => Client(ch).AdjustOutputAsync(0xD100, new byte[] { 1 }));
        Assert.Equal(0x78, ex.Nrc);
        Assert.Single(ch.Sent);
    }

    // ---- Simulator end to end --------------------------------------------------------------------------------------

    private static async Task<(SimulatedAdapter, UdsClient)> Sim()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, new UdsClient(new ElmDiagChannel(s), 0x7E0) { Gate = WriteGate.AlwaysAllow, PendingRetryDelay = TimeSpan.Zero });
    }

    [Fact]
    public async Task Sim_controls_need_the_extended_session()
    {
        var (_, uds) = await Sim();
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.DisableDtcSettingAsync());
        Assert.Equal(0x7F, ex.Nrc);
    }

    [Fact]
    public async Task Sim_leases_restore_every_control()
    {
        var (sim, uds) = await Sim();
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        await using (await uds.DisableDtcSettingAsync())
        await using (await uds.DisableCommunicationAsync(CommunicationControlType.EnableRxDisableTx))
        {
            var (res, io) = await uds.AdjustOutputAsync(SimulatedAdapter.DemoOutputDid, new byte[] { 0x01 });
            await using (io)
            {
                Assert.Contains(0x7E0u, sim.DtcSettingOff);
                Assert.Contains(0x7E0u, sim.CommunicationDisabled);
                Assert.Equal(new byte[] { 0x01 }, sim.OverriddenOutputs[(0x7E0, SimulatedAdapter.DemoOutputDid)]);
                Assert.Equal(new byte[] { 0x01 }, res.ControlStatus);
            }
            Assert.Empty(sim.OverriddenOutputs);
        }
        Assert.Empty(sim.DtcSettingOff);
        Assert.Empty(sim.CommunicationDisabled);
    }

    [Fact]
    public async Task Sim_default_session_undoes_controls_as_a_backstop()
    {
        var (sim, uds) = await Sim();
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        await uds.DisableDtcSettingAsync();
        await uds.AdjustOutputAsync(SimulatedAdapter.DemoOutputDid, new byte[] { 0x01 });
        await uds.DiagnosticSessionControlAsync(UdsSession.Default);
        Assert.Empty(sim.DtcSettingOff);
        Assert.Empty(sim.OverriddenOutputs);
    }

    [Fact]
    public async Task Sim_unknown_output_is_out_of_range()
    {
        var (_, uds) = await Sim();
        await uds.DiagnosticSessionControlAsync(UdsSession.Extended);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => uds.AdjustOutputAsync(0xD101, new byte[] { 1 }));
        Assert.Equal(0x31, ex.Nrc);
    }

    /// <summary>Same lease with no retry delay, so the tests run fast.</summary>
    private static ControlLease Retry(ControlLease l) { l.RetryDelay = TimeSpan.Zero; return l; }
}
