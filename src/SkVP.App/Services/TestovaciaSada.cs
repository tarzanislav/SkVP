using System.Text.Json;
using Microsoft.Win32;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>
/// Testovacia sada učebných osnov: ukážkové osnovy všetkých predmetov učebného plánu gymnázia (podľa ŠVP a návrhy osnov
/// školských predmetov). Slúži na vyskúšanie programu; dá sa kedykoľvek vymazať a začať pracovať s reálnymi osnovami.
/// </summary>
public static class TestovaciaSada
{
    public const string KlucNainstalovana = "testovacia_sada";

    private const string DovodArchivacie = "Pred nahratím testovacej sady";

    public sealed class Sada
    {
        public string Popis { get; set; } = "";
        public List<OblastSady> Oblasti { get; set; } = new();
        public List<PredmetSady> Predmety { get; set; } = new();
    }

    public sealed class OblastSady
    {
        public string Nazov { get; set; } = "";
        public List<string> Charakteristika { get; set; } = new();
    }

    public sealed class PredmetSady
    {
        public string Nazov { get; set; } = "";
        public string Oblast { get; set; } = "";
        public string Zdroj { get; set; } = "";
        public List<string> Charakteristika { get; set; } = new();
        public List<string> Ciele { get; set; } = new();
        public List<RocnikSady> Rocniky { get; set; } = new();
        public List<string> Metody { get; set; } = new();
        public List<string> Hodnotenie { get; set; } = new();
        public List<string> Zdroje { get; set; } = new();
        public string? Stav { get; set; }
        public List<SuvislostSady> Suvisi { get; set; } = new();
        public string? Poznamka { get; set; }

        public bool ZoSvp => Zdroj == "ŠVP";
    }

    public sealed class RocnikSady
    {
        public string Rocnik { get; set; } = "";
        public List<CelokSady> Celky { get; set; } = new();
    }

    public sealed class CelokSady
    {
        public string Nazov { get; set; } = "";
        public List<string> Obsah { get; set; } = new();
        public List<string> Vykon { get; set; } = new();
    }

    public sealed class SuvislostSady
    {
        public string Predmet { get; set; } = "";
        public string Typ { get; set; } = TypVazby.Suvisi;
    }

    public sealed record Vysledok(int Osnovy, int Charakteristiky, int Suvislosti);

    /// <summary>Je v databáze nahratá testovacia sada (zobrazí sa upozornenie s možnosťou ju vymazať).</summary>
    public static bool JeNainstalovana => Databaza.Citaj(KlucNainstalovana) == "1";

    /// <summary>Inštalátor ponúkol testovaciu sadu a používateľ ju zaškrtol.</summary>
    public static bool ZvolenaPriInstalacii
    {
        get
        {
            try
            {
                using var kluc = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\tarzanislav\SkVP");
                return kluc?.GetValue("TestovaciaSada") is int hodnota && hodnota == 1;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static Sada Nacitaj()
    {
        using var prud = typeof(TestovaciaSada).Assembly.GetManifestResourceStream("SkVP.TestovaciaSada.json")
                         ?? throw new SkvpException("V programe chýba testovacia sada učebných osnov.");
        return JsonSerializer.Deserialize<Sada>(prud, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Sada();
    }

    /// <summary>
    /// Nahrá testovaciu sadu: doplní oblasti a predmety učebného plánu gymnázia (ak chýbajú) a do predmetov s rovnakým
    /// názvom uloží ukážkové osnovy. Doterajšie osnovy sa pritom zarchivujú.
    /// </summary>
    public static Vysledok Nainstaluj()
    {
        var sada = Nacitaj();
        UcebnyPlanService.Vytvor(UcebnyPlanService.Vstavany(), nahradit: false);

        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var oblasti = sluzba.Oblasti();
        var predmety = oblasti.SelectMany(o => sluzba.Deti(o.Id)).Where(p => p.Typ == TypObjektu.Predmet)
            .GroupBy(p => p.Nazov, StringComparer.CurrentCultureIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.CurrentCultureIgnoreCase);

        var charakteristiky = 0;
        foreach (var oblastSady in sada.Oblasti)
        {
            if (oblasti.FirstOrDefault(o => string.Equals(o.Nazov, oblastSady.Nazov, StringComparison.CurrentCultureIgnoreCase)) is { } oblast)
            {
                dokumenty.UlozBloky(oblast.Id, CharakteristikaOblasti(oblastSady), DovodArchivacie);
                charakteristiky++;
            }
        }

        var osnovy = 0;
        foreach (var predmetSady in sada.Predmety)
        {
            if (!predmety.TryGetValue(predmetSady.Nazov, out var predmet))
            {
                continue;
            }

            dokumenty.UlozBloky(predmet.Id, Osnova(predmetSady), DovodArchivacie);
            sluzba.NastavStav(predmet.Id, predmetSady.Stav);
            osnovy++;
        }

        var suvislosti = 0;
        foreach (var predmetSady in sada.Predmety)
        {
            if (!predmety.TryGetValue(predmetSady.Nazov, out var predmet))
            {
                continue;
            }

            foreach (var s in predmetSady.Suvisi)
            {
                if (predmety.TryGetValue(s.Predmet, out var ciel) && ciel.Id != predmet.Id)
                {
                    sluzba.Prepoj(predmet.Id, ciel.Id, s.Typ);
                    suvislosti++;
                }
            }
        }

        Databaza.Zapis(KlucNainstalovana, "1");
        return new Vysledok(osnovy, charakteristiky, suvislosti);
    }

    /// <summary>Vymaže všetky osnovy, ich archív a súvislosti; oblasti a predmety zostanú.</summary>
    public static void VymazOsnovy()
    {
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        Databaza.Vykonaj(db, """
            DELETE FROM verzia;
            DELETE FROM dokument;
            DELETE FROM vazba;
            UPDATE objekt SET stav = NULL;
            DELETE FROM nastavenie WHERE kluc = $k;
            """, ("$k", KlucNainstalovana));
        transakcia.Commit();
    }

    /// <summary>Vymaže všetko – oblasti, predmety, osnovy, archív aj súvislosti. Nastavenia (škola, logo, vzhľad) zostanú.</summary>
    public static void VymazVsetko()
    {
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        Databaza.Vykonaj(db, """
            DELETE FROM verzia;
            DELETE FROM dokument;
            DELETE FROM vazba;
            DELETE FROM objekt;
            DELETE FROM nastavenie WHERE kluc = $k;
            """, ("$k", KlucNainstalovana));
        transakcia.Commit();
        using var dbVacuum = Databaza.Otvor();
        Databaza.Vykonaj(dbVacuum, "VACUUM");
    }

    // ---- tvorba textu osnovy ----

    public static List<Blok> CharakteristikaOblasti(OblastSady oblast)
    {
        var bloky = oblast.Charakteristika.Select(t => (Blok)Odsek(t)).ToList();
        bloky.Add(Poznamka());
        return bloky;
    }

    public static List<Blok> Osnova(PredmetSady p)
    {
        var bloky = new List<Blok> { new Nadpis(1, "Charakteristika predmetu") };
        bloky.AddRange(p.Charakteristika.Select(t => (Blok)Odsek(t)));
        bloky.Add(new Odsek(new[]
        {
            new Usek(p.ZoSvp
                ? "Učebná osnova vychádza zo vzdelávacieho štandardu štátneho vzdelávacieho programu pre gymnáziá so štvorročným vzdelávacím programom."
                : "Predmet je zaradený do školského vzdelávacieho programu z disponibilných hodín školy; učebnú osnovu navrhla škola v nadväznosti na štátny vzdelávací program.",
                Kurziva: true),
        }));

        bloky.Add(new Nadpis(1, "Ciele predmetu"));
        bloky.Add(Odsek("Cieľom predmetu je viesť žiakov k tomu, aby vedeli:"));
        bloky.Add(Zoznam(p.Ciele));

        bloky.Add(new Nadpis(1, "Obsahový a výkonový štandard"));
        foreach (var rocnik in p.Rocniky)
        {
            bloky.Add(new Nadpis(2, rocnik.Rocnik));
            var riadky = new List<IReadOnlyList<Bunka>>
            {
                new[]
                {
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek("Tematický celok", Tucne: true) }) }, Hlavicka: true),
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek("Obsahový štandard", Tucne: true) }) }, Hlavicka: true),
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek("Výkonový štandard – žiak vie, dokáže", Tucne: true) }) }, Hlavicka: true),
                },
            };
            foreach (var celok in rocnik.Celky)
            {
                riadky.Add(new[]
                {
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek(celok.Nazov, Tucne: true) }) }),
                    new Bunka(new Blok[] { Zoznam(celok.Obsah) }),
                    new Bunka(new Blok[] { Zoznam(celok.Vykon) }),
                });
            }

            bloky.Add(new Tabulka(riadky, 3, new[] { 2.2, 3.9, 3.9 }));
        }

        if (p.Metody.Count > 0)
        {
            bloky.Add(new Nadpis(1, "Metódy a formy práce"));
            bloky.Add(Zoznam(p.Metody));
        }

        if (p.Hodnotenie.Count > 0)
        {
            bloky.Add(new Nadpis(1, "Hodnotenie predmetu"));
            bloky.AddRange(p.Hodnotenie.Select(t => (Blok)Odsek(t)));
        }

        if (p.Zdroje.Count > 0)
        {
            bloky.Add(new Nadpis(1, "Učebné zdroje"));
            bloky.Add(Zoznam(p.Zdroje));
        }

        bloky.Add(Poznamka());
        return bloky;
    }

    private static Odsek Odsek(string text) => new(new[] { new Usek(text) });

    private static Zoznam Zoznam(IEnumerable<string> polozky) =>
        new(false, polozky.Select(t => (IReadOnlyList<Blok>)new Blok[] { Odsek(t) }).ToList());

    private static Odsek Poznamka() => new(new[]
    {
        new Usek("Testovacia sada – ukážkový text na vyskúšanie programu SkVP, nie oficiálny dokument školy.", Kurziva: true),
    });
}
