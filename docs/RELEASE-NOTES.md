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
  - Removed Chrysler/Dodge/Ram: the vehicle option, tables, simulator data, docs/CHRYSLER-NETWORKS.md, and the example Ram entry.
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
