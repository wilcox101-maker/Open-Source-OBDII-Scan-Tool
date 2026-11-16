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
        Assert.Equal("10 03, 31 01 20 1A, wait 15 s, 10 01", bms.RequestSummary);
        var steps = bms.ConfirmSteps("BCM (0x726)");
        Assert.Equal("UNVERIFIED", steps[^1].TypedPhrase);
        Assert.Contains("NOT been verified", steps[^1].Message);
        Assert.Contains("f150forum.com", steps[^1].Message);
        Assert.Contains("new battery", steps[1].Message);
    }

    [Fact]
    public void BMS_reset_asks_the_battery_question_first_and_a_No_stops_before_anything_is_sent()
    {
        var bms = ServiceProcedures.Find("bcm-bms-reset")!;
        var steps = bms.ConfirmSteps("BCM (0x726)");
        Assert.StartsWith("Did you disconnect and re-connect battery recently?", steps[0].Message);
        Assert.Null(steps[0].TypedPhrase); // a Yes/No question, not a typed phrase
        Assert.Equal(4, steps.Count);
        // Only the BMS reset carries the question; procedures without a gate keep their three steps.
        Assert.All(ServiceProcedures.All.Where(p => p.Key != "bcm-bms-reset"), p => Assert.Null(p.Gate));
    }

    [Fact]
    public async Task Answering_No_to_the_BMS_question_aborts_with_nothing_sent()
    {
        var bms = ServiceProcedures.Find("bcm-bms-reset")!;
        var asked = new List<string>();
        var guard = new ObdLink.Core.Safety.SafetyGuard(null!, new FirstNo(asked)); // RequireAsync never touches the adapter
        await Assert.ThrowsAsync<ObdLink.Core.Safety.UserDeclinedException>(() => guard.RequireAsync(bms.ConfirmSteps("BCM (0x726)")));
        Assert.Single(asked);
    }

    private sealed class FirstNo(List<string> asked) : ObdLink.Core.Safety.IConfirmationProvider
    {
        public Task<bool> ConfirmAsync(ObdLink.Core.Safety.ConfirmationStep step, CancellationToken ct = default)
        {
            asked.Add(step.Title);
            return Task.FromResult(false);
        }
    }

    [Fact]
    public void Transmission_KAM_targets_the_PCM_on_a_vehicle_without_a_TCM()
    {
        // KB-R3: Dale's 2017 F-150 has no TCM (20-module scan, factory .AB); the 6R80 is run by the PCM.
        var p = ServiceProcedures.Find("tcm-kam-reset")!;
        Assert.Equal(0x7E1u, p.TxId);
        bool? NoTcm(uint tx) => tx switch { 0x7E1 => false, 0x7E0 => true, _ => null };
        var r = ServiceProcedures.ResolveFor(p, NoTcm);
        Assert.Equal(("PCM", 0x7E0u), (r.Module, r.TxId));
        Assert.Contains("not the PCM KAM reset", r.TargetNote);
        Assert.Equal(p.Key, r.Key);
        Assert.False(r.Runnable); // still NeedsCapture: resolving the target never makes a procedure runnable

        // A TCM that answered keeps it; before a scan nothing moves but the card says what will happen.
        Assert.Equal(0x7E1u, ServiceProcedures.ResolveFor(p, tx => true).TxId);
        var unscanned = ServiceProcedures.ResolveFor(p, _ => null);
        Assert.Equal(0x7E1u, unscanned.TxId);
        Assert.StartsWith("Scan first", unscanned.TargetNote);
        // No PCM either: nowhere better to go, stay on the TCM.
        Assert.Equal(0x7E1u, ServiceProcedures.ResolveFor(p, _ => false).TxId);
        // Procedures without a fallback never move.
        var bms = ServiceProcedures.Find("bcm-bms-reset")!;
        Assert.Same(bms, ServiceProcedures.ResolveFor(bms, _ => false));
    }

    [Fact]
    public void The_Routines_tab_uses_the_resolved_target_for_selection_card_and_run()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var vm = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.App", "RoutinesViewModel.cs"));
        Assert.Contains("ServiceProcedures.ResolveFor(_procedure, _present)", vm);
        Assert.Contains("if (ResolvedProcedure is { } t) _selectModule?.Invoke(t.TxId);", vm);
        Assert.Contains("var p = ResolvedProcedure ?? throw", vm);
        Assert.Contains(": Card(t);", vm);
        var main = File.ReadAllText(Path.Combine(dir.FullName, "src", "ObdLink.App", "MainViewModel.cs"));
        Assert.Contains("Routines.VehicleChanged();", main);
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
        public byte? StartNrc, ResultsNrc = 0x12, DefaultNrc;
        public List<byte[]> Calls { get; } = new();
        public Task SetTargetAsync(uint txId, uint? rxId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<DiagResponse>> RequestAsync(byte[] r, TimeSpan? timeout = null, CancellationToken ct = default)
        {
            Calls.Add(r.ToArray());
            if (r[0] == 0x3E && r[1] == 0x80) return Task.FromResult<IReadOnlyList<DiagResponse>>(Array.Empty<DiagResponse>()); // suppressed reply
            byte[] a = r[0] switch
            {
                0x10 when r[1] == 0x01 && DefaultNrc is byte d => new byte[] { 0x7F, 0x10, d },
                0x10 => new byte[] { 0x50, r[1], 0x00, 0x32, 0x01, 0xF4 },
                0x31 when r[1] == 0x01 => StartNrc is byte n ? new byte[] { 0x7F, 0x31, n } : new byte[] { 0x71, 0x01, r[2], r[3] },
                0x31 => ResultsNrc is byte m ? new byte[] { 0x7F, 0x31, m } : new byte[] { 0x71, 0x03, r[2], r[3], 0x00 },
                _ => new byte[] { 0x7F, r[0], 0x11 },
            };
            return Task.FromResult<IReadOnlyList<DiagResponse>>(new[] { new DiagResponse(0x72E, a) });
        }
    }

    [Fact]
    public async Task BMS_reset_sends_the_session_and_the_routine_then_keeps_the_session_15_s_and_returns_to_default()
    {
        var m = new Module();
        var waits = new List<TimeSpan>();
        var run = await ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x726), ServiceProcedures.Find("bcm-bms-reset")!, delay: t => { waits.Add(t); return Task.CompletedTask; });
        // X2: 10 03, the routine, a 3E 80 every 2 s for 15 s (the caller holds the adapter lock, so the host's keep-alive can't), 10 01.
        Assert.Equal(new[] { "1003", "3101201A" }.Concat(Enumerable.Repeat("3E80", 8)).Append("1001"), m.Calls.Select(Convert.ToHexString));
        Assert.Equal(TimeSpan.FromSeconds(15), waits.Aggregate(TimeSpan.Zero, (a, b) => a + b));
        Assert.All(waits, w => Assert.True(w <= TimeSpan.FromSeconds(2)));
        Assert.Contains("accepted", run.Log[1]);
        Assert.Contains(run.Log, l => l == "default session (10 01): OK");
        Assert.Contains("done", run.Summary);
    }

    [Fact]
    public async Task A_procedure_without_a_settle_time_returns_to_default_right_after_the_routine()
    {
        var bms = ServiceProcedures.Find("bcm-bms-reset")! with { ExitSettle = null };
        var m = new Module();
        await ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x726), bms, delay: _ => Task.CompletedTask);
        Assert.Equal(new[] { "1003", "3101201A", "1001" }, m.Calls.Select(Convert.ToHexString));
    }

    [Fact]
    public async Task A_failed_10_01_is_reported_not_thrown()
    {
        var m = new Module { DefaultNrc = 0x22 };
        var run = await ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x726), ServiceProcedures.Find("bcm-bms-reset")! with { ExitSettle = null }, delay: _ => Task.CompletedTask);
        Assert.Contains(run.Log, l => l.StartsWith("default session (10 01) failed"));
    }

    [Fact]
    public async Task A_refusal_says_nothing_ran_and_explains_the_NRC()
    {
        var m = new Module { StartNrc = 0x31 };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => ServiceProcedureRunner.RunAsync(new UdsClient(m, 0x726), ServiceProcedures.Find("bcm-bms-reset")!));
        Assert.Contains("nothing ran", ex.Message);
        Assert.Contains("does not apply to this model year", ex.Message);
        Assert.Single(m.Calls, c => c[0] == 0x31);   // never retried
        // Nothing ran, so no settle wait: straight back to the default session.
        Assert.Equal("1001", Convert.ToHexString(m.Calls[^1]));
        Assert.DoesNotContain(m.Calls, c => c[0] == 0x3E);
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

    [Fact]
    public void The_app_checks_the_battery_and_confirms_before_sending_and_never_falls_back_to_another_bus()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ObdLink.sln"))) dir = dir.Parent;
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "src", "ObdLink.App", "RoutinesViewModel.cs"));
        int start = src.IndexOf("private async Task RunProcedureAsync()", StringComparison.Ordinal);
        Assert.True(start > 0);
        var body = src[start..src.IndexOf("private static string Card(", start, StringComparison.Ordinal)];
        int runnable = body.IndexOf("if (!p.Runnable)", StringComparison.Ordinal), module = body.IndexOf("m.Def.TxId != p.TxId", StringComparison.Ordinal);
        int volts = body.IndexOf("EnsureVoltageAsync()", StringComparison.Ordinal), confirm = body.IndexOf("RequireAsync(p.ConfirmSteps(", StringComparison.Ordinal);
        int send = body.IndexOf("ServiceProcedureRunner.RunAsync(", StringComparison.Ordinal);
        Assert.True(runnable >= 0 && runnable < module && module < volts && volts < confirm && confirm < send, $"{runnable} {module} {volts} {confirm} {send}");
        Assert.Contains("r => r.Summary), false);", body);   // no bus fallback for a write
        Assert.Contains("Journaled($\"procedure:{p.Key}\"", body); // R1: the run is journaled (pending before the request)
        var xaml = File.ReadAllText(Path.Combine(dir.FullName, "src", "ObdLink.App", "MainWindow.xaml"));
        Assert.Contains("{Binding Routines.RunProcedureCommand}", xaml);
        Assert.Contains("{Binding Routines.ProcedureText}", xaml);
    }
}
