# Dale's 2017 F-150: factory As-Built (.ab) analysis

Source: Ford's official As-Built file for VIN **XXXXXXXXXXXE25268**. The original is kept locally in `samples/real-logs/` (git-ignored). The committed fixture is `tests/ObdLink.Core.Tests/Fixtures/ab/dale-f150-2017.redacted.ab`: the VIN is masked, including the ASCII copies inside module blocks with checksums recomputed, and module serial/MAC DIDs (F18C/F1D0/F141) read `REDACTED`.

Tooling: `FordAbFile` (Core/AsBuilt/FordAbFile.cs) and `tools/AbTool` (`summary`, `decode`, `redact`).

## File format (as observed in this file)
- XML: `AS_BUILT_DATA/VEHICLE` contains `VIN`, `VEHICLE_DATA/DATA`, `PCM_MODULE` (appears twice, identical), `BCE_MODULE/DATA LABEL="MMM-BB-LL"` with 3 `CODE` elements, `NODEID` (module id plus F1xx DIDs) and `ERROR`.
- **Block labels are hex.** They skip 0A–0F (09 → 10), and the checksum only validates when the label is read as 0x10.
- Line checksum: `(modHi + modLo + block + line + Σdata) & 0xFF`. **295 / 295 lines valid.**
- PCM rows ("PCM 1..9", no address): `(Σdata + 0x10 + n) & 0xFF` matches **9 / 9**. This formula is **empirical**, derived from this one file only.
- `VEHICLE_DATA` (`43EC FFFF FF2C`): meaning and checksum unknown.
- `ERROR 614 "CCC DATA NOT FOUND"`: no Car Configuration Code data was supplied by Ford.

## Modules
| ID | Module (our table) | Blocks | Lines | Bytes/line | F188 / F113 |
|---|---|---|---|---|---|
| 701 | GPSM | 1 | 1 | 1 | DE8T-14F055-AE / DE8T-19H463-AE |
| 716 | GWM | 15 | 15 | 1 | HL3T-14F530-AC / GL3T-14F642-AA |
| 720 | IPC | 9 | 18 | 5,3 | HL3T-14C026-AD / HL3T-10849-AAD |
| 724 | SCCM | 9 | 9 | 4 | HC3T-14C579-AC / HL3T-3F944-AC |
| 726 | BCM | 70 | 148 | 5,1,4 | HU5T-14C184-AAJ / HU5T-14B476-DAJ |
| 727 | ACM | 4 | 5 | 5,2,1 | HL3T-14D099-BC / HL3T-19C107-BC |
| 730 | PSCM | 2 | 10 | 5,4 | HR3C-14D003-AA / HL34-3F964-BG |
| 737 | RCM | 6 | 10 | 5,2,3,1 | GR3T-14C028-AA / HL3T-14B321-AC |
| 740 | DDM | 22 | 27 | 1–5 | DG9T-14C064-CB / DG9T-14B531-FB |
| 741 | PDM | 22 | 27 | 1–5 | DG9T-14C108-CB / DG9T-14B533-FB |
| 751 | RTM | 1 | 1 | 3 | GD9T-14G090-AB / GD9T-15K619-AB |
| 760 | ABS | 3 | 7 | 5,2,3 | HL34-2D053-AG / HL34-2C219-AG |
| 7A5 | FCDIM | 3 | 4 | 5,2,4 | E1GT-14D358-CL / HL3T-18B955-CB |
| 7A7 | FCIM | 2 | 8 | 5,1,2 | HL3T-14G130-GB / HL3T-18E243-BMB |
| 7D0 | APIM | 3 | 5 | 5,3,1 | E1BT-14D205-AJ / FL3T-14D212-RB |
| 7E0 | PCM | – (PCM rows) | 9 | 5 | HL3A-14C204-BDC / FL3A-12A650-CGA |
| 761 | TCCM | NODEID only | | | HL3A-14C366-BC / HL3A-7H417-BC |
| 756 | **unknown** | NODEID only | | | FL34-14C168-AG / FL34-2C496-AK |
| 765 | **unknown** | NODEID only | | | HL3B-14G309-AA / HL3B-604A17-AA |

Line lengths vary between 1 and 5 bytes. The factory diff therefore splits a live block read using the .ab's own line lengths (`FordAbFile.SplitLike`), not a fixed 5-byte split.

## Module table corrections (Modules.cs)
- Added evidence tag `verified-factory-ab` to every ID above (`ModuleDefinition.InFactoryAb`). Evidence is now a comma list, so `SeenInLog` still works.
- TCCM 0x761 is now confirmed (previously community-only). The duplicate **4X4** row at 0x761 was removed.
- RTM 0x751, FCDIM 0x7A5, GWM 0x716, GPSM 0x701 and FCIM 0x7A7 are confirmed. This confirms the **address only**; the bus is still unverified.
- New rows NODE756 and NODE765 are marked unverified. Candidates are PBM/OCS (both appear in the FORScan log without addresses); 2C496 might be a brake-related part base, but **this is not confirmed**.
- Absent from the .ab, annotated in the table: TCM 7E1, HVAC 733, PAM 736, IPMA 706, DSM 744, TRM 791, SODL/SODR 7C4/7C6. A module can be absent from the .ab and still exist, so this does not prove the module is missing from the truck.
- The ASCII VIN is stored in blocks 737-01 (RCM), 760-01 (ABS) and 7A7-02 (FCIM).

## Decoded factory configuration
Decoded with `community-db/f150-asbuilt/features.json`. These are **CommunityReported** meanings, not Ford-documented, and several tabs overlap. The year-appropriate tab is used here: 2017 corresponds to the "2015-2017" tabs and to BCM part prefix HU5T, which uses the "BdyCM-old" tab. Regenerate with `AbTool decode`.

**ABS (760), tab 2015-2017**
- Wheelbase 145 in, base payload
- Engine 3.5L PFI (consistent with VIN engine code 8)
- EPAS 17:1, 17/18 in tires, base tow
- ESOF electric 4x4, 6R80 transmission
- Electric park brake fitted. No hill descent control, ACC, CMbB, e-locker or stop-start.

**BCM (726)**

Drivetrain and controls:
- Axle 3.73, gas engine, normal cruise, standard 4x4
- Key cylinder, column/lever shift

Locks, remote features and security:
- Factory mode off
- TPMS on (MY15 NA, 4 tires)
- Remote start disabled (duration field 15 min)
- Global open/close disabled, approach detection disabled
- Security: none. Perimeter alarm, mislock horn and two-stage unlock enabled.

Lighting:
- Bed lamp disabled, puddle lamps enabled, halogen headlamps
- Front fog enabled at 726-70-01, but the fog-lamp circuit field reads unused; this needs checking on the truck. Rear fog disabled.
- DRL relay used, trailer tow relays present

**IPC (720)**
- 4WD, 1 fuel sender, 23-gal tank
- EPB, RSC and trailer sway on
- Trailer brake (TBA) off, remote start menus off
- MyKey on, oil-life minder on

**PSCM (730)**
- Tune P552, wheelbase 144.5 in
- Pull-drift compensation on, active park assist off

**ACM (727), 2015-17 tab**
- Internal SDARS enabled (U.S.), no data services
- HD Radio disabled, EQ #3

**APIM (7D0)**
- Best match is the Sync 3 tab, but it is **uncertain**.
- Navigation disabled, heated steering wheel available, SD slot present, country 5553 (U.S.)
- "Drive type = FWD" is very likely a tab/layout mismatch, so treat this module's decode as low-confidence.

## Caveats
- Feature meanings are community-reported. Verify before writing anything.
- The .ab is a *factory baseline*. Dealer reprogramming since build would show up as `Changed` in **Compare to factory**.
- Compare to factory only reads; it never writes. Use Write + verify / Restore for changes, through SafetyGuard.
