# Ford data parameters: 4x4 (4x4)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

58 parameters. Index: [README.md](README.md).

| Parameter | Range | Description | Shares entry with | Also under | Notes |
| --- | --- | --- | --- | --- | --- |
| `4WDCPWMOU` | not available | 4WD clutch PWM status. | 4WDCPWMST(%) |  |  |
| `4WDCPWMST(%)` | not available | 4WD clutch PWM status. | 4WDCPWMOU |  |  |
| `4WDHIGLMP` | not available | 4WD HIGH indicator lamp status. |  |  |  |
| `4WDINP_SW` | 2-W DRIVE, GND, OPEN, 4-W LOW, NEUTRAL, A4WD, 4W AUTO/HI | 4WD switch status. |  |  |  |
| `4WDLOWLMP` | not available | 4WD LOW warning indicator. |  |  |  |
| `4WDMODE_L` | ON/OFF | 4WD mode lamp status. |  |  |  |
| `4X4CCWRLY` | ON/OFF | Transfer case shift relay (L2H counterclockwise status. |  |  | Description text may be missing a closing parenthesis. |
| `4X4_BOO` | ON/OFF | Brake ON/OFF status. |  |  |  |
| `4X4_CWRLY` | ON/OFF | 4WD HIGH indicator LED. |  |  | Description appears to belong to a different parameter (reads like a 4WD HIGH LED entry). |
| `4_LOW_LED` | ON/OFF | The 4x4 low status. |  |  |  |
| `ABS_ACTIVE` | ON/OFF | Clockwise relay control. |  | TCM | Also listed under: TCM. Description appears to belong to a different parameter ("Clockwise relay control." fits 4X4_CWRLY). |
| `ABS_MSG` | PRESENT/NOT PRESENT | CAN message is missing from the ABS. |  |  |  |
| `ABS_PWR_R` | ON/OFF | Antilock braking system power relay output status. |  | ABS | Also listed under: ABS. |
| `CCNT` | not available | Number of continuous codes present. |  |  |  |
| `CLCH_SOL (%)` | not available | PWM Output control command #1 status. |  |  |  |
| `COPENPLAT` | not available | Currently open contact plates status. |  |  |  |
| `CW_CCW` | not available | Clockwise relay control status. |  |  |  |
| `FR_OUTSHA` | not available | Front shaft speed status. |  |  |  |
| `HALLPOWER` | not available | Hall effect sensor power status. |  |  |  |
| `HIGH_LAMP` | ON/OFF | 4WD HIGH indicator LED. |  |  |  |
| `IGNITION(V)` | not available | Ignition status. |  | ABS | Also listed under: ABS. |
| `IGN_SW` | START/RUN | Ignition switch status. |  |  |  |
| `IWE_SOL` |  |  |  |  | Range not specified. No description available. |
| `IWE_SOLST` | ON/OFF | Integrated wheel ends solenoid status. |  |  |  |
| `LFDR_SW` | AJAR/CLOSED | Left front door ajar switch status. |  |  |  |
| `LF_WSPD` | not available | Left front wheel speed sensor. |  |  |  |
| `LOW_LAMP` | ON/OFF | 4WD Low indicator status. |  |  |  |
| `LR_WSPD` | not available | Left rear wheel speed sensor. |  |  |  |
| `MTR_CCW` | ON/OFF | Counterclockwise shift motor driver output state. |  |  |  |
| `MTR_CW` | ON/OFF | Clockwise shift motor driver output state. |  |  |  |
| `NSAFETYSW` | NEUTRAL/OFF | Neutral safety switch state. |  |  |  |
| `NTF_4X4M` |  |  |  |  | Range not specified. No description available. |
| `NTF_CMD` | ON/OFF | Neutral tow function state. |  |  |  |
| `NTF_LAMP` |  |  |  |  | Range not specified. No description available. |
| `OILPRES` | not available | All wheel drive oil pressure before any FMEM substitution. |  |  |  |
| `OIL_TMP` | not available | All wheel drive oil temperature before any FMEM substitution. |  |  |  |
| `PCM_MSG` | PRESENT/NOT PRESENT | Indicates if CAN message is missing from the PCM. |  | ABS | Also listed under: ABS. |
| `PLATE_A` | OPEN/CLOSED | Transfer case contact plate switch A state. |  |  |  |
| `PLATE_B` | OPEN/CLOSED | Transfer case contact plate switch B state. |  |  |  |
| `PLATE_C` | OPEN/CLOSED | Transfer case contact plate switch C state. |  |  |  |
| `PLATE_D` | OPEN/CLOSED | Transfer case contact plate switch D state. |  |  |  |
| `PLATE_PWR` | ON/OFF | Contact plate power state. |  |  |  |
| `PMP_AMP` | not available | Pump current - Actual. |  |  |  |
| `RF_WSPD` | not available | Right front wheel speed sensor. |  |  |  |
| `RPM_ENG_E4WD` | not available | Engine speed state. |  |  |  |
| `RR_WSPD` | not available | Right rear wheel speed sensor. |  |  |  |
| `R_PUTSHA` | not available | Rear shaft speed. |  |  |  |
| `SELTESTDTC` | not available | Diagnostic trouble codes state. |  | PATS | Also listed under: PATS. |
| `SHMOTCCLO` | ON/OFF | Counterclockwise shift motor driver short to ground. |  |  |  |
| `SHMOTCLOC` | not available | Clockwise shift motor driver short to ground. |  |  |  |
| `TCYC_FS` | ACTIVE/PASSIVE | Traction control system state. |  | PATS | Also listed under: PATS. |
| `TOW_LAMP` | ON/OFF | Neutral tow light state. |  |  |  |
| `TPI` | not available | Throttle position state. |  |  |  |
| `TRCASEGND` | not available | Transfer case contact plate ground return. |  |  |  |
| `VBATT` | not available | Battery positive voltage. |  |  |  |
| `VLVCHAR` | not available | Valve characteristic signal. |  |  |  |
| `VLV_AMP` | not available | Valve current - Actual. |  |  |  |
| `VSS(KPH/MPH)` | not available | Vehicle speed. |  |  |  |
