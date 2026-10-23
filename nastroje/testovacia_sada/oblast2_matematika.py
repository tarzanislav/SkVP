from spolocne import *

OBL = "Matematika a práca s informáciami"

O(OBL, [
    "Vzdelávacia oblasť Matematika a práca s informáciami rozvíja matematické a informatické myslenie, schopnosť riešiť problémy, "
    "pracovať s údajmi a používať digitálne technológie zodpovedne a tvorivo.",
    "Matematika vedie žiakov k presnému vyjadrovaniu, logickému usudzovaniu a modelovaniu situácií z reálneho sveta. Informatika rozvíja "
    "algoritmické myslenie, porozumenie princípom fungovania počítačov a sietí a schopnosť riešiť problémy pomocou programovania.",
])

P("Matematika", OBL, "ŠVP",
  char=[
      "Matematika rozvíja matematickú kompetenciu – schopnosť používať matematické myslenie na riešenie problémov v bežnom živote, v ďalšom "
      "štúdiu a v povolaní. Dôraz sa kladie na porozumenie, postupy a aktivitu žiaka, nie na mechanické zapamätanie.",
      "Obsah je rozdelený do tematických okruhov: Čísla, premenné a počtové výkony s číslami; Vzťahy, funkcie, tabuľky, diagramy; Geometria "
      "a meranie; Kombinatorika, pravdepodobnosť, štatistika; Logika, dôvodenie, dôkazy. Žiaci, ktorí z matematiky maturujú, dosahujú "
      "vyšší stupeň formalizácie a abstrakcie.",
  ],
  ciele=[
      "rozvíjať logické a kritické myslenie a argumentáciu",
      "čítať s porozumením texty s číslami, tabuľky, grafy a diagramy",
      "získavať a spracúvať informácie a interpretovať výsledky",
      "osvojiť si základné matematické pojmy a postupy a používať ich v praxi",
      "chápať matematiku ako súčasť ľudskej kultúry",
  ],
  roc=[
      ("1. ročník", [
          ("Logika, dôvodenie, dôkazy", "výrok, pravdivostná hodnota; logické spojky – negácia, konjunkcia, disjunkcia, implikácia, ekvivalencia; kvantifikátory; de Morganove pravidlá; protipríklad",
           "rozhodne, či je tvrdenie výrok, a určí pravdivosť zloženého výroku; utvorí negáciu výroku s kvantifikátorom; vyvráti tvrdenie protipríkladom"),
          ("Čísla a počtové výkony", "množiny čísel N, Z, Q, R; deliteľnosť, prvočísla; mocniny s celočíselným exponentom, odmocniny; vedecký zápis; zaokrúhľovanie, platné číslice",
           "zvolí vhodný spôsob výpočtu a správne zaokrúhli výsledok; zapíše číslo vo vedeckom tvare a použije ho pri odhadoch; upraví výraz s mocninami a odmocninami"),
          ("Rovnice a nerovnice", "lineárne rovnice a nerovnice; sústava dvoch lineárnych rovníc; kvadratická rovnica, diskriminant; rovnice s absolútnou hodnotou",
           "nájde všetky riešenia lineárnej a kvadratickej rovnice a zdôvodní úpravy; rieši sústavu rovníc dosadzovacou aj sčítacou metódou; rieši slovné úlohy vedúce na rovnice"),
          ("Planimetria", "uhly, trojuholník a jeho prvky; zhodnosť a podobnosť trojuholníkov; Pytagorova a Euklidove vety; kružnica, kruh, obsah a obvod rovinných útvarov",
           "rozhodne o zhodnosti a podobnosti trojuholníkov; vypočíta dĺžky pomocou Pytagorovej a Euklidových viet; zostrojí trojuholník podľa rozboru"),
      ]),
      ("2. ročník", [
          ("Funkcie", "funkcia, definičný obor, obor hodnôt, graf; monotónnosť, extrémy, ohraničenosť; lineárna, kvadratická, lineárna lomená funkcia; inverzná funkcia",
           "zostrojí graf lineárnej a kvadratickej funkcie a určí vrchol paraboly; z grafu určí vlastnosti funkcie; rieši rovnice a nerovnice graficky"),
          ("Exponenciálna a logaritmická funkcia", "mocninová, exponenciálna a logaritmická funkcia; logaritmus, vety o logaritmoch; exponenciálne a logaritmické rovnice; exponenciálny rast a pokles",
           "rozlíši lineárnu a exponenciálnu závislosť; vyrieši jednoduchú exponenciálnu rovnicu; použije logaritmus pri úlohách z praxe (úroky, rozpad)"),
          ("Goniometria", "oblúková miera; goniometrické funkcie ostrého uhla a všeobecného uhla; grafy; goniometrické rovnice; sínusová a kosínusová veta",
           "vypočíta prvky pravouhlého a všeobecného trojuholníka; načrtne graf goniometrickej funkcie; rieši jednoduché goniometrické rovnice"),
          ("Percentá a finančná matematika", "percento, promile; jednoduché a zložené úrokovanie; úver, splátka, inflácia",
           "porovná výhodnosť pôžičiek a vkladov; vypočíta zložené úrokovanie; interpretuje údaje o úveroch v percentách"),
      ]),
      ("3. ročník", [
          ("Analytická geometria v rovine", "súradnice bodu a vektora; operácie s vektormi; parametrické a všeobecné vyjadrenie priamky; vzájomná poloha priamok; vzdialenosť bodu od priamky; kružnica",
           "vypočíta súradnice vektora a jeho veľkosť; napíše rovnicu priamky a určí vzájomnú polohu dvoch priamok; určí stred a polomer kružnice z rovnice"),
          ("Stereometria", "polohové a metrické vlastnosti v priestore; voľné rovnobežné premietanie; rezy kocky; hranoly, ihlany, valec, kužeľ, guľa – objem a povrch",
           "zobrazí teleso vo voľnom rovnobežnom premietaní; zostrojí rez kocky rovinou; vypočíta objem a povrch telesa v úlohe z praxe"),
          ("Kombinatorika", "pravidlo súčtu a súčinu; permutácie, variácie, kombinácie (aj s opakovaním); faktoriál, kombinačné číslo; binomická veta",
           "zistí počet možností systematickým vypisovaním aj vzorcom; rozlíši variácie a kombinácie; použije binomickú vetu"),
          ("Pravdepodobnosť a štatistika", "náhodný jav, pravdepodobnosť, doplnkový jav, nezávislé javy; štatistický súbor, početnosť; priemer, modus, medián, rozptyl, smerodajná odchýlka; diagramy",
           "vypočíta pravdepodobnosť javu klasickou definíciou; spracuje údaje do tabuľky a diagramu; vypočíta a interpretuje charakteristiky polohy a variability"),
      ]),
      ("4. ročník", [
          ("Postupnosti a rady", "postupnosť a jej vlastnosti; aritmetická a geometrická postupnosť; limita postupnosti; nekonečný geometrický rad",
           "určí člen a súčet aritmetickej a geometrickej postupnosti; rieši úlohy z praxe (úroky, rast populácie); vypočíta súčet nekonečného geometrického radu"),
          ("Úvod do diferenciálneho počtu", "limita a spojitosť funkcie; derivácia a jej geometrický význam; extrémy funkcie; priebeh funkcie",
           "vypočíta deriváciu elementárnych funkcií; nájde lokálne extrémy a intervaly monotónnosti; rieši jednoduché optimalizačné úlohy"),
          ("Systematizácia učiva", "opakovanie tematických okruhov; úlohy maturitného typu",
           "rieši komplexné úlohy kombinujúce viac tematických okruhov; zdôvodní postup riešenia"),
      ]),
  ],
  metody=METODY_PRIRODNE + ["riešenie úloh rôznymi spôsobmi a porovnanie postupov, dynamická geometria (GeoGebra)"],
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY, "Po každom tematickom celku žiaci píšu písomnú prácu; súčasťou hodnotenia je aj ústna odpoveď a riešenie domácich úloh."],
  zdroje=ZDROJE_ZAKLAD + ["zbierky úloh pre gymnáziá", "GeoGebra, tabuľkový kalkulátor"],
  stav="hotova")

P("Informatika", OBL, "ŠVP",
  char=[
      "Informatika rozvíja informatické myslenie: schopnosť formulovať problém, navrhnúť algoritmus, zapísať ho v programovacom jazyku a overiť "
      "jeho správnosť. Žiaci spoznávajú princípy fungovania počítačov, sietí a softvéru a zodpovedné správanie v digitálnom svete.",
      "Obsah je rozdelený do oblastí: Reprezentácie a nástroje; Komunikácia a spolupráca; Algoritmické riešenie problémov; Softvér a hardvér; "
      "Informačná spoločnosť.",
  ],
  ciele=[
      "riešiť problémy algoritmicky a programovať v textovom programovacom jazyku",
      "reprezentovať a spracúvať údaje – text, obraz, zvuk, tabuľky, databázy",
      "porozumieť princípom počítačových sietí a internetu",
      "dodržiavať pravidlá bezpečnosti, ochrany údajov a autorských práv",
  ],
  roc=[
      ("1. ročník", [
          ("Reprezentácie a nástroje", "kódovanie informácií, dvojková sústava; reprezentácia textu, obrazu a zvuku; tabuľkový kalkulátor – vzorce, funkcie, grafy",
           "prevedie číslo medzi desiatkovou a dvojkovou sústavou; vysvetlí rozdiel medzi rastrovou a vektorovou grafikou; spracuje údaje v tabuľkovom kalkulátore a vytvorí graf"),
          ("Algoritmické riešenie problémov I", "algoritmus, program; premenné, vstup, výstup; podmienený príkaz, cyklus; ladenie programu",
           "zapíše algoritmus v programovacom jazyku (Python); použije premenné, podmienky a cykly; nájde a opraví chybu v programe"),
          ("Informačná spoločnosť", "bezpečnosť na internete, heslá, phishing; autorské právo, licencie; digitálna stopa",
           "vytvorí bezpečné heslo a vysvetlí zásady jeho používania; rozpozná podvodnú správu; rešpektuje licencie pri použití cudzieho diela"),
      ]),
      ("2. ročník", [
          ("Algoritmické riešenie problémov II", "funkcie s parametrami; zoznamy, reťazce; práca so súbormi; jednoduché triedenie a vyhľadávanie",
           "napíše funkciu s parametrami a návratovou hodnotou; spracuje zoznam údajov; naprogramuje vyhľadávanie v zozname"),
          ("Softvér a hardvér", "časti počítača, operačný systém; súborový systém; počítačové siete, IP adresa, DNS; internet a jeho služby",
           "opíše funkciu hlavných častí počítača; vysvetlí, ako sa prenáša informácia v sieti; rozlíši klienta a server"),
          ("Komunikácia a spolupráca", "spolupráca v cloude; prezentácia informácií; databázy – tabuľka, záznam, dopyt",
           "spolupracuje na spoločnom dokumente; vyhľadá údaje v databáze pomocou dopytu"),
      ]),
      ("3. ročník", [
          ("Projekt", "analýza problému, návrh riešenia, implementácia, testovanie a prezentácia",
           "samostatne alebo v tíme navrhne a naprogramuje projekt; zdokumentuje a odprezentuje riešenie"),
          ("Umelá inteligencia a spoločnosť", "princípy strojového učenia; etika a riziká digitálnych technológií",
           "vysvetlí princíp učenia z údajov; zhodnotí prínosy a riziká umelej inteligencie"),
      ]),
  ],
  metody=METODY_PRIRODNE + ["programovanie na počítači, párové programovanie, riešenie úloh súťaže iBobor"],
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotia sa praktické úlohy na počítači, projekty a krátke testy porozumenia."],
  zdroje=ZDROJE_ZAKLAD + ["programovacie prostredie Python, tabuľkový kalkulátor, databázový systém"],
  stav="hotova")

P("Aplikovaná informatika", OBL, "ŠkVP",
  char=[
      "Predmet rozširuje informatiku o praktické zručnosti potrebné v štúdiu a v práci: pokročilú prácu s dokumentmi, tabuľkami, grafikou, "
      "tvorbu webu a základy spracovania údajov.",
  ],
  ciele=[
      "efektívne pracovať s kancelárskym softvérom",
      "vytvoriť jednoduchú webovú stránku",
      "spracovať a vizualizovať údaje",
  ],
  roc=[
      ("2. ročník", [
          ("Pokročilá práca s dokumentmi", "štýly, obsah, číslovanie strán, citácie; hromadná korešpondencia",
           "naformátuje rozsiahly dokument pomocou štýlov; vygeneruje obsah a zoznam literatúry"),
          ("Tabuľky a údaje", "kontingenčné tabuľky, podmienené formátovanie, overovanie údajov",
           "vytvorí kontingenčnú tabuľku a graf; overí správnosť vstupných údajov"),
          ("Tvorba webu", "HTML, CSS, štruktúra stránky; zverejnenie",
           "vytvorí jednoduchú webovú stránku s textom, obrázkami a odkazmi"),
      ]),
  ],
  metody=METODY_PRIRODNE,
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotí sa najmä praktická práca a záverečný projekt."],
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Informatika", "nadvazuje")])

P("Aplikovaná matematika", OBL, "ŠkVP",
  char=[
      "Predmet ukazuje využitie matematiky v praxi – vo financiách, technike, prírodných vedách a v spracovaní údajov. Žiaci riešia úlohy "
      "z reálneho života a pracujú s tabuľkovým kalkulátorom a dynamickou geometriou.",
  ],
  ciele=[
      "modelovať reálne situácie matematickými prostriedkami",
      "interpretovať výsledky výpočtov a posúdiť ich reálnosť",
      "používať digitálne nástroje pri riešení úloh",
  ],
  roc=[
      ("2. ročník", [
          ("Matematika v hospodárstve", "percentá, úroky, inflácia; rozpočet domácnosti",
           "zostaví rozpočet; porovná ponuky bánk"),
          ("Matematické modely", "lineárny a exponenciálny model; optimalizácia",
           "zostaví model situácie a interpretuje výsledok"),
          ("Spracovanie údajov", "štatistika v praxi, prieskum, grafy",
           "uskutoční jednoduchý prieskum a spracuje ho"),
      ]),
  ],
  metody=METODY_PRIRODNE,
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Matematika", "nadvazuje"), ("Finančná gramotnosť", "medzipredmetovy")])

P("Technika", OBL, "ŠkVP",
  char=[
      "Predmet rozvíja technické myslenie a praktické zručnosti. Žiaci sa oboznamujú s technickým zobrazovaním, základmi elektrotechniky, "
      "robotiky a 3D modelovania a riešia jednoduché konštrukčné úlohy.",
  ],
  ciele=[
      "čítať a kresliť jednoduchý technický výkres",
      "zostaviť jednoduchý elektrický obvod a naprogramovať mikrokontrolér",
      "navrhnúť a vymodelovať jednoduchý výrobok",
  ],
  roc=[
      ("1. ročník", [
          ("Technické zobrazovanie", "pravouhlé premietanie, kótovanie, mierka; náčrt a výkres",
           "nakreslí náčrt jednoduchého telesa v troch priemetoch; okótuje výkres"),
          ("Elektrotechnika a robotika", "elektrický obvod, súčiastky; mikrokontrolér, senzory, programovanie",
           "zostaví obvod podľa schémy; naprogramuje reakciu na údaj zo senzora"),
          ("3D modelovanie", "modelovací softvér, 3D tlač",
           "vymodeluje jednoduchý predmet a pripraví ho na tlač"),
      ]),
  ],
  metody=METODY_PRIRODNE + ["praktické činnosti v dielni a v laboratóriu"],
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotia sa praktické výrobky a projekty."],
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Fyzika", "medzipredmetovy"), ("Informatika", "medzipredmetovy")])

P("Cvičenia z matematiky", OBL, "ŠkVP",
  char=[
      "Predmet je určený žiakom, ktorí sa chcú v matematike zdokonaliť, pripravujú sa na maturitu alebo na štúdium technických a "
      "prírodovedných odborov. Prehlbuje a precvičuje učivo predmetu Matematika.",
  ],
  ciele=[
      "upevniť a prehĺbiť učivo matematiky",
      "zdokonaliť sa v riešení náročnejších úloh",
      "rozvíjať matematickú argumentáciu",
  ],
  roc=[
      ("3. ročník", [
          ("Výrazy, rovnice a nerovnice", "úpravy výrazov; rovnice a nerovnice s parametrom; sústavy rovníc",
           "rieši rovnice s parametrom a diskutuje o riešiteľnosti"),
          ("Funkcie a ich grafy", "transformácie grafov; zložená a inverzná funkcia",
           "načrtne graf transformovanej funkcie; určí inverznú funkciu"),
      ]),
      ("4. ročník", [
          ("Geometria", "analytická geometria, stereometria – náročnejšie úlohy",
           "rieši úlohy kombinujúce syntetickú a analytickú metódu"),
          ("Kombinatorika a pravdepodobnosť", "úlohy maturitného a prijímacieho typu",
           "rieši komplexné úlohy z kombinatoriky a pravdepodobnosti"),
      ]),
  ],
  metody=METODY_SEMINAR,
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD + ["zbierky úloh na prijímacie skúšky na vysoké školy"],
  suvisi=[("Matematika", "nadvazuje"), ("Matematika pre maturantov", "suvisi")])

P("Matematika pre maturantov", OBL, "ŠkVP",
  char=[
      "Predmet je určený žiakom, ktorí si zvolili matematiku ako maturitný predmet. Systematizuje celé gymnaziálne učivo a pripravuje žiakov "
      "na externú časť a ústnu formu internej časti maturitnej skúšky.",
  ],
  ciele=[
      "systematizovať učivo všetkých tematických okruhov",
      "zvládnuť úlohy externej časti maturity v časovom limite",
      "pripraviť sa na ústnu odpoveď s dôkazom a zdôvodnením",
  ],
  roc=[
      ("4. ročník", [
          ("Čísla, výrazy, rovnice", "opakovanie a systematizácia",
           "rieši úlohy maturitného typu bez chýb v úpravách"),
          ("Funkcie, postupnosti", "vlastnosti funkcií, grafy, postupnosti",
           "analyzuje funkciu a jej graf; rieši úlohy s postupnosťami"),
          ("Geometria", "planimetria, stereometria, analytická geometria",
           "vyrieši geometrickú úlohu a zdôvodní postup"),
          ("Kombinatorika, pravdepodobnosť, štatistika, logika", "systematizácia",
           "rieši úlohy a interpretuje výsledky v kontexte"),
      ]),
  ],
  metody=METODY_SEMINAR,
  hodn=HODNOTENIE_ZAKLAD + ["Pravidelne sa píšu cvičné maturitné testy."],
  zdroje=ZDROJE_ZAKLAD + ["testy externej časti maturity z predchádzajúcich rokov"],
  stav="na_kontrolu",
  suvisi=[("Matematika", "nadvazuje")])

P("Informatika pre maturantov", OBL, "ŠkVP",
  char=[
      "Predmet pripravuje žiakov na maturitnú skúšku z informatiky. Systematizuje teóriu a rozvíja programátorské zručnosti potrebné "
      "na praktickú časť maturity.",
  ],
  ciele=[
      "systematizovať učivo informatiky",
      "zdokonaliť sa v programovaní a riešení algoritmických úloh",
      "pripraviť maturitný projekt alebo praktickú úlohu",
  ],
  roc=[
      ("4. ročník", [
          ("Algoritmy a programovanie", "triedenie, rekurzia, práca so súbormi, zložitosť algoritmov",
           "naprogramuje riešenie maturitnej úlohy; porovná efektívnosť algoritmov"),
          ("Reprezentácia údajov a siete", "kódovanie, kompresia, šifrovanie; siete a internet",
           "vysvetlí princíp kompresie a šifrovania; opíše cestu paketu sieťou"),
          ("Maturitné témy", "systematizácia oblastí informatiky",
           "súvisle odpovie na maturitnú tému s ukážkou na počítači"),
      ]),
  ],
  metody=METODY_SEMINAR,
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Informatika", "nadvazuje")])
