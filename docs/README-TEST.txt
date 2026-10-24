OBDLink Diag - TEST BUILD (win-x64)  2026-10-23
================================================
UNSIGNED, UNTESTED ON WINDOWS. This build was compiled on Linux and has never been run on a
Windows PC. Expect rough edges.

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
Data\logs.
DATA FOLDER (v0.1.4+)
---------------------
Everything the app writes lives in a "Data" folder next to OBDLinkDiag.exe:
  Data\settings.json, Data\logs\ (events-/app-/traffic-*.log, crash-*.txt),
  Data\captures\ (diagnostics zips), Data\backups\ (As-Built backups).
The path is shown in the status bar and on the Guide tab; "Open data folder" opens it.
On first run, anything in the old %LOCALAPPDATA%\OBDLinkDiag folder is moved into Data\
(each file copied, verified by size + SHA-256, then the original removed; a file is never
overwritten by an older one). A MIGRATED.txt note is left in the old folder.
If the app folder is not writable (e.g. Program Files), the app keeps using
%LOCALAPPDATA%\OBDLinkDiag and tells you once. Put the app folder in Documents or on a USB stick.
