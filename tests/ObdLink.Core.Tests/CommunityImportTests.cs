using ObdLink.Core.AsBuilt;
using ObdLink.Core.Modules;
using Xunit;

namespace ObdLink.Core.Tests;

public class CommunityImportTests
{
    private static string Repo([System.Runtime.CompilerServices.CallerFilePath] string f = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(f)!, "..", ".."));

    private static string Db(string rel) => Path.Combine(Repo(), "community-db", rel);

    [Fact]
    public void Livnitup_tutorial_entries_load_and_cross_reference()
    {
        var tut = CommunityDb.LoadFeatures(Db("livnitup-forscan-tutorial/features.json"));
        var old = CommunityDb.LoadFeatures(Db("f150-asbuilt/features.json"));
        Assert.Equal(5, tut.Count);
        var r = CommunityDb.Merge(old, tut);
        var honk = Assert.Single(r, x => x.Incoming.Address == "726-41-02");
        Assert.Equal(MergeStatus.Merged, honk.Status);
        Assert.Contains(r, x => x.Incoming.Address == "741-03-01" && x.Status == MergeStatus.New);
        Assert.DoesNotContain(r, x => x.Status == MergeStatus.Conflict);
    }

    [Fact]
    public void Merge_flags_conflicting_value()
    {
        var ex = new[] { new CommunityFeature("a", "726-41-02", new[] { "xxx*", "xxxx" }, "Double honk", new[] { "0=Disabled, 1=Enabled" }) };
        var inc = new[] { new CommunityFeature("b", "726-41-02", new[] { "xxx7", "xxxx" }, "?", Array.Empty<string>()) };
        Assert.Equal(MergeStatus.Conflict, CommunityDb.Merge(ex, inc).Single().Status);
    }

    [Fact]
    public void F150_2021plus_folder_is_separate_and_flagged()
    {
        var p = Db("f150-2021plus-asbuilt/features.json");
        Assert.True(CommunityDb.LoadFeatures(p).Count > 1000);
        Assert.Contains("\"applies_to_dale_2017\":false", File.ReadAllText(p));
    }

    [Fact]
    public void Module_acronyms_give_full_names_without_touching_addresses()
    {
        Assert.True(ModuleAcronyms.All.Count >= 70);
        Assert.Equal("Accessory Protocol Interface Module", ModuleAcronyms.Find("APIM")!.Name);
        Assert.Equal("BCM – Body Control Module", ModuleAcronyms.Label("BCM", "Body Control Module"));
        Assert.Contains("Community acronym list", ModuleAcronyms.Tooltip("ABS", "Anti-lock Brake System"));
        Assert.Equal("ZZZ", ModuleAcronyms.Label("ZZZ"));
        Assert.Equal(0x726u, FordF150_2017.Modules.Single(m => m.Acronym == "BCM").TxId);
    }
}
