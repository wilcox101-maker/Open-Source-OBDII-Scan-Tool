using ObdLink.Core.AsBuilt;
using ObdLink.Core.Storage;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// v0.23.18: the backup taken just before a module write is flushed to disk, never overwrites another file, is read back, and is
/// found again by "Last backup" whatever name form it has (batch, pre-write with milliseconds, pre-flash, same-second suffix).
/// </summary>
public class DurableBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "obd-durable-" + Guid.NewGuid().ToString("N")[..8]);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { /* temp folder; best effort */ } }

    [Fact]
    public async Task WriteNew_writes_exact_bytes_and_leaves_no_temp_file()
    {
        var p = Path.Combine(_dir, "a.asbuilt.json");
        await DurableFile.WriteNewAsync(p, "{\"x\":\"é\"}");
        Assert.Equal("{\"x\":\"é\"}", await File.ReadAllTextAsync(p));
        Assert.Equal(new byte[] { 0x7B }, (await File.ReadAllBytesAsync(p))[..1]); // no BOM
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task WriteNew_never_overwrites()
    {
        var p = Path.Combine(_dir, "a.asbuilt.json");
        await DurableFile.WriteNewAsync(p, "first");
        await Assert.ThrowsAsync<IOException>(() => DurableFile.WriteNewAsync(p, "second"));
        Assert.Equal("first", await File.ReadAllTextAsync(p));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public async Task Unique_adds_a_counter_before_the_suffix()
    {
        var p = Path.Combine(_dir, "V_726_20261120_120000.asbuilt.json");
        Assert.Equal(p, DurableFile.Unique(p, ".asbuilt.json"));
        await DurableFile.WriteNewAsync(p, "1");
        var p2 = DurableFile.Unique(p, ".asbuilt.json");
        Assert.EndsWith("V_726_20261120_120000_2.asbuilt.json", p2);
        await DurableFile.WriteNewAsync(p2, "2");
        Assert.EndsWith("_3.asbuilt.json", DurableFile.Unique(p, ".asbuilt.json"));
    }

    [Fact]
    public async Task Batch_backups_in_the_same_second_are_both_kept()
    {
        var now = new DateTime(2026, 11, 20, 12, 0, 0);
        var b1 = new AsBuiltBlock { Module = 0x726, Block = 0x01, Data = new byte[] { 1, 2, 3 } };
        var b2 = new AsBuiltBlock { Module = 0x726, Block = 0x01, Data = new byte[] { 4, 5, 6 } };
        var p1 = await AsBuiltBatch.SaveAsync(_dir, "XXXXXXXXXXX000001", 0x726, new[] { b1 }, now);
        var p2 = await AsBuiltBatch.SaveAsync(_dir, "XXXXXXXXXXX000001", 0x726, new[] { b2 }, now);
        Assert.NotEqual(p1, p2);
        Assert.Contains("010203", await File.ReadAllTextAsync(p1));
        Assert.Contains("040506", await File.ReadAllTextAsync(p2));
    }

    [Theory]
    [InlineData("XXXXXXXXXXX000001_726_20261120_120000.asbuilt.json", 0x726u, "2026-11-20 12:00:00.000")]
    [InlineData("XXXXXXXXXXX000001_726_20261120_120000_2.asbuilt.json", 0x726u, "2026-11-20 12:00:00.000")]
    [InlineData("XXXXXXXXXXX000001_7D0_20261120_120000_123.asbuilt.json", 0x7D0u, "2026-11-20 12:00:00.123")]
    [InlineData("NOVIN_760_20261120_120000_123_preflash.asbuilt.json", 0x760u, "2026-11-20 12:00:00.123")]
    [InlineData("NOVIN_760_20261120_120000_123_preflash_2.asbuilt.json", 0x760u, "2026-11-20 12:00:00.123")]
    [InlineData("A_B-C_7e0_20261120_235959_999.asbuilt.json", 0x7E0u, "2026-11-20 23:59:59.999")]
    [InlineData("NOVIN_726_20261120_120000_123_pre-restore.asbuilt.json", 0x726u, "2026-11-20 12:00:00.123")]
    [InlineData("X_726_20261120_120000_726_20261121_080000.asbuilt.json", 0x726u, "2026-11-21 08:00:00.000")] // last match wins
    public void Every_backup_name_form_is_recognised(string name, uint module, string when)
    {
        Assert.True(AsBuiltBatch.TryParseBackupName(name, out var m, out var w));
        Assert.Equal(module, m);
        Assert.Equal(DateTime.Parse(when, System.Globalization.CultureInfo.InvariantCulture), w);
    }

    [Theory]
    [InlineData("notes.asbuilt.json")]
    [InlineData("V_726_20261320_120000.asbuilt.json")] // month 13
    [InlineData("V_726_20261120_120000.json")]
    [InlineData(".V_726_20261120_120000.asbuilt.json.1a2b3c4d.tmp")]
    public void Other_names_are_ignored(string name) => Assert.False(AsBuiltBatch.TryParseBackupName(name, out _, out _));

    [Fact]
    public void Newest_backup_is_picked_across_name_forms()
    {
        Directory.CreateDirectory(_dir);
        void Touch(string n) => File.WriteAllText(Path.Combine(_dir, n), "{}");
        Touch("V_726_20261120_100000.asbuilt.json");            // batch, older
        Touch("V_726_20261120_110000_500.asbuilt.json");        // pre-write, newest for 726
        Touch("V_7E0_20261120_090000_000_preflash.asbuilt.json"); // pre-flash only
        var found = AsBuiltBatch.FindBackups(_dir);
        Assert.EndsWith("V_726_20261120_110000_500.asbuilt.json", found[0x726].Path);
        Assert.Equal(new DateTime(2026, 11, 20, 11, 0, 0, 500), found[0x726].When);
        Assert.True(found.ContainsKey(0x7E0));
    }

    [Fact]
    public void Backup_writers_use_the_durable_write()
    {
        string Src(params string[] p) => File.ReadAllText(Path.Combine(new[] { RepoRoot(), "src", "ObdLink.Core" }.Concat(p).ToArray()));
        foreach (var f in new[] { Src("AsBuilt", "AsBuiltService.cs"), Src("AsBuilt", "AsBuiltBatch.cs") })
        {
            Assert.Contains("DurableFile.WriteNewAsync(path, bk.Serialize(), ct)", f);
            Assert.DoesNotContain("File.WriteAllTextAsync(path, bk.Serialize()", f);
        }
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "ObdLink.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
