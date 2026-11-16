# UDS 0x27 SecurityAccess — Full Reference (ISO 14229-1)

> **Reference doc, written by hand.** What the app actually sends is in `docs/ENHANCED-UDS.md` → "Security access (0x27)" (generated from the code; source `UdsClient.SecurityAccessAsync`, `ISeedKeyProvider`). Sections 8–10 are a **display design reference**; not every field exists in the current UI. No Ford seed/key algorithms or secrets are in this repo; keys come only from user plug-ins approved by SHA-256 (`tools/SeedKeyPluginSample`).

SecurityAccess unlocks protected functions using a seed/key challenge. The tester requests a seed, computes a key with a secret algorithm, and sends it back. A correct key unlocks that level for the current session.

Gated services typically include: `2E` WriteDataByIdentifier, `31` RoutineControl, `34/36/37` download/flash, `2F` InputOutputControl, `3D` WriteMemoryByAddress (module-dependent).

ISO 14229-1:2020 adds `0x29` Authentication (certificate-based) as the modern replacement; Ford modules through ~2020 use `0x27`.

---

## 1. Request Parameters

| # | Parameter | Size | Values | Description |
|---|---|---|---|---|
| 1 | SID | 1 byte | `0x27` | SecurityAccess service. |
| 2 | securityAccessType (sub-function) | 1 byte | See §3 | Odd = requestSeed, even = sendKey. Bits 0–6 = level/type. |
| 2a | suppressPosRspMsgIndicationBit | bit 7 of byte 2 | 0/1 | 1 = ECU sends no positive response. Normally 0 for 0x27 (you need the seed and the unlock confirmation). |
| 3 | securityAccessDataRecord | 0–n bytes | OEM-defined | **requestSeed only**, optional. Extra input to seed generation (tester ID, ECU ID). Usually absent on Ford. |
| 3' | securityKey | n bytes | computed | **sendKey only.** Key derived from the most recent seed. Length fixed by the ECU (Ford: 3 bytes). |

## 2. Positive Response Parameters

| # | Parameter | Size | Values | Description |
|---|---|---|---|---|
| 1 | Response SID | 1 byte | `0x67` | SID + 0x40. |
| 2 | securityAccessType | 1 byte | echo | Confirms the level/type answered. |
| 3 | securitySeed | n bytes | random | **requestSeed only.** The challenge. **All zeros = level already unlocked** — do not send a key. An **empty** seed is malformed (the app throws; v0.6.1 audit). The sendKey response has no further bytes. |

## 3. securityAccessType (Sub-function) Values

| Value | Name | Description |
|---|---|---|
| `0x00` | ISOSAEReserved | Invalid. |
| `0x01` | requestSeed | Level 1 seed. |
| `0x02` | sendKey | Level 1 key. |
| `0x03, 0x05 … 0x41` (odd) | requestSeed | Levels 2–33; meaning is OEM-defined (config, programming, etc.). |
| `0x04, 0x06 … 0x42` (even) | sendKey | Key for the matching level (requestSeed value + 1). |
| `0x43–0x5E` | ISOSAEReserved | Invalid. |
| `0x5F` | ISO 26021-2 requestSeed | End-of-life pyrotechnic activation. **Not built.** |
| `0x60` | ISO 26021-2 sendKey | As above. **Not built.** |
| `0x61–0x7E` | systemSupplierSpecific | Supplier-defined levels. |
| `0x7F` | ISOSAEReserved | Invalid. |

## 4. Negative Response — `7F 27 NRC`

| NRC | Name | Cause | Tool Action |
|---|---|---|---|
| `0x10` | generalReject | Unspecified refusal | Stop; log. |
| `0x11` | serviceNotSupported | Module has no 0x27 | Mark module "no security." |
| `0x12` | subFunctionNotSupported | Level doesn't exist | Don't retry this level. |
| `0x13` | incorrectMessageLengthOrInvalidFormat | Wrong key/record length | Plug-in bug; stop. |
| `0x21` | busyRepeatRequest | ECU busy | Short wait, retry. |
| `0x22` | conditionsNotCorrect | Vehicle state wrong (speed, RPM, voltage, gear) | Show preconditions. |
| `0x24` | requestSequenceError | Key without valid seed, or double sendKey | Request a fresh seed. |
| `0x31` | requestOutOfRange | Bad data record or level | Treat like 0x12. |
| `0x35` | invalidKey | Key wrong | Attempts +1. **Never auto-retry.** |
| `0x36` | exceededNumberOfAttempts | Attempt limit hit | Start delay countdown; disable actions. |
| `0x37` | requiredTimeDelayNotExpired | Delay active (also at power-up) | Wait it out. |
| `0x78` | requestCorrectlyReceived-ResponsePending | ECU still working | Extend wait to P2*. |
| `0x7E` | subFunctionNotSupportedInActiveSession | Level needs another session | Switch session (`10 03` / `10 02`). |
| `0x7F` | serviceNotSupportedInActiveSession | 0x27 not allowed in default session | Switch session. |

Related: **`0x33` securityAccessDenied** on any other service means that service requires an unlocked level.

## 5. Timing and State Parameters

| Parameter | Typical | Description |
|---|---|---|
| P2server | 50 ms | Normal response time. |
| P2*server | 5000 ms | Extended time after NRC 0x78. |
| S3server | 5000 ms | Session timeout without TesterPresent → default session, security lost. |
| S3client (TesterPresent rate) | ~2000 ms | Send `3E 80` to keep the session alive. |
| Delay timer | ECU-defined (often 10 s) | Lockout after 0x36. |
| Power-up delay | ECU-defined | Some ECUs return 0x37 right after power-on. |
| Attempt counter | ECU-defined (often 2–3) | Bad keys allowed before 0x36. May persist across power cycles. |
| Seed validity | Until next request / session change | Only the latest seed is valid. |
| Security scope | Per session | Lost on `10` session change, `11` reset, S3 timeout, ignition off. |

## 6. Message Sequence

```
10 03            → 50 03 P2 P2*     extended session (10 02 = programming)
3E 80            (every ~2 s)       tester present, keep session alive
27 01            → 67 01 AA BB CC   seed
27 02 K1 K2 K3   → 67 02            unlocked
2E / 31 / 34 …                       protected service
```

## 7. Ford Specifics (community-sourced, UNVERIFIED)

Not confirmed by this project. Per-module Ford levels and algorithms are not known here (see `docs/ENHANCED-UDS.md`).

| Item | Value |
|---|---|
| Seed / key length | 3 bytes / 3 bytes |
| Algorithm | Bit-shift function + per-module 5-byte secret; supplied by plug-in, never stored in the tool. |
| Level `0x01/0x02` | Extended session (`10 03`): As-Built / configuration writes. |
| Programming level | Programming session (`10 02`); module-specific (often 0x01, sometimes 0x03/0x11) — confirm per module. |
| PATS timed access | ~10 min ECU-enforced wait, not a computed key. |
| Addressing | Physical only (PCM 7E0→7E8, BCM 726→72E). Never functional 7DF. |

---

## 8. Display Fields (design reference)

| Field | Example | Description |
|---|---|---|
| Module / address | PCM 7E0→7E8 | ECU and request/response CAN IDs. |
| Session | Extended (0x03) | Current diagnostic session. |
| Level | 0x01 (27 01 / 27 02) | Level and its sub-function pair. |
| State | UNLOCKED | See §9. |
| Seed | A3 1F 7C | Last seed and its age. |
| Key | ●● ●● ●● [show] | Masked by default. |
| Attempts | 1 / 3 | Bad keys vs. known limit. |
| Delay | 00:08 | Countdown during 0x36/0x37 lockout. |
| S3 keep-alive | 3.1 s | Time until the session drops. |
| Plug-in | my_plugin.dll · sha256 9f2e…c41 | Approved key source used. |
| Last NRC | 0x35 invalidKey | Decoded name + action. |
| Lost reason | S3 timeout | Why security was dropped. |
| Gates | 2E As-Built · 31 routines | Services this level unlocks. |

### Panel

```
┌ PCM (7E0→7E8) · Extended 0x03 ──────────────┐
│ Level 0x01  ● UNLOCKED      S3 ▓▓▓▓░ 3.1 s   │
│ Seed  A3 1F 7C (0.4 s)  Key ●● ●● ●● [show]  │
│ Attempts 1/3   Delay —   Last NRC —          │
│ Plug-in my_plugin.dll · sha256 9f2e…c41        │
│ Gates: 2E As-Built · 31 routines             │
└──────────────────────────────────────────────┘
```

## 9. States

| State | Color | Trigger |
|---|---|---|
| LOCKED | Grey | Default, or security lost |
| SEED RECEIVED | Amber | `67 odd` with non-zero seed |
| ALREADY UNLOCKED | Green | Zero seed |
| UNLOCKED | Green | `67 even` |
| INVALID KEY | Red | NRC 0x35 |
| LOCKED OUT | Red | NRC 0x36 / 0x37 |
| NOT SUPPORTED | Grey | NRC 0x11 / 0x12 / 0x31 |
| WRONG SESSION | Amber | NRC 0x7E / 0x7F |

### Display Rules

- Mask the key by default; the seed is shown (public, random).
- Decode every NRC to its name plus a one-line action.
- On security loss, state the reason ("Lost: S3 timeout"), not just "Locked."
- On `0x33` from any service, link to the panel: "Blocked: needs PCM level 0x01."
- Never auto-retry after 0x35; disable actions during 0x36/0x37 delay.

## 10. Log Format

```
01:49:02.118 7E0→ 10 03
01:49:02.135 7E8← 50 03 00 32 01 F4        extended · P2 50 ms · P2* 5000 ms
01:49:02.140 7E0→ 27 01                    requestSeed L1
01:49:02.161 7E8← 67 01 A3 1F 7C           seed
01:49:02.170 7E0→ 27 02 ** ** **           sendKey (my_plugin.dll)
01:49:02.192 7E8← 67 02                    UNLOCKED L1
01:49:04.170 7E0→ 3E 80                    keep-alive
01:49:09.500 7E8← 7F 2E 33                 securityAccessDenied · lost: S3 timeout
```
