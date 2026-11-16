using System.Globalization;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Capture;

public enum CaptureReply { Unknown, Positive, Negative }

/// <summary>One UDS request seen in a capture: the header (target CAN ID) it went to, its payload and what came back.</summary>
public sealed record CapturedRequest(int Line, string Time, uint? Header, byte[] Payload, IReadOnlyList<string> ReplyLines)
{
    public byte Sid => Payload[0];

    /// <summary>Positive when a reply carries SID+0x40; Negative with the NRC when it carries 7F SID NRC (pending 0x78 is skipped).</summary>
    public (CaptureReply Kind, byte? Nrc, byte[]? Data) Reply()
    {
        byte? nrc = null;
        foreach (var l in ReplyLines)
        {
            var b = CaptureParser.ReplyBytes(l);
            if (b is null) continue;
            // The reply's service byte sits at 0 (no PCI shown), 1 (single-frame PCI) or 2 (first frame of a long reply).
            for (int i = 0; i < Math.Min(3, b.Length); i++)
            {
                if (b[i] == (byte)(Sid + 0x40)) return (CaptureReply.Positive, null, b[i..]);
                if (b[i] == 0x7F && i + 2 < b.Length && b[i + 1] == Sid && b[i + 2] != 0x78) nrc = b[i + 2];
            }
        }
        return nrc is null ? (CaptureReply.Unknown, null, null) : (CaptureReply.Negative, nrc, null);
    }
}

/// <summary>
/// Reads an <see cref="AdapterProxy"/> log back into UDS requests. Follows the target header the client set (ATSH, or the H: field
/// of STN's STPX), and the request payload forms: plain hex with CAN auto-formatting on (ATCAF1, the default), a single frame
/// with its PCI byte when auto-formatting is off (ATCAF0), or STPX D:.
/// </summary>
public static class CaptureParser
{
    public static IReadOnlyList<CapturedRequest> Parse(string log)
    {
        var list = new List<CapturedRequest>();
        uint? header = null;
        bool caf = true;
        List<string>? replies = null;
        int n = 0;
        foreach (var raw in log.Replace("\r\n", "\n").Split('\n'))
        {
            n++;
            if (raw.Length < 15 || raw[12] != ' ' || raw[14] != ' ') continue;
            string time = raw[..12], text = raw[15..].Trim();
            switch (raw[13])
            {
                case '<': replies?.Add(text); break;
                case '>':
                {
                    var cmd = Compact(text);
                    replies = null;
                    if (cmd.StartsWith("ATSH", StringComparison.Ordinal) && uint.TryParse(cmd[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h)) { header = h; break; }
                    if (cmd == "ATCAF0") { caf = false; break; }
                    if (cmd == "ATCAF1" || cmd is "ATZ" or "ATD") { caf = true; if (cmd != "ATCAF1") header = null; break; }
                    byte[]? payload = null;
                    uint? target = header;
                    if (cmd.StartsWith("STPX", StringComparison.Ordinal))
                    {
                        foreach (var part in text[4..].Split(','))
                        {
                            var kv = part.Split(':', 2);
                            if (kv.Length != 2) continue;
                            var key = kv[0].Trim().ToUpperInvariant();
                            if (key == "H" && uint.TryParse(Compact(kv[1]), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hx)) target = hx;
                            if (key == "D") payload = HexBytes(kv[1]);
                        }
                    }
                    else if (!cmd.StartsWith("AT", StringComparison.Ordinal) && !cmd.StartsWith("ST", StringComparison.Ordinal))
                    {
                        payload = HexBytes(text);
                        if (payload is { Length: > 1 } && !caf && payload[0] is > 0 and <= 7 && payload[0] == payload.Length - 1) payload = payload[1..];
                    }
                    if (payload is { Length: > 0 })
                    {
                        replies = new List<string>();
                        list.Add(new CapturedRequest(n, time, target, payload, replies));
                    }
                    break;
                }
            }
        }
        return list;
    }

    /// <summary>Payloads a command could carry (plain, minus a single-frame PCI, or STPX D:), for the pyrotechnic check.</summary>
    public static IEnumerable<byte[]> PayloadCandidates(string command)
    {
        var cmd = Compact(command);
        if (cmd.StartsWith("STPX", StringComparison.Ordinal))
        {
            foreach (var part in command.Trim()[4..].Split(','))
                if (part.Split(':', 2) is [var k, var v] && k.Trim().Equals("D", StringComparison.OrdinalIgnoreCase) && HexBytes(v) is { } d) yield return d;
            yield break;
        }
        if (cmd.StartsWith("AT", StringComparison.Ordinal) || cmd.StartsWith("ST", StringComparison.Ordinal)) yield break;
        if (HexBytes(command) is not { Length: > 0 } b) yield break;
        yield return b;
        if (b.Length > 1) yield return b[1..];
    }

    /// <summary>
    /// One adapter output line to bytes, without the CAN ID the adapter prints with headers on: "7E8 03 50 03" (spaces) or
    /// "7E8035003" (ATS0: an 11-bit ID makes the digit count odd). Null for text that is not hex (NO DATA, prompts, AT replies).
    /// </summary>
    public static byte[]? ReplyBytes(string line)
    {
        var tokens = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1 && tokens[0].Length is 3 or 8) tokens = tokens[1..];
        var t = string.Concat(tokens).ToUpperInvariant();
        if (t.Length % 2 == 1 && t.Length > 3) t = t[3..];
        return HexBytes(t);
    }

    /// <summary>Hex digits (spaces allowed) to bytes; null if the text is not whole hex bytes.</summary>
    public static byte[]? HexBytes(string text)
    {
        var t = Compact(text);
        if (t.Length == 0 || t.Length % 2 != 0) return null;
        foreach (var c in t) if (!Uri.IsHexDigit(c)) return null;
        return Convert.FromHexString(t);
    }

    private static string Compact(string s) => s.Replace(" ", "").Trim().ToUpperInvariant();
}
