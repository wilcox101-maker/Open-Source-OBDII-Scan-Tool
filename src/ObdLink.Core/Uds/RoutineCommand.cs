using ObdLink.Core.Safety;

namespace ObdLink.Core.Uds;

/// <summary>A routine the user typed in: identifier and optional option record, already checked against the refused ranges.</summary>
public sealed record RoutineCommand(ushort RoutineId, byte[] Option)
{
    /// <summary>
    /// Parses "0202" / "0x0202" / "02 02" and an optional hex option record ("" = none). Throws <see cref="FormatException"/>
    /// with a user-facing message, or <see cref="RoutineRefusedException"/> for identifiers this tool never sends.
    /// </summary>
    public static RoutineCommand Parse(string? routineId, string? option)
    {
        var rid = Hex(routineId, "Routine ID");
        if (rid.Length != 2) throw new FormatException("Routine ID must be exactly 2 bytes (4 hex digits), e.g. 0202");
        ushort id = (ushort)(rid[0] << 8 | rid[1]);
        if (UdsRoutines.RefusalReason(id) is string why) throw new RoutineRefusedException(id, why);
        var opt = string.IsNullOrWhiteSpace(option) ? Array.Empty<byte>() : Hex(option, "Option record");
        if (opt.Length > 4091) throw new FormatException("Option record is longer than one ISO-TP message allows");
        return new RoutineCommand(id, opt);
    }

    public string IdText => $"0x{RoutineId:X4}";

    /// <summary>Confirmation steps before a routine is started. Ends with a typed phrase because a routine can move actuators.</summary>
    public IEnumerable<ConfirmationStep> StartSteps(string module)
    {
        yield return new ConfirmationStep("Routine conditions",
            $"Start routine {IdText} in {module}? A routine can run actuators, change module state or set DTCs. " +
            "This tool does not know what this routine does: only run IDs from a source you trust. " +
            "Vehicle parked, ignition ON, engine OFF unless the procedure says otherwise, battery maintainer connected, nobody near moving parts?");
        yield return new ConfirmationStep("Start routine",
            $"31 01 {RoutineId >> 8:X2} {RoutineId & 0xFF:X2}{(Option.Length > 0 ? " " + Convert.ToHexString(Option) : "")} to {module}. Type RUN to send it.", "RUN");
    }

    public IEnumerable<ConfirmationStep> StopSteps(string module)
    {
        yield return new ConfirmationStep("Stop routine", $"Send stop (31 02) for routine {IdText} to {module}?");
    }

    /// <summary>One line for the results list: "12:00:01 PCM 0x7E0 start 0x0202: 71 status 20 AA".</summary>
    public static string Describe(DateTimeOffset at, string module, RoutineResult r)
    {
        var what = r.Type switch { RoutineControlType.Start => "start", RoutineControlType.Stop => "stop", _ => "results" };
        var status = r.StatusRecord.Length == 0 ? "accepted (no status bytes)" : "status " + Convert.ToHexString(r.StatusRecord);
        return $"{at.ToLocalTime():HH:mm:ss} {module} {what} 0x{r.RoutineId:X4}: {status}";
    }

    private static byte[] Hex(string? s, string what)
    {
        var t = (s ?? "").Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        t = t.Replace(" ", "").Replace("-", "");
        if (t.Length == 0) throw new FormatException($"{what} is empty");
        if (t.Length % 2 != 0 || !t.All(Uri.IsHexDigit)) throw new FormatException($"{what} '{s}' is not whole hex bytes");
        return Convert.FromHexString(t);
    }
}
