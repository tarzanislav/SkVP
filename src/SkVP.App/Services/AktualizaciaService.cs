using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// Nové verzie programu: vydania (releases) v repozitári na GitHube. Inštalátor sa stiahne, overí (veľkosť, SHA-256)
/// a spustí v tichom režime.
/// </summary>
public static class AktualizaciaService
{
    public const string Repozitar = "tarzanislav/skvp";
    public const string StrankaVydani = "https://github.com/" + Repozitar + "/releases";

    private const string KlucKontrolovat = "Aktualizacie.Kontrolovat";
    private const string KlucPoslednaKontrola = "Aktualizacie.PoslednaKontrola";
    private const int ZrusenieUac = 1223;
    private static readonly TimeSpan LimitKontroly = TimeSpan.FromSeconds(30);

    /// <summary>Verzia nainštalovaného programu (napr. 0.2.7).</summary>
    public static Version AktualnaVerzia
    {
        get
        {
            var v = typeof(AktualizaciaService).Assembly.GetName().Version ?? new Version(0, 0, 0);
            return new Version(v.Major, v.Minor, Math.Max(0, v.Build));
        }
    }

    /// <summary>Kontrolovať novú verziu pri spustení programu (predvolene áno).</summary>
    public static bool KontrolovatPriSpusteni
    {
        get => Databaza.Citaj(KlucKontrolovat) != "0";
        set => Databaza.Zapis(KlucKontrolovat, value ? "1" : "0");
    }

    /// <summary>
    /// Novšia verzia na GitHube, alebo null, ak je program aktuálny. Chyba siete = <see cref="SkvpException"/>
    /// so zrozumiteľnou správou.
    /// </summary>
    public static async Task<DostupnaVerzia?> SkontrolujAsync(CancellationToken zrusenie = default)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(zrusenie);
        limit.CancelAfter(LimitKontroly);
        string json;
        try
        {
            using var poziadavka = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{Repozitar}/releases/latest");
            poziadavka.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var odpoved = await Stahovanie.Klient.SendAsync(poziadavka, limit.Token);
            if (odpoved.StatusCode == HttpStatusCode.NotFound)
            {
                throw new SkvpException("Na GitHube sa nenašlo žiadne verejné vydanie programu. Repozitár je pravdepodobne súkromný alebo ešte nemá vydanie.");
            }

            odpoved.EnsureSuccessStatusCode();
            json = await odpoved.Content.ReadAsStringAsync(limit.Token);
        }
        catch (HttpRequestException ex)
        {
            throw new SkvpException($"Nepodarilo sa spojiť s GitHubom. Skontrolujte pripojenie na internet.\n\n{ex.Message}");
        }
        catch (OperationCanceledException) when (!zrusenie.IsCancellationRequested)
        {
            throw new SkvpException("GitHub neodpovedal včas. Skúste to neskôr.");
        }

        ZapisKontrolu();
        return PrecitajVydanie(json) is { } vydanie && vydanie.Verzia > AktualnaVerzia ? vydanie : null;
    }

    /// <summary>Vydanie z odpovede GitHubu; null, ak nemá platnú verziu alebo inštalátor SkVP_Setup*.exe.</summary>
    public static DostupnaVerzia? PrecitajVydanie(string json)
    {
        var vydanie = JsonSerializer.Deserialize<VydanieGitHub>(json);
        if (vydanie is null || !Version.TryParse(vydanie.Znacka?.TrimStart('v', 'V'), out var verzia))
        {
            return null;
        }

        var instalator = vydanie.Subory?.FirstOrDefault(s =>
            s.Nazov is { } nazov && !string.IsNullOrEmpty(s.Adresa)
            && nazov.StartsWith("SkVP_Setup", StringComparison.OrdinalIgnoreCase) && nazov.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (instalator is null)
        {
            return null;
        }

        verzia = new Version(verzia.Major, verzia.Minor, Math.Max(0, verzia.Build));
        const string PredponaSha = "sha256:";
        var sha = instalator.Odtlacok is { } odtlacok && odtlacok.StartsWith(PredponaSha, StringComparison.OrdinalIgnoreCase) ? odtlacok[PredponaSha.Length..] : null;
        DateTime? zverejnene = DateTimeOffset.TryParse(vydanie.Zverejnene, CultureInfo.InvariantCulture, DateTimeStyles.None, out var cas)
            ? cas.LocalDateTime
            : null;
        return new DostupnaVerzia(verzia, vydanie.Nazov ?? $"SkVP {verzia}", vydanie.Popis ?? "", zverejnene, vydanie.Stranka ?? StrankaVydani,
            instalator.Nazov!, instalator.Adresa!, instalator.Velkost ?? 0, sha);
    }

    /// <summary>Stiahne inštalátor do dočasného priečinka a overí jeho veľkosť a kontrolný súčet. Vráti cestu k nemu.</summary>
    public static async Task<string> StiahniAsync(DostupnaVerzia verzia, IProgress<double>? priebeh, CancellationToken zrusenie = default)
    {
        var ciel = Path.Combine(Path.GetTempPath(), "SkVP_aktualizacia", verzia.SuborNazov);
        VysledokStahovania vysledok;
        try
        {
            vysledok = await Stahovanie.StiahniAsync(verzia.SuborAdresa,
                new CielStahovania(ciel, verzia.Sha256, verzia.SuborVelkost > 0 ? verzia.SuborVelkost : null), priebeh, zrusenie);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException
                                       || (ex is OperationCanceledException && !zrusenie.IsCancellationRequested))
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
    /// Spustí inštalátor v tichom režime (Windows sa opýta na povolenie – inštalácia do Program Files); po inštalácii sa
    /// program spustí znova. False = používateľ povolenie odmietol.
    /// </summary>
    public static bool SpustiInstalator(string instalator)
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
        catch (Win32Exception ex) when (ex.NativeErrorCode == ZrusenieUac)
        {
            return false;
        }
    }

    private static void ZapisKontrolu()
    {
        try
        {
            Databaza.Zapis(KlucPoslednaKontrola, Databaza.Teraz());
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // záznam o kontrole nie je dôležitý
        }
    }

    // odpoveď GitHub API (len použité údaje)

    private sealed record VydanieGitHub(
        [property: JsonPropertyName("tag_name")] string? Znacka,
        [property: JsonPropertyName("name")] string? Nazov,
        [property: JsonPropertyName("body")] string? Popis,
        [property: JsonPropertyName("published_at")] string? Zverejnene,
        [property: JsonPropertyName("html_url")] string? Stranka,
        [property: JsonPropertyName("assets")] List<SuborGitHub>? Subory);

    private sealed record SuborGitHub(
        [property: JsonPropertyName("name")] string? Nazov,
        [property: JsonPropertyName("browser_download_url")] string? Adresa,
        [property: JsonPropertyName("size")] long? Velkost,
        [property: JsonPropertyName("digest")] string? Odtlacok);
}
