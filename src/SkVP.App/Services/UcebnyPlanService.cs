using System.IO;
using System.Text.Json;
using ClosedXML.Excel;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Štruktúra vzdelávacích oblastí a predmetov načítaná z tabuľky.</summary>
public sealed class UcebnyPlan
{
    public string? Nazov { get; set; }
    public string? Typ { get; set; }
    public List<OblastPlanu> Oblasti { get; set; } = new();

    public int PocetPredmetov => Oblasti.Sum(o => o.Predmety.Count);
}

public sealed class OblastPlanu
{
    public string Nazov { get; set; } = "";
    public List<string> Predmety { get; set; } = new();
}

/// <summary>Výsledok vytvorenia štruktúry z učebného plánu.</summary>
public sealed record VysledokImportu(int NoveOblasti, int NovePredmety, int Preskocene);

/// <summary>
/// Načítanie vzdelávacích oblastí a predmetov z tabuľky Excel. Očakávaný tvar (ako v tabuľke školy):
/// v stĺpci A je názov oblasti (riadok bez predmetu), v stĺpci B pod ňou názvy predmetov. Riadky so samotným textom
/// v stĺpci A, po ktorých nenasledujú predmety, sú nadpisy tabuľky (napr. „Gymnázium, ISCED 3A“).
/// </summary>
public sealed class UcebnyPlanService
{
    public const string KlucTypSkoly = "skvp.typ";

    /// <summary>Pastelové farby oblastí (<see cref="SkVP.Pomocne.Pastel.Oblasti"/>).</summary>
    private static readonly string[] PaletaOblasti = SkVP.Pomocne.Pastel.Oblasti;

    public static IReadOnlyList<string> Paleta => PaletaOblasti;

    public static string FarbaOblasti(int index) => PaletaOblasti[index % PaletaOblasti.Length];

    /// <summary>Učebný plán gymnázia dodaný s programom (z tabuľky školy).</summary>
    public static UcebnyPlan Vstavany()
    {
        using var prud = typeof(UcebnyPlanService).Assembly.GetManifestResourceStream("SkVP.UcebnyPlanGymnazium.json")
                         ?? throw new SkvpException("V programe chýba vstavaný učebný plán.");
        return JsonSerializer.Deserialize<UcebnyPlan>(prud, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new UcebnyPlan();
    }

    public static UcebnyPlan NacitajZExcelu(string cesta)
    {
        XLWorkbook zosit;
        try
        {
            var prud = new FileStream(cesta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            zosit = new XLWorkbook(prud);
        }
        catch (IOException ex)
        {
            throw new SkvpException($"Súbor sa nedá otvoriť. Ak je otvorený v Exceli, zatvorte ho a skúste znova.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            throw new SkvpException($"Súbor nie je platná tabuľka Excel (.xlsx).\n\n{ex.Message}");
        }

        using (zosit)
        {
            var harok = zosit.Worksheets.First();
            var riadky = new List<(string A, string B)>();
            var posledny = harok.LastRowUsed()?.RowNumber() ?? 0;
            for (var r = 1; r <= posledny; r++)
            {
                var a = Bunka(harok, r, 1);
                var b = Bunka(harok, r, 2);
                if (a.Length > 0 || b.Length > 0)
                {
                    riadky.Add((a, b));
                }
            }

            return Rozober(riadky);
        }
    }

    /// <summary>Rozpozná oblasti, predmety a nadpisy z riadkov tabuľky (stĺpce A a B).</summary>
    public static UcebnyPlan Rozober(IReadOnlyList<(string A, string B)> riadky)
    {
        var plan = new UcebnyPlan();
        var nadpisy = new List<string>();
        OblastPlanu? aktualna = null;

        for (var i = 0; i < riadky.Count; i++)
        {
            var (a, b) = riadky[i];
            if (a.Length > 0 && b.Length == 0)
            {
                var dalsi = i + 1 < riadky.Count ? riadky[i + 1] : ("", "");
                if (dalsi.Item2.Length > 0 && dalsi.Item1.Length == 0)
                {
                    aktualna = new OblastPlanu { Nazov = a };
                    plan.Oblasti.Add(aktualna);
                }
                else if (aktualna is null)
                {
                    nadpisy.Add(a);
                }
                else
                {
                    // oblasť bez predmetov
                    aktualna = new OblastPlanu { Nazov = a };
                    plan.Oblasti.Add(aktualna);
                }
            }
            else if (a.Length > 0 && b.Length > 0)
            {
                // iný tvar tabuľky: oblasť a predmet v jednom riadku
                aktualna = plan.Oblasti.LastOrDefault(o => string.Equals(o.Nazov, a, StringComparison.CurrentCultureIgnoreCase));
                if (aktualna is null)
                {
                    aktualna = new OblastPlanu { Nazov = a };
                    plan.Oblasti.Add(aktualna);
                }

                aktualna.Predmety.Add(b);
            }
            else if (b.Length > 0)
            {
                if (aktualna is null)
                {
                    aktualna = new OblastPlanu { Nazov = "Ďalšie predmety" };
                    plan.Oblasti.Add(aktualna);
                }

                aktualna.Predmety.Add(b);
            }
        }

        plan.Nazov = nadpisy.ElementAtOrDefault(0);
        plan.Typ = nadpisy.ElementAtOrDefault(1);
        if (plan.Oblasti.Count == 0)
        {
            throw new SkvpException(
                "V tabuľke sa nenašli žiadne vzdelávacie oblasti. Názov oblasti má byť v stĺpci A a predmety pod ním v stĺpci B.");
        }

        return plan;
    }

    /// <summary>
    /// Vytvorí oblasti a predmety v databáze. Existujúce oblasti s rovnakým názvom sa doplnia o chýbajúce predmety,
    /// pri <paramref name="nahradit"/> sa doterajšie oblasti odstránia (dajú sa vrátiť späť).
    /// </summary>
    public static VysledokImportu Vytvor(UcebnyPlan plan, bool nahradit)
    {
        var sluzba = new ObjektService();
        if (nahradit)
        {
            foreach (var oblast in sluzba.Oblasti())
            {
                sluzba.Odstran(oblast.Id);
            }
        }

        int noveOblasti = 0, novePredmety = 0, preskocene = 0;
        var existujuce = sluzba.Oblasti();
        foreach (var oblastPlanu in plan.Oblasti)
        {
            var oblast = existujuce.FirstOrDefault(o => string.Equals(o.Nazov, oblastPlanu.Nazov, StringComparison.CurrentCultureIgnoreCase));
            if (oblast is null)
            {
                oblast = sluzba.Pridaj(TypObjektu.Oblast, null, oblastPlanu.Nazov, FarbaOblasti(existujuce.Count));
                existujuce.Add(oblast);
                noveOblasti++;
            }

            var predmety = sluzba.Deti(oblast.Id).Select(p => p.Nazov).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            foreach (var predmet in oblastPlanu.Predmety)
            {
                if (predmety.Add(predmet))
                {
                    sluzba.Pridaj(TypObjektu.Predmet, oblast.Id, predmet);
                    novePredmety++;
                }
                else
                {
                    preskocene++;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(plan.Typ) && string.IsNullOrWhiteSpace(Databaza.Citaj(KlucTypSkoly)))
        {
            Databaza.Zapis(KlucTypSkoly, plan.Typ);
        }

        return new VysledokImportu(noveOblasti, novePredmety, preskocene);
    }

    private static string Bunka(IXLWorksheet harok, int riadok, int stlpec)
    {
        var bunka = harok.Cell(riadok, stlpec);
        if (bunka.IsMerged() && !bunka.MergedRange().FirstCell().Address.Equals(bunka.Address))
        {
            return "";
        }

        return (bunka.GetFormattedString() ?? "").Trim();
    }
}
