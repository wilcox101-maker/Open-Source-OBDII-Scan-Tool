using ObdLink.Core.AsBuilt;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>PCM / TCM As-Built writes need an extra typed confirmation (VID / transmission data); other modules don't.</summary>
public class AsBuiltWritePolicyTests
{
    private sealed class RecordingConfirm(Func<ConfirmationStep, bool> answer) : IConfirmationProvider
    {
        public List<ConfirmationStep> Asked { get; } = new();
        public Task<bool> ConfirmAsync(ConfirmationStep step, CancellationToken ct = default) { Asked.Add(step); return Task.FromResult(answer(step)); }
    }

    private static async Task<(ObdLink.Core.Transport.SimulatedAdapter sim, AsBuiltService svc, RecordingConfirm confirm)> Setup(ushort module, Func<ConfirmationStep, bool> answer)
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.PresentModules.Add(module);
        sim.Dids[module] = new() { [0xDE00] = new byte[] { 1, 2, 3, 4, 5, 6, 7 } };
        var confirm = new RecordingConfirm(answer);
        var dir = Path.Combine(Path.GetTempPath(), "abpol_" + Guid.NewGuid());
        return (sim, new AsBuiltService(m => new UdsClient(ch, m), new SafetyGuard(s, confirm), dir) { PendingSettle = TimeSpan.Zero }, confirm);
    }

    private static AsBuiltBlock Block(ushort m) => new() { Module = m, Block = 1, Data = new byte[] { 1, 2, 3, 4, 5, 6, 9 } };

    [Fact]
    public async Task A_PCM_write_asks_for_POWERTRAIN_and_stops_without_it()
    {
        var (sim, svc, confirm) = await Setup(0x7E0, st => st.TypedPhrase != AsBuiltWritePolicy.PowertrainPhrase);
        await Assert.ThrowsAsync<UserDeclinedException>(() => svc.WriteBlockAsync("1FTFW1E50HFA00001", Block(0x7E0)));
        Assert.Contains(confirm.Asked, st => st.TypedPhrase == "POWERTRAIN" && st.Message.Contains("tire size"));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, sim.Dids[0x7E0][0xDE00]);   // nothing written
    }

    [Fact]
    public async Task A_PCM_write_goes_ahead_when_confirmed()
    {
        var (sim, svc, confirm) = await Setup(0x7E0, _ => true);
        var r = await svc.WriteBlockAsync("1FTFW1E50HFA00001", Block(0x7E0));
        Assert.True(r.Written);
        Assert.Contains(confirm.Asked, st => st.TypedPhrase == "POWERTRAIN");
    }

    [Fact]
    public async Task A_BCM_write_has_no_powertrain_step()
    {
        var (_, svc, confirm) = await Setup(0x726, _ => true);
        await svc.WriteBlockAsync("1FTFW1E50HFA00001", Block(0x726));
        Assert.DoesNotContain(confirm.Asked, st => st.TypedPhrase == "POWERTRAIN");
    }

    [Fact]
    public async Task An_IPMB_write_asks_for_IPMB_and_stops_without_it()
    {
        // KB-R6: FORScan can't back up / restore the IPMB correctly (community guide); ours is untested too.
        var (sim, svc, confirm) = await Setup(AsBuiltWritePolicy.Ipmb, st => st.TypedPhrase != AsBuiltWritePolicy.IpmbPhrase);
        await Assert.ThrowsAsync<UserDeclinedException>(() => svc.WriteBlockAsync("1FTFW1E50HFA00001", Block(AsBuiltWritePolicy.Ipmb)));
        var step = Assert.Single(confirm.Asked, st => st.TypedPhrase == "IPMB");
        Assert.Contains("can't back up or restore", step.Message);
        Assert.Contains("https://", step.Message);
        Assert.DoesNotContain(confirm.Asked, st => st.TypedPhrase == "POWERTRAIN");
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, sim.Dids[AsBuiltWritePolicy.Ipmb][0xDE00]);   // nothing written

        var (_, ok, okConfirm) = await Setup(AsBuiltWritePolicy.Ipmb, _ => true);
        Assert.True((await ok.WriteBlockAsync("1FTFW1E50HFA00001", Block(AsBuiltWritePolicy.Ipmb))).Written);
        Assert.Contains(okConfirm.Asked, st => st.TypedPhrase == "IPMB");
    }

    [Fact]
    public void Only_an_IPMB_backup_carries_the_unreliable_backup_note_and_the_app_shows_it()
    {
        Assert.Contains("untested", AsBuiltWritePolicy.BackupNote(0x7B1));
        Assert.Null(AsBuiltWritePolicy.BackupNote(0x726));
        Assert.Empty(AsBuiltWritePolicy.ExtraSteps(0x726, "x"));
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var vm = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.App", "AsBuiltPageViewModel.cs"));
        Assert.Equal(2, vm.Split("AsBuiltWritePolicy.BackupNote(").Length - 1);   // single-module and all-modules backup
    }

    [Fact]
    public void After_write_advice_mentions_the_ignition_cycle_and_U2100()
    {
        var a = AsBuiltWritePolicy.AfterWriteAdvice(0x726);
        Assert.Contains("ignition OFF", a);
        Assert.Contains("U2100", a);
        Assert.DoesNotContain("PCM / TCM", a);
        Assert.Contains("PCM / TCM", AsBuiltWritePolicy.AfterWriteAdvice(0x7E1));
    }

    [Fact]
    public void Every_message_that_reports_new_data_in_a_module_ends_with_the_ignition_cycle_advice()
    {
        // A4: read-back proves storage, not effect (most modules apply As-Built after an ignition cycle). Write, restore, undo,
        // recover and "check unfinished write → new data" all tell the user to cycle the ignition and read again.
        var src = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Me())!, "..", "..", "src", "ObdLink.App", "AsBuiltPageViewModel.cs"));
        var lines = src.Split('\n').Where(l => l.Contains("verified by read-back (") || l.Contains("verified by read-back on") || l.Contains("the write did take effect")).ToList();
        Assert.Equal(5, lines.Count); // write, restore, undo, recover, check → new data
        Assert.All(lines, l => Assert.Contains("AsBuiltWritePolicy.AfterWriteAdvice(", l));
    }

    private static string Me([System.Runtime.CompilerServices.CallerFilePath] string me = "") => me;
}
