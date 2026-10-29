using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SkVP.Data;

/// <summary>
/// Databáza programu (SQLite). Všetko, čo program eviduje, je <b>objekt</b> (vzdelávacia oblasť, predmet, neskôr
/// súbor, téma, štandard …) a ľubovoľné dva objekty sa dajú prepojiť <b>väzbou</b> – tak sa dá čokoľvek dať
/// do súvislosti s čímkoľvek. Osnovy sú uložené ako dokumenty Word priamo v databáze (tabuľka dokument),
/// staršie verzie v archíve (tabuľka verzia) – záloha databázy tak obsahuje všetko.
/// </summary>
public static class Databaza
{
    public const int VerziaSchemy = 3;

    /// <summary>Priečinok s databázou (pri samoteste dočasný – premenná SKVP_DATA_DIR).</summary>
    public static string Priecinok =>
        Environment.GetEnvironmentVariable("SKVP_DATA_DIR") is { Length: > 0 } vlastny
            ? vlastny
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SkVP", "Data");

    public static string Cesta => Path.Combine(Priecinok, "skvp.db");

    /// <summary>Kľúč nastavenia: vlastný priečinok záloh (USB kľúč, sieťový disk, OneDrive …).</summary>
    public const string KlucPriecinokZaloh = "zaloha.priecinok";

    /// <summary>Predvolený priečinok záloh (Dokumenty\SkVP\Zálohy, pri samoteste v dočasnom priečinku).</summary>
    public static string PredvolenyPriecinokZaloh =>
        Environment.GetEnvironmentVariable("SKVP_DATA_DIR") is { Length: > 0 }
            ? Path.Combine(Priecinok, "Zalohy")
            : Path.Combine(PriecinokDokumentov, "Zálohy");

    /// <summary>Priečinok záloh – vlastný z nastavení (Servis), inak predvolený.</summary>
    public static string PriecinokZaloh
    {
        get
        {
            try
            {
                if (Existuje && Citaj(KlucPriecinokZaloh) is { Length: > 0 } vlastny)
                {
                    return vlastny;
                }
            }
            catch (Exception)
            {
                // databáza ešte nie je – predvolený priečinok
            }

            return PredvolenyPriecinokZaloh;
        }
    }

    /// <summary>Predvolený priečinok pre exporty (Dokumenty\SkVP).</summary>
    public static string PriecinokDokumentov =>
        Environment.GetEnvironmentVariable("SKVP_DATA_DIR") is { Length: > 0 }
            ? Path.Combine(Priecinok, "Export")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SkVP");

    public static bool Existuje => File.Exists(Cesta);

    public static SqliteConnection Otvor() => Otvor(Cesta);

    /// <summary>Spojenie s databázou v inom súbore (záchrana dát).</summary>
    public static SqliteConnection Otvor(string cesta)
    {
        var spojenie = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = cesta,
            ForeignKeys = true,
            Pooling = false,
        }.ToString());
        spojenie.Open();
        return spojenie;
    }

    /// <summary>Vytvorí databázu alebo ju prevedie na aktuálnu verziu schémy.</summary>
    public static void Inicializuj() => Inicializuj(Cesta);

    /// <summary>Vytvorí databázu v zadanom súbore alebo ju prevedie na aktuálnu verziu schémy.</summary>
    public static void Inicializuj(string cesta)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cesta))!);
        using var db = Otvor(cesta);
        Vykonaj(db, "PRAGMA journal_mode = WAL;");
        var verzia = Convert.ToInt32(Skalar(db, "PRAGMA user_version;") ?? 0L, CultureInfo.InvariantCulture);
        if (verzia > VerziaSchemy)
        {
            throw new SkvpException(
                "Databázu vytvorila novšia verzia programu SkVP. Nainštalujte najnovšiu verziu programu.");
        }

        using var transakcia = db.BeginTransaction();
        if (verzia < 1)
        {
            Vykonaj(db, """
                CREATE TABLE objekt(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    typ TEXT NOT NULL,
                    rodic_id INTEGER NULL REFERENCES objekt(id) ON DELETE CASCADE,
                    poradie INTEGER NOT NULL DEFAULT 0,
                    nazov TEXT NOT NULL,
                    farba TEXT NULL,
                    stav TEXT NULL,
                    vlastnosti TEXT NULL,
                    vytvorene TEXT NOT NULL,
                    upravene TEXT NOT NULL,
                    odstranene TEXT NULL
                );
                CREATE INDEX ix_objekt_rodic ON objekt(rodic_id, poradie);
                CREATE INDEX ix_objekt_typ ON objekt(typ);

                -- dokument Word (.docx) objektu – osnova predmetu, charakteristika oblasti
                CREATE TABLE dokument(
                    objekt_id INTEGER PRIMARY KEY REFERENCES objekt(id) ON DELETE CASCADE,
                    data BLOB NOT NULL,
                    text TEXT NOT NULL DEFAULT '',
                    dlzka INTEGER NOT NULL DEFAULT 0,
                    sha256 TEXT NOT NULL,
                    upravene TEXT NOT NULL
                );

                -- archív starších verzií dokumentov
                CREATE TABLE verzia(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    objekt_id INTEGER NOT NULL REFERENCES objekt(id) ON DELETE CASCADE,
                    data BLOB NOT NULL,
                    text TEXT NOT NULL DEFAULT '',
                    sha256 TEXT NOT NULL,
                    povodne_upravene TEXT NULL,
                    vytvorene TEXT NOT NULL,
                    dovod TEXT NULL
                );
                CREATE INDEX ix_verzia_objekt ON verzia(objekt_id, id);

                CREATE TABLE vazba(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    z_id INTEGER NOT NULL REFERENCES objekt(id) ON DELETE CASCADE,
                    do_id INTEGER NOT NULL REFERENCES objekt(id) ON DELETE CASCADE,
                    typ TEXT NOT NULL,
                    poznamka TEXT NULL,
                    vytvorene TEXT NOT NULL,
                    UNIQUE(z_id, do_id, typ),
                    CHECK(z_id <> do_id)
                );
                CREATE INDEX ix_vazba_do ON vazba(do_id);

                CREATE TABLE nastavenie(
                    kluc TEXT PRIMARY KEY,
                    hodnota TEXT NULL
                );
                """);
        }

        if (verzia < 2)
        {
            Vykonaj(db, """
                -- archív osnov za školský rok: snímka predmetov a osnov tak, ako boli v danom roku
                CREATE TABLE archiv_rok(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    skolsky_rok TEXT NOT NULL,
                    vytvorene TEXT NOT NULL,
                    priecinok TEXT NULL,
                    poznamka TEXT NULL
                );
                CREATE TABLE archiv_polozka(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    archiv_id INTEGER NOT NULL REFERENCES archiv_rok(id) ON DELETE CASCADE,
                    objekt_id INTEGER NULL,
                    typ TEXT NOT NULL,
                    oblast TEXT NOT NULL,
                    farba TEXT NULL,
                    poradie_oblasti INTEGER NOT NULL,
                    cislo INTEGER NOT NULL,
                    nazov TEXT NOT NULL,
                    stav TEXT NOT NULL,
                    dlzka INTEGER NOT NULL DEFAULT 0,
                    profilacie TEXT NULL,
                    platnost TEXT NULL,
                    rok_osnovy INTEGER NULL,
                    uroven TEXT NULL,
                    zmenena_v_roku INTEGER NOT NULL DEFAULT 0,
                    sha256 TEXT NULL,
                    data BLOB NULL
                );
                CREATE INDEX ix_archiv_polozka ON archiv_polozka(archiv_id, poradie_oblasti, cislo);
                """);
        }

        if (verzia < 3)
        {
            Vykonaj(db, """
                -- kalendár, úlohy a poznámky: každý záznam je objekt (typ udalost/uloha/poznamka), podrobnosti sú tu;
                -- k predmetom, oblastiam a častiam patria cez väzbu typu 'patri'
                CREATE TABLE typ_udalosti(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    nazov TEXT NOT NULL,
                    farba TEXT NOT NULL,
                    aktivny INTEGER NOT NULL DEFAULT 1,
                    poradie INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE udalost(
                    objekt_id INTEGER PRIMARY KEY REFERENCES objekt(id) ON DELETE CASCADE,
                    guid TEXT NOT NULL,
                    typ_id INTEGER NULL REFERENCES typ_udalosti(id) ON DELETE SET NULL,
                    zaciatok TEXT NOT NULL,
                    koniec TEXT NULL,
                    cely_den INTEGER NOT NULL DEFAULT 0,
                    opakovanie INTEGER NOT NULL DEFAULT 0,
                    opakovanie_do TEXT NULL,
                    pripomienka INTEGER NOT NULL DEFAULT 0,
                    stav INTEGER NOT NULL DEFAULT 0,
                    miesto TEXT NULL,
                    popis TEXT NULL
                );
                CREATE INDEX ix_udalost_zaciatok ON udalost(zaciatok);
                CREATE TABLE uloha(
                    objekt_id INTEGER PRIMARY KEY REFERENCES objekt(id) ON DELETE CASCADE,
                    termin TEXT NULL,
                    priorita INTEGER NOT NULL DEFAULT 1,
                    hotovo TEXT NULL,
                    zodpovedny TEXT NULL,
                    zdroj TEXT NULL,
                    popis TEXT NULL
                );
                CREATE INDEX ix_uloha_termin ON uloha(termin);
                CREATE TABLE poznamka(
                    objekt_id INTEGER PRIMARY KEY REFERENCES objekt(id) ON DELETE CASCADE,
                    text TEXT NOT NULL DEFAULT '',
                    pripnuta INTEGER NOT NULL DEFAULT 0,
                    datum TEXT NULL
                );
                CREATE INDEX ix_poznamka_datum ON poznamka(datum);
                CREATE INDEX ix_vazba_typ ON vazba(typ, do_id);

                -- archív školského roka: úlohy a poznámky k predmetom tak, ako boli v danom roku
                CREATE TABLE archiv_zaznam(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    archiv_id INTEGER NOT NULL REFERENCES archiv_rok(id) ON DELETE CASCADE,
                    druh TEXT NOT NULL,
                    nazov TEXT NOT NULL,
                    text TEXT NULL,
                    datum TEXT NULL,
                    stav TEXT NULL,
                    tyka_sa TEXT NULL
                );
                CREATE INDEX ix_archiv_zaznam ON archiv_zaznam(archiv_id);
                """);
            var poradie = 0;
            foreach (var (nazov, farba) in PredvoleneTypyUdalosti)
            {
                Vykonaj(db, "INSERT INTO typ_udalosti(nazov, farba, aktivny, poradie) VALUES($n, $f, 1, $p)",
                    ("$n", nazov), ("$f", farba), ("$p", poradie++));
            }
        }

        Vykonaj(db, $"PRAGMA user_version = {VerziaSchemy};");
        transakcia.Commit();
    }

    /// <summary>Typy udalostí v novej databáze (dajú sa upraviť v kalendári).</summary>
    public static readonly IReadOnlyList<(string Nazov, string Farba)> PredvoleneTypyUdalosti = new[]
    {
        ("Pedagogická rada", "#7C9CE0"),
        ("Predmetová komisia / metodické združenie", "#5FB3A1"),
        ("Termín k osnovám", "#E39B5B"),
        ("Porada", "#9AA5B8"),
        ("Školská akcia", "#D98BB4"),
        ("Rodičovské združenie", "#B79BD9"),
        ("Maturita", "#E07A7A"),
        ("Prijímacie skúšky", "#D4B04C"),
        ("Iné", "#94A3B8"),
    };

    // ---- nastavenia ----

    public static string? Citaj(string kluc)
    {
        using var db = Otvor();
        return Skalar(db, "SELECT hodnota FROM nastavenie WHERE kluc = $k", ("$k", kluc)) as string;
    }

    public static string Citaj(string kluc, string predvolene) => Citaj(kluc) ?? predvolene;

    /// <summary>Nastavenie uložené ako JSON. Chýbajúca, poškodená alebo nečitateľná hodnota = null (platia predvolené).</summary>
    public static T? CitajJson<T>(string kluc) where T : class
    {
        try
        {
            return Citaj(kluc) is { Length: > 0 } json ? JsonSerializer.Deserialize<T>(json) : null;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or SqliteException)
        {
            return null;
        }
    }

    public static void ZapisJson<T>(string kluc, T hodnota) => Zapis(kluc, JsonSerializer.Serialize(hodnota));

    public static void Zapis(string kluc, string? hodnota)
    {
        using var db = Otvor();
        Vykonaj(db, "INSERT INTO nastavenie(kluc, hodnota) VALUES($k, $h) ON CONFLICT(kluc) DO UPDATE SET hodnota = excluded.hodnota",
            ("$k", kluc), ("$h", hodnota));
    }

    // ---- pomocné funkcie ----

    public static string Teraz() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    public static int Vykonaj(SqliteConnection db, string sql, params (string Nazov, object? Hodnota)[] parametre)
    {
        using var prikaz = Prikaz(db, sql, parametre);
        return prikaz.ExecuteNonQuery();
    }

    public static object? Skalar(SqliteConnection db, string sql, params (string Nazov, object? Hodnota)[] parametre)
    {
        using var prikaz = Prikaz(db, sql, parametre);
        var vysledok = prikaz.ExecuteScalar();
        return vysledok is DBNull ? null : vysledok;
    }

    public static SqliteCommand Prikaz(SqliteConnection db, string sql, params (string Nazov, object? Hodnota)[] parametre)
    {
        var prikaz = db.CreateCommand();
        prikaz.CommandText = sql;
        foreach (var (nazov, hodnota) in parametre)
        {
            prikaz.Parameters.AddWithValue(nazov, hodnota ?? DBNull.Value);
        }

        return prikaz;
    }

    /// <summary>Kópia databázy do súboru (aj počas behu programu – SQLite VACUUM INTO).</summary>
    public static void Kopiruj(string ciel)
    {
        if (File.Exists(ciel))
        {
            File.Delete(ciel);
        }

        using var db = Otvor();
        Vykonaj(db, "VACUUM INTO $ciel", ("$ciel", ciel));
    }
}

/// <summary>Chyba so zrozumiteľnou správou pre používateľa.</summary>
public sealed class SkvpException(string sprava) : Exception(sprava);
