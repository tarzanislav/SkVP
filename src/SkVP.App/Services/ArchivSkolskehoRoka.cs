using System.Globalization;
using System.IO;
using System.Text.Json;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Záznam o archíve osnov jedného školského roka.</summary>
public sealed record ZaznamArchivuRoka(string SkolskyRok, DateTime Vytvorene, string Priecinok, int Dokumentov);

/// <summary>
/// Archivácia celých učebných osnov za školský rok (napr. 2025/2026): všetky aktuálne osnovy a charakteristiky oblastí sa
/// uložia do archívu verzií v programe (so záznamom „Archív školského roka 2025/2026“) a zároveň do priečinka
/// Dokumenty\SkVP\Archív školských rokov\2025-2026 – osnovy vo Worde po oblastiach a celé osnovy ako archívne PDF/A.
/// </summary>
public static class ArchivSkolskehoRoka
{
    private const string KlucZoznam = "archiv.skolske_roky";

    public static string Priecinok => Path.Combine(Databaza.PriecinokDokumentov, "Archív školských rokov");

    /// <summary>Školský rok podľa dátumu: od septembra nový (23. 10. 2026 → 2026/2027).</summary>
    public static string Aktualny(DateTime? datum = null)
    {
        var d = datum ?? DateTime.Today;
        var zaciatok = d.Month >= 9 ? d.Year : d.Year - 1;
        return $"{zaciatok}/{zaciatok + 1}";
    }

    /// <summary>Školské roky na výber: aktuálny a päť predchádzajúcich.</summary>
    public static IReadOnlyList<string> Roky()
    {
        var zaciatok = int.Parse(Aktualny()[..4], CultureInfo.InvariantCulture);
        return Enumerable.Range(0, 6).Select(i => $"{zaciatok - i}/{zaciatok - i + 1}").ToList();
    }

    public static List<ZaznamArchivuRoka> Zoznam()
    {
        try
        {
            return Databaza.Citaj(KlucZoznam) is { Length: > 0 } json
                ? JsonSerializer.Deserialize<List<ZaznamArchivuRoka>>(json) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    /// <summary>Archivuje celé osnovy za školský rok. Vráti záznam (priečinok a počet dokumentov).</summary>
    public static ZaznamArchivuRoka Archivuj(string skolskyRok)
    {
        var ciel = Path.Combine(Priecinok, skolskyRok.Replace('/', '-'));
        if (Directory.Exists(ciel) && Directory.EnumerateFileSystemEntries(ciel).Any())
        {
            ciel += $" ({DateTime.Now:yyyy-MM-dd HH-mm})"; // opakovaná archivácia toho istého roka
        }

        Directory.CreateDirectory(ciel);
        var dovod = $"Archív školského roka {skolskyRok}";
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var pocet = 0;
        var cisloOblasti = 0;
        foreach (var oblast in sluzba.Oblasti())
        {
            cisloOblasti++;
            var priecinokOblasti = Path.Combine(ciel, ImportOsnov.BezpecnyNazov($"{cisloOblasti:00} {oblast.Nazov}"));
            if (dokumenty.NacitajData(oblast.Id) is { Length: > 0 } charakteristika)
            {
                Directory.CreateDirectory(priecinokOblasti);
                File.WriteAllBytes(Path.Combine(priecinokOblasti, ImportOsnov.BezpecnyNazov($"00 Charakteristika – {oblast.Nazov}") + ".docx"), charakteristika);
                dokumenty.ArchivujAktualnu(oblast.Id, dovod);
                pocet++;
            }

            var cislo = 0;
            foreach (var predmet in sluzba.Deti(oblast.Id).Where(p => p.Typ == TypObjektu.Predmet))
            {
                cislo++;
                if (dokumenty.NacitajData(predmet.Id) is not { Length: > 0 } osnova)
                {
                    continue;
                }

                Directory.CreateDirectory(priecinokOblasti);
                var c = cislo.ToString("00", CultureInfo.InvariantCulture);
                var nazov = InfoOsnovyService.NazovDokumentu(c, predmet.Nazov, InfoOsnovyService.Nacitaj(predmet.Id).Rok);
                File.WriteAllBytes(Path.Combine(priecinokOblasti, ImportOsnov.BezpecnyNazov(nazov) + ".docx"), osnova);
                dokumenty.ArchivujAktualnu(predmet.Id, dovod);
                pocet++;
            }
        }

        // celé osnovy v jednom dokumente – Word a archívne PDF/A-2b (bez farebného profilu Windows bežné PDF)
        var celok = ZostavaOsnov.Zostav(new VolbyZostavy());
        var nazovCelku = ImportOsnov.BezpecnyNazov($"Učebné osnovy {skolskyRok.Replace('/', '-')}");
        DocxVystup.Uloz(celok, Path.Combine(ciel, nazovCelku + ".docx"));
        try
        {
            SuboryZostavy.Uloz(PdfVystup.Vytvor(celok), Path.Combine(ciel, nazovCelku + ".pdf"), new VolbyPdf { Format = FormatVystupu.PdfA2b });
        }
        catch (SkvpException)
        {
            SuboryZostavy.Uloz(PdfVystup.Vytvor(celok), Path.Combine(ciel, nazovCelku + ".pdf"), new VolbyPdf());
        }

        var zaznam = new ZaznamArchivuRoka(skolskyRok, DateTime.Now, ciel, pocet);
        var zoznam = Zoznam();
        zoznam.Insert(0, zaznam);
        Databaza.Zapis(KlucZoznam, JsonSerializer.Serialize(zoznam));
        return zaznam;
    }
}
