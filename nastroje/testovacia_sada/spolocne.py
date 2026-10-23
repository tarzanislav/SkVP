"""Pomocné funkcie a spoločné texty testovacej sady učebných osnov.

Každý predmet sa zapisuje funkciou P(...). Ročník je zoznam tematických celkov
(nazov, obsahovy_standard, vykonovy_standard); položky štandardov sa oddeľujú znakom „;“.
Výsledok spojí skript zostav.py do súboru src/SkVP.App/Zdroje/testovacia_sada.json.
"""

PREDMETY = []
OBLASTI = {}


def O(nazov, charakteristika):
    """Charakteristika vzdelávacej oblasti (zoznam odsekov)."""
    OBLASTI[nazov] = charakteristika


def P(nazov, oblast, zdroj, char, ciele, roc, metody=None, hodn=None, zdroje=None, stav=None, suvisi=None, poznamka=None):
    PREDMETY.append({
        "nazov": nazov,
        "oblast": oblast,
        "zdroj": zdroj,            # "ŠVP" alebo "ŠkVP" (predmet z disponibilných hodín školy)
        "charakteristika": char,
        "ciele": ciele,
        "rocniky": [
            {"rocnik": r, "celky": [{"nazov": c[0], "obsah": rozdel(c[1]), "vykon": rozdel(c[2])} for c in celky]}
            for r, celky in roc
        ],
        "metody": metody or [],
        "hodnotenie": hodn or [],
        "zdroje": zdroje or [],
        "stav": stav,              # None, "na_kontrolu", "hotova"
        "suvisi": [{"predmet": s[0], "typ": s[1]} for s in (suvisi or [])],
        "poznamka": poznamka,
    })


def rozdel(text):
    return [x.strip() for x in text.split(";") if x.strip()]


# ---- spoločné texty ----

HODNOTENIE_ZAKLAD = [
    "Žiak je hodnotený v súlade s platnými predpismi o hodnotení a klasifikácii žiakov stredných škôl a so školským poriadkom.",
    "Hodnotí sa priebežne počas celého školského roka a súhrnne na konci každého polroka; výsledné hodnotenie nie je aritmetickým priemerom známok, zohľadňuje aj aktivitu, prístup a pokrok žiaka.",
    "Žiak pozná kritériá hodnotenia vopred; pri písomných prácach učiteľ oznámi rozsah učiva a spôsob bodovania.",
]

HODNOTENIE_ZNAMKY = "Stupnica pre písomné práce a testy: 100 – 90 % výborný, 89 – 75 % chválitebný, 74 – 50 % dobrý, 49 – 30 % dostatočný, menej ako 30 % nedostatočný."

METODY_JAZYKY = [
    "komunikatívny prístup – práca vo dvojiciach a skupinách, rolové hry, simulácie situácií",
    "práca s autentickými textami (články, nahrávky, videá) a s textami rôznych slohových postupov",
    "projektové vyučovanie a prezentácie, debaty a diskusie",
    "samostatná práca so slovníkmi, gramatickými prehľadmi a digitálnymi nástrojmi",
    "sebahodnotenie a vzájomné hodnotenie (portfólio, kontrolné zoznamy)",
]

METODY_PRIRODNE = [
    "bádateľsky orientované vyučovanie – formulovanie hypotéz, pozorovanie, pokus, vyhodnotenie",
    "laboratórne práce a demonštračné pokusy s dôrazom na bezpečnosť práce",
    "riešenie problémových úloh a úloh z praxe, práca s grafmi a tabuľkami",
    "skupinová a projektová práca, prezentácie výsledkov",
    "využitie digitálnych technológií – simulácie, meracie systémy, tabuľkový kalkulátor",
]

METODY_SPOLOCENSKE = [
    "výklad a riadený rozhovor, heuristická metóda",
    "práca s historickými prameňmi, mapami, štatistikami a médiami",
    "diskusia, debata, argumentácia a obhajoba vlastného názoru",
    "projektová a skupinová práca, exkurzie a besedy",
    "kritické myslenie – overovanie informácií, rozlišovanie faktov a názorov",
]

METODY_SEMINAR = [
    "riešenie úloh maturitného typu a úloh z predchádzajúcich ročníkov maturity",
    "samostatná práca s odbornou literatúrou a internetovými zdrojmi",
    "referáty, prezentácie a obhajoba vlastnej práce",
    "skupinové riešenie problémov a vzájomné vysvetľovanie",
    "individuálne konzultácie podľa potrieb žiakov",
]

METODY_TSV = [
    "metódy nácviku pohybových zručností – názorná ukážka, slovný výklad, opakovanie a korekcia",
    "hra a súťaž, kruhový a intervalový tréning",
    "testovanie pohybovej výkonnosti a sebahodnotenie pokroku",
    "individuálny prístup podľa zdravotného stavu a výkonnosti žiaka",
]

ZDROJE_ZAKLAD = [
    "schválené učebnice a pracovné zošity z edičného portfólia ministerstva školstva",
    "učebné materiály a pracovné listy pripravené vyučujúcimi predmetovej komisie",
    "digitálne vzdelávacie zdroje a školské e-learningové prostredie",
]
