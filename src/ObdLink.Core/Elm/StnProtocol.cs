namespace ObdLink.Core.Elm;

/// <summary>
/// STN protocol presets for the STP command. Values verified against the public
/// "OBDLink Family Reference and Programming Manual" (STN11xx/STN21xx FRPM, §"OBD Protocol ST
/// Commands") and the "OBDLink MX Protocol Commands" sheet (scantool.net/downloads/118).
/// NOTE: STP 53/54 are BOTH MS-CAN (pins 3/11, 125 kbps); HS-CAN ISO 15765 is STP 33/34.
/// </summary>
public enum StnProtocol
{
    J1850Pwm = 11,
    J1850Vpw = 12,
    Iso9141NoInit = 21,
    Iso9141_5Baud = 22,
    Iso14230NoInit = 23,
    Iso14230_5Baud = 24,
    Iso14230Fast = 25,
    HsCanRaw11 = 31,      // ISO 11898, 11-bit, 500k, var DLC
    HsCanRaw29 = 32,
    HsCan11_500 = 33,     // ISO 15765, 11-bit, 500k  (Ford HS-CAN / OBD-II)
    HsCan29_500 = 34,
    HsCan11_250 = 35,
    HsCan29_250 = 36,
    MsCanRaw11 = 51,      // ISO 11898, 11-bit, 125k, pins 3/11
    MsCanRaw29 = 52,
    MsCan11_125 = 53,     // ISO 15765, 11-bit, 125k, pins 3/11 (Ford MS-CAN)
    MsCan29_125 = 54,
}

public static class StnProtocolInfo
{
    public static bool Is29Bit(StnProtocol p) => p is StnProtocol.HsCanRaw29 or StnProtocol.HsCan29_500 or StnProtocol.HsCan29_250 or StnProtocol.MsCanRaw29 or StnProtocol.MsCan29_125;
    public static bool IsCan(StnProtocol p) => (int)p >= 31;
}
