using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ObdLink.Core.Elm;
using ObdLink.Core.Transport;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>
/// v0.23.17: a silent adapter must end in a timeout, not a hang. <c>SerialPort.BaseStream.ReadAsync</c> ignores cancellation on
/// Windows (dotnet/corefx#36040), so reads go through <see cref="SerialReadPump"/>. These tests use a fake blocking port.
/// </summary>
public class SerialReadPumpTests
{
    /// <summary>Behaves like SerialPort.Read with ReadTimeout = 20 ms: returns queued bytes, 0 on timeout, throws once failed.</summary>
    private sealed class FakePort
    {
        private readonly BlockingCollection<object> _in = new();
        public int Reads;
        public void Feed(string s) => _in.Add(Encoding.ASCII.GetBytes(s));
        public void Fail(Exception ex) => _in.Add(ex);
        public int Read(byte[] buf)
        {
            Interlocked.Increment(ref Reads);
            if (!_in.TryTake(out var item, 20)) return 0;
            if (item is Exception ex) throw ex;
            var b = (byte[])item; b.CopyTo(buf, 0); return b.Length;
        }
    }

    [Fact]
    public async Task Silent_device_read_is_cancelled_promptly_and_later_data_is_not_lost()
    {
        var port = new FakePort();
        using var pump = new SerialReadPump(port.Read);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pump.ReadAsync(cts.Token).WaitAsync(TimeSpan.FromSeconds(5))); // a hang fails, not blocks CI
        Assert.True(sw.ElapsedMilliseconds < 2000, $"cancel took {sw.ElapsedMilliseconds} ms");
        port.Feed("41 00>");
        Assert.Equal("41 00>", await pump.ReadAsync(new CancellationTokenSource(2000).Token));
    }

    [Fact]
    public async Task Data_arrives_in_order()
    {
        var port = new FakePort();
        using var pump = new SerialReadPump(port.Read);
        port.Feed("AB"); port.Feed("CD"); port.Feed(">");
        var sb = new StringBuilder();
        while (!sb.ToString().Contains('>')) sb.Append(await pump.ReadAsync(new CancellationTokenSource(2000).Token));
        Assert.Equal("ABCD>", sb.ToString());
    }

    [Fact]
    public async Task Read_failure_becomes_IOException_then_closed()
    {
        var port = new FakePort();
        using var pump = new SerialReadPump(port.Read);
        port.Fail(new InvalidOperationException("The port is closed."));
        var ex = await Assert.ThrowsAsync<IOException>(() => pump.ReadAsync(new CancellationTokenSource(2000).Token));
        Assert.Contains("port is closed", ex.Message);
        await Assert.ThrowsAsync<IOException>(() => pump.ReadAsync(new CancellationTokenSource(2000).Token));
    }

    [Fact]
    public async Task IOException_from_the_port_is_passed_through()
    {
        var port = new FakePort();
        using var pump = new SerialReadPump(port.Read);
        var io = new IOException("device gone");
        port.Fail(io);
        Assert.Same(io, await Assert.ThrowsAsync<IOException>(() => pump.ReadAsync(new CancellationTokenSource(2000).Token)));
    }

    [Fact]
    public async Task Discard_drops_unread_text_but_keeps_a_failure()
    {
        var port = new FakePort();
        using var pump = new SerialReadPump(port.Read);
        port.Feed("STALE>");
        await WaitUntil(() => port.Reads >= 3); // the stale text has been queued
        pump.Discard();
        port.Feed("FRESH>");
        Assert.Equal("FRESH>", await pump.ReadAsync(new CancellationTokenSource(2000).Token));

        port.Fail(new IOException("gone"));
        await WaitUntil(() => !pump.IsRunning);
        pump.Discard();
        await Assert.ThrowsAsync<IOException>(() => pump.ReadAsync(new CancellationTokenSource(2000).Token));
    }

    [Fact]
    public async Task Stop_ends_the_thread_within_a_read_timeout_and_is_not_a_failure()
    {
        var port = new FakePort();
        var pump = new SerialReadPump(port.Read);
        Assert.True(pump.Join(TimeSpan.FromSeconds(2)));
        Assert.False(pump.IsRunning);
        var ex = await Assert.ThrowsAsync<IOException>(() => pump.ReadAsync(new CancellationTokenSource(2000).Token));
        Assert.Equal("Serial stream closed", ex.Message);
    }

    /// <summary>End to end: ElmLink over a transport whose adapter never answers times out instead of hanging, and the gate is free again.</summary>
    [Fact]
    public async Task ElmLink_times_out_on_a_silent_adapter_and_recovers()
    {
        var port = new FakePort();
        await using var t = new PumpTransport(port);
        var link = new ElmLink(t) { DefaultTimeout = TimeSpan.FromMilliseconds(200) };
        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAsync<ElmTimeoutException>(() => link.SendAsync("0100").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(sw.ElapsedMilliseconds < 3000, $"timeout took {sw.ElapsedMilliseconds} ms");
        t.ReplyOnNextWrite = "41 00 BE 3F A8 13\r\r>"; // like an adapter: the answer comes after the command
        var r = await link.SendAsync("0100", TimeSpan.FromSeconds(2));
        Assert.True(r.Ok);
        Assert.Equal("41 00 BE 3F A8 13", r.Lines.Single());
    }

    private sealed class PumpTransport(FakePort port) : ITransport
    {
        private readonly SerialReadPump _pump = new(port.Read);
        public string Name => "fake";
        public bool IsOpen => true;
        public Task OpenAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task CloseAsync() => Task.CompletedTask;
        public string? ReplyOnNextWrite;
        public Task WriteAsync(string text, CancellationToken ct = default)
        {
            if (Interlocked.Exchange(ref ReplyOnNextWrite, null) is string r) port.Feed(r);
            return Task.CompletedTask;
        }
        public Task<string> ReadAsync(CancellationToken ct = default) => _pump.ReadAsync(ct);
        public void DiscardInput() => _pump.Discard();
        public ValueTask DisposeAsync() { _pump.Dispose(); return ValueTask.CompletedTask; }
    }

    private static async Task WaitUntil(Func<bool> cond)
    {
        var sw = Stopwatch.StartNew();
        while (!cond()) { if (sw.ElapsedMilliseconds > 3000) throw new TimeoutException("condition not met"); await Task.Delay(10); }
    }

    [Fact]
    public void Serial_transport_uses_the_pump_not_BaseStream_async()
    {
        var src = File.ReadAllText(Path.Combine(RepoRoot(), "src", "ObdLink.Core", "Transport", "SerialPortTransport.cs"));
        Assert.DoesNotContain("BaseStream.ReadAsync", src);
        Assert.DoesNotContain("BaseStream.WriteAsync", src);
        Assert.DoesNotContain("InfiniteTimeout", src);
        Assert.Contains("new SerialReadPump(", src);
    }

    private static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "ObdLink.sln"))) d = d.Parent;
        return d!.FullName;
    }
}
