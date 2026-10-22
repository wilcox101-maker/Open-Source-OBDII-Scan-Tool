using ObdLink.Core.IsoTp;

namespace ObdLink.Core.Uds;

public enum UdsSession : byte { Default = 0x01, Programming = 0x02, Extended = 0x03 }
public enum ResetType : byte { Hard = 0x01, KeyOffOn = 0x02, Soft = 0x03 }

public sealed class UdsNegativeResponseException(byte sid, byte nrc)
    : Exception($"NRC 0x{nrc:X2} ({UdsNrc.Describe(nrc)}) for service 0x{sid:X2}")
{ public byte Service { get; } = sid; public byte Nrc { get; } = nrc; }

public static class UdsNrc
{
    // ISO 14229-1 negative response codes (subset).
    public static string Describe(byte n) => n switch
    {
        0x10 => "generalReject", 0x11 => "serviceNotSupported", 0x12 => "subFunctionNotSupported",
        0x13 => "incorrectMessageLengthOrInvalidFormat", 0x14 => "responseTooLong", 0x21 => "busyRepeatRequest",
        0x22 => "conditionsNotCorrect", 0x24 => "requestSequenceError", 0x31 => "requestOutOfRange",
        0x33 => "securityAccessDenied", 0x35 => "invalidKey", 0x36 => "exceedNumberOfAttempts",
        0x37 => "requiredTimeDelayNotExpired", 0x72 => "generalProgrammingFailure",
        0x78 => "requestCorrectlyReceived-ResponsePending", 0x7E => "subFunctionNotSupportedInActiveSession",
        0x7F => "serviceNotSupportedInActiveSession", _ => "unknown"
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

/// <summary>ISO 14229-1 client (subset) over an IDiagChannel.</summary>
public sealed class UdsClient
{
    private readonly IDiagChannel _ch;
    public uint TargetTxId { get; }
    public ISeedKeyProvider SeedKey { get; set; } = new NullSeedKeyProvider();
    public int PendingRetries { get; set; } = 5;

    public UdsClient(IDiagChannel ch, uint targetTxId) { _ch = ch; TargetTxId = targetTxId; }

    public async Task<byte[]> RequestAsync(byte[] req, CancellationToken ct = default)
    {
        await _ch.SetTargetAsync(TargetTxId, TargetTxId + 8, ct); // Ford/OBD convention rx = tx + 8 [convention, verify per module]
        for (int i = 0; ; i++)
        {
            var resps = await _ch.RequestAsync(req, ct: ct);
            var r = resps.FirstOrDefault()?.Payload ?? throw new TimeoutException($"No response from 0x{TargetTxId:X3}");
            if (r[0] == req[0] + 0x40) return r;
            if (r[0] == 0x7F && r.Length >= 3)
            {
                // 0x78 response-pending: the ELM text interface returns once the first frame arrives,
                // so we can only re-issue the request. [LIMITATION] Not safe for non-idempotent services; see README.
                if ((r[2] == 0x78 || r[2] == 0x21) && i < PendingRetries && IsIdempotent(req[0])) { await Task.Delay(200, ct); continue; }
                throw new UdsNegativeResponseException(r[1], r[2]);
            }
            throw new InvalidDataException($"Unexpected response {Convert.ToHexString(r)}");
        }
    }

    private static bool IsIdempotent(byte sid) => sid is 0x10 or 0x19 or 0x22 or 0x3E;

    public Task<byte[]> DiagnosticSessionControlAsync(UdsSession s, CancellationToken ct = default) => RequestAsync(new[] { (byte)0x10, (byte)s }, ct);
    public Task<byte[]> EcuResetAsync(ResetType t, CancellationToken ct = default) => RequestAsync(new[] { (byte)0x11, (byte)t }, ct);
    public Task<byte[]> TesterPresentAsync(CancellationToken ct = default) => RequestAsync(new byte[] { 0x3E, 0x00 }, ct);

    /// <summary>0x14 ClearDiagnosticInformation, group 0xFFFFFF = all.</summary>
    public Task<byte[]> ClearDtcsAsync(uint group = 0xFFFFFF, CancellationToken ct = default)
        => RequestAsync(new byte[] { 0x14, (byte)(group >> 16), (byte)(group >> 8), (byte)group }, ct);

    /// <summary>0x19 02 reportDTCByStatusMask.</summary>
    public async Task<List<DtcRecord>> ReadDtcsByStatusAsync(byte mask = 0xFF, CancellationToken ct = default)
    {
        var r = await RequestAsync(new byte[] { 0x19, 0x02, mask }, ct);
        var list = new List<DtcRecord>();
        for (int i = 3; i + 3 < r.Length + 0 && i + 4 <= r.Length; i += 4)
            list.Add(new DtcRecord((uint)(r[i] << 16 | r[i + 1] << 8 | r[i + 2]), r[i + 3]));
        return list;
    }

    public async Task<byte[]> ReadDataByIdentifierAsync(ushort did, CancellationToken ct = default)
    {
        var r = await RequestAsync(new byte[] { 0x22, (byte)(did >> 8), (byte)did }, ct);
        if (r.Length < 3 || r[1] != (byte)(did >> 8) || r[2] != (byte)did) throw new InvalidDataException("DID echo mismatch");
        return r[3..];
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
        var seed = seedResp[2..];
        if (seed.All(b => b == 0)) return true; // already unlocked (ISO 14229 convention)
        var key = await SeedKey.ComputeKeyAsync(TargetTxId, level, seed, ct);
        if (key is null) return false;
        var req = new byte[2 + key.Length]; req[0] = 0x27; req[1] = (byte)(level + 1); key.CopyTo(req, 2);
        await RequestAsync(req, ct);
        return true;
    }
}
