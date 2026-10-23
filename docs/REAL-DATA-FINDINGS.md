# What Dale's real logs and reference files show (2026-10-23)

## FORScan text logs from Dale's 2017 F-150 (redacted copies in tests/ObdLink.Core.Tests/Fixtures/forscan)
- **Vehicle:** 2017 F-150, 3.5L TiVCT, SYNC 1.1, odometer 183,387.8 mi. VIN redacted to its last 6 characters (…E25268).
- **Adapter:** FORScan used an **OBDLink EX** (r2.2.1 STN2231, firmware v5.10.3 and v5.13.0), FTDI USB on COM4 at 2,000,000 bps, min delay 14 ms. It is not an MX+. FORScan tried Bluetooth first and that failed. The first vehicle connect failed with "ignition key ON".
- **Modules found (20):** OBD2_PCM, PCM, APIM, BdyCM, FCIM, FCDIM, OCS, TCCM, ABS, PBM, RTM, PDM, DDM, RCM, PSCM, ACM, SCCM, IPC, GWM, GPSM.
- **What the logs contain:** part numbers and strategies, DTCs with FORScan's `code:FTB-status` notation, KOEO/KOER self-tests and Mode 6 results, and PBM service procedures.
- **What the logs don't contain:** CAN IDs, bus assignment, AT/ST commands, STP values, raw frames, As-Built data, session/security exchanges, multi-frame timing, or anything from the Ram. None of those can be verified from these files.
- **The `.fsl` file:** FORScan's binary live-data stream. It isn't parsed and isn't committed.

## Reference files
- **The .docx is not the app spec.** It is Livnitup's community "FORScan guide" for the 2015+ F-150 (updated 12/29/22). It isn't committed for copyright reasons. Useful facts from it:
  - Checksum examples `726-16-02 0101 0101 0049` and `740-13-01 005B`. Both pass our checksum algorithm and are now tests.
  - HS-CAN and MS-CAN are both needed. The OBDLink LX lacks MS-CAN; the MX and EX work.
  - Program with the key on and the engine off (KOEO). Avoid extra current draw while writing.
  - Cycle the key after changing the BCM.
  - FORScan can't back up or restore IPMB correctly.
  - FORScan rewrites checksums itself.
- **The .xlsx is the f150forum 2015–2020 F-150 As-Built decoding spreadsheet** ("Common" sheet revised 10/29/22). I converted it to `community-db/f150-asbuilt/`:
  - `features.json`: 2,709 address, nibble-pattern and description entries.
  - `module-addresses.json`: module address prefixes, one per sheet.
  - `complete-lines.json`: 249 fully specified lines. 239 pass the checksum; the 10 that fail are hand-edited examples.
- **Module address changes from the spreadsheet:** TRM is 0x791 (was 0x775), FCDIM is 0x7A5 (new).
- **Other addresses in the spreadsheet:** HSWM 714, SCME 776, DSP 783, IPMB 7B1, TCU 754, SCMG/SCMH 712.
- **Ram:** no data in any file.

## Gaps against PLAN.md / the build
- We need a FORScan debug log or a raw STN capture to verify buses, CAN IDs and timing.
- We need a FORScan `.abt` backup of Dale's modules to verify DID mapping and bytes per line on his truck.
- Every As-Built value remains CommunityReported.
