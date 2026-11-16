using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;

namespace ObdLink.Core.J2534;

/// <summary>How one Ford bus is opened on a J2534 device.</summary>
public sealed record J2534BusSetup(uint Protocol, uint Baud, uint? Pins);

public static class J2534Buses
{
    /// <summary>
    /// HS-CAN: ISO15765 on pins 6/14 at 500k (J2534-1). MS-CAN: ISO15765_PS on pins 3/11 at 125k (J2534-2 pin selection).
    /// HS-CAN2: ISO15765_PS on pins 3/11 at 500k. MS-CAN and HS-CAN2 need a device that implements J2534-2 pin switching
    /// (genuine VCM II/VCMM, Mongoose-class, many clones); a device without it answers ERR_NOT_SUPPORTED / ERR_PIN_INVALID.
    /// </summary>
    public static J2534BusSetup For(Bus bus) => bus switch
    {
        Bus.HsCan => new(J2534Protocol.Iso15765, 500_000, null),
        Bus.MsCan => new(J2534Protocol.Iso15765Ps, 125_000, 0x030B),
        Bus.HsCan2 => new(J2534Protocol.Iso15765Ps, 500_000, 0x030B),
        _ => throw new NotSupportedException($"Bus {bus} is not available through J2534 in this app"),
    };
}

/// <summary>
/// v0.18.0: diagnostic channel over a J2534 pass-thru device (ISO 15765 handled by the device). Same contract as the ELM
/// channel, plus <see cref="IDiagReceiveChannel"/>, so UdsClient waits for the final answer after "response pending" (0x78)
/// instead of sending the request again.
/// <list type="bullet">
/// <item>Physical target: one flow-control filter (mask 7FF, pattern = response ID, flow control = request ID). A request returns
/// as soon as that module answers.</item>
/// <item>Functional OBD (7DF): flow-control filters for the eight ISO 15765-4 pairs 7E8-7EF ↔ 7E0-7E7; replies are collected
/// until <see cref="FunctionalQuiet"/> passes without a new one.</item>
/// <item>TX indications, loopback copies and first-frame indications are not responses and are skipped.</item>
/// </list>
/// 11-bit IDs only (every Ford module this app knows). Calls into the API run on the thread pool.
/// </summary>
public sealed class J2534DiagChannel : IDiagChannel, IDiagReceiveChannel, IAsyncDisposable
{
    private readonly IJ2534Api _api;
    private readonly uint _device;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private uint? _channel;
    private J2534BusSetup? _setup;
    private (uint Tx, uint? Rx)? _target;
    private readonly List<uint> _filters = new();

    public J2534DiagChannel(IJ2534Api api, uint deviceId) { _api = api; _device = deviceId; }

    public Bus? CurrentBus { get; private set; }
    /// <summary>Wait for the first sign of a reply. 600 ms like the ELM path's ATST 96, which slow modules (IPC, APIM) needed on the truck.</summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMilliseconds(600);
    /// <summary>Once a first-frame indication shows a multi-frame reply is on its way, wait up to this long for the rest (PCM 19 02 took &gt;1.5 s on a 2017 F-150).</summary>
    public TimeSpan MultiFrameTimeout { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>After a functional request, stop collecting when no new reply arrived for this long.</summary>
    public TimeSpan FunctionalQuiet { get; set; } = TimeSpan.FromMilliseconds(100);
    public uint WriteTimeoutMs { get; set; } = 1000;

    /// <summary>Opens the channel for <paramref name="bus"/>, closing the previous one. The target is set again on the next request.</summary>
    public async Task SelectBusAsync(Bus bus, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (CurrentBus == bus && _channel is not null) return;
            var setup = J2534Buses.For(bus);
            await Task.Run(() =>
            {
                CloseChannel();
                var ch = _api.Connect(_device, setup.Protocol, 0, setup.Baud);
                _channel = ch; _setup = setup;
                try
                {
                    var cfg = new List<(uint, uint)> { (J2534Config.Loopback, 0) };
                    if (setup.Pins is uint pins) cfg.Insert(0, (J2534Config.J1962Pins, pins));
                    _api.SetConfig(ch, cfg);
                }
                catch { CloseChannel(); throw; }
            }, ct);
            CurrentBus = bus;
        }
        finally { _gate.Release(); }
    }

    public async Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default)
    {
        if (txId > 0x7FF || rxId > 0x7FF) throw new NotSupportedException("29-bit CAN IDs are not supported on the J2534 channel");
        if (_channel is null) await SelectBusAsync(Bus.HsCan, ct);
        await _gate.WaitAsync(ct);
        try
        {
            if (_target == (txId, rxId)) return;
            await Task.Run(() =>
            {
                var ch = _channel!.Value;
                foreach (var f in _filters) { try { _api.StopMsgFilter(ch, f); } catch (J2534Exception) { /* already gone */ } }
                _filters.Clear();
                var pairs = rxId is uint rx ? new[] { (rx, txId) }
                    : Enumerable.Range(0, 8).Select(i => ((uint)(0x7E8 + i), (uint)(0x7E0 + i))).ToArray();
                foreach (var (r, t) in pairs)
                    _filters.Add(_api.StartMsgFilter(ch, J2534Filter.FlowControl, Msg(0x7FF), Msg(r), Msg(t)));
            }, ct);
            _target = (txId, rxId);
        }
        finally { _gate.Release(); }
    }

    private PassThruMsg Msg(uint id) => new(_setup!.Protocol, 0, J2534TxFlags.Iso15765FramePad, 0, PassThruMsg.IdBytes(id));

    public async Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] request, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (_target is not { } t) throw new InvalidOperationException("SetTargetAsync must be called before RequestAsync");
        await _gate.WaitAsync(ct);
        try
        {
            var ch = _channel!.Value;
            await Task.Run(() =>
            {
                _api.Ioctl(ch, J2534Ioctl.ClearRxBuffer); // a late reply to an earlier request must not be taken for this one
                _api.WriteMsgs(ch, new[] { PassThruMsg.Iso15765(_setup!.Protocol, t.Tx, request) }, WriteTimeoutMs);
            }, ct);
            return await CollectAsync(timeout ?? DefaultTimeout, t.Rx, ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<DiagResponse>> ReceiveAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        if (_target is not { } t) throw new InvalidOperationException("SetTargetAsync must be called before ReceiveAsync");
        await _gate.WaitAsync(ct);
        try { return await CollectAsync(timeout, t.Rx, ct); }
        finally { _gate.Release(); }
    }

    /// <summary>Physical (rx known): returns at the first reply from rx. Functional: collects until quiet or the deadline.</summary>
    private async Task<IReadOnlyList<DiagResponse>> CollectAsync(TimeSpan timeout, uint? rx, CancellationToken ct)
    {
        var ch = _channel!.Value;
        var list = new List<DiagResponse>();
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var left = deadline - DateTime.UtcNow;
            if (left <= TimeSpan.Zero) return list;
            var msgs = await Task.Run(() => _api.ReadMsgs(ch, 16, (uint)Math.Max(1, Math.Min(left.TotalMilliseconds, 100))), ct);
            foreach (var m in msgs)
            {
                if ((m.RxStatus & J2534RxStatus.StartOfMessage) != 0 && (m.RxStatus & J2534RxStatus.TxMsgType) == 0 && (rx is null || m.CanId == rx))
                {
                    var longer = DateTime.UtcNow + MultiFrameTimeout; // a long reply is arriving: give it time to finish
                    if (longer > deadline) deadline = longer;
                    continue;
                }
                if ((m.RxStatus & (J2534RxStatus.TxMsgType | J2534RxStatus.TxIndication | J2534RxStatus.StartOfMessage)) != 0 || m.Data.Length <= 4) continue;
                list.Add(new DiagResponse(m.CanId, m.Payload));
                if (rx is null) deadline = DateTime.UtcNow + FunctionalQuiet;
            }
            if (rx is uint r && list.Any(d => d.SourceId == r)) return list;
        }
    }

    private void CloseChannel()
    {
        if (_channel is uint ch)
        {
            foreach (var f in _filters) { try { _api.StopMsgFilter(ch, f); } catch (J2534Exception) { /* channel closing anyway */ } }
            try { _api.Disconnect(ch); } catch (J2534Exception) { /* device may already be gone */ }
        }
        _filters.Clear(); _channel = null; _setup = null; _target = null; CurrentBus = null;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { await Task.Run(CloseChannel); }
        finally { _gate.Release(); }
    }
}
