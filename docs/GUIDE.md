# FORScan / As-Built programming guide (community)

> This app is a **Scan / Diagnose / Repair (SDR)** tool for **personal use only, on your own Ford vehicle** (README, "Intended use"). No security keys or algorithms are included; anything that needs SecurityAccess uses a seed/key plug-in you supply.

Adapted from **Livnitup's "FORScan Tutorial"** (updated 6/14/26): https://docs.google.com/document/d/1-8dKaS_Spu4Zw4hV_CrKC4tLoP9G8yejqegF1wxIqxY/. All credit goes to Livnitup. This is a summary for Ford trucks. Read the original for full detail. Tips are community-reported (UNVERIFIED on Dale's truck).

## Portable app, supported Windows
**Supported Windows:** Windows 10 version 1607 (Anniversary Update) or later, or Windows 11; x64 (win-x64 zip) or ARM64 (win-arm64 zip). Windows 7, 8 and 8.1 are **not supported** (.NET 8 does not run there, so the exe will not start). **Portable, no installer:** unzip the folder and run OBDLinkDiag.exe; settings and data stay in the `Data` folder next to the exe.

## Adapter firmware check
When you connect a **real** adapter, the app reads the adapter firmware (STI) and, if you are online, compares it with the latest **stable** release in OBDLink's public firmware feed. **DEMO / simulated mode skips this check**, including the first time you run the app on a DEMO port, and Ctrl+Alt+F does not contact OBDLink either. If you are offline, nothing is shown. If your firmware is older, a banner shows the installed and latest versions:
- **Download firmware** saves the official scantool.net package to `Data\firmware\<model>\<version>\`. The app checks the size, the zip and the `STNFWv` header, and keeps the changelog.
- **Open folder** opens that folder.
- **Official updater page** opens OBDLink's page ([EX](https://www.scantool.net/downloads/updates/obdlink_ex/), [MX+](https://www.scantool.net/downloads/updates/obdlink_mxp/)).

The app **never flashes firmware**. Use OBDLink's official updater. To turn the check off, untick Settings → "Check adapter firmware on connect".

## Updating adapter firmware
This app **never flashes firmware** and does **not** include OBDLink's updater. It can fetch the official one for you:
- **Get official updater** (firmware banner, or Tools → Adapter firmware) downloads OBDLink's *STN Firmware Update Utility* installer (`Install-StnFirmwareUpdater.exe`) from https://www.scantool.net/downloads/updates/firmware_updater/ into `Data\firmware\updater\`. Only https links on scantool.net are accepted, including redirects.
  - OBDLink publishes this installer **without a digital signature**. The app records its SHA-256 in `updater.json` and accepts it when the hash matches a known official build (v2.4.2.0: `2bc969d3ad639171449d3451e458e00587e591d3da8c9aaee3d2236f32a29fcf`).
  - If OBDLink ever signs it, the signature must be valid and the publisher must be OBD Solutions/ScanTool. Otherwise the file is rejected.
  - If the installer is unsigned and its hash is unknown (for example, a new release), you are asked before it is kept.
  - If a matching copy is already there, it is not downloaded again. The app checks the server's size/date and downloads again only when they change.
- **Run official updater**:
  - First it shows a checklist. The app then **disconnects the adapter** so the COM port/Bluetooth link is free. Keep USB and vehicle power stable. Updating an **MX+ over Bluetooth** is riskier, so stay close to it.
  - If StnFirmwareUpdater is already installed (found via the Windows uninstall list or Program Files), you can start it directly. You can also run the downloaded installer again instead.
  - Windows shows its own administrator (UAC) prompt for the installer. The updater is a normal installed program; this app stays portable.
  - If you downloaded a firmware package earlier, its folder is opened as well.
- Settings → **Firmware updater exe** lets you point to a specific `StnFirmwareUpdater.exe`. Leave it empty to auto-detect.
- Every step is written to the Events log under "Updater".

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
| Dashboard background image | `backgrounds\` (custom picture only; the choice itself is in `settings.json`) |

Every Save/Open dialog starts in the matching folder above (never the folder Windows used last). You can still browse elsewhere; the full path is written to the Events log.

Use **Open data folder** (status bar or the top of this tab) to open it. Back up the `backups` folder before any write. If the app folder is read-only, the app uses `%LOCALAPPDATA%\OBDLinkDiag` instead and tells you once.

## Console command reference

The **Console** tab sends one line at a time to the adapter. Type `help` (or `?`) in the console for this same list; `cls` clears the output.

**Syntax**

- **AT / ST commands** (`ATZ`, `ATI`, `STI`, `STP33`...) go straight to the ELM327/STN adapter. They configure the adapter, not the vehicle.
- **Hex requests** (`0100`, `22F190`, `19 02 FF`) are OBD-II or UDS requests sent to the current header (set with `ATSH7E0` etc.). Spaces are ignored. The reply is shown raw (header + bytes).
- **Built-ins** (`help`, `?`, `cls`) are handled by the app and send nothing.
- **Allow writes** (checkbox): services that change a module are blocked unless it is ticked, and are *always* blocked while **Read-only test mode** is on (the default). `STPX` and `ATWM` are blocked too, because they can put an arbitrary frame on the bus, and `ATCAF0` is blocked so a PCI byte cannot hide a service id (`06 2E ...` once formatting is off). A line that contains a control character is refused so it cannot hide a second command.

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
| `08` | OBD control of on-board system |
| `11` | ECUReset |
| `14` | ClearDiagnosticInformation |
| `27` | SecurityAccess |
| `28` | CommunicationControl |
| `2C` | DynamicallyDefineDataIdentifier |
| `2E` | WriteDataByIdentifier (As-Built write) |
| `2F` | InputOutputControl |
| `31` | RoutineControl |
| `34-37` | Request download/upload/transfer (flashing) |
| `38` | RequestFileTransfer |
| `3B` | WriteDataByLocalIdentifier |
| `3D` | WriteMemoryByAddress |
| `85` | ControlDTCSetting |
| `86` | ResponseOnEvent |
| `87` | LinkControl |
| `STPX` | STN pass-through transmit (arbitrary bus frame) |
| `ATWM` | Wakeup message the adapter transmits |

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
Use **Identify vehicle** (Ctrl+I): the button next to Connect in the header, the button on the dashboard window when not connected, or Vehicle → Identify vehicle.


Connect, open the **Vehicle** tab and click **Identify vehicle**. It is read-only: it only sends `09 02`, `3E 00` and `22 xxxx` (ReadDataByIdentifier).

1. Reads the VIN (`09 02`; falls back to `22 F190` on the PCM 7E0). **Ford products only:** if the VIN's manufacturer code (WMI) is not Ford Motor Co. (Ford, Lincoln, Mercury; US, Canada, Mexico, Europe, Australia, etc.), it stops here, logs "Ford products only" in Events, and saves nothing.
2. Decodes the VIN offline: manufacturer (WMI), country, model year, Ford assembly plant, check digit.
3. Optional (**Online VIN decode** box, off by default): asks NHTSA vPIC (`vpic.nhtsa.dot.gov`, free US-government service) for make/model/trim/engine. Only the VIN is sent; the request is logged; 10 s timeout. If you are offline the offline decode is used.
4. Sweeps HS-CAN (and MS-CAN on an OBDLink MX+/EX) from 0x700 to 0x7E7 with `3E 00`. Every module that answers (even with a negative response) is listed, named from the module table, with its part numbers (F188 software, F111/F113 hardware/assembly, F124 calibration) and whether As-Built block DE00 is readable.
5. Saves `Data\vehicles\<masked VIN>.json` and adds it to the **Vehicle** drop-down, so its module list is used next time.

**Factory As-Built**: Ford's motorcraftservice.com needs a sign-in and a captcha, so the app cannot download it. **Get factory As-Built (Motorcraft)** opens the page in your browser and copies the VIN to the clipboard; download the `.ab` and click **Import downloaded .ab** (it is copied into `Data\asbuilt` and linked to the vehicle profile).

## Dashboard
Open the dashboard window with **View → Dashboard**, the **Dashboard** button in the header (after the title), or **Ctrl+Shift+G**. Doing any of those again brings that same window to the front, including when it was minimized. The gauges are read-only: they send only Mode 01 and Mode 22 requests. The left rail no longer has a Dashboard page; the main window opens on Modules.

**Three dashboard windows (v0.20.0).** **View → Dashboard 2** (**Ctrl+Shift+2**) and **View → Dashboard 3** (**Ctrl+Shift+3**) open two more windows, for example one per monitor. Each window has its own layout (pick it in the **Layout** box), background and position; all three are saved in `Data\settings.json`. Layouts are shared: two windows can show the same one, and an edit shows in both. One poll loop feeds every open window and reads only what the open windows show, so a second window does not double the traffic. **Pause**, **Keep live** and **Record CSV** apply to all windows. In **Customize**: **Rename** the layout, **Export…** it to a `.dashboard.json` file, or **Import…** one as a new layout (it is checked first; a refused write card rejects the file).

**Write cards (v0.20.0).** In Customize, **Add write card** puts a button on the dashboard that sends to the vehicle when pressed: **Routine** (31 01, type RUN), **OutputHold** (2F 03, type HOLD; the output returns to the module on the next press, after 10 s, when the card is removed or when the window closes), or **WriteDid** (2E, type WRITE; the current value is read and shown first, and read back after; **Undo…** then writes that old value back with the same checks, but only while the module still holds what the card wrote). Every press needs writes enabled (Settings), passes the battery check and a confirmation, and runs in the extended session on the module's bus. Refused on every path: airbag deployment routines (E200-E2FF), flashing routines (FF00/FF01, Flash tab only), As-Built blocks (DE00-DEFF, use the As-Built tab) and identification DIDs (F180-F19F). The app does not know what an ID does on your module: use values from a source you trust. DEMO modules accept routine 1234 and output DID D100.
- RPM and speed are read most often. The other gauges are read less often, and the poll rate slows down if the bus is slow.
- Lamps: MIL (check engine), DTCs (stored count), LINK, LOW V (battery under 11.8 V) and HOT (coolant over its warning limit).
- Gear: the tile is titled **Gear (UNVERIFIED)**. Ford DID **1E12** is used when it answers; otherwise the gear is estimated from RPM, speed and 6R80 ratios. Either way the source line stays UNVERIFIED (warning color) until a truck pass matches the cluster through P, R, N, D and the forward gears. Do not mark 1E12 verified in the PID table before that.
- **Pause** stops polling and leaves the loop running (Ctrl+P). Closing the dashboard window also stops polling, unless **Keep live** is on. **Keep live** (the checkbox on the dashboard window, and View → **Keep dashboard live while closed**) keeps polling while the window is closed and is remembered in `Data\settings.json`. Reopening resumes polling that stopped because the window closed. A Pause you turned on stays on until you resume. **Reset stats** clears min/max/avg. **Record CSV** writes `Data\logs\dashboard-*.csv`. Closing the window does not close an open recording; rows resume when polling does.
- **Tire pressures**: a truck top-view shows LF/RF/RR/LR from BCM 0x726 DIDs 2813-2816 (**verified** by Dale on a 2017 F-150; the kPa formula is still the community equation, and you can edit it in `Data\ford-enhanced-pids.json`). Below the low-tire threshold (Settings, default 30 psi) a tire turns red and the "Low tire" lamp lights. "unsupported" means the BCM did not answer.
- **Output shaft speed (OSS)**: add "Output shaft speed (OSS) (UNVERIFIED)" from the Customize catalog (not on the default layout). Shown in rpm from Ford Mode 22 DID **1E13** (still **UNVERIFIED**, raw/4). The PCM 0x7E0 is asked first and the TCM 0x7E1 if the PCM does not answer; the card says which one answered. "unsupported" means neither answered. At a steady speed, compare it with `mph × axle ratio × 336 / tire diameter in inches` before anyone marks 1E13 verified. Read-only. DEMO shows a simulated value.
- **Customize**: on the dashboard window, click Customize, then add items from the catalog, remove them (Remove), and pick dial/bar/tile/lamp per item. Needle gauges (the ones that offer Dial) also have **Analog** or **Digital**. Analog draws the needle and arc. Digital draws the title, the number, and the unit in the same card. Gear, tire pressure, the MIL lamp, and text-only tiles do not have that choice. "Save as" stores a named layout; pick layouts from the Layout box. Only the items on the dashboard are polled. Face is saved with the layout.
- **Rearranging**:
  - Click **Customize** on the dashboard window. Each card gets a toolbar and a colored border. **Done** locks the layout again; cards do not move while Customize is off.
  - **Reorder:** use **Move left**, **Move right**, **Move up**, and **Move down** on the card. Or drag the **Drag** grip (or the gauge itself) and drop it on another card. Dropping on an empty gap moves the card to the nearest slot.
  - **Keyboard:** click a card (or Tab to one of its buttons), then Ctrl+Left/Right (one place), Ctrl+Up/Down (one row), or Ctrl+Home/End (first/last). Focus stays on the card, so you can keep pressing.
  - **Size:** each card has a size box: Small, Medium (20% bigger), Large, or Wide (two columns).
  - **Reset to default** puts back the built-in items, order and sizes for the current layout.
  - Every change is saved at once to Data\settings.json (Dashboard.Layouts) in the app folder. Named layouts ("Save as" and the Layout box) keep their own order and sizes. Older settings files load unchanged.
  - Click **Done** to leave Customize. The dashboard stays read-only; only the items shown are polled.
- **Narrow dashboard window:** the saved order and sizes stay as you left them, including the width of the dashboard window when you last customized. Gauge-height cards (bars, tiles, lamps) line up in one column only when that window is so narrow that a saved card would not fit. Dials and the tire widget keep their size. The main window's width does not change the layout.
- **Background:** the Background box on the dashboard window (and on Settings) offers material photos: **Fine wood grain**, **Brushed aluminum**, **High-gloss black**, **Diamond-plate metal**, **Carbon fiber**, and **Sports-car leather**, plus **None** and **Custom**. Each built-in photo covers the window once (`UniformToFill`, not tiled). A light dark overlay keeps the gauge cards readable in light and dark themes. **None** uses the app theme color. **Pick image** still copies a PNG, JPEG, BMP, or GIF into `Data\backgrounds`. The choice is saved in `Data\settings.json`. A cabin choice saved in v0.5.2 opens as None.
- **Left rail:** when the main window is short the page list scrolls. Shorter still (the window can shrink to a 480 px minimum), the captions hide and the icons remain. Labels come back only once there is room for them with a little extra, so the rail does not flicker. Alt+1 opens Modules. Alt+2 through Alt+8 open DTCs, As-Built, Vehicle, Console, Logs, Guide, and Settings. Alt+9 does nothing.
- Units and refresh rate are on the **Settings** page.

## Flash (module programming, Phase 5)
The **Flash** page reprograms a module from a VBF file. **It has only been run against the simulated module on a DEMO port. Do not flash a real module until the Phase 5 checks in the release notes pass** and you have the module's seed/key plug-in, its programming security level, and a VBF from a known source.
1. Pick the module. The blue line says which adapter is connected: the wired **OBDLink EX** is recommended; the **MX+** works, with a wireless-link warning.
2. **Open file…** (a VBF; a block with a bad checksum refuses the file) or, on a DEMO port, **DEMO image** (it loads a synthetic SBL too). Check a real file first with `obdcli vbf-info --file x.vbf`. Ford software usually comes with a **secondary bootloader** file (its header says `sw_part_type = SBL`): load it with **SBL file…** (opening it with Open file… also loads it as the SBL). It is sent to the module's RAM and started before the erase. **No SBL** removes it.
3. **Run checks**: file vs module, battery window (12.5-15.0 V), link health (8 probes), the module's part number, adapter, PC on AC power. A failed check blocks the flash; a warning does not.
4. **Flash…** (writes must be enabled in Settings): the checks run again, the warnings are shown, then type **FLASH**. Every As-Built block of the module is backed up first.
5. While it runs: keep the charger on, don't turn the key, don't unplug anything. The app refuses to close (and refuses a Windows log-off) while it runs. Lost replies are resent; low voltage pauses the flash. **Stop after this segment** stops at a safe point.
6. If it fails before anything was erased (no key, wrong key, SBL missing), the module is reset back into its own software and the job is closed: the status says *Nothing was erased*. If the module drops out of the programming session mid-flash, the app starts again from the erase once by itself. If the module rejects the security key, a **security hold** appears: no further key is sent until you fix the seed/key provider and press **Clear security hold…** (each wrong key counts toward the module locking itself). After a lockout the hold lifts by itself after 10 minutes.
7. If it stops later (link lost, a module error), the module may be left in its bootloader. Nothing resets it. Reconnect (EX or MX+), open the **same file**, and press **Resume…**. **Abandon job…** closes an open job without finishing it (asks first; the journal and backup are kept).
The journal of every flash is `Data\flash\flash-<module>.jsonl`. Details: `docs/FLASHING.md`.

## As-Built backup
Open **As-Built** and pick a module (only modules with As-Built blocks are listed unless "Show all modules" is ticked). If a module answers DID DE00 with NRC 0x31, it has no As-Built: it leaves the list, Read / Backup / Write stay off, and an info message explains that. That is not an error. **Show all modules** puts it back in the list with the actions still off. Modules that do answer DE00 keep working. A real adapter remembers which Ford bus (HS-CAN, HS-CAN2, or MS-CAN) each module answered on and tries that bus first next time; DEMO does not save that. If you are not connected the app offers to connect, and it scans first if needed. **Read all blocks** saves the module to `Data\asbuilt\<date>\`; **Read all modules (backup)** backs up every As-Built module that answered, with progress and Cancel, and counts "no As-Built" separately from failures. Everything is read-only until you turn read-only mode off. The DTCs page has the same picker and **Read all modules**. DEMO TCM (0x7E1) has synthetic DE00-DE07 blocks so you can try the page; a 2017 F-150 factory file has no TCM As-Built.

## Keyboard shortcuts
Also under Help → Keyboard shortcuts (Shift+F1); menus show each shortcut next to the item. While you type in a text box or the console, Ctrl+I, Ctrl+B, Ctrl+W and Ctrl+Shift+Delete are left to the text box. Clear DTCs and Write still ask first and are blocked in read-only mode. The menus also have Alt access keys (Alt+F = File, Alt+E = Edit, Alt+V = View, Alt+H = Help, and so on).

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
| Ctrl+Shift+G | Open the dashboard window |
| Ctrl+Shift+2 | Open dashboard window 2 |
| Ctrl+Shift+3 | Open dashboard window 3 |
| Alt+1..8 | Go to page 1-8 (Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings) |
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
FORScan's As-Built editing needs a FORScan extended license. Get one from forscan.org; a free trial license is available. This repo does not include any license procedure or workaround.

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
