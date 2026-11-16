namespace ObdLink.Core.Uds;

/// <summary>0x28 CommunicationControl control types (ISO 14229-1).</summary>
public enum CommunicationControlType : byte
{
    EnableRxAndTx = 0x00,
    EnableRxDisableTx = 0x01,
    DisableRxEnableTx = 0x02,
    DisableRxAndTx = 0x03,
}

/// <summary>0x28 communicationType low bits: which message groups the control applies to (all networks; subnet nibble 0).</summary>
[Flags]
public enum CommunicationGroups : byte
{
    Normal = 0x01,
    NetworkManagement = 0x02,
    Both = Normal | NetworkManagement,
}

/// <summary>0x2F inputOutputControlParameter (ISO 14229-1).</summary>
public enum IoControlParameter : byte
{
    ReturnControlToEcu = 0x00,
    ResetToDefault = 0x01,
    FreezeCurrentState = 0x02,
    ShortTermAdjustment = 0x03,
}

/// <summary>Reply to a 2F request: <see cref="ControlStatus"/> is the module's controlStatusRecord (layout is module-specific).</summary>
public sealed record IoControlResult(ushort Did, IoControlParameter Parameter, byte[] ControlStatus);

/// <summary>
/// The state-changing control services: 85 ControlDTCSetting, 28 CommunicationControl and 2F InputOutputControlByIdentifier.
/// All three are in <see cref="UdsServices.StateChanging"/>, so <see cref="UdsClient.Gate"/> applies to every request,
/// including the requests that undo them.
/// The undo requests go through <see cref="UdsClient.RestoreAsync"/>, which lets them through a closed write gate: turning
/// read-only on mid-job must never strand a module with DTC setting off, communication disabled or an output overridden.
/// <para>
/// Each "off / take over" call returns a <see cref="ControlLease"/> whose <see cref="ControlLease.RestoreAsync"/> (or
/// <c>await using</c>) puts the module back: 85 01, 28 00, or 2F did 00 (return control to ECU). ISO 14229-1 also has the module
/// undo these when it leaves the non-default session (by request or S3 timeout), which is the backstop if the link drops.
/// These services are bench-first: there is no App UI for them yet, and Ford module behaviour is [UNVERIFIED].
/// </para>
/// </summary>
public static class UdsControl
{
    // ---- 85 ControlDTCSetting -------------------------------------------------------------------------------------

    /// <summary>85 02: the module stops updating DTC status bits (used around procedures that would set faults). Restore = 85 01.</summary>
    public static async Task<ControlLease> DisableDtcSettingAsync(this UdsClient uds, CancellationToken ct = default)
    {
        await SetDtcSettingAsync(uds, enabled: false, ct);
        return new ControlLease(uds, $"DTC setting off (85 02) on 0x{uds.TargetTxId:X3}", c => DtcSetting(uds.RestoreAsync, enabled: true, c));
    }

    /// <summary>85 01 / 85 02 without a lease.</summary>
    public static Task SetDtcSettingAsync(this UdsClient uds, bool enabled, CancellationToken ct = default) => DtcSetting(uds.RequestAsync, enabled, ct);

    private static async Task DtcSetting(Func<byte[], CancellationToken, Task<byte[]>> send, bool enabled, CancellationToken ct)
    {
        byte sub = enabled ? (byte)0x01 : (byte)0x02;
        var r = await send(new byte[] { 0x85, sub }, ct);
        if (r.Length < 2 || r[0] != 0xC5 || (r[1] & 0x7F) != sub)
            throw new InvalidDataException($"ControlDTCSetting reply does not confirm sub-function 0x{sub:X2}: {Convert.ToHexString(r)}");
    }

    // ---- 28 CommunicationControl ----------------------------------------------------------------------------------

    /// <summary>
    /// 28 with a disabling control type. Restore = 28 00 (enable Rx and Tx) for the same groups. Disabling normal messages
    /// silences the module on the bus: other modules may set U-codes or degrade, so only on a stationary vehicle.
    /// </summary>
    public static async Task<ControlLease> DisableCommunicationAsync(this UdsClient uds, CommunicationControlType type, CommunicationGroups groups = CommunicationGroups.Normal, CancellationToken ct = default)
    {
        if (type == CommunicationControlType.EnableRxAndTx) throw new ArgumentException("Use SetCommunicationAsync to enable; a lease is for a disabling type", nameof(type));
        await SetCommunicationAsync(uds, type, groups, ct);
        return new ControlLease(uds, $"communication {type} ({groups}) on 0x{uds.TargetTxId:X3}",
            c => Communication(uds.RestoreAsync, CommunicationControlType.EnableRxAndTx, groups, c));
    }

    public static Task SetCommunicationAsync(this UdsClient uds, CommunicationControlType type, CommunicationGroups groups = CommunicationGroups.Normal, CancellationToken ct = default)
        => Communication(uds.RequestAsync, type, groups, ct);

    private static async Task Communication(Func<byte[], CancellationToken, Task<byte[]>> send, CommunicationControlType type, CommunicationGroups groups, CancellationToken ct)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type), type, "Only control types 00-03 are supported");
        if (groups is not (CommunicationGroups.Normal or CommunicationGroups.NetworkManagement or CommunicationGroups.Both))
            throw new ArgumentOutOfRangeException(nameof(groups), groups, "Communication groups must be Normal, NetworkManagement or Both");
        var r = await send(new byte[] { 0x28, (byte)type, (byte)groups }, ct);
        if (r.Length < 2 || r[0] != 0x68 || (r[1] & 0x7F) != (byte)type)
            throw new InvalidDataException($"CommunicationControl reply does not confirm control type 0x{(byte)type:X2}: {Convert.ToHexString(r)}");
    }

    // ---- 2F InputOutputControlByIdentifier ------------------------------------------------------------------------

    /// <summary>
    /// 2F did 03 state [mask]: the tester takes over an output (short-term adjustment). Restore = 2F did 00 (return control to ECU).
    /// <paramref name="state"/> and <paramref name="enableMask"/> layouts are defined per DID by the manufacturer.
    /// </summary>
    public static Task<(IoControlResult Result, ControlLease Lease)> AdjustOutputAsync(this UdsClient uds, ushort did, byte[] state, byte[]? enableMask = null, CancellationToken ct = default)
    {
        if (state.Length == 0) throw new ArgumentException("A short-term adjustment needs a control state", nameof(state));
        return TakeOverAsync(uds, did, IoControlParameter.ShortTermAdjustment, state, enableMask, ct);
    }

    /// <summary>2F did 02 [mask]: freeze the output at its current state. Restore = 2F did 00.</summary>
    public static Task<(IoControlResult Result, ControlLease Lease)> FreezeOutputAsync(this UdsClient uds, ushort did, byte[]? enableMask = null, CancellationToken ct = default)
        => TakeOverAsync(uds, did, IoControlParameter.FreezeCurrentState, Array.Empty<byte>(), enableMask, ct);

    /// <summary>2F did 00 [mask]: give the output back to the module.</summary>
    /// <summary>2F did 00 [mask]: give the output back to the module. Allowed with the write gate closed (it only undoes).</summary>
    public static Task<IoControlResult> ReturnControlToEcuAsync(this UdsClient uds, ushort did, byte[]? enableMask = null, CancellationToken ct = default)
        => IoControl(uds.RestoreAsync, did, IoControlParameter.ReturnControlToEcu, Array.Empty<byte>(), enableMask, ct);

    /// <summary>One 2F request with strict DID and parameter echo checks.</summary>
    public static Task<IoControlResult> IoControlAsync(this UdsClient uds, ushort did, IoControlParameter p, byte[] state, byte[]? enableMask = null, CancellationToken ct = default)
        => IoControl(uds.RequestAsync, did, p, state, enableMask, ct);

    private static async Task<IoControlResult> IoControl(Func<byte[], CancellationToken, Task<byte[]>> send, ushort did, IoControlParameter p, byte[] state, byte[]? enableMask, CancellationToken ct)
    {
        if (!Enum.IsDefined(p)) throw new ArgumentOutOfRangeException(nameof(p), p, "Not an ISO 14229-1 inputOutputControlParameter");
        if (p != IoControlParameter.ShortTermAdjustment && state.Length > 0) throw new ArgumentException($"{p} carries no control state", nameof(state));
        var req = new List<byte> { 0x2F, (byte)(did >> 8), (byte)did, (byte)p };
        req.AddRange(state);
        if (enableMask is { Length: > 0 }) req.AddRange(enableMask);
        var r = await send(req.ToArray(), ct);
        if (r.Length < 4 || r[0] != 0x6F || (r[1] << 8 | r[2]) != did || r[3] != (byte)p)
            throw new InvalidDataException($"InputOutputControl reply does not confirm DID 0x{did:X4} parameter 0x{(byte)p:X2}: {Convert.ToHexString(r)}");
        return new IoControlResult(did, p, r[4..]);
    }

    private static async Task<(IoControlResult, ControlLease)> TakeOverAsync(UdsClient uds, ushort did, IoControlParameter p, byte[] state, byte[]? mask, CancellationToken ct)
    {
        var res = await IoControlAsync(uds, did, p, state, mask, ct);
        return (res, new ControlLease(uds, $"output 0x{did:X4} {p} on 0x{uds.TargetTxId:X3}", c => ReturnControlToEcuAsync(uds, did, mask, c)));
    }
}

/// <summary>
/// Something a control service changed that must be put back. <see cref="RestoreAsync"/> sends the undo request (retrying up to
/// <see cref="Attempts"/> times, ignoring the caller's cancellation so a cancelled job still restores). <see cref="DisposeAsync"/>
/// restores if that has not happened yet and never throws: a failure is kept in <see cref="RestoreError"/> and reported through
/// <see cref="RestoreFailed"/>, so it is not lost. The module's own session timeout is the last backstop.
/// </summary>
public sealed class ControlLease : IAsyncDisposable
{
    private readonly UdsClient _uds;
    private readonly Func<CancellationToken, Task> _undo;
    private int _state; // 0 active, 1 restoring/restored

    public string Description { get; }
    public int Attempts { get; set; } = 3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(100);
    public bool Restored { get; private set; }
    public Exception? RestoreError { get; private set; }
    /// <summary>Raised when a restore made by <see cref="DisposeAsync"/> fails after every attempt.</summary>
    public event Action<ControlLease, Exception>? RestoreFailed;

    internal ControlLease(UdsClient uds, string description, Func<CancellationToken, Task> undo)
    { _uds = uds; Description = description; _undo = undo; }

    public uint TargetTxId => _uds.TargetTxId;

    /// <summary>Sends the undo request. Throws the last error if every attempt fails. A second call does nothing.</summary>
    public async Task RestoreAsync()
    {
        if (Interlocked.Exchange(ref _state, 1) != 0) return;
        Exception? last = null;
        for (int i = 0; i < Math.Max(1, Attempts); i++)
        {
            try { await _undo(CancellationToken.None); Restored = true; RestoreError = null; return; }
            catch (Exception ex) { last = ex; if (i + 1 < Attempts && RetryDelay > TimeSpan.Zero) await Task.Delay(RetryDelay); }
        }
        RestoreError = last;
        throw new InvalidOperationException($"Could not restore {Description} after {Attempts} attempt(s): {last!.Message}. " +
            "The module returns to normal when it leaves the diagnostic session (10 01, ECU reset, or 5 s without TesterPresent).", last);
    }

    public async ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref _state) != 0) return;
        try { await RestoreAsync(); }
        catch (Exception ex) { RestoreError ??= ex; RestoreFailed?.Invoke(this, ex); }
    }
}
