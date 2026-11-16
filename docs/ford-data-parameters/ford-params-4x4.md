# Ford data parameters: 4x4 (4x4)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

58 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `4WDCPWMOU` | not available | 4WDCPWMST(%) |  |
| `4WDCPWMST(%)` | not available | 4WDCPWMOU |  |
| `4WDHIGLMP` | not available |  |  |
| `4WDINP_SW` | 2-W DRIVE, GND, OPEN, 4-W LOW, NEUTRAL, A4WD, 4W AUTO/HI |  |  |
| `4WDLOWLMP` | not available |  |  |
| `4WDMODE_L` | ON/OFF |  |  |
| `4X4CCWRLY` | ON/OFF |  |  |
| `4X4_BOO` | ON/OFF |  |  |
| `4X4_CWRLY` | ON/OFF |  |  |
| `4_LOW_LED` | ON/OFF |  |  |
| `ABS_ACTIVE` | ON/OFF |  | TCM |
| `ABS_MSG` | PRESENT/NOT PRESENT |  |  |
| `ABS_PWR_R` | ON/OFF |  | ABS |
| `CCNT` | not available |  |  |
| `CLCH_SOL (%)` | not available |  |  |
| `COPENPLAT` | not available |  |  |
| `CW_CCW` | not available |  |  |
| `FR_OUTSHA` | not available |  |  |
| `HALLPOWER` | not available |  |  |
| `HIGH_LAMP` | ON/OFF |  |  |
| `IGNITION(V)` | not available |  | ABS |
| `IGN_SW` | START/RUN |  |  |
| `IWE_SOL` |  |  |  |
| `IWE_SOLST` | ON/OFF |  |  |
| `LFDR_SW` | AJAR/CLOSED |  |  |
| `LF_WSPD` | not available |  |  |
| `LOW_LAMP` | ON/OFF |  |  |
| `LR_WSPD` | not available |  |  |
| `MTR_CCW` | ON/OFF |  |  |
| `MTR_CW` | ON/OFF |  |  |
| `NSAFETYSW` | NEUTRAL/OFF |  |  |
| `NTF_4X4M` |  |  |  |
| `NTF_CMD` | ON/OFF |  |  |
| `NTF_LAMP` |  |  |  |
| `OILPRES` | not available |  |  |
| `OIL_TMP` | not available |  |  |
| `PCM_MSG` | PRESENT/NOT PRESENT |  | ABS |
| `PLATE_A` | OPEN/CLOSED |  |  |
| `PLATE_B` | OPEN/CLOSED |  |  |
| `PLATE_C` | OPEN/CLOSED |  |  |
| `PLATE_D` | OPEN/CLOSED |  |  |
| `PLATE_PWR` | ON/OFF |  |  |
| `PMP_AMP` | not available |  |  |
| `RF_WSPD` | not available |  |  |
| `RPM_ENG_E4WD` | not available |  |  |
| `RR_WSPD` | not available |  |  |
| `R_PUTSHA` | not available |  |  |
| `SELTESTDTC` | not available |  | PATS |
| `SHMOTCCLO` | ON/OFF |  |  |
| `SHMOTCLOC` | not available |  |  |
| `TCYC_FS` | ACTIVE/PASSIVE |  | PATS |
| `TOW_LAMP` | ON/OFF |  |  |
| `TPI` | not available |  |  |
| `TRCASEGND` | not available |  |  |
| `VBATT` | not available |  |  |
| `VLVCHAR` | not available |  |  |
| `VLV_AMP` | not available |  |  |
| `VSS(KPH/MPH)` | not available |  |  |
