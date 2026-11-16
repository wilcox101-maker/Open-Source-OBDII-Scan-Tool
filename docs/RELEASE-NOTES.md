## v0.22.0

**Flash hardening and dashboard undo.** Keep-alive and lost-session recovery came in v0.21.2; this release adds what is around them. Includes v0.21.2 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not run by hand on Windows or on the truck. Nothing has flashed a real module. Unsigned. Ford products only.

- **Secondary bootloader (SBL).** Ford software usually ships with an SBL file (`sw_part_type = SBL`, `call = …`). Flash tab: **SBL file…** (or Open file… on an SBL file). After security access the SBL is downloaded to the module's RAM and started with routine 0x0301 and its call address, then the erase and download run as before, followed by commit routine 0x0304. The SBL is loaded again on every attempt. Routine IDs follow an open-source community Ford flasher [UNVERIFIED against Ford documentation]. The DEMO module now requires an SBL, and the DEMO image comes with one.
- **Resume can't lock the module out.** Every security outcome is journaled per module, across jobs. After a rejected key (NRC 0x35) no further key is sent until you press **Clear security hold…**; after a lockout (0x36, or 0x37 that did not clear) no seed is requested for 10 minutes. The hold is checked before the warnings and the typed FLASH.
- **Failure before the erase resets the module back.** If a new job fails in the programming session, security access, the SBL stage, or on a first erase refused with a definite NRC, nothing was erased: the app sends ECU reset so the module runs its own software again, checks it answers, and closes the job (*Nothing was erased*). It never does this on a resume, or once anything may have been erased.
- **Image checks** (preflight and engine): overlapping segments, segments outside the file's erase regions, addresses past 32 bits or too long for the address/length format, an SBL loaded as the image, an SBL without a call address: refused before anything is sent. Warnings: file without `ecu_address`; module and file part numbers with different vehicle-program prefixes; a Ford EXE/DATA file without an SBL.
- **Check routine not supported** (FF01 answered 11/12/31): the flash continues to the reset; the module validates its software at reset.
- **Dashboard write-card undo.** After a DID write whose old value was read, the card shows **Undo…**: it re-reads the DID, refuses if it no longer holds what the card wrote, then writes the old value back (writes enabled, battery check, typed WRITE) and reads it back.
- Test fix: a J2534 timing test failed about 1 run in 6 under parallel load (200 ms wait vs a reply due at 400 ms); the reply now comes at 1.5 s.

Internal: `FlashImage.PartType/CallAddress/IsSbl`, `FlashPlan.Sbl/SblRoutineId/CommitRoutineId`, `FlashRetryPolicy.LockoutHold`, `FlashPhase.SecondaryBootloader`, journal kinds `Security`/`Restarted` (a session re-entry now resets the segment count), `FlashSecurity.Hold`, `FlashEngine.SecurityHold/ClearSecurityHold/Now`, `FlashImageChecks.Layout/SameProgram`; simulator `SecurityDelaysLeft`, `MaxKeyAttempts`, `RequiresSbl`, `CheckRoutineSupported`, `SimFlashFault.SessionDrop`; `DashActions.UndoFor`, `DashWriteUndo`.

### Windows verify (DEMO only; no truck flash)
1. **DEMO image** on the Flash tab: the SBL line shows *DEMO SBL (synthetic).vbf … started at 0x00E00000*. Run checks: a *Secondary bootloader* row passes. Flash (writes on, type FLASH): the log shows *loading secondary bootloader*, then the erase and both segments; it finishes.
2. Press **No SBL**, then Flash again: it stops with *Nothing was erased; the module was reset back to its normal software*. No open job remains.
3. DEMO dashboard: add a WriteDid card for a DID the DEMO module answers on the Live Data page (not F180-F19F or DE00-DEFF). Write a new value, then **Undo…**: the old value comes back and the Undo button disappears. Press Undo on a fresh card: it is not shown (no write yet).
4. Truck, read-only only: Flash → open a real Ford VBF set (SBL + strategy) → Run checks. Do not press Flash.
## v0.21.2

**Flashing: the programming session survives pauses, and a lost session is recovered.** Built and unit-tested on Linux and by GitHub Actions on Windows; DEMO / simulator only. Unsigned. Ford products only. Real-module flashing is still not recommended: the secondary bootloader (SBL) stage is not built yet.

- **Keep-alive during every wait.** Once the module is in the programming session, each pause (battery below the window, the 0x37 security delay, retry back-offs, response-pending settles, routine polls) sends TesterPresent `3E 80` at least every 2 s. Before, a pause longer than the module's 5 s session timeout ended the session and the job failed.
- **Session lost → re-enter from the erase.** NRC 0x7E / 0x7F, or 0x33 (and 0x24 on TransferData) after unlocking, now mean "the session or security was lost" instead of a fatal refusal. The engine re-enters the programming session, unlocks, erases and downloads again (once); if it happens again the job stays open so Resume can finish it.
- Simulator: S3 session timeout on a virtual clock, programming services refused outside the programming session (0x7F), `DropSession()`.

Internal: `FlashEngine.WaitAsync` / `KeepAliveInterval` / `MaxSessionRecoveries` / `ProgramAsync`, `FlashFaultAction.SessionLost`, `FlashSessionLostException`. Tests `FlashSessionTests` (30 s battery dip, security delay, one lost session → finishes, repeated → job open); 4 of 5 fail on the previous engine.

### Windows + truck verify
1. **DEMO (Phase 0):** run a DEMO flash (Flash tab, DEMO-EX). It completes as before.
2. **DEMO:** during the download, watch the log for "paused: battery … (session kept alive)" if you lower the DEMO voltage; the flash continues after it recovers.
3. **Truck:** none (real-module flashing waits for the SBL stage and a bench test).

## v0.21.1

**Configuration write safety brought to FORScan's level.** Built and unit-tested on Linux and by GitHub Actions on Windows; not run by hand on Windows or on the truck. Unsigned. Ford products only.

- **PCM / TCM As-Built writes and restores need an extra typed confirmation (`POWERTRAIN`).** Their configuration can hold checksum-protected vehicle ID data (VIN, tire size, axle ratio) and transmission characterization, which FORScan only changes through dedicated functions that recalculate the checksums. This app has no such function yet, so a raw write there is spelled out first. Applies to every path (As-Built page, Options apply, restore, undo, recovery).
- **After every verified write, restore or undo**, the result says what to do next: ignition off at least 30 s, on, Read again; and that U2100 / U2101 in other modules means the shared settings need a relearn (not in this app yet) or the backup restored.

Internal: Core `AsBuiltWritePolicy` (`ExtraSteps`, `AfterWriteAdvice`), used by `AsBuiltService.WriteBlockAsync` / `RestoreAsync`; tests `AsBuiltWritePolicyTests`.

### Windows + truck verify
1. **DEMO:** write an As-Built block of the PCM (DEMO-EX, writes enabled): after WRITE the app asks to type POWERTRAIN; Cancel there → nothing written. Repeat and type it → written, and the result shows the ignition-cycle advice.
2. **DEMO:** write a BCM block: no POWERTRAIN step; advice shown.
3. **Truck (Phase 2):** after your one-block BCM test write, follow the advice (ignition cycle, Read again) and confirm the block kept the value.

## v0.21.0

**As-Built write recovery: every write is journaled, and a write that did not finish is found and fixed.** Includes v0.20.1 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not run by hand on Windows or on the truck. Unsigned. Ford products only.

- **Write journal.** Before an As-Built 0x2E is sent, a *pending* line is flushed to disk in `asbuilt-history.jsonl` (block, old data, new data, backup file). After the read-back an outcome line follows: **verified**, **mismatch** (the block holds something else), **unknown** (no answer and no read-back, e.g. the adapter was pulled), **rejected** (the module refused with a definite NRC; nothing stored) or **not-written** (the block still holds the old data). A PC crash or a pulled adapter now leaves a *pending* or *unknown* line instead of nothing. Lines from earlier versions read as verified.
- **"A write did not finish" banner** on the As-Built page, with two buttons:
  - **Check** reads the block (read-only) and records whether it holds the new data, the old data, or something else.
  - **Restore backup…** puts back the backup taken just before that write: the normal restore (VIN match, a fresh backup first, typed WRITE, read-back), on the module's own bus only.
- **Right after a failed write, restore or undo**, the app offers to read the block (read-only) so you see at once what it holds.
- **Undo last write** now also covers a write that ended mismatched, unknown or interrupted (its backup was taken first). A refused write (nothing stored) is skipped.
- **A multi-block restore that stops half way** says how far it got ("stopped after 1 of 2 block(s)") and what to do next, instead of a bare error.
- Change history shows an **Outcome** column.

Internal: `AsBuiltOutcome`, `AsBuiltChange.Outcome/AttemptId`, `AsBuiltHistory.LoadLines/Load` (folded per attempt)/`Resolve`/`Unresolved`, journal appends flushed with write-through; `AsBuiltService.CheckAttemptAsync`/`RecoverAttemptAsync`, `PartialRestoreException`; simulator knobs `SilentAfterWrite`/`Reconnect()` and `WritesBeforeReject`. Plan: project note "vehicle write recovery" (gaps A1-A3, X1).

### Windows + truck verify
1. **DEMO (Phase 0):** write a changed As-Built block on DEMO-EX (writes enabled): history shows one row, Outcome *verified*.
2. **DEMO:** history file check: open `Data\backups\asbuilt-history.jsonl`; the write is two lines (pending, verified) with the same AttemptId.
3. **Truck (Phase 2, one block, the module you already tested):** write a one-byte change, and **pull the adapter's USB cable right after typing WRITE and pressing Enter**. Expect an error saying the result is UNKNOWN and the red banner on the As-Built page. Plug back in, Connect, press **Check**: it should report the new or the old data. Then **Restore backup…**: the block returns to the old bytes (read it again to confirm). The banner disappears.
4. **Truck:** close the app while the banner shows, start it again: the banner is still there (it comes from the journal, not memory).
5. **Truck:** Undo last write of this module after a normal verified write still restores the previous bytes.

## v0.20.1

**Fixes found while capturing the README screenshots on Windows 11 (v0.20.0, DEMO-EX).** Includes v0.20.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows. Unsigned. Ford products only.

- **Typed confirmation kept its focus.** After a Yes/No dialog, the app queued a main-window activation; when a second dialog opened right away (Flash warnings, then *Type FLASH*), that activation took keyboard focus from the prompt, so typing went nowhere until you clicked it. The main window is no longer activated while a modal dialog is open. Applies to every multi-step confirmation (FLASH, WRITE, RUN, HOLD).
- **Dashboard toolbar wraps.** At the default 1100 px width the toolbar ran off the window and hid the Layout picker and the rate text. It now wraps to a second row.
- **Dashboard windows 2 and 3 cascade** 48 px from window 1 the first time they open, instead of covering it exactly. Saved positions are unchanged.
- **Write-card buttons** keep their normal height instead of stretching to fill the card.
- README: screenshots (`docs/screenshots/`) and an updated overview.

Seen, not changed: the Flash log does not scroll to the newest line by itself during a flash.

### Windows verify
1. Flash tab on DEMO with writes enabled: Flash… → Yes → the *Type FLASH* box has the cursor; type FLASH and press Enter without clicking first. Same for a dashboard write card (RUN / HOLD / WRITE).
2. Open a dashboard at its default size: the toolbar wraps and the Layout box is visible.
3. With a fresh `Data` folder, open Dashboard, Dashboard 2 and Dashboard 3: each is offset from the previous one. Move them, close, reopen: they come back where you left them.

## v0.20.0

**Three dashboard pop-out windows with saved layouts and write cards.** Includes v0.19.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not run by hand on Windows or on the truck. Unsigned. Ford products only.

- **Three windows.** View → Dashboard (Ctrl+Shift+G), **Dashboard 2** (Ctrl+Shift+2) and **Dashboard 3** (Ctrl+Shift+3). Each window has its own layout, background, arrange width and position, saved in `Data\settings.json`. The old single-window settings become window 1; windows 2 and 3 start with their own copy of the default layout ("Dashboard 2", "Dashboard 3").
- **One poll loop for all of them.** It reads the union of what the open windows show (all three windows' items while Keep live runs with every window closed). A second or third window adds no duplicate requests. Pause, Keep live, Record CSV and the lamp row are shared.
- **Layouts: read and write.** Pick any saved layout per window. In Customize: Save as, Rename, Delete, Reset, **Export…** (`.dashboard.json`) and **Import…** (added as a new layout; every write card in the file is validated and a refused one rejects the file). Two windows can show the same layout; an edit appears in both.
- **Write cards.** Customize → Add write card. **Routine** (31 01, type RUN), **OutputHold** (2F 03, type HOLD; returned with 2F 00 on the next press, after 10 s, when the card is removed or when its window closes), **WriteDid** (2E, type WRITE; current value read and shown as old → new and logged, read back after). Every press: writes enabled, battery window, confirmation, typed phrase, extended session, the module's own bus, and the shared adapter lock (the gauges pause for it). Refused on every path: E200-E2FF (airbag deployment), FF00/FF01 (Flash tab only), DE00-DEFF (As-Built tab), F180-F19F (identification).
- Labelled **Phase 3/4** for the truck: routines need an allow-listed source (verification plan); output holds are bench-first (Phase 4).

Internal: `DashboardPoller` (shared loop, shared gauges) + per-window `DashboardViewModel`; `DashboardWindowContext`; Core `DashAction`/`DashActions`, `DashboardWindowState`/`DashboardWindows`, `DashboardLayoutFile`, `DisplayKind.Action`; `DashboardOptions.Window/LayoutFor/SetLayout`. `XamlBindingTests` checks the dashboard window against its new context.

### Windows + truck verify
1. DEMO-EX: open Dashboard, Dashboard 2 and Dashboard 3 (menu and shortcuts). Each opens once; repeating brings it to the front. Give each a different layout and background, move them to different places (or monitors), close all, restart, reopen: each comes back with its own layout, background and position.
2. With all three open, the Logs show one "Polling:" line per change, not three loops. Close two: polling continues for the last one. Close all: Stopped (or Live with Keep live on).
3. Window 2: Customize → Save as "Towing", Rename to "Tow", Export…, then Import… the file: it appears as "Tow (2)". Edit a layout shown in two windows: both update.
4. DEMO, writes enabled: add a Routine card (module 7E0, ID 1234), press it, type RUN: the card shows the status. Add an OutputHold card (module 7E0, DID D100, data 01): press, type HOLD, the button turns to Release; it releases by itself after 10 s; hold again and close the window: the log shows the output returned. Writes off (read-only): every card is refused before anything is sent.
5. Add cards with ID E201 (routine), FF00 (routine), DE00 (WriteDid) and F190 (WriteDid): each is refused with a reason. Hand-edit an exported file to E201 and import it: refused.
6. Truck: open three windows on gauges only (read-only); note the poll rate with one vs three windows open. Do not press write cards on the truck until the matching phase passes.

## v0.19.0

**Module flashing (Phase 5): Flash tab + Core flash engine.** Includes v0.18.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; **flashing has only run against a simulated module**. Not run by hand on Windows or on the truck. Unsigned. Ford products only.

- **Flash tab**: pick a module, open a VBF file (block checksums checked; a damaged file is refused), run the checks, then Flash: the checks run again, the warnings are shown and you type FLASH. Progress, retries, bytes and a log are shown live; a red banner says not to disconnect.
- **Both adapters.** OBDLink EX and MX+ can flash. The MX+ gets one extra warning (wireless link; wired EX recommended) in the checks and in the confirmation. Nothing is blocked by adapter type.
- **Checks before flashing**: battery window (12.5–15.0 V, three samples), link health (8 tester-present probes: one lost is a warning, two or more stop), file vs selected module, part number read from the module, PC on AC power.
- **Backup**: every As-Built block the module has is saved (`*_preflash.asbuilt.json` in the backups folder) before the programming session.
- **Error handling**: a journal (`flash/flash-<module>.jsonl` in the data folder) records every acknowledged block on disk. Lost answers and lost requests are resent with the same block counter; "response pending" is waited out or polled; low/high voltage pauses the flash; programming failures and security errors stop it. After a failure the module is **never** reset: it stays in its bootloader and **Resume** (same file, either adapter) redoes the programming session, security, erase and download. **Stop after this segment** and **Abandon job** are available. The PC is kept awake during a flash.
- **`obdcli vbf-info --file x.vbf`** (offline): checks a real VBF against the reader before any flash: header, blocks, checksums; names the checksum a file really uses if it is not CRC-16/CCITT-FALSE. Exit 3 = the app would refuse the file.
- **DEMO**: on a DEMO port, **DEMO image** loads a synthetic file and the flash targets a simulated bootloader module, so the whole flow can be tried without the truck.
- Still needed for a real Ford module: a seed/key plug-in for the module and its programming security level (default 01, editable), and a real VBF. Erase/check record layouts per Ford module are not public: `docs/FLASHING.md` lists everything UNVERIFIED.

Internal: Core `Flash/` (VbfReader, FlashPlan, FlashPreflight, FlashWarnings, FlashEngine, FlashFaults, FileFlashJournal, SimulatedFlashEcu with fault injection; PR #37), `AsBuiltService.BackupAvailableAsync`, App `FlashViewModel` + `WindowsHostPower` (AC status, sleep blocking).

### Windows + truck verify
1. DEMO (no vehicle): connect DEMO-EX, select the PCM, Flash tab → DEMO image → Run checks (all PASS except PC power if on battery) → Flash… → type FLASH. It finishes with "Module reports DEMO-14C204-SIM". Repeat on DEMO-MX+: the checks and the confirmation show the wireless warning, and the flash still runs.
2. DEMO: start a flash and press Stop after this segment: the banner says the job is open; Flash… is refused; Resume… finishes it. Abandon job… on an open job asks first and keeps the journal.
3. Offline, any PC: `obdcli vbf-info --file <a real Ford .vbf>`. Expect "the flash engine's reader accepts this file". If it says MISMATCH, send the output: the reader's checksum assumption needs fixing before any real flash.
4. Truck, read-only path only: connect the EX, select a module, open any VBF for that module and press **Run checks** only. Expect PASS for battery (charger on) and link; note the link timings for EX and MX+.
5. **Do not flash a real module yet.** The first real flash is Phase 5: a low-risk module with a known-good backup, a verified seed/key plug-in and a VBF from a known source, EX first.

## v0.18.0

**J2534 pass-thru devices in `obdcli`.** Includes v0.17.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or with a J2534 device. Unsigned. Ford products only.

**Download:** the app as before (`ObdLinkApp-win-x64-v0.18.0.zip`; no app change apart from the version), plus `obdcli-win-x64` and the new **`obdcli-win-x86`** (for 32-bit J2534 drivers, which is most of them).

- `obdcli j2534-list` lists the J2534 v04.04 devices registered on the PC (both registry views), the driver's CPU type, and whether this build can load it.
- `--j2534 NAME|DLL` runs `info`, `scan`, `dtcs`, `live` and `asbuilt-read` through a J2534 device (genuine VCM II / VCMM, Mongoose-class, clones, a DIY device). `--j2534-demo` simulates one.
- Buses: HS-CAN on pins 6/14 (ISO 15765). MS-CAN and HS-CAN2 use J2534-2 pin selection (ISO15765_PS on pins 3/11). A device without it reaches HS-CAN only, and `info` says so.
- Through J2534, a module's "response pending" (0x78) is waited out instead of the request being sent again, which the ELM text interface cannot do. Long multi-frame replies get up to 5 s once their first frame arrives.
- Still read-only toward the vehicle. `monitor` needs an ELM327/STN adapter for now. The app itself does not use J2534 yet.

Internal: Core `J2534/` (constants checked against the Drew Technologies j2534_v0404.h header, `J2534DiagChannel`, `NativeJ2534Api` with Windows-ABI marshalling tested against a compiled stub driver, registry discovery, PE bitness check, `SimulatedJ2534Api`). `ModuleScanner` accepts a bus-select function.

### Windows + device verify
1. `obdcli --j2534-demo scan` lists the same modules as `obdcli --demo scan`, with ACM 727 found on MS-CAN.
2. With a J2534 device installed: `obdcli-win-x86\obdcli j2534-list` shows it (CPU type X86 for most drivers) with no "needs the x86 build" note. The x64 build adds that note for a 32-bit driver.
3. Truck with the J2534 device (FORScan and the app closed): `obdcli info --j2534 "<name>"` shows versions, battery, and whether MS-CAN is available. Then `scan`, `dtcs --module all`, and `asbuilt-read --module 726`. Compare with the OBDLink results.

## v0.17.0

**Command line: `obdcli` (read-only toward the vehicle).** Includes v0.16.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download:** the app as before (`ObdLinkApp-win-x64-v0.17.0.zip`), plus **`obdcli-win-x64`**, a separate self-contained `obdcli.exe` (CI artifact for now).

- `obdcli info | scan | dtcs | live | monitor | asbuilt-read | options`. Full reference: `docs/CLI.md`. `--json` for scripts.
- `--port COM5` for a real adapter, `--demo` for the simulator (no vehicle needed).
- It never writes to the vehicle: every UDS client has a closed write gate, and a test checks the source for write calls.
- `options` works offline on a FORScan `.abt`: list the community options with current values, or `--set "ADDR PATTERN=DIGITS"` into a **new** file (checksums recomputed, conflicts refused). Load it in the app to write it through the normal safe path.

Internal: the module scan with alternate-bus retries is now Core `ModuleScanFlow` (PR #29), shared by the app and the CLI.

### Windows + truck verify
1. Unzip `obdcli-win-x64`, run `obdcli --demo info`, `obdcli --demo scan`, `obdcli --demo dtcs`, `obdcli --demo monitor --bus ms --seconds 3`: output as in `docs/CLI.md`.
2. App scan on DEMO still marks modules found and remembers MS-CAN modules found by retry (same as v0.16.0).
3. Truck (close the app first; one program per adapter): `obdcli info --port COMx`, `obdcli scan --port COMx`, `obdcli dtcs --port COMx`. Compare the DTC list with the app's Read all modules.
4. Truck: `obdcli asbuilt-read --port COMx --module 726 --out bcm.abt`, then `obdcli options --abt bcm.abt --search tire` and compare current values with FORScan.

## v0.16.0

**As-Built change history and undo.** Includes v0.15.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.16.0.zip`** (normal Intel/AMD PCs).

- Every As-Built block write or restore that passes its read-back check is recorded in `asbuilt-history.jsonl` in the backups folder: when, module-block, write/restore, bytes changed, the data before and after, and the backup file taken before it. Writes that changed nothing, or failed their read-back, are not recorded (they are in the Events log as before).
- As-Built tab → **Change history** (next to Options and Compare to factory) lists them, newest first.
- **Undo last write of this module…** restores the backup taken before the module's most recent recorded change. It goes through the normal restore: VIN match, a fresh backup, battery check, typed WRITE and read-back. The undo is recorded too.
- The history stores backup file names, not full paths, so it keeps working when the portable Data folder is moved. It lives next to the backups (which already contain the full VIN) and is not added to the diagnostics capture.

### Windows + truck verify
1. DEMO: As-Built → BCM → Read → change a byte (and its Check) → Write + verify… (writes on). Change history shows one "write" row with the before/after data.
2. DEMO: Identify vehicle first (the undo, like any restore, needs the VIN), then Undo last write of this module… → confirmations → the grid after Read shows the old data, and Change history has a "restore" row on top.
3. DEMO: copy the whole Data folder elsewhere and run from there: Change history still lists the rows, and Undo still finds the backup.
4. Truck: no write is needed to check this. Leave history testing to DEMO until As-Built writes on the truck are verified.

## v0.15.0

**Phase 3 start: As-Built options by name.** Includes v0.14.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.15.0.zip`** (normal Intel/AMD PCs).

As-Built tab, new **Options (community sheet)** panel under the grid (Compare to factory moved to the tab next to it):
- **Refresh** lists the options the 2015-2020 F-150 community sheet (Livnitup, now shipped inside the app) documents for the lines in the grid. It shows the current digits and their meaning.
- **Sheet tab**: tabs describe the same address differently (for example SYNC 1 vs SYNC 3). The default for APIM, BCM and ABS comes from the 2017 F-150 profile; pick the tab that matches your module.
- **New**: pick a documented value from the list (hover for all meanings) or type raw digits. **Search** and **Changed only** filter the list; pending values are kept while filtered out.
- **Apply to grid** changes only the option's digits and recomputes each changed line's checksum. Two options that set the same digit differently are refused. **Nothing is written to the module here.** Then use **Write + verify…** per block: backup, confirmations, typed WRITE and read-back as before.
- Only rows that mark their digits with `*` are offered. Sheet rows with literal digits ("recipes") are not editable here.
- The meanings are community-reported, not Ford's. Compare with FORScan before writing.

Internal: Core `FeatureOption` (load, value parsing), `AsBuiltEditor.ApplyAll`; the sheet is embedded in Core.

### Windows + truck verify
1. DEMO: As-Built → BCM (726) → Read all blocks → Options → Refresh (the sheet has no PCM options). A tab is chosen and options are listed with Now / Meaning. Change one value: State shows "changed". Apply to grid: that line's Data and Check change, and OK stays ticked. Write + verify… on that block runs the usual backup and confirmations (DEMO).
2. DEMO: set two options that share a digit to conflicting values → Apply refuses with "Conflicting changes…".
3. Truck (read-only ON): APIM 7D0 and BCM 726 → Read all blocks → Options → Refresh. For a few options you know, compare Now/Meaning with FORScan's decoded view. **Do not write** until the meanings match FORScan for that module.

## v0.14.0

**Faster OBD-II live data. Read-only.** Includes v0.13.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.14.0.zip`** (normal Intel/AMD PCs).

- The dashboard and Live Data now ask for **up to six OBD-II PIDs in one request** (SAE J1979 multi-PID Mode 01 on CAN) instead of one request per PID. With 6 signals that is one round trip instead of six, so each signal updates more often at the same refresh setting.
- A PID that does not come back in a batch is read on its own in the same cycle, so no value is lost. If batches come back incomplete three times in a row (an ECU without multi-PID support), the app goes back to one PID per request for that polling run.
- Settings → Dashboard → **Ask for up to 6 OBD-II PIDs per request** (on by default) turns it off. It applies the next time polling starts.
- Ford enhanced (Mode 22) DIDs are still read one at a time: multi-DID support on Ford modules is unverified.

Internal: `J1979.ParseMultiPid`, `DashboardEngine.BatchObd` / `Mode01Requests`; simulator answers multi-PID requests (and can imitate an ECU that answers only the first PID).

### Windows + truck verify
1. DEMO: Live Data, pick rpm / speed / coolant / throttle / MAF / IAT, Start. In Adapter traffic, requests look like `010C0D11...` (several PIDs) and values update as before.
2. DEMO: untick the new setting, restart Live Data: one PID per request (`010C`, `010D`, …) and the same values.
3. Truck: same six signals with the setting on, then off. Compare the Samples column after 60 s each way (on should be clearly higher) and check the values agree with FORScan. If the Events log says "polling PIDs one at a time from now on", the PCM did not answer multi-PID requests; note it.

## v0.13.0

**Phase 2: bus monitor. Read-only: nothing is transmitted.** Includes v0.12.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.13.0.zip`** (normal Intel/AMD PCs).

New **Bus Monitor** tab:
- Pick **HS-CAN**, **MS-CAN** or **HS-CAN2** (the last two need an MX+ / EX). Start listens silently (ATCSM1 + ATMA): the adapter does not acknowledge or send frames.
- Grid per CAN ID: count, rate (Hz), DLC, latest data, **Changed bits** and last seen. Changed bits shows, per byte, which bits have changed since Reset stats, so live signals stand out while static bytes show `··`.
- **Filter**: `all`, `diag` (700-7FF), one ID (`7E8`, `18DAF110`) or id/mask (`700/780`). It is applied in the app; the adapter's own receive filters are not touched, so normal requests afterwards are unaffected.
- **Record CSV** writes every kept frame (`time,id,ext,dlc,data`) to the logs folder.
- If the adapter's buffer fills (BUFFER FULL, likely on a busy bus over Bluetooth), the monitor restarts and the status line counts the overflows.
- While it runs, the monitor owns the adapter. Other adapter commands are refused with a message (Connect/Disconnect stop it first). Live data stops, and modules kept in an extended session are released and return to their default session after 5 s. Afterwards the app restores formatting, header and receive filter before the next request.

Internal: Core `MonitorLine` (11/29-bit, spaces on/off), `IdFilter`, `MonitorStats`, `CanCsvLog`, `BusMonitor`; `ElmLink.MonitorAsync`. Stop sends a single CR from the cancellation callback, so it works even when a Windows serial read ignores cancellation. Exactly one CR: a second would repeat ATMA. Simulator streams synthetic broadcast frames per bus.

### Windows + truck verify
1. DEMO: Bus Monitor → HS-CAN → Start: IDs 167, 201, 3B3, 420 appear with rates. 201 and 3B3 show changed bits, 420 mostly `··`. Stop → status shows frames and frames/s.
2. DEMO: filter `201` → only 201; `diag` → nothing (no diagnostic traffic while monitoring). Record CSV for a few seconds → file in the logs folder.
3. DEMO: while monitoring, click Read module DTCs → refused with "The bus monitor is using the adapter…". Stop, then Read works. Disconnect while monitoring → monitor stops cleanly.
4. DEMO (MX+ / EX profile): MS-CAN → 080, 3B2, 433.
5. Truck, engine running, parked: HS-CAN `all` for 30 s. Note the frames/s and any overflows (Bluetooth vs USB if you have both). Then MS-CAN. Then Stop and confirm Read module DTCs and Live Data still work. Expect IDs to change with doors, lights and pedal; the app does not decode them.

## v0.12.0

**Phase 2: charts, replay and triggers. Read-only.** Includes v0.11.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.12.0.zip`** (normal Intel/AMD PCs).

Live Data tab:
- **Chart** under the grid. Tick **Plot** on up to 8 rows (the first 8 are ticked when you Start). Each signal gets its own lane with its own scale, its name and unit, the latest value and the window's high/low. Lanes share one time axis. Window: 10 s to 15 min. Peaks survive however many samples share a pixel. Hover shows a time cursor with every lane's value at that instant. A signal keeps its colour when you tick or untick others.
- **Triggers**: type `rpm > 3000` or `ect < 70 hys 2` and **Add**. Each crossing adds a line to the hit list and the Events log, plus a dashed line in the chart. With hysteresis, a value hovering on the threshold fires once, not every poll.
- **Replay**: **Open recording…** loads a CSV recorded by Live Data or the dashboard. **Play / pause** at 0.5x to 16x through the same grid, chart and triggers. No adapter traffic, and no connection needed. Unreadable lines are skipped and counted.
- Chart history keeps up to one hour per signal at 10 Hz (oldest dropped first).

Internal: Core `SignalHistory` (ring buffer per signal, min/max decimation), `LiveCsvLog` + `ReplayCursor`, `SignalTrigger` + `TriggerMonitor`, all unit-tested. The chart palette is the validated 8-slot categorical palette, checked against this app's light and dark card colours.

### Windows + truck verify
1. DEMO: Live Data → List signals → tick 4-5 → Start. The chart shows one lane per signal, moving left. Hover: the cursor shows each lane's value and the time. Untick one row's Plot: its lane goes, and the others keep their colours. Try ticking a 9th: refused with a message.
2. DEMO: Window 10 s / 60 s / 300 s rescales; Clear chart empties it. Light and Dark themes both readable (lane titles in text colour).
3. DEMO: trigger `rpm > 1500` (or a signal you picked): a hit line and a dashed marker appear when it crosses, once per crossing.
4. DEMO: Record CSV for ~30 s, Stop. Open recording… → that file: Play replays it into grid + chart. 4x is faster, pause and resume work, and Close replay clears it. Adapter traffic shows nothing during replay.
5. Truck: chart rpm / speed / ECT / TFT on a short drive. Replay the recording afterwards and compare with FORScan's graph of the same drive if you have one.

## v0.11.0

**Ford Enhanced UDS, first write-side service: RoutineControl (31), with session keep-alive.** Includes v0.10.0 and earlier, plus the UDS protocol core, read-side services and routine control in Core (PRs #17, #19-#21). Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.11.0.zip`** (normal Intel/AMD PCs).

New **Routines** tab:
- **Extended session (10 03)** / **Default session (10 01)** for the selected module. Allowed in read-only mode. While a module is in a non-default session the app sends a silent TesterPresent (3E 80) whenever the module has been idle for 2 s, so the session does not time out (S3 = 5 s) between clicks. The line under the buttons shows which modules are being kept alive. If a keep-alive fails, the list says so and the module is treated as back in its default session.
- **Start… (31 01)**, **Results (31 03)**, **Stop… (31 02)** for a 2-byte routine ID you type, with an optional hex option record. All three need writes enabled (Settings). Start also checks the battery voltage and asks you to type **RUN**. Replies are listed with their status bytes; a refusal shows the NRC with its meaning. Routines never try another bus.
- **Always refused**, whatever the settings: airbag/pretensioner deployment routines (**E200-E2FF**, also blocked in the raw console and on every internal path) and flashing routines (**FF00/FF01**).
- The app ships **no Ford routine IDs**. It does not know what a routine does; only run IDs from a source you trust.
- DEMO modules accept routine **1234** only, and only in the extended session: Start → status 01 (running), first Results → 01, next Results → 00 55 (done), Stop → accepted.

Internal: `UdsSessionHost` (one client per module per connection, keep-alive under the shared adapter lock), `UdsRoutines`, `RoutineCommand`; Core only, no UI yet (bench-first): 85 ControlDTCSetting, 28 CommunicationControl and 2F InputOutputControl, each returning a lease that puts the module back (85 01 / 28 00 / 2F did 00, retried, allowed even after read-only is switched on); simulator: suppressed 3E 80 is silent (as on real modules), 31 routine 1234, ECU reset returns to the default session.

### Windows + truck verify
1. DEMO, read-only ON: Routines tab, select PCM, Extended session → the list shows "session Extended confirmed (P2 50 ms, P2* 5000 ms)" and the line below says "Kept alive … 0x7E0 Extended". Adapter traffic shows `3E80` about every 2 s and nothing else while idle. Start… with ID 1234 → blocked by read-only mode, nothing sent.
2. DEMO, writes ON: Start… 1234 → battery check, two confirmations, type RUN → "start 0x1234: status 01". Results twice → 01, then 0055. Stop… → "accepted". Default session → keep-alive stops (no more 3E80 in traffic).
3. DEMO: ID E200 or FF00 → refused with a message, nothing in adapter traffic. Console with writes on: `3101E200` is blocked.
4. DEMO: Start 1234 without the extended session → NRC 0x7F line in the list. Disconnect while in extended session → no errors; reconnect → no module listed as kept alive.
5. Truck (read-only ON): PCM → Extended session, watch traffic for 30 s: 3E80 every ~2 s, no errors, Live Data still works alongside. Default session afterwards. **Do not start routines on the truck** until a routine ID comes from a trusted, documented source.

## v0.10.0

**Phase 2: live-data browser. Read-only.** Includes v0.9.0 and earlier. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.10.0.zip`** (normal Intel/AMD PCs).

New **Live Data** tab:
- **List signals** reads which OBD-II Mode 01 PIDs the vehicle supports and lists them alongside the Ford enhanced (Mode 22) DIDs from the editable table. Unverified DIDs are labelled.
- Tick any set of signals and **Start**: each is polled live with current value, running **min / max / average** and a sample count. The poll engine is the same one the dashboard uses, so only services 01 and 22 are ever sent.
- **Record CSV** writes every poll cycle (base units) to the logs folder; **Reset min/max** clears the stats.
- Polling shares the adapter with other commands (it yields whenever a command needs the bus) and stops on disconnect.

Internal: the live-data catalog and the per-signal aggregator are in Core (`LiveSignal`, `LiveReadings`) with unit tests; the view model only drives them.

### Windows + truck verify
1. DEMO: Live Data → List signals shows OBD-II PIDs (rpm, coolant...) plus the Ford DIDs. Tick a few, Start: values update, min/max/avg fill in. Record CSV writes a file in the logs folder; Stop saves it.
2. Truck: List signals, tick rpm / speed / coolant / MAF and a couple of enhanced DIDs, Start, and compare the live values and min/max against FORScan's gauges while driving.
3. Adapter traffic during live data shows only 01 and 22 requests.

## v0.9.0

**Phase 1 continued: Mode 6 and module identification. Read-only.** Includes v0.8.0, v0.7.0 and v0.6.1. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.9.0.zip`** (normal Intel/AMD PCs).

- **Mode 6 tests (06)** on the DTCs page: every supported monitor (OBDMID) from every ECU, grouped by monitor, with value, limits and PASS/FAIL. Pass/fail compares the raw numbers, so it is right even when a unit is not in the table; known units use the SAE J1979 scaling (unknown ones are shown raw with their UASID). Ford's own tests (TID 0x80+) are labeled "Manufacturer test".
- **Module identification** grid on the Vehicle page: bus, address, module, software (F188), core assembly (F111), assembly (F113), calibration (F124), serial (F18C) and As-Built, from Identify vehicle. **Export CSV…** saves to `Data\exports`; serial numbers are blanked unless you choose to keep them.

### Windows + truck verify
1. DEMO: Mode 6 shows 4 tests, 1 FAIL, including "Catalyst bank 1 (MID 21)" with 0.25 :1.
2. DEMO: Identify vehicle, then the Vehicle page grid lists the modules; Export CSV with No: serials read REDACTED.
3. Truck: Mode 6 next to FORScan's Mode 6 page. Every MID/TID and PASS/FAIL should match; note any value whose scaling differs (that UASID needs a fix).
4. Truck: Identify vehicle, compare the grid's F188 values with FORScan's module info.

## v0.8.0

**Phase 1 of the FORScan roadmap: DTC detail. Every new function is read-only.** Includes v0.7.0 and v0.6.1. Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only.

**Download `ObdLinkApp-win-x64-v0.8.0.zip`** (normal Intel/AMD PCs).

DTCs page:
- **State** column for module DTCs: confirmed / pending / history, failing now, lamp (from the ISO 14229 status byte).
- **Details…** for a selected module DTC: all 8 status bits named, freeze-frame (snapshot) records (19 03 → 19 04) and extended data (19 06). Layouts are module-specific, so values are shown raw; a record with one identifier shows its DID.
- **Pending (07)** and **Permanent (0A)** OBD-II DTCs.
- **Freeze frame (02)**: the DTC that stored frame 0 and every supported Mode 01 value the app can decode.

### Windows + truck verify
1. DEMO: Read module DTCs on the BCM: State shows "confirmed, failing now". Details…: bit 3 confirmedDTC ticked, one snapshot record with DID 0xDD00, extended record 01.
2. DEMO: Pending shows P0171, Permanent shows P0301, Freeze frame shows P0301 with 1000 rpm and 50 °C.
3. Truck (read-only): Read all modules, open Details on two DTCs, and compare the status with FORScan. Freeze frame against FORScan's freeze frame for the same code.
4. Adapter traffic during all of the above shows only 01/02/03/07/0A/19/22/3E/10 requests, never 04 or 14.

## v0.7.0

**Includes every v0.6.1 fix below.** Built and unit-tested on Linux and by GitHub Actions on Windows; not yet run by hand on Windows or on the truck. Unsigned. Ford products only. Portable, no installer. Read-only is the default at every start.

**Download `ObdLinkApp-win-x64-v0.7.0.zip`** (normal Intel/AMD PCs).

**Security access (seed/key) plug-ins.** Some modules only accept writes after UDS SecurityAccess (0x27). The app still ships **no** algorithms or keys. You can now add your own:
- Settings → **Security access (seed/key)** → **Use seed/key plug-ins** (off by default; turning it on asks first).
- **Add plug-in…** picks a .NET 8 `.dll` that implements `ISeedKeyProvider`, shows its SHA-256, and asks you to type `TRUST`. It is copied to `Data\plugins\seedkey\`. A file whose hash you did not approve is listed but never loaded; rebuilding a plug-in changes its hash, so approve it again.
- **As-Built level** (odd hex, e.g. `01` or `03`): unlock before As-Built writes. Empty = never unlock.
- A wrong key is sent once and stops the write (NRC 0x35); nothing is written. A module that says "security access required" (NRC 0x33) now tells you what to set.
- Template: `tools/SeedKeyPluginSample` (it only answers the DEMO simulator, never a real module).

A plug-in runs inside the app with your user's rights. Only approve code you built or trust.

### Windows verify
1. Settings: the Security access card shows "Off. No plug-in is loaded." Turn it on: a warning asks first. Turn it off and on again.
2. Build `tools/SeedKeyPluginSample` (`dotnet build -c Release`), Add plug-in…, type TRUST. Status lists `DemoSeedKeyProvider`. Restart: still loaded.
3. Append a byte to the copied dll in `Data\plugins\seedkey\`, press Reload: listed as not approved, nothing loaded.
4. As-Built level: type `02`: refused (log warning), value unchanged. Type `01`: kept after restart.
5. DEMO: with level 01 and the sample loaded, write BCM block 01: succeeds; Adapter traffic shows `2701` then `2702332211` before `2E`.

## v0.6.1

**Write-safety release from the 2026-11-15 code audit.** Built and unit-tested on Linux and by GitHub Actions on Windows (`windows-latest`); not yet run by hand on Windows or on the truck. The exe is still **unsigned**. Ford products only. Portable, no installer. Read-only is the default at every start.

**Download `ObdLinkApp-win-x64-v0.6.1.zip`** (normal Intel/AMD PCs). CI also uploads an unsigned x64 build for every push.

### Fixed (each has tests in `tests/ObdLink.Core.Tests/WriteSafetyTests.cs`)

- **Write could send the wrong data to a block.** Write joined every row in the As-Built grid, so after *Read all blocks* or *Import .abt* it sent several blocks (or another module's lines) into the one selected block. Now only the selected module's lines for the selected block are used, they must run 01..n with no gaps, and the module's current length must match. A different length is never written. Identical data writes nothing.
- **Typed checksum, as in FORScan.** The grid has a **Check** column. Editing Data no longer fixes the checksum for you; the write is refused until the typed checksum matches the data, so a typo in Data is caught.
- **Restore made no backup and did not check the vehicle.** Restore now requires the connected VIN (Read VIN or Identify first), refuses a backup from another VIN, checks every block's length, saves a `_pre-restore` backup of the current contents, and skips blocks that already match.
- **Console could redirect the next module request.** `ATSH`, `ATZ`, `STP` and similar console commands changed the adapter without the app knowing, so the next request could go to the wrong module (for example a BCM request landing on the PCM). Any state-changing AT/ST console command now makes the app re-apply echo/headers/formatting, its protocol and the target header before its next request.
- **A link drop could resend a write.** After a Bluetooth/serial drop the link re-sent whatever command was in flight, including writes, without re-initializing a possibly reset adapter (default header 7DF = every module). Vehicle requests are now never resent; the app reopens the link, re-applies its state on the next request, and tells you the request may or may not have reached the vehicle. Adapter-only AT/ST commands are still retried.
- **"Response pending" write reported as a failure.** A module that answers 0x78 (or does not answer, or the link drops) may still have stored the data. The app now waits, reads the block back, and reports what is actually in the module. A real rejection (for example NRC 0x22) is still an error.
- **Late prompt after a timeout.** The adapter's late `>` from an aborted command is now drained, so it cannot end the next command early.
- **Multi-frame transmit (every As-Built write).** Flow control WAIT, OVERFLOW and block size are handled; intermediate frames use a ~100 ms response wait (ATST19) so the gap stays inside the module's ~1 s timeout. CAN auto-formatting and normal timing are restored even when a transfer fails.
- **UDS replies.** Only the addressed module's reply counts; a final reply wins over a "pending" in the same read; an empty SecurityAccess seed is an error instead of "already unlocked".
- **HS-CAN2** is tracked as its own bus instead of being recorded as MS-CAN 125k.
- **Updater signature name** must be an accepted publisher name, not merely contain "ScanTool".

### Project
- `ObdLink.sln` replaces `ObdLink.slnx` so the build works with the .NET 8 SDK pinned in `global.json`.
- GitHub Actions CI on Windows and Linux (`.github/workflows/ci.yml`).
- Removed the remaining Dodge/Ram/Chrysler references from code comments and docs.
- `docs/ROADMAP-FORSCAN-PARITY.md`: what it takes to reach FORScan-level features, in phases, and the decisions that need Dale.

### Windows + truck verify
1. DEMO: read block 01 of BCM, change one byte without retyping Check. Write is refused and names the line. Retype Check, write: the confirmation says "1 of N bytes change"; the result shows the backup path.
2. DEMO: *Read all blocks* on the BCM, select block 01, write. Only block 01 changes (compare with a re-read).
3. DEMO: Restore a backup before reading the VIN: refused. Read VIN, restore: a `_pre-restore` file appears in `Data\backups`.
4. DEMO: console `ATSH7E0`, then *Read As-Built block* on the BCM. It still reads the BCM (Adapter traffic shows `ATSH726` again).
5. Truck, read-only: Identify vehicle, Read all modules' DTCs, Read all As-Built blocks on two modules, MS-CAN included. Compare with FORScan.
6. Truck, one write you have FORScan-verified before (battery maintainer on): write, then confirm in FORScan. Then restore the backup and confirm again.

## v0.6.0

**Compiled on Linux. This build still needs a Windows pass** (steps below). v0.3.0 through v0.3.5 are **Windows-tested** (Dale confirmed). v0.4.0, v0.5.0, v0.5.1, and v0.5.2 still need that same Windows pass. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64. MIT license. Read-only is the default at every start. No firmware is bundled.

**Download `ObdLinkApp-win-x64-v0.6.0.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.6.0.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download. The ARM64 zip was built and was not run on ARM64 hardware.

**Dashboard pop-out.** The dashboard is no longer a page on the left rail. The main window opens on Modules. View → Dashboard, the header Dashboard button, or Ctrl+Shift+G opens one resizable window titled Dashboard - OBDLink Diag. Doing that again brings the same window to the front. Closing the main window closes the dashboard window and the process exits. Window size and position are saved in `Data\settings.json`.

**Keep live.** Closing the dashboard window stops polling unless **Keep live** is on (the checkbox on the dashboard window, and View → Keep dashboard live while closed). Keep live is remembered in `Data\settings.json` and, when it is on, polling continues while the window is closed and after a restart once you connect. Reopening resumes polling that stopped because the window closed. Pause (Ctrl+P) is separate: it stays on until you resume, including across close and reopen. With Keep live off, a fresh launch that never opens the dashboard stays Stopped.

**Gauge face.** Each needle-capable card (the ones that offer Dial) can be Analog or Digital on its own. Analog draws the needle and arc. Digital draws the title, the number, and the unit in the same card. Gear, tire pressure, the MIL lamp, and text-only tiles do not have that control. The choice is saved on each item in `Data\settings.json`.

**Material backgrounds.** The v0.5.2 cabin photographs are gone. The Background box offers **Fine wood grain**, **Brushed aluminum**, **High-gloss black**, **Diamond-plate metal**, **Carbon fiber**, and **Sports-car leather**, plus **None** and **Custom**. Each built-in photo covers the window once with cover stretch (`UniformToFill`). It is not tiled. A saved cabin id from v0.5.2 (Analog, Wood, Steel, Mirror, Onyx, Leather, Carbon, Brushed) loads as None. Pick image still copies a PNG, JPEG, BMP, or GIF into `Data\backgrounds` only.

The photographs were generated for this app and ship under the MIT license. They are pictures of material surfaces, not a vehicle cabin, not Ford marketing photos, and not files copied from the web. See `assets/backgrounds/PROVENANCE.txt`.

Nothing is written outside `Data\`. There is no installer.

### Windows verify

Run these on Windows 10 version 1607 or later, or on Windows 11, using `ObdLinkApp-win-x64-v0.6.0.zip`. The ARM64 zip was built in the same release and was not run on ARM64 hardware. Run the same checks on ARM64 Windows before calling that zip Windows-tested.

1. Launch the app. The left rail has no Dashboard item. The selected page is Modules. Alt+1 selects Modules. Alt+9 does nothing.
2. View → Dashboard, the header button, and Ctrl+Shift+G each open one window. Invoke any of them again: the same window activates, including when it was minimized. No second window appears.
3. Connect DEMO. With the window open, status is Live and values move. Pause: status Paused and values hold. Resume: Live.
4. Close the window. Status is Paused. Adapter commands on Modules still run. Reopen: status returns to Live. The log shows one new polling start for that reopen, and it does not show two polling loops overlapping.
5. Pause, close, reopen. The window stays Paused until Resume.
6. Turn Keep live on, close the window. The main-window status stays Live. Reopen a few seconds later: DEMO gauge values have changed since the close. Restart the app, connect again, leave the window closed. Status is Live. Turn Keep live off from the View menu. Polling stops.
7. Customize: set RPM to Analog and speed to Digital, both Dial. Both are visible together. Done, close, restart, reopen. The faces and the order match. Narrow the pop-out until gauge-height cards stack, widen it, and confirm the arranged width returns. Drag and the Move buttons still reorder.
8. Pick each material. The photograph covers the canvas once and is not repeated. Switch Light and Dark: cards stay readable. Pick None: the theme color returns. Pick image, restart: the file is under `Data\backgrounds` only and still covers the canvas.
9. Start from a copy of a v0.5.2 `settings.json` whose background id is `Analog`, then one whose id is `Wood`. After load the background is None and the app opens. Layout items are still there.
10. Close the main window while the dashboard window is open. The process exits, and the next launch restores the dashboard window's size and position. Move the saved position off every monitor and launch again: the window comes back on screen at a usable size.

## v0.5.2

**Compiled on Linux. This build still needs a Windows pass** (steps below). v0.3.0 through v0.3.5 are **Windows-tested** (Dale confirmed). v0.4.0, v0.5.0, and v0.5.1 still need that same Windows pass. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64. MIT license. Read-only is the default at every start. No firmware is bundled.

**Download `ObdLinkApp-win-x64-v0.5.2.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.5.2.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

**Dashboard backgrounds.** The built-in wood, steel, mirror, onyx, leather, carbon, and brushed textures are gone. Those were small drawings repeated across the dashboard (`TileMode.Tile` on a tiny viewport), which is why they looked tiled and low resolution. Each built-in is now one late-model full-size pickup cabin photograph, drawn once with cover stretch so it fills the dashboard and is cropped to fit. It is not tiled.

The list includes both kinds of cluster:

- **Analog gauges** — physical needles, round speedometer and tachometer, the layout of a 2015-2020 truck.
- **Digital dash** — full digital instrument cluster and a wide center screen, daylight.
- **Digital cluster** — close-up of a digital cluster.

Also: Night cabin, Highway view, Dusk cabin, Black leather cabin, and Center stack. **None** is still the app theme color. **Pick image** still copies a PNG, JPEG, BMP, or GIF into `Data\backgrounds` (15 MB or smaller) and that picture uses the same cover stretch. A light dark overlay sits on the photo so the gauge cards stay readable in light and dark themes. The cards themselves are unchanged.

A background saved in an older version still loads. Wood now shows the digital dash, Steel the digital cluster, and the other saved ids show the new cabin photos. Nothing is written outside `Data\`.

The photographs were generated for this app and ship under the MIT license. They are not Ford marketing photos and were not copied from the web. See `assets/backgrounds/PROVENANCE.txt`.

### Windows verify

1. Dashboard → Background. Pick **Analog gauges**, then **Digital dash**, then **Digital cluster**. Each picture fills the area once. You should not see a repeated tile. Switch View → Theme to Light, then Dark. The gauge cards stay readable on both.
2. Pick **None**. The dashboard goes back to the app theme color.
3. **Pick image**, choose a picture, restart. It is still selected, the file is under `Data\backgrounds` only, and the picture covers the dashboard without tiling.
4. The v0.5.1 checks still apply: Customize **Move left / right / up / down** and **Drag** still reorder cards.

## v0.5.1

**Compiled on Linux. This build still needs a Windows pass** (steps below). v0.3.0 through v0.3.5 are **Windows-tested** (Dale confirmed). v0.4.0 and v0.5.0 still need that same Windows pass. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64. MIT license. Read-only is the default at every start. No firmware is bundled.

**Download `ObdLinkApp-win-x64-v0.5.1.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.5.1.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

**Fix: dashboard cards did not move in Customize mode.** Dragging a gauge onto another gauge did nothing: Windows only drop-targets the element under the pointer, and the gauge covered the card border that was marked to accept a drop. Customize now shows **Move left / right / up / down** and a **Drag** grip. Drop a card on another card (or on a gap) to reorder. With Customize off, the layout stays put.

Saved order, display, size, and the width you arranged at still go to `Data\settings.json` (v0.4.0). Gauge-height cards still stack only when the window is narrower than the widest saved card. Backgrounds are unchanged.

### Windows verify

1. Dashboard → **Customize**. Each card shows **Move left**, **Move right**, **Move up**, **Move down**, and a **Drag** grip. Drag a gauge onto another gauge: the cards change places. Drop on an empty gap: the card moves to the nearest slot. **Done**, then restart. The order is unchanged.
2. With Customize off, dragging a card does nothing.
3. Narrow the window until bars and tiles stack, then widen it. Saved sizes and the arranged width come back.
4. The v0.5.0 checks still apply: DEMO TCM As-Built, the no-As-Built info message, nav icons, a background image, and UNVERIFIED gear.

## v0.5.0

**Compiled on Linux. This build still needs a Windows pass** (steps below). v0.3.0 through v0.3.5 are **Windows-tested** (Dale confirmed). v0.4.0 still needs that same Windows pass. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64. MIT license. Read-only is the default at every start. No firmware is bundled.

**Download `ObdLinkApp-win-x64-v0.5.0.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.5.0.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

**DEMO TCM As-Built.** DEMO TCM (0x7E1) now answers synthetic As-Built blocks DE00-DE07, same shape as PCM, so Read all blocks, Read, and Backup can be exercised. The bytes are made up. A 2017 F-150 factory `.ab` has no TCM blocks (the 6R80 data lives with the PCM). On a real module, NRC 0x31 for DID DE00 means that module has no As-Built.

**Modules with no As-Built.** If DE00 comes back NRC 0x31 (requestOutOfRange), the app treats that as "no As-Built". Read, Backup, and Write stay off, the module drops out of the As-Built list (Show all modules puts it back, still with the actions off), and an info message explains it. The Events log records that at Information, not as `UdsNegativeResponseException`. Modules that do answer DE00 keep working. A timeout does not hide the module.

**Bus memory.** When a real module answers on HS-CAN, HS-CAN2, or MS-CAN, that bus is saved in `Data\settings.json` and tried first on the next request and the next connect. DEMO sessions do not write this map, so a simulated bus cannot stick to a later real connection.

**Gear and OSS stay UNVERIFIED.** Gear DID **1E12** and output-shaft DID **1E13** are not marked verified. The gear tile title is "Gear (UNVERIFIED)", and the source line is shown in the warning color (PCM DID, or the RPM/speed estimate when 1E12 does not answer). OSS still asks PCM 0x7E0 first, then TCM 0x7E1, and the title and subtitle say UNVERIFIED. Tire-pressure DIDs **2813-2816 stay verified**.

**Truck pass before either DID is called verified** (do not invent a replacement DID):

- Gear 1E12: with the ignition on, shift P, R, N, D and a couple of forward gears. The tile should match the cluster. If it does not, leave 1E12 unverified.
- OSS 1E13: at a steady speed, compare the rpm tile with `mph × axle ratio × 336 / tire diameter in inches` (3.55 and the door-sticker tire size are the usual F-150 starting point). PCM 0x7E0 is asked first, then TCM 0x7E1. If neither matches, leave 1E13 unverified.

**Navigation.** The left rail can now actually reach icon-only: the window minimum height is 480 and the content row can shrink to 160. Labels hide when fewer than four labeled tabs fit, and they come back only when five would fit, so the boundary does not flicker. Dashboard cards that have stacked stay stacked until there is real extra width (about 24 px), so a scrollbar does not bounce them. Background images are still copied only into `Data\backgrounds`.

**DEMO and Identify.** Firmware check is still skipped for a simulated adapter, including the first run. Identify vehicle rebuilds the module list even when the profile name did not change, and a module list from a sweep with no 17-character VIN is applied too. Choosing a real COM port drops a DEMO profile instead of keeping those simulated modules.

### Windows verify

1. Connect **DEMO-EX**. Modules → Transmission Control Module (TCM, 0x7E1) → As-Built → **Read all blocks**, then **Backup**. Both finish, a file `*7E1*.asbuilt.json` appears under `Data\asbuilt`, and there is no error dialog.
2. As-Built → **Show all modules** → Audio Control Module (ACM, 0x727, MS-CAN). It has no DE00 blocks. You get an info message ("no As-Built"), not an `UdsNegativeResponseException` error. Read, Backup, and Write stay off. Turn Show all modules off and ACM leaves the list. PCM read and backup still work.
3. Shrink the window until the left rail shows only icons, then grow it until the labels return. They should not flicker at the boundary. Alt+1..9 still change pages.
4. Dashboard → pick Wood grain, then **Pick image**. Restart. The picture is still selected and the file is under `Data\backgrounds` only.
5. The status bar says read-only at startup. There is no firmware banner on DEMO-EX. Gear says UNVERIFIED. Add Output shaft speed: it says UNVERIFIED, and tire pressures do not.
6. If you have a real EX or MX+: connect, scan, and read As-Built on a module that supports it (PCM or BCM). A module that returns NRC 0x31 shows the info message and stays off. Disconnect and connect again: a module that answered on MS-CAN or HS-CAN2 is tried on that bus first.

## v0.4.0

**Compiled on Linux. This build still needs a Windows pass** (steps below). v0.3.0 through v0.3.5 are **Windows-tested** (Dale confirmed). The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64. MIT license. Read-only is the default at every start.

**Download `ObdLinkApp-win-x64-v0.4.0.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.4.0.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

**Left navigation.** The page rail (Dashboard, Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings) scrolls vertically when the window is short. Shrink it further and the captions hide, leaving the icons; they come back when there is room. Alt+1 through Alt+9, the menus, and the light/dark theme are unchanged.

**Firmware check skips DEMO.** Connecting a simulated adapter, including the first time you try the app in DEMO, does not contact OBDLink's firmware feed. Ctrl+Alt+F explains that it was skipped. A real adapter on the internet still shows the banner and can download the official package into `Data\firmware` (download only; the app never installs firmware and never bundles it).

**Dashboard.** Customize still remembers order, display, and size in `Data\settings.json`, and now also the width you arranged at, so a wider window keeps those rows. Gauge-height cards (bars, tiles, lamps) stack to the window width only when the dashboard is narrower than the widest saved card. Dials and the tire widget keep their saved size, clamped so they are not clipped.

**Dashboard backgrounds.** Wood grain, steel plate, mirror, onyx black, sports-car leather, carbon fiber, and brushed aluminum, or pick an image. The choice is stored in `Data\settings.json`. A custom image is copied into `Data\backgrounds` (PNG, JPEG, BMP, or GIF, 15 MB or smaller). Nothing is written outside `Data\`.

**Tire pressure DIDs 2813-2816 are verified** (Dale, BCM 0x726, LF/RF/RR/LR). The dashboard no longer marks them UNVERIFIED. Output shaft speed DID **1E13 stays UNVERIFIED**.

### Windows verify

1. Shrink the window height until the left rail shows a scrollbar. Shrink further until only icons show, then grow it until the labels return. Alt+1..9 and View → Theme (Light and Dark) still work.
2. Connect **DEMO-EX**. There is no firmware banner, and the Events log does not show a feed request. Ctrl+Alt+F says the check is skipped in DEMO.
3. Connect a real EX or MX+ while online. If the adapter is behind, the banner offers **Download firmware**. The zip lands in `Data\firmware` and the app does not install it.
4. Dashboard → Customize: reorder a card, click Done, restart. The layout is unchanged. Zoom in (Ctrl++) or use a Wide card until it cannot fit: bars and tiles stack. Zoom back out: saved sizes return.
5. Pick Wood grain, then **Pick image** and choose a picture. Restart. The picture is still selected and the file is under `Data\backgrounds`. `Data\settings.json` has `Background`.
6. The status bar says read-only at startup. Tire pressures do not say UNVERIFIED. Add Output shaft speed: it still says UNVERIFIED.

## v0.3.5-test

**Untested on Windows (new build).** Compiled on Linux. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64.

**Download `ObdLinkApp-test-win-x64-v0.3.5.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.3.5.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

- **Demo VIN is fully synthetic.** DEMO now always reports `1FTDEMX95HF000001`; no part of a real VIN is in the app, tests or docs any more (test fixtures use `XXXXXXXXXXX000000`). Vehicle profiles saved from DEMO by older versions are still labeled `[DEMO]`.
- **Hidden failures are now logged.** Errors that used to be silently ignored now appear in the Events/app log: a link or folder that could not be opened (including one refused by the trusted-site check), an unreadable vehicle profile or settings.json, failed update checks, updater fallbacks, signature-check problems and clipboard errors. Harmless cases (port already gone, temp-file cleanup, crash-file fallbacks) stay quiet and are commented in the code. See docs/AUDIT.md (F2, F3).

## v0.3.4-test

**Untested on Windows (new build).** Compiled on Linux. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64.

**Download `ObdLinkApp-test-win-x64-v0.3.4.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.3.4.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

**New: Output shaft speed (OSS) dashboard item.** Click **Customize** and add "Output shaft speed (OSS)" (it is not on the default layout). It shows rpm from a Ford Mode 22 DID that is **unverified** (1E13, raw/4). The app asks the PCM (0x7E0) first, then the TCM (0x7E1), and remembers which one answered. Read-only. DEMO shows a simulated value. An existing `Data\ford-enhanced-pids.json` keeps your edits and gains the new entry.

**Code audit** (full report in docs/AUDIT.md):
- Links and folders are only opened if they are https links to known sites (GitHub, Motorcraft, OBDLink/ScanTool), Explorer, or a full local path.
- A button's background task can no longer crash the app if its error handler fails.
- Hiding and showing the dashboard quickly no longer starts two polling loops at once.
- A COM port that fails to open is closed and released right away (it no longer stays "in use").
- Online VIN decode releases its network connection after each Identify.
- Clearing the default port in Settings no longer writes an empty (null) value to settings.json.
- Device-change messages are read safely on 64-bit Windows.
- Compiler nullable warnings fixed; American spelling throughout (canceled, labeled, license, color, behavior); codespell runs clean with `.codespellrc`.

## v0.3.3-test

**Untested on Windows (new build).** Compiled on Linux. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64.

**Download `ObdLinkApp-test-win-x64-v0.3.3.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.3.3.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

**New: rearrange the dashboard in Customize mode.**
- Click **Customize** on the Dashboard. Each card gets a toolbar.
- **Reorder:** drag a card (grab it by the ⠿ handle or any empty spot) and drop it on another card. Or use ◀ ▶ (one place) and ▲ ▼ (one row).
- **Keyboard:** Tab to any button on a card's toolbar, then press Ctrl+Left/Right (one place), Ctrl+Up/Down (one row) or Ctrl+Home/End (first/last). Focus stays on the card, so you can keep pressing.
- **Size:** each card has a size box: Small, Medium (20% bigger), Large, or Wide (two columns).
- **Reset to default** puts back the built-in items, order and sizes for the current layout.
- Every change is saved at once to Data\settings.json (Dashboard.Layouts) in the app folder. Named layouts ("Save as" and the Layout box) keep their own order and sizes. Older settings files load unchanged.
- Click **Done** to leave Customize. The dashboard stays read-only; only the items shown are polled.

Also includes all v0.3.2 fixes (real COM port preferred over DEMO, read-only at every start, demo vehicle profiles never used on the real adapter, HS-CAN2 bus fallback, friendly no-reply message).

## v0.3.2-test

**Untested on Windows (new build).** Compiled on Linux. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64.

**Download `ObdLinkApp-test-win-x64-v0.3.2.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.3.2.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

**Fix: a DEMO vehicle profile stayed selected after connecting the real EX** (Identify/Vehicle showed VIN ...000001, F188=DEMO-..., and only IPC/BCM/PCM/ACM).
- Cause: "Identify vehicle" on the DEMO adapter saved a profile to Data\vehicles and selected it. That stayed selected when you switched to COM4, so the module list (and the buses they were tried on) came from the simulator.
- Profiles are now tagged as demo (and v0.3.1 demo profiles are recognized by the synthetic VIN / DEMO part numbers) and shown as "[DEMO] ...". When a real adapter connects, a demo profile is dropped, the built-in Ford module table is loaded, and the module list is rebuilt on every connect. All services (identify, scan, DTCs, As-Built) always use the connected adapter.

**Fix: the Module box showed "ObdLink.App.ModuleRow"** - it now shows the module name, address and bus everywhere.

**Fix: reading DTCs (19 02) from IPC 0x720 gave TimeoutException.**
- Your capture showed BCM 0x726 and PCM 0x7E0 answering on HS-CAN. IPC 0x720 did not answer on HS-CAN, and MS-CAN at 125k gave CAN ERROR. On a 2015+ F-150 the pins 3/11 bus is HS-CAN2 at 500k, so the app now tries HS-CAN, then HS-CAN2 (pins 3/11 at 500k), then MS-CAN (125k), and remembers the bus that answers. Scan modules does the same retry.
- Requests now set flow control (ATFCSH/ATFCSD/ATFCSM1) and a longer adapter timeout (ATST96). UDS 19/22/31 requests get 5 s instead of 1.5 s (the PCM DTC list took longer than 1.5 s).
- If nothing answers, you get a friendly message suggesting Scan modules and the ignition, instead of TimeoutException. Raw traffic is in the Events log.

**Fix: the port list stayed on "SIM DEMO-MX+" and the real OBDLink EX (USB, FTDI COM port) never got picked, so the dashboard showed simulated data.**
- Cause: the DEMO entries were added at the top of the list and the app selected the first entry. It also kept the previous selection on refresh, so DEMO stuck. Connect/auto-connect also fell back to DEMO when nothing was selected.
- The port list now shows every COM port: `SerialPort.GetPortNames` (registry) merged with WMI `Win32_PnPEntity` (Device Manager names), sorted by number. Real ports come first, DEMO last.
- FTDI ports (PnP `FTDIBUS` / `VID_0403`, used by the OBDLink EX) are labeled **OBDLink EX / FTDI**, e.g. `USB  COM4  OBDLink EX / FTDI  (USB Serial Port (COM4))`.
- Default selection: your current real port, then the saved default port, then FTDI, other USB, Bluetooth, anything else. **DEMO is only used when you pick it yourself**; it is never auto-selected and never used as a fallback.
- Refresh rescans and moves off DEMO when a real adapter appears (unless you picked DEMO yourself).
- **Rescans automatically** at startup (again ~1.5 s after the window opens, since FTDI ports can show up late) and whenever a USB device is plugged in or removed (WM_DEVICECHANGE), while not connected. Before, the list was only right after clicking Refresh.
- **Remembers the last real port** that connected (`Adapter.LastPort` in `Data\settings.json`) and preselects it next time.
- **Read-only is the default at every start.** "WRITES ENABLED" showed on a first real connection because a `ReadOnlyTestMode: false` saved earlier (e.g. while testing demo mode) carried over in `Data\settings.json`. Turning writes on now lasts for the current session only; the app logs a warning when it resets it.
- EX detection confirmed for STN2231 (Dale's EX reports STN2231 v5.13.1); STN2230/2231/2232 are all treated as OBDLink EX.
- **Manual COM entry**: type e.g. `COM4` next to Refresh and click **Use**.
- **No ports found**: a yellow bar suggests installing the FTDI VCP driver and checking Device Manager → Ports (COM & LPT).
- **DEMO MODE bar**: while connected to a DEMO adapter a yellow banner says all values are SIMULATED. The dashboard only polls the connected adapter, so without DEMO it never shows simulated values.

## v0.3.1-test

**Untested on Windows (new build).** Compiled on Linux. The exe is still **unsigned**. Ford products only. Portable, no installer; x64 and ARM64.

**Download `ObdLinkApp-test-win-x64-v0.3.1.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.3.1.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.

- **Official firmware updater support** (OBDLink's *STN Firmware Update Utility*, v2.4.2.0 when this was written). It is **not bundled**; the app downloads it only when you ask.
  - **Get official updater** (firmware banner and Tools → Adapter firmware):
    - Downloads `Install-StnFirmwareUpdater.exe` over https, from scantool.net only (each redirect is checked too), into `Data\firmware\updater\`.
    - Records the SHA-256 and version in `updater.json`.
    - Does not download again when you already have the current copy.
  - **Verification.** OBDLink's installer has **no Authenticode signature**, so the known official SHA-256 is pinned.
    - A signed file must be signed by OBD Solutions/ScanTool.
    - An unknown unsigned file needs your OK.
  - **Run official updater**:
    - Shows a checklist first: auto-disconnect, stable power, MX+ Bluetooth risk.
    - Disconnects the adapter, then starts the installed StnFirmwareUpdater.exe (found via the uninstall registry or Program Files) or the downloaded installer through Windows (UAC prompt).
    - Opens the folder of a firmware package you downloaded earlier.
  - Settings: optional path override for the updater exe. Events log entries under "Updater". New Guide section "Updating adapter firmware".
- Tools menu: firmware items are grouped under **Adapter firmware**.

## v0.3.0-test

**Untested on Windows (new build).** Compiled on Linux. v0.2.0-v0.2.2 were tested on Windows 11 Pro 24H2 (demo mode) by Dale. The exe is still **unsigned**. Ford products only. Portable, no installer; Windows 10 1607+ / Windows 11, x64 and ARM64.

**Download `ObdLinkApp-test-win-x64-v0.3.0.zip`** (normal Intel/AMD PCs: almost everyone). `ObdLinkApp-ARM64-only-v0.3.0.zip`: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download. Running it on an x64 PC gives "This app can't run on your PC". To check: Settings → System → About → System type ("x64-based processor" = x64 zip).

- **Spare tire removed** from the dashboard TPMS widget and the catalog, and its guessed DID (2817) is no longer polled. Saved layouts and older `Data\ford-enhanced-pids.json` copies that still list `tpms_spare` are ignored. Spelling is now "tire" everywhere.
- **Window focus fix.** Some actions made the window drop behind other apps. Causes found and fixed:
  - Message boxes shown without an owner (errors, Yes/No confirmations, the "Connect now?" prompt, info boxes, startup notices).
  - Open/Save file dialogs (`ShowDialog()` without an owner), including log export.
  - The typed-confirmation prompt used the Visual Basic `InputBox`, which has no owner. It is now an owned WPF prompt.
  - Dialogs could be raised from a background thread after an awaited adapter call.
  - All of these now go through one `DialogService`. It always sets the main window as owner, runs on the UI thread and re-activates the main window afterwards. Opening Explorer or the browser (data folder, firmware folder, GitHub, Motorcraft) still moves focus there, which is expected.
  - A unit test fails the build if `MessageBox.Show(`, `ShowDialog()`, `InputBox` or `Process.Start(` appear anywhere else.
- **Identify vehicle** button next to Connect in the header, on the Dashboard while not connected, and in the Vehicle menu (Ctrl+I).
- **Keyboard shortcuts** for most tasks, shown in the menus and tooltips and listed under Help → Keyboard shortcuts (Shift+F1) and in the Guide. Diagnostics capture moved from Ctrl+Shift+D to **Ctrl+Alt+C**; Ctrl+Shift+D now reads DTCs from all modules. Shortcuts that would clash with typing are ignored while a text box or the console has focus. Clear DTCs and Write still ask first and are blocked in read-only mode. The menus have Alt access keys.
- **Help → About** shows the version, git revision and build date, the MIT license, supported Windows, credits ("Built by OBDLink MX+ Windows App (Grok Bot) for Dale Wilcox"; community data from Livnitup, f150forum and jdunk54nl) and the GitHub link.
- **Tools → Check adapter firmware** (Ctrl+Alt+F) runs the firmware check on demand.

| Shortcut | Action |
|---|---|
| F5 | Connect |
| Shift+F5 | Disconnect |
| Ctrl+I | Identify vehicle (VIN, modules, profile) |
| Ctrl+M | Scan modules |
| Ctrl+D | Read OBD-II DTCs |
| Ctrl+Shift+D | Read DTCs from all modules |
| Ctrl+Shift+Delete | Clear DTCs of the selected module (asks first; blocked in read-only mode) |
| Ctrl+R | Read As-Built of the selected module |
| Ctrl+Shift+R | Read all modules' As-Built (backup) |
| Ctrl+B | Backup As-Built |
| Ctrl+W | Write + verify As-Built (asks first; blocked in read-only mode) |
| Ctrl+O | Open factory .ab |
| Ctrl+S | Export factory .ab |
| Ctrl+Alt+C | Save diagnostics capture (zip; was Ctrl+Shift+D) |
| Ctrl+E | Export events log |
| Ctrl+F | Find in logs |
| Ctrl+L | Show / hide log panel |
| Ctrl+` | Raw console |
| Ctrl+, | Settings |
| Alt+1..9 | Go to page 1-9 (Dashboard, Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings) |
| Ctrl+Tab | Next page |
| Ctrl+Shift+Tab | Previous page |
| Ctrl+P | Pause / resume dashboard |
| Ctrl+Shift+P | Record dashboard CSV |
| Ctrl+Alt+F | Check adapter firmware |
| F1 | Guide |
| Shift+F1 | Keyboard shortcuts list |
| F11 | Full screen |
| Ctrl++ | Zoom in |
| Ctrl+- | Zoom out |
| Ctrl+0 | Reset zoom |
| Alt+F4 | Exit |

## v0.2.2-test

**Tested on Windows 11 Pro 24H2 (demo mode)** by Dale. Compiled on Linux. The exe is still **unsigned**. Ford products only.

**Supported Windows:** Windows 10 version 1607 (Anniversary Update) or later, or Windows 11; x64 (win-x64 zip) or ARM64 (win-arm64 zip). Windows 7, 8 and 8.1 are **not supported** (.NET 8 does not run there, so the exe will not start). **Portable, no installer:** unzip the folder and run OBDLinkDiag.exe; settings and data stay in the `Data` folder next to the exe.

- **Automatic adapter firmware check.**
  - On connect, the app reads STI (for example `STN2231 v5.13.0`; EX = STN2230/2231, MX+ = STN2255).
  - A quick reachability check runs first (3 s). If it passes, the app asks OBDLink's public firmware feed (the one scantool.net's own download page uses) for the latest **stable** release.
  - It is silent when offline and never delays or blocks connecting.
  - If your adapter is behind, a non-blocking banner and an Events log entry show the installed and latest versions, with buttons for:
    - **Download firmware**: downloads the official package zip (https://www.scantool.net only) into `Data\firmware\<model>\<version>\`. It checks the download size and zip integrity, and checks that the .bin has an `STNFWv` header. The changelog is kept.
    - **Open folder**.
    - **Official updater page**: [EX](https://www.scantool.net/downloads/updates/obdlink_ex/) / [MX+](https://www.scantool.net/downloads/updates/obdlink_mxp/).
  - **The app never flashes firmware.** Use OBDLink's official updater. Firmware files are never included in the repo or the release zip.
  - You can turn the check off under Settings → "Check adapter firmware on connect" (it is on by default).
  - The simulator reports older firmware (DEMO-EX v5.13.0, DEMO-MX+ v5.10.3), so you can see the banner in demo mode.
- **Startup OS check.** On a Windows version older than 10 1607 the app shows a warning. Most older systems cannot start a .NET 8 app at all; see Supported Windows above.
- **ARM64 build.** A win-arm64 zip is published alongside win-x64. It is untested.

## v0.2.1-test

**Tested on Windows 11 Pro 24H2 (demo mode)** by Dale. Compiled on Linux. Real-vehicle testing is still pending. The exe is still **unsigned**. Ford products only.

- **As-Built workflow.**
  - The As-Built page has its own module picker. Each entry shows the acronym, full name, CAN address, bus, scan result and the date of the newest backup.
  - The list shows only modules that have As-Built (DE..) blocks: factory .ab evidence, an existing backup, or a Ford module that normally has them. Tick "Show all modules" to see every module.
  - Selecting a module on any page (Modules, DTCs, As-Built) selects it everywhere, and the app remembers the last module you used.
  - No more "Select a module first" error. If you are not connected, the app asks whether to connect. If no scan has been run, it scans first. If no module is selected, it picks the last-used module (or the first one that answered) and says so on the page.
  - **Read all blocks** reads every block of the selected module (stopping at the first block the module does not have) and saves it to `Data\asbuilt\<yyyy-MM-dd>\<masked VIN>_<addr>_<time>.asbuilt.json`.
  - **Read all modules (backup)** does the same for every As-Built module that answered the scan, with a progress bar and a Cancel button. If one module fails, the backup carries on with the next one, and a summary at the end lists each module's result.
  - MS-CAN modules switch the adapter to MS-CAN 125k (MX+ / EX) and back automatically.
  - The Modules list has an "As-Built backup" column.
- **DTCs.** The DTCs page uses the same module picker and auto-connect / auto-scan / auto-pick flow. A new **Read all modules** button reads 19 02 from every module that answered, with progress and cancel.
- **Tire pressures (TPMS)**, **UNVERIFIED**.
  - Read from BCM 0x726 Mode 22 DIDs 2813 (LF), 2814 (RF), 2815 (RR) and 2816 (LR), using the community Torque Pro Ford PIDs (kPa = raw/3 + 22/3). DID 2817 (spare) is a guess.
  - All five entries are in `Data\ford-enhanced-pids.json`, so you can edit them.
  - The dashboard shows a top-view truck widget with the four tires and the spare.
  - Pressures are shown in psi or kPa, per Settings. A tire below the Settings threshold (default 30 psi / 207 kPa) turns red and lights the "Low tire" lamp.
  - A tire that never answers is shown as "unsupported".
  - The simulator reports about 36 psi (RR a little low) and has no spare sensor, so you can see the "unsupported" case.
- **Dashboard customization.**
  - **Customize** button: add any item from a catalog of the supported Mode 01 PIDs (after connecting), the Ford Mode 22 DIDs, TPMS, gear, battery (ATRV), MIL and DTC count. Remove items with the ✕ button.
  - Choose a display type per item (dial, bar, numeric tile or lamp) and a Large size.
  - Reorder items by dragging them or with the ▲ ▼ buttons.
  - Reset to the default layout.
  - Save multiple named layouts and switch between them from the Layout box.
  - Layouts are saved to `Data\settings.json`. v0.2.0 gauge lists are migrated.
  - **Only the items on the dashboard are polled.**
  - Items that the vehicle does not support show "unsupported".
- **Settings.** The free-text gauge list is replaced by Customize. There is a new low-tire threshold.

## v0.2.0-test

**UNTESTED ON WINDOWS.** Compiled on Linux. Earlier builds (v0.1.3/v0.1.4) were run and tested by Dale on Windows 11 Pro 24H2 in simulator/demo mode, but this build has not been run on Windows yet. Real-vehicle testing is still pending. The exe is still **unsigned**. Ford products only.

- **New window layout.** The window now has a menu bar (File, Edit, View, Vehicle, Tools, Settings, Help) and a left navigation rail with icons: Dashboard, Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings. The header is slimmer and keeps only the port, connect/disconnect, status and the LINK/TX/RX/ERR LEDs.
- **Shortcuts.** Ctrl+O opens a factory .ab file, F5 connects, Shift+F5 disconnects, Ctrl+Shift+D starts a diagnostics capture, Ctrl+F finds in the logs, Ctrl+L shows or hides the log panel, Ctrl+plus/minus/0 zoom, F11 toggles full screen and F1 opens the Guide.
- **More visual depth.** Buttons and cards have gradients and soft shadows. Buttons have hover and press states (pressed buttons lose their shadow and shift down). Menus and the nav rail follow the theme. The Light and Dark themes define the same keys, and the XAML test checks this.
- **Settings page.** All settings are on one page and saved to `Data\settings.json` as soon as you change them:
  - theme, units (speed, temperature, pressure), log level and retention;
  - adapter default port, kind and timeout;
  - the simulator (DEMO ports are shown only when it is on);
  - VIN redaction default and the update check;
  - dashboard refresh rate, Ford enhanced PIDs, gear estimate and gauge list.
  Read-only mode asks you to confirm before it is turned off.
- **Dashboard (read-only).** The dashboard polls Mode 01 and Mode 22 only, and only while the Dashboard page is open and the adapter is connected.
  - Polling uses priority scheduling: RPM and speed are read most often. The rate adapts and slows down when replies are slow.
  - Circular RPM and speed gauges, plus bar gauges for coolant, intake air, load, throttle, fuel level, battery (ATRV) and any enabled Ford enhanced PIDs.
  - Warning lamps: MIL, DTC count, link, low voltage and overheat.
  - Min, max and average for each gauge, with warning thresholds.
  - Pause, reset stats and CSV recording to `Data\logs\dashboard-*.csv`.
- **Gear.** The current gear comes from a Ford DID when one is enabled in `Data\ford-enhanced-pids.json`. Those DIDs are **UNVERIFIED** and off by default. Otherwise the gear is estimated from RPM, speed, gear ratios and tire size, and labeled "est.".
- Gauges are drawn by the app itself, with no third-party gauge libraries.
- The simulator plays a drive cycle (idle, accelerate, cruise, brake), so the dashboard moves in DEMO mode.
- **Help menu.** It has the Guide, Console help, Check for updates (asks before going online unless you turned on automatic checks), the GitHub page and About.

## v0.1.5-test

**Tested on Windows 11 Pro 24H2 (demo mode); real-vehicle testing pending.** Dale ran and tested v0.1.3/v0.1.4 on Windows 11 Pro 24H2 in simulator/demo mode. The exe is still **unsigned**.

- **Fix: exports and `.abt` files were saved outside the app folder.** In v0.1.4 the Save/Open dialogs had no `InitialDirectory`, so Windows offered its last-used/working folder (the parent of the app folder). Every dialog now starts in a `Data\` subfolder and the full chosen path is logged. Data paths themselves were correct: Dale's v0.1.4 log shows `data folder ...\ObdLinkApp-test-win-x64-v0.1.4\Data (portable)` and `Data migration: Moved 11 file(s)`.
- New `Data\exports\` (log exports), `Data\asbuilt\` (`.abt`, factory `.ab`) and `Data\vehicles\` (vehicle profiles). `crash.txt` next to the exe is now written only if `Data\logs` is not writable.
- Static test: no source file uses `Environment.CurrentDirectory`/`Directory.GetCurrentDirectory`, and every file dialog sets `InitialDirectory`.
- **Console command reference** on the Guide tab (syntax, examples, blocked services, NRC meanings); `help`/`?` prints it in the console, `cls` clears.
- **Activity LEDs** in the header: LINK, TX, RX, ERR (timeout, NO DATA, `?`, 7F NRC). 120 ms hold (errors 600 ms), sampled every 30 ms; light/dark theme colors.
- **Identify vehicle / scan new vehicle** (Vehicle tab, read-only): VIN via `09 02` (fallback `22 F190`), offline VIN decode (WMI, model year, Ford plant, check digit), opt-in online decode via NHTSA vPIC (10 s timeout, offline-safe, logged), HS-CAN + MS-CAN sweep 0x700-0x7E7 with `3E 00`, part-number DIDs F188/F111/F113/F124/F18C and DE00 presence. Saved to `Data\vehicles\<masked VIN>.json` and selectable in the Vehicle drop-down.
- **App icon / logo** from Dale's artwork (assets/icons, 3 variants, multi-size .ico). The exe and window use variant 3 (legible at 16 px on light and dark taskbars). The header logo switches with the theme (black variant 1 on Light, yellow variant 2 on Dark), and you can override it with the small 1/2/3 picker in the header.
- **Ford products only**: Identify vehicle checks the VIN's WMI against Ford Motor Co. brands (Ford, Lincoln, Mercury, incl. Ford trucks and Canada/Mexico/Europe/Australia/Brazil plants) and stops after the VIN read for anything else, with a "Ford products only" message in Events. No profile is saved and no modules are scanned.
- **Motorcraft As-Built**: Ford's site needs a sign-in and captcha, so the button opens it in the browser and copies the VIN; **Import downloaded .ab** copies it into `Data\asbuilt` and links it to the profile.
- Simulator: check-digit-valid demo VIN `1FTDEMX95HF000001`, synthetic part-number DIDs, and an MS-CAN-only demo module (ACM 0x727) for the identify flow.

## v0.1.4-test

- **Portable data folder.** Settings, logs, crash files, captures and As-Built backups now live in `Data\` next to the exe (paths from `AppContext.BaseDirectory`, centralized in `AppPaths`). On first run the old `%LOCALAPPDATA%\OBDLinkDiag` contents are moved in: copy, verify (size + SHA-256), then remove the original; newer files are never overwritten; a `MIGRATED.txt` note is left behind. If the app folder isn't writable the app falls back to `%LOCALAPPDATA%\OBDLinkDiag` with a one-time notice.
- The data folder path is shown in the status bar and on the Guide tab, with an **Open data folder** button.
- Guide tab renders Markdown (headings, lists, bold, tables) instead of raw `##` / `|` text.
- Light theme: selected / hovered grid rows used white text on a light-blue row (nearly invisible); they now use the theme's selection text color.
- Port picker widened (min 300 px) with a tooltip showing the full port name.
- Module grid shows "not scanned" instead of "?" until you run Scan modules.
- Events log: the "Text / error" column no longer repeats the action name.
- Diagnostics capture: protocol probes send `ATSH7DF` before `0100`, so a header left over from a module scan no longer gives a false "NO DATA".
- Simulator uses a valid-format synthetic VIN (`1FTDEMX9SXM000001`) so you can see VIN redaction working; the old demo VIN contained an "I" and was never masked.

## v0.1.3-test

- Fix startup crash "'System.Windows.Style' is not a valid value for property 'BorderBrush'": key `Danger` was both a SolidColorBrush (Light/Dark.xaml) and a Button Style (Controls.xaml); `DangerButtonStyle`'s `BorderBrush="{DynamicResource Danger}"` resolved to the Style. Brush renamed `DangerBrush`; all style keys now end in `Style` (CardStyle, PrimaryStyle, DangerButtonStyle, ChipStyle, GlyphStyle, ScrollThumbStyle, ScrollPageStyle).
- New static XAML checker tests (XamlResourceTests): key type collisions, Light/Dark key parity, every Static/DynamicResource reference resolves to a compatible type (brush/style/template/font), duplicate keys.

## v0.1.2-test

- Fix: v0.1.1 shipped as a loose 486-file folder (non-single-file publish) at the zip root; v0.1.0 was a single self-contained `OBDLinkDiag.exe`. Restored single-file self-contained publish (natives bundled) in the csproj so the exe runs standalone again.
- Startup never fails silently: global handlers (AppDomain, Dispatcher, TaskScheduler) and a guarded startup write `%LOCALAPPDATA%\OBDLinkDiag\logs\crash-*.txt` and `crash.txt` next to the exe, and show an error box with the paths.
- Theme load failure falls back to the built-in Dark palette; unreadable settings.json falls back to defaults; unwritable log folder falls back to %TEMP%.

## v0.1.1-test (pre-release, 2026-10-23)
**Unsigned and not yet tested on real Windows hardware.** Built and unit-tested on Linux only (EnableWindowsTargeting). Windows SmartScreen will warn when you run it.

- **Scope change: Ford-only.**
  - The vehicle list is Ford-only, and As-Built is always available.
- **Logging.**
  - Central event log covering every user action (timestamp, action, parameters, started/succeeded/failed plus error), a separate app message log (Info/Warn/Error with operation, exception, and adapter response), and adapter traffic.
  - The bottom panel has Events / Messages / Adapter traffic tabs, each with level filter, search, Clear, Copy selected, Export .txt/.csv, and auto-scroll.
  - Rolling Serilog files go to `%LOCALAPPDATA%\OBDLinkDiag\logs` (`events-`, `app-`, and `traffic-YYYYMMDD.log`) and are included in the Diagnostics capture zip.
  - VIN redaction (plain and hex-ASCII) is on by default for export and capture. CSV exports are guarded against formula injection.
- **Fixes.**
  - The adapter label now shows "OBDLink MX+ (simulated)" on the demo MX+ port (it previously said EX).
  - NRC codes are decoded with names (e.g. 0x31 requestOutOfRange).
  - The simulator answers DE00–DE07 As-Built reads.
  - Status, battery, and adapter labels come from one source, so they can't contradict each other.
  - The top bar, As-Built row, and banner wrap instead of clipping. Disabled combo boxes are readable.
- **UI redesign.**
  - Light and Dark themes plus "Follow Windows" (System). They switch live from the header, and the choice is saved in settings.json.
  - The header has a connection card with colored state chips and a battery chip.
  - Cards with rounded corners; Segoe Fluent/MDL2 icon buttons. Primary buttons are blue. Danger buttons (Write, Restore, Clear DTCs) are red and still require confirmation.
  - Status bar; monospace font for hex and logs.
  - Every control is themed. Minimum window size is 1024x640; the layout is designed for 1280x720.
- **Module names.**
  - Imported 74 Ford module acronyms (community-db/ford-module-acronyms, from Airborne_Ape's f150forum list).
  - The modules grid, As-Built header, DTC source, and logs show "ACR – Full name" with a tooltip. CAN addresses are unchanged.
- **Community data (read-only reference; no in-app feature browser yet).**
  - `livnitup-forscan-tutorial`: 5 As-Built lines from Livnitup's FORScan Tutorial, with a merge report against f150-asbuilt (3 merged, 2 new, 0 conflicts).
  - `f150-2021plus-asbuilt`: 16,188 rows from Livnitup's 2021-26 F-150 sheet. Flagged **2021+ only; not for 2015-20 trucks**.
  - The existing `f150-asbuilt` was verified to match Livnitup's live 2015-20 sheet.
- **Guide.**
  - docs/GUIDE.md and an in-app Guide tab, adapted from Livnitup's FORScan Tutorial (credited, with a link). Pictures are linked, not copied.
  - docs/ADAPTERS.md lists the adapters the tutorial recommends.
