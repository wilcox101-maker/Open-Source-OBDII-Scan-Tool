using ObdLink.Core.Dashboard;

namespace ObdLink.Core.Transport;

/// <summary>
/// SYNTHETIC, deterministic 120 s drive cycle for the simulator: idle, launch through 6 gears, cruise, braking, stop.
/// Values are made up (not from a real vehicle). Time comes from the caller so tests are reproducible.
/// </summary>
public static class DriveCycle
{
    public const double Period = 120;
    public sealed record State(double Rpm, double SpeedKmh, int Gear, double Throttle, double Load, double CoolantC, double IatC, double MafGs,
        double Fuel, double Volts, double TftC, double OilC, double Timing, double Stft, double Ltft, double Baro, double Ambient);
    private static readonly GearEstimator G = new();

    /// <summary>Speed profile (km/h) at cycle time t.</summary>
    public static double Speed(double t)
    {
        t %= Period;
        if (t < 10) return 0;                               // idle
        if (t < 40) return 105 * Smooth((t - 10) / 30);     // accelerate
        if (t < 80) return 105 + 6 * Math.Sin((t - 40) / 6); // cruise
        if (t < 105) return 105 * (1 - Smooth((t - 80) / 25)); // brake
        return 0;
    }
    private static double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }

    public static State At(double seconds)
    {
        var t = seconds % Period; var v = Speed(t); var accel = (Speed(t + 0.5) - Speed(t)) * 2;
        int gear = v < 1 ? 0 : v < 18 ? 1 : v < 32 ? 2 : v < 50 ? 3 : v < 70 ? 4 : v < 90 ? 5 : 6;
        var rpm = gear == 0 ? 680 + 15 * Math.Sin(seconds * 3) : Math.Max(750, G.RpmFor(gear, v)) + (accel > 0 ? 250 : 0);
        var throttle = gear == 0 ? 0 : Math.Clamp(14 + accel * 9 + v / 10, 0, 85);
        var warm = Math.Min(1, seconds / 600);
        return new State(rpm, v, gear, throttle, Math.Clamp(18 + throttle * 0.8, 0, 100), 60 + 30 * warm, 28 + 5 * Math.Sin(seconds / 40),
            rpm * (0.4 + throttle / 100) * 0.0045 * 4, Math.Max(5, 68 - seconds / 120 % 60), rpm > 500 ? 14.2 + 0.1 * Math.Sin(seconds) : 12.5,
            55 + 35 * warm, 70 + 30 * warm, 10 + throttle / 6, 2 * Math.Sin(seconds * 1.7), 3.1, 99, 21);
    }

    public static byte B(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
    /// <summary>J1979 data bytes for a Mode 01 PID (inverse of the decode formulas), or null if the sim does not support it.</summary>
    public static byte[]? Encode(byte pid, State s)
    {
        ushort u(double x) => (ushort)Math.Clamp(Math.Round(x), 0, 65535);
        byte[] w(ushort x) => new[] { (byte)(x >> 8), (byte)x };
        return pid switch
        {
            0x04 => new[] { B(s.Load * 255 / 100) },
            0x05 => new[] { B(s.CoolantC + 40) },
            0x06 or 0x08 => new[] { B(s.Stft * 128 / 100 + 128) },
            0x07 or 0x09 => new[] { B(s.Ltft * 128 / 100 + 128) },
            0x0B => new[] { B(30 + s.Throttle) },
            0x0C => w(u(s.Rpm * 4)),
            0x0D => new[] { B(s.SpeedKmh) },
            0x0E => new[] { B((s.Timing + 64) * 2) },
            0x0F => new[] { B(s.IatC + 40) },
            0x10 => w(u(s.MafGs * 100)),
            0x11 => new[] { B(s.Throttle * 255 / 100) },
            0x2F => new[] { B(s.Fuel * 255 / 100) },
            0x33 => new[] { B(s.Baro) },
            0x42 => w(u(s.Volts * 1000)),
            0x46 => new[] { B(s.Ambient + 40) },
            0x5C => new[] { B(s.OilC + 40) },
            _ => null,
        };
    }
    public static readonly byte[] SupportedPids = { 0x01, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x1C, 0x1F, 0x20, 0x21, 0x2F, 0x33, 0x40, 0x42, 0x46, 0x5C };
    /// <summary>4-byte supported bitmap for base 00/20/40.</summary>
    public static byte[] Bitmap(byte basePid)
    {
        uint m = 0; foreach (var p in SupportedPids) if (p > basePid && p <= basePid + 0x20) m |= 1u << (31 - (p - basePid - 1));
        return new[] { (byte)(m >> 24), (byte)(m >> 16), (byte)(m >> 8), (byte)m };
    }
}
