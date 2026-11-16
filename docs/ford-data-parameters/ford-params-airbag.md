# Ford data parameters: Airbag / Restraints (Airbag)

> **Status: reference only, UNVERIFIED.** Ford data-parameter mnemonics (the names scan tools show for live data) with the value range each one reports. These rows carry **no DID numbers, byte layouts or scaling**, so the app cannot read them by name; a parameter becomes readable only when a verified DID + formula is added to `src/ObdLink.Core/Data/ford-enhanced-pids.json` (see `docs/ENHANCED-UDS.md`). Many entries are from older powertrains (EVR, DPFE, TFI era) and may not exist on a 2017 F-150. Descriptions and notes are as supplied with the list (not checked against a vehicle).

21 parameters. Index: [README.md](README.md).

| Parameter | Range | Description | Shares entry with | Also under | Notes |
| --- | --- | --- | --- | --- | --- |
| `ABBRGR` | 0 to 25.5 Ω | Resistance of the airbag mounting bracket to chassis ground. | Bracket(Ω) |  |  |
| `Bracket(Ω)` | 0 to 25.5 Ω | Resistance of the airbag mounting bracket to chassis ground. | ABBRGR |  |  |
| `CRaSHSN1(Ω)` | 0 to 25.5 Ω | Resistance of the crash sensors. • CRaSHSN1 = the resistance of sensor number 1 • CRaSHSN2 = the resistance of sensor number 2 | CRSHSN1(Ω), CRaSHSN2(Ω), CRSHSN2(Ω) |  |  |
| `CRaSHSN2(Ω)` | 0 to 25.5 Ω | Resistance of the crash sensors. • CRaSHSN1 = the resistance of sensor number 1 • CRaSHSN2 = the resistance of sensor number 2 | CRaSHSN1(Ω), CRSHSN1(Ω), CRSHSN2(Ω) |  |  |
| `CRSHSN1(Ω)` | 0 to 25.5 Ω | Resistance of the crash sensors. • CRaSHSN1 = the resistance of sensor number 1 • CRaSHSN2 = the resistance of sensor number 2 | CRaSHSN1(Ω), CRaSHSN2(Ω), CRSHSN2(Ω) |  |  |
| `CRSHSN2(Ω)` | 0 to 25.5 Ω | Resistance of the crash sensors. • CRaSHSN1 = the resistance of sensor number 1 • CRaSHSN2 = the resistance of sensor number 2 | CRaSHSN1(Ω), CRSHSN1(Ω), CRaSHSN2(Ω) |  |  |
| `DSBELTR` | 0 to 25.5 Ω | Driver side seat belt buckle switch resistance. | PSBELTR |  |  |
| `DTC CouNT (AIRBAG)` | 0 to 20 | Number of diagnostic trouble codes (DTCS in the airbag module memory. |  |  | Description text may be missing a closing parenthesis. |
| `D_Airbag(Ω)` | 0 to 25.5 Ω | Resistance between the two contacts of the airbag. • “D” = driver side airbag • “P” = passenger side airbag | D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |  |
| `D_AirBAG2(Ω)` | 0 to 25.5 Ω | Resistance between the two contacts of the airbag. • “D” = driver side airbag • “P” = passenger side airbag | D_Airbag(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |  |
| `D_AirBAGLoop2(Ω)` | 0 to 25.5 Ω | Resistance between the two contacts of the airbag. • “D” = driver side airbag • “P” = passenger side airbag | D_Airbag(Ω), D_AirBAG2(Ω), P_Airbag(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |  |
| `D_Preten(Ω)` | 0 to 25.5 Ω | Resistance in the shoulder belt pre-tensioner circuit. • D_Preten refers to the driver side pre-tensioner. • P_Preten refers to the passenger side pre-tensioner. | D_PRETNR(Ω), P_Preten(Ω), P_PRETNR(Ω) |  |  |
| `D_PRETNR(Ω)` | 0 to 25.5 Ω | Resistance in the shoulder belt pre-tensioner circuit. • D_Preten refers to the driver side pre-tensioner. • P_Preten refers to the passenger side pre-tensioner. | D_Preten(Ω), P_Preten(Ω), P_PRETNR(Ω) |  |  |
| `EXTCRSH` | not available | Information on this External Crash Sensor Test Voltage parameter is not available at this time. |  |  |  |
| `PSBELTR` | 0 to 25.5 Ω | Driver side seat belt buckle switch resistance. | DSBELTR |  |  |
| `P_Airbag(Ω)` | 0 to 25.5 Ω | Resistance between the two contacts of the airbag. • “D” = driver side airbag • “P” = passenger side airbag | D_Airbag(Ω), D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_AirBAG2(Ω), P_AirBAGLoop2(Ω) |  |  |
| `P_AirBAG2(Ω)` | 0 to 25.5 Ω | Resistance between the two contacts of the airbag. • “D” = driver side airbag • “P” = passenger side airbag | D_Airbag(Ω), D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAGLoop2(Ω) |  |  |
| `P_AirBAGLoop2(Ω)` | 0 to 25.5 Ω | Resistance between the two contacts of the airbag. • “D” = driver side airbag • “P” = passenger side airbag | D_Airbag(Ω), D_AirBAG2(Ω), D_AirBAGLoop2(Ω), P_Airbag(Ω), P_AirBAG2(Ω) |  |  |
| `P_Preten(Ω)` | 0 to 25.5 Ω | Resistance in the shoulder belt pre-tensioner circuit. • D_Preten refers to the driver side pre-tensioner. • P_Preten refers to the passenger side pre-tensioner. | D_Preten(Ω), D_PRETNR(Ω), P_PRETNR(Ω) |  |  |
| `P_PRETNR(Ω)` | 0 to 25.5 Ω | Resistance in the shoulder belt pre-tensioner circuit. • D_Preten refers to the driver side pre-tensioner. • P_Preten refers to the passenger side pre-tensioner. | D_Preten(Ω), D_PRETNR(Ω), P_Preten(Ω) |  |  |
| `VBATT(V)` | 0 to 25 V | This parameter displays the battery voltage being supplied to the airbag electronic control module (ECM). |  |  |  |
