OBDLink Diag - TEST BUILD v0.3.4 (win-x64 / win-arm64)  2026-10-24
=======================================================
UNSIGNED TEST BUILD. Compiled on Linux. v0.2.0-v0.2.2 were tested on Windows 11 Pro 24H2
(Dale, demo mode); this v0.3.4 build has not been run on Windows yet. Real-vehicle testing is still pending.
Ford products only.

WHICH ZIP? Almost every PC needs the x64 download: ObdLinkApp-test-win-x64-v0.3.4.zip
  ObdLinkApp-ARM64-only-v0.3.4.zip: For Snapdragon/ARM Windows PCs only; most PCs need the x64 download.
  (Running the ARM64 build on a normal Intel/AMD PC gives "This app can't run on your PC".)
  How to check: Settings > System > About > "System type".
    "64-bit operating system, x64-based processor"  -> x64 zip
    "64-bit operating system, ARM-based processor"  -> ARM64-only zip

SUPPORTED WINDOWS: Windows 10 version 1607 or later, or Windows 11. Windows 7, 8 and 8.1 are NOT
supported: the exe will not start there. PORTABLE, NO INSTALLER: unzip and run; everything stays in
the Data folder.

NEW IN v0.3.4: OUTPUT SHAFT SPEED + CODE AUDIT
- Customize > add "Output shaft speed (OSS)": rpm from an UNVERIFIED Ford Mode 22 DID (1E13), asked of
  PCM 0x7E0 then TCM 0x7E1. Read-only; DEMO shows a simulated value. Not on the default layout.
- Code audit fixes (see docs/AUDIT.md): safer link opening, no double dashboard polling, COM port
  released after a failed open, VIN decode connection released, American spelling throughout.

IN v0.3.3: REARRANGE THE DASHBOARD (Customize)
- Click Customize on the Dashboard. Each card gets a toolbar.
- Reorder: drag a card (grab it by the ⠿ handle or any empty spot) and drop it on another card. Or use ◀ ▶ (one place) and ▲ ▼ (one row).
- Keyboard: Tab to any button on a card's toolbar, then press Ctrl+Left/Right (one place), Ctrl+Up/Down (one row) or Ctrl+Home/End (first/last). Focus stays on the card, so you can keep pressing.
- Size: each card has a size box: Small, Medium (20% bigger), Large, or Wide (two columns).
- Reset to default puts back the built-in items, order and sizes for the current layout.
- Every change is saved at once to Data\settings.json (Dashboard.Layouts) in the app folder. Named layouts ("Save as" and the Layout box) keep their own order and sizes. Older settings files load unchanged.
- Click Done to leave Customize. The dashboard stays read-only; only the items shown are polled.

NEW IN v0.3.2 (fix: stuck on "SIM DEMO-MX+", real OBDLink EX never listed)
- The port list now shows every COM port Windows knows about (registry + Device Manager/WMI), real
  ports first. FTDI ports (VID 0403, the OBDLink EX) are labeled "OBDLink EX / FTDI".
  Example: "USB  COM4  OBDLink EX / FTDI  (USB Serial Port (COM4))".
- A real port is always preselected over DEMO. DEMO is never chosen automatically or as a fallback:
  pick it yourself in the list. Refresh rescans and switches to a newly plugged-in adapter.
- Rescans automatically at startup and when a USB device is plugged in/removed; remembers the last
  real port that connected.
- Read-only test mode is ON at every start; "writes enabled" lasts for the current session only.
- Type a COM port by hand (box next to Refresh, e.g. COM4) and click Use, if it is not listed.
- If no COM ports are found, a yellow bar explains: plug in the EX, install the FTDI VCP driver,
  check Device Manager > Ports (COM & LPT).
- While connected to DEMO a yellow "DEMO MODE ... SIMULATED" bar is shown; without DEMO the
  dashboard only shows values read from the real adapter.

NEW IN v0.3.1
- Firmware banner / Tools > Adapter firmware: "Get official updater" downloads OBDLink's official
  STN Firmware Update Utility (scantool.net, https only) into Data\firmware\updater\ and checks its
  SHA-256 (OBDLink ships it unsigned; the known official hash is pinned). Not bundled.
- "Run official updater": checklist, auto-disconnects the adapter, then starts the installed updater
  or the installer (Windows UAC prompt). Optional exe path in Settings. Guide: "Updating adapter firmware".

NEW IN v0.3.0
- Spare tire removed from the dashboard TPMS widget (its guessed DID is no longer polled).
- Focus fix: dialogs no longer make the window drop behind other apps.
- "Identify vehicle" button next to Connect, on the Dashboard while not connected, and Vehicle menu (Ctrl+I).
- Keyboard shortcuts (list below, and Help > Keyboard shortcuts / Shift+F1).
- Help > About shows version, git revision, build date, license and credits.

HOW TO RUN
1. Unzip the WHOLE folder somewhere, e.g. C:\OBDLinkDiag. Keep the *_cor3.dll files next to the exe.
2. Double-click OBDLinkDiag.exe. No install and no .NET needed (self-contained, 64-bit Windows 10/11).
3. SmartScreen: because the exe is unsigned, Windows may show "Windows protected your PC".
   Click "More info" -> "Run anyway". If the file is blocked, right-click the exe -> Properties ->
   tick "Unblock" -> OK.
4. Try it first with no truck: pick "DEMO-MX+" or "DEMO-EX" as the port and click Connect.
5. Real adapter: pair the OBDLink MX+ (Bluetooth) or plug in the EX (USB), click Refresh, pick its
   COM port, choose the vehicle (Ford F-150; this build is Ford-only), Connect, ignition ON.

READ-ONLY BY DEFAULT
"Read-only test mode" is ON when the app starts. While it is on:
 - As-Built Write/Restore and Clear DTCs are refused before anything is sent to the truck.
 - The Console blocks write/clear/reset/security services (2E 2F 31 34-37 3B 11 14 27 04),
   even if "Allow writes" is ticked.
Reading (scan, DTC read, VIN, As-Built read/backup) works normally. Turning the mode off requires
unticking it (left panel) and confirming a warning. The setting is saved in
Data\settings.json (next to the exe).

HOW TO CAPTURE LOGS (please do this on each truck)
1. Connect to the truck (ignition ON, engine off is fine).
2. Click "Diagnostics capture (save zip)". It runs these, all read-only:
   - adapter ID: ATI / STI / STDI / ATRV
   - protocol probes: ATSP6 + 0100, ATSP2 + 0100, STP 53 test
   - a module scan
   It takes about 1-2 minutes.
   Optional: under "Discovery sweep" tick "Include sweep in Diagnostics capture".
   The default ID range 0x700-0x7EF takes several minutes, and the range is adjustable.
3. A zip is saved to Data\captures\obdlink-capture-<date>.zip and Explorer
   opens on it. Send that zip back, along with Data\logs\ if something crashed.
   Logs (events/app/traffic) are included in the zip. VINs are redacted when "Redact VINs
   in capture" is ticked (on by default). Raw adapter replies may still contain the serial.

LOGS
The bottom panel has three tabs: Events (everything you clicked, with outcome), Messages (Info/Warn/Error),
and Adapter traffic. Each tab has Export (.txt/.csv), and VINs are redacted by default. Files are in
Data\exports (the Save dialog starts there).

DATA FOLDER (v0.1.4+)
---------------------
Everything the app writes lives in a "Data" folder next to OBDLinkDiag.exe:
  Data\settings.json, Data\logs\ (events-/app-/traffic-*.log, crash-*.txt),
  Data\captures\ (diagnostics zips), Data\backups\ (As-Built backups),
  Data\exports\ (exported Events/Messages/Traffic .txt/.csv - v0.1.5),
  Data\asbuilt\ (.abt exports/imports, factory .ab files - v0.1.5),
  Data\vehicles\ (vehicle profiles from Identify vehicle - v0.1.5).
v0.1.5 fix: in v0.1.4 the Save/Open dialogs had no start folder, so Windows offered the folder
it used last (e.g. the folder ABOVE the app) and exports / 7E0.abt landed there. Every dialog
now starts in the matching Data subfolder, and the full saved path is shown in the Events log.
The path is shown in the status bar and on the Guide tab; "Open data folder" opens it.
On first run, anything in the old %LOCALAPPDATA%\OBDLinkDiag folder is moved into Data\
(each file copied, verified by size + SHA-256, then the original removed; a file is never
overwritten by an older one). A MIGRATED.txt note is left in the old folder.
If the app folder is not writable (e.g. Program Files), the app keeps using
%LOCALAPPDATA%\OBDLinkDiag and tells you once. Put the app folder in Documents or on a USB stick.

NEW IN v0.2.2
- Automatic adapter firmware check on connect (reads STI; silent when offline). If a newer stable
  OBDLink firmware exists, a banner shows installed vs latest with "Download firmware" (official
  scantool.net zip -> Data\firmware\<model>\<version>\, verified, changelog kept), "Open folder"
  and "Official updater page". This app NEVER flashes firmware. Off switch: Settings.
  DEMO ports report old firmware so you can see the banner.

NEW IN v0.2.1
- As-Built page: module picker (acronym, name, address, bus, last backup), filtered to modules
  with As-Built blocks ("Show all modules" to see all). No more "select a module first": the app
  offers to connect, scans if needed and picks the last-used module. "Read all blocks" and
  "Read all modules (backup)" save to Data\asbuilt\<date>\ with progress and Cancel. READ-ONLY.
- DTCs page: same picker, plus "Read all modules".
- Dashboard: tire pressures (BCM DIDs 2813-2816, UNVERIFIED) with a truck top-view and a low-tire
  lamp (threshold on Settings). Tires that do not answer show "unsupported".
- Dashboard -> Customize: add/remove items, dial/bar/tile/lamp, Large, drag or up/down to
  reorder, Reset, save named layouts. Only items shown are polled.

NEW IN v0.2.0
-------------
- Menu bar (File/Edit/View/Vehicle/Tools/Settings/Help) and a left navigation rail with icons.
- Shortcuts: Ctrl+O open factory .ab, F5 connect, Shift+F5 disconnect, Ctrl+Shift+D capture,
  Ctrl+F find in logs, Ctrl+L log panel, Ctrl +/-/0 zoom, F11 full screen, F1 Guide.
- Settings page (left rail -> Settings, or the Settings menu). Saved to Data\settings.json.
  Turning Read-only mode OFF asks for confirmation. DEMO ports only appear if "Simulator" is on.
- Dashboard (left rail -> Dashboard, read-only Mode 01/22): RPM and speed dials, bar gauges,
  gear (Ford DID if enabled, otherwise "est."), lamps MIL / DTCs / LINK / LOW V / HOT,
  min/max/avg, Pause, Reset stats, Record CSV (Data\logs\dashboard-*.csv).
  It polls only while the Dashboard page is open and connected. Try it with DEMO first.
- Ford enhanced PIDs: Data\ford-enhanced-pids.json. These are UNVERIFIED and off by default.
- Please check: menus, shortcuts, light/dark themes, the dashboard in DEMO, Settings persistence.


NEW IN v0.1.5
-------------
- Console: type "help" for the command list (also on the Guide tab), "cls" to clear.
- Header LEDs: LINK (connected), TX (sent), RX (reply), ERR (timeout / NO DATA / ? / 7F NRC).
- Vehicle tab -> "Identify vehicle / scan new vehicle" (read-only): VIN, offline decode,
  optional online decode (NHTSA vPIC, tick the box; only the VIN is sent), HS-CAN + MS-CAN
  module sweep with part numbers. Saved to Data\vehicles and added to the Vehicle drop-down.
- "Get factory As-Built (Motorcraft)" opens motorcraftservice.com/AsBuilt in your browser and
  copies the VIN (Ford needs a sign-in + captcha). Then "Import downloaded .ab".
Try all of it on DEMO-MX+ first.

KEYBOARD SHORTCUTS (also Help > Keyboard shortcuts, Shift+F1):
  F5                   Connect
  Shift+F5             Disconnect
  Ctrl+I               Identify vehicle (VIN, modules, profile)
  Ctrl+M               Scan modules
  Ctrl+D               Read OBD-II DTCs
  Ctrl+Shift+D         Read DTCs from all modules
  Ctrl+Shift+Delete    Clear DTCs of the selected module (asks first; blocked in read-only mode)
  Ctrl+R               Read As-Built of the selected module
  Ctrl+Shift+R         Read all modules' As-Built (backup)
  Ctrl+B               Backup As-Built
  Ctrl+W               Write + verify As-Built (asks first; blocked in read-only mode)
  Ctrl+O               Open factory .ab
  Ctrl+S               Export factory .ab
  Ctrl+Alt+C           Save diagnostics capture (zip; was Ctrl+Shift+D)
  Ctrl+E               Export events log
  Ctrl+F               Find in logs
  Ctrl+L               Show / hide log panel
  Ctrl+`               Raw console
  Ctrl+,               Settings
  Alt+1..9             Go to page 1-9 (Dashboard, Modules, DTCs, As-Built, Vehicle, Console, Logs, Guide, Settings)
  Ctrl+Tab             Next page
  Ctrl+Shift+Tab       Previous page
  Ctrl+P               Pause / resume dashboard
  Ctrl+Shift+P         Record dashboard CSV
  Ctrl+Alt+F           Check adapter firmware
  F1                   Guide
  Shift+F1             Keyboard shortcuts list
  F11                  Full screen
  Ctrl++               Zoom in
  Ctrl+-               Zoom out
  Ctrl+0               Reset zoom
  Alt+F4               Exit

v0.3.2 ALSO FIXES
- A DEMO vehicle profile ([DEMO], VIN ...000001) is never used on the real adapter. If you see one, pick
  "2017 Ford F-150" or click Identify vehicle again while connected to COM4 (you may delete the old file in Data\vehicles).
- Module box shows names, not "ObdLink.App.ModuleRow".
- IPC/ACM DTC reads try HS-CAN, then HS-CAN2 (pins 3/11, 500k), then MS-CAN (125k). Run Scan modules first.
