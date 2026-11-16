using System.Text;
using System.Text.RegularExpressions;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Capture;

/// <summary>One state-changing request from a capture, ready to become a catalog candidate.</summary>
public sealed record CaptureCandidate(int Line, string Time, uint? Target, string Request, CaptureReply Reply, byte? Nrc, string? Status)
{
    public string TargetText => Target is uint t ? $"0x{t:X3}" : "(no header)";
}

/// <summary>
/// Turns a capture into the part worth sharing: the session changes and state-changing requests (routines, writes, resets,
/// IO control, clears, DTC-setting and communication control) per module, in order, with what each module answered.
/// <list type="bullet">
/// <item>Security access shows the level only: seeds and keys are left out.</item>
/// <item>VINs (in written data or anywhere in the text) are masked like the app's logs (last 6 characters kept).</item>
/// <item>Reads (22, 19, ...) are counted, not listed.</item>
/// </list>
/// </summary>
public static class CaptureSummary
{
    private static readonly Regex Vin = new("[A-HJ-NPR-Z0-9]{17}", RegexOptions.Compiled);

    public static IReadOnlyList<CaptureCandidate> Candidates(IReadOnlyList<CapturedRequest> requests)
    {
        var list = new List<CaptureCandidate>();
        foreach (var r in requests)
        {
            bool session = r.Sid == 0x10;
            if (!session && !UdsServices.IsStateChanging(r.Sid)) continue;
            var (kind, nrc, data) = r.Reply();
            string req = r.Sid == 0x27
                ? $"27 {r.Payload.ElementAtOrDefault(1):X2}" + (r.Payload.Length > 2 ? " <" + ((r.Payload[1] & 1) == 0 ? "key" : "data") + " left out>" : "")
                : Spaced(r.Payload);
            string? status = kind == CaptureReply.Positive && data is not null && r.Sid != 0x27 && data.Length > 1 ? Spaced(data) : null;
            list.Add(new CaptureCandidate(r.Line, r.Time, r.Header, Mask(req), kind, nrc, status is null ? null : Mask(status)));
        }
        return list;
    }

    public static string Text(string log)
    {
        var reqs = CaptureParser.Parse(log);
        var cands = Candidates(reqs);
        var sb = new StringBuilder();
        sb.AppendLine($"{reqs.Count} UDS requests, {cands.Count} session / state-changing. Seeds and keys left out; VINs masked.");
        foreach (var g in cands.GroupBy(c => c.TargetText))
        {
            sb.AppendLine().AppendLine($"Module {g.Key}:");
            foreach (var c in g)
                sb.AppendLine($"  {c.Time}  {c.Request,-30} {Describe(c)}");
        }
        var routines = cands.Where(c => c.Request.StartsWith("31 01", StringComparison.Ordinal) && c.Reply == CaptureReply.Positive).ToList();
        if (routines.Count > 0)
        {
            sb.AppendLine().AppendLine("Routines the modules accepted (catalog candidates; note which procedure you ran at each time):");
            foreach (var c in routines) sb.AppendLine($"  {c.Time}  {c.TargetText}  {c.Request}");
        }
        return sb.ToString();
    }

    private static string Describe(CaptureCandidate c) => c.Reply switch
    {
        CaptureReply.Positive => "OK" + (c.Status is null ? "" : " → " + c.Status),
        CaptureReply.Negative => $"refused NRC 0x{c.Nrc:X2} {UdsNrc.Describe(c.Nrc!.Value)}",
        _ => "(no reply seen)",
    };

    private static string Spaced(byte[] b) => string.Join(" ", b.Select(x => x.ToString("X2")));

    /// <summary>Masks a VIN written as text or as ASCII hex bytes.</summary>
    public static string Mask(string text)
    {
        var bytes = CaptureParser.HexBytes(text);
        if (bytes is { Length: >= 17 })
        {
            var ascii = new string(bytes.Select(b => b is >= 0x20 and < 0x7F ? (char)b : '.').ToArray());
            var m = Vin.Match(ascii);
            if (m.Success)
            {
                for (int i = m.Index; i < m.Index + 11; i++) bytes[i] = (byte)'X';
                return Spaced(bytes);
            }
        }
        return Vin.Replace(text, v => new string('X', 11) + v.Value[^6..]);
    }
}
