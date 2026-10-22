using System.Globalization;
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
    public StnProtocol? CurrentProtocol { get; private set; }
    private string? _header;

    public AdapterSession(ElmLink link, ILogger? log = null) { _link = link; _log = (log ?? Log.Logger).ForContext<AdapterSession>(); }
    public ElmLink Link => _link;

    /// <summary>ATZ, ATE0, ATL0, ATS0, ATH1, then identification (ATI, STI, STDI) and ATRV.</summary>
    public async Task<AdapterInfo> InitializeAsync(CancellationToken ct = default)
    {
        var z = await _link.SendAsync("ATZ", TimeSpan.FromSeconds(5), ct);
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
        await _link.SendAsync("STPC", ct: ct); // close current; may error if none open - ignored
        await Expect($"STP {(int)p}", ct);
        await Expect("STPO", ct);
        CurrentProtocol = p;
        _header = null;
    }

    /// <summary>Plain ELM327 protocol selection (ATSP n) for non-STN fallback.</summary>
    public Task SelectElmProtocolAsync(char n, CancellationToken ct = default) => Expect($"ATSP{n}", ct);

    /// <summary>Sets transmit header (ATSH) and CAN receive filter (ATCRA) for physical addressing.</summary>
    public async Task SetCanTargetAsync(uint txId, uint? rxId, CancellationToken ct = default)
    {
        bool ext = txId > 0x7FF;
        string h = ext ? txId.ToString("X8") : txId.ToString("X3");
        if (_header == h) return;
        await Expect($"ATSH{h}", ct);
        if (rxId is uint rx) await Expect(ext ? $"ATCRA{rx:X8}" : $"ATCRA{rx:X3}", ct);
        else await Expect("ATCRA", ct); // reset filter
        _header = h;
    }
}
