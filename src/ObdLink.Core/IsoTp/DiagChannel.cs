using ObdLink.Core.Elm;

namespace ObdLink.Core.IsoTp;

public sealed record DiagResponse(uint SourceId, byte[] Payload);

public interface IDiagChannel
{
    /// <summary>Sends a request payload to the current target; returns every complete response (may be several ECUs for functional requests).</summary>
    Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] request, TimeSpan? timeout = null, CancellationToken ct = default);
    Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default);
}

/// <summary>
/// Optional capability: keep receiving from the current target without transmitting anything. ISO 14229-2 requires this after
/// a "response pending" (NRC 0x78) reply: the client waits up to P2* for the final answer instead of sending the request again.
/// The ELM/STN text interface cannot do this (each command transmits), so <see cref="ElmDiagChannel"/> does not implement it;
/// a pass-thru (J2534) transport can.
/// </summary>
public interface IDiagReceiveChannel
{
    /// <summary>Returns every complete response that arrives within <paramref name="timeout"/>; empty if none.</summary>
    Task<IReadOnlyList<DiagResponse>> ReceiveAsync(TimeSpan timeout, CancellationToken ct = default);
}

/// <summary>
/// Diagnostic channel over an ELM/STN adapter with CAN auto-formatting on (ATCAF1, default):
/// the adapter performs ISO-TP segmentation/flow-control on transmit; with ATH1 + ATS0 it prints
/// each received frame as "&lt;id&gt;&lt;PCI&gt;&lt;data&gt;", which we reassemble host-side with IsoTpReassembler.
/// [UNVERIFIED on MX+ hardware] exact multi-frame output formatting with ATS0/ATH1; verified only against ELM327 datasheet behavior.
/// </summary>
public sealed class ElmDiagChannel : IDiagChannel
{
    private readonly AdapterSession _session;
    public bool Use29Bit { get; set; }
    public ElmDiagChannel(AdapterSession session) => _session = session;

    public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) { Use29Bit = txId > 0x7FF; return _session.SetCanTargetAsync(txId, rxId, ct); }

    /// <summary>v0.3.2: UDS 19 (DTCs), 22 (DIDs) and 31 can return long multi-frame replies (PCM 19 02 FF took &gt;1.5 s on a 2017 F-150).</summary>
    public static TimeSpan? LongTimeoutFor(byte[] request) => request.Length > 0 && request[0] is 0x19 or 0x22 or 0x31 ? TimeSpan.FromSeconds(5) : null;
    public async Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] request, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        timeout ??= LongTimeoutFor(request);
        if (request.Length > 7) return await SendMultiFrameAsync(request, timeout, ct);
        var r = await _session.Link.SendAsync(Convert.ToHexString(request), timeout, ct);
        if (r.Error == ElmError.NoData) return Array.Empty<DiagResponse>();
        if (!r.Ok) throw new DiagLinkException(r.Error, string.Join(" | ", r.Lines));
        return ParseFrames(r.Lines, Use29Bit);
    }

    /// <summary>
    /// Host-side ISO-TP transmit: ATCAF0, send First Frame, wait for Flow Control, send Consecutive Frames,
    /// collect the response, then restore ATCAF1 and the normal response wait.
    /// v0.6.1 audit: honors FC WAIT (a CTS later in the same reply is used; WAIT alone stops the transfer), FC OVERFLOW (abort),
    /// and block size BS&gt;0 (expects a new FC after every BS frames). Intermediate frames use a short response wait (ATST19, ~100 ms)
    /// so the gap between frames stays well inside the module's ~1 s N_Cr timeout over Bluetooth.
    /// [UNVERIFIED on MX+]: STN's STPX may do this inside the adapter; host-side timing over Bluetooth not measured on hardware.
    /// </summary>
    private async Task<IReadOnlyList<DiagResponse>> SendMultiFrameAsync(byte[] request, TimeSpan? timeout, CancellationToken ct)
    {
        var link = _session.Link;
        var frames = IsoTpCodec.Segment(request);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await link.SendAsync("ATCAF0", ct: ct);
        try
        {
            var fcResp = await link.SendAsync(Convert.ToHexString(frames[0]), timeout, ct);
            var fc = await AwaitClearToSendAsync(fcResp, ct) ?? throw new IsoTpException("No flow control frame after first frame; nothing more was sent");
            await link.SendAsync("ATST19", ct: ct); // ~100 ms wait after intermediate frames (no reply expected)
            ElmResponse last = fcResp;
            int sinceFc = 0;
            for (int i = 1; i < frames.Count; i++)
            {
                if (fc.StMinMs > 0) await Task.Delay(fc.StMinMs, ct);
                bool final = i == frames.Count - 1;
                if (final) await _session.ApplyDiagTimingAsync(ct); // normal (longer) wait for the module's answer
                last = await link.SendAsync(Convert.ToHexString(frames[i]), final ? timeout : TimeSpan.FromSeconds(1), ct);
                sinceFc++;
                if (!final && fc.BlockSize > 0 && sinceFc == fc.BlockSize)
                {
                    fc = await AwaitClearToSendAsync(last, ct) ?? throw new IsoTpException($"No flow control after a block of {sinceFc} frames; the transfer was stopped at frame {i + 1} of {frames.Count}");
                    sinceFc = 0;
                }
            }
            // v0.22.2: how long a host-segmented message takes on this link (one adapter command per CAN frame).
            Serilog.Log.Debug("ISO-TP host send: {Bytes} bytes in {Frames} frames, {Ms} ms ({PerFrame:0.0} ms/frame)",
                request.Length, frames.Count, sw.ElapsedMilliseconds, sw.Elapsed.TotalMilliseconds / frames.Count);
            if (last.Error == ElmError.NoData) return Array.Empty<DiagResponse>();
            if (!last.Ok) throw new DiagLinkException(last.Error, string.Join(" | ", last.Lines));
            return ParseFrames(last.Lines, Use29Bit);
        }
        finally
        {
            await link.SendAsync("ATCAF1", ct: CancellationToken.None);
            try { await _session.ApplyDiagTimingAsync(CancellationToken.None); } catch (Exception) { /* optional timing command; the link logs failures */ }
        }
    }

    public readonly record struct FlowControl(int Status, int BlockSize, int StMinMs);

    /// <summary>
    /// Finds the clear-to-send flow control in one adapter reply. WAIT frames are skipped when a CTS follows them in the same reply.
    /// If the module only said WAIT, the ELM text interface cannot keep listening between commands, so the transfer stops here
    /// (safe: the module simply times out the partial message). OVERFLOW aborts.
    /// </summary>
    private Task<FlowControl?> AwaitClearToSendAsync(ElmResponse resp, CancellationToken ct) => Task.FromResult(ClearToSend(resp.Lines, Use29Bit));

    public static FlowControl? ClearToSend(IEnumerable<string> lines, bool ext)
    {
        var fcs = ParseFlowControls(lines, ext);
        foreach (var f in fcs)
        {
            if (f.Status == 0) return f;
            if (f.Status == 2) throw new IsoTpException("ECU flow control: overflow/abort; nothing more was sent");
        }
        if (fcs.Count > 0) throw new IsoTpException($"ECU answered flow control WAIT ({fcs.Count}x) with no clear-to-send; the transfer was stopped and nothing more was sent");
        return null;
    }

    public static List<FlowControl> ParseFlowControls(IEnumerable<string> lines, bool ext)
    {
        int hl = ext ? 8 : 3;
        var list = new List<FlowControl>();
        foreach (var raw in lines)
        {
            var l = raw.Replace(" ", "");
            if (!IsFrameLine(l, ext) || l[hl] != '3') continue;
            var b = Convert.FromHexString(l[hl..].PadRight(6, '0')[..6]);
            int st = b[2] <= 0x7F ? b[2] : b[2] is >= 0xF1 and <= 0xF9 ? 1 : 127; // F1-F9 = 100-900 µs -> 1 ms; reserved -> 127 ms (ISO 15765-2)
            list.Add(new FlowControl(b[0] & 0x0F, b[1], st));
        }
        return list;
    }

    private static bool IsFrameLine(string l, bool ext) { l = l.Replace(" ", ""); int hl = ext ? 8 : 3; return l.Length >= hl + 2 && l.Length % 2 == (hl % 2) && IsHex(l); }

    public static IReadOnlyList<DiagResponse> ParseFrames(IEnumerable<string> lines, bool ext)
    {
        int hl = ext ? 8 : 3;
        var asm = new Dictionary<uint, IsoTpReassembler>();
        var result = new List<DiagResponse>();
        foreach (var raw in lines)
        {
            var l = raw.Replace(" ", "");
            if (l.Length < hl + 2 || !IsHex(l)) continue;
            uint id = Convert.ToUInt32(l[..hl], 16);
            var hex = l[hl..]; if (hex.Length % 2 != 0) continue;
            var data = Convert.FromHexString(hex);
            if (!asm.TryGetValue(id, out var a)) asm[id] = a = new IsoTpReassembler();
            var p = a.Feed(data);
            if (p is not null) result.Add(new DiagResponse(id, p));
        }
        return result;
    }

    private static bool IsHex(string s) => s.All(Uri.IsHexDigit);
}

public sealed class DiagLinkException(ElmError e, string detail) : Exception($"Adapter error {e}: {detail}") { public ElmError Error { get; } = e; }
