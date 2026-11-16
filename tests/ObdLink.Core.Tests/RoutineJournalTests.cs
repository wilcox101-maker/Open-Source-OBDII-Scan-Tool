using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>R1 (write recovery plan): routine start / stop / procedure runs are journaled before the request.</summary>
public class RoutineJournalTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rjournal-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

    private static RoutineResult Ok(byte status) => new(0x201A, RoutineControlType.Start, new[] { status });

    [Fact]
    public async Task The_pending_line_is_on_disk_before_the_request_is_sent()
    {
        var j = new RoutineJournal(_dir);
        int linesWhenSent = -1;
        await j.RecordAsync("start", 0x726, "BCM", 0x201A, new byte[] { 0x01 }, () =>
        {
            linesWhenSent = File.ReadAllLines(j.Path).Length;
            Assert.Single(new RoutineJournal(_dir).Unfinished()); // another reader (a restarted app) already sees it
            return Task.FromResult(Ok(0x00));
        });
        Assert.Equal(1, linesWhenSent);
        Assert.Empty(j.Unfinished());
        var last = j.ReadAll()[^1];
        Assert.Equal(RoutineOutcome.Accepted, last.Outcome);
        Assert.Equal("status 00", last.Detail);
        Assert.Equal("01", last.OptionHex);
    }

    [Fact]
    public async Task A_refusal_is_definite_also_when_wrapped_and_is_not_unfinished()
    {
        var j = new RoutineJournal(_dir);
        await Assert.ThrowsAsync<UdsNegativeResponseException>(() => j.RecordAsync<RoutineResult>("start", 0x726, "BCM", 0x201A, null,
            () => throw new UdsNegativeResponseException(0x31, 0x31)));
        // The service procedure runner wraps the NRC in an InvalidOperationException ("nothing ran").
        await Assert.ThrowsAsync<InvalidOperationException>(() => j.RecordAsync<RoutineResult>("procedure:bcm-bms-reset", 0x726, "BCM", 0x201A, null,
            () => throw new InvalidOperationException("refused", new UdsNegativeResponseException(0x31, 0x22))));
        Assert.Empty(j.Unfinished());
        Assert.All(j.ReadAll().Where(e => e.Outcome != RoutineOutcome.Pending), e => Assert.Equal(RoutineOutcome.Refused, e.Outcome));
        Assert.Contains("NRC 0x22", j.ReadAll()[^1].Detail);
    }

    [Fact]
    public async Task A_link_drop_or_timeout_leaves_an_unknown_attempt_that_31_03_can_mark_checked()
    {
        var j = new RoutineJournal(_dir);
        await Assert.ThrowsAsync<TimeoutException>(() => j.RecordAsync<RoutineResult>("start", 0x726, "BCM", 0x201A, null, () => throw new TimeoutException("no reply")));
        var open = Assert.Single(j.Unfinished());
        Assert.Equal(RoutineOutcome.Unknown, open.Outcome);
        Assert.Contains("no reply", open.Detail);
        Assert.Contains("UNFINISHED: start of BCM (0x726) routine 0x201A", RoutineJournal.Describe(open));
        Assert.Contains("Results (31 03)", RoutineJournal.Describe(open));

        Assert.Equal(0, j.MarkChecked(0x726, 0x1234, "other routine")); // only the same module + routine
        Assert.Equal(1, j.MarkChecked(0x726, 0x201A, "31 03: status 00"));
        Assert.Empty(j.Unfinished());
    }

    [Fact]
    public void A_crash_after_the_pending_line_leaves_it_unfinished_and_a_torn_last_line_is_skipped()
    {
        var j = new RoutineJournal(_dir);
        j.Append(new RoutineEntry(DateTimeOffset.Now, "a1", "stop", 0x7E0, "PCM", 0x0300, "", RoutineOutcome.Pending, ""));
        File.AppendAllText(j.Path, "{\"At\":\"2026-11-16T10:00:00+00:00\",\"Id\":\"a2\",\"Act"); // torn write at power loss
        var open = Assert.Single(new RoutineJournal(_dir).Unfinished());
        Assert.Equal("a1", open.Id);
        Assert.Contains("the app or link stopped first", RoutineJournal.Describe(open));
    }

    [Fact]
    public void The_Routines_tab_journals_start_stop_and_procedures_and_checks_with_results()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var vm = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.App", "RoutinesViewModel.cs"));
        string Body(string name) { int i = vm.IndexOf(name, StringComparison.Ordinal); Assert.True(i > 0, name); return vm[i..vm.IndexOf("\n    }", i, StringComparison.Ordinal)]; }
        Assert.Contains("Journaled(\"start\", m, cmd.RoutineId", Body("private async Task StartAsync()"));
        Assert.Contains("Journaled(\"stop\", m, cmd.RoutineId", Body("private async Task StopAsync()"));
        Assert.Contains("MarkChecked(m, cmd,", Body("private async Task ResultsAsync()"));
        Assert.Contains("ShowUnfinished();", vm);
        Assert.Contains("new RoutineJournal(Path.Combine(Paths.DataDir, \"routines\"))", File.ReadAllText(Path.Combine(dir.FullName, "src", "ObdLink.App", "MainViewModel.cs")));
    }

    [Fact]
    public async Task Cancellation_is_rethrown_and_recorded_as_unknown()
    {
        var j = new RoutineJournal(_dir);
        await Assert.ThrowsAsync<OperationCanceledException>(() => j.RecordAsync<RoutineResult>("start", 0x726, "BCM", 0x201A, null, () => throw new OperationCanceledException()));
        Assert.Equal("cancelled before an answer", Assert.Single(j.Unfinished()).Detail);
    }
}
