using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Výsledok jedného nástroja: či je všetko v poriadku a riadky protokolu.</summary>
public sealed record VysledokKontroly(string Nastroj, bool Ok, List<string> Riadky)
{
    public string Text => $"{Nastroj}: {(Ok ? "v poriadku" : "nájdené problémy")}\n" + string.Join("\n", Riadky.Select(r => "  " + r));
}

/// <summary>Počet záznamov v tabuľke databázy.</summary>
public sealed record TabulkaDatabazy(string Nazov, string Popis, long Pocet);

/// <summary>
/// Správa databázy (ako v EviTA): rýchla a úplná kontrola integrity, kontrola väzieb, kontrola dát (osnovy, ktoré sa
/// nedajú prečítať, nesedí kontrolný súčet, duplicitné predmety…), optimalizácia, obnova indexov, kompaktovanie,
/// oprava väzieb a záchrana dát z poškodenej databázy.
/// </summary>
public static class DiagnostikaService
{
    private const string Upozornenie = "UPOZORNENIE: ";

    /// <summary>Kľúč v nastaveniach: čas, nástroj a výsledok poslednej kontroly.</summary>
    public const string KlucPoslednaKontrola = "kontrola.posledna";

    /// <summary>Tabuľky v poradí, v akom sa kopírujú pri záchrane (najprv tie, na ktoré sa odkazuje).</summary>
    private static readonly (string Nazov, string Popis)[] Tabulky =
    {
        ("nastavenie", "nastavenia"),
        ("objekt", "oblasti, predmety a ďalšie objekty"),
        ("dokument", "dokumenty osnov a charakteristík"),
        ("verzia", "archív verzií osnov"),
        ("vazba", "súvislosti medzi predmetmi"),
        ("archiv_rok", "archívy školských rokov"),
        ("archiv_polozka", "osnovy v archívoch školských rokov"),
    };

    public static VysledokKontroly RychlaKontrola() => Kontrola("PRAGMA quick_check;", "Rýchla kontrola");

    public static VysledokKontroly UplnaKontrola() => Kontrola("PRAGMA integrity_check;", "Úplná kontrola integrity");

    /// <summary>Odkazy medzi tabuľkami: predmety bez oblasti, dokumenty a verzie bez predmetu, súvislosti na zmazané predmety.</summary>
    public static VysledokKontroly KontrolaVazieb()
    {
        var riadky = new List<string>();
        using var db = Databaza.Otvor();
        using (var prikaz = Databaza.Prikaz(db, "PRAGMA foreign_key_check;"))
        using (var citac = prikaz.ExecuteReader())
        {
            while (citac.Read())
            {
                riadky.Add($"{Upozornenie}tabuľka {citac.GetString(0)}, riadok {(citac.IsDBNull(1) ? "?" : citac.GetInt64(1).ToString(CultureInfo.InvariantCulture))} odkazuje na neexistujúci záznam v tabuľke {citac.GetString(2)}.");
            }
        }

        foreach (var (popis, sql) in Siroty)
        {
            var pocet = Pocet(db, sql);
            if (pocet > 0)
            {
                riadky.Add($"{Upozornenie}{popis}: {pocet}.");
            }
        }

        var ok = riadky.Count == 0;
        if (ok)
        {
            riadky.Add("Všetky odkazy medzi oblasťami, predmetmi, osnovami, archívom a súvislosťami sú v poriadku.");
        }
        else
        {
            riadky.Add("Chybné odkazy opraví tlačidlo Opraviť väzby (pred opravou sa urobí záloha).");
        }

        return new VysledokKontroly("Kontrola väzieb", ok, riadky);
    }

    /// <summary>Záznamy bez nadradeného záznamu (popis, SQL na počet; oprava zmaže ten istý výber).</summary>
    private static readonly (string Popis, string Pocet)[] Siroty =
    {
        ("predmety bez existujúcej oblasti", "SELECT count(*) FROM objekt WHERE typ = 'predmet' AND (rodic_id IS NULL OR rodic_id NOT IN (SELECT id FROM objekt))"),
        ("dokumenty bez predmetu alebo oblasti", "SELECT count(*) FROM dokument WHERE objekt_id NOT IN (SELECT id FROM objekt)"),
        ("verzie v archíve bez predmetu", "SELECT count(*) FROM verzia WHERE objekt_id NOT IN (SELECT id FROM objekt)"),
        ("súvislosti na neexistujúci predmet", "SELECT count(*) FROM vazba WHERE z_id NOT IN (SELECT id FROM objekt) OR do_id NOT IN (SELECT id FROM objekt)"),
        ("osnovy archívu roka bez archívu", "SELECT count(*) FROM archiv_polozka WHERE archiv_id NOT IN (SELECT id FROM archiv_rok)"),
    };

    /// <summary>Zmaže záznamy bez nadradeného záznamu (chybné väzby). Vráti počet zmazaných.</summary>
    public static VysledokKontroly OpravVazby()
    {
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        var spolu = 0;
        spolu += Databaza.Vykonaj(db, "DELETE FROM dokument WHERE objekt_id NOT IN (SELECT id FROM objekt)");
        spolu += Databaza.Vykonaj(db, "DELETE FROM verzia WHERE objekt_id NOT IN (SELECT id FROM objekt)");
        spolu += Databaza.Vykonaj(db, "DELETE FROM vazba WHERE z_id NOT IN (SELECT id FROM objekt) OR do_id NOT IN (SELECT id FROM objekt)");
        spolu += Databaza.Vykonaj(db, "DELETE FROM archiv_polozka WHERE archiv_id NOT IN (SELECT id FROM archiv_rok)");

        // predmety bez oblasti sa nemažú – presunú sa do oblasti „Nezaradené predmety“
        var siroty = Pocet(db, Siroty[0].Pocet);
        if (siroty > 0)
        {
            var oblast = Databaza.Skalar(db, "SELECT id FROM objekt WHERE typ = 'oblast' AND nazov = 'Nezaradené predmety' LIMIT 1") as long?;
            if (oblast is null)
            {
                var poradie = Pocet(db, "SELECT coalesce(max(poradie), 0) + 1 FROM objekt WHERE typ = 'oblast'");
                Databaza.Vykonaj(db, "INSERT INTO objekt(typ, rodic_id, poradie, nazov, vytvorene, upravene) VALUES('oblast', NULL, $p, 'Nezaradené predmety', $t, $t)",
                    ("$p", poradie), ("$t", Databaza.Teraz()));
                oblast = Convert.ToInt64(Databaza.Skalar(db, "SELECT last_insert_rowid()"), CultureInfo.InvariantCulture);
            }

            spolu += Databaza.Vykonaj(db, "UPDATE objekt SET rodic_id = $o WHERE typ = 'predmet' AND (rodic_id IS NULL OR rodic_id NOT IN (SELECT id FROM objekt))", ("$o", oblast));
        }

        transakcia.Commit();
        return new VysledokKontroly("Oprava väzieb", true, new List<string>
        {
            spolu == 0 ? "Nebolo čo opravovať." : $"Opravených záznamov: {spolu}." + (siroty > 0 ? $" Predmety bez oblasti ({siroty}) sú v oblasti „Nezaradené predmety“." : ""),
        });
    }

    /// <summary>Kontrola údajov: osnovy, ktoré sa nedajú otvoriť, nesedí kontrolný súčet, duplicitné predmety, prázdne oblasti…</summary>
    public static VysledokKontroly KontrolaDat()
    {
        var riadky = new List<string>();
        var problem = false;
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var oblasti = sluzba.Oblasti();
        var predmety = oblasti.SelectMany(o => sluzba.Deti(o.Id).Where(p => p.Typ == TypObjektu.Predmet).Select(p => (Oblast: o, Predmet: p))).ToList();
        riadky.Add($"Oblastí: {oblasti.Count}, predmetov: {predmety.Count}.");

        // duplicitné predmety v oblasti
        foreach (var skupina in predmety.GroupBy(x => (x.Oblast.Id, Kluc: ImportOsnov.Kluc(x.Predmet.Nazov))).Where(g => g.Count() > 1))
        {
            problem = true;
            riadky.Add($"{Upozornenie}predmet „{skupina.First().Predmet.Nazov}“ je v oblasti {skupina.First().Oblast.Nazov} {skupina.Count()}-krát.");
        }

        foreach (var oblast in oblasti.Where(o => !predmety.Any(p => p.Oblast.Id == o.Id)))
        {
            riadky.Add($"Oblasť {oblast.Nazov} nemá žiadne predmety (len informácia).");
        }

        // dokumenty: čitateľnosť a kontrolný súčet
        var necitatelne = new List<string>();
        var sucet = new List<string>();
        using (var db = Databaza.Otvor())
        using (var prikaz = Databaza.Prikaz(db, "SELECT d.objekt_id, o.nazov, d.data, d.sha256 FROM dokument d JOIN objekt o ON o.id = d.objekt_id"))
        using (var citac = prikaz.ExecuteReader())
        {
            while (citac.Read())
            {
                var nazov = citac.GetString(1);
                var data = (byte[])citac.GetValue(2);
                if (!Convert.ToHexString(SHA256.HashData(data)).Equals(citac.GetString(3), StringComparison.OrdinalIgnoreCase))
                {
                    sucet.Add(nazov);
                }

                try
                {
                    using var prud = new MemoryStream(data, false);
                    DocxCitac.Nacitaj(prud);
                }
                catch (Exception)
                {
                    necitatelne.Add(nazov);
                }
            }
        }

        if (necitatelne.Count > 0)
        {
            problem = true;
            riadky.Add($"{Upozornenie}osnovy, ktoré sa nedajú otvoriť ({necitatelne.Count}): {string.Join(", ", necitatelne.Take(15))} – obnovte staršiu verziu z archívu osnovy.");
        }

        if (sucet.Count > 0)
        {
            problem = true;
            riadky.Add($"{Upozornenie}nesedí kontrolný súčet dokumentu ({sucet.Count}): {string.Join(", ", sucet.Take(15))}.");
        }

        var bezOsnovy = predmety.Where(p => !dokumenty.Existuje(p.Predmet.Id)).ToList();
        riadky.Add($"Predmety bez osnovy: {bezOsnovy.Count} (len informácia)" +
                   (bezOsnovy.Count > 0 ? $" – {string.Join(", ", bezOsnovy.Take(10).Select(p => p.Predmet.Nazov))}{(bezOsnovy.Count > 10 ? " …" : "")}." : "."));
        using (var db = Databaza.Otvor())
        {
            riadky.Add($"Verzií v archíve osnov: {Pocet(db, "SELECT count(*) FROM verzia")}, archívov školských rokov: {Pocet(db, "SELECT count(*) FROM archiv_rok")}.");
            var buduce = Pocet(db, "SELECT count(*) FROM objekt WHERE upravene > $t", ("$t", DateTime.Now.AddMinutes(5).ToString("s", CultureInfo.InvariantCulture)));
            if (buduce > 0)
            {
                problem = true;
                riadky.Add($"{Upozornenie}dátum úpravy je v budúcnosti: {buduce} (skontrolujte dátum a čas počítača).");
            }
        }

        if (!problem)
        {
            riadky.Add("Údaje sú v poriadku.");
        }

        ZapisKontrolu("Kontrola dát", !problem);
        return new VysledokKontroly("Kontrola dát", !problem, riadky);
    }

    public static VysledokKontroly Optimalizuj()
    {
        Vykonaj("PRAGMA optimize; ANALYZE;");
        return new VysledokKontroly("Optimalizácia", true, new List<string> { "Štatistiky databázy pre rýchle vyhľadávanie sú aktualizované." });
    }

    public static VysledokKontroly ObnovIndexy()
    {
        Vykonaj("REINDEX;");
        return new VysledokKontroly("Obnova indexov", true, new List<string> { "Všetky indexy sú znovu vytvorené." });
    }

    public static VysledokKontroly Kompaktuj()
    {
        var (pred, po) = ZalohaService.Zmensi();
        return new VysledokKontroly("Kompaktovanie", true, new List<string> { $"Veľkosť pred: {Velkost(pred)}, po: {Velkost(po)}." });
    }

    /// <summary>Počty záznamov v tabuľkách databázy.</summary>
    public static List<TabulkaDatabazy> Prehlad()
    {
        using var db = Databaza.Otvor();
        return Tabulky.Select(t => new TabulkaDatabazy(t.Nazov, t.Popis, Pocet(db, $"SELECT count(*) FROM \"{t.Nazov}\""))).ToList();
    }

    /// <summary>Verzia dátovej štruktúry (PRAGMA user_version).</summary>
    public static int VerziaSchemy()
    {
        using var db = Databaza.Otvor();
        return (int)Pocet(db, "PRAGMA user_version;");
    }

    /// <summary>Posledná kontrola („23. 10. 2026 17:40 – Úplná kontrola integrity: v poriadku“), alebo null.</summary>
    public static string? PoslednaKontrola()
    {
        var zaznam = Databaza.Citaj(KlucPoslednaKontrola)?.Split('|');
        if (zaznam is not { Length: 3 } || !DateTime.TryParse(zaznam[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var cas))
        {
            return null;
        }

        return $"{cas.ToString("d. M. yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))} – {zaznam[1]}: {(zaznam[2] == "ok" ? "v poriadku" : "nájdené problémy")}";
    }

    /// <summary>
    /// Záchrana dát z poškodenej databázy: prenesie všetky čitateľné záznamy do novej databázy a tou nahradí pôvodnú.
    /// Pôvodný súbor sa predtým skopíruje do priečinka záloh.
    /// </summary>
    public static VysledokKontroly Zachran()
    {
        var riadky = new List<string>();
        Directory.CreateDirectory(Databaza.PriecinokZaloh);
        SqliteConnection.ClearAllPools();
        var kopia = Path.Combine(Databaza.PriecinokZaloh, $"SkVP_pred_zachranou_{DateTime.Now:yyyy-MM-dd_HHmmss}.db");
        File.Copy(Databaza.Cesta, kopia, overwrite: true);
        if (File.Exists(Databaza.Cesta + "-wal"))
        {
            File.Copy(Databaza.Cesta + "-wal", kopia + "-wal", overwrite: true);
        }

        riadky.Add($"Pôvodný súbor je uložený: {kopia}");

        var nova = Path.Combine(Databaza.Priecinok, "skvp_zachrana.db");
        ZmazSubory(nova);
        Databaza.Inicializuj(nova);

        var prenesene = 0;
        var strateneSpolu = 0;
        using (var spojenie = Databaza.Otvor(nova))
        {
            Databaza.Vykonaj(spojenie, "PRAGMA foreign_keys = OFF;");
            Databaza.Vykonaj(spojenie, "ATTACH DATABASE $cesta AS stara;", ("$cesta", Databaza.Cesta));
            foreach (var (tabulka, _) in Tabulky)
            {
                var (pocet, stratene, sprava) = PrenesTabulku(spojenie, tabulka);
                prenesene += pocet;
                strateneSpolu += stratene;
                riadky.Add(sprava);
            }

            Databaza.Vykonaj(spojenie, "DETACH DATABASE stara;");
            Databaza.Vykonaj(spojenie, "PRAGMA wal_checkpoint(TRUNCATE);");
        }

        SqliteConnection.ClearAllPools();
        ZmazSubory(Databaza.Cesta);
        File.Move(nova, Databaza.Cesta);
        ZmazSubory(nova);
        Databaza.Inicializuj();

        var vazby = KontrolaVazieb();
        riadky.Add($"Prenesených záznamov: {prenesene}, nečitateľných: {strateneSpolu}.");
        if (!vazby.Ok)
        {
            riadky.AddRange(vazby.Riadky);
        }

        return new VysledokKontroly("Záchrana dát", strateneSpolu == 0 && vazby.Ok, riadky);
    }

    // ---- pomocné ----

    private static VysledokKontroly Kontrola(string sql, string nastroj)
    {
        var riadky = new List<string>();
        using (var db = Databaza.Otvor())
        using (var prikaz = Databaza.Prikaz(db, sql))
        using (var citac = prikaz.ExecuteReader())
        {
            while (citac.Read())
            {
                riadky.Add(citac.GetString(0));
            }
        }

        var ok = riadky is ["ok"];
        ZapisKontrolu(nastroj, ok);
        return new VysledokKontroly(nastroj, ok, ok
            ? new List<string> { "Databáza je v poriadku." }
            : riadky.Select(r => Upozornenie + r).Prepend("Databáza môže byť poškodená. Odporúča sa obnova zo zálohy alebo záchrana dát.").ToList());
    }

    private static void ZapisKontrolu(string nastroj, bool ok)
    {
        try
        {
            Databaza.Zapis(KlucPoslednaKontrola, $"{DateTime.Now:yyyy-MM-ddTHH:mm:ss}|{nastroj}|{(ok ? "ok" : "chyba")}");
        }
        catch (SqliteException)
        {
            // záznam o kontrole nie je dôležitý
        }
    }

    private static (int Pocet, int Stratene, string Sprava) PrenesTabulku(SqliteConnection spojenie, string tabulka)
    {
        var stlpceNove = Stlpce(spojenie, "main", tabulka);
        List<string> stlpceStare;
        try
        {
            stlpceStare = Stlpce(spojenie, "stara", tabulka);
        }
        catch (SqliteException)
        {
            return (0, 0, $"Tabuľka {tabulka}: nedá sa prečítať, preskočená.");
        }

        if (stlpceStare.Count == 0)
        {
            return (0, 0, $"Tabuľka {tabulka}: v pôvodnej databáze neexistuje.");
        }

        var zoznam = string.Join(", ", stlpceNove.Where(stlpceStare.Contains).Select(s => $"\"{s}\""));
        var hromadne = $"INSERT OR IGNORE INTO main.\"{tabulka}\" ({zoznam}) SELECT {zoznam} FROM stara.\"{tabulka}\"";
        try
        {
            var pocet = Databaza.Vykonaj(spojenie, hromadne + ";");
            return (pocet, 0, $"Tabuľka {tabulka}: prenesených {pocet} záznamov.");
        }
        catch (SqliteException)
        {
            // tabuľka je čiastočne poškodená – záznam po zázname
        }

        long najvacsi;
        try
        {
            najvacsi = Databaza.Skalar(spojenie, $"SELECT MAX(rowid) FROM stara.\"{tabulka}\";") is long l ? l : 0;
        }
        catch (SqliteException)
        {
            return (0, 0, $"Tabuľka {tabulka}: nedá sa prečítať, preskočená.");
        }

        var prenesene = 0;
        var stratene = 0;
        for (long rowid = 1; rowid <= najvacsi; rowid++)
        {
            try
            {
                prenesene += Databaza.Vykonaj(spojenie, hromadne + " WHERE rowid = $r;", ("$r", rowid));
            }
            catch (SqliteException)
            {
                stratene++;
            }
        }

        return (prenesene, stratene, $"Tabuľka {tabulka}: prenesených {prenesene} záznamov, nečitateľných {stratene}.");
    }

    private static List<string> Stlpce(SqliteConnection spojenie, string schema, string tabulka)
    {
        var stlpce = new List<string>();
        using var prikaz = Databaza.Prikaz(spojenie, $"PRAGMA {schema}.table_info(\"{tabulka}\");");
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            stlpce.Add(citac.GetString(1));
        }

        return stlpce;
    }

    private static long Pocet(SqliteConnection db, string sql, params (string, object?)[] parametre) =>
        Convert.ToInt64(Databaza.Skalar(db, sql, parametre) ?? 0L, CultureInfo.InvariantCulture);

    private static void Vykonaj(string sql)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, sql);
    }

    private static void ZmazSubory(string cesta)
    {
        foreach (var pripona in new[] { "", "-wal", "-shm" })
        {
            if (File.Exists(cesta + pripona))
            {
                File.Delete(cesta + pripona);
            }
        }
    }

    public static string Velkost(long bajty) =>
        bajty < 1024 * 1024 ? $"{bajty / 1024.0:0.0} kB" : $"{bajty / 1024.0 / 1024.0:0.0} MB";
}
