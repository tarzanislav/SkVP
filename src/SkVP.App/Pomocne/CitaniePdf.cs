using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using SkVP.Data;
using SkVP.Services;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SkVP.Pomocne;

/// <summary>
/// Text z PDF (aj skenovaného) alebo obrázka cez súčasti Windows – vykreslenie PDF a rozpoznávanie textu (OCR) –
/// bez internetu a bez ďalších knižníc. Slová sa skladajú do riadkov podľa polohy, aby riadok tabuľky zostal spolu.
/// </summary>
/// <remarks>
/// Rozpoznáva sa v samostatnom procese SkVP (<see cref="Parameter"/>): súčasti Windows na PDF a OCR pri ukončení procesu
/// niekedy padajú (0xC000000D) – v pomocnom procese to nevadí a hlavný program zostane v poriadku.
/// </remarks>
public static class CitaniePdf
{
    /// <summary>Parameter pomocného procesu: SkVP.exe --ocr &lt;súbor&gt; &lt;výsledok.json&gt;.</summary>
    public const string Parameter = "--ocr";

    private const int NajviacStran = 200;
    private const double DpiStrany = 300;
    private static readonly TimeSpan CasovyLimit = TimeSpan.FromMinutes(10);

    /// <summary>Jazyky rozpoznávania v poradí: slovenčina, čeština (rovnaké znaky s diakritikou).</summary>
    private static readonly string[] JazykyDokumentov = { "sk", "cs" };

    /// <summary>Výsledok pomocného procesu (súbor JSON).</summary>
    private sealed record VysledokOcr(string? Text, string? Jazyk, string? Chyba);

    private static string NedostupneOcr => Preklad.T("Rozpoznávanie textu (OCR) nie je vo Windows dostupné. Uložte dokument vo Worde (DOCX) a naimportujte ho.");

    /// <summary>Prečíta text z PDF (každá strana sa vykreslí a rozpozná) alebo z obrázka v pomocnom procese.</summary>
    public static async Task<(string Text, string Jazyk)> PrecitajAsync(string cesta, CancellationToken zrusenie = default)
    {
        var program = Environment.ProcessPath ?? throw new SkvpException(NedostupneOcr);
        var vysledokSubor = Path.Combine(Path.GetTempPath(), $"skvp_ocr_{Guid.NewGuid():N}.json");
        var spustenie = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { Parameter, Path.GetFullPath(cesta), vysledokSubor })
        {
            spustenie.ArgumentList.Add(argument);
        }

        try
        {
            using var proces = Process.Start(spustenie) ?? throw new SkvpException(NedostupneOcr);
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(zrusenie);
            limit.CancelAfter(CasovyLimit);
            try
            {
                await proces.WaitForExitAsync(limit.Token);
            }
            catch (OperationCanceledException)
            {
                proces.Kill(entireProcessTree: true);
                zrusenie.ThrowIfCancellationRequested();
                throw new SkvpException(Preklad.T("Rozpoznávanie textu trvalo príliš dlho. Skúste menší súbor alebo dokument vo Worde."));
            }

            var vysledok = File.Exists(vysledokSubor)
                ? JsonSerializer.Deserialize<VysledokOcr>(await File.ReadAllTextAsync(vysledokSubor, zrusenie))
                : throw new SkvpException(Preklad.F($"Rozpoznávanie textu zlyhalo (kód {proces.ExitCode})."));
            return vysledok?.Chyba is { } chyba ? throw new SkvpException(chyba) : (vysledok?.Text ?? "", vysledok?.Jazyk ?? "");
        }
        finally
        {
            try
            {
                File.Delete(vysledokSubor);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // dočasný súbor v priečinku TEMP
            }
        }
    }

    /// <summary>Pomocný proces: rozpozná text, zapíše výsledok a hneď sa ukončí (bez upratovania súčastí Windows).</summary>
    public static void SpustiPomocnyProces(string cesta, string vysledokSubor)
    {
        VysledokOcr vysledok;
        try
        {
            var (text, jazyk) = Task.Run(() => RozpoznajAsync(cesta)).GetAwaiter().GetResult();
            vysledok = new VysledokOcr(text, jazyk, null);
        }
        catch (SkvpException ex)
        {
            vysledok = new VysledokOcr(null, null, ex.Message);
        }
        catch (Exception ex)
        {
            vysledok = new VysledokOcr(null, null, Preklad.F($"Dokument sa nepodarilo prečítať: {ex.GetBaseException().Message}"));
        }

        File.WriteAllText(vysledokSubor, JsonSerializer.Serialize(vysledok));
        Process.GetCurrentProcess().Kill();
    }

    /// <summary>
    /// Rozpoznávanie v jazyku dokumentov: slovenčina, inak čeština (oba jazyky majú rovnaké znaky s diakritikou), inak
    /// jazyk používateľa Windows. Null = OCR v systéme nie je.
    /// </summary>
    private static OcrEngine? Rozpoznavac(out string jazyk)
    {
        try
        {
            var dostupne = OcrEngine.AvailableRecognizerLanguages;
            var vhodny = JazykyDokumentov
                .Select(kod => dostupne.FirstOrDefault(l => l.LanguageTag.Equals(kod, StringComparison.OrdinalIgnoreCase)
                                                             || l.LanguageTag.StartsWith(kod + "-", StringComparison.OrdinalIgnoreCase)))
                .FirstOrDefault(l => l is not null);
            var rozpoznavac = vhodny is not null ? OcrEngine.TryCreateFromLanguage(vhodny) : OcrEngine.TryCreateFromUserProfileLanguages();
            jazyk = rozpoznavac?.RecognizerLanguage.DisplayName ?? "";
            return rozpoznavac;
        }
        catch (Exception)
        {
            // súčasť OCR v tejto verzii Windows nie je
            jazyk = "";
            return null;
        }
    }

    private static async Task<(string Text, string Jazyk)> RozpoznajAsync(string cesta)
    {
        var rozpoznavac = Rozpoznavac(out var jazyk) ?? throw new SkvpException(NedostupneOcr);
        var subor = await StorageFile.GetFileFromPathAsync(Path.GetFullPath(cesta));
        var text = new StringBuilder();
        if (!Path.GetExtension(cesta).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            using var obrazok = await subor.OpenReadAsync();
            text.AppendLine(await RozpoznajObrazokAsync(rozpoznavac, obrazok));
            return (text.ToString(), jazyk);
        }

        var pdf = await PdfDocument.LoadFromFileAsync(subor);
        for (uint i = 0; i < Math.Min(pdf.PageCount, NajviacStran); i++)
        {
            using var strana = pdf.GetPage(i);
            using var obraz = new InMemoryRandomAccessStream();
            // čo najostrejšie, ale v hraniciach OCR (najdlhšia strana obrázka)
            var mierka = Math.Min(DpiStrany / 96, OcrEngine.MaxImageDimension / Math.Max(strana.Size.Width, strana.Size.Height));
            await strana.RenderToStreamAsync(obraz, new PdfPageRenderOptions
            {
                DestinationWidth = (uint)(strana.Size.Width * mierka),
                DestinationHeight = (uint)(strana.Size.Height * mierka),
                BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255),
            });
            text.AppendLine(await RozpoznajObrazokAsync(rozpoznavac, obraz));
        }

        return (text.ToString(), jazyk);
    }

    private static async Task<string> RozpoznajObrazokAsync(OcrEngine rozpoznavac, IRandomAccessStream obraz)
    {
        var dekoder = await BitmapDecoder.CreateAsync(obraz);
        var transformacia = new BitmapTransform();
        var najdlhsia = Math.Max(dekoder.PixelWidth, dekoder.PixelHeight);
        if (najdlhsia > OcrEngine.MaxImageDimension)
        {
            var zmensenie = (double)OcrEngine.MaxImageDimension / najdlhsia;
            transformacia.ScaledWidth = (uint)(dekoder.PixelWidth * zmensenie);
            transformacia.ScaledHeight = (uint)(dekoder.PixelHeight * zmensenie);
        }

        using var bitmapa = await dekoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transformacia,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        var vysledok = await rozpoznavac.RecognizeAsync(bitmapa);
        return Riadky(vysledok.Lines.SelectMany(riadok => riadok.Words)
            .Select(slovo => new Slovo(slovo.Text, slovo.BoundingRect.X, slovo.BoundingRect.Y, slovo.BoundingRect.Width, slovo.BoundingRect.Height)));
    }

    private readonly record struct Slovo(string Text, double X, double Y, double Sirka, double Vyska)
    {
        public double Stred => Y + Vyska / 2;
        public double Koniec => X + Sirka;
    }

    /// <summary>Riadok textu – priebežný priemer stredu a výšky jeho slov.</summary>
    private sealed class Riadok
    {
        private double _sucetStredov;
        private double _sucetVysok;

        public List<Slovo> Slova { get; } = new();
        public double Stred => _sucetStredov / Slova.Count;
        public double Vyska => _sucetVysok / Slova.Count;

        public void Pridaj(Slovo slovo)
        {
            Slova.Add(slovo);
            _sucetStredov += slovo.Stred;
            _sucetVysok += slovo.Vyska;
        }
    }

    /// <summary>
    /// Slová zoradené zhora nadol do riadkov: slovo patrí k riadku, ak je jeho stred blízko stredu riadku (vzhľadom na
    /// výšku písma). V riadku zľava doprava; veľká medzera medzi slovami = tabulátor (stĺpec tabuľky).
    /// </summary>
    private static string Riadky(IEnumerable<Slovo> slova)
    {
        var riadky = new List<Riadok>();
        foreach (var slovo in slova.OrderBy(s => s.Stred))
        {
            if (riadky.Count == 0 || Math.Abs(riadky[^1].Stred - slovo.Stred) >= Math.Max(4, riadky[^1].Vyska * 0.55))
            {
                riadky.Add(new Riadok());
            }

            riadky[^1].Pridaj(slovo);
        }

        var text = new StringBuilder();
        foreach (var riadok in riadky)
        {
            Slovo? predosle = null;
            foreach (var slovo in riadok.Slova.OrderBy(s => s.X))
            {
                if (predosle is { } p)
                {
                    text.Append(slovo.X - p.Koniec > riadok.Vyska * 1.8 ? '\t' : ' ');
                }

                text.Append(slovo.Text);
                predosle = slovo;
            }

            text.AppendLine();
        }

        return text.ToString();
    }
}
