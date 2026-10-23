# SkVP – Kalendár, Úlohy, Poznámky: analýza a návrh implementácie

24. 10. 2026 · východisko: EviTA (`main` 7734ce6), SkVP v0.1.85

## 1. Zadanie
- Tri nové sekcie podľa EviTA: **Kalendár**, **Úlohy**, **Poznámky**.
- Musia byť prepojené s časťami SkVP: osnovy, vzdelávacie oblasti, predmety, úvodné a záverečné časti, Novinky,
  dashboard, tlač, import a export, archív, servis a nastavenia.
- Umiestnenie v ľavom menu: **pod Osnovami** (za zoznamom oblastí a záverečnými časťami), **nad Tlačou a zostavami**:

```
Dashboard
Novinky
Osnovy              (počet predmetov)
   Úvodné časti …
   oblasti …
   Záverečné časti …
Kalendár            (dnes: počet udalostí)
Úlohy               (otvorené / po termíne – červený odznak)
Poznámky            (počet)
Tlač a zostavy
Import a export
Archív
Servis
```

## 2. Ako to funguje v EviTA (čo prevziať)

| Sekcia | Čo vie EviTA | Kód (EviTA) |
|---|---|---|
| Kalendár | mesiac / týždeň / zoznam (60 dní); udalosti a úlohy s typom a farbou; celý deň aj viac dní; opakovanie (denne, týždenne, mesačne, ročne, do dátumu); pripomienka (v deň, deň, týždeň vopred); stav (naplánované, hotovo, zrušené); naviazanie na položku, miestnosť, osobu; **automatické termíny** z evidencie (záruky, revízie, zálohy, inventúra); meniny a sviatky (Kalendárium); poznámky k dňu; typy udalostí (paleta); tlač (mesačná mriežka, zoznam); export .ics (RRULE, VALARM); prehľad dňa pri spustení; upozornenia | `KalendarService` (793 r.), `KalendarViewModel`, `UdalostEditViewModel`, `ZostavaKalendara`, okná `UdalostWindow`, `TypyUdalostiWindow`, `NastaveniaKalendaraWindow`, `PrehladDnaWindow`, `UpozornenieWindow` |
| Úlohy | To-Do: rýchle pridanie (Enter), termín, priorita (nízka, normálna, vysoká), popis, hotovo s dátumom, filter otvorené / hotové / všetky, „Vymazať hotové“, naviazanie na položku | `UlohyService`, `UlohyViewModel`, `UlohyView` |
| Poznámky | lístky s farbou (8 farieb), pripnutie, hľadanie bez diakritiky, náhľad, diktovanie, prepojenie na deň v kalendári, položku a udalosť; samostatné okno z kalendára alebo karty položky; automatické ukladanie | `PoznamkyService`, `PoznamkyViewModel`, `PoznamkaEditViewModel`, `PoznamkaEditor`, `PoznamkaWindow` |
| Dashboard | mini kalendár, úlohy po termíne, rýchle pridanie úlohy, pripnuté poznámky, prepínače panelov | `DashboardViewModel` |

**Rozdiel v technike:** EviTA používa Entity Framework Core a samostatné tabuľky s cudzími kľúčmi (jedna úloha patrí
najviac k jednej položke). SkVP používa Microsoft.Data.Sqlite s vlastným SQL a univerzálny model
`objekt` + `vazba` („čokoľvek s čímkoľvek“). Logiku (opakovanie, výskyty v období, prehľad dňa, .ics, zostava
kalendára, rozloženie obrazoviek) prevezmeme takmer bez zmeny. Prístup k dátam treba prepísať na SQL v štýle SkVP.
**Nová knižnica nie je potrebná**: MigraDoc, Kalendárium aj diktovanie už SkVP má.

## 3. Čím je SkVP iné – a čo z toho vyplýva
1. **Školský rok, nie kalendárny rok.** Dôležité je obdobie september – august, prázdniny podľa kraja, polroky,
   maturity a prijímacie skúšky. Všetko to SkVP už má v údajoch Noviniek (`novinky.json`: 20 termínov
   školského roka, 13 termínov maturity, 11 termínov prijímacích skúšok, vždy s oficiálnym zdrojom) a v kraji
   nastavenom na dashboarde (`novinky.kraj`).
2. **Predmetom práce sú osnovy.** Úlohy sa týkajú konkrétnych predmetov, oblastí a častí, napr. „Zapracovať zmenu ŠVP
   do osnov ANJ a NEJ“, „Skontrolovať osnovu fyziky“ alebo „Schválenie osnov na pedagogickej rade“. Jedna úloha často
   patrí k **viacerým** predmetom naraz, preto jeden cudzí kľúč ako v EviTA nestačí.
3. **Osnova má stav** (nezačatá, rozpracovaná, na kontrolu, hotová) a **platnosť** („od 1.9.2026“ z úvodnej tabuľky).
   Z toho sa dajú odvodiť návrhy úloh a termíny bez ručného zadávania.
4. **Univerzálny model `objekt`/`vazba`** už má mäkké mazanie s možnosťou vrátiť späť, zálohu, diagnostiku, Súvislosti
   a obnovu. Nové záznamy ho majú využiť, nie obísť.

## 4. Dátový model – porovnanie variantov

| Variant | Popis | Výhody | Nevýhody |
|---|---|---|---|
| A – tabuľky ako v EviTA | `udalost`, `uloha`, `poznamka` s vlastnými id a stĺpcami `predmet_id` … | priamy prenos z EviTA | len jedno naviazanie; nové id mimo `objekt` → vlastné mazanie, obnova, diagnostika aj súvislosti; rozpor s požiadavkou „čokoľvek s čímkoľvek“ |
| B – všetko v `objekt.vlastnosti` (JSON) | typ `uloha`, `udalost`, `poznamka`, polia v JSON | žiadna nová tabuľka | dátumy v JSON sa zle filtrujú a indexujú (kalendár číta obdobia); krehké pri zmene formátu |
| **C – objekt + tabuľka podrobností (odporúčané)** | každý záznam je riadok v `objekt` (názov, farba, mäkké mazanie, časy), podrobnosti v tabuľke s kľúčom `objekt_id` – **rovnako ako je dnes dokument osnovy v tabuľke `dokument`**; naviazania cez existujúcu `vazba` | jedna sada id; ľubovoľný počet naviazaní (úloha ↔ 3 predmety ↔ udalosť ↔ poznámka); mazanie s vrátením späť, záloha, kontrola dát a Súvislosti fungujú hneď; dátumy v stĺpcoch s indexom | o niečo zložitejšie SQL (spojenie dvoch tabuliek) |

### Schéma 3 (variant C)
```sql
-- typy objektov: 'udalost', 'uloha', 'poznamka' (Models/TypObjektu); objekt.nazov = názov, objekt.farba = farba lístka
CREATE TABLE typ_udalosti(id INTEGER PRIMARY KEY AUTOINCREMENT, nazov TEXT NOT NULL, farba TEXT NOT NULL,
                          aktivny INTEGER NOT NULL DEFAULT 1, poradie INTEGER NOT NULL DEFAULT 0);
CREATE TABLE udalost(objekt_id INTEGER PRIMARY KEY REFERENCES objekt(id) ON DELETE CASCADE,
                     guid TEXT NOT NULL, typ_id INTEGER NULL REFERENCES typ_udalosti(id),
                     zaciatok TEXT NOT NULL, koniec TEXT NULL, cely_den INTEGER NOT NULL,
                     opakovanie INTEGER NOT NULL DEFAULT 0, opakovanie_do TEXT NULL,
                     pripomienka INTEGER NOT NULL DEFAULT 0, stav INTEGER NOT NULL DEFAULT 0,
                     miesto TEXT NULL, popis TEXT NULL);
CREATE INDEX ix_udalost_zaciatok ON udalost(zaciatok);
CREATE TABLE uloha(objekt_id INTEGER PRIMARY KEY REFERENCES objekt(id) ON DELETE CASCADE,
                   termin TEXT NULL, priorita INTEGER NOT NULL DEFAULT 1, hotovo TEXT NULL,
                   zodpovedny TEXT NULL, popis TEXT NULL);
CREATE INDEX ix_uloha_termin ON uloha(termin);
CREATE TABLE poznamka(objekt_id INTEGER PRIMARY KEY REFERENCES objekt(id) ON DELETE CASCADE,
                      text TEXT NOT NULL DEFAULT '', pripnuta INTEGER NOT NULL DEFAULT 0, datum TEXT NULL);
```
- **Naviazanie:** nový typ väzby `TypVazby.Patri = "patri"` (úloha/udalosť/poznámka → predmet, oblasť, časť,
  skupina, iná úloha či udalosť). Pri predmete sa zobrazí ako „Úlohy a poznámky“, nie medzi odbornými súvislosťami
  (nadväznosť, medzipredmetové vzťahy), aby sa tie dve veci nemiešali.
- **Odstránenie predmetu:** predmet sa len označí ako odstránený, väzby zostanú. Vrátenie späť obnoví aj naviazania.
  Úloha bez platného predmetu zostane (ukáže „predmet bol odstránený“) a trvalé vymazanie predmetu po lehote zmaže
  len väzbu.
- **Premenovanie predmetu** sa prejaví samo, lebo väzby idú cez id, nie cez názov.
- **Migrácia:** `Databaza.VerziaSchemy = 3`. Pri prvom spustení sa vytvoria tabuľky a predvolené typy udalostí.
  Staršia verzia programu novú databázu neotvorí, na to už existuje kontrola.
- **Úpravy existujúceho kódu:** `ObjektService.Vsetky()` vracia všetky typy. Výber objektov (`VyberObjektuWindow`)
  a Súvislosti predmetu musia nové typy filtrovať. `Suvislosti` dostanú filter typov väzieb.
  `TestovaciaSada.VymazVsetko` zmaže aj nové tabuľky. `ServisViewModel` (počty) a `DiagnostikaService` (podrobnosti
  bez objektu, väzby) dostanú nové riadky.

## 5. Kalendár

### 5.1 Čo kalendár zobrazuje
| Zdroj | Ako | Farba / poznámka |
|---|---|---|
| Vlastné udalosti | ručne, s typom, opakovaním a pripomienkou (ako EviTA) | typ udalosti |
| Úlohy s termínom | **tá istá úloha** zo sekcie Úlohy (jeden zdroj pravdy, pozri 6.1) | podľa priority, odškrtnutá prečiarknutá |
| Prázdniny | podfarbenie dní a pás „Jesenné prázdniny“ podľa kraja | z `novinky.json` (posledný deň vyučovania, nástup) |
| Školský rok | začiatok, koniec 1. polroka, koniec vyučovania, nástup | téma „skolskyrok“ v Novinkách |
| Termíny maturity a prijímacích skúšok | celodenné alebo viacdňové pásy, odkaz na zdroj | kategória Noviniek; dá sa vypnúť po kategóriách |
| Sviatky a meniny | v hlavičke dňa, deň pracovného pokoja podfarbený | Kalendárium (zdieľané s EviTA) |
| Platnosť osnov | „Osnova FYZ platí od 1. 9. 2026“ (z úvodnej tabuľky) | farba oblasti |
| Pravidelná záloha | dni automatickej zálohy | sivá, dá sa vypnúť |
| Archív školského roka | pripomienka „Archivovať osnovy školského roka 2025/2026“ po konci vyučovania, ak archív za ten rok nie je | len ak chýba |
| Poznámky k dňu | ikonka v bunke a zoznam v detaile dňa | farba lístka |

Automatické termíny sú len na čítanie, rovnako ako záruky a revízie v EviTA. Neukladajú sa, počítajú sa pri
zobrazení, takže po aktualizácii Noviniek sú vždy aktuálne. Pri každom je tlačidlo „Prevziať do kalendára“
(vznikne vlastná udalosť, napr. s pripomienkou) a pri termínoch z Noviniek aj odkaz na oficiálny zdroj.

### 5.2 Zobrazenia
- **Mesiac**, **Týždeň**, **Zoznam** (najbližších 60 dní) – ako EviTA.
- **Nové: Školský rok** – 12 mesiacov od septembra do augusta na jednej obrazovke, s podfarbenými prázdninami, sviatkami
  a bodkami udalostí. Dobre sa tlačí na jednu stranu A4 na šírku a hodí sa na nástenku zborovne.
- Hore: Dnes, šípky, „Skočiť na“, výber kraja (spoločný s dashboardom), prepínače zdrojov (Novinky, prázdniny,
  osnovy, zálohy, hotové a zrušené).
- Pastelové farby cez `FarbaNaStetec` a väčšie písmo ako zvyšok SkVP. Text v detaile udalosti sa dá označiť
  a kopírovať.

### 5.3 Udalosť
Okno ako v EviTA (názov, typ, od – do, celý deň, opakovanie, pripomienka, stav, popis, miesto). Pole
**„Týka sa“** nahrádza položku, miestnosť a osobu z EviTA: viac predmetov, oblastí alebo častí cez existujúci
`VyberObjektuWindow`. Pod udalosťou sú poznámky a úlohy k nej (napr. zápisnica z predmetovej komisie).

**Predvolené typy udalostí** (dajú sa upraviť): Pedagogická rada · Predmetová komisia / metodické združenie ·
Termín k osnovám (odovzdanie, kontrola, schválenie) · Porada · Školská akcia · Rodičovské združenie · Maturita ·
Prijímacie skúšky · Iné.

## 6. Úlohy

### 6.1 Jedna úloha – jeden zdroj (zmena oproti EviTA)
V EviTA sú dve rôzne „úlohy“: druh udalosti v kalendári a samostatný To-Do zoznam. To je mätúce, lebo úloha
zadaná v kalendári nie je v Úlohách a naopak. **Návrh pre SkVP:** úloha je len jedna (tabuľka `uloha`). Ak má
termín, zobrazí sa aj v kalendári. Tlačidlo „Nová úloha“ v kalendári vytvorí úlohu s dňom ako termínom a
odškrtnutie na ktoromkoľvek mieste platí všade. Udalosť (napr. porada) zostane udalosťou.

### 6.2 Funkcie
- Ako EviTA: rýchle pridanie (Enter), termín (dnes, zajtra, bez termínu), priorita, popis, hotovo s dátumom,
  filter otvorené / hotové / všetky, „Vymazať hotové“, poradie (vysoká → termín → poradie).
- Nové pre SkVP:
  - **„Týka sa“** (viac predmetov, oblastí alebo častí; čipy vo farbe oblasti, kliknutím sa otvorí osnova);
  - **filter** podľa oblasti alebo predmetu;
  - nepovinné pole **Zodpovedá** (voľný text, napr. „PK cudzích jazykov“ alebo meno; osoby SkVP zatiaľ nemá);
  - **zoskupenie** po termíne / dnes / tento týždeň / neskôr / bez termínu.
- **Navrhované úlohy z osnov** (vypočítané, neukladajú sa – obdoba automatických termínov):
  - osnovy **na kontrolu** → „Skontrolovať osnovu …“;
  - **nezačaté** osnovy;
  - predmety **bez osnovy** alebo bez platnosti v úvodnej tabuľke;
  - **dôležité novinky** k osnovám a ŠVP, ktoré ešte nie sú vybavené.

  Tlačidlo „Prevziať“ urobí z návrhu skutočnú úlohu (s naviazaním na predmet), „Skryť“ ho schová. Zoznam sa tak
  sám zmenšuje, ako práca na osnovách postupuje.
- Pri zmene stavu osnovy na „Na kontrolu“ program ponúkne v spodnom oznámení „Vytvoriť úlohu na kontrolu?“
  (ponuka, nič sa nevytvára samo).

## 7. Poznámky
- Ako EviTA: lístky v 8 pastelových farbách, pripnutie, hľadanie bez diakritiky (aj v naviazaných názvoch),
  náhľad, automatické uloženie, diktovanie (`DiktovanieDoPola` už v SkVP je), samostatné okno.
- **Prepojenie:** deň v kalendári, udalosť, úloha a **„Týka sa“** (predmety, oblasti, časti). Typické použitie:
  pripomienky ku kontrole osnovy, poznámky z predmetovej komisie, nápady na úpravu tematických celkov.
- Text zostáva **čistý text** (rovnako ako v EviTA). Formátovaný text osnov má svoj editor a bohatý text
  v poznámkach by priniesol zložitosť bez väčšieho úžitku. Dá sa doplniť neskôr.
- Pri predmete sa dá text poznámky jedným kliknutím **vložiť do osnovy** (na miesto kurzora v editore) – prepojenie
  „nápad → osnova“.

## 8. Prepojenia s ostatnými časťami SkVP

| Časť programu | Čo pribudne |
|---|---|
| **Osnovy – predmet / časť** | panel „Úlohy a poznámky“ (počet v nadpise) vedľa Súvislostí: zoznam úloh a poznámok k predmetu, „+ Úloha“, „+ Poznámka“, „+ Termín“ (udalosť naviazaná na predmet); najbližší termín predmetu |
| **Osnovy – oblasť / skupina** | ten istý panel pre oblasť (spolu s úlohami jej predmetov); v zozname predmetov malý odznak s počtom otvorených úloh |
| **Osnovy – prehľad** | na dlaždici oblasti počet otvorených úloh a po termíne |
| **Ľavé menu** | tri položky so živým počtom (úlohy po termíne červeným odznakom) |
| **Dashboard** | panely Kalendár (mini mesiac + dnes a najbližšie), Úlohy (rýchle pridanie, po termíne, dnes), Pripnuté poznámky; prepínače v riadku „Zobraziť na dashboarde“; blok Dnes ukáže aj dnešné udalosti |
| **Novinky** | pri termíne „Pridať do kalendára SkVP“ (doterajší .ics zostane), pri novinke „Vytvoriť úlohu“ (napr. „Zapracovať zmenu do osnov“) s odkazom na novinku |
| **Súvislosti** | pri udalosti, úlohe aj poznámke vidno, k čomu patrí; z predmetu sa dá prejsť na všetko naviazané |
| **Tlač a zostavy** | nové zostavy: Kalendár (mesiac, školský rok na A4 na šírku, zoznam za obdobie), Úlohy (otvorené, podľa oblastí a predmetov), Poznámky (vybrané alebo k predmetu); s hlavičkou a logom školy ako pri osnovách; PDF, tlač, Word |
| **Import a export** | export kalendára .ics (Outlook, Google; vlastné udalosti s opakovaním, úlohy, voliteľne automatické termíny); spoločná trieda `Services/Ics.cs` aj pre Novinky; export úloh a poznámok do Excelu (ClosedXML už je); import .ics až v ďalšej etape |
| **Archív** | do archívu školského roka voliteľne aj poznámky a úlohy k predmetom za daný rok (aby sa zachovalo, prečo sa osnova menila); pripomienka archivácie v kalendári |
| **Servis** | záloha databázy obsahuje všetko automaticky; záloha „len osnovy“ ich vynechá; kontrola dát: podrobnosti bez objektu, väzby na neexistujúce, úlohy naviazané len na odstránené predmety; počty v prehľade |
| **Nastavenia** | Kalendár: kraj (spoločný), zdroje automatických termínov, prehľad dňa pri spustení, upozornenia, začiatok týždňa; typy udalostí; export a import nastavení: typy udalostí a nastavenia kalendára (bez samotných záznamov) |
| **Testovacia sada** | niekoľko ukážkových udalostí, úloh a poznámok k vymyslenému gymnáziu; zmažú sa spolu so sadou bez zvyškov |
| **Pri spustení** | voliteľne prehľad dňa (dnes, pripomienky, po termíne) ako v EviTA; počas behu upozornenie v čase pripomienky |

## 9. Prevzatie kódu z EviTA
| Prevziať takmer bez zmeny | Prispôsobiť | Nové |
|---|---|---|
| `KalendarService.Zaciatky` (opakovanie), `Ics` (zalamovanie, RRULE, VALARM), `Prehlad`, `ZostavaKalendara` (MigraDoc), rozloženie `KalendarView`, `UdalostWindow`, `TypyUdalostiWindow`, `PoznamkaEditor`, `UlohyView`, `PrehladDnaWindow`, `UpozornenieWindow` | prístup k dátam (EF → SQL v štýle `ObjektService`), naviazanie (jeden kľúč → „Týka sa“ cez `vazba`), automatické termíny (záruky → Novinky, prázdniny, osnovy), preklad (`Preklad`), pastelové štýly SkVP | zobrazenie školského roka, navrhované úlohy z osnov, panel pri predmete, väzby na Novinky, zostavy v Tlači |

## 10. Riziká a ako ich riešiť
- **Nové typy v `objekt` sa objavia tam, kde sa čakajú len oblasti a predmety.** Prejdem všetky volania
  `Vsetky()` a dotazy bez filtra typu a samotest overí, že výber objektov, tlač, export a archív nové typy neukazujú.
- **Zmena schémy databázy.** Migrácia na verziu 3 sa spustí po automatickej zálohe pred aktualizáciou. Samotest
  overí migráciu z databázy verzie 2 aj novú inštaláciu.
- **Výkon:** kalendár číta len obdobie cez indexy a automatické termíny sú malé zoznamy z pamäte. Pri stovkách
  záznamov to nie je problém.
- **Zdieľané Kalendárium s EviTA** sa používa len na čítanie (meniny, sviatky), ako doteraz.
- **Zložitosť UI:** každá sekcia dostane samotest so snímkami obrazovky (mesiac, týždeň, zoznam, školský rok, úlohy,
  poznámky, panel pri predmete, dashboard), ako je zvykom.

## 11. Postup po etapách (každá etapa: CI + samotest, vydanie len so súhlasom)
1. **Základ a Úlohy** – schéma 3, `TypObjektu`/`TypVazby`, služby a migrácia; sekcia Úlohy s „Týka sa“, filtrom
   a navrhovanými úlohami; panel pri predmete a oblasti; menu s odznakom; dashboard (Úlohy).
2. **Poznámky** – sekcia, editor s diktovaním, prepojenia, panel pri predmete, pripnuté na dashboarde, vloženie do
   osnovy.
3. **Kalendár** – udalosti, typy, mesiac / týždeň / zoznam / školský rok, automatické termíny (Novinky, prázdniny,
   osnovy, zálohy, archív), úlohy s termínom, poznámky k dňu; dashboard (mini kalendár); Novinky → kalendár a úloha.
4. **Výstupy a dokončenie** – tlač a PDF (kalendár, úlohy, poznámky), .ics a Excel, prehľad dňa a upozornenia,
   nastavenia a ich export/import, archív, servis, testovacia sada, dokumentácia.

## 12. Rozhodnutia (s odporúčaním)
1. **Úlohy a kalendár ako jeden zdroj** (úloha s termínom je v kalendári) – *odporúčam*; alternatíva: oddelené ako
   v EviTA.
2. **Navrhované úlohy z osnov** (vypočítané, s „Prevziať“ / „Skryť“) – *odporúčam*.
3. **Zobrazenie Školský rok** a tlač na A4 na šírku – *odporúčam*.
4. **Pole „Zodpovedá“** ako voľný text (kým nie sú v programe učitelia) – *odporúčam*.
5. **Prehľad dňa pri spustení** – predvolene vypnutý (SkVP sa neotvára denne ako EviTA), dá sa zapnúť.
6. **Poznámky a úlohy v archíve školského roka** – voliteľné pri vytváraní archívu, predvolene zapnuté.
