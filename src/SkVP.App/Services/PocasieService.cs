using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Mesto alebo obec, pre ktorú sa zobrazuje počasie.</summary>
public sealed record MiestoPocasia(string Nazov, string? Oblast, string? Okres, string? Krajina, string? KodKrajiny, double Sirka, double Dlzka)
{
    public string Popis => string.Join(", ", new[] { Nazov, Okres, Oblast, Krajina }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct());

    public string Suradnice => string.Create(CultureInfo.InvariantCulture, $"{Sirka:0.00}°, {Dlzka:0.00}°");
}

/// <summary>Predpoveď na jeden deň.</summary>
public sealed record PocasieDna(DateTime Datum, int Kod, double? Max, double? Min, int? Zrazky)
{
    public string Symbol => PocasieService.Symbol(Kod, true);
    public string Popis => PocasieService.Popis(Kod);
    public string Den => Datum.Date == DateTime.Today ? "dnes"
        : Datum.Date == DateTime.Today.AddDays(1) ? "zajtra"
        : PocasieService.VelkePrve(Datum.ToString("ddd", PocasieService.Kultura));
    public string MaxText => Max is { } m ? $"{Math.Round(m):0}°" : "";
    public string MinText => Min is { } m ? $"{Math.Round(m):0}°" : "";
    public string ZrazkyText => Zrazky is > 0 and var z ? $"{z} %" : "";
}

/// <summary>Aktuálne počasie a predpoveď na niekoľko dní.</summary>
public sealed record Pocasie(
    MiestoPocasia Miesto, DateTime Stiahnute, double? Teplota, double? Pocitova, int? Vlhkost, double? Vietor, double? Zrazky, int Kod, bool Den,
    List<PocasieDna> Dni)
{
    public string Symbol => PocasieService.Symbol(Kod, Den);
    public string Popis => PocasieService.VelkePrve(PocasieService.Popis(Kod));
    public string TeplotaText => Teplota is { } t ? $"{Math.Round(t):0} °C" : "–";
    public string PocitovaText => Pocitova is { } p ? $"pocitovo {Math.Round(p):0} °C" : "";
    public string VlhkostText => Vlhkost is { } v ? $"vlhkosť {v} %" : "";
    public string VietorText => Vietor is { } v ? $"vietor {Math.Round(v):0} km/h" : "";
    public string ZrazkyText => Zrazky is > 0 and var z ? $"zrážky {z.ToString("0.0", PocasieService.Kultura)} mm" : "";

    /// <summary>Podrobnosti v jednom riadku („pocitovo 8 °C · vlhkosť 70 % · vietor 12 km/h“).</summary>
    public string Podrobnosti => string.Join("  ·  ", new[] { PocitovaText, VlhkostText, VietorText, ZrazkyText }.Where(t => t.Length > 0));

    public string Aktualizovane => $"{PocasieService.Zdroj} · aktualizované o {Stiahnute.ToString("H:mm", PocasieService.Kultura)}";
}

/// <summary>
/// Počasie na dashboarde zo služby Open-Meteo (bez registrácie a kľúča; prevzaté z programu EviTA). Miesto sa vyhľadá
/// podľa názvu mesta alebo obce; predvolene podľa mesta v Údajoch o organizácii.
/// </summary>
public sealed class PocasieService
{
    public const string KlucMiesto = "pocasie.miesto";
    public const string KlucData = "pocasie.data";
    public const string Zdroj = "Open-Meteo.com";

    public static readonly CultureInfo Kultura = CultureInfo.GetCultureInfo("sk-SK");

    private static readonly HttpClient Klient = VytvorKlienta();
    private static DateTime _poslednyPokus = DateTime.MinValue;

    public MiestoPocasia? NacitajMiesto()
    {
        try
        {
            var json = Databaza.Citaj(KlucMiesto);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<MiestoPocasia>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Uloží miesto (null = zrušiť). Vyrovnávacia pamäť sa vymaže.</summary>
    public void UlozMiesto(MiestoPocasia? miesto)
    {
        Databaza.Zapis(KlucMiesto, miesto is null ? null : JsonSerializer.Serialize(miesto));
        Databaza.Zapis(KlucData, null);
        _poslednyPokus = DateTime.MinValue;
    }

    /// <summary>Vyhľadá mestá a obce podľa názvu (napr. „Pezinok“, „Nová Dubnica“).</summary>
    public async Task<List<MiestoPocasia>> HladajAsync(string nazov, CancellationToken zrusenie = default)
    {
        nazov = (nazov ?? "").Trim();
        if (nazov.Length < 2)
        {
            throw new SkvpException("Napíšte aspoň dve písmená názvu mesta alebo obce.");
        }

        var adresa = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(nazov)}&count=20&language=sk&format=json";
        var json = await Stiahni(adresa, zrusenie).ConfigureAwait(false);
        return PrecitajMiesta(json);
    }

    /// <summary>
    /// Miesto podľa mesta v Údajoch o organizácii (prvé nájdené na Slovensku, inak prvé vôbec) – použije sa,
    /// kým si používateľ nevyberie iné.
    /// </summary>
    public async Task<MiestoPocasia?> MiestoOrganizacieAsync(CancellationToken zrusenie = default)
    {
        var udaje = UdajeOrganizacie.Nacitaj();
        var mesto = udaje.Mesto.Trim();
        if (mesto.Length < 2)
        {
            return null;
        }

        var miesta = await HladajAsync(mesto, zrusenie).ConfigureAwait(false);
        var slovensko = UdajeOrganizacie.JeSlovensko(udaje.Stat.Length == 0 ? UdajeOrganizacie.PredvolenyStat : udaje.Stat);
        return miesta.FirstOrDefault(m => !slovensko || string.Equals(m.KodKrajiny, "SK", StringComparison.OrdinalIgnoreCase))
               ?? miesta.FirstOrDefault();
    }

    public static List<MiestoPocasia> PrecitajMiesta(string json)
    {
        using var dokument = JsonDocument.Parse(json);
        var vysledok = new List<MiestoPocasia>();
        if (!dokument.RootElement.TryGetProperty("results", out var vysledky) || vysledky.ValueKind != JsonValueKind.Array)
        {
            return vysledok;
        }

        foreach (var m in vysledky.EnumerateArray())
        {
            var nazov = Text(m, "name");
            if (string.IsNullOrWhiteSpace(nazov) || Cislo(m, "latitude") is not { } sirka || Cislo(m, "longitude") is not { } dlzka)
            {
                continue;
            }

            vysledok.Add(new MiestoPocasia(nazov, Text(m, "admin1"), Text(m, "admin2"), Text(m, "country"), Text(m, "country_code"), sirka, dlzka));
        }

        return vysledok;
    }

    public Pocasie? ZVyrovnavacejPamate()
    {
        try
        {
            var miesto = NacitajMiesto();
            var json = Databaza.Citaj(KlucData);
            var pocasie = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<Pocasie>(json);
            return pocasie is not null && miesto is not null && pocasie.Miesto == miesto ? pocasie : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Treba stiahnuť nové údaje? (najviac raz za 30 minút; po neúspechu o 10 minút)</summary>
    public bool TrebaObnovit(Pocasie? pocasie) =>
        DateTime.Now - _poslednyPokus > TimeSpan.FromMinutes(10)
        && (pocasie is null || DateTime.Now - pocasie.Stiahnute > TimeSpan.FromMinutes(30));

    public async Task<Pocasie> StiahniAsync(MiestoPocasia miesto, CancellationToken zrusenie = default)
    {
        _poslednyPokus = DateTime.Now;
        var adresa = string.Create(CultureInfo.InvariantCulture,
                $"https://api.open-meteo.com/v1/forecast?latitude={miesto.Sirka:0.####}&longitude={miesto.Dlzka:0.####}")
            + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,precipitation,is_day"
            + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=5";
        var json = await Stiahni(adresa, zrusenie).ConfigureAwait(false);
        var pocasie = PrecitajPredpoved(json, miesto, DateTime.Now);
        Databaza.Zapis(KlucData, JsonSerializer.Serialize(pocasie));
        return pocasie;
    }

    public static Pocasie PrecitajPredpoved(string json, MiestoPocasia miesto, DateTime stiahnute)
    {
        using var dokument = JsonDocument.Parse(json);
        var koren = dokument.RootElement;
        if (!koren.TryGetProperty("current", out var teraz))
        {
            throw new SkvpException("Odpoveď služby počasia neobsahuje aktuálne údaje.");
        }

        var dni = new List<PocasieDna>();
        if (koren.TryGetProperty("daily", out var denne) && denne.TryGetProperty("time", out var casy) && casy.ValueKind == JsonValueKind.Array)
        {
            var i = 0;
            foreach (var cas in casy.EnumerateArray())
            {
                if (DateTime.TryParseExact(cas.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum))
                {
                    dni.Add(new PocasieDna(datum, (int)(Prvok(denne, "weather_code", i) ?? 0), Prvok(denne, "temperature_2m_max", i),
                        Prvok(denne, "temperature_2m_min", i), Prvok(denne, "precipitation_probability_max", i) is { } z ? (int)Math.Round(z) : null));
                }

                i++;
            }
        }

        return new Pocasie(miesto, stiahnute,
            Cislo(teraz, "temperature_2m"), Cislo(teraz, "apparent_temperature"),
            Cislo(teraz, "relative_humidity_2m") is { } v ? (int)Math.Round(v) : null,
            Cislo(teraz, "wind_speed_10m"), Cislo(teraz, "precipitation"),
            (int)(Cislo(teraz, "weather_code") ?? 0), Cislo(teraz, "is_day") is not 0d,
            dni);
    }

    /// <summary>Popis počasia podľa kódu WMO.</summary>
    public static string Popis(int kod) => kod switch
    {
        0 => "jasno",
        1 => "prevažne jasno",
        2 => "polooblačno",
        3 => "zamračené",
        45 => "hmla",
        48 => "hmla s námrazou",
        51 => "slabé mrholenie",
        53 => "mrholenie",
        55 => "husté mrholenie",
        56 or 57 => "mrznúce mrholenie",
        61 => "slabý dážď",
        63 => "dážď",
        65 => "silný dážď",
        66 or 67 => "mrznúci dážď",
        71 => "slabé sneženie",
        73 => "sneženie",
        75 => "silné sneženie",
        77 => "snehové zrná",
        80 => "slabé prehánky",
        81 => "prehánky",
        82 => "prívalové prehánky",
        85 or 86 => "snehové prehánky",
        95 => "búrka",
        96 or 99 => "búrka s krupobitím",
        _ => "neznáme",
    };

    /// <summary>Symbol počasia (znak písma Segoe UI Symbol / Emoji).</summary>
    public static string Symbol(int kod, bool den) => kod switch
    {
        0 => den ? "☀" : "☾",
        1 => den ? "🌤" : "☾",
        2 => "⛅",
        3 => "☁",
        45 or 48 => "🌫",
        51 or 53 or 55 or 56 or 57 => "🌦",
        61 or 63 or 65 or 66 or 67 or 80 or 81 or 82 => "🌧",
        71 or 73 or 75 or 77 or 85 or 86 => "❄",
        95 or 96 or 99 => "⛈",
        _ => "?",
    };

    public static string VelkePrve(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], Kultura) + text[1..];

    private static async Task<string> Stiahni(string adresa, CancellationToken zrusenie)
    {
        try
        {
            return await Klient.GetStringAsync(adresa, zrusenie).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SkvpException("Údaje o počasí sa nepodarilo stiahnuť – skontrolujte pripojenie k internetu.");
        }
    }

    private static string? Text(JsonElement e, string nazov) =>
        e.TryGetProperty(nazov, out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;

    private static double? Cislo(JsonElement e, string nazov) =>
        e.TryGetProperty(nazov, out var h) && h.ValueKind == JsonValueKind.Number ? h.GetDouble() : null;

    private static double? Prvok(JsonElement e, string nazov, int index)
    {
        if (!e.TryGetProperty(nazov, out var pole) || pole.ValueKind != JsonValueKind.Array || index >= pole.GetArrayLength())
        {
            return null;
        }

        var h = pole[index];
        return h.ValueKind == JsonValueKind.Number ? h.GetDouble() : null;
    }

    private static HttpClient VytvorKlienta()
    {
        var klient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        klient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkVP", AktualizaciaService.AktualnaVerzia.ToString()));
        return klient;
    }
}
