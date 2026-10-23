using System.Text;
using ObdLink.Core.IsoTp;

namespace ObdLink.Core.Obd;

public static class DtcFormat
{
    /// <summary>SAE J2012 / J1979 two-byte DTC encoding: bits 15-14 letter (P,C,B,U), then 4 hex digits.</summary>
    public static ushort ToTwoBytes(string code) => (ushort)("PCBU".IndexOf(char.ToUpperInvariant(code[0])) << 14 | Convert.ToUInt16(code[1..5], 16));
    public static string FromTwoBytes(ushort v) => $"{"PCBU"[v >> 14]}{(v >> 12) & 0x3:X}{v & 0x0FFF:X3}";
}

public sealed record PidValue(byte Pid, string Name, double? Value, string Unit, byte[] Raw);

/// <summary>SAE J1979 services 01, 03, 04, 09 over CAN (functional 0x7DF).</summary>
public sealed class ObdService
{
    public const uint Functional11 = 0x7DF;
    private readonly IDiagChannel _ch;
    public ObdService(IDiagChannel ch) => _ch = ch;

    private async Task<IReadOnlyList<DiagResponse>> Req(params byte[] req)
    {
        await _ch.SetTargetAsync(Functional11, null);
        return await _ch.RequestAsync(req);
    }

    public async Task<HashSet<byte>> GetSupportedPidsAsync()
    {
        var set = new HashSet<byte>();
        for (byte b = 0x00; b <= 0xC0; b += 0x20)
        {
            var r = (await Req(0x01, b)).FirstOrDefault(x => x.Payload.Length >= 6 && x.Payload[0] == 0x41);
            if (r is null) break;
            uint mask = (uint)(r.Payload[2] << 24 | r.Payload[3] << 16 | r.Payload[4] << 8 | r.Payload[5]);
            for (int i = 0; i < 32; i++) if ((mask & (1u << (31 - i))) != 0) set.Add((byte)(b + i + 1));
            if (!set.Contains((byte)(b + 0x20))) break;
        }
        return set;
    }

    public async Task<PidValue?> ReadPidAsync(byte pid)
    {
        var r = (await Req(0x01, pid)).FirstOrDefault(x => x.Payload.Length >= 3 && x.Payload[0] == 0x41 && x.Payload[1] == pid);
        return r is null ? null : DecodePid(pid, r.Payload[2..]);
    }

    /// <summary>Standard J1979 formulas for common PIDs (as documented e.g. on Wikipedia "OBD-II PIDs").</summary>
    public static PidValue DecodePid(byte pid, byte[] d) => pid switch
    {
        0x04 => new(pid, "Calculated engine load", d[0] * 100.0 / 255, "%", d),
        0x05 => new(pid, "Coolant temperature", d[0] - 40, "°C", d),
        0x0C => new(pid, "Engine RPM", (d[0] * 256 + d[1]) / 4.0, "rpm", d),
        0x0D => new(pid, "Vehicle speed", d[0], "km/h", d),
        0x0F => new(pid, "Intake air temperature", d[0] - 40, "°C", d),
        0x11 => new(pid, "Throttle position", d[0] * 100.0 / 255, "%", d),
        0x2F => new(pid, "Fuel level", d[0] * 100.0 / 255, "%", d),
        0x42 => new(pid, "Control module voltage", (d[0] * 256 + d[1]) / 1000.0, "V", d),
        _ => new(pid, $"PID 0x{pid:X2}", null, "", d),
    };

    public async Task<List<(uint Ecu, string Code)>> ReadStoredDtcsAsync()
    {
        var list = new List<(uint, string)>();
        foreach (var r in await Req(0x03))
            list.AddRange(ParseMode03(r.Payload).Select(c => (r.SourceId, c)));
        return list;
    }

    /// <summary>CAN mode 03 response: 43 [count] then 2-byte DTCs.</summary>
    public static IEnumerable<string> ParseMode03(byte[] p)
    {
        if (p.Length < 2 || p[0] != 0x43) yield break;
        for (int i = 2; i + 1 < p.Length; i += 2)
        {
            ushort v = (ushort)(p[i] << 8 | p[i + 1]);
            if (v != 0) yield return DtcFormat.FromTwoBytes(v);
        }
    }

    /// <summary>Mode 04: clears emission DTCs/freeze frame. Caller must confirm via SafetyGuard.</summary>
    public async Task<bool> ClearDtcsAsync() => (await Req(0x04)).Any(r => r.Payload.Length > 0 && r.Payload[0] == 0x44);

    public async Task<string?> ReadVinAsync()
    {
        var r = (await Req(0x09, 0x02)).FirstOrDefault(x => x.Payload.Length >= 3 && x.Payload[0] == 0x49);
        return r is null ? null : ParseVin(r.Payload);
    }

    /// <summary>49 02 [count=01] + 17 ASCII chars (CAN).</summary>
    public static string ParseVin(byte[] p)
    {
        var data = p.Skip(3).Where(b => b >= 0x20 && b < 0x7F).ToArray();
        return Encoding.ASCII.GetString(data).Trim();
    }
}
