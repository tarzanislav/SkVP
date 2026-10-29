using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MigraDoc.DocumentObjectModel;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>
/// Strany listov ako obrázky 300 dpi – PNG alebo JPEG (súbor na každú stranu) alebo jeden viacstranový TIFF –
/// s rovnakým rozmiestnením a úpravami strán ako pri tlači.
/// </summary>
public static class ObrazkyDokumentu
{
    public const double Dpi = 300;

    public static IReadOnlyList<SuborVystupu> Vytvor(Document dokument, string nazov, VolbyTlace volby, FormatVystupu format)
    {
        volby = volby.Opravene() with { Obojstranne = false };
        var vykreslenie = NahladDokumentu.Priprav(dokument);
        var listy = new ListyTlace(vykreslenie, NahladDokumentu.NaSirku(vykreslenie), volby, Dpi);
        var pixely = volby.OdtieneSivej ? PixelFormats.Gray8 : PixelFormats.Bgr24;

        if (format == FormatVystupu.Tiff)
        {
            return new[] { new SuborVystupu(nazov + ".tif", Tiff(listy, pixely), "image/tiff") };
        }

        var jpeg = format == FormatVystupu.Jpeg;
        var (pripona, mime) = jpeg ? (".jpg", "image/jpeg") : (".png", "image/png");
        var cislic = listy.Pocet.ToString(CultureInfo.InvariantCulture).Length;
        var subory = new List<SuborVystupu>(listy.Pocet);
        for (var i = 0; i < listy.Pocet; i++)
        {
            // každá strana sa hneď zakóduje – v pamäti zostanú len hotové súbory, aj pri desiatkach strán
            BitmapEncoder kodovac = jpeg ? new JpegBitmapEncoder { QualityLevel = 92 } : new PngBitmapEncoder();
            kodovac.Frames.Add(BitmapFrame.Create(Snimka(listy, i, pixely)));
            var meno = listy.Pocet == 1 ? nazov : $"{nazov}-{(i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(cislic, '0')}";
            subory.Add(new SuborVystupu(meno + pripona, Zakoduj(kodovac), mime));
        }

        return subory;
    }

    /// <summary>
    /// Viacstranový TIFF. Kodér potrebuje naraz všetky strany, preto sa každá najprv skomprimuje do PNG a do TIFF sa
    /// dekóduje až pri zápise – v pamäti nie sú naraz všetky nekomprimované strany.
    /// </summary>
    private static byte[] Tiff(ListyTlace listy, PixelFormat pixely)
    {
        var prudy = new List<MemoryStream>(listy.Pocet);
        try
        {
            var tiff = new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw };
            for (var i = 0; i < listy.Pocet; i++)
            {
                var png = new PngBitmapEncoder();
                png.Frames.Add(BitmapFrame.Create(Snimka(listy, i, pixely)));
                var prud = new MemoryStream(Zakoduj(png));
                prudy.Add(prud);
                tiff.Frames.Add(BitmapDecoder.Create(prud, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None).Frames[0]);
            }

            return Zakoduj(tiff);
        }
        finally
        {
            prudy.ForEach(p => p.Dispose());
        }
    }

    private static BitmapSource Snimka(ListyTlace listy, int index, PixelFormat pixely)
    {
        var mierka = Dpi / 96;
        var obraz = new RenderTargetBitmap((int)Math.Round(listy.Velkost.Width * mierka), (int)Math.Round(listy.Velkost.Height * mierka),
            Dpi, Dpi, PixelFormats.Pbgra32);
        obraz.Render(listy.List(index));
        var vysledok = new FormatConvertedBitmap(obraz, pixely, null, 0);
        vysledok.Freeze();
        return vysledok;
    }

    private static byte[] Zakoduj(BitmapEncoder kodovac)
    {
        using var prud = new MemoryStream();
        kodovac.Save(prud);
        return prud.ToArray();
    }
}
