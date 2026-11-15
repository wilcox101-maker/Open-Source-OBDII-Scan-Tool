using ObdLink.Core.Adapters;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.9.1: BusRouter, extracted from MainViewModel.OnBus/OnBusCore. Simulator modules: 0x726 on all buses, 0x727 MS-only, 0x999 absent.</summary>
public class BusRouterTests
{
    private sealed record Target(Bus Bus, Bus DefaultBus, uint TxId, string Acronym, bool FoundConfirmed = true) : IBusTarget;

    private static async Task<(SimulatedAdapter sim, AdapterSession s)> Connect(AdapterProfile profile)
    {
        var sim = new SimulatedAdapter();
        var s = new AdapterSession(new ElmLink(sim.CreateTransport()), initial: profile);
        await s.InitializeAsync();
        await s.SelectProtocolAsync(StnProtocol.HsCan11_500);
        return (sim, s);
    }

    private static Func<Task> Ping(AdapterSession s, uint tx) => () => new UdsClient(new ElmDiagChannel(s), tx).TesterPresentAsync();

    [Fact]
    public async Task First_try_success_reports_the_bus_and_runs_once()
    {
        var (sim, s) = await Connect(AdapterProfiles.ObdLinkMxPlus);
        var router = new BusRouter(s, () => true);
        var answered = new List<Bus>();
        var bus = await router.RunAsync(new Target(Bus.HsCan, Bus.HsCan, 0x726, "BCM"), Ping(s, 0x726), onBusAnswered: answered.Add);
        Assert.Equal(Bus.HsCan, bus);
        Assert.Equal(new[] { Bus.HsCan }, answered);
        Assert.DoesNotContain(sim.Sent, x => x.Request.StartsWith("STP 53") || x.Request == "53");
    }

    [Fact]
    public async Task No_reply_on_hs_falls_back_to_a_switchable_bus()
    {
        var (_, s) = await Connect(AdapterProfiles.ObdLinkMxPlus);
        var router = new BusRouter(s, () => true);
        Bus? answered = null;
        var bus = await router.RunAsync(new Target(Bus.HsCan, Bus.HsCan, 0x727, "ACM"), Ping(s, 0x727), onBusAnswered: b => answered = b);
        // Answers on a switchable bus (HS-CAN2 and MS-CAN share the STP53 preset); not HS-CAN.
        Assert.Contains(bus, new[] { Bus.HsCan2, Bus.MsCan });
        Assert.Equal(bus, answered);
        Assert.Equal(StnProtocol.HsCan11_500, s.CurrentProtocol); // default restored after a non-HS bus
    }

    [Fact]
    public async Task No_reply_anywhere_throws_and_never_reports_a_bus()
    {
        var (_, s) = await Connect(AdapterProfiles.ObdLinkMxPlus);
        var router = new BusRouter(s, () => true);
        bool reported = false;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.RunAsync(new Target(Bus.HsCan, Bus.HsCan, 0x999, "XYZ", FoundConfirmed: false), Ping(s, 0x999), onBusAnswered: _ => reported = true));
        Assert.Contains("XYZ", ex.Message);
        Assert.False(reported);
        Assert.Equal(StnProtocol.HsCan11_500, s.CurrentProtocol);
    }

    [Fact]
    public async Task Fallback_disabled_tries_only_the_first_bus()
    {
        var (sim, s) = await Connect(AdapterProfiles.ObdLinkMxPlus);
        var router = new BusRouter(s, () => true);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.RunAsync(new Target(Bus.HsCan, Bus.HsCan, 0x727, "ACM"), Ping(s, 0x727), allowFallback: false));
        Assert.DoesNotContain(sim.Sent, x => x.Request.StartsWith("STP 53")); // never switched off HS-CAN
    }

    [Fact]
    public async Task Non_hs_bus_on_an_adapter_that_cannot_switch_throws_before_any_traffic()
    {
        var (sim, s) = await Connect(AdapterProfiles.GenericElm);
        var router = new BusRouter(s, () => false);
        sim.Sent.Clear();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            router.RunAsync(new Target(Bus.MsCan, Bus.MsCan, 0x727, "ACM"), Ping(s, 0x727)));
        Assert.Contains("cannot use the MS-CAN pins", ex.Message);
        Assert.Empty(sim.Sent); // no request reached the vehicle
    }

    [Fact]
    public async Task Hs_fast_path_does_not_reselect_when_already_on_hs()
    {
        var (sim, s) = await Connect(AdapterProfiles.ObdLinkMxPlus);
        sim.Sent.Clear();
        await new BusRouter(s, () => true).RunAsync(new Target(Bus.HsCan, Bus.HsCan, 0x726, "BCM"), Ping(s, 0x726));
        Assert.DoesNotContain(sim.Sent, x => x.Request.StartsWith("STP")); // already on HS-CAN 500k, so no protocol command
    }
}
