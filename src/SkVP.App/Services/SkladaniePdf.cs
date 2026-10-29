using System.Text;
using System.Windows;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>
/// Nové PDF so stranami zostavy rozmiestnenými podľa <see cref="Impozicia"/> (2/4/8 na list, brožúra, zväčšenie, rámik,
/// dierovač). Strany sa kreslia vektorovo, text zostáva textom. Zosvetlenie je biely priehľadný závoj, odtiene sivej
/// vzniknú prekrytím v režime miešania „Saturation“ – farby stratia sýtosť a text sa dá ďalej označiť a kopírovať.
/// </summary>
internal static class SkladaniePdf
{
    private const double BodovNaMm = 72.0 / 25.4;
    private const double BodovNaJednotkuWpf = 72.0 / 96.0;

    public static PdfDocument Zloz(Document dokument, VolbyTlace volby)
    {
        var vykreslenie = NahladDokumentu.Priprav(dokument);
        var impozicia = new Impozicia(NahladDokumentu.PocetStran(vykreslenie), volby, NahladDokumentu.NaSirku(vykreslenie));
        var rozmery = new Dictionary<int, (Size, Rect)>();
        (Size Velkost, Rect Obsah) Rozmer(int cislo)
        {
            if (!rozmery.TryGetValue(cislo, out var rozmer))
            {
                rozmery[cislo] = rozmer = RozmerStrany(vykreslenie, cislo, zistitObsah: impozicia.Volby.Zvacsenie > 1.001);
            }

            return rozmer;
        }

        var pdf = new PdfDocument();
        pdf.Info.Title = dokument.Info.Title ?? "";
        var list = impozicia.List(BodovNaMm);
        var sivy = impozicia.Volby.OdtieneSivej ? Odfarbenie(pdf) : null;
        for (var index = 0; index < impozicia.PocetStranListov; index++)
        {
            var strana = pdf.AddPage();
            strana.Size = PdfSharp.PageSize.A4;
            strana.Orientation = impozicia.ListNaSirku ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
            using (var gfx = XGraphics.FromPdfPage(strana))
            {
                if (sivy is not null)
                {
                    // biely podklad – prekrytie „Saturation“ zachová jeho svetlosť, prázdne miesta nezošednú
                    gfx.DrawRectangle(XBrushes.White, 0, 0, list.Width, list.Height);
                }

                impozicia.Nakresli(index, BodovNaMm, new Platno(gfx, vykreslenie), Rozmer);
            }

            if (sivy is not null)
            {
                Odfarbi(strana, sivy, Math.Max(list.Width, list.Height));
            }
        }

        OdstranOpakovaneZalozky(pdf);
        return pdf;
    }

    /// <summary>Veľkosť strany v bodoch a voliteľne časť s obsahom (zistená z kresby strany).</summary>
    private static (Size, Rect) RozmerStrany(DocumentRenderer vykreslenie, int cislo, bool zistitObsah)
    {
        var info = vykreslenie.FormattedDocument!.GetPageInfo(cislo);
        var velkost = new Size(info.Width, info.Height);
        if (!zistitObsah)
        {
            return (velkost, Rect.Empty);
        }

        var kresba = NahladDokumentu.Strana(vykreslenie, cislo, velkost.Width > velkost.Height, out var velkostWpf).Drawing;
        var obsah = NahladDokumentu.Obsah(kresba, velkostWpf);
        return (velkost, obsah.IsEmpty
            ? obsah
            : new Rect(obsah.X * BodovNaJednotkuWpf, obsah.Y * BodovNaJednotkuWpf, obsah.Width * BodovNaJednotkuWpf, obsah.Height * BodovNaJednotkuWpf));
    }

    /// <summary>Stav grafiky s režimom miešania „Saturation“ (jeden pre celý dokument).</summary>
    private static PdfExtGState Odfarbenie(PdfDocument pdf)
    {
        var stav = new PdfExtGState(pdf);
        stav.Elements.SetName("/BM", "/Saturation");
        return stav;
    }

    /// <summary>
    /// Sivé prekrytie cez celú stranu v režime „Saturation“: sýtosť farieb pod ním klesne na nulu. Štvorec so stranou
    /// dlhšej hrany listu pokryje stranu v každej orientácii.
    /// </summary>
    private static void Odfarbi(PdfPage strana, PdfExtGState stav, double hrana)
    {
        var meno = strana.Resources.AddExtGState(stav);
        var obsah = strana.Contents.AppendContent();
        obsah.CreateStream(Encoding.ASCII.GetBytes(FormattableString.Invariant($"q {meno} gs 0.5 g -10 -10 {hrana + 20:0.##} {hrana + 20:0.##} re f Q\n")));
    }

    /// <summary>Záložky (nadpisy skupín) len pri prvom výskyte – pri opakovaných stranách by sa zdvojili.</summary>
    private static void OdstranOpakovaneZalozky(PdfDocument pdf)
    {
        var videne = new HashSet<string>();
        foreach (var zalozka in pdf.Outlines.Where(z => !videne.Add(z.Title)).ToList())
        {
            pdf.Outlines.Remove(zalozka);
        }
    }

    /// <summary>Kreslenie strany listu do PDF.</summary>
    private sealed class Platno(XGraphics gfx, DocumentRenderer vykreslenie) : IPlatnoListu
    {
        public void Strana(int cislo, Rect bunka, double mierka, Vector posun)
        {
            var stav = gfx.Save();
            gfx.IntersectClip(Na(bunka));
            gfx.TranslateTransform(posun.X, posun.Y);
            gfx.ScaleTransform(mierka);
            vykreslenie.RenderPage(gfx, cislo);
            gfx.Restore(stav);
        }

        public void Ramcek(Rect obdlznik) => gfx.DrawRectangle(new XPen(XColors.Black, 0.5), Na(obdlznik));

        public void Prelozenie(Point zaciatok, Point koniec) =>
            gfx.DrawLine(new XPen(XColors.Gray, 0.4) { DashStyle = XDashStyle.Dash }, zaciatok.X, zaciatok.Y, koniec.X, koniec.Y);

        public void Zavoj(Rect list, double nepriehladnost) =>
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb((int)Math.Round(255 * nepriehladnost), 255, 255, 255)), Na(list));

        private static XRect Na(Rect r) => new(r.X, r.Y, r.Width, r.Height);
    }
}
