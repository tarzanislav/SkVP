using System.IO;

namespace SkVP.Services;

/// <summary>Rozpoznaný jazykový predmet: názov bez úrovne, úroveň a ďalšie názvy, pod ktorými môže byť v programe.</summary>
/// <param name="Zaklad">názov predmetu bez úrovne, napr. „Ruský jazyk“, „Konverzácia v anglickom jazyku - profilácia anglický jazyk“</param>
/// <param name="Uroven">predvolená úroveň (B1, B2) alebo null (bez úrovne – SJL, SSJ)</param>
/// <param name="Aliasy">doterajšie názvy v programe, ktoré sa premenujú (napr. „Reálie v anglickom jazyku“)</param>
/// <param name="CudziJazyk">pri cudzom jazyku sa sprievodca pýta na úroveň</param>
/// <param name="ZaraditZa">nový predmet sa zaradí za posledný predmet oblasti, ktorého názov začína týmto textom
/// (napr. konverzácie pod seba)</param>
public sealed record JazykovyPredmet(string Zaklad, string? Uroven, IReadOnlyList<string> Aliasy, bool CudziJazyk, string? ZaraditZa = null);

/// <summary>
/// Pravidlá pre osnovy vzdelávacej oblasti Jazyk a komunikácia: skratky v názvoch súborov (SJL, SSJ, ANJ, NEJ, RUJ, KAJ,
/// KNJ, KRJ, Jazykom k maturite_RUJ …), profilácia konverzácie v anglickom jazyku podľa úvodnej tabuľky dokumentu
/// a úroveň cudzieho jazyka v názve predmetu („Ruský jazyk – úroveň B1“).
/// </summary>
public static class JazykovePravidla
{
    public const string Oblast = "Jazyk a komunikácia";

    public const string ProfilaciaAnj = "profilácia anglický jazyk";
    public const string ProfilaciaOstatne = "profilácia šport, informatika, steam";

    public static readonly string[] Urovne = { "B1", "B2" };

    /// <summary>Názov s úrovňou: „Ruský jazyk – úroveň B1“ (bez úrovne len základ).</summary>
    public static string NazovSUrovnou(string zaklad, string? uroven) =>
        uroven is { Length: > 0 } u ? $"{zaklad} – úroveň {u}" : zaklad;

    /// <summary>Názov bez úrovne: „Ruský jazyk – úroveň B1“ → „Ruský jazyk“.</summary>
    public static string BezUrovne(string nazov)
    {
        var i = nazov.LastIndexOf(" – úroveň ", StringComparison.Ordinal);
        return i > 0 ? nazov[..i] : nazov;
    }

    /// <summary>Úroveň z názvu predmetu („… – úroveň B2“ → B2).</summary>
    public static string? UrovenZNazvu(string nazov)
    {
        var i = nazov.LastIndexOf(" – úroveň ", StringComparison.Ordinal);
        return i > 0 ? nazov[(i + " – úroveň ".Length)..].Trim() : null;
    }

    /// <summary>
    /// Kam zaradiť nový predmet: za posledný predmet oblasti, ktorého názov začína textom <paramref name="zaradZa"/>
    /// (index v zozname predmetov oblasti), alebo null = na koniec.
    /// </summary>
    public static int? Pozicia(IReadOnlyList<string> nazvyPredmetov, string? zaradZa)
    {
        if (zaradZa is null)
        {
            return null;
        }

        var kluc = ImportOsnov.Kluc(zaradZa);
        for (var i = nazvyPredmetov.Count - 1; i >= 0; i--)
        {
            if (ImportOsnov.Kluc(nazvyPredmetov[i]).StartsWith(kluc, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }

        return null;
    }

    /// <summary>Je predmet cudzím jazykom (pýta sa úroveň)? Slovenčina nie.</summary>
    public static bool JeCudziJazyk(string nazov)
    {
        var k = ImportOsnov.Kluc(BezUrovne(nazov));
        if (k.Contains("sloven", StringComparison.Ordinal))
        {
            return false;
        }

        return new[] { "anglick", "nemeck", "rusk", "francuz", "spaniel", "talian", "latin", "cinsk", "japonsk", "polsk", "madarsk", "cudz" }
                   .Any(j => k.Contains(j, StringComparison.Ordinal))
               && (k.Contains("jazyk", StringComparison.Ordinal) || k.Contains("realie", StringComparison.Ordinal) || k.Contains("konverzac", StringComparison.Ordinal));
    }

    private static readonly (string Skratka, string Jazyk)[] JazykyMaturity =
    {
        ("ruj", "ruský jazyk"), ("nej", "nemecký jazyk"), ("anj", "anglický jazyk"),
    };

    /// <summary>
    /// Rozpozná jazykový predmet z názvu súboru (skratky) a pri konverzácii v anglickom jazyku aj profiláciu z úvodnej
    /// tabuľky dokumentu (druhý riadok). Vráti null, ak súbor nepatrí k pravidlám.
    /// </summary>
    public static JazykovyPredmet? Rozpoznaj(string nazovSuboru, string text, IReadOnlyList<Blok>? bloky)
    {
        var k = " " + ImportOsnov.Kluc(Path.GetFileNameWithoutExtension(nazovSuboru)) + " ";
        bool Ma(string slovo) => k.Contains($" {slovo} ", StringComparison.Ordinal);

        if (k.Contains("jazykom k maturite", StringComparison.Ordinal))
        {
            foreach (var (skratka, jazyk) in JazykyMaturity)
            {
                if (Ma(skratka) || k.Contains(ImportOsnov.Kluc(jazyk), StringComparison.Ordinal))
                {
                    return new JazykovyPredmet($"Jazykom k maturite - {jazyk}", "B2", new[] { "Jazykom k maturite" }, true, "Jazykom k maturite");
                }
            }

            return new JazykovyPredmet("Jazykom k maturite", null, Array.Empty<string>(), true);
        }

        if (k.Contains("realie", StringComparison.Ordinal))
        {
            return new JazykovyPredmet("Reálie anglicky hovoriacich krajín", "B2", new[] { "Reálie v anglickom jazyku" }, true);
        }

        if (Ma("kaj") || k.Contains("konverzacia v anglickom", StringComparison.Ordinal))
        {
            var profilacia = Profilacia(text, bloky);
            return new JazykovyPredmet($"Konverzácia v anglickom jazyku - {profilacia}", "B2", new[] { "Konverzácia v anglickom jazyku" }, true,
                "Konverzácia v anglickom jazyku");
        }

        if (Ma("knj") || k.Contains("konverzacia v nemeckom", StringComparison.Ordinal))
        {
            return new JazykovyPredmet("Konverzácia v nemeckom jazyku", "B1", new[] { "Konverzácia z nemeckého jazyka" }, true, "Konverzácia");
        }

        if (Ma("krj") || k.Contains("konverzacia v ruskom", StringComparison.Ordinal))
        {
            return new JazykovyPredmet("Konverzácia v ruskom jazyku", "B1", Array.Empty<string>(), true, "Konverzácia");
        }

        if (Ma("ruj") || Ma("rusky jazyk"))
        {
            return new JazykovyPredmet("Ruský jazyk", "B1", Array.Empty<string>(), true);
        }

        if (Ma("nej") || Ma("nemecky jazyk"))
        {
            return new JazykovyPredmet("Nemecký jazyk", "B1", Array.Empty<string>(), true);
        }

        if (Ma("anj") || Ma("anglicky jazyk"))
        {
            return new JazykovyPredmet("Anglický jazyk", "B2", Array.Empty<string>(), true);
        }

        if (Ma("ssj") || k.Contains("seminar zo slovenskeho", StringComparison.Ordinal))
        {
            return new JazykovyPredmet("Seminár zo slovenského jazyka a literatúry", null, Array.Empty<string>(), false);
        }

        if (Ma("sjl") || k.Contains("slovensky jazyk a literatura", StringComparison.Ordinal))
        {
            return new JazykovyPredmet("Slovenský jazyk a literatúra", null, Array.Empty<string>(), false);
        }

        return null;
    }

    /// <summary>
    /// Profilácia konverzácie v anglickom jazyku: druhý riadok úvodnej tabuľky dokumentu (inak text dokumentu).
    /// Anglický jazyk → „profilácia anglický jazyk“, inak „profilácia šport, informatika, steam“.
    /// </summary>
    public static string Profilacia(string text, IReadOnlyList<Blok>? bloky)
    {
        string? riadok = null;
        if (bloky?.OfType<Tabulka>().FirstOrDefault() is { Riadky.Count: >= 2 } tabulka)
        {
            // druhý riadok úvodnej tabuľky (prednostne riadok so slovom profilácia)
            var riadky = tabulka.Riadky.Select(r => string.Join(" ", r.Select(b => DokumentOsnovy.Text(b.Bloky)))).ToList();
            riadok = riadky.FirstOrDefault(r => ImportOsnov.Kluc(r).Contains("profil", StringComparison.Ordinal)) ?? riadky[1];
        }

        // z prečítaného PDF: riadok so slovom profilácia v úvode dokumentu
        riadok ??= text.Replace("\r", "").Split('\n').Take(40)
            .FirstOrDefault(r => ImportOsnov.Kluc(r).Contains("profilac", StringComparison.Ordinal)) ?? "";
        var k = ImportOsnov.Kluc(riadok).Replace("konverzacia v anglickom jazyku", "", StringComparison.Ordinal);
        if (k.Contains("sport", StringComparison.Ordinal) || k.Contains("informatik", StringComparison.Ordinal) || k.Contains("steam", StringComparison.Ordinal))
        {
            return ProfilaciaOstatne;
        }

        return k.Contains("anglick", StringComparison.Ordinal) ? ProfilaciaAnj : ProfilaciaOstatne;
    }
}
