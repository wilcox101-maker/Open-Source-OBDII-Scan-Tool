# Code audit - v0.3.4 (2026-10-24)

Scope: everything under `src/` and `tests/` (C#, XAML), plus README.md, docs/GUIDE.md, docs/ADAPTERS.md,
docs/RELEASE-NOTES.md and docs/README-TEST.txt. Rule: fix what is safe, add a test for every fix, and list
risky items as findings instead of changing behavior. Tests: 320 before, 345 after (all pass).

How to repeat:
- `dotnet build -c Release --no-incremental -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended`
- `codespell` (uses `.codespellrc`) and `codespell --builtin clear,rare,informal,usage,en-GB_to_en-US` for American spelling.

## Counts

| Category | Found | Fixed | Open |
|---|---|---|---|
| Compiler warnings (CS: nullable CS8601/8602/8603, CS9113) | 7 | 7 | 0 |
| Analyzer correctness (CA2020, CA1806, CA1869 x2, CA1847, CA1875) | 6 | 6 | 0 |
| Analyzer: types owning disposables (CA1001) | 5 | 1 (VpicClient) | 4 (finding F4) |
| Analyzer style (CA1707 162, CA1305 81, CA1310 76, CA1861 37, CA1822 20, CA1725 20, CA1826 7, CA1859 6, others 14) | 423 | 0 | style only, not changed |
| async void / unobserved tasks | 5 checked | 1 | 0 |
| Threading / race conditions | 1 | 1 | 0 |
| Disposal (SerialPort, HttpClient) | 2 | 2 | 0 |
| Swallowed exceptions (`catch { }`) | 46 sites | 0 | reviewed, finding F3 |
| Paths outside the Data folder | 2 | 0 | both intentional, F5 |
| DEMO-to-real leaks | 0 new | - | - |
| Security (URL / process launch) | 1 | 1 | 0 |
| Spelling (typos) | 0 real | - | domain words added to `.codespellrc` |
| British spellings (code, comments, UI strings, docs) | 46 | 44 | 2 (F6) |
| Doc consistency | 1 (pids file path) | 1 | 0 |

## Bugs fixed (each has a test in `tests/ObdLink.Core.Tests/V034Tests.cs`)

1. **Unchecked shell launch** - `DialogService.OpenExternal` passed any string to ShellExecute. It now only opens
   https links to an allow-list (github.com, motorcraftservice.com, scantool.net, obdlink.com, docs.google.com) on the
   default port with no user info, `explorer.exe`, or a full local (non-UNC) path (`Core/Security/ExternalTargets.cs`).
   `RunUpdater` still launches the official updater exe found on disk after its signature check; unchanged.
2. **Dashboard polling loops could overlap** - hiding and showing the page (or reconnecting) quickly started a new
   loop while the old one was still finishing; they shared `_engine` and the old loop's `finally` set `Active=false`
   for the new one. The new loop now awaits the old one first.
3. **async void could crash the app** - `AsyncCommand.Execute` is `async void`; if the error handler itself threw,
   the exception went to the dispatcher. It is now caught and logged.
4. **SerialPort leaked on a failed open** - if `Open()` threw (port busy/missing) the half-built `SerialPort` was kept
   and never disposed, and a re-open dropped the old handle. Fixed in `SerialPortTransport.OpenAsync`.
5. **HttpClient leak** - `Identify vehicle` created a new `VpicClient` (own HttpClient) every run without disposing it.
   `VpicClient` is now `IDisposable` and used with `using`.
6. **Null written into a non-null setting** - clearing "default port" stored `null` in `Adapter.Port` (declared
   non-null `""`). Now stores `""`.
7. **WndProc wParam truncation** (CA2020) - `(int)wParam` replaced by `wParam.ToInt64()`.
8. Smaller: second `WinVerifyTrust` (CLOSE) result explicitly discarded; unused `owner` parameter removed from
   `WpfConfirmationProvider`; cached `JsonSerializerOptions` (DtcDatabase, OfficialUpdater); AutoScroll static
   field order (CS8602); test-only nullable/analyzer warnings.

Also in v0.3.4: the **OSS (output shaft speed)** widget. Ford Mode 22 DID 1E13, raw/4 rpm, **UNVERIFIED** (no
confirmed community DID was found for 6R80/10R80); tried on PCM 0x7E0 then TCM 0x7E1, remembering which answered.
An older `Data\ford-enhanced-pids.json` gains only the new `oss` entry; deleted entries stay deleted.

## Checked, no change needed

- Fire-and-forget calls: startup rescan (`MainWindow`), `RefreshPortsCommand.ExecuteAsync` and
  `RunFirmwareCheckAsync` all catch internally; dashboard `RunAsync` catches everything and logs.
- CSV recorder and log timestamps already use InvariantCulture, so a decimal comma cannot corrupt exports.
- ELM/UDS command strings use hex formats (`X2`), which are not culture-dependent.
- Writes: settings, logs, backups, firmware, vehicles and pids all go to `Data\` via `Paths`.
- DEMO: the simulator is only reached through the DEMO port; v0.3.2 demo-profile guards are intact.

## Open findings for Dale

- **F1 OSS DID is a guess.** Please check 1E13 with FORScan (look for OSS / OSS_SRC on PCM or TCM) and send the
  real DID; it is a one-line edit in `Data\ford-enhanced-pids.json`.
- **F2 Simulator VIN tail.** `SimulatedAdapter.Vin = "XXXXXXXXXXXE25268"` contains the last 6 characters of your
  real VIN. Not the full VIN (VIN check passes), but I suggest replacing it with a synthetic tail. Not changed, because
  tests and demo profiles key on it.
- **F3 Empty catches (46).** Most are deliberate (BT dropout on close, best-effort Explorer, cleanup). A few in
  `MainViewModel` (Explorer/firmware page `try { OpenExternal } catch { }`) now also hide an allow-list refusal;
  logging them would be better. Behavior unchanged for now.
- **F4 CA1001.** `MainViewModel` (`_batchCts`, `_bus`) and `DashboardViewModel` (`_cts`, `_rec`) are app-lifetime
  objects; making them IDisposable means wiring Window.Closed. Low risk of a real leak; left as is.
- **F5 Paths outside Data.** crash.txt fallback in the exe folder (when Data cannot be written) and user-chosen Save
  locations for .ab/.abt export (default `Data\asbuilt`). Both intentional.
- **F6 "Cancelled" in identifiers.** `UserCancelledException`, `IEventOperation.Cancelled()` and the log status
  "Cancelled" are kept to avoid breaking saved logs/tests; all user-facing text now says "canceled". A captured
  FORScan fixture also contains "cancelled" and is kept verbatim.
- **F7 Style warnings** (CA1707 test names with underscores, CA1305/CA1310 culture overloads, CA1822 static
  suggestions) are left; changing them is churn with no behavior change.
