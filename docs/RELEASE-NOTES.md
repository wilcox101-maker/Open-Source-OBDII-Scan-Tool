# Release notes

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
- **Gear.** The current gear comes from a Ford DID when one is enabled in `Data\ford-enhanced-pids.json`. Those DIDs are **UNVERIFIED** and off by default. Otherwise the gear is estimated from RPM, speed, gear ratios and tyre size, and labelled "est.".
- Gauges are drawn by the app itself, with no third-party gauge libraries.
- The simulator plays a drive cycle (idle, accelerate, cruise, brake), so the dashboard moves in DEMO mode.
- **Help menu.** It has the Guide, Console help, Check for updates (asks before going online unless you turned on automatic checks), the GitHub page and About.

## v0.1.5-test

**Tested on Windows 11 Pro 24H2 (demo mode); real-vehicle testing pending.** Dale ran and tested v0.1.3/v0.1.4 on Windows 11 Pro 24H2 in simulator/demo mode. The exe is still **unsigned**.

- **Fix: exports and `.abt` files were saved outside the app folder.** In v0.1.4 the Save/Open dialogs had no `InitialDirectory`, so Windows offered its last-used/working folder (the parent of the app folder). Every dialog now starts in a `Data\` subfolder and the full chosen path is logged. Data paths themselves were correct: Dale's v0.1.4 log shows `data folder ...\ObdLinkApp-test-win-x64-v0.1.4\Data (portable)` and `Data migration: Moved 11 file(s)`.
- New `Data\exports\` (log exports), `Data\asbuilt\` (`.abt`, factory `.ab`) and `Data\vehicles\` (vehicle profiles). `crash.txt` next to the exe is now written only if `Data\logs` is not writable.
- Static test: no source file uses `Environment.CurrentDirectory`/`Directory.GetCurrentDirectory`, and every file dialog sets `InitialDirectory`.
- **Console command reference** on the Guide tab (syntax, examples, blocked services, NRC meanings); `help`/`?` prints it in the console, `cls` clears.
- **Activity LEDs** in the header: LINK, TX, RX, ERR (timeout, NO DATA, `?`, 7F NRC). 120 ms hold (errors 600 ms), sampled every 30 ms; light/dark theme colours.
- **Identify vehicle / scan new vehicle** (Vehicle tab, read-only): VIN via `09 02` (fallback `22 F190`), offline VIN decode (WMI, model year, Ford plant, check digit), opt-in online decode via NHTSA vPIC (10 s timeout, offline-safe, logged), HS-CAN + MS-CAN sweep 0x700-0x7E7 with `3E 00`, part-number DIDs F188/F111/F113/F124/F18C and DE00 presence. Saved to `Data\vehicles\<masked VIN>.json` and selectable in the Vehicle drop-down.
- **App icon / logo** from Dale's artwork (assets/icons, 3 variants, multi-size .ico). The exe and window use variant 3 (legible at 16 px on light and dark taskbars). The header logo switches with the theme (black variant 1 on Light, yellow variant 2 on Dark), and you can override it with the small 1/2/3 picker in the header.
- **Ford products only**: Identify vehicle checks the VIN's WMI against Ford Motor Co. brands (Ford, Lincoln, Mercury, incl. Ford trucks and Canada/Mexico/Europe/Australia/Brazil plants) and stops after the VIN read for anything else, with a "Ford products only" message in Events. No profile is saved and no modules are scanned.
- **Motorcraft As-Built**: Ford's site needs a sign-in and captcha, so the button opens it in the browser and copies the VIN; **Import downloaded .ab** copies it into `Data\asbuilt` and links it to the profile.
- Simulator: check-digit-valid demo VIN `1FTDEMX95HF000001`, synthetic part-number DIDs, and an MS-CAN-only demo module (ACM 0x727) for the identify flow.

## v0.1.4-test

- **Portable data folder.** Settings, logs, crash files, captures and As-Built backups now live in `Data\` next to the exe (paths from `AppContext.BaseDirectory`, centralized in `AppPaths`). On first run the old `%LOCALAPPDATA%\OBDLinkDiag` contents are moved in: copy, verify (size + SHA-256), then remove the original; newer files are never overwritten; a `MIGRATED.txt` note is left behind. If the app folder isn't writable the app falls back to `%LOCALAPPDATA%\OBDLinkDiag` with a one-time notice.
- The data folder path is shown in the status bar and on the Guide tab, with an **Open data folder** button.
- Guide tab renders Markdown (headings, lists, bold, tables) instead of raw `##` / `|` text.
- Light theme: selected / hovered grid rows used white text on a light-blue row (nearly invisible); they now use the theme's selection text colour.
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
