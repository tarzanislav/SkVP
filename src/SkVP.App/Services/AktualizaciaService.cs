using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Nová verzia programu zverejnená na GitHube.</summary>
public sealed record DostupnaVerzia(
    Version Verzia,
    string Nazov,
    string Novinky,
    DateTime? Zverejnene,
    string Stranka,
    string SuborNazov,
    string SuborAdresa,
    long SuborVelkost,
    string? Sha256);

/// <summary>
/// Kontrola a inštalácia novej verzie programu. Nové verzie sa zverejňujú ako vydania (releases)
/// v repozitári na GitHube; inštalátor sa stiahne, overí a spustí v tichom režime.
/// </summary>
public sealed class AktualizaciaService
{
    public const string Repozitar = "tarzanislav/skvp";
    public const string StrankaVydani = "https://github.com/" + Repozitar + "/releases";

    private const string KlucKontrolovat = "Aktualizacie.Kontrolovat";
    private const string KlucPoslednaKontrola = "Aktualizacie.PoslednaKontrola";

    private static HttpClient Klient => Stahovanie.Klient;

    /// <summary>Verzia nainštalovaného programu (napr. 0.2.7).</summary>
    public static Version AktualnaVerzia
    {
        get
        {
            var v = typeof(AktualizaciaService).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    public bool KontrolovatPriSpusteni() => Databaza.Citaj(KlucKontrolovat) != "0";

    public void NastavKontroluPriSpusteni(bool kontrolovat) => Databaza.Zapis(KlucKontrolovat, kontrolovat ? "1" : "0");

    public DateTime? PoslednaKontrola() =>
        DateTime.TryParse(Databaza.Citaj(KlucPoslednaKontrola), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var cas) ? cas : null;

    /// <summary>
    /// Zistí, či je na GitHube novšia verzia. Vráti ju, alebo null, ak je program aktuálny.
    /// Pri chybe siete vyhodí <see cref="SkvpException"/> so zrozumiteľnou správou.
    /// </summary>
    public async Task<DostupnaVerzia?> SkontrolujAsync(CancellationToken zrusenie = default)
    {
        string json;
        using var casovac = CancellationTokenSource.CreateLinkedTokenSource(zrusenie);
        casovac.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var poziadavka = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repozitar}/releases/latest");
            poziadavka.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var odpoved = await Klient.SendAsync(poziadavka, casovac.Token);
            if (odpoved.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new SkvpException(
                    "Na GitHube sa nenašlo žiadne verejné vydanie programu. Repozitár je pravdepodobne súkromný alebo ešte nemá vydanie.");
            }

            odpoved.EnsureSuccessStatusCode();
            json = await odpoved.Content.ReadAsStringAsync(casovac.Token);
        }
        catch (HttpRequestException ex)
        {
            throw new SkvpException($"Nepodarilo sa spojiť s GitHubom. Skontrolujte pripojenie na internet.\n\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!zrusenie.IsCancellationRequested)
        {
            throw new SkvpException("GitHub neodpovedal včas. Skúste to neskôr.");
        }

        ZapisKontrolu();
        var vydanie = PrecitajVydanie(json);
        return vydanie is not null && vydanie.Verzia > AktualnaVerzia ? vydanie : null;
    }

    /// <summary>Spracuje odpoveď GitHubu o vydaní. Vráti null, ak vydanie nemá inštalátor alebo platnú verziu.</summary>
    public static DostupnaVerzia? PrecitajVydanie(string json)
    {
        using var dokument = JsonDocument.Parse(json);
        var koren = dokument.RootElement;

        var tag = Text(koren, "tag_name")?.TrimStart('v', 'V');
        if (!Version.TryParse(tag, out var verzia))
        {
            return null;
        }

        verzia = new Version(verzia.Major, verzia.Minor, Math.Max(0, verzia.Build));
        if (!koren.TryGetProperty("assets", out var subory) || subory.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var subor in subory.EnumerateArray())
        {
            var nazov = Text(subor, "name") ?? "";
            if (!nazov.StartsWith("SkVP_Setup", StringComparison.OrdinalIgnoreCase) ||
                !nazov.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var adresa = Text(subor, "browser_download_url");
            if (string.IsNullOrEmpty(adresa))
            {
                continue;
            }

            var velkost = subor.TryGetProperty("size", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
            var digest = Text(subor, "digest");
            var sha = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..] : null;

            return new DostupnaVerzia(
                verzia,
                Text(koren, "name") ?? $"SkVP {verzia}",
                Text(koren, "body") ?? "",
                DateTime.TryParse(Text(koren, "published_at"), out var zverejnene) ? zverejnene.ToLocalTime() : null,
                Text(koren, "html_url") ?? StrankaVydani,
                nazov,
                adresa,
                velkost,
                sha);
        }

        return null;
    }

    /// <summary>Stiahne inštalátor do dočasného priečinka a overí jeho veľkosť a kontrolný súčet. Vráti cestu k nemu.</summary>
    public async Task<string> StiahniAsync(DostupnaVerzia verzia, IProgress<double>? priebeh, CancellationToken zrusenie = default)
    {
        var ciel = Path.Combine(Path.GetTempPath(), "SkVP_aktualizacia", verzia.SuborNazov);
        VysledokStahovania vysledok;
        try
        {
            vysledok = await Stahovanie.StiahniAsync(verzia.SuborAdresa,
                new CielStahovania(ciel, verzia.Sha256, verzia.SuborVelkost > 0 ? verzia.SuborVelkost : null), priebeh, zrusenie);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
                                       || (ex is TaskCanceledException && !zrusenie.IsCancellationRequested))
        {
            throw new SkvpException($"Novú verziu sa nepodarilo stiahnuť.\n\n{ex.Message}");
        }

        return vysledok switch
        {
            VysledokStahovania.ZlaVelkost => throw new SkvpException("Stiahnutý súbor nemá správnu veľkosť. Skúste aktualizáciu znova."),
            VysledokStahovania.ZlySucet => throw new SkvpException("Kontrolný súčet stiahnutého súboru nesedí. Skúste aktualizáciu znova."),
            _ => ciel,
        };
    }

    /// <summary>
    /// Spustí inštalátor v tichom režime. Windows sa opýta na povolenie (inštalácia do Program Files).
    /// Po inštalácii sa program spustí znova. Vráti false, ak používateľ povolenie odmietol.
    /// </summary>
    public bool SpustiInstalator(string instalator)
    {
        try
        {
            Process.Start(new ProcessStartInfo(instalator)
            {
                Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /SPUSTIT=1",
                UseShellExecute = true,
            });
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // používateľ odmietol povolenie v okne Kontrola používateľských kont
            return false;
        }
    }

    private static void ZapisKontrolu()
    {
        try
        {
            Databaza.Zapis(KlucPoslednaKontrola, Databaza.Teraz());
        }
        catch (Exception)
        {
            // záznam o kontrole nie je dôležitý
        }
    }

    private static string? Text(JsonElement prvok, string nazov) =>
        prvok.TryGetProperty(nazov, out var hodnota) && hodnota.ValueKind == JsonValueKind.String ? hodnota.GetString() : null;
}
