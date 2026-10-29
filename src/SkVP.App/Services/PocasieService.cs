using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Mesto alebo obec, pre ktorú sa zobrazuje počasie.</summary>
public sealed record MiestoPocasia(string Nazov, string? Oblast, string? Okres, string? Krajina, string? KodKrajiny, double Sirka, double Dlzka)
{
    /// <summary>Napr. „Banská Bystrica, Okres Banská Bystrica, Banskobystrický kraj, Slovensko“.</summary>
    [JsonIgnore]
    public string Popis => string.Join(", ", new[] { Nazov, Okres, Oblast, Krajina }.Where(cast => !string.IsNullOrWhiteSpace(cast)).Distinct());
}

/// <summary>Predpoveď na jeden deň.</summary>
public sealed record PocasieDna(DateTime Datum, int Kod, double? Max, double? Min, int? Zrazky)
{
    [JsonIgnore] public string Symbol => PocasieService.Symbol(Kod, den: true);
    [JsonIgnore] public string Popis => PocasieService.Popis(Kod);

    [JsonIgnore]
    public string Den => (Datum.Date - DateTime.Today).Days switch
    {
        0 => "dnes",
        1 => "zajtra",
        _ => Preklad.VelkePrve(Datum.ToString("ddd", Planovanie.Sk)),
    };

    [JsonIgnore] public string MaxText => PocasieService.Stupne(Max, "°");
    [JsonIgnore] public string MinText => PocasieService.Stupne(Min, "°");
    [JsonIgnore] public string ZrazkyText => Zrazky is > 0 and var z ? $"{z} %" : "";
}

/// <summary>Aktuálne počasie a predpoveď na niekoľko dní (ukladá sa aj ako vyrovnávacia pamäť).</summary>
public sealed record Pocasie(
    MiestoPocasia Miesto, DateTime Stiahnute, double? Teplota, double? Pocitova, int? Vlhkost, double? Vietor, double? Zrazky, int Kod, bool Den,
    List<PocasieDna> Dni)
{
    [JsonIgnore] public string Symbol => PocasieService.Symbol(Kod, Den);
    [JsonIgnore] public string Popis => Preklad.VelkePrve(PocasieService.Popis(Kod));
    [JsonIgnore] public string TeplotaText => Teplota is null ? "–" : PocasieService.Stupne(Teplota, " °C");

    /// <summary>Podrobnosti v jednom riadku („pocitovo 8 °C · vlhkosť 70 % · vietor 12 km/h“).</summary>
    [JsonIgnore]
    public string Podrobnosti => string.Join("  ·  ", new[]
    {
        Pocitova is null ? "" : "pocitovo " + PocasieService.Stupne(Pocitova, " °C"),
        Vlhkost is { } vlhkost ? $"vlhkosť {vlhkost} %" : "",
        Vietor is { } vietor ? $"vietor {(int)Math.Round(vietor)} km/h" : "",
        Zrazky is > 0 and var zrazky ? $"zrážky {zrazky.ToString("0.0", Planovanie.Sk)} mm" : "",
    }.Where(cast => cast.Length > 0));

    [JsonIgnore] public string Aktualizovane => $"{PocasieService.Zdroj} · aktualizované o {Stiahnute.ToString("H:mm", Planovanie.Sk)}";
}

/// <summary>
/// Počasie na dashboarde zo služby Open-Meteo (bez registrácie a kľúča). Miesto sa vyhľadá podľa názvu mesta alebo obce,
/// predvolene podľa mesta v Údajoch o organizácii. Posledná predpoveď sa pamätá, takže dashboard ju ukáže aj bez siete.
/// </summary>
public sealed class PocasieService
{
    public const string KlucMiesto = "pocasie.miesto";
    public const string KlucData = "pocasie.data";
    public const string Zdroj = "Open-Meteo.com";

    private static readonly TimeSpan CasovyLimit = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PlatnostPredpovede = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan PrestavkaPoPokuse = TimeSpan.FromMinutes(10);

    /// <summary>Kódy počasia WMO: popis a symbol (znak písma Segoe UI Symbol / Emoji) cez deň.</summary>
    private static readonly Dictionary<int, (string Popis, string Symbol)> KodyWmo = new()
    {
        [0] = ("jasno", "☀"), [1] = ("prevažne jasno", "🌤"), [2] = ("polooblačno", "⛅"), [3] = ("zamračené", "☁"),
        [45] = ("hmla", "🌫"), [48] = ("hmla s námrazou", "🌫"),
        [51] = ("slabé mrholenie", "🌦"), [53] = ("mrholenie", "🌦"), [55] = ("husté mrholenie", "🌦"),
        [56] = ("mrznúce mrholenie", "🌦"), [57] = ("mrznúce mrholenie", "🌦"),
        [61] = ("slabý dážď", "🌧"), [63] = ("dážď", "🌧"), [65] = ("silný dážď", "🌧"), [66] = ("mrznúci dážď", "🌧"), [67] = ("mrznúci dážď", "🌧"),
        [71] = ("slabé sneženie", "❄"), [73] = ("sneženie", "❄"), [75] = ("silné sneženie", "❄"), [77] = ("snehové zrná", "❄"),
        [80] = ("slabé prehánky", "🌧"), [81] = ("prehánky", "🌧"), [82] = ("prívalové prehánky", "🌧"),
        [85] = ("snehové prehánky", "❄"), [86] = ("snehové prehánky", "❄"),
        [95] = ("búrka", "⛈"), [96] = ("búrka s krupobitím", "⛈"), [99] = ("búrka s krupobitím", "⛈"),
    };

    private static DateTime _poslednyPokus = DateTime.MinValue;

    public MiestoPocasia? NacitajMiesto() => Databaza.CitajJson<MiestoPocasia>(KlucMiesto);

    /// <summary>Uloží miesto (null = zrušiť) a zabudne predpoveď pre predošlé miesto.</summary>
    public void UlozMiesto(MiestoPocasia? miesto)
    {
        if (miesto is null)
        {
            Databaza.Zapis(KlucMiesto, null);
        }
        else
        {
            Databaza.ZapisJson(KlucMiesto, miesto);
        }

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

        var json = await StiahniAsync($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(nazov)}&count=20&language=sk&format=json",
            zrusenie).ConfigureAwait(false);
        return PrecitajMiesta(json);
    }

    /// <summary>Miesto podľa mesta v Údajoch o organizácii (na Slovensku prednostne slovenské) – kým si používateľ nevyberie iné.</summary>
    public async Task<MiestoPocasia?> MiestoOrganizacieAsync(CancellationToken zrusenie = default)
    {
        var udaje = UdajeOrganizacie.Nacitaj();
        var mesto = udaje.Mesto.Trim();
        if (mesto.Length < 2)
        {
            return null;
        }

        var miesta = await HladajAsync(mesto, zrusenie).ConfigureAwait(false);
        var naSlovensku = UdajeOrganizacie.JeSlovensko(udaje.Stat.Length == 0 ? UdajeOrganizacie.PredvolenyStat : udaje.Stat);
        return (naSlovensku ? miesta.Find(m => string.Equals(m.KodKrajiny, "SK", StringComparison.OrdinalIgnoreCase)) : null)
               ?? miesta.FirstOrDefault();
    }

    /// <summary>Predpoveď z vyrovnávacej pamäte, ak patrí k uloženému miestu.</summary>
    public Pocasie? ZVyrovnavacejPamate() =>
        Databaza.CitajJson<Pocasie>(KlucData) is { } pocasie && NacitajMiesto() is { } miesto && pocasie.Miesto == miesto ? pocasie : null;

    /// <summary>Treba stiahnuť nové údaje? Najviac raz za 30 minút, po neúspešnom pokuse najskôr o 10 minút.</summary>
    public bool TrebaObnovit(Pocasie? pocasie) =>
        DateTime.Now - _poslednyPokus > PrestavkaPoPokuse && (pocasie is null || DateTime.Now - pocasie.Stiahnute > PlatnostPredpovede);

    public async Task<Pocasie> StiahniAsync(MiestoPocasia miesto, CancellationToken zrusenie = default)
    {
        _poslednyPokus = DateTime.Now;
        var adresa = string.Create(CultureInfo.InvariantCulture, $"https://api.open-meteo.com/v1/forecast?latitude={miesto.Sirka:0.####}&longitude={miesto.Dlzka:0.####}")
                     + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,precipitation,is_day"
                     + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=5";
        var pocasie = PrecitajPredpoved(await StiahniAsync(adresa, zrusenie).ConfigureAwait(false), miesto, DateTime.Now);
        Databaza.ZapisJson(KlucData, pocasie);
        return pocasie;
    }

    /// <summary>Miesta z odpovede vyhľadávania Open-Meteo (miesta bez súradníc sa vynechajú).</summary>
    public static List<MiestoPocasia> PrecitajMiesta(string json) =>
        (JsonSerializer.Deserialize<OdpovedHladania>(json)?.Vysledky ?? new())
        .Where(m => !string.IsNullOrWhiteSpace(m.Nazov) && m.Sirka.HasValue && m.Dlzka.HasValue)
        .Select(m => new MiestoPocasia(m.Nazov!, m.Oblast, m.Okres, m.Krajina, m.KodKrajiny, m.Sirka!.Value, m.Dlzka!.Value))
        .ToList();

    /// <summary>Aktuálny stav a predpoveď po dňoch z odpovede Open-Meteo.</summary>
    public static Pocasie PrecitajPredpoved(string json, MiestoPocasia miesto, DateTime stiahnute)
    {
        var odpoved = JsonSerializer.Deserialize<OdpovedPredpovede>(json);
        if (odpoved?.Teraz is not { } teraz)
        {
            throw new SkvpException("Odpoveď služby počasia neobsahuje aktuálne údaje.");
        }

        var dni = new List<PocasieDna>();
        if (odpoved.Dni is { Datumy: { } datumy } denne)
        {
            for (var i = 0; i < datumy.Length; i++)
            {
                if (DateTime.TryParseExact(datumy[i], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum))
                {
                    dni.Add(new PocasieDna(datum, Cele(Prvok(denne.Kody, i)) ?? 0, Prvok(denne.Max, i), Prvok(denne.Min, i), Cele(Prvok(denne.Zrazky, i))));
                }
            }
        }

        return new Pocasie(miesto, stiahnute, teraz.Teplota, teraz.Pocitova, Cele(teraz.Vlhkost), teraz.Vietor, teraz.Zrazky,
            Cele(teraz.Kod) ?? 0, teraz.Den is not 0d, dni);
    }

    /// <summary>Popis počasia podľa kódu WMO.</summary>
    public static string Popis(int kod) => KodyWmo.TryGetValue(kod, out var wmo) ? wmo.Popis : "neznáme";

    /// <summary>Symbol počasia; v noci pri jasnej oblohe mesiac.</summary>
    public static string Symbol(int kod, bool den) =>
        !den && kod is 0 or 1 ? "☾" : KodyWmo.TryGetValue(kod, out var wmo) ? wmo.Symbol : "?";

    /// <summary>Teplota zaokrúhlená na celé stupne (bez „-0“); bez údaja prázdny text.</summary>
    public static string Stupne(double? teplota, string jednotka) => teplota is { } t ? $"{(int)Math.Round(t)}{jednotka}" : "";

    private static double? Prvok(double?[]? hodnoty, int index) => hodnoty is not null && index < hodnoty.Length ? hodnoty[index] : null;

    private static int? Cele(double? hodnota) => hodnota is { } h ? (int)Math.Round(h) : null;

    private static async Task<string> StiahniAsync(string adresa, CancellationToken zrusenie)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(zrusenie);
        limit.CancelAfter(CasovyLimit);
        try
        {
            return await Stahovanie.Klient.GetStringAsync(adresa, limit.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is OperationCanceledException && !zrusenie.IsCancellationRequested))
        {
            throw new SkvpException("Údaje o počasí sa nepodarilo stiahnuť – skontrolujte pripojenie k internetu.");
        }
    }

    // tvar odpovedí Open-Meteo (len použité údaje)

    private sealed record OdpovedHladania([property: JsonPropertyName("results")] List<NajdeneMiesto>? Vysledky);

    private sealed record NajdeneMiesto(
        [property: JsonPropertyName("name")] string? Nazov,
        [property: JsonPropertyName("latitude")] double? Sirka,
        [property: JsonPropertyName("longitude")] double? Dlzka,
        [property: JsonPropertyName("country_code")] string? KodKrajiny,
        [property: JsonPropertyName("country")] string? Krajina,
        [property: JsonPropertyName("admin1")] string? Oblast,
        [property: JsonPropertyName("admin2")] string? Okres);

    private sealed record OdpovedPredpovede(
        [property: JsonPropertyName("current")] UdajeTeraz? Teraz,
        [property: JsonPropertyName("daily")] UdajeDni? Dni);

    private sealed record UdajeTeraz(
        [property: JsonPropertyName("temperature_2m")] double? Teplota,
        [property: JsonPropertyName("apparent_temperature")] double? Pocitova,
        [property: JsonPropertyName("relative_humidity_2m")] double? Vlhkost,
        [property: JsonPropertyName("weather_code")] double? Kod,
        [property: JsonPropertyName("wind_speed_10m")] double? Vietor,
        [property: JsonPropertyName("precipitation")] double? Zrazky,
        [property: JsonPropertyName("is_day")] double? Den);

    private sealed record UdajeDni(
        [property: JsonPropertyName("time")] string[]? Datumy,
        [property: JsonPropertyName("weather_code")] double?[]? Kody,
        [property: JsonPropertyName("temperature_2m_max")] double?[]? Max,
        [property: JsonPropertyName("temperature_2m_min")] double?[]? Min,
        [property: JsonPropertyName("precipitation_probability_max")] double?[]? Zrazky);
}
