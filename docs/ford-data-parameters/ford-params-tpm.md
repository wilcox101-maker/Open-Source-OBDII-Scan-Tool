# Ford data parameters: Tire Pressure Monitor (TPM)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

55 parameters. Index: [README.md](README.md).

| Parameter | Range | Description | Shares entry with | Also under | Notes |
| --- | --- | --- | --- | --- | --- |
| `CCNT_TPMS` | not available | Indicates continuous codes. |  |  |  |
| `HORN_TPM` | ACTIVE/INACTIVE | Indicates horn output. |  |  |  |
| `LAST_ID` | not available | Indicates the last received tire transmitter ID code value. |  |  |  |
| `LFAWAKE` | not available | Left front awake status. |  |  |  |
| `LFIDPRG` | YES/NO | Left front sensor programmed. |  |  |  |
| `LFLOBAT` | LOW/OK | Left front low battery. |  |  |  |
| `LF_ID` | not available | Indicates the left front tire transmitter identifier. |  |  |  |
| `LF_LRN` | ACTIVE/INACTIVE | Indicates the left front learn status. |  |  |  |
| `LF_MES` | ACTIVE/INACTIVE | Left front re-measure status. |  |  |  |
| `LF_NORM` | ACTIVE/INACTIVE | Left front normal status. |  |  |  |
| `LF_PSI` | not available | Left front tire pressure. |  |  |  |
| `LF_REC` | YES/NO | Left front transmit has been received after learn. |  |  |  |
| `LRAWAKE` | not available | Left rear awake status. |  |  |  |
| `LRIDPRG` | YES/NO | Left rear sensor programmed. |  |  |  |
| `LRLOBAT` | LOW/OK | Left rear low battery. |  |  |  |
| `LRO_PSI` | not available | Left rear outer tire pressure. |  |  |  |
| `LR_ID` | not available | Left rear tire transmitter identifier. |  |  |  |
| `LR_LRN` | ACTIVE/INACTIVE | Left rear learn status. |  |  |  |
| `LR_MES` | ACTIVE/INACTIVE | Right rear re-measure status. |  |  |  |
| `LR_NORM` | ACTIVE/INACTIVE | Left rear normal status. |  |  |  |
| `LR_REC` | YES/NO | Left rear transmit has been received after learn. |  |  |  |
| `RESET` | ACTIVE/INACTIVE | Parameter reset command. |  |  |  |
| `RFAWAKE` | not available | Right front awake status. |  |  |  |
| `RFIDPRG` | YES/NO | Right front sensor programmed. |  |  |  |
| `RFLOBAT` | LOW/OK | Right front low battery. |  |  |  |
| `RF_ID` | not available | Right front tire transmitter identifier. |  |  |  |
| `RF_LRN` | ACTIVE/INACTIVE | Right front learn status. |  |  |  |
| `RF_MES` | ACTIVE/INACTIVE | Right front re-measure status. |  |  |  |
| `RF_NORM` | ACTIVE/INACTIVE | Right front normal status. |  |  |  |
| `RF_PSI` | not available | Right front tire pressure. |  |  |  |
| `RF_REC` | YES/NO | Right front transmit bas been received after learn. |  |  |  |
| `RRAWAKE` | not available | Right rear awake status. |  |  |  |
| `RRIDPRG` | YES/NO | Right rear sensor programmed. |  |  |  |
| `RRLOBAT` | LOW/OK | Right rear low battery. |  |  |  |
| `RRO_PSI` | not available | Right rear outer tire pressure. |  |  |  |
| `RR_ID` | not available | Right rear tire transmitter identifier. |  |  |  |
| `RR_LRN` | ACTIVE/INACTIVE | Right rear learn status. |  |  |  |
| `RR_MES` | ACTIVE/INACTIVE | Right rear re-measure status. |  |  |  |
| `RR_NORM` | ACTIVE/INACTIVE | Right rear normal status. |  |  |  |
| `RR_REC` | YES/NO | Right rear transmit has been received after learn. |  |  |  |
| `SPAWAKE` | not available | Spare awake status. |  |  |  |
| `SPIDPRG` | YES/NO | Spare sensor programmed. |  |  |  |
| `SPLOBAT` | ACTIVE/INACTIVE | Spare low battery. |  |  |  |
| `SPR_ID` | not available | Spare tire transmitter identifier. |  |  |  |
| `SPR_PSI` | not available | Spare tire pressure. |  |  |  |
| `SP_LRN` | ACTIVE/INACTIVE | Spare learn status. |  |  |  |
| `SP_MES` | ACTIVE/INACTIVE | Spare re-measure status. |  |  |  |
| `SP_NORM` | ACTIVE/INACTIVE | Spare normal status. |  |  |  |
| `SP_REC` | YES/NO | Spare transmit has been received after learn. |  |  |  |
| `VSS_TPM` | not available | Vehicle speed. |  |  |  |
| `WARN_1` | not available | Transmitter identifier with warning value 1. |  |  |  |
| `WARN_2` | not available | Transmitter identifier with warning value 2. |  |  |  |
| `WARN_3` | not available | Transmitter identifier with warning value 3. |  |  |  |
| `WARN_4` | not available | Transmitter identifier with warning value 4. |  |  |  |
| `WARN_5` | not available | Transmitter identifier with warning value 5. |  |  |  |
