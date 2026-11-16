# Ford data parameters: Passive Antitheft System (PATS)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

55 parameters. Index: [README.md](README.md).

| Parameter | Range | Description | Shares entry with | Also under | Notes |
| --- | --- | --- | --- | --- | --- |
| `AntiScan` | ON/OFF | Indicates if PATS has activated the Antiscan function. The PATS enables Antiscan after an unprogrammed PATS key is used to attempt to start the engine. Antiscan then temporarily locks out all keys from starting the vehicle and flashes a dash-mounted LED or the Theft Lamp indicator. |  |  |  |
| `C KEYMD` | ACTIVE/NOTACTIVE | This parameter should read ACTIVE when PATS has been requested to clear all keys. |  |  |  |
| `ENABLE S` | DISABLE/ENABLE | Information on this “VEHICLE ENABLE STATUS” parameter is not available at this time. It appears to be an output from PATS indicating that the engine will start or is ENABLED. |  |  |  |
| `FAILSAF` | ON/OFF | Information on this “Failsafe Mode” parameter is not available at this time. |  |  |  |
| `M KEY` | NOTPRE/PRESENT | Indicate whether a Master Key is present in the ignition. Master keys are required to program in new spare keys. Depending upon the type of PATS system, one or two Master keys may be needed to program extra spare keys. | Master KEY |  |  |
| `Master KEY` | NOTPRE/PRESENT | Indicate whether a Master Key is present in the ignition. Master keys are required to program in new spare keys. Depending upon the type of PATS system, one or two Master keys may be needed to program extra spare keys. | M KEY |  |  |
| `NUMKEYS` | 0 to 255 | Number of Master and Spare keys that are programmed into the PATS control module. |  | PCM | Also listed under: PCM. |
| `PCM ID` | STORED/NOTSTORED | Indicates whether the PATS has learned and stored the unique identification (ID) of the powertrain control module (PCM). |  |  |  |
| `PCM VFY` | YES/NO | Indicates whether PATS has verified that the PCM ID matches the ID stored from module initialization. |  |  |  |
| `RR_WSPD(KPH/MPH)` | not available | This parameter indicates the speed of the right rear wheel. |  |  |  |
| `SAS_CAL` | TRUE/FALSE | Indicates the status of the steering angle sensor initialization start. |  |  |  |
| `SECND_VLV` | ON/OFF | Indicates the secondary low pressure feed valve. |  |  |  |
| `SECPR_TRN` | PASS/FAIS | Indicates the status of the booster secondary transducer. |  |  |  |
| `SELTESTDTC` | not available | Indicates the DTC count. Count includes those that needing no action. |  | 4x4 | Also listed under: 4x4. |
| `SENVBAT(V)` | not available | Indicates the IVD/DSC sensors supply voltage. |  |  | Closing parenthesis restored in name. |
| `SEN_CAL` | not available | Indicates the status of the pressure sensor calibration. |  |  |  |
| `SPAREKY` | DISABLE/ENABLE | Indicates whether spare key programming is enabled in the PATS module. Spare key programming may not be desired and therefore can be disabled or enabled on certain models. |  |  |  |
| `STEER_ANGL` | not available | Indicates the steering wheel angle sensor. |  |  |  |
| `ST_WH_ANG` | not available | Indicates the ABS steering wheel angle. |  |  |  |
| `SWA1_CIR` | Open/Sht-B+/Sht-Gnd/Normal | Indicates the steering wheel angle 1 circuit state. |  |  |  |
| `SWA1_INPT` | HIGH/LOW | Indicates the steering wheel angle 1 input state. |  |  |  |
| `SWA2_CIR` | Open/Sht-B+/Sht-Gnd/Normal | Indicates the steering wheel angle 2 circuit state. |  |  |  |
| `SWA2_INPT` | HIGH/LOW | Indicates the steering wheel angle 2 input state. |  |  |  |
| `SWA_CCW` | not available | Indicate an angle in degrees clockwise from start. | SWA_CW |  |  |
| `SWA_CW` | not available | Indicate an angle in degrees clockwise from start. | SWA_CCW |  |  |
| `SWA_LPS` | YES/NO | Shows if the SWA Lock to Lock test is passed. YES indicates passed. |  |  |  |
| `SWA_POS(°)` | not available | This parameter indicates the steering wheel angle in degrees. |  |  |  |
| `SW_TEST` | ON/OFF | Indicates the switch test signal. |  |  |  |
| `TA_LVAL` | ON/OFF | Indicates the status of the traction assist left control valve. |  |  |  |
| `TA_RVAL` | ON/OFF | Indicates the status of the traction assist right control valve. |  |  |  |
| `TCYC_FS` | ACTIVE/PASSIVE or ON/OFF | Indicates the status of the traction control system. |  | 4x4 | Also listed under: 4x4. |
| `TCYC_SW` | NOT DEPRESSED/DEPRESSED or ACTIVATED/NOT ACTIVATED | Indicates the status of the traction control switch. |  |  |  |
| `TC_RVAL` | ON/OFF | Indicates the status of the traction assist right control valve |  |  |  |
| `TDPT_COMP` | YES/NO | States if the travel direction plausibility test is complete. YES indicates complete. |  |  |  |
| `TDPT_PASS` | YES/NO | Indicates if the travel direction plausibility test is complete. YES indicates complete. |  |  |  |
| `TRAC SW` | ON/OFF | Indicates the status of the driver-operated Traction Switch. When ON, TRACtionassist should also display ON. |  |  |  |
| `TRACtionassist` | ON/OFF | Reads ON when the PCM has activated the Traction Assist system. |  |  |  |
| `V ENABLE` | DISABLE/ENABLE | Information on this “VEH ENABLE” parameter is not available at this time. |  |  |  |
| `VALVCAL` | not available | Indicates the status of the valve calibration. |  |  |  |
| `VALVE_CAL` | DONE/NOT DONE | Indicates the calibration status of the Electro-Hydraulic valve. |  |  |  |
| `VSS_FF` | not available | Vehicle speed input to overhead trip computer. |  |  |  |
| `VSS_PCM` | not available | Vehicle speed. |  |  |  |
| `WAKESIG` | not available | Wake-up signal. |  |  |  |
| `XDCR_ACCR(KPA/PSI)` | not available | Brake pressure transducer accumulator signal. |  |  |  |
| `XDCR_LFW(KPA/PSI)` | not available | Brake pressure transducer left front wheel signal. |  |  |  |
| `XDCR_LRW(KPA/PSI)` | not available | Brake pressure transducer left rear wheel signal. |  |  |  |
| `XDCR_RFW(KPA/PSI)` | not available | Brake pressure transducer right front wheel signal. |  |  |  |
| `XDCR_RRW(KPA/PSI)` | not available | Brake pressure transducer right rear wheel signal. |  |  |  |
| `XDCR_TMC(KPA/PSI)` | not available | Brake pressure transducer tandem main cylinder signal. |  |  |  |
| `XDUCERCAL` | ENERGIZED/DE-ENERGIZED | Status of the pressure transducer. |  |  |  |
| `YAWRATE` | ON/OFF | Yaw rate sensor initialization start. |  |  |  |
| `YAWRTSEN(°)` | not available | ABS yaw rate value. |  |  |  |
| `YAW_INPUT` | not available | Status of the yaw rate sensor input circuit. |  |  |  |
| `YAW_RATE(°)` | not available | ABS yaw rate value in degrees. |  |  |  |
| `YR_ICS` | YES/NO | Complete status of yaw rate initialization. YES indicates complete. |  |  |  |
