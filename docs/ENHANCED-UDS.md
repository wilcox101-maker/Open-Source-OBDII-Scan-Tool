# Ford Enhanced UDS reference

What this tool sends to Ford modules over UDS (ISO 14229-1 on ISO 15765 CAN), and what it never sends. The tables below are
**generated from the code** (`src/ObdLink.Core/Docs/EnhancedUdsTables.cs`), so they always match what the app does. A test
(`EnhancedUdsDocTests`) fails if they drift; run the tests with `UPDATE_DOCS=1` to regenerate them.

## How to read this

- **Verified** means checked on Dale's 2017 F-150 (or in Ford's own As-Built file for that VIN). Everything else is a
  standard value (ISO / SAE) or community-reported and marked so. Treat community values as a starting point, not a fact.
- **Gated** services change module state. They are refused while read-only mode is on (the default at every start) and
  each app path adds its own checks (typed confirmation, battery check, backup, read-back).
- Proprietary databases and service manuals are not copied here. Ford DID, routine and output catalogs need a
  redistributable source before they ship (see `docs/ROADMAP-FORSCAN-PARITY.md`).
- Request/response framing: a positive response is `SID + 0x40`, a negative one is `7F SID NRC`. Ford physical
  addressing uses 11-bit IDs with response = request + 8.

## Tables

<!-- BEGIN GENERATED: EnhancedUdsTables (do not edit by hand; run the tests with UPDATE_DOCS=1) -->

### Ford modules (2017 F-150 table)

Source: `FordF150_2017.Modules`. Response ID = request ID + 8 (Ford convention). "Log" = FORScan found the module on Dale's truck (presence only). ".ab" = Ford's As-Built file for Dale's VIN lists the module id (confirms the address, not the bus). Bus is a starting guess: the scanner tries both buses.

| Module | Name | Bus | Request | Response | Confidence | Log | .ab | Note |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PCM | Powertrain Control Module | HS-CAN | 0x7E0 | 0x7E8 | Standard | yes | yes | J1979 standard ECU #1 address |
| TCM | Transmission Control Module | HS-CAN | 0x7E1 | 0x7E9 | Unverified |  |  | May be integrated in PCM on some powertrains; not present in Dale's factory .ab |
| ABS | Anti-lock Brake System | HS-CAN | 0x760 | 0x768 | CommunityReported | yes | yes |  |
| RCM | Restraint Control Module | HS-CAN | 0x737 | 0x73F | CommunityReported | yes | yes |  |
| PSCM | Power Steering Control Module | HS-CAN | 0x730 | 0x738 | CommunityReported | yes | yes |  |
| BCM | Body Control Module | HS-CAN | 0x726 | 0x72E | CommunityReported | yes | yes | Reported reachable on HS and MS on some models [UNVERIFIED] |
| IPC | Instrument Panel Cluster | HS-CAN | 0x720 | 0x728 | CommunityReported | yes | yes | Bus [UNVERIFIED] for 2017 |
| APIM | SYNC Accessory Protocol Interface | MS-CAN | 0x7D0 | 0x7D8 | CommunityReported | yes | yes | Bus [UNVERIFIED] |
| ACM | Audio Control Module | MS-CAN | 0x727 | 0x72F | CommunityReported | yes | yes |  |
| HVAC | Climate Control Module | MS-CAN | 0x733 | 0x73B | CommunityReported |  |  | Not present in Dale's factory .ab |
| PAM | Parking Aid Module | HS-CAN | 0x736 | 0x73E | CommunityReported |  |  | Not present in Dale's factory .ab |
| IPMA | Image Processing Module A (camera) | HS-CAN | 0x706 | 0x70E | CommunityReported |  |  | Not present in Dale's factory .ab |
| GWM | Gateway Module | HS-CAN | 0x716 | 0x71E | Unverified | yes | yes |  |
| DDM | Driver Door Module | MS-CAN | 0x740 | 0x748 | CommunityReported | yes | yes |  |
| PDM | Passenger Door Module | MS-CAN | 0x741 | 0x749 | CommunityReported | yes | yes |  |
| SCCM | Steering Column Control Module | HS-CAN | 0x724 | 0x72C | CommunityReported | yes | yes |  |
| TRM | Trailer Module | HS-CAN | 0x791 | 0x799 | CommunityReported |  |  | Was 0x775; changed to 0x791 per the f150forum As-Built spreadsheet (TRM sheet). Not found on Dale's truck; not present in Dale's factory .ab |
| FCIM | Front Controls Interface Module | MS-CAN | 0x7A7 | 0x7AF | Unverified | yes | yes |  |
| DSM | Driver Seat Module | MS-CAN | 0x744 | 0x74C | Unverified |  |  | Not present in Dale's factory .ab |
| GPSM | GPS Module | MS-CAN | 0x701 | 0x709 | Unverified | yes | yes |  |
| SODL | Side Obstacle Detection Left | HS-CAN | 0x7C4 | 0x7CC | Unverified |  |  | Not present in Dale's factory .ab |
| SODR | Side Obstacle Detection Right | HS-CAN | 0x7C6 | 0x7CE | Unverified |  |  | Not present in Dale's factory .ab |
| TCCM | Transfer Case Control Module | HS-CAN | 0x761 | 0x769 | Unverified | yes | yes | 0x761 confirmed by Ford .ab NODEID 761 (part base 7H417); replaces the old 4X4 row |
| RTM | Radio Transceiver Module | HS-CAN | 0x751 | 0x759 | Unverified | yes | yes | 0x751 confirmed by Ford .ab (751-01-01 + NODEID; part base 15K619); bus not confirmed |
| PBM | Park Brake Control Module | HS-CAN | 0x756 | 0x75E | Unverified | yes | yes | 0x756 identified: FORScan profile/config report part FL34-2C496-AK + serial match .ab NODEID 756. Response ID 0x75E assumed (+8) [UNVERIFIED]; bus [UNVERIFIED] |
| OCS | Occupant Classification System Module | HS-CAN | 0x765 | 0x76D | Unverified | yes | yes | 0x765 identified: FORScan profile/config report part HL3B-604A17-AA + serial match .ab NODEID 765. Response ID 0x76D assumed (+8) [UNVERIFIED]; bus [UNVERIFIED] |
| FCDIM | Front Control/Display Interface Module | MS-CAN | 0x7A5 | 0x7AD | CommunityReported | yes | yes | 0x7A5 confirmed by Ford .ab (3 blocks); bus [UNVERIFIED] |

### Services

Source: `UdsServices`. "Gated" = state-changing: refused in read-only mode and, in the console, unless "Allow writes" is on (one table for the client, console and write gate). "Sub-fn" = byte 2 is a sub-function (bit 7 suppresses the positive response).

| SID | Service | Gated | Sub-fn | Used by |
| --- | --- | --- | --- | --- |
| 0x04 | OBD-II clear DTCs (SAE J1979 mode 04) | yes |  | console only |
| 0x08 | OBD-II on-board control (SAE J1979 mode 08) | yes |  | console only |
| 0x10 | DiagnosticSessionControl |  | yes | Routines tab (10 01 / 10 03), session host, flash (10 02) |
| 0x11 | ECUReset | yes | yes | flash engine only (after programming) |
| 0x14 | ClearDiagnosticInformation | yes |  | DTCs tab: Clear module DTCs |
| 0x19 | ReadDTCInformation |  | yes | DTCs tab (19 02 by status; details 19 04 / 19 06) |
| 0x22 | ReadDataByIdentifier |  |  | identify, As-Built read, enhanced PIDs (Live Data, dashboards) |
| 0x23 | ReadMemoryByAddress |  |  | Core only (UdsMemory) |
| 0x27 | SecurityAccess | yes | yes | seed/key plug-ins; flash engine |
| 0x28 | CommunicationControl | yes | yes | Core only (ControlLease, restore 28 00); bench-first |
| 0x2A | ReadDataByPeriodicIdentifier |  |  | Core only (UdsPeriodic) |
| 0x2C | DynamicallyDefineDataIdentifier | yes | yes | Core only (UdsDynamicData) |
| 0x2E | WriteDataByIdentifier | yes |  | As-Built write / restore / undo; dashboard write cards |
| 0x2F | InputOutputControlByIdentifier | yes |  | dashboard output-hold cards (lease + auto-release); Core ControlLease |
| 0x31 | RoutineControl | yes | yes | Routines tab, dashboard routine cards, flash engine (FF00/FF01) |
| 0x34 | RequestDownload | yes |  | flash engine only |
| 0x35 | RequestUpload | yes |  | console only |
| 0x36 | TransferData | yes |  | flash engine only |
| 0x37 | RequestTransferExit | yes |  | flash engine only |
| 0x38 | RequestFileTransfer | yes |  | console only |
| 0x3B | WriteDataByLocalIdentifier (KWP2000) | yes |  | console only |
| 0x3D | WriteMemoryByAddress | yes |  | console only |
| 0x3E | TesterPresent |  | yes | module scan, keep-alive (3E 80), flash preflight |
| 0x85 | ControlDTCSetting | yes | yes | Core only (ControlLease, restore 85 01); bench-first |
| 0x86 | ResponseOnEvent | yes | yes | console only |
| 0x87 | LinkControl | yes | yes | console only |

Restore requests let through a closed gate (`UdsServices.IsRestoreRequest`): `85 01` (DTC setting on), `28 00 xx` (enable Rx/Tx), `2F did 00` (return control to ECU).

### Identification DIDs read by Identify vehicle

Source: `IdentifyOptions.DefaultDids` (ISO 14229-1 Annex C identification range). Read-only: Identify sends only services 0x09, 0x3E and 0x22.

| DID | Meaning in this tool |
| --- | --- |
| 0xF190 | VIN |
| 0xF188 | ECU software (strategy) part |
| 0xF111 | ECU core assembly part |
| 0xF113 | ECU delivery assembly part |
| 0xF124 | Calibration part |
| 0xF18C | ECU serial |
| 0xF17F | Serial / extra id |

### As-Built blocks

Source: `AsBuiltBlock.DidForBlock`. **[UNVERIFIED community convention]**: block N (FORScan numbering, BCD) is DID 0xDE00 + (N - 1). Writes go only through the As-Built tab (length and checksum checks, backup, read-back, history).

| Block | DID |
| --- | --- |
| 01 | 0xDE00 |
| 02 | 0xDE01 |
| 03 | 0xDE02 |
| 09 | 0xDE08 |
| 10 | 0xDE09 |
| 11 | 0xDE0A |

### Enhanced (Mode 22) PIDs built in

Source: `Data/ford-enhanced-pids.json` (the built-in default; users can edit their copy). Value = raw x scale + offset, raw = big-endian data bytes after `62 DID DID` (two's complement when signed). Only rows marked verified were checked on a truck.

| Key | Name | Module | DID | Bytes | Signed | Scale | Offset | Unit | Verified | Source |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| gear | Transmission gear (GEAR_OSC) | 0x7E0 | 0x1E12 | 1 |  | 1 | 0 |  | no | f150ecoboost.net 'custom PIDs for Torque' (221E12, equation A); raw-to-gear mapping is a guess, 10R80 values unknown |
| tft | Transmission fluid temp | 0x7E0 | 0x1E1C | 2 | yes | 0.0625 | 0 | °C | no | torque-bhp.com F-150 2011 (221E1C, ((signed(A)*256)+B)/16 °C); f150forum UltraGauge 2015-2017 6R80 |
| oss | Output shaft speed (OSS) | 0x7E0 / 0x7E1 | 0x1E13 | 2 |  | 0.25 | 0 | rpm | no | UNVERIFIED. Ford PID name OSS / OSS_SRC (Ford expanded PID list). No confirmed community DID for 6R80/10R80 was found: 1E13 is a guess in the 1E1x transmission block (1E12 gear, 1E14 TC slip, 1E15 slip ratio, 1E1C TFT on 6.7L/F-150 Torque lists); scale ((A*256)+B)/4 rpm follows Ford's other speed PIDs. Polled on PCM 7E0 first, then TCM 7E1 (10R80 trucks with a separate TCM). Check against speed: OSS ~ mph x axle ratio x 336 / tire diameter (in). |
| gearActual | Transmission gear actual (?) | 0x7E0 | 0x1E1F | 1 |  | 1 | 0 |  | no | Placeholder DID suggested for 'gear actual'; no confirmed source. Disabled by default. |
| tpms_lf | Tire pressure LF | 0x726 | 0x2813 | 2 |  | 0.333333 | 7.333333 | kPa | **yes** | Verified by Dale on a 2017 F-150. BCM 726 Mode 22 DID 2813 (LF). Formula remains the Torque Pro community equation (((256*A)+B)/3+22/3) kPa. |
| tpms_rf | Tire pressure RF | 0x726 | 0x2814 | 2 |  | 0.333333 | 7.333333 | kPa | **yes** | Verified by Dale on a 2017 F-150. BCM 726 Mode 22 DID 2814 (RF). Formula remains the Torque Pro community equation (((256*A)+B)/3+22/3) kPa. |
| tpms_rr | Tire pressure RR | 0x726 | 0x2815 | 2 |  | 0.333333 | 7.333333 | kPa | **yes** | Verified by Dale on a 2017 F-150. BCM 726 Mode 22 DID 2815 (RR). Formula remains the Torque Pro community equation (((256*A)+B)/3+22/3) kPa. |
| tpms_lr | Tire pressure LR | 0x726 | 0x2816 | 2 |  | 0.333333 | 7.333333 | kPa | **yes** | Verified by Dale on a 2017 F-150. BCM 726 Mode 22 DID 2816 (LR). Formula remains the Torque Pro community equation (((256*A)+B)/3+22/3) kPa. |

### Routine identifier ranges (0x31)

Source: `UdsRoutines.Classify` / `RefusalReason` (ISO 14229-1 Annex F). Ford routine identifiers themselves are catalog data and are not shipped.

| Range | ISO class | Policy |
| --- | --- | --- |
| 0x0000-0x00FF | IsoReserved | allowed, behind the write gate (typed RUN in the app) |
| 0x0100-0x01FF | Tachograph | allowed, behind the write gate (typed RUN in the app) |
| 0x0200-0xDFFF | VehicleManufacturer | allowed, behind the write gate (typed RUN in the app) |
| 0xE000-0xE1FF | ObdTest | allowed, behind the write gate (typed RUN in the app) |
| 0xE200-0xE2FF | PyrotechnicDeploy | **refused**: pyrotechnic deployment routines (E200-E2FF) can fire airbags or pretensioners and are never sent |
| 0xE300-0xEFFF | IsoReserved | allowed, behind the write gate (typed RUN in the app) |
| 0xF000-0xFEFF | SystemSupplier | allowed, behind the write gate (typed RUN in the app) |
| 0xFF00 | Programming | **refused**: programming routines (FF00/FF01) are only sent by the flash engine (Flash/FlashEngine), never from the Routines tab or the console |
| 0xFF01 | Programming | **refused**: programming routines (FF00/FF01) are only sent by the flash engine (Flash/FlashEngine), never from the Routines tab or the console |
| 0xFF02 | EraseMirrorMemoryDtcs | allowed, behind the write gate (typed RUN in the app) |
| 0xFF03-0xFFFF | IsoReserved | allowed, behind the write gate (typed RUN in the app) |

### Never sent / write refusals

| What | Where enforced | Why |
| --- | --- | --- |
| RoutineControl 0xE200-0xE2FF (pyrotechnic deployment) | `UdsClient.RequestAsync`, `UdsRoutines`, `ConsoleGuard` (also inside raw ISO-TP frames) | can fire airbags or pretensioners; never sent whatever the settings |
| RoutineControl 0xFF00 / 0xFF01 (erase / check dependencies) | `UdsRoutines.BuildRequest` (Routines tab, console helpers, dashboard cards) | only the flash engine sends them, inside a journaled job |
| WriteDataByIdentifier 0xDE00-0xDEFF from a dashboard card | `DashAction.Validate` | As-Built writes go through the As-Built tab (checksums, backup, history) |
| WriteDataByIdentifier 0xF180-0xF19F from a dashboard card | `DashAction.Validate` | identification DIDs (VIN, part numbers) |
| Console `STPX`, `ATWM`, `ATCAF0` while writes are locked | `ConsoleGuard` | can send arbitrary frames or hide the service id |
| Immobilizer / anti-theft bypass | not implemented | project limit (Dale, 2026-11-15) |

### Negative response codes

Source: `UdsNrc.Describe` / `Explain` (ISO 14229-1 Annex A) and `FlashFaults.FromNrc` (what the flash engine does). Response format: `7F <SID> <NRC>`. 0x94-0xEF are reserved for specific conditions; 0xF0-0xFE are manufacturer-specific.

| NRC | ISO name | Meaning shown to the user | Flash engine action |
| --- | --- | --- | --- |
| 0x10 | generalReject | the module rejected the request without a specific reason | Fatal |
| 0x11 | serviceNotSupported | the module does not support this service | Fatal |
| 0x12 | subFunctionNotSupported | the module does not support this sub-function | Fatal |
| 0x13 | incorrectMessageLengthOrInvalidFormat | the request length or format is wrong for this module | Fatal |
| 0x14 | responseTooLong | the response would be too long for the transport | Fatal |
| 0x21 | busyRepeatRequest | the module is busy; try again | Retry |
| 0x22 | conditionsNotCorrect | conditions are not correct (e.g. ignition/engine state or wrong session) | Fatal |
| 0x24 | requestSequenceError | request sequence error (a prerequisite step was skipped) | Fatal |
| 0x25 | noResponseFromSubnetComponent | a sub-network component behind this module did not answer | Fatal |
| 0x26 | failurePreventsExecutionOfRequestedAction | a fault in the module prevents this action | Fatal |
| 0x31 | requestOutOfRange | a parameter is out of the range the module accepts | Fatal |
| 0x33 | securityAccessDenied | security access is required (module is locked) | Security |
| 0x35 | invalidKey | the security key was wrong | Security |
| 0x36 | exceedNumberOfAttempts | too many security attempts; wait before retrying | Security |
| 0x37 | requiredTimeDelayNotExpired | the security delay has not expired yet | Security |
| 0x70 | uploadDownloadNotAccepted | the module refused the download/upload request | Fatal |
| 0x71 | transferDataSuspended | the data transfer was suspended | Fatal |
| 0x72 | generalProgrammingFailure | the module reported a programming failure | Fatal |
| 0x73 | wrongBlockSequenceCounter | the transfer block sequence counter was wrong | Fatal |
| 0x78 | requestCorrectlyReceived-ResponsePending | the module is still processing (response pending) | WaitPending |
| 0x7E | subFunctionNotSupportedInActiveSession | sub-function not supported in the current diagnostic session | Fatal |
| 0x7F | serviceNotSupportedInActiveSession | service not supported in the current diagnostic session (an extended session may be required) | Fatal |
| 0x81 | rpmTooHigh | engine speed is too high for this action | Fatal |
| 0x82 | rpmTooLow | engine speed is too low for this action | Fatal |
| 0x83 | engineIsRunning | the engine must be off for this action | Fatal |
| 0x84 | engineIsNotRunning | the engine must be running for this action | Fatal |
| 0x85 | engineRunTimeTooLow | the engine has not run long enough | Fatal |
| 0x86 | temperatureTooHigh | temperature is too high | Fatal |
| 0x87 | temperatureTooLow | temperature is too low | Fatal |
| 0x88 | vehicleSpeedTooHigh | vehicle speed is too high | Fatal |
| 0x89 | vehicleSpeedTooLow | vehicle speed is too low | Fatal |
| 0x8A | throttle/PedalTooHigh | throttle/pedal is too high | Fatal |
| 0x8B | throttle/PedalTooLow | throttle/pedal is too low | Fatal |
| 0x8C | transmissionRangeNotInNeutral | the transmission must be in neutral | Fatal |
| 0x8D | transmissionRangeNotInGear | the transmission must be in gear | Fatal |
| 0x8F | brakeSwitch(es)NotClosed | the brake pedal must be pressed | Fatal |
| 0x90 | shifterLeverNotInPark | the shifter must be in park | Fatal |
| 0x91 | torqueConverterClutchLocked | the torque converter clutch is locked | Fatal |
| 0x92 | voltageTooHigh | supply voltage is too high | WaitVoltage |
| 0x93 | voltageTooLow | supply voltage is too low | WaitVoltage |

### Timing defaults

Source: `UdsServerTiming.Default` (ISO 14229-2); a module reports its own P2/P2* in the `50 ss` reply.

| Parameter | Default |
| --- | --- |
| P2server_max (first response) | 50 ms |
| P2*server_max (after NRC 0x78) | 5000 ms |

<!-- END GENERATED -->
