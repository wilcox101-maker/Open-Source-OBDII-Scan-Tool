using ObdLink.Core.AsBuilt;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// KB-R4: a write or restore that changes shared vehicle configuration (ECC) in the BdyCM needs a typed RELEARN, because this app
/// can't run Ford's relearn and other modules would set U2100/U2101 (FORScan p=59999). Other lines and modules are unaffected.
/// </summary>
public class AsBuiltEccTests
{
    private const string Vin = "1FTEW1EP7HFA00000";
    private static readonly byte[] Old = { 1, 2, 3, 4, 5, 6, 7 };   // block 12: line 01 = bytes 0-4 (axle/tire), line 02 = bytes 5-6

    private sealed class Confirm(Func<ConfirmationStep, bool> answer) : IConfirmationProvider
    {
        public List<ConfirmationStep> Asked { get; } = new();
        public Task<bool> ConfirmAsync(ConfirmationStep step, CancellationToken ct = default) { Asked.Add(step); return Task.FromResult(answer(step)); }
    }

    private static async Task<(ObdLink.Core.Transport.SimulatedAdapter sim, AsBuiltService svc, Confirm confirm, Func<Func<ConfirmationStep, bool>, AsBuiltService> another)> Setup(Func<ConfirmationStep, bool> answer)
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.Dids[0x726] = new() { [AsBuiltBlock.DidForBlock(0x12)] = (byte[])Old.Clone(), [AsBuiltBlock.DidForBlock(0x01)] = (byte[])Old.Clone() };
        var confirm = new Confirm(answer);
        var dir = Path.Combine(Path.GetTempPath(), "abecc_" + Guid.NewGuid().ToString("N"));
        AsBuiltService Make(IConfirmationProvider c) => new(m => new UdsClient(ch, m), new SafetyGuard(s, c), dir) { PendingSettle = TimeSpan.Zero };
        return (sim, Make(confirm), confirm, a => Make(new Confirm(a)));
    }

    private static AsBuiltBlock Block(byte block, params byte[] d) => new() { Module = 0x726, Block = block, Data = d };

    [Fact]
    public async Task Changing_the_tire_or_axle_line_needs_RELEARN_and_nothing_is_written_without_it()
    {
        var (sim, svc, confirm, _) = await Setup(st => st.TypedPhrase != AsBuiltEccPolicy.Phrase);
        await Assert.ThrowsAsync<UserDeclinedException>(() => svc.WriteBlockAsync(Vin, Block(0x12, 9, 2, 3, 4, 5, 6, 7)));
        var step = Assert.Single(confirm.Asked, st => st.TypedPhrase == "RELEARN");
        Assert.Contains("726-12-01 (axle ratio / tire circumference)", step.Message);
        Assert.Contains("U2100", step.Message);
        Assert.Equal(Old, sim.Dids[0x726][AsBuiltBlock.DidForBlock(0x12)]);
    }

    [Fact]
    public async Task With_RELEARN_typed_the_write_goes_ahead()
    {
        var (sim, svc, confirm, _) = await Setup(_ => true);
        var r = await svc.WriteBlockAsync(Vin, Block(0x12, 9, 2, 3, 4, 5, 6, 7));
        Assert.True(r.Written);
        Assert.Contains(confirm.Asked, st => st.TypedPhrase == "RELEARN");
    }

    [Theory]
    [InlineData(0x12, new byte[] { 1, 2, 3, 4, 5, 6, 9 })]   // block 12 but only line 02 changes: not ECC
    [InlineData(0x01, new byte[] { 9, 2, 3, 4, 5, 6, 7 })]   // another block
    public async Task Other_lines_need_no_RELEARN(byte block, byte[] data)
    {
        var (_, svc, confirm, _) = await Setup(_ => true);
        Assert.True((await svc.WriteBlockAsync(Vin, Block(block, data))).Written);
        Assert.DoesNotContain(confirm.Asked, st => st.TypedPhrase == "RELEARN");
    }

    [Fact]
    public async Task A_restore_that_changes_ECC_also_needs_RELEARN()
    {
        var (sim, svc, _, another) = await Setup(_ => true);
        await svc.WriteBlockAsync(Vin, Block(0x12, 9, 2, 3, 4, 5, 6, 7));                 // backup holds Old
        var backup = svc.History.BackupPathOf(svc.History.Load()[^1]);
        var svc2 = another(st => st.TypedPhrase != "RELEARN");
        await Assert.ThrowsAsync<UserDeclinedException>(() => svc2.RestoreAsync(backup, Vin));
        Assert.Equal(new byte[] { 9, 2, 3, 4, 5, 6, 7 }, sim.Dids[0x726][AsBuiltBlock.DidForBlock(0x12)]); // not restored without RELEARN
    }

    [Fact]
    public void Only_the_BdyCM_has_ECC_lines()
    {
        var a = new AsBuiltBlock { Module = 0x7E0, Block = 0x12, Data = Old };
        var b = new AsBuiltBlock { Module = 0x7E0, Block = 0x12, Data = new byte[] { 9, 2, 3, 4, 5, 6, 7 } };
        Assert.Empty(AsBuiltEccPolicy.Changed(a, b));
        Assert.StartsWith("https://", AsBuiltEccPolicy.Source);
    }
}
