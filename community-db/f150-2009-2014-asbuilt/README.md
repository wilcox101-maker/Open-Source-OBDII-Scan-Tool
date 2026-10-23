# F-150 2009–2014 As-Built (community-reported)

- **Source:** https://docs.google.com/spreadsheets/d/15MQ8CEh7eSXUJYjPg-J8uUqpLwP_xyqbfQHB0rAnXNs/edit#gid=1741285730, fetched 2026-10-23 as a public xlsx export.
- **Author:** jdunk54nl, f150forum thread f38/forscan-applications-349018. The "Modules & Utilities" table is credited to EpicCowlick's 2017 Super Duty sheet.
- **Vehicle:** 12th-generation F-150, 2009–2014. Some tabs are year-split, e.g. "ACM (2012)" and "ACM (201314)".
- **Confidence:** everything here is CommunityReported. The sheet itself warns that results vary by model year.
- **Records:** each record carries its `tab` name. The "VINs" tab was not imported because it holds third-party full VINs.
- **Separate from f150-asbuilt:** this data is kept apart from `../f150-asbuilt` (2015–2020) because it covers a different generation. `merge-report.json` lists address+pattern overlaps; nothing was overwritten.
- **Rebuilding:** `python3 tools/import_asbuilt_sheet.py <url> <xlsx> <outdir> <label>`.
