# SkVP – stav projektu a pokyny pre ďalšiu session

Aktualizované: 23. 10. 2026 · posledné vydanie **v0.1.16** (`main`); na vetve `vyvoj/etapa1` je navyše logo a hlavička strán

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

## Logo a hlavička strán (23. 10. 2026, na vetve vyvoj/etapa1, čaká na vydanie)
- Úvodná strana: logo školy sa dá vybrať/odstrániť priamo v okne „Úvodná strana a hlavička“ (rovnaké logo ako v Nastaveniach)
  a vypnúť zaškrtávacím políčkom (`titul.logo.zobrazit`).
- Hlavička ďalších strán: vľavo text (predvolene nadpis – podnadpis „Školský vzdelávací program – Učebné osnovy“, dá sa prepísať;
  rovnaký text ako nadpis sa neukladá, aby sledoval nadpis), vpravo názov školy a logo (výška 0,8 cm, najviac 3,5 cm široké).
  Každá časť sa vypína zvlášť (`titul.hlavicka.text|skola|logo.zobrazit`) a celá hlavička naraz (`titul.hlavicka.zobrazit`,
  jednotlivé voľby sa pritom pamätajú). Okno ukazuje živú ukážku hlavičky.
- PDF/tlač: hlavička je tabuľka (MigraDoc) s čiarou dole; Word: tabuľka v hlavičke + prázdny odsek, obrázok v časti hlavičky;
  pri vypnutej hlavičke sa časť hlavičky do Wordu nepridá.
- Samotest `TestLogaAHlavicky`: hľadá body skúšobného loga na vykreslených stranách, kontroluje hlavičky a obrázky vo Worde,
  snímky `uvodna_strana_logo.png`, `hlavicka_*.png` na vetve `vystupy`.

## Plánované (z celkového zadania používateľa)
6. Priečinky: zdrojový (len čítať) → pracovný → výstupný; rozpoznanie predmetu v dokumente, premenovanie „01 Názov predmetu“,
   prevod PDF → Word vlastnými prostriedkami (bez Wordu); archivácia starých osnov s rokom v názve a záznamom.
7. Sekcie: Dashboard ako v EviTA (+ zaujímavosti k osnovám, učebnému plánu, predmetom, učiteľom), Úlohy a Poznámky ako v EviTA,
   Import a export (DOC, PDF, PDF/A-2b, PDF/A-3b, PDF/X, JPEG, PNG, TIFF – kód je v EviTA), import/export oblastí a predmetov
   z dokumentu, Nastavenia (aj export/import nastavení, jazyk), Servis (zálohy osnov a všetkých dát).
8. Automatický update z GitHubu – hotový základ (kontrola pri spustení, tichá inštalácia).
