using System.Globalization;
using Microsoft.Data.Sqlite;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>
/// Spoločný základ kalendára, úloh a poznámok. Každý záznam je objekt v tabuľke <c>objekt</c> (názov, farba,
/// odstránenie s možnosťou vrátiť späť), podrobnosti sú v tabuľkách <c>udalost</c>, <c>uloha</c>, <c>poznamka</c>.
/// K predmetom, oblastiam, častiam osnov aj k sebe navzájom patria cez väzbu <see cref="TypVazby.Patri"/> –
/// jedna úloha môže patriť k viacerým predmetom naraz.
/// </summary>
public static class Planovanie
{
    public static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");

    /// <summary>Farba záznamov bez vlastnej farby (úloha v kalendári, objekt bez oblasti).</summary>
    public const string FarbaUlohy = "#6C8EBF";
    public const string FarbaSiva = "#94A3B8";

    // ------------------------------------------------------------------ dátumy

    public static string? Iso(DateTime? datum, bool sCasom = false) => datum is not { } d
        ? null
        : sCasom ? d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) : d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static DateTime? Datum(SqliteDataReader citac, int stlpec) =>
        citac.IsDBNull(stlpec) ? null : Datum(citac.GetString(stlpec));

    public static DateTime? Datum(string? text) =>
        text is { Length: > 0 } && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>„dnes“, „zajtra“, „včera“, „pi 6. 11.“, pri inom roku „6. 11. 2027“.</summary>
    public static string TerminText(DateTime? termin, DateTime? dnes = null)
    {
        if (termin is not { } t)
        {
            return "";
        }

        var d = (dnes ?? DateTime.Today).Date;
        return (t.Date - d).Days switch
        {
            0 => "dnes",
            1 => "zajtra",
            -1 => "včera",
            _ => t.Year == d.Year ? $"{t.ToString("ddd", Sk).TrimEnd('.')} {t.Day}. {t.Month}." : $"{t.Day}. {t.Month}. {t.Year}",
        };
    }

    public static string DatumText(DateTime? datum) => datum is { } d ? $"{d.Day}. {d.Month}. {d.Year}" : "";

    public static string DatumCasText(DateTime datum) => $"{datum.Day}. {datum.Month}. {datum.Year} {datum:H:mm}";

    /// <summary>Text bez diakritiky a malými písmenami (hľadanie „slovencina“ nájde „Slovenčina“).</summary>
    public static string Normalizuj(string? text) => ImportOsnov.BezDiakritiky(text ?? "").ToLower(Sk);

    /// <summary>Vyhovuje text všetkým slovám hľadania (bez diakritiky)?</summary>
    public static bool Vyhovuje(string hladat, params string?[] texty)
    {
        var slova = Normalizuj(hladat).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (slova.Length == 0)
        {
            return true;
        }

        var spolu = Normalizuj(string.Join(" ", texty));
        return slova.All(spolu.Contains);
    }

    public static string? Prazdne(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // ------------------------------------------------------------------ objekt záznamu

    /// <summary>Nový objekt záznamu (udalosť, úloha, poznámka). Vráti jeho Id.</summary>
    internal static long VytvorObjekt(SqliteConnection db, string typ, string nazov, string? farba)
    {
        var teraz = Databaza.Teraz();
        return Convert.ToInt64(Databaza.Skalar(db, """
            INSERT INTO objekt(typ, rodic_id, poradie, nazov, farba, vytvorene, upravene)
            VALUES($t, NULL, (SELECT coalesce(max(poradie), 0) + 1 FROM objekt WHERE typ = $t), $n, $f, $c, $c);
            SELECT last_insert_rowid();
            """, ("$t", typ), ("$n", nazov), ("$f", farba), ("$c", teraz)), CultureInfo.InvariantCulture);
    }

    internal static void ZmenObjekt(SqliteConnection db, long id, string nazov, string? farba) =>
        Databaza.Vykonaj(db, "UPDATE objekt SET nazov = $n, farba = $f, upravene = $c WHERE id = $id",
            ("$n", nazov), ("$f", farba), ("$c", Databaza.Teraz()), ("$id", id));

    internal static void Upravene(SqliteConnection db, long id) =>
        Databaza.Vykonaj(db, "UPDATE objekt SET upravene = $c WHERE id = $id", ("$c", Databaza.Teraz()), ("$id", id));

    /// <summary>Existuje platný (neodstránený) záznam daného typu?</summary>
    internal static bool Existuje(SqliteConnection db, long id, string typ) =>
        Databaza.Skalar(db, "SELECT 1 FROM objekt WHERE id = $id AND typ = $t AND odstranene IS NULL", ("$id", id), ("$t", typ)) is not null;

    /// <summary>Odstráni záznam (dá sa vrátiť späť cez <see cref="Obnov"/>; natrvalo sa zmaže s košom po 60 dňoch).</summary>
    public static void Odstran(long id) => new ObjektService().Odstran(id);

    public static void Obnov(long id) => new ObjektService().Obnov(id);

    // ------------------------------------------------------------------ „Týka sa“ (väzby patri)

    private const string SqlNaviazani = """
        SELECT v.z_id, o.id, o.typ, o.nazov, coalesce(o.farba, r.farba, ''), coalesce(r.nazov, ''),
               CASE WHEN o.odstranene IS NOT NULL OR r.odstranene IS NOT NULL THEN 1 ELSE 0 END
        FROM vazba v
        JOIN objekt o ON o.id = v.do_id
        LEFT JOIN objekt r ON r.id = o.rodic_id
        """;

    private static Naviazanie Precitaj(SqliteDataReader c)
    {
        var typ = c.GetString(2);
        var farba = c.GetString(4);
        var popis = typ is TypObjektu.Predmet or TypObjektu.Cast ? c.GetString(5) : TypObjektu.JeSkupina(typ) ? TypObjektu.Nazov(typ) : "";
        return new Naviazanie(c.GetInt64(1), typ, c.GetString(3), farba.Length > 0 ? farba : FarbaSiva, popis, c.GetInt64(6) == 1);
    }

    /// <summary>K čomu záznam patrí (predmety, oblasti, časti, udalosť, úloha) – aj odstránené, s príznakom.</summary>
    public static List<Naviazanie> Naviazania(long id)
    {
        using var db = Databaza.Otvor();
        return Naviazania(db, id);
    }

    internal static List<Naviazanie> Naviazania(SqliteConnection db, long id)
    {
        var vysledok = new List<Naviazanie>();
        using var prikaz = Databaza.Prikaz(db, SqlNaviazani + " WHERE v.typ = $p AND v.z_id = $z ORDER BY v.id",
            ("$p", TypVazby.Patri), ("$z", id));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            vysledok.Add(Precitaj(citac));
        }

        return vysledok;
    }

    /// <summary>Naviazania všetkých záznamov daného typu naraz (zoznam úloh, poznámok).</summary>
    internal static Dictionary<long, List<Naviazanie>> NaviazaniaTypu(SqliteConnection db, string typZaznamu)
    {
        var vysledok = new Dictionary<long, List<Naviazanie>>();
        using var prikaz = Databaza.Prikaz(db,
            SqlNaviazani + " JOIN objekt z ON z.id = v.z_id WHERE v.typ = $p AND z.typ = $t ORDER BY v.id",
            ("$p", TypVazby.Patri), ("$t", typZaznamu));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            var z = citac.GetInt64(0);
            if (!vysledok.TryGetValue(z, out var zoznam))
            {
                vysledok[z] = zoznam = new List<Naviazanie>();
            }

            zoznam.Add(Precitaj(citac));
        }

        return vysledok;
    }

    /// <summary>Nastaví, k čomu záznam patrí (nahradí doterajšie naviazania). Neplatné ciele a samého seba vynechá.</summary>
    internal static void NastavNaviazania(SqliteConnection db, long id, IEnumerable<long> ciele)
    {
        Databaza.Vykonaj(db, "DELETE FROM vazba WHERE z_id = $z AND typ = $p", ("$z", id), ("$p", TypVazby.Patri));
        var teraz = Databaza.Teraz();
        foreach (var ciel in ciele.Distinct().Where(c => c != id))
        {
            Databaza.Vykonaj(db, """
                INSERT OR IGNORE INTO vazba(z_id, do_id, typ, vytvorene)
                SELECT $z, id, $p, $c FROM objekt WHERE id = $d
                """, ("$z", id), ("$d", ciel), ("$p", TypVazby.Patri), ("$c", teraz));
        }
    }

    /// <summary>Id záznamov, ktoré patria k niektorému z objektov (napr. k predmetu alebo k oblasti a jej predmetom).</summary>
    internal static HashSet<long> ZaznamyPre(SqliteConnection db, IReadOnlyCollection<long> ciele)
    {
        var vysledok = new HashSet<long>();
        if (ciele.Count == 0)
        {
            return vysledok;
        }

        var zoznam = string.Join(",", ciele.Select(c => c.ToString(CultureInfo.InvariantCulture)));
        using var prikaz = Databaza.Prikaz(db, $"SELECT DISTINCT z_id FROM vazba WHERE typ = $p AND do_id IN ({zoznam})", ("$p", TypVazby.Patri));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            vysledok.Add(citac.GetInt64(0));
        }

        return vysledok;
    }

    /// <summary>Objekt ako naviazanie (na zobrazenie čipu pred uložením).</summary>
    public static Naviazanie? Objekt(long id)
    {
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db, """
            SELECT o.id, o.id, o.typ, o.nazov, coalesce(o.farba, r.farba, ''), coalesce(r.nazov, ''),
                   CASE WHEN o.odstranene IS NOT NULL OR r.odstranene IS NOT NULL THEN 1 ELSE 0 END
            FROM objekt o
            LEFT JOIN objekt r ON r.id = o.rodic_id
            WHERE o.id = $id
            """, ("$id", id));
        using var citac = prikaz.ExecuteReader();
        return citac.Read() ? Precitaj(citac) : null;
    }
}
