using ObdLink.Core.IsoTp;
using ObdLink.Core.Safety;
using ObdLink.Core.Service;
using ObdLink.Core.Uds;
using Xunit;

namespace ObdLink.Core.Tests;

/// <summary>Service procedure catalog, its generated doc, and the runner against a SYNTHETIC module.</summary>
public class ServiceProcedureTests
{
    private static string DocPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "docs", "SERVICE-PROCEDURES.md");
    }

    [Fact]
    public void The_doc_matches_the_catalog()
    {
        var gen = ServiceProceduresDoc.Markdown();
        var path = DocPath();
        var current = File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";
        if (Environment.GetEnvironmentVariable("UPDATE_DOCS") == "1" && current != gen) { File.WriteAllText(path, gen); return; }
        Assert.True(current == gen, "docs/SERVICE-PROCEDURES.md is out of date. Run: UPDATE_DOCS=1 dotnet test tests/ObdLink.Core.Tests --filter ServiceProcedureTests");
    }

    [Fact]
    public void The_catalog_has_no_problems() => Assert.Empty(ServiceProcedures.Problems());

    [Fact]
    public void Catalog_checks_catch_a_refused_routine_and_identifiers_without_a_status()
    {
        var bms = ServiceProcedures.Find("bcm-bms-reset")!;
        var pyro = bms with { Key = "x", Steps = new[] { new ProcedureStep(ProcedureStepKind.StartRoutine, "x", RoutineId: 0xE201) } };
        var capture = ServiceProcedures.Find("pcm-kam-reset")! with { Key = "y", Steps = bms.Steps };
        var noSource = bms with { Key = "z", Sources = Array.Empty<ProcedureSource>() };
        var problems = ServiceProcedures.Problems(new[] { pyro, capture, noSource, noSource }).ToList();
        Assert.Contains(problems, p => p.Contains("0xE201") && p.Contains("refused"));
        Assert.Contains(problems, p => p.StartsWith("y:") && p.Contains("needs capture but has steps"));
        Assert.Contains(problems, p => p.StartsWith("z:") && p.Contains("no source"));
        Assert.Contains(problems, p => p.Contains("duplicate key z"));
    }

    [Fact]
    public void Unverified_procedures_need_the_UNVERIFIED_phrase_and_say_where_the_ID_came_from()
    {
        var bms = ServiceProcedures.Find("bcm-bms-reset")!;
        Assert.True(bms.Runnable);
        Assert.Equal("10 03, 31 01 20 1A", bms.RequestSummary);
        var steps = bms.ConfirmSteps("BCM (0x726)");
        Assert.Equal("UNVERIFIED", steps[^1].TypedPhrase);
        Assert.Contains("NOT been verified", steps[^1].Message);
        Assert.Contains("f150forum.com", steps[^1].Message);
        Assert.Contains("new battery", steps[0].Message);
    }

    [Fact]
    public async Task A_procedure_without_identifiers_cannot_be_confirmed_or_run()
    {
        var kam = ServiceProcedures.Find("pcm-kam-reset")!;
        Assert.False(kam.Runnable);
        Assert.Throws<InvalidOperationException>(() => kam.ConfirmSteps("PCM"));
        var m = new Module();
        await Assert.ThrowsAsync<InvalidOperationException>(() => ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x7E0), kam));
        Assert.Empty(m.Calls);
    }

    /// <summary>A BCM that answers 10 03 and 31 01 201A (or the NRC set in <see cref="StartNrc"/>), and 31 03 with <see cref="ResultsNrc"/>.</summary>
    private sealed class Module : IDiagChannel
    {
        public byte? StartNrc, ResultsNrc = 0x12;
        public List<byte[]> Calls { get; } = new();
        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] r, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Calls.Add(r.ToArray());
            byte[] a = r[0] switch
            {
                0x10 => new byte[] { 0x50, r[1], 0x00, 0x32, 0x01, 0xF4 },
                0x31 when r[1] == 0x01 => StartNrc is byte n ? new byte[] { 0x7F, 0x31, n } : new byte[] { 0x71, 0x01, r[2], r[3] },
                0x31 => ResultsNrc is byte m ? new byte[] { 0x7F, 0x31, m } : new byte[] { 0x71, 0x03, r[2], r[3], 0x00 },
                _ => new byte[] { 0x7F, r[0], 0x11 },
            };
            return Task.FromResult<IReadOnlyList<DiagResponse>>(new[] { new DiagResponse(0x72E, a) });
        }
    }

    [Fact]
    public async Task BMS_reset_sends_the_extended_session_then_the_routine_and_nothing_else()
    {
        var m = new Module();
        var run = await ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x726), ServiceProcedures.Find("bcm-bms-reset")!, delay: _ => Task.CompletedTask);
        Assert.Equal(new[] { "1003", "3101201A" }, m.Calls.Select(Convert.ToHexString));
        Assert.Contains("accepted", run.Log[1]);
        Assert.Contains("done", run.Summary);
    }

    [Fact]
    public async Task A_refusal_says_nothing_ran_and_explains_the_NRC()
    {
        var m = new Module { StartNrc = 0x31 };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x726), ServiceProcedures.Find("bcm-bms-reset")!));
        Assert.Contains("nothing ran", ex.Message);
        Assert.Contains("does not apply to this model year", ex.Message);
        Assert.Single(m.Calls, c => c[0] == 0x31);   // never retried
    }

    [Fact]
    public async Task Writes_off_blocks_the_routine()
    {
        var m = new Module();
        var uds = new UdsClient(m, 0x726) { Gate = new WriteGate(() => true) };
        await Assert.ThrowsAsync<ReadOnlyModeException>(() => ServiceProcedureRunner.RunAsync(uds, ServiceProcedures.Find("bcm-bms-reset")!));
        Assert.DoesNotContain(m.Calls, c => c[0] == 0x31);
    }

    [Fact]
    public async Task The_wrong_module_is_refused_before_anything_is_sent()
    {
        var m = new Module();
        await Assert.ThrowsAsync<ArgumentException>(() => ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x7E0), ServiceProcedures.Find("bcm-bms-reset")!));
        Assert.Empty(m.Calls);
    }

    [Fact]
    public async Task A_module_that_cannot_report_results_is_not_a_failure()
    {
        var bms = ServiceProcedures.Find("bcm-bms-reset")!;
        var withResults = bms with { Steps = bms.Steps.Append(new ProcedureStep(ProcedureStepKind.RequestResults, "results", RoutineId: 0x201A)).ToArray() };
        var run = await ServiceProcedureRunner.RunAsync(new UdsClient(new Module(), 0x726), withResults, delay: _ => Task.CompletedTask);
        Assert.Contains(run.Log, l => l.Contains("does not report results"));
        var ok = await ServiceProcedureRunner.RunAsync(new UdsClient(new Module { ResultsNrc = null }, 0x726), withResults, delay: _ => Task.CompletedTask);
        Assert.Equal(new byte[] { 0x00 }, ok.LastStatus);
    }
}
