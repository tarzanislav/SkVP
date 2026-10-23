using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Jednorazové úpravy údajov pri aktualizácii programu (každá sa vykoná raz, potom sa zapíše značka).</summary>
public static class Migracie
{
    /// <summary>Predmety premenované v učebnom pláne gymnázia: starý názov → nový názov.</summary>
    public static readonly IReadOnlyList<(string Kluc, string Stary, string Novy)> PremenovanePredmety = new[]
    {
        ("migracia.konverzacia_nj", "Konverzácia z nemeckého jazyka", "Konverzácia v nemeckom jazyku"),
    };

    public static void Spusti()
    {
        foreach (var (kluc, stary, novy) in PremenovanePredmety)
        {
            if (Databaza.Citaj(kluc) == "1")
            {
                continue;
            }

            try
            {
                PremenujPredmet(stary, novy);
                Databaza.Zapis(kluc, "1");
            }
            catch (Exception)
            {
                // skúsi sa znova pri ďalšom spustení
            }
        }
    }

    /// <summary>Premenuje predmet so starým názvom (ak nový ešte neexistuje) a opraví názov aj v textoch dokumentov.</summary>
    public static int PremenujPredmet(string stary, string novy)
    {
        var sluzba = new ObjektService();
        var predmety = sluzba.Oblasti().SelectMany(o => sluzba.Deti(o.Id)).Where(p => p.Typ == TypObjektu.Predmet).ToList();
        if (predmety.Any(p => p.Nazov == novy))
        {
            return 0;
        }

        var premenovane = 0;
        foreach (var predmet in predmety.Where(p => p.Nazov == stary))
        {
            sluzba.Premenuj(predmet.Id, novy);
            premenovane++;
        }

        if (premenovane > 0)
        {
            PremenovanieVDokumentoch.NahradVsade(stary, novy, Array.Empty<long>());
        }

        return premenovane;
    }
}
