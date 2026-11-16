# Roadmap: FORScan-level features (and beyond)

Goal set by Dale (2026-11-15): Ford Enhanced UDS plus the full feature set of FORScan for Windows 11, no install.
This file compares what the app does today (v0.17.0) with FORScan's feature groups and orders the work.

## Where we are (v0.17.0)

| FORScan area | FORScan | This app today | Gap |
|---|---|---|---|
| Connect / adapters | ELM327, OBDLink, J2534 | OBDLink MX+ / EX (STN); plain ELM327 on HS-CAN only | J2534 pass-thru |
| Module discovery | Auto-detects modules on HS, MS, HS2, HS3 | Scan from a module table + discovery sweep on HS / MS / HS2 | HS3 and 2021+ gateway layouts; per-vehicle module tables beyond the 2017 F-150 |
| DTCs | All modules, status, freeze frame, extended data, clear | All modules (19 02) with status meaning, details (19 03/04/06, raw), OBD-II 03/07/0A and freeze frame (02), clear (pinned to bus) | Decoded snapshot DIDs, Ford DTC text |
| Live data | Hundreds of PIDs per module, charts, logging | Live-data browser: any supported OBD-II PID + Ford enhanced DIDs, min/max/avg, CSV (v0.10.0); charts, CSV replay, threshold triggers (v0.12.0); dashboard | Wider Ford DID catalog; faster Mode 22 polling |
| Tests | Self-tests, Mode 6, output control | Mode 6 read live (v0.9.0); self-test only from imported FORScan logs; Routines tab (v0.11.0): 31 start/stop/results by typed ID behind write mode, extended session with keep-alive (pyrotechnic E200-E2FF and programming FF00/FF01 routines are always refused) | On-demand self-test UI and per-module routine IDs (catalog data); output state control UI (2F, plus 85 / 28 are in Core with restore leases, bench-first) |
| Service procedures | PATS, ABS bleed, DPF regen, BMS reset, steering angle, TPMS, TCM adaptive reset, camera/radar calibration | None | All of them (see phase 4; most need security access) |
| Security access | Built in | User-supplied plug-ins (v0.7.0), none shipped | Providers are the user's to supply |
| Configuration (As-Built) | Raw As-Built editing + decoded named options + central configuration | Raw read / write / verify / backup / restore, .ab and .abt import/export, factory compare, community feature data; edit by option name from the 2015-2020 F-150 community sheet into the grid, then the normal safe write (v0.15.0) | Choose the sheet tab from the module part number; literal-digit "recipe" rows; central configuration (CCC), module reset/configuration after changes |
| Programming | Module firmware, PMI (module replacement) | None | Not started |
| Safety | Backups, prompts | Read-only default, typed WRITE, voltage gate, bus pinning, length/VIN/checksum checks, pre-write and pre-restore backups, read-back verify, UDS session keep-alive (v0.11.0), As-Built change history + undo (v0.16.0) | Battery-maintainer detection |

## Second reference: UCDS V3

UCDS is a commercial Ford diagnostic and programming tool with its own adapter (UCDS V5). Its public manual
(https://www.ucdsys.org/documentation/en/index.html, software 3.0.001.007, read 2026-11-15) is used here as a feature and
behaviour reference only. The manual gives no license terms and no protocol data (no DIDs, routines or addresses), so nothing
from UCDS goes into `src/ObdLink.Core/Data/` or `community-db/`.

| UCDS feature (manual section) | This app today | Gap / note |
|---|---|---|
| Read and clear DTCs per module or all modules (vehicle window) | Same (19 02 / 14, OBD-II 03/07/0A) | None |
| Direct Config: per-module named parameter list, keyword search, read/changed markers, read/write module, save/load file, module reset, configuration download by VIN | Raw As-Built read/write/verify, .ab/.abt files, factory compare | Phase 3 decoded editor (see notes) |
| As-built Editor: name/value table with a dropdown of valid values, raw HEX override, changed rows highlighted, writes central configuration to BCM (main) or IPC (backup) | Raw As-Built grid, safe write path | Phase 3 items 1-3 |
| Datalogger: one module's parameters, text table plus graphs, per-signal colour, time cursor after stop | Live-data browser with min/max/avg and CSV; chart with one colour per signal and a hover time cursor across all lanes (v0.12.0) | Right-click remove per signal |
| CAN bus analyzer: up to three buses, per-ID rate, changed-bit highlighting, send a single frame | None | Phase 2 item 4 (bus monitor); sending frames is state-changing |
| CAN bus logger with ID filter (all, 7xx only, pattern + mask), timing columns, playback of recorded logs | Adapter traffic log only | Phase 2 item 4; playback transmits, so state-changing |
| Periodic CAN messages (user frames, 5 ms minimum period) | None | Not started; state-changing |
| PCM/TCM special functions (FLASH/EEPROM block read/write through a custom loader), VBF Loader, Update Wizard, VBF-to-BIN converter | None | Programming row: not started |
| PATS functions (licensed adapter only) | None | Not started |
| Sony ACM radio PIN generator | None | Not planned |
| Mirror auto-fold programming (Focus 2 / Kuga 1 / C-Max) | None | Not applicable to the F-150 |

The manual pages read do not describe backups, read-back verification, typed confirmation or a voltage check before writes.
This app already has all four, and they stay mandatory for every write-type feature added from this list.

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
2. CSV export **done in v0.10.0** (per-cycle, base units). **Done in v0.12.0:** multi-signal charts (one lane per signal, own scale, up to 8), CSV replay at 0.5-16x, threshold triggers with hysteresis (chart markers + log).
3. Faster polling: **done in v0.14.0** for OBD-II: J1979 multi-PID Mode 01 requests (up to 6 PIDs per request, automatic fallback). Still open: per-signal rates chosen by the user; Ford Mode 22 multi-DID (unverified on Ford modules).
4. **Done in v0.13.0:** bus monitor (read-only): silent passive capture (ATCSM1 + ATMA) on HS-CAN, MS-CAN and HS-CAN2 with
   per-ID count, rate and changed-bit marks, a software ID filter (all / diag 7xx / ID / id/mask) and a CSV frame log.
   BUFFER FULL restarts the monitor and is counted. Sending, replaying or periodic frames are separate, state-changing
   features behind the write gate. **Open:** MX+ monitor throughput not measured; STN hardware filters (STFAP) to cut load.

Design notes from UCDS for charts (item 2): a signal table beside the graph with one colour per signal; after Stop, a time
cursor that shows every signal's value at one instant; right-click to remove one signal or clear all. Unlike the UCDS manual's
Datalogger, which works on one module at a time, allow signals from several modules in one chart.

### Phase 3: Configuration editor (decoded As-Built)
1. Decode profiles per module/firmware: named options mapped to bits. **v0.15.0:** editor for `*` options of the 2015-2020 F-150 sheet (shipped in Core), tab chosen by hand (default from `DecodeProfile`). Open: pick the tab from the module part number (F188/F111).
2. **Done in v0.15.0:** edit by option name (value list or raw digits, conflicting edits refused), resulting raw lines and checksums shown in the grid, written through the v0.6.1 safe path (length, checksum, backup, read-back).
3. Central configuration (CCC): read and edit, with module reset/relearn steps after changes.
4. **Done in v0.16.0:** change history saved next to the backups (`asbuilt-history.jsonl`: verified writes and restores with before/after) and "Undo last write" per module (restores the backup taken before that write through the normal restore checks).

Design notes from UCDS for the editor: choose the decode profile from the module's own part number (read F188/F111 first, as
UCDS's "Read HWSW" does), not from the model year alone. Show each option's state as not read / read / changed, with keyword
search. Offer a dropdown of valid values per option plus a raw hex override, and highlight changed rows. For central
configuration, UCDS documents the BCM as the main copy and the IPC as a backup, reversed on some models; confirm which module
holds the primary copy on the 2017 F-150 from a capture before writing either. **[UNVERIFIED for the F-150]**

### Phase 4: Service procedures
Implement one at a time, each from published Ford service information the user owns or from documented community captures:
TPMS sensor relearn, BMS (battery monitor) reset, steering angle sensor calibration, ABS service bleed, transmission adaptive
table reset, DPF regeneration (diesel only), camera/radar calibrations.

### Phase 5: Beyond FORScan
- Fully open and scriptable: **started in v0.17.0** — `obdcli` (`docs/CLI.md`): info, scan, DTCs, live data, bus monitor, As-Built read and offline option editing, read-only toward the vehicle, every command also against the simulator.
- Community database with sources and confidence on every entry, reviewable in pull requests.
- Diff-first editing: every change shown as a before/after against the factory `.ab` from Motorcraft.
- Simulator-first development: every feature runs against `SimulatedAdapter` and replayed real captures, so contributors don't need a truck.
- No license server and no time-limited keys.

## Engineering prerequisites (do before phase 3)
- Move the remaining command logic out of `MainViewModel` (about 1,460 lines) into Core services, as v0.6.1 did for As-Built write/restore, so it is testable. Started: bus routing is now `ObdLink.Core.Modules.BusRouter` (PR #13), the live-data catalog and aggregator are in Core (v0.10.0), and the module scan with alternate-bus retries is `ModuleScanFlow` (PR #29).
- Replace host-side ISO-TP transmit with STN `STPX` if a bench capture shows the adapter handles flow control itself.
- Hardware-in-the-loop check list: a bench harness (spare BCM or an ECU simulator) for every write-type feature before it touches the truck.
