using System.Text.Json;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Úloha, ktorú program navrhuje podľa stavu osnov a Noviniek (neukladá sa, kým ju používateľ neprevezme).</summary>
public sealed record NavrhUlohy(string Kluc, string Text, string Dovod, PrioritaUlohy Priorita, IReadOnlyList<long> TykaSa, string Farba)
{
    public bool Vysoka => Priorita == PrioritaUlohy.Vysoka;
}

/// <summary>
/// Navrhované úlohy z osnov: osnovy na kontrolu, nezačaté osnovy, osnovy bez platnosti v úvodnej tabuľke a dôležité
/// novinky k osnovám a ŠVP. Návrh zmizne, keď sa úloha prevezme (úloha má návrh ako svoj zdroj), keď ho používateľ
/// skryje, alebo keď sa stav osnov zmení (napr. osnova je hotová).
/// </summary>
public static class NavrhyUloh
{
    public const string KlucSkryte = "ulohy.navrhy.skryte";
    private const string KlucZapnute = "ulohy.navrhy";

    /// <summary>Navrhované úlohy sú zapnuté (Nastavenia → Kalendár a úlohy).</summary>
    public static bool Zapnute
    {
        get => Databaza.Citaj(KlucZapnute, "1") != "0";
        set => Databaza.Zapis(KlucZapnute, value ? "1" : "0");
    }

    /// <summary>Kategórie Noviniek, ktoré sa priamo týkajú osnov.</summary>
    private static readonly string[] KategorieOsnov = { "osnovy", "ciele", "tematicke" };

    /// <summary>Aktuálne návrhy – bez prevzatých a skrytých.</summary>
    public static List<NavrhUlohy> Zostav(DateOnly? dnes = null) => BezVybavenych(VsetkyNavrhy(dnes));

    /// <summary>Z návrhov vynechá tie, ktoré sú už prevzaté ako úloha alebo ich používateľ skryl.</summary>
    public static List<NavrhUlohy> BezVybavenych(IEnumerable<NavrhUlohy> navrhy)
    {
        var vylucene = new UlohyService().Zdroje();
        vylucene.UnionWith(Skryte());
        return navrhy.Where(n => !vylucene.Contains(n.Kluc)).ToList();
    }

    /// <summary>Všetky návrhy podľa stavu osnov a Noviniek (číta úvodné tabuľky osnov – volá sa raz pri otvorení).</summary>
    public static List<NavrhUlohy> VsetkyNavrhy(DateOnly? dnes = null)
    {
        var den = dnes ?? DateOnly.FromDateTime(DateTime.Today);
        var vysledok = new List<NavrhUlohy>();
        var sluzba = new ObjektService();
        var skolskyRok = ArchivSkolskehoRoka.Aktualny(den.ToDateTime(TimeOnly.MinValue));

        // dokumenty osnov: predmety vo vzdelávacích oblastiach a časti v zapnutých úvodných/záverečných častiach
        var skupiny = sluzba.Oblasti().Select(o => (Skupina: o, JeOblast: true))
            .Concat(new[] { TypObjektu.Uvod, TypObjektu.Zaver }.Where(CastiOsnov.JeAktivna)
                .SelectMany(t => sluzba.Skupiny(t)).Select(s => (Skupina: s, JeOblast: false)))
            .ToList();
        var nezacate = new List<(Objekt Predmet, Objekt Skupina)>();
        var bezPlatnosti = new List<(Objekt Predmet, Objekt Skupina)>();
        foreach (var (skupina, jeOblast) in skupiny)
        {
            foreach (var predmet in sluzba.Deti(skupina.Id).Where(p => p.Typ is TypObjektu.Predmet or TypObjektu.Cast))
            {
                var stav = StavyOsnovy.Urci(predmet.Stav, predmet.DlzkaTextu);
                if (stav == StavOsnovy.NaKontrolu)
                {
                    vysledok.Add(KontrolaOsnovy(predmet.Id, predmet.Nazov, skupina.Nazov, skupina.Farba));
                }
                else if (stav == StavOsnovy.Prazdna)
                {
                    nezacate.Add((predmet, skupina));
                }
                else if (jeOblast && UvodnaTabulka.Precitaj(predmet.Id).Platnost is null)
                {
                    bezPlatnosti.Add((predmet, skupina));
                }
            }
        }

        if (nezacate.Count > 0)
        {
            vysledok.Add(new NavrhUlohy($"nezacate:{skolskyRok}", nezacate.Count == 1 ? $"Začať osnovu: {nezacate[0].Predmet.Nazov}" : $"Začať nezačaté osnovy ({nezacate.Count})",
                "Osnovy zatiaľ nemajú žiadny text: " + Zoznam(nezacate.Select(n => n.Predmet.Nazov)), PrioritaUlohy.Normalna,
                nezacate.Select(n => n.Predmet.Id).ToList(), nezacate[0].Skupina.Farba ?? Planovanie.FarbaSiva));
        }

        if (bezPlatnosti.Count > 0)
        {
            vysledok.Add(new NavrhUlohy($"platnost:{skolskyRok}", $"Doplniť platnosť osnov v úvodnej tabuľke ({bezPlatnosti.Count})",
                "V úvodnej tabuľke chýba platnosť („od 1.9.2026“): " + Zoznam(bezPlatnosti.Select(n => n.Predmet.Nazov)), PrioritaUlohy.Nizka,
                bezPlatnosti.Select(n => n.Predmet.Id).ToList(), bezPlatnosti[0].Skupina.Farba ?? Planovanie.FarbaSiva));
        }

        // dôležité novinky k osnovám, ŠVP, tematickým plánom a cieľovým požiadavkám (neuplynulé)
        try
        {
            var data = NovinkyService.Nacitaj();
            foreach (var p in data.Polozky.Where(p => p.Dolezite && KategorieOsnov.Contains(p.Kategoria) && p.Druh != NovinkyService.DruhTermin
                                                      && !(p.DatumDo is { } koniec && koniec < den)))
            {
                vysledok.Add(new NavrhUlohy($"novinka:{p.Id}", $"Zapracovať do osnov: {p.Nazov}",
                    $"Dôležitá informácia v sekcii Novinky ({data.Kategoria(p.Kategoria)?.Nazov ?? p.Kategoria}).", PrioritaUlohy.Normalna,
                    Array.Empty<long>(), data.Kategoria(p.Kategoria)?.Farba ?? Planovanie.FarbaSiva));
            }
        }
        catch (Exception)
        {
            // bez údajov Noviniek sa navrhnú len úlohy z osnov
        }

        return vysledok;
    }

    /// <summary>Úloha skontrolovať osnovu v stave „Na kontrolu“ (návrh v Úlohách aj ponuka pri zmene stavu osnovy).</summary>
    public static NavrhUlohy KontrolaOsnovy(long predmetId, string predmet, string skupina, string? farba) =>
        new($"kontrola:{predmetId}", $"Skontrolovať osnovu: {predmet}", $"Osnova je v stave „Na kontrolu“ ({skupina}).", PrioritaUlohy.Vysoka,
            new[] { predmetId }, farba ?? Planovanie.FarbaSiva);

    private static string Zoznam(IEnumerable<string> nazvy)
    {
        var zoznam = nazvy.ToList();
        return string.Join(", ", zoznam.Take(8)) + (zoznam.Count > 8 ? $" a ďalšie ({zoznam.Count - 8})" : "") + ".";
    }

    /// <summary>Prevezme návrh ako úlohu (zdrojom je kľúč návrhu). Vráti Id úlohy.</summary>
    public static long Prevezmi(NavrhUlohy navrh) =>
        new UlohyService().Pridaj(navrh.Text, priorita: navrh.Priorita, tykaSa: navrh.TykaSa, popis: navrh.Dovod, zdroj: navrh.Kluc);

    public static HashSet<string> Skryte()
    {
        try
        {
            return Databaza.Citaj(KlucSkryte) is { Length: > 0 } json
                ? JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>()
                : new HashSet<string>();
        }
        catch (JsonException)
        {
            return new HashSet<string>();
        }
    }

    public static void Skry(string kluc, bool skryt = true)
    {
        var skryte = Skryte();
        if (skryt ? skryte.Add(kluc) : skryte.Remove(kluc))
        {
            Databaza.Zapis(KlucSkryte, JsonSerializer.Serialize(skryte));
        }
    }

    /// <summary>Znova ukáže všetky skryté návrhy.</summary>
    public static void ZobrazSkryte() => Databaza.Zapis(KlucSkryte, null);
}
