using System.Text;
using ObdLink.Core.Flash;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Resume / recovery paths of the flash engine and preflight. SYNTHETIC module only.</summary>
public class FlashRecoveryTests
{
    private sealed class SimKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(seed.Reverse().ToArray());
    }

    private static readonly FlashImage Img = FlashImage.FromSegments("r.vbf",
        new[] { new FlashSegment(0x10000, Enumerable.Range(0, 1500).Select(i => (byte)(i * 5)).ToArray()) }, partNumber: "REC-NEW-PART", ecuTxId: 0x7E0);

    private static (FlashEngine Engine, UdsClient Uds, MemoryFlashJournal Journal, SimulatedFlashEcu Ecu, List<string> Backups) Make(MemoryFlashJournal? journal = null, SimulatedFlashEcu? ecu = null)
    {
        ecu ??= new SimulatedFlashEcu { ErasePendingReplies = 0 };
        ecu.ExpectedImage = Img;
        var uds = new UdsClient(ecu, 0x7E0) { SeedKey = new SimKey(), PendingRetryDelay = TimeSpan.Zero };
        journal ??= new MemoryFlashJournal();
        var backups = new List<string>();
        var engine = new FlashEngine(new FlashPlan { TxId = 0x7E0, Image = Img }, journal)
        {
            ReadVoltage = _ => Task.FromResult<double?>(13.4),
            Backup = _ => { backups.Add("bk.json"); return Task.FromResult("bk.json"); },
            ReadModulePart = async (u, ct) => Encoding.ASCII.GetString(await u.ReadDataByIdentifierAsync(0xF188, ct)),
            Delay = _ => Task.CompletedTask,
        };
        return (engine, uds, journal, ecu, backups);
    }

    [Fact]
    public async Task Link_lost_after_the_reset_then_resume_sends_nothing_again()
    {
        var (eng, uds, journal, ecu, _) = Make();
        bool down = false;
        ecu.Faults = (req, _) => req[0] == 0x11 && !down && (down = true) ? SimFlashFault.LinkDownAfter : null;   // the module resets, then the adapter is pulled
        await Assert.ThrowsAnyAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.True(eng.State.Open);
        Assert.True(eng.State.ReachedReset);

        ecu.LinkDown = false; ecu.Faults = null;
        int callsBefore = ecu.Calls.Count;
        var (eng2, _, _, _, _) = Make(journal, ecu);
        var r = await eng2.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.False(eng2.State.Open);
        var after = ecu.Calls.Skip(callsBefore).ToList();
        Assert.DoesNotContain(after, c => c[0] is 0x10 or 0x31 or 0x34 or 0x36);    // no session change, erase or download
        Assert.Contains("Nothing was sent again", r.Message);
    }

    [Fact]
    public async Task Resume_of_a_job_that_stopped_before_its_backup_takes_the_backup_first()
    {
        var journal = new MemoryFlashJournal();
        journal.Append(new FlashJournalEntry(DateTimeOffset.UtcNow, FlashJournalKind.Started, null, null, null, null, null, "r.vbf", Img.Sha256, 0x7E0));   // crash right after Started
        var (eng, uds, _, ecu, backups) = Make(journal);
        Assert.True(eng.State.Open);
        Assert.Null(eng.State.BackupRef);
        var r = await eng.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.Equal(new[] { "bk.json" }, backups);
        var entries = journal.ReadAll();
        Assert.True(entries.ToList().FindIndex(e => e.Kind == FlashJournalKind.Backup) < entries.ToList().FindIndex(e => e.Phase == nameof(FlashPhase.ProgrammingSession)));
    }

    [Fact]
    public async Task Resume_preflight_reaches_a_bootloader_that_ignores_tester_present()
    {
        var ecu = new SimulatedFlashEcu { IgnoreTesterPresentInBootloader = true, ExpectedImage = Img };
        await ecu.RequestAsync(new byte[] { 0x10, 0x03 });
        await ecu.RequestAsync(new byte[] { 0x10, 0x02 });
        await ecu.RequestAsync(new byte[] { 0x10, 0x01 });   // bootloader, default session: silent to 3E
        Assert.True(ecu.InBootloader);
        var uds = new UdsClient(ecu, 0x7E0) { PendingRetryDelay = TimeSpan.Zero };
        var plan = new FlashPlan { TxId = 0x7E0, Image = Img };

        var fresh = await FlashPreflight.RunAsync(uds, plan, _ => Task.FromResult<double?>(13.4), null);
        Assert.False(fresh.CanFlash);                                         // a new flash still needs a healthy link

        var rec = await FlashPreflight.RunAsync(uds, plan, _ => Task.FromResult<double?>(13.4), null, resume: true);
        Assert.True(rec.CanFlash);
        Assert.Contains(rec.Items, i => i.Name == "Link" && i.Status == PreflightStatus.Warn && i.Detail.Contains("bootloader"));
    }

    [Fact]
    public async Task Resume_preflight_fails_with_guidance_when_the_module_is_silent()
    {
        var ecu = new SimulatedFlashEcu { LinkDown = false, ExpectedImage = Img };
        ecu.Faults = (_, _) => SimFlashFault.DropRequest;                       // nothing answers
        var uds = new UdsClient(ecu, 0x7E0) { PendingRetryDelay = TimeSpan.Zero };
        var rep = await FlashPreflight.RunAsync(uds, new FlashPlan { TxId = 0x7E0, Image = Img }, _ => Task.FromResult<double?>(13.4), null, resume: true);
        Assert.False(rep.CanFlash);
        Assert.Contains(rep.Items, i => i.Name == "Link" && i.Status == PreflightStatus.Fail && i.Detail.Contains("ignition OFF"));
    }
}
