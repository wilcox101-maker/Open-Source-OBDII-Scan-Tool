namespace ObdLink.Core.Uds;

/// <summary>0x31 RoutineControl sub-functions (ISO 14229-1).</summary>
public enum RoutineControlType : byte { Start = 0x01, Stop = 0x02, RequestResults = 0x03 }

/// <summary>Routine identifier ranges from ISO 14229-1 Annex F (Table F.1).</summary>
public enum RoutineIdRange
{
    IsoReserved, Tachograph, VehicleManufacturer, ObdTest, PyrotechnicDeploy, SystemSupplier, Programming, EraseMirrorMemoryDtcs,
}

/// <summary>
/// A positive 71 reply. <see cref="StatusRecord"/> is everything after the routine identifier: ISO 14229-1 puts an optional
/// routineInfo byte first, then the routine status record. Both are manufacturer-defined, so the bytes are kept as sent.
/// </summary>
public sealed record RoutineResult(ushort RoutineId, RoutineControlType Type, byte[] StatusRecord);

/// <summary>Thrown for routine identifiers this tool never sends, whatever the write settings.</summary>
public sealed class RoutineRefusedException(ushort rid, string reason)
    : InvalidOperationException($"Routine 0x{rid:X4} refused: {reason}")
{
    public ushort RoutineId { get; } = rid;
}

/// <summary>
/// 0x31 RoutineControl: start, stop and read the results of a routine in a module (self-tests, calibrations, service
/// procedures). Every 31 request is in <see cref="UdsServices.StateChanging"/>, so <see cref="UdsClient.Gate"/> applies,
/// including to RequestResults (conservative: one table, no sub-function exceptions for writes).
/// <para>
/// Two ranges are refused before anything is sent: pyrotechnic deployment (E200-E2FF, ISO 26021 airbag/pretensioner
/// end-of-life deployment, which can injure people) and programming (FF00 erase memory, FF01 check programming
/// dependencies; module flashing is out of scope). The pyrotechnic refusal is also enforced in
/// <see cref="UdsClient.RequestAsync"/> and the raw console, so it cannot be reached by building the bytes by hand.
/// </para>
/// Ford routine identifiers (for example the per-module on-demand self-test) are catalog data and are not shipped here.
/// </summary>
public static class UdsRoutines
{
    public static RoutineIdRange Classify(ushort rid) => rid switch
    {
        <= 0x00FF => RoutineIdRange.IsoReserved,
        <= 0x01FF => RoutineIdRange.Tachograph,
        <= 0xDFFF => RoutineIdRange.VehicleManufacturer,
        <= 0xE1FF => RoutineIdRange.ObdTest,
        <= 0xE2FF => RoutineIdRange.PyrotechnicDeploy,
        <= 0xEFFF => RoutineIdRange.IsoReserved,
        <= 0xFEFF => RoutineIdRange.SystemSupplier,
        0xFF00 or 0xFF01 => RoutineIdRange.Programming,
        0xFF02 => RoutineIdRange.EraseMirrorMemoryDtcs,
        _ => RoutineIdRange.IsoReserved,
    };

    /// <summary>Null when the routine may be sent (subject to the write gate), otherwise why it never is.</summary>
    public static string? RefusalReason(ushort rid) => Classify(rid) switch
    {
        RoutineIdRange.PyrotechnicDeploy => "pyrotechnic deployment routines (E200-E2FF) can fire airbags or pretensioners and are never sent",
        RoutineIdRange.Programming => "programming routines (FF00/FF01) are only sent by the flash engine (Flash/FlashEngine), never from the Routines tab or the console",
        _ => null,
    };

    /// <summary>
    /// True when a raw request is a RoutineControl for the pyrotechnic range. Checked on every request path. A request too short
    /// to carry a routine identifier is not matched (the module will reject it with NRC 0x13).
    /// </summary>
    public static bool IsPyrotechnicRequest(byte[] req) =>
        req.Length >= 4 && req[0] == 0x31 && Classify((ushort)(req[2] << 8 | req[3])) == RoutineIdRange.PyrotechnicDeploy;

    public static Task<RoutineResult> StartRoutineAsync(this UdsClient uds, ushort rid, byte[]? option = null, CancellationToken ct = default)
        => RunAsync(uds, RoutineControlType.Start, rid, option, ct);

    public static Task<RoutineResult> StopRoutineAsync(this UdsClient uds, ushort rid, byte[]? option = null, CancellationToken ct = default)
        => RunAsync(uds, RoutineControlType.Stop, rid, option, ct);

    public static Task<RoutineResult> RequestRoutineResultsAsync(this UdsClient uds, ushort rid, CancellationToken ct = default)
        => RunAsync(uds, RoutineControlType.RequestResults, rid, null, ct);

    public static byte[] BuildRequest(RoutineControlType type, ushort rid, byte[]? option = null)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type), type, "Not a RoutineControl sub-function");
        if (type == RoutineControlType.RequestResults && option is { Length: > 0 })
            throw new ArgumentException("RequestResults carries no option record", nameof(option));
        if (RefusalReason(rid) is string why) throw new RoutineRefusedException(rid, why);
        var req = new byte[4 + (option?.Length ?? 0)];
        req[0] = 0x31; req[1] = (byte)type; req[2] = (byte)(rid >> 8); req[3] = (byte)rid;
        option?.CopyTo(req, 4);
        return req;
    }

    /// <summary>Checks that a 71 reply echoes the sub-function and routine identifier, and returns the status bytes.</summary>
    public static RoutineResult ParseResponse(byte[] resp, RoutineControlType type, ushort rid)
    {
        if (resp.Length < 4 || resp[0] != 0x71)
            throw new InvalidDataException($"Not a RoutineControl reply: {Convert.ToHexString(resp)}");
        if ((resp[1] & 0x7F) != (byte)type)
            throw new InvalidDataException($"RoutineControl reply is for sub-function 0x{resp[1] & 0x7F:X2}, not 0x{(byte)type:X2}: {Convert.ToHexString(resp)}");
        int echo = resp[2] << 8 | resp[3];
        if (echo != rid)
            throw new InvalidDataException($"RoutineControl reply names routine 0x{echo:X4}, not 0x{rid:X4}: {Convert.ToHexString(resp)}");
        return new RoutineResult(rid, type, resp[4..]);
    }

    private static async Task<RoutineResult> RunAsync(UdsClient uds, RoutineControlType type, ushort rid, byte[]? option, CancellationToken ct)
    {
        var req = BuildRequest(type, rid, option);
        return ParseResponse(await uds.RequestAsync(req, ct), type, rid);
    }
}
