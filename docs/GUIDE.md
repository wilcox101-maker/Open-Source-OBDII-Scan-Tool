# FORScan / As-Built programming guide (community)

Adapted from **Livnitup's "FORScan Tutorial"** (updated 6/14/26): https://docs.google.com/document/d/1-8dKaS_Spu4Zw4hV_CrKC4tLoP9G8yejqegF1wxIqxY/. All credit goes to Livnitup. This is a summary for Ford trucks. Read the original for full detail. Tips are community-reported (UNVERIFIED on Dale's truck).

## Where this app keeps its files

| Item | Location (inside the `Data` folder next to the exe) |
|---|---|
| Settings | `settings.json` |
| Logs and crash reports | `logs\` |
| Diagnostics captures | `captures\` |
| As-Built backups | `backups\` |
| Exported Events / Messages / Traffic logs (.txt/.csv) | `exports\` |
| As-Built files: exported/imported `.abt`, factory `.ab` | `asbuilt\` |
| Vehicle profiles from **Identify vehicle** | `vehicles\<VIN with first 11 masked>.json` |

Every Save/Open dialog starts in the matching folder above (never the folder Windows used last). You can still browse elsewhere; the full path is written to the Events log.

Use **Open data folder** (status bar or the top of this tab) to open it. Back up the `backups` folder before any write. If the app folder is read-only, the app uses `%LOCALAPPDATA%\OBDLinkDiag` instead and tells you once.

## Console command reference

The **Console** tab sends one line at a time to the adapter. Type `help` (or `?`) in the console for this same list; `cls` clears the output.

**Syntax**

- **AT / ST commands** (`ATZ`, `ATI`, `STI`, `STP33`...) go straight to the ELM327/STN adapter. They configure the adapter, not the vehicle.
- **Hex requests** (`0100`, `22F190`, `19 02 FF`) are OBD-II or UDS requests sent to the current header (set with `ATSH7E0` etc.). Spaces are ignored. The reply is shown raw (header + bytes).
- **Built-ins** (`help`, `?`, `cls`) are handled by the app and send nothing.
- **Allow writes** (checkbox): services that change a module are blocked unless it is ticked, and are *always* blocked while **Read-only test mode** is on (the default).

**Examples**

| Command | What it does |
|---|---|
| `help  (or ?)` | Show this list (built-in, nothing is sent) |
| `cls` | Clear the console output (built-in) |
| `ATZ` | Reset the adapter |
| `ATI` | ELM327 version string |
| `STI` | STN firmware id (OBDLink only) |
| `STDI` | OBDLink device/hardware id |
| `ATRV` | Battery voltage at the OBD port |
| `ATSP6` | ELM protocol 6 = ISO 15765-4 CAN 11-bit 500k (HS-CAN) |
| `STP33 / STP53` | STN protocol: 33 = HS-CAN 500k (pins 6/14), 53 = Ford MS-CAN 125k (pins 3/11, MX+ only) |
| `ATSH7E0` | Set the request header (target module). 7DF = OBD broadcast, 7E0 = PCM, 726 = BCM, 720 = IPC |
| `ATDP` | Show the current protocol |
| `0100` | OBD mode 01: supported PIDs (needs ATSH7DF or 7E0) |
| `0902` | OBD mode 09: VIN |
| `22F190` | UDS ReadDataByIdentifier F190 = VIN (send after ATSH to a module) |
| `22F188 / 22F111 / 22F113` | UDS: software part number / hardware part / assembly part |
| `22DE00` | UDS: read As-Built block DE00 (read-only) |
| `1003` | UDS DiagnosticSessionControl: extended session (allowed, changes session only) |
| `3E00` | UDS TesterPresent (keeps a session alive; module presence check) |
| `19 02 FF` | UDS ReadDTCInformation, all DTCs by status mask FF (spaces are ignored) |

**Blocked services** (refused before anything is sent: in read-only mode always, otherwise unless *Allow writes* is ticked)

| SID | Service |
|---|---|
| `04` | OBD clear DTCs |
| `11` | ECUReset |
| `14` | ClearDiagnosticInformation |
| `27` | SecurityAccess |
| `2E` | WriteDataByIdentifier (As-Built write) |
| `2F` | InputOutputControl |
| `31` | RoutineControl |
| `34-37` | Request download/upload/transfer (flashing) |
| `3B` | WriteDataByLocalIdentifier |

**Negative responses**: a reply `7F <SID> <NRC>` means the module refused the request. Common NRCs:

| NRC | Meaning |
|---|---|
| `10` | generalReject |
| `11` | serviceNotSupported |
| `12` | subFunctionNotSupported |
| `13` | incorrect message length/format |
| `22` | conditionsNotCorrect (e.g. engine running, wrong session) |
| `24` | requestSequenceError |
| `31` | requestOutOfRange (DID not supported) |
| `33` | securityAccessDenied |
| `35` | invalidKey |
| `36` | exceededNumberOfAttempts |
| `37` | requiredTimeDelayNotExpired |
| `72` | generalProgrammingFailure |
| `78` | responsePending (module is busy; app waits) |
| `7E` | subFunctionNotSupportedInActiveSession |
| `7F` | serviceNotSupportedInActiveSession (try 1003 first) |

`NO DATA` means no module answered at that header on the current bus (wrong bus, wrong address, or ignition off). `?` means the adapter did not understand the command. The **ERR** LED in the header flashes for both and for any NRC.

## Identify / scan a new vehicle

Connect, open the **Vehicle** tab and click **Identify vehicle**. It is read-only: it only sends `09 02`, `3E 00` and `22 xxxx` (ReadDataByIdentifier).

1. Reads the VIN (`09 02`; falls back to `22 F190` on the PCM 7E0). **Ford products only:** if the VIN's manufacturer code (WMI) is not Ford Motor Co. (Ford, Lincoln, Mercury; US, Canada, Mexico, Europe, Australia, etc.), it stops here, logs "Ford products only" in Events, and saves nothing.
2. Decodes the VIN offline: manufacturer (WMI), country, model year, Ford assembly plant, check digit.
3. Optional (**Online VIN decode** box, off by default): asks NHTSA vPIC (`vpic.nhtsa.dot.gov`, free US-government service) for make/model/trim/engine. Only the VIN is sent; the request is logged; 10 s timeout. If you are offline the offline decode is used.
4. Sweeps HS-CAN (and MS-CAN on an OBDLink MX+/EX) from 0x700 to 0x7E7 with `3E 00`. Every module that answers (even with a negative response) is listed, named from the module table, with its part numbers (F188 software, F111/F113 hardware/assembly, F124 calibration) and whether As-Built block DE00 is readable.
5. Saves `Data\vehicles\<masked VIN>.json` and adds it to the **Vehicle** drop-down, so its module list is used next time.

**Factory As-Built**: Ford's motorcraftservice.com needs a sign-in and a captcha, so the app cannot download it. **Get factory As-Built (Motorcraft)** opens the page in your browser and copies the VIN to the clipboard; download the `.ab` and click **Import downloaded .ab** (it is copied into `Data\asbuilt` and linked to the vehicle profile).

## Dashboard
Open **Dashboard** on the left rail while you are connected. It is read-only and sends only Mode 01 and Mode 22 requests.
- RPM and speed are read most often. The other gauges are read less often, and the poll rate slows down if the bus is slow.
- Lamps: MIL (check engine), DTCs (stored count), LINK, LOW V (battery under 11.8 V) and HOT (coolant over its warning limit).
- Gear: a Ford DID is used if you enable one in `Data\ford-enhanced-pids.json` (the entries are **unverified**). Otherwise the gear is estimated from RPM, speed and ratios, and shown as "est.".
- **Pause** stops polling. **Reset stats** clears min/max/avg. **Record CSV** writes `Data\logs\dashboard-*.csv`.
- Units, refresh rate and the gauge list are on the **Settings** page.

## Keyboard shortcuts
Ctrl+O open factory .ab, F5 connect, Shift+F5 disconnect, Ctrl+Shift+D capture, Ctrl+F find in logs, Ctrl+L log panel, Ctrl +/-/0 zoom, F11 full screen, F1 this guide.

## Header LEDs

**LINK** (blue) lit while connected; **TX** (green) flashes when a command is sent; **RX** (amber) when a reply arrives; **ERR** (red) on timeout, `NO DATA`, `?`, adapter errors or a negative response. Each flash lasts at least 120 ms (errors 600 ms) so single commands are visible. They show one flash per command/reply, not per byte.

## Before you change anything
1. Battery: key ON, engine OFF. Keep extra current draw to a minimum (lights, HVAC blower, doors closed). Use a battery maintainer for long sessions.
2. **Back up every module's As-Built first**, and save the file off the laptop. This app saves backups automatically before any write.
3. **IPMB** values can't be backed up by FORScan. Write them down by hand before changing anything.
4. Change one module at a time and write down the original value.

## Making a change
- Patterns use `x` for "keep what you have". Change only the non-x nibble(s).
- FORScan may say "incorrect checksum" after you edit a line. Livnitup says to ignore it; the checksum is recalculated on write.
- After a BCM write, cycle the key (OFF, wait, ON).
- After an IPC change, TPMS readings can go blank for a short time. They come back after driving.
- SYNC/APIM reset: hold **Power + Seek ▶︎ for about 10 s**.
- With a switch-type USB adapter (OHP/VINT), set HS/MS the way FORScan prompts. The OBDLink EX and MX+ switch automatically. The OBDLink LX has no MS-CAN.

## Worked examples in the tutorial (2015-2020; also in community-db/livnitup-forscan-tutorial)
| Mod | Module | Address | Value |
|---|---|---|---|
| Disable double horn honk | BCM | 726-41-02 | xxx0 xxxx xxxx |
| Autofold mirrors | DDM | 740-03-01 / 740-12-01 | x8xx xx / Fxxx |
| Autofold mirrors | PDM | 741-03-01 / 741-12-01 | x8xx xx / Fxxx |

## Pictures (reference only; images stay on the author's Drive and are not in this repo)
- Pic 1: https://drive.google.com/file/d/0B3jKTCd57R6ycmxGNXFGZUVLb1E/view
- Pic 2: https://drive.google.com/file/d/0B3jKTCd57R6yRTJmYl9qZnlJVlE/view
- Pic 1 (second section): https://drive.google.com/file/d/0B3jKTCd57R6yclU0ajVubTYzaGs/view
- Pic 2 (second section): https://drive.google.com/file/d/0B3jKTCd57R6yVzVFbVJjeC1SZW8/view
- Pic: https://drive.google.com/file/d/0B3jKTCd57R6ycml3X2JtemtkaTA/view

## Licensing
FORScan's As-Built editing needs a FORScan extended licence. Get one from forscan.org; a free trial licence is available. This repo does not include any licence procedure or workaround.

## Helpful links (from the tutorial)
- FORScan download: https://forscan.org/download.html
- 2015-2020 F-150 sheet: https://docs.google.com/spreadsheets/d/1uDSQ1Z5a2Wt8-kjrSiVSlDFGFHnfeuhb3RTMVz95730/ (imported as `community-db/f150-asbuilt`)
- 2021+ F-150 sheet: https://docs.google.com/spreadsheets/d/1-_e09lLPPeSHoO3SsNwyqu5v3dgpbluvJcezUItTSVI/ (imported as `community-db/f150-2021plus-asbuilt`; **not for 2015-20**)
- Factory As-Built by VIN: https://www.motorcraftservice.com/AsBuilt
- As-Built compare: http://compulsivecode.com/Project_AsBuiltCompare.aspx · Firn's decoder: https://asbuilt.altctrlwrench.com/
- Backup video: https://youtu.be/XhFUlFgQPVE
- Module acronyms (Airborne_Ape, f150forum): imported as `community-db/ford-module-acronyms`
- Ford towing calculator: https://www.ford.com/support/towing-calculator · Magnuson-Moss Warranty Act: https://www.consumer.ftc.gov/articles/0138-auto-warranties-routine-maintenance
- The tutorial also links many forum write-ups (BLIS, ACC, LED headlights, ambient lighting, and others) and sheets for other Ford models. Those were not imported.
