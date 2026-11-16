using System.Text;
using ObdLink.Core.Adapters;
using ObdLink.Core.Flash;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Flash engine against the SYNTHETIC bootloader module, with injected faults. No real vehicle or module is involved.</summary>
public class FlashEngineTests
{
    private sealed class SimKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(seed.Reverse().ToArray());
    }

    private sealed class SyncProgress(Action<FlashProgress> f) : IProgress<FlashProgress> { public void Report(FlashProgress p) => f(p); }

    private static byte[] Pattern(int n, int seed) => Enumerable.Range(0, n).Select(i => (byte)(i * 7 + seed)).ToArray();

    private static FlashImage Image(int segs = 2, int size = 1000, string part = "JL3A-14C204-ABC") =>
        FlashImage.FromSegments("test.vbf", Enumerable.Range(0, segs).Select(i => new FlashSegment(0x00010000u + (uint)i * 0x10000, Pattern(size, i))).ToList(),
            partNumber: part, ecuTxId: 0x7E0);

    private static (FlashEngine Engine, UdsClient Uds, MemoryFlashJournal Journal, List<string> Backups) Make(SimulatedFlashEcu ecu, FlashImage img,
        Func<double>? volts = null, FlashPlan? plan = null, IProgress<FlashProgress>? progress = null)
    {
        ecu.ExpectedImage = img;
        var uds = new UdsClient(ecu, 0x7E0) { SeedKey = new SimKey(), PendingRetryDelay = TimeSpan.Zero };
        var journal = new MemoryFlashJournal();
        var backups = new List<string>();
        var engine = new FlashEngine(plan ?? new FlashPlan { TxId = 0x7E0, Image = img }, journal)
        {
            ReadVoltage = _ => Task.FromResult<double?>(volts?.Invoke() ?? ecu.Volts),
            Backup = _ => { backups.Add("backup-1.json"); return Task.FromResult("backup-1.json"); },
            ReadModulePart = async (u, ct) => Encoding.ASCII.GetString((await u.ReadDataByIdentifierAsync(0xF188, ct))),
            Delay = _ => Task.CompletedTask,
            Progress = progress,
        };
        return (engine, uds, journal, backups);
    }

    private static void AssertFlashed(SimulatedFlashEcu ecu, FlashImage img)
    {
        foreach (var s in img.Segments) Assert.Equal(s.Data, ecu.Memory(s.Address));
        Assert.True(ecu.ApplicationValid);
        Assert.False(ecu.InBootloader);
    }

    [Fact]
    public async Task HappyPath_ElmStyleChannel_PollsEraseAndConfirmsPart()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image();
        var (eng, uds, journal, backups) = Make(ecu, img);
        var r = await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.True(r.PartNumberConfirmed);
        Assert.Equal("JL3A-14C204-ABC", r.ModulePartAfter);
        Assert.Single(backups);
        var st = eng.State;
        Assert.False(st.Open);
        Assert.Equal(2, st.SegmentsDone);
        Assert.Contains(ecu.Calls, c => c.Length >= 4 && c[0] == 0x31 && c[1] == 0x03 && c[2] == 0xFF && c[3] == 0x00); // polled results
        Assert.Equal(1, ecu.Resets);
    }

    [Fact]
    public async Task HappyPath_ListeningChannel_WaitsOutPendingWithoutPolling()
    {
        var ecu = new ListeningSimulatedFlashEcu();
        var img = Image();
        var (eng, uds, _, _) = Make(ecu, img);
        var r = await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.True(r.PartNumberConfirmed);
        Assert.DoesNotContain(ecu.Calls, c => c.Length >= 2 && c[0] == 0x31 && c[1] == 0x03);
    }

    [Fact]
    public async Task LostTransferDataAnswer_IsResentWithSameCounter_AndWrittenOnce()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        int seen = 0;
        ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x02 && seen++ == 0 ? SimFlashFault.DropResponse : null;
        var (eng, uds, _, _) = Make(ecu, img);
        var r = await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Equal(1, ecu.TransferRepeatsIgnored);
        Assert.True(r.Retries >= 1);
        Assert.Equal(2, ecu.Calls.Count(c => c[0] == 0x36 && c[1] == 0x02));
    }

    [Fact]
    public async Task LostTransferDataRequest_IsResent()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        int seen = 0;
        ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x03 && seen++ == 0 ? SimFlashFault.DropRequest : null;
        var (eng, uds, _, _) = Make(ecu, img);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Equal(0, ecu.TransferRepeatsIgnored);
    }

    [Fact]
    public async Task PendingOnTransferData_ElmChannel_ResendSettlesIt()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        int seen = 0;
        ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x01 && seen++ == 0 ? SimFlashFault.Pending : null;
        var (eng, uds, _, _) = Make(ecu, img);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Equal(1, ecu.TransferRepeatsIgnored);
    }

    [Fact]
    public async Task LostTransferExitAnswer_SecondExitSequenceErrorMeansDone()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        int seen = 0;
        ecu.Faults = (req, _) => req[0] == 0x37 && seen++ == 0 ? SimFlashFault.Pending : null;
        var (eng, uds, _, _) = Make(ecu, img);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
    }

    [Fact]
    public async Task CounterWrapsFrom0xFFTo0x00()
    {
        var ecu = new SimulatedFlashEcu { MaxBlockLength = 0x12 }; // 16 data bytes per block
        var img = Image(1, 16 * 300);
        var (eng, uds, _, _) = Make(ecu, img);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Contains(ecu.Calls, c => c[0] == 0x36 && c[1] == 0x00);
        Assert.Equal(300, ecu.TransferBlocksWritten);
    }

    [Fact]
    public async Task ProgrammingFailure_StopsWithoutReset_ThenResumeFinishes()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image();
        bool fail = true;
        ecu.Faults = (req, _) => fail && req[0] == 0x36 && req[1] == 0x02 ? SimFlashFault.Negative(0x72) : null;
        var (eng, uds, journal, backups) = Make(ecu, img);
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.True(ex.ModuleMayBeInBootloader);
        Assert.Equal((byte)0x72, ex.Fault?.Nrc);
        Assert.Equal(0, ecu.Resets); // never reset after a failure
        Assert.DoesNotContain(ecu.Calls, c => c[0] == 0x11 || (c[0] == 0x10 && c[1] == 0x01));
        Assert.True(eng.State.Open);
        Assert.Equal(FlashJournalKind.Fault, eng.State.LastFault?.Kind);

        fail = false;
        var r = await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Equal(2, r.Attempt);
        Assert.Single(backups); // a resume does not take a second backup
        Assert.False(eng.State.Open);
    }

    [Fact]
    public async Task LinkLostMidTransfer_InterruptsThenResumesOnOtherAdapter()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(2, 3000);
        ecu.Faults = (req, i) => req[0] == 0x36 && req[1] == 0x05 && !ecu.LinkDown && ecu.TransferBlocksWritten == 4 ? SimFlashFault.LinkDown : null;
        var (eng, uds, _, _) = Make(ecu, img);
        var ex = await Assert.ThrowsAsync<FlashInterruptedException>(() => eng.RunAsync(uds));
        Assert.Equal(FlashFaultAction.LinkLost, ex.Fault?.Action);
        Assert.True(eng.State.Open);
        Assert.True(ecu.InBootloader);

        // "Plug in the EX": a new client on a working link to the same module.
        ecu.LinkDown = false;
        ecu.Faults = null;
        var uds2 = new UdsClient(ecu, 0x7E0) { SeedKey = new SimKey(), PendingRetryDelay = TimeSpan.Zero };
        var r = await eng.RunAsync(uds2);
        AssertFlashed(ecu, img);
        Assert.Equal(2, r.Attempt);
    }

    [Fact]
    public async Task RepeatedTimeouts_RunOutOfRetries_InterruptNotFatal()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x02 ? SimFlashFault.DropRequest : null;
        var (eng, uds, _, _) = Make(ecu, img);
        var ex = await Assert.ThrowsAsync<FlashInterruptedException>(() => eng.RunAsync(uds));
        Assert.Equal(new FlashRetryPolicy().BlockRetries + 1, ecu.Calls.Count(c => c[0] == 0x36 && c[1] == 0x02));
        Assert.True(eng.State.Open);
        Assert.Contains("resume", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VoltageNrcOnBlock_WaitsForBatteryThenResends()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        int seen = 0;
        ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x03 && seen++ == 0 ? SimFlashFault.Negative(0x93) : null;
        int reads = 0;
        var (eng, uds, _, _) = Make(ecu, img, volts: () => reads++ == 5 ? 11.9 : 13.3);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
    }

    [Fact]
    public async Task LowBatteryAtStart_RefusesBeforeSendingAnything()
    {
        var ecu = new SimulatedFlashEcu { Volts = 11.8 };
        var img = Image(1);
        var (eng, uds, journal, backups) = Make(ecu, img);
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.False(ex.ModuleMayBeInBootloader);
        Assert.Empty(ecu.Calls);
        Assert.Empty(backups);
        Assert.False(eng.State.Open);
    }

    [Fact]
    public async Task BatterySagsMidFlash_PausesUntilBackInWindow()
    {
        var ecu = new SimulatedFlashEcu { MaxBlockLength = 0x22 };
        var img = Image(1, 32 * 40);
        int reads = 0;
        var msgs = new List<string>();
        var (eng, uds, _, _) = Make(ecu, img, volts: () => ++reads is 3 or 4 ? 12.1 : 13.2, progress: new SyncProgress(p => msgs.Add(p.Message)));
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Contains(msgs, m => m.Contains("paused: battery"));
    }

    [Fact]
    public async Task NoSeedKeyProvider_FailsAtSecurity_InBootloaderSoRecoveryIsOffered()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        var (eng, uds, _, _) = Make(ecu, img);
        uds.SeedKey = new NullSeedKeyProvider();
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.Equal(FlashFaultAction.Security, ex.Fault?.Action);
        Assert.Contains("seed/key provider", ex.Message);
        Assert.DoesNotContain(ecu.Calls, c => c[0] == 0x31 || c[0] == 0x34);
    }

    [Fact]
    public async Task RefusesWithoutBackupStep_OrWithWritesOff()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        var uds = new UdsClient(ecu, 0x7E0) { SeedKey = new SimKey() };
        var noBackup = new FlashEngine(new FlashPlan { TxId = 0x7E0, Image = img }, new MemoryFlashJournal()) { ReadVoltage = _ => Task.FromResult<double?>(13.0) };
        await Assert.ThrowsAsync<FlashFailedException>(() => noBackup.RunAsync(uds));

        var (eng, uds2, _, _) = Make(ecu, img);
        uds2.Gate = new WriteGate(() => true);
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds2));
        Assert.Contains("Read-only", ex.Message);
        Assert.Empty(ecu.Calls);
    }

    [Fact]
    public async Task StopBeforeStart_LeavesModuleUnchanged_StopBetweenSegments_ResumeFinishes()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(3, 600);
        var (eng, uds, _, _) = Make(ecu, img);
        using (var pre = new CancellationTokenSource())
        {
            pre.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => eng.RunAsync(uds, pre.Token));
            Assert.DoesNotContain(ecu.Calls, c => c[0] == 0x10);
            Assert.False(eng.State.Open);
        }

        var cts = new CancellationTokenSource();
        var ecu2 = new SimulatedFlashEcu();
        var (eng2, uds2, _, _) = Make(ecu2, img, progress: new SyncProgress(p => { if (p.Phase == FlashPhase.TransferExit) cts.Cancel(); }));
        var ex = await Assert.ThrowsAsync<FlashInterruptedException>(() => eng2.RunAsync(uds2, cts.Token));
        Assert.Contains("Stopped by the operator", ex.Message);
        Assert.True(eng2.State.Open);
        var r = await eng2.RunAsync(uds2);
        AssertFlashed(ecu2, img);
    }

    [Fact]
    public async Task EraseResultsNotSupported_WaitsBlindThenDownloadRetriesUntilReady()
    {
        var ecu = new SimulatedFlashEcu { EraseResultsSupported = false, ErasePendingReplies = 6 };
        var img = Image(1);
        var (eng, uds, _, _) = Make(ecu, img);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
    }

    [Fact]
    public async Task OpenJobForAnotherImage_IsNotResumedWithThisOne()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        var journal = new MemoryFlashJournal();
        journal.Append(new FlashJournalEntry(DateTimeOffset.UtcNow, FlashJournalKind.Started, ImageSha256: "DIFFERENT", TxId: 0x7E0));
        var eng = new FlashEngine(new FlashPlan { TxId = 0x7E0, Image = img }, journal) { ReadVoltage = _ => Task.FromResult<double?>(13.0), Backup = _ => Task.FromResult("b") };
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(new UdsClient(ecu, 0x7E0)));
        Assert.Contains("different image", ex.Message);
        Assert.Empty(ecu.Calls);
    }

    [Fact]
    public async Task FileJournal_SurvivesTornLastLine_AndRebuildsState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"flash-{Guid.NewGuid():N}.jsonl");
        try
        {
            var j = new FileFlashJournal(path);
            j.Append(new FlashJournalEntry(DateTimeOffset.UtcNow, FlashJournalKind.Started, ImageSha256: "ABC", TxId: 0x726));
            j.Append(new FlashJournalEntry(DateTimeOffset.UtcNow, FlashJournalKind.Backup, Detail: "asbuilt-726.json"));
            j.Append(new FlashJournalEntry(DateTimeOffset.UtcNow, FlashJournalKind.BlockAcked, Segment: 1, Block: 3, Counter: 3, Offset: 768));
            File.AppendAllText(path, "{\"At\":\"2026-11-1"); // crash mid-write
            var st = FlashJournalState.From(new FileFlashJournal(path).ReadAll());
            Assert.True(st.Open);
            Assert.Equal("ABC", st.ImageSha256);
            Assert.Equal(0x726u, st.TxId);
            Assert.Equal("asbuilt-726.json", st.BackupRef);
            Assert.Equal(768, st.BytesAcked);
        }
        finally { File.Delete(path); }
        await Task.CompletedTask;
    }

    [Theory]
    [InlineData(0x78, FlashFaultAction.WaitPending)]
    [InlineData(0x21, FlashFaultAction.Retry)]
    [InlineData(0x92, FlashFaultAction.WaitVoltage)]
    [InlineData(0x93, FlashFaultAction.WaitVoltage)]
    [InlineData(0x33, FlashFaultAction.Security)]
    [InlineData(0x35, FlashFaultAction.Security)]
    [InlineData(0x36, FlashFaultAction.Security)]
    [InlineData(0x37, FlashFaultAction.Security)]
    [InlineData(0x22, FlashFaultAction.Fatal)]
    [InlineData(0x24, FlashFaultAction.Fatal)]
    [InlineData(0x70, FlashFaultAction.Fatal)]
    [InlineData(0x71, FlashFaultAction.Fatal)]
    [InlineData(0x72, FlashFaultAction.Fatal)]
    [InlineData(0x73, FlashFaultAction.Fatal)]
    public void NrcClassification(byte nrc, FlashFaultAction expected) => Assert.Equal(expected, FlashFaults.FromNrc(nrc).Action);

    [Fact]
    public void ExceptionClassification()
    {
        Assert.Equal(FlashFaultAction.LinkLost, FlashFaults.Classify(new IOException("gone")).Action);
        Assert.Equal(FlashFaultAction.LinkLost, FlashFaults.Classify(new ObjectDisposedException("port")).Action);
        Assert.Equal(FlashFaultAction.Retry, FlashFaults.Classify(new TimeoutException()).Action);
        Assert.Equal(FlashFaultAction.Retry, FlashFaults.Classify(new IsoTp.IsoTpException("fc")).Action);
        Assert.Equal(FlashFaultAction.WaitPending, FlashFaults.Classify(new UdsNegativeResponseException(0x36, 0x78)).Action);
    }

    [Fact]
    public void Crc_CheckValues()
    {
        Assert.Equal(0x29B1, VbfReader.Crc16Ccitt("123456789"u8));
        Assert.Equal(0xCBF43926u, VbfReader.Crc32("123456789"u8.ToArray()));
    }

    [Fact]
    public void Vbf_RoundTrip_HeaderFieldsAndBlocks()
    {
        var segs = new[] { new FlashSegment(0x8000, Pattern(300, 1)), new FlashSegment(0x20000, Pattern(77, 2)) };
        var erase = new[] { new EraseRegion(0x8000, 0x1000), new EraseRegion(0x20000, 0x800) };
        var bytes = VbfReader.Write(segs, "JL3A-14C204-ABC", 0x7E0, erase);
        var img = VbfReader.Parse(bytes, "x.vbf");
        Assert.Equal("JL3A-14C204-ABC", img.SoftwarePartNumber);
        Assert.Equal(0x7E0u, img.EcuTxId);
        Assert.Equal(erase, img.Erase);
        Assert.Equal(2, img.Segments.Count);
        Assert.Equal(segs[1].Data, img.Segments[1].Data);
        Assert.Equal(0x20000u, img.Segments[1].Address);
        Assert.Empty(img.Warnings); // file_checksum matched
        Assert.Equal(64, img.Sha256.Length);
    }

    [Fact]
    public void Vbf_DamagedBlock_IsRejected()
    {
        var bytes = VbfReader.Write(new[] { new FlashSegment(0x8000, Pattern(100, 3)) });
        bytes[^10] ^= 0x01;
        var ex = Assert.Throws<FlashImageException>(() => VbfReader.Parse(bytes));
        Assert.Contains("checksum", ex.Message);
        Assert.Throws<FlashImageException>(() => VbfReader.Parse(bytes[..^5]));
        Assert.Throws<FlashImageException>(() => VbfReader.Parse(Encoding.ASCII.GetBytes("not a vbf")));
    }

    [Fact]
    public void RequestDownloadAndMaxBlockLength()
    {
        Assert.Equal(Convert.FromHexString("3400440001000000000400"), FlashMessages.RequestDownload(0x00, 0x44, 0x00010000, 0x400));
        Assert.Equal(Convert.FromHexString("341033123456ABCDEF"), FlashMessages.RequestDownload(0x10, 0x33, 0x123456, 0xABCDEF));
        Assert.Equal(0x0FFF, FlashMessages.ParseMaxBlockLength(Convert.FromHexString("74200FFF")));
        Assert.Throws<InvalidDataException>(() => FlashMessages.ParseMaxBlockLength(Convert.FromHexString("7450")));
        Assert.Throws<ArgumentOutOfRangeException>(() => FlashMessages.AddressAndLength(0x22, 0x10000, 1));
    }

    [Fact]
    public void ProgrammingRoutinesStillRefusedOutsideTheFlashEngine()
    {
        Assert.Throws<RoutineRefusedException>(() => UdsRoutines.BuildRequest(RoutineControlType.Start, 0xFF00));
        Assert.Contains("flash engine", UdsRoutines.RefusalReason(0xFF01));
        Assert.Equal(Convert.FromHexString("3101FF00"), FlashMessages.Routine(RoutineControlType.Start, 0xFF00));
    }

    [Fact]
    public void Warnings_MxPlusAddsWirelessLine_FinalNeedsTypedFlash()
    {
        var plan = new FlashPlan { TxId = 0x726, Image = Image(1) };
        var mx = FlashWarnings.For(plan, AdapterProfiles.ObdLinkMxPlus);
        var ex = FlashWarnings.For(plan, AdapterProfiles.ObdLinkEx);
        Assert.Contains("Wireless", mx[0].Message);
        Assert.DoesNotContain("Wireless", ex[0].Message);
        Assert.Contains("charger", ex[0].Message);
        Assert.Equal("FLASH", ex[^1].TypedPhrase);
    }

    [Fact]
    public async Task Preflight_ReportsVoltageLinkImageAndAdapter()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        var uds = new UdsClient(ecu, 0x7E0);
        var plan = new FlashPlan { TxId = 0x7E0, Image = img };
        var ok = await FlashPreflight.RunAsync(uds, plan, _ => Task.FromResult<double?>(13.1), AdapterProfiles.ObdLinkEx);
        Assert.True(ok.CanFlash);
        Assert.Contains(ok.Items, i => i.Name == "Link" && i.Status == PreflightStatus.Pass);

        var mx = await FlashPreflight.RunAsync(uds, plan, _ => Task.FromResult<double?>(13.1), AdapterProfiles.ObdLinkMxPlus);
        Assert.True(mx.CanFlash); // a warning, not a block
        Assert.Contains(mx.Warnings, w => w.Name == "Adapter" && w.Detail.Contains("Wired OBDLink EX recommended"));

        var low = await FlashPreflight.RunAsync(uds, plan, _ => Task.FromResult<double?>(12.0), AdapterProfiles.ObdLinkEx);
        Assert.False(low.CanFlash);

        ecu.Faults = (req, i) => req[0] == 0x3E && i % 2 == 0 ? SimFlashFault.DropRequest : null;
        var flaky = await FlashPreflight.RunAsync(uds, plan, _ => Task.FromResult<double?>(13.1), AdapterProfiles.ObdLinkMxPlus);
        Assert.Contains(flaky.Items, i => i.Name == "Link" && i.Status == PreflightStatus.Fail);

        var wrong = new FlashPlan { TxId = 0x726, Image = img };
        var w = await FlashPreflight.RunAsync(new UdsClient(new SimulatedFlashEcu(0x726), 0x726), wrong, _ => Task.FromResult<double?>(13.1), null);
        Assert.Contains(w.Items, i => i.Name == "Image target" && i.Status == PreflightStatus.Fail);
    }
}
