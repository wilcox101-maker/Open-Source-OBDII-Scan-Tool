# Ford data parameters: Antilock Brakes (ABS)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

160 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `4WDINP_SW_ABS` | 2WD/4WD) |  |  |
| `ABPROS` | ON/OFF |  |  |
| `ABS PLS` | ON/OFF |  |  |
| `ABSLAMP` | ON/OFF | ABS_LAMP |  |
| `ABSLF I` | ON/OFF | ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |
| `ABSLF O` | ON/OFF | ABSLF I, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |
| `ABSLR I` | ON/OFF | ABSLF I, ABSLF O, ABSLR O, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |
| `ABSLR O` | ON/OFF | ABSLF I, ABSLF O, ABSLR I, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |
| `ABSPMPRLY` | ON/OFF |  |  |
| `ABSR I` | ON/OFF | ABSR O |  |
| `ABSR O` | ON/OFF | ABSR I |  |
| `ABSRF I` | ON/OFF | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF O, ABSRR I, ABSRR O |  |
| `ABSRF O` | ON/OFF | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRR I, ABSRR O |  |
| `ABSRR I` | ON/OFF | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR O |  |
| `ABSRR O` | ON/OFF | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR I |  |
| `ABSVLVRLY` | ON/OFF |  |  |
| `ABS_LAMP` | ON/OFF | ABSLAMP |  |
| `ABS_MTR` | ACTIVE/INACTIVE |  |  |
| `ABS_partbaseno` | not available |  |  |
| `ABS_PartNoIdSuffix` | not available |  |  |
| `ABS_POWER` | ON/OFF |  |  |
| `ABS_PWR_R` | ON/OFF |  | 4x4 |
| `ABS_SoftVerNo` | not available |  |  |
| `ABS_TCS_WIN126_SELTESTDTC` | not available |  |  |
| `ACCLMTR` | not available |  |  |
| `ACCR_TEMP (°C)(°F)` | not available |  |  |
| `ACCR_TRAV` | ON/OFF |  |  |
| `ACU_CHK` | not available |  |  |
| `ARCFault` | YES/NO |  |  |
| `AutoRideCtrl` | ON/OFF |  |  |
| `AWD_MSG` | PRESENT/NOT PRESENT | AWD_MSG_ABS_MZ |  |
| `AWD_MSG_ABS_MZ` | PRESENT/NOT PRESENT | AWD_MSG |  |
| `BALVLV1` | ON/OFF |  |  |
| `BALVLV2` | ON/OFF |  |  |
| `BLD_STAT` | DONE/NOT DONE |  |  |
| `BOO ABS` | ON/OFF |  |  |
| `BPTSCAL` | not available |  |  |
| `BP_CALST` | DONE/NOT DONE |  |  |
| `BrakePrsApply` | 0 or 12 V |  |  |
| `BRAKE_LMP` | ON/OFF |  |  |
| `BRAKPRES(KPA/PSI)` | not available |  |  |
| `BRKLAMP` | ON/OFF |  |  |
| `BRK_CHK` | not available |  |  |
| `BRK_FLUID` | LOW/OK |  |  |
| `BRK_MSIG(KPA/PSI)` | not available |  |  |
| `BRK_RSIG` | PRESSURE |  |  |
| `BRK_WARN` | ON/OFF |  |  |
| `BSTLRNCYC` | ON/OFF |  |  |
| `BSTRBP_FSNC` | OPEN/CLOSED |  |  |
| `BSTRBP_FSNO` | OPEN/CLOSED |  |  |
| `BSTR_BSWF` | YES/NO |  |  |
| `BSTR_LCCS` | YES/NO |  |  |
| `BSTR_LERN` | PASS/FAIS |  |  |
| `BSTR_MECH` | PASS/FAIS |  |  |
| `BSTR_SOL` | ON/OFF |  |  |
| `BSTR_SOL1` | not available |  |  |
| `BSTR_SW_T` | ON/OFF |  |  |
| `CCB_LCCS` | YES/NO |  |  |
| `CCB_LCTF` | YES/NO |  |  |
| `CCLAS_ICS` | YES/NO |  |  |
| `CCLAS_ITF` | YES/NO |  |  |
| `CCNTABS` | not available |  |  |
| `CCSWA_TPS` | YES/NO |  |  |
| `CCYR_ICS` | YES/NO |  |  |
| `CCYR_ITF` | YES/NO |  |  |
| `CONPROV` | not available |  |  |
| `CUTVLV1` | ON/OFF |  |  |
| `CUTVLV2` | ON/OFF |  |  |
| `DSBL_TOG` | ON/OFF |  |  |
| `DYNOMODE` | YES/NO |  |  |
| `EVACFILL` | DONE/NOT DONE |  |  |
| `G_INPUT` | not available |  |  |
| `G_SENSOR` | ON/OFF |  |  |
| `IC_MSG` | PRESENT/NOT PRESENT |  |  |
| `IC_MSG_ABS_MZ` |  |  |  |
| `IGNITION(V)` | VOLTS |  | 4x4 |
| `IVD_R_IN` | ON/OFF |  |  |
| `IVD_R_OUT` | ON/OFF |  |  |
| `LAS_ICS` | YES/NO |  |  |
| `LATACCEL` | TRUE/FALSE |  |  |
| `LATACCL` | not available |  |  |
| `LATACINPT` | not available |  |  |
| `LF WSPD(MPH)` | 0 to 100+ MPH/KPH | LR WSPD(MPH), R WSPD(MPH), RF WSPD(MPH), RR WSPD(MPH) |  |
| `LF_FAIL` | TRUE/FALSE |  |  |
| `LF_IN(AMP)` | not available |  |  |
| `LF_INLET` | ON/OFF |  |  |
| `LF_OUT(AMP)` | not available |  |  |
| `LF_OUTLET` | ON/OFF |  |  |
| `LF_PRES` | not available |  |  |
| `LF_PRIME` | ON/OFF |  |  |
| `LF_SWITCH` | ON/OFF |  |  |
| `LF_TC_PRV` | ON/OFF |  |  |
| `LF_TC_SWV` | ON/OFF |  |  |
| `LF_WSPD(KPH/MPH)` | not available |  |  |
| `LONG_ACCL` | not available |  |  |
| `LR WSPD(MPH)` | 0 to 100+ MPH/KPH | LF WSPD(MPH), R WSPD(MPH), RF WSPD(MPH), RR WSPD(MPH) |  |
| `LR_FAIL` | TRUE/FALSE |  |  |
| `LR_IN(AMP)` | not available |  |  |
| `LR_INLET` | ON/OFF |  |  |
| `LR_OUT(AMP)` | not available |  |  |
| `LR_OUTLET` | ON/OFF |  |  |
| `LR_PRES(KPA/PSI)` | not available |  |  |
| `LR_PRIME` | ON/OFF |  |  |
| `LR_PRIME` | ON/OFF |  |  |
| `LR_SWITCH` | ON/OFF |  |  |
| `LR_SWITCH` | ON/OFF |  |  |
| `LR_TC_PRV` | ON/OFF |  |  |
| `LR_TC_SWV` | ON/OFF |  |  |
| `LR_TC_SWV` | ON/OFF |  |  |
| `LR_TC_VLV` | ON/OFF |  |  |
| `LR_TC_VLV` | ON/OFF |  |  |
| `LR_WSPD(KPH/MPH)` | not available |  |  |
| `LTRL_ACC` | not available |  |  |
| `L_ACCEL_EPB` | Acceleration/G |  |  |
| `MAP_PCM` | not available |  | PCM |
| `MC_PT_1` | not available |  |  |
| `MC_PT_2` | not available |  |  |
| `MIL_DIS` | not available |  |  |
| `MNC_VEL_1` | not available |  |  |
| `MNC_VEL_2` | not available |  |  |
| `MN_PR_TRN` | PASS/FAIS |  |  |
| `MODSTATE` | not available |  |  |
| `PART#PRE_ABS` | not available |  |  |
| `PCM_MSG` | PRESENT/NOT PRESENT |  | 4x4 |
| `PMPSTAT` | ON/OFF |  |  |
| `PMP_MOTOR` | ON/OFF |  |  |
| `PRE_CHK` | ON/OFF |  |  |
| `PRIM_VLV` | ON/OFF |  |  |
| `PUMP` | ON/OFF |  |  |
| `PUMPEFF` | not available |  |  |
| `PWR RLY` | ON/OFF |  |  |
| `R WSPD(MPH)` | 0 to 100+ MPH/KPH | LF WSPD(MPH), LR WSPD(MPH), RF WSPD(MPH), RR WSPD(MPH) |  |
| `RF WSPD(MPH)` | 0 to 100+ MPH/KPH | LF WSPD(MPH), LR WSPD(MPH), R WSPD(MPH), RR WSPD(MPH) |  |
| `RF_FAIL` | TRUE/FALSE |  |  |
| `RF_IN(AMP)` | not available |  |  |
| `RF_INLET` | ON/OFF |  |  |
| `RF_OUT(AMP)` | not available |  |  |
| `RF_OUTLET` | ON/OFF |  |  |
| `RF_PRES(KPA/PSI)` | not available |  |  |
| `RF_PRIME` | ON/OFF |  |  |
| `RF_SWITCH` | ON/OFF |  |  |
| `RF_TC_PRV` | ON/OFF |  |  |
| `RF_TC_SWV` | ON/OFF |  |  |
| `RF_WSPD(KPH/MPH)` | not available |  |  |
| `ROLLRAT` | not available | ROLLRATE |  |
| `ROLLRATE` | not available | ROLLRAT |  |
| `RPM_FF` | not available |  |  |
| `RR WSPD(MPH)` | 0 to 100+ MPH/KPH | LF WSPD(MPH), LR WSPD(MPH), R WSPD(MPH), RF WSPD(MPH) |  |
| `RR_FAIL` | TRUE/FALSE |  |  |
| `RR_IN(AMP)` | not available |  |  |
| `RR_INLET` | ON/OFF |  |  |
| `RR_OUT(AMP)` | not available |  |  |
| `RR_OUTLET` | ON/OFF |  |  |
| `RR_PRES(KPA/PSI)` | not available |  |  |
| `RR_PRIME` | ON/OFF |  |  |
| `RR_SWITCH` | ON/OFF |  |  |
| `RR_TC_VLV` | ON/OFF |  |  |
| `R_DIF_SPD` | not available |  |  |
| `R_INLET` | ON/OFF |  |  |
| `R_OUTLET` | ON/OFF |  |  |
