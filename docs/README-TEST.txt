OBDLink Diag - TEST BUILD v0.2.1 (win-x64)  2026-10-24
=======================================================
UNSIGNED TEST BUILD. Compiled on Linux. UNTESTED ON WINDOWS: v0.2.0 is confirmed running on
Windows 11 Pro 24H2 (Dale, demo mode), but v0.2.1 (As-Built workflow, TPMS, dashboard
customization) has not been run on Windows yet. Real-vehicle testing is still pending.
Ford products only.

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

NEW IN v0.2.1
- As-Built page: module picker (acronym, name, address, bus, last backup), filtered to modules
  with As-Built blocks ("Show all modules" to see all). No more "select a module first": the app
  offers to connect, scans if needed and picks the last-used module. "Read all blocks" and
  "Read all modules (backup)" save to Data\asbuilt\<date>\ with progress and Cancel. READ-ONLY.
- DTCs page: same picker, plus "Read all modules".
- Dashboard: tyre pressures (BCM DIDs 2813-2816, UNVERIFIED) with a truck top-view and a low-tyre
  lamp (threshold on Settings). Tyres that do not answer show "unsupported".
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
