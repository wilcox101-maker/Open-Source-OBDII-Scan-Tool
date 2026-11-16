using System.Runtime.InteropServices;
using System.Text;

namespace ObdLink.Core.J2534;

/// <summary>
/// v0.18.0: calls a vendor J2534 v04.04 DLL in this process. The DLL must match the process bitness (see
/// <see cref="DllInfo.LoadsInProcess"/>): a 32-bit driver needs the 32-bit (x86) build of the app. Marshalling follows the
/// Windows ABI of the header: every "unsigned long" is 32 bits, PASSTHRU_MSG is 6 × uint32 + 4128 data bytes, SCONFIG_LIST holds
/// a count and a pointer. Calls are serialized: J2534 drivers are not required to be thread-safe.
/// </summary>
public sealed class NativeJ2534Api : IJ2534Api
{
    private const int MsgSize = 6 * 4 + PassThruMsg.MaxData;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int OpenFn(IntPtr name, out uint deviceId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int IdFn(uint id);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int ConnectFn(uint deviceId, uint protocolId, uint flags, uint baud, out uint channelId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int MsgsFn(uint channelId, IntPtr msgs, ref uint numMsgs, uint timeout);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StartFilterFn(uint channelId, uint type, IntPtr mask, IntPtr pattern, IntPtr flow, out uint filterId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StopFilterFn(uint channelId, uint filterId);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int IoctlFn(uint channelId, uint ioctlId, IntPtr input, IntPtr output);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int VersionFn(uint deviceId, IntPtr fw, IntPtr dll, IntPtr api);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LastErrorFn(IntPtr text);

    private readonly IntPtr _lib;
    private readonly object _sync = new();
    private readonly OpenFn _open; private readonly IdFn _close, _disconnect; private readonly ConnectFn _connect;
    private readonly MsgsFn _read, _write; private readonly StartFilterFn _startFilter; private readonly StopFilterFn _stopFilter;
    private readonly IoctlFn _ioctl; private readonly VersionFn _version; private readonly LastErrorFn? _lastError;

    public string Path { get; }

    public NativeJ2534Api(string dllPath)
    {
        Path = dllPath;
        if (File.Exists(dllPath))
        {
            var m = DllInfo.Machine(dllPath);
            if (m != DllMachine.Unknown && !DllInfo.LoadsInProcess(m))
                throw new BadImageFormatException($"{System.IO.Path.GetFileName(dllPath)} is a {m} driver and this app runs as {RuntimeInformation.ProcessArchitecture}. " +
                    (m == DllMachine.X86 ? "Use the 32-bit (x86) build of the app for this device." : "Use the build that matches the driver."));
        }
        _lib = NativeLibrary.Load(dllPath);
        T F<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_lib, name));
        try
        {
            _open = F<OpenFn>("PassThruOpen"); _close = F<IdFn>("PassThruClose");
            _connect = F<ConnectFn>("PassThruConnect"); _disconnect = F<IdFn>("PassThruDisconnect");
            _read = F<MsgsFn>("PassThruReadMsgs"); _write = F<MsgsFn>("PassThruWriteMsgs");
            _startFilter = F<StartFilterFn>("PassThruStartMsgFilter"); _stopFilter = F<StopFilterFn>("PassThruStopMsgFilter");
            _ioctl = F<IoctlFn>("PassThruIoctl"); _version = F<VersionFn>("PassThruReadVersion");
            _lastError = NativeLibrary.TryGetExport(_lib, "PassThruGetLastError", out var le) ? Marshal.GetDelegateForFunctionPointer<LastErrorFn>(le) : null;
        }
        catch (EntryPointNotFoundException ex) { NativeLibrary.Free(_lib); throw new InvalidOperationException($"{System.IO.Path.GetFileName(dllPath)} is not a J2534 v04.04 driver: {ex.Message}", ex); }
    }

    private void Check(string call, int status)
    {
        if (status == 0) return;
        string? detail = null;
        if (_lastError is not null)
        {
            var buf = Marshal.AllocHGlobal(256);
            try { Zero(buf, 256); if (_lastError(buf) == 0) detail = Marshal.PtrToStringAnsi(buf); }
            finally { Marshal.FreeHGlobal(buf); }
        }
        throw new J2534Exception(call, (uint)status, detail);
    }

    private static void Zero(IntPtr p, int n) { for (int i = 0; i < n; i++) Marshal.WriteByte(p, i, 0); }

    /// <summary>Writes a PASSTHRU_MSG at <paramref name="p"/> (Windows layout).</summary>
    public static void WriteMsg(IntPtr p, PassThruMsg m)
    {
        if (m.Data.Length > PassThruMsg.MaxData) throw new ArgumentException($"PASSTHRU_MSG data is limited to {PassThruMsg.MaxData} bytes");
        Marshal.WriteInt32(p, 0, (int)m.ProtocolId); Marshal.WriteInt32(p, 4, (int)m.RxStatus); Marshal.WriteInt32(p, 8, (int)m.TxFlags);
        Marshal.WriteInt32(p, 12, (int)m.Timestamp); Marshal.WriteInt32(p, 16, m.Data.Length); Marshal.WriteInt32(p, 20, (int)m.ExtraDataIndex);
        Marshal.Copy(m.Data, 0, p + 24, m.Data.Length);
    }

    /// <summary>Reads a PASSTHRU_MSG at <paramref name="p"/> (Windows layout). DataSize is capped at 4128.</summary>
    public static PassThruMsg ReadMsg(IntPtr p)
    {
        var size = (uint)Marshal.ReadInt32(p, 16);
        if (size > PassThruMsg.MaxData) size = PassThruMsg.MaxData;
        var data = new byte[size];
        Marshal.Copy(p + 24, data, 0, (int)size);
        return new PassThruMsg((uint)Marshal.ReadInt32(p, 0), (uint)Marshal.ReadInt32(p, 4), (uint)Marshal.ReadInt32(p, 8), (uint)Marshal.ReadInt32(p, 12), data, (uint)Marshal.ReadInt32(p, 20));
    }

    public uint Open(string? name)
    {
        lock (_sync)
        {
            var n = name is null ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(name);
            try { Check("PassThruOpen", _open(n, out var id)); return id; }
            finally { if (n != IntPtr.Zero) Marshal.FreeHGlobal(n); }
        }
    }

    public void Close(uint deviceId) { lock (_sync) Check("PassThruClose", _close(deviceId)); }
    public uint Connect(uint deviceId, uint protocolId, uint flags, uint baud) { lock (_sync) { Check("PassThruConnect", _connect(deviceId, protocolId, flags, baud, out var ch)); return ch; } }
    public void Disconnect(uint channelId) { lock (_sync) Check("PassThruDisconnect", _disconnect(channelId)); }

    public IReadOnlyList<PassThruMsg> ReadMsgs(uint channelId, int max, uint timeoutMs)
    {
        if (max <= 0) return Array.Empty<PassThruMsg>();
        lock (_sync)
        {
            var buf = Marshal.AllocHGlobal(MsgSize * max);
            try
            {
                uint n = (uint)max;
                var st = _read(channelId, buf, ref n, timeoutMs);
                // ERR_TIMEOUT / ERR_BUFFER_EMPTY just mean "fewer than asked" (possibly none).
                if (st != 0 && st != (int)J2534Status.ErrTimeout && st != (int)J2534Status.ErrBufferEmpty) Check("PassThruReadMsgs", st);
                var list = new List<PassThruMsg>((int)Math.Min(n, (uint)max));
                for (int i = 0; i < Math.Min(n, (uint)max); i++) list.Add(ReadMsg(buf + i * MsgSize));
                return list;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }

    public void WriteMsgs(uint channelId, IReadOnlyList<PassThruMsg> msgs, uint timeoutMs)
    {
        if (msgs.Count == 0) return;
        lock (_sync)
        {
            var buf = Marshal.AllocHGlobal(MsgSize * msgs.Count);
            try
            {
                for (int i = 0; i < msgs.Count; i++) WriteMsg(buf + i * MsgSize, msgs[i]);
                uint n = (uint)msgs.Count;
                Check("PassThruWriteMsgs", _write(channelId, buf, ref n, timeoutMs));
                if (n != msgs.Count) throw new J2534Exception("PassThruWriteMsgs", J2534Status.ErrTimeout, $"{n} of {msgs.Count} message(s) sent");
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }

    public uint StartMsgFilter(uint channelId, J2534Filter type, PassThruMsg mask, PassThruMsg pattern, PassThruMsg? flowControl)
    {
        lock (_sync)
        {
            var buf = Marshal.AllocHGlobal(MsgSize * 3);
            try
            {
                WriteMsg(buf, mask); WriteMsg(buf + MsgSize, pattern);
                if (flowControl is not null) WriteMsg(buf + 2 * MsgSize, flowControl);
                Check("PassThruStartMsgFilter", _startFilter(channelId, (uint)type, buf, buf + MsgSize, flowControl is null ? IntPtr.Zero : buf + 2 * MsgSize, out var id));
                return id;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }

    public void StopMsgFilter(uint channelId, uint filterId) { lock (_sync) Check("PassThruStopMsgFilter", _stopFilter(channelId, filterId)); }

    public void SetConfig(uint channelId, IReadOnlyList<(uint Parameter, uint Value)> config)
    {
        lock (_sync)
        {
            // SCONFIG_LIST { unsigned long NumOfParams; SCONFIG* ConfigPtr; } with SCONFIG { unsigned long Parameter, Value; }.
            var items = Marshal.AllocHGlobal(8 * Math.Max(1, config.Count));
            var list = Marshal.AllocHGlobal(IntPtr.Size * 2);
            try
            {
                for (int i = 0; i < config.Count; i++) { Marshal.WriteInt32(items, 8 * i, (int)config[i].Parameter); Marshal.WriteInt32(items, 8 * i + 4, (int)config[i].Value); }
                Marshal.WriteInt32(list, 0, config.Count);
                Marshal.WriteIntPtr(list, IntPtr.Size, items); // the pointer is aligned to its own size after the 32-bit count
                Check("PassThruIoctl(SET_CONFIG)", _ioctl(channelId, (uint)J2534Ioctl.SetConfig, list, IntPtr.Zero));
            }
            finally { Marshal.FreeHGlobal(list); Marshal.FreeHGlobal(items); }
        }
    }

    public void Ioctl(uint channelId, J2534Ioctl ioctl) { lock (_sync) Check($"PassThruIoctl({ioctl})", _ioctl(channelId, (uint)ioctl, IntPtr.Zero, IntPtr.Zero)); }

    public uint ReadVbattMillivolts(uint deviceId)
    {
        lock (_sync)
        {
            var o = Marshal.AllocHGlobal(4);
            try { Marshal.WriteInt32(o, 0); Check("PassThruIoctl(READ_VBATT)", _ioctl(deviceId, (uint)J2534Ioctl.ReadVbatt, IntPtr.Zero, o)); return (uint)Marshal.ReadInt32(o); }
            finally { Marshal.FreeHGlobal(o); }
        }
    }

    public (string Firmware, string Dll, string Api) ReadVersion(uint deviceId)
    {
        lock (_sync)
        {
            var b = Marshal.AllocHGlobal(80 * 3);
            try
            {
                Zero(b, 240);
                Check("PassThruReadVersion", _version(deviceId, b, b + 80, b + 160));
                return (Marshal.PtrToStringAnsi(b) ?? "", Marshal.PtrToStringAnsi(b + 80) ?? "", Marshal.PtrToStringAnsi(b + 160) ?? "");
            }
            finally { Marshal.FreeHGlobal(b); }
        }
    }

    public void Dispose() { lock (_sync) NativeLibrary.Free(_lib); }
}
