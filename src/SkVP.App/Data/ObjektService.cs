using System.Globalization;
using Microsoft.Data.Sqlite;
using SkVP.Models;

namespace SkVP.Data;

/// <summary>Práca s objektmi, ich poradím, obsahom a väzbami.</summary>
public sealed class ObjektService
{
    private const string Stlpce =
        "o.id, o.typ, o.rodic_id, o.poradie, o.nazov, o.farba, o.stav, o.upravene, COALESCE(c.dlzka, 0)";

    private const string Z = "FROM objekt o LEFT JOIN dokument c ON c.objekt_id = o.id";

    // ---- čítanie ----

    public List<Objekt> Oblasti() =>
        Zoznam($"SELECT {Stlpce} {Z} WHERE o.typ = $t AND o.odstranene IS NULL ORDER BY o.poradie, o.id",
            ("$t", TypObjektu.Oblast));

    public List<Objekt> Deti(long rodicId) =>
        Zoznam($"SELECT {Stlpce} {Z} WHERE o.rodic_id = $r AND o.odstranene IS NULL ORDER BY o.poradie, o.id",
            ("$r", rodicId));

    /// <summary>Všetky platné objekty (bez odstránených a bez detí odstránených rodičov).</summary>
    public List<Objekt> Vsetky() =>
        Zoznam($"""
            SELECT {Stlpce} {Z}
            LEFT JOIN objekt r ON r.id = o.rodic_id
            WHERE o.odstranene IS NULL AND (o.rodic_id IS NULL OR r.odstranene IS NULL)
            ORDER BY o.typ, o.poradie, o.id
            """);

    public Objekt? Nacitaj(long id) =>
        Zoznam($"SELECT {Stlpce} {Z} WHERE o.id = $id", ("$id", id)).FirstOrDefault();

    // ---- zmeny ----

    /// <summary>Pridá objekt na koniec (alebo na danú pozíciu medzi súrodencami).</summary>
    public Objekt Pridaj(string typ, long? rodicId, string nazov, string? farba = null, int? pozicia = null)
    {
        nazov = Over(nazov);
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        var teraz = Databaza.Teraz();
        var surodenci = IdSurodencov(db, typ, rodicId);
        var index = Math.Clamp(pozicia ?? surodenci.Count, 0, surodenci.Count);
        var id = Convert.ToInt64(Databaza.Skalar(db, """
            INSERT INTO objekt(typ, rodic_id, poradie, nazov, farba, vytvorene, upravene)
            VALUES($t, $r, 0, $n, $f, $c, $c);
            SELECT last_insert_rowid();
            """, ("$t", typ), ("$r", rodicId), ("$n", nazov), ("$f", farba), ("$c", teraz)), CultureInfo.InvariantCulture);
        surodenci.Insert(index, id);
        Precisluj(db, surodenci);
        transakcia.Commit();
        return Nacitaj(id)!;
    }

    public void Premenuj(long id, string nazov) =>
        Zmen(id, "nazov = $h", Over(nazov));

    public void NastavFarbu(long id, string? farba) => Zmen(id, "farba = $h", farba);

    public void NastavStav(long id, string? stav) => Zmen(id, "stav = $h", stav);

    /// <summary>Posunie objekt medzi súrodencami o jedno miesto vyššie (-1) alebo nižšie (+1). Vráti false na okraji.</summary>
    public bool Posun(long id, int smer)
    {
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        var (typ, rodic) = TypARodic(db, id);
        var surodenci = IdSurodencov(db, typ, rodic);
        var index = surodenci.IndexOf(id);
        var novy = index + Math.Sign(smer);
        if (index < 0 || novy < 0 || novy >= surodenci.Count)
        {
            return false;
        }

        (surodenci[index], surodenci[novy]) = (surodenci[novy], surodenci[index]);
        Precisluj(db, surodenci);
        transakcia.Commit();
        return true;
    }

    /// <summary>Presunie objekt k inému rodičovi (napr. predmet do inej oblasti) na danú pozíciu.</summary>
    public void Presun(long id, long? novyRodic, int pozicia)
    {
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        var (typ, staryRodic) = TypARodic(db, id);
        var stari = IdSurodencov(db, typ, staryRodic);
        stari.Remove(id);
        Precisluj(db, stari);

        Databaza.Vykonaj(db, "UPDATE objekt SET rodic_id = $r, upravene = $c WHERE id = $id",
            ("$r", novyRodic), ("$c", Databaza.Teraz()), ("$id", id));
        var novi = IdSurodencov(db, typ, novyRodic);
        novi.Remove(id);
        novi.Insert(Math.Clamp(pozicia, 0, novi.Count), id);
        Precisluj(db, novi);
        transakcia.Commit();
    }

    /// <summary>Odstráni objekt (do koša – dá sa vrátiť späť pomocou <see cref="Obnov"/>).</summary>
    public void Odstran(long id) => Zmen(id, "odstranene = $h", Databaza.Teraz());

    public void Obnov(long id) => Zmen(id, "odstranene = $h", null);

    /// <summary>Natrvalo vymaže objekty odstránené pred viac ako zadaným počtom dní.</summary>
    public int VyprazdniKos(int dni)
    {
        using var db = Databaza.Otvor();
        var hranica = DateTime.Now.AddDays(-dni).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
        return Databaza.Vykonaj(db, "DELETE FROM objekt WHERE odstranene IS NOT NULL AND odstranene < $h", ("$h", hranica));
    }

    // ---- väzby (čokoľvek s čímkoľvek) ----

    public long Prepoj(long zId, long doId, string typ, string? poznamka = null)
    {
        if (zId == doId)
        {
            throw new SkvpException("Objekt nemožno prepojiť sám so sebou.");
        }

        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, """
            INSERT INTO vazba(z_id, do_id, typ, poznamka, vytvorene) VALUES($z, $d, $t, $p, $c)
            ON CONFLICT(z_id, do_id, typ) DO UPDATE SET poznamka = excluded.poznamka
            """, ("$z", zId), ("$d", doId), ("$t", typ), ("$p", poznamka), ("$c", Databaza.Teraz()));
        return Convert.ToInt64(Databaza.Skalar(db, "SELECT id FROM vazba WHERE z_id = $z AND do_id = $d AND typ = $t",
            ("$z", zId), ("$d", doId), ("$t", typ)), CultureInfo.InvariantCulture);
    }

    public void ZrusVazbu(long vazbaId)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "DELETE FROM vazba WHERE id = $id", ("$id", vazbaId));
    }

    /// <summary>Všetko, s čím je objekt prepojený – v oboch smeroch.</summary>
    public List<Suvislost> Suvislosti(long id)
    {
        var platne = Vsetky().ToDictionary(o => o.Id);
        var vysledok = new List<Suvislost>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db,
            "SELECT id, z_id, do_id, typ, poznamka FROM vazba WHERE z_id = $id OR do_id = $id ORDER BY id", ("$id", id));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            var zId = citac.GetInt64(1);
            var doId = citac.GetInt64(2);
            var spatna = doId == id;
            if (platne.TryGetValue(spatna ? zId : doId, out var druhy))
            {
                vysledok.Add(new Suvislost(citac.GetInt64(0), druhy, citac.GetString(3), spatna,
                    citac.IsDBNull(4) ? null : citac.GetString(4)));
            }
        }

        return vysledok;
    }

    // ---- vnútorné ----

    private static string Over(string nazov)
    {
        nazov = (nazov ?? "").Trim();
        if (nazov.Length == 0)
        {
            throw new SkvpException("Názov nemôže byť prázdny.");
        }

        return nazov.Length > 300 ? nazov[..300] : nazov;
    }

    private static void Zmen(long id, string priradenie, object? hodnota)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, $"UPDATE objekt SET {priradenie}, upravene = $c WHERE id = $id",
            ("$h", hodnota), ("$c", Databaza.Teraz()), ("$id", id));
    }

    private static (string Typ, long? Rodic) TypARodic(SqliteConnection db, long id)
    {
        using var prikaz = Databaza.Prikaz(db, "SELECT typ, rodic_id FROM objekt WHERE id = $id", ("$id", id));
        using var citac = prikaz.ExecuteReader();
        if (!citac.Read())
        {
            throw new SkvpException("Záznam sa nenašiel – pravdepodobne bol medzitým odstránený.");
        }

        return (citac.GetString(0), citac.IsDBNull(1) ? null : citac.GetInt64(1));
    }

    private static List<long> IdSurodencov(SqliteConnection db, string typ, long? rodic)
    {
        var sql = rodic is null
            ? "SELECT id FROM objekt WHERE typ = $t AND rodic_id IS NULL AND odstranene IS NULL ORDER BY poradie, id"
            : "SELECT id FROM objekt WHERE typ = $t AND rodic_id = $r AND odstranene IS NULL ORDER BY poradie, id";
        var zoznam = new List<long>();
        using var prikaz = Databaza.Prikaz(db, sql, ("$t", typ), ("$r", rodic));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            zoznam.Add(citac.GetInt64(0));
        }

        return zoznam;
    }

    private static void Precisluj(SqliteConnection db, List<long> poradie)
    {
        for (var i = 0; i < poradie.Count; i++)
        {
            Databaza.Vykonaj(db, "UPDATE objekt SET poradie = $p WHERE id = $id", ("$p", i), ("$id", poradie[i]));
        }
    }

    private static List<Objekt> Zoznam(string sql, params (string, object?)[] parametre)
    {
        var vysledok = new List<Objekt>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db, sql, parametre);
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            vysledok.Add(new Objekt
            {
                Id = citac.GetInt64(0),
                Typ = citac.GetString(1),
                RodicId = citac.IsDBNull(2) ? null : citac.GetInt64(2),
                Poradie = citac.GetInt32(3),
                Nazov = citac.GetString(4),
                Farba = citac.IsDBNull(5) ? null : citac.GetString(5),
                Stav = citac.IsDBNull(6) ? null : citac.GetString(6),
                Upravene = DateTime.TryParse(citac.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.None, out var u) ? u : DateTime.MinValue,
                DlzkaTextu = citac.GetInt32(8),
            });
        }

        return vysledok;
    }
}
