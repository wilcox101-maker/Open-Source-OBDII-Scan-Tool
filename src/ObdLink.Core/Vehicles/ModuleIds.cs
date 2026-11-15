namespace ObdLink.Core.Vehicles;

/// <summary>v0.9.0: one row of the module identification grid (the DIDs Identify vehicle read).</summary>
public sealed record ModuleIdRow(string Bus, string Address, string Module, string Name, string Software, string CoreAssembly, string Assembly, string Calibration, string Serial, string AsBuilt);

public static class ModuleIds
{
    public static List<ModuleIdRow> FromProfile(VehicleProfile p)
    {
        static string G(ModuleIdentity m, string k) => m.Dids is not null && m.Dids.TryGetValue(k, out var v) ? v : "";
        return p.Modules.OrderBy(m => m.Bus, StringComparer.Ordinal).ThenBy(m => m.TxId)
            .Select(m => new ModuleIdRow(m.Bus, $"{m.TxId:X3}", m.Acronym, m.Name, G(m, "F188"), G(m, "F111"), G(m, "F113"), G(m, "F124"), G(m, "F18C"), m.HasAsBuiltDe00 ? "yes" : ""))
            .ToList();
    }

    /// <summary>CSV (RFC 4180 quoting). Serial numbers become REDACTED when <paramref name="redactSerials"/> is set.</summary>
    public static string Csv(IEnumerable<ModuleIdRow> rows, bool redactSerials)
    {
        static string Q(string v) => v.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        var sb = new System.Text.StringBuilder("Bus,Address,Module,Name,Software (F188),Core assembly (F111),Assembly (F113),Calibration (F124),Serial (F18C),As-Built\r\n");
        foreach (var r in rows)
            sb.Append(string.Join(",", new[] { r.Bus, r.Address, r.Module, r.Name, r.Software, r.CoreAssembly, r.Assembly, r.Calibration, redactSerials && r.Serial.Length > 0 ? "REDACTED" : r.Serial, r.AsBuilt }.Select(Q))).Append("\r\n");
        return sb.ToString();
    }
}
