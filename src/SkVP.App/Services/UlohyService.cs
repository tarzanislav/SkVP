using Microsoft.Data.Sqlite;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Úloha pripravená na zobrazenie (sekcia Úlohy, dashboard, panel pri predmete, kalendár).</summary>
public sealed record UlohaRiadok(
    long Id, string Text, string? Popis, DateTime? Termin, PrioritaUlohy Priorita, DateTime? DatumHotovo, string? Zodpovedny,
    string? Zdroj, DateTime Vytvorene, IReadOnlyList<Naviazanie> TykaSa)
{
    public bool Hotovo => DatumHotovo is not null;
    public bool PoTermine => !Hotovo && Termin is { } t && t.Date < DateTime.Today;
    public bool Dnes => !Hotovo && Termin is { } t && t.Date == DateTime.Today;
    public bool Vysoka => Priorita == PrioritaUlohy.Vysoka;
    public bool Nizka => Priorita == PrioritaUlohy.Nizka;
    public string TerminText => Planovanie.TerminText(Termin);
    public bool MaPopis => !string.IsNullOrWhiteSpace(Popis);
    public bool MaTykaSa => TykaSa.Count > 0;

    /// <summary>„Fyzika · Chémia“ – k čomu úloha patrí.</summary>
    public string TykaSaText => string.Join(" · ", TykaSa.Where(t => !t.Odstranene).Select(t => t.Text));

    /// <summary>Riadok pod textom úlohy: naviazanie a zodpovedný.</summary>
    public string Podrobnosti => string.Join("  ·  ",
        new[] { TykaSaText, string.IsNullOrWhiteSpace(Zodpovedny) ? "" : "zodpovedá: " + Zodpovedny }.Where(t => t.Length > 0));

    public bool MaPodrobnosti => Podrobnosti.Length > 0;

    /// <summary>Skupina v zozname úloh: po termíne, dnes, tento týždeň, neskôr, bez termínu, hotové.</summary>
    public string Skupina
    {
        get
        {
            if (Hotovo)
            {
                return UlohyService.SkupinaHotove;
            }

            if (Termin is not { } t)
            {
                return UlohyService.SkupinaBezTerminu;
            }

            var dnes = DateTime.Today;
            var buduciPondelok = Planovanie.Pondelok(dnes).AddDays(7);
            return t.Date < dnes ? UlohyService.SkupinaPoTermine
                : t.Date == dnes ? UlohyService.SkupinaDnes
                : t.Date < buduciPondelok ? UlohyService.SkupinaTentoTyzden
                : UlohyService.SkupinaNeskor;
        }
    }

    /// <summary>Patrí úloha k objektu (predmetu, oblasti …)?</summary>
    public bool PatriK(long objektId) => TykaSa.Any(t => t.Id == objektId);
}

/// <summary>
/// Úlohy: rýchle pridanie, termín, priorita, zodpovedný, popis, hotovo a naviazanie na predmety, oblasti a časti
/// osnov („Týka sa“). Úloha s termínom sa zobrazuje aj v kalendári – je to tá istá úloha (jeden zdroj).
/// </summary>
public sealed class UlohyService
{
    public const string SkupinaPoTermine = "Po termíne";
    public const string SkupinaDnes = "Dnes";
    public const string SkupinaTentoTyzden = "Tento týždeň";
    public const string SkupinaNeskor = "Neskôr";
    public const string SkupinaBezTerminu = "Bez termínu";
    public const string SkupinaHotove = "Hotové";

    /// <summary>Poradie skupín v zozname úloh.</summary>
    private static readonly string[] PoradieSkupin =
        { SkupinaPoTermine, SkupinaDnes, SkupinaTentoTyzden, SkupinaNeskor, SkupinaBezTerminu, SkupinaHotove };

    private const string Sql = """
        SELECT o.id, o.nazov, u.popis, u.termin, u.priorita, u.hotovo, u.zodpovedny, u.zdroj, o.vytvorene
        FROM uloha u JOIN objekt o ON o.id = u.objekt_id
        WHERE o.odstranene IS NULL
        """;

    /// <summary>Úlohy z databázy (podmienka sa pridá za <c>WHERE</c>) aj s tým, k čomu patria.</summary>
    private static List<UlohaRiadok> Citaj(SqliteConnection db, string podmienka = "", params (string, object?)[] parametre)
    {
        var naviazania = Planovanie.NaviazaniaTypu(db, TypObjektu.Uloha);
        var vysledok = new List<UlohaRiadok>();
        using var prikaz = Databaza.Prikaz(db, Sql + podmienka, parametre);
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            var id = citac.GetInt64(0);
            vysledok.Add(new UlohaRiadok(id, citac.GetString(1), Planovanie.Text(citac, 2), Planovanie.Datum(citac, 3), (PrioritaUlohy)citac.GetInt32(4),
                Planovanie.Datum(citac, 5), Planovanie.Text(citac, 6), Planovanie.Text(citac, 7), Planovanie.Datum(citac, 8) ?? DateTime.MinValue,
                naviazania.TryGetValue(id, out var n) ? n : new List<Naviazanie>()));
        }

        return vysledok;
    }

    /// <summary>
    /// Úlohy zoradené podľa skupín (<see cref="PoradieSkupin"/>), v skupine vysoká priorita, termín a poradie zadania,
    /// hotové naposledy dokončené hore. Voliteľne len úlohy, ktoré patria k niektorému z objektov.
    /// </summary>
    public List<UlohaRiadok> Zoznam(bool ajHotove = true, IReadOnlyCollection<long>? patriK = null)
    {
        using var db = Databaza.Otvor();
        var vyber = patriK is null ? null : Planovanie.ZaznamyPre(db, patriK);
        return Citaj(db, ajHotove ? "" : " AND u.hotovo IS NULL")
            .Where(u => vyber is null || vyber.Contains(u.Id))
            .OrderBy(u => Array.IndexOf(PoradieSkupin, u.Skupina))
            .ThenByDescending(u => u.Hotovo ? u.DatumHotovo : null)
            .ThenByDescending(u => u.Priorita)
            .ThenBy(u => u.Termin ?? DateTime.MaxValue)
            .ThenBy(u => u.Id)
            .ToList();
    }

    /// <summary>Úlohy s termínom v období (od – po vrátane) – pre kalendár.</summary>
    public List<UlohaRiadok> VObdobi(DateTime od, DateTime po)
    {
        using var db = Databaza.Otvor();
        return Citaj(db, " AND u.termin >= $od AND u.termin < $po", ("$od", Planovanie.Iso(od.Date)), ("$po", Planovanie.Iso(po.Date.AddDays(1))));
    }

    public Uloha? Nacitaj(long id)
    {
        using var db = Databaza.Otvor();
        return Citaj(db, " AND o.id = $id", ("$id", id)).FirstOrDefault() is { } u
            ? new Uloha
            {
                Id = u.Id, Text = u.Text, Popis = u.Popis, Termin = u.Termin, Priorita = u.Priorita, Hotovo = u.DatumHotovo,
                Zodpovedny = u.Zodpovedny, Zdroj = u.Zdroj, Vytvorene = u.Vytvorene, TykaSa = u.TykaSa.Select(t => t.Id).ToList(),
            }
            : null;
    }

    /// <summary>Otvorené, po termíne a hotové úlohy.</summary>
    public (int Otvorene, int PoTermine, int Hotove) Pocty()
    {
        using var db = Databaza.Otvor();
        int Pocet(string podmienka) => Convert.ToInt32(Databaza.Skalar(db,
            $"SELECT count(*) FROM uloha u JOIN objekt o ON o.id = u.objekt_id WHERE o.odstranene IS NULL AND {podmienka}",
            ("$dnes", Planovanie.Iso(DateTime.Today))) ?? 0L);
        return (Pocet("u.hotovo IS NULL"), Pocet("u.hotovo IS NULL AND u.termin < $dnes"), Pocet("u.hotovo IS NOT NULL"));
    }

    /// <summary>Rýchle pridanie úlohy. Vráti Id.</summary>
    public long Pridaj(string text, DateTime? termin = null, PrioritaUlohy priorita = PrioritaUlohy.Normalna,
        IEnumerable<long>? tykaSa = null, string? popis = null, string? zdroj = null, string? zodpovedny = null) =>
        Uloz(new Uloha
        {
            Text = text, Termin = termin, Priorita = priorita, TykaSa = tykaSa?.ToList() ?? new List<long>(), Popis = popis, Zdroj = zdroj,
            Zodpovedny = zodpovedny,
        });

    /// <summary>
    /// Uloží novú (Id = 0) alebo upravenú úlohu. Vráti Id. Čas dokončenia sa pri ďalších úpravách nemení
    /// (nastaví sa pri prvom označení ako hotová); zdroj úlohy sa nedá zmeniť ani zmazať.
    /// </summary>
    public long Uloz(Uloha u)
    {
        var text = (u.Text ?? "").Trim();
        if (text.Length == 0)
        {
            throw new SkvpException("Napíšte, čo treba urobiť.");
        }

        if (text.Length > 300)
        {
            throw new SkvpException("Text úlohy je príliš dlhý (najviac 300 znakov) – podrobnosti napíšte do popisu.");
        }

        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        long id;
        if (u.Id == 0)
        {
            id = Planovanie.VytvorObjekt(db, TypObjektu.Uloha, text, null);
            Databaza.Vykonaj(db, "INSERT INTO uloha(objekt_id) VALUES($id)", ("$id", id));
        }
        else
        {
            if (!Planovanie.Existuje(db, u.Id, TypObjektu.Uloha))
            {
                throw new SkvpException("Úloha už neexistuje (bola odstránená).");
            }

            id = u.Id;
            Planovanie.ZmenObjekt(db, id, text, null);
        }

        Databaza.Vykonaj(db, """
            UPDATE uloha SET popis = $popis, termin = $termin, priorita = $priorita, zodpovedny = $zodp, zdroj = coalesce(zdroj, $zdroj),
                hotovo = CASE WHEN $hotovo IS NULL THEN NULL ELSE coalesce(hotovo, $hotovo) END
            WHERE objekt_id = $id
            """,
            ("$popis", Planovanie.Prazdne(u.Popis)), ("$termin", Planovanie.Iso(u.Termin)), ("$priorita", (int)u.Priorita),
            ("$zodp", Planovanie.Prazdne(u.Zodpovedny)), ("$zdroj", Planovanie.Prazdne(u.Zdroj)),
            ("$hotovo", Planovanie.Iso(u.Hotovo, sCasom: true)), ("$id", id));
        Planovanie.NastavNaviazania(db, id, u.TykaSa);
        transakcia.Commit();
        return id;
    }

    public void NastavHotovo(long id, bool hotovo)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "UPDATE uloha SET hotovo = CASE WHEN $h IS NULL THEN NULL ELSE coalesce(hotovo, $h) END WHERE objekt_id = $id",
            ("$h", hotovo ? Planovanie.Iso(DateTime.Now, sCasom: true) : null), ("$id", id));
        Planovanie.Upravene(db, id);
    }

    /// <summary>Odstráni všetky dokončené úlohy (dá sa vrátiť späť). Vráti ich Id.</summary>
    public List<long> OdstranHotove()
    {
        var hotove = Zoznam().Where(u => u.Hotovo).Select(u => u.Id).ToList();
        foreach (var id in hotove)
        {
            Planovanie.Odstran(id);
        }

        return hotove;
    }

    /// <summary>Zdroje existujúcich úloh (aj hotových) – navrhovaná úloha s rovnakým zdrojom sa už neponúkne.</summary>
    public HashSet<string> Zdroje()
    {
        using var db = Databaza.Otvor();
        return Citaj(db, " AND u.zdroj IS NOT NULL").Select(u => u.Zdroj!).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Počet otvorených úloh pre každý objekt, ku ktorému patria (odznaky pri predmetoch).</summary>
    public Dictionary<long, int> OtvorenePodlaObjektov()
    {
        var vysledok = new Dictionary<long, int>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db, """
            SELECT v.do_id, count(*) FROM vazba v
            JOIN uloha u ON u.objekt_id = v.z_id JOIN objekt o ON o.id = v.z_id
            WHERE v.typ = $p AND u.hotovo IS NULL AND o.odstranene IS NULL
            GROUP BY v.do_id
            """, ("$p", TypVazby.Patri));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            vysledok[citac.GetInt64(0)] = (int)citac.GetInt64(1);
        }

        return vysledok;
    }
}
