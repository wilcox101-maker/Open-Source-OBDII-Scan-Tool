using ObdLink.Core.AsBuilt;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// X3 (write recovery plan): every As-Built write path x every fault the simulator can produce, asserting the same four things
/// each time: what the caller is told (exception or not), what the journal says, what the module holds, and that the module
/// ends in the default session (X2). Unfinished rows are then recovered and must give back the old bytes.
/// </summary>
public class WriteFaultMatrixTests
{
    private const string Vin = "1FTEW1EP7HFA00000";
    private static readonly byte[] Old = { 1, 2, 3, 4, 5, 6, 7 }, New = { 1, 2, 3, 4, 5, 6, 9 };

    public enum Fault { Positive, PendingThenPositive, PendingOnly, NoReply, RejectNotStored, LinkDropAfterStore, ReadBackMismatch }

    private static void Apply(SimulatedAdapter sim, Fault f)
    {
        switch (f)
        {
            case Fault.LinkDropAfterStore: sim.SilentAfterWrite = true; break;
            case Fault.ReadBackMismatch: sim.CorruptWrites = true; break;
            default: sim.WriteReply = Enum.Parse<SimWriteReply>(f.ToString()); break;
        }
    }

    private static void Heal(SimulatedAdapter sim) { sim.Reconnect(); sim.CorruptWrites = false; sim.WriteReply = SimWriteReply.Positive; }

    private static async Task<(SimulatedAdapter sim, AsBuiltService svc)> Setup()
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.Dids[0x726] = new() { [0xDE00] = (byte[])Old.Clone() };
        var dir = Path.Combine(Path.GetTempPath(), "x3_" + Guid.NewGuid().ToString("N"));
        return (sim, new AsBuiltService(m => new UdsClient(ch, m), new SafetyGuard(s, new AutoConfirm(true)), dir) { PendingSettle = TimeSpan.Zero });
    }

    private static AsBuiltBlock Block(byte[] d) => new() { Module = 0x726, Block = 1, Data = (byte[])d.Clone() };

    /// <summary>What each fault must lead to for a block whose old data is <c>from</c> and wanted data is <c>to</c>.</summary>
    private static (bool Throws, string Outcome, bool HoldsWanted, bool Open) Expected(Fault f) => f switch
    {
        // The module answered or stored it: an uncertain reply is settled by the read-back, never reported as a failure.
        Fault.Positive or Fault.PendingThenPositive or Fault.PendingOnly or Fault.NoReply => (false, AsBuiltOutcome.Verified, true, false),
        Fault.RejectNotStored => (true, AsBuiltOutcome.Rejected, false, false),
        Fault.LinkDropAfterStore => (true, AsBuiltOutcome.Unknown, true, true),
        Fault.ReadBackMismatch => (true, AsBuiltOutcome.Mismatch, false, true),
        _ => throw new ArgumentOutOfRangeException(nameof(f)),
    };

    public static TheoryData<Fault> All() { var d = new TheoryData<Fault>(); foreach (var f in Enum.GetValues<Fault>()) d.Add(f); return d; }

    private static void EndsInDefaultSession(SimulatedAdapter sim, Fault f)
    {
        var bcm = sim.Sent.Where(x => x.Header == 0x726).Select(x => x.Request).ToList();
        int write = bcm.FindLastIndex(r => r.Contains("2EDE00"));
        Assert.True(write >= 0, string.Join(" ", bcm));
        // 10 01 goes out after the last write on every row; on the link-drop row it is sent into the silence (best-effort).
        Assert.Contains("1001", bcm.Skip(write + 1));
        Assert.Equal("1001", bcm[^1]);
    }

    [Theory]
    [MemberData(nameof(All))]
    public async Task Write(Fault f)
    {
        var (sim, svc) = await Setup();
        Apply(sim, f);
        var (throws, outcome, holdsNew, open) = Expected(f);

        Exception? ex = null;
        try { await svc.WriteBlockAsync(Vin, Block(New)); } catch (Exception e) { ex = e; }

        Assert.Equal(throws, ex is not null);
        var c = Assert.Single(svc.History.Load());
        Assert.Equal(outcome, c.Outcome);
        Assert.Equal(Convert.ToHexString(Old), c.Before);
        Assert.True(File.Exists(svc.History.BackupPathOf(c)), "the backup taken before the write is on disk");
        if (f is Fault.PendingOnly or Fault.NoReply) Assert.Contains("read-back below decided", c.Note);
        if (f == Fault.LinkDropAfterStore) Assert.Contains("UNKNOWN", ex!.Message);
        if (f == Fault.ReadBackMismatch) Assert.Contains("mismatch", ex!.Message, StringComparison.OrdinalIgnoreCase);
        if (f != Fault.ReadBackMismatch) Assert.Equal(holdsNew ? New : Old, sim.Dids[0x726][0xDE00]);
        Assert.Equal(open, svc.History.Unresolved().Count == 1);
        EndsInDefaultSession(sim, f);

        if (!open) return;
        // Recovery: plug back in / fault gone, restore the backup taken before the write: the old bytes come back, nothing is left open.
        Heal(sim);
        var r = await svc.RecoverAttemptAsync(svc.History.Unresolved()[0], Vin);
        Assert.True(r.Written);
        Assert.Equal(Old, sim.Dids[0x726][0xDE00]);
        Assert.Empty(svc.History.Unresolved());
    }

    [Theory]
    [MemberData(nameof(All))]
    public async Task Restore(Fault f)
    {
        var (sim, svc) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(New));                // a good write first: its backup holds Old
        var backup = svc.History.BackupPathOf(svc.History.Load()[^1]);
        Apply(sim, f);
        var (throws, outcome, holdsOld, open) = Expected(f);

        Exception? ex = null;
        try { await svc.RestoreAsync(backup, Vin); } catch (Exception e) { ex = e; }

        Assert.Equal(throws, ex is not null);
        var c = svc.History.Load()[^1];
        Assert.Equal("restore", c.Kind);
        Assert.Equal(outcome, c.Outcome);
        Assert.Equal(Convert.ToHexString(New), c.Before);
        if (f != Fault.ReadBackMismatch) Assert.Equal(holdsOld ? Old : New, sim.Dids[0x726][0xDE00]);
        Assert.Equal(open, svc.History.Unresolved().Count == 1);
        EndsInDefaultSession(sim, f);

        if (!open) return;
        // Retry the restore once the fault is gone: Old in the module.
        Heal(sim);
        await svc.RestoreAsync(backup, Vin);
        Assert.Equal(Old, sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public void Every_fault_has_an_expectation() => Assert.All(Enum.GetValues<Fault>(), f => Expected(f));
}
