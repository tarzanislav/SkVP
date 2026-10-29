using System.Windows;
using System.Windows.Media;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>
/// Strany dokumentu MigraDoc ako kresby WPF – pre náhľad, tlač a obrázky. Kreslí ich ten istý MigraDoc ako PDF,
/// takže vytlačené osnovy vyzerajú presne ako uložené PDF.
/// </summary>
public static class NahladDokumentu
{
    /// <summary>Jednotky WPF na milimeter.</summary>
    public const double JednotiekNaMm = 96.0 / 25.4;

    private const double JednotiekNaBod = 96.0 / 72.0;

    /// <summary>Okolie obsahu strany, ktoré sa pri zväčšení neoreže (mm).</summary>
    private const double RezervaObsahuMm = 2;

    public static DocumentRenderer Priprav(Document dokument)
    {
        var vykreslenie = new DocumentRenderer(dokument);
        vykreslenie.PrepareDocument();
        return vykreslenie;
    }

    public static int PocetStran(DocumentRenderer vykreslenie) => vykreslenie.FormattedDocument!.PageCount;

    /// <summary>Strany zostavy sú na šírku (podľa prvej strany).</summary>
    public static bool NaSirku(DocumentRenderer vykreslenie)
    {
        var prva = vykreslenie.FormattedDocument!.GetPageInfo(1);
        return prva.Width > prva.Height;
    }

    /// <summary>Jedna strana ako vektorový obrázok (ostrý pri každom zväčšení).</summary>
    public static DrawingImage Obrazok(DocumentRenderer vykreslenie, int strana)
    {
        var obrazok = new DrawingImage(Strana(vykreslenie, strana, out _).Drawing);
        obrazok.Freeze();
        return obrazok;
    }

    /// <summary>Strana (od 1) v jej vlastnej orientácii.</summary>
    public static DrawingVisual Strana(DocumentRenderer vykreslenie, int strana, out Size velkost)
    {
        var info = vykreslenie.FormattedDocument!.GetPageInfo(strana);
        return Strana(vykreslenie, strana, info.Width > info.Height, out velkost);
    }

    /// <summary>Strana (od 1) v zadanej orientácii, na bielom podklade, vo veľkosti v jednotkách WPF.</summary>
    public static DrawingVisual Strana(DocumentRenderer vykreslenie, int strana, bool naSirku, out Size velkost)
    {
        var info = vykreslenie.FormattedDocument!.GetPageInfo(strana);
        double sirka = info.Width, vyska = info.Height;
        var dlhsia = Math.Max(sirka, vyska);
        var kratsia = Math.Min(sirka, vyska);
        var vBodoch = naSirku ? new XSize(dlhsia, kratsia) : new XSize(kratsia, dlhsia);
        velkost = new Size(vBodoch.Width * JednotiekNaBod, vBodoch.Height * JednotiekNaBod);

        var vizual = new DrawingVisual();
        using (var dc = vizual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(velkost));
            using var grafika = XGraphics.FromDrawingContext(dc, vBodoch, XGraphicsUnit.Point, new PdfSharp.Events.RenderEvents());
            vykreslenie.RenderPage(grafika, strana);
        }

        return vizual;
    }

    /// <summary>
    /// Časť strany s obsahom (text, čiary, obrázky) s malou rezervou – všetko okrem bieleho podkladu, ktorý je prvou
    /// časťou kresby z <see cref="Strana(DocumentRenderer, int, bool, out Size)"/>. Prázdna strana = Rect.Empty.
    /// </summary>
    public static Rect Obsah(DrawingGroup kresba, Size velkost)
    {
        var obsah = kresba.Children.Skip(1).Aggregate(Rect.Empty, (spolu, cast) => Rect.Union(spolu, cast.Bounds));
        if (!obsah.IsEmpty)
        {
            obsah.Inflate(RezervaObsahuMm * JednotiekNaMm, RezervaObsahuMm * JednotiekNaMm);
            obsah.Intersect(new Rect(velkost));
        }

        return obsah;
    }

    /// <summary>
    /// Vytlačí dokument cez okno tlače SkVP s náhľadom listu (tlačiareň, kópie, obojstranne, strany na list, brožúra,
    /// úpravy strán). Vráti false, ak používateľ tlač zrušil.
    /// </summary>
    public static bool Vytlac(DocumentRenderer vykreslenie, string nazovUlohy, Func<Document>? tvorba = null)
    {
        if (Dialogy.TichyRezim)
        {
            Dialogy.TicheHlasenia.Add($"[Tlač] {nazovUlohy} ({PocetStran(vykreslenie)} strán)");
            return true;
        }

        var okno = new Views.TlacWindow(vykreslenie, false, nazovUlohy, Views.RezimOknaTlace.Tlac, tvorba, null) { Owner = Dialogy.Vlastnik };
        return okno.ShowDialog() == true;
    }
}
