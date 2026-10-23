from spolocne import *

# ---------------------------------------------------------------------------------------------
OBL = "Umenie a kultúra"
O(OBL, [
    "Vzdelávacia oblasť Umenie a kultúra rozvíja estetické vnímanie, kultúrne povedomie a tvorivosť žiakov. Vedie ich k porozumeniu "
    "umeleckým dielam, k rešpektu ku kultúrnemu dedičstvu a k vlastnému tvorivému vyjadreniu.",
])

P("Umenie a kultúra", OBL, "ŠVP",
  char=[
      "Predmet Umenie a kultúra integruje výtvarné umenie, hudbu, film, divadlo, architektúru a dizajn. Žiaci poznávajú umenie v kontexte "
      "doby, rozvíjajú vizuálnu a mediálnu gramotnosť a vlastný kultúrny vkus.",
  ],
  ciele=[
      "vnímať a interpretovať umelecké diela rôznych druhov a období",
      "poznať slovenské a európske kultúrne dedičstvo",
      "kriticky posudzovať mediálne a reklamné posolstvá",
      "tvorivo sa vyjadriť prostriedkami umenia",
  ],
  roc=[
      ("1. ročník", [
          ("Kultúra a umenie", "pojem kultúra, druhy umenia; kultúrne dedičstvo, pamiatky UNESCO na Slovensku",
           "vysvetlí pojmy kultúra a umenie; predstaví pamiatku UNESCO na Slovensku"),
          ("Vizuálna kultúra", "výtvarné umenie od praveku po súčasnosť – kľúčové diela a štýly; architektúra",
           "rozpozná umelecký sloh podľa znakov; interpretuje vybrané výtvarné dielo"),
      ]),
      ("2. ročník", [
          ("Hudba a hudobná kultúra", "hudobné obdobia, žánre, ľudová hudba; súčasná populárna hudba",
           "zaradí skladbu do hudobného obdobia; porovná ľudovú a umelú hudbu"),
          ("Médiá, film a dizajn", "mediálna výchova, reklama; filmová reč; dizajn v každodennom živote",
           "analyzuje reklamu a jej manipulatívne prvky; napíše recenziu filmu"),
      ]),
  ],
  metody=["návštevy galérií, koncertov a divadelných predstavení", "analýza a interpretácia diel, diskusia",
          "tvorivé projekty – fotografia, video, plagát", "práca s digitálnymi zbierkami múzeí a galérií"],
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotí sa aktivita, projekty a prezentácie; predmet môže byť hodnotený aj slovne."],
  zdroje=ZDROJE_ZAKLAD + ["digitálne zbierky galérií a múzeí"],
  stav="hotova",
  suvisi=[("Slovenský jazyk a literatúra", "medzipredmetovy"), ("Dejepis", "medzipredmetovy")])

# ---------------------------------------------------------------------------------------------
OBL = "Človek a hodnoty"
O(OBL, [
    "Vzdelávacia oblasť Človek a hodnoty vedie žiakov k rozvoju morálneho úsudku, k úcte k sebe a druhým, k zodpovednosti a "
    "k prosociálnemu správaniu. Žiak si volí etickú výchovu alebo náboženskú výchovu.",
])

P("Etická výchova", OBL, "ŠVP",
  char=[
      "Etická výchova rozvíja morálne usudzovanie a prosociálne správanie žiakov. Pracuje zážitkovou formou, cez reflexiu skúseností, "
      "diskusiu a riešenie modelových situácií.",
  ],
  ciele=[
      "rozvíjať sebapoznanie, sebaúctu a empatiu",
      "riešiť konflikty a dilemy na základe etických princípov",
      "zaujať zodpovedný postoj k aktuálnym etickým otázkam",
  ],
  roc=[
      ("1. ročník", [
          ("Komunikácia a vzťahy", "asertivita, empatia, aktívne počúvanie; riešenie konfliktov",
           "uplatní asertívne správanie v modelovej situácii; navrhne riešenie konfliktu"),
          ("Etika a morálka", "hodnoty, svedomie, morálny úsudok; etické teórie",
           "vysvetlí rozdiel medzi etikou a morálkou; zdôvodní riešenie etickej dilemy"),
      ]),
      ("2. ročník", [
          ("Aplikovaná etika", "bioetika, etika médií, ekologická etika; ľudská dôstojnosť",
           "zaujme argumentovaný postoj k bioetickej otázke"),
          ("Prosociálnosť a dobrovoľníctvo", "pomoc druhým, solidarita; dobrovoľnícky projekt",
           "naplánuje a zrealizuje dobrovoľnícku aktivitu"),
      ]),
  ],
  metody=["zážitkové aktivity a hry s reflexiou", "diskusia, riešenie dilem a prípadových štúdií", "projektová práca"],
  hodn=["Predmet sa hodnotí slovne: absolvoval / neabsolvoval, v súlade s rozhodnutím pedagogickej rady."],
  zdroje=ZDROJE_ZAKLAD,
  stav="hotova")

P("Náboženská výchova", OBL, "ŠVP",
  char=[
      "Náboženská výchova sa vyučuje podľa učebných osnov schválených príslušnou cirkvou alebo náboženskou spoločnosťou. Vedie žiakov "
      "k poznaniu náboženskej tradície, k hľadaniu zmyslu života a k úcte k druhým.",
  ],
  ciele=[
      "poznať základy viery a náboženskej tradície",
      "uvažovať o zmysle života a hodnotách",
      "rozvíjať dialóg a toleranciu medzi ľuďmi rôzneho vyznania",
  ],
  roc=[
      ("1. ročník", [
          ("Človek a viera", "otázky zmyslu života; viera a rozum; Biblia",
           "formuluje otázky o zmysle života; interpretuje biblický text"),
          ("Svedectvo a tradícia", "dejiny cirkvi; sviatosti a sviatky",
           "opíše význam kresťanských sviatkov"),
      ]),
      ("2. ročník", [
          ("Morálka a svedomie", "Desatoro, láska k blížnemu; svedomie",
           "aplikuje morálne princípy na situácie zo života"),
          ("Náboženstvá sveta", "judaizmus, islam, budhizmus, hinduizmus; medzináboženský dialóg",
           "porovná základné znaky svetových náboženstiev"),
      ]),
  ],
  metody=["rozhovor, práca s textom, reflexia", "projekty a návštevy sakrálnych pamiatok"],
  hodn=["Predmet sa hodnotí slovne: absolvoval / neabsolvoval, v súlade s rozhodnutím pedagogickej rady."],
  zdroje=["učebné osnovy a učebnice schválené cirkvou alebo náboženskou spoločnosťou"],
  stav="na_kontrolu")

# ---------------------------------------------------------------------------------------------
OBL = "Zdravie a pohyb"
O(OBL, [
    "Vzdelávacia oblasť Zdravie a pohyb vedie žiakov k starostlivosti o vlastné zdravie, k pravidelnej pohybovej aktivite a k zdravému "
    "životnému štýlu. Rozvíja pohybové schopnosti a zručnosti, fair play a zodpovednosť za vlastné telo.",
])

P("Telesná a športová výchova", OBL, "ŠVP",
  char=[
      "Telesná a športová výchova rozvíja pohybové schopnosti a zručnosti žiakov, ich telesnú zdatnosť a vzťah k pravidelnej pohybovej "
      "aktivite. Obsah je rozdelený do modulov: Zdravie a jeho poruchy, Zdravý životný štýl, Telesná zdatnosť a pohybová výkonnosť, "
      "Športové činnosti pohybového režimu.",
  ],
  ciele=[
      "rozvíjať kondičné a koordinačné schopnosti",
      "zdokonaliť pohybové zručnosti v zvolených športoch",
      "uplatňovať zásady zdravého životného štýlu a bezpečnosti",
      "dodržiavať pravidlá fair play",
  ],
  roc=[
      ("1. – 4. ročník", [
          ("Zdravie a jeho poruchy", "zdravie, civilizačné ochorenia, prevencia; prvá pomoc",
           "vysvetlí vplyv pohybu na zdravie; poskytne prvú pomoc pri úraze"),
          ("Zdravý životný štýl", "pohybový režim, výživa, regenerácia; škodlivé návyky",
           "zostaví vlastný týždenný pohybový režim; zdôvodní riziká drog a fajčenia"),
          ("Telesná zdatnosť a pohybová výkonnosť", "kondičné a koordinačné schopnosti; testovanie zdatnosti",
           "vykoná testy telesnej zdatnosti a vyhodnotí svoj pokrok"),
          ("Športové činnosti", "atletika, gymnastika, športové hry, plávanie, turistika a pobyt v prírode, úpoly",
           "predvedie herné činnosti jednotlivca v športovej hre; zvládne techniku vybraných atletických disciplín"),
      ]),
  ],
  metody=METODY_TSV,
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotí sa prístup, aktivita, zlepšenie výkonnosti a osvojenie pohybových zručností; zohľadňuje sa zdravotný stav žiaka."],
  zdroje=["športové náradie a náčinie, telocvičňa, posilňovňa, atletický areál", "pravidlá športov a metodické materiály"],
  stav="hotova")

P("Športová príprava", OBL, "ŠkVP",
  char=[
      "Predmet je určený žiakom tried so športovou prípravou. Rozvíja všeobecnú a špeciálnu pohybovú prípravu v zvolenom športe v spolupráci "
      "s trénermi športových klubov.",
  ],
  ciele=[
      "rozvíjať špeciálne pohybové schopnosti v zvolenom športe",
      "zdokonaliť techniku a taktiku",
      "viesť k regenerácii, správnej výžive a prevencii zranení",
  ],
  roc=[
      ("1. – 4. ročník", [
          ("Všeobecná pohybová príprava", "sila, rýchlosť, vytrvalosť, obratnosť, flexibilita",
           "zvládne tréningový program všeobecnej prípravy"),
          ("Špeciálna príprava", "technika a taktika zvoleného športu",
           "predvedie techniku a taktické riešenia herných situácií"),
          ("Regenerácia a teória tréningu", "zaťaženie, regenerácia, výživa športovca, doping",
           "naplánuje regeneráciu po zaťažení; vysvetlí riziká dopingu"),
      ]),
  ],
  metody=METODY_TSV + ["tréningové jednotky pod vedením trénera, sústredenia"],
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotí sa plnenie tréningového plánu a výkonnostný rast."],
  zdroje=["športoviská a tréningové pomôcky klubov"],
  suvisi=[("Telesná a športová výchova", "nadvazuje")])

P("Základy športovej prípravy", OBL, "ŠkVP",
  char=["Predmet rozširuje telesnú výchovu o základy športového tréningu pre žiakov, ktorí sa chcú venovať športu na rekreačnej úrovni."],
  ciele=["osvojiť si princípy športového tréningu", "naplánovať vlastný tréning", "zlepšiť telesnú zdatnosť"],
  roc=[
      ("1. ročník", [
          ("Princípy tréningu", "zaťaženie, adaptácia, superkompenzácia", "vysvetlí princípy zaťaženia a adaptácie"),
          ("Vlastný tréningový plán", "ciele, testovanie, plánovanie", "zostaví a realizuje vlastný tréningový plán"),
      ]),
  ],
  metody=METODY_TSV,
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=["telocvičňa, posilňovňa, monitor srdcovej frekvencie"],
  suvisi=[("Telesná a športová výchova", "nadvazuje")])

P("Športová príprava pre maturantov", OBL, "ŠkVP",
  char=["Predmet pripravuje žiakov tried so športovou prípravou na maturitnú skúšku z teórie a didaktiky športovej prípravy."],
  ciele=["systematizovať teóriu športového tréningu", "pripraviť sa na ústnu formu maturity"],
  roc=[
      ("4. ročník", [
          ("Teória športového tréningu", "systematizácia", "súvisle odpovie na maturitné zadanie"),
          ("Didaktika a organizácia športu", "plánovanie tréningu, organizácia súťaží", "naplánuje tréningovú jednotku a súťaž"),
      ]),
  ],
  metody=METODY_SEMINAR,
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Športová príprava", "nadvazuje")])
