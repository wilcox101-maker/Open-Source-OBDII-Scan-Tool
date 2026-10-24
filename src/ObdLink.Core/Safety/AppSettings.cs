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

    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static AppSettings Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new() : new(); }
        catch { return new(); } // corrupt file: fall back to safe defaults (read-only)
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
