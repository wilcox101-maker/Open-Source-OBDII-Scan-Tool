# Ford data parameters: Powertrain Control Module (PCM)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

810 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `3/2 SHIFT SOL` | ON/OFF | 4/3 SHIFT SOL |  |
| `4/3 SHIFT SOL` | ON/OFF | 3/2 SHIFT SOL |  |
| `4X4Low` | ON/OFF |  |  |
| `A/CClutchSw` | ON/OFF |  |  |
| `A/CPrs(V)` | 0.0 to 5.0 V |  |  |
| `AAT(°C/°F)` | not available |  |  |
| `ACClutchHEV` | ON/OFF |  |  |
| `ACCS` | ON/OFF |  |  |
| `ACCS=A/C` | ON/OFF |  |  |
| `ACDS1` | ENABLE/DISABLE |  |  |
| `ACDS2` | ENABLE/DISABLE |  |  |
| `ACFD_BAT` | ON/OFF |  |  |
| `ACFD_PAS` | ON/OFF |  |  |
| `ACL=ACCEL` | ON/OFF |  |  |
| `AclPedl(V)` | 0.0 to 5.0 V |  |  |
| `ACP` | not available |  |  |
| `ACP` | OPEN/CLOSED |  |  |
| `ACP_PSI(KPA)` | not available |  |  |
| `ACRDV` | ON/OFF |  |  |
| `ACRDV_F` | YES FAULT/NO FAULT |  |  |
| `ACTF` | YES/NO |  |  |
| `ACTMNT_F` | YES FAULT/NO FAULT |  |  |
| `ACT_MNT` | ON/OFF |  |  |
| `AC_RC_SW` | ON/OFF |  |  |
| `AFT2_ADDS` | not available |  |  |
| `AFT_ADDS` | not available |  |  |
| `AIR` | ON/OFF |  |  |
| `AIR A` | ON/OFF |  |  |
| `AIR B` | ON/OFF |  |  |
| `AIR D` | ON/OFF | AIRDivert |  |
| `AIRBA` | ON/OFF |  |  |
| `AIRBypass` |  |  |  |
| `AIRCON_SW_STATUS` | ON/OFF |  |  |
| `AIRD` | ON/OFF |  |  |
| `AIRDA` | ON/OFF |  |  |
| `AIRDivert` | ON/OFF | AIR D |  |
| `AIREVAL_DC_MODE1` | YES/NO |  |  |
| `AIRFault` | YES/NO |  |  |
| `AIRFLOW(g/S)` | 0 to 255 | MAF(gm/S) |  |
| `AIRM` | ON/OFF |  |  |
| `AIRMonitor` | ON/OFF |  |  |
| `AIR_FLT` | YES FAULT/NO FAULT |  |  |
| `ANTISCAN` | ACTIVE/OFF |  |  |
| `AP(%)` | not available |  |  |
| `AP(V)` | 0 to 5.00 V | APP1(V), APP2(V), APP3(V) |  |
| `APP1(V)` | 0 to 5.00 V | AP(V), APP2(V), APP3(V) |  |
| `APP1(V)` | not available |  |  |
| `APP2(V)` | 0 to 5.00 V | AP(V), APP1(V), APP3(V) |  |
| `APP3(V)` | 0 to 5.00 V | AP(V), APP1(V), APP2(V) |  |
| `APP_F` | NO FAULT/FAULT |  |  |
| `APP_LOW(V)` | not available |  |  |
| `APP_MODE` | CT/PT/WOT |  |  |
| `AP_MODE_TCM` | PT/WOT/CT |  |  |
| `AP_PER_TCM(%)` | not available |  |  |
| `ARPMDES(rpm)` | not available |  |  |
| `AST` |  |  |  |
| `AST2` | not available |  |  |
| `AXLE` | not available |  |  |
| `A_CT(°C/°F)` | not available |  |  |
| `A_CT_V(V)` | not available |  |  |
| `BARO(Hz)` | 125 to 160 Hz) |  |  |
| `BARO(V)` | 2.6 to 4.6 V |  |  |
| `BLoWerMoToR` | ON/OFF |  |  |
| `BLSHORT BATT` | YES/NO |  |  |
| `BLSHORT GND` | YES/NO |  |  |
| `BOO` | ON/OFF |  |  |
| `BOO=BRAKE SW` | ON/OFF |  |  |
| `BP=BARO (kPa)` | see description | BP=BARO (”HG) |  |
| `BP=BARO (”HG)` | see description | BP=BARO (kPa) |  |
| `BPA` | ON/OFF |  |  |
| `BPA_SW` | ON/OFF |  |  |
| `BPO` | YES/NO |  |  |
| `BPO_Hz` | not available |  |  |
| `BPP_BOO` | ON/OFF |  |  |
| `BrakeOnOff` | ON/OFF |  |  |
| `CAMDC 1` | not available | CAMDC 2 |  |
| `CAMDC 2` | not available | CAMDC 1 |  |
| `CAMDCR` | 0 to 99% |  |  |
| `CAMERR1` | not available | CAMERR2 |  |
| `CAMERR2` | not available | CAMERR1 |  |
| `CANP=PURGE` | ON/OFF |  |  |
| `CASeGND(V)` | -16.0 to 16.0 V |  |  |
| `CAT mon ready` | YES/NO | CAT MONITOR |  |
| `CAT MONITOR` | YES/NO | CAT mon ready |  |
| `CATCAL1(Hz)` | 1 to 4 Hz) | CATCAL2(Hz) |  |
| `CATCAL2(Hz)` | 1 to 4 Hz) | CATCAL1(Hz) |  |
| `CATEVAL_DC_MODE1` | YES/NO |  |  |
| `CATTST1(Hz)` | 1 to 4 Hz) | CATTST2(Hz) |  |
| `CATTST2(Hz)` | 1 to 4 Hz) | CATTST1(Hz) |  |
| `CCC=TCC` | LOCKED/OFF |  |  |
| `CCL` | ON/OFF |  |  |
| `CCLF` | YES FAULT/NO FAULT |  |  |
| `CCMSTAT_DC_MODE1` | YES/NO |  |  |
| `CCS` | ON/OFF |  |  |
| `CCSA` | ON/OFF |  |  |
| `CCSFault` | YES/NO |  |  |
| `CCS_FLT` | YES FAULT/NO FAULT |  |  |
| `CHT SENSOR(°)` | ON/OFF | CHT(°C/°F) |  |
| `CHT(°C/°F)` | ON/OFF | CHT SENSOR(°) |  |
| `CHTFM` | YES FAULT/NO FAULT |  |  |
| `CHTIL` | ON/OFF |  |  |
| `CHTILFault` | YES/NO |  |  |
| `CHTIL_FLT` | FAULT/NO FAULT |  |  |
| `CHT_COLD(°C/°F)` | not available |  |  |
| `CHT_HOT(°C/°F)` | not available |  |  |
| `CHT_V(°C/°F)` | not available |  |  |
| `CKP_PROF` | YES/NO |  |  |
| `CLRDIST_NOMIL` | not available |  |  |
| `CMPFM` | YES/NO |  |  |
| `CMPFM` | YES FAULT/NO FAULT |  |  |
| `CMPFM2` | YES FAULT/NO FAULT |  |  |
| `CmprCompMon rdy` | YES/NO |  |  |
| `CMPS_lp_Lev` | HIGH/LOW |  |  |
| `CMPS_Status` | OK/FAULT |  |  |
| `COAST` | ON/OFF |  |  |
| `CoastClSol` | ON/OFF |  |  |
| `CoastClSol(%)` | 0 to 100% |  |  |
| `CoastClSol(mA)` | 0 to 1000 mA |  |  |
| `CPC_AMP` | not available |  | TCM |
| `CPP` | ON/OFF |  |  |
| `CPP/TCS` | ON/OFF |  |  |
| `CPP_ST` | NOT DEPRESSED/DEPRESSED |  |  |
| `CRUISE` | ON/OFF |  |  |
| `CRUISE_SW` | ON/OFF |  |  |
| `CSEGND(V)` | -16.0 to 16.0 V |  |  |
| `CylHdTemp(V)` | (range 0.25 to 4.75 V |  |  |
| `DCDC_ENA` | ON/OFF |  |  |
| `DCDC_F` | YES FAULT/NO FAULT |  |  |
| `DPFE(V)` | 0.45 to 4.60 V |  |  |
| `DPFEGR` | not available |  |  |
| `DRIVE CounT` | 0 to 255 |  |  |
| `DRIVECNT` | not available | Drive_COUNT |  |
| `Drive_COUNT` | not available | DRIVECNT |  |
| `DSD` | not available |  |  |
| `DTC` | not available |  |  |
| `DTC CouNT (pcm)` | see description |  |  |
| `DTC_CNT` | not available |  |  |
| `EBP(V)` | 0 to 5 V |  |  |
| `ECT` | NORM/COLD/HOT |  |  |
| `ECT(V)` | 0.0 to 5.0 |  |  |
| `ECT(°C)` | -40 to 199 |  |  |
| `ECT(°F)` | -40 to 389 |  |  |
| `ECTF` | YES FAULT/NO FAULT |  |  |
| `ECT_FF (°C/°F)` | not available |  |  |
| `EFTA` | not available |  |  |
| `EGR mon ready` | YES/NO | EGR MONITOR |  |
| `EGR MONITOR` | YES/NO | EGR mon ready |  |
| `EGRBARO` | 0 to 5 V |  |  |
| `EGRCFault` | YES/NO |  |  |
| `EGRCvacsol` | ON/OFF |  |  |
| `EGRDC(%)` | not available |  |  |
| `EGRDtyCycl(%)` | 0 to 100% |  |  |
| `EGREVAL_DC_MODE1` | YES/NO |  |  |
| `EGRFOpen` | YES/NO |  |  |
| `EGRFShort` | YES/NO |  |  |
| `EGRMC1` | ON/OFF | EGRMC2, EGRMC3, EGRMC4 |  |
| `EGRMC1F` | YES FAULT/NO FAULT | EGRMC2F, EGRMC3F, EGRMC4F |  |
| `EGRMC2` | ON/OFF | EGRMC1, EGRMC3, EGRMC4 |  |
| `EGRMC2F` | YES FAULT/NO FAULT | EGRMC1F, EGRMC3F, EGRMC4F |  |
| `EGRMC3` | ON/OFF | EGRMC1, EGRMC2, EGRMC4 |  |
| `EGRMC3F` | YES FAULT/NO FAULT | EGRMC1F, EGRMC2F, EGRMC4F |  |
| `EGRMC4` | ON/OFF | EGRMC1, EGRMC2, EGRMC3 |  |
| `EGRMC4F` | YES FAULT/NO FAULT | EGRMC1F, EGRMC2F, EGRMC3F |  |
| `EGRMDSD` | not available |  |  |
| `EGRTP(V)` | 0.30 to 4.50 V | EGRVP(V) |  |
| `EGRTPDC(%)` | not available |  |  |
| `EGRVentsol` | ON/OFF |  |  |
| `EGRVFault` | ON/OFF |  |  |
| `EGRVP(V)` | 0.30 to 4.50 V | EGRTP(V) |  |
| `EGRVPDES` | 0 to 14% |  |  |
| `EGRVR(%)` | 0 to 90% |  |  |
| `EGRVR_FLT` | No fault/Short/Open/Yes Fault/Invalid |  |  |
| `EGR_EVAL` | YES/NO |  |  |
| `EGR_Fault` | YES FAULT/NO FAULT |  |  |
| `ElecPrsCtrl` | 0 to 100 psi |  |  |
| `ENG LOAD(%)` | see description | LOAD(%) |  |
| `EngFuelTA(V)` | 0 to 5.00 V | EngFuelTB(V) |  |
| `EngFuelTB(V)` | 0 to 5.00 V | EngFuelTA(V) |  |
| `EngOilTemp(°)` | 0 to 212°F or -17.8 to 100°C | EOT(°), EOT_DEG(°C/°F) |  |
| `EOT(V)` | 0 to 5 V |  |  |
| `EOT(°)` | 0 to 212°F or -17.8 to 100°C | EngOilTemp(°), EOT_DEG(°C/°F) |  |
| `EOTF` | YES FAULT/NO FAULT |  |  |
| `EOTM` | HOT/COLD/NORM/DEF |  |  |
| `EOT_DEG(°C/°F)` | 0 to 212°F or -17.8 to 100°C | EOT(°), EngOilTemp(°) |  |
| `EOT_M(°C/°F)` | not available |  |  |
| `EPC(psi)` | 0 to 100 psi |  |  |
| `EPC(V)` | 2 V to battery voltage |  |  |
| `EPCV(V)` | not available |  |  |
| `EPR(%)` | 0 to 100% |  |  |
| `ETC_ACT` | Actual degrees |  |  |
| `ETC_CTRL` | not available |  |  |
| `ETC_DSD` | Desired degrees |  |  |
| `EVAP mon ready` | YES/NO |  |  |
| `EVAPC` | 0 to 100% | EVAPCV(%) |  |
| `EVAPCP` | ON/OFF |  |  |
| `EVAPCPFault` | YES/NO |  |  |
| `EVAPCP_FLT` | YES FAULT/NO FAULT |  |  |
| `EVAPCV` | 0 to 100% |  |  |
| `EVAPCV(%)` | 0 to 100% | EVAPC |  |
| `EVAPCVA` | ON/OFF |  |  |
| `EVAPCVF1` | YES FAULT/NO FAULT |  |  |
| `EVAPCVFault` | YES/NO |  |  |
| `EVAPCVFLT` | YES FAULT/NO FAULT |  |  |
| `EVAPEVAL_DC_MODE1` | YES/NO |  |  |
| `EVAPPrgFlw(V)` | 0 to 5 V |  |  |
| `EVAPSTA` | not available |  |  |
| `EVAPVM(%)` | not available |  |  |
| `EVAPVMA` | ON/OFF |  |  |
| `EVAPVM_Fault` | YES FAULT/NO FAULT |  |  |
| `EVMV` | OFF/ON/VARYING |  |  |
| `EVP(V)` | 0.30 to 4.50 V | STE=EVP(V) |  |
| `EVR(%)` | 0 to 100% |  |  |
| `ExhBackPrs` | 0 to 2368 psi |  |  |
| `FAN` | LOW/HIGH/OFF |  |  |
| `FAN Monitor` | ON/OFF |  |  |
| `FANM(HIGH)` | ON/OFF |  |  |
| `FANSS` | not available |  |  |
| `FANSSM` | HIGH/LOW |  |  |
| `FAN_FLT` | No Fault/LFC Fault/HFC FAULT |  |  |
| `FAN_REQ` | ON/OFF |  |  |
| `FDSP_ESCS_GPL` | ON/OFF |  |  |
| `FFINF(%)` | not available |  |  |
| `FFLRND` | YES/NO |  |  |
| `FICMSYNC` | YES/NO |  |  |
| `FICM_LPWR` | 10.5 to 15.5 V | FICM_VPWR |  |
| `FICM_MPWR` | 40 to 52 V |  |  |
| `FICM_VPWR` | 10.5 to 15.5 V | FICM_LPWR |  |
| `FIRMSHFT` | ON/OFF |  | TCM |
| `FlexFuel(Hz)` | -32,512 to 32,768 |  |  |
| `FLG_MLUSFM` | YES FAULT/NO FAULT |  | TCM |
| `FLG_OTLK` | YES FAULT/NO FAULT |  | TCM |
| `FLG_SS4` | ON/OFF |  |  |
| `FLI(V)` | not available |  |  |
| `FLIFM` | YES/NO |  |  |
| `FLInput(%)` | 0 to 100% | FueLvlimput(%) |  |
| `FLInput(V)` | 0 to 32.0 V | FueLvlinput(V) |  |
| `FLI_PERCENT(%)` | not available |  |  |
| `FLI_SG(V)` | not available |  |  |
| `FP` | ON/OFF |  |  |
| `FP=FUEL PUMP` | ON/OFF |  |  |
| `FPA STATE` | ON/OFF |  |  |
| `FPFault` | YES/NO |  |  |
| `FPM` | ON/OFF |  |  |
| `FPM(%)` | not available |  |  |
| `FPMode` |  |  |  |
| `FPMonitor` | ON/OFF |  |  |
| `FP_FLT` | YES FAULT/NO FAULT |  |  |
| `FRP(psi)` | 37 to 150 psi |  |  |
| `FRP(V)` | not available |  |  |
| `FRPFM` | YES FAULT/NO FAULT |  |  |
| `FRP_DSD` | not available |  |  |
| `FRP_FAULT` | YES FAULT/NO FAULT |  |  |
| `FRP_PRESS` | not available |  |  |
| `FRT1FM` | YES FAULT/NO FAULT |  |  |
| `FSVFault` | YES/NO | FuelSolVlv |  |
| `FSVMonitor` | ON/OFF |  |  |
| `FTIV_F` | YES FAULT/NO FAULT |  |  |
| `FTIV_HEV(%)` | not available |  |  |
| `FTP(KPA)` | not available |  |  |
| `FTP_FAULT` | YES FAULT/NO FAULT |  |  |
| `FUEL mon ready` | YES/NO |  |  |
| `FUEL STATUS` | see description | FUEL SYS, FUEL SYS1, FUEL SYS2, FUELSYS |  |
| `FUEL SYS` | see description | FUEL STATUS, FUEL SYS1, FUEL SYS2, FUELSYS |  |
| `FUEL SYS1` | see description | FUEL STATUS, FUEL SYS, FUEL SYS2, FUELSYS |  |
| `FUEL SYS2` | see description | FUEL STATUS, FUEL SYS, FUEL SYS1, FUELSYS |  |
| `FuelPumpA` | ON/OFF |  |  |
| `FUELPW` | not available |  |  |
| `FUELPW(mS)` | 0 to 99.9 |  |  |
| `FUELPW1(mS)` | 0 to 99.9 | FUELPW2(mS) |  |
| `FUELPW2(%)` | not available |  |  |
| `FUELPW2(mS)` | 0 to 99.9 | FUELPW1(mS) |  |
| `FuelRailPrs(V)` | 0 to 4.5 V |  |  |
| `FuelSolVlv` | YES/NO | FSVFault |  |
| `FUELSYS` | see description | FUEL STATUS, FUEL SYS, FUEL SYS1, FUEL SYS2 |  |
| `FuelTankPrs(V)` | 0 to 5.0 V |  |  |
| `FueLvlimput(%)` | 0 to 100% | FLInput(%) |  |
| `FueLvlinput(V)` | 0 to 32.0 V | FLInput(V) |  |
| `FUEL_RAIL_TEMP(°C/°F)` | not available |  |  |
| `GEAR` | 1st/2nd/3rd/4th/5th/6th/REV |  |  |
| `GEAR_MAX` | not available |  | TCM |
| `Gear_OSC` | not available |  |  |
| `GEAR_RAT` | not available |  | TCM |
| `GENB_F` | YES FAULT/NO FAULT |  |  |
| `GENB_FAULT` | YES/NO |  |  |
| `GENF` | YES FAULT/NO FAULT |  |  |
| `GENFDC(%)` | not available |  |  |
| `GENFIL` | YES/NO |  |  |
| `GENLMP` | )ON/OFF | GEN_LAMP |  |
| `GENVDSD` | not available |  |  |
| `GEN_FAULT` | YES/NO |  |  |
| `GEN_LAMP` | )ON/OFF | GENLMP |  |
| `GEN_MON` | LOW/HIGH |  |  |
| `GEN_MON(%)` | not available |  |  |
| `GlowPlugCtrl(%)` | 0 to 100% |  |  |
| `GPC(%)` | not available |  |  |
| `GPCoilTiMe` | 10 to 120 seconds |  |  |
| `GPCTM` | not available |  |  |
| `GPC_L` | not available |  |  |
| `GPC_R` | not available |  |  |
| `GPLampTiMe` | 2 to 10 seconds |  |  |
| `GPLTM` | not available |  |  |
| `GR_RATIO` | not available |  | TCM |
| `GTQ_OUT_ENG` | not available |  |  |
| `G_SDN` | YES/NO |  |  |
| `HCATEVAL_DC_MODE1` | YES/NO |  |  |
| `HFC` | ON/OFF |  |  |
| `HFC=HI FAN` | ON/OFF | LFC=LO FAN, HighFanCtrl |  |
| `HFCA` | ON/OFF |  |  |
| `HFCFault` | YES/NO |  |  |
| `HFC_FLT` | YES FAULT/NO FAULT |  |  |
| `HighFanCtrl` | ON/OFF | LFC=LO FAN, HFC=HI FAN |  |
| `HO2S mon ready` | YES/NO | HO2S MONITOR |  |
| `HO2S MONITOR` | YES/NO | HO2S mon ready |  |
| `HO2S11(mA)` | 0 to 1000 mA) | HO2S12(mA), HO2S21(mA), HO2S22(mA) |  |
| `HO2S12(mA)` | 0 to 1000 mA) | HO2S11(mA), HO2S21(mA), HO2S22(mA) |  |
| `HO2S21(mA)` | 0 to 1000 mA) | HO2S11(mA), HO2S12(mA), HO2S22(mA) |  |
| `HO2S22(mA)` | 0 to 1000 mA) | HO2S11(mA), HO2S12(mA), HO2S21(mA) |  |
| `HPRC_ENA` | ON/OFF |  |  |
| `HPRC_F` | YES FAULT/NO FAULT |  |  |
| `HTR11` | ON/OFF | HTR11A, HTR12A, HTR12, HTR21A, HTR21, HTR22A, HTR22 |  |
| `HTR11A` | ON/OFF | HTR11, HTR12A, HTR12, HTR21A, HTR21, HTR22A, HTR22 |  |
| `HTR11F` | YES/NO | HTR11Fault, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |
| `HTR11Fault` | YES/NO | HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |
| `HTR12` | ON/OFF | HTR11A, HTR11, HTR12A, HTR21A, HTR21, HTR22A, HTR22 |  |
| `HTR12A` | ON/OFF | HTR11A, HTR11, HTR12, HTR21A, HTR21, HTR22A, HTR22 |  |
| `HTR12F` | YES/NO | HTR11Fault, HTR11F, HTR12Fault, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |
| `HTR12Fault` | YES/NO | HTR11Fault, HTR11F, HTR12F, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |
| `HTR13` | ON/OFF |  |  |
| `HTR21` | ON/OFF | HTR11A, HTR11, HTR12A, HTR12, HTR21A, HTR22A, HTR22 |  |
| `HTR21A` | ON/OFF | HTR11A, HTR11, HTR12A, HTR12, HTR21, HTR22A, HTR22 |  |
| `HTR21F` | YES/NO | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR22Fault, HTR22F |  |
| `HTR21F` | YES FAULT/NO FAULT |  |  |
| `HTR21Fault` | YES/NO | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21F, HTR22Fault, HTR22F |  |
| `HTR22` | ON/OFF | HTR11A, HTR11, HTR12A, HTR12, HTR21A, HTR21, HTR22A |  |
| `HTR22A` | ON/OFF | HTR11A, HTR11, HTR12A, HTR12, HTR21A, HTR21, HTR22 |  |
| `HTR22F` | YES/NO | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22Fault |  |
| `HTR22Fault` | YES/NO | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22F |  |
| `HTR23` | ON/OFF |  |  |
| `HTRCM11` | not available |  |  |
| `HTRCM12` | not available |  |  |
| `HTRCM21` | not available |  |  |
| `HTRCM22` | not available |  |  |
| `HTREVAL_DC_MODE1` | YES/NO |  |  |
| `HTRX1` | ON/OFF | HTRX2 |  |
| `HTRX2` | ON/OFF | HTRX1 |  |
| `IAC(%)` |  |  |  |
| `IAC=IDLE AIR(%)` | 0 to 100% |  |  |
| `IACDTCY(%)` | not available |  |  |
| `IACF` | not available |  |  |
| `IACFault` | YES/NO |  |  |
| `IAC_CELL0` | not available |  |  |
| `IAC_CELL1` | not available |  |  |
| `IAC_CELL2` | not available |  |  |
| `IAC_CELL3` | not available |  |  |
| `IAC_FLT` | YES FAULT/NO FAULT |  |  |
| `IAC_MODE` | not available |  |  |
| `IAC_TRIM` | not available |  |  |
| `IAT` | 0 to 255°C or °F | IAT(°C), IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |
| `IAT(V)` | 0.0 to 5.0 V | IAT2(V) |  |
| `IAT(°C)` | 0 to 255°C or °F | IAT, IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |
| `IAT(°F)` | 0 to 255°C or °F | IAT, IAT(°C), IAT_C, IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |
| `IAT2` | 0 to 255°C or °F | IAT, IAT(°C), IAT(°F), IAT_C, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |
| `IAT2(V)` | 0.0 to 5.0 V | IAT(V) |  |
| `IAT2_TEMP` | 0 to 255°C or °F | IAT, IAT(°C), IAT(°F), IAT_C, IAT2, IAT_T, IAT2_VOLTS |  |
| `IAT2_VOLTS` | 0 to 255°C or °F | IAT, IAT(°C), IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT_T |  |
| `IAT=ACT(V)` | 0.0 to 5.0 V |  |  |
| `IAT=ACT(°C)` | -40 to 199°C |  |  |
| `IAT=ACT(°F)` | -40 to 389°F |  |  |
| `IAT_C` | 0 to 255°C or °F | IAT, IAT(°C), IAT(°F), IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |
| `IAT_FAULT` | YES FAULT/NO FAULT |  |  |
| `IAT_T` | 0 to 255°C or °F | IAT, IAT(°C), IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT2_VOLTS |  |
| `ICP(V)` | 0.0 to 5.0 V |  |  |
| `ICP_DES` | not available |  |  |
| `ICP_PRESS` | not available |  |  |
| `IGN ADVANCE(°)` | -90° to +90° | SPARK ADV(°), SPRKADV(°) |  |
| `IGN_RUN_PCM` | not available |  |  |
| `ILINE_V` | not available |  |  |
| `IMRC` | ON/OFF |  |  |
| `IMRC1M(V)` |  |  |  |
| `IMRC2M(V)` | not available |  |  |
| `IMRCFault` | YES/NO |  |  |
| `IMRCFLT` | YES FAULT/NO FAULT |  |  |
| `IMRCM(V)` | not available |  |  |
| `IMS_SRC` | not available |  | TCM |
| `IMTV(%)` | 0 to 100% |  |  |
| `IMTV1_PER(%)` | not available |  |  |
| `IMTV2` | ON/OFF |  |  |
| `IMTV2_FLT` | YES FAULT/NO FAULT |  |  |
| `IMTV2_PER(%)` | not available |  |  |
| `IMTVF` | YES FAULT/NO FAULT |  |  |
| `IMTVFault` | YES/NO |  |  |
| `IMTV_FLT` | YES FAULT/NO FAULT |  |  |
| `INJ` | YES FAULT/NO FAULT |  |  |
| `INJ PW1(mS)` | 0 to 99.9) | INJ PW2(mS) |  |
| `INJ PW2(mS)` | 0 to 99.9) | INJ PW1(mS) |  |
| `INJ1` | YES FAULT/NO FAULT | INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |
| `INJ10` | YES FAULT/NO FAULT | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9 |  |
| `INJ2` | YES FAULT/NO FAULT | INJ1, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |
| `INJ3` | YES FAULT/NO FAULT | INJ1, INJ2, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |
| `INJ4` | YES FAULT/NO FAULT | INJ1, INJ2, INJ3, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |
| `INJ5` | YES FAULT/NO FAULT | INJ1, INJ2, INJ3, INJ4, INJ6, INJ7, INJ8, INJ9, INJ10 |  |
| `INJ6` | YES FAULT/NO FAULT | INJ1, INJ2, INJ3, INJ4, INJ5, INJ7, INJ8, INJ9, INJ10 |  |
| `INJ7` | YES FAULT/NO FAULT | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ8, INJ9, INJ10 |  |
| `INJ8` | YES FAULT/NO FAULT | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ9, INJ10 |  |
| `INJ9` | YES FAULT/NO FAULT | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ10 |  |
| `INJnFault` | YES/NO |  |  |
| `InjPrsReg(%)` | 0 to 100% |  |  |
| `INJ_TIM` | not available |  |  |
| `InManRunCtrl` | ON/OFF |  |  |
| `InManRunCtrl(%)` | 0 to 100% |  |  |
| `IPR(%)` | not available |  |  |
| `ISS(rpm)` | not available |  |  |
| `ISS_DIR` | FORWARD/REVERSE |  | TCM |
| `ISS_F` | YES FAULT/NO FAULT |  | TCM |
| `ISS_FAULT` | YES/NO |  |  |
| `ITS1` | GEAR1/NOT GEAR1 |  |  |
| `ITS2` | NOT GEAR/GEAR |  |  |
| `ITSD` | DRIVE/NOT DRIVE |  |  |
| `ITSR` | REVERSED/NOT REVERSED |  |  |
| `IVS` | YES/NO |  |  |
| `KAMFUSE` | ON/FAULT |  |  |
| `KEYPOS` | not available |  |  |
| `KNOCK_1` | not available |  |  |
| `KNOCK_2` | not available |  |  |
| `LCF_FLT` | YES FAULT/NO FAULT |  |  |
| `LFC` | ON/OFF |  |  |
| `LFC=LO FAN` | ON/OFF | HFC=HI FAN, HighFanCtrl |  |
| `LFCFault` | YES/NO |  |  |
| `LFT1(%)` | not available |  |  |
| `LFTRIM 1(%)` | -35% to +35% | LFTRIM 2(%) |  |
| `LFTRIM 2(%)` | -35% to +35% | LFTRIM 1(%) |  |
| `LOAD(%)` | see description | ENG LOAD(%) |  |
| `LOAD_FF(%)` | not available |  |  |
| `LONGFT1(%)` | not available |  |  |
| `LONGFT2(%)` | not available |  |  |
| `LOOP` | OPEN/CLSD) | OPEN/CLSD LOOP |  |
| `LOSSTRT` | YES/NO |  |  |
| `LOS_BRAK` | YES/NO |  |  |
| `LOS_ENG` | YES/NO |  |  |
| `LOS_EQ` | YES/NO |  |  |
| `LOS_ETC` | YES/NO |  |  |
| `LOS_GEN` | YES/NO |  |  |
| `LOS_HV` | YES/NO |  |  |
| `LOS_IPC` | YES/NO |  |  |
| `LOS_KEY` | YES/NO |  |  |
| `LOS_LV` | YES/NO |  |  |
| `LOS_MOT` | YES/NO |  |  |
| `LOS_OWC` | YES/NO |  |  |
| `LOS_TCM` | YES/NO |  |  |
| `LowFanCA` | ON/OFF |  |  |
| `LowFanCtrl` | ON/OFF |  |  |
| `LowFPumpFault` | YES/NO |  |  |
| `LPC_AMP` | not available |  |  |
| `LT TRIM B1(%)` | -20 to +20% | LT TRIM B2(%) |  |
| `LT TRIM B2(%)` | -20 to +20% | LT TRIM B1(%) |  |
| `MAF(gm/S)` | 0 to 255 | AIRFLOW(g/S) |  |
| `MAF(V)` | 0 to 5.0 V |  |  |
| `MAF=MASS AIR(V)` | 0.5 to 5.0 V |  |  |
| `MAFF` | YES FAULT/NO FAULT |  |  |
| `MAF_Fault` | YES/NO | MASS AIR FLOW_STATUS |  |
| `MAF_RATE` | not available |  |  |
| `MAN LEV POS` | see description |  |  |
| `MAN LEV POS(V)` | see description |  |  |
| `MAN VAC` | 0 to 35 “HG |  |  |
| `MAP(Hz)` | see description |  |  |
| `MAP(psi)` | 0 to 2368 psi |  |  |
| `MAP2` | not available |  |  |
| `MAP_DI2` | not available |  |  |
| `MAP_F` | YES FAULT/NO FAULT |  |  |
| `MAP_PCM` | not available |  | ABS |
| `MAP_V` | not available |  |  |
| `MASS AIR FLOW_STATUS` | YES/NO | MAF_Fault |  |
| `MASS AIR(gm/sec)` | 0 to 255 |  |  |
| `MASTERKEY` | PRESENT/NOT PRESENT |  |  |
| `MAT` | not available |  |  |
| `MECP_ENA` | ON/OFF |  |  |
| `MECP_F` | YES FAULT/NO FAULT |  |  |
| `MECT_V(V)` | not available |  |  |
| `MFC` | ON/OFF |  |  |
| `MFC_FLT` | YES FAULT/NO FAULT |  |  |
| `MFDES` | 0 to 255 mG |  |  |
| `MGP` | -10 to 30 psi) |  |  |
| `MIL` | ON/OFF |  |  |
| `MILFault` | YES/NO |  |  |
| `MIL_FLT` | YES FAULT/NO FAULT |  |  |
| `MIN_KEY` | not available |  |  |
| `MISFEVAL_DC_MODE1` | YES/NO |  |  |
| `MISFire` | YES/NO |  |  |
| `MISF_EVAL` | YES/NO |  |  |
| `MPLRN` | YES/NO |  |  |
| `MTQ_OUT_ENG` | not available |  |  |
| `M_DPFE` | not available |  |  |
| `M_IAT` | not available |  |  |
| `M_LOAD(%)` | not available |  |  |
| `M_PNP` | not available |  |  |
| `M_RPM` | not available |  |  |
| `M_RUN` | not available |  |  |
| `M_SDN` | YES/NO |  |  |
| `M_SOAK` | not available |  |  |
| `M_TP` | not available |  |  |
| `M_TRIP` | not available |  |  |
| `M_VSS` | not available |  |  |
| `M_WHEEL` | not available |  |  |
| `NM` | not available |  |  |
| `NUMKEYS` | not available |  | PATS |
| `N_KEYCODE` | not available |  |  |
| `O2 B1-S1(V)` | 0 to 1.800 V | O2 B1-S2(V), O2 B2-S1(V), O2 B2-S2(V) |  |
| `O2 B1-S2(V)` | 0 to 1.800 V | O2 B1-S1(V), O2 B2-S1(V), O2 B2-S2(V) |  |
| `O2 B2-S1(V)` | 0 to 1.800 V | O2 B1-S1(V), O2 B1-S2(V), O2 B2-S2(V) |  |
| `O2 B2-S2(V)` | 0 to 1.800 V | O2 B1-S1(V), O2 B1-S2(V), O2 B2-S1(V) |  |
| `O2OEVAP` | YES/NO |  |  |
| `O2OTEST` | YES/NO |  |  |
| `O2S1(mV)` | 0 to 1800 | O2S2(mV), O2S11(mV), O2S12(mV), O2S21(mV), O2S22(mV) |  |
| `O2S11(mV)` | 0 to 1800 | O2S1(mV), O2S2(mV), O2S12(mV), O2S21(mV), O2S22(mV) |  |
| `O2S11FM` | YES FAULT/NO FAULT |  |  |
| `O2S12(mV)` | 0 to 1800 | O2S1(mV), O2S2(mV), O2S11(mV), O2S21(mV), O2S22(mV) |  |
| `O2S12(V)` | not available |  |  |
| `O2S13(V)` | not available |  |  |
| `O2S2(mV)` | 0 to 1800 | O2S1(mV), O2S11(mV), O2S12(mV), O2S21(mV), O2S22(mV) |  |
| `O2S21(mV)` | 0 to 1800 | O2S1(mV), O2S2(mV), O2S11(mV), O2S12(mV), O2S22(mV) |  |
| `O2S21(V)` | LEAN/RICH |  |  |
| `O2S21FM` | YES FAULT/NO FAULT |  |  |
| `O2S22(mV)` | 0 to 1800 | O2S1(mV), O2S2(mV), O2S11(mV), O2S12(mV), O2S21(mV) |  |
| `O2S22(V)` | LEAN/RICH |  |  |
| `O2S23(V)` | not available |  |  |
| `O2SEVAL_DC_MODE1` | YES/NO |  |  |
| `OCT ADJ` | YES/NO | OCTADJ |  |
| `OCTADJ` | YES/NO | OCT ADJ |  |
| `OCTANE` | not available |  |  |
| `OCTANE ADJ` | ON/OFF |  |  |
| `ODS(rpm)` | not available |  |  |
| `OFMFLG` | YES FAULT/NO FAULT |  | TCM |
| `OPEN/CLSD LOOP` | OPEN/CLSD) | LOOP |  |
| `OSFMFLG` | YES FAULT/NO FAULT |  |  |
| `OSS(rpm)` | not available |  |  |
| `OSS=TSS(RPM)` | 0 to vehicle max |  |  |
| `OSS_DIR` | FORWARD/REVERSE |  | TCM |
| `OSS_F` | YES FAULT/NO FAULT |  | TCM |
| `OSS_FAULT` | YES/NO |  |  |
| `OTEMP_FMFLG` | YES FAULT/NO FAULT |  | TCM |
| `OUTCODE_BIT` | not available | OUT_CODE |  |
| `OutShftSp(RPM)` |  |  |  |
| `OUT_CODE` | not available | OUTCODE_BIT |  |
| `OverdriveCancel` | NOT DEPRESSED/DEPRESSED |  | TCM |
| `P/S PRESS(HIGH)` | YES/NO | PwrStrPrs |  |
| `PARK/NEU POS` | P-N—/-R-DS | ParkNeuPos |  |
| `ParkBrakeApply` | YES/NO |  |  |
| `ParkNeuPos` | P-N—/-R-DS | PARK/NEU POS |  |
| `PARK_INPT` | PARK/NOT PARK |  |  |
| `PATSENABL` | ENABLE/DISABLE |  |  |
| `PATS_TYPE` | TIMED/CODED |  |  |
| `PBA` | ON/OFF |  |  |
| `PCA` | not available |  | TCM |
| `PCAA` | not available |  | TCM |
| `PCA_FAULT` | YES/NO | PCD_FAULT, PCE_FAULT, PCF_FAULT, PCG_FAULT |  |
| `PCA_FLT2` | NO FAULT/FAULT |  |  |
| `PCB` | not available |  | TCM |
| `PCBA` | not available |  | TCM |
| `PCBFLT` | NO FAULT/FAULT |  |  |
| `PCC` | not available |  | TCM |
| `PCCA` | not available |  | TCM |
| `PCC_FLT` | NO FAULT/FAULT |  |  |
| `PCD` | not available |  | TCM |
| `PCD_AMP` | not available |  | TCM |
| `PCD_FAULT` | YES/NO | PCA_FAULT, PCE_FAULT, PCF_FAULT, PCG_FAULT |  |
| `PCD_FLT` | NO FAULT/FAULT |  | TCM |
| `PCE` | not available |  | TCM |
| `PCE_FAULT` | YES/NO | PCA_FAULT, PCD_FAULT, PCF_FAULT, PCG_FAULT |  |
| `PCE_FLT` | NO FAULT/FAULT |  |  |
| `PCF` | not available |  | TCM |
| `PCF_FAULT` | YES/NO | PCA_FAULT, PCD_FAULT, PCE_FAULT, PCG_FAULT |  |
| `PCF_FLT` | NO FAULT/FAULT |  | TCM |
| `PCG` | not available |  | TCM |
| `PCG_FAULT` | YES/NO | PCA_FAULT, PCD_FAULT, PCE_FAULT, PCF_FAULT |  |
| `PCG_FLT` | NO FAULT/FAULT |  | TCM |
| `PCLowOSM` | ON/OFF |  |  |
| `PERDEL1(%)` | not available |  |  |
| `PERDEL2(%)` | not available |  |  |
| `PERDEL3(%)` | not available |  |  |
| `PERDEL4(%)` | not available |  |  |
| `PERDEL5(%)` | not available |  |  |
| `PERDEL6(%)` | not available |  |  |
| `PERDEL7(%)` | not available |  |  |
| `PERDEL8(%)` | not available |  |  |
| `PEVAP_TMP(°C/°F)(V)` | not available |  |  |
| `PFE(V)` | 0.25 to 4.75 V |  |  |
| `PinInputLevel` | ON/OFF |  |  |
| `PIP` | YES/NO |  |  |
| `PNP` | NEUTRAL/DRIVE |  |  |
| `PNP_ONLY` | NEUTRAL/DRIVE |  |  |
| `PS1` | OPEN/CLOSED |  |  |
| `PSP` | HIGH/LOW |  |  |
| `PSP(KPA/PSI)` | not available | PSP(V) |  |
| `PSP(V)` | not available | PSP(KPA/PSI) |  |
| `PSR` | ON/OFF |  |  |
| `PTO` | ON/OFF |  |  |
| `PwrStrPrs` | YES/NO | P/S PRESS(HIGH) |  |
| `PwTakeOff` | ON/OFF |  |  |
| `RCAM` | -15° to +45° |  |  |
| `RCL_F` | YES FAULT/NO FAULT |  | TCM |
| `RCL_FAULT` | YES/NO |  |  |
| `RESUME` | ON/OFF |  |  |
| `REV_SW` | ON/OFF |  |  |
| `RPM` | 0 to engine max |  |  |
| `RPMDES` | 0 to engine max |  |  |
| `SBB_SS2` | ON/OFF |  |  |
| `SC VAC SW` | ON/OFF | SC VENT SW |  |
| `SC VENT SW` | ON/OFF | SC VAC SW |  |
| `SCBC` | ON/OFF |  |  |
| `SCBCF` | YES FAULT/NO FAULT |  |  |
| `SCCS` | see description |  |  |
| `SCCS(V)` | 0 to 10.00 |  |  |
| `SCCS_CANCEL` | ON/OFF |  |  |
| `SCCS_NULL` | ON/OFF |  |  |
| `SCCS_OFF_PID` | ON/OFF |  |  |
| `SCCS_ON_PID` | ON/OFF |  |  |
| `SCCS_RESUME` | ON/OFF |  |  |
| `SCCS_TAP_DN` | ON/OFF |  |  |
| `SCCS_TAP_UP` | ON/OFF |  |  |
| `SCC_AMP` | not available |  |  |
| `SCICP` | ON/OFF |  |  |
| `SCICPF` | YES FAULT/NO FAULT |  |  |
| `SCP_PARK_BRAKE` | ON/OFF |  |  |
| `SC_OFF` | ON/OFF |  |  |
| `SC_ON` | ON/OFF |  |  |
| `SecAIR mon rdy` | YES/NO |  |  |
| `SEC_ACCES` | Denied/In Progress/GrantED |  |  |
| `SERV_MOD` | YES/NO |  |  |
| `SET_ACL` | ON/OFF |  |  |
| `SFT1` | not available |  |  |
| `SFT2` | not available |  |  |
| `SFTRIM 1(%)` | -25% to +35% | SFTRIM 2(%) |  |
| `SFTRIM 2(%)` | -25% to +35% | SFTRIM 1(%) |  |
| `SHFT_TYP` | not available |  | TCM |
| `ShiftSol1` | ON/OFF | ShiftSol2, ShiftSol3 |  |
| `ShiftSol1A` | ON/OFF | ShiftSol2A, ShiftSol3A |  |
| `ShiftSol1Fault` | YES/NO | ShiftSol2Fault, ShiftSol3Fault |  |
| `ShiftSol2` | ON/OFF | ShiftSol1, ShiftSol3 |  |
| `ShiftSol2A` | ON/OFF | ShiftSol1A, ShiftSol3A |  |
| `ShiftSol2Fault` | YES/NO | ShiftSol1Fault, ShiftSol3Fault |  |
| `ShiftSol3` | ON/OFF | ShiftSol1, ShiftSol2 |  |
| `ShiftSol3A` | ON/OFF | ShiftSol1A, ShiftSol2A |  |
| `ShiftSol3Fault` | YES/NO | ShiftSol1Fault, ShiftSol2Fault |  |
| `SHRTFT1(%)` | not available |  |  |
| `SHRTFT2(%)` | not available |  |  |
| `SLIP_DES_SCP` | not available |  | TCM |
| `SPAREKEY_PCM` | ENABLED/DISABLED |  |  |
| `SPARK ADV(°)` | -90° to +90° | SPRKADV(°), IGN ADVANCE(°) |  |
| `SPARKADV(°)` | -54 to +54 degrees |  |  |
| `SPARKDUR1` | not available | SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |
| `SPARKDUR2` | not available | SPARKDUR1, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |
| `SPARKDUR3` | not available | SPARKDUR1, SPARKDUR2, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |
| `SPARKDUR4` | not available | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |
| `SPARKDUR5` | not available | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |
| `SPARKDUR6` | not available | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR7, SPARKDUR8 |  |
| `SPARKDUR7` | not available | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR8 |  |
| `SPARKDUR8` | not available | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7 |  |
| `SPARK_ACT` | not available |  |  |
| `SPRKADV(°)` | -90° to +90° | SPARK ADV(°), IGN ADVANCE(°) |  |
| `SRC_CAN` | ENABLE/DISABLE |  |  |
| `SSA_AMP` | not available |  | TCM |
| `SSA_SS1` | ON/OFF |  |  |
| `SSA_SS1_FLT` | YES FAULT/NO FAULT |  |  |
| `SSB_AMP` | not available |  | TCM |
| `SSB_SS2_FLT` | YES FAULT/NO FAULT |  |  |
| `SSC(%)` | not available |  |  |
| `SSC_SS3` | ON/OFF |  |  |
| `SSC_SS3_FLT` | _YES FAULT/NO FAULT |  |  |
| `SSD(%)` | not available |  |  |
| `SSD_SS4` | ON/OFF |  |  |
| `SSE(%)` | not available |  |  |
| `SSPCA` | not available |  | TCM |
| `SSPCA_FLT` | NO FAULT/FAULT |  | TCM |
| `SSPCB` | not available |  | TCM |
| `SSPCB_FLT` | YES FAULT/NO FAULT |  | TCM |
| `SSPCC` | not available |  | TCM |
| `SSPCC_FLT` | YES FAULT/NO FAULT |  | TCM |
| `ST TRIM B1(%)` | -100% to +100% | ST TRIM B2(%) |  |
| `ST TRIM B2(%)` | -100% to +100% | ST TRIM B1(%) |  |
| `STATUS B1` | see description |  |  |
| `STE=EVP(V)` | 0.30 to 4.50 V | EVP(V) |  |
| `SYNC_DI` | YES/NO |  |  |
| `TCC` | Engaged/Modulated/OFF |  | TCM |
| `TCC SOL` | ON/OFF |  |  |
| `TCC SOL(%)` | 0 to 100% |  |  |
| `TCC(%)` | 0 to 100% |  |  |
| `TCCA` | ON/OFF |  |  |
| `TCCA(V)` | not available |  |  |
| `TCCF` | YES FAULT/NO FAULT |  |  |
| `TCCFault` | YES/NO |  |  |
| `tccmact (RPM)` | 0 to vehicle max | TCCMACT(RPM) |  |
| `TCCMACT(RPM)` | 0 to vehicle max | tccmact (RPM) |  |
| `TCC_FLT` | YES FAULT/NO FAULT |  | TCM |
| `TCC_OSC` | not available |  | TCM |
| `TCIL` | ON/OFF |  | TCM |
| `TCILFault` | YES/NO | TCIL_FAULT, TCIL_FLT |  |
| `TCIL_FAULT` | YES/NO | TCILFault, TCIL_FLT |  |
| `TCIL_FLT` | YES/NO | TCILFault, TCIL_FAULT | TCM |
| `TCS` | DEPRESSED/NOT DEPRESSED |  | TCM |
| `TCSLAMP` | ON/OFF | TrnCtrlSw |  |
| `TC_SLIP` | not available |  | TCM |
| `TFT(V)(°C/°F)` | not available |  |  |
| `TFT=TOT(V)` | 0 to 5.10 V |  |  |
| `THEFT_LMP` | YES FAULT/NO FAULT |  |  |
| `ThrPosMODE` | see description | TP MODE |  |
| `THTRC(%)` | not available |  |  |
| `THTRCF` | YES FAULT/NO FAULT |  |  |
| `TIRESIZE` | not available |  |  |
| `TorqConvCl` | ON/OFF |  |  |
| `TORQUE` | not available |  | TCM |
| `TORQUE(lb-fT)` | 0 to 999 |  |  |
| `TOTF` | YES FAULT/NO FAULT |  | TCM |
| `TOWHAUL` | ON/OFF |  | TCM |
| `TP MODE` | PT/WOT/CT |  |  |
| `TP MODE` | see description | ThrPosMODE |  |
| `TP(V)` | 0.0 to 5.0 V |  |  |
| `TP1` | not available |  |  |
| `TP2` | not available |  |  |
| `TP=TPS(%)` | (range 0 to 100% |  |  |
| `TP=TPS(V)` | 0 to 5.1 V |  |  |
| `TPCT(V)` | not available | TPREL(V) |  |
| `TPF` | YES/NO |  |  |
| `TPREL(V)` | not available | TPCT(V) |  |
| `TP_FAULT` | YES FAULT/NO FAULT |  |  |
| `TQ_CNTL` | not available |  | TCM |
| `TR` | Man1/Man2/D/OD/N/R/P |  | TCM |
| `TR1` | OPEN/CLOSED | TR2, TR3, TR4 |  |
| `TR2` | OPEN/CLOSED | TR1, TR3, TR4 |  |
| `TR3` | OPEN/CLOSED | TR1, TR2, TR4 |  |
| `TR3_MODE` | OPEN/CLOSED |  |  |
| `TR4` | OPEN/CLOSED | TR1, TR2, TR3 |  |
| `TR=GEAR` | see description |  |  |
| `TRAC_ACT` | ON/OFF |  |  |
| `TRANSMIT` | ON/OFF |  |  |
| `TransRange_D` | see description |  |  |
| `TRAN_OT_FAULT` | YES/NO |  |  |
| `TRFM` | YES FAULT/NO FAULT |  | TCM |
| `TRIM B1-S1(%)` | -55% to +55% | TRIM B2-S1(%) |  |
| `TRIM B2-S1(%)` | -55% to +55% | TRIM B1-S1(%) |  |
| `TRIP` | YES/NO |  |  |
| `TRIP CounT` | 0 to 255 |  |  |
| `TRIP_CNT` | not available |  |  |
| `TrnCtrlIndLamp` | ON/OFF |  |  |
| `TrnCtrlSw` | ON/OFF | TCSLAMP |  |
| `TrnFluidTmp(V)` | 0.0 to 5.0 V |  |  |
| `TrnRange` | 0.0 to 5.0 V |  |  |
| `TRO_N_F` | YES/NO |  | TCM |
| `TRO_N_FAULT` | YES/NO |  |  |
| `TRO_P_F` | YES/NO |  | TCM |
| `TRS1(V)` | not available |  |  |
| `TRS2(V)` | not available |  |  |
| `TRS3(V)` | not available |  |  |
| `TR_DC(%)` | not available |  |  |
| `TR_FAULT` | YES/NO |  |  |
| `TR_FREQ(Hz)` | not available |  | TCM |
| `TR_V` | see description |  | TCM |
| `TSFMFLG` | YES FAULT/NO FAULT |  | TCM |
| `TSLIPRAT` | not available |  | TCM |
| `TSS(rpm)` | not available |  |  |
| `TSS_F` | YES FAULT/NO FAULT |  | TCM |
| `TSS_FAULT` | YES/NO |  |  |
| `TurbSpdS(RPM)` | 0 to 8192 |  |  |
| `UNLIMITED_KEY` | ENABLED/DISABLED |  |  |
| `VBAT(V)` | not available | VBATT_PCM(V) |  |
| `VBATT_PCM(V)` | not available | VBAT(V) |  |
| `VCT 1_FAULT` | YES/NO | VCT 2_FAULT |  |
| `VCT 2_FAULT` | YES/NO | VCT 1_FAULT |  |
| `VCT1` | 7.5 to B+ | VCT2 |  |
| `VCT1F` | YES FAULT/NO FAULT |  |  |
| `VCT2` | 7.5 to B+ | VCT1 |  |
| `VCT2F` | YES FAULT/NO FAULT |  |  |
| `VCTA` | ON/OFF |  |  |
| `VCTADV` | not available |  |  |
| `VCTADV2` | not available |  |  |
| `VCTADVERR` | not available |  |  |
| `VCTADVERR2` | not available |  |  |
| `VCTDC(%)` | not available |  |  |
| `VCTDC2(%)` | not available |  |  |
| `VCTSYS` | OPEN/CLOSED LOOP |  |  |
| `VehSpdSensr` | 0 to 255 |  |  |
| `VFC(%)` | not available |  |  |
| `VFCDC(%)` | not available |  |  |
| `VFCF` | YES FAULT/NO FAULT |  |  |
| `VFDES` | 0 to 255 mm3 |  |  |
| `VGTDC(%)` | not available |  |  |
| `VGT_F` | YES FAULT/NOFAULT |  |  |
| `VGT_FAULT` | YES/NO |  |  |
| `VPoWeR` | 0.0 to 24.0 V |  |  |
| `VPoWeR(V)` | 0 to 25.5 V | VPWR=BATT(V) |  |
| `VPWR(V)` | not available |  |  |
| `VPWR=BATT(V)` | 0 to 25.5 V | VPoWeR(V) |  |
| `VREF(V)` | 0 to 5.12 V |  |  |
| `VS SET` | see description |  |  |
| `VSFMFLG` | YES FAULT/NO FAULT |  | TCM |
| `VSS(KPH)` | 0 to vehicle max | VSS(MPH) |  |
| `VSS(MPH)` | 0 to vehicle max | VSS(KPH) |  |
| `VSS_FAULT` | YES FAULT/NO FAULT |  |  |
| `VS_SRC` | not available |  | TCM |
| `V_4X4L` | PRESENT/NOT PRESENT |  |  |
| `V_OCTADJ` | ACTIVE/INACTIVE |  |  |
| `V_TCASE` | PRESENT/NOT PRESENT |  |  |
| `WAC` | ON/OFF |  |  |
| `WAC=WOT A/C` | ON/OFF |  |  |
| `WACA` | ON/OFF |  |  |
| `WACFault` | YES/NO |  |  |
| `WAC_FLT` | YES FAULT/NO FAULT |  |  |
| `WARM_NOMIL` | 0 to vehicle max |  |  |
| `WASTEGATE` | 0 to 100% |  |  |
| `WFS` | YES/NO |  |  |
| `WotA/Ccutoff` | ON/OFF |  |  |
