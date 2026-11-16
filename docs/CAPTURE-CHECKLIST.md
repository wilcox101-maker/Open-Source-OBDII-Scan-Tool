# Capture session checklist (2017 F-150 3.5L, OBDLink EX)

One session with `obdcli proxy` between FORScan and the adapter fills the procedures this app can't run yet. Each capture records the requests a working tool sends; the shareable summary leaves out seeds/keys and masks the VIN. Background: `docs/CLI.md` ("Capturing a service procedure"), `docs/SERVICE-PROCEDURES.md`.

## Before you start
- [ ] Truck parked level, where it can sit for an hour. **Battery charger on** for the whole session (voltage drops abort procedures).
- [ ] Laptop on mains power. **Wired OBDLink EX** (not the MX+).
- [ ] Latest `obdcli` build (CI artifact `obdcli-win-x64-<sha>`); check `SHA256SUMS.txt`.
- [ ] Baseline: in this app (or `obdcli dtcs --port COMx --module all > before.txt`), read DTCs of **all modules** and keep the file. Dale's truck already has some (PCM P0722, FCIM B1081, PBM C2005, PSCM U3003, SCCM B1380 + U2101), so only *new* codes count.
- [ ] Smoke test first: `obdcli proxy --port COMx --log smoke.txt`, then in FORScan pick a **Wi-Fi / TCP adapter at 127.0.0.1:35000** and only read DTCs. If FORScan won't connect through the proxy, stop here (whether it accepts a TCP adapter on 127.0.0.1 is unverified).

## For every procedure
1. **One procedure per log**: `obdcli proxy --port COMx --log <name>.txt` with a new name each time.
2. Note the **clock time** you press start in FORScan, the **ignition state** (KOEO = key on engine off, KOER = engine running) and anything FORScan asks you to do.
3. Run it exactly as FORScan says. If FORScan doesn't list it for this truck, write that down (that's a result too) and move on.
4. Ctrl+C the proxy. Run `obdcli capture-summary --log <name>.txt > <name>-summary.txt`.
5. **Keep the raw `<name>.txt` private** (it can hold seeds, keys and the full VIN). Share only the summary.

## The list (in this order; read-only ones first)
| # | What | Module on this truck | Ignition | Notes |
|---|---|---|---|---|
| 1 | KOEO on-demand self-test | PCM | KOEO | read-only test; note the codes it reports |
| 2 | KOER on-demand self-test | PCM | KOER, warm, park brake on | follow FORScan's prompts (A/C off, steering wheel moves) |
| 3 | BdyCM on-demand self-test | BdyCM | KOEO | |
| 4 | PATS menu, **read-only** | BdyCM / PATS | KOEO | open the PATS section and note which access type it offers (timed or coded) and the key count. **Do not erase or program keys.** |
| 5 | PCM KAM reset | PCM | KOEO | relearn drive afterwards (idle 2 min, A/C off, then drive) |
| 6 | Transmission KAM / adaptive table reset | **PCM** (no TCM on this truck) | KOEO | note whether FORScan lists it under the PCM; adaptive learning drive afterwards |
| 7 | Steering angle sensor calibration | ABS | per FORScan | wheels straight, truck level |
| 8 | Pull-drift reset | PSCM | per FORScan | |
| 9 | TPMS sensor training | BdyCM | per FORScan | have the TPMS tool or follow the tire deflate method FORScan shows |
| 10 | ABS service bleed | ABS | per FORScan | **only if FORScan offers it** for this VIN (reported missing for 2015); needs two people and fluid on hand |
| 11 | Module Initialization / Relearn Vehicle Data | PCM (and what FORScan lists) | KOEO | run the relearn on its own; **don't change tire size, axle ratio or VIN** for this test |

## After the session
- [ ] DTCs of all modules again (`after.txt`); compare with `before.txt`. Any new code goes in the notes for that procedure.
- [ ] Post each `*-summary.txt` as a GitHub issue in this repo, titled with the procedure name. The issue link becomes the catalog source (https).
- [ ] Entries go in as **Community**. They become **Verified** only after this app runs them once on the truck with their conditions and no new DTCs.
