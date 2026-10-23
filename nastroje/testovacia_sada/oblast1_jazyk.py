from spolocne import *

OBL = "Jazyk a komunikácia"

O(OBL, [
    "Vzdelávacia oblasť Jazyk a komunikácia rozvíja komunikačné kompetencie žiakov v materinskom a v cudzích jazykoch. "
    "Jazyk chápe ako nástroj myslenia, poznávania, dorozumievania a osobného i profesijného rastu.",
    "Žiaci sa učia s porozumením počúvať a čítať, ústne aj písomne sa vyjadrovať primerane situácii, kriticky "
    "pracovať s informáciami a interpretovať umelecké texty. V cudzích jazykoch smeruje vzdelávanie k úrovni B2 "
    "(prvý cudzí jazyk) a B1 (druhý cudzí jazyk) podľa Spoločného európskeho referenčného rámca pre jazyky.",
])

# ---------------------------------------------------------------------------------------------
P("Slovenský jazyk a literatúra", OBL, "ŠVP",
  char=[
      "Predmet Slovenský jazyk a literatúra je kľúčovým predmetom gymnaziálneho vzdelávania. Spája jazykovú, slohovú a literárnu zložku "
      "do jedného celku, ktorý rozvíja čitateľskú gramotnosť, komunikačné zručnosti a estetické cítenie žiakov.",
      "Jazyková zložka vedie žiakov k uvedomelému používaniu spisovného jazyka a k poznaniu jeho systému. Slohová zložka rozvíja schopnosť "
      "tvoriť ústne a písomné prejavy rôznych slohových postupov a žánrov. Literárna zložka sprostredkúva poznanie slovenskej a svetovej "
      "literatúry a rozvíja schopnosť interpretovať umelecký text.",
      "Predmet pripravuje žiakov na maturitnú skúšku zo slovenského jazyka a literatúry (externá časť, písomná forma internej časti a ústna forma).",
  ],
  ciele=[
      "používať spisovný jazyk v ústnej aj písomnej podobe v súlade s jazykovou normou",
      "s porozumením čítať náučné, publicistické aj umelecké texty a kriticky hodnotiť informácie",
      "tvoriť jazykovo správne a štylisticky vhodné texty rôznych slohových postupov a žánrov",
      "poznať vývin slovenskej a svetovej literatúry v kontexte literárnych smerov a obdobia",
      "interpretovať umelecký text a vyjadriť vlastný čitateľský zážitok a názor",
      "rozvíjať vzťah k materinskému jazyku a k národnej kultúre",
  ],
  roc=[
      ("1. ročník", [
          ("Jazyk a komunikácia", "jazyk a reč; spisovný jazyk, nárečia, slang; jazyková kultúra; zvuková stránka jazyka – spisovná výslovnosť, prozodické vlastnosti reči",
           "rozlíši spisovnú a nespisovnú podobu jazyka; dodržiava zásady spisovnej výslovnosti; vysvetlí význam jazykovej kultúry"),
          ("Lexikológia", "slovná zásoba, jednovýznamové a viacvýznamové slová; synonymá, antonymá, homonymá; tvorenie slov – odvodzovanie, skladanie, skracovanie; slovníky",
           "určí spôsob tvorenia slova; vyhľadá informáciu v Krátkom slovníku slovenského jazyka a v pravidlách; použije synonymá na štylistické spestrenie textu"),
          ("Sloh – informačný a rozprávací slohový postup", "správa, oznámenie, inzerát; rozprávanie, kompozícia rozprávania; opis a charakteristika",
           "napíše rozprávanie s využitím priamej reči a dejových zvratov; zostaví správu a oznámenie; rozlíši statický a dynamický opis"),
          ("Úvod do literatúry. Lyrika a epika", "literatúra ako umenie; literárne druhy a žánre; báseň, verš, rým, rytmus; umelecké jazykové prostriedky – metafora, epiteton, personifikácia; ľudová slovesnosť; staroveká a stredoveká literatúra",
           "rozlíši lyriku, epiku a drámu; vyhľadá umelecké jazykové prostriedky a vysvetlí ich funkciu; porovná ľudovú a umelú slovesnosť; interpretuje vybraný úryvok z antickej literatúry"),
      ]),
      ("2. ročník", [
          ("Morfológia", "slovné druhy – plnovýznamové a neplnovýznamové; gramatické kategórie podstatných mien, prídavných mien, zámen, čísloviek a slovies; vzory",
           "určí slovný druh a gramatické kategórie; správne skloňuje a časuje podľa vzorov; opraví morfologické chyby v texte"),
          ("Sloh – výkladový slohový postup", "výklad, odborný text, odborný termín; citácia a parafráza; osnova, konspekt, výpisky",
           "napíše výklad na zvolenú tému s dodržaním kompozície; správne cituje zdroj; vytvorí konspekt z odborného textu"),
          ("Renesancia, barok, klasicizmus", "renesančná a humanistická literatúra; barok; klasicizmus a slovenské národné obrodenie – A. Bernolák, J. Hollý, J. Kollár, P. J. Šafárik",
           "charakterizuje literárny smer a jeho znaky; zaradí dielo a autora do obdobia; interpretuje úryvok zo Slávy dcery"),
          ("Romantizmus", "európsky romantizmus; štúrovská generácia a kodifikácia spisovnej slovenčiny; S. Chalupka, J. Kráľ, A. Sládkovič, J. Botto",
           "vysvetlí význam kodifikácie spisovnej slovenčiny Ľ. Štúrom; analyzuje romantického hrdinu; porovná dielo romantizmu s klasicizmom"),
      ]),
      ("3. ročník", [
          ("Syntax", "veta, súvetie, vetné členy; jednoduchá veta, priraďovacie a podraďovacie súvetie; interpunkcia; slovosled",
           "urobí vetný rozbor; určí druh súvetia; správne používa interpunkciu v jednoduchej vete a v súvetí"),
          ("Sloh – úvaha a rečnícky prejav", "úvaha, argumentácia, argument a protiargument; rečnícky prejav, prednáška, diskusný príspevok",
           "napíše úvahu s jasnou tézou a argumentmi; pripraví a prednesie rečnícky prejav; rozlíši fakt a názor"),
          ("Realizmus", "európsky realizmus; slovenský realizmus – S. H. Vajanský, M. Kukučín, P. O. Hviezdoslav, B. Slančíková-Timrava, J. Gregor Tajovský",
           "charakterizuje realizmus a jeho postavy; interpretuje novelu alebo poviedku slovenského realizmu; porovná dedinu v diele M. Kukučína a B. S. Timravy"),
          ("Moderna a medzivojnová literatúra", "symbolizmus, slovenská moderna – I. Krasko; avantgarda; medzivojnová próza a poézia – M. Urban, J. C. Hronský, nadrealizmus",
           "vysvetlí znaky symbolizmu; interpretuje báseň slovenskej moderny; charakterizuje lyrizovanú prózu"),
      ]),
      ("4. ročník", [
          ("Jazyk a jeho štýly", "jazykové štýly – hovorový, náučný, publicistický, administratívny, rečnícky, umelecký; vývin slovenského jazyka; jazykovedné disciplíny",
           "určí jazykový štýl textu a zdôvodní ho; opíše hlavné etapy vývinu slovenčiny; napíše žiadosť a životopis"),
          ("Literatúra po roku 1945", "literatúra druhej svetovej vojny; existencializmus; literatúra 50. a 60. rokov; postmoderna; súčasná slovenská literatúra",
           "zaradí dielo do kontextu obdobia; interpretuje dielo s vojnovou tematikou; porovná prozaické diela rôznych období"),
          ("Svetová a slovenská dráma", "antická a Shakespearova dráma; moderná dráma, absurdná dráma; slovenská dráma – J. Palárik, J. Gregor Tajovský, P. Karvaš",
           "rozlíši tragédiu, komédiu a drámu; analyzuje kompozíciu drámy; porovná klasickú a absurdnú drámu"),
          ("Príprava na maturitnú skúšku", "opakovanie jazyka a slohu; maturitné zadania písomnej formy; systematizácia literárnych smerov",
           "samostatne rieši úlohy externej časti maturity; napíše slohovú prácu v časovom limite; ústne interpretuje text podľa maturitného zadania"),
      ]),
  ],
  metody=METODY_JAZYKY + ["interpretácia umeleckého textu, literárne besedy a návštevy divadelných predstavení"],
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY, "V každom polroku žiak píše jednu slohovú prácu; hodnotí sa obsah, kompozícia, jazyková správnosť a štylistika."],
  zdroje=ZDROJE_ZAKLAD + ["Krátky slovník slovenského jazyka, Pravidlá slovenského pravopisu", "čítanky a výber umeleckých diel podľa odporúčaného zoznamu literatúry"],
  stav="hotova")

# ---------------------------------------------------------------------------------------------
P("Anglický jazyk", OBL, "ŠVP",
  char=[
      "Anglický jazyk je prvým cudzím jazykom; vzdelávanie smeruje k úrovni B2 podľa Spoločného európskeho referenčného rámca pre jazyky. "
      "Predmet rozvíja receptívne (počúvanie, čítanie), produktívne (hovorenie, písanie) a interaktívne jazykové činnosti.",
      "Obsah je usporiadaný do tematických okruhov, ktoré zodpovedajú témam maturitnej skúšky, a do komunikačných jazykových funkcií. "
      "Gramatika a slovná zásoba sú prostriedkom, nie cieľom komunikácie.",
  ],
  ciele=[
      "porozumieť hlavným myšlienkam aj podrobnostiam rozsiahlejšieho ústneho a písomného prejavu",
      "plynulo a spontánne komunikovať v bežných aj odborných situáciách",
      "napísať zrozumiteľný a súdržný text na známe témy (list, e-mail, esej, článok)",
      "používať jazykové prostriedky na úrovni B2 a opraviť vlastné chyby",
      "poznať reálie anglicky hovoriacich krajín a rozvíjať interkultúrnu kompetenciu",
  ],
  roc=[
      ("1. ročník", [
          ("Rodina a spoločnosť. Domov a bývanie", "osobné údaje, rodinné vzťahy; typy bývania, opis bytu; present simple a continuous; there is / are",
           "predstaví seba a svoju rodinu; opíše svoje bydlisko; porozumie krátkej nahrávke o bývaní"),
          ("Ľudské telo, zdravie. Výživa", "časti tela, choroby, u lekára; stravovacie návyky; počítateľné a nepočítateľné podstatné mená; some, any, much, many",
           "opíše zdravotné ťažkosti; objedná jedlo v reštaurácii; vyjadrí, čo má a nemá rád"),
          ("Voľný čas a záľuby. Šport", "záľuby, kultúrne a športové aktivity; past simple, past continuous; frekvenčné príslovky",
           "rozpráva o minulých udalostiach; napíše neformálny e-mail kamarátovi; zúčastní sa rozhovoru o voľnom čase"),
      ]),
      ("2. ročník", [
          ("Doprava a cestovanie", "dopravné prostriedky, ubytovanie, cestovné doklady; present perfect; budúci čas – will, going to",
           "kúpi si cestovný lístok a opýta sa na cestu; porovná dopravné prostriedky; napíše pohľadnicu z dovolenky"),
          ("Vzdelávanie a práca", "školský systém, predmety, povolania; modálne slovesá; podmienkové vety typu 0 a 1",
           "porovná slovenský a britský školský systém; vyjadrí povinnosť a možnosť; opíše svoje plány do budúcnosti"),
          ("Obchod a služby. Obliekanie a móda", "nakupovanie, reklamácia, služby; stupňovanie prídavných mien; trpný rod v prítomnom a minulom čase",
           "zreklamuje tovar; opíše a porovná oblečenie; porozumie reklamnému textu"),
      ]),
      ("3. ročník", [
          ("Človek a príroda. Veda a technika", "počasie, životné prostredie, ekológia; vynálezy, technológie; podmienkové vety typu 2 a 3; trpný rod",
           "vyjadrí názor na ekologický problém a zdôvodní ho; opíše vynález a jeho dopad; napíše esej s argumentáciou"),
          ("Multikultúrna spoločnosť. Krajiny a mestá", "zvyky a tradície, cudzinci; anglicky hovoriace krajiny; nepriama reč",
           "porovná tradície rôznych krajín; prerozpráva, čo niekto povedal; predstaví mesto alebo krajinu"),
          ("Kultúra a umenie. Mládež a jej svet", "film, hudba, literatúra; problémy mladých ľudí; vzťažné vety; frázové slovesá",
           "napíše recenziu filmu alebo knihy; diskutuje o problémoch mládeže; porozumie autentickému článku"),
      ]),
      ("4. ročník", [
          ("Zamestnanie. Človek a spoločnosť", "trh práce, žiadosť o zamestnanie, pohovor; formálny list; komunikačné stratégie",
           "napíše motivačný list a životopis; simuluje prijímací pohovor; zdvorilo vyjadrí nesúhlas"),
          ("Vzory a ideály. Slovensko – moja vlasť", "osobnosti, hodnoty; Slovensko – história, kultúra, prírodné krásy",
           "opíše svoj vzor a zdôvodní výber; predstaví Slovensko zahraničnému návštevníkovi"),
          ("Príprava na maturitnú skúšku", "testy úrovne B2; písomná forma – esej, list, článok; ústna forma – maturitné témy",
           "úspešne rieši úlohy na počúvanie, čítanie a jazykové prostriedky; samostatne rozpráva na maturitnú tému 10 – 12 minút"),
      ]),
  ],
  metody=METODY_JAZYKY,
  hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY, "Hodnotí sa porozumenie, ústny prejav, písomný prejav a používanie jazykových prostriedkov s porovnateľnou váhou."],
  zdroje=ZDROJE_ZAKLAD + ["učebnicový komplet úrovne B1+/B2 s pracovným zošitom a nahrávkami", "výkladové slovníky a online jazykové zdroje"],
  stav="hotova")

# ---------------------------------------------------------------------------------------------
def druhy_jazyk(nazov, jazyk, stav):
    P(nazov, OBL, "ŠVP",
      char=[
          f"{jazyk} sa vyučuje ako druhý cudzí jazyk; vzdelávanie smeruje k úrovni B1 podľa Spoločného európskeho referenčného rámca pre jazyky.",
          "Predmet rozvíja všetky jazykové činnosti s dôrazom na bežnú komunikáciu v každodenných situáciách. Žiaci sa oboznamujú "
          "s reáliami krajín, v ktorých sa jazyk používa, a s ich kultúrou.",
      ],
      ciele=[
          "porozumieť hlavným bodom jasného prejavu o známych témach",
          "zvládnuť bežné situácie počas pobytu v krajine, kde sa jazykom hovorí",
          "napísať jednoduchý súvislý text na známu tému",
          "opísať zážitky, udalosti, sny a nádeje a stručne zdôvodniť názor",
      ],
      roc=[
          ("1. ročník", [
              ("Zoznámenie a rodina", "pozdravy, predstavenie sa; rodina; prítomný čas pravidelných slovies; člen; zápor",
               "pozdraví a predstaví sa; opýta sa na základné osobné údaje; opíše svoju rodinu"),
              ("Škola a voľný čas", "školské predmety, rozvrh; záľuby; čísla, čas; modálne slovesá",
               "povie, čo robí vo voľnom čase; dohodne si stretnutie; porozumie jednoduchému rozvrhu"),
          ]),
          ("2. ročník", [
              ("Bývanie a mesto", "byt, izba, nábytok; orientácia v meste; predložky miesta; rozkazovací spôsob",
               "opíše svoju izbu; opýta sa na cestu a vysvetlí ju; porozumie jednoduchému plánu mesta"),
              ("Jedlo a nakupovanie", "potraviny, obchody, reštaurácia; množstvo; minulý čas",
               "nakúpi potraviny; objedná si jedlo; rozpráva o tom, čo robil cez víkend"),
          ]),
          ("3. ročník", [
              ("Cestovanie a dovolenka", "doprava, ubytovanie, počasie; budúci čas; vedľajšie vety",
               "rezervuje ubytovanie; opíše minulú dovolenku; napíše krátky list z dovolenky"),
              ("Zdravie a šport", "telo, choroby, u lekára; šport; zvratné slovesá",
               "opíše zdravotný problém; porovná športy; vyjadrí odporúčanie"),
          ]),
          ("4. ročník", [
              ("Krajiny a reálie", "geografia, osobnosti a tradície krajín cieľového jazyka; Slovensko",
               "predstaví krajinu cieľového jazyka; porovná zvyky so Slovenskom"),
              ("Práca a budúcnosť", "povolania, plány; podmieňovací spôsob; životopis",
               "opíše svoje plány do budúcnosti; napíše jednoduchý životopis; zúčastní sa rozhovoru o práci"),
          ]),
      ],
      metody=METODY_JAZYKY,
      hodn=HODNOTENIE_ZAKLAD + [HODNOTENIE_ZNAMKY],
      zdroje=ZDROJE_ZAKLAD + ["učebnicový komplet úrovne A1 – B1 s pracovným zošitom a nahrávkami"],
      stav=stav)


druhy_jazyk("Nemecký jazyk", "Nemecký jazyk", "hotova")
druhy_jazyk("Ruský jazyk", "Ruský jazyk", "na_kontrolu")

# ---------------------------------------------------------------------------------------------
P("Seminár zo slovenského jazyka a literatúry", OBL, "ŠkVP",
  char=[
      "Seminár je voliteľný predmet pre žiakov, ktorí chcú prehĺbiť vedomosti zo slovenského jazyka a literatúry, pripravujú sa na "
      "maturitnú skúšku alebo na štúdium humanitných odborov.",
      "Nadväzuje na obsahový a výkonový štandard predmetu Slovenský jazyk a literatúra a systematizuje učivo. Dôraz kladie na interpretáciu "
      "textu, argumentáciu a tvorbu vlastných textov.",
  ],
  ciele=[
      "systematizovať učivo z jazyka, slohu a literatúry",
      "zdokonaliť sa v interpretácii umeleckého a vecného textu",
      "rozvíjať tvorivé písanie a argumentáciu",
      "pripraviť sa na externú časť, písomnú formu internej časti aj ústnu formu maturity",
  ],
  roc=[
      ("3. ročník", [
          ("Práca s textom", "typy textov; súvislý a nesúvislý text; kľúčové slová, tvrdenia, fakty a názory",
           "vyhľadá a porovná informácie z viacerých textov; posúdi spoľahlivosť zdroja; sformuluje hlavnú myšlienku textu"),
          ("Tvorivé písanie", "esej, fejtón, glosa; štylistické prostriedky; kompozícia textu",
           "napíše esej s osobitým štýlom; použije štylistické prostriedky zámerne; zreviduje a upraví vlastný text"),
          ("Interpretácia lyriky", "lyrický subjekt, motív, téma; verzifikácia; básnické figúry a trópy",
           "interpretuje báseň podľa osnovy; porovná dve básne s podobnou témou"),
      ]),
      ("4. ročník", [
          ("Literatúra 20. a 21. storočia", "kľúčové diela slovenskej a svetovej prózy; postmoderna; súčasní autori",
           "zaradí dielo do kontextu; porovná spracovanie témy v rôznych dielach; prezentuje vlastnú čitateľskú skúsenosť"),
          ("Jazykové a slohové opakovanie", "pravopis, morfológia, syntax; slohové postupy a útvary",
           "bezchybne rieši pravopisné úlohy; určí slohový postup a útvar ukážky"),
          ("Maturitný tréning", "testy externej časti; slohové zadania; ústna odpoveď podľa zadaní",
           "rieši test v časovom limite; napíše slohovú prácu do 150 minút; prednesie súvislú ústnu odpoveď"),
      ]),
  ],
  metody=METODY_SEMINAR,
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotí sa aktivita na seminároch, písomné práce, referát a priebežné testy maturitného typu."],
  zdroje=ZDROJE_ZAKLAD + ["zbierky maturitných testov a zadaní", "odporúčaná literatúra pre maturantov"],
  stav="na_kontrolu",
  suvisi=[("Slovenský jazyk a literatúra", "nadvazuje"), ("Jazykom k maturite", "suvisi")])

# ---------------------------------------------------------------------------------------------
P("Konverzácia v anglickom jazyku", OBL, "ŠkVP",
  char=[
      "Predmet rozširuje hodinovú dotáciu anglického jazyka o hodiny zamerané na ústny prejav. Žiaci rozvíjajú plynulosť, slovnú zásobu a "
      "schopnosť spontánne reagovať v diskusii.",
      "Témy nadväzujú na maturitné tematické okruhy; konverzácia pripravuje žiakov na ústnu formu internej časti maturitnej skúšky.",
  ],
  ciele=[
      "rozvíjať plynulosť a spontánnosť ústneho prejavu",
      "rozšíriť slovnú zásobu o idiomatické výrazy a frázové slovesá",
      "argumentovať, vyjadriť súhlas a nesúhlas, viesť diskusiu",
      "opísať a porovnať obrázky a riešiť situačné úlohy",
  ],
  roc=[
      ("3. ročník", [
          ("Diskusia a argumentácia", "vyjadrenie názoru, súhlas a nesúhlas; zdvorilé prerušenie; debatné formáty",
           "vyjadrí a zdôvodní názor; reaguje na protiargument; zúčastní sa debaty"),
          ("Opis a porovnanie obrázkov", "opisné frázy, špekulácia, porovnávanie",
           "opíše a porovná dva obrázky a vyvodí z nich závery"),
          ("Aktuálne témy", "médiá, technológie, životné prostredie, zdravý životný štýl",
           "rozpráva o aktuálnej téme 3 – 5 minút; položí doplňujúce otázky"),
      ]),
      ("4. ročník", [
          ("Maturitné témy", "20 tematických okruhov ústnej maturity",
           "samostatne rozpráva na každú maturitnú tému; prepojí tému so svojou skúsenosťou"),
          ("Situačné úlohy", "rolové hry, riešenie problému, plánovanie",
           "zvládne situačnú úlohu bez prípravy; použije vhodné komunikačné stratégie"),
      ]),
  ],
  metody=METODY_JAZYKY,
  hodn=HODNOTENIE_ZAKLAD + ["Hodnotí sa najmä ústny prejav: plynulosť, rozsah slovnej zásoby, gramatická správnosť, výslovnosť a interakcia."],
  zdroje=ZDROJE_ZAKLAD + ["autentické materiály – články, podcasty, videá", "zbierky maturitných zadaní ústnej formy"],
  suvisi=[("Anglický jazyk", "nadvazuje"), ("Reálie v anglickom jazyku", "suvisi")])

# ---------------------------------------------------------------------------------------------
P("Konverzácia v nemeckom jazyku", OBL, "ŠkVP",
  char=[
      "Predmet rozvíja ústny prejav v nemeckom jazyku a pripravuje žiakov na ústnu formu maturitnej skúšky na úrovni B1.",
  ],
  ciele=[
      "zvýšiť plynulosť a istotu v ústnom prejave",
      "rozšíriť slovnú zásobu k maturitným témam",
      "reagovať v bežných komunikačných situáciách",
  ],
  roc=[
      ("3. ročník", [
          ("Každodenný život", "rodina, bývanie, škola, voľný čas – rozšírená slovná zásoba",
           "rozpráva o svojom živote súvisle 3 minúty; reaguje na otázky"),
          ("Situačné rozhovory", "nákup, cestovanie, u lekára, telefonovanie",
           "zvládne rozhovor v bežnej situácii; vyžiada si informáciu"),
      ]),
      ("4. ročník", [
          ("Maturitné témy", "tematické okruhy ústnej maturity B1",
           "samostatne rozpráva na maturitnú tému; opíše obrázok a vyjadrí názor"),
          ("Reálie nemecky hovoriacich krajín", "Nemecko, Rakúsko, Švajčiarsko – kultúra a tradície",
           "porovná tradície nemecky hovoriacich krajín so Slovenskom"),
      ]),
  ],
  metody=METODY_JAZYKY,
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Nemecký jazyk", "nadvazuje")])

# ---------------------------------------------------------------------------------------------
P("Reálie v anglickom jazyku", OBL, "ŠkVP",
  char=[
      "Predmet oboznamuje žiakov s geografiou, históriou, politickým systémom a kultúrou Spojeného kráľovstva, USA a ďalších anglicky "
      "hovoriacich krajín. Vyučuje sa v anglickom jazyku a rozvíja interkultúrnu kompetenciu.",
  ],
  ciele=[
      "poznať základné reálie anglicky hovoriacich krajín",
      "porozumieť kultúrnym rozdielom a tolerovať ich",
      "rozšíriť slovnú zásobu z oblasti histórie, politiky a kultúry",
  ],
  roc=[
      ("3. ročník", [
          ("Spojené kráľovstvo", "geografia, história, monarchia a parlament, sviatky a tradície",
           "opíše politický systém Spojeného kráľovstva; predstaví významnú historickú udalosť"),
          ("Spojené štáty americké", "štáty a regióny, história, ústava, americký sen",
           "porovná politický systém USA a Spojeného kráľovstva; prezentuje vybraný štát USA"),
          ("Ďalšie anglicky hovoriace krajiny", "Írsko, Kanada, Austrália, Nový Zéland",
           "pripraví projekt o vybranej krajine a odprezentuje ho"),
      ]),
  ],
  metody=METODY_JAZYKY,
  hodn=HODNOTENIE_ZAKLAD + ["Súčasťou hodnotenia je projekt o vybranej krajine."],
  zdroje=ZDROJE_ZAKLAD,
  suvisi=[("Anglický jazyk", "nadvazuje"), ("Dejepis", "medzipredmetovy"), ("Geografia", "medzipredmetovy")])

# ---------------------------------------------------------------------------------------------
P("Jazykom k maturite", OBL, "ŠkVP",
  char=[
      "Predmet pomáha maturantom zvládnuť jazykovú a komunikačnú stránku maturitnej skúšky zo slovenského jazyka a literatúry. Zameriava sa na "
      "čítanie s porozumením, jazykovú správnosť a tvorbu slohovej práce v časovom limite.",
  ],
  ciele=[
      "zlepšiť čítanie s porozumením náučných a umeleckých textov",
      "odstrániť najčastejšie pravopisné a štylistické chyby",
      "naučiť sa plánovať a písať slohovú prácu v časovom limite",
  ],
  roc=[
      ("4. ročník", [
          ("Čítanie s porozumením", "typy úloh externej časti; stratégie čítania",
           "zvolí vhodnú stratégiu čítania; správne odpovedá na úlohy s výberom aj s krátkou odpoveďou"),
          ("Jazyková správnosť", "pravopis, interpunkcia, frazeológia, štylistika",
           "opraví chyby v texte; zdôvodní pravopisné javy"),
          ("Slohová práca", "výber témy, osnova, argumentácia, úprava textu",
           "napíše slohovú prácu požadovaného útvaru a rozsahu; skontroluje a opraví vlastný text"),
      ]),
  ],
  metody=METODY_SEMINAR,
  hodn=HODNOTENIE_ZAKLAD,
  zdroje=ZDROJE_ZAKLAD + ["testy z predchádzajúcich ročníkov externej časti maturity"],
  suvisi=[("Slovenský jazyk a literatúra", "nadvazuje")])
