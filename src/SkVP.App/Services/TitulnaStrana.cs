using System.Globalization;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// Texty úvodnej strany zostáv. Každý riadok sa dá prepísať aj vypnúť: škola, adresa, nadpis, podnadpis, druh školy
/// a stupeň vzdelania, platnosť, vlastné riadky, zoznam oblastí a text dole na strane („Vytvorené …“).
/// Patrí sem aj logo školy na úvodnej strane a hlavička ďalších strán (vľavo text, vpravo názov školy a logo) –
/// každá časť hlavičky sa dá vypnúť zvlášť alebo celá hlavička naraz.
/// </summary>
public sealed class TitulnaStrana
{
    public const string PredvolenyPodnadpis = "Učebné osnovy";
    public const string PredvolenaPata = "Vytvorené {datum} v programe SkVP";

    /// <summary>Súčasný názov stupňa vzdelania gymnázia (školský zákon, ISCED 2011) – náhrada staršieho „ISCED 3A“.</summary>
    public const string AktualnyStupenGymnazia = "Gymnázium – úplné stredné všeobecné vzdelanie (ISCED 344)";

    private const string K = "titul.";

    /// <summary>Druh objektu, ktorého dokument je text dole na úvodnej strane (môže obsahovať aj tabuľku).</summary>
    public const string TypPaty = "titul_pata";

    /// <summary>Logo školy na úvodnej strane (samotný obrázok sa vyberá v Nastaveniach alebo v okne úvodnej strany).</summary>
    public bool LogoZobrazit { get; set; } = true;

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

    /// <summary>Text dole na strane – ľubovoľný obsah (odseky, zoznamy, tabuľka), {datum} a {rok} sa doplnia.</summary>
    public List<Blok> PataBloky { get; set; } = PredvolenaPataBloky();

    public bool PataZobrazit { get; set; } = true;

    /// <summary>Hlavička ďalších strán – vypína všetky jej časti naraz (nastavenie jednotlivých častí sa zachová).</summary>
    public bool HlavickaZobrazit { get; set; } = true;

    /// <summary>Text vľavo v hlavičke; prázdny = podľa nadpisu a podnadpisu („Školský vzdelávací program – Učebné osnovy“).</summary>
    public string TextHlavicky { get; set; } = "";
    public bool TextHlavickyZobrazit { get; set; } = true;

    /// <summary>Názov školy vpravo v hlavičke.</summary>
    public bool SkolaVHlavickeZobrazit { get; set; } = true;

    /// <summary>Logo školy vpravo v hlavičke (za názvom školy).</summary>
    public bool LogoVHlavickeZobrazit { get; set; } = true;

    public static List<Blok> PredvolenaPataBloky() => new() { new Odsek(new[] { new Usek(PredvolenaPata) }) };

    /// <summary>Názov dokumentu (nadpis – podnadpis), napr. „Školský vzdelávací program – Učebné osnovy“.</summary>
    public string Hlavicka => SpojNazov(Nadpis, Podnadpis);

    public static string SpojNazov(string nadpis, string podnadpis) =>
        string.Join(" – ", new[] { nadpis.Trim(), podnadpis.Trim() }.Where(t => t.Length > 0));

    /// <summary>Text vľavo v hlavičke, aký sa vytlačí (vlastný alebo podľa nadpisu a podnadpisu).</summary>
    public string TextHlavickyVysledny => TextHlavicky.Trim().Length > 0 ? TextHlavicky.Trim() : Hlavicka;

    /// <summary>Text vľavo v hlavičke, ak sa má vytlačiť.</summary>
    public string? TextVHlavicke => HlavickaZobrazit && TextHlavickyZobrazit && TextHlavickyVysledny.Length > 0 ? TextHlavickyVysledny : null;

    /// <summary>Názov školy vpravo v hlavičke, ak sa má vytlačiť.</summary>
    public string? SkolaVHlavicke => HlavickaZobrazit && SkolaVHlavickeZobrazit && Skola.Trim().Length > 0 ? Skola.Trim() : null;

    /// <summary>Cesta k logu vpravo v hlavičke, ak sa má vytlačiť (a obrázok sa dá načítať).</summary>
    public string? LogoVHlavicke(string? logo) =>
        HlavickaZobrazit && LogoVHlavickeZobrazit && NastaveniaSkoly.Rozmer(logo) is not null ? logo : null;

    /// <summary>Cesta k logu na úvodnej strane, ak sa má vytlačiť.</summary>
    public string? LogoNaUvodnejStrane(string? logo) => LogoZobrazit && NastaveniaSkoly.Rozmer(logo) is not null ? logo : null;

    /// <summary>Má hlavička ďalších strán aspoň jednu zapnutú časť?</summary>
    public bool MaHlavicku(string? logo) => TextVHlavicke is not null || SkolaVHlavicke is not null || LogoVHlavicke(logo) is not null;

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

    /// <summary>Doplní dátum ({datum}) a rok ({rok}).</summary>
    public static string Dopln(string text, DateTime vytvorene) => text
        .Replace("{datum}", vytvorene.ToString("d. M. yyyy", CultureInfo.GetCultureInfo("sk-SK")), StringComparison.OrdinalIgnoreCase)
        .Replace("{rok}", vytvorene.Year.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

    /// <summary>Text dole na strane s doplneným dátumom; prázdny zoznam, ak je vypnutý.</summary>
    public List<Blok> PataNaStranu(DateTime vytvorene) =>
        PataZobrazit ? PataBloky.Select(b => Dopln(b, vytvorene)).ToList() : new List<Blok>();

    /// <summary>Je text dole na strane len jedna obyčajná veta (vysádza sa malým sivým písmom)?</summary>
    public static bool JeJednaVeta(IReadOnlyList<Blok> bloky) =>
        bloky is [Odsek { Useky: var useky }] && useky.All(u => !u.Tucne && !u.Kurziva && !u.Podciarknute && !u.NovyRiadok);

    private static Blok Dopln(Blok blok, DateTime d) => blok switch
    {
        Nadpis n => n with { Text = Dopln(n.Text, d) },
        Odsek o => o with { Useky = o.Useky.Select(u => u with { Text = Dopln(u.Text, d) }).ToList() },
        Zoznam z => z with { Polozky = z.Polozky.Select(p => (IReadOnlyList<Blok>)p.Select(x => Dopln(x, d)).ToList()).ToList() },
        Tabulka t => t with
        {
            Riadky = t.Riadky.Select(r => (IReadOnlyList<Bunka>)r.Select(b => b with { Bloky = b.Bloky.Select(x => Dopln(x, d)).ToList() }).ToList()).ToList(),
        },
        _ => blok,
    };

    public static TitulnaStrana Nacitaj()
    {
        var udaje = UdajeSkVP.Nacitaj();
        return new TitulnaStrana
        {
            Skola = udaje.Skola,
            Adresa = udaje.Adresa,
            Typ = udaje.Typ,
            LogoZobrazit = Bool("logo"),
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
            PataBloky = NacitajPatu(),
            PataZobrazit = Bool("pata"),
            HlavickaZobrazit = Bool("hlavicka"),
            TextHlavicky = Databaza.Citaj(K + "hlavicka.text") ?? "",
            TextHlavickyZobrazit = Bool("hlavicka.text"),
            SkolaVHlavickeZobrazit = Bool("hlavicka.skola"),
            LogoVHlavickeZobrazit = Bool("hlavicka.logo"),
        };
    }

    private static List<Blok> NacitajPatu()
    {
        var id = IdPaty(vytvorit: false);
        if (id is null)
        {
            return PredvolenaPataBloky();
        }

        try
        {
            var dokumenty = new DokumentService();
            return dokumenty.Existuje(id.Value) ? dokumenty.Nacitaj(id.Value).Bloky : PredvolenaPataBloky();
        }
        catch (SkvpException)
        {
            return PredvolenaPataBloky();
        }
    }

    private static long? IdPaty(bool vytvorit)
    {
        using (var db = Databaza.Otvor())
        {
            if (Databaza.Skalar(db, "SELECT id FROM objekt WHERE typ = $t ORDER BY id LIMIT 1", ("$t", TypPaty)) is long id)
            {
                return id;
            }
        }

        return vytvorit ? new ObjektService().Pridaj(TypPaty, null, "Text dole na úvodnej strane").Id : null;
    }

    /// <summary>Text dole na úvodnej strane ako dokument Word (null, ak nie je vlastný).</summary>
    public static byte[]? DataPaty() => IdPaty(vytvorit: false) is long id ? new DokumentService().NacitajData(id) : null;

    /// <summary>Uloží text dole na úvodnej strane z dokumentu Word (import nastavení).</summary>
    public static void UlozDataPaty(byte[] data) => new DokumentService().Uloz(IdPaty(vytvorit: true)!.Value, data, "Pred importom nastavení");

    /// <summary>Uloží len text dole na úvodnej strane (bez ostatných nastavení).</summary>
    public static void UlozPatu(List<Blok> bloky, string dovod) =>
        new DokumentService().UlozBloky(IdPaty(vytvorit: true)!.Value, bloky, dovod);

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

        // text hlavičky rovnaký ako nadpis – podnadpis sa neukladá, aby sa menil spolu s nimi
        var textHlavicky = TextHlavicky.Trim();
        Databaza.Zapis(K + "hlavicka.text", textHlavicky.Length == 0 || textHlavicky == Hlavicka ? null : textHlavicky);
        new DokumentService().UlozBloky(IdPaty(vytvorit: true)!.Value, PataBloky, "Pred úpravou úvodnej strany");
        Zapis("skola", SkolaZobrazit);
        Zapis("adresa", AdresaZobrazit);
        Zapis("nadpis", NadpisZobrazit);
        Zapis("podnadpis", PodnadpisZobrazit);
        Zapis("typ", TypZobrazit);
        Zapis("platnost", PlatnostZobrazit);
        Zapis("oblasti", OblastiZobrazit);
        Zapis("pata", PataZobrazit);
        Zapis("logo", LogoZobrazit);
        Zapis("hlavicka", HlavickaZobrazit);
        Zapis("hlavicka.text", TextHlavickyZobrazit);
        Zapis("hlavicka.skola", SkolaVHlavickeZobrazit);
        Zapis("hlavicka.logo", LogoVHlavickeZobrazit);
    }

    /// <summary>Vráti texty úvodnej strany a hlavičky na predvolené (škola, adresa, druh školy a logo zostanú).</summary>
    public static void Predvolene()
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "DELETE FROM nastavenie WHERE kluc LIKE 'titul.%'; DELETE FROM objekt WHERE typ = $t;", ("$t", TypPaty));
    }

    private static bool Bool(string nazov) => Databaza.Citaj(K + nazov + ".zobrazit") != "0";

    private static void Zapis(string nazov, bool hodnota) => Databaza.Zapis(K + nazov + ".zobrazit", hodnota ? "1" : "0");
}
