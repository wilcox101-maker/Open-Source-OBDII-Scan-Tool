# Ford data parameters: Passive Antitheft System (PATS)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

55 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `AntiScan` | ON/OFF |  |  |
| `C KEYMD` | ACTIVE/NOTACTIVE |  |  |
| `ENABLE S` | DISABLE/ENABLE |  |  |
| `FAILSAF` | ON/OFF |  |  |
| `M KEY` | NOTPRE/PRESENT | Master KEY |  |
| `Master KEY` | NOTPRE/PRESENT | M KEY |  |
| `NUMKEYS` | 0 to 255 |  | PCM |
| `PCM ID` | STORED/NOTSTORED |  |  |
| `PCM VFY` | YES/NO |  |  |
| `RR_WSPD(KPH/MPH)` | not available |  |  |
| `SAS_CAL` | TRUE/FALSE |  |  |
| `SECND_VLV` | ON/OFF |  |  |
| `SECPR_TRN` | PASS/FAIS |  |  |
| `SELTESTDTC` | not available |  | 4x4 |
| `SENVBAT(V)` | not available |  |  |
| `SEN_CAL` | not available |  |  |
| `SPAREKY` | DISABLE/ENABLE |  |  |
| `STEER_ANGL` | not available |  |  |
| `ST_WH_ANG` | not available |  |  |
| `SWA1_CIR` | Open/Sht-B+/Sht-Gnd/Normal |  |  |
| `SWA1_INPT` | HIGH/LOW |  |  |
| `SWA2_CIR` | Open/Sht-B+/Sht-Gnd/Normal |  |  |
| `SWA2_INPT` | HIGH/LOW |  |  |
| `SWA_CCW` | not available | SWA_CW |  |
| `SWA_CW` | not available | SWA_CCW |  |
| `SWA_LPS` | YES/NO |  |  |
| `SWA_POS(°)` | not available |  |  |
| `SW_TEST` | ON/OFF |  |  |
| `TA_LVAL` | ON/OFF |  |  |
| `TA_RVAL` | ON/OFF |  |  |
| `TCYC_FS` | ACTIVE/PASSIVE or ON/OFF |  | 4x4 |
| `TCYC_SW` | NOT DEPRESSED/DEPRESSED or ACTIVATED/NOT ACTIVATED |  |  |
| `TC_RVAL` | ON/OFF |  |  |
| `TDPT_COMP` | YES/NO |  |  |
| `TDPT_PASS` | YES/NO |  |  |
| `TRAC SW` | ON/OFF |  |  |
| `TRACtionassist` | ON/OFF |  |  |
| `V ENABLE` | DISABLE/ENABLE |  |  |
| `VALVCAL` | not available |  |  |
| `VALVE_CAL` | DONE/NOT DONE |  |  |
| `VSS_FF` | not available |  |  |
| `VSS_PCM` | not available |  |  |
| `WAKESIG` | not available |  |  |
| `XDCR_ACCR(KPA/PSI)` | not available |  |  |
| `XDCR_LFW(KPA/PSI)` | not available |  |  |
| `XDCR_LRW(KPA/PSI)` | not available |  |  |
| `XDCR_RFW(KPA/PSI)` | not available |  |  |
| `XDCR_RRW(KPA/PSI)` | not available |  |  |
| `XDCR_TMC(KPA/PSI)` | not available |  |  |
| `XDUCERCAL` | ENERGIZED/DE-ENERGIZED |  |  |
| `YAWRATE` | ON/OFF |  |  |
| `YAWRTSEN(°)` | not available |  |  |
| `YAW_INPUT` | not available |  |  |
| `YAW_RATE(°)` | not available |  |  |
| `YR_ICS` | YES/NO |  |  |
