# obdcli: command line (read-only toward the vehicle)

`obdcli` runs the app's Core from a terminal or a script. It is for repeatable jobs (scan, DTC report, live-data CSV, bus
capture, As-Built backup) and for contributors who want to work without a truck: every command also runs against the
built-in simulator with `--demo`.

**It never writes to the vehicle.** Every UDS client it creates has a closed write gate, so a state-changing service
(clear DTCs, As-Built write, routines, resets, 85/28/2F) is refused before anything is sent. A test checks the source for
write calls. Writing stays in the app, behind its backups and confirmations.

## Get it
- CI publishes a self-contained Windows build for every commit: artifact `obdcli-win-x64-<sha>` (one `obdcli.exe`, no install).
- From source: `dotnet run --project tools/ObdCli -- <command> ...` (any OS with the .NET 8 SDK; the serial port works on
  Windows and Linux).

## Connection
| Option | Meaning |
|---|---|
| `--port COM5` | Real ELM327 / STN adapter. Baud is probed (MX+ Bluetooth and EX USB rates) unless `--baud N` is given. |
| `--demo [mx\|ex]` | The simulator (MX+ by default; `ex` imitates the OBDLink EX). |
| `--j2534 NAME\|DLL` | J2534 pass-thru device: part of its registered name (see `j2534-list`) or the driver DLL path. |
| `--j2534-demo` | A simulated J2534 device with the same vehicle as `--demo`. |
| `--json` | Machine-readable output instead of text. |

Exit codes: `0` OK, `1` error (adapter, vehicle, file), `2` usage error.

## Commands
| Command | What it does |
|---|---|
| `j2534-list` | J2534 v04.04 devices registered on this PC, the driver's CPU type, and whether this build can load it. |
| `info` | Adapter identity (ATI / STI / STDI, or the J2534 driver's versions), buses, battery voltage. |
| `scan` | 2017 F-150 module scan with the app's alternate-bus retries (MX+ / EX). |
| `dtcs [--module 7E0\|all]` | UDS DTCs (19 02) with status and the open DTC text; `all` (default) scans first. |
| `live --signals rpm,speed,coolant [--seconds 5] [--csv out.csv]` | OBD-II Mode 01 live data, multi-PID requests, min/max/avg summary. Signal keys are the dashboard's (`rpm`, `speed`, `coolant`, `iat`, `maf`, `throttle`, `load`, …). |
| `monitor [--bus hs\|ms\|hs2] [--seconds 5] [--filter all\|diag\|7E8\|700/780] [--csv out.csv]` | Passive bus monitor (silent ATMA), per-ID count / rate / data / changed bits. |
| `asbuilt-read --module 726 [--out 726.abt]` | Reads every As-Built block of one module; `--out` saves a FORScan-format `.abt`. |
| `options --abt 726.abt [--tab NAME] [--search TEXT]` | Offline: lists the 2015-2020 F-150 community-sheet options for the file's lines, with the current value and meaning. |
| `options --abt 726.abt --set "ADDR PATTERN=DIGITS" [--set ...] --out 726-new.abt` | Offline: edits options and writes a **new** `.abt` (checksums recomputed, conflicting edits refused, the input file is never overwritten). Load the new file in the app (Import .abt) to review it and write it through the normal safe path. |

## J2534 pass-thru devices (VCM II / VCMM, Mongoose, clones, DIY)
- `obdcli j2534-list` shows what is installed. Most J2534 drivers are **32-bit**: use the **x86** `obdcli` build
  (`obdcli-win-x86`) for them; the x64 build refuses a 32-bit driver with a message saying so.
- HS-CAN uses ISO 15765 on pins 6/14. MS-CAN and HS-CAN2 use J2534-2 pin selection (ISO15765_PS on pins 3/11); a device
  without J2534-2 support reaches HS-CAN only, and `info` says so.
- With J2534 the app waits properly after a module's "response pending" instead of sending the request again.
- `monitor` needs an ELM327/STN adapter for now.
- Close other programs that use the device first (one program per device).

## Examples
```
obdcli --demo info
obdcli scan --port COM5 --json > scan.json
obdcli dtcs --port COM5 --module all
obdcli live --port COM5 --signals rpm,speed,coolant,maf --seconds 60 --csv drive.csv
obdcli monitor --port COM5 --bus ms --seconds 30 --csv ms.csv
obdcli asbuilt-read --port COM5 --module 7D0 --out apim.abt
obdcli j2534-list
obdcli scan --j2534 "VCM II"
obdcli dtcs --j2534 "C:\\Program Files (x86)\\Vendor\\driver.dll" --module all
obdcli options --abt apim.abt --search "Smart DSP"                      # default tab for 7D0 is "APIM Sync 1"
obdcli options --abt apim.abt --tab "APIM Sync 3" --search "Smart DSP"  # a SYNC 3 truck: pick its tab
obdcli options --abt apim.abt --tab "APIM Sync 3" --set "7D0-01-01 *xxxxxxxxx--=8" --out apim-dsp.abt
```

`--set` takes the address and the pattern exactly as `options` lists them for the chosen tab. The tab matters: the same
address means different things on SYNC 1 / 2 / 3 (`options` prints the other tabs).

The option meanings come from a community spreadsheet (CommunityReported / UNVERIFIED), not from Ford. Compare with
FORScan before writing anything to a module.
