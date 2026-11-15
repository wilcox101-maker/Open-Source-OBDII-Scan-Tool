# Ford VCM II / VCM 3: hardware, technology and DIY potential

Research date: 2026-11-15. Sources are listed at the end. Anything marked **(verify)** isn't confirmed by a primary source.

## 1. Short answer
- **VCM II** is a small embedded-Linux computer (Bosch/ETAS "6516" family) built around a CPU plus an Altera FPGA, with NOR flash, a microSD card, USB and optional Wi-Fi. It isn't a simple USB-to-CAN bridge.
- **VCM 3** is its successor. It adds CAN-FD, DoIP (diagnostics over Ethernet), J1708/J1939, 802.11n Wi-Fi and a backup capacitor for low-voltage ride-through. Its internals aren't publicly documented.
- **Building one from scratch** is realistic as a **J2534 pass-thru device**. That's the route Ford supports for third-party hardware through FJDS and FDRS. A clone that impersonates a VCM so it works with IDS isn't a good idea: IDS only accepts Ford hardware, and passing yourself off as that hardware is exactly what the counterfeit-unit market does.
- **For the 2017 F-150** you only need HS-CAN (pins 6/14) and MS-CAN (pins 3/11). No CAN-FD, no DoIP and no J1850. A two-channel classic-CAN J2534 device covers the truck completely.

## 2. Ford's software and hardware rules
| Software | Hardware it accepts | Notes |
|---|---|---|
| **IDS** | Ford hardware only: VCM, VCM II, VCMM, VCM 3 | Main dealer tool for 1996–2017 vehicles. |
| **FJDS** (Ford J2534 Diagnostic Software) | Any J2534 device, including VCM II and VCMM | Diagnostics and programming on J2534 hardware. |
| **FDRS** | VCM II, VCMM, VCM 3 and J2534 devices | Cloud-based; the main tool for 2018+ vehicles. Licensing is shared with IDS/FJDS. |

- Ford's licensing page states this split directly: IDS needs Ford hardware, while FJDS and FDRS work with "compatible J2534 devices."
- FDRS has an "Other Device" option in its device manager. A cheap VNCI J2534 box has been reported working there for online programming. **Third-party J2534 devices are not locked out of FDRS.** (Reliability for flashing is a separate question.)
- **For your truck:** a 2017 sits at the IDS/FDRS boundary. Shops report FDRS covering diagnosis and programming from roughly 2017 onward, but that's secondhand; Ford's per-module coverage tables are the authority. Short licenses exist. One forum post quotes a 2-day IDS license at about $150 and a 2-day FJDS license at about $50 **(verify current prices on motorcraftservice.com)**.
- **MS-CAN programming:** there are forum reports that Ford won't program MS-CAN modules through some aftermarket J2534 hardware, and that some Mongoose units can't do MS-CAN in FORScan. Treat MS-CAN flashing through a DIY device as unproven.

## 3. VCM II internals
| Item | What's known | Source quality |
|---|---|---|
| Maker / family | Bosch (earlier Vetronix lineage); genuine units are the ETAS/Bosch **6516** family | Bosch flyer, VCM II manual, jakka351 notes |
| OS | **Embedded Linux.** Boot chain fsboot → U-Boot → kernel. RAM-disk rootfs (`root=/dev/ram`, `init=/linuxrc`), BusyBox, bash 3.2 | jakka351/Ford-VCM2 (community RE) |
| FPGA | **Altera** FPGA, configured from an EPCS4 serial configuration flash. A 640 KB FPGA bitstream partition sits in NOR flash | MHH repair thread + jakka351 MTD table |
| Storage | NOR flash in 12 MTD partitions: two kernel/initrd pairs (production + recovery), FPGA, JFFS2 config, serial/MAC region. The microSD card holds data and config and is paired to the unit's serial | jakka351 |
| CPU | A separate processor next to the FPGA; **part number not published** | MHH thread mentions it without naming it |
| PC link | USB (needed for firmware updates and Customer Flight Recorder setup); optional Wi-Fi card. In test mode it serves HTTP and telnet on 192.168.171.2 | Hardware manual, jakka351 |
| Serial console | `ttyS0` at 38400 8N1 on the internal HDL26 connector, pins 24/25 | jakka351 |
| Recovery | Recovery tab under the rubber boot. Hold it while plugging in USB, then run "Recover" in VCI Manager | VCM II hardware manual, jakka351 |
| Extras | Customer Flight Recorder (in-vehicle logging), Vehicle Measurement Module support, SmartPower management, rated −20 °C to 70 °C | Bosch flyer, manual |
| Protocols | HS-CAN, MS-CAN, J1850 PWM (Ford SCP), ISO 9141/K-line, and **UBP** (an old single-wire Ford protocol on pin 3). FORScan has said VCM II is the only J2534 adapter they've seen that supports UBP | FORScan forum |

**Why the FPGA matters:** the genuine unit does its bus timing (J1850 PWM bit-timing, UBP, K-line, pin switching) in programmable logic. That's why good clones are a two-board, FPGA-based copy of the original. The cheap single-board ones are something else: a VXDIAG VCX Nano teardown shows a **single STM32F4 (Cortex-M4)** emulating the VCM in firmware. Forum reports blame those single-chip clones for bricked modules **(anecdotal)**.

## 4. VCM 3
- **Adds:** CAN-FD, DoIP, J1708 and J1939 (VCM II and VCMM can't do those last two), 802.11n Wi-Fi, a backup capacitor for brown-outs, and a USB link for setup and firmware. It works with IDS, FDRS and FJDS. Rotunda part number **164-R9865**.
- **Internals:** no teardown or firmware analysis found. CAN-FD plus DoIP implies a more capable SoC with an Ethernet PHY **(verify)**.
- **Price:** genuine units list around $1,800–$2,200. Clones ("VNCI VCM3") sell for about $120–$300; they're of unknown quality and depend on licensing tricks.
- **DoIP on the OBD port (ISO 13400-4):**
  - Option 1: pins 3/11 = RX, 12/13 = TX, pin 8 = activation.
  - Option 2: pins 1/9 = RX, 12/13 = TX, pin 8 = activation.
  - The tester senses the resistance between pin 8 and pin 5 to tell which layout is in use, then applies +5 V to pin 8 to switch on the Ethernet port.
  - **Option 1 shares pins 3/11 with Ford MS-CAN**, so a DoIP-capable design needs pin switching.
- **Ford DoIP vehicles:** Geotab lists the Explorer EV (2024+) and Capri EV (2025+) as needing an Ethernet bypass, so DoIP is arriving on newer Ford platforms. **Not relevant to a 2017.**

## 5. The J2534 standard (the legal interface to target)
- **API functions:** PassThruOpen, PassThruClose, PassThruConnect, PassThruDisconnect, PassThruReadMsgs, PassThruWriteMsgs, PassThruStartPeriodicMsg, PassThruStartMsgFilter, PassThruSetProgrammingVoltage, PassThruReadVersion, PassThruIoctl.
- **What the device actually is:** a Windows DLL registered in the registry, plus a device on USB, Bluetooth or network.
- **J2534-1** requires the core protocols: J1850 VPW/PWM, CAN, ISO 15765, ISO 9141, ISO 14230 and Chrysler SCI.
- **J2534-2** (optional) adds the protocols Ford needs beyond the basics, among others: **Ford MS-CAN**, single-wire CAN, J1939, J1708 and pin selection. Few adapters fully implement J2534-2.
- **API versions:**
  - **v04.04** (2004 base) is what nearly all OEM software calls.
  - **v05.00** (2015 base, re-issued 2022) adds device discovery, logical channels and larger messages.
  - CAN-FD arrived through later updates; Kvaser notes the 2015 base doesn't include CAN-FD **(verify which version Ford FDRS uses)**.
- **Bitness:** Windows OEM apps generally load a **32-bit** DLL. Few vendors ship 64-bit J2534 drivers.

## 6. DIY: building your own Ford-capable interface

### 6a. Recommended target ("Dale-VCI", stage 1)
A **J2534 v04.04 device** with:
- 2× classic CAN: HS-CAN on 6/14 and MS-CAN on 3/11, each switchable to 125/250/500 kbps.
- ISO 15765 (ISO-TP) handled in firmware.
- Hardware message filters and periodic messages.
- A 32-bit Windows DLL talking to the device over USB (WinUSB or CDC).

That covers every module on your truck. It works with FORScan (which supports J2534), FJDS/FDRS (selected as "Other Device") and your own scan tool.

### 6b. Hardware blocks
| Block | Suggested parts | Notes |
|---|---|---|
| MCU | **STM32G4** (3× FDCAN, classic + FD) or **STM32H5/H7** (FDCAN + Ethernet MAC for later DoIP) | STM32F4 is what the VXDIAG clone uses. FDCAN parts leave room for CAN-FD later. |
| CAN PHYs | TCAN1044 / TJA1443 / TJA1462 (CAN-FD capable), one per channel | Add switchable 120 Ω termination and common-mode chokes. |
| Pin switching | Analog switches or small signal relays routing channel 2 to 3/11 (MS-CAN) or 12/13 | Needed for J2534-2 pin selection and any future DoIP option 1. |
| K-line (optional) | L9637 or a discrete transistor design | Only for pre-CAN Fords. |
| J1850 PWM / SCP (optional) | Discrete differential driver, timed by MCU capture/compare or a small FPGA/CPLD | Pre-2008 Fords only. This is the part where the genuine VCM leans on its FPGA. |
| Programming voltage (optional) | Boost converter plus a switch to the J1962 pin | J2534 `PassThruSetProgrammingVoltage`; older Ford PCMs used a flash-programming supply (FEPS) **(verify which pin)**. Not needed for 2017 CAN modules. |
| Power | Reverse-polarity protection, load-dump TVS, a buck converter from 6–36 V, a supercap or big bulk cap for brown-out ride-through (as VCM 3 does) | Brown-outs during a flash are the real brick risk. |
| PC link | USB full speed (WinUSB) first; Wi-Fi or BLE later (an ESP32 co-processor) | Keep flashing on wired USB. |
| ESD / EMC | TVS on all bus lines, a proper ground split | — |

### 6c. Software stack
1. **Firmware.** Channel manager, ISO-TP engine (block size, STmin, wait frames), filters (pass, block and flow-control filters), periodic messages, timestamps, and a USB command protocol.
2. **J2534 DLL** (C/C++, 32-bit plus an optional 64-bit build). Export the v04.04 functions and register under `HKLM\SOFTWARE\WOW6432Node\PassThruSupport.04.04\<Vendor>` **(verify the key path)**.
3. **Validation.** Test with:
   - the Drew Tech/Opus "J2534 Toolbox"-style tester, or its open clone
   - jakka351's GenericDiagnosticTool
   - FORScan in J2534 mode
   - your own scan tool via a new J2534 backend
4. **Reuse instead of starting cold.** Existing open work:
   - the Macchina M2 J2534 driver and OpenVehicleDiag (Rust; also does SocketCAN)
   - oj2534 firmware
   - SardineCAN (Arduino) firmware and DLL
   - an STM32F4 J2534 + gs_usb project
   - Linux J2534 drivers (dschultzca, james-portman)
   - jakka351's OpenJ2534 index

### 6d. Path and risk
| Stage | Capability | Risk |
|---|---|---|
| 0 | Bench harness with junkyard modules | None |
| 1 | DIY J2534, read/write: DTCs, DIDs, as-built reads in FORScan and your app | Low |
| 2 | As-built/config writes on bench modules, then the truck | Medium. Back up first. |
| 3 | FJDS/FDRS diagnostics through "Other Device" | Low–medium |
| 4 | **Module flashing** through FJDS/FDRS on DIY hardware | **High / brick risk.** Use a genuine VCM II/VCMM or a proven Mongoose-class device until the DIY unit has passed many bench flashes. |
| 5 | CAN-FD + DoIP (VCM 3 class) | Only matters for newer Fords. |

## 7. Tie-ins to the Open-Source-OBDII-Scan-Tool project
- **Add a J2534 transport** next to the STN/ELM transport.
  - The app could then drive genuine VCM II/VCMM units, Mongoose devices and the DIY device. All of them do MS-CAN switching through J2534-2 pin selection instead of STN commands.
  - The existing `BusRouter` (PR #13) is the natural seam for this.
- **Your OBDLink EX logs** give real baselines for timing the DIY device: min delay 14–15 ms, 2 Mbps link.

## Sources
- Ford/Motorcraft licensing page (IDS/FJDS/FDRS hardware rules): https://www.fordtechservice.dealerconnection.com/vdirs/wds/vcm_Retail_renewal.asp
- Bosch VCM II flyer: https://otctools.com/sites/default/files/13-149_revc_ford_vcm_ii_flyer_0.pdf
- VCM II hardware manual: https://motorstate.com.ua/upload/product-manual/VCMII_IDS_Hardware%20Manual_ENG.pdf
- jakka351/Ford-VCM2 (internals, MTD, boot chain): https://github.com/jakka351/Ford-VCM2
- MHH Auto VCM2 recovery thread (Altera FPGA / EPCS4): https://mhhauto.com/Thread-VCM2-VCM-II-Recovery-Procedure-For-genuine-and-clone?page=2
- VXDIAG Nano STM32F4 teardown note: http://ford-vcm-2.blogspot.com/2017/
- Clone board differences: http://blog.obdexpress.co.uk/2017/01/28/vcm-2-clone-review/
- VCM 3 product notes (protocols, Wi-Fi): https://platform.softwareone.com/product/ford-vcm3/PCP-5102-2226 ; https://www.triad-ds.com/product/ford-vcm3/ ; https://uk.fridayparts.com/vcm3-module-3-diagnostic-tool-scanner-with-drivers-communication-diesel-scan-for-ford-lincoln-mercury-vehicle-latest-version
- diag.net: J2534 with IDS/FJDS/FDRS, VCM 3 J1708/J1939: https://diag.net/discussion/szks1atgjjfep5gf3ikx25ookb
- FDRS "Other Device" (VNCI): https://blog.obdii365.com/2023/11/23/set-up-vnci-mf-j2534-with-ford-fdrs/
- MS-CAN via aftermarket J2534: https://www.bobistheoilguy.com/forums/ubbthreads.php/topics/5162312/ford-techs-vcmii-and-ids-fjds-programming-ms-can-modules
- FORScan J2534 support / UBP note: https://forum.forscan.org/viewtopic.php?p=25589
- J2534 overview: https://kvaser.com/about-can/can-standards/j2534/ ; https://kvaser.com/?p=32251 ; https://dap4cs.com/j2534
- DoIP OBD pinouts: https://python-doipclient.readthedocs.io/en/latest/automotive_ethernet.html ; https://installguides.geotab.com/Wiki/DoIP_Ethernet_Bypass_for_HRN-GS16K22-A
- Open J2534 projects: https://github.com/jakka351/OpenJ2534 ; https://github.com/rnd-ash/OpenVehicleDiag ; https://github.com/macchina/m2-hardware
