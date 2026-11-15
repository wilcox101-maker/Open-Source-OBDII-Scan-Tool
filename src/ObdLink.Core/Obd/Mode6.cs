using System.Globalization;
using ObdLink.Core.IsoTp;

namespace ObdLink.Core.Obd;

/// <summary>Unit and scaling id (UASID) from SAE J1979 / ISO 15031-5: value = raw x Scale + Offset. 0x80-0xFE are the signed (two's complement) forms.</summary>
public sealed record UasDef(byte Id, bool Signed, double Scale, string Unit, double Offset = 0);

/// <summary>One Mode 6 test: OBDMID, test id, UASID and raw value/limits (2 bytes each).</summary>
public sealed record Mode6Result(uint Ecu, byte Mid, byte Tid, byte Uasid, int Value, int Min, int Max)
{
    /// <summary>Pass/fail compares the raw numbers (signed when the UASID is signed), so it does not depend on the scaling table.</summary>
    public bool Pass => Value >= Min && Value <= Max;
    public UasDef? Uas => Mode6.Uas(Uasid);
    public string MidName => Mode6.MidName(Mid);
    public string TidName => Mode6.TidName(Mid, Tid);
    public string Show(int raw) => Uas is { } u ? Mode6.Format(raw * u.Scale + u.Offset, u.Unit) : $"{raw} (UASID 0x{Uasid:X2})";
    public string Line => $"{TidName}: {Show(Value)}  (limits {Show(Min)} .. {Show(Max)})  {(Pass ? "PASS" : "FAIL")}";
}

/// <summary>
/// v0.9.0 Phase 1: SAE J1979 service 06 (on-board monitoring test results) over CAN. Read-only.
/// Scaling (UASID) and OBDMID names follow SAE J1979 / ISO 15031-5 as commonly published; tests with an unknown UASID are shown raw.
/// [VERIFY] Compare against FORScan's Mode 6 page for the same truck before relying on a scaled value.
/// </summary>
public static class Mode6
{
    private static readonly Dictionary<byte, UasDef> Table = new UasDef[]
    {
        new(0x01, false, 1, ""), new(0x02, false, 0.1, ""), new(0x03, false, 0.01, ""), new(0x04, false, 0.001, ""),
        new(0x05, false, 0.0000305, ""), new(0x06, false, 0.000305, ""),
        new(0x07, false, 0.25, "rpm"), new(0x08, false, 0.01, "km/h"), new(0x09, false, 1, "km/h"),
        new(0x0A, false, 0.122, "mV"), new(0x0B, false, 0.001, "V"), new(0x0C, false, 0.01, "V"),
        new(0x0D, false, 0.00390625, "mA"), new(0x0E, false, 0.001, "A"), new(0x0F, false, 0.01, "A"),
        new(0x10, false, 1, "ms"), new(0x11, false, 100, "ms"), new(0x12, false, 1, "s"),
        new(0x13, false, 1, "mΩ"), new(0x14, false, 1, "Ω"), new(0x15, false, 1, "kΩ"),
        new(0x16, false, 0.1, "°C", -40), new(0x17, false, 0.01, "kPa"), new(0x18, false, 0.0117, "kPa"), new(0x19, false, 0.079, "kPa"),
        new(0x1A, false, 1, "kPa"), new(0x1B, false, 10, "kPa"), new(0x1C, false, 0.01, "°"), new(0x1D, false, 0.5, "°"),
        new(0x1E, false, 0.0000305, ":1"), new(0x1F, false, 0.05, ":1"), new(0x20, false, 0.00390625, ":1"),
        new(0x21, false, 1, "mHz"), new(0x22, false, 1, "Hz"), new(0x23, false, 1, "kHz"),
        new(0x24, false, 1, "counts"), new(0x25, false, 1, "km"), new(0x27, false, 0.01, "g/s"), new(0x28, false, 1, "g/s"),
        new(0x29, false, 0.25, "Pa/s"), new(0x2F, false, 0.01, "%"), new(0x30, false, 0.001526, "%"),
        new(0x81, true, 1, ""), new(0x82, true, 0.1, ""), new(0x83, true, 0.01, ""), new(0x84, true, 0.001, ""),
        new(0x85, true, 0.0000305, ""), new(0x86, true, 0.000305, ""),
        new(0x8A, true, 0.122, "mV"), new(0x8B, true, 0.001, "V"), new(0x8C, true, 0.01, "V"),
        new(0x8D, true, 0.00390625, "mA"), new(0x8E, true, 0.001, "A"), new(0x90, true, 1, "ms"),
        new(0x96, true, 0.1, "°C"), new(0x9C, true, 0.01, "°"), new(0x9D, true, 0.5, "°"),
        new(0xA8, true, 1, "g/s"), new(0xA9, true, 0.25, "Pa/s"), new(0xAF, true, 0.01, "%"), new(0xB0, true, 0.003052, "%"),
        new(0xB1, true, 2, "mV/s"), new(0xFC, true, 0.01, "kPa"), new(0xFD, true, 0.001, "kPa"), new(0xFE, true, 0.25, "Pa"),
    }.ToDictionary(u => u.Id);

    public static UasDef? Uas(byte id) => Table.GetValueOrDefault(id);
    /// <summary>Signedness is defined by the UASID range even when its scaling is not in the table.</summary>
    public static bool IsSigned(byte uasid) => uasid >= 0x80;

    public static string MidName(byte mid) => mid switch
    {
        >= 0x01 and <= 0x10 => $"O2 sensor bank {(mid - 1) / 4 + 1} sensor {(mid - 1) % 4 + 1}",
        >= 0x21 and <= 0x24 => $"Catalyst bank {mid - 0x20}",
        >= 0x31 and <= 0x34 => $"EGR / VVT bank {mid - 0x30}",
        >= 0x35 and <= 0x38 => $"Variable valve timing bank {mid - 0x34}",
        0x39 => "EVAP leak 0.150\"", 0x3A => "EVAP leak 0.090\"", 0x3B => "EVAP leak 0.040\"", 0x3C => "EVAP leak 0.020\"", 0x3D => "Purge flow",
        >= 0x41 and <= 0x50 => $"O2 sensor heater bank {(mid - 0x41) / 4 + 1} sensor {(mid - 0x41) % 4 + 1}",
        >= 0x61 and <= 0x64 => $"Heated catalyst bank {mid - 0x60}",
        >= 0x71 and <= 0x74 => $"Secondary air {mid - 0x70}",
        >= 0x81 and <= 0x84 => $"Fuel system bank {mid - 0x80}",
        0x85 => "Boost pressure bank 1", 0x86 => "Boost pressure bank 2",
        0x90 => "NOx adsorber bank 1", 0x91 => "NOx adsorber bank 2", 0x98 => "NOx catalyst bank 1", 0x99 => "NOx catalyst bank 2",
        0xA1 => "Misfire, general",
        >= 0xA2 and <= 0xAD => $"Misfire cylinder {mid - 0xA1}",
        0xB0 => "PM filter bank 1", 0xB1 => "PM filter bank 2",
        _ => $"Monitor 0x{mid:X2}",
    };

    /// <summary>Standardized test ids (0x01-0x0A for O2 sensors, 0x0B/0x0C for misfire). 0x80+ are manufacturer-defined.</summary>
    public static string TidName(byte mid, byte tid) => (mid, tid) switch
    {
        (>= 0x01 and <= 0x10, 0x01) => "Rich-to-lean threshold voltage",
        (>= 0x01 and <= 0x10, 0x02) => "Lean-to-rich threshold voltage",
        (>= 0x01 and <= 0x10, 0x03) => "Low voltage for switch time",
        (>= 0x01 and <= 0x10, 0x04) => "High voltage for switch time",
        (>= 0x01 and <= 0x10, 0x05) => "Rich-to-lean switch time",
        (>= 0x01 and <= 0x10, 0x06) => "Lean-to-rich switch time",
        (>= 0x01 and <= 0x10, 0x07) => "Minimum sensor voltage",
        (>= 0x01 and <= 0x10, 0x08) => "Maximum sensor voltage",
        (>= 0x01 and <= 0x10, 0x09) => "Time between transitions",
        (>= 0x01 and <= 0x10, 0x0A) => "Sensor period",
        (>= 0xA1 and <= 0xAD, 0x0B) => "Misfire count (average, last 10 drive cycles)",
        (>= 0xA1 and <= 0xAD, 0x0C) => "Misfire count (last / current drive cycle)",
        (_, >= 0x80) => $"Manufacturer test 0x{tid:X2}",
        _ => $"Test 0x{tid:X2}",
    };

    public static string Format(double v, string unit) => (Math.Abs(v) >= 100 || v == Math.Floor(v) ? v.ToString("0.##", CultureInfo.InvariantCulture) : v.ToString("0.####", CultureInfo.InvariantCulture)) + (unit.Length > 0 ? " " + unit : "");

    /// <summary>CAN reply: 46 then repeated 9-byte records [MID TID UASID V V Min Min Max Max]. A short tail is ignored.</summary>
    public static List<Mode6Result> Parse(uint ecu, byte[] p)
    {
        var list = new List<Mode6Result>();
        if (p.Length < 1 || p[0] != 0x46) return list;
        for (int i = 1; i + 9 <= p.Length; i += 9)
        {
            byte mid = p[i], tid = p[i + 1], uas = p[i + 2];
            int R(int o) { int u = p[i + o] << 8 | p[i + o + 1]; return IsSigned(uas) ? (short)u : u; }
            list.Add(new(ecu, mid, tid, uas, R(3), R(5), R(7)));
        }
        return list;
    }

    /// <summary>Reads every supported OBDMID from every ECU that answers (functional 7DF). Only service 06 requests are sent.</summary>
    public static async Task<List<Mode6Result>> ReadAllAsync(IDiagChannel ch, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await ch.SetTargetAsync(ObdService.Functional11, null, ct);
        var supported = new Dictionary<uint, SortedSet<byte>>();
        for (int b = 0x00; b <= 0xE0; b += 0x20)
        {
            bool any = false;
            foreach (var r in await ch.RequestAsync(new byte[] { 0x06, (byte)b }, ct: ct))
            {
                var p = r.Payload;
                if (p.Length < 6 || p[0] != 0x46 || p[1] != b) continue;
                if (!supported.TryGetValue(r.SourceId, out var set)) supported[r.SourceId] = set = new();
                foreach (var mid in J1979.ParseSupported((byte)b, p[2..6])) set.Add(mid);
                any = true;
            }
            if (!any || !supported.Values.Any(s => s.Contains((byte)(b + 0x20)))) break;
        }
        var results = new List<Mode6Result>();
        var mids = supported.Values.SelectMany(s => s).Where(m => m % 0x20 != 0).Distinct().OrderBy(m => m).ToList();
        foreach (var mid in mids)
        {
            progress?.Report($"Mode 6: {MidName(mid)}");
            foreach (var r in await ch.RequestAsync(new byte[] { 0x06, mid }, ct: ct))
                results.AddRange(Parse(r.SourceId, r.Payload).Where(x => x.Mid == mid));
        }
        return results;
    }

    public static string Report(IReadOnlyList<Mode6Result> results)
    {
        if (results.Count == 0) return "No ECU returned Mode 6 test results. Monitors may not have run since the last clear.";
        var sb = new System.Text.StringBuilder();
        int fails = results.Count(r => !r.Pass);
        sb.AppendLine($"{results.Count} test(s), {fails} FAIL.");
        foreach (var g in results.GroupBy(r => (r.Ecu, r.Mid)))
        {
            sb.AppendLine();
            sb.AppendLine($"[ECU 0x{g.Key.Ecu:X3}] {MidName(g.Key.Mid)} (MID {g.Key.Mid:X2})");
            foreach (var r in g) sb.AppendLine("  " + r.Line);
        }
        sb.AppendLine();
        sb.AppendLine("Pass/fail compares the raw value with its limits. Scaled values use the SAE J1979 unit table; unknown units are shown raw.");
        return sb.ToString();
    }
}
