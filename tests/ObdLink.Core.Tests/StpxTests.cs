using ObdLink.Core.Adapters;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.22.5: multi-frame requests go out through STPX (adapter-side ISO-TP) on STN adapters, with host-side fallback.</summary>
public class StpxTests
{
    private static async Task<(SimulatedAdapter Sim, AdapterSession S, ElmDiagChannel Ch)> Connect(SimProfile p, Action<SimulatedAdapter>? tweak = null)
    {
        var sim = new SimulatedAdapter { Profile = p };
        tweak?.Invoke(sim);
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()), initial: AdapterProfiles.ForPort(PortKind.Usb));
        await s.InitializeAsync();
        return (sim, s, new ElmDiagChannel(s));
    }

    private static readonly byte[] Data = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

    [Fact]
    public async Task Ex_sends_a_long_request_as_one_stpx_message_and_registers_the_ford_pair()
    {
        var (sim, s, ch) = await Connect(SimProfile.Ex);
        Assert.Equal(4095, s.StpxMaxBytes);
        var uds = new UdsClient(ch, 0x726);
        await uds.WriteDataByIdentifierAsync(0x4045, Data);
        Assert.Equal(1, ch.StpxSends);
        Assert.Equal(0, ch.HostSegmentedSends);
        Assert.Equal(1, sim.StpxMessages);
        Assert.Contains("726,72E", sim.FlowControlPairs);
        Assert.Contains(sim.Sent, x => x.Item2 == "2E4045" + Convert.ToHexString(Data));
        Assert.DoesNotContain(sim.Sent, x => x.Item2.StartsWith("10", StringComparison.Ordinal) && x.Item2.Length == 16); // no host first frame

        await uds.WriteDataByIdentifierAsync(0x4045, Data);
        Assert.Single(sim.FlowControlPairs); // once per connection and target
    }

    [Fact]
    public async Task Plain_elm_keeps_host_side_segmentation()
    {
        var (sim, s, ch) = await Connect(SimProfile.GenericElm);
        Assert.Equal(0, s.StpxMaxBytes);
        await new UdsClient(ch, 0x726).WriteDataByIdentifierAsync(0x4045, Data);
        Assert.Equal(0, ch.StpxSends);
        Assert.Equal(1, ch.HostSegmentedSends);
        Assert.Equal(0, sim.StpxMessages);
    }

    [Fact]
    public async Task Refused_stpx_falls_back_and_stays_off_for_the_connection()
    {
        var (sim, s, ch) = await Connect(SimProfile.MxPlus, a => a.StpxSupported = false);
        var uds = new UdsClient(ch, 0x726);
        await uds.WriteDataByIdentifierAsync(0x4045, Data);     // STPX "?" → host-side, same request still delivered
        Assert.True(s.StpxDisabled);
        Assert.Equal(1, ch.HostSegmentedSends);
        await uds.WriteDataByIdentifierAsync(0x4045, Data);
        Assert.Equal(2, ch.HostSegmentedSends);
        Assert.Equal(1, sim.StpxCommands); // not tried again after the "?"
    }

    [Fact]
    public async Task A_full_4095_byte_transfer_block_is_one_stpx_message()
    {
        var (sim, s, ch) = await Connect(SimProfile.Ex);
        var max = new byte[4095]; max[0] = 0x36; max[1] = 0x01;
        await ch.SetTargetAsync(0x7E0, 0x7E8);
        await ch.RequestAsync(max);
        Assert.Equal(1, ch.StpxSends);
        Assert.Equal(0, ch.HostSegmentedSends);
        Assert.Equal(1, sim.StpxMessages);
    }

    [Fact]
    public async Task User_switch_turns_stpx_off()
    {
        var (sim, s, ch) = await Connect(SimProfile.Ex);
        s.StpxDisabled = true;
        await new UdsClient(ch, 0x726).WriteDataByIdentifierAsync(0x4045, Data);
        Assert.Equal(0, sim.StpxMessages);
        Assert.Equal(1, ch.HostSegmentedSends);
    }

    [Fact]
    public void Data_prompt_is_recognised_with_or_without_a_line_break()
    {
        Assert.True(ElmLink.IsDataPrompt(ElmLink.Parse("STPX l:10", "DATA>")));
        Assert.True(ElmLink.IsDataPrompt(ElmLink.Parse("STPX l:10", "DATA\r\r>")));
        Assert.False(ElmLink.IsDataPrompt(ElmLink.Parse("STPX l:10", "?\r\r>")));
        Assert.False(ElmLink.IsDataPrompt(ElmLink.Parse("STPX l:10", "OUT OF MEMORY\r\r>")));
    }

    [Fact]
    public void Transfer_estimate_shows_why_adapter_segmentation_matters()
    {
        // 1 MB in 4095-byte blocks with a 30 ms round trip.
        var host = ObdLink.Core.Flash.FlashPreflight.EstimateTransfer(1_048_576, 4095, 30, adapterSegmentation: false);
        var stpx = ObdLink.Core.Flash.FlashPreflight.EstimateTransfer(1_048_576, 4095, 30, adapterSegmentation: true);
        Assert.InRange(host.TotalHours, 4, 6);       // ~150k frames x 130 ms
        Assert.InRange(stpx.TotalMinutes, 3, 6);      // 257 blocks x (30 ms + ~0.7 s of hex text)
        Assert.True(host > ObdLink.Core.Flash.FlashPreflight.LongFlash);
        Assert.True(stpx < ObdLink.Core.Flash.FlashPreflight.LongFlash);
    }
}
