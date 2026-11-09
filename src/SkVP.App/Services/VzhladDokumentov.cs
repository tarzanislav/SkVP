using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;
using SkVP.Data;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>Odkiaľ majú nadpisy farbu.</summary>
public enum FarbaNadpisov
{
    /// <summary>Farba vzdelávacej oblasti (každá oblasť má svoju).</summary>
    Oblast,

    /// <summary>Jedna farba šablóny pre všetky oblasti.</summary>
    Akcent,

    /// <summary>Tmavé nadpisy bez farby.</summary>
    Tmava,
}

/// <summary>Ozdoba nadpisu 1. úrovne (časť osnovy).</summary>
public enum OzdobaNadpisu
{
    Ziadna,

    /// <summary>Čiara pod nadpisom.</summary>
    Ciara,

    /// <summary>Zvislý pásik vľavo od nadpisu.</summary>
    Pasik,

    /// <summary>Svetlý podfarbený pás cez celú šírku s pásikom vľavo.</summary>
    Pas,

    /// <summary>Sýta farebná karta cez celú šírku s bielym textom.</summary>
    Karta,

    /// <summary>Hrubá čiara nad nadpisom – štýl časopisov a novín.</summary>
    Nadciarka,

    /// <summary>Tenký farebný rámik okolo nadpisu.</summary>
    Ramik,
}

/// <summary>Čiary tabuliek.</summary>
public enum RamTabulky
{
    /// <summary>Jemná svetlá mriežka.</summary>
    Jemny,

    /// <summary>Výraznejšia tmavšia mriežka (úradné dokumenty).</summary>
    Mriezka,

    /// <summary>Len vodorovné čiary medzi riadkami – vzdušná moderná tabuľka.</summary>
    Vodorovny,

    /// <summary>Akademická tabuľka: čiara nad a pod tabuľkou a pod hlavičkou, inak bez čiar.</summary>
    Akademicky,
}

/// <summary>Podklad hlavičky tabuľky (prvý riadok označený ako hlavička).</summary>
public enum HlavickaTabulky
{
    Siva,

    /// <summary>Svetlý odtieň farby šablóny alebo oblasti.</summary>
    Odtien,

    /// <summary>Plná pastelová farba.</summary>
    Plna,

    /// <summary>Sýta tmavá farba s bielym textom.</summary>
    Tmava,
}

/// <summary>
/// Šablóna vzhľadu dokumentov osnov – písma, veľkosti, riadkovanie, farby a ozdoby nadpisov, tabuľky. Platí rovnako
/// v editore programu, v PDF, vo Worde aj v tlačových zostavách. Veľkosti sú v bodoch (pt). Vlastná šablóna
/// (<see cref="VzhladDokumentov.IdVlastnej"/>) sa ukladá ako JSON.
/// </summary>
public sealed record SablonaVzhladu(
    string Id,
    string Nazov,
    string Popis,
    string[] PismaTextu,
    string[] PismaNadpisov,
    double VelkostTextu,
    double Nadpis1,
    double Nadpis2,
    double Nadpis3,
    double Riadkovanie,
    double MedzeraZaOdsekom,
    bool DoBloku,
    FarbaNadpisov Farba,
    OzdobaNadpisu Ozdoba,
    RamTabulky Ram,
    HlavickaTabulky Hlavicka,
    bool Pruhy,
    string? Akcent = null,
    bool FarebnyNadpis2 = false,
    bool CitatelnaFarba = true,
    bool Nadpis2Pasik = false,
    string FarbaTextu = "#1F2933",
    string Skupina = VzhladDokumentov.SkupinaFarebne)
{
    private const string Atrament = "#1F2933";
    private const string Biela = "#FFFFFF";
    private const string Svetla = "#CBD2D9";

    /// <summary>Písmo textu – prvé nainštalované zo zoznamu.</summary>
    [JsonIgnore]
    public string PismoTextu => VzhladDokumentov.Najdi(PismaTextu);

    /// <summary>Písmo nadpisov – prvé nainštalované zo zoznamu.</summary>
    [JsonIgnore]
    public string PismoNadpisov => VzhladDokumentov.Najdi(PismaNadpisov);

    public double VelkostNadpisu(int uroven) => uroven switch
    {
        1 => Nadpis1,
        2 => Nadpis2,
        _ => Nadpis3,
    };

    /// <summary>Body → jednotky WPF (editor).</summary>
    public static double NaDip(double body) => body * 96 / 72;

    /// <summary>Základná farba šablóny pre oblasť (na pásiky, čiary a podklady).</summary>
    public string Zakladna(string? oblast) => Farba switch
    {
        FarbaNadpisov.Akcent => Akcent ?? VzhladDokumentov.PredvolenaOblast,
        FarbaNadpisov.Tmava => "#5B6573",
        _ => Pastelova(oblast),
    };

    /// <summary>Pastelový odtieň farby šablóny (plné hlavičky tabuliek).</summary>
    public string Pastelova(string? oblast) => VzhladDokumentov.Hex(Pastel.Zjemni(VzhladDokumentov.ZHex(
        Farba == FarbaNadpisov.Akcent ? Akcent ?? VzhladDokumentov.PredvolenaOblast : oblast ?? VzhladDokumentov.PredvolenaOblast)));

    /// <summary>Sýta tmavá farba (karta nadpisu, tmavá hlavička tabuľky) – biely text na nej je dobre čitateľný.</summary>
    public string Syta(string? oblast) => Farba switch
    {
        FarbaNadpisov.Akcent => Akcent ?? VzhladDokumentov.PredvolenaOblast,
        FarbaNadpisov.Tmava => "#2B3440",
        _ => VzhladDokumentov.Hex(Pastel.Text(VzhladDokumentov.ZHex(oblast ?? VzhladDokumentov.PredvolenaOblast), false)),
    };

    /// <summary>Farba textu nadpisu danej úrovne (null = bežná farba textu).</summary>
    public string? FarbaNadpisu(int uroven, string? oblast)
    {
        if (uroven == 1 && Ozdoba == OzdobaNadpisu.Karta)
        {
            return Biela;
        }

        if (Farba == FarbaNadpisov.Tmava || uroven > 2 || (uroven == 2 && !FarebnyNadpis2))
        {
            return null;
        }

        if (Farba == FarbaNadpisov.Akcent)
        {
            return Akcent;
        }

        var farba = oblast ?? VzhladDokumentov.PredvolenaOblast;
        return CitatelnaFarba ? VzhladDokumentov.Hex(Pastel.Text(VzhladDokumentov.ZHex(farba), false)) : farba;
    }

    /// <summary>Farba čiary, pásika, rámika alebo karty pri nadpise.</summary>
    public string FarbaOzdoby(string? oblast) => Ozdoba == OzdobaNadpisu.Karta ? Syta(oblast)
        : Farba == FarbaNadpisov.Tmava ? Atrament
        : Zakladna(oblast);

    /// <summary>Čiara pod názvom predmetu v zostave (pri šablóne s farbami oblastí farba oblasti).</summary>
    public string FarbaPredmetu(string? oblast) =>
        Farba == FarbaNadpisov.Oblast ? oblast ?? VzhladDokumentov.PredvolenaOblast : FarbaOzdoby(oblast);

    /// <summary>Riadok „oblasť • číslo“ nad názvom predmetu v zostave.</summary>
    public string FarbaZahlavia(string? oblast) => Farba == FarbaNadpisov.Tmava ? "#6B7280" : FarbaPredmetu(oblast);

    /// <summary>Podklad nadpisu (pás alebo karta), null = bez podkladu.</summary>
    public string? PodkladNadpisu(string? oblast) => Ozdoba switch
    {
        OzdobaNadpisu.Karta => Syta(oblast),
        OzdobaNadpisu.Pas => VzhladDokumentov.Zmiesaj(Zakladna(oblast), Biela, 0.84),
        _ => null,
    };

    /// <summary>Podklad hlavičky tabuľky.</summary>
    public string PodkladHlavicky(string? oblast) => Hlavicka switch
    {
        HlavickaTabulky.Tmava => Syta(oblast),
        HlavickaTabulky.Plna => Pastelova(oblast),
        HlavickaTabulky.Odtien => VzhladDokumentov.Zmiesaj(Zakladna(oblast), Biela, 0.7),
        _ => "#F1F3F5",
    };

    /// <summary>Farba textu v hlavičke tabuľky (null = bežná farba textu).</summary>
    [JsonIgnore]
    public string? TextHlavicky => Hlavicka == HlavickaTabulky.Tmava ? Biela : null;

    /// <summary>Podklad každého druhého riadku tabuľky (null = bez pruhov).</summary>
    public string? PodkladPruhu(string? oblast) =>
        Pruhy ? VzhladDokumentov.Zmiesaj(Farba == FarbaNadpisov.Tmava ? "#AEB6C2" : Zakladna(oblast), Biela, 0.9) : null;

    /// <summary>Farba čiar tabuľky.</summary>
    public string Linka(string? oblast) => Ram switch
    {
        RamTabulky.Mriezka => "#6B7480",
        RamTabulky.Akademicky => Farba == FarbaNadpisov.Tmava ? "#2B3440" : Syta(oblast),
        RamTabulky.Vodorovny when Farba != FarbaNadpisov.Tmava => VzhladDokumentov.Zmiesaj(Zakladna(oblast), Svetla, 0.45),
        _ => Svetla,
    };

    /// <summary>Hrúbka čiar tabuľky (pt).</summary>
    [JsonIgnore]
    public double HrubkaLinky => Ram switch
    {
        RamTabulky.Mriezka => 0.75,
        RamTabulky.Akademicky => 1.25,
        _ => 0.5,
    };

    /// <summary>Hrúbka čiary pod hlavičkou tabuľky pri vodorovných alebo akademických čiarach (pt).</summary>
    [JsonIgnore]
    public double HrubkaPodHlavickou => Ram switch
    {
        RamTabulky.Vodorovny => 1.5,
        RamTabulky.Akademicky => 0.75,
        _ => HrubkaLinky,
    };

    /// <summary>Tabuľka má čiaru pod hlavičkou inú ako ostatné (vodorovné a akademické čiary).</summary>
    [JsonIgnore]
    public bool CiaraPodHlavickou => Ram is RamTabulky.Vodorovny or RamTabulky.Akademicky;
}

/// <summary>
/// Katalóg šablón vzhľadu a zvolená šablóna (uložená v nastaveniach). Zmena šablóny sa prejaví v celom programe –
/// otvorené dokumenty sa prekreslia (<see cref="Zmenena"/>), zostavy a súbory Word sa vytvoria v novom vzhľade.
/// </summary>
public static class VzhladDokumentov
{
    public const string Kluc = "dokumenty.sablona";
    public const string KlucVlastnej = "dokumenty.sablona_vlastna";
    public const string PredvolenaOblast = "#7B93DB";
    public const string IdPredvolenej = "pastelovy";
    public const string IdVlastnej = "vlastna";

    public const string SkupinaFarebne = "Farebné";
    public const string SkupinaModerne = "Moderné";
    public const string SkupinaKlasicke = "Klasické";
    public const string SkupinaPrakticke = "Praktické";

    /// <summary>Skupiny šablón v galérii.</summary>
    public static IReadOnlyList<string> Skupiny { get; } = new[] { SkupinaFarebne, SkupinaModerne, SkupinaKlasicke, SkupinaPrakticke };

    private static readonly ConcurrentDictionary<string, string> NajdenePisma = new();
    private static SablonaVzhladu? _ulozena;

    [ThreadStatic]
    private static SablonaVzhladu? _docasna;

    /// <summary>Šablóny programu v poradí, v akom sa ponúkajú (bez vlastnej).</summary>
    public static IReadOnlyList<SablonaVzhladu> Vsetky { get; } = new SablonaVzhladu[]
    {
        // ---- farebné ----
        new(IdPredvolenej, "Pastelový", "Pôvodný vzhľad programu – pätkové písmo textu, nadpisy vo farbe oblasti, jemné tabuľky.",
            new[] { "Constantia", "Georgia" }, new[] { "Segoe UI" },
            11, 14, 12, 11, 1.18, 6, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Ziadna, RamTabulky.Jemny, HlavickaTabulky.Siva, false,
            CitatelnaFarba: false),
        new("farebny", "Farebný", "Nadpisy na farebnom páse, plné farebné hlavičky tabuliek – každá oblasť vo svojej farbe.",
            new[] { "Calibri", "Segoe UI" }, new[] { "Segoe UI" },
            11, 15, 12.5, 11, 1.25, 7, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Pas, RamTabulky.Jemny, HlavickaTabulky.Plna, true,
            FarebnyNadpis2: true),
        new("pestry", "Pestrý", "Sýte farebné karty nadpisov a tmavé hlavičky tabuliek vo farbe každej oblasti – výrazný a radostný.",
            new[] { "Segoe UI" }, new[] { "Segoe UI" },
            10.5, 15, 12.5, 11, 1.3, 7, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Karta, RamTabulky.Vodorovny, HlavickaTabulky.Tmava, true,
            FarebnyNadpis2: true, Nadpis2Pasik: true),
        new("tyrkysovy", "Tyrkysový", "Svieža tyrkysová – karty nadpisov, tmavé hlavičky tabuliek, pruhované riadky.",
            new[] { "Calibri", "Segoe UI" }, new[] { "Trebuchet MS", "Segoe UI" },
            11, 15, 12.5, 11, 1.25, 7, false, FarbaNadpisov.Akcent, OzdobaNadpisu.Karta, RamTabulky.Vodorovny, HlavickaTabulky.Tmava, true,
            Akcent: "#0E7C7B", FarebnyNadpis2: true),
        new("koralovy", "Korálový", "Teplá korálová – pásiky pri nadpisoch oboch úrovní a jemné korálové tabuľky.",
            new[] { "Corbel", "Calibri" }, new[] { "Candara", "Segoe UI" },
            11.5, 16, 13, 11.5, 1.25, 7, false, FarbaNadpisov.Akcent, OzdobaNadpisu.Pasik, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, true,
            Akcent: "#C2503B", FarebnyNadpis2: true, Nadpis2Pasik: true),
        new("levandulovy", "Levanduľový", "Jemná fialová – pás pri nadpisoch, pastelové hlavičky tabuliek, pätkové písmo textu.",
            new[] { "Georgia" }, new[] { "Segoe UI" },
            10.5, 15, 12.5, 11, 1.28, 7, false, FarbaNadpisov.Akcent, OzdobaNadpisu.Pas, RamTabulky.Jemny, HlavickaTabulky.Plna, false,
            Akcent: "#6A48B0", FarebnyNadpis2: true),
        new("smaragdovy", "Smaragdový", "Zelená ako príroda – čiara nad nadpisom, tmavozelené hlavičky tabuliek.",
            new[] { "Georgia" }, new[] { "Franklin Gothic Medium", "Segoe UI" },
            10.5, 16, 13, 11, 1.28, 7, true, FarbaNadpisov.Akcent, OzdobaNadpisu.Nadciarka, RamTabulky.Vodorovny, HlavickaTabulky.Tmava, false,
            Akcent: "#23784B", FarebnyNadpis2: true),
        new("ocean", "Oceán", "Hlboká modrá – rámik okolo nadpisov, tmavomodré hlavičky tabuliek s jemnou mriežkou.",
            new[] { "Constantia", "Georgia" }, new[] { "Segoe UI" },
            11, 15, 12.5, 11, 1.25, 7, false, FarbaNadpisov.Akcent, OzdobaNadpisu.Ramik, RamTabulky.Jemny, HlavickaTabulky.Tmava, true,
            Akcent: "#1D5D9B", FarebnyNadpis2: true),

        // ---- moderné ----
        new("moderny", "Moderný", "Bezpätkové písmo, farebný pásik pri nadpisoch, vzdušné tabuľky s pruhmi vo farbe oblasti.",
            new[] { "Segoe UI" }, new[] { "Segoe UI" },
            10.5, 15, 12.5, 11, 1.3, 7, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Pasik, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, true,
            FarebnyNadpis2: true, Skupina: SkupinaModerne),
        new("svieze", "Svieži", "Zaoblené písma, nadpisy s farebnou čiarou, tabuľky s jemným farebným odtieňom.",
            new[] { "Corbel", "Calibri" }, new[] { "Candara", "Segoe UI" },
            11.5, 16, 13, 11.5, 1.25, 7, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Ciara, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, false,
            FarebnyNadpis2: true, Skupina: SkupinaModerne),
        new("minimalisticky", "Minimalistický", "Veľa bieleho miesta, takmer čierny text, bez ozdôb – čistý ako moderná webová stránka.",
            new[] { "Segoe UI" }, new[] { "Segoe UI" },
            10.5, 16, 12.5, 11, 1.4, 9, false, FarbaNadpisov.Tmava, OzdobaNadpisu.Ziadna, RamTabulky.Vodorovny, HlavickaTabulky.Siva, false,
            FarbaTextu: "#333840", Skupina: SkupinaModerne),
        new("indigo", "Indigo", "Technologický vzhľad – indigové karty nadpisov, tmavé hlavičky a vzdušné tabuľky.",
            new[] { "Segoe UI" }, new[] { "Segoe UI" },
            10.5, 15, 12.5, 11, 1.3, 7, false, FarbaNadpisov.Akcent, OzdobaNadpisu.Karta, RamTabulky.Vodorovny, HlavickaTabulky.Tmava, true,
            Akcent: "#4338CA", FarebnyNadpis2: true, Nadpis2Pasik: true, Skupina: SkupinaModerne),
        new("grafitovy", "Grafitový", "Elegantná sivá – grafitové karty nadpisov a hlavičky, farba len v pásikoch nadpisov 2. úrovne.",
            new[] { "Calibri", "Segoe UI" }, new[] { "Segoe UI" },
            11, 15, 12.5, 11, 1.25, 7, false, FarbaNadpisov.Akcent, OzdobaNadpisu.Karta, RamTabulky.Vodorovny, HlavickaTabulky.Tmava, true,
            Akcent: "#374151", Nadpis2Pasik: true, Skupina: SkupinaModerne),
        new("casopis", "Časopis", "Štýl novín a časopisov – výrazné bezpätkové nadpisy pod hrubou čiarou, pätkový text do bloku.",
            new[] { "Georgia" }, new[] { "Franklin Gothic Medium", "Arial" },
            10.5, 17, 13, 11, 1.28, 6, true, FarbaNadpisov.Tmava, OzdobaNadpisu.Nadciarka, RamTabulky.Akademicky, HlavickaTabulky.Siva, false,
            FarbaTextu: "#262A30", Skupina: SkupinaModerne),
        new("firemny", "Firemný", "Ako profesionálna správa – Calibri, modré nadpisy, modré hlavičky tabuliek s bielym textom.",
            new[] { "Calibri", "Segoe UI" }, new[] { "Calibri", "Segoe UI" },
            11, 16, 13, 11.5, 1.15, 7, false, FarbaNadpisov.Akcent, OzdobaNadpisu.Ciara, RamTabulky.Jemny, HlavickaTabulky.Tmava, true,
            Akcent: "#2E5E9E", FarebnyNadpis2: true, FarbaTextu: "#333333", Skupina: SkupinaModerne),

        // ---- klasické ----
        new("elegantny", "Elegantný", "Klasické knižné písma, tmavomodré nadpisy s tenkou čiarou, text do bloku.",
            new[] { "Georgia" }, new[] { "Palatino Linotype", "Georgia" },
            10.5, 15, 12.5, 11, 1.28, 6, true, FarbaNadpisov.Akcent, OzdobaNadpisu.Ciara, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, false,
            Akcent: "#24406E", FarebnyNadpis2: true, Skupina: SkupinaKlasicke),
        new("klasicky", "Klasický", "Tradičný vzhľad školských dokumentov – Times New Roman, čierne nadpisy, mriežka, text do bloku.",
            new[] { "Times New Roman" }, new[] { "Times New Roman" },
            11.5, 14, 12, 11.5, 1.15, 6, true, FarbaNadpisov.Tmava, OzdobaNadpisu.Ciara, RamTabulky.Mriezka, HlavickaTabulky.Siva, false,
            Skupina: SkupinaKlasicke),
        new("akademicky", "Akademický", "Ako odborná publikácia – Times New Roman 12, akademické tabuľky len s tromi čiarami.",
            new[] { "Times New Roman" }, new[] { "Times New Roman" },
            12, 14, 12.5, 12, 1.2, 6, true, FarbaNadpisov.Tmava, OzdobaNadpisu.Ziadna, RamTabulky.Akademicky, HlavickaTabulky.Siva, false,
            FarbaTextu: "#111111", Skupina: SkupinaKlasicke),
        new("knizny", "Knižný", "Ako kniha – Palatino, bordové nadpisy, akademické tabuľky, text do bloku.",
            new[] { "Palatino Linotype", "Georgia" }, new[] { "Palatino Linotype", "Georgia" },
            11, 15, 12.5, 11, 1.3, 6, true, FarbaNadpisov.Akcent, OzdobaNadpisu.Ciara, RamTabulky.Akademicky, HlavickaTabulky.Siva, false,
            Akcent: "#7A2E35", FarebnyNadpis2: true, FarbaTextu: "#2A2522", Skupina: SkupinaKlasicke),
        new("archivny", "Archívny", "Teplé hnedé tóny – Georgia, rámik okolo nadpisov, klasická mriežka.",
            new[] { "Georgia" }, new[] { "Georgia" },
            10.5, 14.5, 12, 11, 1.25, 6, true, FarbaNadpisov.Akcent, OzdobaNadpisu.Ramik, RamTabulky.Mriezka, HlavickaTabulky.Odtien, false,
            Akcent: "#7A5230", FarebnyNadpis2: true, FarbaTextu: "#2E2A26", Skupina: SkupinaKlasicke),
        new("uradny", "Úradný", "Strohý vzhľad na úradné účely – Arial, bez farieb a ozdôb, výrazná mriežka, text do bloku.",
            new[] { "Arial" }, new[] { "Arial" },
            11, 13, 11.5, 11, 1.15, 6, true, FarbaNadpisov.Tmava, OzdobaNadpisu.Ziadna, RamTabulky.Mriezka, HlavickaTabulky.Siva, false,
            Skupina: SkupinaKlasicke),

        // ---- praktické ----
        new("usporny", "Úsporný", "Menšie písmo a riadkovanie – osnovy zaberú menej strán pri tlači.",
            new[] { "Calibri", "Segoe UI" }, new[] { "Calibri", "Segoe UI" },
            10, 12.5, 11, 10, 1.05, 4, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Ziadna, RamTabulky.Jemny, HlavickaTabulky.Siva, false,
            Skupina: SkupinaPrakticke),
        new("kompaktny", "Kompaktný", "Najmenej strán – Tahoma 9,5, tesné riadky, tabuľky s jemnou mriežkou, čiernobiely.",
            new[] { "Tahoma", "Arial" }, new[] { "Tahoma", "Arial" },
            9.5, 12, 10.5, 9.5, 1.05, 3, false, FarbaNadpisov.Tmava, OzdobaNadpisu.Ziadna, RamTabulky.Jemny, HlavickaTabulky.Siva, false,
            Skupina: SkupinaPrakticke),
        new("citatelny", "Čitateľný", "Väčšie písmo, voľnejšie riadky a výrazné nadpisy – dobre sa číta aj zo zhoršeným zrakom.",
            new[] { "Verdana", "Tahoma" }, new[] { "Verdana", "Tahoma" },
            11.5, 16, 13.5, 12, 1.42, 9, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Pas, RamTabulky.Mriezka, HlavickaTabulky.Odtien, false,
            FarebnyNadpis2: true, Skupina: SkupinaPrakticke),
        new("prezentacny", "Prezentačný", "Veľké nadpisy na farebných kartách a väčšie písmo – na premietanie a čítanie z obrazovky.",
            new[] { "Trebuchet MS", "Segoe UI" }, new[] { "Trebuchet MS", "Segoe UI" },
            12, 18, 14, 12.5, 1.35, 9, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Karta, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, true,
            FarebnyNadpis2: true, Skupina: SkupinaPrakticke),
        new("tlac", "Na tlač", "Čiernobiely vzhľad pre bežnú tlačiareň – kontrastné nadpisy s čiarou, sivé hlavičky, bez farieb.",
            new[] { "Calibri", "Arial" }, new[] { "Calibri", "Arial" },
            11, 14, 12, 11, 1.15, 6, false, FarbaNadpisov.Tmava, OzdobaNadpisu.Ciara, RamTabulky.Mriezka, HlavickaTabulky.Siva, false,
            FarbaTextu: "#000000", Skupina: SkupinaPrakticke),
    };

    /// <summary>Šablóna prispôsobená používateľom (null = zatiaľ nie je).</summary>
    public static SablonaVzhladu? Vlastna
    {
        get
        {
            try
            {
                return Databaza.CitajJson<SablonaVzhladu>(KlucVlastnej) is { } vlastna ? vlastna with { Id = IdVlastnej } : null;
            }
            catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException or System.IO.IOException)
            {
                return null;
            }
        }
    }

    /// <summary>Šablóny v galérii – vlastná (ak je) a šablóny programu.</summary>
    public static IReadOnlyList<SablonaVzhladu> VGalerii => Vlastna is { } vlastna ? Vsetky.Prepend(vlastna).ToList() : Vsetky;

    /// <summary>Zvolená šablóna (počas <see cref="Docasne"/> šablóna náhľadu).</summary>
    public static SablonaVzhladu Aktivna => _docasna ?? (_ulozena ??= Nacitaj());

    /// <summary>Šablóna sa zmenila – otvorené dokumenty sa majú prekresliť.</summary>
    public static event Action? Zmenena;

    public static SablonaVzhladu Podla(string? id) =>
        (id == IdVlastnej ? Vlastna : null) ?? Vsetky.FirstOrDefault(s => s.Id == id) ?? Vsetky[0];

    /// <summary>
    /// Vlastná šablóna podľa vybranej – iná farba, písma, veľkosť textu a zarovnanie. Nazov a popis vzniknú zo základu.
    /// </summary>
    /// <param name="farba">farba nadpisov a ozdôb (#RRGGBB) alebo null = farby oblastí</param>
    public static SablonaVzhladu Prisposob(SablonaVzhladu zaklad, string? farba, string pismoNadpisov, string pismoTextu, double velkost, bool doBloku)
    {
        var pomer = velkost / zaklad.VelkostTextu;
        double Zaokruhli(double body) => Math.Round(body * pomer * 2, MidpointRounding.AwayFromZero) / 2;
        var nazovZakladu = zaklad.Id == IdVlastnej ? zaklad.Popis.Replace("Upravená šablóna ", "", StringComparison.Ordinal).TrimEnd('.') : zaklad.Nazov;
        return zaklad with
        {
            Id = IdVlastnej,
            Nazov = "Vlastná",
            Popis = $"Upravená šablóna {nazovZakladu}.",
            PismaNadpisov = new[] { pismoNadpisov },
            PismaTextu = new[] { pismoTextu },
            VelkostTextu = velkost,
            Nadpis1 = Zaokruhli(zaklad.Nadpis1),
            Nadpis2 = Zaokruhli(zaklad.Nadpis2),
            Nadpis3 = Zaokruhli(zaklad.Nadpis3),
            DoBloku = doBloku,
            Farba = farba is null ? FarbaNadpisov.Oblast : FarbaNadpisov.Akcent,
            Akcent = farba,
            FarebnyNadpis2 = true,
            CitatelnaFarba = true,
            Skupina = "Vlastná",
        };
    }

    /// <summary>Uloží šablónu ako zvolenú pre všetky dokumenty (vlastnú aj s jej nastavením).</summary>
    public static void Nastav(SablonaVzhladu sablona)
    {
        if (sablona.Id == IdVlastnej)
        {
            Databaza.ZapisJson(KlucVlastnej, sablona);
        }

        Databaza.Zapis(Kluc, sablona.Id);
        _ulozena = sablona;
        Oznam();
    }

    /// <summary>Šablónu znova načíta z nastavení (po importe nastavení).</summary>
    public static void Obnov()
    {
        _ulozena = null;
        Oznam();
    }

    /// <summary>Udalosť zmeny vždy vo vlákne okien (editory sa prekresľujú).</summary>
    private static void Oznam()
    {
        var vlakno = System.Windows.Application.Current?.Dispatcher;
        if (vlakno is null || vlakno.CheckAccess())
        {
            Zmenena?.Invoke();
        }
        else
        {
            vlakno.BeginInvoke(() => Zmenena?.Invoke());
        }
    }

    /// <summary>Dočasne použije inú šablónu v tomto vlákne (náhľad, ukážky šablón).</summary>
    public static IDisposable Docasne(SablonaVzhladu sablona)
    {
        var predosla = _docasna;
        _docasna = sablona;
        return new Navrat(() => _docasna = predosla);
    }

    private static SablonaVzhladu Nacitaj()
    {
        try
        {
            return Podla(Databaza.Citaj(Kluc));
        }
        catch (Exception ex) when (ex is Microsoft.Data.Sqlite.SqliteException or InvalidOperationException or System.IO.IOException)
        {
            return Vsetky[0];
        }
    }

    /// <summary>
    /// Prvé písmo zo zoznamu, ktoré je nainštalované a dá sa vložiť do PDF (písma v kolekcii .ttc, napr. Cambria,
    /// PDFsharp nevie použiť); inak Arial. Výsledok sa pamätá.
    /// </summary>
    public static string Najdi(IReadOnlyList<string> mena) => NajdenePisma.GetOrAdd(string.Join("|", mena), _ => Vyhladaj(mena));

    /// <summary>Písmo sa dá použiť v programe aj v PDF.</summary>
    public static bool JePouzitelne(string meno) => Najdi(new[] { meno }) == meno;

    private static string Vyhladaj(IReadOnlyList<string> mena)
    {
        HashSet<string> systemove;
        try
        {
            systemove = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return mena[0];
        }

        foreach (var meno in mena.Where(systemove.Contains))
        {
            try
            {
                _ = new PdfSharp.Drawing.XFont(meno, 10);
                return meno;
            }
            catch (Exception ex)
            {
                Zaznam.Upozornenie($"Písmo {meno} sa nedá použiť v PDF – {ex.Message}");
            }
        }

        return "Arial";
    }

    // ---- farby ----

    public static Color ZHex(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return (Color)ColorConverter.ConvertFromString(PredvolenaOblast);
        }
    }

    public static string Hex(Color c) => string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}");

    /// <summary>Zmes dvoch farieb – <paramref name="podielDruhej"/> = koľko z druhej farby (0 – 1).</summary>
    public static string Zmiesaj(string prva, string druha, double podielDruhej)
    {
        var a = ZHex(prva);
        var b = ZHex(druha);
        var t = Math.Clamp(podielDruhej, 0, 1);
        byte Kanal(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
        return Hex(Color.FromRgb(Kanal(a.R, b.R), Kanal(a.G, b.G), Kanal(a.B, b.B)));
    }

    /// <summary>Štetec WPF z farby #RRGGBB (zmrazený).</summary>
    public static SolidColorBrush Stetec(string hex)
    {
        var stetec = new SolidColorBrush(ZHex(hex));
        stetec.Freeze();
        return stetec;
    }

    private sealed class Navrat(Action akcia) : IDisposable
    {
        public void Dispose() => akcia();
    }
}
