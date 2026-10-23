from spolocne import *

OBL = "Človek a spoločnosť"

O(OBL, [
    "Vzdelávacia oblasť Človek a spoločnosť vedie žiakov k poznaniu minulosti a súčasnosti ľudskej spoločnosti, vzťahov medzi človekom, "
    "spoločnosťou a priestorom a k porozumeniu princípom demokracie, právneho štátu a ekonomiky.",
    "Rozvíja historické a geografické myslenie, kritickú prácu s informáciami a prameňmi, občiansku zodpovednosť a toleranciu "
    "k odlišným kultúram a názorom.",
])

P("Dejepis", OBL, "ŠVP",
  char=[
      "Dejepis rozvíja historické vedomie a historické myslenie žiakov. Učí ich pracovať s historickými prameňmi, rozlišovať fakty a "
      "interpretácie, chápať príčiny a dôsledky udalostí a porovnávať dejiny Slovenska s európskymi a svetovými dejinami.",
      "Dôraz sa kladie na dejiny 19. a 20. storočia, na vývoj demokracie a na pamäť obetí totalitných režimov.",
  ],
  ciele=[
      "orientovať sa v historickom čase a priestore",
      "kriticky pracovať s historickými prameňmi a rozlišovať fakt, názor a propagandu",
      "vysvetliť príčiny a dôsledky kľúčových historických udalostí",
      "chápať hodnotu demokracie, ľudských práv a historickej pamäti",
  ],
  roc=[
      ("1. ročník", [
          ("Úvod do štúdia dejepisu", "historický prameň, historiografia; historický čas, periodizácia",
           "rozlíši primárny a sekundárny prameň; zaradí udalosť do historického obdobia"),
          ("Antika a stredovek", "grécka demokracia, Rím; Veľká Morava; stredoveký štát a cirkev; Uhorsko",
           "porovná aténsku demokraciu s modernou; vysvetlí význam cyrilo-metodskej misie"),
          ("Raný novovek", "humanizmus a renesancia, reformácia; objavné plavby; Habsburská monarchia, osvietenstvo",
           "vysvetlí príčiny a dôsledky reformácie; zhodnotí reformy osvietenských panovníkov"),
      ]),
      ("2. ročník", [
          ("Dlhé 19. storočie", "francúzska revolúcia, priemyselná revolúcia; národné hnutia; slovenské národné hnutie, revolúcia 1848/49; Rakúsko-Uhorsko",
           "vysvetlí príčiny a dôsledky priemyselnej revolúcie; charakterizuje požiadavky slovenského národného hnutia"),
          ("Prvá svetová vojna a vznik ČSR", "príčiny a priebeh vojny; zahraničný a domáci odboj; vznik Československa",
           "vysvetlí príčiny prvej svetovej vojny; zhodnotí úlohu M. R. Štefánika pri vzniku ČSR"),
          ("Medzivojnové obdobie", "demokracia a totalitné režimy; Československá republika 1918 – 1938; Mníchov",
           "porovná demokratický a totalitný režim; vysvetlí dôsledky Mníchovskej dohody"),
      ]),
      ("3. ročník", [
          ("Druhá svetová vojna", "Slovenská republika 1939 – 1945; holokaust; SNP; priebeh a dôsledky vojny",
           "zhodnotí charakter Slovenskej republiky 1939 – 1945; vysvetlí podstatu holokaustu; zhodnotí význam SNP"),
          ("Svet a Československo po roku 1945", "studená vojna; komunistický režim v ČSR, rok 1968, normalizácia; nežná revolúcia 1989",
           "vysvetlí podstatu studenej vojny; analyzuje pramene k udalostiam roku 1968; zhodnotí význam novembra 1989"),
          ("Slovenská republika a súčasný svet", "vznik SR 1993; integrácia do EÚ a NATO; globalizácia",
           "opíše vznik samostatnej SR; zhodnotí význam vstupu SR do EÚ a NATO"),
      ]),
  ],
  metody=METODY_SPOLOCENSKE + ["návšteva múzeí a pamätných miest, rozhovory s pamätníkmi"],
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY],
  zdroje=ZDROJE_ZAKLAD + ["historické atlasy, pramenné čítanky, dokumentárne filmy"],
  stav="hotova",
  suvisi=[("Občianska náuka", "medzipredmetovy")])

P("Geografia", OBL, "ŠVP",
  char=[
      "Geografia skúma vzťahy medzi prírodou a spoločnosťou v priestore. Rozvíja schopnosť čítať a interpretovať mapy, pracovať s geografickými "
      "informáciami a chápať regionálne a globálne problémy súčasného sveta.",
  ],
  ciele=[
      "orientovať sa na mape a v teréne, používať GIS a digitálne mapy",
      "vysvetliť zákonitosti prírodných a spoločenských procesov",
      "charakterizovať regióny Slovenska, Európy a sveta",
      "posúdiť globálne problémy a možnosti udržateľného rozvoja",
  ],
  roc=[
      ("1. ročník", [
          ("Planéta Zem a mapa", "Zem vo vesmíre; geografická sieť, časové pásma; mapa, mierka, kartografické zobrazenia; GIS",
           "určí geografické súradnice miesta; vypočíta časový rozdiel; použije mierku mapy"),
          ("Prírodná sféra", "litosféra, atmosféra, hydrosféra, pedosféra, biosféra; podnebné pásma",
           "vysvetlí vznik pohorí a zemetrasení; opíše cirkuláciu atmosféry; priradí krajinu k podnebnému pásmu"),
      ]),
      ("2. ročník", [
          ("Humánna geografia", "obyvateľstvo a sídla; hospodárstvo – poľnohospodárstvo, priemysel, služby, doprava; globalizácia",
           "interpretuje vekovú pyramídu; vysvetlí faktory lokalizácie priemyslu; zhodnotí dôsledky globalizácie"),
          ("Regionálna geografia sveta", "Afrika, Ázia, Amerika, Austrália a Oceánia – prírodné a spoločenské pomery",
           "porovná vybrané regióny sveta; vysvetlí príčiny rozdielov vo vyspelosti štátov"),
      ]),
      ("3. ročník", [
          ("Európa", "prírodné pomery, obyvateľstvo, hospodárstvo; Európska únia; štáty Európy",
           "charakterizuje región Európy; vysvetlí princípy fungovania EÚ"),
          ("Slovensko", "poloha, prírodné pomery, obyvateľstvo, sídla, hospodárstvo; regióny Slovenska; ochrana prírody",
           "charakterizuje región Slovenska; zhodnotí regionálne rozdiely; navrhne trasu exkurzie po regióne"),
      ]),
  ],
  metody=METODY_SPOLOCENSKE + ["práca s mapami a GIS, terénne cvičenia"],
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY],
  zdroje=ZDROJE_ZAKLAD + ["školský atlas sveta a Slovenska, digitálne mapy"],
  stav="hotova")

P("Občianska náuka", OBL, "ŠVP",
  char=[
      "Občianska náuka pripravuje žiakov na aktívny a zodpovedný život v demokratickej spoločnosti. Prepája psychológiu, sociológiu, "
      "politológiu, právo, ekonómiu a filozofiu.",
  ],
  ciele=[
      "poznať seba a rozvíjať vzťahy s inými ľuďmi",
      "chápať princípy demokracie a právneho štátu a uplatňovať svoje práva",
      "rozumieť fungovaniu trhovej ekonomiky a hospodáriť s financiami",
      "kriticky myslieť a filozoficky uvažovať o otázkach človeka a sveta",
  ],
  roc=[
      ("2. ročník", [
          ("Človek ako jednotlivec", "psychológia – osobnosť, poznávacie procesy, učenie sa; stres a jeho zvládanie",
           "opíše štruktúru osobnosti; navrhne efektívny spôsob učenia sa; uvedie spôsoby zvládania stresu"),
          ("Človek v spoločnosti", "sociológia – socializácia, sociálne skupiny, rodina; sociálne problémy",
           "vysvetlí proces socializácie; analyzuje sociálny problém a navrhne riešenie"),
      ]),
      ("3. ročník", [
          ("Občan a štát", "štát, formy vlády; ústava SR; demokracia, voľby, politické strany; ľudské práva",
           "opíše zloženie a právomoci orgánov štátnej moci SR; vysvetlí princípy volebného systému"),
          ("Občan a právo", "právny poriadok; občianske, rodinné, pracovné a trestné právo",
           "rozlíši oblasti práva; uvedie práva a povinnosti zamestnanca"),
          ("Ekonomický život", "trh, dopyt a ponuka; peniaze, banky, dane; rodinný rozpočet",
           "vysvetlí mechanizmus trhu; zostaví rodinný rozpočet"),
      ]),
      ("4. ročník", [
          ("Filozofia", "vznik filozofie; antická, stredoveká, novoveká a súčasná filozofia; etika",
           "porovná názory filozofov na vybranú otázku; sformuluje vlastný argumentovaný postoj"),
      ]),
  ],
  metody=METODY_SPOLOCENSKE,
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY],
  zdroje=ZDROJE_ZAKLAD + ["Ústava SR, aktuálne právne predpisy, publicistické texty"],
  stav="hotova",
  suvisi=[("Dejepis", "medzipredmetovy")])

P("Finančná gramotnosť", OBL, "ŠkVP",
  char=[
      "Predmet rozvíja finančnú gramotnosť žiakov v súlade s národným štandardom finančnej gramotnosti. Žiaci sa učia rozhodovať o financiách, "
      "plánovať rozpočet, sporiť, investovať a rozpoznávať riziká úverov a podvodov.",
  ],
  ciele=[
      "zodpovedne hospodáriť s osobnými financiami",
      "porovnávať finančné produkty a posúdiť ich výhodnosť a riziká",
      "chrániť sa pred finančnými podvodmi a predlžením",
  ],
  roc=[
      ("2. ročník", [
          ("Človek vo sfére peňazí", "potreby a túžby; rozhodovanie; spotrebiteľské práva",
           "zdôvodní finančné rozhodnutie; uplatní reklamáciu ako spotrebiteľ"),
          ("Finančná zodpovednosť a rozpočet", "príjmy a výdavky, osobný a rodinný rozpočet",
           "zostaví osobný rozpočet a navrhne úspory"),
          ("Úver, dlh, sporenie a investovanie", "úroková miera, RPMN; sporiace a investičné produkty; riziko a výnos",
           "vypočíta celkové náklady úveru; porovná sporenie a investovanie"),
          ("Riadenie rizika a poistenie", "poistenie, finančné podvody, ochrana údajov",
           "rozpozná znaky podvodnej ponuky; vyberie vhodný typ poistenia"),
      ]),
  ],
  metody=METODY_SPOLOCENSKE + ["prípadové štúdie, simulácie a výpočty v tabuľkovom kalkulátore"],
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD + ["Národný štandard finančnej gramotnosti", "kalkulačky finančných produktov"],
  suvisi=[("Občianska náuka", "nadvazuje"), ("Aplikovaná matematika", "medzipredmetovy")])

P("Seminár z dejepisu", OBL, "ŠkVP",
  char=[
      "Seminár je určený žiakom so záujmom o históriu, právo, žurnalistiku, politológiu a učiteľstvo. Prehlbuje kľúčové témy dejepisu "
      "a rozvíja prácu s historickými prameňmi.",
  ],
  ciele=["prehĺbiť poznatky z dejín Slovenska a sveta", "samostatne analyzovať a interpretovať pramene", "pripraviť sa na maturitu a prijímacie skúšky"],
  roc=[
      ("3. ročník", [
          ("Práca historika", "heuristika, kritika prameňov, interpretácia", "analyzuje prameň a posúdi jeho dôveryhodnosť"),
          ("Kľúčové obdobia slovenských dejín", "Veľká Morava, Uhorsko, národné hnutie", "porovná interpretácie udalosti v rôznych prameňoch"),
      ]),
      ("4. ročník", [
          ("Dejiny 20. storočia", "totalitné režimy, studená vojna, rok 1989", "napíše esej na historickú tému s využitím prameňov"),
          ("Regionálne dejiny", "dejiny obce a regiónu školy", "spracuje projekt z regionálnej histórie"),
      ]),
  ],
  metody=METODY_SEMINAR + METODY_SPOLOCENSKE[1:3],
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Dejepis", "nadvazuje"), ("Dejepis pre maturantov", "suvisi")])

P("Seminár z geografie", OBL, "ŠkVP",
  char=["Seminár prehlbuje fyzickú, humánnu a regionálnu geografiu a rozvíja prácu s GIS a terénny výskum."],
  ciele=["prehĺbiť geografické poznatky", "zvládnuť prácu s GIS", "pripraviť sa na maturitu a geografickú olympiádu"],
  roc=[
      ("3. ročník", [
          ("Geografické informačné systémy", "vrstvy, priestorová analýza, tvorba mapy", "vytvorí tematickú mapu v GIS"),
          ("Fyzická geografia", "geomorfológia, klimatológia, hydrológia", "interpretuje klimadiagram a hydrologický režim rieky"),
      ]),
      ("4. ročník", [
          ("Humánna a regionálna geografia", "demografia, urbanizácia, geopolitika", "analyzuje demografický vývoj štátu"),
          ("Terénny výskum", "dotazník, mapovanie, spracovanie údajov", "uskutoční terénny výskum v okolí školy"),
      ]),
  ],
  metody=METODY_SEMINAR + ["práca v teréne a s GIS"],
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Geografia", "nadvazuje"), ("Geografia pre maturantov", "suvisi")])


def pre_maturantov(nazov, predmet, okruhy):
    P(nazov, OBL, "ŠkVP",
      char=[f"Predmet je určený žiakom, ktorí z predmetu {predmet} maturujú. Systematizuje učivo a pripravuje žiakov na ústnu formu internej časti maturitnej skúšky."],
      ciele=["systematizovať učivo podľa maturitných zadaní", "súvisle a argumentovane odpovedať", "pracovať s prameňmi, mapami a grafmi"],
      roc=[("4. ročník", okruhy)],
      metody=METODY_SEMINAR,
      hodn=HODNOTENIE_ZAKLAD,
      zdroje=ZDROJE_ZAKLAD + ["maturitné zadania"],
      suvisi=[(predmet, "nadvazuje")])


pre_maturantov("Dejepis pre maturantov", "Dejepis", [
    ("Staroveké a stredoveké dejiny", "systematizácia", "súvisle odpovie s využitím mapy a prameňa"),
    ("Novovek a 19. storočie", "systematizácia", "vysvetlí príčiny a dôsledky udalostí"),
    ("20. storočie a súčasnosť", "systematizácia", "porovná udalosti slovenských a svetových dejín"),
])
pre_maturantov("Geografia pre maturantov", "Geografia", [
    ("Fyzická geografia", "systematizácia", "vysvetlí prírodný proces na príklade"),
    ("Humánna geografia", "systematizácia", "interpretuje štatistické údaje"),
    ("Regionálna geografia", "systematizácia", "charakterizuje región podľa osnovy"),
])
pre_maturantov("Občianska náuka pre maturantov", "Občianska náuka", [
    ("Psychológia a sociológia", "systematizácia", "uvedie príklady z praxe"),
    ("Politológia a právo", "systematizácia", "vysvetlí fungovanie štátu a práva na príklade"),
    ("Ekonómia a filozofia", "systematizácia", "porovná ekonomické a filozofické koncepcie"),
])
