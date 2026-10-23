using System.Text.Json;
using System.Text.Json.Serialization;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Výrok slávnej osobnosti (databáza zo stránky citaty-slavnych.sk).</summary>
public sealed record Citat(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("autor")] string Autor,
    [property: JsonPropertyName("zivot")] string? Zivot,
    [property: JsonPropertyName("popis")] string? Popis,
    [property: JsonPropertyName("skryty")] bool Skryty)
{
    /// <summary>Text v úvodzovkách („…“).</summary>
    public string TextVUvodzovkach => $"„{Text.Trim().Trim('"', '„', '“')}“";

    /// <summary>„— Jan Amos Komenský (1592–1670), český pedagóg…“</summary>
    public string AutorText
    {
        get
        {
            var autor = "— " + Autor.Trim();
            if (!string.IsNullOrWhiteSpace(Zivot))
            {
                autor += $" ({Zivot.Trim()})";
            }

            return autor;
        }
    }

    public string PopisText => string.IsNullOrWhiteSpace(Popis) || Popis == Autor ? "" : Popis.Trim().TrimEnd('…').TrimEnd(',', ' ');

    public bool JeKratky => Text.Length <= CitatyService.MaxDlzkaNaDashboard;
}

/// <summary>Zabudovaná databáza výrokov a ich výber na dashboard.</summary>
public sealed class DatabazaCitatov
{
    [JsonPropertyName("zdroj")] public string Zdroj { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("stiahnute")] public string Stiahnute { get; set; } = "";
    [JsonPropertyName("vylucene")] public string Vylucene { get; set; } = "";
    [JsonPropertyName("skryte")] public string Skryte { get; set; } = "";
    [JsonPropertyName("citaty")] public List<Citat> Citaty { get; set; } = new();
}

/// <summary>
/// Výroky slávnych osobností na dashboarde. Čerpá sa výlučne zo stránky citaty-slavnych.sk (databáza je súčasťou programu);
/// výroky žijúcich politikov sú z databázy vynechané, výroky diktátorov sú predvolene skryté. Výrok dňa sa vyberá podľa dátumu,
/// používateľ si môže zobraziť ďalší alebo výrok skryť.
/// </summary>
public static class CitatyService
{
    public const int MaxDlzkaNaDashboard = 280;
    public const string KlucSkryte = "citaty.skryte";
    public const string KlucZobrazene = "citaty.zobrazene";

    private static readonly Lazy<DatabazaCitatov> Zabudovana = new(Nacitaj);

    public static DatabazaCitatov Zbierka => Zabudovana.Value;

    public static string ZdrojUrl => string.IsNullOrEmpty(Zbierka.Url) ? "https://citaty-slavnych.sk/citaty/" : Zbierka.Url;

    private static DatabazaCitatov Nacitaj()
    {
        using var prud = typeof(CitatyService).Assembly.GetManifestResourceStream("SkVP.Citaty.json")
                         ?? throw new InvalidOperationException("Chýba zabudovaná databáza citátov.");
        return JsonSerializer.Deserialize<DatabazaCitatov>(prud) ?? new DatabazaCitatov();
    }

    /// <summary>Výroky, ktoré si používateľ skryl (okrem predvolene skrytých).</summary>
    public static HashSet<int> SkryteVlastne()
    {
        var text = Databaza.Citaj(KlucSkryte, "");
        return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => int.TryParse(t, out var id) ? id : 0).Where(id => id > 0).ToHashSet();
    }

    /// <summary>Výroky, ktoré sa môžu zobraziť na dashboarde (krátke, nie skryté).</summary>
    public static IReadOnlyList<Citat> NaDashboard()
    {
        var skryte = SkryteVlastne();
        return Zbierka.Citaty.Where(c => !c.Skryty && c.JeKratky && !skryte.Contains(c.Id) && c.Text.Trim().Length > 0).ToList();
    }

    /// <summary>Výrok dňa – rovnaký celý deň, každý deň iný (poradie zamiešané pevným kľúčom).</summary>
    public static Citat? CitatDna(DateOnly datum)
    {
        var zoznam = NaDashboard();
        if (zoznam.Count == 0)
        {
            return null;
        }

        var index = (int)(((long)datum.DayNumber * 7919L) % zoznam.Count);
        return zoznam[index];
    }

    /// <summary>Náhodný výrok, iný ako práve zobrazený.</summary>
    public static Citat? Nahodny(int? okrem)
    {
        var zoznam = NaDashboard().Where(c => c.Id != okrem).ToList();
        return zoznam.Count == 0 ? null : zoznam[Random.Shared.Next(zoznam.Count)];
    }

    public static void Skry(int id)
    {
        var skryte = SkryteVlastne();
        skryte.Add(id);
        Databaza.Zapis(KlucSkryte, string.Join(",", skryte.Order()));
    }

    public static void ObnovSkryte() => Databaza.Zapis(KlucSkryte, null);

    public static int PocetSkrytychVlastnych => SkryteVlastne().Count;
}
