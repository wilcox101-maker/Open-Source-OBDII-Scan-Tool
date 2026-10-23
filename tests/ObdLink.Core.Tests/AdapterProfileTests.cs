using ObdLink.Core.Adapters;
using ObdLink.Core.Elm;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

public class AdapterProfileTests
{
    static async Task<AdapterSession> Connect(SimProfile p, PortKind port = PortKind.Unknown)
    {
        var s = new AdapterSession(new ElmLink(new SimulatedAdapter { Profile = p }.CreateTransport()), initial: AdapterProfiles.ForPort(port));
        await s.InitializeAsync();
        return s;
    }

    [Theory]
    [InlineData(null, "STN2231 v5.10.3", "OBDLink EX r2.2.1", AdapterKind.ObdLinkEx)]      // Dale's real EX (from log)
    [InlineData(null, "STN2232 v5.13.0", null, AdapterKind.ObdLinkEx)]
    [InlineData(null, "STN2255 v5.10.3", "OBDLink MX+ r1.0", AdapterKind.ObdLinkMxPlus)]
    [InlineData(null, "STN2256 v5.x", null, AdapterKind.ObdLinkMxPlus)]
    [InlineData(null, "STN1155 v4", "OBDLink LX", AdapterKind.OtherStn)]
    [InlineData(null, null, null, AdapterKind.GenericElm)]
    public void Detects_kind(string? _, string? sti, string? stdi, AdapterKind expected) =>
        Assert.Equal(expected, AdapterProfiles.Detect(new AdapterInfo("ELM327 v1.4b", sti, stdi, 12.5)).Kind);

    [Fact]
    public async Task Sim_EX_detected_with_fast_timeouts_and_ms_can()
    {
        var s = await Connect(SimProfile.Ex, PortKind.Usb);
        Assert.Equal(AdapterKind.ObdLinkEx, s.Profile.Kind);
        Assert.True(s.Profile.Has(AdapterCapabilities.MsCan | AdapterCapabilities.HighUartBaud));
        Assert.True(s.Link.DefaultTimeout < AdapterProfiles.ObdLinkMxPlus.CommandTimeout);
        await s.SelectProtocolAsync(StnProtocol.MsCan11_125);
        Assert.Equal(StnProtocol.MsCan11_125, s.CurrentProtocol);
    }

    [Fact]
    public async Task Sim_MXplus_detected_with_bt_timeouts_and_ms_can()
    {
        var s = await Connect(SimProfile.MxPlus, PortKind.Bluetooth);
        Assert.Equal(AdapterKind.ObdLinkMxPlus, s.Profile.Kind);
        Assert.Equal(TimeSpan.FromSeconds(4), s.Link.DefaultTimeout);
        Assert.False(s.Profile.Has(AdapterCapabilities.HighUartBaud));
        await s.SelectProtocolAsync(StnProtocol.MsCan11_125);
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
    }

    [Fact]
    public async Task Generic_elm_has_no_stp_or_mscan()
    {
        var s = await Connect(SimProfile.GenericElm);
        Assert.Equal(AdapterKind.GenericElm, s.Profile.Kind);
        await Assert.ThrowsAsync<NotSupportedException>(() => s.SelectProtocolAsync(StnProtocol.MsCan11_125));
    }

    [Fact]
    public async Task Other_stn_refuses_mscan()
    {
        var s = new AdapterSession(new ElmLink(new MockTransport(c => c.Replace(" ", "") switch
        {
            "ATZ" or "ATI" => "ELM327 v1.4b", "STI" => "STN1155 v4.2", "STDI" => "OBDLink LX", "ATRV" => "12.1V", _ => "OK",
        })));
        await s.InitializeAsync();
        await Assert.ThrowsAsync<NotSupportedException>(() => s.SelectProtocolAsync(StnProtocol.MsCan11_125));
    }

    [Fact]
    public async Task Baud_probe_finds_EX_at_2Mbps_after_failures()
    {
        var sim = new SimulatedAdapter { Profile = SimProfile.Ex, UartBaud = 2000000 };
        var tried = new List<int>();
        var r = await BaudProber.ProbeAsync(b => { tried.Add(b); return sim.CreateTransportAtBaud(b); }, AdapterProfiles.ObdLinkEx.BaudCandidates, TimeSpan.FromMilliseconds(300));
        Assert.NotNull(r);
        Assert.Equal(2000000, r!.Value.Baud);
        Assert.Equal(new[] { 115200, 2000000 }, tried);
    }

    [Fact]
    public async Task Baud_probe_returns_null_when_nothing_answers()
    {
        var sim = new SimulatedAdapter { Profile = SimProfile.Ex, UartBaud = 12345 };
        Assert.Null(await BaudProber.ProbeAsync(sim.CreateTransportAtBaud, new[] { 115200, 500000 }, TimeSpan.FromMilliseconds(200)));
    }

    [Fact]
    public void Bluetooth_profile_baud_is_nominal_single_candidate() => Assert.Single(AdapterProfiles.ObdLinkMxPlus.BaudCandidates);

    [Theory]
    [InlineData("COM4", "USB Serial Port (COM4)", @"FTDIBUS\VID_0403+PID_6015+XXXXXXXX\0000", PortKind.Usb)]
    [InlineData("COM7", "Standard Serial over Bluetooth link (COM7)", @"BTHENUM\{00001101-0000-1000-8000-00805F9B34FB}_LOCALMFG&0002\7&XXXX", PortKind.Bluetooth)]
    [InlineData("COM3", "Communications Port (COM3)", @"ACPI\PNP0501\0", PortKind.Unknown)]
    [InlineData("COM9", "OBDLink EX USB (COM9)", null, PortKind.Usb)]
    public void Classifies_ports(string port, string friendly, string? pnp, PortKind k)
    {
        var p = PortEnumerator.Classify(port, friendly, pnp);
        Assert.Equal(k, p.Kind);
        Assert.Contains(port, p.Label);
    }

    [Fact]
    public void Port_listing_does_not_throw_off_windows() => Assert.NotNull(PortEnumerator.List());

    [Fact]
    public void Capability_text_lists_features() =>
        Assert.Equal("HS-CAN, MS-CAN, J1850 PWM, ST cmds, HighUartBaud", AdapterProfiles.ObdLinkEx.CapabilityText);
}
