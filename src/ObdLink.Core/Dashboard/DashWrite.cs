using ObdLink.Core.Uds;

namespace ObdLink.Core.Dashboard;

/// <summary>What a DID write card found after writing and reading back.</summary>
public enum DashWriteVerdict
{
    /// <summary>The module holds the written value.</summary>
    Verified,
    /// <summary>The module holds a value that is neither the written one nor the one read before.</summary>
    Different,
    /// <summary>The module refused the write (negative response: not carried out, ISO 14229-1) or still holds the old value.</summary>
    NotApplied,
    /// <summary>The reply was lost and the DID could not be read back: whether the write landed is not known.</summary>
    Unknown,
}

/// <summary>Outcome of <see cref="DashWrite.WriteAndSettleAsync"/>. <see cref="Message"/> says what happened and what to do next.</summary>
public sealed record DashWriteOutcome(DashWriteVerdict Verdict, byte[]? ReadBack, bool ReplyLost, byte? Nrc, string Message)
{
    public bool Verified => Verdict == DashWriteVerdict.Verified;
}

/// <summary>
/// One DID write (2E) whose result is always settled by reading the DID back (22), so the card never reports "failed" for a write
/// that landed or "done" for one that did not:
/// <list type="bullet">
/// <item>A lost or "pending" reply (timeout, or NRC 0x78 on an ELM-style adapter that cannot keep listening) is uncertain: the
/// engine waits <see cref="SettleDelay"/> (the module may still be writing its EEPROM) and reads back, up to <see cref="ReadAttempts"/> times.</item>
/// <item>Any other negative response is definite (the request was not carried out) and comes back as <see cref="DashWriteVerdict.NotApplied"/>
/// with the likely cause and the next step for that NRC.</item>
/// <item>A read-back that differs gets a next step: ignition cycle and read again, or Undo.</item>
/// </list>
/// Transport failures (adapter gone) are not settled: they are thrown so the caller reports the link.
/// </summary>
public static class DashWrite
{
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(500);
    public const int ReadAttempts = 3;

    public static async Task<DashWriteOutcome> WriteAndSettleAsync(UdsClient uds, ushort did, byte[] data, byte[]? before,
        Func<TimeSpan, Task>? delay = null, CancellationToken ct = default)
    {
        delay ??= t => Task.Delay(t, ct);
        string id = $"DID 0x{did:X4}";
        bool lost = false;
        string? lostWhy = null;
        try { await uds.WriteDataByIdentifierAsync(did, data, ct); }
        catch (TimeoutException ex) { lost = true; lostWhy = ex.Message; }
        catch (UdsNegativeResponseException ex) when (ex.Nrc == 0x78) { lost = true; lostWhy = "the module answered \"response pending\" and the adapter could not wait for the final answer"; }
        catch (UdsNegativeResponseException ex)
        {
            return new(DashWriteVerdict.NotApplied, null, false, ex.Nrc,
                $"{id}: the module refused the write (NRC 0x{ex.Nrc:X2} {UdsNrc.Describe(ex.Nrc)}); nothing was changed. {NextStep(ex.Nrc)}");
        }

        byte[]? back = null;
        Exception? readErr = null;
        for (int i = 0; i < ReadAttempts && back is null; i++)
        {
            if (lost || i > 0) await delay(SettleDelay);
            try { back = await uds.ReadDataByIdentifierAsync(did, ct); }
            catch (Exception ex) when (ex is TimeoutException or UdsNegativeResponseException { Nrc: 0x78 or 0x21 }) { readErr = ex; }
            catch (UdsNegativeResponseException ex) { readErr = ex; break; }   // definite: reading this DID is not possible now
        }

        string w = Convert.ToHexString(data);
        if (back is null)
            return new(lost ? DashWriteVerdict.Unknown : DashWriteVerdict.Different, null, lost, null,
                lost ? $"{id}: no answer to the write ({lostWhy}) and the DID could not be read back ({readErr?.Message}). The write may or may not have landed: read the DID again before writing anything else."
                     : $"{id}: the module accepted {w}, but the DID could not be read back ({readErr?.Message}). Read it again to check.");

        string b = Convert.ToHexString(back);
        string note = lost ? " (the reply to the write was lost; settled by reading back)" : "";
        if (back.AsSpan().SequenceEqual(data))
            return new(DashWriteVerdict.Verified, back, lost, null, $"{id}: wrote {w}; read back {b}: verified{note}.");
        if (before is not null && back.AsSpan().SequenceEqual(before))
            return new(DashWriteVerdict.NotApplied, back, lost, null, lost
                ? $"{id}: the write did not land (no answer, and the DID still holds {b}). Nothing changed; it is safe to try again."
                : $"{id}: the module accepted {w} but still holds {b}. Some values are only stored or applied after a reset: turn the ignition OFF for 10 s and ON, then read the DID again.");
        return new(DashWriteVerdict.Different, back, lost, null,
            $"{id}: wrote {w}; read back {b}: DIFFERENT{note}. The module may limit or reformat the value, or apply it only after a reset: turn the ignition OFF for 10 s and ON and read the DID again. Undo… writes the old value back.");
    }

    /// <summary>The usual cause and next step for a refused DID write (ISO 14229-1 meanings; Ford specifics [UNVERIFIED]).</summary>
    public static string NextStep(byte nrc) => nrc switch
    {
        0x13 => "The data length is wrong for this DID: check the value's length against the source you took it from.",
        0x22 => "Conditions not correct: usually ignition ON with the engine OFF, vehicle stopped, or another session. Check the conditions and try again.",
        0x31 => "Out of range: the DID is not writable on this module, or the value is not allowed.",
        0x33 => "This DID needs security access, which write cards do not do. Use a procedure that unlocks the module first.",
        0x72 => "General programming failure: the module could not store the value. Check the battery voltage; do not repeat the write many times.",
        0x7E or 0x7F => "Not allowed in this session: the module may need a different diagnostic session for this DID.",
        _ => "Check the DID and value against your source before trying again.",
    };
}
