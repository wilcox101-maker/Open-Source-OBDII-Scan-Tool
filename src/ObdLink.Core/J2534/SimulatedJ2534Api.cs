namespace ObdLink.Core.J2534;

/// <summary>One reply the simulated vehicle sends: from <paramref name="RxId"/>, after <paramref name="DelayMs"/>.</summary>
public sealed record SimReply(uint RxId, byte[] Payload, int DelayMs = 0);

/// <summary>
/// SYNTHETIC J2534 device for tests and demos. It follows the J2534-1 rules that matter to the channel:
/// ISO 15765 replies are received only through a matching flow-control filter; every write is followed by a TX indication;
/// a reply longer than 7 bytes is preceded by a first-frame indication (START_OF_MESSAGE); J1962_PINS is accepted only on a
/// pin-switched protocol; ERR_TIMEOUT is never returned by ReadMsgs (an empty list is). Replies come from <see cref="Responder"/>.
/// </summary>
public sealed class SimulatedJ2534Api : IJ2534Api
{
    private readonly object _sync = new();
    private readonly List<(DateTime Due, uint Channel, PassThruMsg Msg)> _rx = new();
    private readonly Dictionary<uint, Channel> _channels = new();
    private uint _nextChannel = 1, _nextFilter = 1;
    private bool _open;

    public sealed class Channel
    {
        public uint Protocol { get; init; }
        public uint Baud { get; init; }
        public uint? Pins { get; set; }
        public Dictionary<uint, (J2534Filter Type, uint Mask, uint Pattern, uint? Flow)> Filters { get; } = new();
    }

    /// <summary>(channel setup, request CAN ID, request payload) → replies. Default: nothing answers.</summary>
    public Func<Channel, uint, byte[], IEnumerable<SimReply>> Responder { get; set; } = (_, _, _) => Array.Empty<SimReply>();
    /// <summary>False imitates a J2534-1-only device: pin-switched protocols are refused.</summary>
    public bool PinSwitching { get; set; } = true;
    public uint VbattMillivolts { get; set; } = 12_600;
    /// <summary>True: a multi-frame reply's first-frame indication is delivered at once (as a device does when the FF arrives), not with the reply.</summary>
    public bool EarlyFirstFrame { get; set; }
    public List<(uint Channel, PassThruMsg Msg)> Written { get; } = new();
    public List<string> Calls { get; } = new();
    public IReadOnlyDictionary<uint, Channel> Channels { get { lock (_sync) return new Dictionary<uint, Channel>(_channels); } }

    public uint Open(string? name) { lock (_sync) { Calls.Add("Open"); _open = true; return 1; } }
    public void Close(uint deviceId) { lock (_sync) { Calls.Add("Close"); _open = false; _channels.Clear(); } }

    public uint Connect(uint deviceId, uint protocolId, uint flags, uint baud)
    {
        lock (_sync)
        {
            Calls.Add($"Connect {protocolId:X} {baud}");
            if (!_open) throw new J2534Exception("PassThruConnect", 0x1A);
            if (protocolId is J2534Protocol.Iso15765Ps or J2534Protocol.CanPs && !PinSwitching) throw new J2534Exception("PassThruConnect", 0x03);
            var id = _nextChannel++;
            _channels[id] = new Channel { Protocol = protocolId, Baud = baud };
            return id;
        }
    }

    public void Disconnect(uint channelId) { lock (_sync) { Calls.Add($"Disconnect {channelId}"); if (!_channels.Remove(channelId)) throw new J2534Exception("PassThruDisconnect", 0x02); _rx.RemoveAll(x => x.Channel == channelId); } }

    private Channel Ch(uint id, string call) => _channels.TryGetValue(id, out var c) ? c : throw new J2534Exception(call, 0x02);

    public void SetConfig(uint channelId, IReadOnlyList<(uint Parameter, uint Value)> config)
    {
        lock (_sync)
        {
            var ch = Ch(channelId, "PassThruIoctl");
            foreach (var (p, v) in config)
            {
                if (p == J2534Config.J1962Pins)
                {
                    if (ch.Protocol is not (J2534Protocol.Iso15765Ps or J2534Protocol.CanPs)) throw new J2534Exception("PassThruIoctl(SET_CONFIG)", 0x05, "J1962_PINS on a protocol without pin switching");
                    if (v != 0x060E && v != 0x030B) throw new J2534Exception("PassThruIoctl(SET_CONFIG)", 0x13);
                    ch.Pins = v;
                }
                Calls.Add($"SetConfig {p:X}={v:X}");
            }
        }
    }

    public void Ioctl(uint channelId, J2534Ioctl ioctl)
    {
        lock (_sync)
        {
            Ch(channelId, "PassThruIoctl");
            Calls.Add($"Ioctl {ioctl}");
            if (ioctl == J2534Ioctl.ClearRxBuffer) { var now = DateTime.UtcNow; _rx.RemoveAll(x => x.Channel == channelId && x.Due <= now); }
        }
    }

    public uint StartMsgFilter(uint channelId, J2534Filter type, PassThruMsg mask, PassThruMsg pattern, PassThruMsg? flowControl)
    {
        lock (_sync)
        {
            var ch = Ch(channelId, "PassThruStartMsgFilter");
            if (type == J2534Filter.FlowControl && flowControl is null) throw new J2534Exception("PassThruStartMsgFilter", 0x04);
            var id = _nextFilter++;
            ch.Filters[id] = (type, mask.CanId, pattern.CanId, flowControl?.CanId);
            return id;
        }
    }

    public void StopMsgFilter(uint channelId, uint filterId) { lock (_sync) { if (!Ch(channelId, "PassThruStopMsgFilter").Filters.Remove(filterId)) throw new J2534Exception("PassThruStopMsgFilter", 0x16); } }

    public void WriteMsgs(uint channelId, IReadOnlyList<PassThruMsg> msgs, uint timeoutMs)
    {
        lock (_sync)
        {
            var ch = Ch(channelId, "PassThruWriteMsgs");
            var now = DateTime.UtcNow;
            foreach (var m in msgs)
            {
                if (m.ProtocolId != ch.Protocol) throw new J2534Exception("PassThruWriteMsgs", 0x15);
                if (m.Data.Length < 5) throw new J2534Exception("PassThruWriteMsgs", 0x0A);
                Written.Add((channelId, m));
                _rx.Add((now, channelId, new PassThruMsg(ch.Protocol, J2534RxStatus.TxIndication, 0, 0, PassThruMsg.IdBytes(m.CanId))));
                foreach (var r in Responder(ch, m.CanId, m.Payload))
                {
                    // ISO 15765: a reply is received only when a flow-control filter matches its ID.
                    if (!ch.Filters.Values.Any(f => f.Type == J2534Filter.FlowControl && (r.RxId & f.Mask) == (f.Pattern & f.Mask))) continue;
                    var due = now.AddMilliseconds(r.DelayMs);
                    if (r.Payload.Length > 7) _rx.Add((EarlyFirstFrame ? now : due, channelId, new PassThruMsg(ch.Protocol, J2534RxStatus.StartOfMessage, 0, 0, PassThruMsg.IdBytes(r.RxId))));
                    _rx.Add((due, channelId, new PassThruMsg(ch.Protocol, 0, 0, 0, PassThruMsg.IdBytes(r.RxId).Concat(r.Payload).ToArray())));
                }
            }
            System.Threading.Monitor.PulseAll(_sync);
        }
    }

    public IReadOnlyList<PassThruMsg> ReadMsgs(uint channelId, int max, uint timeoutMs)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        lock (_sync)
        {
            Ch(channelId, "PassThruReadMsgs");
            while (true)
            {
                var now = DateTime.UtcNow;
                var due = _rx.Where(x => x.Channel == channelId && x.Due <= now).OrderBy(x => x.Due).Take(max).ToList();
                if (due.Count > 0) { foreach (var d in due) _rx.Remove(d); return due.Select(d => d.Msg).ToList(); }
                if (now >= until) return Array.Empty<PassThruMsg>();
                var next = _rx.Where(x => x.Channel == channelId).Select(x => x.Due).DefaultIfEmpty(until).Min();
                var wait = (next < until ? next : until) - now;
                System.Threading.Monitor.Wait(_sync, wait > TimeSpan.Zero ? wait : TimeSpan.FromMilliseconds(1));
            }
        }
    }

    /// <summary>
    /// Answers like the ELM simulator's SYNTHETIC vehicle, so a J2534 demo shows the same modules, DTCs and DIDs as the DEMO
    /// port. HS-CAN (ISO15765, 500k) reaches the simulator's HS modules and the functional OBD address; MS-CAN (ISO15765_PS
    /// 125k on 3/11) reaches its MS modules. Other setups get no answer.
    /// </summary>
    public static Func<Channel, uint, byte[], IEnumerable<SimReply>> FromElmSimulator(Transport.SimulatedAdapter sim) => (ch, id, req) =>
    {
        bool hs = ch.Protocol == J2534Protocol.Iso15765 && ch.Baud == 500_000;
        bool ms = ch.Protocol == J2534Protocol.Iso15765Ps && ch.Baud == 125_000 && ch.Pins == 0x030B;
        if (!(hs && (id == 0x7DF || sim.PresentModules.Contains(id))) && !(ms && sim.MsCanModules.Contains(id))) return Array.Empty<SimReply>();
        lock (sim)
        {
            sim.Respond(ms ? "STP 53" : "STP 33");
            sim.Respond($"ATSH{id:X3}");
            var text = sim.Respond(Convert.ToHexString(req));
            if (text is null || text.StartsWith("NO DATA", StringComparison.Ordinal) || text == "?") return Array.Empty<SimReply>();
            var lines = text.Split('\r', '\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            return IsoTp.ElmDiagChannel.ParseFrames(lines, false).Select(d => new SimReply(d.SourceId, d.Payload)).ToList();
        }
    };

    public uint ReadVbattMillivolts(uint deviceId) => VbattMillivolts;
    public (string Firmware, string Dll, string Api) ReadVersion(uint deviceId) => ("SIM 1.0", "SIM 1.0", "04.04");
    public void Dispose() { }
}
