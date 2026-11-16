# Ford data parameters: sensor reference tables

> **Status: reference only, UNVERIFIED** (values as supplied with the parameter list; typical readings, not limits for a specific vehicle). Index: [README.md](README.md).

## Normal AP sensor voltage readings  (AP(V), APP1(V), APP2(V), APP3(V))

| SENSOR | CLOSED THROTTLE | WIDE OPEN THROTTLE |
| --- | --- | --- |
| APP1 | 3.9–4.2 V | 0.7–1.2 V |
| APP2 | 1.4–1.6 V | 3.6–4.1 V |
| APP3 | 0.8–1.1 V | 3.1–3.5 V |

## Outputs for DPFE sensors with 0.55 V and 1.0 V offsets  (DPFE(V))

| 0.55 V OFFSET |  |  | 1.0 V OFFSET |  |  |
| --- | --- | --- | --- | --- | --- |
| inHg | kPa | Volts | inHg | kPa | Volts |
| 8.83 | 29.81 | 4.66 | 8.56 | 28.9 | 4.95 |
| 6.62 | 22.36 | 3.64 | 4.3 | 14.4 | 2.97 |
| 4.41 | 14.9 | 2.61 | 0 | 0 | 1 |
| 2.21 | 7.46 | 1.58 |  |  |  |
| 0 | 0 | 0.55 |  |  |  |

## Outputs for exhaust backpressure sensor  (EBP(V))

| VOLTS | psi | kPa |
| --- | --- | --- |
| 0 | 0 | 0 |
| 0.25 | 8 | 55.1 |
| 1.45 | 20 | 137 |
| 4.45 | 50 | 344 |
| 4.75 | 53 | 365 |
| 5 | 60 | 413 |

## EVR(%) readings  (EVR(%))

| EVR(%) | inHg RANGE | kPa RANGE |
| --- | --- | --- |
| 0% | 0 to 0.75 | 0 to 2.53 |
| 33% | 0.55 to 2.05 | 1.86 to 6.9 |
| 90% | 5.69 to 6.95 | 19.2 to 23.47 |

## IAC(%) and IAC=IDLE AIR(%) readings  (IAC(%), IAC=IDLE AIR(%))

| PERCENTAGE READING | IAC VALVE VOLTAGE | IAC VALVE AMPERAGE |
| --- | --- | --- |
| 10% | 1 V | 0.1 A |
| 50% | 5 V | 0.5 A |
| 80% | 8 V | 0.8 A |
| 100% | 10–12 V | 1.0 A |

## PFE sensor outputs  (PFE(V))

| inHg | kPa | VOLTS |
| --- | --- | --- |
| 3.7 | 12.5 | 4.75 |
| 2.79 | 9.42 | 4.38 |
| 1.85 | 6.25 | 4 |
| 0.94 | 3.17 | 3.82 |
| 0 | 0 | 3.25 |
| -5.03 | -17 | 1.22 |
| -7.4 | -25 | 0.25 |

## S/C VAC SW and S/C VENT SW readings and throttle position  (SC VAC SW, SC VENT SW)

| SC VAC SW | SC VENT SW | THROTTLE |
| --- | --- | --- |
| ON | OFF | Accelerate |
| OFF | ON | Decelerate |
| OFF | OFF | Steady |

## Sample TR_V readings  (TR_V (PCM))

| TR_V READING | GEAR |
| --- | --- |
| 0.0 V | PARK |
| 1.5 V | REVERSE |
| 1.5 V | NEUTRAL |
| 1.5 V | ODRIVE |
| 0.0 V | MAN2 |
| 0.0 V | MAN1 |

