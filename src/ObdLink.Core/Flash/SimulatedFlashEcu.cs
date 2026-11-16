using ObdLink.Core.IsoTp;

namespace ObdLink.Core.Flash;

/// <summary>A fault the simulated module or link injects for one request.</summary>
public sealed record SimFlashFault(SimFlashFaultKind Kind, byte Nrc = 0)
{
    public static readonly SimFlashFault DropRequest = new(SimFlashFaultKind.DropRequest);
    public static readonly SimFlashFault DropResponse = new(SimFlashFaultKind.DropResponse);
    public static readonly SimFlashFault Pending = new(SimFlashFaultKind.Pending);
    public static readonly SimFlashFault LinkDown = new(SimFlashFaultKind.LinkDown);
    public static readonly SimFlashFault SessionDrop = new(SimFlashFaultKind.SessionDrop);
    public static readonly SimFlashFault LinkDownAfter = new(SimFlashFaultKind.LinkDownAfter);
    public static SimFlashFault Negative(byte nrc) => new(SimFlashFaultKind.Nrc, nrc);
}

public enum SimFlashFaultKind
{
    /// <summary>The request never reached the module (nothing changes; no answer).</summary>
    DropRequest,
    /// <summary>The module processed the request but its answer was lost.</summary>
    DropResponse,
    /// <summary>The module answers 7F xx 78 first; the final answer follows (receive channels) or is lost (ELM-style channels).</summary>
    Pending,
    /// <summary>The adapter/port is gone: the request throws <see cref="IOException"/> and so does every later one until <see cref="SimulatedFlashEcu.LinkDown"/> is cleared.</summary>
    LinkDown,
    /// <summary>The module answers this NRC without processing the request.</summary>
    Nrc,
    /// <summary>The module falls back to the default session (as after an S3 timeout) just before handling the request.</summary>
    SessionDrop,
    /// <summary>The module processes the request, then the adapter/port dies before the answer arrives (IOException from then on).</summary>
    LinkDownAfter,
}

/// <summary>
/// SYNTHETIC module with a bootloader, for DEMO mode and flash fault-injection tests. Follows ISO 14229-1 download semantics:
/// programming session only from the extended session (or while already in the bootloader), seed/key like the main simulator
/// (seed 11 22 33, key 33 22 11), erase before download, block sequence counter from 0x01 with a repeat of the last accepted
/// counter answered positively without writing, transfer exit only after all bytes, check routine compares memory with the
/// expected image. After a reset with an invalid image the module stays in its bootloader (only session, security, download,
/// routines and tester present work), like a real module after an interrupted flash.
/// Implements the ELM-style behaviour (a pending reply hides the final answer); <see cref="ListeningSimulatedFlashEcu"/> adds
/// <see cref="IDiagReceiveChannel"/> like a J2534 pass-thru.
/// </summary>
public class SimulatedFlashEcu : IDiagChannel
{
    public uint TxId { get; }
    public uint RxId => TxId + 8;
    public double Volts { get; set; } = 13.4;
    public string PartBefore { get; set; } = "SIM-OLD-PART";
    public FlashImage? ExpectedImage { get; set; }
    /// <summary>Seen per request (0-based call index): return a fault to inject, or null.</summary>
    public Func<byte[], int, SimFlashFault?>? Faults { get; set; }
    public bool LinkDown { get; set; }
    /// <summary>The erase routine reports "pending" this many times (31 03 polls on ELM, receive waits on pass-thru) before it finishes.</summary>
    public int ErasePendingReplies { get; set; } = 2;
    /// <summary>When false, 31 03 for the erase routine answers NRC 0x12 (results not supported).</summary>
    public bool EraseResultsSupported { get; set; } = true;
    /// <summary>Largest TransferData message (SID + counter + data) reported in the 74 reply.</summary>
    public int MaxBlockLength { get; set; } = 0x102;
    /// <summary>
    /// Session timeout (ISO 14229-2 S3server). When set, a non-default session ends if no request arrives within it: back to the
    /// default session, security locked, any open download dropped (the module stays in its bootloader). Time comes from <see cref="Now"/>.
    /// </summary>
    public TimeSpan? S3 { get; set; }
    /// <summary>Clock for <see cref="S3"/> (tests pass a virtual clock that the engine's delay hook advances).</summary>
    public Func<DateTime> Now { get; set; } = () => DateTime.UtcNow;
    /// <summary>Session drops caused by <see cref="S3"/>.</summary>
    public int SessionTimeouts { get; private set; }
    /// <summary>Drops the session right now, as an S3 timeout would (for fault injection).</summary>
    public void DropSession() { Session = 0x01; Unlocked = false; _dl = null; SessionTimeouts++; SblRunning = false; _ram.Clear(); }
    private DateTime? _lastRequest;
    /// <summary>Some bootloaders ignore TesterPresent in their default session (no answer at all). Recovery must still reach them.</summary>
    public bool IgnoreTesterPresentInBootloader { get; set; }

    public List<byte[]> Calls { get; } = new();
    public int TransferBlocksWritten { get; private set; }
    public int TransferRepeatsIgnored { get; private set; }
    public int Resets { get; private set; }
    public byte Session { get; private set; } = 0x01;
    public bool InBootloader { get; private set; }
    public bool Unlocked { get; private set; }
    public bool ApplicationValid { get; private set; } = true;
    /// <summary>The next seed request(s) answer NRC 0x37 (required time delay not expired).</summary>
    public int SecurityDelaysLeft { get; set; }
    /// <summary>Wrong keys accepted before the module answers NRC 0x36 and locks.</summary>
    public int MaxKeyAttempts { get; set; } = 3;
    /// <summary>v0.23.15: the key this simulated module accepts (default 33 22 11, the seed reversed). Set it to something else to make a correct DEMO key "wrong" (used by <c>obdcli selftest</c>, so it needs no extra seed/key provider).</summary>
    public byte[] AcceptedKey { get; set; } = { 0x33, 0x22, 0x11 };
    public int WrongKeys { get; private set; }
    public bool SecurityLocked { get; private set; }
    /// <summary>Ford-style: the erase is refused (NRC 0x22) until a secondary bootloader was downloaded to <see cref="SblRam"/> and started
    /// with routine 0x0301 and its call address.</summary>
    public bool RequiresSbl { get; set; }
    /// <summary>RAM the SBL is downloaded to (no erase needed). Default 0x00E00000, 64 KiB.</summary>
    public EraseRegion SblRam { get; set; } = new(0x00E00000, 0x10000);
    public bool SblRunning { get; private set; }
    /// <summary>When false, the ISO check routine 0xFF01 answers NRC 0x31 (as on modules that don't have it).</summary>
    public bool CheckRoutineSupported { get; set; } = true;
    public int Commits { get; private set; }
    private readonly Dictionary<uint, byte[]> _ram = new();

    protected readonly Queue<byte[]> Deferred = new();
    private readonly Dictionary<uint, byte[]> _memory = new();
    private readonly List<EraseRegion> _erased = new();
    protected int ErasePollsLeft;
    private Download? _dl;
    private sealed class Download { public bool Ram; public uint Address; public byte[] Data = Array.Empty<byte>(); public int Received; public byte Expected = 1; public byte? Last; }

    public SimulatedFlashEcu(uint txId = 0x7E0) => TxId = txId;

    public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] request, TimeSpan? timeout = null, CancellationToken ct = default)
        => Task.FromResult(Request(request));

    private IReadOnlyList<DiagResponse> Request(byte[] request)
    {
        int index = Calls.Count;
        Calls.Add(request.ToArray());
        if (LinkDown) throw new IOException("The port is closed (simulated adapter unplugged)");
        var now = Now();
        if (S3 is TimeSpan s3 && Session != 0x01 && _lastRequest is DateTime last && now - last > s3) DropSession();
        _lastRequest = now;
        var fault = Faults?.Invoke(request, index);
        if (fault?.Kind == SimFlashFaultKind.SessionDrop) DropSession();
        switch (fault?.Kind)
        {
            case SimFlashFaultKind.LinkDown: LinkDown = true; throw new IOException("The port is closed (simulated adapter unplugged)");
            case SimFlashFaultKind.DropRequest: return Result();
            case SimFlashFaultKind.Nrc: return Result(new byte[] { 0x7F, request[0], fault.Nrc });
        }
        bool eraseStart = request.Length >= 4 && request[0] == 0x31 && (request[1] & 0x7F) == 0x01 && request[2] == 0xFF && request[3] == 0x00;
        if (ErasePollsLeft > 0 && !eraseStart) ErasePollsLeft--; // time passes while the erase runs
        var answer = Handle(request);
        switch (fault?.Kind)
        {
            case SimFlashFaultKind.LinkDownAfter: LinkDown = true; throw new IOException("The port is closed (simulated adapter unplugged after the request)");
            case SimFlashFaultKind.DropResponse: return Result();
            case SimFlashFaultKind.Pending:
                if (answer is not null) Deferred.Enqueue(answer);
                return Result(new byte[] { 0x7F, request[0], 0x78 });
        }
        return answer is null ? Result() : Result(answer);
    }

    protected IReadOnlyList<DiagResponse> Result(params byte[][] payloads) => payloads.Select(p => new DiagResponse(RxId, p)).ToList();

    /// <summary>Reads module memory written by the download (for test assertions).</summary>
    public byte[]? Memory(uint address) => _memory.TryGetValue(address, out var d) ? d : null;

    private byte[]? Handle(byte[] r)
    {
        byte sid = r[0];
        switch (sid)
        {
            case 0x3E when IgnoreTesterPresentInBootloader && InBootloader && Session == 0x01: return null;
            case 0x3E: return (r.Length > 1 && (r[1] & 0x80) != 0) ? null : new byte[] { 0x7E, 0x00 };
            case 0x10 when r.Length >= 2:
                byte s = (byte)(r[1] & 0x7F);
                if (s == 0x02)
                {
                    if (Session != 0x03 && !InBootloader) return Neg(0x10, 0x22);
                    InBootloader = true; Unlocked = false;
                }
                else if (s is not (0x01 or 0x03)) return Neg(0x10, 0x12);
                else if (InBootloader && s == 0x03) return Neg(0x10, 0x7E); // this bootloader has no extended session
                Session = s;
                return new byte[] { 0x50, s, 0x00, 0x32, 0x01, 0xF4 };
            case 0x27 or 0x31 or 0x34 or 0x36 or 0x37 when InBootloader && Session != 0x02:
                return Neg(sid, 0x7F); // serviceNotSupportedInActiveSession (bootloader dropped to its default session)
            case 0x27 when r.Length >= 2:
                if (r[1] % 2 == 1)
                {
                    if (SecurityLocked) return Neg(0x27, 0x37);
                    if (SecurityDelaysLeft > 0) { SecurityDelaysLeft--; return Neg(0x27, 0x37); }
                    return Unlocked ? new byte[] { 0x67, r[1], 0, 0, 0 } : new byte[] { 0x67, r[1], 0x11, 0x22, 0x33 };
                }
                if (SecurityLocked) return Neg(0x27, 0x36);
                if (r.AsSpan(2).SequenceEqual(AcceptedKey)) { Unlocked = true; WrongKeys = 0; return new byte[] { 0x67, r[1] }; }
                if (++WrongKeys >= MaxKeyAttempts) { SecurityLocked = true; return Neg(0x27, 0x36); }
                return Neg(0x27, 0x35);
            case 0x31 when r.Length >= 4: return Routine(r);
            case 0x34: return RequestDownload(r);
            case 0x36: return Transfer(r);
            case 0x37:
                if (_dl is null) return Neg(0x37, 0x24);
                if (_dl.Received != _dl.Data.Length) return Neg(0x37, 0x24);
                if (_dl.Ram) _ram[_dl.Address] = _dl.Data; else _memory[_dl.Address] = _dl.Data;
                _dl = null;
                return new byte[] { 0x77 };
            case 0x11 when r.Length >= 2:
                Resets++;
                if (!ApplicationValid) ApplicationValid = CheckImage(); // untouched software stays valid; erased/written software must match the image
                InBootloader = !ApplicationValid; Session = 0x01; Unlocked = false; _erased.Clear(); SblRunning = false; _ram.Clear();
                return new byte[] { 0x51, r[1] };
            case 0x22 when InBootloader: return Neg(0x22, 0x22); // bootloader: no application data
            case 0x22 when r.Length >= 3 && r[1] == 0xF1 && r[2] == 0x88:
                var part = ApplicationValid && Resets > 0 && ExpectedImage?.SoftwarePartNumber is string p && CheckImage() ? p : PartBefore;
                return new byte[] { 0x62, 0xF1, 0x88 }.Concat(System.Text.Encoding.ASCII.GetBytes(part)).ToArray();
            default: return Neg(sid, 0x11);
        }
    }

    private byte[] Routine(byte[] r)
    {
        ushort rid = (ushort)(r[2] << 8 | r[3]);
        byte type = (byte)(r[1] & 0x7F);
        if (!InBootloader || !Unlocked) return Neg(0x31, InBootloader ? (byte)0x33 : (byte)0x22);
        if (rid == 0x0301 && type == 0x01)
        {
            if (r.Length < 8) return Neg(0x31, 0x13);
            uint call = U32(r, 4);
            if (!_ram.Any(kv => call >= kv.Key && call < kv.Key + kv.Value.Length)) return Neg(0x31, 0x31); // nothing loaded there
            SblRunning = true;
            return new byte[] { 0x71, 0x01, 0x03, 0x01 };
        }
        if (rid == 0x0304 && type == 0x01) { Commits++; return new byte[] { 0x71, 0x01, 0x03, 0x04, 0x00 }; }
        if (rid == 0xFF00 && type == 0x01 && RequiresSbl && !SblRunning) return Neg(0x31, 0x22);
        if (rid == 0xFF01 && !CheckRoutineSupported) return Neg(0x31, 0x31);
        if (rid == 0xFF00 && type == 0x01)
        {
            if (r.Length >= 12) _erased.Add(new EraseRegion(U32(r, 4), U32(r, 8)));
            ApplicationValid = false;
            foreach (var k in _memory.Keys.ToList()) if (_erased.Any(e => k >= e.Address && k < e.Address + e.Length)) _memory.Remove(k);
            ErasePollsLeft = ErasePendingReplies;
            if (ErasePollsLeft == 0) return new byte[] { 0x71, 0x01, 0xFF, 0x00, 0x00 };
            Deferred.Enqueue(new byte[] { 0x71, 0x01, 0xFF, 0x00, 0x00 });
            return Neg(0x31, 0x78);
        }
        if (rid == 0xFF00 && type == 0x03)
        {
            if (!EraseResultsSupported) return Neg(0x31, 0x12);
            if (ErasePollsLeft > 0) return Neg(0x31, 0x78);
            return new byte[] { 0x71, 0x03, 0xFF, 0x00, 0x00 };
        }
        if (rid == 0xFF01 && type is 0x01 or 0x03) return new byte[] { 0x71, type, 0xFF, 0x01, CheckImage() ? (byte)0x00 : (byte)0x01 };
        return Neg(0x31, 0x31);
    }

    private byte[] RequestDownload(byte[] r)
    {
        if (!InBootloader || !Unlocked) return Neg(0x34, 0x33);
        if (r.Length < 3) return Neg(0x34, 0x13);
        int aBytes = r[2] & 0x0F, lBytes = r[2] >> 4;
        if (r.Length != 3 + aBytes + lBytes) return Neg(0x34, 0x13);
        uint addr = 0, len = 0;
        for (int i = 0; i < aBytes; i++) addr = addr << 8 | r[3 + i];
        for (int i = 0; i < lBytes; i++) len = len << 8 | r[3 + aBytes + i];
        if (ErasePollsLeft > 0) return Neg(0x34, 0x22); // erase still running
        bool ram = addr >= SblRam.Address && (long)addr + len <= (long)SblRam.Address + SblRam.Length;
        if (!ram && !_erased.Any(e => addr >= e.Address && (long)addr + len <= (long)e.Address + e.Length)) return Neg(0x34, 0x70);
        _dl = new Download { Address = addr, Data = new byte[len], Ram = ram };
        return new byte[] { 0x74, 0x20, (byte)(MaxBlockLength >> 8), (byte)MaxBlockLength };
    }

    private byte[] Transfer(byte[] r)
    {
        if (_dl is null) return Neg(0x36, 0x24);
        if (r.Length < 2) return Neg(0x36, 0x13);
        byte c = r[1];
        if (_dl.Last == c) { TransferRepeatsIgnored++; return new byte[] { 0x76, c }; } // repeat of the last accepted block: no second write
        if (c != _dl.Expected) return Neg(0x36, 0x73);
        int n = r.Length - 2;
        if (n > MaxBlockLength - 2 || _dl.Received + n > _dl.Data.Length) return Neg(0x36, 0x71);
        Array.Copy(r, 2, _dl.Data, _dl.Received, n);
        _dl.Received += n; _dl.Last = c; _dl.Expected = unchecked((byte)(c + 1));
        TransferBlocksWritten++;
        return new byte[] { 0x76, c };
    }

    private bool CheckImage() => ExpectedImage is not null && ExpectedImage.Segments.All(s => _memory.TryGetValue(s.Address, out var d) && d.AsSpan().SequenceEqual(s.Data));

    private static byte[] Neg(byte sid, byte nrc) => new byte[] { 0x7F, sid, nrc };
    private static uint U32(byte[] b, int i) => (uint)(b[i] << 24 | b[i + 1] << 16 | b[i + 2] << 8 | b[i + 3]);
}

/// <summary>The simulated module behind a channel that can keep listening after "response pending" (J2534-style).</summary>
public sealed class ListeningSimulatedFlashEcu(uint txId = 0x7E0) : SimulatedFlashEcu(txId), IDiagReceiveChannel
{
    public Task<IReadOnlyList<DiagResponse>> ReceiveAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        if (LinkDown) throw new IOException("The port is closed (simulated adapter unplugged)");
        if (Deferred.Count == 0) return Task.FromResult(Result());
        ErasePollsLeft = 0; // the module finished whatever it was busy with
        return Task.FromResult(Result(Deferred.Dequeue()));
    }
}
