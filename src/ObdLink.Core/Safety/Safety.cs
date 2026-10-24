using ObdLink.Core.Elm;

namespace ObdLink.Core.Safety;

public sealed record ConfirmationStep(string Title, string Message, string? TypedPhrase = null);

/// <summary>UI implements this; every risky operation must pass all steps (any false = abort).</summary>
public interface IConfirmationProvider
{
    Task<bool> ConfirmAsync(ConfirmationStep step, CancellationToken ct = default);
}

public sealed class AutoConfirm(bool answer) : IConfirmationProvider
{
    public List<ConfirmationStep> Asked { get; } = new();
    public Task<bool> ConfirmAsync(ConfirmationStep s, CancellationToken ct = default) { Asked.Add(s); return Task.FromResult(answer); }
}

public class SafetyException(string m) : Exception(m);
/// <summary>The user answered No / Cancel to a confirmation. Logged as "Cancelled", not as an error.</summary>
public sealed class UserDeclinedException(string title) : SafetyException($"User declined: {title}") { public string Title { get; } = title; }

/// <summary>Wraps a confirmation provider and records every dialog in the event log as shown / accepted / cancelled.</summary>
public sealed class LoggingConfirmationProvider(IConfirmationProvider inner, Logging.IEventLog log) : IConfirmationProvider
{
    public async Task<bool> ConfirmAsync(ConfirmationStep step, CancellationToken ct = default)
    {
        var op = log.Begin("Confirmation dialog", ("title", step.Title), ("typed", step.TypedPhrase is not null));
        bool ok;
        try { ok = await inner.ConfirmAsync(step, ct); }
        catch (Exception ex) { op.Failed(ex); throw; }
        if (ok) op.Succeeded("Accepted: " + step.Title); else op.Cancelled(step.TypedPhrase is null ? "User answered No" : "Typed phrase not entered");
        return ok;
    }
}

public sealed class SafetyOptions
{
    /// <summary>Minimum ATRV voltage before any write. 12.5 V is a conservative project default, not an OEM figure. Use a battery maintainer.</summary>
    public double MinWriteVoltage { get; set; } = 12.5;
    public double MaxVoltage { get; set; } = 15.0;
}

public sealed class SafetyGuard
{
    private readonly AdapterSession _session;
    private readonly IConfirmationProvider _confirm;
    public SafetyOptions Options { get; }
    public WriteGate Gate { get; }
    public SafetyGuard(AdapterSession s, IConfirmationProvider c, SafetyOptions? o = null, WriteGate? gate = null) { _session = s; _confirm = c; Options = o ?? new(); Gate = gate ?? WriteGate.AlwaysAllow; }

    public async Task EnsureVoltageAsync(CancellationToken ct = default)
    {
        Gate.Require("write (voltage pre-check)");
        var v = await _session.ReadBatteryVoltageAsync(ct) ?? throw new SafetyException("Could not read battery voltage (ATRV)");
        if (v < Options.MinWriteVoltage) throw new SafetyException($"Battery {v:0.0} V below minimum {Options.MinWriteVoltage:0.0} V");
        if (v > Options.MaxVoltage) throw new SafetyException($"Battery {v:0.0} V above maximum {Options.MaxVoltage:0.0} V");
    }

    public async Task RequireAsync(IEnumerable<ConfirmationStep> steps, CancellationToken ct = default)
    {
        // Every risky operation goes through RequireAsync, so read-only mode is enforced here first.
        var list = steps.ToList();
        Gate.Require(list.FirstOrDefault()?.Title ?? "write");
        steps = list;
        foreach (var s in steps)
            if (!await _confirm.ConfirmAsync(s, ct)) throw new UserDeclinedException(s.Title);
    }

    public static IEnumerable<ConfirmationStep> WriteSteps(string what) => new[]
    {
        new ConfirmationStep("Conditions", "Ignition ON, engine OFF, battery maintainer connected, no other tools on the bus?"),
        new ConfirmationStep("Backup", $"A backup will be saved before writing {what}. Continue?"),
        new ConfirmationStep("Final", $"Write {what} now? Type WRITE to confirm.", "WRITE"),
    };
}
