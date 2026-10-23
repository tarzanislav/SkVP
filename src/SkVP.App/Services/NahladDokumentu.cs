using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>
/// Vykreslenie dokumentu (MigraDoc) na obrazovku a na tlačiareň. Náhľad aj tlač používajú rovnaké vykreslenie ako PDF.
/// </summary>
public static class NahladDokumentu
{
    private const double BodovNaDip = 96.0 / 72.0;

    public static DocumentRenderer Priprav(Document dokument)
    {
        var vykreslenie = new DocumentRenderer(dokument);
        vykreslenie.PrepareDocument();
        return vykreslenie;
    }

    public static int PocetStran(DocumentRenderer vykreslenie) => vykreslenie.FormattedDocument!.PageCount;

    /// <summary>Jedna strana ako vektorový obrázok (ostrý pri každom zväčšení).</summary>
    public static DrawingImage Obrazok(DocumentRenderer vykreslenie, int strana)
    {
        var kresba = Strana(vykreslenie, strana, out _);
        var obrazok = new DrawingImage(kresba.Drawing);
        obrazok.Freeze();
        return obrazok;
    }

    /// <summary>Vykreslí stranu (číslovanú od 1) vo veľkosti A4 v jednotkách WPF.</summary>
    public static DrawingVisual Strana(DocumentRenderer vykreslenie, int strana, out Size velkost)
    {
        var info = vykreslenie.FormattedDocument!.GetPageInfo(strana);
        double sirka = info.Width;
        double vyska = info.Height;
        velkost = new Size(sirka * BodovNaDip, vyska * BodovNaDip);
        var kresba = new DrawingVisual();
        using (var kontext = kresba.RenderOpen())
        {
            kontext.DrawRectangle(Brushes.White, null, new Rect(velkost));
            using var grafika = XGraphics.FromDrawingContext(kontext, new XSize(sirka, vyska), XGraphicsUnit.Point, new PdfSharp.Events.RenderEvents());
            vykreslenie.RenderPage(grafika, strana);
        }

        return kresba;
    }

    /// <summary>Vytlačí dokument cez okno tlače Windows (výber tlačiarne, rozsah strán, kópie).</summary>
    public static bool Vytlac(DocumentRenderer vykreslenie, string nazovUlohy)
    {
        var pocet = PocetStran(vykreslenie);
        if (Dialogy.TichyRezim)
        {
            Dialogy.TicheHlasenia.Add($"[Tlač] {nazovUlohy} ({pocet} strán)");
            return true;
        }

        var dialog = new PrintDialog
        {
            UserPageRangeEnabled = true,
            MinPage = 1,
            MaxPage = (uint)Math.Max(1, pocet),
            PageRange = new PageRange(1, Math.Max(1, pocet)),
        };
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        var od = 1;
        var po = pocet;
        if (dialog.PageRangeSelection == PageRangeSelection.UserPages)
        {
            od = Math.Clamp(dialog.PageRange.PageFrom, 1, pocet);
            po = Math.Clamp(dialog.PageRange.PageTo, od, pocet);
        }

        dialog.PrintDocument(new Strankovanie(vykreslenie, od, po), nazovUlohy);
        return true;
    }

    /// <summary>Strany dokumentu MigraDoc pre tlač WPF.</summary>
    private sealed class Strankovanie(DocumentRenderer vykreslenie, int od, int po) : DocumentPaginator
    {
        private Size _velkost = new(793.7, 1122.5);

        public override bool IsPageCountValid => true;
        public override int PageCount => po - od + 1;

        public override Size PageSize
        {
            get => _velkost;
            set => _velkost = value;
        }

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            var kresba = Strana(vykreslenie, od + pageNumber, out var velkost);
            return new DocumentPage(kresba, velkost, new Rect(velkost), new Rect(velkost));
        }
    }
}
