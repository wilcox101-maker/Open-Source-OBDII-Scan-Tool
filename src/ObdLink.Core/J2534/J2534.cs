namespace ObdLink.Core.J2534;

/// <summary>
/// SAE J2534-1 v04.04 and J2534-2 numeric constants. Values checked against the Drew Technologies j2534_v0404.h header
/// (published copies: hackingvolvo/SardineCAN-Win32, erik-smit/j2534-logger, charlie-x/J2534-Sim). Only the numbers are
/// restated here; they are interface facts, not code.
/// </summary>
public static class J2534Protocol
{
    public const uint Can = 5, Iso15765 = 6;
    /// <summary>J2534-2 pin-switched protocols: the pins are chosen with <see cref="J2534Config.J1962Pins"/>.</summary>
    public const uint CanPs = 0x8004, Iso15765Ps = 0x8005;
}

public static class J2534Connect { public const uint Can29BitId = 0x100, CanIdBoth = 0x800; }

public static class J2534RxStatus
{
    public const uint TxMsgType = 0x01, StartOfMessage = 0x02, RxBreak = 0x04, TxIndication = 0x08, PaddingError = 0x10, AddrType = 0x80, Can29BitId = 0x100;
}

public static class J2534TxFlags { public const uint Iso15765FramePad = 0x40, Can29BitId = 0x100; }

public enum J2534Filter : uint { Pass = 1, Block = 2, FlowControl = 3 }

public enum J2534Ioctl : uint { GetConfig = 0x01, SetConfig = 0x02, ReadVbatt = 0x03, ClearTxBuffer = 0x07, ClearRxBuffer = 0x08, ClearPeriodicMsgs = 0x09, ClearMsgFilters = 0x0A }

public static class J2534Config
{
    public const uint DataRate = 0x01, Loopback = 0x03, Iso15765Bs = 0x1E, Iso15765StMin = 0x1F, Iso15765WftMax = 0x25;
    /// <summary>J2534-2: high byte = CAN-H pin, low byte = CAN-L pin, e.g. 0x030B = pins 3 and 11.</summary>
    public const uint J1962Pins = 0x8001;
}

/// <summary>J2534 status codes and their names.</summary>
public static class J2534Status
{
    public const uint NoError = 0x00, ErrTimeout = 0x09, ErrBufferEmpty = 0x10;

    public static string Name(uint code) => code switch
    {
        0x00 => "STATUS_NOERROR", 0x01 => "ERR_NOT_SUPPORTED", 0x02 => "ERR_INVALID_CHANNEL_ID", 0x03 => "ERR_INVALID_PROTOCOL_ID",
        0x04 => "ERR_NULL_PARAMETER", 0x05 => "ERR_INVALID_IOCTL_VALUE", 0x06 => "ERR_INVALID_FLAGS", 0x07 => "ERR_FAILED",
        0x08 => "ERR_DEVICE_NOT_CONNECTED", 0x09 => "ERR_TIMEOUT", 0x0A => "ERR_INVALID_MSG", 0x0B => "ERR_INVALID_TIME_INTERVAL",
        0x0C => "ERR_EXCEEDED_LIMIT", 0x0D => "ERR_INVALID_MSG_ID", 0x0E => "ERR_DEVICE_IN_USE", 0x0F => "ERR_INVALID_IOCTL_ID",
        0x10 => "ERR_BUFFER_EMPTY", 0x11 => "ERR_BUFFER_FULL", 0x12 => "ERR_BUFFER_OVERFLOW", 0x13 => "ERR_PIN_INVALID",
        0x14 => "ERR_CHANNEL_IN_USE", 0x15 => "ERR_MSG_PROTOCOL_ID", 0x16 => "ERR_INVALID_FILTER_ID", 0x17 => "ERR_NO_FLOW_CONTROL",
        0x18 => "ERR_NOT_UNIQUE", 0x19 => "ERR_INVALID_BAUDRATE", 0x1A => "ERR_INVALID_DEVICE_ID",
        _ => $"0x{code:X2}",
    };
}

/// <summary>A J2534 call failed. <see cref="Detail"/> is PassThruGetLastError's text when the driver gave one.</summary>
public sealed class J2534Exception(string call, uint code, string? detail = null)
    : Exception($"{call} failed: {J2534Status.Name(code)}" + (string.IsNullOrWhiteSpace(detail) ? "" : $" ({detail.Trim()})"))
{
    public string Call { get; } = call;
    public uint Code { get; } = code;
    public string? Detail { get; } = detail;
}

/// <summary>PASSTHRU_MSG. For CAN and ISO 15765 the first four data bytes are the CAN ID (big-endian), then the payload.</summary>
public sealed record PassThruMsg(uint ProtocolId, uint RxStatus, uint TxFlags, uint Timestamp, byte[] Data, uint ExtraDataIndex = 0)
{
    /// <summary>J2534-1 maximum data size.</summary>
    public const int MaxData = 4128;

    public uint CanId => Data.Length >= 4 ? (uint)(Data[0] << 24 | Data[1] << 16 | Data[2] << 8 | Data[3]) : 0;
    public byte[] Payload => Data.Length > 4 ? Data[4..] : Array.Empty<byte>();

    public static byte[] IdBytes(uint id) => new[] { (byte)(id >> 24), (byte)(id >> 16), (byte)(id >> 8), (byte)id };

    public static PassThruMsg Iso15765(uint protocol, uint canId, byte[] payload, bool pad = true) =>
        new(protocol, 0, (pad ? J2534TxFlags.Iso15765FramePad : 0) | (canId > 0x7FF ? J2534TxFlags.Can29BitId : 0), 0, IdBytes(canId).Concat(payload).ToArray());
}

/// <summary>
/// The J2534 v04.04 API as the rest of Core uses it. One implementation calls a vendor DLL (same bitness as the process);
/// another forwards to a 32-bit helper process; tests and the demo use a simulated device.
/// All calls are synchronous like the C API; callers run them off the UI thread.
/// </summary>
public interface IJ2534Api : IDisposable
{
    uint Open(string? name);
    void Close(uint deviceId);
    uint Connect(uint deviceId, uint protocolId, uint flags, uint baud);
    void Disconnect(uint channelId);
    /// <summary>Up to <paramref name="max"/> messages; waits up to <paramref name="timeoutMs"/> for the first. Empty on timeout (not an error).</summary>
    IReadOnlyList<PassThruMsg> ReadMsgs(uint channelId, int max, uint timeoutMs);
    void WriteMsgs(uint channelId, IReadOnlyList<PassThruMsg> msgs, uint timeoutMs);
    uint StartMsgFilter(uint channelId, J2534Filter type, PassThruMsg mask, PassThruMsg pattern, PassThruMsg? flowControl);
    void StopMsgFilter(uint channelId, uint filterId);
    void SetConfig(uint channelId, IReadOnlyList<(uint Parameter, uint Value)> config);
    void Ioctl(uint channelId, J2534Ioctl ioctl);
    /// <summary>Battery voltage on pin 16 in millivolts (READ_VBATT on the device).</summary>
    uint ReadVbattMillivolts(uint deviceId);
    (string Firmware, string Dll, string Api) ReadVersion(uint deviceId);
}
