using ObdLink.Core.Elm;
using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using Serilog;

namespace ObdLink.Core.Diagnostics;

public sealed record DiscoveryHit(uint RequestId, string Probe, uint ResponseId, string PayloadHex);

/// <summary>
/// Sends read-only probes to each 11-bit ID in a range and logs which IDs answer. The receive filter is left OPEN (no ATCRA filter), so a reply from any ID is recorded. That's because Chrysler request/response pairing is [UNVERIFIED].
/// The probes are 3E 00 (TesterPresent), 22 F1 90 (UDS read VIN) and 1A xx (KWP2000 ReadEcuIdentification). None of them changes ECU state.
/// A negative response (7F ..) still counts as "responded".
/// </summary>
public sealed class DiscoverySweep
{
    private readonly AdapterSession _s; private readonly IDiagChannel _ch;
    public DiscoverySweep(AdapterSession s, IDiagChannel ch) { _s = s; _ch = ch; }

    public static IEnumerable<(string Name, byte[] Req)> Probes(DiscoveryOptions o)
    {
        if (o.TesterPresent) yield return ("3E00 TesterPresent", new byte[] { 0x3E, 0x00 });
        if (o.ReadVinF190) yield return ("22F190 ReadVIN", new byte[] { 0x22, 0xF1, 0x90 });
        if (o.Kwp1A) yield return ($"1A{o.Kwp1AOption:X2} KWP ReadEcuId", new byte[] { 0x1A, o.Kwp1AOption });
    }

    public async Task<List<DiscoveryHit>> RunAsync(DiscoveryOptions o, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (o.EndId < o.StartId || o.EndId > 0x7FF) throw new ArgumentException("Discovery range must be 11-bit and Start <= End");
        var hits = new List<DiscoveryHit>();
        var probes = Probes(o).ToList();
        var to = TimeSpan.FromMilliseconds(Math.Max(o.TimeoutMs, 50) + 1000);
        await _s.Link.SendAsync($"ATST{Math.Clamp(o.TimeoutMs / 4, 1, 255):X2}", ct: ct); // ELM timeout, 4 ms units
        try
        {
            for (uint id = o.StartId; id <= o.EndId; id++)
            {
                ct.ThrowIfCancellationRequested();
                await _ch.SetTargetAsync(id, null, ct);
                foreach (var (name, req) in probes)
                {
                    IReadOnlyList<DiagResponse> rs;
                    try { rs = await _ch.RequestAsync(req, to, ct); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) { Log.Debug("Discovery 0x{Id:X3} {Probe}: {Err}", id, name, ex.Message); continue; }
                    foreach (var r in rs)
                    {
                        var h = new DiscoveryHit(id, name, r.SourceId, Convert.ToHexString(r.Payload));
                        hits.Add(h);
                        Log.Information("Discovery HIT req 0x{Id:X3} {Probe} -> 0x{Rx:X3} {Data}", id, name, r.SourceId, h.PayloadHex);
                        progress?.Report($"HIT 0x{id:X3} {name} -> 0x{r.SourceId:X3} {h.PayloadHex}");
                    }
                }
                if ((id & 0xF) == 0xF) progress?.Report($"Discovery swept to 0x{id:X3} ({hits.Select(x => x.RequestId).Distinct().Count()} responding IDs so far)");
            }
        }
        finally { await _s.Link.SendAsync("ATST32", ct: CancellationToken.None); } // restore ~200 ms
        return hits;
    }

    public static string Summarize(IEnumerable<DiscoveryHit> hits) =>
        string.Join(Environment.NewLine, hits.GroupBy(h => h.RequestId).Select(g =>
            $"0x{g.Key:X3}: " + string.Join("; ", g.Select(h => $"{h.Probe} -> 0x{h.ResponseId:X3} {h.PayloadHex}"))));
}
