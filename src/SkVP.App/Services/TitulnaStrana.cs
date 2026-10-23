using System.Globalization;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// Texty úvodnej strany zostáv. Každý riadok sa dá prepísať aj vypnúť: škola, adresa, nadpis, podnadpis, druh školy
/// a stupeň vzdelania, platnosť, vlastné riadky, zoznam oblastí a text dole na strane („Vytvorené …“).
/// </summary>
public sealed class TitulnaStrana
{
    public const string PredvolenyPodnadpis = "Učebné osnovy";
    public const string PredvolenaPata = "Vytvorené {datum} v programe SkVP";

    /// <summary>Súčasný názov stupňa vzdelania gymnázia (školský zákon, ISCED 2011) – náhrada staršieho „ISCED 3A“.</summary>
    public const string AktualnyStupenGymnazia = "Gymnázium – úplné stredné všeobecné vzdelanie (ISCED 344)";

    private const string K = "titul.";

    public string Skola { get; set; } = "";
    public bool SkolaZobrazit { get; set; } = true;
    public string Adresa { get; set; } = "";
    public bool AdresaZobrazit { get; set; } = true;
    public string Nadpis { get; set; } = UdajeSkVP.PredvolenyNazov;
    public bool NadpisZobrazit { get; set; } = true;
    public string Podnadpis { get; set; } = PredvolenyPodnadpis;
    public bool PodnadpisZobrazit { get; set; } = true;
    public string Typ { get; set; } = "";
    public bool TypZobrazit { get; set; } = true;
    public string Platnost { get; set; } = "";
    public bool PlatnostZobrazit { get; set; } = true;

    /// <summary>Ďalšie ľubovoľné riadky (každý riadok textu je jeden riadok na strane).</summary>
    public string VlastneRiadky { get; set; } = "";

    public bool OblastiZobrazit { get; set; } = true;
    public string Pata { get; set; } = PredvolenaPata;
    public bool PataZobrazit { get; set; } = true;

    /// <summary>Text hlavičky strán dokumentu (nadpis – podnadpis).</summary>
    public string Hlavicka => string.Join(" – ", new[] { Nadpis, Podnadpis }.Where(t => t.Trim().Length > 0));

    public IEnumerable<string> RiadkyPodNadpisom()
    {
        if (TypZobrazit && Typ.Trim().Length > 0)
        {
            yield return Typ.Trim();
        }

        if (PlatnostZobrazit && Platnost.Trim().Length > 0)
        {
            yield return Platnost.Trim();
        }

        foreach (var riadok in VlastneRiadky.Replace("\r", "").Split('\n'))
        {
            if (riadok.Trim().Length > 0)
            {
                yield return riadok.Trim();
            }
        }
    }

    /// <summary>Text dole na strane s doplneným dátumom ({datum}) a rokom ({rok}).</summary>
    public string PataText(DateTime vytvorene) => Pata
        .Replace("{datum}", vytvorene.ToString("d. M. yyyy", CultureInfo.GetCultureInfo("sk-SK")), StringComparison.OrdinalIgnoreCase)
        .Replace("{rok}", vytvorene.Year.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

    public static TitulnaStrana Nacitaj()
    {
        var udaje = UdajeSkVP.Nacitaj();
        return new TitulnaStrana
        {
            Skola = udaje.Skola,
            Adresa = udaje.Adresa,
            Typ = udaje.Typ,
            SkolaZobrazit = Bool("skola"),
            AdresaZobrazit = Bool("adresa"),
            Nadpis = Databaza.Citaj(K + "nadpis") ?? udaje.NazovProgramu,
            NadpisZobrazit = Bool("nadpis"),
            Podnadpis = Databaza.Citaj(K + "podnadpis") ?? PredvolenyPodnadpis,
            PodnadpisZobrazit = Bool("podnadpis"),
            TypZobrazit = Bool("typ"),
            Platnost = Databaza.Citaj(K + "platnost") ?? (udaje.Obdobie.Length > 0 ? "Platnosť: " + udaje.Obdobie : ""),
            PlatnostZobrazit = Bool("platnost"),
            VlastneRiadky = Databaza.Citaj(K + "riadky") ?? "",
            OblastiZobrazit = Bool("oblasti"),
            Pata = Databaza.Citaj(K + "pata") ?? PredvolenaPata,
            PataZobrazit = Bool("pata"),
        };
    }

    public void Uloz()
    {
        // škola, adresa a druh školy sú spoločné s nastaveniami programu
        Databaza.Zapis(UdajeSkVP.KlucSkola, Skola.Trim());
        Databaza.Zapis(UdajeSkVP.KlucAdresa, Adresa.Trim());
        Databaza.Zapis(UcebnyPlanService.KlucTypSkoly, Typ.Trim());
        Databaza.Zapis(K + "nadpis", Nadpis.Trim());
        Databaza.Zapis(K + "podnadpis", Podnadpis.Trim());
        Databaza.Zapis(K + "platnost", Platnost.Trim());
        Databaza.Zapis(K + "riadky", VlastneRiadky.Trim());
        Databaza.Zapis(K + "pata", Pata.Trim());
        Zapis("skola", SkolaZobrazit);
        Zapis("adresa", AdresaZobrazit);
        Zapis("nadpis", NadpisZobrazit);
        Zapis("podnadpis", PodnadpisZobrazit);
        Zapis("typ", TypZobrazit);
        Zapis("platnost", PlatnostZobrazit);
        Zapis("oblasti", OblastiZobrazit);
        Zapis("pata", PataZobrazit);
    }

    /// <summary>Vráti texty úvodnej strany na predvolené (škola, adresa a druh školy zostanú).</summary>
    public static void Predvolene()
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "DELETE FROM nastavenie WHERE kluc LIKE 'titul.%'");
    }

    private static bool Bool(string nazov) => Databaza.Citaj(K + nazov + ".zobrazit") != "0";

    private static void Zapis(string nazov, bool hodnota) => Databaza.Zapis(K + nazov + ".zobrazit", hodnota ? "1" : "0");
}
