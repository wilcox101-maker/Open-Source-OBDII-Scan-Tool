using System.Text;
using ObdLink.Core.Flash;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// Flash engine vs the session timeout (S3 = 5 s) on a virtual clock: every wait inside the programming session must keep the
/// session alive, and a session that is lost anyway is re-entered from the erase once, then left open for resume.
/// SYNTHETIC module only.
/// </summary>
public class FlashSessionTests
{
    private sealed class SimKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(seed.Reverse().ToArray());
    }

    private sealed class Clock { public DateTime Now = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc); }

    private static FlashImage Image() => FlashImage.FromSegments("s3.vbf",
        new[] { new FlashSegment(0x10000, Enumerable.Range(0, 3000).Select(i => (byte)(i * 3)).ToArray()) }, partNumber: "S3-NEW-PART", ecuTxId: 0x7E0);

    /// <summary>Engine whose delays advance a virtual clock that the simulated module's S3 timer reads.</summary>
    private static (FlashEngine Engine, UdsClient Uds, MemoryFlashJournal Journal, SimulatedFlashEcu Ecu, Clock Clock) Make(Func<int, double>? voltsByRead = null)
    {
        var clock = new Clock();
        var img = Image();
        var ecu = new SimulatedFlashEcu { ExpectedImage = img, S3 = TimeSpan.FromSeconds(5), Now = () => clock.Now, ErasePendingReplies = 0 };
        var uds = new UdsClient(ecu, 0x7E0) { SeedKey = new SimKey(), PendingRetryDelay = TimeSpan.Zero };
        var journal = new MemoryFlashJournal();
        int reads = 0;
        var engine = new FlashEngine(new FlashPlan { TxId = 0x7E0, Image = img }, journal)
        {
            ReadVoltage = _ => Task.FromResult<double?>(voltsByRead?.Invoke(reads++) ?? 13.4),
            Backup = _ => Task.FromResult("backup.json"),
            ReadModulePart = async (u, ct) => Encoding.ASCII.GetString(await u.ReadDataByIdentifierAsync(0xF188, ct)),
            Delay = t => { clock.Now += t; return Task.CompletedTask; },
            VoltageCheckEveryBlocks = 4,
        };
        return (engine, uds, journal, ecu, clock);
    }

    private static int KeepAlives(SimulatedFlashEcu ecu) => ecu.Calls.Count(c => c.Length == 2 && c[0] == 0x3E && c[1] == 0x80);

    [Fact]
    public async Task A_30_second_battery_dip_mid_download_does_not_end_the_session()
    {
        // Voltage reads: fine at the start, then 15 low reads (2 s apart = 30 s) at the first in-flight check, then fine.
        var (eng, uds, _, ecu, _) = Make(i => i is >= 1 and <= 15 ? 11.2 : 13.4);
        var r = await eng.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.Equal(0, ecu.SessionTimeouts);
        Assert.True(KeepAlives(ecu) >= 14, $"keep-alives sent: {KeepAlives(ecu)}");
    }

    [Fact]
    public async Task The_security_delay_wait_keeps_the_session_alive()
    {
        var (eng, uds, _, ecu, _) = Make();
        bool once = false;
        ecu.Faults = (req, _) => req[0] == 0x27 && req[1] == 0x01 && !once && (once = true) ? SimFlashFault.Negative(0x37) : null;   // 10 s delay
        var r = await eng.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.Equal(0, ecu.SessionTimeouts);
        Assert.True(KeepAlives(ecu) >= 5);
    }

    [Fact]
    public async Task A_session_lost_mid_download_is_re_entered_from_the_erase_and_finishes()
    {
        var (eng, uds, journal, ecu, _) = Make();
        int blocks = 0;
        ecu.Faults = (req, _) => { if (req[0] == 0x36 && ++blocks == 5) ecu.DropSession(); return null; };   // the module drops the session before block 5
        var r = await eng.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.Equal(2, ecu.Calls.Count(c => c.Length >= 4 && c[0] == 0x31 && c[1] == 0x01 && c[2] == 0xFF && c[3] == 0x00));   // erased twice
        Assert.Equal(2, ecu.Calls.Count(c => c.Length >= 2 && c[0] == 0x10 && c[1] == 0x02));
        Assert.Contains(journal.ReadAll(), e => e.Detail?.Contains("re-entering programming from the erase") == true);
        foreach (var s in Image().Segments) Assert.Equal(s.Data, ecu.Memory(s.Address));
    }

    [Fact]
    public async Task A_session_lost_twice_leaves_the_job_open_for_resume()
    {
        var (eng, uds, _, ecu, _) = Make();
        int blocks = 0;
        ecu.Faults = (req, _) => { if (req[0] == 0x36 && ++blocks % 5 == 0) ecu.DropSession(); return null; };   // keeps happening
        var ex = await Assert.ThrowsAsync<FlashSessionLostException>(() => eng.RunAsync(uds));
        Assert.True(ex.ModuleMayBeInBootloader);
        Assert.True(eng.State.Open);                                  // resumable, not abandoned or failed
        Assert.Contains("re-enters programming from the erase", ex.Message);
    }

    [Fact]
    public async Task Without_keep_alive_the_same_dip_would_have_ended_the_session()
    {
        // Control: the module's S3 really drops a silent session (so the first test proves the keep-alive, not a lenient simulator).
        var clock = new Clock();
        var ecu = new SimulatedFlashEcu { S3 = TimeSpan.FromSeconds(5), Now = () => clock.Now };
        await ecu.RequestAsync(new byte[] { 0x10, 0x03 });
        await ecu.RequestAsync(new byte[] { 0x10, 0x02 });
        clock.Now += TimeSpan.FromSeconds(6);
        var r = await ecu.RequestAsync(new byte[] { 0x34, 0x00, 0x44, 0, 1, 0, 0, 0, 0, 0x0B, 0xB8 });
        Assert.Equal(new byte[] { 0x7F, 0x34, 0x7F }, r[0].Payload);
        Assert.Equal(1, ecu.SessionTimeouts);
    }
}
