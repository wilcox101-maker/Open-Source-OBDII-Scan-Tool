# OBDLink MX+ Diagnostics (open source, MIT)

A Windows diagnostic app for the **OBDLink MX+** (STN-based) adapter. The app is **Ford-only**; the priority vehicle is the **2017 Ford F-150** (HS-CAN + MS-CAN, As-Built) and the **2008 Dodge Ram 1500**. See `PLAN.md` for the design.

> ⚠ Writing module configuration can make a vehicle inoperable. Back up first, keep a battery maintainer connected, and use this at your own risk.

## Layout
```
ObdLink.slnx
src/ObdLink.Core      transport, ELM/STN, ISO-TP, UDS, OBD-II, modules, As-Built, safety, logging, DTC stub
src/ObdLink.App       WPF dark-theme UI (net8.0-windows): connect panel, modules, DTCs, As-Built raw grid, console, log
tests/ObdLink.Core.Tests  xUnit tests against SimulatedAdapter (no hardware needed)
community-db/         JSON schema + placeholder example entries
```

## Build
```
dotnet build ObdLink.slnx         # Windows: everything. Linux: Core + tests + App (EnableWindowsTargeting) compile
dotnet test tests/ObdLink.Core.Tests
```
On Windows, choose **DEMO (simulated)** in the Port box to try the UI without an adapter.

## Supported Windows / portable

**Supported Windows:** Windows 10 version 1607 (Anniversary Update) or later, or Windows 11; **x64: download `ObdLinkApp-test-win-x64-v<version>.zip` (almost all PCs).** `ObdLinkApp-ARM64-only-v<version>.zip` is for Snapdragon/ARM Windows PCs only; most PCs need the x64 download (check Settings → System → About → System type). Windows 7, 8 and 8.1 are **not supported** (.NET 8 does not run there, so the exe will not start). **Portable, no installer:** unzip the folder and run OBDLinkDiag.exe; settings and data stay in the `Data` folder next to the exe. There is no installer and none is planned.

## Tested platforms
| Platform | Build | Mode | Result |
|---|---|---|---|
| Windows 11 Pro 24H2 (x64) | v0.1.3-test, v0.1.4-test | Simulator / demo (DEMO-MX+, DEMO-EX) | Run and tested by Dale |
| Windows 11 Pro 24H2 (x64) | v0.2.0-test, v0.2.1-test, v0.2.2-test | Simulator / demo | Run and tested by Dale |
| Windows | v0.3.0-test | - | **Untested** (new build) |
| Real vehicle (2017 F-150, OBDLink MX+/EX) | - | - | **Pending** |

Release builds are **unsigned** (SmartScreen "More info -> Run anyway"). v0.2.0-v0.2.2 were tested on Windows 11 Pro 24H2 (demo mode) by Dale; v0.3.0 has not been run on Windows yet.

## Verified facts and sources
| Item | Value | Source |
|---|---|---|
| STN HS-CAN ISO 15765 11-bit 500k | `STP 33` (29-bit: 34) | OBDLink Family Reference & Programming Manual (FRPM), "OBD Protocol ST Commands"; OBDLink MX Protocol Commands sheet (scantool.net/downloads/118) |
| STN MS-CAN ISO 15765 11-bit 125k, pins 3/11 | `STP 53` (29-bit: 54) | same |
| J1850 PWM / VPW | `STP 11` / `STP 12` | same |
| As-Built line checksum | low byte of the sum of (module hi, module lo, block, line, data bytes) | FORScan forum t=2012; f150forum FORScan thread p.13 worked examples (used as test vectors) |
| DTC 2-byte encoding | SAE J2012 / J1979 | public J1979 documentation |

**Brief discrepancy:** the task brief said "STP 53 (HS-CAN) / STP 54 (MS-CAN)". That's wrong according to the FRPM: 53 and 54 are both MS-CAN (11-bit and 29-bit). HS-CAN is 33. The presets can be changed in `BusProfile`.

## Unverified / stubbed (see code comments marked [UNVERIFIED]/[STUB]/[TODO])
- As-Built block → UDS DID mapping `0xDE00 + (block-1)` and 5 data bytes per line come from community convention. Confirm both with a capture before writing.
- F-150 2017 module addresses and bus assignments come from community reports (each row has a `Confidence`). The scanner probes both buses.
- Multi-frame transmit uses host-side ISO-TP with `ATCAF0`. It hasn't been timed on real MX+ hardware over Bluetooth.
- UDS 0x78 (response pending): the client re-requests only for idempotent services. A write that gets 0x78 is reported as an error, and read-back verify catches the outcome.
- 0x27 SecurityAccess is only an interface (`ISeedKeyProvider`). No proprietary algorithms are included.
- The DTC DB is a small stub of SAE generic titles.
- The battery threshold of 12.5 V is a project default, not an OEM specification.

## v0.1.1 additions
- **Ford-only.** Chrysler/Ram support was removed.
- **Logging.** The bottom panel has Events, Messages, and Adapter traffic tabs, each with filter, search, copy, and export (.txt/.csv; VINs redacted by default). Rolling files go to `Data\logs` next to the exe (portable; falls back to `%LOCALAPPDATA%\OBDLinkDiag` if the app folder is read-only) (`events-`, `app-`, and `traffic-YYYYMMDD.log`) and are included in the Diagnostics capture zip.
- **Module names.** Full names come from `community-db/ford-module-acronyms` (74 entries) and appear in the grid, the As-Built view, DTCs, and logs. Names only; CAN addresses are not changed.
- **Community data.**
  - `f150-asbuilt` (2015-20, Livnitup sheet).
  - `f150-2021plus-asbuilt` (2021+, **not for 2017**).
  - `livnitup-forscan-tutorial`.
  - Read-only reference; there is no in-app feature browser yet.
- **Guide.** docs/GUIDE.md and the in-app Guide tab (credit: Livnitup).
- **Themes.** Light, Dark, or Follow Windows. They switch live and the choice is saved in settings.
