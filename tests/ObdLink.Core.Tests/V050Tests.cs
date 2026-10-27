using ObdLink.Core.AsBuilt;
using ObdLink.Core.Dashboard;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Obd;
using ObdLink.Core.Safety;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using ObdLink.Core.Ui;
using ObdLink.Core.Vehicles;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.5.0: DEMO TCM As-Built, unsupported-module UX, remembered buses, obvious UNVERIFIED gear/OSS, nav reachability.</summary>
public class V050Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(me)!, "..", ".."));

    private static async Task<(SimulatedAdapter Sim, AsBuiltService Svc, string Dir)> Setup()
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var dir = Path.Combine(Path.GetTempPath(), "v050-" + Guid.NewGuid().ToString("N"));
        var svc = new AsBuiltService(m => new UdsClient(new ElmDiagChannel(s), m), new SafetyGuard(s, new AutoConfirm(false)), dir);
        return (sim, svc, dir);
    }

    [Fact]
    public async Task Demo_tcm_reads_and_backs_up_de00_blocks_like_pcm()
    {
        var (sim, svc, dir) = await Setup();
        Assert.True(sim.Dids[0x7E1].ContainsKey(0xDE00));
        Assert.Equal(15, sim.Dids[0x7E1][0xDE00].Length);
        var blocks = await AsBuiltBatch.ReadAllBlocksAsync((b, ct) => svc.ReadBlockAsync(0x7E1, b, ct));
        Assert.Equal(8, blocks.Count);
        Assert.Equal(0xDE00, blocks[0].Did);
        Assert.Equal(0xDE07, blocks[^1].Did);
        Assert.Equal(sim.Dids[0x7E1][0xDE00], blocks[0].Data);
        var path = await svc.BackupAsync("NOVIN", 0x7E1, blocks.Select(b => b.Block));
        Assert.StartsWith(dir, path);
        Assert.Contains("7E1", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase);
        var text = await File.ReadAllTextAsync(path);
        Assert.Contains(Convert.ToHexString(blocks[0].Data), text);
        var pcm = await AsBuiltBatch.ReadAllBlocksAsync((b, ct) => svc.ReadBlockAsync(0x7E0, b, ct));
        Assert.Equal(8, pcm.Count);
    }

    [Fact]
    public async Task Module_without_de00_raises_a_clear_no_asbuilt_result()
    {
        var (sim, svc, _) = await Setup();
        foreach (var did in sim.Dids[0x7E1].Keys.Where(k => k is >= 0xDE00 and <= 0xDE0F).ToList())
            sim.Dids[0x7E1].Remove(did);
        var ex = await Assert.ThrowsAsync<AsBuiltNotSupportedException>(() => svc.ReadBlockAsync(0x7E1, 0x01));
        Assert.Equal(0x7E1u, ex.Module);
        Assert.Equal((ushort)0xDE00, ex.Did);
        Assert.Equal(0x31, ex.Nrc);
        Assert.Contains("no As-Built", ex.Message);
        Assert.Contains("0x31", ex.Message);
        Assert.DoesNotContain("UdsNegativeResponseException", ex.Message);
        await Assert.ThrowsAsync<AsBuiltNotSupportedException>(() => svc.BackupAsync("NOVIN", 0x7E1, new byte[] { 0x01 }));
        await Assert.ThrowsAsync<AsBuiltNotSupportedException>(() =>
            AsBuiltBatch.ReadAllBlocksAsync((b, ct) => svc.ReadBlockAsync(0x7E1, b, ct)));
    }

    [Fact]
    public void Unsupported_modules_are_hidden_and_their_actions_stay_off()
    {
        Assert.False(AsBuiltSupportPolicy.ShowInList(AsBuiltSupport.Unsupported, guessed: true, showAll: false, found: "YES"));
        Assert.True(AsBuiltSupportPolicy.ShowInList(AsBuiltSupport.Unsupported, guessed: true, showAll: true, found: "YES"));
        Assert.False(AsBuiltSupportPolicy.ShowInList(AsBuiltSupport.Supported, guessed: false, showAll: false, found: "no"));
        Assert.True(AsBuiltSupportPolicy.ShowInList(AsBuiltSupport.Supported, guessed: false, showAll: false, found: "YES"));
        Assert.True(AsBuiltSupportPolicy.ShowInList(AsBuiltSupport.Unknown, guessed: true, showAll: false, found: "not scanned"));
        Assert.False(AsBuiltSupportPolicy.ActionsEnabled(AsBuiltSupport.Unsupported));
        Assert.True(AsBuiltSupportPolicy.ActionsEnabled(AsBuiltSupport.Unknown));
        Assert.True(AsBuiltSupportPolicy.ActionsEnabled(AsBuiltSupport.Supported));
        Assert.Equal(AsBuiltSupport.Unsupported, AsBuiltSupportPolicy.FromDe00(false));
        Assert.Equal(AsBuiltSupport.Supported, AsBuiltSupportPolicy.FromDe00(true));
    }

    [Fact]
    public void Backup_summary_calls_out_no_asbuilt_without_calling_it_a_failure()
    {
        var tcm = FordF150_2017.Modules.Single(m => m.Acronym == "TCM");
        var pcm = FordF150_2017.Modules.Single(m => m.Acronym == "PCM");
        var acm = FordF150_2017.Modules.Single(m => m.Acronym == "ACM");
        var res = new List<BatchItemResult>
        {
            new(pcm, true, "8 block(s)", "pcm.json"),
            new(tcm, false, AsBuiltNotSupportedException.Describe("TCM", 0x7E1, 0xDE00), NoAsBuilt: true),
            new(acm, false, "timeout"),
        };
        var text = AsBuiltBatch.BackupSummary(res, @"C:\app\Data\asbuilt\2026-10-26");
        Assert.Contains("1 saved", text);
        Assert.Contains("1 with no As-Built", text);
        Assert.Contains("1 failed", text);
        Assert.False(AsBuiltBatch.HasHardFailure(res.Take(2).ToList()));
        Assert.True(AsBuiltBatch.HasHardFailure(res));
        Assert.Contains("no As-Built", AsBuiltBatch.Line(res[1]));
        Assert.DoesNotContain("failed", AsBuiltBatch.Line(res[1]));
        Assert.Contains("failed", AsBuiltBatch.Line(res[2]));
    }

    [Fact]
    public void Remembered_bus_is_tried_first_and_roundtrips_in_settings()
    {
        var ipc = FordF150_2017.Modules.Single(m => m.Acronym == "IPC");
        Assert.Equal(Bus.HsCan, ipc.Bus);
        var preferred = ModuleBusMemory.ForScan(ipc, Bus.HsCan2);
        Assert.Equal(Bus.HsCan2, preferred.Bus);
        Assert.Equal(ipc.TxId, preferred.TxId);
        Assert.Equal(new[] { Bus.HsCan, Bus.MsCan }, ModuleBusMemory.Remaining(tried: Bus.HsCan2, table: Bus.HsCan));
        Assert.Equal(new[] { Bus.HsCan2, Bus.MsCan }, ModuleBusMemory.Remaining(tried: Bus.HsCan, table: Bus.HsCan));
        Assert.False(ModuleBusMemory.ShouldPersist(simulatedAdapter: true));
        Assert.True(ModuleBusMemory.ShouldPersist(simulatedAdapter: false));

        var map = new Dictionary<string, string>();
        ModuleBusMemory.Remember(map, 0x720, Bus.HsCan2);
        Assert.Equal(Bus.HsCan2, ModuleBusMemory.Recall(map, 0x720));
        Assert.Null(ModuleBusMemory.Recall(map, 0x726));
        Assert.False(ModuleBusMemory.TryParse("J1850Vpw", out _));

        var path = Path.Combine(Path.GetTempPath(), "v050-bus-" + Guid.NewGuid().ToString("N"), "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var settings = new AppSettings();
        settings.ModuleBuses["720"] = "HsCan2";
        settings.ModuleBuses["nope"] = "HsCan";
        settings.ModuleBuses["726"] = "not-a-bus";
        settings.Normalize();
        Assert.Equal("HsCan2", Assert.Single(settings.ModuleBuses).Value);
        Assert.Equal("720", Assert.Single(settings.ModuleBuses).Key);
        settings.Save(path);
        Assert.Equal(Bus.HsCan2, ModuleBusMemory.Recall(AppSettings.Load(path).ModuleBuses, 0x720));
    }

    [Fact]
    public void Gear_and_oss_labels_say_unverified_and_pids_are_not_promoted()
    {
        Assert.Contains("UNVERIFIED", UnverifiedLabels.GearTitle);
        Assert.DoesNotContain("1E13", UnverifiedLabels.GearTitle);
        Assert.Contains("UNVERIFIED", UnverifiedLabels.GearFromDid("7E0", "1E12"));
        Assert.Contains("1E12", UnverifiedLabels.GearFromDid("7E0", "1E12"));
        Assert.Contains("Estimated", UnverifiedLabels.GearFromEstimate());
        Assert.Contains("UNVERIFIED", UnverifiedLabels.GearFromEstimate());
        Assert.Equal("Transmission fluid temp", UnverifiedLabels.Mark("Transmission fluid temp", verified: true));
        Assert.Equal("Output shaft speed (OSS) (UNVERIFIED)", UnverifiedLabels.Mark("Output shaft speed (OSS)", verified: false));
        var oss = UnverifiedLabels.OssSource(0x7E1, "1E13");
        Assert.Contains("TCM", oss);
        Assert.Contains("0x7E1", oss);
        Assert.Contains("UNVERIFIED", oss);
        Assert.Contains("PCM", UnverifiedLabels.OssSource(0x7E0, "1E13"));

        var table = EnhancedPidTable.Parse(EnhancedPidTable.DefaultJson());
        var ossPid = table.Pids.Single(p => p.Key == "oss");
        var gear = table.Pids.Single(p => p.Key == "gear");
        Assert.False(ossPid.Verified);
        Assert.False(gear.Verified);
        Assert.Equal("1E13", ossPid.Did);
        Assert.Equal(new uint[] { 0x7E0, 0x7E1 }, ossPid.Candidates.ToArray());
        Assert.DoesNotContain(EnhancedPidTable.VerifiedStockDids, kv => kv.Key == "oss" || kv.Value == "1E13");
        Assert.All(new[] { "2813", "2814", "2815", "2816" }, did => Assert.Contains(EnhancedPidTable.VerifiedStockDids.Values, v => v == did));
        Assert.Contains(DashCatalog.Build(table), c => c.Key == "gear" && !c.Verified && c.Label.Contains("UNVERIFIED"));
        Assert.Contains(DashCatalog.Build(table), c => c.Key == "oss" && !c.Verified && c.Label.Contains("UNVERIFIED"));
        Assert.Contains(DashCatalog.Build(table), c => c.Key == "tpms" && c.Verified && !c.Label.Contains("UNVERIFIED"));
    }

    [Fact]
    public void Nav_icon_mode_is_reachable_and_does_not_flicker_at_the_boundary()
    {
        var xaml = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainWindow.xaml"));
        var windowMin = int.Parse(System.Text.RegularExpressions.Regex.Match(xaml, @"<Window\b[^>]*\bMinHeight=""(\d+)""").Groups[1].Value);
        var rowMin = int.Parse(System.Text.RegularExpressions.Regex.Match(xaml, @"RowDefinition Height=""\*"" MinHeight=""(\d+)""").Groups[1].Value);
        Assert.True(windowMin <= 520, "the window must shrink far enough to reach icon-only nav");
        Assert.True(NavRailLayout.Decide(rowMin, 9).Compact, "the content row must be allowed to shrink into icon mode");
        Assert.False(NavRailLayout.Decide(360, 9).Compact);
        Assert.True(NavRailLayout.Decide(270, 9, currentlyCompact: true).Compact);
        Assert.False(NavRailLayout.Decide(270, 9, currentlyCompact: false).Compact);
        Assert.False(NavRailLayout.Decide(340, 9, currentlyCompact: true).Compact);
    }

    [Fact]
    public void Stacked_cards_stay_stacked_until_there_is_real_room()
    {
        var items = new (DisplayKind Display, ItemSize Size)[] { (DisplayKind.Bar, ItemSize.Wide) };
        var tight = DashboardFlow.Plan(items, 631, arrangeWidth: 0);
        Assert.True(tight.Stacking);
        var barely = DashboardFlow.Plan(items, 640, arrangeWidth: 0, currentlyStacking: true);
        Assert.True(barely.Stacking);
        var roomy = DashboardFlow.Plan(items, 680, arrangeWidth: 0, currentlyStacking: true);
        Assert.False(roomy.Stacking);
        Assert.False(DashboardFlow.Plan(items, 640, arrangeWidth: 0).Stacking);
    }

    [Fact]
    public void Real_connect_path_drops_a_demo_profile_and_identify_rebuilds()
    {
        Assert.True(VehicleSelection.DropDemoProfile(realConnectPath: true, profileLooksDemo: true));
        Assert.False(VehicleSelection.DropDemoProfile(realConnectPath: true, profileLooksDemo: false));
        Assert.False(VehicleSelection.DropDemoProfile(realConnectPath: false, profileLooksDemo: true));
        var again = VehicleSelection.AfterIdentify(" [DEMO] same", "[DEMO] same", vinRead: true, moduleCount: 4);
        Assert.True(again.SaveProfile);
        Assert.True(again.RebuildModules);
        Assert.Equal("[DEMO] same", again.SelectLabel);
        var noVin = VehicleSelection.AfterIdentify("2017 Ford F-150", "ignored", vinRead: false, moduleCount: 3);
        Assert.False(noVin.SaveProfile);
        Assert.True(noVin.RebuildModules);
        Assert.Null(noVin.SelectLabel);
        Assert.False(VehicleSelection.AfterIdentify("2017 Ford F-150", "x", vinRead: false, moduleCount: 0).RebuildModules);

        var vm = File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "MainViewModel.cs"));
        Assert.Contains("VehicleSelection.DropDemoProfile", vm);
        Assert.Contains("VehicleSelection.AfterIdentify", vm);
        Assert.Contains("AsBuiltSupportPolicy", vm);
        Assert.Contains("ModuleBusMemory", vm);
        Assert.Contains("FirmwareCheckPolicy.ShouldQuery", vm);
    }

    [Fact]
    public void Version_is_0_5_1_and_notes_list_the_windows_checks()
    {
        Assert.Contains("<Version>0.5.1</Version>", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.App", "ObdLink.App.csproj")));
        Assert.Contains("<Version>0.5.1</Version>", File.ReadAllText(Path.Combine(Root(), "src", "ObdLink.Core", "ObdLink.Core.csproj")));
        var notes = File.ReadAllText(Path.Combine(Root(), "docs", "RELEASE-NOTES.md"));
        Assert.StartsWith("## v0.5.1", notes.TrimStart());
        Assert.Contains("Move left", notes);
        Assert.Contains("Drag", notes);
        Assert.Contains("## v0.5.0", notes);
        Assert.Contains("DEMO", notes);
        Assert.Contains("TCM", notes);
        Assert.Contains("As-Built", notes);
        Assert.Contains("1E13", notes);
        Assert.Contains("1E12", notes);
        Assert.Contains("UNVERIFIED", notes);
        Assert.Contains("2813", notes);
        Assert.DoesNotContain("v0.5.0-test", notes);
        Assert.DoesNotContain("v0.5.1-test", notes);
        Assert.Contains("## v0.4.0", notes);
        var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
        Assert.Contains("v0.5.0", readme);
        Assert.Contains("v0.5.1", readme);
        Assert.Contains("Windows-tested", readme);
    }
}
