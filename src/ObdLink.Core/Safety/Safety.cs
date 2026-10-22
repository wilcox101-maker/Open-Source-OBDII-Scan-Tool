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

public sealed class SafetyException(string m) : Exception(m);

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
    public SafetyGuard(AdapterSession s, IConfirmationProvider c, SafetyOptions? o = null) { _session = s; _confirm = c; Options = o ?? new(); }

    public async Task EnsureVoltageAsync(CancellationToken ct = default)
    {
        var v = await _session.ReadBatteryVoltageAsync(ct) ?? throw new SafetyException("Could not read battery voltage (ATRV)");
        if (v < Options.MinWriteVoltage) throw new SafetyException($"Battery {v:0.0} V below minimum {Options.MinWriteVoltage:0.0} V");
        if (v > Options.MaxVoltage) throw new SafetyException($"Battery {v:0.0} V above maximum {Options.MaxVoltage:0.0} V");
    }

    public async Task RequireAsync(IEnumerable<ConfirmationStep> steps, CancellationToken ct = default)
    {
        foreach (var s in steps)
            if (!await _confirm.ConfirmAsync(s, ct)) throw new SafetyException($"User declined: {s.Title}");
    }

    public static IEnumerable<ConfirmationStep> WriteSteps(string what) => new[]
    {
        new ConfirmationStep("Conditions", "Ignition ON, engine OFF, battery maintainer connected, no other tools on the bus?"),
        new ConfirmationStep("Backup", $"A backup will be saved before writing {what}. Continue?"),
        new ConfirmationStep("Final", $"Write {what} now? Type WRITE to confirm.", "WRITE"),
    };
}
