# Module flashing (Phase 5)

Status (v0.22.0): **Flash tab (v0.19.0) on the Core engine; keep-alive and lost-session recovery in v0.21.2; SBL stage, lockout hold, early-failure reset and image checks in v0.22.0. Run end to end on Windows 11 against the SYNTHETIC simulated module (DEMO-EX, 2026-11-16, see the README screenshots). Nothing here has flashed a real module.**
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
| Security | 27 xx / 27 xx+1 | Key from the user's seed/key provider. None ships with the repo. Outcome journaled (lockout hold, below) |
| SBL (optional) | 34 / 36 / 37 to RAM, 31 01 0301 + call address | Secondary bootloader (VBF `sw_part_type = SBL`, `call = …`). Not journaled as progress; reloaded on every attempt |
| Erase | 31 01 FF00 + option | Per erase region; polled with 31 03 when the channel can't wait out 0x78 |
| Download | 34 / 36 ×N / 37 | Block counter 0x01 → 0xFF → 0x00; message size min(module limit, 4095) |
| Commit (optional) | 31 01 0304 | Sent by the app when an SBL is loaded (community Ford flashers do) |
| Check | 31 01 FF01 | Positive reply = pass unless the plan sets a status check. NRC 0x11/0x12/0x31 (not supported) → continue to the reset |
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
  un-erased flash), redoing security (subject to the lockout hold) and the SBL; if it happens again the job stays open for resume.
- **Stop** only at safe points: before the programming session (module unchanged, job abandoned) or between segments (job open).
- **No ECU reset or session exit after a failure once memory may have changed.** A module left in its bootloader can usually be flashed again.
- **Failure before anything was erased** (programming session, security, SBL, or a first erase refused with a definite NRC) on a
  fresh job: the engine sends 11 01 so the module runs its own software again, checks that it answers, and closes the job as
  unchanged. On a resume it never does this (an earlier attempt may have erased).
- **Lockout hold** (v0.22.0): every SecurityAccess outcome is journaled per module, across jobs. After a rejected key (0x35) no
  new attempt is made until the operator clears the hold (Flash tab) — a wrong key sent again only spends the module's attempt
  counter. After 0x36, or 0x37 that did not clear after one 10 s wait, no seed is requested for 10 minutes. 0x37 on the seed is
  waited out once (no key was sent, so the counter is untouched).
- **Image checks** (v0.22.0): segments must not overlap, must fit the 32-bit space and the address/length format, and must lie
  inside the file's erase regions; an SBL needs a call address inside its own data. The engine refuses a bad layout before
  sending anything. A file without `ecu_address`, a different vehicle-program prefix in the part number, or a Ford EXE/DATA file
  without an SBL is a warning.

## UNVERIFIED (must be settled before a real flash)

- VBF layout and per-block CRC-16/CCITT come from community descriptions, not Ford documentation; no real file parsed yet.
  A block checksum mismatch rejects the file. **Check a real file first:** `obdcli vbf-info --file x.vbf` reports per block
  whether the stored checksum is CCITT-FALSE or which other common checksum it matches (exit 3 = the app would refuse it).
- Ford security level for programming, erase option record layout and FF01 status meaning are per module and not public.
  `FlashPlan` exposes all three.
- SBL start routine 0x0301 with a 4-byte call address and the commit routine 0x0304 come from an open-source community flasher
  (VBFlasher, rollsch/VBFlasher, protocol behaviour only; none of its code or key constants are used here), not from Ford
  documentation.
- Throughput: ELM host-side ISO-TP sends each CAN frame as a separate command. A 4 KB block is ~585 commands, which will be slow
  over Bluetooth. Measure on the truck; STN-side segmentation is a later optimization.
- First real test: a low-risk module with a known-good backup, EX first, then MX+.
