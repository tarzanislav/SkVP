"""Zostaví testovaciu sadu učebných osnov do src/SkVP.App/Zdroje/testovacia_sada.json.

Spustenie: python3 nastroje/testovacia_sada/zostav.py
"""
import json
import os
import sys

TU = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, TU)

import spolocne  # noqa: E402
import oblast1_jazyk, oblast2_matematika, oblast3_priroda, oblast4_spolocnost, oblast5_7  # noqa: E402,F401

plan = json.load(open(os.path.join(TU, "..", "..", "src", "SkVP.App", "Zdroje", "ucebny_plan_gymnazium.json"), encoding="utf-8"))
nazvy_planu = [p for o in plan["oblasti"] for p in o["predmety"]]
nazvy = [p["nazov"] for p in spolocne.PREDMETY]

chyby = []
for n in nazvy_planu:
    if n not in nazvy:
        chyby.append(f"chýba osnova: {n}")
for n in nazvy:
    if n not in nazvy_planu:
        chyby.append(f"predmet nie je v učebnom pláne: {n}")
    if nazvy.count(n) > 1:
        chyby.append(f"duplicitný predmet: {n}")
for p in spolocne.PREDMETY:
    oblast = next((o for o in plan["oblasti"] if p["nazov"] in o["predmety"]), None)
    if oblast and oblast["nazov"] != p["oblast"]:
        chyby.append(f"{p['nazov']}: oblasť {p['oblast']} ≠ {oblast['nazov']}")
    for s in p["suvisi"]:
        if s["predmet"] not in nazvy:
            chyby.append(f"{p['nazov']}: súvislosť s neznámym predmetom {s['predmet']}")
for o in plan["oblasti"]:
    if o["nazov"] not in spolocne.OBLASTI:
        chyby.append(f"chýba charakteristika oblasti: {o['nazov']}")
if chyby:
    print("\n".join(chyby))
    sys.exit(1)

vystup = {
    "popis": "Testovacia sada učebných osnov – ukážkové osnovy podľa ŠVP pre gymnáziá a návrhy osnov školských predmetov. "
             "Slúži na vyskúšanie programu, nie je to oficiálny školský vzdelávací program.",
    "oblasti": [{"nazov": k, "charakteristika": v} for k, v in spolocne.OBLASTI.items()],
    "predmety": spolocne.PREDMETY,
}
ciel = os.path.join(TU, "..", "..", "src", "SkVP.App", "Zdroje", "testovacia_sada.json")
with open(ciel, "w", encoding="utf-8") as f:
    json.dump(vystup, f, ensure_ascii=False, indent=1)
celky = sum(len(r["celky"]) for p in spolocne.PREDMETY for r in p["rocniky"])
print(f"{len(spolocne.PREDMETY)} predmetov, {celky} tematických celkov, {os.path.getsize(ciel) // 1024} kB")
