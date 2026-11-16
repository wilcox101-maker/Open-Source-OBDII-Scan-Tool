using System.Globalization;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Dashboard;

/// <summary>What a dashboard write card sends.</summary>
public enum DashActionKind
{
    /// <summary>31 01 routine [option], in the extended session. Same refusals as the Routines tab (pyrotechnic, FF00/FF01).</summary>
    Routine,
    /// <summary>2F did 03 state: the tester takes over an output; released by 2F did 00 on a second press, after <see cref="DashActions.HoldSeconds"/>, or when the window closes.</summary>
    OutputHold,
    /// <summary>2E did data, with the current value read first (shown in the confirmation and logged) and a read-back check after.</summary>
    WriteDid,
}

/// <summary>A write card on a dashboard layout. Stored as text so a hand-edited or imported layout is validated before every use.</summary>
public sealed class DashAction
{
    public DashActionKind Kind { get; set; }
    /// <summary>Module request CAN ID, hex (e.g. "7E0", "726").</summary>
    public string Module { get; set; } = "";
    /// <summary>Routine identifier or DID, 2 bytes hex.</summary>
    public string Id { get; set; } = "";
    /// <summary>Routine option record, output state, or DID data, hex. May be empty for a routine.</summary>
    public string Data { get; set; } = "";
    /// <summary>Card title chosen by the user.</summary>
    public string Label { get; set; } = "";
    public DashAction Clone() => new() { Kind = Kind, Module = Module, Id = Id, Data = Data, Label = Label };
}

/// <summary>A validated write card: what will be sent, to which module, and the phrase the user must type.</summary>
public sealed record DashActionPlan(DashActionKind Kind, uint TxId, ushort Id, byte[] Data, string Phrase)
{
    public string RequestHex => Kind switch
    {
        DashActionKind.Routine => $"31 01 {Id:X4}" + (Data.Length > 0 ? " " + Convert.ToHexString(Data) : ""),
        DashActionKind.OutputHold => $"2F {Id:X4} 03 {Convert.ToHexString(Data)}",
        _ => $"2E {Id:X4} {Convert.ToHexString(Data)}",
    };
    public string Summary => Kind switch
    {
        DashActionKind.Routine => $"Routine 0x{Id:X4} on 0x{TxId:X3}",
        DashActionKind.OutputHold => $"Hold output DID 0x{Id:X4} on 0x{TxId:X3} (max {DashActions.HoldSeconds} s)",
        _ => $"Write DID 0x{Id:X4} on 0x{TxId:X3} ({Data.Length} bytes)",
    };
}

/// <summary>
/// Validation and confirmation text for dashboard write cards. Every card is checked again before it runs, so a layout edited
/// by hand or imported from a file can't bypass the refusals. The App runs the plan under the write gate, the battery check and a
/// typed phrase, holding the shared adapter lock. Ford meanings of routine IDs, output states and DIDs are not shipped: the user
/// supplies them and is told so.
/// </summary>
public static class DashActions
{
    public const string KeyPrefix = "action:";
    /// <summary>An output hold is released automatically after this many seconds.</summary>
    public const int HoldSeconds = 10;

    public static bool IsActionKey(string? key) => key?.StartsWith(KeyPrefix, StringComparison.Ordinal) == true;
    public static string NewKey() => KeyPrefix + Guid.NewGuid().ToString("N")[..8];

    /// <summary>Validates a card. Throws <see cref="FormatException"/> (bad input) or <see cref="RoutineRefusedException"/> (never-sent routine).</summary>
    public static DashActionPlan Plan(DashAction a)
    {
        var tx = ParseModule(a.Module);
        switch (a.Kind)
        {
            case DashActionKind.Routine:
            {
                var cmd = RoutineCommand.Parse(a.Id, a.Data);
                return new(a.Kind, tx, cmd.RoutineId, cmd.Option, "RUN");
            }
            case DashActionKind.OutputHold:
            {
                var did = ParseId(a.Id, "Output DID");
                var state = Hex(a.Data, "Output state", allowEmpty: false);
                if (state.Length > 64) throw new FormatException("Output state is longer than 64 bytes");
                return new(a.Kind, tx, did, state, "HOLD");
            }
            case DashActionKind.WriteDid:
            {
                var did = ParseId(a.Id, "DID");
                if (did is >= 0xDE00 and <= 0xDEFF)
                    throw new FormatException($"DID 0x{did:X4} is an As-Built block: use the As-Built tab, which checks lengths and checksums and keeps a history.");
                if (did is >= 0xF180 and <= 0xF19F)
                    throw new FormatException($"DID 0x{did:X4} is an identification DID (VIN, part numbers); the dashboard does not write those.");
                var data = Hex(a.Data, "DID data", allowEmpty: false);
                if (data.Length > 4092) throw new FormatException("DID data is longer than one ISO-TP message allows");
                return new(a.Kind, tx, did, data, "WRITE");
            }
            default: throw new FormatException($"Unknown write card kind {a.Kind}");
        }
    }

    /// <summary>
    /// Undo for a DID write card: the value read before the write, ready to be written back. Null when that value was not read
    /// (nothing to put back). The plan goes through <see cref="Plan"/> again, so the same refusals apply.
    /// </summary>
    public static DashWriteUndo? UndoFor(DashAction spec, byte[]? before, byte[] written, byte[] readBack)
    {
        if (spec.Kind != DashActionKind.WriteDid || before is not { Length: > 0 }) return null;
        var back = spec.Clone(); back.Data = Convert.ToHexString(before);
        return new DashWriteUndo(Plan(back), written, readBack);
    }

    /// <summary>Confirmation steps. <paramref name="currentHex"/> is the value read before a DID write, shown as old → new.</summary>
    public static IReadOnlyList<ConfirmationStep> Steps(DashActionPlan p, string label, string module, string? currentHex = null)
    {
        var what = p.Kind switch
        {
            DashActionKind.Routine => "A routine can run actuators, change module state or set DTCs.",
            DashActionKind.OutputHold => $"The output is taken over until you press the card again, for at most {HoldSeconds} s, or until the window closes. Then control returns to the module (2F 00).",
            _ => "The module stores the new value. The current value is logged first so it can be written back.",
        };
        var change = p.Kind == DashActionKind.WriteDid ? $"\n{currentHex ?? "(current value unreadable)"} → {Convert.ToHexString(p.Data)}" : "";
        return new[]
        {
            new ConfirmationStep($"Dashboard: {label}",
                $"{p.Summary} in {module}. {what} This tool does not know what this ID does: only use values from a source you trust. " +
                "Vehicle parked, ignition ON, battery maintainer connected, nobody near moving parts?" + change),
            new ConfirmationStep($"Dashboard: {label}", $"{p.RequestHex} to {module}. Type {p.Phrase} to send it.", p.Phrase),
        };
    }

    public static uint ParseModule(string? s)
    {
        var t = (s ?? "").Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        if (!uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) || v is < 0x700 or > 0x7FF)
            throw new FormatException($"Module '{s}' must be an 11-bit request ID from 700 to 7FF (hex), e.g. 7E0 or 726");
        return v;
    }

    private static ushort ParseId(string? s, string what)
    {
        var b = Hex(s, what, allowEmpty: false);
        if (b.Length != 2) throw new FormatException($"{what} must be exactly 2 bytes (4 hex digits)");
        return (ushort)(b[0] << 8 | b[1]);
    }

    private static byte[] Hex(string? s, string what, bool allowEmpty)
    {
        var t = (s ?? "").Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) t = t[2..];
        t = t.Replace(" ", "").Replace("-", "");
        if (t.Length == 0) return allowEmpty ? Array.Empty<byte>() : throw new FormatException($"{what} is empty");
        if (t.Length % 2 != 0 || !t.All(Uri.IsHexDigit)) throw new FormatException($"{what} '{s}' is not whole hex bytes");
        return Convert.FromHexString(t);
    }
}

/// <summary>
/// Writing back the value a DID write card replaced. <see cref="Plan"/> writes the old value; <see cref="Refusal"/> keeps an undo
/// from overwriting a value someone or something else has written since (only what this card left there may be undone).
/// </summary>
public sealed record DashWriteUndo(DashActionPlan Plan, byte[] Written, byte[] ReadBack)
{
    public byte[] Before => Plan.Data;

    /// <summary>Null when the module still holds what the card wrote (or what it read back right after); otherwise why the undo is refused.</summary>
    public string? Refusal(byte[] current) =>
        current.AsSpan().SequenceEqual(Written) || current.AsSpan().SequenceEqual(ReadBack) ? null
        : current.AsSpan().SequenceEqual(Before) ? "The module already holds the old value; nothing to undo."
        : $"DID 0x{Plan.Id:X4} now holds {Convert.ToHexString(current)}, not the {Convert.ToHexString(Written)} this card wrote. It was changed since; undo refused so that change is not overwritten.";

    public string Summary => $"Undo: write DID 0x{Plan.Id:X4} on 0x{Plan.TxId:X3} back to {Convert.ToHexString(Before)}";
}
