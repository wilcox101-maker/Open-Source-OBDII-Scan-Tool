namespace ObdLink.Core.Obd;

public sealed class UnitPrefs
{
    public string Speed { get; set; } = "mph";        // mph | km/h
    public string Temperature { get; set; } = "°F";   // °F | °C
    public string Pressure { get; set; } = "psi";     // psi | kPa
}

/// <summary>Converts base (metric, J1979) values to the user's display units.</summary>
public static class Units
{
    public static double KmhToMph(double v) => v / 1.609344;
    public static double CToF(double v) => v * 9 / 5 + 32;
    public static double KpaToPsi(double v) => v * 0.1450377;
    public static double GsToLbMin(double v) => v * 0.1322774;

    public static (double Value, string Unit) Convert(double v, Quantity q, string baseUnit, UnitPrefs p) => q switch
    {
        Quantity.Speed when p.Speed == "mph" => (KmhToMph(v), "mph"),
        Quantity.Temperature when p.Temperature == "°F" => (CToF(v), "°F"),
        Quantity.Pressure when p.Pressure == "psi" => (KpaToPsi(v), "psi"),
        _ => (v, baseUnit),
    };
    public static double ConvertOnly(double v, Quantity q, UnitPrefs p) => Convert(v, q, "", p).Value;
}
