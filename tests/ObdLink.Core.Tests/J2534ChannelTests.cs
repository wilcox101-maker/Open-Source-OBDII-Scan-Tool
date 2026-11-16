using System.Runtime.InteropServices;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.J2534;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.18.0: J2534 channel (ISO 15765 via a pass-thru device), bus setups, discovery, DLL bitness.</summary>
public class J2534ChannelTests
{
    private static (SimulatedJ2534Api Api, J2534DiagChannel Ch) Make(Func<SimulatedJ2534Api.Channel, uint, byte[], IEnumerable<SimReply>>? responder = null)
    {
        var api = new SimulatedJ2534Api();
        if (responder != null) api.Responder = responder;
        var dev = api.Open(null);
        return (api, new J2534DiagChannel(api, dev) { DefaultTimeout = TimeSpan.FromMilliseconds(500) });
    }

    /// <summary>A module at <paramref name="tx"/> that answers 22 F1 90 with a VIN-like payload and 3E 00 with 7E 00, on HS-CAN only.</summary>
    private static IEnumerable<SimReply> Pcm(SimulatedJ2534Api.Channel ch, uint id, byte[] req)
    {
        if (ch.Protocol != J2534Protocol.Iso15765 || ch.Baud != 500_000) yield break;
        if (id is 0x7E0 && req is [0x3E, 0x00]) yield return new SimReply(0x7E8, new byte[] { 0x7E, 0x00 });
        if (id is 0x7E0 && req is [0x22, 0xF1, 0x90]) yield return new SimReply(0x7E8, new byte[] { 0x62, 0xF1, 0x90 }.Concat("1FTEW1EP0HKD00000"u8.ToArray()).ToArray());
        if (id is 0x7DF && req is [0x01, 0x00])
        {
            yield return new SimReply(0x7E8, new byte[] { 0x41, 0x00, 0xBE, 0x3F, 0xB8, 0x13 });
            yield return new SimReply(0x7E9, new byte[] { 0x41, 0x00, 0x98, 0x18, 0x00, 0x01 }, DelayMs: 20);
        }
    }

    [Fact]
    public async Task Hs_can_setup_and_physical_flow_control_filter()
    {
        var (api, ch) = Make(Pcm);
        await ch.SetTargetAsync(0x7E0, 0x7E8);
        var c = Assert.Single(api.Channels.Values);
        Assert.Equal((J2534Protocol.Iso15765, 500_000u, (uint?)null), (c.Protocol, c.Baud, c.Pins));
        var f = Assert.Single(c.Filters.Values);
        Assert.Equal((J2534Filter.FlowControl, 0x7FFu, 0x7E8u, (uint?)0x7E0), (f.Type, f.Mask, f.Pattern, f.Flow));
        Assert.Contains("SetConfig 3=0", api.Calls); // loopback off
    }

    [Fact]
    public async Task Request_writes_id_and_payload_with_padding_and_returns_the_reply()
    {
        var (api, ch) = Make(Pcm);
        await ch.SetTargetAsync(0x7E0, 0x7E8);
        var r = await ch.RequestAsync(new byte[] { 0x22, 0xF1, 0x90 });
        var w = Assert.Single(api.Written).Msg;
        Assert.Equal(new byte[] { 0, 0, 0x07, 0xE0, 0x22, 0xF1, 0x90 }, w.Data);
        Assert.Equal(J2534TxFlags.Iso15765FramePad, w.TxFlags);
        var d = Assert.Single(r); // the TX indication and the first-frame indication are skipped
        Assert.Equal(0x7E8u, d.SourceId);
        Assert.Equal(20, d.Payload.Length);
        Assert.Contains("Ioctl ClearRxBuffer", api.Calls);
    }

    [Fact]
    public async Task Functional_request_collects_every_ecu()
    {
        var (_, ch) = Make(Pcm);
        await ch.SetTargetAsync(0x7DF, null);
        var r = await ch.RequestAsync(new byte[] { 0x01, 0x00 });
        Assert.Equal(new uint[] { 0x7E8, 0x7E9 }, r.Select(x => x.SourceId).OrderBy(x => x));
    }

    [Fact]
    public async Task Functional_target_has_the_eight_obd_flow_control_pairs()
    {
        var (api, ch) = Make();
        await ch.SetTargetAsync(0x7DF, null);
        var filters = api.Channels.Values.Single().Filters.Values.OrderBy(f => f.Pattern).ToList();
        Assert.Equal(8, filters.Count);
        Assert.Equal(Enumerable.Range(0, 8).Select(i => ((uint)(0x7E8 + i), (uint?)(uint)(0x7E0 + i))), filters.Select(f => (f.Pattern, f.Flow)));
    }

    [Fact]
    public async Task Changing_target_replaces_the_filters()
    {
        var (api, ch) = Make();
        await ch.SetTargetAsync(0x7E0, 0x7E8);
        await ch.SetTargetAsync(0x726, 0x72E);
        var f = Assert.Single(api.Channels.Values.Single().Filters.Values);
        Assert.Equal(0x72Eu, f.Pattern);
    }

    [Fact]
    public async Task No_reply_is_an_empty_list_after_the_timeout()
    {
        var (_, ch) = Make(Pcm);
        await ch.SetTargetAsync(0x726, 0x72E);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Assert.Empty(await ch.RequestAsync(new byte[] { 0x3E, 0x00 }, TimeSpan.FromMilliseconds(150)));
        Assert.True(sw.ElapsedMilliseconds >= 120);
    }

    [Fact]
    public async Task Ms_can_uses_iso15765_ps_on_pins_3_11_at_125k()
    {
        var (api, ch) = Make();
        await ch.SelectBusAsync(Bus.MsCan);
        var c = Assert.Single(api.Channels.Values);
        Assert.Equal((J2534Protocol.Iso15765Ps, 125_000u, (uint?)0x030B), (c.Protocol, c.Baud, c.Pins));
        await ch.SelectBusAsync(Bus.HsCan2);
        c = Assert.Single(api.Channels.Values); // the MS channel was closed
        Assert.Equal((J2534Protocol.Iso15765Ps, 500_000u, (uint?)0x030B), (c.Protocol, c.Baud, c.Pins));
        Assert.Equal(Bus.HsCan2, ch.CurrentBus);
    }

    [Fact]
    public async Task Device_without_pin_switching_refuses_ms_can()
    {
        var (api, ch) = Make();
        api.PinSwitching = false;
        var ex = await Assert.ThrowsAsync<J2534Exception>(() => ch.SelectBusAsync(Bus.MsCan));
        Assert.Contains("ERR_INVALID_PROTOCOL_ID", ex.Message);
        Assert.Null(ch.CurrentBus);
    }

    [Fact]
    public async Task Uds_waits_for_the_final_answer_after_pending_without_resending()
    {
        var (api, ch) = Make((c, id, req) => id == 0x7E0 && req[0] == 0x31
            ? new[] { new SimReply(0x7E8, new byte[] { 0x7F, 0x31, 0x78 }), new SimReply(0x7E8, new byte[] { 0x71, 0x01, 0x12, 0x34 }, DelayMs: 150) }
            : Array.Empty<SimReply>());
        var uds = new UdsClient(ch, 0x7E0);
        var r = await uds.StartRoutineAsync(0x1234);
        Assert.Equal(0x1234, r.RoutineId);
        Assert.Single(api.Written); // one request on the bus (the ELM path cannot do this)
    }

    [Fact]
    public async Task Uds_end_to_end_read_did()
    {
        var (_, ch) = Make(Pcm);
        var vin = await new UdsClient(ch, 0x7E0).ReadDataByIdentifierAsync(0xF190);
        Assert.Equal("1FTEW1EP0HKD00000", System.Text.Encoding.ASCII.GetString(vin));
    }

    [Fact]
    public async Task Request_before_target_is_refused_and_29_bit_is_not_supported()
    {
        var (_, ch) = Make();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ch.RequestAsync(new byte[] { 0x3E, 0x00 }));
        await Assert.ThrowsAsync<NotSupportedException>(() => ch.SetTargetAsync(0x18DA10F1, 0x18DAF110));
    }

    [Fact]
    public async Task Dispose_disconnects()
    {
        var (api, ch) = Make();
        await ch.SetTargetAsync(0x7E0, 0x7E8);
        await ch.DisposeAsync();
        Assert.Empty(api.Channels);
        Assert.Contains(api.Calls, c => c.StartsWith("Disconnect"));
    }

    // ---- Discovery and DLL bitness ---------------------------------------------------------------------------------

    private sealed class FakeRegistry(params J2534RegistryKey[] keys) : IJ2534RegistrySource
    {
        public IEnumerable<J2534RegistryKey> PassThruKeys() => keys;
    }

    [Fact]
    public void Registry_devices_are_listed_once_with_their_protocols()
    {
        var vcm = new Dictionary<string, object?> { ["Name"] = "VCM II", ["Vendor"] = "Bosch", ["FunctionLibrary"] = @"C:\x\vcm.dll", ["CAN"] = 1, ["ISO15765"] = 1 };
        var devs = J2534Registry.Enumerate(new FakeRegistry(
            new J2534RegistryKey(true, "VCMII", vcm),
            new J2534RegistryKey(false, "VCMII", vcm),                                      // same DLL in the 64-bit view
            new J2534RegistryKey(true, "Broken", new Dictionary<string, object?> { ["Name"] = "No DLL" }),
            new J2534RegistryKey(false, "Mongoose", new Dictionary<string, object?> { ["functionlibrary"] = @"C:\y\mgp.dll", ["CAN"] = 1, ["ISO15765"] = 0 })));
        Assert.Equal(2, devs.Count);
        Assert.Equal(("VCM II", "Bosch", true, true, true), (devs[0].Name, devs[0].Vendor, devs[0].Can, devs[0].Iso15765, devs[0].In32BitView));
        Assert.Equal(("Mongoose", false), (devs[1].Name, devs[1].Iso15765)); // key name when Name is missing
        Assert.Equal("VCM II (Bosch)", devs[0].Display);
    }

    [Fact]
    public void Windows_registry_source_is_empty_elsewhere()
    {
        if (!OperatingSystem.IsWindows()) Assert.Empty(new WindowsJ2534Registry().PassThruKeys());
    }

    private static MemoryStream Pe(ushort machine)
    {
        var b = new byte[0x100];
        b[0] = (byte)'M'; b[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(b, 0x3C);
        "PE\0\0"u8.ToArray().CopyTo(b, 0x80);
        BitConverter.GetBytes(machine).CopyTo(b, 0x84);
        return new MemoryStream(b);
    }

    [Theory]
    [InlineData((ushort)0x014C, DllMachine.X86)]
    [InlineData((ushort)0x8664, DllMachine.X64)]
    [InlineData((ushort)0xAA64, DllMachine.Arm64)]
    [InlineData((ushort)0x01C4, DllMachine.Unknown)]
    public void Reads_the_pe_machine(ushort machine, DllMachine expected) => Assert.Equal(expected, DllInfo.Machine(Pe(machine)));

    [Fact]
    public void Not_a_pe_file_is_unknown()
    {
        Assert.Equal(DllMachine.Unknown, DllInfo.Machine(new MemoryStream(new byte[] { 1, 2, 3 })));
        Assert.Equal(DllMachine.Unknown, DllInfo.Machine(new MemoryStream("MZ"u8.ToArray())));
    }

    [Theory]
    [InlineData(Architecture.X64, DllMachine.X86, false)]   // the usual case: a 32-bit J2534 driver in the 64-bit app
    [InlineData(Architecture.X64, DllMachine.X64, true)]
    [InlineData(Architecture.X86, DllMachine.X86, true)]
    [InlineData(Architecture.X86, DllMachine.X64, false)]
    [InlineData(Architecture.X64, DllMachine.Unknown, false)]
    public void In_process_loading(Architecture proc, DllMachine dll, bool ok) => Assert.Equal(ok, DllInfo.LoadsInProcess(dll, proc));
}
