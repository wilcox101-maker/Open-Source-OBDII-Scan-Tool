# OpenDiag-MX: Technical Design Plan

Open-source Windows diagnostic app for the **OBDLink MX+** (STN2255 / STN2256 chipset). Target vehicles: Ford / Lincoln / Mercury, and Chrysler / Dodge / Jeep / Ram (FCA / Stellantis).

**Status:** draft v0.1 (2026-10-22, America/New_York).  
Anything marked **[UNKNOWN]** must be confirmed on a real vehicle or against a published public spec before implementation. Do not invent protocol bytes.

> **Scope / legal note.** This is a clean-room reimplementation of *functionality* built only from public standards, the public OBDLink Family Reference and Programming Manual (FRPM), Motorcraft As-Built documentation, SAE/ISO standards summaries, and community-published data. We will not copy, decompile, or redistribute FORScan or AlfaOBD binaries, assets, or proprietary databases. We will not ship proprietary OEM security algorithms or secret keys. We will not implement any hardware or software bypass of the FCA/Stellantis Security Gateway (SGW); write access on 2018+ FCA vehicles requires legitimate AutoAuth / OEM-authorized tooling [S10][S11].

---

## 1. Recommended stack

| Layer | Choice | Reasoning |
|---|---|---|
| Runtime | **C# / .NET 8 LTS** (plan to move to .NET 10 LTS when stable) | First-class `System.IO.Ports` for Bluetooth SPP virtual COM ports on Windows; strong async/`CancellationToken` story; single-file self-contained publish; excellent test tooling. |
| UI | **WPF + CommunityToolkit.Mvvm** | Mature, high-density DataGrid / TreeView for modules & As-Built lines, no MSIX packaging tax. Fallback / future port: **Avalonia** for Linux/macOS. WinUI 3 deferred due to packaging/deployment friction for a hobbyist installer. |
| Storage | **SQLite** (`Microsoft.Data.Sqlite`) | Local DTC definitions, module address tables, session backups, community-DB cache. |
| Community DB | **YAML (+ JSON Schema) in a Git repo** | Diffable PRs, signed releases, offline clone. Optional later: SQLite export for fast search. |
| Bluetooth | Paired Classic SPP → COM port (MVP). Optional later: `Windows.Devices.Bluetooth.Rfcomm` or 32feet.NET | Matches how OBDLink documents host use; simplest path on Windows. |
| Logging | Serilog → rolling session files (anonymized VIN on export) | Needed for support, replay tests, and safety audits of writes. |
| Tests | xUnit + **ReplayAdapter** that feeds captured AT/ST/OBD traces | Most of the stack can be tested without a car or adapter. |

**Alternatives considered**

- **Python + PySide6 / Qt:** fastest prototyping (see public `fdiag` [S5]); weaker single-exe Windows distribution and COM-port reliability under load.
- **Rust + egui / Tauri:** memory-safe, but thinner Windows serial/BT ecosystem for a volunteer project; higher ramp-up cost.
- **Electron / web UI:** rejected for a tool that must stay snappy while streaming bus traffic and must not drag Chromium into a garage laptop.

**License intent (recommendation):** GPL-3.0-or-later for the app (discourages closed forks that hide write tooling), MIT/Apache-2.0 for pure protocol libraries that others can reuse.

---

## 2. Solution architecture

```
UI (WPF, MVVM)
 └─ Application services
     SessionService, ModuleScanner, DtcService,
     ConfigService (As-Built / Proxi), BackupService, SafetyGuard
     └─ Protocol clients
         UdsClient (ISO 14229), Kwp2000Client (ISO 14230),
         ObdiiClient (SAE J1979)
         └─ Transport
             IsoTp (adapter-side CAF/segmentation or host-side)
             ElmStnLink (AT / ST command framing)
             ByteStream (SerialPort | Rfcomm | ReplayFile)
Data stores: SQLite + community YAML sync
```

Every request/response is logged with monotonic timestamp, bus (HS/MS), CAN ID, SID, and NRC. Exported bug reports strip or hash VIN and plate fields.

---

## 3. Adapter transport layer (OBDLink MX+ / STN2xx)

**Primary references:** OBDLink FRPM Rev E [S1]; MX protocol command sheet [S2]; STN21xx datasheet [S3].

### 3.1 Link

1. User holds the MX+ pairing button; Windows creates an outgoing SPP COM port.
2. App enumerates ports via WMI (`Win32_PnPEntity` / `Win32_SerialPort`) and prefers devices whose name contains `OBDLink MX+` (Bluetooth SPP UUID `00001101-0000-1000-8000-00805F9B34FB`).
3. Open `SerialPort`: 8N1. Baud is largely nominal over Classic BT SPP (adapter UART baud cannot be changed the same way as USB STN devices [S1 §6]). **[UNKNOWN]** Measure real request/response latency and throughput on a specific Windows Bluetooth stack (Intel vs Realtek) — expect tens of ms of jitter.
4. Framing: write ASCII + CR (`0x0D`); read until `>` prompt. Handle `?`, `NO DATA`, `CAN ERROR`, `BUFFER FULL`, `STOPPED`, `<DATA ERROR`, `UNABLE TO CONNECT`, `BUS BUSY`, `OUT OF MEMORY` [S1 §9].
5. Concurrency: single writer queue; cancel in-flight OBD request by sending any character (adapter prints `STOPPED`).

### 3.2 Init sequence (draft)

```
ATZ | ATWS
ATE0
ATL0
ATS0
ATH1
ATAL          ; allow >7-byte payloads where needed
ATCAF1        ; ISO-TP PCI auto-format ON for simple UDS (or OFF + host ISO-TP)
STI / STDI / STDIX / STSN   ; firmware, hardware, serial
STVR          ; battery voltage for SafetyGuard
```

Prefer `ST*` equivalents over deprecated `AT*` where both exist (`STVR` vs `ATRV`, `STP` vs `ATSP`, `STPTO` vs `ATST`) [S1].

### 3.3 Protocol selection (`STP` presets)

From FRPM [S1][S2]:

| Bus / mode | Preset | Notes |
|---|---|---|
| HS-CAN ISO 11898 raw, 11-bit, 500k, var DLC | `STP 31` | Raw frames |
| HS-CAN ISO 15765, 11-bit, 500k, DLC=8 | `STP 33` | Default Ford / modern OBD |
| HS-CAN ISO 15765, 29-bit, 500k | `STP 34` | Some FCA / extended addressing |
| HS-CAN ISO 15765, 11-bit, 250k | `STP 35` | Less common light-duty |
| **MS-CAN** ISO 15765, 11-bit, 125k, DLC=8 | **`STP 53`** | Ford Medium-Speed CAN on OBD pins **3 & 11** |
| MS-CAN ISO 15765, 29-bit, 125k | `STP 54` | |
| MS-CAN raw ISO 11898, 11-bit, 125k | `STP 51` | |
| SW-CAN (GM) | `STP 61`–`64` | Out of scope for v1 |
| J1850 PWM / VPW | `STP 11` / `12` | Legacy Ford / Chrysler (Phase 4) |
| ISO 9141 / KWP2000 | `STP 21`–`25` | Legacy (Phase 4) |

- `STPO` / `STPC` open and close the protocol. `STPBR baud` / `STPBRR` change and report baud [S1].
- **Important hardware constraint:** STN maps a *single* CAN controller onto HS / MS / SW pins — only one CAN channel is active at a time [S1 §8.6]. Switching HS↔MS requires `STP` (+ optional `STPC`/`STPO`).

### 3.4 Arbitrary frames: `STPX`

Preferred send path for non-legislated traffic [S1]:

```
STPX h:<CAN_ID>,d:<HEX_DATA>[,t:<timeout_ms>][,r:<response_count>][,l:<len>]
```

Examples (illustrative of syntax only):

```
STPX h:7E0,d:22F190,t:100,r:1
STPX h:726,d:22DE00,t:200,r:1
```

- `l:` length mode prompts `DATA>` for payloads larger than the UART buffer (MX+ max message size **4 KB** [S1]).
- With `ATCAF1` + ISO 15765 presets, adapter can assemble/disassemble multi-frame ISO-TP (`STCSEGT` / `STCSEGR`). For full control, set `ATCAF0`, implement host-side ISO-TP, and use flow-control address pairs via `STCFCPA tx,rx` [S1 §8.8 / §12].

### 3.5 MS-CAN / HM-CAN notes

- MX+ MS-CAN transceiver is wired to DLC pins 3/11 (Ford MSC) [S1][S3].
- Ford **HM-CAN / I-CAN** (infotainment) often sits behind a gateway; access from the OBD port is vehicle-specific. **[UNKNOWN]** Per-platform whether APIM/ACM are reachable on MS-CAN @ 125k, on a remapped 500k MS pinout, or only via gateway on HS-CAN. Scanner strategy: try `STP 53`, then `STPBR 500000` on MS, log which modules answer, never assume a single map.

### 3.6 Voltage / safety hook

`STVR` returns calibrated battery voltage (e.g. `12.34`) [S1]. SafetyGuard polls this before any write and aborts if below a configurable threshold (default **12.0 V** engine-off / **13.2 V** recommended with charger — user-configurable).

---

## 4. ISO-TP (ISO 15765-2) and addressing

UDS/KWP enhanced diagnostics ride on ISO-TP over CAN.

### 4.1 Two operating modes

| Mode | Adapter settings | When to use |
|---|---|---|
| **A – Adapter ISO-TP** | `STP 33/53`, `ATCAF1`, `ATCFC1`, `ATH1` | MVP: VIN, DTCs, single-frame & adapter-segmented multi-frame DIDs |
| **B – Host ISO-TP** | `STP 31/51` (raw) or `ATCAF0` + `STCSEGT0`/`STCSEGR0`, own FF/CF/FC | Complex multi-DID, non-standard addressing, research captures |

Implement Mode A first; keep a Mode B ISO-TP state machine (SF / FF / CF / FC, BS, STmin) behind an interface so tests can run either path.

### 4.2 Addressing

- **Functional (broadcast):** e.g. `0x7DF` (11-bit) for legislated OBD; OEM functional IDs vary. **[UNKNOWN]** Ford/FCA functional IDs for enhanced diagnostics beyond OBD — confirm per platform.
- **Physical:** request ID N → response typically N+8 for many 11-bit HS layouts (e.g. `7E0`/`7E8`), but **OEM maps are not universal**. Always drive from a per-vehicle module table, not hardcoded +8 logic alone.
- Use `ATSH` / `STPX h:` for Tx ID; `STCFCPA` to register FC pairs; filters (`STFPA` / `STFAC`) to reduce BT traffic [S1].

---

## 5. UDS (ISO 14229) and KWP2000 (ISO 14230)

### 5.1 UDS services for this project

| SID | Name | MVP | Notes |
|---|---|---|---|
| `0x10` | DiagnosticSessionControl | Yes | Default `01`, extended `03`; programming `02` later / gated |
| `0x11` | ECUReset | Later | Soft/key-off; warn user |
| `0x14` | ClearDiagnosticInformation | Yes | Confirm dialog + SGW check on FCA |
| `0x19` | ReadDTCInformation | Yes | Sub-functions `02`, `0A`, etc. |
| `0x22` | ReadDataByIdentifier | Yes | VIN `F190`, As-Built DIDs, part numbers |
| `0x2E` | WriteDataByIdentifier | Phase 2+ | Only after backup + SafetyGuard |
| `0x27` | SecurityAccess | Phase 2+ | See §7 — public info only |
| `0x3E` | TesterPresent | Yes | Keep-alive while session open |
| `0x31` | RoutineControl | Phase 3 | Dangerous; per-routine allow-list |
| `0x34/36/37` | Download / Transfer | **Out of scope** | No firmware flashing in v1 |

Negative responses: `0x7F SID NRC`. Surface common NRCs (`0x11` serviceNotSupported, `0x33` securityAccessDenied, `0x78` responsePending, `0x22` conditionsNotCorrect, …) with human text [S8][S9].

### 5.2 KWP2000

Still present on older Ford/Chrysler ECUs (K-line and some CAN-wrapped variants). Reuse session / read / clear patterns with KWP SIDs where they differ. Phase 4 priority after CAN-UDS platforms are solid.

### 5.3 Legislated OBD-II (J1979) — always available

Mode `$01` PIDs, `$02` freeze frame, `$03/$07/$0A` DTCs, `$04` clear, `$09` VIN (`0902`) — useful for adapter bring-up before enhanced addressing works.

---

## 6. Ford module addressing (HS-CAN / MS-CAN / HM-CAN)

### 6.1 Concept

Ford vehicles expose dozens of modules (PCM, BCM, ABS, IPC, APIM, ACM, GWM, …) on HS-CAN and/or MS-CAN. Each entry in our DB needs:

```yaml
id: APIM
name: Accessory Protocol Interface Module
buses: [MS-CAN]          # and/or HS-CAN
request_id: 0x7D0        # example commonly cited for APIM As-Built UI lines
response_id: 0x7D8       # [UNKNOWN] confirm per year/platform
protocol: UDS
asbuilt_dids: [0xDE00, 0xDE01, ...]   # where publicly documented
notes: "Sync APIM; bitfields documented by CyanLabs community [S6]"
```

**Do not ship a guessed global address map.** Seed the DB from:

1. Public Motorcraft As-Built downloads (module address appears in AB line prefixes, e.g. `7D0-01-01 …`) [S4].
2. Community-curated tables with citations.
3. Live discovery: probe known candidate IDs with `0x22 F190` / `0x22 F18C` / `0x22 F187` and record positive responders per VIN prefix / model year.

### 6.2 Bus strategy during a scan

1. HS-CAN `@ STP 33` — OBD functional + PCM/physical candidates.
2. Switch to MS-CAN `@ STP 53` — body / interior / Sync-class modules.
3. Optional MS @ 500k experiment (logged) for platforms that remap.
4. Persist discovered `{bus, req, rsp, partNumber, asBuiltPresent}` into the session + optional contribution to community DB.

### 6.3 HM-CAN

Treat as **[UNKNOWN]** until proven on user vehicles. UI should show “not reachable from OBD on this platform” rather than inventing pinouts. Hardware pin adapters / wire mods are out of app scope (document only).

---

## 7. Ford As-Built data (read / write)

### 7.1 What As-Built is

Ford factory module configuration. Motorcraft publishes per-VIN As-Built (HTML / `.AB` XML). FORScan’s public forum describes the line format [S4]:

```
<module>-<block>-<line> <D1D2> <D3D4> <D5Cs>
```

Example shape: `7D0-01-01 0A6A 0196 5034` where the final byte is a checksum.

**Important distinction [S4]:** “As-Built” = factory data. After any change it is simply “module configuration.” Our UI will label factory import vs current live values vs pending edits.

### 7.2 On-vehicle DIDs

Community tooling treats blocks such as **DE00 / DE01 / …** as UDS DIDs that back APIM (and other) configuration, often displayed as `7D0-01-01` style lines [S6][S7]. Exact DID ↔ block mapping can vary by module and generation — **[UNKNOWN]** for non-APIM modules until verified.

**Read path (MVP):**

1. Enter extended session (`10 03`) if required.
2. `22 DExx` (or module-specific DID list from DB).
3. Parse bytes into AB lines for display; compute/verify checksum.
4. Also offer import of Motorcraft `.AB` / HTML for offline compare.

**Write path (Phase 2):**

1. Mandatory backup (§10).
2. SafetyGuard voltage + ignition-state checks.
3. SecurityAccess if NRC `0x33` / session requires it (§8).
4. `2E DExx` + data.
5. Read-back verify; checksum recompute; show diff.

### 7.3 Checksum (community-documented)

Public forum write-up for AB line checksum [S12]:

1. Take address + data **excluding** the final checksum byte, as hex pairs.  
   Example: line `7D0-01-01 0000 0020 0018` → bytes from `07 D0 01 01 00 00 00 20 00`.
2. Sum all bytes.
3. `checksum = sum mod 256`.
4. Example: sum `280` → `280 mod 256 = 24` → `0x18`.

App must recalculate on every edit (FORScan does this automatically per their public how-tos [S13]). Treat other modules’ checksum variants as **[UNKNOWN]** until tested — never silently write a line that fails local verification.

### 7.4 Bitfield decode

CyanLabs As-Built databases and `apim-asbuilt-decode` show community bit-level meaning for Sync APIM DE00+ fields [S6][S7]. We will:

- Store decode maps as versioned YAML in the community repo.
- Show raw hex always; show decoded fields only when a map exists for that module/block/year.
- Never block a backup because decode is missing.

---

## 8. Ford security access (0x27) — public info & policy

### 8.1 Public mechanism (standard UDS)

1. Request seed: `27 xx` (odd sub-function, often `01` / `03`).
2. ECU returns seed in `67 xx <seed bytes>`.
3. Host computes key with an OEM-/module-/level-specific algorithm.
4. Send key: `27 xx+1 <key>`.
5. Success → `67 xx+1`; failure → NRC (lockout possible) [S8][S9].

Algorithms and secret constants are **proprietary** and vary by module, year, and security level. Some older platforms have community-discussed examples (e.g. research repos and FG Falcon–scoped public APIs [S14][S15]); many 2011+ modules do not.

### 8.2 Project policy

| Allowed | Forbidden |
|---|---|
| Implement the UDS 0x27 state machine | Bundle or scrape FORScan/AlfaOBD key tables |
| Plug-in interface: `ISeedKeyProvider` | Distribute reverse-engineered modern PCM/BCM keys |
| Document that user may supply their own provider for vehicles they own | Brute-force helpers aimed at immobilizer / theft |
| Cite public, vehicle-scoped research with clear age limits | Anything that enables vehicle theft or defeat of emissions monitors |

**Legal / safety:** Unlocking and writing modules can brick ECUs, disable safety systems, or violate emissions / safety regulations. UI must require explicit typed confirmation (“WRITE”) and show non-lawyerly warnings. Prefer read-only defaults.

### 8.3 FEPS / programming voltage

Some Ford powertrain unlock/programming flows historically needed FEPS voltage on a DLC pin. **MX+ does not provide FEPS.** Mark PCM security/programming that requires FEPS as **unsupported** on this adapter [UNKNOWN exact module list — gate behind capability flags].

---

## 9. Chrysler / FCA / Stellantis modules & SGW

### 9.1 Diagnostic stack

Modern FCA light-duty: UDS on CAN (11- and/or 29-bit). Older: ISO 9141 / KWP2000 / J1850 VPW. AlfaOBD-like feature targets (HVAC, BCM, RFH, PCM, SGW, …) need a module DB analogous to Ford’s, seeded from public service info and live discovery.

**[UNKNOWN]** Exact physical request/response ID pairs per module/year — same discovery approach as Ford.

### 9.2 Security Gateway (SGW) — hard constraint

Starting with **most 2018 MY** (some 2017) FCA vehicles, an SGW firewall sits between the OBD connector and vehicle networks [S10][S11]:

- **Allowed without auth:** diagnostic *data* reads (DTCs, many PIDs/DIDs) per FCA descriptions.
- **Blocked without auth:** intrusive ops — clear DTCs, bidirectional controls, calibrations, relearns, programming.
- **Legitimate unlock path:** **AutoAuth** (ETI / FCA program) — shop/user registration, tool manufacturer agreement, online challenge-response. Annual end-user fee (~$50/shop tier per public FAQ) [S11].
- Only tool companies under FCA/Stellantis agreements can integrate the Authentication Bridge [S10].

**OpenDiag-MX stance:**

1. Detect probable SGW (NRC patterns / known year+model table / failed clear).
2. Explain clearly; link to AutoAuth / OEM tooling.
3. **Do not** implement OBD-port jumper “SGW bypass cable” workflows, credential spoofing, or MITM of the auth bridge.
4. On pre-SGW vehicles, normal UDS write/clear paths apply with the same SafetyGuard as Ford.

### 9.3 “Proxi” / car configuration

FCA vehicles use central configuration (often called Proxi / PROXI alignment) stored across BCM and peers. Exact DID layout and alignment routines are **[UNKNOWN]** in public detail sufficient for safe automation. Phase 3+: read/backup first; write only with verified per-platform procedures in the community DB.

---

## 10. DTC lookup database

### 10.1 Layers

1. **SAE generic (P0xxx, U0xxx, C0xxx, B0xxx)** — public definitions from SAE J2012 / EPA summaries; store code → short text → severity hint.
2. **Manufacturer enhanced** — Ford/FCA-specific wording where legally redistributable (OEM service pubs the user owns, or community-submitted with source tag). Prefer linking to user-supplied FSM PDFs over redistributing copyrighted text.
3. **Status bits** — decode UDS `0x19` status availability masks (testFailed, confirmed, pending, …).

### 10.2 Schema (SQLite sketch)

```sql
CREATE TABLE dtc_def (
  code TEXT NOT NULL,           -- e.g. 'P0420' or 'U0100'
  oem TEXT,                     -- NULL | 'FORD' | 'FCA'
  system TEXT,                  -- powertrain/body/...
  title TEXT NOT NULL,
  description TEXT,
  source TEXT,                  -- citation URL or 'SAE-J2012'
  PRIMARY KEY (code, oem)
);
CREATE TABLE dtc_observation (
  session_id TEXT,
  module_id TEXT,
  code TEXT,
  status_mask INTEGER,
  raw_hex TEXT,
  observed_at TEXT
);
```

Ship a baseline SAE pack; OEM packs as optional downloads.

---

## 11. Community programming database

Shareable **configuration mods** (As-Built bit flips, FCA feature flags), not firmware.

### 11.1 Goals

- Versioned, reviewed, diffable.
- Bound to vehicle platform (make, model, year range, module HW/SW part numbers).
- Explicit risk level and restore instructions.
- Signed releases (git tags + optional minisign).

### 11.2 Proposed YAML record

```yaml
id: ford.apim.disable_bto
title: "APIM: example feature flag change"
platform:
  make: Ford
  models: ["F-150"]
  years: [2015, 2016, 2017]
  module: APIM
  part_number_glob: "HS7T-14G371-*"   # optional
risk: moderate   # low | moderate | high | brick
requires_security_access: true
buses: [MS-CAN]
changes:
  - did: 0xDE00
    line: "7D0-01-01"
    mask:  "0000FF0000"      # which nibbles/bytes touch
    from:  "........"        # optional precondition
    to:    "......xx...."    # documented pattern
safety_notes: |
  Reverse camera / 911 assist may be affected. Verify local law.
  Backup module before write. Battery maintainer required.
restore: "Re-apply Motorcraft As-Built or restore app backup UUID …"
references:
  - https://cyanlabs.net/asbuilt-db/...
authors: ["community:alice"]
schema_version: 1
```

### 11.3 Process

- PR review checklist: platform evidence, backup tested, checksum validated, no security-key material.
- App shows risk banner; high/brick requires typing module name.
- Offline pack + optional sync from GitHub releases.

---

## 12. Backup / restore

Before **any** `0x2E` / clear that mutates config:

1. Create `BackupBundle` (ZIP or folder):
   - `manifest.json` — app version, adapter serial (`STSN`), ISO timestamp (America/New_York displayed; UTC stored), VIN hash + last-6 optional, vehicle profile.
   - `modules/<id>/dids/*.bin` + `*.json` metadata.
   - `asbuilt/<id>.ab.txt` human-readable lines.
   - `dtcs.json` snapshot.
   - `trace.ndjson` optional request log excerpt.
2. Compute SHA-256 of bundle; refuse write if backup step failed.
3. Restore = guided replay of original DID values with same SafetyGuard; never auto-restore without confirmation.

Store bundles under `%LocalAppData%/OpenDiagMX/backups/` and offer copy-to-USB.

---

## 13. Safety interlocks (`SafetyGuard`)

| Check | Default policy |
|---|---|
| Battery voltage (`STVR`) | Abort write if \< 12.0 V; warn if \< 12.6 V; recommend maintainer |
| Ignition / engine | Prefer KOEO for config writes; block some routines if engine running **[UNKNOWN]** exact per-module needs |
| Session validity | Extended session + TesterPresent active |
| Backup exists | Required, age \< 30 min or same session |
| Read-back verify | After every write, `0x22` must match; else restore prompt |
| SGW (FCA) | Block mutating ops if locked; show AutoAuth guidance |
| User confirmation | Typed challenge for high-risk community mods |
| Rate limit | Back off on NRC lockouts (`0x37`/`0x36` etc.) |

No silent retries that could amplify a bad write.

---

## 14. UI layout (WPF)

```
┌─ Menu: File | Adapter | Vehicle | Tools | Help ───────────────────┐
├─ Toolbar: Connect | Voltage | Bus: HS/MS | Scan | Backup ─────────┤
├─ Left nav          │ Main content                                  │
│  • Dashboard       │  (context views below)                        │
│  • Modules         │                                               │
│  • DTCs            │                                               │
│  • As-Built/Config │                                               │
│  • Live data       │                                               │
│  • Community mods  │                                               │
│  • Backups         │                                               │
│  • Session log     │                                               │
├─ Status bar: COM7 | STN2256 v5.x | 12.4V | VIN •••••••123456 | HS ┤
└────────────────────────────────────────────────────────────────────┘
```

**Views**

1. **Connect:** adapter picker, pair instructions, `STDIX` details, self-test (voltage, OBD mode 01 PID 00).
2. **Dashboard:** VIN, CALIDs, mileage if available, last scan summary, safety strip (voltage).
3. **Modules:** tree by bus; columns for address, part #, response time; actions Read ID / DTCs / Config.
4. **DTCs:** grid with OEM text, status flags, freeze frame; Clear (guarded).
5. **As-Built:** line editor with checksum, decode tooltips, diff vs Motorcraft import / backup.
6. **Community mods:** search + risk badges + “apply to backup as dry-run”.
7. **Backups:** timeline, restore wizard.

Dark theme default (garage glare); large touch-friendly hit targets optional.

---

## 15. Phased milestone roadmap

### Phase 0 — Skeleton (1–2 weeks)

- .NET 8 + WPF solution layout, DI, Serilog, settings.
- `ElmStnLink` + ReplayAdapter; unit tests on FRPM examples.
- Connect UI: port enum, init sequence, show `STI`/`STDI`/`STVR`.

### Phase 1 — MVP (target)

**Must work on a real MX+ and at least one user vehicle:**

1. Connect / disconnect reliably over BT SPP.
2. Read battery voltage continuously.
3. Read VIN (OBD `$0902` and/or UDS `22 F190`).
4. Scan modules on HS-CAN + MS-CAN (`STP 33` / `STP 53`) from a seed address list + discovery.
5. Read & display DTCs (J1979 + UDS `0x19` where available); clear with confirm (skip/block on SGW).
6. Read As-Built / config DIDs for modules we can document; export backup bundle.
7. **No writes** in MVP except DTC clear.

### Phase 2 — Config writes (Ford-focused)

- Checksum-correct As-Built editing for documented modules (start with APIM if user vehicles have Sync).
- `ISeedKeyProvider` integration for modules with *user-supplied* public/owner keys only.
- Read-back verification + restore.
- Motorcraft `.AB` import/compare.

### Phase 3 — Community DB + FCA read depth

- YAML mod format + Git sync + risk UI.
- Broader Ford module decode packs.
- FCA module scan / DTC / read-config on **pre-SGW** vehicles; SGW detection UX on 2018+.
- Live data PID/DID browser.

### Phase 4 — Legacy & advanced

- KWP2000 / J1850 paths.
- Host-side ISO-TP Mode B.
- Guided routines from allow-listed community definitions.
- Optional Avalonia multi-platform shell.

### Explicit non-goals (v1)

- ECU firmware flash / J2534 passthru replacement.
- Key / immobilizer programming.
- SGW bypass.
- Supporting non-MX+ clones with broken STN command sets (may work accidentally; not a support target).

---

## 16. Testing strategy

- **Golden traces:** record anonymized sessions from user vehicles; ReplayAdapter CI.
- **Property tests:** As-Built checksum round-trip; ISO-TP segmentation reassembly.
- **Hardware-in-loop:** scripted nightly run when MX+ + bench ECU or vehicle available.
- **Safety tests:** ensure write path throws if backup missing or voltage low (no adapter needed).

---

## 17. Open questions for the product owner

1. Which exact vehicles / model years / engines will you test on first (Ford and/or FCA)?
2. Do those vehicles expose the modules you care about on MS-CAN @ 125k from a stock MX+, or do you already use a modified cable / OBD splitter?
3. Is Sync/APIM As-Built editing a day-one Phase 2 goal, or is PCM/BCM DTC work enough?
4. For FCA 2018+, is read-only acceptable, or is AutoAuth OEM-tool partnership eventually in scope (likely out of reach for a pure OSS volunteer app)?
5. Preferred license (GPL-3.0 vs MIT)?
6. Do you need Spanish/English UI from the start?
7. Any requirement to also support OBDLink EX (USB STN223x, MS-CAN) with the same codebase?

---

## 18. Sources cited

| ID | Source | URL |
|---|---|---|
| S1 | OBDLink Family Reference and Programming Manual (FRPM), Rev E | https://www.scantool.net/scantool/downloads/678/obdlink_frpm_e.pdf |
| S2 | OBDLink MX Protocol Commands (STP presets incl. MS-CAN 51–54) | https://www.scantool.net/scantool/downloads/118/mx_protocol_commands.pdf |
| S3 | STN21xx datasheet (MS-CAN pins) | https://www.scantool.net/scantool/downloads/206/stn2120-ds.pdf |
| S4 | FORScan forum: How to manage module configuration / As-Built format | https://forscan.org/forum/viewtopic.php?t=17208 |
| S5 | Community STPX usage (fdiag-related gist) | https://gist.github.com/JJTech0130/4c65c8ed706680b114276ad0b101c21e |
| S6 | CyanLabs Sync As-Built databases | https://cyanlabs.net/asbuilt-db/sync-1-apim/ |
| S7 | cyanlabs/apim-asbuilt-decode | https://github.com/cyanlabs/apim-asbuilt-decode |
| S8 | UDS overview (ISO 14229 services, 0x22/0x27) | https://icanhack.nl/knowledge-base/diagnostics/uds/ |
| S9 | UDS command cheatsheet / addressing notes | https://udssimulator.com/docs/uds-cheatsheet |
| S10 | AutoAuth High Level Overview (ETI / FCA SGW) | https://etools.org/wp-content/uploads/2024/09/AutoAuth-High-Level-Overview-Ver10.pdf |
| S11 | Security Gateway Module FAQ (ETI) | https://etools.org/wp-content/uploads/2024/09/SGW-FAQ-Ver-12.pdf |
| S12 | Community AB checksum explanation (MaverickTruckClub) | https://www.mavericktruckclub.com/forum/threads/decoding-the-ab-file-with-vba-and-a-bonus-pointless-math-lesson.48443/ |
| S13 | FORScan programming how-to (checksum auto-fix mention) | https://my-ford-focus.de/en/forscan-programming/ |
| S14 | Public research: older Ford seed/key discussions (historical; not a key pack) | https://github.com/andrewraharjo/CAN-Bus-Hack_Prius_Focus/blob/master/ecomcat_api/FordStuff.py |
| S15 | FoA Orion Keys (FG Falcon–scoped public derive API; illustrates policy boundary) | https://github.com/jakka351/FoA-Orion-Keys |
| S16 | Motorcraft As-Built portal (VIN factory data) | https://www.motorcraftservice.com/AsBuilt |
| S17 | Stellantis / ETI SGW landing | https://etools.org/stellantis-gateway/ |
| S18 | Faraday project notes on FORScan-like UDS scope (prior art, not a dependency) | https://github.com/r1cm3d/faraday/blob/main/docs/UDS.md |

---

## 19. Document history

| Ver | Date (America/New_York) | Notes |
|---|---|---|
| 0.1 | 2026-10-22 | Initial technical design plan |

