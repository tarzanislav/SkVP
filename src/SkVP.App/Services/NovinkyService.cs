using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Oficiálny zdroj informácie; <see cref="Overit"/> = texty, ktoré musí stránka obsahovať (overenie údaja).</summary>
public sealed class ZdrojNovinky
{
    [JsonPropertyName("nazov")] public string Nazov { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("overit")] public List<string>? Overit { get; set; }

    [JsonIgnore] public string Domena => Uri.TryCreate(Url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : "";
}

/// <summary>Jedna informácia sekcie Novinky – termín, novinka alebo odporúčanie.</summary>
public sealed class PolozkaNoviniek
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("kategoria")] public string Kategoria { get; set; } = "";
    [JsonPropertyName("druh")] public string Druh { get; set; } = NovinkyService.DruhNovinka;
    [JsonPropertyName("nazov")] public string Nazov { get; set; } = "";
    [JsonPropertyName("text")] public string Text { get; set; } = "";
    [JsonPropertyName("od")] public string? Od { get; set; }
    [JsonPropertyName("do")] public string? Do { get; set; }
    [JsonPropertyName("dolezite")] public bool Dolezite { get; set; }
    [JsonPropertyName("skupina")] public string? Skupina { get; set; }

    /// <summary>Pri prázdninách: posledný deň vyučovania pred prázdninami.</summary>
    [JsonPropertyName("posledny")] public string? Posledny { get; set; }

    /// <summary>Pri prázdninách: začiatok vyučovania po prázdninách.</summary>
    [JsonPropertyName("nastup")] public string? Nastup { get; set; }
    [JsonPropertyName("poznamka")] public string? Poznamka { get; set; }
    [JsonPropertyName("zdroje")] public List<ZdrojNovinky> Zdroje { get; set; } = new();
    [JsonPropertyName("overene")] public string? Overene { get; set; }

    /// <summary>Kedy sa informácia v programe objavila (pri prvom načítaní; inak dátum overenia).</summary>
    [JsonPropertyName("pridane")] public string? Pridane { get; set; }

    /// <summary>Informácia už nie je v aktuálnych údajoch – zostáva v histórii bloku (nič sa nemaže).</summary>
    [JsonIgnore] public bool Archivna { get; set; }

    [JsonIgnore] public DateOnly DatumPridania => NovinkyService.Datum(Pridane) ?? NovinkyService.Datum(Overene) ?? DateOnly.MinValue;

    [JsonIgnore] public DateOnly? DatumOd => NovinkyService.Datum(Od);
    [JsonIgnore] public DateOnly? DatumDo => NovinkyService.Datum(Do);

    /// <summary>Prvý deň (alebo jediný termín „do …“) – podľa neho sa termíny zoraďujú.</summary>
    [JsonIgnore] public DateOnly? Zaciatok => DatumOd ?? DatumDo;

    /// <summary>Posledný deň, po ktorom je termín minulý.</summary>
    [JsonIgnore] public DateOnly? Koniec => DatumDo ?? DatumOd;

    [JsonIgnore] public bool MaTermin => Zaciatok is not null;

    /// <summary>„9. 3. 2027“, „6. – 9. 4. 2027“, „do 13. 11. 2026“, „od 1. 9. 2026“ (pri novinke s účinnosťou).</summary>
    [JsonIgnore]
    public string TerminText
    {
        get
        {
            var od = DatumOd;
            var dokedy = DatumDo;
            if (od is null && dokedy is null)
            {
                return "";
            }

            if (od is null)
            {
                return "do " + NovinkyService.Datum(dokedy!.Value);
            }

            if (dokedy is null || dokedy == od)
            {
                return Druh == NovinkyService.DruhTermin ? NovinkyService.Datum(od.Value) : "od " + NovinkyService.Datum(od.Value);
            }

            var d1 = od.Value;
            var d2 = dokedy.Value;
            var prvy = d1.Year != d2.Year ? NovinkyService.Datum(d1) : d1.Month != d2.Month ? $"{d1.Day}. {d1.Month}." : $"{d1.Day}.";
            return $"{prvy} – {NovinkyService.Datum(d2)}";
        }
    }
}

public sealed class KategoriaNoviniek
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("nazov")] public string Nazov { get; set; } = "";
    [JsonPropertyName("popis")] public string Popis { get; set; } = "";
    [JsonPropertyName("farba")] public string Farba { get; set; } = "#3B5BDB";
    [JsonPropertyName("ikona")] public string Ikona { get; set; } = "";
}

public sealed class OdporucanaStranka
{
    [JsonPropertyName("nazov")] public string Nazov { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("popis")] public string Popis { get; set; } = "";
    [JsonPropertyName("rss")] public string? Rss { get; set; }
    [JsonPropertyName("kategorie")] public List<string> Kategorie { get; set; } = new();

    [JsonIgnore] public string Domena => Uri.TryCreate(Url, UriKind.Absolute, out var u) ? u.Host.Replace("www.", "") : Url;
}

/// <summary>Overené údaje sekcie Novinky (zabudované v programe, aktualizujú sa z repozitára programu).</summary>
public sealed class DataNoviniek
{
    [JsonPropertyName("verzia")] public int Verzia { get; set; } = 1;
    [JsonPropertyName("aktualizovane")] public string Aktualizovane { get; set; } = "";
    [JsonPropertyName("skolskyRok")] public string SkolskyRok { get; set; } = "";
    [JsonPropertyName("popis")] public string Popis { get; set; } = "";
    [JsonPropertyName("kategorie")] public List<KategoriaNoviniek> Kategorie { get; set; } = new();
    [JsonPropertyName("polozky")] public List<PolozkaNoviniek> Polozky { get; set; } = new();
    [JsonPropertyName("stranky")] public List<OdporucanaStranka> Stranky { get; set; } = new();

    [JsonIgnore] public DateOnly? DatumAktualizacie => NovinkyService.Datum(Aktualizovane);

    public KategoriaNoviniek? Kategoria(string id) => Kategorie.FirstOrDefault(k => k.Id == id);
}

/// <summary>Správa z RSS kanála oficiálnej stránky (ministerstvo, NIVaM).</summary>
public sealed record SpravaZoZdroja(string Titulok, string Odkaz, string Popis, DateTime? Datum, string Zdroj, string? Kategoria)
{
    /// <summary>Iné oficiálne zdroje, ktoré o tej istej téme píšu tiež (krížové potvrdenie).</summary>
    public List<string> PotvrdeneInde { get; init; } = new();
}

/// <summary>Správy zo zdrojov s časom stiahnutia (vyrovnávacia pamäť v databáze).</summary>
public sealed record SpravyZoZdrojov(DateTime Stiahnute, List<SpravaZoZdroja> Spravy, int Nerelevantnych, List<string> Chyby);

public enum StavOverenia
{
    /// <summary>Stránka obsahuje overovaný údaj.</summary>
    Potvrdene,

    /// <summary>Dokument je dostupný, obsah (PDF) sa automaticky neporovnáva.</summary>
    Dostupne,

    /// <summary>Stránka je dostupná, ale údaj na nej už nie je – treba skontrolovať, či sa nezmenil.</summary>
    Nenajdene,

    /// <summary>Stránku sa nepodarilo načítať.</summary>
    Nedostupne,
}

public sealed record VysledokOverenia(string Url, StavOverenia Stav, string Sprava);

/// <summary>Výsledok overenia všetkých zdrojov (uložený v databáze).</summary>
public sealed record OverenieNoviniek(DateTime Cas, Dictionary<string, List<VysledokOverenia>> Polozky);

/// <summary>
/// Sekcia Novinky: overené informácie o učebných osnovách, ŠVP, tematických plánoch, cieľových požiadavkách, maturite,
/// prijímacích skúškach a organizácii školského roka. Čerpá výlučne z oficiálnych stránok ministerstva školstva a jeho
/// organizácií (žiadne médiá ani noviny) – každý odkaz sa kontroluje proti zoznamu oficiálnych domén.
/// <list type="bullet">
/// <item>Overené údaje sú zabudované v programe a dajú sa aktualizovať z repozitára programu (bez novej verzie).</item>
/// <item>Aktuálne správy sa sťahujú z RSS kanálov ministerstva a NIVaM, filtrujú sa podľa tém a krížovo sa porovnávajú.</item>
/// <item>„Overiť na zdrojoch“ načíta zdrojové stránky a skontroluje, či na nich údaj stále je.</item>
/// </list>
/// </summary>
public static partial class NovinkyService
{
    public const string DruhTermin = "termin";
    public const string DruhNovinka = "novinka";
    public const string DruhOdporucanie = "odporucanie";

    public const string KlucData = "novinky.data";
    public const string KlucSpravy = "novinky.spravy";
    public const string KlucOverenie = "novinky.overenie";
    public const string KlucKraj = "novinky.kraj";
    public const string VsetkyKraje = "Všetky kraje";
    public const string KlucHistoria = "novinky.historia";

    /// <summary>Počet informácií na jednej strane bloku; staršie sú v histórii bloku (ďalšie strany).</summary>
    public const int NaStranu = 6;

    /// <summary>Téma Prázdniny a školský rok – zobrazuje sa na dashboarde (tabuľka prázdnin), nie v Novinkách.</summary>
    public const string TemaSkolskyRok = "skolskyrok";

    public const string AdresaAktualizacie =
        "https://raw.githubusercontent.com/tarzanislav/SkVP/main/src/SkVP.App/Zdroje/novinky.json";

    /// <summary>Oficiálne domény – iné odkazy program nepoužije (médiá a noviny sú vylúčené).</summary>
    public static readonly string[] OficialneDomeny =
    {
        "minedu.sk", "nivam.sk", "nucem.sk", "statpedu.sk", "iedu.sk", "slov-lex.sk", "slovensko.sk", "cvtisr.sk",
        "svsbb.sk", "svs.edu.sk", "vzdelavanie21.sk", "edu.sk",
    };

    public static readonly CultureInfo Kultura = CultureInfo.GetCultureInfo("sk-SK");

    private static readonly HttpClient Klient = VytvorKlienta();
    private static readonly JsonSerializerOptions Nastavenia = new() { WriteIndented = false };

    // ------------------------------------------------------------------ overené údaje

    /// <summary>Údaje zabudované v programe.</summary>
    public static DataNoviniek Zabudovane()
    {
        using var prud = typeof(NovinkyService).Assembly.GetManifestResourceStream("SkVP.Novinky.json")
                         ?? throw new InvalidOperationException("Chýbajú zabudované údaje sekcie Novinky.");
        return Precitaj(prud);
    }

    public static DataNoviniek Precitaj(Stream prud) =>
        JsonSerializer.Deserialize<DataNoviniek>(prud) ?? throw new SkvpException("Údaje Noviniek sú prázdne.");

    public static DataNoviniek Precitaj(string json) =>
        JsonSerializer.Deserialize<DataNoviniek>(json) ?? throw new SkvpException("Údaje Noviniek sú prázdne.");

    /// <summary>Aktuálne údaje: stiahnuté z repozitára, ak sú novšie ako zabudované, inak zabudované.</summary>
    public static DataNoviniek Nacitaj()
    {
        var zabudovane = Zabudovane();
        try
        {
            var json = Databaza.Citaj(KlucData);
            if (!string.IsNullOrEmpty(json))
            {
                var stiahnute = Precitaj(json);
                Skontroluj(stiahnute);
                if (stiahnute.DatumAktualizacie > zabudovane.DatumAktualizacie)
                {
                    return stiahnute;
                }
            }
        }
        catch (Exception)
        {
            // poškodené alebo neplatné stiahnuté údaje – použijú sa zabudované
        }

        return zabudovane;
    }

    /// <summary>
    /// Kontrola údajov (zabudovaných aj stiahnutých): jedinečné položky, známe kategórie, platné dátumy, aspoň jeden zdroj
    /// a všetky odkazy na oficiálnych stránkach. Vráti zoznam chýb (prázdny = v poriadku) alebo vyhodí výnimku.
    /// </summary>
    public static List<string> Skontroluj(DataNoviniek data, bool vyhodit = true)
    {
        var chyby = new List<string>();
        var kategorie = data.Kategorie.Select(k => k.Id).ToHashSet();
        if (data.Polozky.Count == 0)
        {
            chyby.Add("žiadne položky");
        }

        foreach (var duplicita in data.Polozky.GroupBy(p => p.Id).Where(g => g.Count() > 1))
        {
            chyby.Add($"položka {duplicita.Key} je viackrát");
        }

        foreach (var p in data.Polozky)
        {
            if (!kategorie.Contains(p.Kategoria))
            {
                chyby.Add($"{p.Id}: neznáma kategória {p.Kategoria}");
            }

            if (p.Druh is not (DruhTermin or DruhNovinka or DruhOdporucanie))
            {
                chyby.Add($"{p.Id}: neznámy druh {p.Druh}");
            }

            if ((p.Od is not null && p.DatumOd is null) || (p.Do is not null && p.DatumDo is null) || p.DatumOd > p.DatumDo)
            {
                chyby.Add($"{p.Id}: neplatný dátum");
            }

            if (p.Druh == DruhTermin && !p.MaTermin)
            {
                chyby.Add($"{p.Id}: termín bez dátumu");
            }

            if (p.Zdroje.Count == 0)
            {
                chyby.Add($"{p.Id}: bez zdroja");
            }

            chyby.AddRange(p.Zdroje.Where(z => !JeOficialna(z.Url)).Select(z => $"{p.Id}: neoficiálny zdroj {z.Url}"));
        }

        chyby.AddRange(data.Stranky.Where(s => !JeOficialna(s.Url) || (s.Rss is not null && !JeOficialna(s.Rss)))
            .Select(s => $"stránka {s.Nazov}: neoficiálna adresa"));
        if (chyby.Count > 0 && vyhodit)
        {
            throw new SkvpException("Údaje Noviniek nie sú platné: " + string.Join("; ", chyby.Take(5)));
        }

        return chyby;
    }

    /// <summary>Je adresa na oficiálnej stránke (https/http a doména zo zoznamu alebo jej subdoména)?</summary>
    public static bool JeOficialna(string? adresa)
    {
        if (!Uri.TryCreate(adresa, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return OficialneDomeny.Any(d => host == d || host.EndsWith("." + d, StringComparison.Ordinal));
    }

    /// <summary>Stiahne overené údaje z repozitára programu; vráti true, ak boli novšie a uložili sa.</summary>
    public static async Task<bool> AktualizujUdajeAsync(CancellationToken zrusenie = default)
    {
        string json;
        try
        {
            json = await Klient.GetStringAsync(AdresaAktualizacie, zrusenie).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SkvpException("Overené údaje sa nepodarilo stiahnuť – skontrolujte pripojenie k internetu.");
        }

        var nove = Precitaj(json);
        Skontroluj(nove);
        if (nove.DatumAktualizacie <= Nacitaj().DatumAktualizacie)
        {
            return false;
        }

        Databaza.Zapis(KlucData, json);
        return true;
    }

    // ------------------------------------------------------------------ história

    /// <summary>
    /// Aktuálne údaje spolu s históriou: informácie, ktoré v novších údajoch už nie sú, sa nezmažú – zostanú v histórii
    /// svojho bloku (<see cref="PolozkaNoviniek.Archivna"/>). Každá informácia si pamätá, kedy sa v programe objavila.
    /// </summary>
    public static List<PolozkaNoviniek> SHistoriou(DataNoviniek data, bool ulozit = true)
    {
        var historia = new List<PolozkaNoviniek>();
        try
        {
            var json = Databaza.Citaj(KlucHistoria);
            if (!string.IsNullOrEmpty(json))
            {
                historia = JsonSerializer.Deserialize<List<PolozkaNoviniek>>(json) ?? new();
            }
        }
        catch (Exception)
        {
            historia = new List<PolozkaNoviniek>();
        }

        var zHistorie = historia.Where(h => h.Id.Length > 0).GroupBy(h => h.Id).ToDictionary(g => g.Key, g => g.Last());
        var vysledok = new List<PolozkaNoviniek>();
        foreach (var p in data.Polozky)
        {
            p.Archivna = false;
            p.Pridane ??= zHistorie.TryGetValue(p.Id, out var h) && h.Pridane is not null ? h.Pridane : p.Overene ?? Iso(DateOnly.FromDateTime(DateTime.Today));
            vysledok.Add(p);
        }

        var aktualne = data.Polozky.Select(p => p.Id).ToHashSet();
        var kategorie = data.Kategorie.Select(k => k.Id).ToHashSet();
        foreach (var h in zHistorie.Values.Where(h => !aktualne.Contains(h.Id)))
        {
            // do histórie sa vráti len platná informácia z oficiálnych zdrojov
            if (kategorie.Contains(h.Kategoria) && h.Zdroje.Count > 0 && h.Zdroje.All(z => JeOficialna(z.Url)))
            {
                h.Archivna = true;
                vysledok.Add(h);
            }
        }

        if (ulozit)
        {
            try
            {
                Databaza.Zapis(KlucHistoria, JsonSerializer.Serialize(vysledok, Nastavenia));
            }
            catch (Exception)
            {
                // história sa uloží nabudúce
            }
        }

        return vysledok;
    }

    private static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ termíny

    public static DateOnly? Datum(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static string Datum(DateOnly datum) => $"{datum.Day}. {datum.Month}. {datum.Year}";

    /// <summary>Stav termínu voči dnešku: „dnes“, „zajtra“, „o 5 dní“, „prebieha“, „pred 3 dňami“…</summary>
    public static string Odpocet(PolozkaNoviniek polozka, DateOnly dnes)
    {
        if (polozka.Zaciatok is not { } zaciatok || polozka.Koniec is not { } koniec)
        {
            return "";
        }

        if (koniec < dnes)
        {
            return "uplynulo";
        }

        if (polozka.DatumOd is null)
        {
            // lehota „do …“
            var zostava = koniec.DayNumber - dnes.DayNumber;
            return zostava switch
            {
                0 => "dnes posledný deň",
                1 => "do zajtra",
                <= 4 => $"ešte {zostava} dni",
                _ => $"ešte {zostava} dní",
            };
        }

        if (zaciatok <= dnes)
        {
            return koniec == dnes ? (zaciatok == koniec ? "dnes" : "dnes posledný deň") : $"prebieha (do {koniec.Day}. {koniec.Month}.)";
        }

        var dni = zaciatok.DayNumber - dnes.DayNumber;
        return dni switch
        {
            1 => "zajtra",
            <= 4 => $"o {dni} dni",
            < 60 => $"o {dni} dní",
            < 75 => "o 2 mesiace",
            _ => (int)Math.Round(dni / 30.44) is var m && m <= 4 ? $"o {m} mesiace" : $"o {(int)Math.Round(dni / 30.44)} mesiacov",
        };
    }

    /// <summary>Nadchádzajúce a prebiehajúce termíny zoradené podľa dátumu.</summary>
    public static IReadOnlyList<PolozkaNoviniek> NajblizsieTerminy(DataNoviniek data, DateOnly dnes, int pocet, bool lenDolezite = false) =>
        data.Polozky.Where(p => p.Druh == DruhTermin && p.Koniec >= dnes && (!lenDolezite || p.Dolezite))
            .OrderBy(p => p.Zaciatok).ThenBy(p => p.Koniec).Take(pocet).ToList();

    // ------------------------------------------------------------------ RSS kanály oficiálnych stránok

    /// <summary>Témy aktuálnych správ podľa kľúčových slov v nadpise (bez diakritiky, malými písmenami).</summary>
    private static readonly (string Kategoria, string[] Slova)[] TemySprav =
    {
        ("maturita", new[] { "matur", "emat", "ec ms", "pfic" }),
        ("prijimacky", new[] { "prijimac", "prijimani", "testovanie 9", "testovania 9", "t9", "eprihlask", "prihlask" }),
        ("ciele", new[] { "cielove poziadavk", "cielovych poziadav", "vzdelavaci standard", "vzdelavacie standard", "vzdelavacich standard" }),
        ("osnovy", new[] { "vzdelavaci program", "vzdelavacieho programu", "vzdelavaciemu programu", "vzdelavacich programov", "svp", "skvp", "osnov",
                           "kurikul", "dodatok", "dodatku", "reforma", "reformy", "gramotnost", "pisa", "timss", "pirls" }),
        ("tematicke", new[] { "tematick", "metodick", "ucebnic", "didaktick" }),
        ("skolskyrok", new[] { "prazdnin", "pedagogicko-organizacn", "organizacia skolskeho roka", "organizacii skolskeho roka", "zaciatok skolskeho roka",
                               "koniec skolskeho roka", "polrok", "vysvedcen" }),
    };

    /// <summary>Kľúčové slová v popise – len jednoznačné témy (aby sa nezaradili správy, ktoré tému len spomínajú).</summary>
    private static readonly (string Kategoria, string[] Slova)[] TemyPopisu =
    {
        ("maturita", new[] { "maturitn", "maturita z", "maturitu z", "ematurit" }),
        ("prijimacky", new[] { "prijimacie skusk", "prijimacich skusok", "prijimacie konanie", "testovanie 9" }),
        ("ciele", new[] { "cielove poziadavk", "cielovych poziadav" }),
        ("osnovy", new[] { "statny vzdelavaci program", "statneho vzdelavacieho programu", "ucebne osnovy", "ucebnych osnov", "kurikularn" }),
        ("skolskyrok", new[] { "skolske prazdniny", "skolskych prazdnin", "pedagogicko-organizacne pokyny" }),
    };

    /// <summary>Témy, ktoré nie sú vzdelávacím obsahom (súťaže, šport, veda, vysoké školy, výstavba) – podľa nadpisu.</summary>
    private static readonly string[] MimoTemy =
    {
        "olympiad", "sutaz", "medail", "sport", "cern", "jadrov", "univerzit", "vysok", "vedc", "vyskum", "grant", "stavb", "rekonstrukc",
        "investic", "plan obnovy", "mladez", "dobrovoln",
    };

    /// <summary>Téma správy (kategória sekcie Novinky) alebo null, ak správa nesúvisí so vzdelávacím obsahom.</summary>
    public static string? TemaSpravy(string titulok, string popis)
    {
        static string Uprav(string t) => " " + Regex.Replace(ImportOsnov.BezDiakritiky(t).ToLowerInvariant(), @"\s+", " ") + " ";
        static bool Obsahuje(string text, string slovo) =>
            slovo.Length <= 4 ? Regex.IsMatch(text, $@"\b{Regex.Escape(slovo)}\b") : text.Contains(slovo, StringComparison.Ordinal);

        var nadpis = Uprav(titulok);
        var text = Uprav(popis);
        foreach (var (kategoria, slova) in TemySprav)
        {
            if (slova.Any(s => Obsahuje(nadpis, s)))
            {
                return kategoria;
            }
        }

        if (MimoTemy.Any(s => nadpis.Contains(s, StringComparison.Ordinal)))
        {
            return null;
        }

        foreach (var (kategoria, slova) in TemyPopisu)
        {
            if (slova.Any(s => Obsahuje(text, s)))
            {
                return kategoria;
            }
        }

        return null;
    }

    /// <summary>Prečíta kanál RSS 2.0 alebo Atom (iba odkazy na oficiálne stránky).</summary>
    public static List<SpravaZoZdroja> PrecitajRss(string xml, string zdroj)
    {
        var nastavenia = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var citac = XmlReader.Create(new StringReader(xml.TrimStart('﻿', ' ', '\r', '\n', '\t')), nastavenia);
        var dokument = XDocument.Load(citac);
        var vysledok = new List<SpravaZoZdroja>();
        foreach (var polozka in dokument.Descendants().Where(e => e.Name.LocalName is "item" or "entry"))
        {
            string? Prvok(string nazov) => polozka.Elements().FirstOrDefault(e => e.Name.LocalName == nazov)?.Value?.Trim();
            var titulok = Cisty(Prvok("title") ?? "");
            var odkaz = Prvok("link");
            if (string.IsNullOrEmpty(odkaz))
            {
                odkaz = polozka.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value;
            }

            if (titulok.Length == 0 || !JeOficialna(odkaz))
            {
                continue;
            }

            var popis = Cisty(Prvok("description") ?? Prvok("summary") ?? "");
            if (popis.Length > 320)
            {
                var koniec = popis.LastIndexOf(' ', 317);
                popis = popis[..(koniec > 100 ? koniec : 317)].TrimEnd(',', '.', ' ') + "…";
            }

            var datumText = Prvok("pubDate") ?? Prvok("published") ?? Prvok("updated") ?? Prvok("date");
            DateTime? datum = DateTimeOffset.TryParse(datumText, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var d)
                ? d.LocalDateTime : null;
            vysledok.Add(new SpravaZoZdroja(titulok, odkaz!, popis, datum, zdroj, TemaSpravy(titulok, popis)));
        }

        return vysledok;
    }

    /// <summary>
    /// Krížové potvrdenie: správe sa pridajú iné zdroje, ktoré v rovnakom období (±10 dní) píšu o tej istej téme
    /// (zhoda aspoň troch výrazných slov názvu alebo popisu).
    /// </summary>
    public static List<SpravaZoZdroja> Porovnaj(List<SpravaZoZdroja> spravy)
    {
        var slova = spravy.Select(s => VyrazneSlova(s.Titulok + " " + s.Popis)).ToList();
        var vysledok = new List<SpravaZoZdroja>();
        for (var i = 0; i < spravy.Count; i++)
        {
            var potvrdene = new SortedSet<string>(StringComparer.Ordinal);
            for (var j = 0; j < spravy.Count; j++)
            {
                if (spravy[j].Zdroj == spravy[i].Zdroj)
                {
                    continue;
                }

                var blizko = spravy[i].Datum is not { } a || spravy[j].Datum is not { } b || Math.Abs((a - b).TotalDays) <= 10;
                if (blizko && slova[i].Intersect(slova[j]).Count() >= 3)
                {
                    potvrdene.Add(spravy[j].Zdroj);
                }
            }

            vysledok.Add(spravy[i] with { PotvrdeneInde = potvrdene.ToList() });
        }

        return vysledok;
    }

    private static readonly HashSet<string> BezvyznamneSlova = new(StringComparer.Ordinal)
    {
        "ktore", "ktory", "ktora", "alebo", "preto", "tiez", "ktori", "budu", "bude", "mozu", "skoly", "skolach", "skolstva", "ministerstvo",
        "ministerstva", "slovenskej", "republiky", "ziakov", "ziaci", "rokov", "roka", "roku", "pocas", "medzi", "podla", "takze", "vsetky",
    };

    private static HashSet<string> VyrazneSlova(string text) =>
        Regex.Split(ImportOsnov.BezDiakritiky(text).ToLowerInvariant(), @"[^\p{L}\p{N}]+")
            .Where(s => s.Length >= 5 && !BezvyznamneSlova.Contains(s))
            .Select(s => s.Length > 7 ? s[..7] : s)
            .ToHashSet();

    /// <summary>Stiahne správy zo všetkých RSS kanálov odporúčaných stránok a uloží ich (zlyhanie jedného kanála nevadí).</summary>
    public static async Task<SpravyZoZdrojov> StiahniSpravyAsync(DataNoviniek data, CancellationToken zrusenie = default)
    {
        var vsetky = new List<SpravaZoZdroja>();
        var chyby = new List<string>();
        foreach (var stranka in data.Stranky.Where(s => JeOficialna(s.Rss)))
        {
            try
            {
                var xml = await Klient.GetStringAsync(stranka.Rss, zrusenie).ConfigureAwait(false);
                vsetky.AddRange(PrecitajRss(xml, stranka.Nazov));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or XmlException)
            {
                chyby.Add($"{stranka.Nazov}: {(ex is XmlException ? "kanál sa nedá prečítať" : "nedostupné")}");
            }
        }

        var relevantne = vsetky.Where(s => s.Kategoria is not null).ToList();
        var vysledok = new SpravyZoZdrojov(DateTime.Now,
            Porovnaj(relevantne).OrderByDescending(s => s.Datum ?? DateTime.MinValue).ToList(),
            vsetky.Count - relevantne.Count, chyby);
        if (vsetky.Count > 0)
        {
            Databaza.Zapis(KlucSpravy, JsonSerializer.Serialize(vysledok, Nastavenia));
        }

        return vysledok;
    }

    /// <summary>Naposledy stiahnuté správy (alebo null).</summary>
    public static SpravyZoZdrojov? UlozeneSpravy()
    {
        try
        {
            var json = Databaza.Citaj(KlucSpravy);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SpravyZoZdrojov>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Správy sa obnovujú najviac raz za 3 hodiny.</summary>
    public static bool TrebaObnovitSpravy(SpravyZoZdrojov? spravy) => spravy is null || DateTime.Now - spravy.Stiahnute > TimeSpan.FromHours(3);

    // ------------------------------------------------------------------ overenie na zdrojových stránkach

    /// <summary>
    /// Načíta zdrojové stránky všetkých položiek a skontroluje overované údaje (HTML a Word priamo, PDF len dostupnosť).
    /// Každá stránka sa sťahuje raz. Výsledok sa uloží do databázy.
    /// </summary>
    public static async Task<OverenieNoviniek> OverAsync(DataNoviniek data, IProgress<string>? priebeh = null, CancellationToken zrusenie = default)
    {
        var adresy = data.Polozky.SelectMany(p => p.Zdroje).Select(z => z.Url).Where(JeOficialna).Distinct().ToList();
        var texty = new Dictionary<string, (bool Ok, string? Text, string Sprava)>();
        var i = 0;
        foreach (var adresa in adresy)
        {
            priebeh?.Report($"Overujem {++i}/{adresy.Count}: {new Uri(adresa).Host}");
            texty[adresa] = await NacitajText(adresa, zrusenie).ConfigureAwait(false);
        }

        var vysledok = new Dictionary<string, List<VysledokOverenia>>();
        foreach (var polozka in data.Polozky)
        {
            vysledok[polozka.Id] = polozka.Zdroje.Select(z => Vyhodnot(z, texty.TryGetValue(z.Url, out var t) ? t : (false, null, "neoficiálny zdroj")))
                .ToList();
        }

        var overenie = new OverenieNoviniek(DateTime.Now, vysledok);
        Databaza.Zapis(KlucOverenie, JsonSerializer.Serialize(overenie, Nastavenia));
        return overenie;
    }

    public static OverenieNoviniek? UlozeneOverenie()
    {
        try
        {
            var json = Databaza.Citaj(KlucOverenie);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<OverenieNoviniek>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Vyhodnotenie jedného zdroja podľa načítaného textu stránky.</summary>
    public static VysledokOverenia Vyhodnot(ZdrojNovinky zdroj, (bool Ok, string? Text, string Sprava) stranka)
    {
        if (!stranka.Ok)
        {
            return new VysledokOverenia(zdroj.Url, StavOverenia.Nedostupne, stranka.Sprava);
        }

        if (zdroj.Overit is not { Count: > 0 } overit)
        {
            return new VysledokOverenia(zdroj.Url, StavOverenia.Dostupne, "zdroj je dostupný");
        }

        if (stranka.Text is null)
        {
            return new VysledokOverenia(zdroj.Url, StavOverenia.Dostupne, "dokument je dostupný (obsah PDF porovnajte ručne)");
        }

        var chybaju = overit.Where(t => !stranka.Text.Contains(Normalizuj(t), StringComparison.Ordinal)).ToList();
        return chybaju.Count == 0
            ? new VysledokOverenia(zdroj.Url, StavOverenia.Potvrdene, "údaj je na stránke uvedený")
            : new VysledokOverenia(zdroj.Url, StavOverenia.Nenajdene, "na stránke sa nenašlo: „" + string.Join("“, „", chybaju) + "“ – údaj sa mohol zmeniť");
    }

    /// <summary>Normalizovaný text na porovnanie: malé písmená, rovnaké pomlčky, bez medzier a zalomení.</summary>
    public static string Normalizuj(string text)
    {
        var t = WebUtility.HtmlDecode(text).ToLowerInvariant();
        t = Regex.Replace(t, "[‐-―−]", "-");
        return Regex.Replace(t, @"[\s ­​]+", "");
    }

    /// <summary>Text stránky z HTML (bez skriptov a značiek), normalizovaný.</summary>
    public static string TextZHtml(string html)
    {
        var t = Regex.Replace(html, @"(?is)<(script|style)\b.*?</\1>", " ");
        t = Regex.Replace(t, "<[^>]+>", " ");
        return Normalizuj(t);
    }

    private static async Task<(bool Ok, string? Text, string Sprava)> NacitajText(string adresa, CancellationToken zrusenie)
    {
        try
        {
            using var odpoved = await Klient.GetAsync(adresa, HttpCompletionOption.ResponseHeadersRead, zrusenie).ConfigureAwait(false);
            if (!odpoved.IsSuccessStatusCode)
            {
                return (false, null, $"stránka vrátila chybu {(int)odpoved.StatusCode}");
            }

            var cesta = odpoved.RequestMessage?.RequestUri?.AbsolutePath ?? adresa;
            var typ = odpoved.Content.Headers.ContentType?.MediaType ?? "";
            if (typ.Contains("pdf", StringComparison.OrdinalIgnoreCase) || cesta.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return (true, null, "PDF");
            }

            var bajty = await odpoved.Content.ReadAsByteArrayAsync(zrusenie).ConfigureAwait(false);
            if (cesta.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) || typ.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
            {
                using var prud = new MemoryStream(bajty);
                using var dokument = WordprocessingDocument.Open(prud, false);
                return (true, Normalizuj(dokument.MainDocumentPart?.Document?.InnerText ?? ""), "Word");
            }

            return (true, TextZHtml(ImportStruktury.Dekoduj(bajty, odpoved.Content.Headers.ContentType?.CharSet)), "HTML");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidDataException
                                       or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException)
        {
            return (false, null, "stránku sa nepodarilo načítať");
        }
    }

    // ------------------------------------------------------------------ pomocné

    private static string Cisty(string text)
    {
        var t = Regex.Replace(text, "<[^>]+>", " ");
        t = WebUtility.HtmlDecode(t);
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t.Replace("[…]", "…").Replace("[&#8230;]", "…").Trim();
    }

    private static HttpClient VytvorKlienta()
    {
        var klient = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(30) };
        klient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkVP", AktualizaciaService.AktualnaVerzia.ToString()));
        klient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("(+https://github.com/" + AktualizaciaService.Repozitar + ")"));
        return klient;
    }
}
