namespace ObdLink.Core.Vehicles;

public sealed record VinInfo(string Vin, bool Valid, bool CheckDigitOk, string Wmi, string Manufacturer, string Country, int? ModelYear,
    string PlantCode, string Plant, string Vds, string Serial, IReadOnlyList<string> Notes);

/// <summary>Offline VIN decode (ISO 3779 / 49 CFR 565): WMI, check digit, model year, Ford assembly plant. No network.</summary>
public static class VinDecoder
{
    private const string Alphabet = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";
    private static readonly int[] Weights = { 8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2 };
    private const string YearCodes = "ABCDEFGHJKLMNPRSTVWXY123456789"; // 30-year cycle starting 1980 / 2010

    public static readonly IReadOnlyDictionary<string, string> Wmi = new Dictionary<string, string>
    {
        ["1FA"] = "Ford (USA) passenger car", ["1FB"] = "Ford (USA) bus", ["1FC"] = "Ford (USA) chassis/stripped", ["1FD"] = "Ford (USA) incomplete vehicle / heavy truck",
        ["1FM"] = "Ford (USA) MPV / SUV", ["1FT"] = "Ford (USA) truck", ["1FV"] = "Freightliner", ["1LN"] = "Lincoln (USA) car", ["1ZV"] = "Ford (AutoAlliance) car",
        ["2FA"] = "Ford (Canada) car", ["2FM"] = "Ford (Canada) MPV", ["2FT"] = "Ford (Canada) truck", ["2LM"] = "Lincoln (Canada) MPV",
        ["3FA"] = "Ford (Mexico) car", ["3FM"] = "Ford (Mexico) MPV", ["3FT"] = "Ford (Mexico) truck", ["3LN"] = "Lincoln (Mexico) car",
        ["5LM"] = "Lincoln (USA) MPV", ["5LT"] = "Lincoln (USA) truck", ["1L1"] = "Lincoln (USA) limousine", ["1LJ"] = "Lincoln (USA) incomplete",
        ["1ME"] = "Mercury (USA) car", ["2ME"] = "Mercury (Canada) car", ["2MR"] = "Mercury (Canada) MPV", ["3ME"] = "Mercury (Mexico) car", ["4M2"] = "Mercury (USA) MPV", ["4F2"] = "Mazda-built Ford (USA)",
["3FR"] = "Ford (Mexico) medium truck", ["3FE"] = "Ford (Mexico) truck", ["2FD"] = "Ford (Canada) incomplete", ["2FZ"] = "Sterling/Ford (Canada) truck",
        ["6FP"] = "Ford (Australia)", ["6F1"] = "Ford (Australia)", ["6MP"] = "Mercury (Australia)", ["VS6"] = "Ford (Spain)", ["WF1"] = "Merkur (Ford Germany)", ["9BF"] = "Ford (Brazil)", ["8AF"] = "Ford (Argentina)", ["AFA"] = "Ford (South Africa)", ["LVS"] = "Changan Ford (China)", ["X9F"] = "Ford (Russia)", ["NM0"] = "Ford Otosan (Turkey)", ["WF0"] = "Ford (Germany)", ["SFA"] = "Ford (UK)", ["MAJ"] = "Ford (India)", ["MNB"] = "Ford (Thailand)",
    };

    /// <summary>Ford NA assembly plant codes (VIN position 11). Commonly published; individual codes are community-reported.</summary>
    public static readonly IReadOnlyDictionary<char, string> FordPlants = new Dictionary<char, string>
    {
        ['A'] = "Atlanta, GA", ['B'] = "Oakville, ON", ['C'] = "Ontario Truck, ON", ['D'] = "Ohio Assembly, Avon Lake OH", ['E'] = "Kentucky Truck, Louisville KY",
        ['F'] = "Dearborn Truck, MI", ['G'] = "Chicago, IL", ['H'] = "Lorain, OH", ['J'] = "Flat Rock, MI", ['K'] = "Kansas City, MO", ['L'] = "Michigan Assembly, Wayne MI",
        ['M'] = "Cuautitlan, Mexico", ['N'] = "Norfolk, VA", ['P'] = "Twin Cities, MN", ['R'] = "Hermosillo, Mexico", ['S'] = "Allen Park (pilot), MI",
        ['T'] = "Edison, NJ", ['U'] = "Louisville Assembly, KY", ['V'] = "Kentucky Truck (Louisville), KY", ['W'] = "Wayne, MI", ['X'] = "St. Thomas, ON", ['Z'] = "St. Louis, MO",
    };

    /// <summary>Ford Motor Co. brands only (Ford, Lincoln, Mercury, Merkur): this app is Ford-only.</summary>
    public static bool IsFordWmi(string vin)
    {
        var v = Normalize(vin);
        return v.Length >= 3 && Wmi.TryGetValue(v[..3], out var m) && (m.StartsWith("Ford") || m.StartsWith("Lincoln") || m.StartsWith("Mercury") || m.StartsWith("Merkur") || m.StartsWith("Mazda-built Ford") || m.Contains("Ford ("));
    }
    public const string FordOnlyMessage = "Ford products only: this VIN's manufacturer code (WMI) is not Ford, Lincoln or Mercury. Identify vehicle stopped after the VIN read; no profile was saved and no modules were scanned.";

    public static string Normalize(string vin) => new((vin ?? "").Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    public static bool IsWellFormed(string vin) => vin.Length == 17 && vin.All(c => Alphabet.Contains(c));

    public static int Translit(char c) => char.IsDigit(c) ? c - '0' : "ABCDEFGH".IndexOf(c) is var a and >= 0 ? a + 1
        : "JKLMN".IndexOf(c) is var b and >= 0 ? b + 1 : c == 'P' ? 7 : c == 'R' ? 9 : "STUVWXYZ".IndexOf(c) + 2;

    public static char CheckDigit(string vin)
    {
        int sum = 0; for (int i = 0; i < 17; i++) sum += Translit(vin[i]) * Weights[i];
        var r = sum % 11; return r == 10 ? 'X' : (char)('0' + r);
    }

    /// <summary>Model year from position 10. For North-American passenger/light-truck VINs, a letter in position 7 means 2010-2039, a digit 1980-2009.</summary>
    public static int? ModelYear(string vin)
    {
        var idx = YearCodes.IndexOf(vin[9]); if (idx < 0) return null;
        var baseYear = char.IsLetter(vin[6]) ? 2010 : 1980;
        return baseYear + idx;
    }

    public static VinInfo Decode(string raw)
    {
        var vin = Normalize(raw); var notes = new List<string>();
        if (!IsWellFormed(vin))
            return new(vin, false, false, vin.Length >= 3 ? vin[..3] : vin, "", "", null, "", "", "", "", new[] { "Not a 17-character VIN (letters I, O, Q are never used)" });
        var cdOk = CheckDigit(vin) == vin[8];
        if (!cdOk) notes.Add($"Check digit mismatch (pos 9 is '{vin[8]}', expected '{CheckDigit(vin)}'): typo, misread, or non-North-American VIN");
        var wmi = vin[..3];
        var mfr = Wmi.TryGetValue(wmi, out var m) ? m : "Unknown WMI";
        var country = vin[0] switch { '1' or '4' or '5' => "USA", '2' => "Canada", '3' => "Mexico", 'J' => "Japan", 'K' => "Korea", 'S' => "UK", 'W' => "Germany", 'N' => "Turkey", 'M' => "India/Thailand", _ => "Other" };
        var isFord = IsFordWmi(vin);
        var plant = isFord && FordPlants.TryGetValue(vin[10], out var pl) ? pl : (isFord ? "Unknown Ford plant code" : "(plant table is Ford-only)");
        if (!isFord) notes.Add("Not a Ford Motor Co. WMI (Ford/Lincoln/Mercury): Ford products only");
        return new(vin, true, cdOk, wmi, mfr, country, ModelYear(vin), vin[10].ToString(), plant, vin[3..8], vin[11..], notes);
    }
}
