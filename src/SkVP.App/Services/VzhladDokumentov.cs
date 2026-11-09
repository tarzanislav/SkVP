using System.Globalization;
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

    /// <summary>Podfarbený pás cez celú šírku s pásikom vľavo.</summary>
    Pas,
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
}

/// <summary>Podklad hlavičky tabuľky (prvý riadok označený ako hlavička).</summary>
public enum HlavickaTabulky
{
    Siva,

    /// <summary>Svetlý odtieň farby šablóny alebo oblasti.</summary>
    Odtien,

    /// <summary>Plná pastelová farba s tučným textom.</summary>
    Plna,
}

/// <summary>
/// Šablóna vzhľadu dokumentov osnov – písma, veľkosti, riadkovanie, farby a ozdoby nadpisov, tabuľky. Platí rovnako
/// v editore programu, v PDF, vo Worde aj v tlačových zostavách. Veľkosti sú v bodoch (pt).
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
    bool CitatelnaFarba = true)
{
    private const string Atrament = "#1F2933";
    private const string Biela = "#FFFFFF";
    private const string Svetla = "#CBD2D9";

    private string? _pismoTextu;
    private string? _pismoNadpisov;

    /// <summary>Písmo textu – prvé nainštalované zo zoznamu.</summary>
    public string PismoTextu => _pismoTextu ??= VzhladDokumentov.Najdi(PismaTextu);

    /// <summary>Písmo nadpisov – prvé nainštalované zo zoznamu.</summary>
    public string PismoNadpisov => _pismoNadpisov ??= VzhladDokumentov.Najdi(PismaNadpisov);

    public double VelkostNadpisu(int uroven) => uroven switch
    {
        1 => Nadpis1,
        2 => Nadpis2,
        _ => Nadpis3,
    };

    /// <summary>Body → jednotky WPF (editor).</summary>
    public static double NaDip(double body) => body * 96 / 72;

    /// <summary>Základná farba šablóny pre oblasť (pastelová – na pásiky, čiary a podklady).</summary>
    public string Zakladna(string? oblast) => Farba switch
    {
        FarbaNadpisov.Akcent => Akcent ?? VzhladDokumentov.PredvolenaOblast,
        FarbaNadpisov.Tmava => "#5B6573",
        _ => VzhladDokumentov.Hex(Pastel.Zjemni(VzhladDokumentov.ZHex(oblast ?? VzhladDokumentov.PredvolenaOblast))),
    };

    /// <summary>Farba textu nadpisu danej úrovne (null = bežná farba textu).</summary>
    public string? FarbaNadpisu(int uroven, string? oblast)
    {
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

    /// <summary>Farba čiary alebo pásika pri nadpise 1.</summary>
    public string FarbaOzdoby(string? oblast) => Farba == FarbaNadpisov.Tmava ? Atrament : Zakladna(oblast);

    /// <summary>Podklad pásu nadpisu (<see cref="OzdobaNadpisu.Pas"/>).</summary>
    public string PodkladNadpisu(string? oblast) => VzhladDokumentov.Zmiesaj(Zakladna(oblast), Biela, 0.84);

    /// <summary>Podklad hlavičky tabuľky.</summary>
    public string PodkladHlavicky(string? oblast) => Hlavicka switch
    {
        HlavickaTabulky.Plna => Zakladna(oblast),
        HlavickaTabulky.Odtien => VzhladDokumentov.Zmiesaj(Zakladna(oblast), Biela, 0.7),
        _ => "#F1F3F5",
    };

    /// <summary>Podklad každého druhého riadku tabuľky (null = bez pruhov).</summary>
    public string? PodkladPruhu(string? oblast) =>
        Pruhy ? VzhladDokumentov.Zmiesaj(Farba == FarbaNadpisov.Tmava ? "#AEB6C2" : Zakladna(oblast), Biela, 0.9) : null;

    /// <summary>Farba čiar tabuľky.</summary>
    public string Linka(string? oblast) => Ram switch
    {
        RamTabulky.Mriezka => "#6B7480",
        RamTabulky.Vodorovny when Farba != FarbaNadpisov.Tmava => VzhladDokumentov.Zmiesaj(Zakladna(oblast), Svetla, 0.45),
        _ => Svetla,
    };

    /// <summary>Hrúbka čiar tabuľky (pt).</summary>
    public double HrubkaLinky => Ram == RamTabulky.Mriezka ? 0.75 : 0.5;

    /// <summary>Hrúbka čiary pod hlavičkou tabuľky pri vodorovných čiarach (pt).</summary>
    public double HrubkaPodHlavickou => Ram == RamTabulky.Vodorovny ? 1.5 : HrubkaLinky;
}

/// <summary>
/// Katalóg šablón vzhľadu a zvolená šablóna (uložená v nastaveniach). Zmena šablóny sa prejaví v celom programe –
/// otvorené dokumenty sa prekreslia (<see cref="Zmenena"/>), zostavy a súbory Word sa vytvoria v novom vzhľade.
/// </summary>
public static class VzhladDokumentov
{
    public const string Kluc = "dokumenty.sablona";
    public const string PredvolenaOblast = "#7B93DB";
    public const string IdPredvolenej = "pastelovy";

    private static SablonaVzhladu? _ulozena;

    [ThreadStatic]
    private static SablonaVzhladu? _docasna;

    /// <summary>Šablóny v poradí, v akom sa ponúkajú.</summary>
    public static IReadOnlyList<SablonaVzhladu> Vsetky { get; } = new SablonaVzhladu[]
    {
        new(IdPredvolenej, "Pastelový", "Pôvodný vzhľad programu – pätkové písmo textu, nadpisy vo farbe oblasti, jemné tabuľky.",
            new[] { "Constantia", "Georgia", "Cambria" }, new[] { "Segoe UI" },
            11, 14, 12, 11, 1.18, 6, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Ziadna, RamTabulky.Jemny, HlavickaTabulky.Siva, false,
            CitatelnaFarba: false),
        new("moderny", "Moderný", "Bezpätkové písmo, farebný pásik pri nadpisoch, vzdušné tabuľky s pruhmi vo farbe oblasti.",
            new[] { "Segoe UI" }, new[] { "Segoe UI" },
            10.5, 15, 12.5, 11, 1.3, 7, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Pasik, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, true,
            FarebnyNadpis2: true),
        new("farebny", "Farebný", "Nadpisy na farebnom páse, plné farebné hlavičky tabuliek – každá oblasť vo svojej farbe.",
            new[] { "Calibri", "Segoe UI" }, new[] { "Segoe UI" },
            11, 15, 12.5, 11, 1.25, 7, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Pas, RamTabulky.Jemny, HlavickaTabulky.Plna, true,
            FarebnyNadpis2: true),
        new("svieze", "Svieži", "Zaoblené písma, nadpisy s farebnou čiarou, tabuľky s jemným farebným odtieňom.",
            new[] { "Corbel", "Calibri" }, new[] { "Candara", "Segoe UI" },
            11.5, 16, 13, 11.5, 1.25, 7, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Ciara, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, false,
            FarebnyNadpis2: true),
        new("elegantny", "Elegantný", "Klasické knižné písma, tmavomodré nadpisy s tenkou čiarou, text do bloku.",
            new[] { "Georgia", "Cambria" }, new[] { "Palatino Linotype", "Georgia" },
            10.5, 15, 12.5, 11, 1.28, 6, true, FarbaNadpisov.Akcent, OzdobaNadpisu.Ciara, RamTabulky.Vodorovny, HlavickaTabulky.Odtien, false,
            Akcent: "#24406E", FarebnyNadpis2: true),
        new("klasicky", "Klasický", "Tradičný vzhľad školských dokumentov – Cambria, čierne nadpisy, mriežka, text do bloku.",
            new[] { "Cambria", "Times New Roman" }, new[] { "Cambria", "Times New Roman" },
            11.5, 14, 12, 11.5, 1.15, 6, true, FarbaNadpisov.Tmava, OzdobaNadpisu.Ciara, RamTabulky.Mriezka, HlavickaTabulky.Siva, false),
        new("uradny", "Úradný", "Strohý vzhľad na úradné účely – Arial, bez farieb a ozdôb, výrazná mriežka, text do bloku.",
            new[] { "Arial" }, new[] { "Arial" },
            11, 13, 11.5, 11, 1.15, 6, true, FarbaNadpisov.Tmava, OzdobaNadpisu.Ziadna, RamTabulky.Mriezka, HlavickaTabulky.Siva, false),
        new("usporny", "Úsporný", "Menšie písmo a riadkovanie – osnovy zaberú menej strán pri tlači.",
            new[] { "Calibri", "Segoe UI" }, new[] { "Calibri", "Segoe UI" },
            10, 12.5, 11, 10, 1.05, 4, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Ziadna, RamTabulky.Jemny, HlavickaTabulky.Siva, false),
        new("citatelny", "Čitateľný", "Väčšie písmo, voľnejšie riadky a výrazné nadpisy – dobre sa číta aj zo zhoršeným zrakom.",
            new[] { "Verdana", "Tahoma" }, new[] { "Verdana", "Tahoma" },
            11.5, 16, 13.5, 12, 1.42, 9, false, FarbaNadpisov.Oblast, OzdobaNadpisu.Pas, RamTabulky.Mriezka, HlavickaTabulky.Odtien, false,
            FarebnyNadpis2: true),
    };

    /// <summary>Zvolená šablóna (počas <see cref="Docasne"/> šablóna náhľadu).</summary>
    public static SablonaVzhladu Aktivna => _docasna ?? (_ulozena ??= Nacitaj());

    /// <summary>Šablóna sa zmenila – otvorené dokumenty sa majú prekresliť.</summary>
    public static event Action? Zmenena;

    public static SablonaVzhladu Podla(string? id) => Vsetky.FirstOrDefault(s => s.Id == id) ?? Vsetky[0];

    /// <summary>Uloží šablónu ako zvolenú pre všetky dokumenty.</summary>
    public static void Nastav(SablonaVzhladu sablona)
    {
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

    /// <summary>Prvé nainštalované písmo zo zoznamu (inak posledné – Windows ho nahradí podobným).</summary>
    public static string Najdi(IReadOnlyList<string> mena)
    {
        try
        {
            var systemove = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return mena.FirstOrDefault(systemove.Contains) ?? mena[^1];
        }
        catch (Exception)
        {
            return mena[^1];
        }
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
