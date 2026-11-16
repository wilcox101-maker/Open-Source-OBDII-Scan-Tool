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

    private static async Task<(ObdLink.Core.Transport.SimulatedAdapter sim, AsBuiltService svc, string dir)> Setup()
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.Dids[0x726] = new() { [0xDE00] = (byte[])Original.Clone() };
        var dir = Path.Combine(Path.GetTempPath(), "abhist_" + Guid.NewGuid());
        var svc = new AsBuiltService(m => new UdsClient(ch, m), new SafetyGuard(s, new AutoConfirm(true)), dir) { PendingSettle = TimeSpan.Zero };
        return (sim, svc, dir);
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
    public async Task Nothing_is_recorded_when_nothing_was_written_or_the_read_back_failed()
    {
        var (sim, svc, _) = await Setup();
        await svc.WriteBlockAsync(Vin, Block(Original));          // unchanged
        sim.CorruptWrites = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => svc.WriteBlockAsync(Vin, Block(1, 2, 3, 4, 5, 6, 9)));
        Assert.Empty(svc.History.Load());
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
