namespace ObdLink.Core.Obd;

public enum GaugeKind { Circular, Bar, Tile }
public enum Quantity { None, Speed, Temperature, Pressure, Flow, Voltage, Percent, Rpm, Angle }

/// <summary>One SAE J1979 Mode 01 PID: byte count, formula and display range (in metric/base units).</summary>
public sealed record PidDef(byte Pid, string Key, string Name, string Unit, Quantity Quantity, int Bytes, double Min, double Max, Func<byte[], double> Formula,
    GaugeKind Kind = GaugeKind.Bar, double? WarnHigh = null, double? WarnLow = null, int Priority = 1)
{
    public double Decode(byte[] data)
    {
        if (data.Length < Bytes) throw new ArgumentException($"PID {Pid:X2} needs {Bytes} data bytes, got {data.Length}");
        return Formula(data);
    }
}

/// <summary>
/// SAE J1979 / ISO 15031-5 Mode 01 formulas (A = first data byte after "41 pid"). Priority: 0 = fast (RPM/speed), 1 = normal, 2 = slow (temps, baro).
/// </summary>
public static class J1979
{
    private static double U16(byte[] d) => d[0] * 256 + d[1];
    public static readonly IReadOnlyList<PidDef> All = new PidDef[]
    {
        new(0x04, "load", "Engine load", "%", Quantity.Percent, 1, 0, 100, d => d[0] * 100.0 / 255, WarnHigh: 95),
        new(0x05, "coolant", "Coolant temp", "°C", Quantity.Temperature, 1, -40, 130, d => d[0] - 40.0, WarnHigh: 110, Priority: 2),
        new(0x06, "stft1", "Short fuel trim B1", "%", Quantity.Percent, 1, -25, 25, d => (d[0] - 128) * 100.0 / 128, WarnHigh: 20, WarnLow: -20),
        new(0x07, "ltft1", "Long fuel trim B1", "%", Quantity.Percent, 1, -25, 25, d => (d[0] - 128) * 100.0 / 128, WarnHigh: 20, WarnLow: -20, Priority: 2),
        new(0x08, "stft2", "Short fuel trim B2", "%", Quantity.Percent, 1, -25, 25, d => (d[0] - 128) * 100.0 / 128, WarnHigh: 20, WarnLow: -20),
        new(0x09, "ltft2", "Long fuel trim B2", "%", Quantity.Percent, 1, -25, 25, d => (d[0] - 128) * 100.0 / 128, WarnHigh: 20, WarnLow: -20, Priority: 2),
        new(0x0B, "map", "Intake manifold pressure", "kPa", Quantity.Pressure, 1, 0, 255, d => d[0]),
        new(0x0C, "rpm", "Engine RPM", "rpm", Quantity.Rpm, 2, 0, 7000, d => U16(d) / 4.0, GaugeKind.Circular, WarnHigh: 6000, Priority: 0),
        new(0x0D, "speed", "Vehicle speed", "km/h", Quantity.Speed, 1, 0, 200, d => d[0], GaugeKind.Circular, Priority: 0),
        new(0x0E, "timing", "Timing advance", "°", Quantity.Angle, 1, -64, 63.5, d => d[0] / 2.0 - 64),
        new(0x0F, "iat", "Intake air temp", "°C", Quantity.Temperature, 1, -40, 100, d => d[0] - 40.0, WarnHigh: 70, Priority: 2),
        new(0x10, "maf", "MAF air flow", "g/s", Quantity.Flow, 2, 0, 400, d => U16(d) / 100.0),
        new(0x11, "throttle", "Throttle position", "%", Quantity.Percent, 1, 0, 100, d => d[0] * 100.0 / 255, Priority: 0),
        new(0x2F, "fuel", "Fuel level", "%", Quantity.Percent, 1, 0, 100, d => d[0] * 100.0 / 255, WarnLow: 12, Priority: 2),
        new(0x33, "baro", "Barometric pressure", "kPa", Quantity.Pressure, 1, 60, 110, d => d[0], Priority: 2),
        new(0x42, "voltage", "Control module voltage", "V", Quantity.Voltage, 2, 8, 16, d => U16(d) / 1000.0, WarnLow: 11.8, WarnHigh: 15.2, Priority: 2),
        new(0x46, "ambient", "Ambient air temp", "°C", Quantity.Temperature, 1, -40, 60, d => d[0] - 40.0, Priority: 2),
        new(0x5C, "oiltemp", "Engine oil temp", "°C", Quantity.Temperature, 1, -40, 160, d => d[0] - 40.0, WarnHigh: 130, Priority: 2),
    };
    public static PidDef? Get(byte pid) => All.FirstOrDefault(p => p.Pid == pid);
    public static PidDef? Get(string key) => All.FirstOrDefault(p => p.Key == key);

    /// <summary>PID 01 (monitor status): A bit 7 = MIL on, A bits 0-6 = number of emission DTCs.</summary>
    public static (bool Mil, int DtcCount) DecodeMonitorStatus(byte[] d) => ((d[0] & 0x80) != 0, d[0] & 0x7F);

    /// <summary>Supported-PID bitmap from PIDs 00/20/40/...: bit 31 of the 4 bytes = base+1, bit 0 = base+0x20.</summary>
    public static IEnumerable<byte> ParseSupported(byte basePid, byte[] d)
    {
        uint mask = (uint)(d[0] << 24 | d[1] << 16 | d[2] << 8 | d[3]);
        for (int i = 0; i < 32; i++) if ((mask & (1u << (31 - i))) != 0) yield return (byte)(basePid + i + 1);
    }
}
