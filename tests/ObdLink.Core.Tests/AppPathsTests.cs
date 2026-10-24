using ObdLink.Core.Docs;
using ObdLink.Core.Logging;
using ObdLink.Core.Storage;
using Xunit;

namespace ObdLink.Core.Tests;

public class AppPathsTests
{
    private static string Tmp() { var d = Path.Combine(Path.GetTempPath(), "appaths-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(d); return d; }

    [Fact]
    public void Portable_data_folder_next_to_exe()
    {
        var exe = Tmp(); var legacy = Path.Combine(Tmp(), "OBDLinkDiag");
        var p = AppPaths.Resolve(exe, legacy);
        Assert.True(p.IsPortable);
        Assert.Equal(Path.Combine(exe, "Data"), p.DataDir);
        Assert.Equal(Path.Combine(exe, "Data", "logs"), p.LogsDir);
        Assert.Equal(Path.Combine(exe, "Data", "settings.json"), p.SettingsFile);
        Assert.True(Directory.Exists(p.BackupsDir) && Directory.Exists(p.CapturesDir));
        Assert.False(p.ShouldShowFallbackNotice());
        Assert.Empty(Directory.GetFiles(p.DataDir)); // write probe cleaned up
    }

    [Fact]
    public void Falls_back_to_legacy_when_exe_dir_unwritable_and_notice_once()
    {
        var exe = Tmp(); File.WriteAllText(Path.Combine(exe, "Data"), "a file blocks the folder");
        var legacy = Path.Combine(Tmp(), "OBDLinkDiag");
        var p = AppPaths.Resolve(exe, legacy);
        Assert.False(p.IsPortable);
        Assert.Equal(legacy, p.DataDir);
        Assert.NotNull(p.FallbackReason);
        Assert.True(p.ShouldShowFallbackNotice());
        Assert.False(p.ShouldShowFallbackNotice());
        Assert.False(p.MigrateFrom(legacy).Ran);
    }

    [Fact]
    public void Migration_moves_verified_files_keeps_newer_destination_and_leaves_note()
    {
        var exe = Tmp(); var legacy = Path.Combine(Tmp(), "OBDLinkDiag");
        Directory.CreateDirectory(Path.Combine(legacy, "logs")); Directory.CreateDirectory(Path.Combine(legacy, "backups"));
        File.WriteAllText(Path.Combine(legacy, "settings.json"), "{\"old\":true}");
        File.WriteAllText(Path.Combine(legacy, "logs", "events-1.log"), "old log");
        File.WriteAllText(Path.Combine(legacy, "backups", "a.asbuilt.json"), "backup");
        var p = AppPaths.Resolve(exe, legacy);
        File.WriteAllText(p.SettingsFile, "{\"new\":true}");
        File.SetLastWriteTimeUtc(p.SettingsFile, DateTime.UtcNow.AddHours(1));

        var r = p.MigrateFrom(legacy);
        Assert.True(r.Ran);
        Assert.Equal(2, r.Moved); Assert.Equal(1, r.SkippedNewer); Assert.Equal(0, r.KeptUnverified);
        Assert.Equal("{\"new\":true}", File.ReadAllText(p.SettingsFile));               // newer file not overwritten
        Assert.True(File.Exists(Path.Combine(legacy, "settings.json")));                 // skipped original kept
        Assert.Equal("backup", File.ReadAllText(Path.Combine(p.BackupsDir, "a.asbuilt.json")));
        Assert.False(File.Exists(Path.Combine(legacy, "backups", "a.asbuilt.json")));   // removed only after verify
        Assert.Equal("old log", File.ReadAllText(Path.Combine(p.LogsDir, "events-1.log")));
        Assert.Contains(p.DataDir, File.ReadAllText(Path.Combine(legacy, AppPaths.MigratedNote)));
        Assert.False(p.MigrateFrom(legacy).Ran); // one-time
    }

    [Fact]
    public void Markdown_parses_headings_lists_tables()
    {
        var b = MarkdownBlocks.Parse("# Title\n\nSome **bold** text\nwraps.\n\n- one\n- two\n\n1. first\n\n| A | B |\n|---|---|\n| 1 | 2 |\n");
        Assert.Equal(MdKind.Heading, b[0].Kind); Assert.Equal("Title", b[0].Text); Assert.Equal(1, b[0].Level);
        Assert.Equal("Some **bold** text wraps.", b[1].Text);
        Assert.Equal(2, b.Count(x => x.Kind == MdKind.Bullet));
        Assert.Equal(MdKind.Numbered, b[4].Kind);
        var t = b.Single(x => x.Kind == MdKind.Table);
        Assert.Equal(2, t.Rows!.Count); Assert.Equal(new[] { "1", "2" }, t.Rows[1]);
        Assert.Contains(MarkdownBlocks.Inline("a **b** `c`"), r => r.Bold && r.Text == "b");
        Assert.DoesNotContain(b, x => x.Text.Contains('|') || x.Text.StartsWith("#"));
    }

    [Fact]
    public void Simulator_demo_vin_is_redactable()
    {
        var vin = new ObdLink.Core.Transport.SimulatedAdapter().Vin;
        Assert.Equal(17, vin.Length);
        Assert.DoesNotContain(vin, VinRedactor.Redact("VIN " + vin));
    }
}
