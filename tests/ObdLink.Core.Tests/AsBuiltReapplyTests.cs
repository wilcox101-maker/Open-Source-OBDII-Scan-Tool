using ObdLink.Core.AsBuilt;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>KB-R5: after a dealer update / OTA reload reverts As-Built, the user's verified writes can be found and re-applied.</summary>
public class AsBuiltReapplyTests
{
    private const string Vin = "1FTEW1EP7HFA00000";
    private static readonly byte[] Old = { 1, 2, 3, 4, 5, 6, 7 }, Mine = { 1, 2, 3, 4, 5, 6, 9 };

    private static async Task<(ObdLink.Core.Transport.SimulatedAdapter sim, AsBuiltService svc)> Setup()
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.Dids[0x726] = new() { [0xDE00] = (byte[])Old.Clone(), [0xDE01] = (byte[])Old.Clone() };
        var dir = Path.Combine(Path.GetTempPath(), "abre_" + Guid.NewGuid().ToString("N"));
        return (sim, new AsBuiltService(m => new UdsClient(ch, m), new SafetyGuard(s, new AutoConfirm(true)), dir) { PendingSettle = TimeSpan.Zero });
    }

    private static AsBuiltBlock Block(byte b, byte[] d) => new() { Module = 0x726, Block = b, Data = (byte[])d.Clone() };

    private static Task<IReadOnlyList<ReapplyItem>> Plan(AsBuiltService svc, string vin = Vin) =>
        AsBuiltReapply.PlanAsync(svc.History, 0x726, vin, (m, b, ct) => svc.ReadBlockAsync(m, b, ct));

    [Fact]
    public async Task A_dealer_revert_is_found_and_re_applying_restores_my_value()
    {
        var (sim, svc) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(0x01, Mine));
        Assert.Equal(ReapplyState.Kept, Assert.Single(await Plan(svc)).State);

        sim.Dids[0x726][0xDE00] = (byte[])Old.Clone();                 // dealer update puts the old value back
        var item = Assert.Single(await Plan(svc));
        Assert.Equal(ReapplyState.Reverted, item.State);
        Assert.Contains("dealer update", item.Line);
        Assert.Equal(Mine, item.Wanted);

        var r = await svc.WriteBlockAsync(Vin, Block(item.Change.Block, item.Wanted));   // the normal write path
        Assert.True(r.Written);
        Assert.Equal(Mine, sim.Dids[0x726][0xDE00]);
        Assert.Equal(ReapplyState.Kept, Assert.Single(await Plan(svc)).State);
    }

    [Fact]
    public async Task Something_else_in_the_block_is_reported_as_different()
    {
        var (sim, svc) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(0x01, Mine));
        sim.Dids[0x726][0xDE00] = new byte[] { 7, 7, 7, 7, 7, 7, 7 };
        var item = Assert.Single(await Plan(svc));
        Assert.Equal(ReapplyState.Different, item.State);
        Assert.Contains("neither your value", item.Line);
    }

    [Fact]
    public async Task A_block_I_undid_is_never_offered()
    {
        var (_, svc) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(0x01, Mine));
        var last = svc.History.LastUndoable(0x726)!;
        await svc.RestoreAsync(svc.History.BackupPathOf(last), Vin);   // undo = restore: newest verified change is a restore
        Assert.Empty(await Plan(svc));
    }

    [Fact]
    public async Task Writes_for_another_vehicle_and_unverified_writes_are_not_offered()
    {
        var (sim, svc) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(0x01, Mine));
        Assert.Empty(await Plan(svc, "1FTEW1EP7HFB99999"));            // different VIN tail

        sim.WriteReply = ObdLink.Core.Transport.SimWriteReply.RejectNotStored;
        await Assert.ThrowsAnyAsync<Exception>(() => svc.WriteBlockAsync(Vin, Block(0x02, Mine)));
        Assert.DoesNotContain(await Plan(svc), i => i.Change.Block == 0x02); // rejected write: nothing to re-apply
    }

    [Fact]
    public void The_As_Built_page_re_applies_through_the_normal_write_path_and_never_falls_back_to_another_bus()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var vm = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.App", "AsBuiltPageViewModel.cs"));
        int i = vm.IndexOf("private async Task ReapplyAsync()", StringComparison.Ordinal);
        Assert.True(i > 0);
        var body = vm[i..vm.IndexOf("private async Task UndoAsync()", i, StringComparison.Ordinal)];
        Assert.Contains("AsBuiltReapply.PlanAsync(", body);
        Assert.Contains("res = await AsBuilt.WriteBlockAsync(_vin, block), allowFallback: false);", body);
        Assert.Contains("i.State != ReapplyState.Kept", body);
        Assert.Contains("{Binding AsBuiltPage.ReapplyAsBuiltCommand}", File.ReadAllText(Path.Combine(dir.FullName, "src", "ObdLink.App", "MainWindow.xaml")));
    }
}
