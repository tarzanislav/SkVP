using Microsoft.Data.Sqlite;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Poznámka pripravená na zobrazenie v zozname (lístok).</summary>
public sealed record PoznamkaRiadok(
    long Id, string Nazov, string Text, string Farba, bool Pripnuta, DateTime? Datum, DateTime Vytvorene, DateTime Zmenene,
    IReadOnlyList<Naviazanie> TykaSa)
{
    /// <summary>Prvé riadky textu bez prázdnych riadkov.</summary>
    public string Nahlad
    {
        get
        {
            var riadky = Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var nahlad = string.Join(" · ", riadky.Where(r => r != Nazov).Take(3));
            return nahlad.Length > 180 ? nahlad[..180].TrimEnd() + "…" : nahlad;
        }
    }

    public bool MaDatum => Datum is not null;
    public string DatumText => Planovanie.DatumText(Datum);
    public string ZmeneneText => Planovanie.DatumCasText(Zmenene);
    public bool MaTykaSa => TykaSa.Any(t => !t.Odstranene);
    public string TykaSaText => string.Join(" · ", TykaSa.Where(t => !t.Odstranene).Select(t => t.Text));

    /// <summary>Riadok pod náhľadom: deň v kalendári a naviazanie.</summary>
    public string Podrobnosti => string.Join("  ·  ", new[] { MaDatum ? "deň " + DatumText : "", TykaSaText }.Where(t => t.Length > 0));

    public bool MaPodrobnosti => Podrobnosti.Length > 0;

    public bool PatriK(long objektId) => TykaSa.Any(t => t.Id == objektId);
}

/// <summary>
/// Poznámky (lístky): farba, pripnutie, hľadanie bez diakritiky, deň v kalendári a naviazanie na predmety, oblasti,
/// časti osnov, udalosti a úlohy. Názov sa doplní z prvého riadku textu, ak ho používateľ nenapíše.
/// </summary>
public sealed class PoznamkyService
{
    /// <summary>Pastelové farby lístkov – dobre čitateľné s tmavým textom vo svetlom aj tmavom vzhľade.</summary>
    public static readonly string[] Farby =
    {
        "#FFF1A8", "#FFD8A8", "#FFC9D9", "#E5D4F5", "#C9DDF8", "#C4ECEC", "#CDEBC5", "#E6E8EC",
    };

    public static bool JePlatnaFarba(string? farba) =>
        farba is { Length: 7 } && farba[0] == '#' && farba.Skip(1).All(Uri.IsHexDigit);

    /// <summary>Názov z prvého riadku textu.</summary>
    public static string NazovZTextu(string? text)
    {
        var prvy = (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "";
        return prvy.Length == 0 ? "Poznámka" : prvy.Length > 80 ? prvy[..80].TrimEnd() + "…" : prvy;
    }

    private const string Sql = """
        SELECT o.id, o.nazov, p.text, coalesce(o.farba, ''), p.pripnuta, p.datum, o.vytvorene, o.upravene
        FROM poznamka p JOIN objekt o ON o.id = p.objekt_id
        WHERE o.odstranene IS NULL
        """;

    private static List<PoznamkaRiadok> Citaj(SqliteConnection db, string podmienka = "", params (string, object?)[] parametre)
    {
        var naviazania = Planovanie.NaviazaniaTypu(db, TypObjektu.Poznamka);
        var vysledok = new List<PoznamkaRiadok>();
        using var prikaz = Databaza.Prikaz(db, Sql + podmienka, parametre);
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            var id = citac.GetInt64(0);
            var farba = citac.GetString(3);
            vysledok.Add(new PoznamkaRiadok(id, citac.GetString(1), citac.GetString(2), JePlatnaFarba(farba) ? farba : Farby[0],
                citac.GetInt64(4) == 1, Planovanie.Datum(citac, 5), Planovanie.Datum(citac, 6) ?? DateTime.MinValue,
                Planovanie.Datum(citac, 7) ?? DateTime.MinValue, naviazania.TryGetValue(id, out var n) ? n : new List<Naviazanie>()));
        }

        return vysledok;
    }

    private static List<PoznamkaRiadok> Zorad(IEnumerable<PoznamkaRiadok> poznamky) =>
        poznamky.OrderByDescending(p => p.Pripnuta).ThenByDescending(p => p.Zmenene).ToList();

    /// <summary>Všetky poznámky (pripnuté hore, potom naposledy zmenené), voliteľne len tie, ktoré zodpovedajú hľadaniu.</summary>
    public List<PoznamkaRiadok> Zoznam(string? hladat = null)
    {
        using var db = Databaza.Otvor();
        var vsetky = Citaj(db);
        if (!string.IsNullOrWhiteSpace(hladat))
        {
            vsetky = vsetky.Where(p => Planovanie.Vyhovuje(hladat, p.Nazov, p.Text, p.TykaSaText, p.DatumText)).ToList();
        }

        return Zorad(vsetky);
    }

    /// <summary>Poznámky, ktoré patria k niektorému z objektov (predmet, oblasť s predmetmi, udalosť, úloha).</summary>
    public List<PoznamkaRiadok> PatriaK(IReadOnlyCollection<long> objekty)
    {
        using var db = Databaza.Otvor();
        var vyber = Planovanie.ZaznamyPre(db, objekty);
        return Zorad(Citaj(db).Where(p => vyber.Contains(p.Id)));
    }

    /// <summary>Poznámky pre dni v kalendári (od – po vrátane).</summary>
    public List<PoznamkaRiadok> PreDni(DateTime od, DateTime po)
    {
        using var db = Databaza.Otvor();
        return Citaj(db, " AND p.datum >= $od AND p.datum < $po", ("$od", Planovanie.Iso(od.Date)), ("$po", Planovanie.Iso(po.Date.AddDays(1))))
            .OrderBy(p => p.Datum).ThenBy(p => p.Vytvorene).ToList();
    }

    public List<PoznamkaRiadok> Pripnute(int pocet)
    {
        using var db = Databaza.Otvor();
        return Citaj(db, " AND p.pripnuta = 1").OrderByDescending(p => p.Zmenene).Take(pocet).ToList();
    }

    public int Pocet()
    {
        using var db = Databaza.Otvor();
        return Convert.ToInt32(Databaza.Skalar(db, "SELECT count(*) FROM poznamka p JOIN objekt o ON o.id = p.objekt_id WHERE o.odstranene IS NULL") ?? 0L);
    }

    public Poznamka? Nacitaj(long id)
    {
        using var db = Databaza.Otvor();
        var riadok = Citaj(db, " AND o.id = $id", ("$id", id)).FirstOrDefault();
        return riadok is null
            ? null
            : new Poznamka
            {
                Id = riadok.Id,
                Nazov = riadok.Nazov,
                Text = riadok.Text,
                Farba = riadok.Farba,
                Pripnuta = riadok.Pripnuta,
                Datum = riadok.Datum,
                Vytvorene = riadok.Vytvorene,
                Zmenene = riadok.Zmenene,
                TykaSa = riadok.TykaSa.Select(t => t.Id).ToList(),
            };
    }

    /// <summary>Uloží novú (Id = 0) alebo upravenú poznámku. Vráti Id.</summary>
    public long Uloz(Poznamka p)
    {
        var text = (p.Text ?? "").Replace("\r\n", "\n").TrimEnd();
        var nazov = (p.Nazov ?? "").Trim();
        if (nazov.Length == 0 && text.Trim().Length == 0)
        {
            throw new SkvpException("Poznámka je prázdna – napíšte názov alebo text.");
        }

        if (nazov.Length == 0)
        {
            nazov = NazovZTextu(text);
        }

        if (nazov.Length > 200)
        {
            throw new SkvpException("Názov poznámky je príliš dlhý (najviac 200 znakov).");
        }

        var farba = JePlatnaFarba(p.Farba) ? p.Farba.ToUpperInvariant() : Farby[0];
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        long id;
        if (p.Id == 0)
        {
            id = Planovanie.VytvorObjekt(db, TypObjektu.Poznamka, nazov, farba);
            Databaza.Vykonaj(db, "INSERT INTO poznamka(objekt_id) VALUES($id)", ("$id", id));
        }
        else
        {
            if (!Planovanie.Existuje(db, p.Id, TypObjektu.Poznamka))
            {
                throw new SkvpException("Poznámka už neexistuje (bola odstránená).");
            }

            id = p.Id;
            Planovanie.ZmenObjekt(db, id, nazov, farba);
        }

        Databaza.Vykonaj(db, "UPDATE poznamka SET text = $t, pripnuta = $p, datum = $d WHERE objekt_id = $id",
            ("$t", text), ("$p", p.Pripnuta ? 1 : 0), ("$d", Planovanie.Iso(p.Datum)), ("$id", id));
        Planovanie.NastavNaviazania(db, id, p.TykaSa);
        transakcia.Commit();
        return id;
    }

    public void NastavPripnutie(long id, bool pripnuta)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "UPDATE poznamka SET pripnuta = $p WHERE objekt_id = $id", ("$p", pripnuta ? 1 : 0), ("$id", id));
    }
}
