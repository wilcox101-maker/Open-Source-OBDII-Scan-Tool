# Ford data parameters: Antilock Brakes (ABS)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

160 parameters. Index: [README.md](README.md).

| Parameter | Range | Description | Shares entry with | Also under | Notes |
| --- | --- | --- | --- | --- | --- |
| `4WDINP_SW_ABS` | 2WD/4WD) | Status of the 4WD switch status. |  |  |  |
| `ABPROS` | ON/OFF | Antilock braking system power relay output status. |  |  |  |
| `ABS PLS` | ON/OFF | Information on this “ABS MODULATE” parameter is not available at this time. |  |  |  |
| `ABSLAMP` | ON/OFF | PCM command status to the ABS indicator lamp and reads ON when the Lamp is lit. | ABS_LAMP |  |  |
| `ABSLF I` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |  |
| `ABSLF O` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF I, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |  |
| `ABSLR I` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF I, ABSLF O, ABSLR O, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |  |
| `ABSLR O` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF I, ABSLF O, ABSLR I, ABSRF I, ABSRF O, ABSRR I, ABSRR O |  |  |
| `ABSPMPRLY` | ON/OFF | ABS pump motor relay. |  |  |  |
| `ABSR I` | ON/OFF | PCM commanded status to the rear ABS inlet (I) or outlet (O) valves. This parameter is only used in 4WABS systems that use a rear differential speed sensor instead of a sensor at both rear wheels. In an ABS stop this system controls both rear wheels at the same time. | ABSR O |  |  |
| `ABSR O` | ON/OFF | PCM commanded status to the rear ABS inlet (I) or outlet (O) valves. This parameter is only used in 4WABS systems that use a rear differential speed sensor instead of a sensor at both rear wheels. In an ABS stop this system controls both rear wheels at the same time. | ABSR I |  |  |
| `ABSRF I` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF O, ABSRR I, ABSRR O |  |  |
| `ABSRF O` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRR I, ABSRR O |  |  |
| `ABSRR I` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR O |  |  |
| `ABSRR O` | ON/OFF | State of the PCM commands to the ABS inlet (I) and outlet (O) valves. • LF = left front • LR = left rear • RF = right front • RR = right rear | ABSLF I, ABSLF O, ABSLR I, ABSLR O, ABSRF I, ABSRF O, ABSRR I |  |  |
| `ABSVLVRLY` | ON/OFF | Status of the ABS valve control relay. |  |  |  |
| `ABS_LAMP` | ON/OFF | PCM command status to the ABS indicator lamp and reads ON when the Lamp is lit. | ABSLAMP |  |  |
| `ABS_MTR` | ACTIVE/INACTIVE | Status of the ABS pump motor. |  |  |  |
| `ABS_partbaseno` | not available | Module part base number. |  |  |  |
| `ABS_PartNoIdSuffix` | not available | Part version suffix. |  |  |  |
| `ABS_POWER` | ON/OFF | Status of the ABS power relay. |  |  |  |
| `ABS_PWR_R` | ON/OFF | Antilock braking system power relay output status. |  | 4x4 | Also listed under: 4x4. |
| `ABS_SoftVerNo` | not available | Software version number. |  |  |  |
| `ABS_TCS_WIN126_SELTESTDTC` | not available | DTC count. The count includes those needing no action. |  |  |  |
| `ACCLMTR` | not available | Indicates the accelerometer. |  |  |  |
| `ACCR_TEMP (°C)(°F)` | not available | Brake accumulator temperature. |  |  | Closing parenthesis restored in name. |
| `ACCR_TRAV` | ON/OFF | Brake accumulator travel signal. |  |  |  |
| `ACU_CHK` | not available | ACU Bleed Check. |  |  |  |
| `ARCFault` | YES/NO | Indicates whether an automatic ride control DTC has set. |  |  |  |
| `AutoRideCtrl` | ON/OFF | PCM command status to the automatic ride control system. |  |  |  |
| `AWD_MSG` | PRESENT/NOT PRESENT | Indicate if there is CAN communication with the 4x4 module. NOT PRESENT indicates no communication. | AWD_MSG_ABS_MZ |  |  |
| `AWD_MSG_ABS_MZ` | PRESENT/NOT PRESENT | Indicate if there is CAN communication with the 4x4 module. NOT PRESENT indicates no communication. | AWD_MSG |  |  |
| `BALVLV1` | ON/OFF | Indicates the status of balance valve 1. |  |  |  |
| `BALVLV2` | ON/OFF | Indicates the status of balance valve 2. |  |  |  |
| `BLD_STAT` | DONE/NOT DONE | Indicates the status of the brake air bleed check. |  |  |  |
| `BOO ABS` | ON/OFF | Brake pedal switch status and reads as follows: • OFF when the brakes are not applied • ON when the brakes are applied This parameter should read the same as “BOO=BRAKE SW”. |  |  |  |
| `BPTSCAL` | not available | Indicates the offset calibration. |  |  |  |
| `BP_CALST` | DONE/NOT DONE | Indicates the status of the brake pedal travel sensor calibration. |  |  |  |
| `BrakePrsApply` | 0 or 12 V | PCM command status to the brake pressure applied switch and reads as follows: • 0 V when the switch is on • 12 V when the switch is off |  |  |  |
| `BRAKE_LMP` | ON/OFF | Brake warning lamp. |  |  |  |
| `BRAKPRES(KPA/PSI)` | not available | ABS main brake pressure. |  |  |  |
| `BRKLAMP` | ON/OFF | PCM command status to the brake warning indicator lamp on the instrument panel and reads ON when the Lamp is lit. |  |  |  |
| `BRK_CHK` | not available | Brake bleed check. |  |  |  |
| `BRK_FLUID` | LOW/OK | Brake fluid level. |  |  |  |
| `BRK_MSIG(KPA/PSI)` | not available | ABS main brake pressure. |  |  |  |
| `BRK_RSIG` | PRESSURE | ABS redundant brake pressure. |  |  |  |
| `BRK_WARN` | ON/OFF | State of the brake warning lamp. |  |  |  |
| `BSTLRNCYC` | ON/OFF | Status of the booster learn cycle. |  |  |  |
| `BSTRBP_FSNC` | OPEN/CLOSED | Booster brake pedal force switch. The switch is normally closed. |  |  |  |
| `BSTRBP_FSNO` | OPEN/CLOSED | Booster brake pedal force switch. The switch is normally open. |  |  |  |
| `BSTR_BSWF` | YES/NO | Booster solenoid brake switch failure. |  |  |  |
| `BSTR_LCCS` | YES/NO | Booster learn cycle is complete. YES indicates complete. |  |  |  |
| `BSTR_LERN` | PASS/FAIS | Booster self check/calibration status. |  |  |  |
| `BSTR_MECH` | PASS/FAIS | Booster mechanical failure status. |  |  |  |
| `BSTR_SOL` | ON/OFF | Interactive vehicle dynamics booster solenoid status. |  |  |  |
| `BSTR_SOL1` | not available | Booster solenoid output state. |  |  |  |
| `BSTR_SW_T` | ON/OFF | Booster switch test signal output state. |  |  |  |
| `CCB_LCCS` | YES/NO | Indicates if the current cycle booster learn cycle is complete. YES indicates complete. |  |  |  |
| `CCB_LCTF` | YES/NO | Indicates if the current cycle booster learn cycle test has failed. Yes indicated failed. |  |  |  |
| `CCLAS_ICS` | YES/NO | Indicates the if the current cycle lateral acceleration sensor initialization test is complete. YES indicates complete. |  |  |  |
| `CCLAS_ITF` | YES/NO | Indicates if the current cycle lateral acceleration sensor initialization test has failed. YES indicates failed. |  |  |  |
| `CCNTABS` | not available | Indicates the total number of continuous (soft) DTCs stored in the ABS control module. |  |  |  |
| `CCSWA_TPS` | YES/NO | Indicates if the current cycle steering wheel angle lock to lock test passed. YES indicates passed. |  |  |  |
| `CCYR_ICS` | YES/NO | Indicates if current cycle yaw rate initialization is complete. |  |  |  |
| `CCYR_ITF` | YES/NO | Indicates if the current cycle yaw rate initialization test is complete. YES indicates complete. |  |  |  |
| `CONPROV` | not available | Indicates the configuration and programming version. |  |  |  |
| `CUTVLV1` | ON/OFF | Indicates the status of cut valve 1. |  |  |  |
| `CUTVLV2` | ON/OFF | Indicates cut valve 2. |  |  |  |
| `DSBL_TOG` | ON/OFF | Indicates the state disable valve toggle. |  |  |  |
| `DYNOMODE` | YES/NO | Indicates the state of the brake two-wheel dynomode. |  |  |  |
| `EVACFILL` | DONE/NOT DONE | Indicates the fill status of the Electro-Hydraulic EVAC. |  |  |  |
| `G_INPUT` | not available | Indicates the accelerometer. |  |  |  |
| `G_SENSOR` | ON/OFF | Indicates the longitudinal acceleration sensor initialization start. |  |  |  |
| `IC_MSG` | PRESENT/NOT PRESENT | Indicate if the CAN message is missing from the IC. NOT PRESENT indicates missing. |  |  |  |
| `IC_MSG_ABS_MZ` |  |  |  |  | Range not specified. No description available. |
| `IGNITION(V)` | VOLTS | Indicates the status of the ignition. |  | 4x4 | Also listed under: 4x4. |
| `IVD_R_IN` | ON/OFF | Status of the rear-only dump valve. |  |  |  |
| `IVD_R_OUT` | ON/OFF | Status of the rear-only isolation valve. |  |  |  |
| `LAS_ICS` | YES/NO | Status of the EEPROM - lateral acceleration sensor initialization. YES indicates complete. |  |  |  |
| `LATACCEL` | TRUE/FALSE | State of the lateral accel sensor initialization start. |  |  |  |
| `LATACCL` | not available | ABS lateral acceleration rate. |  |  |  |
| `LATACINPT` | not available | Status of the lateral accelerometer input. |  |  |  |
| `LF WSPD(MPH)` | 0 to 100+ MPH/KPH | Display the speed signals being sent by each front wheel speed sensor (LF/RF, each rear wheel speed sensors (LR or RR), or the single rear wheel speed sensor (R) to the electronic control module (ECM). Wheel speeds should be equal to each other and to vehicle speed as the vehicle is driven in a straight line without braking. Wheel speeds vary when turning a corner and during braking without antilock operation. During antilock braking, wheel speeds should remain close to equal. Some 4WABS systems use a rear differential speed sensor (R) instead of a sensor at both rear wheels. In an ABS stop this system controls both rear wheels at the same time. | LR WSPD(MPH), R WSPD(MPH), RF WSPD(MPH), RR WSPD(MPH) |  | Closing parenthesis restored in name. Description text may be missing a closing parenthesis. |
| `LF_FAIL` | TRUE/FALSE | Left front failure test. |  |  |  |
| `LF_IN(AMP)` | not available | Current of the left front current inlet valve. |  |  |  |
| `LF_INLET` | ON/OFF | Status of the left front inlet valve. |  |  |  |
| `LF_OUT(AMP)` | not available | Current of the front left current outlet valve. |  |  |  |
| `LF_OUTLET` | ON/OFF | Left front outlet valve. |  |  |  |
| `LF_PRES` | not available | Left front brake pressure applied. |  |  |  |
| `LF_PRIME` | ON/OFF | Traction assist left front priming valve output state. |  |  |  |
| `LF_SWITCH` | ON/OFF | Traction assist left front switching valve output state. |  |  |  |
| `LF_TC_PRV` | ON/OFF | Status of the left front traction control priming valve. |  |  |  |
| `LF_TC_SWV` | ON/OFF | State of the traction assist left priming valve output. |  |  |  |
| `LF_WSPD(KPH/MPH)` | not available | Status of the left front wheel speed sensor. |  |  |  |
| `LONG_ACCL` | not available | Status of the longitudinal accelerometer signal. |  |  |  |
| `LR WSPD(MPH)` | 0 to 100+ MPH/KPH | Display the speed signals being sent by each front wheel speed sensor (LF/RF, each rear wheel speed sensors (LR or RR), or the single rear wheel speed sensor (R) to the electronic control module (ECM). Wheel speeds should be equal to each other and to vehicle speed as the vehicle is driven in a straight line without braking. Wheel speeds vary when turning a corner and during braking without antilock operation. During antilock braking, wheel speeds should remain close to equal. Some 4WABS systems use a rear differential speed sensor (R) instead of a sensor at both rear wheels. In an ABS stop this system controls both rear wheels at the same time. | LF WSPD(MPH), R WSPD(MPH), RF WSPD(MPH), RR WSPD(MPH) |  | Closing parenthesis restored in name. Description text may be missing a closing parenthesis. |
| `LR_FAIL` | TRUE/FALSE | Left rear failure test. |  |  |  |
| `LR_IN(AMP)` | not available | Current of the left rear current inlet valve. |  |  |  |
| `LR_INLET` | ON/OFF | Status of the left rear inlet valve. |  |  |  |
| `LR_OUT(AMP)` | not available | Current of the left rear current outlet valve. |  |  |  |
| `LR_OUTLET` | ON/OFF | Status of the left rear outlet valve. |  |  |  |
| `LR_PRES(KPA/PSI)` | not available | Left rear brake pressure applied. |  |  |  |
| `LR_PRIME` | ON/OFF | Status of the traction assist left rear priming valve output. |  |  | Also listed under: ABS. |
| `LR_PRIME` | ON/OFF | State of the traction assist left rear priming valve output. |  |  | Also listed under: ABS. |
| `LR_SWITCH` | ON/OFF | Status of the traction assist left rear switching valve output state. |  |  | Also listed under: ABS. |
| `LR_SWITCH` | ON/OFF | State of the traction assist left rear switching valve output. |  |  | Also listed under: ABS. |
| `LR_TC_PRV` | ON/OFF | Status of the left rear traction control priming valve. |  |  |  |
| `LR_TC_SWV` | ON/OFF | Status of the left rear traction control switching valve. |  |  | Also listed under: ABS. |
| `LR_TC_SWV` | ON/OFF | Status of the left rear traction control switching valve. |  |  | Also listed under: ABS. |
| `LR_TC_VLV` | ON/OFF | Status of the left rear traction control valve. |  |  | Also listed under: ABS. |
| `LR_TC_VLV` | ON/OFF | Status of the left rear traction control valve. |  |  | Also listed under: ABS. |
| `LR_WSPD(KPH/MPH)` | not available | Status of the left rear wheel speed sensor. |  |  |  |
| `LTRL_ACC` | not available | ABS lateral acceleration rate. |  |  |  |
| `L_ACCEL_EPB` | Acceleration/G | Longitudinal acceleration sensor. |  |  |  |
| `MAP_PCM` | not available | Manifold absolute pressure sensor. |  | PCM | Also listed under: PCM. |
| `MC_PT_1` | not available | Master cylinder travel signal 1. |  |  |  |
| `MC_PT_2` | not available | Master cylinder travel signal 2. |  |  |  |
| `MIL_DIS` | not available | Distance travelled since the MIL was activated. |  |  |  |
| `MNC_VEL_1` | not available | Brake tandem main cylinder velocity signal 1. |  |  |  |
| `MNC_VEL_2` | not available | Brake tandem main cylinder velocity signal 2. |  |  |  |
| `MN_PR_TRN` | PASS/FAIS | Booster main pressure transducer status. |  |  |  |
| `MODSTATE` | not available | Indicates the module state. |  |  |  |
| `PART#PRE_ABS` | not available | Indicates the part version prefix. |  |  |  |
| `PCM_MSG` | PRESENT/NOT PRESENT | Indicates if there is CAN communication with the PCM. NOT PRESENT indicates no communication. |  | 4x4 | Also listed under: 4x4. |
| `PMPSTAT` | ON/OFF | Indicates the status of the ABS pump motor. |  |  |  |
| `PMP_MOTOR` | ON/OFF | Indicates the status of the hydraulic pump motor. |  |  |  |
| `PRE_CHK` | ON/OFF | Indicates the pre-drive check. |  |  |  |
| `PRIM_VLV` | ON/OFF | Indicates the status of the primary low pressure supply valve. |  |  |  |
| `PUMP` | ON/OFF | Indicates the status of the pump. |  |  |  |
| `PUMPEFF` | not available | Indicates the status of the pump efficiency test. |  |  |  |
| `PWR RLY` | ON/OFF | Indicates whether the relay for the ABS System has been commanded on by the ABS module. PWR RLY reads ON when the relay contacts are closed, which directs current to the ABS pump motor. |  |  |  |
| `R WSPD(MPH)` | 0 to 100+ MPH/KPH | Display the speed signals being sent by each front wheel speed sensor (LF/RF, each rear wheel speed sensors (LR or RR), or the single rear wheel speed sensor (R) to the electronic control module (ECM). Wheel speeds should be equal to each other and to vehicle speed as the vehicle is driven in a straight line without braking. Wheel speeds vary when turning a corner and during braking without antilock operation. During antilock braking, wheel speeds should remain close to equal. Some 4WABS systems use a rear differential speed sensor (R) instead of a sensor at both rear wheels. In an ABS stop this system controls both rear wheels at the same time. | LF WSPD(MPH), LR WSPD(MPH), RF WSPD(MPH), RR WSPD(MPH) |  | Closing parenthesis restored in name. Description text may be missing a closing parenthesis. |
| `RF WSPD(MPH)` | 0 to 100+ MPH/KPH | Display the speed signals being sent by each front wheel speed sensor (LF/RF, each rear wheel speed sensors (LR or RR), or the single rear wheel speed sensor (R) to the electronic control module (ECM). Wheel speeds should be equal to each other and to vehicle speed as the vehicle is driven in a straight line without braking. Wheel speeds vary when turning a corner and during braking without antilock operation. During antilock braking, wheel speeds should remain close to equal. Some 4WABS systems use a rear differential speed sensor (R) instead of a sensor at both rear wheels. In an ABS stop this system controls both rear wheels at the same time. | LF WSPD(MPH), LR WSPD(MPH), R WSPD(MPH), RR WSPD(MPH) |  | Closing parenthesis restored in name. Description text may be missing a closing parenthesis. |
| `RF_FAIL` | TRUE/FALSE | Indicates right front failure test. |  |  |  |
| `RF_IN(AMP)` | not available | Indicates the current of the right front current inlet valve. |  |  |  |
| `RF_INLET` | ON/OFF | Indicates the status of the right front inlet valve. |  |  |  |
| `RF_OUT(AMP)` | not available | Indicates the current of the right front current outlet valve. |  |  |  |
| `RF_OUTLET` | ON/OFF | Indicates the status of the right front outlet valve. |  |  |  |
| `RF_PRES(KPA/PSI)` | not available | Indicates right front brake pressure applied. |  |  |  |
| `RF_PRIME` | ON/OFF | Indicates the state of the right front priming valve output. |  |  |  |
| `RF_SWITCH` | ON/OFF | Indicates the state of the traction assist right front switching valve output. |  |  |  |
| `RF_TC_PRV` | ON/OFF | Indicates the status of the front right traction control priming valve. |  |  |  |
| `RF_TC_SWV` | ON/OFF | Indicates the status of the front right traction control switching valve. |  |  |  |
| `RF_WSPD(KPH/MPH)` | not available | Indicates the status of the right front wheel speed sensor. |  |  |  |
| `ROLLRAT` | not available | Indicate the roll rate value. | ROLLRATE |  |  |
| `ROLLRATE` | not available | Indicate the roll rate value. | ROLLRAT |  |  |
| `RPM_FF` | not available | Indicates the engine speed. |  |  |  |
| `RR WSPD(MPH)` | 0 to 100+ MPH/KPH | Display the speed signals being sent by each front wheel speed sensor (LF/RF, each rear wheel speed sensors (LR or RR), or the single rear wheel speed sensor (R) to the electronic control module (ECM). Wheel speeds should be equal to each other and to vehicle speed as the vehicle is driven in a straight line without braking. Wheel speeds vary when turning a corner and during braking without antilock operation. During antilock braking, wheel speeds should remain close to equal. Some 4WABS systems use a rear differential speed sensor (R) instead of a sensor at both rear wheels. In an ABS stop this system controls both rear wheels at the same time. | LF WSPD(MPH), LR WSPD(MPH), R WSPD(MPH), RF WSPD(MPH) |  | Closing parenthesis restored in name. Description text may be missing a closing parenthesis. |
| `RR_FAIL` | TRUE/FALSE | Indicates the status of the right rear failure test. |  |  |  |
| `RR_IN(AMP)` | not available | Indicates the current of the right rear current inlet valve. |  |  |  |
| `RR_INLET` | ON/OFF | Indicates the status of the right rear inlet valve. |  |  |  |
| `RR_OUT(AMP)` | not available | Indicates the current of the right rear current outlet valve. |  |  |  |
| `RR_OUTLET` | ON/OFF | Indicates the status of the right rear outlet valve. |  |  |  |
| `RR_PRES(KPA/PSI)` | not available | Indicates the right rear brake pressure applied. |  |  |  |
| `RR_PRIME` | ON/OFF | Indicates the state of the right rear priming valve output. |  |  |  |
| `RR_SWITCH` | ON/OFF | Indicates the state of the traction assist right rear switching valve output. |  |  |  |
| `RR_TC_VLV` | ON/OFF | Indicates the status of the right rear traction assist valve. |  |  |  |
| `R_DIF_SPD` | not available | Indicates the rear differential wheel speed. |  |  |  |
| `R_INLET` | ON/OFF | Indicates the status of the rear only isolation valve. |  |  |  |
| `R_OUTLET` | ON/OFF | Indicates the status of the right outlet. |  |  |  |
