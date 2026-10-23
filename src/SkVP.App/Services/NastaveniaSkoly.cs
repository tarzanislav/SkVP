using System.IO;
using System.Windows.Media.Imaging;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Logo školy a vzhľad programu.</summary>
public static class NastaveniaSkoly
{
    private const string KlucLogo = "skvp.logo";
    public const string KlucTema = "vzhlad.tema";

    public const string TemaSystem = "system";
    public const string TemaSvetla = "svetla";
    public const string TemaTmava = "tmava";

    /// <summary>Cesta k logu školy v priečinku dát, alebo null.</summary>
    public static string? CestaLoga()
    {
        var nazov = Databaza.Citaj(KlucLogo);
        if (string.IsNullOrEmpty(nazov))
        {
            return null;
        }

        var cesta = Path.Combine(Databaza.Priecinok, nazov);
        return File.Exists(cesta) ? cesta : null;
    }

    /// <summary>Rozmer obrázka v pixeloch, alebo null, ak sa nedá načítať.</summary>
    public static (int Sirka, int Vyska)? Rozmer(string? cesta)
    {
        if (string.IsNullOrEmpty(cesta) || !File.Exists(cesta))
        {
            return null;
        }

        try
        {
            using var prud = File.OpenRead(cesta);
            var snimka = BitmapFrame.Create(prud, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            return snimka.PixelWidth > 0 && snimka.PixelHeight > 0 ? (snimka.PixelWidth, snimka.PixelHeight) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Skopíruje obrázok loga do priečinka dát (PNG, JPEG, BMP, GIF).</summary>
    public static void NastavLogo(string zdroj)
    {
        var pripona = Path.GetExtension(zdroj).ToLowerInvariant();
        if (pripona is not (".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif"))
        {
            throw new SkvpException("Logo musí byť obrázok PNG, JPEG, BMP alebo GIF.");
        }

        try
        {
            using var prud = File.OpenRead(zdroj);
            var snimka = BitmapFrame.Create(prud, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (snimka.PixelWidth < 1 || snimka.PixelHeight < 1)
            {
                throw new SkvpException("Obrázok loga je prázdny.");
            }
        }
        catch (SkvpException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SkvpException($"Súbor sa nedá načítať ako obrázok.\n\n{ex.Message}");
        }

        OdstranLogo();
        var nazov = "logo_skoly" + pripona;
        File.Copy(zdroj, Path.Combine(Databaza.Priecinok, nazov), true);
        Databaza.Zapis(KlucLogo, nazov);
    }

    public static void OdstranLogo()
    {
        var stare = CestaLoga();
        if (stare is not null)
        {
            try
            {
                File.Delete(stare);
            }
            catch (IOException)
            {
                // starý súbor loga nevadí
            }
        }

        Databaza.Zapis(KlucLogo, null);
    }
}
