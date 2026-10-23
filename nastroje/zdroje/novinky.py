"""Vytvorí src/SkVP.App/Zdroje/novinky.json – overené údaje sekcie Novinky (zdroje: oficiálne dokumenty stiahnuté nástrojom zdroje.yml)."""
import json
OVERENE = "2026-10-23"
POP = {"nazov": "Pedagogicko-organizačné pokyny na školský rok 2026/2027 (MŠVVaM SR)", "url": "https://www.minedu.sk/data/att/a96/36583.42ace6.pdf"}
POP_STR = {"nazov": "MŠVVaM SR – Pedagogicko-organizačné pokyny", "url": "https://www.minedu.sk/38037-sk/pedagogicko-organizacne-pokyny/"}
PRAZDNINY = {"nazov": "MŠVVaM SR – Školské prázdniny v šk. roku 2026/2027 (.docx)", "url": "https://www.minedu.sk/data/att/2da/36701.feb2b6.docx"}
PRAZDNINY_STR = {"nazov": "MŠVVaM SR – Termíny prázdnin", "url": "https://www.minedu.sk/terminy-prazdnin/"}
NUCEM_MS = {"nazov": "NÚCEM / NIVaM – Maturita", "url": "https://www2.nucem.sk/sk/merania/narodne-merania/maturita"}
NUCEM_ROK = {"nazov": "NÚCEM / NIVaM – Maturita, školský rok 2026/2027", "url": "https://www2.nucem.sk/sk/merania/narodne-merania/maturita/roky/2026-2027"}
MS_INFO = {"nazov": "NIVaM – Základné informácie k Maturite 2027 (PDF)", "url": "https://www2.nucem.sk/dl/6553/Z%C3%A1kladn%C3%A9%20inform%C3%A1cie%20MS%202027_final.pdf"}
NIVAM_MS = {"nazov": "NIVaM – Maturitné skúšky a cieľové požiadavky", "url": "https://nivam.sk/pre-skoly/maturitne-skusky/"}
NUCEM_O = {"nazov": "NÚCEM / NIVaM – O maturite", "url": "https://www2.nucem.sk/sk/merania/narodne-merania/maturita/o-maturite"}
PRIJ_PDF = {"nazov": "MŠVVaM SR – Termíny prijímacích skúšok na SŠ pre šk. rok 2027/2028 (PDF)", "url": "https://www.minedu.sk/data/att/161/36571.5bc5c8.pdf"}
PRIJ_STR = {"nazov": "MŠVVaM SR – Pre školský rok 2027/2028", "url": "https://www.minedu.sk/pre-skolsky-rok-20272028/"}
EPRIHLASKY = {"nazov": "ePrihlášky (iedu.sk)", "url": "https://eprihlasky.iedu.sk/"}
ZAKON = {"nazov": "Zákon č. 245/2008 Z. z. (školský zákon), znenie od 1. 9. 2026 – Slov-Lex (PDF)", "url": "https://static.slov-lex.sk/pdf/SK/ZZ/2008/245/ZZ_2008_245_20260901.pdf"}
AI = {"nazov": "MŠVVaM SR – AI gramotnosť prichádza na gymnáziá (18. 6. 2026)", "url": "https://www.minedu.sk/ai-gramotnost-prichadza-na-gymnazia-ministerstvo-meni-statny-vzdelavaci-program/"}
STATPEDU = {"nazov": "NIVaM (statpedu.sk) – iŠVP pre gymnáziá so 4- a 5-ročným programom", "url": "https://www.statpedu.sk/sk/svp/inovovany-statny-vzdelavaci-program/inovovany-svp-gymnazia-so-stvorrocnym-patrocnym-vzdelavacim-programom/"}
FAQ = {"nazov": "MŠVVaM SR – Stredné školy: otázky a odpovede k novele", "url": "https://www.minedu.sk/44151-sk/stredne-skoly/"}
T9 = {"nazov": "MŠVVaM SR – Testovanie 9 v školskom roku 2026-2027", "url": "https://www.minedu.sk/testovanie-9-v-skolskom-roku-2026-2027/"}
KURIKULARNA = {"nazov": "MŠVVaM SR – Kurikulárna reforma", "url": "https://www.minedu.sk/kurikularna-reforma/"}

def z(zdroj, *overit):
    d = dict(zdroj)
    if overit: d["overit"] = list(overit)
    return d

kategorie = [
 {"id": "osnovy", "nazov": "Učebné osnovy a ŠVP", "popis": "Štátny vzdelávací program, dodatky a revízia učebných osnov", "farba": "#8E9BE0", "ikona": ""},
 {"id": "tematicke", "nazov": "Tematické plány", "popis": "Čo zohľadniť pri tvorbe a úprave tematických výchovno-vzdelávacích plánov", "farba": "#6CC2B5", "ikona": ""},
 {"id": "ciele", "nazov": "Cieľové požiadavky", "popis": "Cieľové požiadavky na vedomosti a zručnosti maturantov, špecifikácie testov", "farba": "#B49BE3", "ikona": ""},
 {"id": "maturita", "nazov": "Maturitná skúška", "popis": "Termíny, novinky a odporúčania k maturite 2027", "farba": "#EE9A9A", "ikona": ""},
 {"id": "prijimacky", "nazov": "Prijímacie skúšky", "popis": "Prihlášky, prijímacie skúšky a Testovanie 9", "farba": "#F2B27A", "ikona": ""},
 {"id": "skolskyrok", "nazov": "Prázdniny a školský rok", "popis": "Organizácia školského roka, prázdniny a termínovník", "farba": "#8CCB98", "ikona": ""},
]

P = []
def p(id, kat, druh, nazov, text, od=None, do=None, dolezite=False, zdroje=(), poznamka=None, skupina=None, posledny=None, nastup=None):
    x = {"id": id, "kategoria": kat, "druh": druh, "nazov": nazov, "text": text}
    if od: x["od"] = od
    if do: x["do"] = do
    if dolezite: x["dolezite"] = True
    if skupina: x["skupina"] = skupina
    if posledny: x["posledny"] = posledny
    if nastup: x["nastup"] = nastup
    if poznamka: x["poznamka"] = poznamka
    x["zdroje"] = list(zdroje)
    x["overene"] = OVERENE
    P.append(x)

# ---------------- Učebné osnovy a ŠVP
p("svp-dodatok-6", "osnovy", "novinka", "Dodatok č. 6 k ŠVP pre gymnáziá: AI gramotnosť",
  "MŠVVaM SR schválilo dodatok č. 6 k Štátnemu vzdelávaciemu programu pre gymnáziá s účinnosťou od 1. 9. 2026. AI gramotnosť, kritické overovanie výstupov umelej inteligencie a zodpovedné používanie digitálnych nástrojov sa stávajú súčasťou cieľov vzdelávania a profilu absolventa. Gymnáziá so zameraním na matematiku majú ciele rozšírené o systematický rozvoj AI gramotnosti.",
  od="2026-09-01", dolezite=True, zdroje=[z(AI, "dodatok č. 6 k štátnemu vzdelávaciemu programu pre gymnáziá", "účinnosť od 1. septembra 2026")])
p("svp-revizia-osnov-ai", "osnovy", "odporucanie", "Revízia učebných osnov podľa dodatku č. 6",
  "Skontrolujte, či ciele vzdelávania, profil absolventa v ŠkVP a učebné osnovy predmetov (najmä informatiky, matematiky a predmetov rozvíjajúcich digitálnu gramotnosť) zohľadňujú AI gramotnosť: pochopenie princípov AI, jej zodpovedné používanie ako nástroja na učenie, kritické overovanie výstupov a transparentné priznanie miery jej využitia.",
  od="2026-09-01", zdroje=[z(AI, "transparentné priznávanie miery využitia AI")])
p("svp-platny", "osnovy", "novinka", "Platný ŠVP pre gymnáziá v školskom roku 2026/2027",
  "Gymnáziá postupujú podľa Štátneho vzdelávacieho programu pre gymnáziá schváleného pod č. 2015-7846/10840:1-10B0 v znení jeho dodatkov – pre gymnáziá so štvorročným a päťročným vzdelávacím programom a pre gymnáziá s osemročným vzdelávacím programom (vrátane vzdelávacieho štandardu z dejepisu pre gymnáziá s vyučovacím jazykom maďarským).",
  od="2026-09-01", zdroje=[POP, z(STATPEDU, "Dodatok č. 5")])
p("svp-ai-ss-2027", "osnovy", "novinka", "Stredné školy majú učiť o AI systematicky od 2027/2028",
  "Ministerstvo pripravuje ďalšie úpravy vzdelávacích programov (napr. v informatike), aby stredné školy začali systematicky učiť o umelej inteligencii a jej zodpovednom používaní od školského roka 2027/2028. Do konca roka 2026 má tému AI postupne doplniť aj do stredného odborného vzdelávania.",
  od="2027-09-01", zdroje=[z(AI, "od školského roka 2027/2028")])
p("svp-kurikularna-zs", "osnovy", "novinka", "Kurikulárna reforma: ŠVP 2023 povinne v 1. ročníku ZŠ",
  "V školskom roku 2026/2027 všetky základné školy implementujú nový Štátny vzdelávací program pre základné vzdelávanie (ŠVP 2023) – uplatňuje sa povinne od 1. ročníka. Reforma dáva školám väčšiu autonómiu pri tvorbe učebných osnov: obsah je definovaný rámcovo a školy ho konkretizujú vo svojich ŠkVP. Pre gymnáziá s osemročným programom je dôležitá nadväznosť na nové kurikulum.",
  od="2026-09-01", zdroje=[POP, KURIKULARNA])
p("svp-zverejnenie-skvp", "osnovy", "odporucanie", "Nový ŠkVP zverejniť do 60 dní od schválenia",
  "POP 2026/2027 pri kurikulárnej reforme uvádza, že nový školský vzdelávací program škola zverejní na webovom sídle určenom ministerstvom školstva do 60 dní od jeho schválenia. Po revízii učebných osnov si overte aktuálne povinnosti zverejnenia ŠkVP pre vašu školu.",
  zdroje=[POP])

# ---------------- Tematické plány
p("tvvp-ai", "tematicke", "odporucanie", "Doplňte AI gramotnosť do tematických plánov",
  "Tematické výchovno-vzdelávacie plány vychádzajú z učebných osnov ŠkVP. Po úprave osnov podľa dodatku č. 6 k ŠVP pre gymnáziá (účinnosť od 1. 9. 2026) doplňte do plánov dotknutých predmetov témy a aktivity rozvíjajúce AI gramotnosť a kritické overovanie digitálnych výstupov.",
  od="2026-09-01", zdroje=[z(AI, "dodatok č. 6")])
p("tvvp-prazdniny", "tematicke", "odporucanie", "Rozvrhnite celky podľa prázdnin 2026/2027",
  "Pri rozvrhnutí tematických celkov rátajte s prázdninami podľa POP 2026/2027: jesenné 29. – 30. 10. 2026, vianočné 23. 12. 2026 – 7. 1. 2027, polročné 1. 2. 2027, jarné podľa kraja (15. 2. – 5. 3. 2027), veľkonočné 25. – 30. 3. 2027. Vyučovanie sa končí 30. 6. 2027.",
  zdroje=[POP, PRAZDNINY])
p("tvvp-maturitne-rocniky", "tematicke", "odporucanie", "Maturitné ročníky: plány zosúlaďte s cieľovými požiadavkami",
  "EČ a PFIČ maturitnej skúšky sa uskutočňujú podľa vzdelávacích štandardov ŠVP a cieľových požiadaviek na maturitné skúšky. V posledných ročníkoch zosúlaďte tematické plány s katalógom cieľových požiadaviek a naplánujte opakovanie pred písomnou maturitou 9. – 12. 3. 2027 (pri eMaturite aj generálna skúška 9. – 10. 2. 2027).",
  zdroje=[POP, z(NIVAM_MS, "Cieľové požiadavky na maturitné skúšky")])
p("tvvp-specifikacie", "tematicke", "odporucanie", "Využite špecifikácie testov EČ a PFIČ MS 2027",
  "NIVaM zverejňuje špecifikácie testov EČ MS a tém a zadaní PFIČ MS pre školský rok 2026/2027 (NÚCEM → Maturita → Dokumenty na stiahnutie → Dokumenty Maturita 2027). Sú vhodným podkladom pre tematické plány predmetov s externou časťou maturity.",
  zdroje=[z(NUCEM_MS, "Špecifikácie testov EČ a PFIČ MS v školskom roku 2026/2027"), MS_INFO])

# ---------------- Cieľové požiadavky
p("ciele-katalog", "ciele", "novinka", "Platný katalóg cieľových požiadaviek na maturitnú skúšku",
  "Katalóg cieľových požiadaviek definuje požiadavky na vedomosti a zručnosti maturantov z jednotlivých predmetov maturitnej skúšky. Platný katalóg nadobudol platnosť od 1. 9. 2018 (nahradil katalóg z 1. 9. 2013); Dodatok č. 1 s platnosťou od 1. 9. 2019 obsahuje podrobnosti o ústnej forme internej časti MS. Katalógy pre jednotlivé predmety zverejňuje NIVaM.",
  dolezite=True, zdroje=[z(NIVAM_MS, "nadobudol platnosť od 1. 9. 2018", "Dodatok č. 1"), z(NUCEM_O, "Cieľových požiadaviek na maturanta")])
p("ciele-ms-obsah", "ciele", "novinka", "Maturita vychádza z cieľových požiadaviek",
  "Maturitná skúška po obsahovej stránke vychádza z cieľových požiadaviek na maturanta; EČ a PFIČ MS sa uskutočňujú podľa vzdelávacích štandardov ŠVP pre jednotlivé skupiny stredných škôl a cieľových požiadaviek na maturitné skúšky.",
  zdroje=[z(NIVAM_MS, "Maturitná skúška po obsahovej stránke vychádza z Cieľových požiadaviek na maturanta"), POP])
p("ciele-cudzie-jazyky", "ciele", "odporucanie", "Cudzie jazyky: cieľové požiadavky podľa úrovne",
  "Pre cudzie jazyky NIVaM zverejňuje cieľové požiadavky podľa referenčných úrovní (B1, B2, C1). Pri revízii učebných osnov a tematických plánov cudzích jazykov skontrolujte, ktorá úroveň sa na vašej škole maturuje.",
  zdroje=[z(NIVAM_MS, "referenčná úroveň C1")])
p("ciele-specifikacie-2027", "ciele", "novinka", "Špecifikácie testov EČ a PFIČ MS 2026/2027",
  "Podrobné špecifikácie testov EČ MS, tém a zadaní PFIČ MS sú zverejnené na stránke NÚCEM v časti Dokumenty na stiahnutie – Dokumenty Maturita 2027.",
  zdroje=[z(NUCEM_MS, "Špecifikácie testov"), MS_INFO])

# ---------------- Maturita
p("ms-zber-udajov", "maturita", "termin", "Zber údajov o maturantoch (IS EČ a PFIČ MS)",
  "Riaditeľ školy nahlási v Informačnom systéme CVTI SR-ŠVS Banská Bystrica údaje o všetkých žiakoch maturujúcich z predmetov s EČ a/alebo PFIČ MS vrátane žiakov prihlásených na eMaturitu.",
  od="2026-10-01", do="2026-10-31", dolezite=True,
  poznamka="Zdroje sa líšia v poslednom dni: NIVaM (Základné informácie, stránka NÚCEM) uvádza 1. – 31. 10. 2026, POP 2026/2027 uvádza 1. – 30. 10. 2026. Odporúčame dokončiť zber do 30. 10. 2026.",
  zdroje=[z(NUCEM_ROK, "od 1. do 31. októbra 2026"), MS_INFO, POP])
p("ms-zmena-eformy", "maturita", "termin", "Zmena výberu elektronickej formy EČ MS",
  "Školy môžu v prípade potreby zmeniť výber elektronickej formy testovania žiakov zo SJL, MJL a MAT.",
  do="2026-11-13", zdroje=[MS_INFO])
p("ms-zmeny-databazy", "maturita", "termin", "Nahlasovanie zmien v databáze maturantov",
  "Zmeny v databáze škôl a žiakov prihlásených na EČ a PFIČ MS 2027 je možné nahlasovať iba v odôvodnených prípadoch a v súlade s platnou legislatívou.",
  od="2026-11-20", do="2027-01-31", zdroje=[z(NUCEM_ROK, "od 20. 11. 2026 do 31. 1. 2027")])
p("ms-gs-sjl", "maturita", "termin", "Generálna skúška eMaturity – SJL, MJL",
  "Generálna skúška eMaturity 2027 zo slovenského jazyka a literatúry, resp. maďarského jazyka a literatúry. Povinne ju absolvujú všetci maturanti prihlásení na elektronickú formu EČ MS z daného predmetu.",
  od="2027-02-09", zdroje=[MS_INFO])
p("ms-gs-mat", "maturita", "termin", "Generálna skúška eMaturity – matematika",
  "Generálna skúška eMaturity 2027 z matematiky (povinná pre žiakov prihlásených na elektronickú formu EČ MS z MAT). Výsledky GS zašle NIVaM školám najneskôr do 3. 3. 2027.",
  od="2027-02-10", zdroje=[MS_INFO])
p("ms-ec-sjl", "maturita", "termin", "EČ a PFIČ MS – slovenský jazyk a literatúra, maďarský jazyk a literatúra",
  "Riadny termín externej časti a písomnej formy internej časti maturitnej skúšky (aj eMaturita zo SJL a MJL).",
  od="2027-03-09", dolezite=True, zdroje=[z(NUCEM_MS, "9. marec 2027 (utorok) – slovenský jazyk a literatúra"), POP])
p("ms-ec-cj", "maturita", "termin", "EČ a PFIČ MS – cudzie jazyky",
  "Riadny termín EČ a PFIČ MS z anglického, francúzskeho, nemeckého, ruského, španielskeho a talianskeho jazyka.",
  od="2027-03-10", dolezite=True, zdroje=[z(NUCEM_MS, "10. marec 2027 (streda) – anglický"), POP])
p("ms-ec-mat", "maturita", "termin", "EČ MS – matematika",
  "Riadny termín externej časti maturitnej skúšky z matematiky (aj eMaturita z MAT).",
  od="2027-03-11", dolezite=True, zdroje=[z(NUCEM_MS, "11. marec 2027 (štvrtok) – matematika"), POP])
p("ms-ec-sjsl", "maturita", "termin", "EČ a PFIČ MS – slovenský jazyk a slovenská literatúra, ukrajinský jazyk a literatúra",
  "Riadny termín EČ a PFIČ MS pre školy s vyučovacím jazykom národnostných menšín.",
  od="2027-03-12", zdroje=[z(NUCEM_MS, "12. marec 2027 (piatok) – slovenský jazyk a slovenská literatúra"), POP])
p("ms-nahradny-april", "maturita", "termin", "Náhradný termín EČ a PFIČ MS (apríl)",
  "Aprílový náhradný termín externej časti a písomnej formy internej časti maturitnej skúšky.",
  od="2027-04-06", do="2027-04-09", zdroje=[z(NUCEM_MS, "9. apríla 2027"), POP])
p("ms-opravny-prihlaska", "maturita", "termin", "Prihlásenie na septembrový opravný termín",
  "Na opravnú skúšku EČ a PFIČ MS v septembri sa žiak prihlási riaditeľovi školy do 30. júna (školský zákon, § 77 ods. 7); ak chce opravnú skúšku konať v marci, do 30. septembra nasledujúceho školského roka.",
  do="2027-06-30", zdroje=[MS_INFO])
p("ms-opravny-dni", "maturita", "termin", "Zverejnenie dní opravného termínu",
  "Dni konania opravného termínu pre jednotlivé predmety určí NIVaM podľa počtu prihlásených žiakov a zverejní ich najneskôr 1. augusta 2027.",
  do="2027-08-01", zdroje=[POP])
p("ms-september", "maturita", "termin", "Opravný a náhradný termín EČ a PFIČ MS (september)",
  "Septembrový opravný a náhradný termín EČ a PFIČ MS pre školský rok 2026/2027.",
  od="2027-09-02", do="2027-09-07", zdroje=[z(NUCEM_MS, "7. septembra 2027"), POP])
p("ms-ematurita", "maturita", "novinka", "eMaturita 2027 zo SJL, MJL a matematiky",
  "Elektronickú formu EČ MS si žiaci volia dobrovoľne, z jedného alebo z oboch predmetov. Školy sú povinné umožniť ju, ak to dovoľujú ich technické, priestorové a personálne kapacity. Testy sú obsahovo totožné s papierovými, EČ MS zo SJL/MJL elektronicky trvá 110 minút a z MAT 160 minút. Ostatné predmety sa v 2026/2027 píšu iba papierovo.",
  dolezite=True, zdroje=[MS_INFO, z(NUCEM_ROK, "eMaturitu")])
p("ms-rozsah-predmetov", "maturita", "novinka", "Nový rozsah maturitných predmetov až pre žiakov od 1. 9. 2027",
  "Rozsah predmetov maturitnej skúšky podľa predpisov účinných od 1. januára 2026 (napr. povinná maturita z matematiky v študijných odboroch zo zoznamu ministerstva) sa nevzťahuje na žiakov, ktorí začnú štúdium pred 1. septembrom 2027. Úroveň maturity z matematiky určí pripravovaná novela vyhlášky o strednej škole č. 224/2022 Z. z.",
  dolezite=True, zdroje=[z(FAQ, "nevzťahuje na žiakov, ktorí začnú štúdium pred 1. septembrom 2027")])
p("ms-dodatocna", "maturita", "odporucanie", "Dodatočná maturitná skúška",
  "Do troch rokov od úspešnej maturity možno vykonať dodatočnú MS najviac z dvoch ďalších predmetov; žiadosť sa podáva orgánu miestnej štátnej správy v školstve podľa trvalého pobytu do 30. septembra školského roka, v ktorom ju chce uchádzač vykonať.",
  zdroje=[MS_INFO])

# ---------------- Prijímacie skúšky
p("ps-zverejnenie-ss", "prijimacky", "termin", "SŠ zverejní počty prijímaných žiakov a termíny skúšok",
  "Riaditeľ strednej školy zverejní na webovom sídle školy počet žiakov, ktorých možno prijať do prvého ročníka, a termíny prijímacích skúšok do 7. februára (školský zákon, § 65 ods. 1).",
  do="2027-02-07", zdroje=[ZAKON])
p("ps-prihlasky", "prijimacky", "termin", "Podávanie prihlášok na stredné školy (1. kolo)",
  "Zákonný zástupca podáva prihlášku na vzdelávanie (elektronicky cez ePrihlášky alebo listinne) riaditeľovi základnej školy do 20. februára. V 1. kole možno uviesť najviac dva talentové a dva netalentové odbory v poradí podľa záujmu; prijímanie na osemročné gymnáziá je iba v 1. kole.",
  od="2027-02-08", do="2027-02-20", dolezite=True,
  zdroje=[z(EPRIHLASKY, "8. február - 20. február 2027"), ZAKON])
p("ps-talentove-1", "prijimacky", "termin", "Prijímacie skúšky – talentové odbory, 1. termín",
  "Prvý termín prijímacích skúšok pre odbory, v ktorých sa vyžaduje overenie špeciálnych schopností, zručností alebo nadania; podľa počtu uchádzačov aj 23. a 24. 3. 2027.",
  od="2027-03-22", do="2027-03-24", zdroje=[PRIJ_PDF, z(PRIJ_STR, "2027/2028")])
p("ps-sportove", "prijimacky", "termin", "Overenie športového nadania (stredné športové školy)",
  "Overenie športového nadania pre všetky druhy športov od 15. do 31. marca 2027, okrem 17. a 18. marca a veľkonočných prázdnin.",
  od="2027-03-15", do="2027-03-31", zdroje=[PRIJ_PDF])
p("ps-talentove-2", "prijimacky", "termin", "Prijímacie skúšky – talentové odbory, 2. termín",
  "Druhý termín prijímacích skúšok pre talentové odbory; podľa počtu uchádzačov aj 1. a 2. 4. 2027.",
  od="2027-03-31", do="2027-04-02", zdroje=[PRIJ_PDF])
p("ps-netalentove-1", "prijimacky", "termin", "Prijímacie skúšky na gymnáziá a SOŠ – 1. termín",
  "Prvý termín prijímacích skúšok pre odbory bez overenia špeciálnych schopností (gymnáziá, SOŠ, SPŠ); podľa počtu uchádzačov aj 4. 5. 2027.",
  od="2027-05-03", do="2027-05-04", dolezite=True, zdroje=[PRIJ_PDF])
p("ps-netalentove-2", "prijimacky", "termin", "Prijímacie skúšky na gymnáziá a SOŠ – 2. termín",
  "Druhý termín prijímacích skúšok pre netalentové odbory; podľa počtu uchádzačov aj 11. 5. 2027.",
  od="2027-05-10", do="2027-05-11", dolezite=True, zdroje=[PRIJ_PDF])
p("ps-dalsi-termin", "prijimacky", "termin", "Ďalší termín prijímacej skúšky (nenaplnené miesta)",
  "Ďalší termín prijímacej skúšky pre nenaplnený počet miest (okrem osemročných gymnázií) je 22. 6. 2027; z organizačných dôvodov sa môže skončiť 23. 6. 2027.",
  od="2027-06-22", do="2027-06-23", zdroje=[PRIJ_PDF])
p("ps-zverejnenie-vysledkov", "prijimacky", "odporucanie", "Zoznam uchádzačov = rozhodnutie o prijatí",
  "Stredná škola zverejní zoznam uchádzačov podľa výsledkov prijímacieho konania cez ePrihlášky, na svojom webovom sídle a na verejne prístupnom mieste školy. Informácia o prijatí v zozname sa považuje za rozhodnutie o prijatí a deň zverejnenia za deň jeho doručenia.",
  zdroje=[PRIJ_PDF])
p("ps-jednotna-skuska", "prijimacky", "novinka", "Jednotná prijímacia skúška najskôr pre 2028/2029",
  "Jednotnú prijímaciu skúšku na stredné školy podľa predpisov účinných od 1. januára 2026 vykonajú žiaci najskôr v prijímacom konaní na školský rok 2028/2029. Prijímacie skúšky na školské roky 2026/2027 a 2027/2028 sa vykonajú podľa predpisov účinných do 31. decembra 2025 (vrátane prijatia bez prijímacej skúšky podľa § 65 ods. 5).",
  dolezite=True, zdroje=[z(FAQ, "najskôr v rámci prijímacieho konania na školský rok 2028/2029")])
p("ps-poradie-odborov", "prijimacky", "novinka", "Poradie preferencie odborov na prihláške",
  "Elektronická aj listinná prihláška na strednú školu obsahuje poradie preferencie odborov vzdelávania; uchádzač môže byť prijatý len na jeden odbor podľa poradia uvedeného na prihláške.",
  zdroje=[z(FAQ, "Poradie preferencie odborov vzdelávania"), ZAKON])
p("t9-prihlasovanie", "prijimacky", "termin", "Prihlasovanie žiakov na Testovanie 9",
  "Prihlasovanie žiakov 9. ročníka ZŠ na Testovanie 9 2027.",
  od="2026-11-16", do="2026-11-30", zdroje=[z(T9, "16. – 30. november 2026"), POP])
p("t9-riadny", "prijimacky", "termin", "Testovanie 9 – riadny termín",
  "Externé testovanie žiakov 9. ročníka ZŠ z matematiky a vyučovacieho jazyka (17. 3.) a zo slovenského jazyka a slovenskej literatúry na školách s VJM (18. 3.). V rovnakom termíne je externé testovanie na účel získania nižšieho stredného vzdelania.",
  od="2027-03-17", do="2027-03-18", dolezite=True, zdroje=[z(T9, "17. a 18. marca 2027"), POP])
p("t9-nahradny", "prijimacky", "termin", "Testovanie 9 – náhradný termín",
  "Náhradný termín Testovania 9 2027.",
  od="2027-03-31", do="2027-04-01", zdroje=[z(T9, "31. marca a 1. apríla 2027"), POP])

# ---------------- Prázdniny a školský rok
p("sr-zaciatok", "skolskyrok", "termin", "Začiatok školského vyučovania 2026/2027",
  "Obdobie školského vyučovania sa začína 1. 9. 2026 (utorok) a končí sa 30. 6. 2027 (streda).",
  od="2026-09-01", zdroje=[POP])
p("pr-jesenne", "skolskyrok", "termin", "Jesenné prázdniny",
  "Posledný deň vyučovania 28. 10. 2026 (streda), vyučovanie sa začína 2. 11. 2026 (pondelok).",
  od="2026-10-29", do="2026-10-30", dolezite=True, posledny="2026-10-28", nastup="2026-11-02", zdroje=[POP, PRAZDNINY])
p("pr-vianocne", "skolskyrok", "termin", "Vianočné prázdniny",
  "Posledný deň vyučovania 22. 12. 2026 (utorok), vyučovanie sa začína 8. 1. 2027 (piatok).",
  od="2026-12-23", do="2027-01-07", dolezite=True, posledny="2026-12-22", nastup="2027-01-08", zdroje=[POP, PRAZDNINY])
p("sr-polrok", "skolskyrok", "termin", "Koniec prvého polroka",
  "Vyučovanie v prvom polroku sa končí 29. 1. 2027 (piatok); druhý polrok sa začína 2. 2. 2027 (utorok).",
  od="2027-01-29", zdroje=[POP])
p("pr-polrocne", "skolskyrok", "termin", "Polročné prázdniny",
  "Posledný deň vyučovania 29. 1. 2027 (piatok), vyučovanie sa začína 2. 2. 2027 (utorok).",
  od="2027-02-01", posledny="2027-01-29", nastup="2027-02-02", zdroje=[POP, PRAZDNINY])
p("pr-jarne-bb", "skolskyrok", "termin", "Jarné prázdniny – Banskobystrický, Žilinský, Trenčiansky kraj",
  "Posledný deň vyučovania 12. 2. 2027 (piatok), vyučovanie sa začína 22. 2. 2027 (pondelok).",
  od="2027-02-15", do="2027-02-19", skupina="BB, ZA, TN",
  poznamka="Tabuľka v .docx na stránke ministerstva uvádza pri tomto termíne len BB a ZA kraj; Trenčiansky kraj je uvedený v POP 2026/2027 (Tabuľka 1).",
  posledny="2027-02-12", nastup="2027-02-22", zdroje=[POP, PRAZDNINY])
p("pr-jarne-ke", "skolskyrok", "termin", "Jarné prázdniny – Košický, Prešovský kraj",
  "Posledný deň vyučovania 19. 2. 2027 (piatok), vyučovanie sa začína 1. 3. 2027 (pondelok).",
  od="2027-02-22", do="2027-02-26", skupina="KE, PO", posledny="2027-02-19", nastup="2027-03-01", zdroje=[POP, PRAZDNINY])
p("pr-jarne-ba", "skolskyrok", "termin", "Jarné prázdniny – Bratislavský, Nitriansky, Trnavský kraj",
  "Posledný deň vyučovania 26. 2. 2027 (piatok), vyučovanie sa začína 8. 3. 2027 (pondelok).",
  od="2027-03-01", do="2027-03-05", skupina="BA, NR, TT", posledny="2027-02-26", nastup="2027-03-08", zdroje=[POP, PRAZDNINY])
p("pr-velkonocne", "skolskyrok", "termin", "Veľkonočné prázdniny",
  "Posledný deň vyučovania 24. 3. 2027 (streda), vyučovanie sa začína 31. 3. 2027 (streda).",
  od="2027-03-25", do="2027-03-30", dolezite=True, posledny="2027-03-24", nastup="2027-03-31", zdroje=[POP, PRAZDNINY])
p("sr-zaverecna", "skolskyrok", "termin", "Záverečné a absolventské skúšky",
  "Riadny termín skúšobného obdobia: absolventská skúška 1. – 25. 6. 2027, záverečná skúška 7. – 25. 6. 2027 (stredné odborné školy a konzervatóriá).",
  od="2027-06-01", do="2027-06-25", zdroje=[POP])
p("sr-koniec", "skolskyrok", "termin", "Koniec školského vyučovania 2026/2027",
  "Vyučovanie v druhom polroku sa končí 30. 6. 2027 (streda).",
  od="2027-06-30", zdroje=[POP])
p("pr-letne", "skolskyrok", "termin", "Letné prázdniny",
  "Posledný deň vyučovania 30. 6. 2027 (streda), vyučovanie v školskom roku 2027/2028 sa začína 1. 9. 2027 (streda).",
  od="2027-07-01", do="2027-08-31", dolezite=True, posledny="2027-06-30", nastup="2027-09-01", zdroje=[POP, PRAZDNINY])
# 2027/2028 (POP, Príloha 1)
p("pr27-jesenne", "skolskyrok", "termin", "Jesenné prázdniny 2027/2028",
  "Posledný deň vyučovania 27. 10. 2027 (streda), vyučovanie sa začína 2. 11. 2027 (utorok).",
  od="2027-10-28", do="2027-10-29", posledny="2027-10-27", nastup="2027-11-02", zdroje=[POP])
p("pr27-vianocne", "skolskyrok", "termin", "Vianočné prázdniny 2027/2028",
  "Posledný deň vyučovania 22. 12. 2027 (streda), vyučovanie sa začína 10. 1. 2028 (pondelok).",
  od="2027-12-23", do="2028-01-07", posledny="2027-12-22", nastup="2028-01-10", zdroje=[POP])
p("pr27-polrocne", "skolskyrok", "termin", "Polročné prázdniny 2027/2028",
  "Posledný deň vyučovania 3. 2. 2028 (štvrtok), vyučovanie sa začína 7. 2. 2028 (pondelok).",
  od="2028-02-04", posledny="2028-02-03", nastup="2028-02-07", zdroje=[POP])
p("pr27-jarne-ke", "skolskyrok", "termin", "Jarné prázdniny 2027/2028 – Košický, Prešovský kraj",
  "Posledný deň vyučovania 18. 2. 2028, vyučovanie sa začína 28. 2. 2028.", od="2028-02-21", do="2028-02-25", skupina="KE, PO", posledny="2028-02-18", nastup="2028-02-28", zdroje=[POP])
p("pr27-jarne-ba", "skolskyrok", "termin", "Jarné prázdniny 2027/2028 – Bratislavský, Nitriansky, Trnavský kraj",
  "Posledný deň vyučovania 25. 2. 2028, vyučovanie sa začína 6. 3. 2028.", od="2028-02-28", do="2028-03-03", skupina="BA, NR, TT", posledny="2028-02-25", nastup="2028-03-06", zdroje=[POP])
p("pr27-jarne-bb", "skolskyrok", "termin", "Jarné prázdniny 2027/2028 – Banskobystrický, Žilinský, Trenčiansky kraj",
  "Posledný deň vyučovania 3. 3. 2028, vyučovanie sa začína 13. 3. 2028.", od="2028-03-06", do="2028-03-10", skupina="BB, ZA, TN", posledny="2028-03-03", nastup="2028-03-13", zdroje=[POP])
p("pr27-velkonocne", "skolskyrok", "termin", "Veľkonočné prázdniny 2027/2028",
  "Posledný deň vyučovania 12. 4. 2028 (streda), vyučovanie sa začína 19. 4. 2028 (streda).", od="2028-04-13", do="2028-04-18", posledny="2028-04-12", nastup="2028-04-19", zdroje=[POP])
p("pr27-letne", "skolskyrok", "termin", "Letné prázdniny 2028",
  "Posledný deň vyučovania 30. 6. 2028 (piatok), vyučovanie sa začína 4. 9. 2028 (pondelok).", od="2028-07-03", do="2028-09-01", posledny="2028-06-30", nastup="2028-09-04", zdroje=[POP])
p("pr28-prehlad", "skolskyrok", "novinka", "Prázdniny v školskom roku 2028/2029",
  "Jesenné 30. – 31. 10. 2028; vianočné do 5. 1. 2029 (vyučovanie od 8. 1. 2029); polročné 2. 2. 2029; jarné BA, NR, TT 19. – 23. 2. 2029, BB, ZA, TN 26. 2. – 2. 3. 2029, KE, PO 5. – 9. 3. 2029; veľkonočné 29. 3. – 3. 4. 2029; letné 2. 7. – 31. 8. 2029.",
  od="2028-10-30",
  poznamka="V POP 2026/2027 (Príloha 1) je pri vianočných prázdninách 2028/2029 zjavný preklep v dátume začiatku („27. január 2027“, posledný deň vyučovania 22. 12. 2028). Overte si termín, keď ministerstvo zverejní pokyny na školský rok 2028/2029.",
  zdroje=[POP])

stranky = [
 {"nazov": "Ministerstvo školstva, výskumu, vývoja a mládeže SR", "url": "https://www.minedu.sk/", "popis": "Pedagogicko-organizačné pokyny, termíny prázdnin, prijímacie konanie, štátne vzdelávacie programy a tlačové správy.", "rss": "https://www.minedu.sk/rss/", "kategorie": ["osnovy", "prijimacky", "skolskyrok"]},
 {"nazov": "Národný inštitút vzdelávania a mládeže (NIVaM)", "url": "https://nivam.sk/", "popis": "Metodická podpora, cieľové požiadavky, maturitné skúšky, olympiády a súťaže.", "rss": "https://nivam.sk/feed/", "kategorie": ["ciele", "maturita", "tematicke"]},
 {"nazov": "NÚCEM – národné merania (NIVaM)", "url": "https://www2.nucem.sk/", "popis": "Maturita, Testovanie 9, špecifikácie testov, výsledky a dokumenty na stiahnutie.", "kategorie": ["maturita", "prijimacky", "ciele"]},
 {"nazov": "Štátne vzdelávacie programy (statpedu.sk)", "url": "https://www.statpedu.sk/", "popis": "Inovovaný ŠVP pre gymnáziá, dodatky a vzdelávacie štandardy.", "kategorie": ["osnovy", "tematicke"]},
 {"nazov": "Kurikulárna reforma", "url": "https://reforma.iedu.sk/", "popis": "Nový ŠVP, sprievodné materiály a podpora škôl pri revízii ŠkVP.", "kategorie": ["osnovy", "tematicke"]},
 {"nazov": "ePrihlášky", "url": "https://eprihlasky.iedu.sk/", "popis": "Elektronické prihlášky do materských, základných a stredných škôl, výsledky prijímacieho konania.", "kategorie": ["prijimacky"]},
 {"nazov": "Maturita – zber údajov (CVTI SR-ŠVS Banská Bystrica)", "url": "https://maturita.svsbb.sk/", "popis": "Informačný systém pre prihlasovanie maturantov na EČ a PFIČ MS.", "kategorie": ["maturita"]},
 {"nazov": "Slov-Lex – právne predpisy", "url": "https://www.slov-lex.sk/", "popis": "Platné znenie školského zákona č. 245/2008 Z. z., vyhlášky o strednej škole č. 224/2022 Z. z. a vyhlášky č. 231/2009 Z. z.", "kategorie": ["osnovy", "maturita", "prijimacky", "skolskyrok"]},
 {"nazov": "Centrum vedecko-technických informácií SR", "url": "https://www.cvtisr.sk/", "popis": "Rezortné informačné systémy a štatistiky školstva.", "kategorie": ["skolskyrok"]},
 {"nazov": "Ústredný portál verejnej správy", "url": "https://www.slovensko.sk/", "popis": "Životné situácie – prihláška na strednú školu, elektronické schránky.", "kategorie": ["prijimacky"]},
]

data = {"verzia": 1, "aktualizovane": OVERENE, "skolskyRok": "2026/2027",
        "popis": "Overené informácie z oficiálnych stránok ministerstva školstva a jeho organizácií. Médiá ani noviny sa nepoužívajú.",
        "kategorie": kategorie, "polozky": P, "stranky": stranky}
import sys
json.dump(data, open(sys.argv[1], "w", encoding="utf-8"), ensure_ascii=False, indent=2)
print(len(P), "položiek")
