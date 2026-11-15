# Roadmap: FORScan-level features (and beyond)

Goal set by Dale (2026-11-15): reach the full feature set of FORScan for Windows, then go past it where an open tool can.
This file compares what the app does today (v0.10.0) with FORScan's feature groups, orders the work, and lists the
decisions only Dale can make. It follows the policy in `PLAN.md`: clean-room, public sources only, no copied FORScan
data, no proprietary OEM security algorithms.

**Reference tools are not data sources.** FORScan, OBDwiz (OCTech) and the OBD Solutions STN firmware updater are
installed on Dale's PC as behavior and scope references for parity work only. Their definition databases are packed and
proprietary, and nothing is extracted from them into this repo. The same applies to commercial scan-tool manuals and
other copyrighted service documents. A fact known only from one of these is not license-clean; it has to be confirmed
from a redistributable source or a capture from a real vehicle before it goes into `src/ObdLink.Core/Data/` or
`community-db/`.

## Where we are (v0.10.0)

| FORScan area | FORScan | This app today | Gap |
|---|---|---|---|
| Connect / adapters | ELM327, OBDLink, J2534 | OBDLink MX+ / EX (STN); plain ELM327 on HS-CAN only | J2534 pass-thru |
| Module discovery | Auto-detects modules on HS, MS, HS2, HS3 | Scan from a module table + discovery sweep on HS / MS / HS2 | HS3 and 2021+ gateway layouts; per-vehicle module tables beyond the 2017 F-150 |
| DTCs | All modules, status, freeze frame, extended data, clear | All modules (19 02) with status meaning, details (19 03/04/06, raw), OBD-II 03/07/0A and freeze frame (02), clear (pinned to bus) | Decoded snapshot DIDs, Ford DTC text |
| Live data | Hundreds of PIDs per module, charts, logging | Live-data browser: any supported OBD-II PID + Ford enhanced DIDs, min/max/avg, CSV (v0.10.0); dashboard | Charts; wider Ford DID catalog |
| Tests | Self-tests, Mode 6, output control | Mode 6 read live (v0.9.0); self-test only from imported FORScan logs | On-demand self-test (31 01 0202 style routines), output state control (2F) |
| Service procedures | PATS, ABS bleed, DPF regen, BMS reset, steering angle, TPMS, TCM adaptive reset, camera/radar calibration | None | All of them (see phase 4; most need security access) |
| Security access | Built in | User-supplied plug-ins (v0.7.0), none shipped | Providers are the user's to supply |
| Configuration (As-Built) | Raw As-Built editing + decoded named options + central configuration | Raw read / write / verify / backup / restore, .ab and .abt import/export, factory compare, community feature data (read-only) | Decoded named-option editor, central configuration (CCC), module reset/configuration after changes |
| Programming | Module firmware, PMI (module replacement) | None | Out of scope for now (see decisions) |
| Safety | Backups, prompts | Read-only default, typed WRITE, voltage gate, bus pinning, length/VIN/checksum checks, pre-write and pre-restore backups, read-back verify | Battery-maintainer detection; session keep-alive during long jobs |

## Phases

Each phase ships with tests against `SimulatedAdapter` and a written Windows + truck check list, like the current
release notes. Nothing that writes is called done until it has been verified on Dale's truck.

### Phase 1: Diagnostics depth (read-only, lowest risk)
1. **Done in v0.8.0:** DTC detail: snapshot/freeze frame (19 03/19 04), extended data (19 06), DTC count (19 01), status bits and confirmed/pending/history view; OBD-II pending (07), permanent (0A) and freeze frame (02). Snapshot/extended layouts are shown raw until per-module definitions exist.
2. DTC text: grow `dtc_generic.json`; add Ford-specific text only from redistributable sources (cite each source in the entry). **Open:** needs a source whose license allows redistribution; not started.
3. **Done in v0.9.0:** Mode 6 (OBD 06) monitor results read live: every supported OBDMID from every ECU, J1979 unit scaling, pass/fail on raw limits.
4. **Done in v0.9.0:** Module identification grid on the Vehicle page (F188/F111/F113/F124/F18C, As-Built) with CSV export (serials blanked by default).
5. HS3 bus and 2021+ gateway module table (as a separate, opt-in vehicle). **Open:** needs a 2021+ truck capture to confirm pins and addresses (`community-db/f150-2021plus-asbuilt/module-addresses.json` is a start, unverified).

### Phase 2: Live data and logging
1. **Done in v0.10.0:** live-data browser — pick any supported OBD-II PID or Ford enhanced DID, watch it live with running min/max/avg and sample count. Reuses the dashboard's poll engine (services 01/22 only). The enhanced-DID catalog is the editable `ford-enhanced-pids.json`.
2. CSV export **done in v0.10.0** (per-cycle, base units). Still open: multi-signal **charts**, replay, triggers.
3. Faster polling: STN batch requests where supported; per-signal rates in `PollScheduler`.

### Phase 3: Configuration editor (decoded As-Built)
1. Decode profiles per module/firmware: named options mapped to bits (the community-db schema already exists; add an editor).
2. Edit by option name, show the resulting raw lines and checksums, write through the v0.6.1 safe path (length, checksum, backup, read-back).
3. Central configuration (CCC): read and edit, with module reset/relearn steps after changes.
4. Undo stack per session, and a change history saved next to the backups.

### Phase 4: Service procedures
Implement one at a time, each from published Ford service information the user owns or from documented community captures:
TPMS sensor relearn, BMS (battery monitor) reset, steering angle sensor calibration, ABS service bleed, transmission adaptive
table reset, DPF regeneration (diesel only), camera/radar calibrations.
Most of these run UDS routines (31) and many need SecurityAccess (27). See decision D1.

### Phase 5: Beyond FORScan
- Fully open and scriptable: a documented command-line/automation layer over Core (the `tools/AbTool` pattern) for repeatable jobs.
- Community database with sources and confidence on every entry, reviewable in pull requests.
- Diff-first editing: every change shown as a before/after against the factory `.ab` from Motorcraft.
- Simulator-first development: every feature runs against `SimulatedAdapter` and replayed real captures, so contributors don't need a truck.
- No license server and no time-limited keys.

## Decisions for Dale

- **D1. Security access (0x27). DECIDED 2026-11-15: yes, through user-supplied plug-ins.** v0.7.0 ships the loader
  (off by default, approve by SHA-256, typed TRUST), As-Built unlock at a set level, and a template. The project still
  ships no algorithms or keys; whoever installs a plug-in is responsible for where it came from. This unblocks phase 4 and
  configuration writes on secured modules for owners who have a provider.
- **D2. Module programming (firmware flashing / PMI).** High brick risk, needs Ford calibration files and usually 0x27. Recommend: out of scope.
- **D3. J2534 support.** Opens other adapters (and is how FORScan reaches some functions). Moderate work; not needed for the MX+.
  Candidate scope (not started): a J2534 v04.04 transport next to the STN/ELM one, so the app can drive genuine VCM II /
  VCMM units, Mongoose-class devices or a DIY pass-through device. MS-CAN would use J2534-2 pin selection instead of STN commands;
  `BusRouter` is the seam. Read and As-Built scope only; module flashing stays out (D2). Windows OEM J2534 DLLs are
  usually 32-bit, so a 64-bit app may need an out-of-process bridge. Background: `docs/vcm-research.md`.
- **D4. Vehicles beyond the 2017 F-150.** Every added vehicle needs a module table and test captures. Which ones next?

## Engineering prerequisites (do before phase 3)
- Move the remaining command logic out of `MainViewModel` (about 1,460 lines) into Core services, as v0.6.1 did for As-Built write/restore, so it is testable. Started: bus routing is now `ObdLink.Core.Modules.BusRouter` (PR #13), and the live-data catalog and aggregator are in Core (v0.10.0).
- Replace host-side ISO-TP transmit with STN `STPX` if a bench capture shows the adapter handles flow control itself.
- Hardware-in-the-loop check list: a bench harness (spare BCM or an ECU simulator) for every write-type feature before it touches the truck.
