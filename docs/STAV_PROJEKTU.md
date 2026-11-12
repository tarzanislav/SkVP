# SkVP – stav projektu a pokyny pre ďalšiu session

Aktualizované: 9. 11. 2026 · posledné vydanie **v0.1.98**; na vetve `vyvoj/etapa1` je nevydaná hĺbková revízia,
vlastné riešenia namiesto kódu z EviTA a rozloženie strany / jednotný vzhľad (pozri posledné tri časti)

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

## Novinky – strany a história blokov (vydané vo v0.1.85)
- Blok témy po stranách (`NovinkyService.NaStranu` = 6, `BlokNoviniekVM.Rozdel`): hore najprv dôležité, potom odporúčané
  (najviac polovica strany – `MaxVybranychNaStranu` = 3; viac len keď už nie sú bežné informácie), zvyšok najnovšie
  informácie (podľa `pridane`). Novšia dôležitá/odporúčaná posunie staršiu na ďalšiu stranu, kde je opäť hore; uplynulé
  a archivované sú na ďalších stranách „História bloku“ (odkazy „Staršie informácie a história bloku (N) →“ / „← Novšie
  informácie“). Dôležité: hrubší okraj a jemné podfarbenie, odporúčané: podfarbenie farbou bloku. Prepínač „Zobraziť aj
  uplynulé“ zrušený.
- Nič sa nemaže: `NovinkyService.SHistoriou` ukladá všetky informácie do `novinky.historia`; informácia, ktorá v novších
  stiahnutých údajoch chýba, zostane v histórii bloku so štítkom „z histórie“ (len s oficiálnymi zdrojmi).
- Nadpisy väčšie a tučné; nadpis, text a poznámka informácie aj popis správy sa dajú označiť a kopírovať (štýl
  `VyberatelnyText` – TextBox iba na čítanie s `Decorator` namiesto posúvača, koliesko myši posúva stránku); tlačidlo
  Kopírovať skopíruje celú informáciu so zdrojmi.

## Úvodné a záverečné časti učebných osnov (vydané vo v0.1.85)
- Nové typy objektov (`Models/TypObjektu`): `uvod`, `zaver` (jediná skupina každého druhu, vytvorí sa automaticky –
  `CastiOsnov.Skupina`) a `cast` (dokument v skupine, ako osnova predmetu). `Services/CastiOsnov.cs`: názvy, pevné
  charakteristiky, pastelové farby, zapnutie (`osnovy.uvod.aktivne`, `osnovy.zaver.aktivne`, predvolene vypnuté),
  `SkupinyVPoradi` (úvod – oblasti – záver), `Dokumenty`, `TypPodlaNazvu` (rozpoznanie „Úvodné časti…“ / „00 …“).
- UI: Osnovy – bloky ÚVODNÉ / ZÁVEREČNÉ ČASTI UČEBNÝCH OSNOV cez celú šírku nad a pod oblasťami (tenká čiara),
  posuvník (štýl `Prepinac`), vypnutý blok zosivený a neotvoriteľný; OblastView pre skupinu (bez charakteristiky a palety,
  „Pridať úvodnú/záverečnú časť“); PredmetView pre časť (bez šablóny úvodnej tabuľky); ľavé menu (zapnuté skupiny);
  dashboard (stena osnov); súvislosti. Predmety a časti sa medzi sebou nepresúvajú.
- Zaradenie zapnutých skupín: tlač a zostavy (`KapitolaOblasti.Typ`, na začiatku/konci, bez charakteristiky, PDF/Word),
  export osnov (priečinky „00 Úvodné časti…“ / „99 Záverečné časti…“), import osnov (ciele, nová časť z priečinka
  00/99 – skupina sa zapne), oblasti a predmety (Excel, Word, PDF, HTML, XML, JSON, diktovanie – skupina podľa názvu),
  archív školského roka (typ `cast`, poradie 0/99), záloha osnov (aj vypnuté), servis (kontrola dát, oprava väzieb –
  časti bez skupiny do úvodných), export/import nastavení (skupina „úvodné a záverečné časti…“; vyrovnávacie pamäte
  Noviniek a počasia sa neprenášajú).
- Kontrola pred vydaním v0.1.85: skupiny úvodných a záverečných častí vytvára `Migracie.Spusti` pri štarte (pred
  zálohou na pozadí – bez duplicít), kontrola dát v Servise nič nevytvára, `StrukturaOsnov.ZnameOblasti` pozná názvy
  skupín (import z PDF), odstránený nepoužívaný kód (filter krajov a uplynulých v Novinkách, kľúč citátov).

## Kalendár, Úlohy, Poznámky (24. 10. 2026, vydané v0.1.98)
Analýza a rozhodnutia: `docs/ANALYZA_KALENDAR_ULOHY_POZNAMKY.md` (používateľ schválil: úloha a kalendár jeden zdroj,
navrhované úlohy z osnov, zobrazenie Školský rok, pole Zodpovedá, úlohy a poznámky v archíve roka; všetky 4 etapy naraz).
Kód je napísaný nanovo pre SkVP (z EviTA prevzatý len princíp funkcií).
- **Dáta (schéma 3)**: záznam = objekt (`TypObjektu.Udalost/Uloha/Poznamka`, mäkké mazanie s Vrátiť späť) + tabuľka
  podrobností `udalost`, `uloha`, `poznamka`; `typ_udalosti` (predvolené typy `Databaza.PredvoleneTypyUdalosti`);
  „Týka sa“ = väzba `TypVazby.Patri` (viac predmetov, oblastí, častí, udalosť); `archiv_zaznam` (úlohy a poznámky roka).
  `ObjektService.Suvislosti` väzby `patri` vynecháva. Služby: `Services/Planovanie.cs` (spoločné), `UlohyService`,
  `PoznamkyService`, `KalendarService` (opakovanie, výskyty, automatické termíny, prehľad dňa, .ics), `NavrhyUloh`,
  `Ics` (zápis iCalendar, aj pre Novinky), `ZostavaPlanovania` (MigraDoc: mesiac, školský rok, zoznam, úlohy, poznámky).
- **Menu**: Kalendár (počet dnes), Úlohy (otvorené + po termíne červeno), Poznámky (počet) pod Osnovami, nad Tlačou.
- **Úlohy** (`UlohyViewModel`, `UlohyView`): skupiny podľa termínu, filter Týka sa + hľadanie, rýchle pridanie (s filtrom
  sa naviaže na predmet), detail s priebežným ukladaním (termín, priorita, zodpovedá, Týka sa, popis), odstránenie a
  „Odstrániť hotové“ s Vrátiť späť, navrhované úlohy (kontrola, nezačaté, bez platnosti, dôležité novinky) Prevziať/Skryť,
  tlač a PDF. Úloha s termínom sa zobrazuje v kalendári.
- **Poznámky** (`PoznamkyViewModel`, `PoznamkaEditorVM`, `PoznamkaEditor`, `PoznamkaWindow`): nástenka lístkov,
  pripnutie, 8 pastelových farieb, diktovanie (`DiktovanieDoPola.PopisSposobu/PripravModel`), deň, udalosť, Týka sa,
  Kopírovať text (do osnovy Ctrl+V), automatické ukladanie, tlač a PDF.
- **Kalendár** (`KalendarViewModel`, `KalendarView`, `UdalostEditorVM`, `UdalostWindow`, `TypyUdalostiWindow`):
  mesiac, týždeň a zoznam 60 dní (dlhé automatické obdobia len v prvý deň), školský rok (12 mesiacov); vpravo vybraný deň (dá sa skryť) (meniny, sviatok, prázdniny, záznamy
  s akciami Otvoriť/Hotovo/Zdroj/Prevziať do kalendára, poznámky); prepínače zdrojov (prázdniny a školský rok,
  maturita a prijímačky, platnosť osnov, archivácia, týždenná záloha, hotové); kraj spoločný s dashboardom; export .ics,
  tlač a PDF (mesiac A4 na šírku, školský rok na jednej strane, zoznam).
- **Prepojenia**: panel „Úlohy a poznámky“ pri predmete a oblasti (`PanelPlanovaniaVM`, `PanelPlanovania`), odznak
  otvorených úloh pri predmete, pri stave „Na kontrolu“ ponuka úlohy v oznámení (`Oznam` s textom akcie), dashboard
  (kalendár na týždeň, úlohy, pripnuté poznámky), Novinky (Pridať do kalendára, Vytvoriť úlohu), prehľad dňa pri
  spustení (`PrehladDnaWindow`, predvolene vypnutý) a pripomienky udalostí počas práce, Nastavenia (karta Kalendár a
  úlohy), export/import nastavení (kľúče `kalendar.*`, `ulohy.*`, typy udalostí), Servis (počty, kontrola dát a väzieb,
  záchrana), Archív (voľba „aj úlohy a poznámky“, zoznam v detaile, PDF v priečinku), testovacia sada (ukážkové
  záznamy, pri vymazaní sady sa odstránia len tie – `TestovaciaSada.KlucPlanovanie`).
- **Revízia kódu (24. 10. 2026)**: spoločné pomôcky v `Planovanie` (`Text`, `Pondelok`, `DenVTyzdni`, farby
  automatických termínov), každá služba číta záznamy jednou metódou `Citaj(db, podmienka, …)`; `UlohyService.Zoznam`
  už vracia úlohy zoradené podľa skupín (zoznam, dashboard aj tlač ho len preberajú); `PoznamkyService.Zoznam(hladat,
  patriK)`; názvy stavu, opakovania a pripomienky len v `KalendarService`; skloňovanie cez `Preklad.Pocet`; zapnutie
  návrhov `NavrhyUloh.Zapnute`; odstránený nepoužívaný kód (presun udalosti, `NastavTermin`, `NazovPriority` …).

## Hĺbková revízia celého kódu (29. 10. 2026, vydané vo v0.1.115)
Nástroje: všetky analyzátory .NET (AnalysisMode=All, vybrané IDE pravidlá) a kontrola balíkov v GitHub Actions
(`.github/workflows/analyza.yml` na vetve `analyza/kod`, výsledky na vetve `analyza-vystupy`), duplicity (jscpd),
zložitosť (lizard), porovnanie s EviTA (jscpd medzi projektmi), chyby väzieb XAML v samoteste.
- **Bezpečnosť** (so súhlasom používateľa): Microsoft.Data.Sqlite 10.0.14 (SQLite s opravou CVE-2025-6965);
  P/Invoke systémových DLL len zo System32, SkvpHlas.dll len z priečinka programu; import nastavení – prípona loga
  nesmie byť cesta (samotest); XML z importu štruktúry bez DTD; sťahovanie z webu najviac 50 MB.
  Ostatné knižnice (ClosedXML, MVVM Toolkit, OpenXml, PDFsharp) zámerne bez aktualizácie.
- **Správnosť**: `MainViewModel.Prejdi(sekcia, Func<object>)` – stará stránka sa uloží pred vytvorením novej;
  `DocxCitac` mapuje aj XmlException; import osnovy nezlyhá pre pomocnú kópiu; nedokončený archív roka sa celý
  odstráni; záloha cez `.zip.tmp` a premenovanie; mikrofón kontroluje návratové kódy waveIn a uvoľní pamäť až po
  skončení vlákna; automatické termíny v kalendári bez stavu.
- **Výkon**: `UvodnaTabulka.Precitaj(id)` s vyrovnávacou pamäťou podľa SHA-256 dokumentu (oblasť, kalendár,
  dashboard, návrhy úloh už neparsujú všetky osnovy pri každom otvorení); súvislosti predmetu raz; kraj raz.
  Zámerne nezmenené: `Pooling=false` (súbor DB nie je zamknutý pri obnove a záchrane).
- **Čistota**: `CastiOsnov.FarbaSkupiny`, `ImportOsnov.BezpecnyNazov` (jediný), `ZabudovaneZdroje.Json<T>`,
  `NavrhyUloh.KontrolaOsnovy`, `PolozkaArchivuRoka.TypSkupiny`, `Preklad.Pocet`, `TlacWindow.ZobrazVolby`;
  odstránené nepotrebné using a duplicitný alias `NovinkyViewModel.VsetkyKraje`.
- **Samotest**: chyby väzieb XAML sú chybou testu (výnimka: prechodné hlásenie šablóny DatePicker v téme Fluent).
- **Originalita**: pôvodne približne 3 800 riadkov (13 % kódu SkVP) takmer zhodných s EviTA – prepísané, pozri nižšie.

## Vlastné riešenia namiesto kódu z EviTA (29. 10. 2026, vydané vo v0.1.115)
Moduly prevzaté z EviTA sú nanovo navrhnuté a napísané; správanie, uložené nastavenia (`tlac.volby`, `tlac.pdf`,
`tlac.obojstranne`, `pocasie.*`, `diktovanie.*`) a rozhrania pre ostatný kód zostali. Zhoda s EviTA (jscpd, 50 tokenov,
6 riadkov): **3 797 → 364 riadkov (12,8 % → 1,2 %)**; zvyšok sú najmä rovnaké dátové tvary (enumy, kľúče PDF podľa normy,
zoznam modelov Whisper) a nastavovanie ovládacích prvkov.
- **Tlač a PDF**: `Impozicia` – jeden výpočet rozmiestnenia strán (mriežka 1/2/4/8, brožúra, opakovanie, dierovač,
  zväčšenie bez orezania obsahu) pre tlač, náhľad, PDF aj obrázky; kreslenie cez `IPlatnoListu` (`ListyTlace` pre WPF,
  `SkladaniePdf` pre PDF) namiesto dvoch kópií tej istej geometrie. `PlanTlace` – tlačové úlohy obojstrannej tlače
  (duplex, ručné otáčanie, poradie, otočenie zadných strán) ako čistý výpočet; strany sa kreslia až pri tlači.
  `TlacoveFronty`, `SkusobnyList`, sprievodca obojstrannou tlačou riadený údajmi, `ProfilObojstrannej.ZDuplexu/ZRucneho`.
  `VolbyPdf` a vlastnosti formátov (`FormatyVystupu`) v samostatnom súbore, `PrilohaPdf.Word`. `PdfNorma`: XMP cez
  System.Xml.Linq, prírastková aktualizácia z posledného traileru (overené qpdf a pikepdf: PDF/A-2b, PDF/A-3b s prílohou,
  PDF/X-4). Viacstranový TIFF bez všetkých nekomprimovaných strán v pamäti.
- **Diktovanie**: `Mikrofon` – waveIn so signálom z Windows (CALLBACK_EVENT) namiesto opakovaného zisťovania, buffery
  v poradí plnenia, jeden blok pamäte; `PrepisHlasu` – jeden prístup k modelu pod zámkom, WAV cez `ReadOnlySpan`;
  `Stahovanie` – spoločné sťahovanie modelu aj inštalátora, SHA-256 počas sťahovania, súbor až po overení;
  `HlasovePisanie` s úniou INPUT (správna veľkosť na 32 aj 64 bitoch).
- **Ostatné**: `PocasieService` a `AktualizaciaService` – odpovede služieb cez typové záznamy System.Text.Json,
  spoločný HttpClient; `CitaniePdf` – jazyk OCR z dostupných jazykov Windows, riadky textu lineárne; `Dialogy` – tichý
  režim samotestu na jednom mieste; `Prevodniky` – spoločné predky; `Databaza.CitajJson/ZapisJson`; `Preklad.VelkePrve`.
- **Samotest** doplnený: rozmiestnenie strán (brožúra, 2 a 8 na list, opakovanie, dierovač, zväčšenie), plán tlačových
  úloh, odpovede sprievodcu a profil tlačiarne, staršie a poškodené uložené voľby tlače.

## Rozloženie strany z dokumentu a jednotný vzhľad (9. 11. 2026, vydané vo v0.1.115)
Podnet z testovania na reálnych osnovách: osnova vo Worde na šírku sa v programe zobrazovala na výšku s hluchými miestami.
- **Model**: `Sekcia(RozlozenieStrany)` – od tohto miesta iná veľkosť/orientácia strany (ako prvý blok = celý dokument;
  A4 na výšku blok nemá), `ZlomStrany`, `Tabulka.Podiel` (šírka tabuľky voči textu – úzka tabuľka zostane úzka).
- **Čítanie Wordu** (`DocxCitac.Telo`): časti (sekcie) s veľkosťou a orientáciou strany, zlomy strán (aj „zlom pred odsekom“),
  šírka tabuliek podľa šírky textu časti.
- **Náhľad a editor**: list na šírku pre osnovu na šírku, sivá značka „nová strana“, `DokumentOsnovy.Upratane` zlúči
  prázdne riadky a vynechá zbytočné zlomy; rozloženie strany sa pri úprave v programe zachová (`DocxVystup.Osnova`
  zapisuje časti so `sectPr`).
- **Zostavy**: PDF (`PdfVystup.Sadzba`) a Word (`DocxVystup.CastiZostavy`) – predmet s osnovou na šírku má strany na šírku
  (aj oddeľujúca strana), hlavička a päta pre šírku strany; oblasti a charakteristiky sú na výšku. Tlač: strana inej
  orientácie ako list sa otočí o 90° (`Impozicia.Transformacia`), každá strana sa vykresľuje vo svojej orientácii
  (`NahladDokumentu.StranaNaSirku` podľa orientácie MigraDoc).
- **Jednotný vzhľad…** (tlačidlo pri osnove aj charakteristike, `Services/JednotnyVzhlad`, `Views/JednotnyVzhladWindow`):
  dokument v štýle osnov programu (testovacia sada) – nadpisy častí z tučného/veľkého textu alebo jednopoložkového
  číslovaného zoznamu („1. CHARAKTERISTIKA PREDMETU“ → nadpis 1), zvýraznené krátke riadky → nadpis 2, hlavička tabuliek
  (nie pri tabuľke popis – hodnota), bez hluchých miest, rozloženie strany zostane. Náhľad nový/pôvodný, upozornenie na
  obrázky a textové polia, pôvodná verzia do archívu („Pred jednotným vzhľadom“).
- Samotest: osnova na šírku Word → program → Word, zostava so zmiešanými stranami (PDF „VVSSVV…“, Word 3 časti s pätou),
  snímky `13l_jednotny_vzhlad.png`, `13m_…povodny.png`, `osnova_na_sirku.png`.

## Nepravidelné tabuľky z Wordu a záznamy činnosti (9. 11. 2026, vydané vo v0.1.115)
Podnet: po importe osnov (Word aj PDF) zostava padala s „GetMinMergedCell: Unexpected problem #1“ (MigraDoc).
- **Príčina**: tabuľky Wordu s krížovými/neúplnými zlúčeniami (`gridBefore`, chýbajúce bunky, pokračovanie `vMerge`
  bez začiatku) – čítanie posúvalo bunky do iných stĺpcov a sadzba orezávala zlúčenia len na počet stĺpcov, takže sa
  prekrývali so zvislými zlúčeniami.
- **Oprava**: `Tabulka.Pravidelna()` – bunky do voľných políčok, zlúčenia orezané tak, aby sa neprekrývali, nadbytočný
  text sa pripojí k susednej bunke (nič sa nestratí); volá sa pri čítaní Wordu, v PDF, vo Worde aj v editore.
  `DocxCitac.NacitajTabulku` číta bunky na ich stĺpcoch (medzery = prázdne bunky).
- **Diagnostika**: keď zostava zlyhá, `PdfVystup.ChybneCasti` vysádza každú osnovu zvlášť a hlásenie vymenuje tie, ktoré
  sa nedajú vysádzať.
- **Záznamy činnosti** (`Pomocne/Zaznam`): denné súbory `Záznamy/skvp-RRRR-MM-DD.log` pri dátach (30 dní) – spustenie
  (verzia, systém), chyby s technickými podrobnosťami (aj neošetrené), import osnov; Servis → Záznamy činnosti →
  „Uložiť záznamy (ZIP)…“ (záznamy + o-programe.txt) a „Otvoriť priečinok“.
- Samotest: krížové zlúčenia v zostave PDF aj Word, `gridBefore` a osamotené pokračovanie zlúčenia, záznam a balík ZIP.
- **Druhá príčina (z logu používateľa, v0.1.115: Nemecký a Ruský jazyk B1)**: MigraDoc 6.1 zlyhá aj na pravidelnej
  tabuľke, ak v niektorom riadku nezačína žiadna bunka bez zvislého zlúčenia (všetky bunky riadka zlúčené nadol alebo
  riadok celý pokrytý zlúčeniami) – jeho `GetMinMergedCell` hľadá len bunky s `MergeDown == 0`. `PdfVystup.Mriezka`
  v takom riadku rozdelí zvislé zlúčenie na časti bez čiar medzi nimi. Samotest: schodovité zlúčenia a 300 náhodných
  tabuliek (vydané vo v0.1.119).

## Šablóny vzhľadu osnov (9. 11. 2026, vydané vo v0.1.125)
Podnet: „Jednotný vzhľad“ pri importovanej osnove nič nezmenil (program zobrazuje osnovy vo svojom štýle už od importu)
a používateľ chcel viac vzhľadov – centrálne pre všetky dokumenty, farebné a moderné.
- **`Services/VzhladDokumentov`**: `SablonaVzhladu` (písma, veľkosti v bodoch, riadkovanie, medzera za odsekom, text do
  bloku, farba nadpisov – oblasť / akcent / tmavá, ozdoba nadpisu 1 – čiara / pásik / farebný pás, tabuľky – jemná
  mriežka / výrazná mriežka / vodorovné čiary, hlavička sivá / odtieň / plná farba, pruhy). Katalóg 9 šablón:
  Pastelový (pôvodný, predvolený), Moderný, Farebný, Svieži, Elegantný, Klasický, Úradný, Úsporný, Čitateľný.
  Zvolená šablóna v nastavení `dokumenty.sablona` (prenáša sa v exporte nastavení), `Docasne` pre náhľad a samotest.
  Písma len také, ktoré PDFsharp vie vložiť (Cambria v .ttc nie – `Najdi` overí cez XFont).
- **Editor** (`DokumentOsnovy.KDokumentu/NastavNadpis/StylBunky/NovaTabulka`), **PDF** (`PdfVystup.Styly`, nadpisy,
  `Ozdob`, tabuľky, hlavička predmetu) a **Word** (`DocxVystup.Styly`, nadpisy, `Ozdob`, tabuľky) podľa šablóny.
  Zarovnanie do bloku je zo šablóny (v modeli sa ukladá len odsek zarovnaný inak ako vľavo); pruhy tabuliek sú len
  vzhľad (hlavička sa v editore rozpozná len v prvom riadku). Otvorené editory sa po zmene šablóny prekreslia;
  Enter na konci nadpisu vytvorí bežný odsek. Rám tabuľky v PDF je zarovno s okrajmi textu.
- **Okno Vzhľad osnov** (`Views/VzhladOsnovWindow`, tlačidlo na stránke Osnovy aj „Vzhľad…“ pri dokumente – nahradilo
  okno Jednotný vzhľad): galéria s miniatúrami vo farbe oblasti, náhľad na vybranom dokumente (alebo ukážkovej osnove),
  voliteľné upratanie textu všetkých dokumentov alebo len otvoreného (`JednotnyVzhlad.Uprac` – archivácia „Pred
  upratovaním textu“, dokumenty s obrázkami a otvorené vo Worde sa vynechajú).
- Samotest: každá šablóna – PDF (obrázky `sablona_N_id.png`), Word (OpenXML), editor a Word bez zmeny textu, zarovnania,
  hlavičiek a tučného písma; snímky `13l_vzhlad_osnov.png`, `13m_vzhlad_N_id.png`.

## Ďalšie šablóny, skupiny a vlastná šablóna (9. 11. 2026, vydané vo v0.1.129)
Podnet: „vytvor lepšie šablóny, tie tam ponechaj… prehľadaj internet pre inšpiráciu a vytvor niečo veľké“.
- Inšpirácia: odporúčania typografie správ (text 11 b, nadpisy 14–20 b, riadkovanie 1,15–1,3, viac miesta nad nadpisom,
  tmavomodré nadpisy, hlavičky tabuliek tmavomodré s bielym textom alebo svetlosivé, pruhy, akademické tabuľky s tromi
  čiarami), schémy písiem Office (nadpisy / text), zásady tabuliek (len vodorovné čiary, pruhy, výrazná hlavička).
- **26 šablón v 4 skupinách** (`SablonaVzhladu.Skupina`): Farebné (Pastelový, Farebný, Pestrý, Tyrkysový, Korálový,
  Levanduľový, Smaragdový, Oceán), Moderné (Moderný, Svieži, Minimalistický, Indigo, Grafitový, Časopis, Firemný),
  Klasické (Elegantný, Klasický, Akademický, Knižný, Archívny, Úradný), Praktické (Úsporný, Kompaktný, Čitateľný,
  Prezentačný, Na tlač).
- Nové prvky: ozdoby nadpisu `Karta` (sýta farba, biely text), `Nadciarka`, `Ramik`; `Nadpis2Pasik`; tabuľky
  `RamTabulky.Akademicky` a `HlavickaTabulky.Tmava` (biely text – štýl `SkvpTabulkaSvetla` v PDF aj Worde, v editore
  len farba bunky, aby sa tučné písmo nepreniesla do textu); `FarbaTextu`.
- **Vlastná šablóna**: v okne „Prispôsobiť šablónu…“ – farba (14 sýtych farieb alebo farby oblastí), písma nadpisov a
  textu (len písma, ktoré sa dajú vložiť do PDF), veľkosť textu (nadpisy v rovnakom pomere), text do bloku.
  `VzhladDokumentov.Prisposob` → Id `vlastna`, uložená ako JSON v `dokumenty.sablona_vlastna`, v galérii prvá.
- Galéria so skupinami (prepínače nad zoznamom) a počtom šablón.
- Samotest: 26 šablón + vlastná (PDF, Word, editor), JSON vlastnej šablóny, skupiny, prispôsobenie v okne,
  snímky `13n_vzhlad_skupina_klasicke.png`, `13o_vzhlad_prisposobenie.png`.

## Export osnov „ako pri importe“ (12. 11. 2026, vydané vo v0.1.140)
Podnet: upravené osnovy (štýly, zhustenie, úpravy v programe) uložiť do priečinkov tak, ako boli zdrojové súbory pri
importe – s voľbou formátu (ako pôvodné / všetko Word / všetko PDF) a názvov (ako pôvodné / ako nové).
- Import si pamätá cestu súboru v priečinku importu: `InfoOsnovy.CestaZdroja` (kľúč `cestaZdroja`,
  „03 Človek a príroda/Fyzika.pdf“; pri jednotlivom súbore len názov), `PolozkaImportu.Koren/CestaVImporte`.
- `ExportOsnov.UlozAkoPriImporte(volby, FormatPodlaImportu, povodneNazvy, priecinok, povodnyPriecinok)`: podpriečinky
  podľa cesty importu; osnovy zo staršieho importu (len `Zdroj`) sa nájdu podľa názvu v pôvodnom priečinku (pri
  viacerých rovnakých názvoch ten v priečinku oblasti), inak priečinok oblasti; osnovy bez importu do priečinka oblasti
  s novým názvom (Word). Existujúce súbory sa neprepíšu (`ImportOsnov.VolnaCesta` – „(2)“), do pôvodného priečinka
  importu sa ukladať nedá. Výsledok `VysledokExportuOsnov` (podľa importu / bez importu).
- Okno exportu: tretia voľba „Ako pri importe“ (`SposobExportu`), formát a názvy súborov, pôvodný priečinok importu,
  prehľad, koľko osnov pozná svoj súbor; snímka `13g2_okno_export_ako_pri_importe.png`.
- Samotest: priečinky, pôvodné názvy a formáty, starší import podľa pôvodného priečinka (archív sa preskočí), všetko PDF
  s novými názvami, neprepisovanie, ochrana pôvodného priečinka, okno exportu, zaznamenaná cesta pri importe.

## Úvodná strana oblasti na jednej strane (12. 11. 2026, vydané vo v0.1.140)
Podnet: zoznam predmetov na oddeľujúcej strane oblasti (Jazyk a komunikácia, 12 predmetov) pretiekol na druhú stranu.
- `Services/UvodnaStranaOblasti.Pre(oblast, sablona, sirkaTextu)`: odhad výšky zoznamu (zalamovanie názvov podľa šírky
  stĺpca a písma šablóny); najprv sa zmenší voľné miesto nad nadpisom (plné 269 b = 4,5 cm + 5 cm, najmenej 28 b), potom
  5 stupňov písma a riadkov zoznamu (15/12,5 b → 10,5/8,5 b). Riadky zoznamu majú jednoduché riadkovanie a žiadnu medzeru
  za odsekom (inak by sa pridalo riadkovanie a medzera šablóny).
- PDF aj Word (vo Worde navyše `keepNext` – zoznam drží spolu, medzera nad nadpisom v rovnakom pomere).
- Samotest: 12, 16 a 20 predmetov s dlhými názvami v šablónach Pastelový, Čitateľný a Prezentačný → počet strán =
  úvodná strana + strana na predmet; obrázky `uvodna_strana_oblasti_12.png`, `uvodna_strana_oblasti_20.png`.

## Súbor s jedným predmetom bez zbytočnej strany (12. 11. 2026, vydané vo v0.1.144)
Podnet: súbory z exportu po jednom predmete sa vo Worde pýtali na aktualizáciu polí a začínali stranou s nadpisom oblasti.
- `VolbyZostavy.NadpisOblasti` (pri `ExportOsnov.PredmetyZvlast` = false): nadpis oblasti sa v PDF ani vo Worde nevypíše.
- Prepnutie rozloženia na prázdnej časti ju nahradí (`Sadzba.Prepni` – odstráni prázdnu sekciu MigraDoc; `CastiZostavy.Prepni`
  – bez zlomu časti, ak časť ešte nemá obsah) – osnova na šírku začína na 1. strane.
- Word: `UpdateFieldsOnOpen` len pri obsahu alebo oddeľujúcej strane oblasti (čísla strán predmetov).
- Samotest: súbor s jedným predmetom bez nadpisu oblasti a bez aktualizácie polí, osnova na šírku od 1. strany (PDF aj Word).

## Bez ISCED a bez „Vytvorené … v programe SkVP“ (12. 11. 2026, na vetve vyvoj/etapa1, nevydané)
Podnet: v žiadnom dokumente (zostava aj súbory po jednom predmete) nesmie byť označenie ISCED (už sa nepoužíva) ani
zmienka, kedy a v akom programe dokument vznikol.
- `TitulnaStrana.BezIsced` (regex) – druh školy a riadky úvodnej strany; aplikuje sa pri načítaní `UdajeSkVP`, štruktúre
  osnov, nastaveniach, vstavanom učebnom pláne (JSON už bez ISCED) a importe plánu z Excelu; `AktualnyStupenGymnazia` =
  „Gymnázium – úplné stredné všeobecné vzdelanie“.
- Text dole na úvodnej strane je predvolene prázdny; uložený text so „v programe SkVP“ zo starších verzií sa pri načítaní
  vynechá (vlastný text zostane).
- Vlastnosti PDF: Creator = autor (škola), program sa nespomína.

## Plánované (z celkového zadania používateľa)
6. Priečinky: zdrojový (len čítať) → pracovný → výstupný; rozpoznanie predmetu v dokumente, premenovanie „01 Názov predmetu“,
   prevod PDF → Word vlastnými prostriedkami (bez Wordu); archivácia starých osnov s rokom v názve a záznamom.
7. Sekcie: Dashboard ako v EviTA (+ zaujímavosti k osnovám, učebnému plánu, predmetom, učiteľom), Úlohy a Poznámky ako v EviTA,
   Import a export (DOC, PDF, PDF/A-2b, PDF/A-3b, PDF/X, JPEG, PNG, TIFF – kód je v EviTA), import/export oblastí a predmetov
   z dokumentu, Nastavenia (aj export/import nastavení, jazyk), Servis (zálohy osnov a všetkých dát).
8. Automatický update z GitHubu – hotový základ (kontrola pri spustení, tichá inštalácia).
