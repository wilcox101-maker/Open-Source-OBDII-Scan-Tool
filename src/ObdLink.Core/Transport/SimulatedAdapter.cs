namespace ObdLink.Core.Transport;

/// <summary>
/// Scripted STN-like adapter + fake ECUs for tests and the UI "Demo" connection.
/// All ECU data here is SYNTHETIC test data, not captured from a real vehicle.
/// </summary>
public sealed class SimulatedAdapter
{
    public double Voltage { get; set; } = 12.6;
    public string Vin { get; set; } = "TESTVIN0000000001"; // synthetic
    public Dictionary<uint, Dictionary<ushort, byte[]>> Dids { get; } = new();
    public HashSet<uint> PresentModules { get; } = new() { 0x7E0, 0x726, 0x720 };
    public bool CorruptWrites { get; set; }
    public string? Protocol { get; private set; }
    private uint _header = 0x7DF;
    private bool _caf = true;
    private readonly IsoTp.IsoTpReassembler _rxAsm = new();

    public MockTransport CreateTransport() => new(Respond);

    public string? Respond(string cmd)
    {
        var c = cmd.Replace(" ", "").ToUpperInvariant();
        if (c == "ATZ") return "ELM327 v1.4b";
        if (c is "ATE0" or "ATL0" or "ATS0" or "ATH1" or "ATCRA" or "STPO" or "STPC") return "OK";
        if (c == "ATI") return "ELM327 v1.4b";
        if (c == "STI") return "STN2255 v5.10.3 (SIMULATED)";
        if (c == "STDI") return "OBDLink MX+ (SIMULATED)";
        if (c == "ATRV") return $"{Voltage:0.0}V".Replace(',', '.');
        if (c.StartsWith("STP")) { Protocol = c[3..]; return "OK"; }
        if (c.StartsWith("ATSH")) { _header = Convert.ToUInt32(c[4..], 16); return "OK"; }
        if (c.StartsWith("ATCRA")) return "OK";
        if (c == "ATCAF0") { _caf = false; return "OK"; }
        if (c == "ATCAF1") { _caf = true; return "OK"; }
        if (c.StartsWith("AT") || c.StartsWith("ST")) return "?";
        byte[] req;
        try { req = Convert.FromHexString(c); } catch { return "?"; }
        if (!_caf)
        {   // raw frame mode: host performs ISO-TP
            var type = req[0] >> 4;
            var msg = _rxAsm.Feed(req);
            if (type == 1) return (_header + 8).ToString("X3") + "300000";
            if (msg is null) return "NO DATA";
            return Uds(_header, msg, padded: true);
        }
        return _header == 0x7DF ? Obd(req) : Uds(_header, req);
    }

    private string Obd(byte[] r) => (r[0], r.Length > 1 ? r[1] : (byte)0) switch
    {
        (0x01, 0x00) => "7E8064100BE3EA813",
        (0x01, 0x0C) => "7E804410C1AF8",
        (0x01, 0x05) => "7E803410582",
        (0x03, _) => "7E806430203010101",     // 43 02 | 0301 0101 -> P0301, P0101 (synthetic)
        (0x04, _) => "7E80144",
        (0x09, 0x02) => Multi(0x7E8, new byte[] { 0x49, 0x02, 0x01 }.Concat(System.Text.Encoding.ASCII.GetBytes(Vin)).ToArray()),
        _ => "NO DATA",
    };

    private string Uds(uint tx, byte[] r, bool padded = false)
    {
        if (!PresentModules.Contains(tx)) return "NO DATA";
        uint rx = tx + 8;
        byte[] resp = r[0] switch
        {
            0x3E => new byte[] { 0x7E, 0x00 },
            0x10 => new byte[] { 0x50, r[1], 0x00, 0x32, 0x01, 0xF4 },
            0x11 => new byte[] { 0x51, r[1] },
            0x14 => new byte[] { 0x54 },
            0x19 => new byte[] { 0x59, 0x02, 0xFF, 0x91, 0x23, 0x45, 0x2F },
            0x27 => r[1] % 2 == 1 ? new byte[] { 0x67, r[1], 0x11, 0x22, 0x33 } : new byte[] { 0x67, r[1] },
            0x22 => Read(tx, (ushort)(r[1] << 8 | r[2])),
            0x2E => Write(tx, (ushort)(r[1] << 8 | r[2]), r[3..]),
            _ => new byte[] { 0x7F, r[0], 0x11 },
        };
        return Multi(rx, resp, padded);
    }

    private byte[] Read(uint tx, ushort did) =>
        Dids.TryGetValue(tx, out var d) && d.TryGetValue(did, out var v) ? new byte[] { 0x62, (byte)(did >> 8), (byte)did }.Concat(v).ToArray() : new byte[] { 0x7F, 0x22, 0x31 };

    private byte[] Write(uint tx, ushort did, byte[] data)
    {
        if (!Dids.TryGetValue(tx, out var d)) Dids[tx] = d = new();
        if (CorruptWrites) { data = (byte[])data.Clone(); data[0] ^= 0xFF; }
        d[did] = data;
        return new byte[] { 0x6E, (byte)(did >> 8), (byte)did };
    }

    /// <summary>Formats a payload as ELM output lines (ATH1 ATS0 ATCAF1): header + PCI + data.</summary>
    public static string Multi(uint rx, byte[] p, bool padded = false)
    {
        var frames = IsoTp.IsoTpCodec.Segment(p, pad: padded ? (byte)0 : null);
        return string.Join("\r", frames.Select(f => rx.ToString("X3") + Convert.ToHexString(f)));
    }
}
