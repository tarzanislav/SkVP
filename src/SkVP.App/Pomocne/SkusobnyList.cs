using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SkVP.Services;

namespace SkVP.Pomocne;

/// <summary>
/// Skúšobná strana sprievodcu obojstrannou tlačou: veľké číslo, šípka HORE a dva krúžky ako po dierovači – na nepárnej
/// (prednej) strane pri ľavej hrane, na párnej (zadnej) pri pravej. Keď je zadná strana vytlačená správne, po otočení
/// listu okolo ľavej hrany stojí číslo rovno a krúžky sa proti svetlu prekrývajú.
/// </summary>
public static class SkusobnyList
{
    private const double Mm = 96.0 / 25.4;

    /// <summary>Obrys šípky v mm voči hornému stredu strany.</summary>
    private static readonly Point[] Sipka =
    {
        new(0, 16), new(14, 34), new(5, 34), new(5, 52), new(-5, 52), new(-5, 34), new(-14, 34),
    };

    public static DrawingVisual Strana(int cislo, Size velkost)
    {
        var zadna = cislo % 2 == 0;
        var stred = velkost.Width / 2;
        var tucne = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        var vizual = new DrawingVisual();
        using (var dc = vizual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(velkost));
            dc.DrawRectangle(null, new Pen(Brushes.Black, 1.5), new Rect(8 * Mm, 8 * Mm, velkost.Width - 16 * Mm, velkost.Height - 16 * Mm));

            var velkeCislo = Text(cislo.ToString(CultureInfo.InvariantCulture), tucne, Math.Min(velkost.Width, velkost.Height) * 0.45);
            dc.DrawText(velkeCislo, new Point(stred - velkeCislo.Width / 2, (velkost.Height - velkeCislo.Height) / 2));

            var obrys = new StreamGeometry();
            using (var kreslenie = obrys.Open())
            {
                kreslenie.BeginFigure(Bod(Sipka[0]), true, true);
                kreslenie.PolyLineTo(Sipka.Skip(1).Select(Bod).ToList(), true, false);
            }

            obrys.Freeze();
            dc.DrawGeometry(Brushes.Black, null, obrys);
            var hore = Text(Preklad.T("HORE"), tucne, 8 * Mm);
            dc.DrawText(hore, new Point(stred - hore.Width / 2, 54 * Mm));

            var popis = Text(zadna ? Preklad.F($"SkVP – skúšobný list, zadná strana {cislo}") : Preklad.F($"SkVP – skúšobný list, predná strana {cislo}"),
                new Typeface("Segoe UI"), 14);
            dc.DrawText(popis, new Point(stred - popis.Width / 2, velkost.Height - 22 * Mm));

            // krúžky nie sú súmerné podľa stredu listu – pri otočení o 180° by sa neprekrývali
            var x = zadna ? velkost.Width - 18 * Mm : 18 * Mm;
            var obrysKruzku = new Pen(Brushes.Black, 2);
            foreach (var y in new[] { velkost.Height / 4, velkost.Height / 2 })
            {
                dc.DrawEllipse(null, obrysKruzku, new Point(x, y), 5 * Mm, 5 * Mm);
                dc.DrawEllipse(Brushes.Black, null, new Point(x, y), 1.2 * Mm, 1.2 * Mm);
            }
        }

        return vizual;

        Point Bod(Point mm) => new(stred + mm.X * Mm, mm.Y * Mm);
    }

    private static FormattedText Text(string text, Typeface pismo, double velkost) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, pismo, velkost, Brushes.Black, 1.0);
}
