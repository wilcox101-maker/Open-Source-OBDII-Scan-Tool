using ObdLink.Core.IsoTp;

namespace ObdLink.Core.Uds;

public enum UdsSession : byte { Default = 0x01, Programming = 0x02, Extended = 0x03 }
public enum ResetType : byte { Hard = 0x01, KeyOffOn = 0x02, Soft = 0x03 }

public sealed class UdsNegativeResponseException : Exception
{
    public byte Service { get; }
    public byte Nrc { get; }
    public uint? Target { get; }
    public byte[]? Request { get; }
    public UdsNegativeResponseException(byte sid, byte nrc, uint? target = null, byte[]? request = null)
        : base(Build(sid, nrc, target, request)) { Service = sid; Nrc = nrc; Target = target; Request = request; }

    /// <summary>e.g. "Module 0x7E0 rejected ReadDataByIdentifier (0x22) DID 0xDE00: NRC 0x31 requestOutOfRange - ... [request 22DE00, response 7F 22 31]".</summary>
    private static string Build(byte sid, byte nrc, uint? target, byte[]? req)
    {
        var who = target is uint t ? $"Module 0x{t:X3}" : "Module";
        var what = UdsNrc.ServiceName(sid) + $" (0x{sid:X2})";
        if (req is { Length: >= 3 } && sid is 0x22 or 0x2E) what += $" DID 0x{req[1]:X2}{req[2]:X2}";
        var raw = (req is null ? "" : $"request {Convert.ToHexString(req)}, ") + $"response 7F {sid:X2} {nrc:X2}";
        return $"{who} rejected {what}: NRC 0x{nrc:X2} {UdsNrc.Describe(nrc)} - {UdsNrc.Explain(nrc, sid)} [{raw}]";
    }
}

/// <summary>
/// v0.23.20: the module answered, but its negative response names another service (a late answer to an earlier request), so the
/// outcome of the request that was sent is unknown. Write paths treat this like a timeout: settle, then read back.
/// </summary>
public sealed class UdsStrayResponseException(string message) : Exception(message);

public static class UdsNrc
{
    // ISO 14229-1 negative response codes. 0x94-0xEF and 0xF0-0xFE are ranges (specific / manufacturer conditions not correct).
    public static string Describe(byte n) => n switch
    {
        0x10 => "generalReject", 0x11 => "serviceNotSupported", 0x12 => "subFunctionNotSupported",
        0x13 => "incorrectMessageLengthOrInvalidFormat", 0x14 => "responseTooLong", 0x21 => "busyRepeatRequest",
        0x22 => "conditionsNotCorrect", 0x24 => "requestSequenceError", 0x25 => "noResponseFromSubnetComponent",
        0x26 => "failurePreventsExecutionOfRequestedAction", 0x31 => "requestOutOfRange",
        0x33 => "securityAccessDenied", 0x35 => "invalidKey", 0x36 => "exceedNumberOfAttempts",
        0x37 => "requiredTimeDelayNotExpired", 0x70 => "uploadDownloadNotAccepted", 0x71 => "transferDataSuspended",
        0x72 => "generalProgrammingFailure", 0x73 => "wrongBlockSequenceCounter",
        0x78 => "requestCorrectlyReceived-ResponsePending", 0x7E => "subFunctionNotSupportedInActiveSession",
        0x7F => "serviceNotSupportedInActiveSession",
        0x81 => "rpmTooHigh", 0x82 => "rpmTooLow", 0x83 => "engineIsRunning", 0x84 => "engineIsNotRunning",
        0x85 => "engineRunTimeTooLow", 0x86 => "temperatureTooHigh", 0x87 => "temperatureTooLow",
        0x88 => "vehicleSpeedTooHigh", 0x89 => "vehicleSpeedTooLow", 0x8A => "throttle/PedalTooHigh",
        0x8B => "throttle/PedalTooLow", 0x8C => "transmissionRangeNotInNeutral", 0x8D => "transmissionRangeNotInGear",
        0x8F => "brakeSwitch(es)NotClosed", 0x90 => "shifterLeverNotInPark", 0x91 => "torqueConverterClutchLocked",
        0x92 => "voltageTooHigh", 0x93 => "voltageTooLow",
        >= 0x94 and <= 0xEF => "reservedForSpecificConditionsNotCorrect",
        >= 0xF0 and <= 0xFE => "vehicleManufacturerSpecificConditionsNotCorrect",
        _ => "unknown"
    };

    /// <summary>Plain-English meaning of an NRC for the message log / error dialogs.</summary>
    public static string Explain(byte n, byte sid = 0) => n switch
    {
        0x10 => "the module rejected the request without a specific reason",
        0x11 => "the module does not support this service",
        0x12 => "the module does not support this sub-function",
        0x13 => "the request length or format is wrong for this module",
        0x14 => "the response would be too long for the transport",
        0x21 => "the module is busy; try again",
        0x22 => "conditions are not correct (e.g. ignition/engine state or wrong session)",
        0x24 => "request sequence error (a prerequisite step was skipped)",
        0x31 => sid == 0x22 || sid == 0x2E
            ? "the module does not support this data identifier (wrong block number, or this module has no As-Built block at that DID)"
            : "a parameter is out of the range the module accepts",
        0x33 => "security access is required (module is locked)",
        0x35 => "the security key was wrong",
        0x36 => "too many security attempts; wait before retrying",
        0x37 => "the security delay has not expired yet",
        0x72 => "the module reported a programming failure",
        0x78 => "the module is still processing (response pending)",
        0x7E => "sub-function not supported in the current diagnostic session",
        0x7F => "service not supported in the current diagnostic session (an extended session may be required)",
        0x25 => "a sub-network component behind this module did not answer",
        0x26 => "a fault in the module prevents this action",
        0x70 => "the module refused the download/upload request",
        0x71 => "the data transfer was suspended",
        0x73 => "the transfer block sequence counter was wrong",
        0x81 => "engine speed is too high for this action", 0x82 => "engine speed is too low for this action",
        0x83 => "the engine must be off for this action", 0x84 => "the engine must be running for this action",
        0x85 => "the engine has not run long enough", 0x86 => "temperature is too high", 0x87 => "temperature is too low",
        0x88 => "vehicle speed is too high", 0x89 => "vehicle speed is too low",
        0x8A => "throttle/pedal is too high", 0x8B => "throttle/pedal is too low",
        0x8C => "the transmission must be in neutral", 0x8D => "the transmission must be in gear",
        0x8F => "the brake pedal must be pressed", 0x90 => "the shifter must be in park",
        0x91 => "the torque converter clutch is locked", 0x92 => "supply voltage is too high", 0x93 => "supply voltage is too low",
        >= 0x94 and <= 0xEF => "a vehicle condition required for this action is not met",
        >= 0xF0 and <= 0xFE => "a manufacturer-specific vehicle condition is not met",
        _ => "manufacturer-specific or unknown negative response",
    };

    public static string ServiceName(byte sid) => sid switch
    {
        0x01 => "OBD mode 01", 0x03 => "OBD mode 03", 0x09 => "OBD mode 09",
        0x10 => "DiagnosticSessionControl", 0x11 => "ECUReset", 0x14 => "ClearDiagnosticInformation", 0x19 => "ReadDTCInformation",
        0x1A => "KWP ReadEcuIdentification", 0x22 => "ReadDataByIdentifier", 0x23 => "ReadMemoryByAddress",
        0x24 => "ReadScalingDataByIdentifier", 0x27 => "SecurityAccess", 0x28 => "CommunicationControl", 0x29 => "Authentication",
        0x2A => "ReadDataByPeriodicIdentifier", 0x2C => "DynamicallyDefineDataIdentifier", 0x2E => "WriteDataByIdentifier",
        0x2F => "InputOutputControl", 0x31 => "RoutineControl", 0x34 => "RequestDownload", 0x35 => "RequestUpload",
        0x36 => "TransferData", 0x37 => "RequestTransferExit", 0x38 => "RequestFileTransfer", 0x3D => "WriteMemoryByAddress",
        0x3E => "TesterPresent", 0x83 => "AccessTimingParameter", 0x84 => "SecuredDataTransmission", 0x85 => "ControlDTCSetting",
        0x86 => "ResponseOnEvent", 0x87 => "LinkControl", _ => "service",
    };
}

/// <summary>Pluggable seed→key algorithm. The project ships NO proprietary algorithms.</summary>
public interface ISeedKeyProvider
{
    /// <returns>Key bytes, or null if this provider cannot compute a key for that module/level.</returns>
    Task<byte[]?> ComputeKeyAsync(uint moduleTxId, byte level, byte[] seed, CancellationToken ct = default);
}

public sealed class NullSeedKeyProvider : ISeedKeyProvider
{
    public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] s, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
}

public sealed record DtcRecord(uint Code, byte Status)
{
    /// <summary>Formats a 3-byte UDS DTC as SAE-style e.g. "B1234-56" (high 2 bytes per J2012 + failure type byte).</summary>
    public string Display => $"{Obd.DtcFormat.FromTwoBytes((ushort)(Code >> 8))}-{Code & 0xFF:X2}";
}

/// <summary>ISO 14229-1 client over an IDiagChannel. Transport rules (P2/P2*, response pending, suppressed responses) follow ISO 14229-2.</summary>
public sealed class UdsClient
{
    private readonly IDiagChannel _ch;
    public uint TargetTxId { get; }
    public ISeedKeyProvider SeedKey { get; set; } = new NullSeedKeyProvider();
    /// <summary>ELM-style channels only: how many times an idempotent request is sent again after "pending" (0x78) or "busy" (0x21).</summary>
    public const int DefaultPendingRetries = 5, DefaultMaxPendingReplies = 20;
    public static readonly TimeSpan DefaultPendingRetryDelay = TimeSpan.FromMilliseconds(200);
    public int PendingRetries { get; set; } = DefaultPendingRetries;
    public TimeSpan PendingRetryDelay { get; set; } = DefaultPendingRetryDelay;
    /// <summary>Receive-capable channels only: how many consecutive "pending" (0x78) replies are accepted, each followed by a P2* wait.</summary>
    public int MaxPendingReplies { get; set; } = DefaultMaxPendingReplies;
    /// <summary>When set, state-changing services (<see cref="UdsServices.StateChanging"/>) need the gate open before anything is sent.</summary>
    public Safety.WriteGate? Gate { get; set; }
    public TimeProvider Clock { get; set; } = TimeProvider.System;
    /// <summary>P2/P2* the module reported in its last DiagnosticSessionControl reply (ISO defaults until then).</summary>
    public UdsServerTiming ServerTiming { get; private set; } = UdsServerTiming.Default;
    /// <summary>Session the module confirmed last; null until a DiagnosticSessionControl succeeds. An ECU reset returns it to Default.</summary>
    public UdsSession? ActiveSession { get; private set; }
    /// <summary>When the last request was transmitted (S3 activity for <see cref="TesterPresentKeeper"/>).</summary>
    public DateTimeOffset? LastRequestAt { get; private set; }

    public UdsClient(IDiagChannel ch, uint targetTxId) { _ch = ch; TargetTxId = targetTxId; }
    /// <summary>The channel this client talks through (used by services that need to keep receiving, e.g. 2A periodic data).</summary>
    public IDiagChannel Channel => _ch;
    /// <summary>The module's response CAN ID: request ID + 8 [Ford/OBD convention, verify per module].</summary>
    public uint ResponseId => TargetTxId + 8;

    /// <summary>
    /// Sends one request and returns the module's positive response. Returns an empty array when the request set the
    /// suppress-positive-response bit and the module stayed silent (success, per ISO 14229-1).
    /// After "response pending" (0x78): a receive-capable channel waits up to P2* for the final answer without re-sending;
    /// an ELM-style channel re-sends idempotent requests only, and otherwise reports NRC 0x78 so the caller can settle the outcome.
    /// </summary>
    public Task<byte[]> RequestAsync(byte[] req, CancellationToken ct = default) => SendAsync(req, gated: true, ct);

    /// <summary>
    /// Sends a request that only undoes a control service (<see cref="UdsServices.IsRestoreRequest"/>) even when the write gate is
    /// closed, so switching to read-only mid-job can never leave DTC setting off, communication disabled or an output overridden.
    /// Any other request is refused.
    /// </summary>
    public Task<byte[]> RestoreAsync(byte[] req, CancellationToken ct = default)
    {
        if (!UdsServices.IsRestoreRequest(req)) throw new ArgumentException($"{Convert.ToHexString(req)} is not a restore request (85 01, 28 00 xx, 2F did 00)", nameof(req));
        return SendAsync(req, gated: false, ct);
    }

    private async Task<byte[]> SendAsync(byte[] req, bool gated, CancellationToken ct)
    {
        if (req.Length == 0) throw new ArgumentException("Empty UDS request", nameof(req));
        if (UdsRoutines.IsPyrotechnicRequest(req))
            throw new RoutineRefusedException((ushort)(req[2] << 8 | req[3]), UdsRoutines.RefusalReason((ushort)(req[2] << 8 | req[3]))!);
        if (gated && Gate is not null && UdsServices.IsStateChanging(req[0])) Gate.Require($"{UdsNrc.ServiceName(req[0])} (0x{req[0]:X2})");
        bool suppressed = UdsServices.SuppressesPositiveResponse(req);
        uint rx = ResponseId;
        await _ch.SetTargetAsync(TargetTxId, rx, ct);
        for (int attempt = 0; ; attempt++)
        {
            LastRequestAt = Clock.GetUtcNow();
            var resps = await _ch.RequestAsync(req, ct: ct);
            bool resend = false;
            for (int pending = 0; !resend;)
            {
                var r = PickFinal(resps, rx);
                if (r is null)
                {
                    if (pending > 0)
                        throw new TimeoutException($"Module 0x{TargetTxId:X3} answered response pending (0x78) to {UdsNrc.ServiceName(req[0])} but sent no final answer within P2* ({ServerTiming.P2Star.TotalMilliseconds:0} ms)");
                    if (suppressed) { OnPositive(req, Array.Empty<byte>()); return Array.Empty<byte>(); }
                    throw new TimeoutException($"No response from 0x{TargetTxId:X3}");
                }
                if (r[0] == req[0] + 0x40) { OnPositive(req, r); return r; }
                if (r[0] != 0x7F || r.Length < 3)
                    throw new InvalidDataException($"Unexpected response from 0x{TargetTxId:X3} to {Convert.ToHexString(req)}: {Convert.ToHexString(r)}");
                // v0.23.19: a negative response names the service it answers. One for another service (a late answer to an
                // earlier request) is not this request's outcome: never report its NRC, or treat its 0x78 as "pending", as ours.
                if (r[1] != req[0])
                    throw new UdsStrayResponseException($"Module 0x{TargetTxId:X3} sent a negative response for service 0x{r[1]:X2} ({UdsNrc.ServiceName(r[1])}) while 0x{req[0]:X2} was outstanding; the outcome of {Convert.ToHexString(req)} is unknown: {Convert.ToHexString(r)}");
                if (r[2] == 0x78 && _ch is IDiagReceiveChannel listen)
                {
                    if (pending >= MaxPendingReplies)
                        throw new TimeoutException($"Module 0x{TargetTxId:X3} answered response pending (0x78) {pending} times to {UdsNrc.ServiceName(req[0])}; stopped waiting");
                    pending++;
                    resps = await listen.ReceiveAsync(ServerTiming.P2Star, ct);
                    continue;
                }
                // ELM text interface: it returns on the first reply and cannot keep listening, so the only option is to send again.
                // [LIMITATION] Only safe for idempotent services; see README.
                if ((r[2] is 0x78 or 0x21) && attempt < PendingRetries && IsIdempotent(req)) { await Task.Delay(PendingRetryDelay, ct); resend = true; continue; }
                throw new UdsNegativeResponseException(r[1], r[2], TargetTxId, req);
            }
        }
    }

    private void OnPositive(byte[] req, byte[] resp)
    {
        switch (req[0])
        {
            case 0x10 when req.Length >= 2:
                ActiveSession = (UdsSession)(req[1] & 0x7F);
                ServerTiming = UdsServerTiming.TryParse(resp, out var t) ? t : UdsServerTiming.Default;
                break;
            case 0x11:
                ActiveSession = UdsSession.Default;
                ServerTiming = UdsServerTiming.Default;
                break;
        }
    }

    /// <summary>
    /// v0.6.1 audit: picks the module's final answer. Only replies from <paramref name="rx"/> count (another module's frame is never
    /// taken as this module's answer). When the adapter captured "7F xx 78" (pending) and then the real reply in one read,
    /// the real reply wins; a pending reply is returned only if nothing else arrived.
    /// </summary>
    public static byte[]? PickFinal(IReadOnlyList<DiagResponse> resps, uint rx)
    {
        byte[]? pending = null;
        foreach (var d in resps)
        {
            if (d.SourceId != rx || d.Payload.Length == 0) continue;
            var p = d.Payload;
            if (p[0] == 0x7F && p.Length >= 3 && p[2] == 0x78) { pending ??= p; continue; }
            return p;
        }
        return pending;
    }

    /// <summary>Requests that may be sent again after 0x78/0x21 on an ELM-style channel. RoutineControl RequestResults (31 03) only reads.</summary>
    public static bool IsIdempotent(byte[] req) =>
        req[0] is 0x10 or 0x19 or 0x22 or 0x23 or 0x3E
        || (req[0] == 0x31 && req.Length >= 2 && (req[1] & 0x7F) == (byte)RoutineControlType.RequestResults);

    public Task<byte[]> DiagnosticSessionControlAsync(UdsSession s, CancellationToken ct = default) => RequestAsync(new[] { (byte)0x10, (byte)s }, ct);
    public Task<byte[]> EcuResetAsync(ResetType t, CancellationToken ct = default) => RequestAsync(new[] { (byte)0x11, (byte)t }, ct);
    public Task<byte[]> TesterPresentAsync(CancellationToken ct = default) => TesterPresentAsync(false, ct);
    /// <summary>3E 00, or 3E 80 when <paramref name="suppressResponse"/> (no reply expected; an empty result means success).</summary>
    public Task<byte[]> TesterPresentAsync(bool suppressResponse, CancellationToken ct = default)
        => RequestAsync(new byte[] { 0x3E, suppressResponse ? (byte)0x80 : (byte)0x00 }, ct);

    /// <summary>0x14 ClearDiagnosticInformation, group 0xFFFFFF = all.</summary>
    public Task<byte[]> ClearDtcsAsync(uint group = 0xFFFFFF, CancellationToken ct = default)
        => RequestAsync(new byte[] { 0x14, (byte)(group >> 16), (byte)(group >> 8), (byte)group }, ct);

    /// <summary>0x19 02 reportDTCByStatusMask.</summary>
    public async Task<List<DtcRecord>> ReadDtcsByStatusAsync(byte mask = 0xFF, CancellationToken ct = default)
    {
        var r = await RequestAsync(new byte[] { 0x19, 0x02, mask }, ct);
        var list = new List<DtcRecord>();
        for (int i = 3; i + 4 <= r.Length; i += 4)
            list.Add(new DtcRecord((uint)(r[i] << 16 | r[i + 1] << 8 | r[i + 2]), r[i + 3]));
        return list;
    }

    public async Task<byte[]> ReadDataByIdentifierAsync(ushort did, CancellationToken ct = default)
    {
        var r = await RequestAsync(new byte[] { 0x22, (byte)(did >> 8), (byte)did }, ct);
        if (r.Length < 3 || r[1] != (byte)(did >> 8) || r[2] != (byte)did) throw new InvalidDataException("DID echo mismatch");
        return r[3..];
    }

    /// <summary>
    /// 0x22 with several DIDs in one request. The reply is the DIDs' records back to back, so each DID's data length must be known
    /// (<see cref="DidRequest.Length"/>). Modules may cap the number of DIDs per request; they answer NRC 0x13 or 0x14 when it is exceeded.
    /// </summary>
    public async Task<IReadOnlyList<DidValue>> ReadDataByIdentifiersAsync(IReadOnlyList<DidRequest> dids, CancellationToken ct = default)
    {
        if (dids.Count == 0) throw new ArgumentException("No DIDs to read", nameof(dids));
        if (dids.Select(d => d.Did).Distinct().Count() != dids.Count) throw new ArgumentException("A DID is listed more than once", nameof(dids));
        if (dids.Any(d => d.Length <= 0)) throw new ArgumentException("Every DID needs a data length above zero", nameof(dids));
        var req = new byte[1 + 2 * dids.Count];
        req[0] = 0x22;
        for (int i = 0; i < dids.Count; i++) { req[1 + 2 * i] = (byte)(dids[i].Did >> 8); req[2 + 2 * i] = (byte)dids[i].Did; }
        return ParseMultiDid(await RequestAsync(req, ct), dids);
    }

    /// <summary>Splits a 62 reply into per-DID records. Every DID must be echoed in order with exactly its length, and nothing may be left over.</summary>
    public static IReadOnlyList<DidValue> ParseMultiDid(byte[] resp, IReadOnlyList<DidRequest> dids)
    {
        if (resp.Length == 0 || resp[0] != 0x62) throw new InvalidDataException($"Not a ReadDataByIdentifier reply: {Convert.ToHexString(resp)}");
        var list = new List<DidValue>(dids.Count);
        int pos = 1;
        foreach (var d in dids)
        {
            if (pos + 2 + d.Length > resp.Length)
                throw new InvalidDataException($"ReadDataByIdentifier reply too short for DID 0x{d.Did:X4} ({d.Length} bytes): {Convert.ToHexString(resp)}");
            int echo = resp[pos] << 8 | resp[pos + 1];
            if (echo != d.Did) throw new InvalidDataException($"ReadDataByIdentifier reply has DID 0x{echo:X4} where 0x{d.Did:X4} was expected: {Convert.ToHexString(resp)}");
            list.Add(new DidValue(d.Did, resp[(pos + 2)..(pos + 2 + d.Length)]));
            pos += 2 + d.Length;
        }
        if (pos != resp.Length) throw new InvalidDataException($"ReadDataByIdentifier reply has {resp.Length - pos} unexpected trailing byte(s): {Convert.ToHexString(resp)}");
        return list;
    }

    public async Task WriteDataByIdentifierAsync(ushort did, byte[] data, CancellationToken ct = default)
    {
        var req = new byte[3 + data.Length];
        req[0] = 0x2E; req[1] = (byte)(did >> 8); req[2] = (byte)did; data.CopyTo(req, 3);
        await RequestAsync(req, ct);
    }

    /// <summary>0x27 SecurityAccess: requests seed at odd level, asks provider for key, sends key at level+1.</summary>
    public async Task<bool> SecurityAccessAsync(byte level, CancellationToken ct = default)
    {
        if (level % 2 == 0) throw new ArgumentException("Seed request level must be odd");
        var seedResp = await RequestAsync(new byte[] { 0x27, level }, ct);
        // v0.6.1 audit: an empty seed used to count as "already unlocked" (All() is true for no bytes).
        if (seedResp.Length < 3 || seedResp[1] != level) throw new InvalidDataException($"Malformed SecurityAccess seed reply from 0x{TargetTxId:X3}: {Convert.ToHexString(seedResp)}");
        var seed = seedResp[2..];
        if (seed.All(b => b == 0)) return true; // already unlocked (ISO 14229 convention)
        var key = await SeedKey.ComputeKeyAsync(TargetTxId, level, seed, ct);
        if (key is null) return false;
        var req = new byte[2 + key.Length]; req[0] = 0x27; req[1] = (byte)(level + 1); key.CopyTo(req, 2);
        await RequestAsync(req, ct);
        return true;
    }
}
