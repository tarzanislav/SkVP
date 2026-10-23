# SkVP – stav projektu a pokyny pre ďalšiu session

Aktualizované: 23. 10. 2026 · posledné vydanie **v0.1.37** (vetva `main` = `vyvoj/etapa1`)

## Spolupráca (dohodnuté s používateľom)
- Používateľ zadáva úlohy postupne; všetky zmeny v repozitári robí Claude, používateľ potvrdzuje len nevyhnutné veci:
  **vydanie (release), nové knižnice/licencie, bezpečnosť a nastavenia repozitára**.
- Release len na výslovný súhlas používateľa (alebo keď ho Claude navrhne a používateľ súhlasí).
- Návrhy ďalších funkcií robí Claude **až po sérii úloh**, na základe dôkladnej analýzy, s vysvetlením.
- Všetko musí fungovať v programe bez Wordu; ak je Word nainštalovaný, použije sa na úpravu osnov.
- Komunikácia a texty v programe po slovensky; dizajn moderný, farebný (každá vzdelávacia oblasť má svoju farbu).

## Postup práce
- Vývoj na vetve `vyvoj/etapaN` → GitHub Actions (zostavenie, samotest `SkVP.exe --samotest`, inštalátor) →
  po súhlase fast-forward `main` → workflow sám vydá release `v0.1.<číslo behu>`.
- Lokálne sa nedá kompilovať (NuGet je v prostredí blokovaný) – kompiluje sa v CI.
  - chyby kompilácie: `gh api repos/tarzanislav/SkVP/check-runs/<job>/annotations`
  - výstupy samotestu (záznam, snímky obrazovky, vzorové PDF/DOCX): vetva **`vystupy`**
    (`git clone -b vystupy https://github.com/tarzanislav/SkVP`).
- Repozitár: **tarzanislav/SkVP** (verejný). Predvolená vetva je zatiaľ `vyvoj/etapa1` (zmeniť na `main` môže len používateľ).

## Technológie a architektúra
C# .NET 10, WPF (ThemeMode Fluent – svetlý/tmavý/podľa Windows), SQLite (Microsoft.Data.Sqlite), CommunityToolkit.Mvvm,
ClosedXML (import Excelu), DocumentFormat.OpenXml (čítanie/zápis Wordu), PDFsharp-MigraDoc-WPF (náhľad, tlač, PDF). Inno Setup.

- Databáza „čokoľvek s čímkoľvek“: tabuľky `objekt` (oblasť, predmet, …), `vazba` (súvislosti), `dokument`
  (osnova ako **.docx priamo v DB**), `verzia` (archív starších verzií), `nastavenie`; od schémy 2 aj `archiv_rok`
  a `archiv_polozka` (snímka osnov za školský rok).
- Spoločný model textu `Blok` (Nadpis, Odsek, Zoznam, Tabulka so zlúčenými bunkami) – DOCX ↔ model ↔ editor (FlowDocument)
  ↔ PDF/Word výstup. Súbory: `Services/Dokument.cs`, `DocxCitac.cs`, `DocxVystup.cs`, `PdfVystup.cs`.
- Úprava vo Worde: pracovná kópia v %LOCALAPPDATA%\SkVP\Úpravy vo Worde, sledovanie uloženia, prevzatie do DB s archiváciou
  (`Services/UpravaVoWorde.cs`, správa v `MainViewModel`).
- Testovacia sada 43 osnov: zdroj `nastroje/testovacia_sada/*.py` → `python3 nastroje/testovacia_sada/zostav.py`
  → `src/SkVP.App/Zdroje/testovacia_sada.json`; voľba v inštalátore (register HKLM\Software\tarzanislav\SkVP) a pri prvom spustení,
  vymazanie (len osnovy / všetko) so zálohou.
- Úvodná strana zostáv: `Services/TitulnaStrana.cs` + okno `Views/UvodnaStranaWindow` – každý riadok prepísateľný/vypínateľný,
  text dole ľubovoľný (aj tabuľka), {datum}/{rok}; stupeň vzdelania „úplné stredné všeobecné vzdelanie (ISCED 344)“.

## Hotové (úlohy 1–5 a doplnky)
Sekcie Dashboard, Osnovy (oblasti → predmety v poradí 01, 02…, pridanie, premenovanie, presun šípkami/Alt+↑↓/myšou aj do inej
oblasti, odstránenie s „Vrátiť späť“), osnova predmetu (náhľad, Úprava v programe / Upraviť vo Worde, stav, súvislosti),
charakteristika oblasti, Tlač a zostavy (všetky/jedna/vybrané oblasti, úvodná strana s logom, obsah, oddeľujúce strany
oblasti a predmetu, tlač, PDF, Word), Nastavenia (škola, logo, program, Word/editor, vzhľad, import z Excelu, zálohy,
testovacia sada, aktualizácie), automatické denné zálohy číslované za sebou, aktualizácia z GitHub releases.

## Logo a hlavička strán (vydané vo v0.1.24, 23. 10. 2026)
- Úvodná strana: logo školy sa dá vybrať/odstrániť priamo v okne „Úvodná strana a hlavička“ (rovnaké logo ako v Nastaveniach)
  a vypnúť zaškrtávacím políčkom (`titul.logo.zobrazit`).
- Hlavička ďalších strán: vľavo text (predvolene nadpis – podnadpis „Školský vzdelávací program – Učebné osnovy“, dá sa prepísať;
  rovnaký text ako nadpis sa neukladá, aby sledoval nadpis), vpravo názov školy a logo (výška 0,8 cm, najviac 3,5 cm široké).
  Každá časť sa vypína zvlášť (`titul.hlavicka.text|skola|logo.zobrazit`) a celá hlavička naraz (`titul.hlavicka.zobrazit`,
  jednotlivé voľby sa pritom pamätajú). Okno ukazuje živú ukážku hlavičky.
- PDF/tlač: hlavička je tabuľka (MigraDoc) s čiarou dole; Word: tabuľka v hlavičke + prázdny odsek, obrázok v časti hlavičky;
  pri vypnutej hlavičke sa časť hlavičky do Wordu nepridá.
- Testovacia sada obsahuje vymyslenú školu (nikde neexistuje): Gymnázium Javorová, Javorová 21, 999 01 Hviezdany;
  ŠkVP „Hviezdy na dosah“, platnosť od 1. 9. 2026, schvaľovacie riadky, text hlavičky, text dole s tabuľkou podpisov
  (vymyslené mená) a logo kreslené v programe (`TestovaciaSada.VytvorLogo`). Zapíše sa len do programu bez vlastnej školy a loga;
  zápis sa zaznamená v `testovacia_sada.skola` (pôvodná a nová hodnota, hash loga, text päty). Vymazanie sady (osnovy aj všetko)
  vráti pôvodný stav; čo používateľ medzitým zmenil, zostane.
- Úvodná strana sa automaticky zhustí (stupne 0–3: menšie medzery, oblasti v dvoch stĺpcoch, menšie logo a nadpis), aby sa
  zmestila na jednu stranu; stupeň sa určí skúšobným vysádzaním (`PdfVystup.StupenZhustenia`) a použije aj pre Word.
- Samotest `TestLogaAHlavicky`: hľadá body skúšobného loga na vykreslených stranách, kontroluje hlavičky a obrázky vo Worde,
  snímky `uvodna_strana_logo.png`, `hlavicka_*.png` na vetve `vystupy`.

## Oprava názvu predmetu (vydané vo v0.1.37)
- V zozname predmetov oblasti je pri názve ceruzka (a F2) – oprava názvu priamo v riadku.
- Nový názov predmetu alebo oblasti sa prenesie aj do textov dokumentov Word v programe (`PremenovanieVDokumentoch`):
  len celé výskyty, nie časť dlhšieho názvu iného predmetu/oblasti; formátovanie zostane; predtým sa zarchivuje verzia
  („Pred premenovaním …“). Dokument práve otvorený vo Worde sa nemení (oznámenie). Názvy súborov sa tvoria z aktuálneho názvu.
- „Konverzácia z nemeckého jazyka“ → „Konverzácia v nemeckom jazyku“ (učebný plán, testovacia sada) + jednorazová úprava
  existujúcich databáz pri spustení (`Migracie`, značka `migracia.konverzacia_nj`).

## Tlač, Import a export, Servis, Nastavenia (vydané vo v0.1.37)
- Tlač ako v EviTA: vlastné okno tlače (`Views/TlacWindow`, `Pomocne/RozvrhTlace`, `ObojstrannaTlac`, `ObojstrannaWindow`)
  – náhľad listu, tlačiareň, kópie, obojstranná tlač so sprievodcom, 1/2/4/8 strán na list, opakovanie, brožúra,
  zväčšenie, rámik, sivá, zosvetlenie, bez pozadia, dierovač. Tlač ide priamo na tlačiareň (XPS), nie cez okno Windows
  (to v náhľade neukazovalo dokument). Režim PDF: PDF, PDF/A-2b, PDF/A-3b (príloha Word), PDF/X-4, PNG, JPEG, TIFF,
  šifrovanie, heslo, zákazy (`Services/SuboryZostavy`, `PdfNorma`, `ObrazkyDokumentu`, `PozadieDokumentu`). Bez e-mailu (rozhodnutie používateľa).
- Import a export (`ImportExportView`): export osnov (`ExportOsnovWindow`, `Services/ExportOsnov`) – všetky/oblasti/predmety,
  jeden dokument alebo každý predmet zvlášť „05 Fyzika 2026“ v priečinku oblasti; Word a všetky formáty PDF/obrázkov.
  Import osnov (`ImportOsnovWindow`, `Services/ImportOsnov`) z DOCX a PDF (PDF: čítanie PDF a OCR Windows v pomocnom
  procese `--ocr`, `Pomocne/CitaniePdf` z EviTA; text → bloky `Services/TextNaOsnovu`) do existujúceho alebo nového predmetu;
  rozpoznanie predmetu z názvu súboru/textu, rok zo súboru. Pred importom záloha; stará osnova do archívu verzií s dôvodom
  „Archivované … (rok R) – pred importom …“ a súbor `Dokumenty\SkVP\Osnovy\Archív\05 Fyzika – archív R.docx`; nová kópia
  `Osnovy\<rok>\05 Fyzika <rok>.docx`. Rok zmeny osnov a import v `objekt.vlastnosti` (`Services/InfoOsnovy`), poznámka
  a tlačidlá „Importovať novú osnovu…“ a „Archív (N)“ pri predmete (`ArchivOsnovyWindow` – otvoriť, uložiť, obnoviť).
  Oblasti a predmety z dokumentu (Excel, Word – tabuľka alebo nadpisy/zoznamy, PDF) a export do Excelu, Wordu, PDF (`Services/StrukturaOsnov`).
- Servis (`ServisView`): záloha všetkých dát, záloha osnov vo Worde (ZIP s priečinkami oblastí a archívom), pravidelná
  záloha (denne/týždenne/vypnutá, ponechať N), vlastný priečinok záloh (`zaloha.priecinok`), obnova zo zálohy (overenie,
  záloha pred obnovou, reštart), kontrola a zmenšenie databázy, testovacia sada a vymazanie údajov (presunuté z Nastavení).
- Nastavenia: jazyk (`Services/Preklad` ako v EviTA, zatiaľ len slovenčina – rozhodnutie používateľa), export a import
  nastavení do JSON (`Services/NastaveniaProgramu` – aj logo a text dole na úvodnej strane).
- Cieľová platforma `net10.0-windows10.0.19041.0` (súčasti Windows na PDF a OCR, schválené používateľom).
- OCR na GitHube má len angličtinu (diakritika sa stráca) – kvalitu importu PDF treba overiť na počítači používateľa.

## Jazykové osnovy a úroveň (vydané vo v0.1.37)
- Pri importe osnovy cudzieho jazyka sa sprievodca pýta na úroveň (B1, B2, bez úrovne); úroveň ide do názvu predmetu
  „Ruský jazyk – úroveň B1“ (aj B2) a do `objekt.vlastnosti` (uroven). Existujúci predmet sa pri importe premenuje na
  názov zo sprievodcu (aj v textoch dokumentov).
- Pravidlá oblasti Jazyk a komunikácia (`Services/JazykovePravidla`), podľa skratiek v názve súboru: KAJ → profilácia
  z 2. riadku úvodnej tabuľky („Konverzácia v anglickom jazyku - profilácia anglický jazyk“ / „… - profilácia šport,
  informatika, steam“), B2; RUJ, NEJ, KRJ (Konverzácia v ruskom jazyku – nový predmet), KNJ → B1; ANJ, Reálie anglicky
  hovoriacich krajín (premenuje „Reálie v anglickom jazyku“), Jazykom k maturite RUJ/NEJ/ANJ („Jazykom k maturite -
  ruský jazyk“, pôvodný predmet sa použije pre prvý, ďalšie nové) → B2; SJL, SSJ bez úrovne. Predmet už použitý iným
  súborom v zozname sa znova nepoužije (druhá profilácia KAJ = nový predmet).
- Import celého priečinka (Import a export → Z priečinka…, v sprievodcovi Pridať priečinok…), súbory sa len čítajú.

## Údaje z úvodnej tabuľky a archív školského roka (vydané vo v0.1.37)
- Pod názvom predmetu v zozname oblasti (aj na stránke predmetu) profilácie vľavo a platnosť vpravo – čítajú sa
  z úvodnej tabuľky dokumentu osnovy (`Services/UvodnaTabulka`: bunka „Profilácia/Profilácie“, „Platnosť“, hodnota vedľa
  alebo za dvojbodkou; platnosť aj podľa textu „od d.m.rrrr“). Políčko „opraviť údaje“ → úprava a „Uložiť do osnovy“
  zapíše do tabuľky dokumentu (verzia sa zarchivuje). Po každej zmene dokumentu (`PoZmeneDokumentu`) sa údaje znova
  načítajú. Testovacia sada má v osnovách úvodnú tabuľku.
- Servis: archív osnov za školský rok (`Services/ArchivSkolskehoRoka`) – kópia do archívu verzií („Archív školského roka
  2025/2026“) a priečinok Dokumenty\SkVP\Archív školských rokov\2025-2026 (Word po oblastiach, celé osnovy Word + PDF/A-2b).
- Export „Doc“ = .docx; starý .doc by vyžadoval Word (zatiaľ nerobené, čaká na rozhodnutie používateľa).

## Sekcia Archív (na vetve vyvoj/etapa1, zatiaľ nevydané)
- Menu Archív (medzi Import a export a Servis), `ViewModels/ArchivViewModel`, `Views/ArchivView`. Archivácia roka
  (`ArchivSkolskehoRoka.Archivuj`) okrem priečinka a archívu verzií uloží snímku do DB: `archiv_rok` + `archiv_polozka`
  (charakteristiky oblastí a všetky predmety: oblasť, farba, číslo, názov, stav, dĺžka, profilácie, platnosť, rok osnovy,
  úroveň, „zmenená v roku“ = úprava/verzia od 1. 9. do 31. 8. okrem archivácie, SHA-256 a kópia .docx).
- Prehľad: roky ako karty, dlaždice štatistík (predmety, % hotových, na kontrolu, zmenené v roku, normostrany, jazyky
  B1/B2), pruh stavu osnov, porovnanie s predchádzajúcim archivovaným rokom (nové / zmenené / bez zmeny / odstránené podľa
  objekt_id a SHA), osnovy po oblastiach s hľadaním a značkami; klik → `ArchivnaOsnovaWindow` (len na čítanie, kópia vo
  Worde, Uložiť ako). Celé PDF roka, priečinok, štatistiky do Excelu (hárky Štatistiky, Oblasti, Predmety), odstránenie
  z programu (súbory na disku zostanú). Servis má pri rokoch „Prezerať v Archíve“; po archivácii sa otvorí Archív.
- Archívy z v0.1.37 (kľúč `archiv.skolske_roky`) sa pri spustení prenesú (`Migracie` → `PrenesStareZaznamy`) so všetkými
  údajmi – osnovy z archívu verzií (dôvod „Archív školského roka R“, verzia najbližšia k času archivácie), profilácie
  a platnosť z úvodnej tabuľky, rozsah z textu, stav = terajší, ak sa dokument odvtedy nezmenil, inak podľa textu.
  Bez nájdených verzií zostane len záznam s priečinkom (poznámka „bez prehľadu…“, doplní sa pri ďalšom spustení, ak verzie pribudnú). Vymazanie osnov / všetkého maže aj archív rokov v programe.
- Samotest: snímky 13k, 22f–22j, `samotest_statistiky_archivu.xlsx`.

## Údaje o organizácii, ŠVP, import oblastí, správa databázy (na vetve vyvoj/etapa1, nevydané)
- Nastavenia → Údaje o organizácii (`Services/UdajeOrganizacie`, `ViewModels/UdajeOrganizacieVM`, `Views/UdajeOrganizacieEditor`):
  názov školy (`skvp.skola`), organizačná zložka, ulica, mesto/obec, PSČ, štát, IČO, DIČ, e-mail, telefón (`org.*`);
  `skvp.adresa` sa skladá z častí (staršia adresa v 1 riadku sa rozdelí). Kontrola IČO a e-mailu.
- Údaje o škole v zostave (`ViewModels/UdajeOSkoleVZostaveVM`, `Views/UdajeOSkoleVZostave` – v Tlač a zostavy aj v okne
  Export, spoločné voľby `titul.*.zobrazit`): úvodná strana – názov, zložka, adresa, IČO a DIČ, e-mail a telefón, logo;
  hlavička – názov, zložka, logo; päta strán – názov školy a kontakt vľavo (vpravo číslo strany); autor vo vlastnostiach
  PDF/Word. Predvolene IČO/DIČ, kontakt a päta vypnuté. PDF aj Word (`PdfVystup`, `DocxVystup`).
- Export a import nastavení programu: premenované, popis „nie je to záloha databázy“, prehľad skupín v súbore
  (`NastaveniaProgramu.Prehlad`), vynechaný aj čas poslednej kontroly aktualizácií.
- ŠVP pre gymnáziá: `Zdroje/svp_gymnazia.json` (kap. 4.1–4.7 doslovne, Tab. 1 predmety, poznámky pod čiarou, zdroj),
  text stiahnutý workflowom `.github/workflows/zdroje.yml` (vetva `zdroje`, ručné spustenie). `Services/SvpGymnazia`:
  `VytvorOblasti`, `PrevezmiCharakteristiku`, `OblastPredmetu` (oblasť pre predmet podľa plánu, ŠVP a kľúčových slov).
  Testovacia sada má charakteristiky oblastí zo ŠVP. V oblasti → Charakteristika: pás „Prevziať zo ŠVP…“.
- Prvé spustenie (`PrveSpustenieWindow`) v 2 krokoch: 1. údaje o škole + logo, 2. voľba – oblasti podľa ŠVP (predvolené;
  voliteľne priečinok s osnovami → po spustení sprievodca importu), testovacia sada, učebný plán gymnázia (+ charakteristiky
  zo ŠVP), Excel, prázdny. Import osnov rozpozná predmet podľa „Názov predmetu“ v úvodnej tabuľke (`UvodnaTabulka.NazovPredmetu`,
  pri PDF riadok „Názov predmetu: …“) a nový predmet zaradí do oblasti podľa `SvpGymnazia.OblastPredmetu`.
- Import a export → Vzdelávacie oblasti a predmety: zo súboru (aj HTML, XML, JSON, TXT), z webovej stránky (HttpClient, aj
  PDF/Word/Excel/JSON/XML na webe), diktovaním (hlasové písanie Windows Win+H, `Pomocne/HlasovePisanie` z EviTA) s náhľadom
  (`Views/ImportStrukturyWindow`); export aj do HTML, XML, JSON (`Services/ImportStruktury`, vlastný jednoduchý HTML parser
  bez knižnice). Prepis bez internetu (schválené používateľom 23. 10. 2026): whisper.cpp v1.9.5 (commit d1be6fde…, MIT),
  `nastroje/hlas/` (CMake + `skvp_hlas.c`) → `SkvpHlas.dll` zostavená v CI zo zdrojov, kontrola importov (len kernel32,
  advapi32) a exportov, `LICENSE-whisper.cpp.txt` pri programe. Kód z EviTA: `Services/Diktovanie` (modely small/turbo/base
  z Hugging Face s overením SHA-256, priečinok Modely vedľa dát), `Services/PrepisHlasu`, `Pomocne/Mikrofon` (winmm),
  `Pomocne/DiktovanieDoPola`, `Views/DiktovanieWindow` (ozubené koliesko v okne diktovania). Kľúče `diktovanie.sposob/model`.
  Vylepšenia po kontrole: výzva modelu v tvare „Oblasť: predmet, predmet.“ so všetkými 7 oblasťami; oblasť sa rozpozná aj
  bez dvojbodky (čiarka/pomlčka, bez diakritiky – `ImportStruktury.ZnamaNaZaciatku`, `ImportOsnov.BezDiakritiky`); model sa
  načíta do pamäte pri otvorení okna (`PrepisHlasu.PripravAsync`); model stiahnutý v EviTA (C:\ProgramData\EviTA\Modely)
  sa použije bez sťahovania; filter „ozveny“ výzvy pri tichu. CI: model ggml-small (cache, SHA-256) + slovenská veta
  z Piper (sk_SK-lili) → samotest prepisu: 6 s zvuku za ~7 s, 7/7 výrazov, oblasti a predmety rozpoznané.
- Servis: „Zálohovať učebné osnovy (Word, PDF)“ – pri každom .docx v ZIP aj .pdf (`ZalohaService.PdfDokumentu`).
  Správa databázy ako v EviTA (`Services/DiagnostikaService`): rýchla/úplná kontrola, kontrola väzieb, kontrola dát
  (čitateľnosť a SHA dokumentov, duplicitné predmety…), optimalizácia, obnova indexov, kompaktovanie, oprava väzieb
  (predmety bez oblasti → „Nezaradené predmety“), záchrana dát (prenos do novej DB, `Databaza.Inicializuj(cesta)`),
  prehľad tabuliek, verzia schémy, posledná kontrola, protokol (uložiť/vymazať).

## Audit zadania (23. 10. 2026, na vetve vyvoj/etapa1)
- Oprava chyby: okno úvodnej strany pri uložení prepísalo voľby údajov o škole na predvolené – okno má teraz aj zložku,
  IČO/DIČ, kontakt, zložku v hlavičke, pätu a autora súboru; adresa z okna sa uloží aj do Údajov o organizácii po častiach.
- Údaje o organizácii: kontrola PSČ (SK, CZ) a DIČ; adresa bez PSČ už neponechá staré PSČ.
- Import osnov z priečinka aj z podpriečinkov; podpriečinok s názvom oblasti („06 Človek a hodnoty“) určí oblasť nového
  predmetu; priečinky Archív sa preskočia.
- Charakteristiky zo ŠVP pre existujúce inštalácie: migrácia `migracia.charakteristiky_svp` nahradí len nezmenené ukážkové
  texty testovacej sady; Osnovy → „Charakteristiky zo ŠVP…“ prevezme všetky (`SvpGymnazia.PrevezmiVsetky`, archivácia).
- Údaje o škole (podľa volieb úvodnej strany, `TitulnaStrana.UdajeOSkole`) aj v exporte oblastí a predmetov (Word, PDF,
  HTML, Excel) a v štatistikách archívu.
- Export/import nastavení programu po skupinách (`Views/VyberSkupinWindow`), stálé poradie skupín, bez `kontrola.*`, priečinok
  záloh sa prevezme len ak na počítači existuje.
- Archív: PDF pri každom Worde aj v priečinku archívu roka; „Náhľad a tlač…“, „Uložiť ako PDF/Word…“ celých osnov roka zo
  snímky v programe (`ArchivSkolskehoRoka.Zostava`) – funguje aj bez priečinka na disku.
- Databáza: rýchla kontrola pri spustení (upozornenie pri chybe), optimalizácia pri zatvorení programu.
- Web/HTML/XML: kódovanie z BOM, `<meta charset>` alebo deklarácie XML (windows-1250, iso-8859-2).

## Sekcia Novinky a dashboard (vydané vo v0.1.70)
- **Novinky** (`Services/NovinkyService`, `ViewModels/NovinkyViewModel`, `Views/NovinkyView`, menu pod Dashboardom):
  overené informácie v 6 farebných témach – Učebné osnovy a ŠVP, Tematické plány, Cieľové požiadavky, Maturitná skúška,
  Prijímacie skúšky, Prázdniny a školský rok; druhy Termín / Novinka / Odporúčanie (farebné štítky), odpočet, poznámky
  k nesúladu zdrojov, kraj pre jarné prázdniny (`novinky.kraj`), hľadanie, záložky Prehľad podľa tém / Termínovník /
  Aktuálne správy zo zdrojov / Odporúčané stránky, „Do kalendára…“ (.ics).
- Údaje: `Zdroje/novinky.json` (65 položiek, stav k 23. 10. 2026; vygenerované nástrojom zo stiahnutých oficiálnych
  dokumentov – POP 2026/2027, termíny prázdnin .docx, termíny prijímacích skúšok 2027/2028, Základné informácie MS 2027,
  stránky NÚCEM/NIVaM, ePrihlášky, školský zákon zo Slov-Lex). Každý zdroj musí byť na oficiálnej doméne
  (`NovinkyService.OficialneDomeny` – minedu, nivam, nucem, statpedu, iedu, slov-lex, slovensko.sk, cvtisr, svsbb…);
  médiá a noviny sa odmietnu aj v stiahnutých údajoch. Pole `overit` = texty, ktoré musí zdrojová stránka obsahovať.
- Aktualizácia bez novej verzie: „Aktualizovať“ stiahne `src/SkVP.App/Zdroje/novinky.json` z vetvy main (raw GitHub),
  použije ho, ak je novší a platný (`novinky.data`). Nový stav údajov = upraviť JSON a vydať (main).
- RSS kanály ministerstva (`minedu.sk/rss/`) a NIVaM (`nivam.sk/feed/`): výber správ k vzdelávaciemu obsahu podľa
  nadpisu (súťaže, šport, veda, výstavba sa vynechajú), krížové potvrdenie, ak o téme píše aj druhý zdroj (≥3 výrazné
  slová, ±10 dní); obnova najviac raz za 3 h (`novinky.spravy`).
- „Overiť na zdrojoch“ načíta zdrojové stránky (HTML aj Word priamo, PDF len dostupnosť) a skontroluje údaje
  (`novinky.overenie`). Samotest v CI 23. 10. 2026: 29 údajov potvrdených na stránkach, 67 dokumentov dostupných,
  0 nenájdených; report `novinky_overenie.txt` vo výstupoch samotestu.
- Zistené nesúlady (uvedené pri položke): zber údajov o maturantoch NIVaM 1. – 31. 10. vs. POP 1. – 30. 10. 2026;
  .docx prázdnin na webe ministerstva vynecháva pri jarných prázdninách Trenčiansky kraj (POP ho uvádza); preklep
  v POP pri vianočných prázdninách 2028/2029.
- **Dashboard** (`ViewModels/DashboardDen.cs`): panel Dnes (dátum, týždeň, meniny dnes/zajtra, sviatky, najbližší deň
  pracovného pokoja – Kalendárium z EviTA `src/Kalendarium.Core`, spoločná DB C:\ProgramData\Kalendarium, údaje mien
  MIT `LICENSE-meniny.txt`; stav školského roka/prázdniny a najbližší termín z Noviniek), Počasie (`Services/PocasieService`
  z EviTA, Open-Meteo bez kľúča; predvolene mesto z Údajov o organizácii, zmena miesta priamo v paneli), Výrok dňa
  (`Services/CitatyService`, `Zdroje/citaty.json`). Prepínače „Zobraziť na dashboarde“ (`dashboard.dnes/pocasie/citat`).
- Výroky: výlučne z citaty-slavnych.sk (stiahnuté 23. 10. 2026), 1974 výrokov s autorom, rokmi života a popisom;
  vynechané výroky žijúcich politikov a osôb s politickou funkciou (Danko, Kiska, Schwarzenegger, Slota, Čaputová,
  Harabin, Dalajláma); predvolene skryté výroky diktátorov (Stalin, Lenin, Husák). Na dashboarde len výroky do 280 znakov,
  výrok dňa podľa dátumu, „Ďalší“ a „Skryť“ (`citaty.skryte`, s vrátením späť).
- Nástroj zdrojov: `.github/workflows/zdroje.yml` + `nastroje/zdroje/stiahni.py` (`zoznam.txt`, `overenie.txt`, premenná
  ZOZNAM) – stiahne oficiálne dokumenty na runneri do vetvy `zdroje` (shell session nemá prístup na tieto stránky).

## Pastelové farby, nový dashboard, prázdniny (vydané vo v0.1.75)
- Pastelové farby v celom programe: `Pomocne/Pastel.cs` + `FarbaNaStetec` – každá zobrazená farba sa zjemní (pôvodné sýte
  farby oblastí sa mapujú na novú paletu `Pastel.Oblasti`, ostatné: sýtosť ≤ 0,62, svetlosť 0,60–0,82); jemné podklady
  (parameter priehľadnosti) sú o 60 % sýtejšie; parameter „T“ = čitateľný odtieň na text a ikony (svetlý vzhľad tmavší,
  tmavý vzhľad svetlejší). Paleta nových oblastí je pastelová. Výstupné dokumenty (Word, PDF) farby nemenia.
- Dashboard: blok Dnes (dátum, meniny, sviatky, školský rok a najbližší termín) cez celú šírku, pod ním Počasie a Výrok
  dňa, potom tabuľka Prázdniny v školskom roku (`ViewModels/DashboardPrazdniny.cs`: výber školského roka a kraja, posledný
  deň vyučovania, termín, nástup, odpočet, zvýraznené najbližšie prázdniny); väčšie písmo; prepínač „Prázdniny“.
- Téma Prázdniny a školský rok sa v Novinkách nezobrazuje (`NovinkyService.TemaSkolskyRok`), údaje zostávajú v
  `novinky.json` (polia `posledny`, `nastup`); export .ics z Noviniek ich obsahuje. Nástroje `nastroje/zdroje/novinky.py`
  (generovanie JSON) a `over.py` (kontrola textov `overit` v stiahnutých stránkach).

## Novinky – strany a história blokov (23. 10. 2026, na vetve vyvoj/etapa1, nevydané)
- Blok témy: hore odporúčania (podfarbené farbou bloku), pod nimi `NovinkyService.NaStranu` = 6 najnovších informácií
  (podľa dátumu pridania `pridane`), staršie a uplynulé na ďalších stranách „História bloku“ (`BlokNoviniekVM.Strana`,
  odkazy „Staršie informácie a história bloku (N) →“ / „← Novšie informácie“). Prepínač „Zobraziť aj uplynulé“ zrušený.
- Nič sa nemaže: `NovinkyService.SHistoriou` ukladá všetky informácie do `novinky.historia`; informácia, ktorá v novších
  stiahnutých údajoch chýba, zostane v histórii bloku so štítkom „z histórie“ (len s oficiálnymi zdrojmi).
- Nadpisy väčšie a tučné; nadpis, text a poznámka informácie aj popis správy sa dajú označiť a kopírovať (štýl
  `VyberatelnyText` – TextBox iba na čítanie s `Decorator` namiesto posúvača, koliesko myši posúva stránku); tlačidlo
  Kopírovať skopíruje celú informáciu so zdrojmi.

## Plánované (z celkového zadania používateľa)
6. Priečinky: zdrojový (len čítať) → pracovný → výstupný; rozpoznanie predmetu v dokumente, premenovanie „01 Názov predmetu“,
   prevod PDF → Word vlastnými prostriedkami (bez Wordu); archivácia starých osnov s rokom v názve a záznamom.
7. Sekcie: Dashboard ako v EviTA (+ zaujímavosti k osnovám, učebnému plánu, predmetom, učiteľom), Úlohy a Poznámky ako v EviTA,
   Import a export (DOC, PDF, PDF/A-2b, PDF/A-3b, PDF/X, JPEG, PNG, TIFF – kód je v EviTA), import/export oblastí a predmetov
   z dokumentu, Nastavenia (aj export/import nastavení, jazyk), Servis (zálohy osnov a všetkých dát).
8. Automatický update z GitHubu – hotový základ (kontrola pri spustení, tichá inštalácia).
