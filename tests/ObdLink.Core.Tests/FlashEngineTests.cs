using System.Text;
using ObdLink.Core.Adapters;
using ObdLink.Core.AsBuilt;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Transport;
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
    public async Task NoSeedKeyProvider_FailsAtSecurity_ResetsModuleBackToItsSoftware()
    {
        // The likeliest real-world failure (no Ford key): nothing was erased, so the module is reset into its own software
        // and the job is closed rather than left open as "may be in the bootloader".
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        var (eng, uds, _, backups) = Make(ecu, img);
        uds.SeedKey = new NullSeedKeyProvider();
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.Equal(FlashFaultAction.Security, ex.Fault?.Action);
        Assert.Contains("seed/key provider", ex.Message);
        Assert.Contains("Nothing was erased", ex.Message);
        Assert.False(ex.ModuleMayBeInBootloader);
        Assert.DoesNotContain(ecu.Calls, c => c[0] == 0x31 || c[0] == 0x34);
        Assert.Equal(1, ecu.Resets);
        Assert.False(ecu.InBootloader);
        Assert.True(ecu.ApplicationValid);
        Assert.False(eng.State.Open);
        Assert.Single(backups); // the backup taken before the attempt is kept
    }

    [Fact]
    public async Task FailureBeforeErase_ModuleSilentAfterReset_JobStaysOpen()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        ecu.Faults = (req, _) => req[0] == 0x11 ? SimFlashFault.LinkDown : null;
        var (eng, uds, _, _) = Make(ecu, img);
        uds.SeedKey = new NullSeedKeyProvider();
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.True(ex.ModuleMayBeInBootloader);
        Assert.DoesNotContain("Nothing was erased", ex.Message);
        Assert.True(eng.State.Open);
    }

    [Fact]
    public async Task ResumedJob_FailingAtSecurity_IsNotReset()
    {
        // A resume follows an attempt that may have erased the module: a failure now must not reset it.
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        bool fail = true;
        ecu.Faults = (req, _) => fail && req[0] == 0x36 ? SimFlashFault.Negative(0x72) : null;
        var (eng, uds, _, _) = Make(ecu, img);
        await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        fail = false;
        uds.SeedKey = new NullSeedKeyProvider();
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.True(ex.ModuleMayBeInBootloader);
        Assert.Equal(0, ecu.Resets);
        Assert.True(eng.State.Open);
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

    [Fact]
    public async Task PreflashBackup_SavesEveryAsBuiltBlock_OrNullWhenTheModuleHasNone()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var dir = Path.Combine(Path.GetTempPath(), "preflash-" + Guid.NewGuid().ToString("N"));
        try
        {
            var svc = new AsBuiltService(m => new UdsClient(new ElmDiagChannel(s), m), new SafetyGuard(s, new AutoConfirm(false)), dir);
            var path = await svc.BackupAvailableAsync("NOVIN", 0x7E1);
            Assert.NotNull(path);
            Assert.EndsWith("_preflash.asbuilt.json", path);
            var bk = AsBuiltBackup.Deserialize(File.ReadAllText(path!));
            Assert.Equal(sim.Dids[0x7E1].Keys.Count(k => k >= 0xDE00 && k <= 0xDEFF), bk.Blocks.Count);
            sim.Dids[0x7E0].Remove(0xDE00); // a module that answers but has no As-Built block 1
            Assert.Null(await svc.BackupAvailableAsync("NOVIN", 0x7E0));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    /// <summary>A VBF whose block checksums use CRC-16/XMODEM (init 0) instead of CCITT-FALSE: Inspect names it, Parse refuses it.</summary>
    private static byte[] XmodemVbf()
    {
        var good = VbfReader.Write(new[] { new FlashSegment(0x8000, Pattern(64, 5)) });
        var data = Pattern(64, 5);
        ushort crc = 0;
        foreach (var b in data) { crc ^= (ushort)(b << 8); for (int i = 0; i < 8; i++) crc = (crc & 0x8000) != 0 ? (ushort)(crc << 1 ^ 0x1021) : (ushort)(crc << 1); }
        good[^2] = (byte)(crc >> 8); good[^1] = (byte)crc;
        return good;
    }

    [Fact]
    public void Vbf_Inspect_NamesTheChecksumAFileReallyUses()
    {
        var ok = VbfReader.Inspect(VbfReader.Write(new[] { new FlashSegment(0x8000, Pattern(64, 5)) }, "P-1", 0x7E0));
        Assert.True(ok.ReaderAccepts);
        Assert.Equal("\"P-1\"", ok.Header["sw_part_number"]);
        var x = VbfReader.Inspect(XmodemVbf());
        Assert.False(x.ReaderAccepts);
        Assert.Contains("XMODEM", x.Blocks[0].AlternativeMatch);
        Assert.Throws<FlashImageException>(() => VbfReader.Parse(XmodemVbf()));
        Assert.NotNull(VbfReader.Inspect(Encoding.ASCII.GetBytes("garbage")).Error);
    }

    [Fact]
    public async Task Cli_VbfInfo_AcceptsGoodFile_RefusesOtherChecksumWithExit3()
    {
        var dir = Path.Combine(Path.GetTempPath(), "vbfinfo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var good = Path.Combine(dir, "good.vbf"); File.WriteAllBytes(good, VbfReader.Write(new[] { new FlashSegment(0x8000, Pattern(300, 1)) }, "JL3A-1", 0x7E0));
            var bad = Path.Combine(dir, "bad.vbf"); File.WriteAllBytes(bad, XmodemVbf());
            var o = new StringWriter(); var e = new StringWriter();
            Assert.Equal(0, await ObdLink.Cli.Cli.RunAsync(new[] { "vbf-info", "--file", good }, o, e));
            Assert.Contains("reader accepts", o.ToString());
            o = new StringWriter();
            Assert.Equal(3, await ObdLink.Cli.Cli.RunAsync(new[] { "vbf-info", "--file", bad }, o, e));
            Assert.Contains("XMODEM", o.ToString());
            Assert.Contains("REFUSE", o.ToString());
            o = new StringWriter();
            Assert.Equal(0, await ObdLink.Cli.Cli.RunAsync(new[] { "vbf-info", "--file", good, "--json" }, o, e));
            Assert.True(System.Text.Json.JsonDocument.Parse(o.ToString()).RootElement.GetProperty("accepted").GetBoolean());
        }
        finally { Directory.Delete(dir, true); }
    }

    private static FlashPlan PlanFor(IReadOnlyList<FlashSegment> segs, IReadOnlyList<EraseRegion>? erase = null, byte alfid = 0x44) =>
        new() { TxId = 0x7E0, AddressAndLengthFormat = alfid, Image = FlashImage.FromSegments("t.vbf", segs, erase, ecuTxId: 0x7E0) };

    [Fact]
    public void Layout_AcceptsSeparateSegmentsInsideTheirEraseRegions()
    {
        var segs = new[] { new FlashSegment(0x10000, new byte[0x100]), new FlashSegment(0x10100, new byte[0x100]) };
        Assert.Null(FlashImageChecks.Layout(PlanFor(segs)));
        Assert.Null(FlashImageChecks.Layout(PlanFor(segs, new[] { new EraseRegion(0x10000, 0x10000) })));
    }

    [Fact]
    public void Layout_RefusesOverlapsOutsideEraseOverflowAndFormatMisfits()
    {
        Assert.Contains("overlap", FlashImageChecks.Layout(PlanFor(new[] { new FlashSegment(0x10000, new byte[0x200]), new FlashSegment(0x10100, new byte[0x10]) })));
        Assert.Contains("not inside any erase region", FlashImageChecks.Layout(PlanFor(new[] { new FlashSegment(0x30000, new byte[0x10]) }, new[] { new EraseRegion(0x10000, 0x10000) })));
        Assert.Contains("32-bit", FlashImageChecks.Layout(PlanFor(new[] { new FlashSegment(0xFFFFFFF0, new byte[0x20]) })));
        Assert.Contains("format 0x33", FlashImageChecks.Layout(PlanFor(new[] { new FlashSegment(0x01000000, new byte[0x10]) }, alfid: 0x33)));
        Assert.Contains("not supported", FlashImageChecks.Layout(PlanFor(new[] { new FlashSegment(0x1000, new byte[0x10]) }, alfid: 0x50)));
    }

    [Fact]
    public async Task Engine_RefusesABadLayoutBeforeSendingAnything()
    {
        var ecu = new SimulatedFlashEcu();
        var img = FlashImage.FromSegments("bad.vbf", new[] { new FlashSegment(0x10000, new byte[0x200]), new FlashSegment(0x10100, new byte[0x10]) }, ecuTxId: 0x7E0);
        var (eng, uds, _, backups) = Make(ecu, img);
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.Contains("overlap", ex.Message);
        Assert.False(ex.ModuleMayBeInBootloader);
        Assert.Empty(ecu.Calls);
        Assert.Empty(backups);
    }

    [Theory]
    [InlineData("JL3A-12A650-AB", "JL3A-14C204-ABC", true)]
    [InlineData("jl3a12a650ab", "JL3A-14C204-ABC", true)]
    [InlineData("HL3A-12A650-AB", "JL3A-14C204-ABC", false)]
    [InlineData("AB", "JL3A-14C204-ABC", true)]
    public void SameProgram_ComparesTheVehicleProgramCode(string module, string file, bool same) =>
        Assert.Equal(same, FlashImageChecks.SameProgram(module, file));

    [Fact]
    public async Task Preflight_WarnsWhenTheFileNamesNoModuleOrAnotherProgram()
    {
        var ecu = new SimulatedFlashEcu();
        var uds = new UdsClient(ecu, 0x7E0);
        var plan = new FlashPlan { TxId = 0x7E0, Image = FlashImage.FromSegments("t.vbf", new[] { new FlashSegment(0x10000, new byte[0x10]) }, partNumber: "HL3A-14C204-AA") };
        var r = await FlashPreflight.RunAsync(uds, plan, _ => Task.FromResult<double?>(13.1), AdapterProfiles.ObdLinkEx,
            readModulePart: (_, _) => Task.FromResult<string?>("JL3A-12A650-AB"));
        Assert.Contains(r.Items, i => i.Name == "Image target" && i.Status == PreflightStatus.Warn);
        Assert.Contains(r.Items, i => i.Name == "Image layout" && i.Status == PreflightStatus.Pass);
        Assert.Contains(r.Items, i => i.Name == "Module part number" && i.Status == PreflightStatus.Warn && i.Detail.Contains("program code"));
    }

    // ---- keep-alive, lost session, lockout-safe security ----

    private sealed class WrongKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(new byte[] { 0xDE, 0xAD, 0x00 });
    }

    /// <summary>An engine whose waits advance the simulated module's clock, so S3 session timeouts happen as on a real module.</summary>
    private static (FlashEngine Engine, UdsClient Uds, MemoryFlashJournal Journal) Timed(SimulatedFlashEcu ecu, FlashImage img, FlashRetryPolicy? retry = null,
        Func<DateTimeOffset>? now = null, MemoryFlashJournal? journal = null)
    {
        ecu.ExpectedImage = img;
        var uds = new UdsClient(ecu, 0x7E0) { SeedKey = new SimKey(), PendingRetryDelay = TimeSpan.Zero };
        journal ??= new MemoryFlashJournal();
        var engine = new FlashEngine(new FlashPlan { TxId = 0x7E0, Image = img, Retry = retry ?? new FlashRetryPolicy() }, journal)
        {
            ReadVoltage = _ => Task.FromResult<double?>(13.2),
            Backup = _ => Task.FromResult("b.json"),
            Delay = t => { ecu.Advance(t); return Task.CompletedTask; },
            Now = now ?? (() => DateTimeOffset.UtcNow),
        };
        return (engine, uds, journal);
    }

    [Fact]
    public async Task SecurityDelay_InProgrammingSession_KeepsTheSessionAlive()
    {
        // 0x37 on the seed makes the engine wait 10 s; the module's S3 is 5 s. Without TesterPresent the session would be gone.
        var ecu = new SimulatedFlashEcu { S3 = TimeSpan.FromSeconds(5), SecurityDelaysLeft = 1 };
        var img = Image(1);
        var (eng, uds, _) = Timed(ecu, img);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Equal(0, ecu.SessionDrops);
        Assert.Contains(ecu.Calls, c => c.Length == 2 && c[0] == 0x3E && c[1] == 0x80);
    }

    [Fact]
    public async Task LowBatteryPause_InProgrammingSession_KeepsTheSessionAlive()
    {
        var ecu = new SimulatedFlashEcu { S3 = TimeSpan.FromSeconds(5) };
        var img = Image(1, 3000);
        int reads = 0;
        var (eng, uds, _) = Timed(ecu, img);
        var eng2 = new FlashEngine(new FlashPlan { TxId = 0x7E0, Image = img }, new MemoryFlashJournal())
        {
            ReadVoltage = _ => Task.FromResult<double?>(++reads is >= 3 and <= 7 ? 11.9 : 13.2), // ~10 s below the window mid-download
            Backup = _ => Task.FromResult("b.json"),
            Delay = t => { ecu.Advance(t); return Task.CompletedTask; },
            VoltageCheckEveryBlocks = 1,
        };
        await eng2.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Equal(0, ecu.SessionDrops);
    }

    [Fact]
    public async Task SessionDroppedMidDownload_RestartsFromEraseAndFinishes()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(2, 3000);
        int seen = 0;
        ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x04 && seen++ == 0 ? SimFlashFault.SessionDrop : null;
        var (eng, uds, journal) = Timed(ecu, img);
        var r = await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
        Assert.Equal(1, ecu.SessionDrops);
        Assert.Contains(journal.ReadAll(), e => e.Kind == FlashJournalKind.Restarted);
        Assert.Equal(2, ecu.Calls.Count(c => c[0] == 0x10 && c[1] == 0x02));
        Assert.Equal(2, eng.State.SegmentsDone); // the restart reset the count
        Assert.False(eng.State.Open);
        Assert.True(r.Retries >= 1);
    }

    [Fact]
    public async Task SessionDroppingRepeatedly_InterruptsWithTheJobOpen()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1, 3000);
        ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x03 ? SimFlashFault.SessionDrop : null;
        var (eng, uds, _) = Timed(ecu, img);
        var ex = await Assert.ThrowsAsync<SessionLostException>(() => eng.RunAsync(uds));
        Assert.Equal(FlashFaultAction.SessionLost, ex.Fault?.Action);
        Assert.Contains("Resume", ex.Message);
        Assert.True(eng.State.Open);
        Assert.Equal(0, ecu.Resets); // erased: never reset after a failure
    }

    [Fact]
    public async Task WrongKey_IsSentOnce_ThenResumeIsHeldUntilCleared()
    {
        var ecu = new SimulatedFlashEcu();
        var img = Image(1);
        var (eng, uds, journal) = Timed(ecu, img);
        uds.SeedKey = new WrongKey();
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.Equal((byte)0x35, ex.Fault?.Nrc);
        Assert.Equal(1, ecu.WrongKeys);
        Assert.False(ex.ModuleMayBeInBootloader); // nothing erased: reset back to its software

        // Run again (and again): refused before a single request, so the module's attempt counter is never spent.
        int before = ecu.Calls.Count;
        for (int i = 0; i < 3; i++)
        {
            var held = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
            Assert.Contains("clear the security hold", held.Message);
        }
        Assert.Equal(before, ecu.Calls.Count);
        Assert.Equal(1, ecu.WrongKeys);
        Assert.False(ecu.SecurityLocked);

        // Fixed provider, hold cleared: it flashes.
        uds.SeedKey = new SimKey();
        eng.ClearSecurityHold("provider fixed");
        Assert.Null(eng.SecurityHold);
        await eng.RunAsync(uds);
        AssertFlashed(ecu, img);
    }

    [Fact]
    public async Task Lockout_HoldsNewAttemptsForTheLockoutTime()
    {
        var ecu = new SimulatedFlashEcu { MaxKeyAttempts = 1 }; // first wrong key locks
        var img = Image(1);
        var clock = DateTimeOffset.Parse("2026-11-16T12:00:00Z");
        var (eng, uds, _) = Timed(ecu, img, now: () => clock);
        uds.SeedKey = new WrongKey();
        var ex = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.Equal((byte)0x36, ex.Fault?.Nrc);

        clock += TimeSpan.FromMinutes(4);
        int before = ecu.Calls.Count;
        var held = await Assert.ThrowsAsync<FlashFailedException>(() => eng.RunAsync(uds));
        Assert.Contains("6 more minute", held.Message);
        Assert.Equal(before, ecu.Calls.Count);

        clock += TimeSpan.FromMinutes(7);
        Assert.Null(eng.SecurityHold);
    }

    [Fact]
    public void SecurityState_SurvivesNewJobsAndClearsOnSuccess()
    {
        var t = DateTimeOffset.UtcNow;
        var e = new List<FlashJournalEntry>
        {
            new(t, FlashJournalKind.Security, Detail: FlashSecurity.WrongKey),
            new(t, FlashJournalKind.Started, ImageSha256: "x", TxId: 0x7E0),
        };
        Assert.Equal(1, FlashJournalState.From(e).WrongKeys);
        e.Add(new(t, FlashJournalKind.Security, Detail: FlashSecurity.Ok));
        Assert.Equal(0, FlashJournalState.From(e).WrongKeys);
        e.Add(new(t, FlashJournalKind.Security, Detail: FlashSecurity.Locked));
        Assert.Equal(t, FlashJournalState.From(e).LockedAt);
        Assert.NotNull(FlashSecurity.Hold(FlashJournalState.From(e), TimeSpan.FromMinutes(10), t.AddMinutes(1)));
        Assert.Null(FlashSecurity.Hold(FlashJournalState.From(e), TimeSpan.FromMinutes(10), t.AddMinutes(11)));
    }
}
