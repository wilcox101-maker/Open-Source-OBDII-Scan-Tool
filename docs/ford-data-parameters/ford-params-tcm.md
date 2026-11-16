# Ford data parameters: Transmission Control Module (TCM)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

179 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `ABS_ACTIVE` | NORMAL/ACTIVE |  | 4x4 |
| `ABS_MSG_TCM` | PRESENT/NOT PRESENT |  |  |
| `ACCLUTCH_TCM` | ON/OFF |  |  |
| `APP1_FN_TCM` | not available |  |  |
| `APP1_TCM` | not available |  |  |
| `APP2_TCM` | not available |  |  |
| `APP3_TCM` | not available |  |  |
| `AP_PER_TCM` | not available |  |  |
| `BARO_TCM` | not available |  |  |
| `BATT_I_REC` | not available |  |  |
| `BATT_V_REC` | not available |  |  |
| `BOO_TCM` | ON/OFF |  |  |
| `BRKPEDSW` | ACTIVE/INACTIVE |  |  |
| `CCNT_TCM` | not available |  |  |
| `CLTCHAMP_CMD` | not available |  |  |
| `CONTRACTOR` | OPEN/CLOSED |  |  |
| `CPC_AMP` | not available |  | PCM |
| `DISTDIG_TCM` | not available |  |  |
| `DRIVECNT_TCM` | not available |  |  |
| `DRIVEPOS_TCM` | YES/NO |  |  |
| `DTC_CNT_TCM` | not available |  |  |
| `ECT_TCM (°C/°F)` | not available |  |  |
| `ECT_TCM_DI (°C/°F)` | not available |  |  |
| `ENGINE_STAT` | YES/NO |  |  |
| `ENG_CTO` | not available |  |  |
| `ENG_TORQ` | not available |  |  |
| `FIRMSHFT` | ON/OFF |  | PCM |
| `FLG_MLUSFM` | YES FAULT/NO FAULT |  | PCM |
| `FLG_OTLK` | YES FAULT/NO FAULT |  | PCM |
| `FRZ_DTC` | not available |  |  |
| `FUEL_CUT` | not available |  |  |
| `Gear` | not available |  |  |
| `GEAR_MAX` | not available |  | PCM |
| `Gear_OSCIP` | not available |  |  |
| `GEAR_RAT` | not available |  | PCM |
| `GEN_BRK_CMD` | ON/OFF |  |  |
| `GEN_COILTEMP (°C)(°F)` | not available |  |  |
| `GEN_CTRL` | not available |  |  |
| `GEN_INV_V` | not available |  |  |
| `GEN_SPEED` | not available |  |  |
| `GEN_TEMPHI (°C)(°F)` | not available |  |  |
| `GEN_TORQ_CMD` | not available |  |  |
| `GR_RATIO` | not available |  | PCM |
| `GTQ_OUT` | not available |  |  |
| `G_SDN_A` | not available |  |  |
| `G_SDN_B` | not available |  |  |
| `G_SDN_C` | not available |  |  |
| `IAT_TCM (°C)(°F)` | not available |  |  |
| `IGNITION` | not available |  |  |
| `IMAN_DN_TCM` | OPEN/CLOSED |  |  |
| `IMIN_UP_TCM` | OPEN/CLOSED |  |  |
| `IMS_SRC` | not available |  | PCM |
| `ISS` | not available |  |  |
| `ISS_DIR` | FORWARD/REVERSE |  | PCM |
| `ISS_F` | YES FAULT/NO FAULT |  | PCM |
| `I_SDN_1` | not available |  |  |
| `I_SDN_2` | not available |  |  |
| `LCP_AMP` | not available |  |  |
| `LF_WSPD_TCM` | not available |  |  |
| `LOAD_TCM` | not available |  |  |
| `MANSW_TCM` | OPEN/CLOSED |  |  |
| `MECH_TCM (°C/°F)` | not available |  |  |
| `MIL_DIS_TCM` | not available |  |  |
| `MTQ_OUT` | not available |  |  |
| `MTR_COILTEMP (°C)(°F)` | not available |  |  |
| `MTR_INV_V` | not available |  |  |
| `MTR_SPEED` | not available |  |  |
| `MTR_TEMPHI (°C)(°F)` | not available |  |  |
| `MTR_TORQ_CMD` | not available |  |  |
| `M_SDN_A` | not available |  |  |
| `M_SDN_B` | not available |  |  |
| `M_SDN_C` | not available |  |  |
| `NEUTPOS_TCM` | YES/NO |  |  |
| `ODOMETER_TCM` | not available |  |  |
| `OFMFLG` | YES FAULT/NO FAULT |  | PCM |
| `OSS` | not available |  |  |
| `OSS_DIR` | FORWARD/REVERSE |  | PCM |
| `OSS_F` | YES FAULT/NO FAULT |  | PCM |
| `OSS_TCM` | not available |  |  |
| `OTEMP_FMFLG` | YES FAULT/NO FAULT |  | PCM |
| `OverdriveCancel` | NOT DEPRESSED/DEPRESSED |  | PCM |
| `PCA` | not available |  | PCM |
| `PCAA` | not available |  | PCM |
| `PCAAMP_MES` | not available |  |  |
| `PCA_FLT` | NO FAULT/FAULT |  |  |
| `PCA_MES` | not available |  |  |
| `PCB` | not available |  | PCM |
| `PCBA` | not available |  | PCM |
| `PCBAMP_MES` | not available |  |  |
| `PCC` | not available |  | PCM |
| `PCCA` | not available |  | PCM |
| `PCCAMP_MES` | not available |  |  |
| `PCC_MES` | not available |  |  |
| `PCD` | not available |  | PCM |
| `PCD_AMP` | not available |  | PCM |
| `PCD_FLT` | YES FAULT/NO FAULT |  | PCM |
| `PCE` | not available |  | PCM |
| `PCE_AMP` | not available |  |  |
| `PCF` | not available |  | PCM |
| `PCF_FLT` | YES FAULT/NO FAULT |  | PCM |
| `PCG` | not available |  | PCM |
| `PCG_FLT` | YES FAULT/NO FAULT |  | PCM |
| `PCM_MSG_TCM` | PRESENT/NOT PRESENT |  |  |
| `PVT_WDS` | not available |  |  |
| `RCL_F` | YES FAULT/NO FAULT |  | PCM |
| `RESPOS_TCM` | YES/NO |  |  |
| `RF_WSPD_TCM` | not available |  |  |
| `RPM_DSD_TCM` | not available |  |  |
| `RPM_TCM` | not available |  |  |
| `RPM_TCM_HEV` | not available |  |  |
| `SHFT_TYP` | not available |  | PCM |
| `SHIFT_POS` | not available |  |  |
| `SLIP_DES_SCP` | not available |  | PCM |
| `SSAFM` | YES/NO |  |  |
| `SSA_AMP` | not available |  | PCM |
| `SSBFM` | YES/NO |  |  |
| `SSB_AMP` | not available |  | PCM |
| `SSB_SS1` | ON/OFF |  |  |
| `SSB_SS2` | ON/OFF |  |  |
| `SSC_AMP` | not available |  |  |
| `SSE` | not available |  |  |
| `SSPCA` | not available |  | PCM |
| `SSPCA_FLT` | NO FAULT/FAULT |  | PCM |
| `SSPCB` | not available |  | PCM |
| `SSPCB_FLT` | NO FAULT/FAULT |  | PCM |
| `SSPCC` | not available |  | PCM |
| `SSPCC_FLT` | NO FAULT/FAULT |  | PCM |
| `TCC` | not available |  | PCM |
| `TCCAMP_MES` | not available |  |  |
| `TCC_FLT` | YES FAULT/NO FAULT |  | PCM |
| `TCC_OSC` | not available |  | PCM |
| `TCC_SLIP_DSD` | not available |  |  |
| `TCIL` | ON/OFF |  | PCM |
| `TCIL_FLT` | YES FAULT/NO FAULT |  | PCM |
| `TCM_CAUTION` | ON/OFF |  |  |
| `TCM_HAZ` | ON/OFF |  |  |
| `TCS` | NOT DEPRESSED/DEPRESSED |  | PCM |
| `TC_SLIP` | not available |  | PCM |
| `TFT (°C/°F)` | not available |  |  |
| `TIP_MIN_TCM` | OPEN/CLOSED |  |  |
| `TIP_PL_TCM` | OPEN/CLOSED |  |  |
| `TORQUE` | not available |  | PCM |
| `TORQUE_DSD` | not available | TORQ_DSD |  |
| `TORQUE_TCM` | not available |  |  |
| `TORQ_DSD` | not available | TORQUE_DSD |  |
| `TOTF` | YES FAULT/NO FAULT |  | PCM |
| `TOT_ENG (°C/°F)` | not available |  |  |
| `TOWHAUL` | ON/OFF |  | PCM |
| `TP_PER` | not available |  |  |
| `TP_REL_TCM` | not available |  |  |
| `TQ_CNTL` | not available |  | PCM |
| `TR` | not available | TR_FREQ(Hz), TTR_DC, TR_V | PCM |
| `TRAN_TEMP (°C/°F)` | not available |  |  |
| `TRFM` | YES FAULT/NO FAULT |  | PCM |
| `TRO_N_F` | YES FAULT/NO FAULT |  | PCM |
| `TRO_P_F` | YES FAULT/NO FAULT |  | PCM |
| `TR_FREQ(Hz)` | not available | TR, TTR_DC, TR_V | PCM |
| `TR_V` | not available | TR, TR_FREQ(Hz), TTR_DC | PCM |
| `TSES_TCM` | CLOCKTIME |  |  |
| `TSFMFLG` | YES FAULT/NO FAULT | TSFMFLG_TCM | PCM |
| `TSFMFLG_TCM` | YES FAULT/NO FAULT | TSFMFLG |  |
| `TSLIPRAT` | not available |  | PCM |
| `TSS` | not available |  |  |
| `TSS_F` | YES FAULT/NO FAULT |  | PCM |
| `TSS_TCM` | not available |  |  |
| `TTR_DC` | not available | TR, TR_FREQ(Hz), TR_V |  |
| `VAR_CMD` | not available |  |  |
| `VAR_MES` | not available |  |  |
| `VEH_CTRL` | not available |  |  |
| `VOLT_SENSR` | not available |  |  |
| `VPWR` | not available | VPWR_TCM |  |
| `VPWR_TCM` | not available | VPWR |  |
| `VSFMFLG` | YES FAULT/NO FAULT |  | PCM |
| `VSS_F` | YES FAULT/NO FAULT |  |  |
| `VSS_TCM` | not available |  |  |
| `VSS_TCM_HEV` | not available |  |  |
| `VSS_TCM_HR` | not available |  |  |
| `VS_SRC` | not available |  | PCM |
| `WARMUPDTC_TCM` | not available |  |  |
