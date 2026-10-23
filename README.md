# SkVP – školský vzdelávací program

Program pre Windows na evidenciu, spracovanie a archiváciu školského vzdelávacieho programu (ŠkVP).
Prvý modul: **Učebné osnovy**.

## Čo program vie

- **Vzdelávacie oblasti a predmety** – štruktúra sa načíta z tabuľky Excel (názov oblasti v stĺpci A, predmety pod ním
  v stĺpci B). Predmety sa dajú pridávať, premenovať, odstraňovať (s možnosťou vrátiť späť) a presúvať vyššie či nižšie
  aj do inej oblasti (šípkami, klávesmi Alt+↑/↓ alebo ťahaním myšou).
- **Osnovy ako dokumenty Word** – osnova každého predmetu je dokument Word (.docx) uložený priamo v databáze.
  Zobrazuje sa na čítanie; upravuje sa vo Worde (ak je nainštalovaný) alebo vo vstavanom editore programu.
  Pred každou úpravou sa doterajšia verzia automaticky zarchivuje.
- **Súvislosti** – ľubovoľný predmet sa dá prepojiť s iným predmetom alebo oblasťou (nadväznosť, predpoklady,
  medzipredmetové vzťahy).
- **Tlač a zostavy** – spojenie osnov všetkých, jednej alebo vybraných oblastí do jedného dokumentu s úvodnou stranou
  (škola, logo), obsahom a oddeľujúcimi stranami. Tlač, PDF a Word – bez potreby Wordu.
- **Zálohy** – automaticky raz za deň, číslované za sebou (Dokumenty\SkVP\Zálohy).
- **Aktualizácie** – program sám ponúkne novú verziu z GitHubu.

## Technológie

C# / .NET 10, WPF (vzhľad Fluent – Windows 11), SQLite. Knižnice s otvorenou licenciou bez poplatkov:
ClosedXML, DocumentFormat.OpenXml, PDFsharp-MigraDoc, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite (všetky MIT).

## Zostavenie

Každý push zostaví program v GitHub Actions, spustí samotest (`SkVP.exe --samotest`) a vytvorí inštalátor.
Vývojové vetvy `vyvoj/**` ukladajú snímky obrazovky a vzorové PDF do vetvy `vystupy`; verzia sa zverejní
len z vetvy `main`.
