# F-150 2021+ As-Built (Livnitup, community-reported)

- **Vehicle: 14th-generation F-150, 2021+ (sheet covers 2021-26, incl. Lightning/Raptor/Tremor tabs).** Different module layout (e.g. BCMC 6F0, VDM 721, LDCMA 6F6, SOBDMC 7E6). **Not applicable to Dale's 2017** - the app does not load this folder for the 2017 profile.
- **Source:** "Livnitup's FORScan - 2021-26 F150", https://docs.google.com/spreadsheets/d/1-_e09lLPPeSHoO3SsNwyqu5v3dgpbluvJcezUItTSVI/ (linked from Livnitup's "FORScan Tutorial", updated 6/14/26). Exported 2026-10-23 via Google Drive; all 77 tabs read (67 yielded rows; e.g. "Module List" has no address rows).
- **Confidence:** CommunityReported / UNVERIFIED. Credit: Livnitup (f150forum / f150gen14).
- **Records:** 16188 feature/pattern rows in `features.json`, 27 module address prefixes in `module-addresses.json`, 0 complete lines. Each row keeps its `tab`.
- **Parser caveat:** some tabs put the value options ("0 =", "1 =") before the feature name, so `description`/`values` for those rows are shifted. Read the `values` array as the raw row text.
- Same schema as `../f150-asbuilt`. Files are stored compact (no indentation) to keep the repo small.
- Rebuild: `python3 tools/import_asbuilt_sheet.py <url> <xlsx> community-db/f150-2021plus-asbuilt "<label>"`.

| Tab | Rows |
|---|---|
| ABS (25-26) • 760 | 85 |
| ABS (old) • 760 | 53 |
| ABS - (21-24)  • 760 | 52 |
| ABS 25-26 RaptorTremor • 760 | 85 |
| ABS - 2022 RaptorTremor • 760 | 71 |
| ABS - 2021 Tremor (old) • 760 | 71 |
| ABS (21-23 RaptorTremor)  • 760 | 71 |
| ACM (21-23) • 727 | 1049 |
| ACM (25-26) • 727 | 1051 |
| APIM • 7D0 | 207 |
| APIM (2024) • 7D0 | 211 |
| BCM (2026) • 726 | 738 |
| BCM (2024-25) • 726 | 737 |
| BCM (21-23) • 726 | 687 |
| BCM-old • 726 | 679 |
| APIM (21-23) (old) • 7D0 | 180 |
| ACM (old) • 727 | 1044 |
| APIM (old) • 7D0 | 183 |
| APIM • 7D0 OLD | 156 |
| APIM - 2022 Lightning (old) • 7 | 150 |
| BCM (old) • 726 | 681 |
| BCM (21-23 Lightning)  • 726 | 716 |
| BCM- 2022 Lightning (old) • 726 | 549 |
| BCMC (old) • 6F0 | 34 |
| BCMC (21-23) • 6F0 | 52 |
| BCMC (24-25) • 6F0 | 63 |
| CMR (21-26) • 7C1 | 10 |
| DCACA (2024) • 6F1 | 13 |
| DSM • 744 | 410 |
| DSM (2023) • 744 | 484 |
| DSP • 783 | 8 |
| DSP (old) • 783 | 8 |
| DSP (2024) • 783 | 8 |
| FHCM-2022 Lightning • 6E3 | 42 |
| HCM • 734 | 35 |
| HCM -2022 Lightning • 734 | 36 |
| HCM (2024) • 734 | 52 |
| HCM (2024 Lightning) • 734 | 61 |
| HUD (2024) • 7B2 | 30 |
| HVAC (2024-26) • 733 | 71 |
| HVAC (21-23) • 733 | 90 |
| HVAC (Lightning) • 733 | 68 |
| IPC • 720 | 192 |
| IPC (2024) • 720 | 195 |
| IPC (24 Lightning) • 720 | 191 |
| HVAC • 733 - old | 45 |
| IPMA • 706 | 539 |
| IPMA_MY22 • 706 | 544 |
| IPMA - 2023 • 706 | 550 |
| IPMA (BC 1.3) • 706 | 561 |
| IPMA (2024-AB) • 706 | 563 |
| IPMA (24 Lightning) • 706  | 560 |
| LDCMA • 6F6 | 159 |
| PSCM • 730 | 161 |
| RCM • 737 | 32 |
| SASM • 797 | 13 |
| SCCM (old) • 724 | 39 |
| SCCM (2024) • 724 | 41 |
| SCCM - (21-23) • 724 | 38 |
| SCMG • 712 | 181 |
| SECM • 7C5 | 35 |
| SOBDMC (22 Lightning) • 7E6 | 8 |
| TCU • 754 | 172 |
| TRM (21-23) • 791 | 55 |
| VDM -2022 • 721 | 107 |
| VDM (2024) • 721 | 120 |
| WACM • 725 | 6 |
