using ObdLink.Core.Dtc;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>C1 (write recovery plan): DTCs, freeze frames and extended data are saved to disk before a clear.</summary>
public class DtcSnapshotTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "dtcsnap-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Snapshot_reads_every_dtc_with_its_freeze_frame_and_is_on_disk_before_the_clear()
    {
        var (_, _, ch) = await AsBuiltServiceSafetyTests.Connect();
        var uds = new UdsClient(ch, 0x726);
        var snap = await DtcSnapshot.TakeAsync(uds, "BCM", 0x726);
        Assert.True(snap.Complete);
        var e = Assert.Single(snap.Entries);
        Assert.Equal(0x912345u, e.Dtc.Code);
        Assert.Equal((ushort)0xDD00, Assert.Single(e.Detail!.Snapshots).FirstDid);
        Assert.Equal("1 DTC(s), 1 freeze frame(s)", snap.Summary);

        var dir = TempDir();
        try
        {
            var path = snap.Save(dir, code => $"{code}: test description");
            await uds.ClearDtcsAsync();
            var text = File.ReadAllText(path);
            Assert.StartsWith("DTC snapshot before clear: BCM (0x726)", text);
            Assert.Contains(e.Dtc.Display, text);
            Assert.Contains("DID 0xDD00", text);
            Assert.Contains("test description", text);
            Assert.DoesNotContain("INCOMPLETE", text);
            Assert.Matches(@"dtc-snapshot-BCM-726-\d{8}-\d{6}\.txt$", path);
            // A second save in the same second never overwrites the first.
            var second = snap.Save(dir);
            Assert.NotEqual(path, second);
            Assert.True(File.Exists(path));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task A_module_that_will_not_list_its_dtcs_gives_an_incomplete_snapshot_not_an_exception()
    {
        var snap = await DtcSnapshot.TakeAsync(new UdsClient(new Module { ListNrc = 0x22 }, 0x726), "BCM", 0x726);
        Assert.False(snap.Complete);
        Assert.Empty(snap.Entries);
        Assert.Contains("could not be read", snap.Summary);
        Assert.Contains("INCOMPLETE", snap.Format());
    }

    [Fact]
    public async Task A_dtc_whose_details_fail_is_kept_with_the_reason_and_marks_the_snapshot_incomplete()
    {
        var snap = await DtcSnapshot.TakeAsync(new UdsClient(new Module { DetailsThrow = true }, 0x726), "BCM", 0x726);
        Assert.False(snap.Complete);
        var e = Assert.Single(snap.Entries);
        Assert.Null(e.Detail);
        Assert.Contains("link lost", e.Error);
        Assert.Contains("details missing for 1", snap.Summary);
        Assert.Contains("details not read: ", snap.Format());
    }

    [Fact]
    public async Task Cancelling_stops_the_snapshot()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DtcSnapshot.TakeAsync(new UdsClient(new Module { Cancel = true }, 0x726), "BCM", 0x726, cts.Token));
    }

    /// <summary>A module holding one DTC (B1123-45). 19 02 can be refused; 19 03+ can throw like a dropped link.</summary>
    private sealed class Module : IDiagChannel
    {
        public byte? ListNrc;
        public bool DetailsThrow, Cancel;
        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] r, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            if (Cancel) ct.ThrowIfCancellationRequested();
            if (r[0] == 0x19 && r[1] != 0x02 && DetailsThrow) throw new IOException("link lost");
            byte[] a = (r[0], r[1]) switch
            {
                (0x19, 0x02) => ListNrc is byte n ? new byte[] { 0x7F, 0x19, n } : new byte[] { 0x59, 0x02, 0xFF, 0x91, 0x23, 0x45, 0x2F },
                _ => new byte[] { 0x7F, r[0], 0x31 },
            };
            return Task.FromResult<IReadOnlyList<DiagResponse>>(new[] { new DiagResponse(0x72E, a) });
        }
    }
}
