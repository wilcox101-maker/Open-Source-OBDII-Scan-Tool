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
    public void After_write_advice_mentions_the_ignition_cycle_and_U2100()
    {
        var a = AsBuiltWritePolicy.AfterWriteAdvice(0x726);
        Assert.Contains("ignition OFF", a);
        Assert.Contains("U2100", a);
        Assert.DoesNotContain("PCM / TCM", a);
        Assert.Contains("PCM / TCM", AsBuiltWritePolicy.AfterWriteAdvice(0x7E1));
    }
}
