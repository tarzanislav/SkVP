from spolocne import *

OBL = "Človek a príroda"

O(OBL, [
    "Vzdelávacia oblasť Človek a príroda umožňuje žiakom poznávať prírodu, jej zákonitosti a vzťahy medzi živou a neživou prírodou. "
    "Rozvíja prírodovednú gramotnosť a vedecký spôsob myslenia – kladenie otázok, formulovanie hypotéz, experimentovanie a vyvodzovanie záverov.",
    "Predmety fyzika, chémia a biológia vedú žiakov k zodpovednému vzťahu k zdraviu a k životnému prostrediu a k porozumeniu významu "
    "prírodných vied pre technický pokrok a udržateľný rozvoj.",
])

P("Fyzika", OBL, "ŠVP",
  char=[
      "Fyzika poskytuje žiakom poznatky o základných fyzikálnych javoch a zákonoch a rozvíja schopnosť skúmať prírodu vedeckými metódami. "
      "Obsah je koncipovaný tak, aby žiaci javy pozorovali, merali, opisovali modelmi a vysvetľovali ich.",
      "Dôraz sa kladie na bádateľské aktivity, prácu s meracími prístrojmi, spracovanie a interpretáciu nameraných údajov.",
  ],
  ciele=[
      "pozorovať a skúmať fyzikálne javy a formulovať hypotézy",
      "navrhnúť a uskutočniť experiment, spracovať a vyhodnotiť namerané údaje",
      "používať fyzikálne zákony pri riešení úloh z praxe",
      "chápať význam fyziky pre techniku a spoločnosť",
  ],
  roc=[
      ("1. ročník", [
          ("Skúmanie vlastností kvapalín a plynov", "hustota, tlak, hydrostatický a atmosférický tlak; Pascalov a Archimedov zákon; prúdenie, rovnica spojitosti, Bernoulliho rovnica",
           "vypočíta hydrostatický tlak a vztlakovú silu; vysvetlí plávanie telies; navrhne experiment na overenie Archimedovho zákona"),
          ("Teplo a teplota", "teplota, teplotná rozťažnosť; vnútorná energia, teplo, merná tepelná kapacita; kalorimetrická rovnica; zmeny skupenstva",
           "zostaví kalorimetrickú rovnicu a vypočíta výslednú teplotu; zmeria mernú tepelnú kapacitu látky; vysvetlí zmeny skupenstva pomocou časticovej stavby látok"),
          ("Pohyb", "poloha, dráha, rýchlosť, zrýchlenie; rovnomerný a rovnomerne zrýchlený pohyb; voľný pád; pohyb po kružnici",
           "zostrojí a interpretuje graf závislosti dráhy a rýchlosti od času; vypočíta veličiny rovnomerného a rovnomerne zrýchleného pohybu"),
      ]),
      ("2. ročník", [
          ("Sila a pohyb", "Newtonove zákony; gravitačná sila, trenie; hybnosť a zákon zachovania hybnosti; mechanická práca, energia, výkon",
           "použije Newtonove zákony pri riešení úloh; vysvetlí zákon zachovania mechanickej energie; vypočíta prácu a výkon"),
          ("Elektrický náboj a elektrické pole", "elektrický náboj, Coulombov zákon; intenzita a potenciál; kondenzátor, kapacita",
           "vypočíta silu medzi nábojmi; opíše elektrické pole pomocou siločiar; vysvetlí funkciu kondenzátora"),
          ("Elektrický prúd", "prúd, napätie, odpor; Ohmov zákon; zapojenie rezistorov; práca a výkon elektrického prúdu; prúd v polovodičoch",
           "zostaví obvod podľa schémy a zmeria prúd a napätie; vypočíta odpor sériového a paralelného zapojenia; vypočíta spotrebu elektrickej energie"),
      ]),
      ("3. ročník", [
          ("Elektromagnetizmus", "magnetické pole, magnetická indukcia; elektromagnetická indukcia; striedavý prúd, transformátor",
           "opíše magnetické pole vodiča s prúdom; vysvetlí princíp elektromagnetickej indukcie a transformátora"),
          ("Kmitanie, vlnenie, optika", "mechanické kmitanie a vlnenie, zvuk; elektromagnetické vlnenie; odraz a lom svetla; šošovky",
           "určí periódu a frekvenciu kmitania; vysvetlí lom svetla; zostrojí obraz predmetu šošovkou"),
          ("Fyzika mikrosveta", "fotoelektrický jav; model atómu; jadro, rádioaktivita, polčas rozpadu; jadrová energetika",
           "vysvetlí fotoelektrický jav; opíše druhy žiarenia a ochranu pred ním; zhodnotí výhody a riziká jadrovej energetiky"),
      ]),
  ],
  metody=METODY_PRIRODNE,
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY, "Súčasťou hodnotenia sú protokoly z laboratórnych prác."],
  zdroje=ZDROJE_ZAKLAD + ["laboratórne pomôcky a súpravy na meranie, simulácie fyzikálnych javov"],
  stav="hotova",
  suvisi=[("Matematika", "predpoklad")])

P("Chémia", OBL, "ŠVP",
  char=[
      "Chémia vedie žiakov k poznaniu zloženia, štruktúry a vlastností látok a ich premien. Spája teoretické poznatky s experimentálnou "
      "činnosťou a s využitím chémie v každodennom živote, priemysle a ochrane životného prostredia.",
  ],
  ciele=[
      "vysvetliť vlastnosti látok na základe ich zloženia a štruktúry",
      "zapisovať a vyčísľovať chemické rovnice a riešiť chemické výpočty",
      "bezpečne pracovať s chemickými látkami a laboratórnym vybavením",
      "posúdiť vplyv chemických látok na zdravie a životné prostredie",
  ],
  roc=[
      ("1. ročník", [
          ("Zloženie a štruktúra látok", "sústavy látok, zmesi a ich oddeľovanie; atóm, protón, neutrón, elektrón; elektrónový obal, periodická sústava prvkov; chemická väzba",
           "navrhne postup oddelenia zložiek zmesi; zapíše elektrónovú konfiguráciu prvku; vysvetlí vznik kovalentnej a iónovej väzby"),
          ("Chemické reakcie", "chemická rovnica, zákon zachovania hmotnosti; látkové množstvo, mol, molárna hmotnosť; zloženie roztokov; chemická kinetika a rovnováha",
           "vyčísli chemickú rovnicu; vypočíta látkové množstvo a hmotnosť; pripraví roztok požadovanej koncentrácie"),
          ("Protolytické a redoxné reakcie", "kyseliny a zásady, pH; neutralizácia; oxidácia a redukcia; elektrolýza, galvanický článok",
           "určí pH roztoku indikátorom; zapíše rovnicu neutralizácie; vysvetlí princíp galvanického článku"),
      ]),
      ("2. ročník", [
          ("Prvky a ich zlúčeniny", "vodík, kyslík, voda; prvky skupín periodickej sústavy – vlastnosti a zlúčeniny; kovy a ich výroba",
           "porovná vlastnosti prvkov v skupine; opíše výrobu železa; uvedie využitie významných zlúčenín"),
          ("Úvod do organickej chémie", "vlastnosti atómu uhlíka; vzorce organických zlúčenín; izoméria; typy organických reakcií",
           "napíše štruktúrny a racionálny vzorec; určí typ izomérie; rozlíši adíciu, substitúciu a elimináciu"),
          ("Uhľovodíky", "alkány, alkény, alkíny, arény; ropa a zemný plyn",
           "pomenuje uhľovodík podľa názvoslovia; opíše spracovanie ropy a jeho vplyv na životné prostredie"),
      ]),
      ("3. ročník", [
          ("Deriváty uhľovodíkov", "halogénderiváty, alkoholy, fenoly, aldehydy, ketóny, karboxylové kyseliny a ich deriváty",
           "zaradí zlúčeninu podľa funkčnej skupiny; opíše vlastnosti a využitie etanolu a kyseliny octovej"),
          ("Biochémia", "lipidy, sacharidy, bielkoviny, nukleové kyseliny; enzýmy; metabolizmus",
           "opíše stavbu a význam biomolekúl; vysvetlí funkciu enzýmov"),
          ("Chémia v živote", "liečivá, potravinové aditíva, plasty, drogy; chémia a životné prostredie",
           "posúdi riziká látok v domácnosti; navrhne spôsob triedenia a recyklácie odpadu"),
      ]),
  ],
  metody=METODY_PRIRODNE,
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY, "Súčasťou hodnotenia sú protokoly z laboratórnych prác a chemické výpočty."],
  zdroje=ZDROJE_ZAKLAD + ["tabuľky a periodická sústava prvkov, laboratórne vybavenie"],
  stav="hotova",
  suvisi=[("Biológia", "medzipredmetovy")])

P("Biológia", OBL, "ŠVP",
  char=[
      "Biológia vedie žiakov k poznaniu živých organizmov, ich stavby, funkcií, vývinu a vzťahov k prostrediu. Rozvíja porozumenie ľudskému "
      "telu a zdraviu a buduje zodpovedný vzťah k prírode.",
  ],
  ciele=[
      "poznať stavbu a funkcie buniek, organizmov a ekosystémov",
      "pozorovať biologické objekty a javy a pracovať s mikroskopom",
      "vysvetliť princípy dedičnosti a evolúcie",
      "chrániť si zdravie a prírodu na základe poznatkov",
  ],
  roc=[
      ("1. ročník", [
          ("Biológia ako veda. Bunka", "metódy biológie; znaky živých sústav; stavba prokaryotickej a eukaryotickej bunky; bunkový cyklus, mitóza, meióza",
           "rozlíši prokaryotickú a eukaryotickú bunku; pripraví mikroskopický preparát; porovná mitózu a meiózu"),
          ("Vírusy, baktérie, huby", "stavba a rozmnožovanie vírusov a baktérií; huby a lišajníky; význam pre človeka",
           "vysvetlí rozdiel medzi vírusom a baktériou; uvedie choroby spôsobené vírusmi a baktériami a prevenciu"),
          ("Rastliny", "rastlinné pletivá a orgány; fotosyntéza a dýchanie; rozmnožovanie rastlín; systém rastlín",
           "opíše stavbu rastlinných orgánov; vysvetlí podstatu fotosyntézy; určí rastlinu pomocou kľúča"),
      ]),
      ("2. ročník", [
          ("Živočíchy", "stavba tela a orgánové sústavy živočíchov; systém bezstavovcov a stavovcov; etológia",
           "porovná orgánové sústavy vybraných skupín živočíchov; zaradí živočícha do systému"),
          ("Biológia človeka", "oporná a pohybová sústava; obehová, dýchacia, tráviaca, vylučovacia sústava; nervová a hormonálna regulácia; zmysly; rozmnožovanie",
           "opíše stavbu a funkciu orgánových sústav človeka; vysvetlí zásady prvej pomoci; zdôvodní zásady zdravého životného štýlu"),
      ]),
      ("3. ročník", [
          ("Genetika", "nukleové kyseliny, gén, genetický kód; Mendelove zákony; dedičnosť pohlavia; mutácie; genetické inžinierstvo",
           "rieši genetické úlohy s jedným a dvoma znakmi; vysvetlí príčiny a dôsledky mutácií; zhodnotí využitie a riziká genetického inžinierstva"),
          ("Ekológia a evolúcia", "organizmus a prostredie; populácia, spoločenstvo, ekosystém; kolobeh látok a tok energie; evolúcia a jej dôkazy; ochrana prírody",
           "zostaví potravový reťazec; vysvetlí kolobeh uhlíka; uvedie dôkazy evolúcie; navrhne opatrenia na ochranu prírody"),
      ]),
  ],
  metody=METODY_PRIRODNE + ["terénne pozorovania a exkurzie, práca s mikroskopom a určovacími kľúčmi"],
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY, "Súčasťou hodnotenia sú protokoly z laboratórnych prác a projekty."],
  zdroje=ZDROJE_ZAKLAD + ["mikroskopy, preparáty, určovacie kľúče, atlasy"],
  stav="hotova",
  suvisi=[("Chémia", "medzipredmetovy")])

P("Experimentálna prírodoveda", OBL, "ŠkVP",
  char=[
      "Predmet rozvíja bádateľské zručnosti žiakov prvého ročníka. Prepája fyziku, chémiu a biológiu prostredníctvom experimentov a "
      "projektov, pri ktorých žiaci kladú otázky, navrhujú pokusy a vyhodnocujú výsledky.",
  ],
  ciele=[
      "formulovať výskumnú otázku a hypotézu",
      "navrhnúť a bezpečne uskutočniť experiment",
      "spracovať údaje, vyvodiť závery a prezentovať ich",
  ],
  roc=[
      ("1. ročník", [
          ("Vedecká metóda", "pozorovanie, otázka, hypotéza, premenné, kontrolný pokus",
           "rozlíši závislú a nezávislú premennú; navrhne kontrolovaný experiment"),
          ("Meranie a údaje", "meracie prístroje, chyba merania, tabuľka, graf",
           "vykoná opakované meranie, vypočíta priemer a znázorní údaje v grafe"),
          ("Prírodovedné projekty", "voda, vzduch, pôda, energia v domácnosti",
           "realizuje projekt v tíme a odprezentuje výsledky"),
      ]),
  ],
  metody=METODY_PRIRODNE,
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotí sa aktivita, protokoly a projekt; predmet môže byť hodnotený aj slovne podľa rozhodnutia pedagogickej rady."],
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Fyzika", "medzipredmetovy"), ("Chémia", "medzipredmetovy"), ("Biológia", "medzipredmetovy")])


def seminar(nazov, predmet, char, celky3, celky4, stav=None):
    P(nazov, OBL, "ŠkVP",
      char=[char, f"Nadväzuje na obsahový a výkonový štandard predmetu {predmet}; učivo rozširuje, prehlbuje a dopĺňa o laboratórne a projektové aktivity."],
      ciele=[
          f"prehĺbiť vedomosti z predmetu {predmet} nad rámec štátneho vzdelávacieho programu",
          "rozvíjať experimentálne zručnosti a samostatnú prácu s odbornou literatúrou",
          "pripraviť sa na maturitnú skúšku, prijímacie skúšky a predmetové olympiády",
      ],
      roc=[("3. ročník", celky3), ("4. ročník", celky4)],
      metody=METODY_SEMINAR + METODY_PRIRODNE[:2],
      hodn=HODNOTENIE_ZAKLAD,
      zdroje=ZDROJE_ZAKLAD + ["vysokoškolské učebnice a odborné časopisy", "zadania predmetovej olympiády"],
      stav=stav,
      suvisi=[(predmet, "nadvazuje"), (f"{predmet} pre maturantov", "suvisi")])


seminar("Seminár z biológie", "Biológia",
        "Seminár je určený žiakom so záujmom o biológiu, medicínu, veterinu, farmáciu a príbuzné odbory.",
        [("Cytológia a molekulová biológia", "bunkové organely, membránový transport; replikácia, transkripcia, translácia",
          "vysvetlí proteosyntézu; porovná pasívny a aktívny transport"),
         ("Fyziológia rastlín a živočíchov", "vodný režim, výživa, rast; regulácia u živočíchov",
          "navrhne pokus z fyziológie rastlín a vyhodnotí ho")],
        [("Anatómia a fyziológia človeka", "orgánové sústavy – podrobne; imunita; hormóny",
          "vysvetlí mechanizmus imunitnej odpovede; rieši úlohy prijímacích skúšok na lekárske fakulty"),
         ("Genetika a ekológia", "populačná genetika; ekologické faktory; globálne problémy",
          "rieši úlohy z populačnej genetiky; zhodnotí globálny ekologický problém")],
        stav="na_kontrolu")

seminar("Seminár z chémie", "Chémia",
        "Seminár je určený žiakom so záujmom o chémiu, medicínu, farmáciu, chemické a potravinárske odbory.",
        [("Všeobecná chémia", "štruktúra atómu, väzby, termochémia; chemické výpočty – náročnejšie úlohy",
          "rieši stechiometrické úlohy; vypočíta reakčné teplo"),
         ("Anorganická chémia", "komplexné zlúčeniny; prvky d-bloku",
          "pomenuje komplexnú zlúčeninu; opíše vlastnosti prechodných kovov")],
        [("Organická chémia", "reakčné mechanizmy; syntéza organických látok",
          "opíše mechanizmus substitúcie a adície; navrhne jednoduchú syntézu"),
         ("Analytická chémia", "kvalitatívna a kvantitatívna analýza; titrácia",
          "uskutoční acidobázickú titráciu a vypočíta koncentráciu")])

seminar("Seminár z fyziky", "Fyzika",
        "Seminár je určený žiakom so záujmom o fyziku, techniku, informatiku a prírodovedné odbory.",
        [("Mechanika", "kinematika a dynamika – náročnejšie úlohy; gravitačné pole; mechanika tuhého telesa",
          "rieši úlohy s pohybom v gravitačnom poli; vypočíta moment sily"),
         ("Termodynamika", "stavová rovnica ideálneho plynu; termodynamické zákony; tepelné stroje",
          "použije stavovú rovnicu; vypočíta účinnosť tepelného stroja")],
        [("Elektrina a magnetizmus", "obvody striedavého prúdu; elektromagnetické vlny",
          "rieši úlohy s obvodmi striedavého prúdu"),
         ("Moderná fyzika", "špeciálna teória relativity; kvantová fyzika; astrofyzika",
          "vysvetlí dilatáciu času; opíše vývoj hviezd")])


def pre_maturantov(nazov, predmet, okruhy):
    P(nazov, OBL, "ŠkVP",
      char=[f"Predmet je určený žiakom, ktorí si zvolili predmet {predmet} ako maturitný. Systematizuje učivo celého gymnaziálneho štúdia "
            "a pripravuje žiakov na ústnu formu internej časti maturitnej skúšky."],
      ciele=["systematizovať učivo podľa maturitných zadaní",
             "naučiť sa súvisle a odborne odpovedať a interpretovať pokus či graf",
             "prepojiť poznatky z rôznych tematických celkov"],
      roc=[("4. ročník", okruhy)],
      metody=METODY_SEMINAR,
      hodn=HODNOTENIE_ZAKLAD + ["Súčasťou hodnotenia sú cvičné ústne odpovede podľa maturitných zadaní."],
      zdroje=ZDROJE_ZAKLAD + ["maturitné zadania a súbory úloh"],
      suvisi=[(predmet, "nadvazuje")])


pre_maturantov("Biológia pre maturantov", "Biológia", [
    ("Bunka a organizmy", "cytológia, mikrobiológia, botanika, zoológia", "súvisle odpovie na maturitné zadanie s využitím obrazového materiálu"),
    ("Človek a zdravie", "anatómia a fyziológia človeka, prvá pomoc", "vysvetlí funkciu orgánovej sústavy a súvis so zdravím"),
    ("Genetika, ekológia, evolúcia", "systematizácia", "rieši genetickú úlohu a interpretuje ekologický problém"),
])
pre_maturantov("Chémia pre maturantov", "Chémia", [
    ("Všeobecná chémia", "systematizácia", "vysvetlí štruktúru látok a priebeh reakcií; rieši chemické výpočty"),
    ("Anorganická chémia", "systematizácia", "charakterizuje skupinu prvkov a ich zlúčeniny"),
    ("Organická chémia a biochémia", "systematizácia", "charakterizuje skupinu organických zlúčenín a biomolekúl"),
])
pre_maturantov("Fyzika pre maturantov", "Fyzika", [
    ("Mechanika a termika", "systematizácia", "vysvetlí jav, uvedie zákon a vyrieši úlohu"),
    ("Elektrina a magnetizmus", "systematizácia", "navrhne a opíše experiment"),
    ("Vlnenie, optika, mikrosvet", "systematizácia", "interpretuje graf a vyvodí závery"),
])
