using System.Diagnostics;
using System.Runtime.InteropServices;
using ObdLink.Core.J2534;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// v0.18.0: NativeJ2534Api against a real native library. On Linux the test builds Native/j2534stub.c with gcc (uint32_t for
/// every "unsigned long", i.e. the Windows layout) and calls it through the same marshalling the app uses on Windows.
/// Elsewhere, or without gcc, the native tests return early (the managed layout test still runs).
/// </summary>
public class NativeJ2534Tests
{
    private static string Root([System.Runtime.CompilerServices.CallerFilePath] string me = "") => Path.GetDirectoryName(me)!;

    private static readonly Lazy<string?> Stub = new(() =>
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64) return null;
        var outDir = Path.Combine(Path.GetTempPath(), "j2534stub-" + Environment.ProcessId);
        Directory.CreateDirectory(outDir);
        var so = Path.Combine(outDir, "libj2534stub.so");
        try
        {
            var p = Process.Start(new ProcessStartInfo("gcc", $"-shared -fPIC -O1 -o \"{so}\" \"{Path.Combine(Root(), "Native", "j2534stub.c")}\"") { RedirectStandardError = true, RedirectStandardOutput = true });
            if (p is null) return null;
            p.WaitForExit(60_000);
            return p.ExitCode == 0 && File.Exists(so) ? so : null;
        }
        catch (System.ComponentModel.Win32Exception) { return null; } // no gcc on this machine
    });

    [DllImport("libdl.so.2")] private static extern IntPtr dlopen(string file, int mode);
    [DllImport("libdl.so.2")] private static extern IntPtr dlsym(IntPtr handle, string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint U0();
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint U1(int i);

    private static T Sym<T>(string so, string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(dlsym(dlopen(so, 2), name));

    [Fact]
    public void Passthru_msg_layout_round_trips()
    {
        var p = Marshal.AllocHGlobal(6 * 4 + PassThruMsg.MaxData);
        try
        {
            var m = new PassThruMsg(6, 0x02, 0x40, 99, new byte[] { 0, 0, 7, 0xE0, 0x22, 0xF1, 0x90 }, 7);
            NativeJ2534Api.WriteMsg(p, m);
            Assert.Equal(6, Marshal.ReadInt32(p, 0));
            Assert.Equal(7, Marshal.ReadInt32(p, 16));           // DataSize
            Assert.Equal(0xE0, Marshal.ReadByte(p, 24 + 3));     // Data starts after six 32-bit fields
            var back = NativeJ2534Api.ReadMsg(p);
            Assert.Equal((m.ProtocolId, m.RxStatus, m.TxFlags, m.Timestamp, m.ExtraDataIndex), (back.ProtocolId, back.RxStatus, back.TxFlags, back.Timestamp, back.ExtraDataIndex));
            Assert.Equal(m.Data, back.Data);
            Assert.Throws<ArgumentException>(() => NativeJ2534Api.WriteMsg(p, m with { Data = new byte[PassThruMsg.MaxData + 1] }));
        }
        finally { Marshal.FreeHGlobal(p); }
    }

    [Fact]
    public void Full_api_through_the_native_stub()
    {
        if (Stub.Value is not string so) return;
        using var api = new NativeJ2534Api(so);
        var dev = api.Open(null);
        Assert.Equal(42u, dev);
        Assert.Equal(("STUB-FW 1.2", "STUB-DLL 3.4", "04.04"), api.ReadVersion(dev));
        Assert.Equal(12345u, api.ReadVbattMillivolts(dev));

        var ch = api.Connect(dev, J2534Protocol.Iso15765Ps, 0, 125_000);
        api.SetConfig(ch, new (uint, uint)[] { (J2534Config.J1962Pins, 0x030B), (J2534Config.Loopback, 0) });
        Assert.Equal(2u, Sym<U0>(so, "Stub_ConfigCount")());
        Assert.Equal((J2534Config.J1962Pins, 0x030Bu), (Sym<U1>(so, "Stub_ConfigParam")(0), Sym<U1>(so, "Stub_ConfigValue")(0)));
        Assert.Equal((J2534Config.Loopback, 0u), (Sym<U1>(so, "Stub_ConfigParam")(1), Sym<U1>(so, "Stub_ConfigValue")(1)));

        var f = api.StartMsgFilter(ch, J2534Filter.FlowControl, new PassThruMsg(6, 0, 0, 0, PassThruMsg.IdBytes(0x7FF)), new PassThruMsg(6, 0, 0, 0, PassThruMsg.IdBytes(0x72E)), new PassThruMsg(6, 0, 0, 0, PassThruMsg.IdBytes(0x726)));
        var filter = Sym<U1>(so, "Stub_Filter");
        Assert.Equal((3u, 0x7FFu, 0x72Eu, 0x726u), (filter(0), filter(1), filter(2), filter(3)));

        Assert.Empty(api.ReadMsgs(ch, 4, 10)); // ERR_BUFFER_EMPTY is "no messages", not an error
        api.WriteMsgs(ch, new[] { PassThruMsg.Iso15765(J2534Protocol.Iso15765Ps, 0x726, new byte[] { 0x22, 0xDE, 0x00 }), PassThruMsg.Iso15765(J2534Protocol.Iso15765Ps, 0x7E0, new byte[] { 0x3E, 0x00 }) }, 100);
        var got = api.ReadMsgs(ch, 8, 10);
        Assert.Equal(2, got.Count);
        Assert.Equal((0x72Eu, 1234u), (got[0].CanId, got[0].Timestamp));
        Assert.Equal(new byte[] { 0x62, 0xDE, 0x00 }, got[0].Payload);
        Assert.Equal(0x7E8u, got[1].CanId);
        Assert.Equal(new byte[] { 0x7E, 0x00 }, got[1].Payload);

        api.StopMsgFilter(ch, f);
        var ex = Assert.Throws<J2534Exception>(() => api.StopMsgFilter(ch, 99));
        Assert.Equal(0x16u, ex.Code);
        api.Disconnect(ch);
        api.Close(dev);
    }

    [Fact]
    public void Errors_carry_the_driver_text()
    {
        if (Stub.Value is not string so) return;
        using var api = new NativeJ2534Api(so);
        var dev = api.Open(null);
        var ex = Assert.Throws<J2534Exception>(() => api.Connect(dev, 99, 0, 500_000));
        Assert.Equal(0x03u, ex.Code);
        Assert.Contains("ERR_INVALID_PROTOCOL_ID", ex.Message);
        Assert.Contains("protocol 99 is not supported", ex.Message);
    }

    [Fact]
    public async Task Uds_over_the_channel_over_the_native_stub()
    {
        if (Stub.Value is not string so) return;
        using var api = new NativeJ2534Api(so);
        var dev = api.Open(null);
        await using var ch = new J2534DiagChannel(api, dev) { DefaultTimeout = TimeSpan.FromMilliseconds(300) };
        var r = await new UdsClient(ch, 0x7E0).TesterPresentAsync();
        Assert.Equal(new byte[] { 0x7E, 0x00 }, r);
    }

    [Fact]
    public void A_missing_library_or_a_non_j2534_library_is_reported()
    {
        Assert.ThrowsAny<Exception>(() => new NativeJ2534Api(Path.Combine(Path.GetTempPath(), "no-such-j2534.dll")));
        if (!OperatingSystem.IsLinux()) return;
        var ex = Assert.Throws<InvalidOperationException>(() => new NativeJ2534Api("libc.so.6"));
        Assert.Contains("not a J2534 v04.04 driver", ex.Message);
    }
}
