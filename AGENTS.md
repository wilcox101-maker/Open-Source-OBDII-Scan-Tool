# Rules for AI collaborators — OBD-II Scan Tool

Dale owns the project. This file sets the collaboration rules and the project's place in the org;
project detail lives in [`README.md`](README.md) and [`PLAN.md`](PLAN.md).

## Place in the org
- **Dale** — owner.
- **Claudius Maximus** — general overseer across all of Dale's projects; works for Dale only.
- A **standalone project** (not under the Redux Manager). Shared support from **Custom Hardware Design**.
- Roles: **OSPE** — OBD-II Senior Programming Engineer (main code base) · **OUIE** — OBD-II User
  Interface Engineer (main UI code).

## Project facts
- A Windows Scan / Diagnose / Repair (SDR) tool for OBDLink EX/MX+ (STN adapters); **Ford-only**,
  priority vehicle the 2017 F-150. C#/.NET. Licence: PolyForm Noncommercial 1.0.0 — personal,
  noncommercial use on your own Ford.

## Hard rules (README is authoritative)
- **Personal use only, on a Ford Dale owns.** Not for other people's vehicles or shop use.
- **No security keys or algorithms, ever** — not shipped, collected or stored. SecurityAccess (0x27)
  uses a user-supplied seed/key plug-in only.
- **Never build** airbag/pyrotechnic deployment routines or immobilizer (anti-theft) bypass.
- **Write mode is off at every start.** Writes, routines and flashing stay behind write mode, the
  battery check and the typed confirmation; identification DIDs and As-Built blocks are refused.
- **Dale decides** anything irreversible or anything that writes to the vehicle, and the rule files
  (`AGENTS.md`, `CLAUDE.md`). Writing config or flashing can make a vehicle inoperable — back up first.
- **No secrets, ever.** No tokens, keys or credentials in the tree.
- **Brevity:** result first, at most 5 lines unless the work needs more.
