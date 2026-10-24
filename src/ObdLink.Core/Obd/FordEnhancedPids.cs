using System.Text.Json;
using System.Text.Json.Serialization;

namespace ObdLink.Core.Obd;

/// <summary>One Ford Mode 22 PID from the editable JSON table (all UNVERIFIED by default).</summary>
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
            return (Parse(File.ReadAllText(path)), path, null);
        }
        catch (Exception ex) { return (Parse(DefaultJson()), path, ex.Message); }
    }
}
