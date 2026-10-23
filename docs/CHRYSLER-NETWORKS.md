# Chrysler/Dodge vehicle networks — 2008 Ram 1500 (DR/DH) vs 2009+ DS

Research date: 2026-10-23. Confidence levels: HIGH = factory wiring/connector data; MED = multiple secondary sources agree; LOW = single or unverified source.

## 2008 Ram 1500 (DR/DH) verdict
- The factory 2008 RAM 1500 connector view C219 (74-pin) lists **CAN B BUS (+/-)** (D55 20WT/OR, D54 20WT), **CAN C BUS (+/-)** (D65 20WT/LG, D64 20WT/LB), **DIAGNOSTIC CAN C (+/-)** (D52 20WT/LB, D51 20WT/BR), plus **SCI transmit/receive to the TCM** (D15/D16). It does **not** list CAN IHS. HIGH confidence. Source: https://charm.li/Dodge%20and%20Ram/2008/RAM%201500%20Truck%202WD%20V8-4.7L/Repair%20and%20Diagnosis/Diagrams/Connector%20Views/C200%20-%20C299/C219/
- The 2008 Ram FSM "CAN Bus" operation text says the **TIPM/Central Gateway (TIPMCGW)** moves messages between the CAN-B, CAN-C, and CAN-D buses. HIGH confidence. Source: https://lemon-manuals.org.ua/Dodge%20and%20Ram/2008/Pickup%20R1500%2C%204D%20Pickup%2C%203.7%20K%2C%20Automatic/Repair%20and%20Diagnosis/Accessories%20%26%20Equipment/Communication%20Devices/Electronic%20Control%20Modules%20%28Service%20Information%29/Electronic%20Control%20Modules/Communication/Operation/Can%20Bus/
- Conclusion: the 2008 DR uses **CAN C** (500k, high-speed) and **CAN B** (low-speed, fault-tolerant body bus) with a TIPM gateway. A scan tool reaches modules through the **diagnostic CAN C pair on DLC 6/14**, and the TIPM routes requests to CAN-B modules. There is **no CAN IHS**; that is a 2009+ DS-era bus. I found no evidence that the 2008 Ram uses J1850 PCI on pin 2. Pre-CAN DR trucks (2002 to roughly 2005) did use PCI. The exact model year each module switched was not verified, so check the 2006-2007 DR wiring before assuming PCI or CAN. MED confidence.
- CAN B bit rate: Chrysler CAN-B is generally documented as 83.3 kbps fault-tolerant (ISO 11898-3). I did not confirm this from a 2008 Ram document in this pass. MED-LOW confidence. Assume CAN B is **not** on DLC pins 3/11 for 2008. The C219 data suggests it is reached only through the gateway.
- The TCM on the 2008 uses **SCI** lines (C219 pins 2/44) for at least part of its communication. HIGH confidence that the circuits exist; their exact role was not researched.

## 2009+ Ram DS (and later TIPM/CGW vehicles)
- CAN C runs at 500 kbps on DLC 6/14, and CAN IHS (interior high speed) runs at 125 kbps on DLC 3/11. MED confidence; a video of a scoped DS-era Ram shows IHS going straight from the DLC to a star connector with no gateway. Sources: https://www.youtube.com/watch?v=iY-9vXA9C_c , https://magikh0e.pl/pubCarHacking/ODB-II_Diagnostic_Cable/ , https://openlaborproject.com/diagnostics/can-bus/ram/ (that last page itself flags conflicting drafts)
- The TIPM acts as the gateway, and scan-tool traffic goes over diagnostic CAN C on 6/14. MED confidence. Source: https://www.macsw.org/common/Uploaded%20files/MACS%20Service%20Reports/2020_03msr4email.pdf
- The 2009 FSM "CAN communication – 1500" no-response tests (ABS, CCN, DDM, PDM, DTCM, HFM, HSM, HVAC, MSMD, ORC, PCM NGC, PTS, Radio, SAS, TIPM, WIN, etc.) all run through diagnostic CAN C. MED-HIGH confidence. Source: https://alloemmanuals.com/manual/Dodge%20and%20Ram/2009/Pickup%20R2500%2C%204D%20Pickup%20Crew%20Cab%2C%205.9%207%2C%204WD%2C%20Standard/Repair%20and%20Diagnosis/External%20Pages/Different%20variant%2Ftrim/Section%2054%20(Can%20Communication%20System%20-%20Non-DTC%20Diagnostics%20-%201500)
- 2018+ Ram: a Secure Gateway (SGW) sits between the DLC and both diagnostic CAN C and diagnostic CAN IHS, acting as a frame gateway. Source: https://diag.net/msg/m1fsoznwl3nndqti9pxq9k4nz0

## Key facts table
| Item | 2008 DR | 2009+ DS | Confidence |
|---|---|---|---|
| High-speed bus | CAN C, 500k | CAN C, 500k | HIGH/MED |
| Body bus | CAN B (≈83.3k FT, unverified for 2008) | CAN IHS 125k (high-speed physical layer, not fault-tolerant per common sources) | MED / MED |
| DLC 6/14 | Diagnostic CAN C (from TIPM) | Diagnostic CAN C | HIGH / MED |
| DLC 3/11 | Not found on 2008 | CAN IHS | LOW-MED / MED |
| DLC 2 (PCI) | Not found for 2008 | none | LOW |
| Gateway | TIPM/CGW | TIPM | HIGH / MED |
| Wire colors | Diag CAN C+ D52 WT/LB, - D51 WT/BR; CAN C+ D65 WT/LG, - D64 WT/LB; CAN B+ D55 WT/OR, - D54 WT | not verified | HIGH (2008) |
| ID format | 11-bit | 11-bit | MED |
| Termination | not verified | not verified | — |

## Diagnostic IDs and protocols (not verified this pass)
- OBD-II emissions requests go to PCM 0x7E0 and replies come from 0x7E8 (standard ISO 15765-4). HIGH confidence (generic).
- Chrysler enhanced diagnostics use proprietary 11-bit request/response pairs. One source shows a UDS session request sent to the BCM on 0x620 (`cansend can0 620#02100300...`), from https://magikh0e.pl/pubCarHacking/ODB-II_Diagnostic_Cable/ (DS/later-era context). The per-module ID map for the 2008 DR was **not found**. Do not hard-code IDs yet: sniff wiTECH/AlfaOBD traffic or read the AlfaOBD module definitions.
- KWP2000 vs UDS: common lore says 2008-era Chrysler CAN modules mostly use KWP2000-over-CAN (ISO 14230 services on ISO 15765 transport), while later modules moved to UDS. LOW confidence, not sourced here.
- StarSCAN/wiTECH/AlfaOBD: these tools reach the modules through diagnostic CAN C at 6/14, and the gateway routes requests to CAN-B/IHS modules. No primary documentation was retrieved for this.

## OBDLink MX+/EX STN commands (verify against the STN command reference before use)
- 6/14 at 500k, 11-bit: `ATSP6` or `STP 33` (ISO 15765, 11-bit, 500k). MED confidence.
- 3/11 (MS-CAN/IHS): the OBDLink MX+ supports a second CAN channel on pins 3/11. The EX is documented mainly for HS-CAN plus Ford MS-CAN; check its pin support. The exact STP number and how to set a custom 125k rate (STPBR / STCSEG-style commands) were **not verified**; consult the STN11xx/STN2xx Family Reference Manual (OBDSolutions). For the 2008 DR, 3/11 likely carries nothing.
- CAN B (83.3k fault-tolerant) is not reachable at the DLC on the 2008. Reach it through gateway requests on 6/14, or tap the bus directly with an ISO 11898-3 transceiver.

## Open items
STN protocol numbers; the 2008 module-by-bus list (the 8E/8W section "Communication > Description" should be fetched); termination locations; enhanced CAN IDs; the PCI-to-CAN cutover year for DR.
