using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Odkiaľ je záznam v kalendári.</summary>
public enum ZdrojKalendara
{
    /// <summary>Vlastná udalosť (dá sa upraviť).</summary>
    Udalost,

    /// <summary>Úloha s termínom – tá istá úloha ako v sekcii Úlohy.</summary>
    Uloha,

    /// <summary>Termín maturity alebo prijímacích skúšok zo sekcie Novinky.</summary>
    Novinky,

    /// <summary>Organizácia školského roka (začiatok, polrok, koniec vyučovania).</summary>
    SkolskyRok,

    /// <summary>Školské prázdniny podľa kraja.</summary>
    Prazdniny,

    /// <summary>Začiatok platnosti osnovy (z úvodnej tabuľky).</summary>
    Osnova,

    /// <summary>Týždenná automatická záloha.</summary>
    Zaloha,

    /// <summary>Pripomienka archivácie osnov školského roka.</summary>
    Archiv,
}

/// <summary>Jeden výskyt v kalendári – udalosť (pri opakovanej každé opakovanie zvlášť), úloha alebo automatický termín.</summary>
public sealed record VyskytVKalendari
{
    public required string Kluc { get; init; }
    public required ZdrojKalendara Zdroj { get; init; }

    /// <summary>Id udalosti alebo úlohy; pri automatickom termíne null.</summary>
    public long? Id { get; init; }

    public required string Nazov { get; init; }
    public string? Popis { get; init; }
    public required DateTime Zaciatok { get; init; }
    public DateTime? Koniec { get; init; }
    public bool CelyDen { get; init; }
    public string Typ { get; init; } = "";
    public string Farba { get; init; } = Planovanie.FarbaSiva;
    public StavUdalosti Stav { get; init; }
    public OpakovanieUdalosti Opakovanie { get; init; }
    public PripomienkaUdalosti Pripomienka { get; init; }
    public PrioritaUlohy Priorita { get; init; } = PrioritaUlohy.Normalna;
    public IReadOnlyList<Naviazanie> TykaSa { get; init; } = Array.Empty<Naviazanie>();
    public string? Miesto { get; init; }

    /// <summary>Oficiálny zdroj automatického termínu (Novinky).</summary>
    public string? Odkaz { get; init; }

    public bool JeVlastny => Zdroj is ZdrojKalendara.Udalost or ZdrojKalendara.Uloha;
    public bool JeUloha => Zdroj == ZdrojKalendara.Uloha;
    public bool JeAutomaticky => !JeVlastny;
    public bool JeVybavena => Stav != StavUdalosti.Naplanovane;
    public bool JeZrusena => Stav == StavUdalosti.Zrusene;
    public bool MaOdkaz => Odkaz is { Length: > 0 };

    public DateTime PoslednyDen => (Koniec ?? Zaciatok).Date < Zaciatok.Date ? Zaciatok.Date : (Koniec ?? Zaciatok).Date;
    public bool ViacDni => PoslednyDen > Zaciatok.Date;

    /// <summary>„8:00 – 9:30“, „celý deň“, „29. 10. – 30. 10. 2026“.</summary>
    public string CasText => ViacDni
        ? $"{Zaciatok.Day}. {Zaciatok.Month}. – {Planovanie.DatumText(PoslednyDen)}"
        : CelyDen ? JeUloha ? "termín úlohy" : "celý deň"
        : Koniec is { } k && k > Zaciatok ? $"{Zaciatok:H:mm} – {k:H:mm}" : $"{Zaciatok:H:mm}";

    /// <summary>Text do bunky kalendára („8:00 Porada“, „✓ Skontrolovať osnovu“).</summary>
    public string KratkyText => (JeUloha ? (JeVybavena ? "✓ " : "☐ ") : "") + (CelyDen || ViacDni ? Nazov : $"{Zaciatok:H:mm} {Nazov}");

    public string TykaSaText => string.Join(" · ", TykaSa.Where(t => !t.Odstranene).Select(t => t.Text));

    public string Tip => string.Join("\n", new[] { Nazov, CasText, Typ, TykaSaText, Popis ?? "" }.Where(t => t.Length > 0));

    public string StavText => JeUloha
        ? JeVybavena ? "hotová" : "otvorená"
        : Stav switch { StavUdalosti.Hotovo => "prebehla", StavUdalosti.Zrusene => "zrušená", _ => "naplánovaná" };
}

/// <summary>Čo kalendár ukazuje okrem vlastných udalostí a úloh, a čo robí pri spustení programu.</summary>
public sealed record NastaveniaKalendara(bool Novinky, bool SkolskyRok, bool Osnovy, bool Zalohy, bool Archiv, bool PrehladPriSpusteni, bool Upozornenia)
{
    public const string Kluc = "kalendar.";

    public static NastaveniaKalendara Nacitaj()
    {
        bool Zapnute(string nazov, bool predvolene) => Databaza.Citaj(Kluc + nazov) is { } h ? h != "0" : predvolene;
        return new NastaveniaKalendara(Zapnute("novinky", true), Zapnute("skolskyrok", true), Zapnute("osnovy", true), Zapnute("zalohy", true),
            Zapnute("archiv", true), Zapnute("prehlad", false), Zapnute("upozornenia", true));
    }

    public void Uloz()
    {
        void Zapis(string nazov, bool hodnota) => Databaza.Zapis(Kluc + nazov, hodnota ? "1" : "0");
        Zapis("novinky", Novinky);
        Zapis("skolskyrok", SkolskyRok);
        Zapis("osnovy", Osnovy);
        Zapis("zalohy", Zalohy);
        Zapis("archiv", Archiv);
        Zapis("prehlad", PrehladPriSpusteni);
        Zapis("upozornenia", Upozornenia);
    }
}

/// <summary>Prehľad dňa: čo je dnes, na čo pripomenúť a ktoré úlohy sú po termíne.</summary>
public sealed record PrehladDna(List<VyskytVKalendari> Dnes, List<VyskytVKalendari> Pripomienky, List<UlohaRiadok> PoTermine)
{
    public bool JePrazdny => Dnes.Count == 0 && Pripomienky.Count == 0 && PoTermine.Count == 0;
}

/// <summary>
/// Kalendár SkVP: vlastné udalosti (typ, opakovanie, pripomienka, „Týka sa“), úlohy s termínom a automatické termíny
/// školského roka – prázdniny podľa kraja, organizácia školského roka, termíny maturity a prijímacích skúšok zo sekcie
/// Novinky, začiatok platnosti osnov, týždenná záloha a pripomienka archivácie. Automatické termíny sa neukladajú,
/// počítajú sa pri zobrazení, takže sú vždy podľa aktuálnych údajov.
/// </summary>
public sealed partial class KalendarService
{
    /// <summary>Kategórie Noviniek s termínmi do kalendára.</summary>
    private static readonly string[] KategorieTerminov = { "maturita", "prijimacky" };

    private DataNoviniek? _novinky;
    private List<(long Id, string Nazov, string Farba, DateTime Od)>? _platnosti;

    public NastaveniaKalendara Nastavenia { get; set; } = NastaveniaKalendara.Nacitaj();

    /// <summary>Kraj pre jarné prázdniny (spoločný s dashboardom).</summary>
    public string Kraj { get; set; } = Databaza.Citaj(NovinkyService.KlucKraj, NovinkyService.VsetkyKraje);

    // ------------------------------------------------------------------ typy udalostí

    public List<TypUdalosti> Typy(bool lenAktivne = false)
    {
        var vysledok = new List<TypUdalosti>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db, "SELECT id, nazov, farba, aktivny, poradie FROM typ_udalosti" +
                                               (lenAktivne ? " WHERE aktivny = 1" : "") + " ORDER BY poradie, id");
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            vysledok.Add(new TypUdalosti
            {
                Id = citac.GetInt64(0), Nazov = citac.GetString(1), Farba = citac.GetString(2), Aktivny = citac.GetInt64(3) == 1, Poradie = citac.GetInt32(4),
            });
        }

        return vysledok;
    }

    public int PocetPouziti(long typId)
    {
        using var db = Databaza.Otvor();
        return Convert.ToInt32(Databaza.Skalar(db, "SELECT count(*) FROM udalost u JOIN objekt o ON o.id = u.objekt_id WHERE u.typ_id = $t AND o.odstranene IS NULL",
            ("$t", typId)) ?? 0L, CultureInfo.InvariantCulture);
    }

    /// <summary>Pridá (Id = 0) alebo upraví typ udalosti. Vráti jeho Id.</summary>
    public long UlozTyp(long id, string nazov, string farba)
    {
        nazov = (nazov ?? "").Trim();
        if (nazov.Length == 0)
        {
            throw new SkvpException("Zadajte názov typu udalosti.");
        }

        if (Typy().Any(t => t.Id != id && t.Aktivny && string.Equals(t.Nazov, nazov, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new SkvpException($"Typ „{nazov}“ už existuje.");
        }

        farba = PoznamkyService.JePlatnaFarba(farba) ? farba : Planovanie.FarbaSiva;
        using var db = Databaza.Otvor();
        if (id == 0)
        {
            return Convert.ToInt64(Databaza.Skalar(db, """
                INSERT INTO typ_udalosti(nazov, farba, aktivny, poradie) VALUES($n, $f, 1, (SELECT coalesce(max(poradie), 0) + 1 FROM typ_udalosti));
                SELECT last_insert_rowid();
                """, ("$n", nazov), ("$f", farba)), CultureInfo.InvariantCulture);
        }

        Databaza.Vykonaj(db, "UPDATE typ_udalosti SET nazov = $n, farba = $f, aktivny = 1 WHERE id = $id", ("$n", nazov), ("$f", farba), ("$id", id));
        return id;
    }

    /// <summary>Odstráni typ. Ak ho používa niektorá udalosť, len ho skryje z ponuky. Vráti true, ak bol vymazaný.</summary>
    public bool OdstranTyp(long id)
    {
        using var db = Databaza.Otvor();
        if (PocetPouziti(id) > 0)
        {
            Databaza.Vykonaj(db, "UPDATE typ_udalosti SET aktivny = 0 WHERE id = $id", ("$id", id));
            return false;
        }

        if (Typy(lenAktivne: true).Count(t => t.Id != id) == 0)
        {
            throw new SkvpException("Musí zostať aspoň jeden typ udalosti.");
        }

        Databaza.Vykonaj(db, "DELETE FROM typ_udalosti WHERE id = $id", ("$id", id));
        return true;
    }

    // ------------------------------------------------------------------ udalosti

    private const string SqlUdalosti = """
        SELECT o.id, u.guid, o.nazov, u.popis, u.typ_id, u.zaciatok, u.koniec, u.cely_den, u.opakovanie, u.opakovanie_do,
               u.pripomienka, u.stav, u.miesto
        FROM udalost u JOIN objekt o ON o.id = u.objekt_id
        WHERE o.odstranene IS NULL
        """;

    private static Udalost CitajUdalost(SqliteDataReader c) => new()
    {
        Id = c.GetInt64(0),
        Guid = Guid.TryParse(c.GetString(1), out var g) ? g : Guid.NewGuid(),
        Nazov = c.GetString(2),
        Popis = c.IsDBNull(3) ? null : c.GetString(3),
        TypId = c.IsDBNull(4) ? null : c.GetInt64(4),
        Zaciatok = Planovanie.Datum(c, 5) ?? DateTime.Today,
        Koniec = Planovanie.Datum(c, 6),
        CelyDen = c.GetInt64(7) == 1,
        Opakovanie = (OpakovanieUdalosti)c.GetInt32(8),
        OpakovanieDo = Planovanie.Datum(c, 9),
        Pripomienka = (PripomienkaUdalosti)c.GetInt32(10),
        Stav = (StavUdalosti)c.GetInt32(11),
        Miesto = c.IsDBNull(12) ? null : c.GetString(12),
    };

    public Udalost? Nacitaj(long id)
    {
        using var db = Databaza.Otvor();
        Udalost? udalost;
        using (var prikaz = Databaza.Prikaz(db, SqlUdalosti + " AND o.id = $id", ("$id", id)))
        using (var citac = prikaz.ExecuteReader())
        {
            udalost = citac.Read() ? CitajUdalost(citac) : null;
        }

        udalost?.TykaSa.AddRange(Planovanie.Naviazania(db, id).Select(n => n.Id));
        return udalost;
    }

    /// <summary>Udalosti na výber (prepojenie poznámky) – najbližšie budúce a nedávne, najviac rok dozadu aj dopredu.</summary>
    public List<Udalost> UdalostiNaVyber()
    {
        var dnes = DateTime.Today;
        using var db = Databaza.Otvor();
        return Udalosti(db, dnes.AddYears(1))
            .Where(u => u.Zaciatok >= dnes.AddYears(-1) || u.Opakovanie != OpakovanieUdalosti.Ziadne)
            .OrderBy(u => Math.Abs((u.Zaciatok.Date - dnes).TotalDays))
            .Take(200)
            .OrderBy(u => u.Zaciatok)
            .ToList();
    }

    private static List<Udalost> Udalosti(SqliteConnection db, DateTime po1)
    {
        var vysledok = new List<Udalost>();
        using var prikaz = Databaza.Prikaz(db, SqlUdalosti + " AND u.zaciatok < $po", ("$po", Planovanie.Iso(po1, sCasom: true)));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            vysledok.Add(CitajUdalost(citac));
        }

        return vysledok;
    }

    /// <summary>Uloží novú (Id = 0) alebo upravenú udalosť. Vráti jej Id.</summary>
    public long Uloz(Udalost u)
    {
        var nazov = (u.Nazov ?? "").Trim();
        if (nazov.Length == 0)
        {
            throw new SkvpException("Zadajte názov udalosti.");
        }

        var zaciatok = u.CelyDen ? u.Zaciatok.Date : u.Zaciatok;
        var koniec = u.CelyDen ? u.Koniec?.Date : u.Koniec;
        if (koniec is { } k && k < zaciatok)
        {
            throw new SkvpException("Koniec udalosti je skôr ako jej začiatok.");
        }

        var opakovanieDo = u.Opakovanie == OpakovanieUdalosti.Ziadne ? null : u.OpakovanieDo?.Date;
        if (opakovanieDo is { } d && d < zaciatok.Date)
        {
            throw new SkvpException("Opakovanie končí skôr, ako sa udalosť začína.");
        }

        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        long id;
        if (u.Id == 0)
        {
            id = Planovanie.VytvorObjekt(db, TypObjektu.Udalost, nazov, null);
            Databaza.Vykonaj(db, "INSERT INTO udalost(objekt_id, guid, zaciatok) VALUES($id, $g, $z)",
                ("$id", id), ("$g", u.Guid.ToString("D")), ("$z", Planovanie.Iso(zaciatok, sCasom: true)));
        }
        else
        {
            if (!Planovanie.Existuje(db, u.Id, TypObjektu.Udalost))
            {
                throw new SkvpException("Udalosť už neexistuje (bola odstránená).");
            }

            id = u.Id;
            Planovanie.ZmenObjekt(db, id, nazov, null);
        }

        Databaza.Vykonaj(db, """
            UPDATE udalost SET popis = $popis, typ_id = $typ, zaciatok = $z, koniec = $k, cely_den = $cd, opakovanie = $op,
                opakovanie_do = $opdo, pripomienka = $pr, stav = $st, miesto = $m
            WHERE objekt_id = $id
            """,
            ("$popis", Planovanie.Prazdne(u.Popis)), ("$typ", u.TypId), ("$z", Planovanie.Iso(zaciatok, sCasom: true)),
            ("$k", Planovanie.Iso(koniec, sCasom: true)), ("$cd", u.CelyDen ? 1 : 0), ("$op", (int)u.Opakovanie),
            ("$opdo", Planovanie.Iso(opakovanieDo)), ("$pr", (int)u.Pripomienka), ("$st", (int)u.Stav), ("$m", Planovanie.Prazdne(u.Miesto)),
            ("$id", id));
        Planovanie.NastavNaviazania(db, id, u.TykaSa);
        transakcia.Commit();
        return id;
    }

    public void NastavStav(long id, StavUdalosti stav)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "UPDATE udalost SET stav = $s WHERE objekt_id = $id", ("$s", (int)stav), ("$id", id));
        Planovanie.Upravene(db, id);
    }

    /// <summary>Presunie udalosť na iný deň (čas sa zachová) – ťahanie v kalendári.</summary>
    public void PresunNaDen(long id, DateTime den)
    {
        if (Nacitaj(id) is not { } u)
        {
            return;
        }

        var posun = den.Date - u.Zaciatok.Date;
        u.Zaciatok += posun;
        u.Koniec += posun;
        if (u.OpakovanieDo is { } dokedy && dokedy < u.Zaciatok.Date)
        {
            u.OpakovanieDo = null;
        }

        Uloz(u);
    }

    // ------------------------------------------------------------------ opakovanie

    /// <summary>Začiatky opakovaní udalosti, ktoré zasahujú do dní od – po (vrátane).</summary>
    public static IEnumerable<DateTime> Opakovania(Udalost u, DateTime od, DateTime po)
    {
        var prvyDen = od.Date;
        var poslednyDen = po.Date;
        var trvanie = (u.Koniec ?? u.Zaciatok) - u.Zaciatok;
        if (trvanie < TimeSpan.Zero)
        {
            trvanie = TimeSpan.Zero;
        }

        var hranica = u.OpakovanieDo?.Date ?? DateTime.MaxValue.Date;
        bool Zasahuje(DateTime zaciatok) => zaciatok.Date <= poslednyDen && (zaciatok + trvanie).Date >= prvyDen;

        if (u.Opakovanie == OpakovanieUdalosti.Ziadne)
        {
            if (Zasahuje(u.Zaciatok))
            {
                yield return u.Zaciatok;
            }

            yield break;
        }

        // prvé opakovanie, ktoré môže zasahovať do obdobia (bez prechádzania celej histórie)
        var preskocit = u.Opakovanie switch
        {
            OpakovanieUdalosti.Denne => (int)((prvyDen - u.Zaciatok.Date - trvanie).TotalDays),
            OpakovanieUdalosti.Tyzdenne => (int)((prvyDen - u.Zaciatok.Date - trvanie).TotalDays / 7),
            OpakovanieUdalosti.Mesacne => (prvyDen.Year - u.Zaciatok.Year) * 12 + prvyDen.Month - u.Zaciatok.Month - 1 - (int)(trvanie.TotalDays / 28),
            _ => prvyDen.Year - u.Zaciatok.Year - 1 - (int)(trvanie.TotalDays / 365),
        };

        for (var k = Math.Max(0, preskocit - 1); k < int.MaxValue; k++)
        {
            var zaciatok = u.Opakovanie switch
            {
                OpakovanieUdalosti.Denne => u.Zaciatok.AddDays(k),
                OpakovanieUdalosti.Tyzdenne => u.Zaciatok.AddDays(7.0 * k),
                OpakovanieUdalosti.Mesacne => u.Zaciatok.AddMonths(k),
                _ => u.Zaciatok.AddYears(k),
            };

            if (zaciatok.Date > poslednyDen || zaciatok.Date > hranica)
            {
                yield break;
            }

            if (Zasahuje(zaciatok))
            {
                yield return zaciatok;
            }
        }
    }

    public static string NazovOpakovania(OpakovanieUdalosti o) => o switch
    {
        OpakovanieUdalosti.Denne => "každý deň",
        OpakovanieUdalosti.Tyzdenne => "každý týždeň",
        OpakovanieUdalosti.Mesacne => "každý mesiac",
        OpakovanieUdalosti.Rocne => "každý rok",
        _ => "neopakuje sa",
    };

    public static string NazovPripomienky(PripomienkaUdalosti p) => p switch
    {
        PripomienkaUdalosti.VDen => "v deň udalosti",
        PripomienkaUdalosti.DenVopred => "deň vopred",
        PripomienkaUdalosti.TyzdenVopred => "týždeň vopred",
        _ => "bez pripomienky",
    };

    /// <summary>Kedy sa má na výskyt upozorniť (null = bez pripomienky).</summary>
    public static DateTime? CasPripomienky(VyskytVKalendari v) => v.Pripomienka switch
    {
        PripomienkaUdalosti.VDen => v.CelyDen ? v.Zaciatok.Date.AddHours(7) : v.Zaciatok.AddMinutes(-15),
        PripomienkaUdalosti.DenVopred => v.Zaciatok.Date.AddDays(-1).AddHours(12),
        PripomienkaUdalosti.TyzdenVopred => v.Zaciatok.Date.AddDays(-7).AddHours(12),
        _ => null,
    };

    // ------------------------------------------------------------------ výskyty v období

    /// <summary>Všetko, čo zasahuje do dní od – po (vrátane): udalosti, úlohy s termínom a automatické termíny.</summary>
    public List<VyskytVKalendari> Vyskyty(DateTime od, DateTime po, bool automaticke = true)
    {
        var vysledok = new List<VyskytVKalendari>();
        using (var db = Databaza.Otvor())
        {
            var typy = Typy().ToDictionary(t => t.Id);
            var naviazania = Planovanie.NaviazaniaTypu(db, TypObjektu.Udalost);
            foreach (var u in Udalosti(db, po.Date.AddDays(1)))
            {
                var typ = u.TypId is { } t && typy.TryGetValue(t, out var x) ? x : null;
                var tykaSa = naviazania.TryGetValue(u.Id, out var n) ? n : new List<Naviazanie>();
                foreach (var zaciatok in Opakovania(u, od, po))
                {
                    vysledok.Add(new VyskytVKalendari
                    {
                        Kluc = $"{u.Guid:N}-{zaciatok:yyyyMMddHHmm}",
                        Zdroj = ZdrojKalendara.Udalost,
                        Id = u.Id,
                        Nazov = u.Nazov,
                        Popis = u.Popis,
                        Zaciatok = zaciatok,
                        Koniec = u.Koniec + (zaciatok - u.Zaciatok),
                        CelyDen = u.CelyDen,
                        Typ = typ?.Nazov ?? "",
                        Farba = typ?.Farba ?? Planovanie.FarbaSiva,
                        Stav = u.Stav,
                        Opakovanie = u.Opakovanie,
                        Pripomienka = u.Pripomienka,
                        TykaSa = tykaSa,
                        Miesto = u.Miesto,
                    });
                }
            }
        }

        foreach (var uloha in new UlohyService().VObdobi(od, po))
        {
            vysledok.Add(new VyskytVKalendari
            {
                Kluc = $"uloha-{uloha.Id}",
                Zdroj = ZdrojKalendara.Uloha,
                Id = uloha.Id,
                Nazov = uloha.Text,
                Popis = uloha.Popis,
                Zaciatok = uloha.Termin!.Value.Date,
                CelyDen = true,
                Typ = "úloha" + (uloha.Vysoka ? " (vysoká priorita)" : ""),
                Farba = Planovanie.FarbaUlohy,
                Stav = uloha.Hotovo ? StavUdalosti.Hotovo : StavUdalosti.Naplanovane,
                Priorita = uloha.Priorita,
                TykaSa = uloha.TykaSa,
            });
        }

        if (automaticke)
        {
            vysledok.AddRange(AutomatickeTerminy(od.Date, po.Date));
        }

        return vysledok
            .OrderBy(v => v.Zaciatok.Date)
            .ThenBy(v => v.Zdroj == ZdrojKalendara.Prazdniny ? 0 : v.ViacDni ? 1 : v.CelyDen ? 2 : 3)
            .ThenBy(v => v.Zaciatok)
            .ThenBy(v => v.Nazov, StringComparer.Create(Planovanie.Sk, false))
            .ToList();
    }

    /// <summary>Najbližšie udalosti a termíny úloh, ktoré patria k objektom (panel pri predmete či oblasti).</summary>
    public List<VyskytVKalendari> Najblizsie(IReadOnlyCollection<long> objekty, int dni = 120, int pocet = 8)
    {
        var dnes = DateTime.Today;
        return Vyskyty(dnes, dnes.AddDays(dni), automaticke: false)
            .Where(v => v.Zdroj == ZdrojKalendara.Udalost && v.Stav == StavUdalosti.Naplanovane && v.TykaSa.Any(t => objekty.Contains(t.Id)))
            .Take(pocet)
            .ToList();
    }

    // ------------------------------------------------------------------ automatické termíny

    private DataNoviniek Novinky()
    {
        if (_novinky is null)
        {
            try
            {
                _novinky = NovinkyService.Nacitaj();
            }
            catch (Exception)
            {
                _novinky = new DataNoviniek();
            }
        }

        return _novinky;
    }

    private static DateTime Den(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    /// <summary>Patrí termín školského roka do zvoleného kraja (jarné prázdniny sa líšia podľa krajov)?</summary>
    private bool VKraji(PolozkaNoviniek p) => p.Skupina is null || Kraj == NovinkyService.VsetkyKraje || p.Skupina == Kraj;

    public static bool JePrazdniny(PolozkaNoviniek p) =>
        p.Kategoria == NovinkyService.TemaSkolskyRok && p.Druh == NovinkyService.DruhTermin && p.Zaciatok is not null && p.Posledny is not null
        && p.Nazov.Contains("prázdniny", StringComparison.OrdinalIgnoreCase);

    /// <summary>Dni prázdnin v období (podľa kraja) s názvom prázdnin – podfarbenie dní v kalendári.</summary>
    public Dictionary<DateTime, string> DniPrazdnin(DateTime od, DateTime po)
    {
        var vysledok = new Dictionary<DateTime, string>();
        if (!Nastavenia.SkolskyRok)
        {
            return vysledok;
        }

        foreach (var p in Novinky().Polozky.Where(p => JePrazdniny(p) && VKraji(p)))
        {
            var nazov = KratkyNazovPrazdnin(p.Nazov);
            for (var d = Den(p.Zaciatok!.Value); d <= Den(p.Koniec!.Value); d = d.AddDays(1))
            {
                if (d >= od.Date && d <= po.Date)
                {
                    vysledok[d] = nazov;
                }
            }
        }

        return vysledok;
    }

    /// <summary>„Jarné prázdniny 2027/2028 – Košický, Prešovský kraj“ → „Jarné prázdniny“.</summary>
    public static string KratkyNazovPrazdnin(string nazov) => PrazdninyVNazve().Match(nazov) is { Success: true } m ? m.Value : nazov;

    [GeneratedRegex(@"^\p{L}+ prázdniny", RegexOptions.IgnoreCase)]
    private static partial Regex PrazdninyVNazve();

    private List<VyskytVKalendari> AutomatickeTerminy(DateTime od, DateTime po)
    {
        var vysledok = new List<VyskytVKalendari>();
        bool Zasahuje(DateTime zaciatok, DateTime koniec) => zaciatok <= po && koniec >= od;
        var data = Novinky();

        foreach (var p in data.Polozky.Where(p => p.Druh == NovinkyService.DruhTermin && p.Zaciatok is not null && !p.Archivna))
        {
            var jeSkolskyRok = p.Kategoria == NovinkyService.TemaSkolskyRok;
            if (jeSkolskyRok ? !Nastavenia.SkolskyRok || !VKraji(p) : !Nastavenia.Novinky || !KategorieTerminov.Contains(p.Kategoria))
            {
                continue;
            }

            // termín „do …“ je jeden deň (posledný deň lehoty)
            var zaciatok = Den(p.DatumOd ?? p.DatumDo!.Value);
            var koniec = Den(p.Koniec!.Value);
            if (!Zasahuje(zaciatok, koniec) || (jeSkolskyRok && p.Posledny is null && p.Nazov.Contains("prázdniny", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var kategoria = data.Kategoria(p.Kategoria);
            var prazdniny = JePrazdniny(p);
            vysledok.Add(new VyskytVKalendari
            {
                Kluc = $"novinky-{p.Id}",
                Zdroj = prazdniny ? ZdrojKalendara.Prazdniny : jeSkolskyRok ? ZdrojKalendara.SkolskyRok : ZdrojKalendara.Novinky,
                Nazov = p.DatumOd is null ? $"Termín: {p.Nazov}" : p.Nazov,
                Popis = p.Text + (p.Poznamka is { Length: > 0 } pozn ? "\n" + pozn : ""),
                Zaciatok = zaciatok,
                Koniec = koniec,
                CelyDen = true,
                Typ = prazdniny ? "prázdniny" : kategoria?.Nazov ?? "",
                Farba = prazdniny ? "#8CCB98" : kategoria?.Farba ?? Planovanie.FarbaSiva,
                Odkaz = p.Zdroje.FirstOrDefault()?.Url,
            });
        }

        if (Nastavenia.Osnovy)
        {
            foreach (var (id, nazov, farba, platiOd) in Platnosti().Where(x => x.Od >= od && x.Od <= po))
            {
                vysledok.Add(new VyskytVKalendari
                {
                    Kluc = $"osnova-{id}-{platiOd:yyyyMMdd}",
                    Zdroj = ZdrojKalendara.Osnova,
                    Id = id,
                    Nazov = $"Platí osnova: {nazov}",
                    Popis = "Začiatok platnosti podľa úvodnej tabuľky osnovy.",
                    Zaciatok = platiOd,
                    CelyDen = true,
                    Typ = "platnosť osnovy",
                    Farba = farba,
                });
            }
        }

        if (Nastavenia.Zalohy && ZalohaService.Frekvencia == ZalohaService.Tyzdenne && ZalohaService.PoslednaAutomaticka is { } posledna)
        {
            for (var d = posledna.Date.AddDays(7); d <= po; d = d.AddDays(7))
            {
                if (d >= od)
                {
                    vysledok.Add(new VyskytVKalendari
                    {
                        Kluc = $"zaloha-{d:yyyyMMdd}",
                        Zdroj = ZdrojKalendara.Zaloha,
                        Nazov = "Týždenná záloha",
                        Popis = "Automatická záloha databázy pri spustení programu (Servis → Zálohy).",
                        Zaciatok = d,
                        CelyDen = true,
                        Typ = "záloha",
                        Farba = "#A7B1C2",
                    });
                }
            }
        }

        if (Nastavenia.Archiv)
        {
            var archivovane = ArchivSkolskehoRoka.Zoznam().Select(a => a.SkolskyRok).ToHashSet();
            foreach (var koniecRoka in data.Polozky.Where(p => p.Id.StartsWith("sr-koniec", StringComparison.Ordinal) && p.Zaciatok is not null))
            {
                var den = Den(koniecRoka.Zaciatok!.Value);
                var rok = ArchivSkolskehoRoka.Aktualny(den);
                if (!archivovane.Contains(rok) && den >= od && den <= po)
                {
                    vysledok.Add(new VyskytVKalendari
                    {
                        Kluc = $"archiv-{rok}",
                        Zdroj = ZdrojKalendara.Archiv,
                        Nazov = $"Archivovať osnovy školského roka {rok}",
                        Popis = "Na konci školského roka archivujte osnovy v sekcii Archív – uložia sa tak, ako v tomto roku platili.",
                        Zaciatok = den,
                        CelyDen = true,
                        Typ = "archív",
                        Farba = "#C7A27C",
                    });
                }
            }
        }

        return vysledok;
    }

    /// <summary>Začiatky platnosti osnov („od 1.9.2026“ v úvodnej tabuľke) – čítajú sa z dokumentov raz za zobrazenie kalendára.</summary>
    private List<(long Id, string Nazov, string Farba, DateTime Od)> Platnosti()
    {
        if (_platnosti is not null)
        {
            return _platnosti;
        }

        _platnosti = new List<(long, string, string, DateTime)>();
        try
        {
            var sluzba = new ObjektService();
            foreach (var oblast in sluzba.Oblasti())
            {
                foreach (var predmet in sluzba.Deti(oblast.Id).Where(p => p.Typ == TypObjektu.Predmet && p.DlzkaTextu > 0))
                {
                    if (DatumPlatnosti(UvodnaTabulka.Precitaj(predmet.Id).Platnost) is { } od)
                    {
                        _platnosti.Add((predmet.Id, predmet.Nazov, oblast.Farba ?? Planovanie.FarbaSiva, od));
                    }
                }
            }
        }
        catch (Exception)
        {
            // kalendár funguje aj bez platnosti osnov
        }

        return _platnosti;
    }

    /// <summary>„od 1.9.2026“, „od 1. 9. 2026“ → 1. 9. 2026.</summary>
    public static DateTime? DatumPlatnosti(string? platnost) =>
        platnost is not null && DatumOd().Match(platnost) is { Success: true } m
        && DateTime.TryParseExact($"{m.Groups[1].Value}.{m.Groups[2].Value}.{m.Groups[3].Value}", "d.M.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    [GeneratedRegex(@"(\d{1,2})\.\s*(\d{1,2})\.\s*(\d{4})")]
    private static partial Regex DatumOd();

    // ------------------------------------------------------------------ prehľad dňa

    public PrehladDna Prehlad(DateTime dnes)
    {
        dnes = dnes.Date;
        var dnesne = Vyskyty(dnes, dnes).Where(v => !v.JeZrusena && v.Zdroj != ZdrojKalendara.Prazdniny).ToList();
        var pripomienky = Vyskyty(dnes.AddDays(1), dnes.AddDays(7), automaticke: false)
            .Where(v => v.Zdroj == ZdrojKalendara.Udalost && v.Stav == StavUdalosti.Naplanovane && CasPripomienky(v) is { } cas && cas.Date <= dnes)
            .ToList();
        var poTermine = new UlohyService().Zoznam(ajHotove: false).Where(u => u.PoTermine).ToList();
        return new PrehladDna(dnesne, pripomienky, poTermine);
    }

    public static int CisloTyzdna(DateTime datum) => ISOWeek.GetWeekOfYear(datum);

    // ------------------------------------------------------------------ export .ics

    /// <summary>
    /// Kalendár vo formáte iCalendar: vlastné udalosti (opakované jedným pravidlom), úlohy s termínom a voliteľne
    /// automatické termíny školského roka – v období od – po.
    /// </summary>
    public string Kalendar(DateTime od, DateTime po, bool automaticke)
    {
        var ics = new Ics("SkVP – kalendár");
        var typy = Typy().ToDictionary(t => t.Id);
        using (var db = Databaza.Otvor())
        {
            var naviazania = Planovanie.NaviazaniaTypu(db, TypObjektu.Udalost);
            foreach (var u in Udalosti(db, po.Date.AddDays(1)).Where(u => Opakovania(u, od, po).Any()).OrderBy(u => u.Zaciatok))
            {
                var tykaSa = naviazania.TryGetValue(u.Id, out var n) ? string.Join(", ", n.Where(x => !x.Odstranene).Select(x => x.Nazov)) : "";
                var popis = string.Join("\n\n", new[] { u.Popis ?? "", tykaSa.Length > 0 ? "Týka sa: " + tykaSa : "" }.Where(t => t.Length > 0));
                string? pravidlo = u.Opakovanie == OpakovanieUdalosti.Ziadne
                    ? null
                    : "FREQ=" + u.Opakovanie switch
                    {
                        OpakovanieUdalosti.Denne => "DAILY",
                        OpakovanieUdalosti.Tyzdenne => "WEEKLY",
                        OpakovanieUdalosti.Mesacne => "MONTHLY",
                        _ => "YEARLY",
                    } + (u.OpakovanieDo is { } dokedy
                        ? ";UNTIL=" + (u.CelyDen ? dokedy.ToString("yyyyMMdd", CultureInfo.InvariantCulture) : dokedy.ToString("yyyyMMdd'T'235959", CultureInfo.InvariantCulture))
                        : "");
                int? pripomienka = u.Pripomienka switch
                {
                    PripomienkaUdalosti.VDen => 15,
                    PripomienkaUdalosti.DenVopred => 24 * 60,
                    PripomienkaUdalosti.TyzdenVopred => 7 * 24 * 60,
                    _ => null,
                };
                ics.Udalost($"{u.Guid:D}@skvp", u.Nazov, u.Zaciatok, u.Koniec, u.CelyDen, popis,
                    u.TypId is { } t && typy.TryGetValue(t, out var typ) ? typ.Nazov : null, u.Miesto, pravidlo: pravidlo,
                    pripomienkaMinut: pripomienka, zrusena: u.Stav == StavUdalosti.Zrusene);
            }
        }

        foreach (var v in Vyskyty(od, po, automaticke).Where(v => v.Zdroj != ZdrojKalendara.Udalost))
        {
            ics.Udalost($"{v.Kluc}@skvp", v.JeUloha ? "Úloha: " + v.Nazov : v.Nazov, v.Zaciatok, v.Koniec, celyDen: true,
                popis: string.Join("\n\n", new[] { v.Popis ?? "", v.TykaSaText.Length > 0 ? "Týka sa: " + v.TykaSaText : "" }.Where(t => t.Length > 0)),
                kategoria: v.Typ, url: v.Odkaz, volno: v.JeAutomaticky);
        }

        return ics.ToString();
    }
}
