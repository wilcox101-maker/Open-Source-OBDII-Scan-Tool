# Livnitup "FORScan Tutorial" – programming entries

- **Source / credit:** Livnitup, "FORScan Tutorial" Google Doc (updated 6/14/26): https://docs.google.com/document/d/1-8dKaS_Spu4Zw4hV_CrKC4tLoP9G8yejqegF1wxIqxY/
- Imported as data. Confidence is CommunityReported, UNVERIFIED on Dale's truck. Applies to 2015-2020 F-150.
- The doc gives only **2 worked examples (5 As-Built lines)**: double-honk disable (BCM 726-41-02) and autofold mirrors (DDM 740-03-01 / 740-12-01, PDM 741-03-01 / 741-12-01).
- `merge-report.json` cross-references these against `../f150-asbuilt`. Result: **3 merged** (same address and nibble, value documented), **2 new** (the PDM 741 lines are not in the imported spreadsheet's tabs), **0 conflicting**. Nothing was overwritten.
- `mentioned_without_values` lists 7 mods the doc names without giving values. They are marked UNVERIFIED. For values, use the 2015-20 spreadsheet, which is already imported as `f150-asbuilt`.
- **Skipped on purpose:**
  - FORScan licence / hardware-ID / extended-licence steps (licensing, not data; nothing that gets around licensing is copied).
  - Pictures, which are referenced by link in docs/GUIDE.md only.
- Procedural tips are in docs/GUIDE.md and the in-app Guide tab. Adapter notes are in docs/ADAPTERS.md.
