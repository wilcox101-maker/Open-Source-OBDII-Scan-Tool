using ObdLink.Core.AsBuilt;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.16.0: As-Built change history (verified writes and restores) and undo via the last backup.</summary>
public class AsBuiltHistoryTests
{
    private const string Vin = "1FTFW1E80HFA00001";
    private static readonly byte[] Original = { 1, 2, 3, 4, 5, 6, 7 };

    private static Func<string, AsBuiltService>? _reopen;

    private static async Task<(ObdLink.Core.Transport.SimulatedAdapter sim, AsBuiltService svc, string dir)> Setup()
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.Dids[0x726] = new() { [0xDE00] = (byte[])Original.Clone() };
        var dir = Path.Combine(Path.GetTempPath(), "abhist_" + Guid.NewGuid());
        _reopen = d => new AsBuiltService(m => new UdsClient(ch, m), new SafetyGuard(s, new AutoConfirm(true)), d) { PendingSettle = TimeSpan.Zero };
        return (sim, _reopen(dir), dir);
    }

    private static AsBuiltBlock Block(params byte[] d) => new() { Module = 0x726, Block = 1, Data = d };

    [Fact]
    public async Task Verified_write_is_recorded_with_before_after_and_backup()
    {
        var (_, svc, dir) = await Setup();
        var r = await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9));
        var h = Assert.Single(svc.History.Load());
        Assert.Equal(("write", (ushort)0x726, (byte)1), (h.Kind, h.Module, h.Block));
        Assert.Equal("01020304050607", h.Before);
        Assert.Equal("01020304050609", h.After);
        Assert.Equal(1, h.BytesChanged);
        Assert.Equal(Path.GetFileName(r.BackupPath), h.BackupFile);           // a name, so a moved Data folder still works
        Assert.Equal(r.BackupPath, svc.History.BackupPathOf(h));
        Assert.Equal("A00001", h.VinTail);
        Assert.StartsWith(dir, svc.History.Path);
    }

    [Fact]
    public async Task Nothing_is_recorded_when_nothing_was_written()
    {
        var (_, svc, _) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(Original));          // unchanged: no 2E is sent
        Assert.Empty(svc.History.LoadLines());
    }

    [Fact]
    public async Task A_verified_write_is_a_pending_line_then_an_outcome_line_folded_into_one_change()
    {
        var (_, svc, _) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9));
        var lines = svc.History.LoadLines();
        Assert.Equal(new[] { AsBuiltOutcome.Pending, AsBuiltOutcome.Verified }, lines.Select(l => l.Outcome));
        Assert.Equal(lines[0].AttemptId, lines[1].AttemptId);
        var c = Assert.Single(svc.History.Load());
        Assert.Equal(AsBuiltOutcome.Verified, c.Outcome);
        Assert.Equal(lines[0].At, c.At);                           // shown at the time the write started
        Assert.Empty(svc.History.Unresolved());
    }

    [Fact]
    public async Task A_read_back_mismatch_is_recorded_undoable_and_unresolved()
    {
        var (sim, svc, _) = await Setup();
        sim.CorruptWrites = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9)));
        var c = Assert.Single(svc.History.Load());
        Assert.Equal(AsBuiltOutcome.Mismatch, c.Outcome);
        Assert.Equal("01020304050609", c.After);                  // what was intended; the read value is in the note
        Assert.Contains("read back FE020304050609", c.Note);
        Assert.Equal(c.AttemptId, svc.History.LastUndoable(0x726)!.AttemptId);
        Assert.Single(svc.History.Unresolved());
    }

    [Fact]
    public async Task Recovering_a_mismatched_write_restores_the_old_bytes_and_settles_it()
    {
        var (sim, svc, _) = await Setup();
        sim.CorruptWrites = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9)));
        sim.CorruptWrites = false;
        var bad = Assert.Single(svc.History.Unresolved());
        var r = await svc.RecoverAttemptAsync(bad, Vin);
        Assert.True(r.Written);
        Assert.Equal(Original, sim.Dids[0x726][0xDE00]);
        Assert.Empty(svc.History.Unresolved());
        var all = svc.History.Load();
        Assert.Equal(AsBuiltOutcome.Recovered, all[0].Outcome);
        Assert.Equal(("restore", AsBuiltOutcome.Verified), (all[^1].Kind, all[^1].Outcome));
    }

    [Fact]
    public async Task A_refused_write_is_recorded_as_rejected_and_needs_nothing()
    {
        var (sim, svc, _) = await Setup();
        sim.WriteReply = ObdLink.Core.Transport.SimWriteReply.RejectNotStored;
        await Assert.ThrowsAnyAsync<Exception>(() => svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9)));
        var c = Assert.Single(svc.History.Load());
        Assert.Equal(AsBuiltOutcome.Rejected, c.Outcome);
        Assert.Contains("NRC 0x22", c.Note);
        Assert.Empty(svc.History.Unresolved());
        Assert.Null(svc.History.LastUndoable(0x726));             // nothing was stored, so nothing to undo
    }

    [Fact]
    public async Task Adapter_pulled_right_after_the_write_is_unknown_then_check_finds_it_stored()
    {
        var (sim, svc, _) = await Setup();
        sim.SilentAfterWrite = true;                               // module stores the data, then the link is gone
        var ex = await Assert.ThrowsAsync<InvalidDataException>(() => svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9)));
        Assert.Contains("UNKNOWN", ex.Message);
        var open = Assert.Single(svc.History.Unresolved());
        Assert.Equal(AsBuiltOutcome.Unknown, open.Outcome);

        sim.Reconnect();
        var (outcome, now) = await svc.CheckAttemptAsync(open);
        Assert.Equal(AsBuiltOutcome.Verified, outcome);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 9 }, now);
        Assert.Empty(svc.History.Unresolved());
    }

    [Fact]
    public async Task A_write_interrupted_by_a_crash_is_found_and_recovered_from_its_backup()
    {
        var (sim, svc, dir) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9));
        // Simulate a crash after the pending line of a second write reached disk and the module stored it: keep only the pending line.
        var lines = File.ReadAllLines(svc.History.Path);
        sim.Dids[0x726][0xDE00] = new byte[] { 1, 2, 3, 4, 5, 8, 9 };
        var backup = Directory.GetFiles(dir, "*.asbuilt.json").Single();
        var crashed = new AsBuiltChange(DateTimeOffset.Now, "write", 0x726, 1, "01020304050609", "01020304050809", 1, Path.GetFileName(backup), "A00001",
            Outcome: AsBuiltOutcome.Pending, AttemptId: "crash1");
        File.WriteAllLines(svc.History.Path, lines);
        svc.History.Append(crashed);

        var after = _reopen!(dir);                                 // a fresh app start on the same folder
        var open = Assert.Single(after.History.Unresolved());
        Assert.Equal(("crash1", AsBuiltOutcome.Pending), (open.AttemptId, open.Outcome));
        var (outcome, _) = await after.CheckAttemptAsync(open);
        Assert.Equal(AsBuiltOutcome.Verified, outcome);            // the module did store it
        Assert.Empty(after.History.Unresolved());
    }

    [Fact]
    public async Task A_restore_that_stops_half_way_says_how_far_it_got()
    {
        var (sim, svc, dir) = await Setup();
        sim.Dids[0x726][0xDE01] = new byte[] { 9, 9, 9 };
        var bk = await svc.BackupAsync(Vin, 0x726, new byte[] { 1, 2 });
        await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9));
        await svc.WriteBlockAsync(Vin, new AsBuiltBlock { Module = 0x726, Block = 2, Data = new byte[] { 8, 8, 8 } });
        sim.WritesBeforeReject = 1;                                // block 1 restores, block 2 is refused
        var ex = await Assert.ThrowsAsync<PartialRestoreException>(() => svc.RestoreAsync(bk, Vin));
        Assert.Equal((1, 2), (ex.Done, ex.Total));
        Assert.Contains("after 1 of 2", ex.Message);
        Assert.Equal(Original, sim.Dids[0x726][0xDE00]);
        Assert.Equal(new byte[] { 8, 8, 8 }, sim.Dids[0x726][0xDE01]);
    }

    [Fact]
    public void Lines_written_before_the_journal_existed_read_as_verified()
    {
        var dir = Path.Combine(Path.GetTempPath(), "abhist_" + Guid.NewGuid());
        var h = new AsBuiltHistory(dir);
        File.WriteAllText(h.Path, "{\"At\":\"2026-11-15T12:00:00+00:00\",\"Kind\":\"write\",\"Module\":1830,\"Block\":1,\"Before\":\"00\",\"After\":\"01\",\"BytesChanged\":1,\"BackupFile\":\"x\",\"VinTail\":\"A00001\",\"Note\":\"\"}\n");
        var c = Assert.Single(h.Load());
        Assert.Equal(AsBuiltOutcome.Verified, c.Outcome);
        Assert.Empty(h.Unresolved());
        Directory.Delete(dir, true);
    }

    [Fact]
    public async Task Undo_restores_the_backup_of_the_last_write_and_is_recorded()
    {
        var (sim, svc, _) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9));
        await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 8, 9));
        var last = svc.History.LastUndoable(0x726)!;
        Assert.Equal("01020304050609", last.Before);
        var r = await svc.RestoreAsync(svc.History.BackupPathOf(last), Vin);
        Assert.True(r.Written);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 9 }, sim.Dids[0x726][0xDE00]);
        var all = svc.History.Load();
        Assert.Equal(3, all.Count);
        Assert.Equal("restore", all[^1].Kind);
        Assert.Equal("01020304050809", all[^1].Before);
        Assert.Equal("01020304050609", all[^1].After);
        Assert.Contains(last.BackupFile, all[^1].Note);
    }

    [Fact]
    public async Task No_undo_when_the_backup_file_is_gone_or_for_another_module()
    {
        var (_, svc, _) = await Setup();
        var r = await svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9));
        Assert.Null(svc.History.LastUndoable(0x7D0));
        File.Delete(r.BackupPath!);
        Assert.Null(svc.History.LastUndoable(0x726));
    }

    [Fact]
    public void Torn_lines_are_skipped()
    {
        var dir = Path.Combine(Path.GetTempPath(), "abhist_" + Guid.NewGuid());
        var h = new AsBuiltHistory(dir);
        h.Append(new AsBuiltChange(DateTimeOffset.Now, "write", 0x726, 1, "00", "01", 1, "x", "A00001"));
        File.AppendAllText(h.Path, "{\"At\":\"2026-11-15T12:00:00+00:00\",\"Kind\":\"wri\n\n");
        h.Append(new AsBuiltChange(DateTimeOffset.Now, "write", 0x726, 2, "00", "02", 1, "y", "A00001"));
        Assert.Equal(new byte[] { 1, 2 }, h.Load().Select(c => c.Block));
        Directory.Delete(dir, true);
    }
}
