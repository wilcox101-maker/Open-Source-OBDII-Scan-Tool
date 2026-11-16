using System.Net;
using System.Net.Sockets;
using System.Text;
using ObdLink.Core.Capture;
using ObdLink.Core.Transport;
using CliApp = ObdLink.Cli.Cli;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Capture path for service-procedure identifiers: adapter proxy, log parser, shareable summary. SYNTHETIC traffic.</summary>
public class CaptureTests
{
    private const string Vin = "1FTEW1EP7HFA00000";   // synthetic, not a real truck

    private static string Log(params string[] lines) => string.Join("\n", lines.Select((l, i) => $"10:00:{i:00}.000 {l}")) + "\n";

    [Fact]
    public void Parser_follows_ATSH_and_reads_positive_and_negative_replies_with_or_without_headers()
    {
        var reqs = CaptureParser.Parse(Log(
            "> ATZ", "< ELM327 v1.4b",
            "> ATSH726", "< OK",
            "> 10 03", "< 72E 06 50 03 00 32 01 F4",
            "> 3101201A", "< 72E037F3178", "< 72E047101201A",          // ATS0: pending, then positive
            "> ATSH7E0", "< OK",
            "> 31 01 40 44", "< 7E8 03 7F 31 31"));
        Assert.Equal(3, reqs.Count);
        Assert.Equal(0x726u, reqs[0].Header);
        Assert.Equal(CaptureReply.Positive, reqs[0].Reply().Kind);
        Assert.Equal(CaptureReply.Positive, reqs[1].Reply().Kind);            // the pending 0x78 is skipped
        Assert.Equal(0x7E0u, reqs[2].Header);
        var (kind, nrc, _) = reqs[2].Reply();
        Assert.Equal(CaptureReply.Negative, kind);
        Assert.Equal((byte)0x31, nrc);
    }

    [Fact]
    public void Parser_reads_STPX_and_strips_the_PCI_when_auto_formatting_is_off()
    {
        var reqs = CaptureParser.Parse(Log(
            "> STPX H:760, D:3101ABCD, R:1", "< 768 04 71 01 AB CD",
            "> ATSH730", "< OK", "> ATCAF0", "< OK",
            "> 04 31 01 12 34", "< 738 04 71 01 12 34"));
        Assert.Equal(0x760u, reqs[0].Header);
        Assert.Equal(new byte[] { 0x31, 0x01, 0xAB, 0xCD }, reqs[0].Payload);
        Assert.Equal(new byte[] { 0x31, 0x01, 0x12, 0x34 }, reqs[1].Payload);
        Assert.Equal(0x730u, reqs[1].Header);
    }

    [Fact]
    public void Summary_lists_only_state_changing_requests_leaves_out_keys_and_masks_VINs()
    {
        var vinHex = string.Join(" ", Encoding.ASCII.GetBytes(Vin).Select(b => b.ToString("X2")));
        var text = CaptureSummary.Text(Log(
            "> ATSH726", "< OK",
            "> 22 F1 90", "< 72E 10 14 62 F1 90 " + vinHex,
            "> 10 03", "< 72E 02 50 03",
            "> 27 01", "< 72E 05 67 01 AA BB CC",
            "> 27 02 11 22 33", "< 72E 02 67 02",
            "> 2E F1 90 " + vinHex, "< 72E 03 6E F1 90",
            "> 31 01 20 1A", "< 72E 04 71 01 20 1A"));
        Assert.DoesNotContain("22 F1 90", text);                 // reads are counted, not listed
        Assert.Contains("27 02 <key left out>", text);
        Assert.DoesNotContain("11 22 33", text);
        Assert.DoesNotContain("AA BB CC", text);
        Assert.DoesNotContain(vinHex, text);
        Assert.Contains("58 58 58", text);                         // 'XXX' in the masked VIN bytes
        Assert.Contains("Module 0x726:", text);
        Assert.Matches(@"Routines the modules accepted[^\n]*\n\s+10:00:\d\d\.000\s+0x726\s+31 01 20 1A", text);
    }

    [Theory]
    [InlineData("3101E201", true)]
    [InlineData("31 01 E2 FF 01", true)]
    [InlineData("04 31 01 E2 01", true)]                 // ATCAF0 single frame with PCI
    [InlineData("STPX H:737, D:3101E201, R:1", true)]
    [InlineData("3101201A", false)]
    [InlineData("31 03 E2 01", true)]                    // same rule as everywhere: the whole range is refused, any sub-function
    [InlineData("ATSH737", false)]
    public void The_proxy_blocks_pyrotechnic_routines_in_every_request_form(string cmd, bool blocked) => Assert.Equal(blocked, AdapterProxy.IsBlocked(cmd));

    [Fact]
    public async Task The_proxy_relays_and_logs_and_never_forwards_a_pyrotechnic_routine()
    {
        var adapter = new MockTransport(cmd => cmd switch
        {
            "ATZ" => "ELM327 v1.4b",
            "ATSH726" => "OK",
            "1003" => "72E 02 50 03",
            "3101201A" => "72E 04 71 01 20 1A",
            _ => "?",
        });
        await adapter.OpenAsync();
        var logText = new StringWriter();
        var proxy = new AdapterProxy(adapter, logText, () => new DateTimeOffset(2026, 11, 16, 10, 0, 0, TimeSpan.Zero));
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using var server = await listener.AcceptTcpClientAsync();
        var run = proxy.RunAsync(server.GetStream());
        var s = client.GetStream();

        async Task<string> Send(string cmd)
        {
            await s.WriteAsync(Encoding.ASCII.GetBytes(cmd + "\r"));
            var sb = new StringBuilder(); var buf = new byte[256];
            using var to = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!sb.ToString().EndsWith('>')) sb.Append(Encoding.ASCII.GetString(buf, 0, await s.ReadAsync(buf, to.Token)));
            return sb.ToString();
        }

        Assert.Contains("ELM327", await Send("ATZ"));
        Assert.Contains("OK", await Send("ATSH726"));
        Assert.Contains("50 03", await Send("1003"));
        Assert.StartsWith("?", await Send("3101E201"));
        Assert.Contains("71 01 20 1A", await Send("3101201A"));
        client.Close();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();

        Assert.DoesNotContain(adapter.Sent, c => c.Contains("E201"));
        Assert.Equal(4, proxy.Forwarded);
        Assert.Equal(1, proxy.Blocked);
        var log = logText.ToString();
        Assert.Contains("! BLOCKED 3101E201", log);
        Assert.Contains("> 3101201A", log);
        Assert.Contains("< 72E 04 71 01 20 1A", log);
        // and the log reads back into the same requests
        var reqs = CaptureParser.Parse(log);
        Assert.Equal(new[] { "1003", "3101201A" }, reqs.Select(r => Convert.ToHexString(r.Payload)));
        Assert.All(reqs, r => Assert.Equal(CaptureReply.Positive, r.Reply().Kind));
    }

    [Fact]
    public async Task Cli_capture_summary_and_proxy_usage()
    {
        var path = Path.Combine(Path.GetTempPath(), "cap-" + Guid.NewGuid().ToString("N") + ".txt");
        await File.WriteAllTextAsync(path, Log("> ATSH726", "< OK", "> 3101201A", "< 72E 04 71 01 20 1A"));
        try
        {
            var o = new StringWriter(); var e = new StringWriter();
            Assert.Equal(0, await CliApp.RunAsync(new[] { "capture-summary", "--log", path }, o, e));
            Assert.Contains("0x726  31 01 20 1A", o.ToString());
            Assert.Equal(2, await CliApp.RunAsync(new[] { "proxy", "--log", path }, new StringWriter(), new StringWriter()));        // no --port
            Assert.Equal(2, await CliApp.RunAsync(new[] { "proxy", "--port", "COM9" }, new StringWriter(), new StringWriter()));    // no --log
        }
        finally { File.Delete(path); }
    }
}
