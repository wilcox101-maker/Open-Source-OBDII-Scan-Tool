using System.Diagnostics;
using ObdLink.Core.Adapters;
using ObdLink.Core.Safety;
using ObdLink.Core.Uds;

namespace ObdLink.Core.Flash;

/// <summary>
/// Everything one flash job needs. Defaults follow ISO 14229-1 conventions; Ford module specifics (security level, erase option
/// record layout, check routine meaning) are NOT documented publicly and are marked [UNVERIFIED]: set them per module profile.
/// </summary>
public sealed class FlashPlan
{
    public required uint TxId { get; init; }
    public required FlashImage Image { get; init; }

    /// <summary>SecurityAccess seed level for programming. [UNVERIFIED for Ford] 0x01 is the ISO convention; some modules use another odd level.</summary>
    public byte SecurityLevel { get; init; } = 0x01;
    /// <summary>addressAndLengthFormatIdentifier for 34 (and the default erase option): high nibble = length bytes, low nibble = address bytes.</summary>
    public byte AddressAndLengthFormat { get; init; } = 0x44;
    /// <summary>Run the erase routine (31 01 <see cref="EraseRoutineId"/>) for each erase region before the download.</summary>
    public bool Erase { get; init; } = true;
    public ushort EraseRoutineId { get; init; } = 0xFF00;
    /// <summary>Option record for the erase routine. Default: ALFID-formatted address + length. [UNVERIFIED for Ford bootloaders]</summary>
    public Func<EraseRegion, byte[]>? EraseOption { get; init; }
    /// <summary>Run 31 01 <see cref="CheckRoutineId"/> (check programming dependencies) after the last segment.</summary>
    public bool CheckDependencies { get; init; } = true;
    public ushort CheckRoutineId { get; init; } = 0xFF01;
    /// <summary>Decides whether the check routine's status record means "valid". Default: any positive reply passes (meaning of the status bytes is module-specific).</summary>
    public Func<byte[], bool>? CheckPassed { get; init; }
    public ResetType Reset { get; init; } = ResetType.Hard;
    /// <summary>Upper bound for one TransferData message (SID + counter + data), below the module's maxNumberOfBlockLength. ISO-TP caps a message at 4095 bytes.</summary>
    public int MaxBlockLength { get; init; } = 4095;

    public FlashRetryPolicy Retry { get; init; } = new();
    /// <summary>Battery voltage window enforced before and during the flash. Defaults from the app's write limits; tune per vehicle on the truck.</summary>
    public double MinVoltage { get; init; } = 12.5;
    public double MaxVoltage { get; init; } = 15.0;

    public byte[] BuildEraseOption(EraseRegion r) => EraseOption?.Invoke(r) ?? FlashMessages.AddressAndLength(AddressAndLengthFormat, r.Address, r.Length);

    /// <summary>Erase regions: the image's list, or one region per segment when the file has none.</summary>
    public IReadOnlyList<EraseRegion> EraseRegions => Image.Erase.Count > 0 ? Image.Erase : Image.Segments.Select(s => new EraseRegion(s.Address, s.Length)).ToList();
}

public sealed class FlashRetryPolicy
{
    /// <summary>Resends of one TransferData block (same counter) before the job is interrupted for link recovery.</summary>
    public int BlockRetries { get; init; } = 4;
    /// <summary>Resends of other steps (session, download request, exit, check) on transient faults.</summary>
    public int StepRetries { get; init; } = 3;
    public TimeSpan Backoff { get; init; } = TimeSpan.FromMilliseconds(250);
    /// <summary>How long to wait after "response pending" on a channel that cannot keep listening, before settling (resend / poll).</summary>
    public TimeSpan PendingSettle { get; init; } = TimeSpan.FromSeconds(2);
    /// <summary>Longest time the erase routine is polled for results before the engine gives up on it.</summary>
    public TimeSpan EraseTimeout { get; init; } = TimeSpan.FromSeconds(90);
    /// <summary>Longest wait for the battery to come back into the window after NRC 0x92/0x93 or a low reading.</summary>
    public TimeSpan VoltageWait { get; init; } = TimeSpan.FromSeconds(60);
    /// <summary>Wait after NRC 0x37 (security delay not expired) before one more seed request. ISO 14229 suggests about 10 s.</summary>
    public TimeSpan SecurityDelay { get; init; } = TimeSpan.FromSeconds(10);
    /// <summary>Settle time after the final ECU reset before the software ID is read back.</summary>
    public TimeSpan PostResetDelay { get; init; } = TimeSpan.FromSeconds(3);
}

/// <summary>Request builders for the download services (ISO 14229-1 §14).</summary>
public static class FlashMessages
{
    /// <summary>Address then length, each with the byte count the ALFID gives (low nibble = address bytes, high nibble = length bytes).</summary>
    public static byte[] AddressAndLength(byte alfid, uint address, uint length)
    {
        int aBytes = alfid & 0x0F, lBytes = alfid >> 4;
        if (aBytes is < 1 or > 4 || lBytes is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(alfid), $"ALFID 0x{alfid:X2}: 1-4 address and length bytes are supported");
        if (aBytes < 4 && address >> (8 * aBytes) != 0) throw new ArgumentOutOfRangeException(nameof(address), $"Address 0x{address:X} does not fit in {aBytes} bytes");
        if (lBytes < 4 && length >> (8 * lBytes) != 0) throw new ArgumentOutOfRangeException(nameof(length), $"Length {length} does not fit in {lBytes} bytes");
        var b = new byte[aBytes + lBytes];
        for (int i = 0; i < aBytes; i++) b[i] = (byte)(address >> (8 * (aBytes - 1 - i)));
        for (int i = 0; i < lBytes; i++) b[aBytes + i] = (byte)(length >> (8 * (lBytes - 1 - i)));
        return b;
    }

    public static byte[] RequestDownload(byte dataFormat, byte alfid, uint address, uint length)
        => new byte[] { 0x34, dataFormat, alfid }.Concat(AddressAndLength(alfid, address, length)).ToArray();

    /// <summary>Parses 74 [LFID] [maxNumberOfBlockLength]: the largest whole TransferData message (SID and counter included).</summary>
    public static int ParseMaxBlockLength(byte[] resp)
    {
        if (resp.Length < 3 || resp[0] != 0x74) throw new InvalidDataException($"Not a RequestDownload reply: {Convert.ToHexString(resp)}");
        int n = resp[1] >> 4;
        if (n is < 1 or > 4 || resp.Length < 2 + n) throw new InvalidDataException($"RequestDownload reply has a bad length format 0x{resp[1]:X2}: {Convert.ToHexString(resp)}");
        long v = 0;
        for (int i = 0; i < n; i++) v = v << 8 | resp[2 + i];
        if (v < 3) throw new InvalidDataException($"Module allows only {v} bytes per TransferData message: {Convert.ToHexString(resp)}");
        return (int)Math.Min(v, int.MaxValue);
    }

    public static byte[] TransferData(byte counter, ReadOnlySpan<byte> data)
    {
        var b = new byte[2 + data.Length];
        b[0] = 0x36; b[1] = counter; data.CopyTo(b.AsSpan(2));
        return b;
    }

    public static byte[] Routine(RoutineControlType type, ushort rid, byte[]? option = null)
    {
        // Built directly (not via UdsRoutines.BuildRequest, which refuses FF00/FF01 outside the flash engine).
        var b = new byte[4 + (option?.Length ?? 0)];
        b[0] = 0x31; b[1] = (byte)type; b[2] = (byte)(rid >> 8); b[3] = (byte)rid;
        option?.CopyTo(b, 4);
        return b;
    }
}

public enum PreflightStatus { Pass, Warn, Fail }
public sealed record PreflightItem(string Name, PreflightStatus Status, string Detail);
public sealed record FlashPreflightReport(IReadOnlyList<PreflightItem> Items)
{
    public bool CanFlash => Items.All(i => i.Status != PreflightStatus.Fail);
    public IEnumerable<PreflightItem> Warnings => Items.Where(i => i.Status == PreflightStatus.Warn);
}

/// <summary>Host-side power facts and sleep blocking. The Windows app implements it; elsewhere it reports "unknown".</summary>
public interface IHostPower
{
    /// <summary>True on AC, false on battery, null when unknown.</summary>
    bool? OnAcPower { get; }
    /// <summary>Keeps the PC awake (no sleep or hibernate) until disposed.</summary>
    IDisposable KeepAwake();
}

public sealed class UnknownHostPower : IHostPower
{
    public static readonly UnknownHostPower Instance = new();
    public bool? OnAcPower => null;
    public IDisposable KeepAwake() => new Noop();
    private sealed class Noop : IDisposable { public void Dispose() { } }
}

/// <summary>Checks run before a flash. Hard failures (voltage, link, image/module mismatch) stop it; soft ones become warnings.</summary>
public static class FlashPreflight
{
    public const int LinkProbes = 8;

    public static async Task<FlashPreflightReport> RunAsync(UdsClient uds, FlashPlan plan, Func<CancellationToken, Task<double?>> readVoltage,
        AdapterProfile? adapter, IHostPower? host = null, Func<UdsClient, CancellationToken, Task<string?>>? readModulePart = null,
        CancellationToken ct = default, bool resume = false)
    {
        host ??= UnknownHostPower.Instance;
        var items = new List<PreflightItem>();

        // Image
        if (plan.Image.EcuTxId is uint fileTx && fileTx != plan.TxId)
            items.Add(new("Image target", PreflightStatus.Fail, $"The file is for module 0x{fileTx:X3}, not the selected 0x{plan.TxId:X3}"));
        else items.Add(new("Image target", PreflightStatus.Pass, $"0x{plan.TxId:X3}, {plan.Image.Segments.Count} segment(s), {plan.Image.TotalBytes:N0} bytes"));
        foreach (var w in plan.Image.Warnings) items.Add(new("Image checksum", PreflightStatus.Warn, w));

        // Voltage, sampled a few times
        var samples = new List<double>();
        for (int i = 0; i < 3; i++)
        {
            if (await readVoltage(ct) is double v) samples.Add(v);
            if (i < 2) await Task.Delay(200, ct);
        }
        if (samples.Count == 0) items.Add(new("Battery", PreflightStatus.Fail, "Could not read battery voltage from the adapter"));
        else
        {
            double lo = samples.Min(), hi = samples.Max();
            items.Add(lo < plan.MinVoltage || hi > plan.MaxVoltage
                ? new("Battery", PreflightStatus.Fail, $"{lo:0.0}-{hi:0.0} V is outside {plan.MinVoltage:0.0}-{plan.MaxVoltage:0.0} V. Connect a battery charger/maintainer.")
                : new("Battery", PreflightStatus.Pass, $"{lo:0.0}-{hi:0.0} V"));
        }

        // Link health: harmless TesterPresent round trips to the target
        int answered = 0; var times = new List<double>();
        for (int i = 0; i < LinkProbes; i++)
        {
            var sw = Stopwatch.StartNew();
            try { await uds.TesterPresentAsync(ct); answered++; times.Add(sw.Elapsed.TotalMilliseconds); }
            catch (UdsNegativeResponseException) { answered++; times.Add(sw.Elapsed.TotalMilliseconds); } // an NRC still proves the link
            catch (Exception ex) when (ex is TimeoutException or IOException or IsoTp.DiagLinkException) { /* counted as a lost probe */ }
        }
        int lost = LinkProbes - answered;
        string stats = times.Count > 0 ? $"{answered}/{LinkProbes} answered, avg {times.Average():0} ms, max {times.Max():0} ms" : "no answers";
        if (resume && lost >= 2)
        {
            // Recovery: a module stuck in its bootloader may ignore TesterPresent in its default session but still answer a
            // programming-session request. Any answer (positive or negative) proves it is alive and reachable.
            bool alive;
            try { await uds.RequestAsync(new byte[] { 0x10, 0x02 }, ct); alive = true; }
            catch (UdsNegativeResponseException) { alive = true; }
            catch (Exception ex) when (ex is TimeoutException or IOException or IsoTp.DiagLinkException or InvalidDataException) { alive = false; }
            items.Add(alive
                ? new("Link", PreflightStatus.Warn, stats + ". The module ignores TesterPresent but answers a programming-session request (bootloader): recovery can continue.")
                : new("Link", PreflightStatus.Fail, stats + ", and no answer to a programming-session request. Turn the ignition OFF for 10 s and ON, check the battery charger, " +
                    "use the wired OBDLink EX, wait 10 s (security delay), then run the checks again. Don't loop: if it stays silent, stop and keep the journal and backup."));
        }
        else items.Add(lost switch
        {
            0 => new("Link", PreflightStatus.Pass, stats),
            1 => new("Link", PreflightStatus.Warn, stats + ". One probe was lost; keep the PC close and still."),
            _ => new("Link", PreflightStatus.Fail, stats + ". The link to the module is not reliable enough to flash."),
        });

        // Module identity vs file
        if (readModulePart is not null && plan.Image.SoftwarePartNumber is string want)
        {
            string? have = null;
            try { have = await readModulePart(uds, ct); } catch (Exception ex) when (ex is not OperationCanceledException) { have = null; }
            items.Add(have is null
                ? new("Module part number", PreflightStatus.Warn, $"Could not read the module's part number to compare with the file ({want})")
                : new("Module part number", PreflightStatus.Pass, $"module reports {have}; file is {want}"));
        }

        // Adapter and host
        if (adapter?.Kind == AdapterKind.ObdLinkMxPlus)
            items.Add(new("Adapter", PreflightStatus.Warn, "OBDLink MX+ is a wireless link. A dropout mid-flash can leave the module unbootable until recovery. Wired OBDLink EX recommended."));
        else if (adapter is not null) items.Add(new("Adapter", PreflightStatus.Pass, adapter.DisplayName));
        items.Add(host.OnAcPower switch
        {
            true => new("PC power", PreflightStatus.Pass, "on AC power"),
            false => new("PC power", PreflightStatus.Warn, "the PC is on battery. Plug it in before flashing."),
            null => new("PC power", PreflightStatus.Warn, "could not tell whether the PC is on AC power. Plug it in."),
        });
        return new FlashPreflightReport(items);
    }
}

/// <summary>The acknowledgement screens shown before every flash. MX+ adds a wireless-link line.</summary>
public static class FlashWarnings
{
    public static IReadOnlyList<ConfirmationStep> For(FlashPlan plan, AdapterProfile? adapter)
    {
        var lines = new List<string>
        {
            "Battery charger/maintainer connected and on.",
            "Ignition ON, engine OFF. Don't open doors, touch switches or unplug anything until it finishes.",
            "PC on AC power. Sleep is blocked while flashing.",
            $"Target module 0x{plan.TxId:X3}; file {plan.Image.Name}" + (plan.Image.SoftwarePartNumber is string p ? $" ({p})" : "") + ".",
            "An interrupted flash can leave the module inoperable until it is recovered. A backup is saved first.",
        };
        if (adapter?.Kind == AdapterKind.ObdLinkMxPlus)
            lines.Add("Wireless link (OBDLink MX+): keep the PC close to the truck. A wired OBDLink EX is recommended for flashing.");
        return new[]
        {
            new ConfirmationStep("Flash: conditions", string.Join("\n", lines.Select(l => "• " + l))),
            new ConfirmationStep("Flash: final", $"Flash module 0x{plan.TxId:X3} now? Type FLASH to confirm.", "FLASH"),
        };
    }
}
