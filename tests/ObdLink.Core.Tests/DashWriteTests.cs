using ObdLink.Core.Dashboard;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>D2/D3: a DID write card settles every uncertain reply by reading back, and says what to do next. SYNTHETIC module.</summary>
public class DashWriteTests
{
    /// <summary>One module holding DID values. <see cref="OnWrite"/> picks what happens to a 2E request.</summary>
    private sealed class Module : IDiagChannel
    {
        public readonly Dictionary<ushort, byte[]> Dids = new() { [0x4040] = new byte[] { 0x0A, 0x0B } };
        /// <summary>apply = store the value; reply = answer to send (null = no answer).</summary>
        public Func<byte[], (bool Apply, byte[]? Reply)> OnWrite = req => (true, new byte[] { 0x6E, req[1], req[2] });
        /// <summary>Value the module keeps when a write is applied (default: what was written).</summary>
        public Func<byte[], byte[]> Store = v => v;
        public int ReadFailures;            // the next N reads get no answer
        public byte? ReadNrc;               // every read answers this NRC
        public int Writes, Reads;

        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] r, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            ushort did = (ushort)(r[1] << 8 | r[2]);
            byte[]? reply;
            if (r[0] == 0x2E)
            {
                Writes++;
                var (apply, rep) = OnWrite(r);
                if (apply) Dids[did] = Store(r[3..]);
                reply = rep;
            }
            else
            {
                Reads++;
                if (ReadNrc is byte n) reply = new byte[] { 0x7F, 0x22, n };
                else if (ReadFailures > 0) { ReadFailures--; reply = null; }
                else reply = new byte[] { 0x62, r[1], r[2] }.Concat(Dids[did]).ToArray();
            }
            IReadOnlyList<DiagResponse> res = reply is null ? Array.Empty<DiagResponse>() : new[] { new DiagResponse(0x7E8, reply) };
            return Task.FromResult(res);
        }
    }

    private static readonly byte[] Old = { 0x0A, 0x0B }, New = { 0x01, 0x02 };
    private static Task NoWait(TimeSpan _) => Task.CompletedTask;
    private static UdsClient Client(Module m) => new(m, 0x7E0) { PendingRetryDelay = TimeSpan.Zero };
    private static Task<DashWriteOutcome> Write(Module m) => DashWrite.WriteAndSettleAsync(Client(m), 0x4040, New, Old, NoWait);

    [Fact]
    public async Task A_clean_write_is_verified()
    {
        var m = new Module();
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.Verified, o.Verdict);
        Assert.False(o.ReplyLost);
        Assert.Equal(1, m.Writes);
    }

    [Fact]
    public async Task A_lost_reply_to_a_write_that_landed_is_verified_by_reading_back_not_reported_as_failed()
    {
        var m = new Module { OnWrite = _ => (true, null) };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.Verified, o.Verdict);
        Assert.True(o.ReplyLost);
        Assert.Contains("settled by reading back", o.Message);
        Assert.Equal(1, m.Writes);   // never written twice
    }

    [Fact]
    public async Task A_pending_reply_on_an_ELM_adapter_is_settled_the_same_way()
    {
        var m = new Module { OnWrite = req => (true, new byte[] { 0x7F, 0x2E, 0x78 }) };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.Verified, o.Verdict);
        Assert.True(o.ReplyLost);
    }

    [Fact]
    public async Task A_write_that_never_arrived_is_reported_as_not_landed_and_safe_to_retry()
    {
        var m = new Module { OnWrite = _ => (false, null) };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.NotApplied, o.Verdict);
        Assert.Contains("did not land", o.Message);
        Assert.Contains("safe to try again", o.Message);
    }

    [Fact]
    public async Task No_reply_and_no_read_back_is_unknown_with_a_read_first_instruction()
    {
        var m = new Module { OnWrite = _ => (true, null), ReadFailures = 99 };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.Unknown, o.Verdict);
        Assert.Contains("may or may not have landed", o.Message);
        Assert.Equal(DashWrite.ReadAttempts, m.Reads);
    }

    [Fact]
    public async Task A_slow_read_back_after_a_lost_reply_is_retried()
    {
        var m = new Module { OnWrite = _ => (true, null), ReadFailures = DashWrite.ReadAttempts - 1 };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.Verified, o.Verdict);
    }

    [Theory]
    [InlineData(0x33, "security access")]
    [InlineData(0x22, "Conditions not correct")]
    [InlineData(0x31, "not writable")]
    [InlineData(0x13, "length")]
    public async Task A_refused_write_says_nothing_changed_and_gives_the_next_step(byte nrc, string hint)
    {
        var m = new Module { OnWrite = _ => (false, new byte[] { 0x7F, 0x2E, nrc }) };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.NotApplied, o.Verdict);
        Assert.Equal(nrc, o.Nrc);
        Assert.Contains("nothing was changed", o.Message);
        Assert.Contains(hint, o.Message);
        Assert.Equal(0, m.Reads);   // a negative response is definite: no read-back needed
    }

    [Fact]
    public async Task An_accepted_write_that_keeps_the_old_value_suggests_an_ignition_cycle()
    {
        var m = new Module { Store = _ => Old.ToArray() };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.NotApplied, o.Verdict);
        Assert.Contains("ignition OFF", o.Message);
    }

    [Fact]
    public async Task A_different_read_back_offers_an_ignition_cycle_and_undo()
    {
        var m = new Module { Store = _ => new byte[] { 0x01, 0x00 } };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.Different, o.Verdict);
        Assert.Contains("ignition OFF", o.Message);
        Assert.Contains("Undo", o.Message);
        Assert.Equal(new byte[] { 0x01, 0x00 }, o.ReadBack);
    }

    [Fact]
    public async Task An_accepted_write_whose_DID_cannot_be_read_says_so_without_retrying_reads()
    {
        var m = new Module { ReadNrc = 0x31 };
        var o = await Write(m);
        Assert.Equal(DashWriteVerdict.Different, o.Verdict);
        Assert.Contains("could not be read back", o.Message);
        Assert.Equal(1, m.Reads);   // a definite NRC on the read: no pointless retries
    }

    [Fact]
    public async Task A_lost_link_is_thrown_not_settled()
    {
        var m = new Module { OnWrite = _ => throw new IOException("port closed") };
        await Assert.ThrowsAsync<IOException>(() => Write(m));
    }
}
