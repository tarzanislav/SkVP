using System.IO;

namespace SkVP.Services;

/// <summary>Formát exportu osnov.</summary>
public enum FormatExportu
{
    Word,
    Pdf,
    PdfA2b,
    PdfA3b,
    PdfX4,
    Jpeg,
    Png,
    Tiff,
}

/// <summary>
/// Export učebných osnov s voľbami ako pri tlači (ktoré oblasti a predmety, úvodná strana, obsah, oddeľujúce strany …)
/// do Wordu, PDF, PDF/A-2b, PDF/A-3b, PDF/X-4 a obrázkov JPEG, PNG, TIFF – ako jeden dokument alebo každý predmet zvlášť.
/// Pri PDF platia aj nastavenia PDF (šifrovanie, heslo, strany na list, sivá …).
/// </summary>
public static class ExportOsnov
{
    public static string Nazov(FormatExportu format) => format switch
    {
        FormatExportu.Word => "Word (DOCX)",
        FormatExportu.Pdf => "PDF",
        FormatExportu.PdfA2b => "PDF/A-2b – archívne",
        FormatExportu.PdfA3b => "PDF/A-3b – archívne s Wordom",
        FormatExportu.PdfX4 => "PDF/X-4 – pre tlačiareň",
        FormatExportu.Jpeg => "Obrázky JPEG (300 dpi)",
        FormatExportu.Png => "Obrázky PNG (300 dpi)",
        FormatExportu.Tiff => "Obrázok TIFF (300 dpi, všetky strany)",
        _ => format.ToString(),
    };

    public static string Pripona(FormatExportu format) => format switch
    {
        FormatExportu.Word => ".docx",
        FormatExportu.Jpeg => ".jpg",
        FormatExportu.Png => ".png",
        FormatExportu.Tiff => ".tif",
        _ => ".pdf",
    };

    public static string Filter(FormatExportu format) => format switch
    {
        FormatExportu.Word => "Dokument Word (*.docx)|*.docx",
        FormatExportu.Jpeg => "Obrázky JPEG (*.jpg)|*.jpg",
        FormatExportu.Png => "Obrázky PNG (*.png)|*.png",
        FormatExportu.Tiff => "Obrázok TIFF (*.tif)|*.tif",
        _ => "PDF (*.pdf)|*.pdf",
    };

    private static FormatVystupu FormatPdf(FormatExportu format) => format switch
    {
        FormatExportu.PdfA2b => FormatVystupu.PdfA2b,
        FormatExportu.PdfA3b => FormatVystupu.PdfA3b,
        FormatExportu.PdfX4 => FormatVystupu.PdfX4,
        FormatExportu.Jpeg => FormatVystupu.Jpeg,
        FormatExportu.Png => FormatVystupu.Png,
        FormatExportu.Tiff => FormatVystupu.Tiff,
        _ => FormatVystupu.Pdf,
    };

    /// <summary>Uloží jeden dokument (všetky vybrané osnovy spolu). Vráti cesty k súborom.</summary>
    public static IReadOnlyList<string> Uloz(CelyDokument dokument, FormatExportu format, string cesta)
    {
        var priecinok = Path.GetDirectoryName(Path.GetFullPath(cesta));
        if (!string.IsNullOrEmpty(priecinok))
        {
            Directory.CreateDirectory(priecinok);
        }

        if (format == FormatExportu.Word)
        {
            DocxVystup.Uloz(dokument, cesta);
            return new[] { cesta };
        }

        var volby = VolbyPdf.Nacitaj() with { Format = FormatPdf(format) };
        return SuboryZostavy.Uloz(PdfVystup.Vytvor(dokument), cesta, volby,
            () => new SuboryZostavy.Priloha(Path.GetFileNameWithoutExtension(cesta) + ".docx", DocxVystup.Data(dokument),
                "/application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Učebné osnovy vo Worde"));
    }

    /// <summary>
    /// Každý predmet do samostatného súboru „číslo názov rok“ v podpriečinku oblasti („01 Jazyk a komunikácia“).
    /// Vráti cesty k súborom.
    /// </summary>
    public static IReadOnlyList<string> UlozJednotlivo(VolbyZostavy volby, FormatExportu format, string priecinok)
    {
        var celok = ZostavaOsnov.Zostav(volby with { TitulnaStrana = false, Obsah = false });
        var vsetkyOblasti = new Data.ObjektService().Oblasti();
        var subory = new List<string>();
        foreach (var oblast in celok.Oblasti)
        {
            var poradieOblasti = vsetkyOblasti.FindIndex(o => o.Id == oblast.Id) + 1;
            var priecinokOblasti = Path.Combine(priecinok, ImportOsnov.BezpecnyNazov($"{poradieOblasti:00} {oblast.Nazov}"));
            foreach (var predmet in oblast.Predmety)
            {
                var jeden = ZostavaOsnov.Zostav(volby with
                {
                    Oblasti = new[] { oblast.Id },
                    Predmety = new[] { predmet.Id },
                    TitulnaStrana = false,
                    Obsah = false,
                    OddelovacOblasti = false,
                    OddelovacPredmetu = false,
                    CharakteristikyOblasti = false,
                    NespracovanePredmety = true,
                });
                var rok = InfoOsnovyService.Nacitaj(predmet.Id).Rok;
                var nazov = ImportOsnov.BezpecnyNazov(InfoOsnovyService.NazovDokumentu(predmet.Cislo, predmet.Nazov, rok));
                subory.AddRange(Uloz(jeden, format, Path.Combine(priecinokOblasti, nazov + Pripona(format))));
            }
        }

        return subory;
    }
}
