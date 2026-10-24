namespace ObdLink.Core.Dashboard;

/// <summary>
/// Fallback gear estimate from the RPM/speed ratio when no Ford gear DID answers.
/// Default ratios: 6R80 (4.17, 2.34, 1.52, 1.14, 0.87, 0.69), 3.55 axle, 275/65R18 tire (~2.62 m circumference). Approximate (torque converter slip).
/// </summary>
public sealed class GearEstimator
{
    public double[] Ratios { get; init; } = { 4.17, 2.34, 1.52, 1.14, 0.87, 0.69 };
    public double Axle { get; init; } = 3.55;
    public double TireCircumferenceM { get; init; } = 2.62;
    public double Tolerance { get; init; } = 0.15;

    /// <summary>Returns 1..n, or null when stopped, coasting or out of tolerance.</summary>
    public int? Estimate(double rpm, double speedKmh)
    {
        if (speedKmh < 5 || rpm < 400) return null;
        var wheelRps = speedKmh / 3.6 / TireCircumferenceM;
        var overall = rpm / 60.0 / wheelRps;
        var gearRatio = overall / Axle;
        int best = -1; double bestErr = double.MaxValue;
        for (int i = 0; i < Ratios.Length; i++) { var e = Math.Abs(gearRatio - Ratios[i]) / Ratios[i]; if (e < bestErr) { bestErr = e; best = i; } }
        return bestErr <= Tolerance ? best + 1 : null;
    }
    /// <summary>Engine RPM for a given gear and speed (used by the simulator).</summary>
    public double RpmFor(int gear, double speedKmh) => speedKmh / 3.6 / TireCircumferenceM * Ratios[gear - 1] * Axle * 60;
}
