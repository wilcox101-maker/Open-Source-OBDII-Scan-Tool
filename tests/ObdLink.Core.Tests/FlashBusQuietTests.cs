using System.Text;
using ObdLink.Core.Flash;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// Bus quieting around a flash (B3): functional 10 83 / 85 82 / 28 83 01 before programming, functional 3E 80 while quiet,
/// 28 80 01 / 85 81 (and 10 81 after success only) afterwards. Two SYNTHETIC bystander modules keep their own S3 timers on a
/// virtual clock; every request costs bus time, so a long download outlasts S3 unless the engine keeps them alive.
/// </summary>
public class FlashBusQuietTests
{
    private sealed class SimKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(seed.Reverse().ToArray());
    }

    private sealed class Clock { public DateTime Now = new(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc); }

    /// <summary>A module on the same bus that is not being flashed. Only functional requests reach it.</summary>
    private sealed class Bystander
    {
        public byte Session = 0x01;
        public bool CommOff, DtcOff;
        public int Timeouts;
        private DateTime? _last;

        public void Tick(DateTime now)
        {
            if (Session != 0x01 && _last is DateTime l && now - l > TimeSpan.FromSeconds(5)) { Session = 0x01; CommOff = false; DtcOff = false; Timeouts++; }
        }

        public void Receive(byte[] r, DateTime now)
        {
            Tick(now);
            _last = now;
            switch (r[0])
            {
                case 0x10: Session = (byte)(r[1] & 0x7F); if (Session == 0x01) { CommOff = false; DtcOff = false; } break;
                case 0x85 when Session != 0x01: DtcOff = (r[1] & 0x7F) == 0x02; break;
                case 0x28 when Session != 0x01: CommOff = (r[1] & 0x7F) != 0x00; break;
            }
        }
    }

    /// <summary>One CAN bus: the module being flashed plus bystanders. Records the target of every request and charges bus time.</summary>
    private sealed class Bus(SimulatedFlashEcu ecu, Clock clock, params Bystander[] others) : IDiagChannel
    {
        private uint _target;
        public List<(uint Target, byte[] Req)> Log { get; } = new();
        public TimeSpan StepCost { get; set; } = TimeSpan.FromMilliseconds(400);

        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) { _target = txId; return ecu.SetTargetAsync(txId, rxId, ct); }

        public async Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] request, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            clock.Now += StepCost;
            Log.Add((_target, request.ToArray()));
            if (_target == 0x7DF) foreach (var o in others) o.Receive(request, clock.Now);
            return await ecu.RequestAsync(request, timeout, ct); // the flashed module hears functional requests too
        }

        public IEnumerable<byte[]> Functional => Log.Where(l => l.Target == 0x7DF).Select(l => l.Req);
    }

    private static FlashImage Image() => FlashImage.FromSegments("quiet.vbf",
        new[] { new FlashSegment(0x10000, Enumerable.Range(0, 8000).Select(i => (byte)(i * 7)).ToArray()) }, partNumber: "QUIET-NEW", ecuTxId: 0x7E0);

    private static FlashImage Sbl() => VbfReader.Parse(VbfReader.Write(
        new[] { new FlashSegment(0x00E00000, Enumerable.Range(0, 0x3D0).Select(i => (byte)(i * 5)).ToArray()) }, "QUIET-SBL", 0x7E0, partType: "SBL", call: 0x00E00000), "sbl.vbf");

    private static (FlashEngine Engine, UdsClient Uds, MemoryFlashJournal Journal, SimulatedFlashEcu Ecu, Bus Bus, Bystander[] Others, Clock Clock) Make(
        bool quiet = true, bool requiresSbl = false, FlashImage? sbl = null)
    {
        var clock = new Clock();
        var img = Image();
        var ecu = new SimulatedFlashEcu { ExpectedImage = img, S3 = TimeSpan.FromSeconds(5), Now = () => clock.Now, ErasePendingReplies = 0, RequiresSbl = requiresSbl };
        var others = new[] { new Bystander(), new Bystander() };
        var bus = new Bus(ecu, clock, others);
        var uds = new UdsClient(bus, 0x7E0) { SeedKey = new SimKey(), PendingRetryDelay = TimeSpan.Zero };
        var journal = new MemoryFlashJournal();
        var engine = new FlashEngine(new FlashPlan { TxId = 0x7E0, Image = img, Sbl = sbl, CommitRoutineId = sbl is null ? null : (ushort)0x0304 }, journal)
        {
            ReadVoltage = _ => Task.FromResult<double?>(13.4),
            Backup = _ => Task.FromResult("backup.json"),
            ReadModulePart = async (u, ct) => Encoding.ASCII.GetString(await u.ReadDataByIdentifierAsync(0xF188, ct)),
            Delay = t => { clock.Now += t; return Task.CompletedTask; },
            Now = () => new DateTimeOffset(clock.Now),
            QuietBus = quiet,
        };
        return (engine, uds, journal, ecu, bus, others, clock);
    }

    private static bool Is(byte[] r, params byte[] want) => r.SequenceEqual(want);
    private static int IndexOf(Bus bus, uint target, params byte[] req) => bus.Log.FindIndex(l => l.Target == target && Is(l.Req, req));

    [Fact]
    public async Task A_long_download_keeps_the_bus_quiet_throughout_then_restores_it()
    {
        var (eng, uds, _, ecu, bus, others, clock) = Make();
        int quietSince = -1;
        bool brokeSilence = false;
        ecu.Faults = (req, i) =>
        {
            // From the first download block on, every bystander must still be silent and DTC setting must still be off.
            if (req[0] == 0x36) { foreach (var o in others) { o.Tick(clock.Now); if (!o.CommOff || !o.DtcOff) brokeSilence = true; } quietSince = quietSince < 0 ? i : quietSince; }
            return null;
        };
        var r = await eng.RunAsync(uds);

        Assert.True(r.PartNumberConfirmed);
        Assert.True(quietSince >= 0);
        Assert.False(brokeSilence, "a bystander's S3 expired mid-download: its traffic and DTC setting came back during the flash");
        Assert.All(others, o => Assert.Equal(0, o.Timeouts));
        Assert.True(clock.Now - new DateTime(2026, 11, 16, 9, 0, 0, DateTimeKind.Utc) > TimeSpan.FromSeconds(15), "the download must outlast S3 for this test to mean anything");
        Assert.True(bus.Functional.Count(f => Is(f, 0x3E, 0x80)) >= 3);

        // Order: quiet before the programming session; restore after the reset.
        int q10 = IndexOf(bus, 0x7DF, 0x10, 0x83), q85 = IndexOf(bus, 0x7DF, 0x85, 0x82), q28 = IndexOf(bus, 0x7DF, 0x28, 0x83, 0x01);
        int prog = IndexOf(bus, 0x7E0, 0x10, 0x02), reset = bus.Log.FindIndex(l => l.Target == 0x7E0 && l.Req[0] == 0x11);
        int r28 = IndexOf(bus, 0x7DF, 0x28, 0x80, 0x01), r85 = IndexOf(bus, 0x7DF, 0x85, 0x81), r10 = IndexOf(bus, 0x7DF, 0x10, 0x81);
        Assert.True(q10 >= 0 && q10 < q85 && q85 < q28 && q28 < prog, $"quiet order {q10},{q85},{q28} before 10 02 at {prog}");
        Assert.True(reset >= 0 && reset < r28 && r28 < r85 && r85 < r10, $"restore order {r28},{r85},{r10} after reset at {reset}");
        Assert.All(others, o => { Assert.False(o.CommOff); Assert.False(o.DtcOff); Assert.Equal(0x01, o.Session); });
    }

    [Fact]
    public async Task A_programming_failure_restores_traffic_but_leaves_the_target_in_its_programming_session()
    {
        var (eng, uds, journal, ecu, bus, others, _) = Make();
        int blocks = 0;
        ecu.Faults = (req, _) => req[0] == 0x36 && ++blocks == 3 ? SimFlashFault.Negative(0x72) : null;   // general programming failure
        await Assert.ThrowsAnyAsync<FlashFailedException>(() => eng.RunAsync(uds));

        Assert.Contains(bus.Functional, f => Is(f, 0x28, 0x80, 0x01));
        Assert.Contains(bus.Functional, f => Is(f, 0x85, 0x81));
        Assert.DoesNotContain(bus.Functional, f => Is(f, 0x10, 0x81));          // would pull the target out of its programming session
        Assert.DoesNotContain(bus.Log, l => l.Target == 0x7E0 && l.Req[0] == 0x11);
        Assert.Equal(0x02, ecu.Session);
        Assert.All(others, o => { Assert.False(o.CommOff); Assert.False(o.DtcOff); });
        Assert.StartsWith("bus restored", journal.ReadAll().Last(e => e.Kind == FlashJournalKind.Phase).Detail);
        Assert.True(FlashJournalState.From(journal.ReadAll()).Open);
    }

    [Fact]
    public async Task A_lost_link_is_reported_as_an_interruption_not_as_a_restore_error()
    {
        var (eng, uds, journal, ecu, bus, _, _) = Make();
        int blocks = 0;
        ecu.Faults = (req, _) => req[0] == 0x36 && ++blocks == 4 ? SimFlashFault.LinkDown : null;
        var ex = await Assert.ThrowsAsync<FlashInterruptedException>(() => eng.RunAsync(uds));
        Assert.DoesNotContain("restore", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(journal.ReadAll(), e => e.Detail?.Contains("bus restore incomplete") == true);   // tried, failed, logged
        Assert.Contains(bus.Functional, f => Is(f, 0x28, 0x80, 0x01));
    }

    [Fact]
    public async Task A_re_entered_session_quiets_the_bus_again_and_restores_once()
    {
        var (eng, uds, _, ecu, bus, others, _) = Make();
        int blocks = 0;
        ecu.Faults = (req, _) => { if (req[0] == 0x36 && ++blocks == 5) ecu.DropSession(); return null; };
        var r = await eng.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.Equal(2, bus.Functional.Count(f => Is(f, 0x28, 0x83, 0x01)));
        Assert.Equal(1, bus.Functional.Count(f => Is(f, 0x28, 0x80, 0x01)));
        Assert.All(others, o => Assert.False(o.CommOff));
    }

    [Fact]
    public async Task Without_QuietBus_nothing_is_sent_functionally()
    {
        var (eng, uds, _, _, bus, others, _) = Make(quiet: false);
        var r = await eng.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.Empty(bus.Functional);
        Assert.All(others, o => Assert.Equal(0x01, o.Session));
    }

    [Fact]
    public async Task An_SBL_flash_with_a_quiet_bus_completes_and_restores()
    {
        var (eng, uds, _, ecu, bus, others, _) = Make(requiresSbl: true, sbl: Sbl());
        var r = await eng.RunAsync(uds);
        Assert.True(r.PartNumberConfirmed);
        Assert.Equal(1, ecu.Commits);
        Assert.True(IndexOf(bus, 0x7DF, 0x28, 0x83, 0x01) < bus.Log.FindIndex(l => l.Target == 0x7E0 && Convert.ToHexString(l.Req).StartsWith("31010301", StringComparison.Ordinal)));
        Assert.All(others, o => { Assert.Equal(0, o.Timeouts); Assert.False(o.CommOff); Assert.Equal(0x01, o.Session); });
    }

    [Fact]
    public async Task A_failure_before_the_erase_resets_the_target_back_and_restores_the_whole_bus()
    {
        var (eng, uds, _, ecu, bus, others, _) = Make(requiresSbl: true, sbl: null);   // erase refused (0x22): nothing written
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.Contains("Nothing was erased", ex.Message);
        Assert.False(ecu.InBootloader);
        int reset = bus.Log.FindIndex(l => l.Target == 0x7E0 && l.Req[0] == 0x11);
        Assert.True(reset >= 0 && reset < IndexOf(bus, 0x7DF, 0x10, 0x81));   // the target runs its software again: 10 81 is safe
        Assert.Equal(1, bus.Functional.Count(f => Is(f, 0x28, 0x80, 0x01)));
        Assert.All(others, o => { Assert.False(o.CommOff); Assert.False(o.DtcOff); Assert.Equal(0x01, o.Session); });
    }
}
