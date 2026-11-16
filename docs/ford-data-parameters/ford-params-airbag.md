# Ford data parameters: Airbag / Restraints (Airbag)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions are not published here (source text, see the index).

21 parameters. Index: [README.md](README.md).

| Parameter | Range | Shares entry with | Also under |
| --- | --- | --- | --- |
| `ABBRGR` | 0 to 25.5 Ω | Bracket(Ω) |  |
| `Bracket(Ω)` | 0 to 25.5 Ω | ABBRGR |  |
| `CRaSHSN1(Ω)` | 0 to 25.5 Ω | CRSHSN1(Ω), CRaSHSN2(Ω), CRSHSN2(Ω) |  |
| `CRaSHSN2(Ω)` | 0 to 25.5 Ω | CRaSHSN1(Ω), CRSHSN1(Ω), CRSHSN2(Ω) |  |
| `CRSHSN1(Ω)` | 0 to 25.5 Ω | CRaSHSN1(Ω), CRaSHSN2(Ω), CRSHSN2(Ω) |  |
| `CRSHSN2(Ω)` | 0 to 25.5 Ω | CRaSHSN1(Ω), CRSHSN1(Ω), CRaSHSN2(Ω) |  |
| `DSBELTR` | 0 to 25.5 Ω | PSBELTR |  |
| `DTC CouNT (AIRBAG)` | 0 to 20 |  |  |
| `D_Airbag(Ω)` | 0 to 25.5 Ω | D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |
| `D_AirBAG2(Ω)` | 0 to 25.5 Ω | D_Airbag(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |
| `D_AirBAGLoop2(Ω)` | 0 to 25.5 Ω | D_Airbag(Ω), D_AirBAG2(Ω), P_Airbag(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |
| `D_Preten(Ω)` | 0 to 25.5 Ω | D_PRETNR(Ω), P_Preten(Ω), P_PRETNR(Ω) |  |
| `D_PRETNR(Ω)` | 0 to 25.5 Ω | D_Preten(Ω), P_Preten(Ω), P_PRETNR(Ω) |  |
| `EXTCRSH` | not available |  |  |
| `PSBELTR` | 0 to 25.5 Ω | DSBELTR |  |
| `P_Airbag(Ω)` | 0 to 25.5 Ω | D_Airbag(Ω), D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |
| `P_AirBAG2(Ω)` | 0 to 25.5 Ω | D_Airbag(Ω), D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAGLoop2(Ω) |  |
| `P_AirBAGLoop2(Ω)` | 0 to 25.5 Ω | D_Airbag(Ω), D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAG2(Ω) |  |
| `P_Preten(Ω)` | 0 to 25.5 Ω | D_Preten(Ω), D_PRETNR(Ω), P_PRETNR(Ω) |  |
| `P_PRETNR(Ω)` | 0 to 25.5 Ω | D_Preten(Ω), D_PRETNR(Ω), P_Preten(Ω) |  |
| `VBATT(V)` | 0 to 25 V |  |  |
