namespace ObdLink.Core.AsBuilt;

/// <summary>How sure a part-number → tab rule is. <see cref="Synthesized"/> = lifted from this repo's own notes (one truck), not from a cited source.</summary>
public enum TabRuleConfidence { Synthesized, Low, Medium, High }

/// <summary>
/// One part-number → community-sheet tab rule. Matches when the module's <paramref name="Did"/> value (null = F188 or F111)
/// starts with <paramref name="Prefix"/> (case, spaces and dashes ignored).
/// </summary>
public sealed record TabRule(ushort Module, string ModuleName, string? Did, string Prefix, string Tab, TabRuleConfidence Confidence, string Source);

/// <summary>Why a tab was chosen.</summary>
public enum TabChoiceReason
{
    /// <summary>The sheet has no tab for the lines in the grid.</summary>
    None,
    /// <summary>The user picked it; kept on Refresh while the tab still applies.</summary>
    Manual,
    /// <summary>A part-number rule matched the module's F188/F111.</summary>
    PartNumber,
    /// <summary>No rule matched; fell back to the 2017 F-150 profile map. Must be confirmed by the user.</summary>
    DefaultedProfile,
    /// <summary>No rule matched and no profile entry; fell back to the first tab. Must be confirmed by the user.</summary>
    DefaultedFirst,
}

/// <summary>The chosen tab and a one-line reason for the Info text.</summary>
public sealed record TabChoice(string? Tab, TabChoiceReason Reason, string Why, TabRule? Rule = null)
{
    /// <summary>True when the tab was not justified by a part number or the user: the UI must ask the user to confirm it.</summary>
    public bool NeedsConfirm => Reason is TabChoiceReason.DefaultedProfile or TabChoiceReason.DefaultedFirst;
}

/// <summary>
/// W4: picks the community-sheet tab for a module from its part numbers (F188 software, F111 hardware) instead of a
/// hard-coded per-vehicle map. Order: the user's own pick (sticky) → a part-number rule → <see cref="DecodeProfile.DaleF150_2017"/>
/// → the first tab. Read-only and offline: it only looks at DIDs already read by Identify vehicle.
/// It never silently commits to a tab it can't justify: the defaulted paths say so (<see cref="TabChoice.NeedsConfirm"/>).
/// </summary>
public static class DecodeTabResolver
{
    /// <summary>DIDs checked, in this order.</summary>
    public static readonly IReadOnlyList<string> Dids = new[] { "F188", "F111" };

    private const string DecodeProfileNote = "src/ObdLink.Core/AsBuilt/DecodeProfile.cs (owner's 2017 F-150, factory .ab part numbers)";

    /// <summary>
    /// Built-in rules. Until KB's cited table lands (claude lanes "SPE → KB", 2026-11-16), only what DecodeProfile's own
    /// comments assert, matched on the exact part bases of one truck, marked <see cref="TabRuleConfidence.Synthesized"/>.
    /// </summary>
    public static readonly IReadOnlyList<TabRule> Rules = new TabRule[]
    {
        // The factory .ab lists these part bases for the module; it doesn't say which DID holds which, so either DID matches.
        new(0x7D0, "APIM", null, "E1BT-14D205", "APIM Sync 1", TabRuleConfidence.Synthesized, DecodeProfileNote + ": APIM = SYNC 1 (confirmed by owner 2026-10-23)"),
        new(0x7D0, "APIM", null, "FL3T-14D212", "APIM Sync 1", TabRuleConfidence.Synthesized, DecodeProfileNote + ": APIM = SYNC 1 (confirmed by owner 2026-10-23)"),
        new(0x726, "BCM", null, "HU5T", "BCM (BdyCM)-old", TabRuleConfidence.Synthesized, DecodeProfileNote + ": BCM HU5T → 'BCM (BdyCM)-old'"),
        new(0x760, "ABS", null, "HL34-2C219", "ABS (2015-2017)", TabRuleConfidence.Synthesized, DecodeProfileNote + ": ABS tab = 2015-2017"),
        new(0x760, "ABS", null, "HL34-2D053", "ABS (2015-2017)", TabRuleConfidence.Synthesized, DecodeProfileNote + ": ABS tab = 2015-2017"),
    };

    /// <summary>Upper-case letters and digits only, so "e1bt-14d205-aj", "E1BT 14D205 AJ" and "E1BT14D205AJ" compare equal.</summary>
    public static string Normalize(string? s) => s is null ? "" : new string(s.Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// Picks the tab for <paramref name="module"/>. <paramref name="tabs"/> = tabs that have options for the grid's lines.
    /// <paramref name="dids"/> = that module's DIDs from Identify vehicle (null if not identified).
    /// <paramref name="manual"/> = the tab the user picked for this module earlier (null if none).
    /// </summary>
    public static TabChoice Pick(ushort module, IReadOnlyCollection<string> tabs, IReadOnlyDictionary<string, string>? dids,
        string? manual = null, IReadOnlyList<TabRule>? rules = null, DecodeProfile? fallback = null)
    {
        if (tabs.Count == 0) return new(null, TabChoiceReason.None, "the community sheet has no tab for these lines");
        if (manual is not null && tabs.Contains(manual)) return new(manual, TabChoiceReason.Manual, $"tab '{manual}' picked by you");

        rules ??= Rules;
        if (dids is not null)
            foreach (var did in Dids)
            {
                if (!dids.TryGetValue(did, out var value)) continue;
                var v = Normalize(value);
                if (v.Length == 0) continue;
                var rule = rules.FirstOrDefault(r => r.Module == module && (r.Did is null || r.Did == did) && tabs.Contains(r.Tab)
                                                      && Normalize(r.Prefix) is { Length: > 0 } p && v.StartsWith(p, StringComparison.Ordinal));
                if (rule is not null)
                    return new(rule.Tab, TabChoiceReason.PartNumber,
                        $"tab chosen from {rule.ModuleName} {did}={value.Trim()} ({ConfidenceText(rule.Confidence)})", rule);
            }

        var why = dids is null ? "module not identified (run Identify vehicle)" : "no part-number match";
        fallback ??= DecodeProfile.DaleF150_2017;
        if (fallback.TabByModule.TryGetValue(module, out var pt) && tabs.Contains(pt))
            return new(pt, TabChoiceReason.DefaultedProfile, $"{why} — defaulted to '{pt}' (from the {fallback.Name} profile), confirm it's right");
        var first = tabs.First();
        return new(first, TabChoiceReason.DefaultedFirst, $"{why} — defaulted to '{first}' (first tab), confirm it's right");
    }

    public static string ConfidenceText(TabRuleConfidence c) => c switch
    {
        TabRuleConfidence.Synthesized => "Synthesized from this project's notes, low confidence: confirm it's right",
        TabRuleConfidence.Low => "low confidence",
        TabRuleConfidence.Medium => "medium confidence",
        _ => "high confidence",
    };
}

/// <summary>W4 per-option state shown in the Options grid.</summary>
public enum OptionState
{
    /// <summary>The grid has no line for this address (block not read / not imported).</summary>
    NotRead,
    /// <summary>The line is there but too short for this option's digits.</summary>
    TooShort,
    /// <summary>Read; no new value.</summary>
    Read,
    /// <summary>Read and a new value is pending (not yet applied to the grid).</summary>
    Changed,
}

public static class OptionStates
{
    public static OptionState Of(bool hasLine, string? currentDigits, string? newValue) =>
        !hasLine ? OptionState.NotRead
        : currentDigits is null ? OptionState.TooShort
        : !string.IsNullOrEmpty(newValue) && !string.Equals(newValue, currentDigits, StringComparison.OrdinalIgnoreCase) ? OptionState.Changed
        : OptionState.Read;

    public static string Text(OptionState s) => s switch
    {
        OptionState.NotRead => "not read",
        OptionState.TooShort => "n/a (line too short)",
        OptionState.Changed => "changed",
        _ => "read",
    };
}
