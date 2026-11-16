namespace ObdLink.Core.Transport;

/// <summary>
/// Scripted STN-like adapter + fake ECUs for tests and the UI "Demo" connection.
/// All ECU data here is SYNTHETIC test data, not captured from a real vehicle.
/// </summary>
public enum SimProfile { MxPlus, Ex, GenericElm }
/// <summary>Simulated answers to WriteDataByIdentifier.</summary>
public enum SimWriteReply { Positive, PendingThenPositive, PendingOnly, NoReply, RejectNotStored }

public sealed class SimulatedAdapter
{
    /// <summary>
    /// Which adapter to imitate. The EX ID strings are copied from Dale's FORScan log ("OBDLink EX r2.2.1 STN2231 v5.10.3", later "v5.13.0" in the 2026-10 session). FORScan prints a composite string; the raw STI/STDI replies are NOT captured, so the split into STI and STDI is [UNVERIFIED].
    /// The MX+ strings are SYNTHETIC; the exact STDI text of a real MX+ is [UNVERIFIED].
    /// </summary>
    public SimProfile Profile { get; set; } = SimProfile.MxPlus;
    /// <summary>Host baud the simulated UART accepts (EX only; Bluetooth ignores it). Used by CreateTransportAtBaud.</summary>
    public int UartBaud { get; set; } = 115200;
    public double Voltage { get; set; } = 12.6;
    /// <summary>The only VIN the simulator reports (synthetic; shares no 5-character run with any real VIN in the logs).</summary>
    public const string DemoVin = "1FTDEMX95HF000001";
    public string Vin { get; set; } = DemoVin; // synthetic (check digit valid, MY2017, plant F), valid VIN alphabet so redaction can be seen working (not a real vehicle)
    public Dictionary<uint, Dictionary<ushort, byte[]>> Dids { get; } = new();
    public HashSet<uint> PresentModules { get; } = new() { 0x7E0, 0x7E1, 0x726, 0x720 };
    /// <summary>Modules that answer only while the MS-CAN preset (STP53) is selected; used by the Identify vehicle demo.</summary>
    public HashSet<uint> MsCanModules { get; } = new() { 0x727 };
    /// <summary>Per-module UDS DTCs (3-byte DTC, status). When a module has no entry, a synthetic DTC is returned.</summary>
    public Dictionary<uint, List<(uint Dtc, byte Status)>> UdsDtcs { get; } = new();
    public bool CorruptWrites { get; set; }
    /// <summary>How the simulated module answers 0x2E. The data is stored in every mode except <see cref="SimWriteReply.RejectNotStored"/>.</summary>
    public SimWriteReply WriteReply { get; set; } = SimWriteReply.Positive;
    /// <summary>Raw reply to an ISO-TP first frame (CAF0), e.g. "72E310000" for WAIT. Null = clear-to-send "&lt;rx&gt;300000".</summary>
    public string? FlowControlReply { get; set; }
    /// <summary>Every vehicle request (hex) with the header it went out on, for tests.</summary>
    public List<(uint Header, string Request)> Sent { get; } = new();

    public SimulatedAdapter() { Clock = () => _sw.Elapsed.TotalSeconds; SeedDemoAsBuilt(this); }

    /// <summary>
    /// SYNTHETIC As-Built blocks DE00-DE07 for the F-150 demo modules (PCM 7E0, TCM 7E1, IPC 720, BCM 726) so "Read" works in demo mode.
    /// TCM blocks are synthetic so DEMO can exercise the As-Built page. A 2017 F-150 factory .ab has no TCM (7E1) blocks;
    /// on a real module, NRC 0x31 for DE00 means "no As-Built", not a failed request.
    /// Values are made up (not from any real vehicle). Each block is 3 lines of 5 data bytes.
    /// </summary>
    public static void SeedDemoAsBuilt(SimulatedAdapter sim)
    {
        foreach (var m in new uint[] { 0x7E0, 0x7E1, 0x720, 0x726 })
        {
            if (!sim.Dids.TryGetValue(m, out var d)) sim.Dids[m] = d = new();
            for (int b = 0; b < 8; b++)
                d.TryAdd((ushort)(0xDE00 + b), Enumerable.Range(0, 15).Select(i => (byte)((m + b * 17 + i * 31) & 0xFF)).ToArray());
        }
        // SYNTHETIC identification DIDs (made-up Ford-style part numbers) for the Identify vehicle demo.
        // DEMO-TCM-0001-* is not a real part number.
        var ids = new (uint M, string F188, string F111, string F113, string F124)[]
        {
            (0x7E0, "DEMO-14C204-AA", "DEMO-12A650-AA", "DEMO-12A650-AB", "DEMO-14C204-AC"),
            (0x7E1, "DEMO-TCM-0001-AA", "DEMO-TCM-0001-AB", "DEMO-TCM-0001-AC", "DEMO-TCM-0001-AD"),
            (0x720, "DEMO-14C026-AA", "DEMO-10849-AA", "DEMO-10849-AB", "DEMO-14C088-AA"),
            (0x726, "DEMO-14C184-AA", "DEMO-14B476-AA", "DEMO-14B476-AB", "DEMO-14C184-AB"),
            (0x727, "DEMO-14D099-AA", "DEMO-18C815-AA", "DEMO-18C815-AB", "DEMO-14D099-AB"),
        };
        foreach (var (m, f188, f111, f113, f124) in ids)
        {
            if (!sim.Dids.TryGetValue(m, out var d)) sim.Dids[m] = d = new();
            d.TryAdd(0xF188, System.Text.Encoding.ASCII.GetBytes(f188)); d.TryAdd(0xF111, System.Text.Encoding.ASCII.GetBytes(f111));
            d.TryAdd(0xF113, System.Text.Encoding.ASCII.GetBytes(f113)); d.TryAdd(0xF124, System.Text.Encoding.ASCII.GetBytes(f124));
        }

    }
    public string? Protocol { get; private set; }
    private uint _header = 0x7DF;
    private bool _caf = true;
    private readonly IsoTp.IsoTpReassembler _rxAsm = new();

    public MockTransport CreateTransport() => new(Respond);

    /// <summary>Imitates opening the UART at a given baud. A mismatched rate produces garbage, like a real wrong-baud link.</summary>
    public MockTransport CreateTransportAtBaud(int baud) => new(cmd => Profile == SimProfile.MxPlus || baud == UartBaud ? Respond(cmd) : "\u00ff\u00fe?");

    public string? Respond(string cmd)
    {
        var c = cmd.Replace(" ", "").ToUpperInvariant();
        if (c == "ATZ") { _header = 0x7DF; _caf = true; Protocol = null; return "ELM327 v1.4b"; } // a reset forgets header, formatting and protocol
        if (c is "ATE0" or "ATL0" or "ATS0" or "ATH1" or "ATCRA" or "STPO" or "STPC") return "OK";
        if (c == "ATI") return "ELM327 v1.4b";
        if (Profile == SimProfile.GenericElm && (c.StartsWith("ST"))) return "?";
        if (c == "STI") return Profile == SimProfile.Ex ? "STN2231 v5.13.0" : "STN2255 v5.10.3 (SIMULATED)";
        if (c == "STDI") return Profile == SimProfile.Ex ? "OBDLink EX r2.2.1" : "OBDLink MX+ (SIMULATED)";
        if (c == "ATRV") return $"{Voltage:0.0}V".Replace(',', '.');
        if (c.StartsWith("ATSP")) { Protocol = "ATSP" + c[4..]; return "OK"; }
        if (c == "ATDP") return Protocol == "ATSP2" ? "SAE J1850 VPW" : "ISO 15765-4 (CAN 11/500)";
        if (c.StartsWith("STP")) { Protocol = c[3..]; return "OK"; }
        if (c.StartsWith("ATSH")) { _header = Convert.ToUInt32(c[4..], 16); return "OK"; }
        if (c.StartsWith("ATCRA")) return "OK";
        if (c == "ATCAF0") { _caf = false; return "OK"; }
        if (c == "ATCAF1") { _caf = true; return "OK"; }
        if (c.StartsWith("AT") || c.StartsWith("ST")) return "?";
        byte[] req;
        try { req = Convert.FromHexString(c); } catch { return "?"; } // ELM answers "?" to a malformed command
        Sent.Add((_header, c));
        if (!_caf)
        {   // raw frame mode: host performs ISO-TP
            var type = req[0] >> 4;
            var msg = _rxAsm.Feed(req);
            if (type == 1) return FlowControlReply ?? (_header + 8).ToString("X3") + "300000";
            if (msg is null) return "NO DATA";
            return Uds(_header, msg, padded: true);
        }
        if (Protocol == "ATSP2") return "NO DATA"; // simulated vehicle has no J1850 bus
        return _header == 0x7DF ? Obd(req) : Uds(_header, req);
    }

    /// <summary>Clock for the SYNTHETIC drive cycle (seconds). Defaults to wall time since construction; tests inject a fixed clock.</summary>
    public Func<double> Clock { get; set; }
    private readonly System.Diagnostics.Stopwatch _sw = System.Diagnostics.Stopwatch.StartNew();
    /// <summary>MIL + emission DTC count reported by PID 01 (synthetic: MIL on, 2 DTCs to match Mode 03).</summary>
    public byte MonitorStatusA { get; set; } = 0x82;

    private string Obd(byte[] r) => (r[0], r.Length > 1 ? r[1] : (byte)0) switch
    {
        (0x01, 0x00 or 0x20 or 0x40) => Multi(0x7E8, new byte[] { 0x41, r[1] }.Concat(DriveCycle.Bitmap(r[1])).ToArray()),
        (0x01, 0x01) => Multi(0x7E8, new byte[] { 0x41, 0x01, MonitorStatusA, 0x07, 0xE5, 0x00 }),
        (0x01, var pid) when DriveCycle.Encode(pid, DriveCycle.At(Clock())) is { } d => Multi(0x7E8, new byte[] { 0x41, pid }.Concat(d).ToArray()),
        (0x03, _) => "7E806430203010101",     // 43 02 | 0301 0101 -> P0301, P0101 (synthetic)
        (0x04, _) => "7E80144",
        (0x06, var mid) when Mode6Reply(mid) is { } m6 => Multi(0x7E8, new byte[] { 0x46 }.Concat(m6).ToArray()),
        (0x07, _) => Multi(0x7E8, new byte[] { 0x47, 0x01, 0x01, 0x71 }),             // pending P0171 (synthetic)
        (0x0A, _) => Multi(0x7E8, new byte[] { 0x4A, 0x01, 0x03, 0x01 }),             // permanent P0301 (synthetic)
        (0x02, var pid) when r.Length >= 3 && r[2] == 0x00 && FreezeFrame(pid) is { } ff => Multi(0x7E8, new byte[] { 0x42, pid, 0x00 }.Concat(ff).ToArray()),
        (0x09, 0x02) => Multi(0x7E8, new byte[] { 0x49, 0x02, 0x01 }.Concat(System.Text.Encoding.ASCII.GetBytes(Vin)).ToArray()),
        _ => "NO DATA",
    };

    /// <summary>SYNTHETIC Mode 6: MID 01 (O2 B1S1, two passing ms tests) and MID 21 (catalyst B1: one pass, one FAIL in counts).</summary>
    private static byte[]? Mode6Reply(byte mid) => mid switch
    {
        0x00 => new byte[] { 0x00, 0x80, 0x00, 0x00, 0x01 },  // 01 and next range
        0x20 => new byte[] { 0x20, 0x80, 0x00, 0x00, 0x00 },  // 21
        0x01 => Convert.FromHexString("01" + "87" + "10" + "001F00000190" + "01" + "88" + "10" + "005600000190"),
        0x21 => Convert.FromHexString("21" + "80" + "20" + "004000000080" + "21" + "81" + "24" + "000300000002"),
        _ => null,
    };

    /// <summary>SYNTHETIC freeze frame 0 (stored by P0301): PIDs 02, 04, 05, 0C, 0D.</summary>
    private static byte[]? FreezeFrame(byte pid) => pid switch
    {
        0x00 => new byte[] { 0x58, 0x18, 0x00, 0x00 }, // 02 04 05 0C 0D
        0x20 or 0x40 => new byte[] { 0x00, 0x00, 0x00, 0x00 },
        0x02 => new byte[] { 0x03, 0x01 },
        0x04 => new byte[] { 0x80 },
        0x05 => new byte[] { 0x5A },
        0x0C => new byte[] { 0x0F, 0xA0 },
        0x0D => new byte[] { 0x30 },
        _ => null,
    };

    private string Uds(uint tx, byte[] r, bool padded = false)
    {
        if (!PresentModules.Contains(tx) && !(Protocol == "53" && MsCanModules.Contains(tx))) return "NO DATA";
        uint rx = tx + 8;
        // Suppressed positive response (ISO 14229-1): a module that accepts 3E 80 (or any sub-function request with bit 7) stays silent.
        if (r[0] == 0x3E && r.Length >= 2 && (r[1] & 0x80) != 0) return "NO DATA";
        byte[] resp = r[0] switch
        {
            0x3E => new byte[] { 0x7E, 0x00 },
            0x31 => Routine(tx, r),
            0x85 => DtcSettingControl(tx, r),
            0x28 => CommControl(tx, r),
            0x2F => IoControl(tx, r),
            0x10 => SessionControl(tx, r),
            0x11 => EcuReset(tx, r),
            0x14 => new byte[] { 0x54 },
            0x19 => ReadDtcInfo(tx, r),
            0x27 => SecurityAccess(tx, r),
            0x1A => new byte[] { 0x7F, 0x1A, 0x11 },
            0x22 => Read(tx, (ushort)(r[1] << 8 | r[2])),
            0x2E => Write(tx, (ushort)(r[1] << 8 | r[2]), r[3..]),
            _ => new byte[] { 0x7F, r[0], 0x11 },
        };
        if (r[0] == 0x2E && resp[0] == 0x6E)
            return WriteReply switch
            {
                SimWriteReply.PendingThenPositive => Multi(rx, new byte[] { 0x7F, 0x2E, 0x78 }, padded) + "\r" + Multi(rx, resp, padded),
                SimWriteReply.PendingOnly => Multi(rx, new byte[] { 0x7F, 0x2E, 0x78 }, padded),
                SimWriteReply.NoReply => "NO DATA",
                _ => Multi(rx, resp, padded),
            };
        return Multi(rx, resp, padded);
    }

    private byte[] Read(uint tx, ushort did) =>
        tx == 0x7E0 && did == 0x1E12 ? new byte[] { 0x62, 0x1E, 0x12, (byte)DriveCycle.At(Clock()).Gear } :
        tx == 0x7E0 && did == 0x1E1C ? Tft() :
        tx == 0x7E1 && did == 0x1E13 ? Oss() :
        tx == 0x726 && did is >= 0x2813 and <= 0x2816 ? Tire(did) :
        tx == 0x7E0 && did == 0xF190 ? new byte[] { 0x62, 0xF1, 0x90 }.Concat(System.Text.Encoding.ASCII.GetBytes(Vin)).ToArray() :
        Dids.TryGetValue(tx, out var d) && d.TryGetValue(did, out var v) ? new byte[] { 0x62, (byte)(did >> 8), (byte)did }.Concat(v).ToArray() : new byte[] { 0x7F, 0x22, 0x31 };

    /// <summary>SYNTHETIC TPMS (BCM 726 DIDs 2813-2816): about 35-37 psi warming up with the drive cycle, RR a little low. Other DIDs give NRC 31.</summary>
    public static readonly double[] TireBaseKpa = { 245, 247, 226, 244 };
    private byte[] Tire(ushort did)
    {
        var kpa = TireBaseKpa[did - 0x2813] + Math.Min(8, Clock() / 15);
        var raw = (ushort)Math.Round((kpa - 22.0 / 3) * 3);
        return new byte[] { 0x62, (byte)(did >> 8), (byte)did, (byte)(raw >> 8), (byte)raw };
    }
    /// <summary>SYNTHETIC OSS on the TCM (7E1) only, so DEMO exercises the PCM-then-TCM fallback: speed / tire circumference x 3.55 axle.</summary>
    public static double OssRpm(double kmh) => kmh / 3.6 / 2.6 * 60 * 3.55;
    private byte[] Oss() { var raw = (ushort)Math.Round(OssRpm(DriveCycle.At(Clock()).SpeedKmh) * 4); return new byte[] { 0x62, 0x1E, 0x13, (byte)(raw >> 8), (byte)raw }; }
    private byte[] Tft() { var raw = (short)Math.Round(DriveCycle.At(Clock()).TftC * 16); return new byte[] { 0x62, 0x1E, 0x1C, (byte)(raw >> 8), (byte)raw }; }

    /// <summary>
    /// SYNTHETIC UDS 0x19. 02: DTC list (default one DTC 9123-45 status 2F). 01: count. 03: snapshot ids (record 01 for the first DTC).
    /// 04: one snapshot record with one identifier (DID DD00, 4 data bytes). 06: one extended record (01 = occurrence counter 5).
    /// Other sub-functions answer NRC 0x12.
    /// </summary>
    private byte[] ReadDtcInfo(uint tx, byte[] r)
    {
        var dl = UdsDtcs.TryGetValue(tx, out var l) ? l : new List<(uint Dtc, byte Status)> { (0x912345, 0x2F) };
        byte[] D(uint d) => new[] { (byte)(d >> 16), (byte)(d >> 8), (byte)d };
        if (r.Length < 2) return new byte[] { 0x7F, 0x19, 0x13 };
        switch (r[1])
        {
            case 0x01: return new byte[] { 0x59, 0x01, 0xFF, 0x01, (byte)(dl.Count >> 8), (byte)dl.Count };
            case 0x02: return new byte[] { 0x59, 0x02, 0xFF }.Concat(dl.SelectMany(d => D(d.Dtc).Append(d.Status))).ToArray();
            case 0x03: return dl.Count == 0 ? new byte[] { 0x59, 0x03 } : new byte[] { 0x59, 0x03 }.Concat(D(dl[0].Dtc)).Append((byte)0x01).ToArray();
            case 0x04 or 0x06 when r.Length >= 6:
            {
                uint dtc = (uint)(r[2] << 16 | r[3] << 8 | r[4]);
                var hit = dl.FirstOrDefault(d => d.Dtc == dtc);
                if (hit == default) return new byte[] { 0x7F, 0x19, 0x31 };
                var head = new byte[] { 0x59, r[1] }.Concat(D(dtc)).Append(hit.Status);
                bool first = dl[0].Dtc == dtc;
                if (r[1] == 0x04) return (first && r[5] is 0x01 or 0xFF ? head.Concat(new byte[] { 0x01, 0x01, 0xDD, 0x00, 0x00, 0x01, 0x23, 0x45 }) : head).ToArray();
                return head.Concat(new byte[] { 0x01, 0x05 }).ToArray();
            }
            default: return new byte[] { 0x7F, 0x19, 0x12 };
        }
    }

    /// <summary>SYNTHETIC SecurityAccess: seed 11 22 33; the accepted key is the seed reversed (33 22 11). A wrong key gives NRC 0x35.</summary>
    private byte[] SecurityAccess(uint tx, byte[] r)
    {
        if (r.Length < 2) return new byte[] { 0x7F, 0x27, 0x13 };
        if (r[1] % 2 == 1) return new byte[] { 0x67, r[1], 0x11, 0x22, 0x33 };
        if (r.Length == 5 && r[2] == 0x33 && r[3] == 0x22 && r[4] == 0x11) { _unlocked.Add(tx); return new byte[] { 0x67, r[1] }; }
        return new byte[] { 0x7F, 0x27, 0x35 };
    }
    private readonly HashSet<uint> _unlocked = new();
    /// <summary>A session change re-locks the module, as on real ECUs.</summary>
    private byte[] SessionControl(uint tx, byte[] r) { _unlocked.Remove(tx); _session[tx] = (byte)(r[1] & 0x7F); if (_session[tx] == 0x01) ResetControls(tx); return new byte[] { 0x50, r[1], 0x00, 0x32, 0x01, 0xF4 }; }
    private readonly Dictionary<uint, byte> _session = new();
    /// <summary>A reset returns the module to the default session and locks it.</summary>
    private byte[] EcuReset(uint tx, byte[] r) { _session.Remove(tx); ResetControls(tx); _unlocked.Remove(tx); _routinePolls.Remove(tx); return new byte[] { 0x51, r.Length > 1 ? r[1] : (byte)0 }; }
    private readonly Dictionary<uint, int> _routinePolls = new();

    /// <summary>Modules with DTC setting off (85 02), until 85 01, a default session or a reset.</summary>
    public HashSet<uint> DtcSettingOff { get; } = new();
    /// <summary>Modules with communication disabled (28 01/02/03), until 28 00, a default session or a reset.</summary>
    public HashSet<uint> CommunicationDisabled { get; } = new();
    /// <summary>Outputs the tester controls (2F 02/03): (module, DID) → state. Cleared by 2F 00, a default session or a reset.</summary>
    public Dictionary<(uint Tx, ushort Did), byte[]> OverriddenOutputs { get; } = new();
    /// <summary>SYNTHETIC output DID the DEMO modules accept for 2F (1 state byte). Not a Ford DID.</summary>
    public const ushort DemoOutputDid = 0xD100;

    private void ResetControls(uint tx)
    {
        DtcSettingOff.Remove(tx); CommunicationDisabled.Remove(tx);
        foreach (var k in OverriddenOutputs.Keys.Where(k => k.Tx == tx).ToList()) OverriddenOutputs.Remove(k);
    }
    private bool Extended(uint tx) => _session.TryGetValue(tx, out var s) && s == 0x03;

    /// <summary>SYNTHETIC 85: 01 on / 02 off, extended session only (NRC 0x7F otherwise).</summary>
    private byte[] DtcSettingControl(uint tx, byte[] r)
    {
        if (r.Length < 2) return new byte[] { 0x7F, 0x85, 0x13 };
        if (!Extended(tx)) return new byte[] { 0x7F, 0x85, 0x7F };
        switch (r[1] & 0x7F)
        {
            case 0x01: DtcSettingOff.Remove(tx); break;
            case 0x02: DtcSettingOff.Add(tx); break;
            default: return new byte[] { 0x7F, 0x85, 0x12 };
        }
        return new byte[] { 0xC5, (byte)(r[1] & 0x7F) };
    }

    /// <summary>SYNTHETIC 28: control types 00-03, extended session only.</summary>
    private byte[] CommControl(uint tx, byte[] r)
    {
        if (r.Length < 3) return new byte[] { 0x7F, 0x28, 0x13 };
        if (!Extended(tx)) return new byte[] { 0x7F, 0x28, 0x7F };
        byte t = (byte)(r[1] & 0x7F);
        if (t > 0x03) return new byte[] { 0x7F, 0x28, 0x12 };
        if (t == 0x00) CommunicationDisabled.Remove(tx); else CommunicationDisabled.Add(tx);
        return new byte[] { 0x68, t };
    }

    /// <summary>SYNTHETIC 2F on <see cref="DemoOutputDid"/> only (NRC 0x31 otherwise), extended session only. Status = current state byte.</summary>
    private byte[] IoControl(uint tx, byte[] r)
    {
        if (r.Length < 4) return new byte[] { 0x7F, 0x2F, 0x13 };
        if (!Extended(tx)) return new byte[] { 0x7F, 0x2F, 0x7F };
        ushort did = (ushort)(r[1] << 8 | r[2]);
        if (did != DemoOutputDid) return new byte[] { 0x7F, 0x2F, 0x31 };
        byte state;
        switch (r[3])
        {
            case 0x00: case 0x01: OverriddenOutputs.Remove((tx, did)); state = 0x00; break;
            case 0x02: state = OverriddenOutputs.TryGetValue((tx, did), out var cur) ? cur[0] : (byte)0x00; OverriddenOutputs[(tx, did)] = new[] { state }; break;
            case 0x03:
                if (r.Length < 5) return new byte[] { 0x7F, 0x2F, 0x13 };
                state = r[4]; OverriddenOutputs[(tx, did)] = new[] { state }; break;
            default: return new byte[] { 0x7F, 0x2F, 0x31 };
        }
        return new byte[] { 0x6F, r[1], r[2], r[3], state };
    }

    /// <summary>SYNTHETIC routine identifier the DEMO modules accept. Not a Ford routine.</summary>
    public const ushort DemoRoutineId = 0x1234;

    /// <summary>
    /// SYNTHETIC RoutineControl: only <see cref="DemoRoutineId"/>, only in the extended session (otherwise NRC 0x7F).
    /// Start answers 71 01 12 34 01 (running); the first RequestResults after a start answers status 01 (running), later
    /// ones 00 + result 0x55 (done); stop answers 71 02 12 34. Results before any start give NRC 0x24 (sequence error).
    /// Other routines give NRC 0x31.
    /// </summary>
    private byte[] Routine(uint tx, byte[] r)
    {
        if (r.Length < 4) return new byte[] { 0x7F, 0x31, 0x13 };
        if (!_session.TryGetValue(tx, out var s) || s != 0x03) return new byte[] { 0x7F, 0x31, 0x7F };
        if ((r[2] << 8 | r[3]) != DemoRoutineId) return new byte[] { 0x7F, 0x31, 0x31 };
        var head = new byte[] { 0x71, (byte)(r[1] & 0x7F), r[2], r[3] };
        switch (r[1] & 0x7F)
        {
            case 0x01: _routinePolls[tx] = 0; return head.Append((byte)0x01).ToArray();
            case 0x02: _routinePolls.Remove(tx); return head;
            case 0x03:
                if (!_routinePolls.TryGetValue(tx, out var n)) return new byte[] { 0x7F, 0x31, 0x24 };
                _routinePolls[tx] = n + 1;
                return n == 0 ? head.Append((byte)0x01).ToArray() : head.Concat(new byte[] { 0x00, 0x55 }).ToArray();
            default: return new byte[] { 0x7F, 0x31, 0x12 };
        }
    }

    /// <summary>Modules whose 0x2E answers NRC 0x33 (securityAccessDenied) until unlocked with 0x27.</summary>
    public HashSet<uint> SecuredModules { get; } = new();

    private byte[] Write(uint tx, ushort did, byte[] data)
    {
        if (SecuredModules.Contains(tx) && !_unlocked.Contains(tx)) return new byte[] { 0x7F, 0x2E, 0x33 };
        if (WriteReply == SimWriteReply.RejectNotStored) return new byte[] { 0x7F, 0x2E, 0x22 };
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

    /// <summary>
    /// Builds a simulator from Dale's FORScan 2017 F-150 logs: modules listed as found (only those with
    /// a known address in the table) and their DTCs. FORScan "C1034:78-AB" is encoded as UDS DTC bytes
    /// [J2012 2 bytes][FTB 0x78] with status 0xAB. Reading "-AB" as the UDS status byte is an assumption,
    /// but it fits the values seen (08, 48, 68, 6C, AB, C8 are all valid status masks).
    /// </summary>
    public static SimulatedAdapter FromForscanLogs(string scanLog, params string[] dtcReports)
    {
        var sim = new SimulatedAdapter { Vin = DemoVin };
        sim.PresentModules.Clear();
        var table = Modules.FordF150_2017.Modules;
        uint? Addr(string ac) { var a = Modules.FordF150_2017.ForscanAlias.GetValueOrDefault(ac, ac); return table.FirstOrDefault(m => m.Acronym == a && m.HasAddress)?.TxId; }
        foreach (var ac in Logs.ForscanLogParser.FoundModules(scanLog)) if (Addr(ac) is uint t) sim.PresentModules.Add(t);
        foreach (var d in dtcReports.SelectMany(Logs.ForscanLogParser.ParseDtcs))
        {
            if (Addr(d.Module) is not uint t) continue;
            var two = ObdLink.Core.Obd.DtcFormat.ToTwoBytes(d.Code);
            var code = (uint)two << 8 | Convert.ToByte(d.Symptom, 16);
            if (!sim.UdsDtcs.TryGetValue(t, out var l)) sim.UdsDtcs[t] = l = new();
            if (!l.Any(x => x.Dtc == code)) l.Add((code, Convert.ToByte(d.Status, 16)));
        }
        return sim;
    }
}
