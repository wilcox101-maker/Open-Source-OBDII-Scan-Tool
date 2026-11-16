# Module flashing (Phase 5)

Status (v0.20.1): **Flash tab (v0.19.0) on the Core engine. Run end to end on Windows 11 against the SYNTHETIC simulated module (DEMO-EX, 2026-11-16, see the README screenshots). Nothing here has flashed a real module.**
Code: `src/ObdLink.Core/Flash/` (engine), `src/ObdLink.App/FlashViewModel.cs` (tab). Tests: `tests/ObdLink.Core.Tests/FlashEngineTests.cs`.
How to use the tab: `docs/GUIDE.md` → Flash. Check a real file offline first: `obdcli vbf-info --file x.vbf`.

Policy (Dale, 2026-11-15): flashing is available on every supported adapter (OBDLink EX and MX+) with the usual warnings.
The MX+ gets one extra warning (wireless link; wired EX recommended). Nothing is blocked by adapter type.

## Sequence (ISO 14229-1 download)

| Step | Request | Notes |
|---|---|---|
| Preflight | ATRV ×3, 3E 00 ×8 | Voltage window (default 12.5–15.0 V), link health, file vs module, adapter, PC power |
| Backup | (caller) | Mandatory for a new job; its reference goes in the journal |
| Extended session | 10 03 | Optional: a refusal is accepted (bootloaders often lack it) |
| Programming session | 10 02 | From here on the engine never resets the module after a failure |
| Security | 27 xx / 27 xx+1 | Key from the user's seed/key provider. None ships with the repo |
| Erase | 31 01 FF00 + option | Per erase region; polled with 31 03 when the channel can't wait out 0x78 |
| Download | 34 / 36 ×N / 37 | Block counter 0x01 → 0xFF → 0x00; message size min(module limit, 4095) |
| Check | 31 01 FF01 | Positive reply = pass unless the plan sets a status check |
| Reset + verify | 11 01, 22 F188 | Part number read back and compared with the file |

## Error handling

- **Journal** (`FileFlashJournal`): JSON lines, write-through + flush per entry. Every acknowledged block is on disk before the
  next is sent. A torn last line (crash mid-write) is ignored.
- **Same-counter retry**: a TransferData block with no answer is sent again with the same counter. ISO 14229-1 servers answer a
  repeat of the last accepted counter positively without writing it twice.
- **NRC table** (`FlashFaults`): 0x78 wait/settle, 0x21 retry, 0x92/0x93 pause until the battery is back in the window,
  0x33/0x35/0x36/0x37 stop (operator re-does security), everything else (0x22, 0x24, 0x70–0x73, …) stop and recover.
- **Link loss** (port closed, adapter unplugged): the job stays open. Reconnect with either adapter and run again: a resume
  restarts from the erase (bootloaders generally can't continue mid-segment) and does not take a second backup.
- **ELM/STN channels** can't keep listening after 0x78. The engine settles instead: resend (TransferData, exit), or poll 31 03
  (erase, check). If a module can't report routine results it waits a bounded time with keep-alives, and RequestDownload is
  retried while the module answers 0x21/0x22/0x70.
- **Session kept alive during every wait** (v0.21.2): once the module is in the programming session, every pause (low battery,
  the 0x37 security delay, retry back-offs, response-pending settles, routine polls) sends TesterPresent `3E 80` at least every
  2 s, so S3 (5 s) never ends the session while the engine waits.
- **Session lost** (v0.21.2): 0x7E / 0x7F at any time, or 0x33 (and 0x24 on TransferData) after unlocking, mean the module left
  the programming session or lost security. The engine re-enters programming **from the erase** once (never writes into
  un-erased flash); if it happens again the job stays open for resume.
- **Recovery-safe resume** (v0.21.3):
  - Resume checks: if a module ignores TesterPresent (a bootloader in its default session), a programming-session request is
    tried; any answer lets recovery continue. A silent module fails the check with operator steps (ignition cycle, charger,
    wired EX, security delay) instead of looping. A new flash keeps the strict link check.
  - A job interrupted after the module reset is checked first: if the module already reports the file's part number, the job
    is marked complete and nothing is erased or written again.
  - A job interrupted before its backup was saved (module still in its application) takes the backup before anything else.
- **Stop** only at safe points: before the programming session (module unchanged, job abandoned) or between segments (job open).
- **No ECU reset or session exit after a failure.** A module left in its bootloader can usually be flashed again.

## UNVERIFIED (must be settled before a real flash)

- VBF layout and per-block CRC-16/CCITT come from community descriptions, not Ford documentation; no real file parsed yet.
  A block checksum mismatch rejects the file. **Check a real file first:** `obdcli vbf-info --file x.vbf` reports per block
  whether the stored checksum is CCITT-FALSE or which other common checksum it matches (exit 3 = the app would refuse it).
- Ford security level for programming, erase option record layout and FF01 status meaning are per module and not public.
  `FlashPlan` exposes all three.
- Throughput: ELM host-side ISO-TP sends each CAN frame as a separate command. A 4 KB block is ~585 commands, which will be slow
  over Bluetooth. Measure on the truck; STN-side segmentation is a later optimization.
- First real test: a low-risk module with a known-good backup, EX first, then MX+.
