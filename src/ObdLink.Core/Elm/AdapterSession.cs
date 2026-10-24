using System.Globalization;
using ObdLink.Core.Adapters;
using Serilog;

namespace ObdLink.Core.Elm;

public sealed record AdapterInfo(string ElmId, string? StnId, string? DeviceId, double? BatteryVolts);

/// <summary>High-level adapter control: init sequence, protocol select, headers, voltage.</summary>
public sealed class AdapterSession
{
    private readonly ElmLink _link;
    private readonly ILogger _log;
    public AdapterInfo? Info { get; private set; }
    public bool IsStn => Info?.StnId is not null;
    /// <summary>Profile detected from the ATI/STI/STDI replies (set by InitializeAsync).</summary>
    public AdapterProfile Profile { get; private set; }
    public StnProtocol? CurrentProtocol { get; private set; }
    private string? _header;

    public AdapterSession(ElmLink link, ILogger? log = null, AdapterProfile? initial = null) { _link = link; Profile = initial ?? AdapterProfiles.OtherStn; _link.DefaultTimeout = Profile.CommandTimeout; _log = (log ?? Log.Logger).ForContext<AdapterSession>(); }
    public ElmLink Link => _link;

    /// <summary>ATZ, ATE0, ATL0, ATS0, ATH1, then identification (ATI, STI, STDI) and ATRV.</summary>
    public async Task<AdapterInfo> InitializeAsync(CancellationToken ct = default)
    {
        var z = await _link.SendAsync("ATZ", Profile.ResetTimeout, ct);
        string elmId = z.Lines.LastOrDefault() ?? "";
        foreach (var c in new[] { "ATE0", "ATL0", "ATS0", "ATH1" })
            await Expect(c, ct);
        if (string.IsNullOrEmpty(elmId)) elmId = (await _link.SendAsync("ATI", ct: ct)).Lines.FirstOrDefault() ?? "";
        var sti = await _link.SendAsync("STI", ct: ct);              // STN firmware id; '?' on genuine ELM/clones
        string? stnId = sti.Ok ? sti.Lines.FirstOrDefault() : null;
        string? devId = null;
        if (stnId is not null)
        {
            var stdi = await _link.SendAsync("STDI", ct: ct);         // device hardware id, e.g. "OBDLink MX+ r..."
            devId = stdi.Ok ? stdi.Lines.FirstOrDefault() : null;
        }
        var v = await ReadBatteryVoltageAsync(ct);
        Info = new AdapterInfo(elmId, stnId, devId, v);
        Profile = AdapterProfiles.Detect(Info);
        _link.DefaultTimeout = Profile.CommandTimeout;
        _log.Information("Profile {P}: {Caps}", Profile.DisplayName, Profile.CapabilityText);
        _log.Information("Adapter: {Elm} / {Stn} / {Dev}, {V} V", elmId, stnId, devId, v);
        return Info;
    }

    private async Task Expect(string cmd, CancellationToken ct)
    {
        var r = await _link.SendAsync(cmd, ct: ct);
        if (!r.Ok || !r.Lines.Any(l => l.Equals("OK", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Adapter rejected {cmd}: {string.Join("|", r.Lines)}");
    }

    /// <summary>ATRV: returns e.g. "12.6V".</summary>
    public async Task<double?> ReadBatteryVoltageAsync(CancellationToken ct = default)
    {
        var r = await _link.SendAsync("ATRV", ct: ct);
        return r.Ok ? ParseVoltage(r.Lines.FirstOrDefault()) : null;
    }

    public static double? ParseVoltage(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim().TrimEnd('V', 'v');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>Selects an STN preset (STP) and opens it (STPO). Requires STN firmware.</summary>
    public async Task SelectProtocolAsync(StnProtocol p, CancellationToken ct = default)
    {
        if (!IsStn) throw new NotSupportedException("STP requires an STN-based adapter (e.g. OBDLink MX+)");
        bool ms = (int)p is >= 51 and <= 54, sw = (int)p is >= 61 and <= 64;
        if (ms && !Profile.Has(AdapterCapabilities.MsCan)) throw new NotSupportedException($"{Profile.DisplayName} does not support MS-CAN");
        if (sw && !Profile.Has(AdapterCapabilities.SwCan)) throw new NotSupportedException($"{Profile.DisplayName} does not support SW-CAN");
        await _link.SendAsync("STPC", ct: ct); // close current; may error if none open - ignored
        await Expect($"STP {(int)p}", ct);
        await Expect("STPO", ct);
        await ApplyDiagTimingAsync(ct);
        CurrentProtocol = p;
        _header = null;
    }

    /// <summary>
    /// v0.3.2: response wait ATST 96 (~600 ms, ELM default ~200 ms) for slow modules (IPC, APIM); adaptive timing stays at the default ATAT1.
    /// Optional commands: failures are logged by the link but ignored.
    /// </summary>
    public async Task ApplyDiagTimingAsync(CancellationToken ct = default) { await Try("ATAT1", ct); await Try("ATST96", ct); }
    private async Task Try(string cmd, CancellationToken ct) { try { await _link.SendAsync(cmd, ct: ct); } catch (OperationCanceledException) { throw; } catch (Exception ex) { _log.Debug("{Cmd} ignored: {Err}", cmd, ex.Message); } }

    /// <summary>
    /// v0.3.2: selects a Ford bus. HS-CAN = STP 33 (ATSP6 on plain ELM); MS-CAN = STP 53 (125k, pins 3/11);
    /// HS-CAN2 = STP 53 with STPBR 500000 (500k on pins 3/11). MS pins need an STN adapter with MS-CAN (EX / MX+).
    /// </summary>
    public async Task SelectBusAsync(Bus bus, CancellationToken ct = default)
    {
        if (bus == Bus.HsCan) { if (IsStn) await SelectProtocolAsync(StnProtocol.HsCan11_500, ct); else await SelectElmProtocolAsync('6', ct); return; }
        if (bus is not (Bus.MsCan or Bus.HsCan2)) throw new NotSupportedException($"Bus {bus} is not implemented");
        if (!IsStn || !Profile.Has(AdapterCapabilities.MsCan)) throw new NotSupportedException($"{Profile.DisplayName} cannot use the MS-CAN pins (3/11)");
        if (bus == Bus.MsCan) { await SelectProtocolAsync(StnProtocol.MsCan11_125, ct); return; }
        await _link.SendAsync("STPC", ct: ct);
        await Expect("STP 53", ct);
        await Expect("STPBR 500000", ct);
        await Expect("STPO", ct);
        await ApplyDiagTimingAsync(ct);
        CurrentProtocol = StnProtocol.MsCan11_125; _header = null;
    }

    /// <summary>Plain ELM327 protocol selection (ATSP n) for non-STN fallback.</summary>
    public async Task SelectElmProtocolAsync(char n, CancellationToken ct = default) { await Expect($"ATSP{n}", ct); await ApplyDiagTimingAsync(ct); _header = null; }

    /// <summary>Sets transmit header (ATSH) and CAN receive filter (ATCRA) for physical addressing.</summary>
    public async Task SetCanTargetAsync(uint txId, uint? rxId, CancellationToken ct = default)
    {
        bool ext = txId > 0x7FF;
        string h = ext ? txId.ToString("X8") : txId.ToString("X3");
        if (_header == h) return;
        await Expect($"ATSH{h}", ct);
        if (rxId is uint rx) await Expect(ext ? $"ATCRA{rx:X8}" : $"ATCRA{rx:X3}", ct);
        else await Expect("ATCRA", ct); // reset filter
        if (rxId is not null && !ext)
        {   // v0.3.2: explicit ISO-TP flow control to the target (ID = our header, CTS / BS 0 / STmin 0), so multi-frame replies
            // such as 19 02 with several DTCs are not cut off. Optional: a plain ELM clone without ATFC* just answers '?'.
            await Try($"ATFCSH{h}", ct); await Try("ATFCSD300000", ct); await Try("ATFCSM1", ct);
        }
        _header = h;
    }
}
