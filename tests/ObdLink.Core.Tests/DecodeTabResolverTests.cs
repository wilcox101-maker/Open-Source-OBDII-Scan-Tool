using ObdLink.Core.AsBuilt;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// W4: the Options sheet tab comes from the module's F188/F111 part number; the user's pick is sticky; when nothing matches it
/// defaults and says "confirm it's right". Built-in rules are only what DecodeProfile already asserted, marked Synthesized.
/// </summary>
public class DecodeTabResolverTests
{
    static readonly string[] ApimTabs = { "APIM Sync 1", "APIM Sync 2", "APIM Sync 3", "APIM-old" };
    static readonly string[] BcmTabs = { "BCM (BdyCM)-old", "BCM (JU5T)", "Headlights-Draft" };
    static readonly string[] AbsTabs = { "ABS (2015-2017)", "ABS (2018-2019)" };
    static Dictionary<string, string> D(params (string k, string v)[] kv) => kv.ToDictionary(x => x.k, x => x.v);

    [Theory]
    [InlineData((ushort)0x7D0, "F188", "E1BT-14D205-AJ", "APIM Sync 1")]
    [InlineData((ushort)0x7D0, "F111", "FL3T-14D212-RB", "APIM Sync 1")]
    [InlineData((ushort)0x726, "F188", "HU5T-14C184-AAJ", "BCM (BdyCM)-old")]
    [InlineData((ushort)0x726, "F111", "HU5T-14B476-DAJ", "BCM (BdyCM)-old")]
    [InlineData((ushort)0x760, "F188", "HL34-2C219-AG", "ABS (2015-2017)")]
    [InlineData((ushort)0x760, "F111", "HL34-2D053-AG", "ABS (2015-2017)")]
    public void Known_part_number_picks_the_tab_and_says_why(ushort module, string did, string part, string tab)
    {
        var tabs = module switch { 0x7D0 => ApimTabs, 0x726 => BcmTabs, _ => AbsTabs };
        // Put the expected tab last so "first tab" can't pass by accident.
        var ordered = tabs.Where(t => t != tab).Append(tab).ToList();
        var c = DecodeTabResolver.Pick(module, ordered, D((did, part)), fallback: new DecodeProfile("empty", new Dictionary<ushort, string>()));
        Assert.Equal(tab, c.Tab);
        Assert.Equal(TabChoiceReason.PartNumber, c.Reason);
        Assert.False(c.NeedsConfirm);
        Assert.Contains($"{did}={part}", c.Why);
        Assert.StartsWith("tab chosen from ", c.Why);
        Assert.Equal(TabRuleConfidence.Synthesized, c.Rule!.Confidence);
        Assert.Contains("confirm it's right", c.Why); // Synthesized rules still ask
    }

    [Fact]
    public void F188_is_checked_before_F111()
    {
        var rules = new[]
        {
            new TabRule(0x7D0, "APIM", "F111", "AAAA", "APIM Sync 2", TabRuleConfidence.High, "https://example.invalid/a"),
            new TabRule(0x7D0, "APIM", "F188", "BBBB", "APIM Sync 3", TabRuleConfidence.High, "https://example.invalid/b"),
        };
        var c = DecodeTabResolver.Pick(0x7D0, ApimTabs, D(("F111", "AAAA-1"), ("F188", "BBBB-2")), rules: rules);
        Assert.Equal("APIM Sync 3", c.Tab);
        Assert.Contains("F188=BBBB-2", c.Why);
        Assert.Contains("high confidence", c.Why);
    }

    [Theory]
    [InlineData("e1bt-14d205-aj")]
    [InlineData("E1BT 14D205 AJ")]
    [InlineData("E1BT14D205AJ\0\0")]
    [InlineData("  E1BT-14D205-AJ  ")]
    public void Part_numbers_match_ignoring_case_spaces_dashes(string raw)
    {
        var c = DecodeTabResolver.Pick(0x7D0, ApimTabs, D(("F188", raw)));
        Assert.Equal("APIM Sync 1", c.Tab);
        Assert.Equal(TabChoiceReason.PartNumber, c.Reason);
    }

    [Fact]
    public void Rule_for_another_module_does_not_match()
    {
        // HU5T is a BCM rule: on the APIM it must not pick a BCM tab or claim a match.
        var c = DecodeTabResolver.Pick(0x7D0, ApimTabs, D(("F188", "HU5T-14C184-AAJ")));
        Assert.NotEqual(TabChoiceReason.PartNumber, c.Reason);
        // Even when a grid holds two modules (both tabs offered), the BCM rule must not fire for the APIM.
        var mixed = DecodeTabResolver.Pick(0x7D0, ApimTabs.Concat(BcmTabs).ToList(), D(("F188", "HU5T-14C184-AAJ")));
        Assert.NotEqual("BCM (BdyCM)-old", mixed.Tab);
        Assert.NotEqual(TabChoiceReason.PartNumber, mixed.Reason);
    }

    [Fact]
    public void Rule_whose_tab_is_not_offered_is_skipped()
    {
        var c = DecodeTabResolver.Pick(0x7D0, new[] { "APIM Sync 3" }, D(("F188", "E1BT-14D205-AJ")));
        Assert.Equal("APIM Sync 3", c.Tab);
        Assert.Equal(TabChoiceReason.DefaultedFirst, c.Reason);
    }

    [Fact]
    public void Unknown_part_number_defaults_to_profile_map_and_asks_to_confirm()
    {
        var c = DecodeTabResolver.Pick(0x7D0, ApimTabs, D(("F188", "ZZZZ-14D205-XX")));
        Assert.Equal("APIM Sync 1", c.Tab); // DaleF150_2017 map
        Assert.Equal(TabChoiceReason.DefaultedProfile, c.Reason);
        Assert.True(c.NeedsConfirm);
        Assert.Equal("no part-number match — defaulted to 'APIM Sync 1' (from the Dale 2017 F-150 (SYNC 1) profile), confirm it's right", c.Why);
    }

    [Fact]
    public void Unknown_module_defaults_to_first_tab_and_asks_to_confirm()
    {
        var c = DecodeTabResolver.Pick(0x720, new[] { "IPC", "IPC-old" }, D(("F188", "XXXX")));
        Assert.Equal("IPC", c.Tab);
        Assert.Equal(TabChoiceReason.DefaultedFirst, c.Reason);
        Assert.True(c.NeedsConfirm);
        Assert.Contains("no part-number match — defaulted to 'IPC' (first tab), confirm it's right", c.Why);
    }

    [Fact]
    public void Not_identified_says_so()
    {
        var c = DecodeTabResolver.Pick(0x7D0, ApimTabs, null);
        Assert.True(c.NeedsConfirm);
        Assert.StartsWith("module not identified (run Identify vehicle) — defaulted to", c.Why);
    }

    [Fact]
    public void Demo_part_numbers_never_match()
    {
        var c = DecodeTabResolver.Pick(0x726, BcmTabs, D(("F188", "DEMO-14C184-AA"), ("F111", "DEMO")));
        Assert.True(c.NeedsConfirm);
    }

    [Fact]
    public void Manual_pick_is_sticky_over_a_part_number_match()
    {
        var c = DecodeTabResolver.Pick(0x7D0, ApimTabs, D(("F188", "E1BT-14D205-AJ")), manual: "APIM Sync 3");
        Assert.Equal("APIM Sync 3", c.Tab);
        Assert.Equal(TabChoiceReason.Manual, c.Reason);
        Assert.False(c.NeedsConfirm);
    }

    [Fact]
    public void Manual_pick_no_longer_offered_falls_through()
    {
        var c = DecodeTabResolver.Pick(0x7D0, ApimTabs, D(("F188", "E1BT-14D205-AJ")), manual: "BCM (JU5T)");
        Assert.Equal("APIM Sync 1", c.Tab);
        Assert.Equal(TabChoiceReason.PartNumber, c.Reason);
    }

    [Fact]
    public void No_tabs_means_no_choice()
    {
        var c = DecodeTabResolver.Pick(0x7D0, Array.Empty<string>(), D(("F188", "E1BT-14D205-AJ")));
        Assert.Null(c.Tab);
        Assert.Equal(TabChoiceReason.None, c.Reason);
    }

    [Fact]
    public void Every_builtin_rule_names_a_real_sheet_tab_for_its_module_and_has_a_source()
    {
        var sheet = FeatureOption.LoadF150_2015_2020();
        foreach (var r in DecodeTabResolver.Rules)
        {
            Assert.Contains(sheet, o => o.Tab == r.Tab && o.Address.StartsWith(r.Module.ToString("X3"), StringComparison.OrdinalIgnoreCase));
            Assert.False(string.IsNullOrWhiteSpace(r.Source));
            Assert.True(r.Did is null || DecodeTabResolver.Dids.Contains(r.Did));
            Assert.NotEmpty(DecodeTabResolver.Normalize(r.Prefix));
        }
    }

    [Fact]
    public void Builtin_rules_are_all_synthesized_until_KB_supplies_cited_rows()
    {
        // When KB's cited table lands, rows carry Low/Medium/High and an https source; this test then changes on purpose.
        Assert.All(DecodeTabResolver.Rules, r => Assert.Equal(TabRuleConfidence.Synthesized, r.Confidence));
    }

    [Fact]
    public void Builtin_rules_agree_with_the_DecodeProfile_map()
    {
        foreach (var r in DecodeTabResolver.Rules)
            Assert.Equal(DecodeProfile.DaleF150_2017.TabByModule[r.Module], r.Tab);
    }

    [Theory]
    [InlineData(false, null, null, OptionState.NotRead, "not read")]
    [InlineData(false, null, "1", OptionState.NotRead, "not read")]
    [InlineData(true, null, "1", OptionState.TooShort, "n/a (line too short)")]
    [InlineData(true, "0", "0", OptionState.Read, "read")]
    [InlineData(true, "0a", "0A", OptionState.Read, "read")]
    [InlineData(true, "0", "", OptionState.Read, "read")]
    [InlineData(true, "0", "1", OptionState.Changed, "changed")]
    public void Option_state_markers(bool hasLine, string? current, string? newValue, OptionState state, string text)
    {
        var s = OptionStates.Of(hasLine, current, newValue);
        Assert.Equal(state, s);
        Assert.Equal(text, OptionStates.Text(s));
    }
}
