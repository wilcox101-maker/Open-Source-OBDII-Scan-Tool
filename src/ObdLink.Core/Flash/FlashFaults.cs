using ObdLink.Core.IsoTp;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Flash;

/// <summary>What the engine does about a failed request during a flash.</summary>
public enum FlashFaultAction
{
    /// <summary>Send the same request again (TransferData keeps the same block sequence counter) after a short delay.</summary>
    Retry,
    /// <summary>Response pending (0x78) that the channel could not wait out: wait P2*, then settle (resend or poll).</summary>
    WaitPending,
    /// <summary>Supply voltage out of range (0x92/0x93): wait until the battery is back in the window, then resend.</summary>
    WaitVoltage,
    /// <summary>The module refused the step for good (programming failure, sequence error, ...). Stop and go to recovery.</summary>
    Fatal,
    /// <summary>Security problem (invalid key, too many attempts, delay). Stop; re-authenticating needs the operator.</summary>
    Security,
    /// <summary>The adapter or port went away. Stop sending; recovery reconnects (same or other adapter) and resumes.</summary>
    LinkLost,
}

public sealed record FlashFault(FlashFaultAction Action, string Reason, byte? Nrc = null);

/// <summary>Maps exceptions from the UDS / ISO-TP / adapter stack to flash actions. One table, unit-tested per NRC.</summary>
public static class FlashFaults
{
    public static FlashFault Classify(Exception ex) => ex switch
    {
        UdsNegativeResponseException n => FromNrc(n.Nrc, n.Message),
        TimeoutException => new(FlashFaultAction.Retry, "no answer in time: " + ex.Message),
        IsoTpException => new(FlashFaultAction.Retry, "ISO-TP transfer problem: " + ex.Message),
        DiagLinkException d => new(FlashFaultAction.Retry, "adapter reported " + d.Error + ": " + ex.Message),
        IOException or ObjectDisposedException or UnauthorizedAccessException => new(FlashFaultAction.LinkLost, "adapter connection lost: " + ex.Message),
        InvalidOperationException when ex.Message.Contains("port", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("closed", StringComparison.OrdinalIgnoreCase)
            => new(FlashFaultAction.LinkLost, "adapter connection lost: " + ex.Message),
        InvalidDataException => new(FlashFaultAction.Retry, "garbled reply: " + ex.Message),
        _ => new(FlashFaultAction.Fatal, ex.GetType().Name + ": " + ex.Message),
    };

    public static FlashFault FromNrc(byte nrc, string detail = "") => nrc switch
    {
        0x78 => new(FlashFaultAction.WaitPending, "response pending", nrc),
        0x21 => new(FlashFaultAction.Retry, "module busy, repeat request", nrc),
        0x92 or 0x93 => new(FlashFaultAction.WaitVoltage, nrc == 0x92 ? "supply voltage too high" : "supply voltage too low", nrc),
        0x33 or 0x35 or 0x36 or 0x37 => new(FlashFaultAction.Security, UdsNrc.Describe(nrc), nrc),
        // 0x73 wrong block sequence counter: a repeat of the last accepted counter is answered positively (ISO 14229-1), so this is
        // a real desync. Restarting the segment from RequestDownload is the only safe fix.
        _ => new(FlashFaultAction.Fatal, $"NRC 0x{nrc:X2} {UdsNrc.Describe(nrc)}" + (detail.Length > 0 ? $" ({detail})" : ""), nrc),
    };
}

/// <summary>The flash stopped. <see cref="ModuleMayBeInBootloader"/> is true once the programming session was entered: run recovery.</summary>
public class FlashFailedException(string message, string phase, bool moduleMayBeInBootloader, FlashFault? fault = null, Exception? inner = null)
    : Exception(message, inner)
{
    public string Phase { get; } = phase;
    public bool ModuleMayBeInBootloader { get; } = moduleMayBeInBootloader;
    public FlashFault? Fault { get; } = fault;
}

/// <summary>The adapter link was lost or retries ran out mid-job. The job stays open in the journal; reconnect and call resume.</summary>
public sealed class FlashInterruptedException(string message, string phase, FlashFault fault, Exception? inner = null)
    : FlashFailedException(message, phase, true, fault, inner);

/// <summary>A preflight check failed before anything was sent to the module.</summary>
public sealed class FlashPreflightException(FlashPreflightReport report)
    : Exception("Preflight failed: " + string.Join("; ", report.Items.Where(i => i.Status == PreflightStatus.Fail).Select(i => $"{i.Name}: {i.Detail}")))
{
    public FlashPreflightReport Report { get; } = report;
}
