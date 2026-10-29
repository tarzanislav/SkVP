using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MigraDoc.Rendering;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>
/// Strany listov ako kresby WPF – pre tlačiareň, náhľad v okne tlače a obrázky. Strany zostavy sa rozmiestnia podľa
/// <see cref="Pomocne.Impozicia"/> a každá sa vykreslí len raz. V odtieňoch sivej sa strany rastrujú, aby sivá vyšla
/// na každej tlačiarni, aj keď jej ovládač farbu nevypne.
/// </summary>
public sealed class ListyTlace
{
    private readonly DocumentRenderer _vykreslenie;
    private readonly bool _stranyNaSirku;
    private readonly double _dpiSivej;
    private readonly Dictionary<int, StranaZostavy> _strany = new();

    /// <param name="dpiSivej">rozlíšenie strán v odtieňoch sivej (náhľad menej, tlač a obrázky viac)</param>
    public ListyTlace(DocumentRenderer vykreslenie, bool stranyNaSirku, VolbyTlace volby, double dpiSivej = 250)
    {
        _vykreslenie = vykreslenie;
        _stranyNaSirku = stranyNaSirku;
        _dpiSivej = dpiSivej;
        Impozicia = new Impozicia(NahladDokumentu.PocetStran(vykreslenie), volby, stranyNaSirku);
        Velkost = Impozicia.List(NahladDokumentu.JednotiekNaMm);
    }

    public Impozicia Impozicia { get; }

    /// <summary>Veľkosť listu v jednotkách WPF.</summary>
    public Size Velkost { get; }

    public int Pocet => Impozicia.PocetStranListov;

    public bool NaSirku => Impozicia.ListNaSirku;

    /// <summary>Strana listu (od 0) na bielom papieri.</summary>
    public DrawingVisual List(int index)
    {
        var vizual = new DrawingVisual();
        using (var dc = vizual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(Velkost));
            Impozicia.Nakresli(index, NahladDokumentu.JednotiekNaMm, new Platno(dc, this), cislo =>
            {
                var strana = Strana(cislo);
                return (strana.Velkost, strana.Obsah);
            });
        }

        return vizual;
    }

    /// <summary>Prázdna strana listu (doplnenie na párny počet pri obojstrannej tlači).</summary>
    public DrawingVisual Prazdny() => Kresba(dc => dc.DrawRectangle(Brushes.White, null, new Rect(Velkost)));

    /// <summary>Strana listu otočená o 180° (zadná strana pre tlačiareň, ktorá by ju inak vytlačila hore nohami).</summary>
    public DrawingVisual Otoceny(int index)
    {
        var kresba = List(index).Drawing;
        return Kresba(dc =>
        {
            dc.PushTransform(new RotateTransform(180, Velkost.Width / 2, Velkost.Height / 2));
            dc.DrawDrawing(kresba);
            dc.Pop();
        });
    }

    private static DrawingVisual Kresba(Action<DrawingContext> kreslenie)
    {
        var vizual = new DrawingVisual();
        using (var dc = vizual.RenderOpen())
        {
            kreslenie(dc);
        }

        return vizual;
    }

    private StranaZostavy Strana(int cislo)
    {
        if (!_strany.TryGetValue(cislo, out var strana))
        {
            var kresba = NahladDokumentu.Strana(_vykreslenie, cislo, _stranyNaSirku, out var velkost).Drawing;
            var obsah = NahladDokumentu.Obsah(kresba, velkost);
            Drawing hotova = Impozicia.Volby.OdtieneSivej ? Sivo(kresba, velkost, _dpiSivej) : kresba;
            if (hotova.CanFreeze)
            {
                hotova.Freeze();
            }

            _strany[cislo] = strana = new StranaZostavy(hotova, velkost, obsah);
        }

        return strana;
    }

    /// <summary>Kresba prevedená na obrázok v odtieňoch sivej.</summary>
    private static ImageDrawing Sivo(Drawing kresba, Size velkost, double dpi)
    {
        var mierka = dpi / 96.0;
        var vizual = Kresba(dc =>
        {
            dc.PushTransform(new ScaleTransform(mierka, mierka));
            dc.DrawRectangle(Brushes.White, null, new Rect(velkost));
            dc.DrawDrawing(kresba);
            dc.Pop();
        });
        var farebny = new RenderTargetBitmap(Math.Max(1, (int)Math.Round(velkost.Width * mierka)), Math.Max(1, (int)Math.Round(velkost.Height * mierka)),
            96, 96, PixelFormats.Pbgra32);
        farebny.Render(vizual);
        var sivy = new FormatConvertedBitmap(farebny, PixelFormats.Gray8, null, 0);
        sivy.Freeze();
        return new ImageDrawing(sivy, new Rect(velkost));
    }

    private sealed record StranaZostavy(Drawing Kresba, Size Velkost, Rect Obsah);

    /// <summary>Kreslenie strany listu do kontextu WPF.</summary>
    private sealed class Platno(DrawingContext dc, ListyTlace listy) : IPlatnoListu
    {
        private static readonly Pen PeroRamceka = Zmrazene(new Pen(Brushes.Black, 0.6));
        private static readonly Pen PeroPrelozenia = Zmrazene(new Pen(Brushes.Gray, 0.4) { DashStyle = DashStyles.Dash });

        public void Strana(int cislo, Rect bunka, double mierka, Vector posun)
        {
            dc.PushClip(new RectangleGeometry(bunka));
            dc.PushTransform(new MatrixTransform(mierka, 0, 0, mierka, posun.X, posun.Y));
            dc.DrawDrawing(listy.Strana(cislo).Kresba);
            dc.Pop();
            dc.Pop();
        }

        public void Ramcek(Rect obdlznik) => dc.DrawRectangle(null, PeroRamceka, obdlznik);

        public void Prelozenie(Point zaciatok, Point koniec) => dc.DrawLine(PeroPrelozenia, zaciatok, koniec);

        public void Zavoj(Rect list, double nepriehladnost) =>
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * nepriehladnost), 255, 255, 255)), null, list);

        private static Pen Zmrazene(Pen pero)
        {
            pero.Freeze();
            return pero;
        }
    }
}
