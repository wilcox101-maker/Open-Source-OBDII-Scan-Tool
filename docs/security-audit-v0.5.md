# Security scrub — v0.5.0 (post-release hardening)

Date: 2026-10-26. Tree reviewed: `master` at `5ea7f8b` (v0.5.0). Version left at 0.5.0. No git history rewrite. No release cut.

**No back doors found.** No reverse shell, hardcoded password, private key, `Assembly.Load` from bytes, `BinaryFormatter` / `SoapFormatter` / `LosFormatter`, `TypeNameHandling`, `WebBrowser` / `WebView2`, eval, debugger-only Release path, or telemetry that posts VIN, logs, or adapter serials to a third party.

**VIN / secrets scan.** Current tree has no `E25268`, no `BEGIN PRIVATE KEY`, and no AWS-style access keys. The synthetic demo VIN `1FTDEMX95HF000001` is intentional. `E25268` remains only in old history (commits through `7e49068`, which removed the tail from the tree). History was not rewritten and old release assets were not deleted. The restdb.io key in `FirmwareFeed` is the public browser key already embedded in scantool.net; it is not a private secret. Removing it would break the firmware check.

How this was checked: source under `src/`, `tests/`, build scripts, and docs that describe behavior. `dotnet test` (Release) 410 passed. `dotnet build src/ObdLink.App/ObdLink.App.csproj -c Release` succeeded with 0 warnings (Linux, `EnableWindowsTargeting`).

## Counts

| Severity | Found | Fixed in this PR | Left open |
|---|---|---|---|
| Critical | 0 | 0 | 0 |
| High | 3 | 3 | 0 |
| Medium | 7 | 4 | 3 |
| Low | 5 | 0 | 5 |
| Informational | several | — | documented |

## High — fixed

### H1. Raw console could send a write while read-only

Location: `RawConsoleService` in `src/ObdLink.Core/Logging/Logging.cs`, guard in `src/ObdLink.Core/Console/ConsoleGuard.cs`.

The old filter only looked at a prefix. A line starting with `AT` or `ST` skipped the service-id check. `STPX` (STN pass-through) and `ATWM` (wakeup message) can put an arbitrary frame on the bus. Several state-changing services were missing (`0x08`, `0x28`, `0x2C`, `0x38`, `0x3D`, `0x85`, `0x86`, `0x87`). With CAN auto-formatting off (`ATCAF0`), the service id sits behind an ISO-TP PCI byte (`06 2E ...`, or a first frame `10 0A 2E ...`). The adapter pads a short CAN frame, so the byte count does not have to match the PCI length. A carriage return inside the line (`ATRV` then `2E...`) is one string to the app and two commands to the adapter, because the link appends a single CR.

Default is read-only and "Allow writes" off. A pasted line bypassed that control without turning writes on. This is local (the person at the keyboard, or a script they paste), not remote.

Fix: `ConsoleGuard` blocks control characters, `STPX`, `ATWM`, and `ATCAF0` while locked; blocks the full state-changing SID list; and, only when formatting is already off, also blocks a state-changing SID behind a single-frame or first-frame PCI. `01 2E` stays a mode-01 PID read while auto-formatting is on (the ELM default after `ATZ` / `ATWS` / `ATD`). Formatting state is tracked for the console session so a prior `ATCAF0` (sent while writes were allowed) still applies after read-only is turned back on. A blocked `ATCAF0` does not flip the flag.

### H2. Clear, As-Built write, and restore were not pinned to the module bus

Location: `ClearModuleDtcsCommand`, `WriteAsBuiltCommand`, `RestoreAsBuiltCommand`, `OnBus` / `OnBusCore` in `src/ObdLink.App/MainViewModel.cs`.

Reads switch to the remembered bus and return the adapter to HS-CAN. Clear, write, and restore did not use `OnBus`, so the UDS request went out on whatever protocol was current (usually HS-CAN). `OnBus` also retries another Ford bus after a no-reply. Using that retry on a write could send `0x14` or `0x2E` to the same transmit id on a different bus. Module ids in the 2017 F-150 table are unique across buses, but the table is marked unverified and v0.5.0 bus memory exists because the table can be wrong.

Fix: those three commands call `OnBus(..., allowFallback: false)`. The confirmation names the bus and says other buses are not tried. Write and restore add that target step in front of the existing typed `WRITE` phrase. If the adapter is not already on HS-CAN, an HS-CAN operation selects HS-CAN first. Non-HS operations still return to HS-CAN in `finally`. Reads keep the fallback.

### H3. A VIN (or other adapter text) could leave the data folder as a file name

Location: `AsBuiltService.BackupAsync`, `AsBuiltBatch.FileName` / `SaveAsync`, `VehicleProfile.FileNameFor`.

`Read VIN` keeps printable ASCII, not the VIN alphabet. Identify can save a 17-character string whose normalized WMI is `1FT` even when the raw string contains `..\`. `MaskVin` is eleven `X` plus the last six characters, so a backslash in those six still reached the vehicle-profile file name. `Path.Combine` then wrote outside `Data\`.

Fix: `SafePaths.Token` keeps ASCII letters, digits, `_`, and `-` (max 40). Backups and batch saves also require `SafePaths.IsUnder` the destination folder. The normal demo VIN still saves as `XXXXXXXXXXX000001.json`.

## Medium — fixed

### M1. Firmware zip download could follow a redirect and fail open

Location: `FirmwareFeed.IsOfficialUrl`, `FirmwareDownloader.DownloadAsync` in `src/ObdLink.Core/Firmware/FirmwareCheck.cs`; the download client in `MainViewModel.DownloadFirmwareAsync`.

`HttpClient.GetAsync` follows redirects. The final-URL check was skipped when `RequestUri` was null. `IsOfficialUrl` did not require the default port or an empty user-info. The feed itself is on `obdlinkappregs-58d8.restdb.io` (third party, public key, TLS with the default validator). The package URL must still be `https://scantool.net` or `https://www.scantool.net` and end in `.zip`. The app downloads and checks the `STNFWv` header. It does not flash.

Fix: auto-redirect is off. Each hop is checked before the body is read, and a null final URI is refused. Zip entry names are reduced to a leaf and must stay under the download folder. Expanded zip bytes are capped at 64 MB (the zip itself at 20 MB).

### M2. `updater.json` file name could leave the updater folder

Location: `OfficialUpdater.ExistingValid` / `DownloadAsync`, `MainViewModel` launch path.

`Path.Combine(dir, manifest.FileName)` accepts `..\..\payload.exe`. The installer kind is still signature-checked before launch, but the path should not leave `Data\updater`. A local writer of that folder already has the user's file rights.

Fix: `SafePaths.ConfineFile`. A name with a directory, drive, or `..` is rejected. The known installer name is the fallback.

### M3. First-run migration followed a junction, then deleted the source

Location: `AppPaths.MigrateFrom`.

On the first portable run the app copies `%LOCALAPPDATA%\OBDLinkDiag` into `Data\` and deletes each source file after a verified copy. `Directory.GetFiles` followed directory junctions and file symlinks, so a junction inside the old folder could delete files outside it.

Fix: enumeration skips reparse points. A relative path containing `.` or `..`, or a destination outside `DataDir`, is left in place and noted.

### M4. Update check showed whatever `html_url` the GitHub JSON contained

Location: `UpdateChecker.CheckAsync`.

The URL is shown in a message box. It is not opened by itself. A non-GitHub `html_url` could still be presented as the release link.

Fix: `SafeReleaseUrl` keeps only `https://github.com` on the default port with no user-info. Anything else becomes the repository releases page.

## Medium — deferred

### M5. HKCU uninstall key names the "installed" updater

`UpdaterWindows.FindInstalled` reads the uninstall registry. HKCU is writable by the user. "Run official updater" can then point at that path. The user sees the path and must click Yes. The installed copy and the settings override are not re-checked with Authenticode on every launch (the pinned hash is the installer, and the installed official updater is often unsigned). Re-prompting every launch would change the designed flow. The downloaded manifest path is now confined (M2).

### M6. Settings override can launch any executable

`FirmwareUpdaterPath` is an intentional "use this updater" setting. Launch still requires the Yes/No that shows the path. Anyone who can edit `settings.json` can already write files as the user.

### M7. `OpenExternal` allows any absolute local path

`ExternalTargets` allow-lists https hosts, `explorer.exe`, and absolute non-UNC paths. Callers pass app-controlled paths. `explorer.exe` arguments are not passed through `cmd.exe`. `RunUpdater` does not use this allow-list; it starts the updater executable directly so the official installer can request UAC. The app manifest does not request administrator.

## Low — deferred

### L1. `SafetyGuard` defaults to `WriteGate.AlwaysAllow`

If a caller omits the gate, the read-only switch is not consulted. The UI always passes `Gate`. Changing the default would break tests that write with an explicit confirm. Left as-is.

### L2. DTC clear is one Yes/No

As-Built write and restore still require the voltage check, confirmations, and the typed `WRITE` phrase, plus the new bus-target step. Clear names the module and the bus and does not hunt another bus. A typed phrase was not added; that would change the clear flow beyond the bus bug.

### L3. The owner's event log stores the VIN they just read

"Read VIN" writes the full VIN into `Data\logs`. Serilog file sinks only; the console sink package is referenced and unused. If the log folder cannot be created, logging falls back to `%TEMP%\OBDLinkDiag\logs` (same class as the `crash.txt` fallback next to the exe, prior audit F5). Diagnostics capture redacts VINs by default. The in-app event log was not masked; it is the owner's own record of a command they ran.

### L4. Save dialogs and the crash file can write outside `Data\`

Intentional. The user picks the save path. `crash.txt` is only the last resort when the data folder cannot be written (prior audit F5).

### L5. No NuGet lock file

Packages are `System.IO.Ports` 8.0.0, `Serilog` 4.0.2, `Serilog.Sinks.File` 6.0.0, `Serilog.Sinks.Console` 6.0.0 (unused at runtime), `System.Management` 8.0.0. No unexpected native DLL, `.pfx`, `.pem`, or `.env` in the tree. No packages.lock.json was added (would be a drive-by).

## Informational — confirmed, not changed

- Read-only is forced on every start (`AppSettings.ApplyStartupSafety` from the main view-model constructor). Writes are session-only.
- DEMO uses `PortKind.Virtual` and `SimulatedAdapter` only. `PortEnumerator` never marks a real COM port virtual. Demo profiles are dropped on a real port. Bus memory is not persisted from a simulated session. `FirmwareCheckPolicy.ShouldQuery` is false for a simulated adapter, including first run and the force path.
- UDS NRC `0x78` / `0x21` retries are limited to idempotent services `0x10`, `0x19`, `0x22`, `0x3E`. They are not used for `0x2E` or `0x14`.
- As-Built read NRC `0x31` becomes "not supported" and does not write.
- Outbound HTTP: firmware feed (restdb.io metadata, scantool.net zip only), official updater (scantool.net, hop-checked, Authenticode / known SHA-256, not flashed), opt-in GitHub releases API (user-agent only), opt-in NHTSA vPIC (VIN only, default off). No custom certificate validator.
- `DllImport` of `wintrust.dll` is Authenticode verification. `git rev-parse` in the project file is build metadata, not user input.
- `XDocument.Parse` uses the .NET default (DTDs prohibited). No XAML loaded from a user file.
- `community` feature names that contain the word "password" are As-Built labels, not credentials.
- No `.github` workflow, no service install, no autostart, no firewall rule, no Bluetooth pairing code.
- Firmware binaries are not in the repository.

## Tests

`tests/ObdLink.Core.Tests/SecurityScrubTests.cs` covers the locked console (including PCI-behind-SID only when formatting is off), backup and profile path confinement, official URL checks, zip entry and updater file names, a firmware redirect that must not be followed, and the three `allowFallback: false` call sites. `V020Tests` expects a non-GitHub `html_url` to become the releases page.
