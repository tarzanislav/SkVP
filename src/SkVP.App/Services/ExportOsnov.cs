using System.IO;
using SkVP.Data;

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

    public static string Pripona(FormatExportu format) => format == FormatExportu.Word ? ".docx" : FormatPdf(format).Pripona();

    public static string Filter(FormatExportu format) =>
        format == FormatExportu.Word ? "Dokument Word (*.docx)|*.docx" : FormatPdf(format).Filter();

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
            () => PrilohaPdf.Word(Path.GetFileNameWithoutExtension(cesta) + ".docx", DocxVystup.Data(dokument), "Učebné osnovy vo Worde"));
    }

    /// <summary>
    /// Každý predmet do samostatného súboru „číslo názov rok“ v podpriečinku oblasti („01 Jazyk a komunikácia“).
    /// Vráti cesty k súborom.
    /// </summary>
    public static IReadOnlyList<string> UlozJednotlivo(VolbyZostavy volby, FormatExportu format, string priecinok)
    {
        var subory = new List<string>();
        foreach (var (_, _, jeden, priecinokOblasti, nazov) in PredmetyZvlast(volby))
        {
            subory.AddRange(Uloz(jeden, format, Path.Combine(priecinok, priecinokOblasti, nazov + Pripona(format))));
        }

        return subory;
    }

    /// <summary>
    /// Export „ako pri importe“: každá osnova do rovnakého podpriečinka ako jej importovaný súbor, s pôvodným alebo novým
    /// názvom („05 Fyzika 2026“), vo formáte pôvodného súboru (Word zostane Word, PDF zostane PDF) alebo všetko vo Worde
    /// či PDF. Osnovy naimportované pred verziou, ktorá si pamätá cestu, sa nájdu podľa názvu súboru v pôvodnom
    /// priečinku importu (ak je zadaný), inak idú do priečinka oblasti. Existujúce súbory sa neprepíšu.
    /// </summary>
    /// <param name="povodnyPriecinok">priečinok, z ktorého sa osnovy importovali (len sa číta); null = bez neho</param>
    public static VysledokExportuOsnov UlozAkoPriImporte(VolbyZostavy volby, FormatPodlaImportu formaty, bool povodneNazvy, string priecinok,
        string? povodnyPriecinok = null)
    {
        if (povodnyPriecinok is { Length: > 0 } && JeVnutri(priecinok, povodnyPriecinok))
        {
            throw new SkvpException(Preklad.T("Osnovy uložte do iného priečinka, než z ktorého ste ich importovali – pôvodné súbory musia zostať bez zmeny."));
        }

        var vPovodnom = SuboryVPriecinku(povodnyPriecinok);
        var subory = new List<string>();
        int podlaImportu = 0, bezImportu = 0;
        foreach (var (oblast, predmet, jeden, priecinokOblasti, novyNazov) in PredmetyZvlast(volby))
        {
            var povodna = PovodnaCesta(InfoOsnovyService.Nacitaj(predmet.Id), oblast.Nazov, priecinokOblasti, vPovodnom);
            if (povodna is null)
            {
                bezImportu++;
            }
            else
            {
                podlaImportu++;
            }

            var pripona = povodna is null ? "" : Path.GetExtension(povodna).ToLowerInvariant();
            var format = formaty switch
            {
                FormatPodlaImportu.VsetkoWord => FormatExportu.Word,
                FormatPodlaImportu.VsetkoPdf => FormatExportu.Pdf,
                _ => pripona == ".pdf" ? FormatExportu.Pdf : FormatExportu.Word,
            };

            // podpriečinky ako pri importe; osnova bez importovaného súboru do priečinka oblasti
            var casti = povodna is null
                ? new[] { priecinokOblasti }
                : povodna.Split('/', '\\').SkipLast(1).Select(c => ImportOsnov.BezpecnyNazov(c)).ToArray();
            var ciel = Path.Combine(casti.Prepend(priecinok).ToArray());
            Directory.CreateDirectory(ciel);
            var nazov = povodneNazvy && povodna is not null ? Path.GetFileNameWithoutExtension(povodna) : novyNazov;
            subory.AddRange(Uloz(jeden, format, ImportOsnov.VolnaCesta(ciel, nazov, Pripona(format))));
        }

        return new VysledokExportuOsnov(subory, podlaImportu, bezImportu);
    }

    /// <summary>
    /// Cesta importovaného súboru osnovy („03 Človek a príroda/Fyzika.pdf“): zapamätaná pri importe, inak nájdená podľa
    /// názvu v pôvodnom priečinku (pri viacerých rovnakých názvoch ten v priečinku oblasti), inak v priečinku oblasti;
    /// null = osnova nebola importovaná zo súboru.
    /// </summary>
    private static string? PovodnaCesta(InfoOsnovy info, string oblast, string priecinokOblasti, IReadOnlyDictionary<string, List<string>> vPovodnom)
    {
        if (info.CestaZdroja is { Length: > 0 } cesta && JeBezpecna(cesta))
        {
            return cesta;
        }

        if (info.Zdroj is not { Length: > 0 } subor || !JeBezpecna(subor))
        {
            return null;
        }

        if (vPovodnom.TryGetValue(subor, out var najdene))
        {
            var kluc = ImportOsnov.Kluc(oblast);
            return najdene.FirstOrDefault(c => c.Split('/').SkipLast(1).Any(p => ImportOsnov.Kluc(ImportOsnov.CistyNazov(p)) == kluc)) ?? najdene[0];
        }

        return $"{priecinokOblasti}/{Path.GetFileName(subor)}";
    }

    /// <summary>Relatívna cesta bez návratu nad priečinok („..“) a bez písmena disku.</summary>
    private static bool JeBezpecna(string cesta) =>
        !Path.IsPathRooted(cesta) && !cesta.Split('/', '\\').Any(c => c is ".." or ".") && cesta.IndexOfAny(Path.GetInvalidPathChars()) < 0;

    /// <summary>Súbory Word a PDF v priečinku a podpriečinkoch podľa názvu (bez archívu a dočasných súborov Wordu).</summary>
    private static Dictionary<string, List<string>> SuboryVPriecinku(string? priecinok)
    {
        var subory = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (priecinok is not { Length: > 0 } || !Directory.Exists(priecinok))
        {
            return subory;
        }

        foreach (var cesta in Directory.EnumerateFiles(priecinok, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
        {
            var relativna = Path.GetRelativePath(priecinok, cesta).Replace('\\', '/');
            if (!ImportOsnov.Pripony.Contains(Path.GetExtension(cesta).ToLowerInvariant())
                || Path.GetFileName(cesta).StartsWith("~$", StringComparison.Ordinal)
                || relativna.Split('/').SkipLast(1).Any(c => ImportOsnov.Kluc(c).StartsWith("archiv", StringComparison.Ordinal)))
            {
                continue;
            }

            if (!subory.TryGetValue(Path.GetFileName(cesta), out var zoznam))
            {
                subory[Path.GetFileName(cesta)] = zoznam = new List<string>();
            }

            zoznam.Add(relativna);
        }

        return subory;
    }

    private static bool JeVnutri(string priecinok, string koren)
    {
        var cesta = Path.GetFullPath(priecinok).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var zaklad = Path.GetFullPath(koren).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return cesta.StartsWith(zaklad, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Každý predmet ako samostatný dokument s priečinkom oblasti a názvom „číslo názov rok“.</summary>
    private static IEnumerable<(KapitolaOblasti Oblast, KapitolaPredmetu Predmet, CelyDokument Dokument, string PriecinokOblasti, string Nazov)>
        PredmetyZvlast(VolbyZostavy volby)
    {
        var celok = ZostavaOsnov.Zostav(volby with { TitulnaStrana = false, Obsah = false });
        var vsetkyOblasti = new Data.ObjektService().Oblasti();
        foreach (var oblast in celok.Oblasti)
        {
            // úvodné časti do „00 Úvodné časti učebných osnov“, záverečné do „99 Záverečné časti učebných osnov“
            var poradieOblasti = vsetkyOblasti.FindIndex(o => o.Id == oblast.Id) + 1;
            var priecinokOblasti = ImportOsnov.BezpecnyNazov(oblast.JeOblast ? $"{poradieOblasti:00} {oblast.Nazov}" : CastiOsnov.Priecinok(oblast.Typ));
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
                    NadpisOblasti = false,
                    NespracovanePredmety = true,
                });
                var rok = InfoOsnovyService.Nacitaj(predmet.Id).Rok;
                yield return (oblast, predmet, jeden, priecinokOblasti,
                    ImportOsnov.BezpecnyNazov(InfoOsnovyService.NazovDokumentu(predmet.Cislo, predmet.Nazov, rok)));
            }
        }
    }
}

/// <summary>Formát súborov pri exporte „ako pri importe“.</summary>
public enum FormatPodlaImportu
{
    /// <summary>Word zostane Word, PDF zostane PDF.</summary>
    AkoPovodne,
    VsetkoWord,
    VsetkoPdf,
}

/// <summary>Výsledok exportu „ako pri importe“.</summary>
/// <param name="PodlaImportu">osnovy uložené podľa importovaného súboru (priečinok, názov, formát)</param>
/// <param name="BezImportu">osnovy, ktoré neboli importované zo súboru – sú v priečinku oblasti s novým názvom</param>
public sealed record VysledokExportuOsnov(IReadOnlyList<string> Subory, int PodlaImportu, int BezImportu);
