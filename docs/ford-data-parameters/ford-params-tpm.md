# Ford data parameters: Tire Pressure Monitor (TPM)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

55 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `CCNT_TPMS` | not available |  |  |
| `HORN_TPM` | ACTIVE/INACTIVE |  |  |
| `LAST_ID` | not available |  |  |
| `LFAWAKE` | not available |  |  |
| `LFIDPRG` | YES/NO |  |  |
| `LFLOBAT` | LOW/OK |  |  |
| `LF_ID` | not available |  |  |
| `LF_LRN` | ACTIVE/INACTIVE |  |  |
| `LF_MES` | ACTIVE/INACTIVE |  |  |
| `LF_NORM` | ACTIVE/INACTIVE |  |  |
| `LF_PSI` | not available |  |  |
| `LF_REC` | YES/NO |  |  |
| `LRAWAKE` | not available |  |  |
| `LRIDPRG` | YES/NO |  |  |
| `LRLOBAT` | LOW/OK |  |  |
| `LRO_PSI` | not available |  |  |
| `LR_ID` | not available |  |  |
| `LR_LRN` | ACTIVE/INACTIVE |  |  |
| `LR_MES` | ACTIVE/INACTIVE |  |  |
| `LR_NORM` | ACTIVE/INACTIVE |  |  |
| `LR_REC` | YES/NO |  |  |
| `RESET` | ACTIVE/INACTIVE |  |  |
| `RFAWAKE` | not available |  |  |
| `RFIDPRG` | YES/NO |  |  |
| `RFLOBAT` | LOW/OK |  |  |
| `RF_ID` | not available |  |  |
| `RF_LRN` | ACTIVE/INACTIVE |  |  |
| `RF_MES` | ACTIVE/INACTIVE |  |  |
| `RF_NORM` | ACTIVE/INACTIVE |  |  |
| `RF_PSI` | not available |  |  |
| `RF_REC` | YES/NO |  |  |
| `RRAWAKE` | not available |  |  |
| `RRIDPRG` | YES/NO |  |  |
| `RRLOBAT` | LOW/OK |  |  |
| `RRO_PSI` | not available |  |  |
| `RR_ID` | not available |  |  |
| `RR_LRN` | ACTIVE/INACTIVE |  |  |
| `RR_MES` | ACTIVE/INACTIVE |  |  |
| `RR_NORM` | ACTIVE/INACTIVE |  |  |
| `RR_REC` | YES/NO |  |  |
| `SPAWAKE` | not available |  |  |
| `SPIDPRG` | YES/NO |  |  |
| `SPLOBAT` | ACTIVE/INACTIVE |  |  |
| `SPR_ID` | not available |  |  |
| `SPR_PSI` | not available |  |  |
| `SP_LRN` | ACTIVE/INACTIVE |  |  |
| `SP_MES` | ACTIVE/INACTIVE |  |  |
| `SP_NORM` | ACTIVE/INACTIVE |  |  |
| `SP_REC` | YES/NO |  |  |
| `VSS_TPM` | not available |  |  |
| `WARN_1` | not available |  |  |
| `WARN_2` | not available |  |  |
| `WARN_3` | not available |  |  |
| `WARN_4` | not available |  |  |
| `WARN_5` | not available |  |  |
