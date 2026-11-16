# Ford data parameters: Transmission Control Module (TCM)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

179 parameters. Index: [README.md](README.md).

| Parameter | Range | Description | Shares entry with | Also under | Notes |
| --- | --- | --- | --- | --- | --- |
| `ABS_ACTIVE` | NORMAL/ACTIVE | Antilock braking system. |  | 4x4 | Also listed under: 4x4. |
| `ABS_MSG_TCM` | PRESENT/NOT PRESENT | CAN message is missing from the ABS. |  |  |  |
| `ACCLUTCH_TCM` | ON/OFF | Status of the air conditioning clutch. |  |  |  |
| `APP1_FN_TCM` | not available | Accelerator pedal position sensor 1. |  |  |  |
| `APP1_TCM` | not available | Accelerator pedal position sensor 1. |  |  |  |
| `APP2_TCM` | not available | Accelerator pedal position sensor 2. |  |  |  |
| `APP3_TCM` | not available | Accelerator pedal position sensor 3. |  |  |  |
| `AP_PER_TCM` | not available | Accelerator pedal position 1 in percentage. |  |  |  |
| `BARO_TCM` | not available | Barometric pressure. |  |  |  |
| `BATT_I_REC` | not available | Battery current received. |  |  |  |
| `BATT_V_REC` | not available | Battery voltage received from the transmission control module. |  |  |  |
| `BOO_TCM` | ON/OFF | Brake switch input status. |  |  |  |
| `BRKPEDSW` | ACTIVE/INACTIVE | Status of the brake pedal switch. |  |  |  |
| `CCNT_TCM` | not available | Indicates continuous codes. |  |  |  |
| `CLTCHAMP_CMD` | not available | Commanded current for the clutch actuator. |  |  |  |
| `CONTRACTOR` | OPEN/CLOSED | Contractor status. |  |  |  |
| `CPC_AMP` | not available | Converter pressure control. |  | PCM | Also listed under: PCM. |
| `DISTDIG_TCM` | not available | Distance since the last time diagnostic trouble codes were cleared. |  |  |  |
| `DRIVECNT_TCM` | not available | Valid drive counter. |  |  |  |
| `DRIVEPOS_TCM` | YES/NO | Gear level position - drive. |  |  |  |
| `DTC_CNT_TCM` | not available | DTC count (includes those needing no action |  |  | Description text may be missing a closing parenthesis. |
| `ECT_TCM (°C/°F)` | not available | (ECT transmission control module temperature. |  |  | Description text may be missing a closing parenthesis. |
| `ECT_TCM_DI (°C/°F)` | not available | Engine coolant temperature. |  |  |  |
| `ENGINE_STAT` | YES/NO | Engine running status. |  |  |  |
| `ENG_CTO` | not available | Engine speed clean tachometer output. |  |  |  |
| `ENG_TORQ` | not available | Engine torque. |  |  |  |
| `FIRMSHFT` | ON/OFF | Firm shift status. |  | PCM | Also listed under: PCM. |
| `FLG_MLUSFM` | YES FAULT/NO FAULT | Torque converter had unlocked due to slipping. |  | PCM | Also listed under: PCM. |
| `FLG_OTLK` | YES FAULT/NO FAULT | Transmission over temperature lockup mode. |  | PCM | Also listed under: PCM. |
| `FRZ_DTC` | not available | Freeze frame diagnostic trouble code. |  |  |  |
| `FUEL_CUT` | not available | Fuel cut-off signal. |  |  |  |
| `Gear` | not available | Commanded by module. |  |  |  |
| `GEAR_MAX` | not available | Highest gear allowed. |  | PCM | Also listed under: PCM. |
| `Gear_OSCIP` | not available | Gear command by output state control. |  |  |  |
| `GEAR_RAT` | not available | Gear ratio. |  | PCM | Also listed under: PCM. |
| `GEN_BRK_CMD` | ON/OFF | Generator brake command. |  |  |  |
| `GEN_COILTEMP (°C)(°F)` | not available | Generator coil temperature. |  |  |  |
| `GEN_CTRL` | not available | Generator control mode. |  |  |  |
| `GEN_INV_V` | not available | Voltage of the generator inverter. |  |  |  |
| `GEN_SPEED` | not available | Generator speed. |  |  |  |
| `GEN_TEMPHI (°C)(°F)` | not available | Generator inverter phase temperature. |  |  |  |
| `GEN_TORQ_CMD` | not available | Generator torque command. |  |  |  |
| `GR_RATIO` | not available | Transmission gear ratio. |  | PCM | Also listed under: PCM. |
| `GTQ_OUT` | not available | Generator torque from AC Source. |  |  |  |
| `G_SDN_A` | not available | Generator shutdown from e-quizzer. |  |  |  |
| `G_SDN_B` | not available | Generator shutdown for vehicle system control. |  |  |  |
| `G_SDN_C` | not available | Generator shutdown from the powertrain control module. |  |  |  |
| `IAT_TCM (°C)(°F)` | not available | Intake air temperature. |  |  |  |
| `IGNITION` | not available | Indicates the ignition. |  |  |  |
| `IMAN_DN_TCM` | OPEN/CLOSED | Gear lever position - Tip Minus. |  |  |  |
| `IMIN_UP_TCM` | OPEN/CLOSED | Position of the gear level - Tip Plus. |  |  |  |
| `IMS_SRC` | not available | Immediate shaft speed. |  | PCM | Also listed under: PCM. |
| `ISS` | not available | Input shaft speed. |  |  |  |
| `ISS_DIR` | FORWARD/REVERSE | Intermediate shaft direction. |  | PCM | Also listed under: PCM. |
| `ISS_F` | YES FAULT/NO FAULT | Intermediate shaft speed reliability. |  | PCM | Also listed under: PCM. |
| `I_SDN_1` | not available | Rapid discharge signal 1. |  |  |  |
| `I_SDN_2` | not available | Rapid discharge signal 2. |  |  |  |
| `LCP_AMP` | not available | Line pressure control. |  |  |  |
| `LF_WSPD_TCM` | not available | Left front wheel speed sensor. |  |  |  |
| `LOAD_TCM` | not available | Percentage engine load. |  |  |  |
| `MANSW_TCM` | OPEN/CLOSED | Gear level position - manual. |  |  |  |
| `MECH_TCM (°C/°F)` | not available | Motor electronics coolant temperature. |  |  |  |
| `MIL_DIS_TCM` | not available | Distance traveled since the MIL was activated. |  |  |  |
| `MTQ_OUT` | not available | Motor torque from AC source. |  |  |  |
| `MTR_COILTEMP (°C)(°F)` | not available | Motor coil temperature. |  |  |  |
| `MTR_INV_V` | not available | Motor inverter voltage. |  |  |  |
| `MTR_SPEED` | not available | Motor speed in RPM (Rotations Per Minute). |  |  |  |
| `MTR_TEMPHI (°C)(°F)` | not available | This parameter indicates the motor inverse phase temperature. |  |  |  |
| `MTR_TORQ_CMD` | not available | This parameter indicates the motor torque command. |  |  |  |
| `M_SDN_A` | not available | Motor shutdown from E-quizzer. |  |  |  |
| `M_SDN_B` | not available | Motor shutdown from vehicle system control. |  |  |  |
| `M_SDN_C` | not available | Motor shutdown from powertrain control module. |  |  |  |
| `NEUTPOS_TCM` | YES/NO | Gear level position is neutral. |  |  |  |
| `ODOMETER_TCM` | not available | Indicates the total distance. |  |  |  |
| `OFMFLG` | YES FAULT/NO FAULT | Indicates the pressure control failure mode |  | PCM | Also listed under: PCM. |
| `OSS` | not available | Output shaft speed. |  |  |  |
| `OSS_DIR` | FORWARD/REVERSE | Output shaft direction. |  | PCM | Also listed under: PCM. |
| `OSS_F` | YES FAULT/NO FAULT | Reliability of the output shaft speed. |  | PCM | Also listed under: PCM. |
| `OSS_TCM` | not available | Output shaft speed in RPM. |  |  |  |
| `OTEMP_FMFLG` | YES FAULT/NO FAULT | Transmission over temperature. |  | PCM | Also listed under: PCM. |
| `OverdriveCancel` | NOT DEPRESSED/DEPRESSED | State of the overdrive cancel switch/hold switch. |  | PCM | Also listed under: PCM. |
| `PCA` | not available | Pressure control solenoid A. |  | PCM | Also listed under: PCM. |
| `PCAA` | not available | Pressure control solenoid A. |  | PCM | Also listed under: PCM. |
| `PCAAMP_MES` | not available | Measured current PCA. |  |  |  |
| `PCA_FLT` | NO FAULT/FAULT | Pressure control solenoid A. |  |  |  |
| `PCA_MES` | not available | Measured pressure of the transmission PCA. |  |  |  |
| `PCB` | not available | Pressure control solenoid B. |  | PCM | Also listed under: PCM. |
| `PCBA` | not available | Pressure control solenoid B. |  | PCM | Also listed under: PCM. |
| `PCBAMP_MES` | not available | Measured current for the transmission PCB. |  |  |  |
| `PCC` | not available | Pressure control solenoid C. |  | PCM | Also listed under: PCM. |
| `PCCA` | not available | Pressure control solenoid C. |  | PCM | Also listed under: PCM. |
| `PCCAMP_MES` | not available | Measured current for the PCC. |  |  |  |
| `PCC_MES` | not available | Measured pressure of the PCC. |  |  |  |
| `PCD` | not available | Shift solenoid pressure control D. |  | PCM | Also listed under: PCM. |
| `PCD_AMP` | not available | Shift solenoid pressure control D. |  | PCM | Also listed under: PCM. |
| `PCD_FLT` | YES FAULT/NO FAULT | Indicates the SSPCD status. |  | PCM | Also listed under: PCM. |
| `PCE` | not available | Shift solenoid pressure control E. |  | PCM | Also listed under: PCM. |
| `PCE_AMP` | not available | Shift solenoid pressure control E. |  |  |  |
| `PCF` | not available | Line pressure control |  | PCM | Also listed under: PCM. |
| `PCF_FLT` | YES FAULT/NO FAULT | LPC status. |  | PCM | Also listed under: PCM. |
| `PCG` | not available | Converter pressure control. |  | PCM | Also listed under: PCM. |
| `PCG_FLT` | YES FAULT/NO FAULT | Torque converter clutch status. |  | PCM | Also listed under: PCM. |
| `PCM_MSG_TCM` | PRESENT/NOT PRESENT | CAN message is missing from the PCM. |  |  |  |
| `PVT_WDS` | not available | Pressure vacuum transducer. |  |  |  |
| `RCL_F` | YES FAULT/NO FAULT | Reverse control lamp status. |  | PCM | Also listed under: PCM. |
| `RESPOS_TCM` | YES/NO | Gear level position - Reverse. |  |  |  |
| `RF_WSPD_TCM` | not available | Right front wheel speed sensor. |  |  |  |
| `RPM_DSD_TCM` | not available | Desired idle speed (RPM). |  |  |  |
| `RPM_TCM` | not available | Engine rpm (revolutions per minutE. |  |  | Description text may be missing a closing parenthesis. |
| `RPM_TCM_HEV` | not available | Shift position. |  |  |  |
| `SHFT_TYP` | not available | Shift type. |  | PCM | Also listed under: PCM. |
| `SHIFT_POS` | not available | Status of the transmission control indicator light. |  |  |  |
| `SLIP_DES_SCP` | not available | Desired torque converter slip. |  | PCM | Also listed under: PCM. |
| `SSAFM` | YES/NO | Shift solenoid 1 status. |  |  |  |
| `SSA_AMP` | not available | Shift solenoid pressure control A. |  | PCM | Also listed under: PCM. |
| `SSBFM` | YES/NO | Shift solenoid 2 status. |  |  |  |
| `SSB_AMP` | not available | Shift solenoid pressure control B. |  | PCM | Also listed under: PCM. |
| `SSB_SS1` | ON/OFF | Shift solenoid 1. |  |  |  |
| `SSB_SS2` | ON/OFF | Shift solenoid 2. |  |  |  |
| `SSC_AMP` | not available | Shift solenoid pressure control C. |  |  |  |
| `SSE` | not available | Shift solenoid pressure control E. |  |  |  |
| `SSPCA` | not available | Shift solenoid pressure control A. |  | PCM | Also listed under: PCM. |
| `SSPCA_FLT` | NO FAULT/FAULT | SSPCA status. |  | PCM | Also listed under: PCM. |
| `SSPCB` | not available | Shift solenoid pressure control B. |  | PCM | Also listed under: PCM. |
| `SSPCB_FLT` | NO FAULT/FAULT | SSPCB status. |  | PCM | Also listed under: PCM. |
| `SSPCC` | not available | Shift solenoid pressure control C. |  | PCM | Also listed under: PCM. |
| `SSPCC_FLT` | NO FAULT/FAULT | SSPCC status. |  | PCM | Also listed under: PCM. |
| `TCC` | not available | Torque converter clutch (modulatED. |  | PCM | Description text may be missing a closing parenthesis. Also listed under: PCM. |
| `TCCAMP_MES` | not available | Measured current for the TCC pressure control. |  |  |  |
| `TCC_FLT` | YES FAULT/NO FAULT | Status of the torque converter clutch fault. |  | PCM | Also listed under: PCM. |
| `TCC_OSC` | not available | Output state control of the torque converter. |  | PCM | Also listed under: PCM. |
| `TCC_SLIP_DSD` | not available | Desired torque converter slip. |  |  |  |
| `TCIL` | ON/OFF | Status of the transmission control indicator light. |  | PCM | Also listed under: PCM. |
| `TCIL_FLT` | YES FAULT/NO FAULT | Transmission control indicator light. |  | PCM | Also listed under: PCM. |
| `TCM_CAUTION` | ON/OFF | Status of the transmission control module caution. |  |  |  |
| `TCM_HAZ` | ON/OFF | Transmission control module hazard warning. |  |  |  |
| `TCS` | NOT DEPRESSED/DEPRESSED | Status of the overdrive cancel switch/hold switch. |  | PCM | Also listed under: PCM. |
| `TC_SLIP` | not available | Rpm of the torque converter slip actual. |  | PCM | Also listed under: PCM. |
| `TFT (°C/°F)` | not available | Transmission fluid temperature. |  |  |  |
| `TIP_MIN_TCM` | OPEN/CLOSED | Gear level position - Tip Minus. |  |  |  |
| `TIP_PL_TCM` | OPEN/CLOSED | Gear level position - Tip Plus. |  |  |  |
| `TORQUE` | not available | Net engine torque. |  | PCM | Also listed under: PCM. |
| `TORQUE_DSD` | not available | Desired total torque. | TORQ_DSD |  |  |
| `TORQUE_TCM` | not available | Net engine torque. |  |  |  |
| `TORQ_DSD` | not available | Desired total torque. | TORQUE_DSD |  |  |
| `TOTF` | YES FAULT/NO FAULT | Transmission fluid temperature status. |  | PCM | Also listed under: PCM. |
| `TOT_ENG (°C/°F)` | not available | Transmission fluid temperature. |  |  |  |
| `TOWHAUL` | ON/OFF | Tow haul switch. |  | PCM | Also listed under: PCM. |
| `TP_PER` | not available | Throttle position in percentage. |  |  |  |
| `TP_REL_TCM` | not available | Relative throttle position. |  |  |  |
| `TQ_CNTL` | not available | Torque fuel/spark limiting status. |  | PCM | Also listed under: PCM. |
| `TR` | not available | Indicate the transmission range. | TR_FREQ(Hz), TTR_DC, TR_V | PCM | Also listed under: PCM. |
| `TRAN_TEMP (°C/°F)` | not available | Indicates the transmission oil temperature. |  |  |  |
| `TRFM` | YES FAULT/NO FAULT | Indicates the transmission range status. |  | PCM | Also listed under: PCM. |
| `TRO_N_F` | YES FAULT/NO FAULT | Neutral output status. |  | PCM | Also listed under: PCM. |
| `TRO_P_F` | YES FAULT/NO FAULT | Park output status. |  | PCM | Also listed under: PCM. |
| `TR_FREQ(Hz)` | not available | Indicate the transmission range. | TR, TTR_DC, TR_V | PCM | Also listed under: PCM. |
| `TR_V` | not available | Indicate the transmission range. | TR, TR_FREQ(Hz), TTR_DC | PCM | Also listed under: PCM. |
| `TSES_TCM` | CLOCKTIME | Time since start in seconds. |  |  |  |
| `TSFMFLG` | YES FAULT/NO FAULT | Status of the turbine speed sensor failure mode. | TSFMFLG_TCM | PCM | Also listed under: PCM. |
| `TSFMFLG_TCM` | YES FAULT/NO FAULT | Status of the turbine speed sensor failure mode. | TSFMFLG |  |  |
| `TSLIPRAT` | not available | Transmission slip ratio. |  | PCM | Also listed under: PCM. |
| `TSS` | not available | Turbine shaft speed. |  |  |  |
| `TSS_F` | YES FAULT/NO FAULT | Turbine shaft speed reliability. |  | PCM | Also listed under: PCM. |
| `TSS_TCM` | not available | Turbine shaft speed. |  |  |  |
| `TTR_DC` | not available | Indicate the transmission range. | TR, TR_FREQ(Hz), TR_V |  |  |
| `VAR_CMD` | not available | Commanded variator ratio (input speed/output speed). |  |  |  |
| `VAR_MES` | not available | Measured variator ratio (input speed/output speed). |  |  |  |
| `VEH_CTRL` | not available | Vehicle control mode. |  |  |  |
| `VOLT_SENSR` | not available | Sensor supply voltage. |  |  |  |
| `VPWR` | not available | Module supply voltage. | VPWR_TCM |  |  |
| `VPWR_TCM` | not available | Module supply voltage. | VPWR |  |  |
| `VSFMFLG` | YES FAULT/NO FAULT | Vehicle speed sensor signal status. |  | PCM | Also listed under: PCM. |
| `VSS_F` | YES FAULT/NO FAULT | Vehicle speed sensor reliability. |  |  |  |
| `VSS_TCM` | not available | Vehicle speed. |  |  |  |
| `VSS_TCM_HEV` | not available | Vehicle speed. |  |  |  |
| `VSS_TCM_HR` | not available | Vehicle speed - high resolution. |  |  |  |
| `VS_SRC` | not available | Transfer case sensor. |  | PCM | Also listed under: PCM. |
| `WARMUPDTC_TCM` | not available | Number of warm-ups since the DTCs were cleared. |  |  |  |
