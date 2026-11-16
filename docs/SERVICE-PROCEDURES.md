# Service procedures

Generated from `src/ObdLink.Core/Service/ServiceProcedures.cs` (do not edit by hand; run the tests with `UPDATE_DOCS=1`).

Ford's tools (IDS/FDRS) and FORScan offer "service procedures": resets, calibrations and relearns. This catalog lists them with **where every identifier comes from**. Nothing is guessed silently:

| Confidence | Meaning | Can be run |
|---|---|---|
| Verified | From Ford service information or a capture on this model, and run on the truck | yes, type RUN |
| Community | From a cited public source (e.g. a forum post quoting a Ford tool); not run on a 2017 F-150 by this project | yes, type UNVERIFIED |
| Synthesized | Inferred by this project from related evidence | yes, type UNVERIFIED (bench first) |
| NeedsCapture | The procedure exists but no source gives its identifiers | no |

Every run needs writes turned on, the battery check and the typed phrase. Pyrotechnic (E200-E2FF) and programming (FF00/FF01) routines are refused everywhere. Many procedures also have a **tool-free method** that Ford documents; it is listed where known and needs no adapter at all.

## Summary

| Procedure | Module | Confidence | Requests | Tool-free method |
|---|---|---|---|---|
| [Battery Monitoring System (BMS) reset](#bcm-bms-reset) | BCM 0x726 | Community | `10 03, 31 01 20 1A` | yes |
| [TPMS sensor training](#bcm-tpms-training) | BCM 0x726 | NeedsCapture | — | yes |
| [PCM Keep Alive Memory (KAM) reset](#pcm-kam-reset) | PCM 0x7E0 | NeedsCapture | — | — |
| [TCM Keep Alive Memory reset / clear adaptive tables](#tcm-kam-reset) | TCM 0x7E1 | NeedsCapture | — | — |
| [Steering angle sensor calibration](#sas-calibration) | ABS 0x760 | NeedsCapture | — | — |
| [PSCM pull-drift compensation reset](#pscm-pull-drift-reset) | PSCM 0x730 | NeedsCapture | — | — |
| [ABS service bleed](#abs-service-bleed) | ABS 0x760 | NeedsCapture | — | — |

## Battery Monitoring System (BMS) reset

<a id="bcm-bms-reset"></a>Key `bcm-bms-reset` · BCM 0x726 · **Community** · Ford with BMS (2011+). Routine reported for the 2011 F-150 BCM; not verified on the 2017 BCM.

Tells the BCM a new battery is installed: battery state of charge back to its default (about 85 %), drain timers and learned battery data reset.

**Use when:**

- A new battery was just installed (fully charged), or Ford service information tells you to.

**Conditions:**

- New battery fully charged; charger DISCONNECTED before the reset (Ford TSB wording).
- Ignition ON, engine OFF, doors closed.
- Afterwards leave the truck undisturbed for about 8 hours if possible.

**Warnings:**

- Not after a simple charge: the reset clears the battery's learned aging data, which is only right for a new battery.
- Battery type (flooded / AGM / EFB) is NOT set by this procedure. If you changed type, check the BCM As-Built battery type first [UNVERIFIED location].

**Requests:** `10 03, 31 01 20 1A` (type UNVERIFIED)

**Afterwards:**

- The battery light may flash to confirm. Check for load-shed messages over the next few days.

**Tool-free method:**

1. Ignition ON, engine OFF.
2. Within 10 seconds: flash the high beams 5 times (ending with them off), then press and release the brake pedal 3 times.
3. The battery warning light flashes 3 times within about 15 seconds when the reset worked.

**Sources:**

- Ford engineer's post: BCM 726, extended session, routine 201A with Ford's Diagnostic Engineering Tool (2011 F-150): <https://www.f150forum.com/f38/battery-management-system-reset-109656/>
- Same procedure quoted for other Ford models: <https://www.focusfanatics.com/threads/low-battery-audio-off-message.673337/>
- Ford TSB: disconnect the charger, then perform the BMS reset: <https://static.nhtsa.gov/odi/tsbs/2024/MC-10249755-0001.pdf>
- Ford service note: reset only for a new battery: <https://forum.forscan.org/viewtopic.php?p=84356>
- Tool-free method (high beams 5x, brake 3x): <https://www.f150forum.com/f118/forscan-reset-bms-code-437277/>

## TPMS sensor training

<a id="bcm-tpms-training"></a>Key `bcm-tpms-training` · BCM 0x726 · **NeedsCapture** · Ford with TPMS (BCM-hosted).

Teaches the BCM which tire pressure sensor sits at which wheel.

**Use when:**

- After a tire rotation, a sensor or wheel replacement, or a BCM replacement.

**Conditions:**

- Parked, doors closed, no RF noise nearby. Vehicle-speed DTCs fixed first.

**Not runnable:** no public source gives this procedure's identifiers yet. It can be added after a capture on the truck (see docs/SERVICE-PROCEDURES.md).

**Afterwards:**

- The horn sounds twice when all wheels are trained.

**Tool-free method:**

1. Steps vary by model year: your owner's manual is authoritative.
2. Ignition OFF, then OFF → RUN three times, ending in RUN (no more than 2 minutes between cycles).
3. Press and release the brake pedal, then ignition OFF.
4. OFF → RUN three times again, ending in RUN: the horn sounds once and the TPMS light flashes (training mode).
5. Starting at the left front and going clockwise, let air out of each tire (or use a TPMS activation tool) until the horn sounds.
6. After the last tire the horn sounds twice. Re-inflate to the placard pressure.

**Sources:**

- Ford tool-free training procedure: <https://www.brakeandfrontend.com/ford-tpms-relearn-training-procedures/>

## PCM Keep Alive Memory (KAM) reset

<a id="pcm-kam-reset"></a>Key `pcm-kam-reset` · PCM 0x7E0 · **NeedsCapture** · Ford PCM.

Returns the PCM's adaptive learning (fuel trims, idle air, adaptive airflow) to defaults.

**Use when:**

- After repairs that invalidate learned values (injectors, MAF, intake leaks), when service information says so.

**Conditions:**

- Ignition ON, engine OFF.

**Warnings:**

- Clearing DTCs does not reset KAM; a KAM reset also clears the idle air trim table.

**Not runnable:** no public source gives this procedure's identifiers yet. It can be added after a capture on the truck (see docs/SERVICE-PROCEDURES.md).

**Afterwards:**

- Idle at operating temperature with the A/C off for about 2 minutes, then drive normally; driveability may be odd until it relearns.

**Sources:**

- Ford PCED text on KAM reset (quoted): <https://fordraptorforum.com/threads/pcm.23710>

## TCM Keep Alive Memory reset / clear adaptive tables

<a id="tcm-kam-reset"></a>Key `tcm-kam-reset` · TCM 0x7E1 · **NeedsCapture** · 6R80 / 10R80. On the 2017 F-150 3.5L NA (6R80) the TCM may be inside the PCM: the address is [UNVERIFIED].

Clears the transmission's learned shift adaptations.

**Use when:**

- After transmission repair, valve body or TCM replacement, fluid service where Ford asks for it, or a shift-quality TSB.

**Conditions:**

- Ignition ON, engine OFF.

**Warnings:**

- TCM KAM reset and "clear adaptive tables" are separate functions in Ford's tools; a TSB requires TCM KAM first. Disconnecting the battery does not clear TCM KAM. Do not clear PCM KAM for this.

**Not runnable:** no public source gives this procedure's identifiers yet. It can be added after a capture on the truck (see docs/SERVICE-PROCEDURES.md).

**Afterwards:**

- Key OFF for at least 2 minutes, then the adaptive learning drive cycle on a level road (fluid ≥ 175 °F).

**Sources:**

- Ford TSB (transmission relearn): TCM KAM vs adaptive tables: <https://www.vehicleservicepros.com/tsbs/article/53092343/ford-trans-relearn-2014-11-10>

## Steering angle sensor calibration

<a id="sas-calibration"></a>Key `sas-calibration` · ABS 0x760 · **NeedsCapture** · 2015+ F-150 with EPAS.

Zeroes the steering angle so 0° means straight ahead (stability control, lane keeping).

**Use when:**

- After an alignment, steering column or clock spring work, or an ABS/PSCM replacement; DTC C0051-type steering angle faults.

**Conditions:**

- Alignment correct, wheels and steering wheel straight, vehicle level and stopped, engine running or ignition ON per procedure.

**Warnings:**

- Some trucks host this in the PSCM (0x730) or SCCM instead of the ABS.

**Not runnable:** no public source gives this procedure's identifiers yet. It can be added after a capture on the truck (see docs/SERVICE-PROCEDURES.md).

**Afterwards:**

- Drive straight and check the steering wheel and stability-control light.

**Sources:**

- Community description (ABS/SCCM and PSCM procedures): <https://www.fordtremor.com/threads/steering-wheel-angle-sensor-calibration.20048/>

## PSCM pull-drift compensation reset

<a id="pscm-pull-drift-reset"></a>Key `pscm-pull-drift-reset` · PSCM 0x730 · **NeedsCapture** · 2015+ F-150 with EPAS.

Clears the steering's learned pull-drift compensation.

**Use when:**

- After an alignment or suspension change, when the truck pulls because the PSCM learned the old fault.

**Conditions:**

- Alignment correct first. Ignition ON, engine OFF unless the procedure says otherwise.

**Not runnable:** no public source gives this procedure's identifiers yet. It can be added after a capture on the truck (see docs/SERVICE-PROCEDURES.md).

**Afterwards:**

- Drive on a straight road so it relearns.

**Sources:**

- Community description: <https://www.fordtremor.com/threads/steering-wheel-angle-sensor-calibration.20048/>

## ABS service bleed

<a id="abs-service-bleed"></a>Key `abs-service-bleed` · ABS 0x760 · **NeedsCapture** · Ford ABS.

Cycles the ABS valves and pump so trapped air leaves the hydraulic unit during a brake bleed.

**Use when:**

- After the ABS hydraulic unit was opened or air reached it.

**Conditions:**

- Brake bleeding set up (second person or pressure bleeder), reservoir full, ignition ON, engine OFF.

**Warnings:**

- Reported missing from FORScan for some 2015+ F-150 ABS modules.

**Not runnable:** no public source gives this procedure's identifiers yet. It can be added after a capture on the truck (see docs/SERVICE-PROCEDURES.md).

**Afterwards:**

- Bleed the brakes again; firm pedal before driving.

**Sources:**

- Community report: <https://www.ford-trucks.com/forums/1695552-no-more-abs-service-bleed-function-in-forscan.html>

## Filling the gaps

A NeedsCapture procedure becomes runnable only with identifiers from one of:

1. **A cited public source** (Ford service information, a TSB, a forum post quoting a Ford tool). Confidence Community.
2. **A capture on the truck**: the requests a working tool sends for that procedure on this model, recorded from the adapter link. Confidence Verified after a successful run (verification plan, Phase 3 D2).
3. **Synthesis**: the same routine on a related model year or module generation. Confidence Synthesized; the app asks for UNVERIFIED and the release notes say so.

Whatever the source, a routine only goes on the truck allow-list after it was run once with its listed conditions and left no new DTCs.
