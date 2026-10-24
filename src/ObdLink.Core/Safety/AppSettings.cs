using System.Text.Json;

namespace ObdLink.Core.Safety;

/// <summary>
/// Persisted user settings. <see cref="ReadOnlyTestMode"/> defaults to TRUE. While it's on, every write, clear, reset and As-Built restore is refused before anything is sent to a module, and the raw console blocks state-changing services whatever its "Allow writes" box says.
/// </summary>
public sealed class AppSettings
{
    public bool ReadOnlyTestMode { get; set; } = true;
    /// <summary>"Light", "Dark" or "System" (follow Windows app mode).</summary>
    public string Theme { get; set; } = "System";
    public DiscoveryOptions Discovery { get; set; } = new();
    /// <summary>Opt-in: Identify vehicle may send the VIN to NHTSA vPIC (vpic.nhtsa.dot.gov) for an online decode. Default OFF.</summary>
    public bool OnlineVinDecode { get; set; }
    /// <summary>Header logo: "Auto" (variant 1 on light, 2 on dark), or "1", "2", "3".</summary>
    public string HeaderIcon { get; set; } = "Auto";
    /// <summary>Opt-in: Help > Check for updates (and at startup) asks the public GitHub releases API for a newer build. Default OFF.</summary>
    public bool UpdateCheck { get; set; }
    /// <summary>Default for "Redact VINs" in diagnostics captures and exports. Default ON.</summary>
    public bool RedactVinsDefault { get; set; } = true;
    public ObdLink.Core.Obd.UnitPrefs Units { get; set; } = new();
    public AdapterDefaults Adapter { get; set; } = new();
    public DashboardOptions Dashboard { get; set; } = new();
    /// <summary>Minimum level shown in the log panels: Debug, Info, Warn, Error.</summary>
    public string LogLevel { get; set; } = "Info";
    /// <summary>Delete session logs / dashboard CSVs older than this many days at startup (0 = keep forever).</summary>
    public int LogRetentionDays { get; set; } = 30;
    /// <summary>Offer the Demo (simulated) connection in the port list.</summary>
    public bool SimulatorEnabled { get; set; } = true;

    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static AppSettings Load(string path)
    {
        try { return (File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new() : new()).Normalize(); }
        catch { return new(); } // corrupt file: fall back to safe defaults (read-only)
    }
    /// <summary>Repairs nulls / out-of-range values from older or hand-edited files.</summary>
    public AppSettings Normalize()
    {
        Discovery ??= new(); Units ??= new(); Adapter ??= new(); Dashboard ??= new();
        Dashboard.Gauges ??= DashboardOptions.DefaultGauges.ToList();
        Dashboard.RefreshMs = Math.Clamp(Dashboard.RefreshMs, 100, 5000);
        Adapter.TimeoutMs = Math.Clamp(Adapter.TimeoutMs, 50, 10000);
        LogRetentionDays = Math.Clamp(LogRetentionDays, 0, 3650);
        if (Units.Speed is not ("mph" or "km/h")) Units.Speed = "mph";
        if (Units.Temperature is not ("°F" or "°C")) Units.Temperature = "°F";
        if (Units.Pressure is not ("psi" or "kPa")) Units.Pressure = "psi";
        return this;
    }
    public void Save(string path) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(this, Json)); }
}

public sealed class ReadOnlyModeException(string what) : SafetyException($"Read-only test mode is ON: '{what}' is disabled. Turn it off in Settings to allow writes.");

/// <summary>Single switch checked by every state-changing path.</summary>
public sealed class WriteGate
{
    private readonly Func<bool> _readOnly;
    public WriteGate(Func<bool> readOnly) => _readOnly = readOnly;
    public static readonly WriteGate AlwaysAllow = new(() => false);
    public bool ReadOnly => _readOnly();
    public void Require(string what) { if (_readOnly()) throw new ReadOnlyModeException(what); }
}

/// <summary>Configurable CAN ID discovery sweep (all probes are read-only diagnostic requests).</summary>
public sealed class DiscoveryOptions
{
    public uint StartId { get; set; } = 0x700;
    public uint EndId { get; set; } = 0x7EF;
    /// <summary>Response ID = request + offset. +8 is the ISO 15765-4 convention for 7E0-7E7. Other ranges may differ [UNVERIFIED].</summary>
    public uint RxOffset { get; set; } = 8;
    public bool TesterPresent { get; set; } = true;
    public bool ReadVinF190 { get; set; } = true;
    /// <summary>KWP2000 ReadEcuIdentification (0x1A) with this option byte. 0x87 is a commonly cited identifier [UNVERIFIED].</summary>
    public bool Kwp1A { get; set; } = true;
    public byte Kwp1AOption { get; set; } = 0x87;
    public int TimeoutMs { get; set; } = 150;
}

public sealed class AdapterDefaults
{
    /// <summary>Port to preselect ("" = first found, "Demo" = simulator).</summary>
    public string Port { get; set; } = "";
    /// <summary>EX (USB/UART) host baud; 115200 is the STN default. Ignored over Bluetooth (MX+).</summary>
    public int Baud { get; set; } = 115200;
    public int TimeoutMs { get; set; } = 1000;
    /// <summary>"Auto", "EX" or "MX+" (only changes hints/defaults; the adapter is still identified via STI/STDI).</summary>
    public string Kind { get; set; } = "Auto";
}

public sealed class DashboardOptions
{
    public static readonly string[] DefaultGauges = { "rpm", "speed", "fuel", "coolant", "iat", "load", "throttle", "maf", "voltage", "stft1", "ltft1", "timing", "baro", "oiltemp", "ambient" };
    /// <summary>Poll period for the fastest (priority 0) gauges, ms. Slower gauges run at 2x / 8x this.</summary>
    public int RefreshMs { get; set; } = 250;
    public List<string> Gauges { get; set; } = DefaultGauges.ToList();
    public bool FordEnhancedPids { get; set; } = true;
    /// <summary>Gear estimate from RPM/speed when the Ford gear DID does not answer.</summary>
    public bool EstimateGear { get; set; } = true;
    public double AxleRatio { get; set; } = 3.55;
    public double TireCircumferenceM { get; set; } = 2.62;
    public double LowVoltage { get; set; } = 11.8;
    public double OverheatC { get; set; } = 110;
}
