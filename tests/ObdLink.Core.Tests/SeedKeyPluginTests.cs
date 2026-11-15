using ObdLink.Core.AsBuilt;
using ObdLink.Core.Safety;
using ObdLink.Core.Security;
using ObdLink.Core.Uds;
using ObdLink.SeedKey.Sample;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>v0.7.0: user-supplied seed/key plug-ins. The project ships no algorithms; only approved files load.</summary>
public class SeedKeyPluginTests
{
    private static string SampleDll => typeof(DemoSeedKeyProvider).Assembly.Location;

    private static string NewDir() { var d = Path.Combine(Path.GetTempPath(), "seedkey_" + Guid.NewGuid()); Directory.CreateDirectory(d); return d; }

    [Fact]
    public void Disabled_loads_nothing_even_when_approved()
    {
        var dir = NewDir();
        var (_, sha) = SeedKeyPlugins.Install(SampleDll, dir);
        var (p, files) = SeedKeyPlugins.Load(dir, new SeedKeyOptions { Enabled = false, TrustedSha256 = { sha } });
        Assert.Empty(p.Providers);
        Assert.Empty(files);
    }

    [Fact]
    public void Unapproved_file_is_listed_but_not_loaded()
    {
        var dir = NewDir();
        SeedKeyPlugins.Install(SampleDll, dir);
        var (p, files) = SeedKeyPlugins.Load(dir, new SeedKeyOptions { Enabled = true });
        Assert.Empty(p.Providers);
        Assert.False(Assert.Single(files).Trusted);
        Assert.Contains("not approved", files[0].Summary);
    }

    [Fact]
    public void Approved_file_loads_its_providers()
    {
        var dir = NewDir();
        var (dest, sha) = SeedKeyPlugins.Install(SampleDll, dir);
        Assert.StartsWith(dir, dest);
        var (p, files) = SeedKeyPlugins.Load(dir, new SeedKeyOptions { Enabled = true, TrustedSha256 = { sha.ToLowerInvariant() } });
        Assert.Single(p.Providers);
        Assert.Contains("DemoSeedKeyProvider", Assert.Single(files).Summary);
    }

    [Fact]
    public void Changed_file_needs_approval_again()
    {
        var dir = NewDir();
        var (dest, sha) = SeedKeyPlugins.Install(SampleDll, dir);
        File.AppendAllText(dest, "tamper");
        var (p, files) = SeedKeyPlugins.Load(dir, new SeedKeyOptions { Enabled = true, TrustedSha256 = { sha } });
        Assert.Empty(p.Providers);
        Assert.False(files.Single().Trusted);
    }

    [Fact]
    public void Install_refuses_non_dll() => Assert.Throws<ArgumentException>(() => SeedKeyPlugins.Install("evil.exe", NewDir()));

    [Fact]
    public void Options_normalize_drops_bad_levels_and_hashes()
    {
        var o = new SeedKeyOptions { AsBuiltLevel = 0x02, TrustedSha256 = { "nothex", new string('a', 64), new string('A', 64) } }.Normalize();
        Assert.Null(o.AsBuiltLevel);
        Assert.Equal(new[] { new string('A', 64) }, o.TrustedSha256);
    }

    private sealed class Throws : ISeedKeyProvider { public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] s, CancellationToken ct = default) => throw new InvalidOperationException("boom"); }
    private sealed class Wrong : ISeedKeyProvider { public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] s, CancellationToken ct = default) => Task.FromResult<byte[]?>(new byte[] { 1, 2, 3 }); }

    [Fact]
    public async Task Composite_skips_a_throwing_provider()
    {
        var c = new CompositeSeedKeyProvider(new ISeedKeyProvider[] { new Throws(), new DemoSeedKeyProvider() });
        Assert.Equal(new byte[] { 0x33, 0x22, 0x11 }, await c.ComputeKeyAsync(0x726, 1, new byte[] { 0x11, 0x22, 0x33 }));
        Assert.Null(await c.ComputeKeyAsync(0x726, 1, new byte[] { 0x99 })); // sample never guesses for a real seed
    }

    private static async Task<(SimulatedAdapterHolder h, AsBuiltService svc)> SecuredBcm(ISeedKeyProvider? provider, byte? level)
    {
        var (sim, s, ch) = await AsBuiltServiceSafetyTests.Connect();
        sim.Dids[0x726] = new() { [0xDE00] = new byte[] { 1, 2, 3, 4, 5, 6, 7 } };
        sim.SecuredModules.Add(0x726);
        var dir = NewDir();
        var svc = new AsBuiltService(m => new UdsClient(ch, m) { SeedKey = provider ?? new NullSeedKeyProvider() }, new SafetyGuard(s, new AutoConfirm(true)), dir) { SecurityLevel = level };
        return (new SimulatedAdapterHolder(sim), svc);
    }
    private sealed record SimulatedAdapterHolder(ObdLink.Core.Transport.SimulatedAdapter Sim);

    private static readonly byte[] NewData = { 9, 9, 9, 9, 9, 9, 9 };
    private static AsBuiltBlock Bcm01 => new() { Module = 0x726, Block = 1, Data = NewData };

    [Fact]
    public async Task Secured_write_without_unlock_explains_what_to_do()
    {
        var (h, svc) = await SecuredBcm(null, null);
        var ex = await Assert.ThrowsAsync<SafetyException>(() => svc.WriteBlockAsync("1FTFW1E80HFA00001", Bcm01));
        Assert.Contains("security access", ex.Message);
        Assert.Contains("Settings", ex.Message);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7 }, h.Sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Secured_write_with_loaded_plugin_succeeds()
    {
        var dir = NewDir();
        var (_, sha) = SeedKeyPlugins.Install(SampleDll, dir);
        var (p, _) = SeedKeyPlugins.Load(dir, new SeedKeyOptions { Enabled = true, TrustedSha256 = { sha } });
        var (h, svc) = await SecuredBcm(p, 0x01);
        Assert.True((await svc.WriteBlockAsync("1FTFW1E80HFA00001", Bcm01)).Written);
        Assert.Equal(NewData, h.Sim.Dids[0x726][0xDE00]);
    }

    [Fact]
    public async Task Level_set_but_no_key_writes_nothing()
    {
        var (h, svc) = await SecuredBcm(new NullSeedKeyProvider(), 0x01);
        var ex = await Assert.ThrowsAsync<SafetyException>(() => svc.WriteBlockAsync("1FTFW1E80HFA00001", Bcm01));
        Assert.Contains("no approved seed/key plug-in", ex.Message);
        Assert.DoesNotContain(h.Sim.Sent, x => x.Request.StartsWith("2E"));
    }

    [Fact]
    public async Task Wrong_key_is_sent_once_and_nothing_is_written()
    {
        var (h, svc) = await SecuredBcm(new Wrong(), 0x01);
        var ex = await Assert.ThrowsAsync<UdsNegativeResponseException>(() => svc.WriteBlockAsync("1FTFW1E80HFA00001", Bcm01));
        Assert.Equal(0x35, ex.Nrc);
        Assert.Single(h.Sim.Sent, x => x.Request.StartsWith("2702"));
        Assert.DoesNotContain(h.Sim.Sent, x => x.Request.StartsWith("2E"));
    }
}
