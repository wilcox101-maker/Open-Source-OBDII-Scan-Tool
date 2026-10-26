namespace ObdLink.Core.ConsoleCmds;

/// <summary>
/// Decides whether a raw console line may be sent. Read-only mode and "Allow writes" off both refuse
/// state-changing services. With CAN auto-formatting on (ELM default, ATCAF1) the service id is the first byte,
/// so <c>01 2E</c> stays a mode-01 PID read. With formatting off, the id is also taken from an ISO-TP
/// single-frame or first-frame PCI. <c>ATCAF0</c> is refused while writes are locked so formatting cannot be
/// turned off in that state. <c>STPX</c> and <c>ATWM</c> can transmit an arbitrary frame and are refused.
/// A control character is refused so one line cannot smuggle a second command after a carriage return.
/// </summary>
public static class ConsoleGuard
{
    public static readonly byte[] StateChanging =
    {
        0x04, 0x08, 0x11, 0x14, 0x27, 0x28, 0x2C, 0x2E, 0x2F,
        0x31, 0x34, 0x35, 0x36, 0x37, 0x38, 0x3B, 0x3D, 0x85, 0x86, 0x87,
    };

    public readonly record struct Decision(bool Allowed, string? Reason);

    public static Decision Evaluate(string input, bool readOnly, bool allowWrites, bool canAutoFormat = true)
    {
        if (input.Any(char.IsControl)) return Block(readOnly);
        var compact = input.Trim().Replace(" ", "").ToUpperInvariant();
        if (compact.Length == 0 || compact is "HELP" or "?" or "/?" or "/HELP" or "CLS" or "CLEAR") return new(true, null);
        if (!readOnly && allowWrites) return new(true, null);
        if (compact.StartsWith("STPX", StringComparison.Ordinal) || compact.StartsWith("ATWM", StringComparison.Ordinal) || compact == "ATCAF0") return Block(readOnly);
        if (HitsStateChange(compact, canAutoFormat)) return Block(readOnly);
        return new(true, null);
    }

    /// <summary>ELM ATZ / ATWS / ATD restore auto-formatting. ATCAF0 turns it off; ATCAF and ATCAF1 turn it on.</summary>
    public static bool NextAutoFormat(bool current, string sent)
    {
        var compact = sent.Trim().Replace(" ", "").ToUpperInvariant();
        if (compact is "ATZ" or "ATWS" or "ATD") return true;
        if (compact == "ATCAF0") return false;
        if (compact is "ATCAF" or "ATCAF1") return true;
        return current;
    }

    public static Decision Block(bool readOnly) => new(false, readOnly
        ? "BLOCKED: read-only test mode is ON (Settings) - state-changing services disabled"
        : "BLOCKED: state-changing service; enable writes in console settings first");

    public static bool HitsStateChange(string compact, bool canAutoFormat = true)
    {
        if (compact.Length < 2 || compact.Length % 2 != 0 || !compact.All(Uri.IsHexDigit)) return false;
        byte[] bytes;
        try { bytes = Convert.FromHexString(compact); } catch (FormatException) { return false; }
        if (IsChanging(bytes[0])) return true;
        if (canAutoFormat) return false;
        // Formatting off: the user supplies the ISO-TP PCI. Single frame 0x0L (L = 1..7). The adapter
        // pads a short CAN frame, so the byte count does not have to equal L for the service id to be sent.
        if (bytes.Length >= 2 && bytes[0] >= 0x01 && bytes[0] <= 0x07 && IsChanging(bytes[1])) return true;
        // First frame: high nibble 0x1 and length > 7. A 2-byte "1003" is session control, not a first frame.
        if (bytes.Length >= 3 && (bytes[0] & 0xF0) == 0x10)
        {
            int len = ((bytes[0] & 0x0F) << 8) | bytes[1];
            if (len > 7 && IsChanging(bytes[2])) return true;
        }
        return false;
    }

    private static bool IsChanging(byte sid) => StateChanging.Contains(sid);
}
