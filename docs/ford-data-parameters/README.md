# Ford data parameters (reference)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

1451 parameter rows in 8 module groups, from a parameter list supplied by the project owner (2026-11-16).

| Module | Code | Parameters | File |
| --- | --- | --- | --- |
| 4x4 | 4x4 | 58 | [ford-params-4x4.md](ford-params-4x4.md) |
| Powertrain Control Module | PCM | 810 | [ford-params-pcm.md](ford-params-pcm.md) |
| Airbag / Restraints | Airbag | 21 | [ford-params-airbag.md](ford-params-airbag.md) |
| Antilock Brakes | ABS | 160 | [ford-params-abs.md](ford-params-abs.md) |
| Passive Antitheft System | PATS | 55 | [ford-params-pats.md](ford-params-pats.md) |
| Generic Electronic Module | GEM | 113 | [ford-params-gem.md](ford-params-gem.md) |
| Transmission Control Module | TCM | 179 | [ford-params-tcm.md](ford-params-tcm.md) |
| Tire Pressure Monitor | TPM | 55 | [ford-params-tpm.md](ford-params-tpm.md) |

## How to read the module files

- **Parameter**: the mnemonic as scan tools show it. A suffix in brackets is the unit form, e.g. `ECT` vs `ECT(V)`.
- **Range**: the values or states the parameter reports (`not available` = the source gave none).
- **Shares entry with**: other names the source documents together with this one (same range and meaning).
- **Also under**: other module groups that list the same name.
- **Description**: what the parameter shows, as supplied with the list.
- **Notes**: data-quality flags from the list (e.g. a description that seems to belong to another parameter). Treat flagged rows with extra care.
- PATS parameters may be located in any of these modules: HEC, ICM, PATS, PCM, SCIL, VIC.

Sensor reference tables (APP, DPFE, EBP, EVR, IAC, PFE, S/C, TR readings): [ford-params-reference-tables.md](ford-params-reference-tables.md).

## What is not here

- **DIDs / scaling**: not in the source. Adding a parameter to the app needs a verified DID, byte count and formula (`ford-enhanced-pids.json`), checked on a truck like TPMS 2813-2816.
