# Ford data parameters: Powertrain Control Module (PCM)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

810 parameters. Index: [README.md](README.md).

| Parameter | Range | Description | Shares entry with | Also under | Notes |
| --- | --- | --- | --- | --- | --- |
| `3/2 SHIFT SOL` | ON/OFF | State of the two solenoids used to activate the 3-2 and 4-3 downshift valves during deceleration and read ON when energized. | 4/3 SHIFT SOL |  |  |
| `4/3 SHIFT SOL` | ON/OFF | State of the two solenoids used to activate the 3-2 and 4-3 downshift valves during deceleration and read ON when energized. | 3/2 SHIFT SOL |  |  |
| `4X4Low` | ON/OFF | State of 4-wheel drive low switch on the instrument panel. The switch sends a ground signal to the PCM when 4x4 low is ON, which is used to adjust the transmission shift schedule. 4x4Low reads as follows: • ON when the switch circuit is closed • OFF when the switch is open |  |  |  |
| `A/CClutchSw` | ON/OFF | State of the A/C cycling switch. The PCM uses this parameter to adjust engine speed for added load. A/CClutchSw reads as follows: • ON when the switch is closed • OFF when the switch is open |  |  |  |
| `A/CPrs(V)` | 0.0 to 5.0 V | Condition of the air conditioning pressure (ACP) switch and reads as follows: • 0 V when the switch is closed • 4.0–5.0 V when the switch is open On some models, the PCM turns on the high-speed cooling fan if ACP readings indicate A/C compressor head pressure is over 325 psi. The PCM may also use the signal to control idle speed and the A/C clutch. |  |  |  |
| `AAT(°C/°F)` | not available | Ambient air temperature. |  |  |  |
| `ACClutchHEV` | ON/OFF | Air conditioning clutch. |  |  |  |
| `ACCS` | ON/OFF | Air conditioning compressor cycling switch. |  |  |  |
| `ACCS=A/C` | ON/OFF | Position of the air conditioning cycle switch (ACCS. It reads ON if the A/C switch on the instrument panel is on, or the PCM is commanding A/C on. In some cases, the A/C compressor may not turn on even though the switch is closed. Several other switch or sensor signals may prevent the PCM from engaging the A/C compressor clutch. |  |  | Description text may be missing a closing parenthesis. |
| `ACDS1` | ENABLE/DISABLE | Air conditioning diagnostic switch 1. |  |  |  |
| `ACDS2` | ENABLE/DISABLE | Air conditioning diagnostic switch 2. |  |  |  |
| `ACFD_BAT` | ON/OFF | A/C full demand switch for the battery area. |  |  |  |
| `ACFD_PAS` | ON/OFF | A/C full demand switch for the passenger area. |  |  |  |
| `ACL=ACCEL` | ON/OFF | Accelerometer status. |  |  |  |
| `AclPedl(V)` | 0.0 to 5.0 V | Accelerator pedal (AP) sensor signal voltage to the PCM on diesel control systems. The AP sensor is similar to the throttle position sensor on a gasoline engine. The AP sensor signal is used to calculate the fuel injection quantity and to control the exhaust backpressure regulator. The MIL lights if the AP sensor signal does not match the IVS signal at idle. A faulty AP sensor allows the engine to run at low idle speed only. |  |  |  |
| `ACP` | not available | Air conditioning pressure switch. |  |  | Also listed under: PCM. |
| `ACP` | OPEN/CLOSED | Air conditioning pressure switch. |  |  | Also listed under: PCM. |
| `ACP_PSI(KPA)` | not available | A/C pressure sensor. |  |  |  |
| `ACRDV` | ON/OFF | A/C refrigerant distribution valve commanded. |  |  |  |
| `ACRDV_F` | YES FAULT/NO FAULT | A/C refrigerant distribution valve status. |  |  |  |
| `ACTF` | YES/NO | Inlet air temperature status. |  |  |  |
| `ACTMNT_F` | YES FAULT/NO FAULT | Active mount control status. |  |  |  |
| `ACT_MNT` | ON/OFF | Active mount control commanded. |  |  |  |
| `AC_RC_SW` | ON/OFF | A/C recirculation switch. |  |  |  |
| `AFT2_ADDS` | not available | Adaptive fuel table 2. |  |  |  |
| `AFT_ADDS` | not available | Passenger side power sliding door module. |  |  |  |
| `AIR` | ON/OFF | PCM command to the electric air pump and reads ON when the PCM has commanded the air pump on. The parameter also indicates the secondary air injector. The AIR and AIRMonitor parameters should read the same. |  |  |  |
| `AIR A` | ON/OFF | PCM command to the secondary air relay and reads ON when the PCM is energizing the relay solenoid to close the relay contacts and run the air pump. |  |  |  |
| `AIR B` | ON/OFF | Air bypass solenoid, which controls vacuum to the air bypass valve. If bypass is activated, air is directed to either the catalyst or an upstream-downstream valve. When the system is deactivated, air is blocked from the catalyst or routed to atmosphere. These parameters read as follows: • ON when the air bypass solenoid is on and air bypass voltage is low • OFF if the solenoid is off and air bypass voltage is high |  |  |  |
| `AIR D` | ON/OFF | State of the air diverter solenoid, which controls vacuum to the air diverter valve. When the air diverter system is activated, air is routed upstream on many systems. These parameters read as follows: • ON when the air diverter solenoid is on and air bypass voltage is low • OFF when the solenoid is off and voltage is high | AIRDivert |  |  |
| `AIRBA` | ON/OFF | Secondary air bypass actual. |  |  |  |
| `AIRBypass` |  |  |  |  | Range not specified. No description available. |
| `AIRCON_SW_STATUS` | ON/OFF | Air conditioning switch status. |  |  |  |
| `AIRD` | ON/OFF | Secondary air injection divert. |  |  |  |
| `AIRDA` | ON/OFF | Secondary air divert actual. |  |  |  |
| `AIRDivert` | ON/OFF | State of the air diverter solenoid, which controls vacuum to the air diverter valve. When the air diverter system is activated, air is routed upstream on many systems. These parameters read as follows: • ON when the air diverter solenoid is on and air bypass voltage is low • OFF when the solenoid is off and voltage is high | AIR D |  |  |
| `AIREVAL_DC_MODE1` | YES/NO | Secondary air system is evaluated. |  |  |  |
| `AIRFault` | YES/NO | Secondary air injection system and reads YES when a fault is present. |  |  |  |
| `AIRFLOW(g/S)` | 0 to 255 | Amount of air flowing into the engine in grams per second. It is the same as the MAF=MASS AIR(gm/sec) parameter. Reading should be low at idle and higher at high RPM. | MAF(gm/S) |  |  |
| `AIRM` | ON/OFF | Status of the secondary air monitor. |  |  |  |
| `AIRMonitor` | ON/OFF | Status for the electric air pump based on a PCM feedback signal and reads ON if the PCM detects a voltage change when the pump is commanded on. The AIR and AIRMonitor parameters should read the same. |  |  |  |
| `AIR_FLT` | YES FAULT/NO FAULT | Secondary air injection status. |  |  |  |
| `ANTISCAN` | ACTIVE/OFF | Anti-scan function status. |  |  |  |
| `AP(%)` | not available | Accelerator pedal position. |  |  |  |
| `AP(V)` | 0 to 5.00 V | Voltage signal of the accelerator pedal position on a diesel engine control system. The accelerator position (AP) sensor is a three-wire potentiometer. Normal AP sensor voltage readings: SENSOR \| CLOSED THROTTLE \| WIDE OPEN THROTTLE APP1 \| 3.9–4.2 V \| 0.7–1.2 V APP2 \| 1.4–1.6 V \| 3.6–4.1 V APP3 \| 0.8–1.1 V \| 3.1–3.5 V | APP1(V), APP2(V), APP3(V) |  |  |
| `APP1(V)` | 0 to 5.00 V | Voltage signal of the accelerator pedal position on a diesel engine control system. The accelerator position (AP) sensor is a three-wire potentiometer. Normal AP sensor voltage readings: SENSOR \| CLOSED THROTTLE \| WIDE OPEN THROTTLE APP1 \| 3.9–4.2 V \| 0.7–1.2 V APP2 \| 1.4–1.6 V \| 3.6–4.1 V APP3 \| 0.8–1.1 V \| 3.1–3.5 V | AP(V), APP2(V), APP3(V) |  | Also listed under: PCM. |
| `APP1(V)` | not available | Accelerator pedal position sensor 1. |  |  | Also listed under: PCM. |
| `APP2(V)` | 0 to 5.00 V | Voltage signal of the accelerator pedal position on a diesel engine control system. The accelerator position (AP) sensor is a three-wire potentiometer. Normal AP sensor voltage readings: SENSOR \| CLOSED THROTTLE \| WIDE OPEN THROTTLE APP1 \| 3.9–4.2 V \| 0.7–1.2 V APP2 \| 1.4–1.6 V \| 3.6–4.1 V APP3 \| 0.8–1.1 V \| 3.1–3.5 V | AP(V), APP1(V), APP3(V) |  |  |
| `APP3(V)` | 0 to 5.00 V | Voltage signal of the accelerator pedal position on a diesel engine control system. The accelerator position (AP) sensor is a three-wire potentiometer. Normal AP sensor voltage readings: SENSOR \| CLOSED THROTTLE \| WIDE OPEN THROTTLE APP1 \| 3.9–4.2 V \| 0.7–1.2 V APP2 \| 1.4–1.6 V \| 3.6–4.1 V APP3 \| 0.8–1.1 V \| 3.1–3.5 V | AP(V), APP1(V), APP2(V) |  |  |
| `APP_F` | NO FAULT/FAULT | Accelerator pedal position status. |  |  |  |
| `APP_LOW(V)` | not available | Closed throttle status. |  |  |  |
| `APP_MODE` | CT/PT/WOT | Indicates whether the APP sensor mode is closed throttle (CT, partial throttle (PT, or wide open throttle (WOT. |  |  | Description text may be missing a closing parenthesis. |
| `AP_MODE_TCM` | PT/WOT/CT |  |  |  | No description available. |
| `AP_PER_TCM(%)` | not available | Accelerator pedal position. |  |  |  |
| `ARPMDES(rpm)` | not available | Ancillary RPM desired, which is the engine speed required to maintain the vehicle speed being commanded by the speed control system. |  |  |  |
| `AST` |  |  |  |  | Range not specified. No description available. |
| `AST2` | not available | Time since start in seconds. |  |  |  |
| `AXLE` | not available | This parameter indicates the axle ratio. |  |  |  |
| `A_CT(°C/°F)` | not available | Evaporator temperature state. |  |  | Closing parenthesis restored in name. |
| `A_CT_V(V)` | not available | Evaporator temperature signal voltage. |  |  | Closing parenthesis restored in name. |
| `BARO(Hz)` | 125 to 160 Hz) | Barometric pressure in cycles per second, or Hertz (Hz) and should read as follows: • 160 Hz at sea level • 125 Hz at an elevation of 10,000 feet |  |  |  |
| `BARO(V)` | 2.6 to 4.6 V | Barometric pressure as voltage and should read as follows: • 4.6 V at sea level • 2.6 V at an elevation of 10,000 feet |  |  |  |
| `BLoWerMoToR` | ON/OFF | Blower motor status. |  |  |  |
| `BLSHORT BATT` | YES/NO | Presence of a short to battery positive in the blower motor circuit. YES means that a short exists. |  |  |  |
| `BLSHORT GND` | YES/NO | Presence of a short to ground in the blower motor circuit. YES means that a short exists. |  |  |  |
| `BOO` | ON/OFF | Brake ON/OFF status. |  |  |  |
| `BOO=BRAKE SW` | ON/OFF | Brake pedal switch. Whenever the brake pedal is pressed, the brake ON/OFF (BOO) switch sends a battery voltage signal to the PCM. BOO=BRAKE SW should read as follows: • ON when the brake pedal is pressed • OFF at all other times Depending on the vehicle, the PCM may adjust engine idle speed with the brakes applied and the A/C turned on, or may turn the A/C off after a preset time. On vehicles with locking torque converters, the PCM may unlock the torque converter when the brakes are applied. |  |  |  |
| `BP=BARO (kPa)` | see description | Barometric pressure, which is calculated by the PCM based on the frequency of the barometric pressure (BP) sensor signal. This parameter reads as follows: • 102 kPa (30.1 inHG at sea level • 78 kPa (23 inHG at 7,000 feet Actual readings vary with weather conditions that cause barometric pressure to change. | BP=BARO (”HG) |  | Description text may be missing a closing parenthesis. |
| `BP=BARO (”HG)` | see description | Barometric pressure, which is calculated by the PCM based on the frequency of the barometric pressure (BP) sensor signal. This parameter reads as follows: • 102 kPa (30.1 inHG at sea level • 78 kPa (23 inHG at 7,000 feet Actual readings vary with weather conditions that cause barometric pressure to change. | BP=BARO (kPa) |  | Description text may be missing a closing parenthesis. |
| `BPA` | ON/OFF | Brake pressure applied switch status. |  |  |  |
| `BPA_SW` | ON/OFF | Brake pedal position status. |  |  |  |
| `BPO` | YES/NO | Battery power off request. |  |  |  |
| `BPO_Hz` | not available | Battery power off signal. |  |  |  |
| `BPP_BOO` | ON/OFF | Brake pedal position. |  |  |  |
| `BrakeOnOff` | ON/OFF | Brake pedal switch and reads as follows: • ON when the brakes are applied • OFF when the brakes are not applied This parameter should read the same as the BOO=BRAKE SW parameter. |  |  |  |
| `CAMDC 1` | not available | Duty cycle of the variable cam timing solenoids. • CAMDC 1 = bank 1 • CAMDC 2 = bank 2 | CAMDC 2 |  |  |
| `CAMDC 2` | not available | Duty cycle of the variable cam timing solenoids. • CAMDC 1 = bank 1 • CAMDC 2 = bank 2 | CAMDC 1 |  |  |
| `CAMDCR` | 0 to 99% | “On” time of the variable camshaft timing (VCT solenoid as requested by the PCM. CAMDCR should read as follows: • Near 0% with the engine running at a warm idle, camshaft fully advanced with no EGR • Near 50% with a warm engine and the vehicle operating at a steady cruise speed |  |  | Description text may be missing a closing parenthesis. |
| `CAMERR1` | not available | Variable cam timing error in crankshaft degrees. • CAMERR 1 = bank 1 • CAMERR 2 = bank 2 | CAMERR2 |  |  |
| `CAMERR2` | not available | Variable cam timing error in crankshaft degrees. • CAMERR 1 = bank 1 • CAMERR 2 = bank 2 | CAMERR1 |  |  |
| `CANP=PURGE` | ON/OFF | Canister purge solenoid, which opens canister purge (CANP) valve to regulate the flow of fuel vapors from the EVAP canister to the intake manifold. • When the engine is at operating temperature, the solenoid turns ON. This opens the canister purge valve and purging occurs. • When the engine is at idle or below operating temperature, the solenoid turns OFF. The valve closes and purge stops. |  |  |  |
| `CASeGND(V)` | -16.0 to 16.0 V | Voltage difference between the PCM case ground and the hardwired Fuel Level Input from the fuel pump driver module. • Above 0.050 V indicates high resistance • 0.027 V is a typical reading |  |  |  |
| `CAT mon ready` | YES/NO | Indicate whether the Catalyst Efficiency Monitor has successfully completed and read as follows: • YES if the monitor completed • NO if it did not | CAT MONITOR |  |  |
| `CAT MONITOR` | YES/NO | Indicate whether the Catalyst Efficiency Monitor has successfully completed and read as follows: • YES if the monitor completed • NO if it did not | CAT mon ready |  |  |
| `CATCAL1(Hz)` | 1 to 4 Hz) | Catalyst calibrated frequency for each cylinder bank, which is determined by the PCM based on engine load and RPM. See CATTST for further information. | CATCAL2(Hz) |  |  |
| `CATCAL2(Hz)` | 1 to 4 Hz) | Catalyst calibrated frequency for each cylinder bank, which is determined by the PCM based on engine load and RPM. See CATTST for further information. | CATCAL1(Hz) |  |  |
| `CATEVAL_DC_MODE1` | YES/NO | Catalyst monitor evaluated. |  |  |  |
| `CATTST1(Hz)` | 1 to 4 Hz) | Steady State CAT monitor measured frequency for banks 1 and 2 and are only used in 1996 OBD-II vehicles. The reading is the frequency of the rear O2S during a catalyst efficiency monitor test. This number is compared to a calibrated frequency number determined by the PCM (CATCAS based on engine load and RPM. If the CATTST value is greater than CATCAL, a Diagnostic Trouble Code (DTC is stored. The DTCs associated with this test are DTCs P0420 and P0430. The MIL is activated after a fault is detected on two consecutive drive cycles. If the CATTST value is less than CATCAL the catalyst monitor will indicate YES or passed. | CATTST2(Hz) |  | Description text may be missing a closing parenthesis. |
| `CATTST2(Hz)` | 1 to 4 Hz) | Steady State CAT monitor measured frequency for banks 1 and 2 and are only used in 1996 OBD-II vehicles. The reading is the frequency of the rear O2S during a catalyst efficiency monitor test. This number is compared to a calibrated frequency number determined by the PCM (CATCAS based on engine load and RPM. If the CATTST value is greater than CATCAL, a Diagnostic Trouble Code (DTC is stored. The DTCs associated with this test are DTCs P0420 and P0430. The MIL is activated after a fault is detected on two consecutive drive cycles. If the CATTST value is less than CATCAL the catalyst monitor will indicate YES or passed. | CATTST1(Hz) |  | Description text may be missing a closing parenthesis. |
| `CCC=TCC` | LOCKED/OFF | PCM command to the torque converter clutch control (TCC solenoid and reads as follows: • LOCKED when the TCC is engaged • OFF when the TCC is disengaged |  |  | Description text may be missing a closing parenthesis. |
| `CCL` | ON/OFF | Fuel cap off indicator lamp state. |  |  |  |
| `CCLF` | YES FAULT/NO FAULT | Fuel cap off indicator lamp fault state. |  |  |  |
| `CCMSTAT_DC_MODE1` | YES/NO | Comprehensive components monitor evaluated state. |  |  |  |
| `CCS` | ON/OFF | Coast clutch solenoid state. |  |  |  |
| `CCSA` | ON/OFF | PCM feedback signal indicating the actual state of the coast clutch solenoid. When activated, fourth gear is disabled to allow engine braking in third gear. CCSA and the CoastClSol readings should simultaneously display ON or OFF. |  |  |  |
| `CCSFault` | YES/NO | Presence of a fault in the coast clutch solenoid circuit. Reads YES or YES FAULT when a circuit fault is present. |  |  |  |
| `CCS_FLT` | YES FAULT/NO FAULT |  |  |  | No description available. |
| `CHT SENSOR(°)` | ON/OFF | Cylinder head temperature. | CHT(°C/°F) |  |  |
| `CHT(°C/°F)` | ON/OFF | Cylinder head temperature. | CHT SENSOR(°) |  |  |
| `CHTFM` | YES FAULT/NO FAULT | Cylinder head temperature status. |  |  |  |
| `CHTIL` | ON/OFF |  |  |  | No description available. |
| `CHTILFault` | YES/NO |  |  |  | No description available. |
| `CHTIL_FLT` | FAULT/NO FAULT | Operating status of the Cylinder Head Temperature Indicator Lamp (CHTIS. • CHTIL reads ON when the lamp is on. • CHTILFault reads YES if a fault was detected. |  |  | Description text may be missing a closing parenthesis. |
| `CHT_COLD(°C/°F)` | not available | Cylinder head temperature cold value. |  |  |  |
| `CHT_HOT(°C/°F)` | not available | Cylinder head hot value. |  |  |  |
| `CHT_V(°C/°F)` | not available | Cylinder head temperature. |  |  |  |
| `CKP_PROF` | YES/NO | Learned misfire correction profile state. |  |  |  |
| `CLRDIST_NOMIL` | not available | Distance since the last time diagnostic trouble codes were cleared. |  |  |  |
| `CMPFM` | YES/NO |  |  |  | No description available. Also listed under: PCM. |
| `CMPFM` | YES FAULT/NO FAULT | Camshaft position (CMP) failure mode, which indicates if a fault was detected in the camshaft position (CMP) sensor circuits and reads YES if a fault occurred. When the engine is cranking, or if operating in CMPFM mode, two spark plugs fire simultaneously until the CMP is determined by the PCM. This allows the engine to run even though the PCM is unable to determine if cylinder number 1 is on compression. |  |  | Also listed under: PCM. |
| `CMPFM2` | YES FAULT/NO FAULT | Camshaft position sensor 2 status. |  |  |  |
| `CmprCompMon rdy` | YES/NO | Indicates whether the Component monitor has successfully completed and reads as follows: • YES if the monitor completed • NO if it did not |  |  |  |
| `CMPS_lp_Lev` | HIGH/LOW | Camshaft position sensor level state. |  |  |  |
| `CMPS_Status` | OK/FAULT | Camshaft position sensor state. |  |  |  |
| `COAST` | ON/OFF | Speed control actuator coast switch state. |  |  |  |
| `CoastClSol` | ON/OFF | PCM command to the coast clutch solenoid, which allows engine braking in third gear when fourth gear is disabled by the transmission control switch. Reads ON when the coast clutch solenoid is activated. |  |  |  |
| `CoastClSol(%)` | 0 to 100% | PCM command to the coast clutch solenoid. When activated, coast clutch solenoid allows engine braking in third gear when fourth gear is disabled by the transmission control switch. The milliamp parameter displays the coast clutch solenoid current, and the percent parameter displays the duty cycle of the pulse width modulated solenoid signal. |  |  |  |
| `CoastClSol(mA)` | 0 to 1000 mA |  |  |  | No description available. |
| `CPC_AMP` | not available | Converter pressure control state. |  | TCM | Also listed under: TCM. |
| `CPP` | ON/OFF | Clutch pedal position switch state. |  |  |  |
| `CPP/TCS` | ON/OFF | Indicates the following: • Manual transmission vehicles—the status of the clutch pedal position switch (CPP) • Automatic transmission vehicles—the status of the transmission control switch (TCS CCP/TCS reads as follows: • ON when the switch is closed • OFF when the clutch pedal or TCS is pressed and the switch opens |  |  | Description text may be missing a closing parenthesis. |
| `CPP_ST` | NOT DEPRESSED/DEPRESSED | Clutch pedal position switch state. |  |  |  |
| `CRUISE` | ON/OFF | Reads ON if the driver selectable cruise control switch is on. |  |  |  |
| `CRUISE_SW` | ON/OFF | Speed control command switch state. |  |  |  |
| `CSEGND(V)` | -16.0 to 16.0 V | Voltage difference between the PCM case ground and signal return. • Above 0.050 V indicates high resistance • 0.027 V is a typical reading |  |  |  |
| `CylHdTemp(V)` | (range 0.25 to 4.75 V | Cylinder head temperature as voltage. The higher the voltage, the lower the temperature. |  |  |  |
| `DCDC_ENA` | ON/OFF | DC to DC converter enabled status. |  |  |  |
| `DCDC_F` | YES FAULT/NO FAULT | DC to DC converter status. |  |  |  |
| `DPFE(V)` | 0.45 to 4.60 V | Differential pressure feedback EGR (DPFE sensor signal, which displays exhaust pressure to the PCM. The PCM uses DPFE to compute optimum EGR flow. Pressure feedback EGR systems control EGR flow rate by monitoring pressure drop across a remotely located sharp-edged orifice. There are several sensor designs, some have an aluminum housing and use a 0.55 V offset, others have aluminum or plastic housings and use a 1.0 V offset. Typically, sensor output should be as shown below. Outputs for DPFE sensors with 0.55 V and 1.0 V offsets: 0.55 V OFFSET inHg \| 0.55 V OFFSET kPa \| 0.55 V OFFSET Volts \| 1.0 V OFFSET inHg \| 1.0 V OFFSET kPa \| 1.0 V OFFSET Volts 8.83 \| 29.81 \| 4.66 \| 8.56 \| 28.9 \| 4.95 6.62 \| 22.36 \| 3.64 \| 4.3 \| 14.4 \| 2.97 4.41 \| 14.90 \| 2.61 \| 0 \| 0 \| 1.0 2.21 \| 7.46 \| 1.58 0 \| 0 \| 0.55 |  |  | Description text may be missing a closing parenthesis. |
| `DPFEGR` | not available | Delta feedback pressure exhaust gas recirculation state. |  |  |  |
| `DRIVE CounT` | 0 to 255 | The number of times the engine has been started since the last DTC P1000 (monitor readinesS set. |  |  | Description text may be missing a closing parenthesis. |
| `DRIVECNT` | not available | Valid drive counters. | Drive_COUNT |  |  |
| `Drive_COUNT` | not available | Valid drive counters. | DRIVECNT |  |  |
| `DSD` | not available | Desired idle speed (rpm). |  |  |  |
| `DTC` | not available | Diagnostic trouble codes. |  |  |  |
| `DTC CouNT (pcm)` | see description | The number of continuous or pending codes that have set. The number changes each time a new code sets. |  |  |  |
| `DTC_CNT` | not available | DTC count and includes DTCs needing no action. |  |  |  |
| `EBP(V)` | 0 to 5 V | Voltage signal from the exhaust backpressure sensor, which is used by the PCM to control the exhaust backpressure regulator (EPR). As voltage increases, so does pressure: Outputs for exhaust backpressure sensor: VOLTS \| psi \| kPa 0.00 \| 0.00 \| 0.00 0.25 \| 8.00 \| 55.1 1.45 \| 20.0 \| 137 4.45 \| 50.0 \| 344 4.75 \| 53.0 \| 365 5.00 \| 60.0 \| 413 |  |  |  |
| `ECT` | NORM/COLD/HOT | Engine coolant temperature (ECT sensor signal as temperature or voltage. The ECT is a thermistor installed in the engine coolant passages. A 5 V reference signal is applied to the ECT. As temperature increases, sensor resistance decreases to provide a variable voltage signal to the PCM. The PCM converts ECT sensor voltage to temperature. These parameters read as follows: • The temperature parameter reads 185° to 220°F (85° to 105°C on a warm engine at idle. • A temperature reading of -40°C or -40°F may indicate an open sensor or circuit. • A temperature reading above 185°C or 366°F may indicate a short in the sensor or circuit. • The voltage parameter reads 0.70 to 0.40 V on a warm engine at idle. |  |  | Description text may be missing a closing parenthesis. |
| `ECT(V)` | 0.0 to 5.0 |  |  |  | No description available. |
| `ECT(°C)` | -40 to 199 |  |  |  | No description available. |
| `ECT(°F)` | -40 to 389 |  |  |  | No description available. |
| `ECTF` | YES FAULT/NO FAULT | Engine coolant temperature state. |  |  |  |
| `ECT_FF (°C/°F)` | not available | Engine coolant temperature. |  |  |  |
| `EFTA` | not available | Fuel rail pressure state. |  |  |  |
| `EGR mon ready` | YES/NO | EGR system monitor has successfully completed and read as follows: • YES if the monitor completed • No if it did not. | EGR MONITOR |  |  |
| `EGR MONITOR` | YES/NO | EGR system monitor has successfully completed and read as follows: • YES if the monitor completed • No if it did not. | EGR mon ready |  |  |
| `EGRBARO` | 0 to 5 V | EGR Baro sensor voltage signal. The signal is used by the PCM to control EGR flow. The greater the EGR flow, the lower the voltage. |  |  |  |
| `EGRCFault` | YES/NO | PCM has detected a fault in the EGR control solenoid circuit and reads YES when a fault is present. |  |  |  |
| `EGRCvacsol` | ON/OFF | State of a normally closed solenoid that regulates vacuum to the EGR valve with a variable duty cycle and reads as follows: • ON when the solenoid is enabled • OFF when it is disabled |  |  |  |
| `EGRDC(%)` | not available | Exhaust gas recirculation valve duty cycle. |  |  |  |
| `EGRDtyCycl(%)` | 0 to 100% | Duty cycle of the EGR vacuum regulator valve or EVR. The EVR controls the amount of EGR flow. EGRDtyCycl reads as follows: • 0% when the EVR is fully open and allowing vacuum to vent to atmosphere • 100% when the EVR is fully closed allowing vacuum to open the EGR valve |  |  |  |
| `EGREVAL_DC_MODE1` | YES/NO | Indicates if the exhaust gas recirculation system has been evaluated. |  |  |  |
| `EGRFOpen` | YES/NO | Indicates whether the PCM has detected an open circuit in the EGR vacuum regulator valve or EVR circuit and reads YES when a fault is present. |  |  |  |
| `EGRFShort` | YES/NO | Indicates whether the PCM has detected a short circuit in the EGR vacuum regulator valve or EVR circuit and reads YES when a fault is present. |  |  |  |
| `EGRMC1` | ON/OFF | EGR motor control state for the indicated EGR motor control number. | EGRMC2, EGRMC3, EGRMC4 |  |  |
| `EGRMC1F` | YES FAULT/NO FAULT | EGR motor control fault state for the indicated EGR motor control number. | EGRMC2F, EGRMC3F, EGRMC4F |  |  |
| `EGRMC2` | ON/OFF | EGR motor control state for the indicated EGR motor control number. | EGRMC1, EGRMC3, EGRMC4 |  |  |
| `EGRMC2F` | YES FAULT/NO FAULT | EGR motor control fault state for the indicated EGR motor control number. | EGRMC1F, EGRMC3F, EGRMC4F |  |  |
| `EGRMC3` | ON/OFF | EGR motor control state for the indicated EGR motor control number. | EGRMC1, EGRMC2, EGRMC4 |  |  |
| `EGRMC3F` | YES FAULT/NO FAULT | EGR motor control fault state for the indicated EGR motor control number. | EGRMC1F, EGRMC2F, EGRMC4F |  |  |
| `EGRMC4` | ON/OFF | EGR motor control state for the indicated EGR motor control number. | EGRMC1, EGRMC2, EGRMC3 |  |  |
| `EGRMC4F` | YES FAULT/NO FAULT | EGR motor control fault state for the indicated EGR motor control number. | EGRMC1F, EGRMC2F, EGRMC3F |  |  |
| `EGRMDSD` | not available | EGR motor position desired. |  |  |  |
| `EGRTP(V)` | 0.30 to 4.50 V | Voltage signal sent to the PCM by the EGR valve position sensor, which indicates the position of the EGR valve pintle. • A low reading indicates low EGR flow • A high reading indicates high EGR flow | EGRVP(V) |  |  |
| `EGRTPDC(%)` | not available | EGR throttle position. |  |  |  |
| `EGRVentsol` | ON/OFF | PCM command to the EGR vent solenoid and reads ON when the solenoid is energized to bleed vacuum from the EGR valve and closing it. |  |  |  |
| `EGRVFault` | ON/OFF | PCM has detected a fault in the EGR vent solenoid circuit and reads YES when a fault is present. |  |  |  |
| `EGRVP(V)` | 0.30 to 4.50 V | Voltage signal sent to the PCM by the EGR valve position sensor, which indicates the position of the EGR valve pintle. • A low reading indicates low EGR flow • A high reading indicates high EGR flow | EGRTP(V) |  |  |
| `EGRVPDES` | 0 to 14% | Desired EGR. |  |  |  |
| `EGRVR(%)` | 0 to 90% | Signal sent to the PCM by the EGR valve position sensor as a percentage. A low reading indicates low EGR flow, while a high reading indicates high EGR flow. |  |  |  |
| `EGRVR_FLT` | No fault/Short/Open/Yes Fault/Invalid | Exhaust gas recirculation vacuum regulator fault. |  |  |  |
| `EGR_EVAL` | YES/NO | Exhaust gas recirculation system has been evaluated. |  |  |  |
| `EGR_Fault` | YES FAULT/NO FAULT | Exhaust gas recirculation state. |  |  |  |
| `ElecPrsCtrl` | 0 to 100 psi | PCM commanded transmission electronic control pressure. This is a calculated parameter based on PCM-controlled pulse-width output. |  |  |  |
| `ENG LOAD(%)` | see description | Engine load as calculated by the PCM based on engine speed (RPM), number of cylinders, airflow, and cylinder air charge compared to the theoretical air charge that occurs at standard PCM temperature and pressure (volumetric efficiency). The resulting ratio—called engine load—is expressed as a percentage. The engine load parameters read as follows: • 20–40% = a normal load at idle • Less than 80% = normal driving • More than 100% = under hard acceleration in cold temperatures or high barometric pressure, or on supercharged vehicles | LOAD(%) |  |  |
| `EngFuelTA(V)` | 0 to 5.00 V | Engine fuel temperature sensor voltage on diesel, flexible-fuel, and natural-gas powered vehicles. • EngFuelTA = bank 1 • EngFuelTB = bank 2 | EngFuelTB(V) |  |  |
| `EngFuelTB(V)` | 0 to 5.00 V | Engine fuel temperature sensor voltage on diesel, flexible-fuel, and natural-gas powered vehicles. • EngFuelTA = bank 1 • EngFuelTB = bank 2 | EngFuelTA(V) |  |  |
| `EngOilTemp(°)` | 0 to 212°F or -17.8 to 100°C | Engine oil temperature (EOT in volts and degrees. | EOT(°), EOT_DEG(°C/°F) |  | Description text may be missing a closing parenthesis. |
| `EOT(V)` | 0 to 5 V |  |  |  | No description available. |
| `EOT(°)` | 0 to 212°F or -17.8 to 100°C | Engine oil temperature (EOT in volts and degrees. | EngOilTemp(°), EOT_DEG(°C/°F) |  | Description text may be missing a closing parenthesis. |
| `EOTF` | YES FAULT/NO FAULT | Engine oil temperature state. |  |  |  |
| `EOTM` | HOT/COLD/NORM/DEF | General state, or mode, of the engine oil temperature (EOT on diesel powertrain control systems. Possible states are HOT, COLD, NORM (normal, and DEF (deFAULT. If the engine oil temperature is out of range, EOTM reads DEF. |  |  | Description text may be missing a closing parenthesis. |
| `EOT_DEG(°C/°F)` | 0 to 212°F or -17.8 to 100°C | Engine oil temperature (EOT in volts and degrees. | EOT(°), EngOilTemp(°) |  | Description text may be missing a closing parenthesis. |
| `EOT_M(°C/°F)` | not available | Engine oil temperature. |  |  |  |
| `EPC(psi)` | 0 to 100 psi | PCM commanded transmission electronic control pressure (EPC. This is a calculated parameter base on PCM-controlled Pulse-Width output. |  |  | Description text may be missing a closing parenthesis. |
| `EPC(V)` | 2 V to battery voltage | Average operating voltage of the pulse-width-modulated electronic pressure control solenoid. A low average voltage reading indicates a higher EPC pressure; a higher reading indicates a lower EPC pressure. |  |  |  |
| `EPCV(V)` | not available | Electronic pressure control voltage. |  |  |  |
| `EPR(%)` | 0 to 100% | Position of the exhaust pressure regulator (EPR). No further definition available at this time. |  |  |  |
| `ETC_ACT` | Actual degrees |  |  |  | No description available. |
| `ETC_CTRL` | not available | Throttle control for drive shaft balance. |  |  |  |
| `ETC_DSD` | Desired degrees | Actual (ACT and desired (DSD) electronic throttle control. |  |  | Description text may be missing a closing parenthesis. |
| `EVAP mon ready` | YES/NO | Evaporative monitor has successfully completed and reads as follows: • YES if the monitor completed. • NO if it did not complete. |  |  |  |
| `EVAPC` | 0 to 100% | PCM-commanded canister purge valve duty cycle and reads 100% when the canister purge valve is closed and not allowing fuel vapors from the evaporative canister. | EVAPCV(%) |  |  |
| `EVAPCP` | ON/OFF | Indicates whether the canister purge solenoid is on (ON or off (OFF. |  |  | Description text may be missing a closing parenthesis. |
| `EVAPCPFault` | YES/NO | Indicates whether the PCM detects a fault in the canister purge solenoid circuit and reads YES when a fault is present. |  |  |  |
| `EVAPCP_FLT` | YES FAULT/NO FAULT | Evaporative emission canister purge fault. |  |  |  |
| `EVAPCV` | 0 to 100% | Duty cycle of the EVAP canister vent solenoid, which controls the amount of air entering the EVAP system. EVAPCV reads as follows: • 0% when the solenoid is fully open • 100% when the solenoid is fully closed |  |  |  |
| `EVAPCV(%)` | 0 to 100% | PCM-commanded canister purge valve duty cycle and reads 100% when the canister purge valve is closed and not allowing fuel vapors from the evaporative canister. | EVAPC |  |  |
| `EVAPCVA` | ON/OFF | Evaporative emission canister vent valve. |  |  |  |
| `EVAPCVF1` | YES FAULT/NO FAULT | Evaporative emission canister vent valve status. |  |  |  |
| `EVAPCVFault` | YES/NO |  |  |  | No description available. |
| `EVAPCVFLT` | YES FAULT/NO FAULT | PCM detects a fault in the canister vent solenoid circuit and reads YES when a fault is present. |  |  |  |
| `EVAPEVAL_DC_MODE1` | YES/NO | Evaporative system monitor evaluated. |  |  |  |
| `EVAPPrgFlw(V)` | 0 to 5 V | Presence of purge flow from the canister to the engine. Voltage should increase as purge flow increases. EVAPPrgFlw reads as follows: • 0.4 to 1.3 V at idle • 0.4 to 4.0 V at steady cruise |  |  |  |
| `EVAPSTA` | not available | Evaporative emissions monitor status. |  |  |  |
| `EVAPVM(%)` | not available | Evaporative emission vapor management valve status command. |  |  |  |
| `EVAPVMA` | ON/OFF | Commanded state of the EVAPVM. |  |  |  |
| `EVAPVM_Fault` | YES FAULT/NO FAULT | Evaporative emission vapor management fault. |  |  |  |
| `EVMV` | OFF/ON/VARYING | Evaporative emission vapor management valve status. |  |  |  |
| `EVP(V)` | 0.30 to 4.50 V | EGR valve position (EVP) sensor signal, which reflects EGR valve pintle position. The EVP sensor is mounted on the EGR valve. The spring-loaded EGR valve is operated by a vacuum signal from the EGR vacuum regulator solenoid. As supply vacuum overcomes spring force, the diaphragm moves. This lifts the pintle off of its seat and allows exhaust gas to recirculate. The amount of flow is proportional to the pintle position. Specifications are model dependant. | STE=EVP(V) |  |  |
| `EVR(%)` | 0 to 100% | Duty-cycle output by the PCM to operate the EGR vacuum regulator (EVR) solenoid. The EVR solenoid is a pulse-width-modulated (PWM) device that controls manifold vacuum to the EGR valve. The PCM outputs to the EVR regulates the vacuum output to the EGR valve. As the duty-cycle is increased, so is the vacuum output to the EGR valve. EVR(% reads as described below. EVR(%) readings: EVR(%) \| inHg RANGE \| kPa RANGE 0% \| 0 to 0.75 \| 0 to 2.53 33% \| 0.55 to 2.05 \| 1.86 to 6.9 90% \| 5.69 to 6.95 \| 19.2 to 23.47 |  |  | Description text may be missing a closing parenthesis. |
| `ExhBackPrs` | 0 to 2368 psi | Exhaust back pressure sensor as determined by a variable-capacitor type sensor in the right exhaust manifold on diesel powertrain control systems. |  |  |  |
| `FAN` | LOW/HIGH/OFF | Operational status of the engine cooling fan. |  |  |  |
| `FAN Monitor` | ON/OFF | Feedback signal that determines if the fan has been successfully commanded on or off by the PCM. The PCM monitors the fan side of the CCRM (or IRCM). • ON indicates high input voltage • OFF indicates low input voltage |  |  |  |
| `FANM(HIGH)` | ON/OFF | Indicates whether the PCM is commanding the fan to run at high speed and reads ON when the fan is running at high speed. |  |  |  |
| `FANSS` | not available | Electronic cooling fan speed sensor. |  |  |  |
| `FANSSM` | HIGH/LOW | Fan speed sensor monitor. |  |  |  |
| `FAN_FLT` | No Fault/LFC Fault/HFC FAULT | Engine cooling fan operation. |  |  |  |
| `FAN_REQ` | ON/OFF | Cooling fan state requested. |  |  |  |
| `FDSP_ESCS_GPL` | ON/OFF | Glow plug lamp state. |  |  |  |
| `FFINF(%)` | not available | Inferred flex fuel. |  |  |  |
| `FFLRND` | YES/NO | Flex fuel learned status. |  |  |  |
| `FICMSYNC` | YES/NO | Synchronization from the FICM (Fuel Injector Control ModulE. |  |  | Description text may be missing a closing parenthesis. |
| `FICM_LPWR` | 10.5 to 15.5 V |  | FICM_VPWR |  | No description available. |
| `FICM_MPWR` | 40 to 52 V | Voltage readings from the fuel injector control module (FICM). • LPWR = Logic power • MPWR = Main power • VPWR = Vehicle power |  |  |  |
| `FICM_VPWR` | 10.5 to 15.5 V |  | FICM_LPWR |  | No description available. |
| `FIRMSHFT` | ON/OFF | Firm shift status. |  | TCM | Also listed under: TCM. |
| `FlexFuel(Hz)` | -32,512 to 32,768 | Flexible fuel output frequency. |  |  |  |
| `FLG_MLUSFM` | YES FAULT/NO FAULT | Torque converter unlocking due to slipping. |  | TCM | Also listed under: TCM. |
| `FLG_OTLK` | YES FAULT/NO FAULT | Transmission over temperature lock up mode. |  | TCM | Also listed under: TCM. |
| `FLG_SS4` | ON/OFF | Shift solenoid 4 state. |  |  |  |
| `FLI(V)` | not available | Fuel level input voltage. |  |  |  |
| `FLIFM` | YES/NO | Fuel level input FMEM flag. |  |  |  |
| `FLInput(%)` | 0 to 100% | Fuel level in the fuel tank as a voltage. A voltage or percentage at the high end of the sensor range indicates a high fuel level. The PCM must receive a signal of 25–75% before beginning the evaporative emission monitor test. | FueLvlimput(%) |  |  |
| `FLInput(V)` | 0 to 32.0 V |  | FueLvlinput(V) |  | No description available. |
| `FLI_PERCENT(%)` | not available | Fuel level state. |  |  |  |
| `FLI_SG(V)` | not available | Fuel level input voltage. |  |  |  |
| `FP` | ON/OFF | Fuel pump status. |  |  |  |
| `FP=FUEL PUMP` | ON/OFF | ON/OFF status of the fuel pump relay. • The fuel pump turns ON when the ignition switch is turned on and the fuel pump relay is energized. • The fuel pump shuts OFF if the ignition switch is not turned to the start position and the PCM de-energizes the fuel pump relay after approximately one second. The PCM operates the fuel pump when the ignition is in the start position to provide fuel while the engine is cranking. Once the engine starts the PCM continues operating the fuel pump unless the engine speed drops below 120 RPM or the inertia fuel switch (IFS is activated. |  |  | Description text may be missing a closing parenthesis. |
| `FPA STATE` | ON/OFF | Actual state of the fuel pump power circuit and reads as follows: • ON when the pump is running • OFF when it is not |  |  |  |
| `FPFault` | YES/NO | Indicates whether the PCM has detected a fault in the fuel pump circuit and reads YES when a fault is present. |  |  |  |
| `FPM` | ON/OFF |  |  |  | No description available. |
| `FPM(%)` | not available | Fuel pump monitor. |  |  |  |
| `FPMode` |  |  |  |  | Range not specified. No description available. |
| `FPMonitor` | ON/OFF | Indicate whether the fuel pump has turned on or off in response to a command from the PCM. |  |  |  |
| `FP_FLT` | YES FAULT/NO FAULT | Fuel pump fault status. |  |  |  |
| `FRP(psi)` | 37 to 150 psi | Fuel rail pressure (FRP) in psi on natural gas powered vehicles. |  |  |  |
| `FRP(V)` | not available | Fuel rail pressure voltage. |  |  |  |
| `FRPFM` | YES FAULT/NO FAULT | Fuel rail pressure transducer status. |  |  |  |
| `FRP_DSD` | not available | Fuel pressure desired. |  |  |  |
| `FRP_FAULT` | YES FAULT/NO FAULT | Fuel rail pressure status. |  |  |  |
| `FRP_PRESS` | not available | Fuel rail pressure. |  |  |  |
| `FRT1FM` | YES FAULT/NO FAULT | Fuel rail temp sensor #1 status. |  |  |  |
| `FSVFault` | YES/NO | Indicates whether the PCM detects a fault in the fuel solenoid valve circuit on flexible-fuel vehicles and reads YES when a fault is present. | FuelSolVlv |  |  |
| `FSVMonitor` | ON/OFF | Fuel solenoid valve (FSV circuit on flexible-fuel vehicles state and reads ON when the PCM has turned on the fuel solenoid valve. |  |  | Description text may be missing a closing parenthesis. |
| `FTIV_F` | YES FAULT/NO FAULT | Fuel tank isolation valve status. |  |  |  |
| `FTIV_HEV(%)` | not available | Fuel tank isolation valve. |  |  |  |
| `FTP(KPA)` | not available | Fuel tank pressure transducer. |  |  |  |
| `FTP_FAULT` | YES FAULT/NO FAULT | Fuel tank pressure transducer status. |  |  |  |
| `FUEL mon ready` | YES/NO | Indicates whether the Fuel monitor has successfully completed and reads as follows: • YES if the monitor completed • NO if it did not |  |  |  |
| `FUEL STATUS` | see description | Indicate whether the vehicle is operating in open or closed loop. It is similar to the OPEN/ CLSD LOOP but provides more information about the cause of the open- or closed-loop condition. FUEL SYS1 and FUEL SYS2 refer to bank 1 and 2. These parameters read as follows: • CL = normal closed loop • CL FLT = One O2S is not switching and the PCM is using the other one for feedback • OL = normal open loop • OL DRV = Open loop because of driver action or other special circumstances • OL FLT = Open loop with O2S problem or primary side coil failure | FUEL SYS, FUEL SYS1, FUEL SYS2, FUELSYS |  |  |
| `FUEL SYS` | see description | Indicate whether the vehicle is operating in open or closed loop. It is similar to the OPEN/ CLSD LOOP but provides more information about the cause of the open- or closed-loop condition. FUEL SYS1 and FUEL SYS2 refer to bank 1 and 2. These parameters read as follows: • CL = normal closed loop • CL FLT = One O2S is not switching and the PCM is using the other one for feedback • OL = normal open loop • OL DRV = Open loop because of driver action or other special circumstances • OL FLT = Open loop with O2S problem or primary side coil failure | FUEL STATUS, FUEL SYS1, FUEL SYS2, FUELSYS |  |  |
| `FUEL SYS1` | see description | Indicate whether the vehicle is operating in open or closed loop. It is similar to the OPEN/ CLSD LOOP but provides more information about the cause of the open- or closed-loop condition. FUEL SYS1 and FUEL SYS2 refer to bank 1 and 2. These parameters read as follows: • CL = normal closed loop • CL FLT = One O2S is not switching and the PCM is using the other one for feedback • OL = normal open loop • OL DRV = Open loop because of driver action or other special circumstances • OL FLT = Open loop with O2S problem or primary side coil failure | FUEL STATUS, FUEL SYS, FUEL SYS2, FUELSYS |  |  |
| `FUEL SYS2` | see description | Indicate whether the vehicle is operating in open or closed loop. It is similar to the OPEN/ CLSD LOOP but provides more information about the cause of the open- or closed-loop condition. FUEL SYS1 and FUEL SYS2 refer to bank 1 and 2. These parameters read as follows: • CL = normal closed loop • CL FLT = One O2S is not switching and the PCM is using the other one for feedback • OL = normal open loop • OL DRV = Open loop because of driver action or other special circumstances • OL FLT = Open loop with O2S problem or primary side coil failure | FUEL STATUS, FUEL SYS, FUEL SYS1, FUELSYS |  |  |
| `FuelPumpA` | ON/OFF | Actual state of the commanded fuel pump output and should read the same as the fuel pump monitor and fuel pump command output displays. All three should be ON or OFF at the same time. |  |  |  |
| `FUELPW` | not available | Fuel pulse width. |  |  |  |
| `FUELPW(mS)` | 0 to 99.9 | Time in milliseconds (mS that the PCM commands the fuel injectors to turn on for diesel powertrain control systems. • A high reading indicates more on-time. • A low reading indicates less on-time. |  |  | Description text may be missing a closing parenthesis. |
| `FUELPW1(mS)` | 0 to 99.9 | Current commanded pulse width of the injectors affected by O2S1 and O2S2. The displayed value is the pulse width that was commanded at the time of the last update. On some vehicles, updating may occur only when a PIP signal is being received, and the last updated value is retained after the PIP signal stops. In these cases, a value greater than zero may display during KOEO. | FUELPW2(mS) |  |  |
| `FUELPW2(%)` | not available | Fuel pulse width #2 percentage. |  |  |  |
| `FUELPW2(mS)` | 0 to 99.9 | Current commanded pulse width of the injectors affected by O2S1 and O2S2. The displayed value is the pulse width that was commanded at the time of the last update. On some vehicles, updating may occur only when a PIP signal is being received, and the last updated value is retained after the PIP signal stops. In these cases, a value greater than zero may display during KOEO. | FUELPW1(mS) |  |  |
| `FuelRailPrs(V)` | 0 to 4.5 V | Fuel rail pressure in volts on natural gas powered vehicles. As voltage increases, so does pressure. • 0 V = 37.6 psi (260 kPa) • 4.5 V = 150 psi (1034 kPa) |  |  |  |
| `FuelSolVlv` | YES/NO | Indicates whether the PCM detects a fault in the fuel solenoid valve circuit on flexible-fuel vehicles and reads YES when a fault is present. | FSVFault |  |  |
| `FUELSYS` | see description | Indicate whether the vehicle is operating in open or closed loop. It is similar to the OPEN/ CLSD LOOP but provides more information about the cause of the open- or closed-loop condition. FUEL SYS1 and FUEL SYS2 refer to bank 1 and 2. These parameters read as follows: • CL = normal closed loop • CL FLT = One O2S is not switching and the PCM is using the other one for feedback • OL = normal open loop • OL DRV = Open loop because of driver action or other special circumstances • OL FLT = Open loop with O2S problem or primary side coil failure | FUEL STATUS, FUEL SYS, FUEL SYS1, FUEL SYS2 |  |  |
| `FuelTankPrs(V)` | 0 to 5.0 V | Signal voltage from the fuel tank pressure sensor. The Evaporative Emissions Monitor requires input from this sensor. With the gas cap removed, signal voltage should be between 2.4 and 2.8 V. During the evaporative emissions test, expect voltage to decrease while the PCM applies vacuum to the fuel tank. |  |  |  |
| `FueLvlimput(%)` | 0 to 100% | Fuel level in the fuel tank as a voltage. A voltage or percentage at the high end of the sensor range indicates a high fuel level. The PCM must receive a signal of 25–75% before beginning the evaporative emission monitor test. | FLInput(%) |  |  |
| `FueLvlinput(V)` | 0 to 32.0 V |  | FLInput(V) |  | Closing parenthesis restored in name. No description available. |
| `FUEL_RAIL_TEMP(°C/°F)` | not available | Fuel rail temperature. |  |  |  |
| `GEAR` | 1st/2nd/3rd/4th/5th/6th/REV | Processor commanded gear position on vehicles with electronic transmissions. |  |  |  |
| `GEAR_MAX` | not available | Highest gear allowed. |  | TCM | Also listed under: TCM. |
| `Gear_OSC` | not available | Gear command by output state control. |  |  |  |
| `GEAR_RAT` | not available | Gear ratio. |  | TCM | Also listed under: TCM. |
| `GENB_F` | YES FAULT/NO FAULT | Generator B fault. |  |  |  |
| `GENB_FAULT` | YES/NO | Reads YES when the PCM detects a fault in the generator B+ circuit. |  |  |  |
| `GENF` | YES FAULT/NO FAULT | Generator output fault. |  |  |  |
| `GENFDC(%)` | not available | Generator field duty cycle. |  |  |  |
| `GENFIL` | YES/NO | Reads YES when the PCM detects a fault in the generator lamp circuit. |  |  |  |
| `GENLMP` | )ON/OFF | Generator lamp circuit. | GEN_LAMP |  |  |
| `GENVDSD` | not available | Generator voltage desired. |  |  |  |
| `GEN_FAULT` | YES/NO | Reads YES when the PCM detects a generator output fault. |  |  |  |
| `GEN_LAMP` | )ON/OFF | Generator lamp circuit. | GENLMP |  |  |
| `GEN_MON` | LOW/HIGH |  |  |  | No description available. |
| `GEN_MON(%)` | not available | Generator monitor status. |  |  |  |
| `GlowPlugCtrl(%)` | 0 to 100% | Glow plug control (GPC duty cycle on diesel truck powertrain control systems, this. At high battery voltage, the PCM cycles the glow plug current on and off to prevent damage to the glow plugs. GlowPlugCtrl reads as follows: • 0% = the glow plug circuit is off • 100% = the circuit is on continuously Related parameters that may display are GPCTM and GPLTM. |  |  | Description text may be missing a closing parenthesis. |
| `GPC(%)` | not available | Glow plug control percentage. |  |  |  |
| `GPCoilTiMe` | 10 to 120 seconds | Glow plug control time on diesel powertrain control systems, which shows the actual time the glow plugs would be switched on for if the engine were started under current conditions. The time interval is based on engine oil temperature and reads as follows: • About 10 seconds on a warm engine • 120 seconds on a cold engine |  |  |  |
| `GPCTM` | not available | Glow plug coil on time. |  |  |  |
| `GPC_L` | not available | Glow plug current, left bank. |  |  |  |
| `GPC_R` | not available | Glow plug current, right bank. |  |  |  |
| `GPLampTiMe` | 2 to 10 seconds | Glow plug lamp time on diesel powertrain control systems, which shows the length of time that the “wait to start” would be illuminated if the ignition was switched on under current conditions. The lamp comes on every time a key reset occurs, and it remains on from 2 to 10 seconds depending upon engine temperature. |  |  |  |
| `GPLTM` | not available | Glow plug lamp time if active. |  |  |  |
| `GR_RATIO` | not available | Transmission gear ratio. |  | TCM | Also listed under: TCM. |
| `GTQ_OUT_ENG` | not available | Measured generator torque. |  |  |  |
| `G_SDN` | YES/NO | Generator shutdown request. |  |  |  |
| `HCATEVAL_DC_MODE1` | YES/NO | Heated catalyst monitor is evaluated. |  |  |  |
| `HFC` | ON/OFF | Fan control high speed. |  |  |  |
| `HFC=HI FAN` | ON/OFF | Fan relays. Some engines have a two-speed electric fan, which the PCM controls through LFC and HFC outputs to the fan relays. The high speed relay is a normally open for all models. However, the low speed fan relay is normally closed for Escort and Tracer, and normally open for Probe. When the fan is off: • HFC should read OFF. • LFC should read OFF for Probe and ON for Escort and Tracer. With the low speed fan on: • HFC should read OFF. • LFC should read ON for Probe and OFF for Escort or Tracer. When the high speed fan is on: • HFC should read ON. • LFC should read OFF for all models. | LFC=LO FAN, HighFanCtrl |  |  |
| `HFCA` | ON/OFF | Actual state of the commanded output to the fan. It should be the same as the fan monitor and fan command output displays; that is, all three should display ON or OFF at the same time. |  |  |  |
| `HFCFault` | YES/NO | Indicates whether the PCM detects a fault with the high speed cooling fan circuit and reads YES if a fault is present. |  |  |  |
| `HFC_FLT` | YES FAULT/NO FAULT | Fan control high fault. |  |  |  |
| `HighFanCtrl` | ON/OFF | Fan relays. Some engines have a two-speed electric fan, which the PCM controls through LFC and HFC outputs to the fan relays. The high speed relay is a normally open for all models. However, the low speed fan relay is normally closed for Escort and Tracer, and normally open for Probe. When the fan is off: • HFC should read OFF. • LFC should read OFF for Probe and ON for Escort and Tracer. With the low speed fan on: • HFC should read OFF. • LFC should read ON for Probe and OFF for Escort or Tracer. When the high speed fan is on: • HFC should read ON. • LFC should read OFF for all models. | LFC=LO FAN, HFC=HI FAN |  |  |
| `HO2S mon ready` | YES/NO | Indicate whether the heated oxygen sensor (HO2S monitor has successfully completed and reads as follows: • YES if the monitor completed • NO if it did not | HO2S MONITOR |  | Description text may be missing a closing parenthesis. |
| `HO2S MONITOR` | YES/NO | Indicate whether the heated oxygen sensor (HO2S monitor has successfully completed and reads as follows: • YES if the monitor completed • NO if it did not | HO2S mon ready |  | Description text may be missing a closing parenthesis. |
| `HO2S11(mA)` | 0 to 1000 mA) | Actual current in the heated oxygen sensor (HO2S heater circuit in milli-amps. The two-digit numbers after HTR in the parameter refer to the HO2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HO2S12(mA), HO2S21(mA), HO2S22(mA) |  | Description text may be missing a closing parenthesis. |
| `HO2S12(mA)` | 0 to 1000 mA) | Actual current in the heated oxygen sensor (HO2S heater circuit in milli-amps. The two-digit numbers after HTR in the parameter refer to the HO2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HO2S11(mA), HO2S21(mA), HO2S22(mA) |  | Description text may be missing a closing parenthesis. |
| `HO2S21(mA)` | 0 to 1000 mA) | Actual current in the heated oxygen sensor (HO2S heater circuit in milli-amps. The two-digit numbers after HTR in the parameter refer to the HO2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HO2S11(mA), HO2S12(mA), HO2S22(mA) |  | Description text may be missing a closing parenthesis. |
| `HO2S22(mA)` | 0 to 1000 mA) | Actual current in the heated oxygen sensor (HO2S heater circuit in milli-amps. The two-digit numbers after HTR in the parameter refer to the HO2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HO2S11(mA), HO2S12(mA), HO2S21(mA) |  | Description text may be missing a closing parenthesis. |
| `HPRC_ENA` | ON/OFF | Heater pump command. |  |  |  |
| `HPRC_F` | YES FAULT/NO FAULT | Heater pump status. |  |  |  |
| `HTR11` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11A, HTR12A, HTR12, HTR21A, HTR21, HTR22A, HTR22 |  |  |
| `HTR11A` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11, HTR12A, HTR12, HTR21A, HTR21, HTR22A, HTR22 |  |  |
| `HTR11F` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11Fault, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |  |
| `HTR11Fault` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |  |
| `HTR12` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11A, HTR11, HTR12A, HTR21A, HTR21, HTR22A, HTR22 |  |  |
| `HTR12A` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11A, HTR11, HTR12, HTR21A, HTR21, HTR22A, HTR22 |  |  |
| `HTR12F` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11Fault, HTR11F, HTR12Fault, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |  |
| `HTR12Fault` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11Fault, HTR11F, HTR12F, HTR21Fault, HTR21F, HTR22Fault, HTR22F |  |  |
| `HTR13` | ON/OFF | Heated exhaust gas oxygen sensor heater (bank 1, sensor 3). |  |  |  |
| `HTR21` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11A, HTR11, HTR12A, HTR12, HTR21A, HTR22A, HTR22 |  |  |
| `HTR21A` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11A, HTR11, HTR12A, HTR12, HTR21, HTR22A, HTR22 |  |  |
| `HTR21F` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR22Fault, HTR22F |  | Also listed under: PCM. |
| `HTR21F` | YES FAULT/NO FAULT | Heater control for O2S21 fault. |  |  | Also listed under: PCM. |
| `HTR21Fault` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21F, HTR22Fault, HTR22F |  |  |
| `HTR22` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11A, HTR11, HTR12A, HTR12, HTR21A, HTR21, HTR22A |  |  |
| `HTR22A` | ON/OFF | O2S heaters state. The state should be the same as for the corresponding HTRX data parameter; both should read ON or OFF at the same time. • HTR11/HTR11A = bank 1 upstream O2S, should read the same as HTRX1 • HTR12/HTR12A = bank 1 downstream O2S, should the same as HTRX2 • HTR21/HTR21A = bank 2 upstream O2S, should read the same as HTRX1 • HTR22/HTR22A = bank 2 downstream O2S, should read the same as HTRX2 | HTR11A, HTR11, HTR12A, HTR12, HTR21A, HTR21, HTR22 |  |  |
| `HTR22F` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22Fault |  |  |
| `HTR22Fault` | YES/NO | PCM detects a fault in the oxygen sensor (O2S) heater circuit. Reads YES when a fault is present. The two-digit numbers after HTR in the parameter refer to the O2S position: • 11—Bank 1, Upstream • 12—Bank 1, Downstream • 21—Bank 2, Upstream • 22—Bank 2, Downstream | HTR11Fault, HTR11F, HTR12Fault, HTR12F, HTR21Fault, HTR21F, HTR22F |  |  |
| `HTR23` | ON/OFF | Heated exhaust gas oxygen sensor heater (bank 2, sensor 3). |  |  |  |
| `HTRCM11` | not available | Heater current monitor for heated exhaust oxygen sensor 11. |  |  |  |
| `HTRCM12` | not available | Heater current monitor for heated exhaust oxygen sensor 12. |  |  |  |
| `HTRCM21` | not available | Heater current monitor for heated exhaust oxygen sensor 21. |  |  |  |
| `HTRCM22` | not available | Heater current monitor for heated exhaust oxygen sensor 22. |  |  |  |
| `HTREVAL_DC_MODE1` | YES/NO | Heated exhaust oxygen sensor heater system evaluated. |  |  |  |
| `HTRX1` | ON/OFF | PCM-command status for the upstream (HTRX1) or downstream (HTRX2) O2S heater. ON means the heater is on. | HTRX2 |  |  |
| `HTRX2` | ON/OFF | PCM-command status for the upstream (HTRX1) or downstream (HTRX2) O2S heater. ON means the heater is on. | HTRX1 |  |  |
| `IAC(%)` |  |  |  |  | Range not specified. No description available. |
| `IAC=IDLE AIR(%)` | 0 to 100% | PCM output command to the idle air bypass solenoid. The reading displays as a percentage value and corresponds to the IAC valve amperage (currenT and voltages shown below. IAC(%) and IAC=IDLE AIR(%) readings: PERCENTAGE READING \| IAC VALVE VOLTAGE \| IAC VALVE AMPERAGE 10% \| 1 V \| 0.1 A 50% \| 5 V \| 0.5 A 80% \| 8 V \| 0.8 A 100% \| 10–12 V \| 1.0 A The idle air control (IAC) solenoid is used to regulate the amount of air that bypasses the throttle at idle, and thus the idle speed. A lower number indicates the valve is open for a shorter period, providing less idle air. A higher number indicates that the valve is open longer, providing more idle air. An adaptive IAC strategy is used by the PCM to compensate for wear, vacuum leaks, and throttle plate coking. When engine conditions meet IAC adaptive strategy learning requirements, the PCM monitors and determines the values for ideal idle calibration. These values are stored in PCM memory as a correction factor for controlling idle speed. |  |  | Description text may be missing a closing parenthesis. |
| `IACDTCY(%)` | not available | Idle air control valve duty cycle. |  |  |  |
| `IACF` | not available | Idle air control fault. |  |  |  |
| `IACFault` | YES/NO |  |  |  | No description available. |
| `IAC_CELL0` | not available | Airflow time learned (Drive, A/C, OFF). |  |  |  |
| `IAC_CELL1` | not available | Airflow time learned (Drive, A/C, ON). |  |  |  |
| `IAC_CELL2` | not available | Airflow time learned (Neutral, A/C, On.) |  |  |  |
| `IAC_CELL3` | not available | Airflow time learned (Neutral, A/C, OFF.) |  |  |  |
| `IAC_FLT` | YES FAULT/NO FAULT | Indicate whether the PCM detects a fault in the idle air control (IAC system and reads YES if a fault is present. |  |  | Description text may be missing a closing parenthesis. |
| `IAC_MODE` | not available | Idle air control. |  |  |  |
| `IAC_TRIM` | not available | Short term airflow trim before KAM learned. |  |  |  |
| `IAT` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT(°C), IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |  |
| `IAT(V)` | 0.0 to 5.0 V | Voltage signal from the intake air temperature sensor. A 5 V reference signal is applied to the IAT. As temperature increases, the sensor resistance decreases, which provides a variable intake air voltage signal to the PCM. | IAT2(V) |  |  |
| `IAT(°C)` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT, IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |  |
| `IAT(°F)` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT, IAT(°C), IAT_C, IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |  |
| `IAT2` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT, IAT(°C), IAT(°F), IAT_C, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |  |
| `IAT2(V)` | 0.0 to 5.0 V | Voltage signal from the intake air temperature sensor. A 5 V reference signal is applied to the IAT. As temperature increases, the sensor resistance decreases, which provides a variable intake air voltage signal to the PCM. | IAT(V) |  |  |
| `IAT2_TEMP` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT, IAT(°C), IAT(°F), IAT_C, IAT2, IAT_T, IAT2_VOLTS |  |  |
| `IAT2_VOLTS` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT, IAT(°C), IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT_T |  |  |
| `IAT=ACT(V)` | 0.0 to 5.0 V | Intake air temperature (IAT) sensor signal as temperature or voltage. The IAT is a thermistor that is typically installed in the air cleaner. A 5 V reference signal is applied to the IAT. As temperature increases, the sensor resistance decreases, which provides a variable intake air voltage signal to the PCM. The PCM converts IAT voltage signals to temperature readings. IAT readings, along with MAF or MAP, are primary parameters used by the PCM to calculate the amount of air entering the engine, which is critical for determining the amount of fuel required. The IAT reading should be close to the ambient air temperature on a cold engine and should rise steadily as the engine warms up. Readings on a warm engine differ greatly between vehicles because of under-hood temperature variations and hot-soak conditions. |  |  |  |
| `IAT=ACT(°C)` | -40 to 199°C |  |  |  | No description available. |
| `IAT=ACT(°F)` | -40 to 389°F |  |  |  | No description available. |
| `IAT_C` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT, IAT(°C), IAT(°F), IAT2, IAT2_TEMP, IAT_T, IAT2_VOLTS |  |  |
| `IAT_FAULT` | YES FAULT/NO FAULT | Indicates the inlet air temperature status. |  |  |  |
| `IAT_T` | 0 to 255°C or °F | Intake air temperature. The IAT2 sensor provides a manifold air temperature signal to the PCM and is located in the intake manifold. | IAT, IAT(°C), IAT(°F), IAT_C, IAT2, IAT2_TEMP, IAT2_VOLTS |  |  |
| `ICP(V)` | 0.0 to 5.0 V | Injector control pressure (ICP) sensor signal, which indicates absolute pressure for the fuel injection on a diesel engine. The ICP sensor is mounted on the left cylinder bank. The display may be the unconverted voltage signal, or it may be the PCM converted equivalent pressure in psi or megapascal (MPa). On any of these scales, a low reading indicates low injection pressure and a high reading indicates higher injection pressure. |  |  |  |
| `ICP_DES` | not available | Injector control pressure desired. |  |  |  |
| `ICP_PRESS` | not available | Injector control pressure. |  |  |  |
| `IGN ADVANCE(°)` | -90° to +90° | Total spark advance or retard, including the base timing, that the PCM is commanding. | SPARK ADV(°), SPRKADV(°) |  |  |
| `IGN_RUN_PCM` | not available | Ignition key status. |  |  |  |
| `ILINE_V` | not available | Generator command. |  |  |  |
| `IMRC` | ON/OFF | Intake manifold runner control. |  |  |  |
| `IMRC1M(V)` |  |  |  |  | Range not specified. No description available. |
| `IMRC2M(V)` | not available | Position of the intake manifold runner control valves on cylinder Bank 1 (IMRC1M) and Bank 2 (IMRC2M). Depending on the vehicle, a high voltage may mean that the valve is open or that the value is shut. |  |  |  |
| `IMRCFault` | YES/NO |  |  |  | No description available. |
| `IMRCFLT` | YES FAULT/NO FAULT | Indicate whether the PCM detected a fault in the intake manifold runner control system and reads YES when a fault is present. |  |  |  |
| `IMRCM(V)` | not available | Intake manifold runner control monitor. |  |  |  |
| `IMS_SRC` | not available | Intermediate shaft speed. |  | TCM | Also listed under: TCM. |
| `IMTV(%)` | 0 to 100% | PCM-command status for the intake manifold tuning valve. At 100% the valve should be fully open. |  |  |  |
| `IMTV1_PER(%)` | not available | Intake manifold tuning valve bank 1. |  |  |  |
| `IMTV2` | ON/OFF | Intake manifold tuning valve bank 2. |  |  |  |
| `IMTV2_FLT` | YES FAULT/NO FAULT | IMTV 2 status. |  |  |  |
| `IMTV2_PER(%)` | not available | Intake manifold tuning valve bank 2. |  |  |  |
| `IMTVF` | YES FAULT/NO FAULT | Intake manifold tuning valve fault status. |  |  |  |
| `IMTVFault` | YES/NO | Indicates whether the PCM has set an intake manifold tuning valve DTC and reads YES when the PCM set a DTC. |  |  |  |
| `IMTV_FLT` | YES FAULT/NO FAULT |  |  |  | No description available. |
| `INJ` | YES FAULT/NO FAULT | Indicates the injector fault status. |  |  |  |
| `INJ PW1(mS)` | 0 to 99.9) | Length of time in milliseconds (mS) that the PCM commands the fuel injectors to energize. These parameters are used on multiport fuel injection systems. On Electronic Fuel Injection (EFI) systems, the PCM simultaneously pulses half of the fuel injectors at a time. INJ PW1(mS refers to those injectors installed in bank #1, and INJ PW2(mS refers to the injectors in bank #2. Depending on the engine, banks #1 and #2 may be staggered. These parameters read as follows: • A high pulse width indicates more on-time and a richer mixture. • A low pulse width indicates less on-time and a leaner mixture. There are no definite specifications for injector pulse width, but the reading should change as engine speed and load change. On EFI systems, these parameters read as follows: • 1 to 5 mS at idle • About 18 mS at wide open throttle (a little higher for some engines) Since the PCM on SEFI systems pulses each injector individually, the readings represent, at best, the “average” injector pulse width for each bank. Ford SEFI systems use these parameters to allow the PCM to conduct rationality tests on the fuel delivery, the mass airflow (MAF sensor, and the throttle position (TP) sensor. | INJ PW2(mS) |  | Description text may be missing a closing parenthesis. |
| `INJ PW2(mS)` | 0 to 99.9) | Length of time in milliseconds (mS) that the PCM commands the fuel injectors to energize. These parameters are used on multiport fuel injection systems. On Electronic Fuel Injection (EFI) systems, the PCM simultaneously pulses half of the fuel injectors at a time. INJ PW1(mS refers to those injectors installed in bank #1, and INJ PW2(mS refers to the injectors in bank #2. Depending on the engine, banks #1 and #2 may be staggered. These parameters read as follows: • A high pulse width indicates more on-time and a richer mixture. • A low pulse width indicates less on-time and a leaner mixture. There are no definite specifications for injector pulse width, but the reading should change as engine speed and load change. On EFI systems, these parameters read as follows: • 1 to 5 mS at idle • About 18 mS at wide open throttle (a little higher for some engines) Since the PCM on SEFI systems pulses each injector individually, the readings represent, at best, the “average” injector pulse width for each bank. Ford SEFI systems use these parameters to allow the PCM to conduct rationality tests on the fuel delivery, the mass airflow (MAF sensor, and the throttle position (TP) sensor. | INJ PW1(mS) |  | Description text may be missing a closing parenthesis. |
| `INJ1` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |  |
| `INJ10` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9 |  |  |
| `INJ2` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |  |
| `INJ3` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ4, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |  |
| `INJ4` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ3, INJ5, INJ6, INJ7, INJ8, INJ9, INJ10 |  |  |
| `INJ5` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ3, INJ4, INJ6, INJ7, INJ8, INJ9, INJ10 |  |  |
| `INJ6` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ3, INJ4, INJ5, INJ7, INJ8, INJ9, INJ10 |  |  |
| `INJ7` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ8, INJ9, INJ10 |  |  |
| `INJ8` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ9, INJ10 |  |  |
| `INJ9` | YES FAULT/NO FAULT | Injector fault according to injector number. | INJ1, INJ2, INJ3, INJ4, INJ5, INJ6, INJ7, INJ8, INJ10 |  |  |
| `INJnFault` | YES/NO | Reads YES when the PCM has detected a fault, such as an open or short, in the injector circuit. The “n” in INJnFault is a variable from 1 to 10, depending on the indicated cylinder. |  |  |  |
| `InjPrsReg(%)` | 0 to 100% | On-time of the pulse-width-modulated injector pressure regulator on a diesel engine as a percentage. The PCM controls the fuel volume by varying the injection oil pressure with the injector pressure regulator. The injector pressure regulator is a ground-side controlled 12 V regulator. |  |  |  |
| `INJ_TIM` | not available | Injector timing before top dead center. |  |  |  |
| `InManRunCtrl` | ON/OFF | Intake manifold runner control valve status. Reads as follows: • ON = the valve is open • OFF = the valve is close Normally this valve is closed when the engine speed is below 3000 RPM. |  |  |  |
| `InManRunCtrl(%)` | 0 to 100% | PCM-command status for the intake manifold runner control valve. At 100% the pulse-width-modulated valve should be fully open. |  |  |  |
| `IPR(%)` | not available | Injector control pressure regulator. |  |  |  |
| `ISS(rpm)` | not available | Input shaft speed. |  |  |  |
| `ISS_DIR` | FORWARD/REVERSE | Immediate shaft direction. |  | TCM | Also listed under: TCM. |
| `ISS_F` | YES FAULT/NO FAULT | Intermediate shaft speed reliable. |  | TCM | Also listed under: TCM. |
| `ISS_FAULT` | YES/NO | Reads YES when a fault is detected in the input shaft speed (ISS circuit. |  |  | Description text may be missing a closing parenthesis. |
| `ITS1` | GEAR1/NOT GEAR1 | 1st gear indicator. |  |  |  |
| `ITS2` | NOT GEAR/GEAR | 2nd gear indicator. |  |  |  |
| `ITSD` | DRIVE/NOT DRIVE | Drive status. |  |  |  |
| `ITSR` | REVERSED/NOT REVERSED | Reverse indicator. |  |  |  |
| `IVS` | YES/NO | Idle validation switch (IVS state, which verifies that the accelerator pedal (AP) is in the idle position on diesel powertrain control systems. The IVS provides a check on the AP sensor. The MIL lights if the IVS signal does not match the AP sensor signal. A faulty IVS or AP sensor allows the engine to run at low idle speed only. |  |  | Description text may be missing a closing parenthesis. |
| `KAMFUSE` | ON/FAULT | Keep alive memory power. |  |  |  |
| `KEYPOS` | not available | Ignition key status. |  |  |  |
| `KNOCK_1` | not available | Sensor 1 status. |  |  |  |
| `KNOCK_2` | not available | Sensor 2 status. |  |  |  |
| `LCF_FLT` | YES FAULT/NO FAULT | Fan control low fault. |  |  |  |
| `LFC` | ON/OFF | Fan control low speed. |  |  |  |
| `LFC=LO FAN` | ON/OFF | Fan relays. Some engines have a two-speed electric fan, which the PCM controls through LFC and HFC outputs to the fan relays. The high speed relay is a normally open for all models. However, the low speed fan relay is normally closed for Escort and Tracer, and normally open for Probe. When the fan is off: • HFC should read OFF. • LFC should read OFF for Probe and ON for Escort and Tracer. With the low speed fan on: • HFC should read OFF. • LFC should read ON for Probe and OFF for Escort or Tracer. When the high speed fan is on: • HFC should read ON. • LFC should read OFF for all models. | HFC=HI FAN, HighFanCtrl |  |  |
| `LFCFault` | YES/NO | Reads YES when the PCM has detected a low speed fan control module fault. |  |  |  |
| `LFT1(%)` | not available | Long term fuel trim 1. |  |  |  |
| `LFTRIM 1(%)` | -35% to +35% | The long-term fuel trim (LFTRIM) numbers represent the operation and long-term correction of fuel-metering on a fuel-injected engine. The value shows if the PCM is commanding a rich or a lean mixture. Similar to short-term fuel trim (SFTRIM), the LF TRIM number can range from -35% to +35%, with 000% as the midpoint. A number above zero percent indicates the PCM has commanded a long-term rich mixture correction. A number below zero percent indicates the PCM is commanding a lean mixture. The LF TRIM number follows the short-term fuel (ST FUES) number and makes long-term corrections to the fuel-metering in response to a pattern or trend of short-term fuel changes. Compare LF TRIM numbers to injector on-time. Numbers above zero percent indicate increased on-time. Numbers below zero percent indicate decreased on-time. LF TRIM corrections operate only in closed loop. In open loop, the number goes to a fixed value. | LFTRIM 2(%) |  |  |
| `LFTRIM 2(%)` | -35% to +35% | The long-term fuel trim (LFTRIM) numbers represent the operation and long-term correction of fuel-metering on a fuel-injected engine. The value shows if the PCM is commanding a rich or a lean mixture. Similar to short-term fuel trim (SFTRIM), the LF TRIM number can range from -35% to +35%, with 000% as the midpoint. A number above zero percent indicates the PCM has commanded a long-term rich mixture correction. A number below zero percent indicates the PCM is commanding a lean mixture. The LF TRIM number follows the short-term fuel (ST FUES) number and makes long-term corrections to the fuel-metering in response to a pattern or trend of short-term fuel changes. Compare LF TRIM numbers to injector on-time. Numbers above zero percent indicate increased on-time. Numbers below zero percent indicate decreased on-time. LF TRIM corrections operate only in closed loop. In open loop, the number goes to a fixed value. | LFTRIM 1(%) |  |  |
| `LOAD(%)` | see description | Engine load as calculated by the PCM based on engine speed (RPM), number of cylinders, airflow, and cylinder air charge compared to the theoretical air charge that occurs at standard PCM temperature and pressure (volumetric efficiency). The resulting ratio—called engine load—is expressed as a percentage. The engine load parameters read as follows: • 20–40% = a normal load at idle • Less than 80% = normal driving • More than 100% = under hard acceleration in cold temperatures or high barometric pressure, or on supercharged vehicles | ENG LOAD(%) |  |  |
| `LOAD_FF(%)` | not available | Engine load. |  |  |  |
| `LONGFT1(%)` | not available | Long term fuel trim 1. |  |  |  |
| `LONGFT2(%)` | not available | Long term fuel trim 2. |  |  |  |
| `LOOP` | OPEN/CLSD) | Indicate whether the PCM is operating the engine in open or closed loop and read as follows: • OPEN during warm-up • CLSD when the engine reaches normal operating temperature and the PCM responds to O2S voltage Some failure conditions (many associated with trouble codes) will cause the PCM to return to open-loop operation. Additionally, some vehicles normally return to open-loop operation at idle. This occurs because the O2S cools off at idle, and the PCM returns to open loop because the signal is no longer reliable. To restore closed-loop operation, accelerate and hold engine speed at a high idle to warm the sensor. | OPEN/CLSD LOOP |  |  |
| `LOSSTRT` | YES/NO | Limited operating mode due to the engine no start. |  |  |  |
| `LOS_BRAK` | YES/NO | Limited operating mode due to the regenerative braking system. |  |  |  |
| `LOS_ENG` | YES/NO | Limited operating mode due to the engine. |  |  |  |
| `LOS_EQ` | YES/NO | Limited operating mode due to the E-quizzer. |  |  |  |
| `LOS_ETC` | YES/NO | Limited operating mode due to the electronic throttle control. |  |  |  |
| `LOS_GEN` | YES/NO | Limited operating mode due to the generator. |  |  |  |
| `LOS_HV` | YES/NO | Limited operating mode due to the high voltage battery. |  |  |  |
| `LOS_IPC` | YES/NO | Limited operating mode due to the independent plausibility checker. |  |  |  |
| `LOS_KEY` | YES/NO | This parameter indicates the limited operating mode due to the key position. |  |  |  |
| `LOS_LV` | YES/NO | Limited operating mode due to the 12-Volt battery. |  |  |  |
| `LOS_MOT` | YES/NO | Limited operating mode due to the electric motor. |  |  |  |
| `LOS_OWC` | YES/NO | Limited operating mode due to the one way clutch. |  |  |  |
| `LOS_TCM` | YES/NO | Limited operating mode due to the transmission control module. |  |  |  |
| `LowFanCA` | ON/OFF | Actual state of the commanded output to the fan. It should read the same as the fan monitor and fan command output; that is, all three should be ON or OFF at the same time. |  |  |  |
| `LowFanCtrl` | ON/OFF | State of the low-speed fan control on vehicles with multiple fan speed control. |  |  |  |
| `LowFPumpFault` | YES/NO | Reads YES when the PCM has detected a fault in the low speed fuel pump circuit. |  |  |  |
| `LPC_AMP` | not available | Line pressure. |  |  |  |
| `LT TRIM B1(%)` | -20 to +20% | Long-term fuel trim (LT TRIM B1 or B2 = BANK 1, or BANK 2) numbers represent the operation and long-term correction of fuel-metering on a fuel-injected engine. The value shows if the PCM is commanding a rich or a lean mixture. Similar to short-term fuel trim (“ST TRIM B1(%)”), the LT TRIM parameter ranges from -20% to +20% with 00% as the midpoint: • A number above zero percent indicates the PCM has commanded a long-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a lean mixture. LT TRIM follows the short-term fuel (ST FUES number and makes long-term corrections to the fuel-metering in response to a pattern or trend of short-term fuel changes. Compare LT TRIM numbers to injector on-time. Numbers above zero percent indicate increased on-time. Numbers below zero percent indicate decreased on-time. LT TRIM corrections operate only in closed loop. In open loop, the number goes to a fixed value. | LT TRIM B2(%) |  | Description text may be missing a closing parenthesis. |
| `LT TRIM B2(%)` | -20 to +20% | Long-term fuel trim (LT TRIM B1 or B2 = BANK 1, or BANK 2) numbers represent the operation and long-term correction of fuel-metering on a fuel-injected engine. The value shows if the PCM is commanding a rich or a lean mixture. Similar to short-term fuel trim (“ST TRIM B1(%)”), the LT TRIM parameter ranges from -20% to +20% with 00% as the midpoint: • A number above zero percent indicates the PCM has commanded a long-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a lean mixture. LT TRIM follows the short-term fuel (ST FUES number and makes long-term corrections to the fuel-metering in response to a pattern or trend of short-term fuel changes. Compare LT TRIM numbers to injector on-time. Numbers above zero percent indicate increased on-time. Numbers below zero percent indicate decreased on-time. LT TRIM corrections operate only in closed loop. In open loop, the number goes to a fixed value. | LT TRIM B1(%) |  | Description text may be missing a closing parenthesis. |
| `MAF(gm/S)` | 0 to 255 | Amount of air flowing into the engine in grams per second. It is the same as the MAF=MASS AIR(gm/sec) parameter. Reading should be low at idle and higher at high RPM. | AIRFLOW(g/S) |  |  |
| `MAF(V)` | 0 to 5.0 V | Voltage signal from the mass airflow (MAF sensor to the PCM. It is the same as the MAF=MASS AIR(V parameter. The reading should be low at idle and higher at high RPM. |  |  | Closing parenthesis restored in name. Description text may be missing a closing parenthesis. |
| `MAF=MASS AIR(V)` | 0.5 to 5.0 V |  |  |  | Closing parenthesis restored in name. No description available. |
| `MAFF` | YES FAULT/NO FAULT | Mass air flow status. |  |  |  |
| `MAF_Fault` | YES/NO | Indicate whether a fault has occurred in the MAF circuit. | MASS AIR FLOW_STATUS |  |  |
| `MAF_RATE` | not available | Indicates the mass air flow. |  |  |  |
| `MAN LEV POS` | see description | Position of the gear selector lever: PARK, REV, NEUT, O/D, DRIVE, MAN1, and MAN2. |  |  |  |
| `MAN LEV POS(V)` | see description | Status of the manual lever position (MLP) sensor, which is a ratiometric sensor with six resistors in series. The PCM looks at the voltage for each shift lever position. Typical readings are: • Park = 4.41 V • Reverse = 3.60 V • Neutral = 2.83 V • Drive = 2.09 V • 2nd = 1.37 V • 1st = 0.68 V |  |  |  |
| `MAN VAC` | 0 to 35 “HG | Internally calculated PCM value that indicates engine manifold vacuum as determined by inputs from various system sensors. Values should equal actual vacuum gauge readings. |  |  |  |
| `MAP(Hz)` | see description | Manifold absolute pressure (MAP) sensor outputs frequency signal, which is relative to intake manifold vacuum. The MAP sensor frequency increases as vacuum decreases. MAP(Hz) reads as follows: • 80 Hz at 101.6 kPa (30 inHG of manifold vacuum • 159 Hz at zero manifold vacuum The PCM uses the MAP signal to determine engine load and regulate the air-fuel ratio, ignition timing, and EGR flow, and to compensate for altitude. |  |  | Description text may be missing a closing parenthesis. |
| `MAP(psi)` | 0 to 2368 psi | Manifold absolute pressure on turbocharged diesel powertrain systems in pounds per square inch (psi). The PCM uses the MAP sensor signal to decrease fuel during acceleration until reaching a specified boost pressure. This decreases tailpipe smoke. |  |  |  |
| `MAP2` | not available | Manifold absolute pressure sensor. |  |  |  |
| `MAP_DI2` | not available | Manifold absolute pressure sensor status. |  |  |  |
| `MAP_F` | YES FAULT/NO FAULT | Manifold absolute pressure sensor status. |  |  |  |
| `MAP_PCM` | not available | Manifold absolute pressure sensor. |  | ABS | Also listed under: ABS. |
| `MAP_V` | not available | Manifold absolute pressure sensor. |  |  |  |
| `MASS AIR FLOW_STATUS` | YES/NO | Indicate whether a fault has occurred in the MAF circuit. | MAF_Fault |  |  |
| `MASS AIR(gm/sec)` | 0 to 255 | Display the MAF sensor output in volts on EEC-IV systems with DCL. They display a value in grams per second calculated by the PCM based on the MAF sensor signal on EEC-V (OBD-II) systems. The MAF sensor, which is located between the air cleaner and the throttle body, measures the mass of air flowing to the engine. The PCM uses this value to calculate the injector pulse width needed to obtain an optimal air-fuel ratio. MAF sensor readings should be low at idle and should increase as the throttle opens. |  |  |  |
| `MASTERKEY` | PRESENT/NOT PRESENT | Master key present status. |  |  |  |
| `MAT` | not available | Input signal from the manifold air temperature sensor to the PCM on diesel powertrain systems. The PCM uses this signal and other sensor signals to determine fuel injection rate. |  |  |  |
| `MECP_ENA` | ON/OFF | Motor electronics coolant pump. |  |  |  |
| `MECP_F` | YES FAULT/NO FAULT | Motor electronics coolant pump status. |  |  |  |
| `MECT_V(V)` | not available | Motor electronics coolant temperature. |  |  |  |
| `MFC` | ON/OFF | Medium fan control |  |  |  |
| `MFC_FLT` | YES FAULT/NO FAULT | Medium fan control status. |  |  |  |
| `MFDES` | 0 to 255 mG | Internal PCM calculation of the mass fuel desired (MFDES per stroke of the injection pump to operate the engine under the current speed, load, and temperature conditions on diesel powertrain systems. The readings are in milligrams (mG: • 8 to 10 mg at idle • About 60 mg at full throttle |  |  | Description text may be missing a closing parenthesis. |
| `MGP` | -10 to 30 psi) | Manifold gauge pressure (MGP). |  |  |  |
| `MIL` | ON/OFF |  |  |  | No description available. |
| `MILFault` | YES/NO | Indicate whether a fault has occurred in the MIL circuit: • ON = the MIL is lit • OFF = the MIL is off |  |  |  |
| `MIL_FLT` | YES FAULT/NO FAULT | MIL driver fault. |  |  |  |
| `MIN_KEY` | not available | Minimum number of keys required. |  |  |  |
| `MISFEVAL_DC_MODE1` | YES/NO | Misfire monitor evaluation is complete. |  |  |  |
| `MISFire` | YES/NO | Indicates whether a misfire has occurred. Crankshaft acceleration is measured for each cylinder firing event. Should the acceleration drop below a specified threshold, a misfire is deemed to have occurred. |  |  |  |
| `MISF_EVAL` | YES/NO | Misfire monitor evaluated. |  |  |  |
| `MPLRN` | YES/NO | Learned status of the misfire wheel. Reads as follows: • YES if the PCM has learned and stored the misfire wheel profile • NO if the profile has not been learned |  |  |  |
| `MTQ_OUT_ENG` | not available | Total number of misfires. |  |  |  |
| `M_DPFE` | not available | EGR sensor input at the time of a misfire. |  |  |  |
| `M_IAT` | not available | Intake air temperature (IAT at the time of a misfire. |  |  | Description text may be missing a closing parenthesis. |
| `M_LOAD(%)` | not available | Engine load at the time of a misfire. |  |  |  |
| `M_PNP` | not available | Park/Neutral Position (PNP) at the time of a misfire. |  |  |  |
| `M_RPM` | not available | Engine speed at the time of a misfire. |  |  |  |
| `M_RUN` | not available | Engine running time at the time of a misfire. |  |  |  |
| `M_SDN` | YES/NO | Electronic motor shutdown request. |  |  |  |
| `M_SOAK` | not available | Engine-off soak time in minutes prior to a misfire. |  |  |  |
| `M_TP` | not available | Throttle position at the time of a misfire. |  |  |  |
| `M_TRIP` | not available | Number of trips since the time of a misfire. |  |  |  |
| `M_VSS` | not available | Vehicle speed at the time of a misfire. |  |  |  |
| `M_WHEEL` | not available | Learned misfire correction profile at the time of a misfire. |  |  |  |
| `NM` | not available | Total number of misfires. |  |  |  |
| `NUMKEYS` | not available | Number of keys stored in the module. |  | PATS | Also listed under: PATS. |
| `N_KEYCODE` | not available | Number of keys stored. |  |  |  |
| `O2 B1-S1(V)` | 0 to 1.800 V | Oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V: • 0.800 V or higher signal indicates a rich exhaust • 0.200 V or lower signal indicates a lean exhaust An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, while EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have additional oxygen sensors after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS. This means EEC-V vehicles with dual exhaust have four sensors: • B1-S1—The upstream sensor for bank 1 • B1-S2—The downstream sensor for bank 1 • B2-S1—The upstream sensor for bank 2 • B2-S2—The downstream sensor for bank 2 | O2 B1-S2(V), O2 B2-S1(V), O2 B2-S2(V) |  | Description text may be missing a closing parenthesis. |
| `O2 B1-S2(V)` | 0 to 1.800 V | Oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V: • 0.800 V or higher signal indicates a rich exhaust • 0.200 V or lower signal indicates a lean exhaust An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, while EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have additional oxygen sensors after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS. This means EEC-V vehicles with dual exhaust have four sensors: • B1-S1—The upstream sensor for bank 1 • B1-S2—The downstream sensor for bank 1 • B2-S1—The upstream sensor for bank 2 • B2-S2—The downstream sensor for bank 2 | O2 B1-S1(V), O2 B2-S1(V), O2 B2-S2(V) |  | Description text may be missing a closing parenthesis. |
| `O2 B2-S1(V)` | 0 to 1.800 V | Oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V: • 0.800 V or higher signal indicates a rich exhaust • 0.200 V or lower signal indicates a lean exhaust An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, while EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have additional oxygen sensors after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS. This means EEC-V vehicles with dual exhaust have four sensors: • B1-S1—The upstream sensor for bank 1 • B1-S2—The downstream sensor for bank 1 • B2-S1—The upstream sensor for bank 2 • B2-S2—The downstream sensor for bank 2 | O2 B1-S1(V), O2 B1-S2(V), O2 B2-S2(V) |  | Description text may be missing a closing parenthesis. |
| `O2 B2-S2(V)` | 0 to 1.800 V | Oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V: • 0.800 V or higher signal indicates a rich exhaust • 0.200 V or lower signal indicates a lean exhaust An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, while EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have additional oxygen sensors after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS. This means EEC-V vehicles with dual exhaust have four sensors: • B1-S1—The upstream sensor for bank 1 • B1-S2—The downstream sensor for bank 1 • B2-S1—The upstream sensor for bank 2 • B2-S2—The downstream sensor for bank 2 | O2 B1-S1(V), O2 B1-S2(V), O2 B2-S1(V) |  | Description text may be missing a closing parenthesis. |
| `O2OEVAP` | YES/NO | Indicates whether the EVAP purge leak check monitor has completed. • YES indicates the leak check was performed and the PCM can run a purge monitor idle test. • NO indicates that the purge monitor is being prevented because the leak check failed. |  |  |  |
| `O2OTEST` | YES/NO | Indicates whether the minimum soak time has elapsed to enable an EVAP monitor test. • YES indicates the purge monitor test is enabled. • NO indicates the purge monitor is being prevented due to insufficient soak time. |  |  |  |
| `O2S1(mV)` | 0 to 1800 | The oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V (0 to 1000 mV. • A high millivolt signal indicates a rich exhaust. • A low signal indicates a lean exhaust. An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have an additional O2S after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS). This means EEC-V vehicles with dual exhaust have four sensors: • O2S11 — The upstream sensor for bank 1 • O2S12 — The downstream sensor for bank 1 • O2S21 — The upstream sensor for bank 2 • O2S22 — The downstream sensor for bank 2. | O2S2(mV), O2S11(mV), O2S12(mV), O2S21(mV), O2S22(mV) |  | Description text may be missing a closing parenthesis. |
| `O2S11(mV)` | 0 to 1800 | The oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V (0 to 1000 mV. • A high millivolt signal indicates a rich exhaust. • A low signal indicates a lean exhaust. An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have an additional O2S after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS). This means EEC-V vehicles with dual exhaust have four sensors: • O2S11 — The upstream sensor for bank 1 • O2S12 — The downstream sensor for bank 1 • O2S21 — The upstream sensor for bank 2 • O2S22 — The downstream sensor for bank 2. | O2S1(mV), O2S2(mV), O2S12(mV), O2S21(mV), O2S22(mV) |  | Description text may be missing a closing parenthesis. |
| `O2S11FM` | YES FAULT/NO FAULT | Heated exhaust oxygen sensor (bank1, sensor 1 status. |  |  | Description text may be missing a closing parenthesis. |
| `O2S12(mV)` | 0 to 1800 | The oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V (0 to 1000 mV. • A high millivolt signal indicates a rich exhaust. • A low signal indicates a lean exhaust. An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have an additional O2S after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS). This means EEC-V vehicles with dual exhaust have four sensors: • O2S11 — The upstream sensor for bank 1 • O2S12 — The downstream sensor for bank 1 • O2S21 — The upstream sensor for bank 2 • O2S22 — The downstream sensor for bank 2. | O2S1(mV), O2S2(mV), O2S11(mV), O2S21(mV), O2S22(mV) |  | Description text may be missing a closing parenthesis. |
| `O2S12(V)` | not available | Heated exhaust oxygen sensor (bank 1, sensor 2). |  |  |  |
| `O2S13(V)` | not available | Heated exhaust oxygen sensor (bank 1, sensor 3). |  |  |  |
| `O2S2(mV)` | 0 to 1800 | The oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V (0 to 1000 mV. • A high millivolt signal indicates a rich exhaust. • A low signal indicates a lean exhaust. An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have an additional O2S after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS). This means EEC-V vehicles with dual exhaust have four sensors: • O2S11 — The upstream sensor for bank 1 • O2S12 — The downstream sensor for bank 1 • O2S21 — The upstream sensor for bank 2 • O2S22 — The downstream sensor for bank 2. | O2S1(mV), O2S11(mV), O2S12(mV), O2S21(mV), O2S22(mV) |  | Description text may be missing a closing parenthesis. |
| `O2S21(mV)` | 0 to 1800 | The oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V (0 to 1000 mV. • A high millivolt signal indicates a rich exhaust. • A low signal indicates a lean exhaust. An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have an additional O2S after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS). This means EEC-V vehicles with dual exhaust have four sensors: • O2S11 — The upstream sensor for bank 1 • O2S12 — The downstream sensor for bank 1 • O2S21 — The upstream sensor for bank 2 • O2S22 — The downstream sensor for bank 2. | O2S1(mV), O2S2(mV), O2S11(mV), O2S12(mV), O2S22(mV) |  | Description text may be missing a closing parenthesis. |
| `O2S21(V)` | LEAN/RICH | Heated exhaust oxygen sensor (bank 2, sensor 1. |  |  | Description text may be missing a closing parenthesis. |
| `O2S21FM` | YES FAULT/NO FAULT | Heated exhaust oxygen sensor (bank 2, sensor 1. |  |  | Description text may be missing a closing parenthesis. |
| `O2S22(mV)` | 0 to 1800 | The oxygen sensors (O2SS) are the primary sensors used to determine if the engine is running rich or lean. The O2S generate a voltage signal ranging from 0 to a little over 1 V (0 to 1000 mV. • A high millivolt signal indicates a rich exhaust. • A low signal indicates a lean exhaust. An O2S must be hot (above 500°F/260°C), and the PCM must be operating in closed loop before the PCM responds to the sensor signal. EEC-IV V-type engines have separate sensors for the left and right banks, EEC-IV transverse-mounted engines have separate sensors for front and rear. EEC-V systems have an additional O2S after each catalyst. Ford calls these downstream sensors catalyst monitor sensors (CMS). This means EEC-V vehicles with dual exhaust have four sensors: • O2S11 — The upstream sensor for bank 1 • O2S12 — The downstream sensor for bank 1 • O2S21 — The upstream sensor for bank 2 • O2S22 — The downstream sensor for bank 2. | O2S1(mV), O2S2(mV), O2S11(mV), O2S12(mV), O2S21(mV) |  | Description text may be missing a closing parenthesis. |
| `O2S22(V)` | LEAN/RICH | Heated exhaust gas oxygen sensor (bank 2, sensor 2). |  |  |  |
| `O2S23(V)` | not available | Heated exhaust gas oxygen sensor (bank 2, sensor 3). |  |  |  |
| `O2SEVAL_DC_MODE1` | YES/NO | Oxygen sensor monitor evaluated. |  |  |  |
| `OCT ADJ` | YES/NO | Status of the octane adjust shorting bar, which is used to retard spark, and read as follows: • ON when the shorting bar is removed • OFF at all other times NOTE: Removing the shorting bar retards spark about 3 degrees. | OCTADJ |  |  |
| `OCTADJ` | YES/NO | Status of the octane adjust shorting bar, which is used to retard spark, and read as follows: • ON when the shorting bar is removed • OFF at all other times NOTE: Removing the shorting bar retards spark about 3 degrees. | OCT ADJ |  |  |
| `OCTANE` | not available | Octane of the fuel. |  |  |  |
| `OCTANE ADJ` | ON/OFF |  |  |  | No description available. |
| `ODS(rpm)` | not available | Overdrive drum speed sensor. |  |  |  |
| `OFMFLG` | YES FAULT/NO FAULT | Pressure control failure mode. |  | TCM | Also listed under: TCM. |
| `OPEN/CLSD LOOP` | OPEN/CLSD) | Indicate whether the PCM is operating the engine in open or closed loop and read as follows: • OPEN during warm-up • CLSD when the engine reaches normal operating temperature and the PCM responds to O2S voltage Some failure conditions (many associated with trouble codes) will cause the PCM to return to open-loop operation. Additionally, some vehicles normally return to open-loop operation at idle. This occurs because the O2S cools off at idle, and the PCM returns to open loop because the signal is no longer reliable. To restore closed-loop operation, accelerate and hold engine speed at a high idle to warm the sensor. | LOOP |  |  |
| `OSFMFLG` | YES FAULT/NO FAULT | Output-shaft speed failure mode. |  |  |  |
| `OSS(rpm)` | not available | Output shaft speed. |  |  |  |
| `OSS=TSS(RPM)` | 0 to vehicle max | PCM calculated RPM value for the transmission turbine shaft. The turbine speed sensor (TSS is a voltage-generating magnetic pickup. The processor converts the voltage signal of the TSS to an RPM value. |  |  | Description text may be missing a closing parenthesis. |
| `OSS_DIR` | FORWARD/REVERSE | Output shaft direction. |  | TCM | Also listed under: TCM. |
| `OSS_F` | YES FAULT/NO FAULT | EGR motor position desired. |  | TCM | Also listed under: TCM. |
| `OSS_FAULT` | YES/NO | Reads YES when a fault is detected in the output shaft speed (OSS circuit. |  |  | Description text may be missing a closing parenthesis. |
| `OTEMP_FMFLG` | YES FAULT/NO FAULT | Transmission over temperature. |  | TCM | Also listed under: TCM. |
| `OUTCODE_BIT` | not available | Indicate the outcode status. | OUT_CODE |  |  |
| `OutShftSp(RPM)` |  | PCM calculated RPM value for the transmission output shaft. The output shaft speed sensor (OSS is a voltage-generating magnetic pickup. The processor converts the voltage signal of the OSS to an RPM value. |  |  | Range not specified. Description text may be missing a closing parenthesis. |
| `OUT_CODE` | not available | Indicate the outcode status. | OUTCODE_BIT |  |  |
| `OverdriveCancel` | NOT DEPRESSED/DEPRESSED | Overdrive cancel switch/Hold switch. |  | TCM | Also listed under: TCM. |
| `P/S PRESS(HIGH)` | YES/NO | Status of the power steering pressure (PSP) switch, which closes under high pressure and read as follows: • YES when the steering wheel is turned right or left to full lock • NO at all other times If it does not change between NO and YES as the wheel turns through its full range, there may be a problem with the switch or other component in the power steering system. | PwrStrPrs |  |  |
| `PARK/NEU POS` | P-N—/-R-DS | Indicate whether an automatic transmission is in park or neutral or in one of the drive ranges. These parameters read as follows: • P-N— if the transmission is in either park or neutral • -R-DL if the transmission is in any forward gear or reverse The park/neutral switch is a grounding switch that is closed in park or neutral and open in any forward gear or reverse. | ParkNeuPos |  |  |
| `ParkBrakeApply` | YES/NO | State of the parking brake applied switch on diesel powertrain control systems. The parking brake applied switch is located under the instrument panel. ParkBrakeApply reads as follows: • YES with the brake applied • NO with the brake not applied This switch disables speed control when it is applied and it also prevents operation of the power take off (PTO) when it is not applied. |  |  |  |
| `ParkNeuPos` | P-N—/-R-DS | Indicate whether an automatic transmission is in park or neutral or in one of the drive ranges. These parameters read as follows: • P-N— if the transmission is in either park or neutral • -R-DL if the transmission is in any forward gear or reverse The park/neutral switch is a grounding switch that is closed in park or neutral and open in any forward gear or reverse. | PARK/NEU POS |  |  |
| `PARK_INPT` | PARK/NOT PARK | Park position input. |  |  |  |
| `PATSENABL` | ENABLE/DISABLE | Vehicle enable status. |  |  |  |
| `PATS_TYPE` | TIMED/CODED | Security access type. |  |  |  |
| `PBA` | ON/OFF | Parking brake switch status. |  |  |  |
| `PCA` | not available | Pressure control solenoid A. |  | TCM | Also listed under: TCM. |
| `PCAA` | not available | Pressure control solenoid A. |  | TCM | Also listed under: TCM. |
| `PCA_FAULT` | YES/NO | Read YES when a fault is detected in pressure control (PC solenoids A, D, E, F, and G, respectively. | PCD_FAULT, PCE_FAULT, PCF_FAULT, PCG_FAULT |  | Description text may be missing a closing parenthesis. |
| `PCA_FLT2` | NO FAULT/FAULT | Pressure control solenoid A. |  |  |  |
| `PCB` | not available | Pressure control solenoid B. |  | TCM | Also listed under: TCM. |
| `PCBA` | not available | Pressure control solenoid B. |  | TCM | Also listed under: TCM. |
| `PCBFLT` | NO FAULT/FAULT | Pressure control solenoid B fault state. |  |  |  |
| `PCC` | not available | Pressure control solenoid C. |  | TCM | Also listed under: TCM. |
| `PCCA` | not available | Pressure control solenoid C. |  | TCM | Also listed under: TCM. |
| `PCC_FLT` | NO FAULT/FAULT | Shift solenoid pressure control C. |  |  |  |
| `PCD` | not available | Shift solenoid pressure control D. |  | TCM | Also listed under: TCM. |
| `PCD_AMP` | not available | Shift solenoid pressure control D. |  | TCM | Also listed under: TCM. |
| `PCD_FAULT` | YES/NO | Read YES when a fault is detected in pressure control (PC solenoids A, D, E, F, and G, respectively. | PCA_FAULT, PCE_FAULT, PCF_FAULT, PCG_FAULT |  | Description text may be missing a closing parenthesis. |
| `PCD_FLT` | NO FAULT/FAULT | SSPCD status. |  | TCM | Also listed under: TCM. |
| `PCE` | not available | Shift solenoid pressure control E. |  | TCM | Also listed under: TCM. |
| `PCE_FAULT` | YES/NO | Read YES when a fault is detected in pressure control (PC solenoids A, D, E, F, and G, respectively. | PCA_FAULT, PCD_FAULT, PCF_FAULT, PCG_FAULT |  | Description text may be missing a closing parenthesis. |
| `PCE_FLT` | NO FAULT/FAULT | Shift solenoid pressure control E. |  |  |  |
| `PCF` | not available | Line pressure control. |  | TCM | Also listed under: TCM. |
| `PCF_FAULT` | YES/NO | Read YES when a fault is detected in pressure control (PC solenoids A, D, E, F, and G, respectively. | PCA_FAULT, PCD_FAULT, PCE_FAULT, PCG_FAULT |  | Description text may be missing a closing parenthesis. |
| `PCF_FLT` | NO FAULT/FAULT | LPC status. |  | TCM | Also listed under: TCM. |
| `PCG` | not available | Converter pressure control. |  | TCM | Also listed under: TCM. |
| `PCG_FAULT` | YES/NO | Read YES when a fault is detected in pressure control (PC solenoids A, D, E, F, and G, respectively. | PCA_FAULT, PCD_FAULT, PCE_FAULT, PCF_FAULT |  | Description text may be missing a closing parenthesis. |
| `PCG_FLT` | NO FAULT/FAULT | Torque converter clutch status. |  | TCM | Also listed under: TCM. |
| `PCLowOSM` | ON/OFF | Low speed engine cooling fan monitor. |  |  |  |
| `PERDEL1(%)` | not available | Percent delta for cylinder 1. |  |  |  |
| `PERDEL2(%)` | not available | Percent delta for cylinder 2. |  |  |  |
| `PERDEL3(%)` | not available | Percent delta for cylinder 3. |  |  |  |
| `PERDEL4(%)` | not available | Percent delta for cylinder 4. |  |  |  |
| `PERDEL5(%)` | not available | Percent delta for cylinder 5. |  |  |  |
| `PERDEL6(%)` | not available | Percent delta for cylinder 6. |  |  |  |
| `PERDEL7(%)` | not available | Percent delta for cylinder 7. |  |  |  |
| `PERDEL8(%)` | not available | Percent delta for cylinder 8. |  |  |  |
| `PEVAP_TMP(°C/°F)(V)` | not available | Post evaporator temperature sensor input. |  |  |  |
| `PFE(V)` | 0.25 to 4.75 V | Pressure feedback EGR (PFE sensor signal voltage, which determines exhaust system pressure. The PCM uses this signal to compute optimum EGR flow. PFE systems control EGR flow rate by monitoring pressure drop across a remotely located sharp-edged orifice. Typically, sensor output should be as described below. PFE sensor outputs: inHg \| kPa \| VOLTS 3.70 \| 12.5 \| 4.75 2.79 \| 9.42 \| 4.38 1.85 \| 6.25 \| 4.0 0.94 \| 3.17 \| 3.82 0 \| 0 \| 3.25 -5.03 \| -17.0 \| 1.22 -7.40 \| -25.0 \| 0.25 The PFE pressure transducer is a feedback device. Pressure is varied by valve modulation. The vacuum output of the EGR vacuum regulator (EVR) solenoid modulates the valve. With PFE, the EGR valve serves as a pressure regulator, rather than a metering device. |  |  | Description text may be missing a closing parenthesis. |
| `PinInputLevel` | ON/OFF | Profile ignition pick-up. |  |  |  |
| `PIP` | YES/NO | Status of the profile ignition pickup (PIP) and reads as follows: • YES when the PIP is switching between low and high voltage • NO when there are no PIP signal transitions |  |  |  |
| `PNP` | NEUTRAL/DRIVE | Neutral switch circuit. |  |  |  |
| `PNP_ONLY` | NEUTRAL/DRIVE | Park/neutral position switch. |  |  |  |
| `PS1` | OPEN/CLOSED | Pressure switch 1. |  |  |  |
| `PSP` | HIGH/LOW |  |  |  | No description available. |
| `PSP(KPA/PSI)` | not available | Power steering pressure switch status. | PSP(V) |  |  |
| `PSP(V)` | not available | Power steering pressure switch status. | PSP(KPA/PSI) |  |  |
| `PSR` | ON/OFF | Power sustained relay commanded. |  |  |  |
| `PTO` | ON/OFF | Power take off status. |  |  |  |
| `PwrStrPrs` | YES/NO | Status of the power steering pressure (PSP) switch, which closes under high pressure and read as follows: • YES when the steering wheel is turned right or left to full lock • NO at all other times If it does not change between NO and YES as the wheel turns through its full range, there may be a problem with the switch or other component in the power steering system. | P/S PRESS(HIGH) |  |  |
| `PwTakeOff` | ON/OFF | Status of the driver-operated power take off (PTO) switch on diesel powertrain control systems. |  |  |  |
| `RCAM` | -15° to +45° | Actual position of the camshaft in relation to the crankshaft in degrees of rotation and reads as follows: • Near -15° with the engine running at a warm idle, camshaft fully advanced with no EGR • Near 40° with a warm engine and the vehicle operating at a steady cruise speed |  |  |  |
| `RCL_F` | YES FAULT/NO FAULT | Cooling fan state. |  | TCM | Also listed under: TCM. |
| `RCL_FAULT` | YES/NO | Reads YES when a fault is detected in the reverse control lamp (RCS circuit. |  |  | Description text may be missing a closing parenthesis. |
| `RESUME` | ON/OFF | Speed control actuator switch RESUME. |  |  |  |
| `REV_SW` | ON/OFF | Reverse switch. |  |  |  |
| `RPM` | 0 to engine max | Engine speed and is always shown at the left of the top line on the display. RPM is internally computed by the PCM based on ignition reference pulses or a crankshaft sensor. |  |  |  |
| `RPMDES` | 0 to engine max | Desired engine speed as calculated by the PCM for base idle. This reading should always be close to actual idle RPM. |  |  |  |
| `SBB_SS2` | ON/OFF | Shift solenoid 2. |  |  |  |
| `SC VAC SW` | ON/OFF | PCM output commands to the speed control (SC vacuum and vent solenoids, which control the speed control servo. The SC VAC SW and SC VENT SW readings have the following relationships with throttle position control: S/C VAC SW and S/C VENT SW readings and throttle position: SC VAC SW \| SC VENT SW \| THROTTLE ON \| OFF \| Accelerate OFF \| ON \| Decelerate OFF \| OFF \| Steady | SC VENT SW |  | Description text may be missing a closing parenthesis. |
| `SC VENT SW` | ON/OFF | PCM output commands to the speed control (SC vacuum and vent solenoids, which control the speed control servo. The SC VAC SW and SC VENT SW readings have the following relationships with throttle position control: S/C VAC SW and S/C VENT SW readings and throttle position: SC VAC SW \| SC VENT SW \| THROTTLE ON \| OFF \| Accelerate OFF \| ON \| Decelerate OFF \| OFF \| Steady | SC VAC SW |  | Description text may be missing a closing parenthesis. |
| `SCBC` | ON/OFF | Super charger bypass control. |  |  |  |
| `SCBCF` | YES FAULT/NO FAULT | Super charger bypass control fault. |  |  |  |
| `SCCS` | see description | Position of the driver-operated speed control command switch (SCCS on the steering wheel. The SCCS parameter displays several range states according to which SCCS button is pressed: • OFF • COAST • ACCEL • RESUME • ON Two hyphens (– –), the default range state, indicates that none of the speed control switch buttons are pressed. |  |  | Description text may be missing a closing parenthesis. |
| `SCCS(V)` | 0 to 10.00 | Speed control command switch (SCCS voltage. |  |  | Description text may be missing a closing parenthesis. |
| `SCCS_CANCEL` | ON/OFF | Speed control cancel switch status. |  |  |  |
| `SCCS_NULL` | ON/OFF | Speed control no switch input status. |  |  |  |
| `SCCS_OFF_PID` | ON/OFF | Speed control actuator switch off status. |  |  |  |
| `SCCS_ON_PID` | ON/OFF | Speed control actuator switch on status. |  |  |  |
| `SCCS_RESUME` | ON/OFF | Speed control actuator switch RESUME status. |  |  |  |
| `SCCS_TAP_DN` | ON/OFF | Speed control actuator switch coast status. |  |  |  |
| `SCCS_TAP_UP` | ON/OFF | Speed pressure actuator switch SET/ACCEL status. |  |  |  |
| `SCC_AMP` | not available | Shift solenoid pressure control C. |  |  |  |
| `SCICP` | ON/OFF | Super charger intercooler pump. |  |  |  |
| `SCICPF` | YES FAULT/NO FAULT | Supercharger intercooler pump fault. |  |  |  |
| `SCP_PARK_BRAKE` | ON/OFF | Parking brake applied status. |  |  |  |
| `SC_OFF` | ON/OFF | Speed control actuator switch off status. |  |  |  |
| `SC_ON` | ON/OFF | Speed control actuator switch on status |  |  |  |
| `SecAIR mon rdy` | YES/NO | Secondary air monitor status and reads YES when the monitor successfully completed. |  |  |  |
| `SEC_ACCES` | Denied/In Progress/GrantED | Vehicle security access status. |  |  |  |
| `SERV_MOD` | YES/NO | PATS service module status. |  |  |  |
| `SET_ACL` | ON/OFF | Speed control actuator switch SET/ACCEL. |  |  |  |
| `SFT1` | not available | Short term fuel trim 1. |  |  |  |
| `SFT2` | not available | Short term fuel trim 2. |  |  |  |
| `SFTRIM 1(%)` | -25% to +35% | The short-term fuel (ST FUES numbers are short-term fuel-metering corrections on a fuel-injected engine. It indicates whether the PCM is commanding a rich or a lean mixture. The SFTRIM parameter ranges from -25% to +35% with 000% as the midpoint: • A number above zero percent indicates the PCM has commanded a short-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a short-term lean mixture correction. The ST FUEL number leads the long-term fuel (LT FUES number. When a pattern or trend of short-term corrections to fuel-metering occur, long-term fuel (LT FUES responds with a similar correction. Compare ST FUEL numbers to injector on-time. Numbers above zero indicate increased on-time, below zero indicates decreased on-time. The ST FUEL corrections operate only in closed loop. In open loop, it goes to a fixed value. | SFTRIM 2(%) |  | Description text may be missing a closing parenthesis. |
| `SFTRIM 2(%)` | -25% to +35% | The short-term fuel (ST FUES numbers are short-term fuel-metering corrections on a fuel-injected engine. It indicates whether the PCM is commanding a rich or a lean mixture. The SFTRIM parameter ranges from -25% to +35% with 000% as the midpoint: • A number above zero percent indicates the PCM has commanded a short-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a short-term lean mixture correction. The ST FUEL number leads the long-term fuel (LT FUES number. When a pattern or trend of short-term corrections to fuel-metering occur, long-term fuel (LT FUES responds with a similar correction. Compare ST FUEL numbers to injector on-time. Numbers above zero indicate increased on-time, below zero indicates decreased on-time. The ST FUEL corrections operate only in closed loop. In open loop, it goes to a fixed value. | SFTRIM 1(%) |  | Description text may be missing a closing parenthesis. |
| `SHFT_TYP` | not available | Indicates the shift type. |  | TCM | Also listed under: TCM. |
| `ShiftSol1` | ON/OFF | PCM commands for the 1, 2, and 3 shift solenoids and read ON when the PCM has commanded the shift solenoid to energize. | ShiftSol2, ShiftSol3 |  |  |
| `ShiftSol1A` | ON/OFF | Shift solenoid 1, 2, and 3 feedback status to the PCM. When the solenoid circuits and PCM function properly, the command parameter, such as ShiftSol1, and the feedback parameter, such as ShiftSol1A, should both display ON or OFF. | ShiftSol2A, ShiftSol3A |  |  |
| `ShiftSol1Fault` | YES/NO | Read YES when a shift solenoid circuit fault is present. | ShiftSol2Fault, ShiftSol3Fault |  |  |
| `ShiftSol2` | ON/OFF | PCM commands for the 1, 2, and 3 shift solenoids and read ON when the PCM has commanded the shift solenoid to energize. | ShiftSol1, ShiftSol3 |  |  |
| `ShiftSol2A` | ON/OFF | Shift solenoid 1, 2, and 3 feedback status to the PCM. When the solenoid circuits and PCM function properly, the command parameter, such as ShiftSol1, and the feedback parameter, such as ShiftSol1A, should both display ON or OFF. | ShiftSol1A, ShiftSol3A |  |  |
| `ShiftSol2Fault` | YES/NO | Read YES when a shift solenoid circuit fault is present. | ShiftSol1Fault, ShiftSol3Fault |  |  |
| `ShiftSol3` | ON/OFF | PCM commands for the 1, 2, and 3 shift solenoids and read ON when the PCM has commanded the shift solenoid to energize. | ShiftSol1, ShiftSol2 |  |  |
| `ShiftSol3A` | ON/OFF | Shift solenoid 1, 2, and 3 feedback status to the PCM. When the solenoid circuits and PCM function properly, the command parameter, such as ShiftSol1, and the feedback parameter, such as ShiftSol1A, should both display ON or OFF. | ShiftSol1A, ShiftSol2A |  |  |
| `ShiftSol3Fault` | YES/NO | Read YES when a shift solenoid circuit fault is present. | ShiftSol1Fault, ShiftSol2Fault |  |  |
| `SHRTFT1(%)` | not available | Short term fuel trim 1. |  |  |  |
| `SHRTFT2(%)` | not available | Short term fuel trim 2. |  |  |  |
| `SLIP_DES_SCP` | not available | Torque converter slip desired in rpm. |  | TCM | Also listed under: TCM. |
| `SPAREKEY_PCM` | ENABLED/DISABLED | Spare key programming status. |  |  |  |
| `SPARK ADV(°)` | -90° to +90° | Total spark advance or retard, including the base timing, that the PCM is commanding. | SPRKADV(°), IGN ADVANCE(°) |  |  |
| `SPARKADV(°)` | -54 to +54 degrees | Amount of spark advance in degrees. An advance of more than 54 degrees shows as a negative value; this is normal. |  |  |  |
| `SPARKDUR1` | not available | Spark duration of the indicated cylinder. | SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |  |
| `SPARKDUR2` | not available | Spark duration of the indicated cylinder. | SPARKDUR1, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |  |
| `SPARKDUR3` | not available | Spark duration of the indicated cylinder. | SPARKDUR1, SPARKDUR2, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |  |
| `SPARKDUR4` | not available | Spark duration of the indicated cylinder. | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR5, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |  |
| `SPARKDUR5` | not available | Spark duration of the indicated cylinder. | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR6, SPARKDUR7, SPARKDUR8 |  |  |
| `SPARKDUR6` | not available | Spark duration of the indicated cylinder. | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR7, SPARKDUR8 |  |  |
| `SPARKDUR7` | not available | Spark duration of the indicated cylinder. | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR8 |  |  |
| `SPARKDUR8` | not available | Spark duration of the indicated cylinder. | SPARKDUR1, SPARKDUR2, SPARKDUR3, SPARKDUR4, SPARKDUR5, SPARKDUR6, SPARKDUR7 |  |  |
| `SPARK_ACT` | not available | Spark advance actual. |  |  |  |
| `SPRKADV(°)` | -90° to +90° | Total spark advance or retard, including the base timing, that the PCM is commanding. | SPARK ADV(°), IGN ADVANCE(°) |  |  |
| `SRC_CAN` | ENABLE/DISABLE | Starter motor relay enable status. |  |  |  |
| `SSA_AMP` | not available | Shift solenoid pressure control A. |  | TCM | Also listed under: TCM. |
| `SSA_SS1` | ON/OFF | Shift solenoid 1. |  |  |  |
| `SSA_SS1_FLT` | YES FAULT/NO FAULT | Shift solenoid 1 status. |  |  |  |
| `SSB_AMP` | not available | Soft solenoid pressure control B. |  | TCM | Also listed under: TCM. |
| `SSB_SS2_FLT` | YES FAULT/NO FAULT | Shift solenoid 2 status. |  |  |  |
| `SSC(%)` | not available | Shift solenoid pressure control C. |  |  |  |
| `SSC_SS3` | ON/OFF | Shift solenoid 3. |  |  |  |
| `SSC_SS3_FLT` | _YES FAULT/NO FAULT | Shift solenoid 3 status. |  |  |  |
| `SSD(%)` | not available | Shift solenoid pressure control D. |  |  |  |
| `SSD_SS4` | ON/OFF | Shift solenoid 4. |  |  |  |
| `SSE(%)` | not available | Shift solenoid pressure control E. |  |  |  |
| `SSPCA` | not available | Shift solenoid pressure control A. |  | TCM | Also listed under: TCM. |
| `SSPCA_FLT` | NO FAULT/FAULT | SSPCA status. |  | TCM | Also listed under: TCM. |
| `SSPCB` | not available | Shift solenoid pressure control B. |  | TCM | Also listed under: TCM. |
| `SSPCB_FLT` | YES FAULT/NO FAULT | SSPCB status. |  | TCM | Also listed under: TCM. |
| `SSPCC` | not available | Shift solenoid pressure control C in KPA/PSI. |  | TCM | Also listed under: TCM. |
| `SSPCC_FLT` | YES FAULT/NO FAULT | SSPCC status. |  | TCM | Also listed under: TCM. |
| `ST TRIM B1(%)` | -100% to +100% | The short-term fuel (ST TRIM) numbers are short-term fuel-metering corrections for a fuel-injected engine. It indicates whether the PCM is commanding a rich or a lean mixture. Similar to long-term fuel trim (“LT TRIM B1(%)”), the ST TRIM parameter ranges from -100% to +100% with 000% as the midpoint: • A number above zero percent indicates the PCM has commanded a short-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a short-term lean mixture correction. The ST TRIM number leads the long-term fuel (LT TRIM) number. When a pattern or trend of short-term corrections to fuel-metering occur, long-term fuel (LT TRIM) responds with a similar correction. Compare ST TRIM numbers to injector on-time. Numbers above zero indicate increased on-time, below zero indicates decreased on-time. The ST TRIM corrections operate only in closed loop. In open loop, it goes to a fixed value. | ST TRIM B2(%) |  |  |
| `ST TRIM B2(%)` | -100% to +100% | The short-term fuel (ST TRIM) numbers are short-term fuel-metering corrections for a fuel-injected engine. It indicates whether the PCM is commanding a rich or a lean mixture. Similar to long-term fuel trim (“LT TRIM B1(%)”), the ST TRIM parameter ranges from -100% to +100% with 000% as the midpoint: • A number above zero percent indicates the PCM has commanded a short-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a short-term lean mixture correction. The ST TRIM number leads the long-term fuel (LT TRIM) number. When a pattern or trend of short-term corrections to fuel-metering occur, long-term fuel (LT TRIM) responds with a similar correction. Compare ST TRIM numbers to injector on-time. Numbers above zero indicate increased on-time, below zero indicates decreased on-time. The ST TRIM corrections operate only in closed loop. In open loop, it goes to a fixed value. | ST TRIM B1(%) |  |  |
| `STATUS B1` | see description | Operating status of fuel bank 1 and reads as follows: • CL (closed-loop) • OL (open-loop) • OL DRIVE • OL FAULT • CL FAULT |  |  |  |
| `STE=EVP(V)` | 0.30 to 4.50 V | EGR valve position (EVP) sensor signal, which reflects EGR valve pintle position. The EVP sensor is mounted on the EGR valve. The spring-loaded EGR valve is operated by a vacuum signal from the EGR vacuum regulator solenoid. As supply vacuum overcomes spring force, the diaphragm moves. This lifts the pintle off of its seat and allows exhaust gas to recirculate. The amount of flow is proportional to the pintle position. Specifications are model dependant. | EVP(V) |  |  |
| `SYNC_DI` | YES/NO | Indicates if the CMP and CKP are synchronized. |  |  |  |
| `TCC` | Engaged/Modulated/OFF | Torque converter clutch (modulatED. |  | TCM | Description text may be missing a closing parenthesis. Also listed under: TCM. |
| `TCC SOL` | ON/OFF | PCM command status for the torque converter clutch. When the system functions properly, ON means the torque converter clutch solenoid is energized to lock the converter. |  |  |  |
| `TCC SOL(%)` | 0 to 100% | Pulse-width-modulated (PWM) signal to the torque converter clutch (TCC) solenoid and reads as follows: • 0% with exhaust solenoid (TCC solenoid) closed, TCC not applied • 100% with exhaust solenoid fully open, TCC applied |  |  |  |
| `TCC(%)` | 0 to 100% | Pulse-width-modulated (PWM) signal to the torque converter clutch (TCC) solenoid and reads as follows: • 0% with exhaust solenoid (TCC solenoid) closed, TCC not applied • 100% with exhaust solenoid fully open, TCC applied |  |  |  |
| `TCCA` | ON/OFF | Torque converter clutch actual (TCCA) feedback signal to the PCM indicating the status of the torque converter clutch. Normally, the TCC SOL and TCCA parameters should both read ON or OFF simultaneously. |  |  |  |
| `TCCA(V)` | not available | Voltage signal to the torque converter clutch (TCC) solenoid. Information on this parameter is not available at this time. |  |  |  |
| `TCCF` | YES FAULT/NO FAULT | Torque converter clutch fault status. |  |  |  |
| `TCCFault` | YES/NO | Presence of a PCM detected fault in the torque converter clutch circuit and reads YES when a fault is present. |  |  |  |
| `tccmact (RPM)` | 0 to vehicle max | PCM calculated value of torque converter slippage. The value is derived by subtracting the turbine speed from the engine RPM. TCCMACT(RPM) reads less than 50 RPM when TCC(%), TCC SOL(%) reads 95% or greater or when TCC SOL reads ON. | TCCMACT(RPM) |  |  |
| `TCCMACT(RPM)` | 0 to vehicle max | PCM calculated value of torque converter slippage. The value is derived by subtracting the turbine speed from the engine RPM. TCCMACT(RPM) reads less than 50 RPM when TCC(%), TCC SOL(%) reads 95% or greater or when TCC SOL reads ON. | tccmact (RPM) |  |  |
| `TCC_FLT` | YES FAULT/NO FAULT | Torque converter clutch fault. |  | TCM | Also listed under: TCM. |
| `TCC_OSC` | not available | Output state control of the torque converter. |  | TCM | Also listed under: TCM. |
| `TCIL` | ON/OFF | Transmission Control Indicator Lamp (TCIS) command status. TCIL reads ON when the TCIL in the dash or shifter should be on. |  | TCM | Also listed under: TCM. |
| `TCILFault` | YES/NO | Indicate if a fault has occurred in the Transmission Control Indicator Lamp circuit. | TCIL_FAULT, TCIL_FLT |  |  |
| `TCIL_FAULT` | YES/NO | Indicate if a fault has occurred in the Transmission Control Indicator Lamp circuit. | TCILFault, TCIL_FLT |  |  |
| `TCIL_FLT` | YES/NO | Indicate if a fault has occurred in the Transmission Control Indicator Lamp circuit. | TCILFault, TCIL_FAULT | TCM | Also listed under: TCM. |
| `TCS` | DEPRESSED/NOT DEPRESSED | Overdrive cancel switch/hold switch. |  | TCM | Also listed under: TCM. |
| `TCSLAMP` | ON/OFF | Condition of the transmission control switch (TCS) and read as follows: • OFF = the switch is open • ON = the driver requests overdrive cancellation and the switch closes | TrnCtrlSw |  |  |
| `TC_SLIP` | not available | Torque converter slip actual. |  | TCM | Also listed under: TCM. |
| `TFT(V)(°C/°F)` | not available | Transmission fluid temperature. |  |  |  |
| `TFT=TOT(V)` | 0 to 5.10 V | Transmission fluid temperature (TFT), which is also referred to by Ford as the transmission oil temperature (TOT, as voltage. The TFT sensor is a thermistor that changes resistance in response to temperature changes. Resistance decreases as temperature increases to provide a variable voltage signal to the PCM. TFT=TOT(V) typically reads as follows: • 0.60 V at 194°F (90°C) • 3.88 V at 32°F (0°C) |  |  | Description text may be missing a closing parenthesis. |
| `THEFT_LMP` | YES FAULT/NO FAULT | Antitheft indicator lamp control. |  |  |  |
| `ThrPosMODE` | see description | Throttle position (TP) as calculated by the PCM based on the TP sensor signal and read as follows: • C/T - closed throttleat idle and during deceleration • P/T - part throttle at cruise or during moderate acceleration • WOT - wide open throttle at de-choke on crank, A/C cutout, or during maximum acceleration | TP MODE |  |  |
| `THTRC(%)` | not available | Thermostat heater control. |  |  |  |
| `THTRCF` | YES FAULT/NO FAULT | Thermostat heater control fault. |  |  |  |
| `TIRESIZE` | not available | Tire size. |  |  |  |
| `TorqConvCl` | ON/OFF | Output used by the PCM to control the converter clutch by way of a solenoid. ON or OFF displays as the PCM commands the solenoid on or off. |  |  |  |
| `TORQUE` | not available | PCM calculated engine torque in Nm or ft-lb on diesel powertrain control systems. The torque reading indicates how much load is placed on the engine. |  | TCM | Also listed under: TCM. |
| `TORQUE(lb-fT)` | 0 to 999 | Engine torque as calculated by the PCM. |  |  |  |
| `TOTF` | YES FAULT/NO FAULT | Transmission fluid temperature status. |  | TCM | Also listed under: TCM. |
| `TOWHAUL` | ON/OFF | Tow haul switch. |  | TCM | Also listed under: TCM. |
| `TP MODE` | PT/WOT/CT | Position of the accelerator pedal based on accelerator position sensor readings on diesel powertrain systems. |  |  | Also listed under: PCM. |
| `TP MODE` | see description | Throttle position (TP) as calculated by the PCM based on the TP sensor signal and read as follows: • C/T - closed throttleat idle and during deceleration • P/T - part throttle at cruise or during moderate acceleration • WOT - wide open throttle at de-choke on crank, A/C cutout, or during maximum acceleration | ThrPosMODE |  | Also listed under: PCM. |
| `TP(V)` | 0.0 to 5.0 V | Voltage signal from the throttle position (TP) sensor to the PCM. |  |  | Closing parenthesis restored in name. |
| `TP1` | not available | Throttle position sensor 1. |  |  |  |
| `TP2` | not available | Throttle position sensor 2. |  |  |  |
| `TP=TPS(%)` | (range 0 to 100% | Throttle position (TP) as calculated by the PCM based on TP sensor voltage on models with an autoranging TP sensor. The PCM resets the 0 to 100% range in relation to TP sensor voltage as it senses new minimum and maximum voltages. |  |  |  |
| `TP=TPS(V)` | 0 to 5.1 V | Throttle position (TP) sensor voltage signal, which is in proportion to the throttle position. The TP signal allows the PCM to determine throttle opening: low voltage at closed throttle and high voltage at wide open throttle. TP=TPS(V reads as follows: • About 0.5 V at idle • 4.5 V at WOT |  |  | Description text may be missing a closing parenthesis. |
| `TPCT(V)` | not available | Throttle position closed throttle (TPCT indicates the most recent throttle position (TP) sensor voltage reading at closed throttle. This parameter updates each time the throttle is fully closed. | TPREL(V) |  | Description text may be missing a closing parenthesis. |
| `TPF` | YES/NO | Throttle position sensor status. |  |  |  |
| `TPREL(V)` | not available | Throttle position closed throttle (TPCT indicates the most recent throttle position (TP) sensor voltage reading at closed throttle. This parameter updates each time the throttle is fully closed. | TPCT(V) |  | Description text may be missing a closing parenthesis. |
| `TP_FAULT` | YES FAULT/NO FAULT | Throttle position sensor status. |  |  |  |
| `TQ_CNTL` | not available | Torque fuel/spark limiting status. |  | TCM | Also listed under: TCM. |
| `TR` | Man1/Man2/D/OD/N/R/P | Transmission range. |  | TCM | Also listed under: TCM. |
| `TR1` | OPEN/CLOSED | Digital transmission range switch numbered. | TR2, TR3, TR4 |  |  |
| `TR2` | OPEN/CLOSED | Digital transmission range switch numbered. | TR1, TR3, TR4 |  |  |
| `TR3` | OPEN/CLOSED | Digital transmission range switch numbered. | TR1, TR2, TR4 |  |  |
| `TR3_MODE` | OPEN/CLOSED | Digital transmission range switch 3. |  |  |  |
| `TR4` | OPEN/CLOSED | Digital transmission range switch numbered. | TR1, TR2, TR3 |  |  |
| `TR=GEAR` | see description | Displays the driver demanded gear selection from the transmission range sensor and reads as follows: • MAN1 • MAN2 • DRIVE • ODRIVE • REVERSE • PARK/NEUTRAL |  |  |  |
| `TRAC_ACT` | ON/OFF | Traction control system modulating status. |  |  |  |
| `TRANSMIT` | ON/OFF | Anti-theft RF enabled and transmit command status. |  |  |  |
| `TransRange_D` | see description | Four possible switch states of the transmission range switches TR1 through TR4 and reads as follows: • 0 indicates the switch is closed • 1 indicates the switch is open Typical readings are as follows • PARK POSITION = 0000 • Between P & R = 0100 • REVERSE POSITION = 1100 • Between R & N = 0100 • NEUTRAL POSITION = 0110 • Between N & O/D = 1100 • O/D POSITION = 1111 • Between O/D & 2 = 1011 • MANUAL 2 POSITION = 1001 • Between 1 & 2 = 1011 • MANUAL 1 POSITION = 0011 |  |  |  |
| `TRAN_OT_FAULT` | YES/NO | Reads YES when a fault is detected in the trans over temp circuit. |  |  |  |
| `TRFM` | YES FAULT/NO FAULT | Transmission range status. |  | TCM | Also listed under: TCM. |
| `TRIM B1-S1(%)` | -55% to +55% | The trim bank x – sensor x (TRIM Bx-Sx numbers are short-term fuel-metering corrections for a fuel-injected engine. It indicates whether the PCM is commanding a rich or a lean mixture. The TRIM parameters range from -45% to +35% with 000% as the midpoint: • A number above zero percent indicates the PCM has commanded a short-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a short-term lean mixture correction. The TRIM Bx-Sx number leads the long-term fuel (LT TRIM) number. When a pattern or trend of short-term corrections to fuel-metering occur, long-term fuel (LT TRIM) responds with a similar correction. Compare ST TRIM numbers to injector on-time. Numbers above zero indicate increased on-time, below zero indicates decreased on-time. The ST TRIM corrections operate only in closed loop. In open loop, it goes to a fixed value. TRIM B1-S1 should read the same as ST TRIM B1. TRIM B2-S1 should read the same as ST TRIM B2. | TRIM B2-S1(%) |  | Description text may be missing a closing parenthesis. |
| `TRIM B2-S1(%)` | -55% to +55% | The trim bank x – sensor x (TRIM Bx-Sx numbers are short-term fuel-metering corrections for a fuel-injected engine. It indicates whether the PCM is commanding a rich or a lean mixture. The TRIM parameters range from -45% to +35% with 000% as the midpoint: • A number above zero percent indicates the PCM has commanded a short-term rich mixture correction. • A number below zero percent indicates the PCM is commanding a short-term lean mixture correction. The TRIM Bx-Sx number leads the long-term fuel (LT TRIM) number. When a pattern or trend of short-term corrections to fuel-metering occur, long-term fuel (LT TRIM) responds with a similar correction. Compare ST TRIM numbers to injector on-time. Numbers above zero indicate increased on-time, below zero indicates decreased on-time. The ST TRIM corrections operate only in closed loop. In open loop, it goes to a fixed value. TRIM B1-S1 should read the same as ST TRIM B1. TRIM B2-S1 should read the same as ST TRIM B2. | TRIM B1-S1(%) |  | Description text may be missing a closing parenthesis. |
| `TRIP` | YES/NO | Indicates whether an On-Board diagnostic trip has occurred. A trip is a complete ignition on, engine run, ignition off cycle that tests all components and systems on an OBD-II vehicle. |  |  |  |
| `TRIP CounT` | 0 to 255 | Number of completed OBD-II drive cycles completed since the last DTC P1000 monitor readiness set. |  |  |  |
| `TRIP_CNT` | not available | Number of on board diagnostic tripe completed. |  |  |  |
| `TrnCtrlIndLamp` | ON/OFF | Displays ON and an indicator lamp is lit when “Overdrive Cancel” is requested. |  |  |  |
| `TrnCtrlSw` | ON/OFF | Condition of the transmission control switch (TCS) and read as follows: • OFF = the switch is open • ON = the driver requests overdrive cancellation and the switch closes | TCSLAMP |  |  |
| `TrnFluidTmp(V)` | 0.0 to 5.0 V | Voltage signal from the transmission fluid temperature (TFT) sensor to the PCM. A low voltage reading indicates a high fluid temperature, while a high voltage reading indicates a low fluid temperature. |  |  |  |
| `TrnRange` | 0.0 to 5.0 V | Voltage at PCM pin 64 to signal return, or ground. This voltage indicates the position of the gear selector lever. Each position has a typical range, as follows: • L = 0.29 to 0.79 V • D2 = 1.29 to 1.53 V • OD = 1.88 to 2.29 V • N = 2.53 to 3.04 V • R = 3.43 to 3.78 V • P = 4.30 to 4.73 V |  |  |  |
| `TRO_N_F` | YES/NO | Reads YES when a fault is detected in the Neutral Output circuit. |  | TCM | Also listed under: TCM. |
| `TRO_N_FAULT` | YES/NO |  |  |  | No description available. |
| `TRO_P_F` | YES/NO | Reads YES when a fault is detected in the Park Output circuit. |  | TCM | Also listed under: TCM. |
| `TRS1(V)` | not available | Transmission range selector 1. |  |  |  |
| `TRS2(V)` | not available | Transmission range selector 2. |  |  |  |
| `TRS3(V)` | not available | Transmission range selector 3. |  |  |  |
| `TR_DC(%)` | not available | Transmission range. |  |  |  |
| `TR_FAULT` | YES/NO | Reads YES when a fault is detected in the Transmission Range (TR) circuit. |  |  |  |
| `TR_FREQ(Hz)` | not available | Transmission range. |  | TCM | Also listed under: TCM. |
| `TR_V` | see description | Voltage at processor pin 64 (signal returN. Voltage for a digital transmission range (DTR) should match the examples below ± 0.3 V. Sample TR_V readings: TR_V READING \| GEAR 0.0 V \| PARK 1.5 V \| REVERSE 1.5 V \| NEUTRAL 1.5 V \| ODRIVE 0.0 V \| MAN2 0.0 V \| MAN1 |  | TCM | Description text may be missing a closing parenthesis. Also listed under: TCM. |
| `TSFMFLG` | YES FAULT/NO FAULT | Turbine speed sensor failure mode. |  | TCM | Also listed under: TCM. |
| `TSLIPRAT` | not available | Transmission slip ratio. |  | TCM | Also listed under: TCM. |
| `TSS(rpm)` | not available | Turbine shaft speed. |  |  |  |
| `TSS_F` | YES FAULT/NO FAULT | Turbine shaft speed reliable. |  | TCM | Also listed under: TCM. |
| `TSS_FAULT` | YES/NO | Reads YES when a fault is detected in the Turbine Shaft Speed (TSS circuit. |  |  | Description text may be missing a closing parenthesis. |
| `TurbSpdS(RPM)` | 0 to 8192 | Speed of the transmission turbine shaft. |  |  |  |
| `UNLIMITED_KEY` | ENABLED/DISABLED | Unlimited key mode status. |  |  |  |
| `VBAT(V)` | not available | This parameter indicates the battery positive voltage. | VBATT_PCM(V) |  |  |
| `VBATT_PCM(V)` | not available | This parameter indicates the battery positive voltage. | VBAT(V) |  |  |
| `VCT 1_FAULT` | YES/NO | Read YES if a fault has occurred in the VCT circuit. | VCT 2_FAULT |  |  |
| `VCT 2_FAULT` | YES/NO | Read YES if a fault has occurred in the VCT circuit. | VCT 1_FAULT |  |  |
| `VCT1` | 7.5 to B+ | Variable valve timing. | VCT2 |  |  |
| `VCT1F` | YES FAULT/NO FAULT | Variable cam timing status. |  |  |  |
| `VCT2` | 7.5 to B+ | Variable valve timing. | VCT1 |  |  |
| `VCT2F` | YES FAULT/NO FAULT | Variable cam timing 2 status. |  |  |  |
| `VCTA` | ON/OFF | Variable cam timing solenoid actual state. |  |  |  |
| `VCTADV` | not available | Variable cam timing actual advance status. |  |  |  |
| `VCTADV2` | not available | Variable cam timing 2 actual advance. |  |  |  |
| `VCTADVERR` | not available | VCT advance error status. |  |  |  |
| `VCTADVERR2` | not available | Variable cam timing 2 advance error. |  |  |  |
| `VCTDC(%)` | not available | Variable camshaft timing duty cycle. |  |  |  |
| `VCTDC2(%)` | not available | Variable cam timing 2 duty cycle. |  |  |  |
| `VCTSYS` | OPEN/CLOSED LOOP | VCT system status. |  |  |  |
| `VehSpdSensr` | 0 to 255 | Vehicle speed in miles per hour or kilometers per hour. |  |  |  |
| `VFC(%)` | not available | Fan speed monitor. |  |  |  |
| `VFCDC(%)` | not available | Variable fan duty cycle. |  |  |  |
| `VFCF` | YES FAULT/NO FAULT | Cooling fan driver fault. |  |  |  |
| `VFDES` | 0 to 255 mm3 | Internal PCM calculation of the volume of fuel desired (VFDES per stroke from the injection pump to operate the engine for current speed, load, and temperature conditions on diesel powertrain systems. Readings are in cubic millimeters (mm3) and 10–15 mm3 at idle. |  |  | Description text may be missing a closing parenthesis. |
| `VGTDC(%)` | not available | Variable geometry turbocharger. |  |  |  |
| `VGT_F` | YES FAULT/NOFAULT | EGR motor position desired. |  |  |  |
| `VGT_FAULT` | YES/NO | reads YES if a fault is detected in the variable geometry turbo (VGT circuit. |  |  | Description text may be missing a closing parenthesis. |
| `VPoWeR` | 0.0 to 24.0 V | Vehicle battery voltage. Some vehicles may normally read lower (8 V or higher (16 V than expected. Verify system voltage with a DVOM. |  |  | Description text may be missing a closing parenthesis. |
| `VPoWeR(V)` | 0 to 25.5 V | Vehicle battery voltage. The powertrain control system has no specific sensor to measure battery voltage, but some PCMs calculate this parameter from a sensing circuit on the supply voltage circuit. The reading should be close to normal charging system regulated voltage, or about 13.5 to 14.5 V, at idle. Check against actual voltage measured at the battery or alternator. This parameter is principally for PCM self-diagnostics. Some PCM functions are modified if voltage is out of range. For example, if voltage drops below a minimum, the PCM may try to recharge the battery by running the engine at a higher idle. This may affect the idle control, fuel metering, and ignition timing parameters. | VPWR=BATT(V) |  |  |
| `VPWR(V)` | not available | Module supply voltage. |  |  |  |
| `VPWR=BATT(V)` | 0 to 25.5 V | Vehicle battery voltage. The powertrain control system has no specific sensor to measure battery voltage, but some PCMs calculate this parameter from a sensing circuit on the supply voltage circuit. The reading should be close to normal charging system regulated voltage, or about 13.5 to 14.5 V, at idle. Check against actual voltage measured at the battery or alternator. This parameter is principally for PCM self-diagnostics. Some PCM functions are modified if voltage is out of range. For example, if voltage drops below a minimum, the PCM may try to recharge the battery by running the engine at a higher idle. This may affect the idle control, fuel metering, and ignition timing parameters. | VPoWeR(V) |  |  |
| `VREF(V)` | 0 to 5.12 V | Reference voltage that system sensors operate on for some vehicles. The PCM monitors this voltage and provides the parameter. Nominal reference voltage is 5.0 V. Depending on system calibration and charging system voltage, readings may vary a few tenths of a volt. Some models normally read about 6.5 V. Verify voltage with a DVOM. |  |  |  |
| `VS SET` | see description | Set speed of the speed control system. |  |  |  |
| `VSFMFLG` | YES FAULT/NO FAULT | Vehicle speed sensor signal status. |  | TCM | Also listed under: TCM. |
| `VSS(KPH)` | 0 to vehicle max | VSS is the measurement provided by the vehicle speed sensor (VSS) pulses to the PCM. The PCM calculates the actual speed. The PCM uses vehicle speed to control fuel injection, ignition timing, and transmission and trans-axle shift timing. | VSS(MPH) |  |  |
| `VSS(MPH)` | 0 to vehicle max | VSS is the measurement provided by the vehicle speed sensor (VSS) pulses to the PCM. The PCM calculates the actual speed. The PCM uses vehicle speed to control fuel injection, ignition timing, and transmission and trans-axle shift timing. | VSS(KPH) |  |  |
| `VSS_FAULT` | YES FAULT/NO FAULT | Vehicle speed sensor reliable. |  |  |  |
| `VS_SRC` | not available | Transfer case speed sensor. |  | TCM | Also listed under: TCM. |
| `V_4X4L` | PRESENT/NOT PRESENT | The 4x4 low range input. |  |  |  |
| `V_OCTADJ` | ACTIVE/INACTIVE | The Octane Adjust/Spark Retard. |  |  |  |
| `V_TCASE` | PRESENT/NOT PRESENT | Manual shift on the fly. |  |  |  |
| `WAC` | ON/OFF | Air conditioning clutch at wide open throttle. |  |  |  |
| `WAC=WOT A/C` | ON/OFF | Indicates whether the PCM is preventing the A/C system from operating due to undesirable engine conditions. These include operation during engine cranking and wide open throttle. On almost all vehicles, WAC=WOT A/C reads as follows: • OFF when the PCM senses undesirable conditions and is currently preventing the A/C clutch from energizing • ON when the PCM is allowing the A/C clutch to energize |  |  |  |
| `WACA` | ON/OFF | Wide open throttle air conditioning cutoff fault. |  |  |  |
| `WACFault` | YES/NO | Reads YES when the PCM has set a wide open throttle A/C cutout fault. |  |  |  |
| `WAC_FLT` | YES FAULT/NO FAULT | Wide open throttle A/C cutoff fault. |  |  |  |
| `WARM_NOMIL` | 0 to vehicle max | Number of warm-ups since the DTCs were cleared. |  |  |  |
| `WASTEGATE` | 0 to 100% | PCM input that indicates the position of the wastegate as a percentage on diesel powertrain control systems. |  |  |  |
| `WFS` | YES/NO | Indicates the water in fuel. |  |  |  |
| `WotA/Ccutoff` | ON/OFF | Reads ON when the wide open throttle (WOT A/C cutoff has been activated and the A/C has been switched off. |  |  | Description text may be missing a closing parenthesis. |
