"""Import 'FORScan Programming Modules Acronyms-Abbreviations.xlsx' (community list, updated May-20-2019)
into community-db/ford-module-acronyms/. Names/descriptions only; never CAN addresses."""
import sys, json, csv, collections, openpyxl
src, out = sys.argv[1], sys.argv[2]
ws = openpyxl.load_workbook(src).active
rows = []
for r in ws.iter_rows(min_row=5):
    code, text = r[0].value, r[1].value
    if code is None or text is None: continue
    code, text = str(code).strip(), " ".join(str(text).split())
    if not code: continue
    color = r[0].font.color.rgb if r[0].font and r[0].font.color and r[0].font.color.type == "rgb" else None
    red = isinstance(color, str) and color.upper()[-6:] in ("FF0000", "800000", "C00000")
    rows.append({"code": code, "name": text, "seenIn2019F150Log": red})
by = collections.defaultdict(list)
for x in rows: by[x["code"].upper()].append(x)
dups = []
for k, v in by.items():
    names = sorted({x["name"] for x in v})
    if len(v) > 1:
        dups.append({"code": k, "kind": "conflicting-meanings" if len(names) > 1 else "duplicate", "meanings": names})
    for x in v: x["ambiguous"] = len(names) > 1
meta = {"source": "FORScan Programming Modules Acronyms-Abbreviations.xlsx (community list, 'Updated May-20-2019')",
        "note": "Names/descriptions only. UNVERIFIED community data; does not change any module CAN address.",
        "count": len(rows), "uniqueCodes": len(by), "duplicateOrConflictCount": len(dups)}
json.dump({"meta": meta, "acronyms": rows}, open(f"{out}/acronyms.json", "w"), indent=1, ensure_ascii=False)
json.dump({"meta": meta, "issues": dups}, open(f"{out}/duplicates-report.json", "w"), indent=1, ensure_ascii=False)
with open(f"{out}/acronyms.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f); w.writerow(["code", "name", "seenIn2019F150Log", "ambiguous"])
    for x in rows: w.writerow([x["code"], x["name"], x["seenIn2019F150Log"], x["ambiguous"]])
print(json.dumps(meta)); print(sum(x["seenIn2019F150Log"] for x in rows), "red")
for d in dups[:40]: print(d)
