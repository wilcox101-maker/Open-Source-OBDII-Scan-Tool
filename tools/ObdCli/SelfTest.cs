using System.Diagnostics;
using ObdLink.Core.Adapters;
using ObdLink.Core.Elm;
using ObdLink.Core.Flash;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Transport;
using ObdLink.Core.Uds;

namespace ObdLink.Cli;

/// <summary>
/// v0.22.5 <c>obdcli selftest</c>: end-to-end checks of the safety-critical paths against the built-in simulators, run by the real
/// build on the user's own PC (and by CI on the published Windows executable). Never opens a COM port or touches a vehicle. Each
/// check prints PASS or FAIL; exit 0 only when all pass. Writes only to a temporary folder, deleted afterwards.
/// </summary>
internal static class SelfTest
{
    private sealed class ReverseKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(seed.Reverse().ToArray());
    }
    private sealed class WrongKey : ISeedKeyProvider
    {
        public Task<byte[]?> ComputeKeyAsync(uint m, byte l, byte[] seed, CancellationToken ct = default) => Task.FromResult<byte[]?>(new byte[] { 0, 0, 0 });
    }

    public static async Task<int> RunAsync(TextWriter o)
    {
        var dir = Path.Combine(Path.GetTempPath(), "obdcli-selftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        int failed = 0, n = 0;
        async Task Check(string name, Func<Task<string>> f)
        {
            n++;
            var sw = Stopwatch.StartNew();
            try { var detail = await f(); o.WriteLine($"PASS  {name} ({sw.ElapsedMilliseconds} ms){(detail.Length > 0 ? ": " + detail : "")}"); }
            catch (Exception ex) { failed++; o.WriteLine($"FAIL  {name}: {ex.GetType().Name}: {ex.Message}"); }
        }
        static void Must(bool ok, string what) { if (!ok) throw new InvalidOperationException(what); }

        o.WriteLine($"obdcli selftest {typeof(SelfTest).Assembly.GetName().Version} on {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture})");
        o.WriteLine("Simulators only: no COM port, no vehicle.");
        try
        {
            await Check("Adapter: connect to a simulated OBDLink EX", async () =>
            {
                var s = new AdapterSession(new ElmLink(new SimulatedAdapter { Profile = SimProfile.Ex }.CreateTransport()), initial: AdapterProfiles.ForPort(PortKind.Usb));
                await s.InitializeAsync();
                Must(s.Profile.Kind == AdapterKind.ObdLinkEx, $"detected {s.Profile.Kind}");
                return s.Profile.DisplayName;
            });

            var payload = Enumerable.Range(0, 20).Select(i => (byte)i).ToArray();
            await Check("Long request through the adapter (STPX)", async () =>
            {
                var sim = new SimulatedAdapter { Profile = SimProfile.Ex };
                var s = new AdapterSession(new ElmLink(sim.CreateTransport()), initial: AdapterProfiles.ForPort(PortKind.Usb));
                await s.InitializeAsync();
                var ch = new ElmDiagChannel(s);
                var uds = new UdsClient(ch, 0x726);
                await uds.WriteDataByIdentifierAsync(0x4045, payload);
                Must(ch.StpxSends == 1 && sim.StpxMessages == 1, $"STPX sends {ch.StpxSends}, host {ch.HostSegmentedSends}");
                Must((await uds.ReadDataByIdentifierAsync(0x4045)).AsSpan().SequenceEqual(payload), "read-back differs");
                return "one adapter command, read back verified";
            });

            await Check("Long request, host-side fallback when STPX is refused", async () =>
            {
                var sim = new SimulatedAdapter { Profile = SimProfile.MxPlus, StpxSupported = false };
                var s = new AdapterSession(new ElmLink(sim.CreateTransport()), initial: AdapterProfiles.ForPort(PortKind.Bluetooth));
                await s.InitializeAsync();
                var ch = new ElmDiagChannel(s);
                await new UdsClient(ch, 0x726).WriteDataByIdentifierAsync(0x4045, payload);
                Must(ch.HostSegmentedSends == 1 && s.StpxDisabled, "did not fall back");
                return "fell back and stayed off";
            });

            FlashImage Exe() => VbfReader.Parse(VbfReader.Write(new[] { new FlashSegment(0x10000, Enumerable.Range(0, 6000).Select(i => (byte)(i * 7)).ToArray()) }, "SELF-14C204-AA", 0x7E0, partType: "EXE"), "selftest-exe.vbf");
            FlashImage Sbl() => VbfReader.Parse(VbfReader.Write(new[] { new FlashSegment(0xE00000, new byte[0x200]) }, "SELF-14C039-AA", 0x7E0, partType: "SBL", call: 0xE00000), "selftest-sbl.vbf");
            FlashEngine Engine(SimulatedFlashEcu ecu, FlashImage img, string journal, FlashImage? sbl) => new(new FlashPlan { TxId = 0x7E0, Image = img, Sbl = sbl, CommitRoutineId = sbl is null ? null : 0x0304 }, new FileFlashJournal(journal))
            {
                ReadVoltage = _ => Task.FromResult<double?>(13.2),
                Backup = _ => Task.FromResult("selftest: no backup needed"),
                ReadModulePart = async (u, ct) => System.Text.Encoding.ASCII.GetString(await u.ReadDataByIdentifierAsync(0xF188, ct)),
                Delay = _ => Task.CompletedTask,
            };

            await Check("Flash: SBL + EXE on the simulated bootloader, journal on disk", async () =>
            {
                var img = Exe();
                var ecu = new SimulatedFlashEcu { RequiresSbl = true, ExpectedImage = img };
                var path = Path.Combine(dir, "flash-ok.jsonl");
                var r = await Engine(ecu, img, path, Sbl()).RunAsync(new UdsClient(ecu, 0x7E0) { SeedKey = new ReverseKey(), PendingRetryDelay = TimeSpan.Zero });
                Must(r.PartNumberConfirmed && ecu.ApplicationValid && !ecu.InBootloader, "module not running the new software");
                Must(!FlashJournalState.From(new FileFlashJournal(path).ReadAll()).Open, "journal still open");
                return $"{img.TotalBytes:N0} bytes, journal {new FileInfo(path).Length:N0} bytes";
            });

            await Check("Flash: module drops the session mid-download → restart from the erase", async () =>
            {
                var img = Exe();
                var ecu = new SimulatedFlashEcu { RequiresSbl = true, ExpectedImage = img };
                int seen = 0;
                ecu.Faults = (req, _) => req[0] == 0x36 && req[1] == 0x03 && seen++ == 0 ? SimFlashFault.SessionDrop : null;
                var r = await Engine(ecu, img, Path.Combine(dir, "flash-drop.jsonl"), Sbl()).RunAsync(new UdsClient(ecu, 0x7E0) { SeedKey = new ReverseKey(), PendingRetryDelay = TimeSpan.Zero });
                Must(r.PartNumberConfirmed, "not confirmed");
                return "recovered";
            });

            await Check("Flash: wrong key → module reset back, no second key sent", async () =>
            {
                var img = Exe();
                var ecu = new SimulatedFlashEcu { ExpectedImage = img };
                var path = Path.Combine(dir, "flash-key.jsonl");
                var uds = new UdsClient(ecu, 0x7E0) { SeedKey = new WrongKey(), PendingRetryDelay = TimeSpan.Zero };
                try { await Engine(ecu, img, path, null).RunAsync(uds); throw new InvalidOperationException("flash did not stop"); }
                catch (FlashFailedException ex) when (!ex.ModuleMayBeInBootloader) { }
                Must(ecu.ApplicationValid && !ecu.InBootloader, "module left in its bootloader");
                try { await Engine(ecu, img, path, null).RunAsync(uds); throw new InvalidOperationException("second attempt was not held"); }
                catch (FlashFailedException ex) when (ex.Message.Contains("hold", StringComparison.OrdinalIgnoreCase)) { }
                Must(ecu.WrongKeys == 1, $"{ecu.WrongKeys} wrong keys reached the module");
                return "held after one wrong key";
            });

            await Check("VBF: write, read back and check a Ford set", () =>
            {
                var set = FlashSet.Check(new[] { Sbl(), Exe() });
                Must(set.Ok, string.Join("; ", set.Problems));
                return Task.FromResult("SBL + EXE accepted");
            });
        }
        finally { try { Directory.Delete(dir, true); } catch (IOException) { /* temp folder: Windows may hold a file briefly */ } }
        o.WriteLine(failed == 0 ? $"All {n} checks passed." : $"{failed} of {n} checks FAILED.");
        return failed == 0 ? 0 : 1;
    }
}
