using ObdLink.Core.AsBuilt;
using ObdLink.Core.Logs;

namespace ObdLink.Core.Vehicles;

/// <summary>How one module ID compares with the factory .AB record.</summary>
public enum CalibrationStatus
{
    /// <summary>Installed value equals the factory record.</summary>
    Same,
    /// <summary>Installed value differs from the factory record. Says nothing about which is newer.</summary>
    Differs,
    /// <summary>The module answered but the factory record has no entry for it (or not for this ID).</summary>
    NotInFactoryRecord,
    /// <summary>The factory record lists the module, but it wasn't read (didn't answer, or Identify vehicle didn't reach it).</summary>
    NotRead,
}

/// <summary>One module ID (F188 / F111 / F113) checked against the factory record.</summary>
public sealed record CalibrationFinding(ushort Module, string Acronym, string Did, string Installed, string Factory, CalibrationStatus Status)
{
    public string Label => CalibrationCheck.LabelOf(Did);

    /// <summary>Plain text for the vehicle page and the log. A difference is reported as a difference, never as "newer".</summary>
    public string Line => Status switch
    {
        CalibrationStatus.Same => $"{Acronym} (0x{Module:X3}): {Label} ({Did}) matches the factory record ({Installed}).",
        CalibrationStatus.Differs => $"{Acronym} (0x{Module:X3}): installed {Label} ({Did}) differs from the factory record (installed {Installed}, record {CalibrationCheck.Short(Factory, Installed)}).",
        CalibrationStatus.NotInFactoryRecord => $"{Acronym} (0x{Module:X3}): {Label} ({Did}) {Installed} is not in the factory record.",
        _ => $"{Acronym} (0x{Module:X3}): not read; the factory record has {Label} ({Did}) {Factory}.",
    };
}

/// <summary>
/// W5 v1 (scope doc): offline check of each module's software / assembly IDs against the vehicle's factory As-Built record
/// (Motorcraft <c>.AB</c>, its NODEID entries). Reads nothing from the network: no "latest known calibration" (that's v2, blocked on
/// a redistributable source). It reports <b>differences</b> only. It never claims which side is newer: on Dale's 2017 F-150 the
/// factory record (PCM HL3A-14C204-BDC) differs from the truck (BDB), and why is unknown (docs/REAL-DATA-FINDINGS.md: UNVERIFIED).
/// </summary>
public static class CalibrationCheck
{
    /// <summary>The IDs compared. Serials (F18C) and the VIN are never compared.</summary>
    public static readonly IReadOnlyList<string> Dids = new[] { "F188", "F111", "F113" };

    public static string LabelOf(string did) => did switch
    {
        "F188" => "software / strategy",
        "F111" => "core assembly",
        "F113" => "assembly part number",
        _ => did,
    };

    /// <summary>"HL3A-14C204-BDC" against "HL3A-14C204-BDB" → "BDC" (same base part, so only the suffix); otherwise the full value.</summary>
    public static string Short(string value, string other)
    {
        int a = value.LastIndexOf('-'), b = other.LastIndexOf('-');
        return a > 0 && b > 0 && value[..a] == other[..b] ? value[(a + 1)..] : value;
    }

    /// <summary>Compares installed module IDs (from Identify vehicle) with the factory record.</summary>
    public static IReadOnlyList<CalibrationFinding> Compare(IEnumerable<ModuleIdentity> installed, FordAbFile factory)
    {
        var res = new List<CalibrationFinding>();
        var seen = new HashSet<ushort>();
        foreach (var m in installed.OrderBy(m => m.TxId))
        {
            var tx = (ushort)m.TxId;
            seen.Add(tx);
            factory.Nodes.TryGetValue(tx, out var rec);
            foreach (var did in Dids)
            {
                if (!m.Dids.TryGetValue(did, out var cur) || cur.Length == 0) continue;
                if (rec is null || !rec.TryGetValue(did, out var fac) || fac.Length == 0)
                    res.Add(new(tx, m.Acronym, did, cur, "", CalibrationStatus.NotInFactoryRecord));
                else
                    res.Add(new(tx, m.Acronym, did, cur, fac, string.Equals(cur.Trim(), fac.Trim(), StringComparison.OrdinalIgnoreCase) ? CalibrationStatus.Same : CalibrationStatus.Differs));
            }
        }
        foreach (var (tx, rec) in factory.Nodes.Where(n => !seen.Contains(n.Key)))
            if (rec.TryGetValue("F188", out var fac) && fac.Length > 0)
                res.Add(new(tx, $"0x{tx:X3}", "F188", "", fac, CalibrationStatus.NotRead));
        return res;
    }

    /// <summary>The same check from a FORScan vehicle profile (modules mapped to the .AB by part number and serial).</summary>
    public static IReadOnlyList<CalibrationFinding> Compare(ForscanProfile profile, FordAbFile factory)
    {
        var map = profile.MapToAb(factory);
        var mods = profile.Modules.Where(m => map.ContainsKey(m.Key))
            .Select(m => new ModuleIdentity(m.Key, m.Key, "", map[m.Key], 0, true, m.Dids, false));
        return Compare(mods, factory).Where(f => f.Status != CalibrationStatus.NotRead).ToList();
    }

    /// <summary>Summary + one line per difference / missing entry (matches are counted, not listed).</summary>
    public static string Report(IReadOnlyList<CalibrationFinding> findings)
    {
        int same = findings.Count(f => f.Status == CalibrationStatus.Same), diff = findings.Count(f => f.Status == CalibrationStatus.Differs);
        var head = $"Software vs factory record (offline): {same} match, {diff} differ"
            + (findings.Any(f => f.Status == CalibrationStatus.NotInFactoryRecord) ? $", {findings.Count(f => f.Status == CalibrationStatus.NotInFactoryRecord)} not in the record" : "")
            + (findings.Any(f => f.Status == CalibrationStatus.NotRead) ? $", {findings.Count(f => f.Status == CalibrationStatus.NotRead)} module(s) in the record not read" : "") + ".";
        var lines = findings.Where(f => f.Status != CalibrationStatus.Same).Select(f => "  " + f.Line);
        var note = diff > 0 ? new[] { "  A difference doesn't show which version came later, or whether anything needs updating; check with Ford service information." } : Array.Empty<string>();
        return string.Join("\n", new[] { head }.Concat(lines).Concat(note));
    }
}
