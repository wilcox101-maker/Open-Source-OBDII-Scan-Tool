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
/// Diagnostic channel over an ELM/STN adapter with CAN auto-formatting on (ATCAF1, default):
/// the adapter performs ISO-TP segmentation/flow-control on transmit; with ATH1 + ATS0 it prints
/// each received frame as "&lt;id&gt;&lt;PCI&gt;&lt;data&gt;", which we reassemble host-side with IsoTpReassembler.
/// [UNVERIFIED on MX+ hardware] exact multi-frame output formatting with ATS0/ATH1; verified only against ELM327 datasheet behaviour.
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
    /// collect the response, then restore ATCAF1. Honours FC STmin (ms range) only coarsely; ignores BS&gt;0 waits.
    /// [UNVERIFIED on MX+]: STN firmware may support native multi-frame TX with CAF1 (see FRPM); timing of
    /// per-frame ATST waits over Bluetooth not measured.
    /// </summary>
    private async Task<IReadOnlyList<DiagResponse>> SendMultiFrameAsync(byte[] request, TimeSpan? timeout, CancellationToken ct)
    {
        var link = _session.Link;
        var frames = IsoTpCodec.Segment(request);
        await link.SendAsync("ATCAF0", ct: ct);
        try
        {
            var fc = await link.SendAsync(Convert.ToHexString(frames[0]), timeout, ct);
            var fcFrame = fc.Lines.Select(l => l.Replace(" ", "")).FirstOrDefault(l => l.Length > (Use29Bit ? 8 : 3) && l[Use29Bit ? 8 : 3] == '3')
                ?? throw new IsoTpException("No flow control frame after first frame");
            var fcBytes = Convert.FromHexString(fcFrame[(Use29Bit ? 8 : 3)..].PadRight(6, '0')[..6]);
            if ((fcBytes[0] & 0x0F) == 2) throw new IsoTpException("ECU flow control: overflow/abort");
            int stMin = fcBytes[2] <= 0x7F ? fcBytes[2] : 1;
            ElmResponse last = fc;
            for (int i = 1; i < frames.Count; i++)
            {
                if (stMin > 0) await Task.Delay(stMin, ct);
                last = await link.SendAsync(Convert.ToHexString(frames[i]), i == frames.Count - 1 ? timeout : TimeSpan.FromSeconds(1), ct);
            }
            if (last.Error == ElmError.NoData) return Array.Empty<DiagResponse>();
            if (!last.Ok) throw new DiagLinkException(last.Error, string.Join(" | ", last.Lines));
            return ParseFrames(last.Lines, Use29Bit);
        }
        finally { await link.SendAsync("ATCAF1", ct: CancellationToken.None); }
    }

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
