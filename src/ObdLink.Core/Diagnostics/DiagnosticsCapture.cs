using System.IO.Compression;
using System.Text;
using System.Text.Json;
using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Modules;
using ObdLink.Core.Safety;

namespace ObdLink.Core.Diagnostics;

/// <summary>
/// One-click capture that only reads and changes nothing. Every adapter exchange (raw traffic) goes into a zip the user can send back.
/// Order of steps:
/// 1. Adapter ID: ATI, STI, STDI, ATRV.
/// 2. Probe ATSP6 + ATDP + 0100.
/// 3. Probe ATSP2 + ATDP + 0100 (J1850 VPW).
/// 4. Test STP 53 + 0100 (Ford MS-CAN preset; on other adapters or vehicles it can return '?' or NO DATA).
/// 5. Select the vehicle's diag protocol and scan the module list.
/// 6. Optionally run the discovery sweep.
/// Serial numbers in STDI or other replies are NOT redacted, because the user sends the zip back privately. The VIN is read only if a module returns it in a probe.
/// </summary>
public sealed class DiagnosticsCapture
{
    private readonly AdapterSession _s; private readonly IDiagChannel _ch;
    private readonly StringBuilder _raw = new(); private readonly StringBuilder _sum = new();
    public DiagnosticsCapture(AdapterSession s, IDiagChannel ch) { _s = s; _ch = ch; }

    public sealed record Options(string Vehicle, IReadOnlyList<ModuleDefinition> Modules, StnProtocol ScanProtocol, char ElmFallback, DiscoveryOptions? Discovery, string AppVersion, string? ExtraNote = null,
        IReadOnlyList<(string Name, string Text)>? ExtraFiles = null, bool RedactVins = false);

    public async Task<string> RunAsync(Options o, string outDir, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        void P(string m) { lock (_sum) _sum.AppendLine(m); progress?.Report(m); }
        void T(string d, string t) { lock (_raw) _raw.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append(' ').Append(d).Append(' ').AppendLine(t.Replace("\r", "\\r").Replace("\n", "\\n")); }
        var link = _s.Link;
        link.Traffic += T;
        var started = DateTime.Now;
        P($"Diagnostics capture {started:yyyy-MM-dd HH:mm:ss zzz}  app {o.AppVersion}  vehicle '{o.Vehicle}'");
        if (o.ExtraNote is not null) P(o.ExtraNote);
        async Task Cmd(string c) { try { var r = await link.SendAsync(c, ct: ct); P($"{c,-8} => {string.Join(" | ", r.Lines)} [{r.Error}]"); } catch (Exception ex) when (ex is not OperationCanceledException) { P($"{c,-8} => EXCEPTION {ex.GetType().Name}: {ex.Message}"); } }
        try
        {
            P("== Adapter identification ==");
            foreach (var c in new[] { "ATI", "STI", "STDI", "ATRV" }) await Cmd(c);
            P("== Protocol probes ==");
            await Cmd("STPC"); // close any STN protocol so ATSP takes effect [STN behaviour UNVERIFIED]
            foreach (var c in new[] { "ATSH7DF", "ATSP6", "ATDP", "0100", "ATSP2", "ATDP", "0100" }) await Cmd(c);
            P("-- STP 53 (MS-CAN preset) test --");
            foreach (var c in new[] { "STP53", "STPO", "ATSH7DF", "0100", "STPC" }) await Cmd(c);
            P("== Module scan ==");
            try
            {
                if (_s.IsStn) await _s.SelectProtocolAsync(o.ScanProtocol, ct); else await _s.SelectElmProtocolAsync(o.ElmFallback, ct);
                var res = await new ModuleScanner(_s, _ch).ScanAsync(o.Modules, null, ct);
                foreach (var r in res) P($"{r.Module.Acronym,-8} 0x{r.Module.TxId:X3} {(r.Responded ? "RESPONDED" : "no reply")}  {r.Detail}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { P($"Module scan failed: {ex.GetType().Name}: {ex.Message}"); }
            if (o.Discovery is { } d)
            {
                P($"== Discovery sweep 0x{d.StartId:X3}-0x{d.EndId:X3} ==");
                try
                {
                    if (_s.IsStn) await _s.SelectProtocolAsync(o.ScanProtocol, ct); else await _s.SelectElmProtocolAsync(o.ElmFallback, ct);
                    var hits = await new DiscoverySweep(_s, _ch).RunAsync(d, null, ct);
                    P(hits.Count == 0 ? "No IDs responded." : DiscoverySweep.Summarize(hits));
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { P($"Discovery failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            await Cmd("ATRV");
        }
        finally { link.Traffic -= T; }

        Directory.CreateDirectory(outDir);
        var path = Path.Combine(outDir, $"obdlink-capture-{started:yyyyMMdd-HHmmss}.zip");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            void Add(string n, string text) { using var w = new StreamWriter(zip.CreateEntry(n).Open()); w.Write(o.RedactVins ? Logging.VinRedactor.Redact(text) : text); }
            Add("summary.txt", _sum.ToString());
            Add("raw-traffic.log", _raw.ToString());
            Add("options.json", JsonSerializer.Serialize(new { o.Vehicle, ScanProtocol = o.ScanProtocol.ToString(), o.ElmFallback, o.Discovery, o.AppVersion, Modules = o.Modules.Select(m => $"{m.Acronym} {m.TxId:X3}") }, AppSettings.Json));
            // App logs (events, app messages, traffic) so the user's actions leading up to a problem are included.
            foreach (var (n, text) in o.ExtraFiles ?? Array.Empty<(string, string)>()) Add("logs/" + Path.GetFileName(n), text);
        }
        P($"Saved {path}");
        return path;
    }
}
