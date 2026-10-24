using ObdLink.Core.Adapters;
using ObdLink.Core.Transport;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

public class V032Tests
{
    private static readonly PortInfo Sim = new("DEMO-MX+", "Simulated OBDLink MX+", PortKind.Virtual);
    private static readonly PortInfo SimEx = new("DEMO-EX", "Simulated OBDLink EX", PortKind.Virtual);
    private static PortInfo Com4 => PortEnumerator.Classify("COM4", "USB Serial Port (COM4)", @"FTDIBUS\VID_0403+PID_6015+DA1234A\0000");

    [Fact]
    public void DalesCom4IsUsbFtdiAndLabeled()
    {
        Assert.Equal(PortKind.Usb, Com4.Kind); Assert.True(Com4.IsFtdi); Assert.False(Com4.IsSimulated);
        Assert.Contains("COM4", Com4.Label); Assert.Contains("OBDLink EX / FTDI", Com4.Label);
        Assert.Contains("DEMO", Sim.Label);
    }

    [Fact]
    public void RealPortPreferredOverSimEvenWhenSimListedFirstOrSavedOrPrevious()
    {
        var ports = new[] { Sim, SimEx, Com4 };
        Assert.Equal("COM4", PortEnumerator.ChooseDefault(ports, null, null, false)!.Name);
        Assert.Equal("COM4", PortEnumerator.ChooseDefault(ports, "DEMO-MX+", "DEMO-MX+", false)!.Name); // sticky SIM from startup/refresh
        Assert.Equal("DEMO-MX+", PortEnumerator.ChooseDefault(ports, "DEMO-MX+", null, true)!.Name);  // explicit choice kept
    }

    [Fact]
    public void SimNeverAutoSelectedWhenNoRealPorts() => Assert.Null(PortEnumerator.ChooseDefault(new[] { Sim, SimEx }, null, "DEMO-MX+", false));

    [Fact]
    public void PreferenceOrder()
    {
        var bt = PortEnumerator.Classify("COM3", "Standard Serial over Bluetooth link (COM3)", @"BTHENUM\{0000}");
        var plain = new PortInfo("COM1", null, PortKind.Unknown);
        Assert.Equal("COM4", PortEnumerator.ChooseDefault(new[] { plain, bt, Com4 }, null, null, false)!.Name);
        Assert.Equal("COM3", PortEnumerator.ChooseDefault(new[] { plain, bt }, null, null, false)!.Name);
        Assert.Equal("COM1", PortEnumerator.ChooseDefault(new[] { plain, bt }, "COM1", null, false)!.Name);
        Assert.Equal("COM3", PortEnumerator.ChooseDefault(new[] { plain, bt, Com4 }, "COM9", "com3", false)!.Name);
    }

    [Fact]
    public void MergeListsAllPortsFromBothSourcesSortedNumerically()
    {
        var l = PortEnumerator.Merge(new[] { "COM10", "com1", "COM1" }, new (string, string?, string?)[] { ("COM4", "USB Serial Port (COM4)", @"FTDIBUS\VID_0403+PID_6015\0000"), ("COM2", "Communications Port (COM2)", @"ACPI\PNP0501\1") });
        Assert.Equal(new[] { "COM1", "COM2", "COM4", "COM10" }, l.Select(p => p.Name));
        Assert.True(l[2].IsFtdi);
        Assert.Empty(PortEnumerator.Merge(Array.Empty<string>(), Array.Empty<(string, string?, string?)>()));
    }

    [Theory]
    [InlineData("COM4", "COM4")] [InlineData(" com4 ", "COM4")] [InlineData("4", "COM4")] [InlineData(@"\\.\COM12", "COM12")]
    [InlineData("COM0", null)] [InlineData("COM999", null)] [InlineData("LPT1", null)] [InlineData("", null)] [InlineData(null, null)] [InlineData("COM4; rm", null)]
    public void NormalizePort(string? input, string? expected) => Assert.Equal(expected, PortEnumerator.NormalizePort(input));

    [Fact]
    public void NoPortsHintMentionsDriverAndDeviceManager()
    { Assert.Contains("FTDI VCP driver", PortEnumerator.NoPortsHint); Assert.Contains("Device Manager", PortEnumerator.NoPortsHint); }

    private static string Src(string f, [System.Runtime.CompilerServices.CallerFilePath] string me = "") =>
        File.ReadAllText(Path.Combine(Path.GetDirectoryName(me)!, "..", "..", "src", "ObdLink.App", f));

    [Fact]
    public void AppNeverFallsBackToSimAndLabelsDemo()
    {
        var vm = Src("MainViewModel.cs");
        Assert.DoesNotContain("SelectedPort ?? DemoMx", vm); Assert.DoesNotContain("?? DemoMx.Name", vm);
        Assert.Contains("PortEnumerator.ChooseDefault", vm);
        var x = Src("MainWindow.xaml");
        Assert.Contains("{Binding IsDemo", x); Assert.Contains("SIMULATED", x); Assert.Contains("UseManualPortCommand", x); Assert.Contains("{Binding PortHint}", x);
    }

    [Fact]
    public void LastRealPortIsRememberedAndPreferredOverSim()
    {
        var ports = new[] { Sim, new PortInfo("COM3", null, PortKind.Unknown), Com4 };
        Assert.Equal("COM3", PortEnumerator.ChooseDefault(ports, null, "COM3", false)!.Name);
        Assert.Equal("COM4", PortEnumerator.ChooseDefault(new[] { Sim, Com4 }, null, "COM9", false)!.Name); // remembered port gone -> FTDI
        Assert.Null(new ObdLink.Core.Safety.AppSettings().Adapter.LastPort);
    }

    [Fact]
    public void WritesEnabledIsSessionOnly()
    {
        var s = new ObdLink.Core.Safety.AppSettings { ReadOnlyTestMode = false };
        Assert.True(s.ApplyStartupSafety()); Assert.True(s.ReadOnlyTestMode);
        Assert.False(s.ApplyStartupSafety());
        var vm = Src("MainViewModel.cs"); Assert.Contains("ApplyStartupSafety()", vm); Assert.Contains("Adapter.LastPort = sel.Name", vm);
    }

    [Theory]
    [InlineData("STN2231 v5.13.1", "OBDLink EX r2.2.1")] [InlineData("STN2231 v5.13.1", null)] [InlineData("STN2230 v5.10.3", null)]
    public void ExDetectionAcceptsStn2231(string sti, string? dev)
    {
        Assert.Equal(AdapterKind.ObdLinkEx, AdapterProfiles.Detect(new ObdLink.Core.Elm.AdapterInfo("ELM327 v1.4b", sti, dev, 12.5)).Kind);
        Assert.Equal(2231 == int.Parse(sti[3..7]) ? "OBDLink-EX-STN2231" : "OBDLink-EX-STN2230", ObdLink.Core.Firmware.FirmwareFeed.ModelOf(int.Parse(sti[3..7])));
    }

    [Fact]
    public void UsbArrivalTriggersRescan() => Assert.Contains("WM_DEVICECHANGE", Src("MainWindow.xaml.cs"));

    [Fact]
    public async Task RealAdapterRequestPathSetsHeaderFilterFlowControlAndTiming()
    {
        var sim = new SimulatedAdapter { Profile = SimProfile.Ex };
        var t = sim.CreateTransport();
        var s = new AdapterSession(new ElmLink(t));
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        var dtcs = await new UdsClient(new ElmDiagChannel(s), 0x720).ReadDtcsByStatusAsync();
        var sent = t.Sent.Select(x => x.Trim().Replace(" ", "").ToUpperInvariant()).ToList();
        int i(string c) => sent.FindIndex(x => x == c);
        Assert.True(i("STP33") >= 0 && i("STPO") > i("STP33"));
        Assert.True(i("ATST96") > i("STPO"));
        Assert.True(i("ATSH720") > 0 && i("ATCRA728") > i("ATSH720") && i("ATFCSH720") > i("ATCRA728"));
        Assert.Contains("ATFCSD300000", sent); Assert.Contains("ATFCSM1", sent);
        Assert.True(sent.FindIndex(x => x.StartsWith("1902")) > i("ATFCSM1"));
    }

    [Fact]
    public void NoReplyMessageIsFriendly()
    {
        var m = ModuleReplies.NoReplyMessage("IPC", 0x720, ObdLink.Core.Elm.Bus.HsCan, scanned: false, triedBoth: true);
        Assert.Contains("0x720", m); Assert.Contains("0x728", m); Assert.Contains("Scan modules", m); Assert.Contains("HS-CAN2", m); Assert.Contains("Events log", m);
        Assert.Contains("ignition", ModuleReplies.NoReplyMessage("IPC", 0x720, ObdLink.Core.Elm.Bus.HsCan, true, false));
    }

    [Fact]
    public void ModuleRowsNeverShowTypeName()
    {
        Assert.Contains("override string ToString() => PickerText", Src("MainViewModel.cs"));
        var c = Src(Path.Combine("Themes", "Controls.xaml"));
        Assert.Contains("ContentTemplateSelector=\"{TemplateBinding ItemTemplateSelector}\"", c);
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(Src("MainWindow.xaml"), @"<ComboBox[^>]*ItemsSource=""\{Binding (Modules|AsBuiltModules)\}""[^>]*>"))
            Assert.Contains("DisplayMemberPath=", m.Value);
    }

    [Fact]
    public void BusFallbackAndScanRetryOnOtherBus()
    {
        var vm = Src("MainViewModel.cs");
        Assert.Contains("BusOverride = alt", vm); Assert.Contains("ModuleReplies.NoReplyMessage", vm); Assert.Contains("ModuleReplies.Alternates", vm);
        Assert.Equal(new[] { ObdLink.Core.Elm.Bus.HsCan2, ObdLink.Core.Elm.Bus.MsCan }, ModuleReplies.Alternates(ObdLink.Core.Elm.Bus.HsCan));
        Assert.True(ModuleReplies.IsNoReply(new DiagLinkException(ElmError.CanError, "CAN ERROR"))); Assert.True(ModuleReplies.IsNoReply(new TimeoutException()));
        Assert.False(ModuleReplies.IsNoReply(new InvalidOperationException()));
    }

    [Fact]
    public async Task HsCan2UsesMsPinsAt500k()
    {
        var sim = new SimulatedAdapter { Profile = SimProfile.Ex };
        var t = sim.CreateTransport(); var s = new AdapterSession(new ElmLink(t));
        await s.InitializeAsync();
        await s.SelectBusAsync(ObdLink.Core.Elm.Bus.HsCan2);
        var sent = t.Sent.Select(x => x.Trim().ToUpperInvariant()).ToList();
        var a = sent.LastIndexOf("STP 53"); Assert.True(a >= 0);
        Assert.True(sent.LastIndexOf("STPBR 500000") > a && sent.LastIndexOf("STPO") > sent.LastIndexOf("STPBR 500000"));
    }

    [Fact]
    public void DemoProfilesAreTaggedAndNotUsedForRealAdapters()
    {
        var legacy = new ObdLink.Core.Vehicles.VehicleProfile { Vin = "1FTDEMX95HF000001", DisplayName = "2017 Ford (USA) truck" };
        Assert.True(legacy.LooksDemo); Assert.StartsWith("[DEMO] ", legacy.Label);
        var byDid = new ObdLink.Core.Vehicles.VehicleProfile { Vin = "1FTFW1E50HFA00000" };
        byDid.Modules.Add(new("IPC", "x", "HsCan", 0x720, 0x728, true, new() { ["F188"] = "DEMO-IPC-1" }, false));
        Assert.True(byDid.LooksDemo);
        var real = new ObdLink.Core.Vehicles.VehicleProfile { Vin = "1FTFW1E50HFA00000" };
        real.Modules.Add(new("IPC", "x", "HsCan2", 0x720, 0x728, true, new() { ["F188"] = "HL3T-14C026-AA" }, false));
        Assert.False(real.LooksDemo); Assert.Equal(ObdLink.Core.Elm.Bus.HsCan2, real.ToModuleDefinitions()[0].Bus);
        var vm = Src("MainViewModel.cs");
        Assert.Contains("ResetVehicleForAdapter(sel.IsSimulated)", vm); Assert.Contains("prof.IsDemo = _connectedSim", vm);
    }

    [Fact]
    public void UdsDtcReadsGetLongerHostTimeout()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), ObdLink.Core.IsoTp.ElmDiagChannel.LongTimeoutFor(new byte[] { 0x19, 0x02, 0xFF }));
        Assert.Null(ObdLink.Core.IsoTp.ElmDiagChannel.LongTimeoutFor(new byte[] { 0x3E, 0x00 }));
    }
}
