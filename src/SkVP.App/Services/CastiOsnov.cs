using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>
/// Úvodné a záverečné časti učebných osnov – dve všeobecné skupiny podobné vzdelávacím oblastiam (úvodné pred oblasťami,
/// záverečné za nimi). Obsahujú časti (dokumenty ako osnovy predmetov) so všeobecnými témami, ktoré sa týkajú všetkých
/// oblastí a predmetov. Skupina je predvolene vypnutá (zosivená); zapnutá sa zaraďuje do tlače, exportu, importu, archívu
/// a záloh.
/// </summary>
public static class CastiOsnov
{
    public const string KlucUvodAktivne = "osnovy.uvod.aktivne";
    public const string KlucZaverAktivne = "osnovy.zaver.aktivne";

    public const string NazovUvod = "Úvodné časti učebných osnov";
    public const string NazovZaver = "Záverečné časti učebných osnov";

    public const string CharakteristikaUvod =
        "Úvodné časti učebných osnov, implementácie všeobecných tém, ktoré sa týkajú všetkých vzdelávacích oblastí a predmetov";

    public const string CharakteristikaZaver =
        "Záverečné časti učebných osnov so všeobecnými témami, ktoré sa týkajú všetkých vzdelávacích oblastí a predmetov";

    /// <summary>Pastelové farby skupín (sivomodrá a teplá sivá – odlíšené od oblastí).</summary>
    public const string FarbaUvod = "#94A3B8";
    public const string FarbaZaver = "#A8A29E";

    /// <summary>Názov priečinka pri exporte a importe („00 Úvodné časti učebných osnov“, „99 Záverečné…“).</summary>
    public static string Priecinok(string typ) => typ == TypObjektu.Uvod ? "00 " + NazovUvod : "99 " + NazovZaver;

    public static string Nazov(string typ) => typ == TypObjektu.Uvod ? NazovUvod : NazovZaver;

    public static string Charakteristika(string typ) => typ == TypObjektu.Uvod ? CharakteristikaUvod : CharakteristikaZaver;

    public static string Farba(string typ) => typ == TypObjektu.Uvod ? FarbaUvod : FarbaZaver;

    private static string Kluc(string typ) => typ == TypObjektu.Uvod ? KlucUvodAktivne : KlucZaverAktivne;

    public static bool JeAktivna(string typ) => Databaza.Citaj(Kluc(typ), "0") == "1";

    public static void NastavAktivnu(string typ, bool aktivna) => Databaza.Zapis(Kluc(typ), aktivna ? "1" : "0");

    /// <summary>Skupina úvodných alebo záverečných častí (vytvorí sa, ak ešte nie je).</summary>
    public static Objekt Skupina(ObjektService sluzba, string typ)
    {
        if (sluzba.Skupiny(typ).FirstOrDefault() is { } existujuca)
        {
            return existujuca;
        }

        return sluzba.Pridaj(typ, null, Nazov(typ), Farba(typ));
    }

    /// <summary>
    /// Všetky skupiny v poradí dokumentu: úvodné časti (ak sú zapnuté), vzdelávacie oblasti, záverečné časti (ak sú zapnuté).
    /// </summary>
    public static List<Objekt> SkupinyVPoradi(ObjektService sluzba, bool ajVypnute = false)
    {
        var vysledok = new List<Objekt>();
        if (ajVypnute || JeAktivna(TypObjektu.Uvod))
        {
            vysledok.Add(Skupina(sluzba, TypObjektu.Uvod));
        }

        vysledok.AddRange(sluzba.Oblasti());
        if (ajVypnute || JeAktivna(TypObjektu.Zaver))
        {
            vysledok.Add(Skupina(sluzba, TypObjektu.Zaver));
        }

        return vysledok;
    }

    /// <summary>Dokumenty skupiny – predmety oblasti alebo úvodné/záverečné časti.</summary>
    public static List<Objekt> Dokumenty(ObjektService sluzba, Objekt skupina) =>
        sluzba.Deti(skupina.Id).Where(p => p.Typ == (skupina.Typ == TypObjektu.Oblast ? TypObjektu.Predmet : TypObjektu.Cast)).ToList();

    /// <summary>Typ dokumentov v skupine.</summary>
    public static string TypDokumentu(string typSkupiny) => typSkupiny == TypObjektu.Oblast ? TypObjektu.Predmet : TypObjektu.Cast;

    /// <summary>„3 úvodné časti“, „1 záverečná časť“.</summary>
    public static string PocetCasti(string typ, int pocet)
    {
        var uvod = typ == TypObjektu.Uvod;
        return pocet switch
        {
            0 => uvod ? "bez úvodných častí" : "bez záverečných častí",
            1 => uvod ? "1 úvodná časť" : "1 záverečná časť",
            <= 4 => $"{pocet} " + (uvod ? "úvodné časti" : "záverečné časti"),
            _ => $"{pocet} " + (uvod ? "úvodných častí" : "záverečných častí"),
        };
    }

    /// <summary>Rozpozná priečinok alebo nadpis úvodných/záverečných častí („Úvodné časti“, „00 Úvod…“, „Záverečné časti“).</summary>
    public static string? TypPodlaNazvu(string nazov)
    {
        var k = ImportOsnov.Kluc(System.Text.RegularExpressions.Regex.Replace(nazov, @"^\s*\d+[\s._-]*", ""));
        if (k.StartsWith("uvodne cast", StringComparison.Ordinal) || k.StartsWith("uvodna cast", StringComparison.Ordinal))
        {
            return TypObjektu.Uvod;
        }

        if (k.StartsWith("zaverecne cast", StringComparison.Ordinal) || k.StartsWith("zaverecna cast", StringComparison.Ordinal))
        {
            return TypObjektu.Zaver;
        }

        return null;
    }
}
