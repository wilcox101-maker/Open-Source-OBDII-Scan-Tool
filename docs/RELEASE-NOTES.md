# Release notes

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
