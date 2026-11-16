# Ford data parameters: Generic Electronic Module (GEM)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

113 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `4WDClutch(%)` | 0 to 100% |  |  |
| `4WDClutchElec` | ON/OFF |  |  |
| `4WDClutchPWM` | ON/OFF |  |  |
| `4WDClutchStat` | ON/OFF |  |  |
| `4WDHigh` | ON/OFF |  |  |
| `4WDLOLED` | ON/OFF |  |  |
| `4WDLow` | ON/OFF |  |  |
| `4WDPlatePwr` | ON/OFF |  |  |
| `4WDPlate_A` | OPEN/CLOSED | PLATE A, 4WDPlate_B, PLATE B, 4WDPlate_C, PLATE C, 4WDPlate_D, PLATE D |  |
| `4WDPlate_B` | OPEN/CLOSED | 4WDPlate_A, PLATE A, PLATE B, 4WDPlate_C, PLATE C, 4WDPlate_D, PLATE D |  |
| `4WDPlate_C` | OPEN/CLOSED | 4WDPlate_A, PLATE A, 4WDPlate_B, PLATE B, PLATE C, 4WDPlate_D, PLATE D |  |
| `4WDPlate_D` | OPEN/CLOSED | 4WDPlate_A, PLATE A, 4WDPlate_B, PLATE B, 4WDPlate_C, PLATE C, PLATE D |  |
| `4WD_FrShftSp` | 0 to 255 MPH | 4WD_RrShftSp |  |
| `4WD_RrShftSp` | 0 to 255 MPH | 4WD_FrShftSp |  |
| `4WD_Switch` | AUTO/4HI/4LOW |  |  |
| `A/C BLndoorPos` | OPEN/CLOSE |  |  |
| `ABagCHIME` | ON/OFF |  |  |
| `ACC DeLaY` | ON/OFF | AccessoryDly |  |
| `AccessoryDly` | ON/OFF | ACC DeLaY |  |
| `ACC_Pos` | YES/NO | RUN_Pos, IGN Run, START_Pos, OFF/LOCK_Pos, IGN O/L |  |
| `ALarm EVent1-8` | see description |  |  |
| `AUTOLMP` | ON/OFF |  |  |
| `BATSAV` | ON/OFF | BattSaver |  |
| `BattSaver` | ON/OFF | BATSAV |  |
| `BOO SW` | ON/OFF | BOO_GEM |  |
| `BOO_GEM` | ON/OFF | BOO SW |  |
| `CCW_Shift_Mtr` | ON/OFF | CW_Shift_Mtr |  |
| `ChimeReq` | ON/OFF | CHIMERQ |  |
| `CHIMERQ` | ON/OFF | ChimeReq |  |
| `ClutchLock` | ACTIVE/INACTV |  |  |
| `COOLANT` | notOK/OK |  |  |
| `CW_Shift_Mtr` | ON/OFF | CCW_Shift_Mtr |  |
| `D DR SW` | CLOSED/OPEN | DECKLID, Dr DR SW, LGATESW, LRDR Switch, LRDoor SWitch, LRDR SW, P DR SW, Pass DR SW, RRDoor SWitch |  |
| `D SBELT` | IN/OUT |  |  |
| `DECKLID` | CLOSED/OPEN | D DR SW, Dr DR SW, LGATESW, LRDR Switch, LRDoor SWitch, LRDR SW, P DR SW, Pass DR SW, RRDoor SWitch |  |
| `DIM DEC` | NotAct/ActivE |  |  |
| `DIM INC` | NotAct/ActivE |  |  |
| `DoorAjarLamp` | ON/OFF | DoorAJR L |  |
| `DoorAJR L` | ON/OFF | DoorAjarLamp |  |
| `DoorLocK` | LOCK/UNLOCKN | DoorUnlock |  |
| `DoorUnlock` | LOCK/UNLOCKN | DoorLocK |  |
| `Dr DR SW` | CLOSED/OPEN | D DR SW, DECKLID, LGATESW, LRDR Switch, LRDoor SWitch, LRDR SW, P DR SW, Pass DR SW, RRDoor SWitch |  |
| `Dr SBELT` | IN/OUT |  |  |
| `DTC CouNT` | 0 to 20 |  |  |
| `FrWiperMd` | see description | RearWiperMd |  |
| `HallPwr` | ON/OFF |  |  |
| `HatchUnlkSw` | ON/OFF | LF_UnlockSw, RF_UnlockSw |  |
| `HDLMPSW` | ON/OFF | HeaDLaMP |  |
| `HeaDLaMP` | ON/OFF | HDLMPSW |  |
| `HORN SW` | ON/OFF |  |  |
| `IGN O/L` | YES/NO | ACC_Pos, RUN_Pos, IGN Run, START_Pos, OFF/LOCK_Pos |  |
| `IGN Run` | YES/NO | ACC_Pos, RUN_Pos, START_Pos, OFF/LOCK_Pos, IGN O/L |  |
| `IllumEntry` | ON/OFF |  |  |
| `KeyInIgn` | ON/OFF |  |  |
| `LFBeltFstnd` | ON/OFF |  |  |
| `LFDoorAjar` | ON/OFF | LRDoorAjar, RFDoorAjar, RR/SlidDr_Ajar, Trunk/Htch_Ajar |  |
| `LFWindowDn` | YES/NO |  |  |
| `LFWindowMotr(A)` | 0 to 64 |  |  |
| `LFWndwPeak(A)` | 0 to 64 |  |  |
| `LF_PwrWndw` | ENABLE/DISABLE |  |  |
| `LF_UnlockSw` | ON/OFF | HatchUnlkSw, RF_UnlockSw |  |
| `LGATESW` | CLOSED/OPEN | D DR SW, DECKLID, Dr DR SW, LRDR Switch, LRDoor SWitch, LRDR SW, P DR SW, Pass DR SW, RRDoor SWitch |  |
| `LR TURN` | ON/OFF | RR TURN |  |
| `LRDoor SWitch` | CLOSED/OPEN | D DR SW, DECKLID, Dr DR SW, LGATESW, LRDR Switch, LRDR SW, P DR SW, Pass DR SW, RRDoor SWitch |  |
| `LRDoorAjar` | ON/OFF | LFDoorAjar, RFDoorAjar, RR/SlidDr_Ajar, Trunk/Htch_Ajar |  |
| `LRDR SW` | CLOSED/OPEN | D DR SW, DECKLID, Dr DR SW, LGATESW, LRDR Switch, LRDoor SWitch, P DR SW, Pass DR SW, RRDoor SWitch |  |
| `LRDR Switch` | CLOSED/OPEN | D DR SW, DECKLID, Dr DR SW, LGATESW, LRDoor SWitch, LRDR SW, P DR SW, Pass DR SW, RRDoor SWitch |  |
| `NeuSafetySw` | ACTIVE/INACTV |  |  |
| `NeuTowFcnLmp` | ON/OFF |  |  |
| `NeutTowFcn` | ENABLE/DISABLE |  |  |
| `NTRL SW` | NotAct/ActivE |  |  |
| `OFF/LOCK_Pos` | YES/NO | ACC_Pos, RUN_Pos, IGN Run, START_Pos, IGN O/L |  |
| `OverSpdWarn` | ENABLE/DISABLE |  |  |
| `P DR SW` | CLOSED/OPEN | D DR SW, DECKLID, Dr DR SW, LGATESW, LRDR Switch, LRDoor SWitch, LRDR SW, Pass DR SW, RRDoor SWitch |  |
| `ParkLmpSw` | ON/OFF |  |  |
| `Pass DR SW` | CLOSED/OPEN | D DR SW, DECKLID, Dr DR SW, LGATESW, LRDR Switch, LRDoor SWitch, LRDR SW, P DR SW, RRDoor SWitch |  |
| `PLATE A` | OPEN/CLOSED | 4WDPlate_A, 4WDPlate_B, PLATE B, 4WDPlate_C, PLATE C, 4WDPlate_D, PLATE D |  |
| `PLATE B` | OPEN/CLOSED | 4WDPlate_A, PLATE A, 4WDPlate_B, 4WDPlate_C, PLATE C, 4WDPlate_D, PLATE D |  |
| `PLATE C` | OPEN/CLOSED | 4WDPlate_A, PLATE A, 4WDPlate_B, PLATE B, 4WDPlate_C, 4WDPlate_D, PLATE D |  |
| `PLATE D` | OPEN/CLOSED | 4WDPlate_A, PLATE A, 4WDPlate_B, PLATE B, 4WDPlate_C, PLATE C, 4WDPlate_D |  |
| `RearDefRly` | ON/OFF |  |  |
| `RearDefSw` | ON/OFF |  |  |
| `RearPosSense` | YES/NO |  |  |
| `RearWasher` | ON/OFF |  |  |
| `RearWASHSW` | ON/OFF |  |  |
| `RearWiper` | ON/OFF |  |  |
| `RearWiperDn` | ON/OFF | RearWiperUp |  |
| `RearWiperMd` | see description | FrWiperMd |  |
| `RearWiperUp` | ON/OFF | RearWiperDn |  |
| `RearWPRUN` | ON/OFF |  |  |
| `RevSel` | ON/OFF |  |  |
| `RFDoorAjar` | ON/OFF | LFDoorAjar, LRDoorAjar, RR/SlidDr_Ajar, Trunk/Htch_Ajar |  |
| `RF_UnlockSw` | ON/OFF | HatchUnlkSw, LF_UnlockSw |  |
| `RR TURN` | ON/OFF | LR TURN |  |
| `RR/SlidDr_Ajar` | ON/OFF | LFDoorAjar, LRDoorAjar, RFDoorAjar, Trunk/Htch_Ajar |  |
| `RRDoor SWitch` | CLOSED/OPEN | D DR SW, DECKLID, Dr DR SW, LGATESW, LRDR Switch, LRDoor SWitch, LRDR SW, P DR SW, Pass DR SW |  |
| `RUN_Pos` | YES/NO | ACC_Pos, IGN Run, START_Pos, OFF/LOCK_Pos, IGN O/L |  |
| `SeatBeltLamp` | ON/OFF |  |  |
| `SpdSensWipr` | ENABLE/DISABLE |  |  |
| `START_Pos` | YES/NO | ACC_Pos, RUN_Pos, IGN Run, OFF/LOCK_Pos, IGN O/L |  |
| `Trunk/Htch_Ajar` | ON/OFF | LFDoorAjar, LRDoorAjar, RFDoorAjar, RR/SlidDr_Ajar |  |
| `VBatt(V)` | 0 to 25 V |  |  |
| `VBatt_GEM(V)` | 0 to 25 V |  |  |
| `VBatt_REM(V)` | 0 to 25 V |  |  |
| `VSS_GEM` | 0 to 120 MPH |  |  |
| `WasherPumpRly` | ON/OFF | WASHRLY |  |
| `WasherPumpSw` | ON/OFF |  |  |
| `WASHRLY` | ON/OFF | WasherPumpRly |  |
| `WEAR OK` | YES/NO |  |  |
| `WFLUID` | LOW/OK |  |  |
| `Wiper2SpdRly` | ON/OFF |  |  |
| `WiperPk-Pk(mS)` | 0 to 255 mS |  |  |
| `WiperRunRly` | ON/OFF |  |  |
