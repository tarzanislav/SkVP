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
  (osnova ako **.docx priamo v DB**), `verzia` (archív starších verzií), `nastavenie`.
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
  2025/2026“) a priečinok Dokumenty\SkVP\Archív školských rokov\2025-2026 (Word po oblastiach, celé osnovy Word + PDF/A-2b),
  zoznam v `archiv.skolske_roky`.
- Export „Doc“ = .docx; starý .doc by vyžadoval Word (zatiaľ nerobené, čaká na rozhodnutie používateľa).

## Plánované (z celkového zadania používateľa)
6. Priečinky: zdrojový (len čítať) → pracovný → výstupný; rozpoznanie predmetu v dokumente, premenovanie „01 Názov predmetu“,
   prevod PDF → Word vlastnými prostriedkami (bez Wordu); archivácia starých osnov s rokom v názve a záznamom.
7. Sekcie: Dashboard ako v EviTA (+ zaujímavosti k osnovám, učebnému plánu, predmetom, učiteľom), Úlohy a Poznámky ako v EviTA,
   Import a export (DOC, PDF, PDF/A-2b, PDF/A-3b, PDF/X, JPEG, PNG, TIFF – kód je v EviTA), import/export oblastí a predmetov
   z dokumentu, Nastavenia (aj export/import nastavení, jazyk), Servis (zálohy osnov a všetkých dát).
8. Automatický update z GitHubu – hotový základ (kontrola pri spustení, tichá inštalácia).
