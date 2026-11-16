using System.Text;

namespace ObdLink.Core.Service;

/// <summary>docs/SERVICE-PROCEDURES.md, generated from <see cref="ServiceProcedures.All"/> (the doc test fails on drift; UPDATE_DOCS=1 rewrites it).</summary>
public static class ServiceProceduresDoc
{
    public static string Markdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Service procedures").AppendLine();
        sb.AppendLine("Generated from `src/ObdLink.Core/Service/ServiceProcedures.cs` (do not edit by hand; run the tests with `UPDATE_DOCS=1`).").AppendLine();
        sb.AppendLine("Ford's tools (IDS/FDRS) and FORScan offer \"service procedures\": resets, calibrations and relearns. This catalog lists them with **where every identifier comes from**. Nothing is guessed silently:").AppendLine();
        sb.AppendLine("| Confidence | Meaning | Can be run |").AppendLine("|---|---|---|");
        sb.AppendLine("| Verified | From Ford service information or a capture on this model, and run on the truck | yes, type RUN |");
        sb.AppendLine("| Community | From a cited public source (e.g. a forum post quoting a Ford tool); not run on a 2017 F-150 by this project | yes, type UNVERIFIED |");
        sb.AppendLine("| Synthesized | Inferred by this project from related evidence | yes, type UNVERIFIED (bench first) |");
        sb.AppendLine("| NeedsCapture | The procedure exists but no source gives its identifiers | no |").AppendLine();
        sb.AppendLine("Every run needs writes turned on, the battery check and the typed phrase. Pyrotechnic (E200-E2FF) and programming (FF00/FF01) routines are refused everywhere. Many procedures also have a **tool-free method** that Ford documents; it is listed where known and needs no adapter at all.").AppendLine();

        sb.AppendLine("## Summary").AppendLine();
        sb.AppendLine("| Procedure | Module | Confidence | Requests | Tool-free method |").AppendLine("|---|---|---|---|---|");
        foreach (var p in ServiceProcedures.All)
            sb.AppendLine($"| [{p.Name}](#{p.Key}) | {p.Module} 0x{p.TxId:X3} | {p.Confidence} | {(p.Runnable ? "`" + p.RequestSummary + "`" : "—")} | {(p.ManualMethod is { Count: > 0 } ? "yes" : "—")} |");
        sb.AppendLine();

        foreach (var p in ServiceProcedures.All)
        {
            sb.AppendLine($"## {p.Name}").AppendLine();
            sb.AppendLine($"<a id=\"{p.Key}\"></a>Key `{p.Key}` · {p.Module} 0x{p.TxId:X3} · **{p.Confidence}** · {p.AppliesTo}").AppendLine();
            sb.AppendLine(p.Purpose).AppendLine();
            List("Use when", p.UseWhen);
            List("Conditions", p.Conditions);
            List("Warnings", p.Warnings);
            if (p.Runnable) sb.AppendLine($"**Requests:** `{p.RequestSummary}` (type {p.Phrase})").AppendLine();
            else sb.AppendLine($"**Not runnable:** {p.NotRunnableReason}").AppendLine();
            List("Afterwards", p.After);
            if (p.ManualMethod is { Count: > 0 } m)
            {
                sb.AppendLine("**Tool-free method:**").AppendLine();
                for (int i = 0; i < m.Count; i++) sb.AppendLine($"{i + 1}. {m[i]}");
                sb.AppendLine();
            }
            sb.AppendLine("**Sources:**").AppendLine();
            foreach (var s in p.Sources) sb.AppendLine($"- {s.What}: <{s.Url}>");
            sb.AppendLine();
        }

        sb.AppendLine("## Filling the gaps").AppendLine();
        sb.AppendLine("A NeedsCapture procedure becomes runnable only with identifiers from one of:").AppendLine();
        sb.AppendLine("1. **A cited public source** (Ford service information, a TSB, a forum post quoting a Ford tool). Confidence Community.");
        sb.AppendLine("2. **A capture on the truck**: the requests a working tool sends for that procedure on this model, recorded with `obdcli proxy` and summarized with `obdcli capture-summary` (`docs/CLI.md`). Confidence Verified after this app runs it once with its conditions and no new DTCs (verification plan, Phase 3 D2).");
        sb.AppendLine("3. **Synthesis**: the same routine on a related model year or module generation. Confidence Synthesized; the app asks for UNVERIFIED and the release notes say so.").AppendLine();
        sb.AppendLine("Whatever the source, a routine only goes on the truck allow-list after it was run once with its listed conditions and left no new DTCs.");
        return sb.ToString().Replace("\r\n", "\n");

        void List(string title, IReadOnlyList<string> items)
        {
            if (items.Count == 0) return;
            sb.AppendLine($"**{title}:**").AppendLine();
            foreach (var i in items) sb.AppendLine($"- {i}");
            sb.AppendLine();
        }
    }
}
