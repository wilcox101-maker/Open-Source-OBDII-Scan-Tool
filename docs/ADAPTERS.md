# Adapter compatibility (OBDLink EX and MX+)

| | OBDLink EX (USB) | OBDLink MX+ (Bluetooth SPP) |
|---|---|---|
| Detection | STI `STN223x` or STDI contains "OBDLink EX". Dale's logs show `OBDLink EX r2.2.1 STN2231 v5.10.3` and later `v5.13.0` (FORScan composite string; raw STI/STDI not captured) | STI `STN2255`/`STN2256` or STDI contains "MX+" |
| Chip and MS-CAN | FRPM device table: STN2230/2231/2232 "OBDLink EX… with support for MS CAN" **[VERIFIED-DOC]** | STN2255/2256 "w/ support for MS CAN and SW CAN" **[VERIFIED-DOC]** |
| MS-CAN | STP 53 (ISO 15765, 11-bit, 125k). FRPM: the MS-CAN transceiver is "typically connected to pins 3 and 11". Electronic switch, no toggle | same |
| Host baud | Probes 115200 first (factory default per ScanTool support), then 2,000,000, 500000, 1000000, 230400, 38400 and 9600. 2 Mbps was seen in Dale's FORScan log, but only after FORScan's auto-increase | Nominal: SPP ignores the baud setting |
| Timeouts | 1.5 s per command, 3 s for ATZ **[UNVERIFIED estimate]** | 4 s per command, 6 s for ATZ **[UNVERIFIED estimate]** |
| J1850 | PWM: Yes (FORScan compatibility table). VPW: not used (Ford-only scope) | PWM and VPW listed |

Other STN adapters (for example the LX) and generic ELM327 clones get conservative profiles. MS-CAN requests on those throw `NotSupportedException`.

Port labels come from WMI `Win32_PnPEntity` on Windows: `BTHENUM\…` or a name containing "Bluetooth" is labelled BT; `FTDIBUS\…`, `USB\…` or a name containing "USB" is labelled USB. On other operating systems the label falls back to the plain port name.

Not implemented: switching the EX to a higher UART rate during a session (STBR/STSBR handshake). Read the FRPM before adding it.

## Adapters recommended in Livnitup's FORScan Tutorial (community, updated 6/14/26)
Source: https://docs.google.com/document/d/1-8dKaS_Spu4Zw4hV_CrKC4tLoP9G8yejqegF1wxIqxY/ (credit: Livnitup). Marked UNVERIFIED unless this app has tested it.

| Adapter | Link type | Driver / notes |
|---|---|---|
| **OBDLink EX** (USB, HS/MS auto-switch) | USB | Recommended. For 2021+ trucks the doc says to use the EX rather than other USB adapters. FTDI VCP driver (ftdichip.com CDM setup). Firmware updates: https://www.scantool.net/downloads/updates/obdlink_ex/ |
| **OHP "Ford ELMconfig"** USB (ELM327 with HS/MS toggle switch) | USB | FTDI driver. Set the switch to the correct position for HS- or MS-CAN modules; FORScan prompts when to toggle it. Installation guide by tmcolegr (Drive link in the doc). |
| **VINT ELM327** USB (switch-modded) | USB | CH340 driver (Win8/10). Has an HS/MS switch like the OHP. |
| **OBDLink MX+** | Bluetooth | Recommended, including for 2021+. **This app's primary adapter.** The LX does **not** support MS-CAN. If the MX+ reports "No ELM found", close OBDLink's OBDwiz or any other app holding the port. |
| **vLinker FS** | Bluetooth | Livnitup lists it for FORScan Lite (iOS/Android) only. Not tested here. |

Untested in this app: OHP, VINT and vLinker (UNVERIFIED). The EX and MX+ have profiles in `AdapterProfiles`.
