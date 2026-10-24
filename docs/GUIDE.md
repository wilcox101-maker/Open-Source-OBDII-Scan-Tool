# FORScan / As-Built programming guide (community)

Adapted from **Livnitup's "FORScan Tutorial"** (updated 6/14/26): https://docs.google.com/document/d/1-8dKaS_Spu4Zw4hV_CrKC4tLoP9G8yejqegF1wxIqxY/. All credit goes to Livnitup. This is a summary for Ford trucks. Read the original for full detail. Tips are community-reported (UNVERIFIED on Dale's truck).

## Where this app keeps its files

| Item | Location (inside the `Data` folder next to the exe) |
|---|---|
| Settings | `settings.json` |
| Logs and crash reports | `logs\` |
| Diagnostics captures | `captures\` |
| As-Built backups | `backups\` |

Use **Open data folder** (status bar or the top of this tab) to open it. Back up the `backups` folder before any write. If the app folder is read-only, the app uses `%LOCALAPPDATA%\OBDLinkDiag` instead and tells you once.

## Before you change anything
1. Battery: key ON, engine OFF. Keep extra current draw to a minimum (lights, HVAC blower, doors closed). Use a battery maintainer for long sessions.
2. **Back up every module's As-Built first**, and save the file off the laptop. This app saves backups automatically before any write.
3. **IPMB** values can't be backed up by FORScan. Write them down by hand before changing anything.
4. Change one module at a time and write down the original value.

## Making a change
- Patterns use `x` for "keep what you have". Change only the non-x nibble(s).
- FORScan may say "incorrect checksum" after you edit a line. Livnitup says to ignore it; the checksum is recalculated on write.
- After a BCM write, cycle the key (OFF, wait, ON).
- After an IPC change, TPMS readings can go blank for a short time. They come back after driving.
- SYNC/APIM reset: hold **Power + Seek ▶︎ for about 10 s**.
- With a switch-type USB adapter (OHP/VINT), set HS/MS the way FORScan prompts. The OBDLink EX and MX+ switch automatically. The OBDLink LX has no MS-CAN.

## Worked examples in the tutorial (2015-2020; also in community-db/livnitup-forscan-tutorial)
| Mod | Module | Address | Value |
|---|---|---|---|
| Disable double horn honk | BCM | 726-41-02 | xxx0 xxxx xxxx |
| Autofold mirrors | DDM | 740-03-01 / 740-12-01 | x8xx xx / Fxxx |
| Autofold mirrors | PDM | 741-03-01 / 741-12-01 | x8xx xx / Fxxx |

## Pictures (reference only; images stay on the author's Drive and are not in this repo)
- Pic 1: https://drive.google.com/file/d/0B3jKTCd57R6ycmxGNXFGZUVLb1E/view
- Pic 2: https://drive.google.com/file/d/0B3jKTCd57R6yRTJmYl9qZnlJVlE/view
- Pic 1 (second section): https://drive.google.com/file/d/0B3jKTCd57R6yclU0ajVubTYzaGs/view
- Pic 2 (second section): https://drive.google.com/file/d/0B3jKTCd57R6yVzVFbVJjeC1SZW8/view
- Pic: https://drive.google.com/file/d/0B3jKTCd57R6ycml3X2JtemtkaTA/view

## Licensing
FORScan's As-Built editing needs a FORScan extended licence. Get one from forscan.org; a free trial licence is available. This repo does not include any licence procedure or workaround.

## Helpful links (from the tutorial)
- FORScan download: https://forscan.org/download.html
- 2015-2020 F-150 sheet: https://docs.google.com/spreadsheets/d/1uDSQ1Z5a2Wt8-kjrSiVSlDFGFHnfeuhb3RTMVz95730/ (imported as `community-db/f150-asbuilt`)
- 2021+ F-150 sheet: https://docs.google.com/spreadsheets/d/1-_e09lLPPeSHoO3SsNwyqu5v3dgpbluvJcezUItTSVI/ (imported as `community-db/f150-2021plus-asbuilt`; **not for 2015-20**)
- Factory As-Built by VIN: https://www.motorcraftservice.com/AsBuilt
- As-Built compare: http://compulsivecode.com/Project_AsBuiltCompare.aspx · Firn's decoder: https://asbuilt.altctrlwrench.com/
- Backup video: https://youtu.be/XhFUlFgQPVE
- Module acronyms (Airborne_Ape, f150forum): imported as `community-db/ford-module-acronyms`
- Ford towing calculator: https://www.ford.com/support/towing-calculator · Magnuson-Moss Warranty Act: https://www.consumer.ftc.gov/articles/0138-auto-warranties-routine-maintenance
- The tutorial also links many forum write-ups (BLIS, ACC, LED headlights, ambient lighting, and others) and sheets for other Ford models. Those were not imported.
