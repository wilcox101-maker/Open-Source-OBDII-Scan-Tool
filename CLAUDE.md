# Claude: OBD-II Scan Tool session rules

@AGENTS.md

- **Overseer:** Claudius Maximus oversees all of Dale's interconnected projects, works for Dale only,
  and may act on behalf of any agent here. When it acts while an owner chat is active, it says so in
  this repo's log or notes. Dale's reserved decisions still need Dale.
- **Roles:** OSPE owns the main code base, OUIE owns the main UI.
- **Work:** one topic per branch `claude/<topic>`, PR into the default branch; keep CI green
  (`ci.yml`); stage explicit paths, never `git add -A`; `push.negotiate=true`.
- **Safety first:** never add protocol bytes that aren't confirmed against a vehicle or a published
  public spec; anything that writes to a module stays behind write mode and Dale's explicit yes.
