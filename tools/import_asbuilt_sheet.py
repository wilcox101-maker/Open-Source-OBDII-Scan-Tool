"""Imports a community As-Built Google Sheet (xlsx export) into community-db JSON with provenance."""
import openpyxl, re, json, sys
src_url, path, outdir, label = sys.argv[1:5]
SKIP = {"VINs"}  # third-party full VINs: not imported (privacy)
wb = openpyxl.load_workbook(path, read_only=True, data_only=True)
A = re.compile(r'^([0-9A-F]{3})-([0-9A-F]{2})-([0-9A-F]{2})$')
G = re.compile(r'^[0-9A-Fa-fx*\-]{2,4}$')
def norm(v):
    if v is None: return ""
    if isinstance(v, float) and v.is_integer(): v = int(v)
    return str(v).replace('\xa0', ' ').strip()
feats, full, mods, skipped = [], [], {}, []
for ws in wb.worksheets:
    if ws.title in SKIP: skipped.append(ws.title); continue
    last_addr = None; last = None
    for r in ws.iter_rows(values_only=True):
        c = [norm(v) for v in r]
        if not any(c): continue
        a = A.match(c[0].upper())
        if a:
            last_addr = c[0].upper(); rest = c[1:]
        elif last_addr and G.fullmatch(c[0]) and ('x' in c[0].lower() or '*' in c[0]):
            rest = c  # pattern row that inherits the previous address
        else:
            if last and any(c): last["values"] += [x for x in c if x]
            continue
        groups = []
        for x in rest:
            if G.fullmatch(x) and len(groups) < 3: groups.append(x.upper().replace('X', 'x'))
            else: break
        desc = [x for x in rest[len(groups):] if x]
        last = {"tab": ws.title, "address": last_addr, "pattern": groups, "description": desc[0] if desc else "", "values": desc[1:]}
        mods.setdefault(last_addr[:3], set()).add(ws.title)
        if groups and all(re.fullmatch(r'[0-9A-F]{2,4}', g) for g in groups):
            full.append({"tab": ws.title, "line": last_addr + " " + " ".join(groups)}); last = None
        else: feats.append(last)
prov = {"source": label, "source_url": src_url, "confidence": "CommunityReported", "skipped_tabs": skipped}
json.dump({**prov, "entries": feats}, open(f"{outdir}/features.json", "w"), indent=1)
json.dump({**prov, "lines": full}, open(f"{outdir}/complete-lines.json", "w"), indent=1)
json.dump({**prov, "modules": {k: sorted(v) for k, v in sorted(mods.items())}}, open(f"{outdir}/module-addresses.json", "w"), indent=1)
print(len(feats), len(full), sorted(mods))
