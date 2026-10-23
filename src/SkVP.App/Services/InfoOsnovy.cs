using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Rok, od ktorého osnova platí (rok zmeny osnov), a odkiaľ bola naimportovaná.</summary>
public sealed record InfoOsnovy(int? Rok, DateTime? Importovane, string? Zdroj, string? Uroven = null)
{
    public static readonly InfoOsnovy Prazdne = new(null, null, null);

    /// <summary>Poznámka pri predmete, napr. „Osnovy zmenené v roku 2026 · importované 23. 10. 2026 zo súboru Fyzika.pdf“.</summary>
    public string Poznamka
    {
        get
        {
            var casti = new List<string>();
            if (Rok is int rok)
            {
                casti.Add($"Osnovy zmenené v roku {rok}");
            }

            if (Uroven is { Length: > 0 } uroven)
            {
                casti.Add($"úroveň {uroven}");
            }

            if (Importovane is DateTime kedy)
            {
                casti.Add($"importované {kedy.ToString("d. M. yyyy", CultureInfo.GetCultureInfo("sk-SK"))}" + (Zdroj is { Length: > 0 } z ? $" zo súboru {z}" : ""));
            }

            return string.Join("  •  ", casti);
        }
    }
}

/// <summary>
/// Údaje o osnove predmetu uložené v databáze pri objekte (stĺpec vlastnosti, JSON): rok zmeny osnov a import. Názov
/// dokumentu je „číslo názov rok“ (napr. „05 Fyzika 2026“), archivovaná verzia „05 Fyzika – archív 2026“.
/// </summary>
public static class InfoOsnovyService
{
    private const string KlucRok = "rokOsnovy";
    private const string KlucImport = "importovane";
    private const string KlucZdroj = "zdrojImportu";
    private const string KlucUroven = "uroven";

    public static InfoOsnovy Nacitaj(long objektId)
    {
        var json = Vlastnosti(objektId);
        return new InfoOsnovy(
            json[KlucRok]?.GetValue<int>(),
            json[KlucImport]?.GetValue<string>() is { } d && DateTime.TryParse(d, CultureInfo.InvariantCulture, DateTimeStyles.None, out var kedy) ? kedy : null,
            json[KlucZdroj]?.GetValue<string>(),
            json[KlucUroven]?.GetValue<string>());
    }

    public static void Uloz(long objektId, InfoOsnovy info)
    {
        var json = Vlastnosti(objektId);
        json[KlucRok] = info.Rok is int r ? JsonValue.Create(r) : null;
        json[KlucImport] = info.Importovane?.ToString("s", CultureInfo.InvariantCulture);
        json[KlucZdroj] = info.Zdroj;
        json[KlucUroven] = info.Uroven;
        foreach (var kluc in json.Where(p => p.Value is null).Select(p => p.Key).ToList())
        {
            json.Remove(kluc);
        }

        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "UPDATE objekt SET vlastnosti = $v WHERE id = $id",
            ("$v", json.Count == 0 ? null : json.ToJsonString()), ("$id", objektId));
    }

    private static JsonObject Vlastnosti(long objektId)
    {
        using var db = Databaza.Otvor();
        if (Databaza.Skalar(db, "SELECT vlastnosti FROM objekt WHERE id = $id", ("$id", objektId)) is string { Length: > 0 } text)
        {
            try
            {
                if (JsonNode.Parse(text) is JsonObject objekt)
                {
                    return objekt;
                }
            }
            catch (JsonException)
            {
                // poškodené vlastnosti – začne sa nanovo
            }
        }

        return new JsonObject();
    }

    /// <summary>Názov dokumentu osnovy: „05 Fyzika“ alebo s rokom zmeny „05 Fyzika 2026“.</summary>
    public static string NazovDokumentu(string cislo, string nazov, int? rok) =>
        $"{cislo} {nazov}" + (rok is int r ? $" {r}" : "");

    /// <summary>Názov archivovanej verzie: „05 Fyzika – archív 2026“.</summary>
    public static string NazovArchivu(string cislo, string nazov, int rokArchivacie) => $"{cislo} {nazov} – archív {rokArchivacie}";
}
