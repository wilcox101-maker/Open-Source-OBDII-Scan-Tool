using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObdLink.Core.Obd;

/// <summary>One Ford Mode 22 PID from the editable JSON table. UNVERIFIED unless <see cref="Verified"/> is set (TPMS 2813-2816 are verified; OSS 1E13 is not).</summary>
public sealed class EnhancedPid
{
    public string Key { get; set; } = ""; public string Name { get; set; } = "";
    public string Header { get; set; } = "7E0"; public string Did { get; set; } = "";
    public int Bytes { get; set; } = 1; public bool Signed { get; set; }
    public double Scale { get; set; } = 1; public double Offset { get; set; }
    public string Unit { get; set; } = ""; public string? Quantity { get; set; }
    public double Min { get; set; } public double Max { get; set; } = 100;
    public double? WarnHigh { get; set; } public double? WarnLow { get; set; }
    public Dictionary<string, string>? Map { get; set; }
    public bool Enabled { get; set; } = true; public bool Verified { get; set; }
    public string? Source { get; set; }
    /// <summary>v0.3.4: other modules to try when <see cref="Header"/> gives no positive answer (e.g. OSS on PCM 7E0, else TCM 7E1).</summary>
    public List<string>? AltHeaders { get; set; }
    [JsonIgnore] public IEnumerable<uint> Candidates => new[] { Header }.Concat(AltHeaders ?? new()).Select(h => Convert.ToUInt32(h, 16)).Distinct();

    [JsonIgnore] public uint TxId => Convert.ToUInt32(Header, 16);
    [JsonIgnore] public ushort DidValue => Convert.ToUInt16(Did, 16);
    [JsonIgnore] public Quantity QuantityValue => Enum.TryParse<Quantity>(Quantity, true, out var q) ? q : Obd.Quantity.None;
    public byte[] Request => new byte[] { 0x22, (byte)(DidValue >> 8), (byte)DidValue };

    /// <summary>Decodes a positive response payload "62 DID DID data..." (or just the data bytes).</summary>
    public double Decode(byte[] payload)
    {
        var d = payload.Length >= 3 && payload[0] == 0x62 ? payload[3..] : payload;
        if (d.Length < Bytes) throw new ArgumentException($"DID {Did} needs {Bytes} bytes, got {d.Length}");
        long raw = 0; for (int i = 0; i < Bytes; i++) raw = raw << 8 | d[i];
        if (Signed && (raw & (1L << (Bytes * 8 - 1))) != 0) raw -= 1L << (Bytes * 8);
        return raw * Scale + Offset;
    }
    public string Label(double v) => Map != null && Map.TryGetValue(((long)Math.Round(v)).ToString(), out var s) ? s : v.ToString("0.#");
}

public sealed class EnhancedPidTable
{
    public List<EnhancedPid> Pids { get; set; } = new();
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    public const string FileName = "ford-enhanced-pids.json";

    public static string DefaultJson()
    {
        var asm = typeof(EnhancedPidTable).Assembly;
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith(FileName));
        using var s = asm.GetManifestResourceStream(name)!; return new StreamReader(s).ReadToEnd();
    }
    public static EnhancedPidTable Parse(string json) => JsonSerializer.Deserialize<EnhancedPidTable>(json, Json) ?? new();

    /// <summary>Loads <dir>\ford-enhanced-pids.json, writing the built-in (UNVERIFIED) default there first if missing. A broken file falls back to the default.</summary>
    public static (EnhancedPidTable Table, string Path, string? Error) LoadOrCreate(string dir)
    {
        var path = System.IO.Path.Combine(dir, FileName);
        try
        {
            Directory.CreateDirectory(dir);
            if (!File.Exists(path)) File.WriteAllText(path, DefaultJson());
            return (WithBuiltIns(Parse(File.ReadAllText(path))), path, null);
        }
        catch (Exception ex) { return (Parse(DefaultJson()), path, ex.Message); }
    }

    /// <summary>v0.3.4: a Data\pids file written by an older version keeps the user's edits but gains new built-in entries (e.g. "oss") in memory.</summary>
    /// <summary>Only keys added after v0.2.0 are merged, so entries a user deliberately deleted stay deleted.</summary>
    public static readonly IReadOnlySet<string> AddedLater = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "oss" };
    /// <summary>Stock DIDs Dale verified. A user file that still has that DID is treated as verified. A changed DID is left alone. OSS 1E13 is not here.</summary>
    public static readonly IReadOnlyDictionary<string, string> VerifiedStockDids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["tpms_lf"] = "2813", ["tpms_rf"] = "2814", ["tpms_rr"] = "2815", ["tpms_lr"] = "2816",
    };
    public static EnhancedPidTable WithBuiltIns(EnhancedPidTable user)
    {
        foreach (var d in Parse(DefaultJson()).Pids.Where(d => AddedLater.Contains(d.Key)))
            if (user.Pids.All(p => !string.Equals(p.Key, d.Key, StringComparison.OrdinalIgnoreCase))) user.Pids.Add(d);
        foreach (var p in user.Pids)
            if (VerifiedStockDids.TryGetValue(p.Key, out var did) && string.Equals(p.Did, did, StringComparison.OrdinalIgnoreCase))
                p.Verified = true;
        return user;
    }
}
