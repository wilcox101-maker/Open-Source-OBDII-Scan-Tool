namespace ObdLink.Core.Elm;

/// <summary>
/// Configurable mapping from logical bus to STP preset. Defaults are the values verified in the
/// public STN FRPM: HS-CAN = STP 33, MS-CAN = STP 53. (The original task brief said
/// "STP 53 (HS-CAN) / STP 54 (MS-CAN)"; that is incorrect per the FRPM — 54 is MS-CAN 29-bit.)
/// Override via settings if a firmware revision differs.
/// </summary>
public sealed class BusProfile
{
    public int HsCanPreset { get; set; } = (int)StnProtocol.HsCan11_500;
    public int MsCanPreset { get; set; } = (int)StnProtocol.MsCan11_125;
    /// <summary>[UNVERIFIED] Some Ford buses (e.g. HM-CAN) may need MS pins at 500k via STPBR.</summary>
    public int? MsCanBaudOverride { get; set; }
}

public enum Bus { HsCan, MsCan, J1850Vpw, J1850Pwm, KLine }
