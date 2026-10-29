using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Pdf;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Zostava ako súbory podľa volieb: PDF (vektorové, voliteľne zašifrované), PDF/A, PDF/X alebo obrázky.</summary>
public static class SuboryZostavy
{
    /// <summary>
    /// Uloží zostavu do priečinka zadanej cesty a vráti cesty k súborom (obrázky PNG a JPEG: súbor na každú stranu).
    /// Bez volieb platia uložené voľby PDF; cesta s príponou .pdf dá vždy PDF.
    /// </summary>
    public static IReadOnlyList<string> Uloz(Document dokument, string cesta, VolbyPdf? volby = null, Func<PrilohaPdf?>? priloha = null)
    {
        volby ??= VolbyPdf.Nacitaj();
        if (!volby.JePdf && Path.GetExtension(cesta).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            volby = volby with { Format = FormatVystupu.Pdf };
        }

        var priecinok = Path.GetDirectoryName(Path.GetFullPath(cesta))!;
        var cesty = new List<string>();
        foreach (var subor in Vytvor(dokument, Path.GetFileNameWithoutExtension(cesta), volby, priloha))
        {
            var ciel = Path.Combine(priecinok, subor.Nazov);
            File.WriteAllBytes(ciel, subor.Data);
            cesty.Add(ciel);
        }

        return cesty;
    }

    /// <summary>Súbory zostavy v pamäti (na uloženie alebo odoslanie bez ukladania na disk).</summary>
    public static IReadOnlyList<SuborVystupu> Vytvor(Document dokument, string nazov, VolbyPdf? volby = null, Func<PrilohaPdf?>? priloha = null)
    {
        volby ??= VolbyPdf.Nacitaj();
        if (volby.SHeslom && string.IsNullOrEmpty(VolbyPdf.Heslo))
        {
            throw new SkvpException(Preklad.T("PDF sa má chrániť heslom, ale heslo nie je zadané. Zadajte ho v nastaveniach PDF."));
        }

        if (volby.Rozvrh.BezPozadia)
        {
            PozadieDokumentu.Odstran(dokument);
        }

        if (!volby.JePdf)
        {
            return ObrazkyDokumentu.Vytvor(dokument, nazov, volby.Rozvrh, volby.Format);
        }

        using var pdf = volby.ZlozitStrany ? SkladaniePdf.Zloz(dokument, volby.Rozvrh) : Vykresli(dokument);
        Popis(pdf, dokument, nazov);
        var data = volby.Format.JeNorma()
            ? PdfNorma.Uloz(pdf, volby.Format, nazov, volby.Format == FormatVystupu.PdfA3b ? priloha?.Invoke() : null)
            : Bezne(pdf, volby);
        return new[] { new SuborVystupu(nazov + ".pdf", data, "application/pdf") };
    }

    /// <summary>PDF priamo z MigraDoc – aj s odkazmi a záložkami.</summary>
    private static PdfDocument Vykresli(Document dokument)
    {
        var vykreslenie = new PdfDocumentRenderer { Document = dokument };
        vykreslenie.RenderDocument();
        return vykreslenie.PdfDocument;
    }

    private static void Popis(PdfDocument pdf, Document dokument, string nazov)
    {
        pdf.Info.Creator = $"SkVP {AktualizaciaService.AktualnaVerzia}";
        if (string.IsNullOrWhiteSpace(pdf.Info.Title))
        {
            pdf.Info.Title = string.IsNullOrWhiteSpace(dokument.Info.Title) ? nazov : dokument.Info.Title;
        }

        if (pdf.Outlines.Count > 0)
        {
            pdf.PageMode = PdfPageMode.UseOutlines;
        }
    }

    /// <summary>
    /// Bežné PDF, podľa volieb zašifrované (AES) s heslom na otvorenie a zákazmi. Bez hesla vlastníka sa použije
    /// náhodné – zákazy sa potom nedajú zrušiť.
    /// </summary>
    private static byte[] Bezne(PdfDocument pdf, VolbyPdf volby)
    {
        if (volby.Zasifrovat)
        {
            var zabezpecenie = pdf.SecuritySettings;
            if (volby.PouzitHeslo)
            {
                zabezpecenie.UserPassword = VolbyPdf.Heslo!;
            }

            zabezpecenie.OwnerPassword = string.IsNullOrEmpty(VolbyPdf.HesloVlastnika) ? Guid.NewGuid().ToString("N") : VolbyPdf.HesloVlastnika;
            zabezpecenie.PermitPrint = zabezpecenie.PermitFullQualityPrint = !volby.ZakazTlace;
            zabezpecenie.PermitExtractContent = !volby.ZakazKopirovania;
            zabezpecenie.PermitModifyDocument = zabezpecenie.PermitAssembleDocument = !volby.ZakazUprav;
            zabezpecenie.PermitAnnotations = zabezpecenie.PermitFormsFill = !volby.ZakazPoznamok;
        }

        using var prud = new MemoryStream();
        pdf.Save(prud, false);
        return prud.ToArray();
    }
}
