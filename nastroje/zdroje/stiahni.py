"""Stiahne oficiálne dokumenty zo zoznam.txt a stránky citaty-slavnych.sk, prevedie ich na text (pre prípravu dát)."""
import os, re, sys, time, subprocess, urllib.request, json

ciel = sys.argv[1]
os.makedirs(ciel, exist_ok=True)
hlavicky = {"User-Agent": "SkVP-zdroje/1.0 (+https://github.com/tarzanislav/SkVP)"}

def stiahni(url, cesta):
    try:
        with urllib.request.urlopen(urllib.request.Request(url, headers=hlavicky), timeout=60) as r:
            data = r.read()
        with open(cesta, "wb") as f:
            f.write(data)
        return True
    except Exception as e:
        print("CHYBA", url, e)
        return False

def text(cesta):
    if cesta.endswith(".pdf"):
        subprocess.run(["pdftotext", "-layout", cesta, cesta + ".txt"])
    elif cesta.endswith(".docx"):
        import docx
        d = docx.Document(cesta)
        riadky = [p.text for p in d.paragraphs]
        for t in d.tables:
            for r in t.rows:
                riadky.append(" | ".join(c.text.strip() for c in r.cells))
        open(cesta + ".txt", "w", encoding="utf-8").write("\n".join(riadky))
    elif cesta.endswith(".xml"):
        open(cesta + ".txt", "wb").write(open(cesta, "rb").read())
    elif cesta.endswith(".html"):
        from bs4 import BeautifulSoup
        s = BeautifulSoup(open(cesta, "rb").read(), "html.parser")
        for x in s(["script", "style", "noscript"]):
            x.decompose()
        odkazy = [f"{a.get_text(' ', strip=True)} -> {a['href']}" for a in s.find_all("a", href=True)]
        open(cesta + ".txt", "w", encoding="utf-8").write(s.get_text("\n", strip=True) + "\n\n=== ODKAZY ===\n" + "\n".join(odkazy))

for riadok in open(os.path.join(os.path.dirname(__file__), "zoznam.txt"), encoding="utf-8"):
    riadok = riadok.strip()
    if not riadok or riadok.startswith("#"):
        continue
    nazov, url = riadok.split("|", 1)
    cesta = os.path.join(ciel, nazov)
    if stiahni(url, cesta):
        text(cesta)
    time.sleep(1)

if os.environ.get("BEZ_CITATOV"):
    print("hotovo (bez citátov)")
    sys.exit(0)

# citáty: stránky zoznamu (20 na stranu), slušne s odstupom
citaty = os.path.join(ciel, "citaty")
os.makedirs(citaty, exist_ok=True)
for strana in range(1, 121):
    url = "https://citaty-slavnych.sk/citaty/" + ("" if strana == 1 else f"?page={strana}")
    cesta = os.path.join(citaty, f"strana_{strana:03}.html")
    if not stiahni(url, cesta):
        break
    if strana > 1 and b"?page=" + str(strana + 1).encode() not in open(cesta, "rb").read() and strana > 105:
        break
    time.sleep(1.5)
print("hotovo")
