using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace SkVP.Services;

/// <summary>Čo sa sťahuje a ako sa to overí.</summary>
/// <param name="Cesta">kam sa súbor uloží (až keď je celý a overený)</param>
/// <param name="Sha256">očakávaný kontrolný súčet (hex); null = neoveruje sa</param>
/// <param name="Velkost">presná veľkosť v bajtoch, ak je známa (overí sa)</param>
/// <param name="OdhadVelkosti">približná veľkosť pre priebeh, keď ju server neuvedie</param>
public sealed record CielStahovania(string Cesta, string? Sha256 = null, long? Velkost = null, long OdhadVelkosti = 0);

/// <summary>Výsledok sťahovania – sieťové chyby sa hlásia výnimkou.</summary>
public enum VysledokStahovania
{
    Overeny,
    ZlaVelkost,
    ZlySucet,
}

/// <summary>
/// Sťahovanie veľkých súborov (modely prepisu, inštalátor novej verzie) s priebehom a zrušením. Kontrolný súčet
/// SHA-256 sa počíta už počas sťahovania, súbor sa netreba čítať druhýkrát. Do cieľa sa presunie, až keď je celý
/// a overený – nedokončený alebo poškodený súbor na disku nezostane.
/// </summary>
public static class Stahovanie
{
    private const int VelkostBloku = 1 << 16;

    /// <summary>Spoločný klient na sťahovanie – bez časového limitu (veľké súbory), ukončuje sa zrušením.</summary>
    public static HttpClient Klient { get; } = VytvorKlienta();

    public static async Task<VysledokStahovania> StiahniAsync(string adresa, CielStahovania ciel, IProgress<double>? priebeh, CancellationToken zrusenie)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(ciel.Cesta))!);
        var docasny = ciel.Cesta + ".stahuje";
        try
        {
            using var odpoved = await Klient.GetAsync(adresa, HttpCompletionOption.ResponseHeadersRead, zrusenie);
            odpoved.EnsureSuccessStatusCode();
            var spolu = odpoved.Content.Headers.ContentLength ?? ciel.Velkost ?? ciel.OdhadVelkosti;
            using var sucet = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long prijate = 0;
            await using (var zdroj = await odpoved.Content.ReadAsStreamAsync(zrusenie))
            await using (var subor = new FileStream(docasny, FileMode.Create, FileAccess.Write, FileShare.None, VelkostBloku, useAsync: true))
            {
                var blok = new byte[VelkostBloku];
                int precitane;
                while ((precitane = await zdroj.ReadAsync(blok, zrusenie)) > 0)
                {
                    sucet.AppendData(blok, 0, precitane);
                    await subor.WriteAsync(blok.AsMemory(0, precitane), zrusenie);
                    prijate += precitane;
                    if (spolu > 0)
                    {
                        priebeh?.Report(Math.Min(0.99, (double)prijate / spolu));
                    }
                }
            }

            if (ciel.Velkost is { } velkost && prijate != velkost)
            {
                return VysledokStahovania.ZlaVelkost;
            }

            if (ciel.Sha256 is { } ocakavany && !Convert.ToHexString(sucet.GetHashAndReset()).Equals(ocakavany, StringComparison.OrdinalIgnoreCase))
            {
                return VysledokStahovania.ZlySucet;
            }

            File.Move(docasny, ciel.Cesta, overwrite: true);
            priebeh?.Report(1);
            return VysledokStahovania.Overeny;
        }
        finally
        {
            ZmazTicho(docasny);
        }
    }

    /// <summary>SHA-256 súboru sa zhoduje s očakávaným.</summary>
    public static async Task<bool> OverAsync(string cesta, string sha256, CancellationToken zrusenie = default)
    {
        await using var subor = new FileStream(cesta, FileMode.Open, FileAccess.Read, FileShare.Read, VelkostBloku, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(subor, zrusenie)).Equals(sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static void ZmazTicho(string cesta)
    {
        try
        {
            File.Delete(cesta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // dočasný súbor prepíše ďalšie sťahovanie
        }
    }

    private static HttpClient VytvorKlienta()
    {
        var klient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        klient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkVP", AktualizaciaService.AktualnaVerzia.ToString()));
        return klient;
    }
}
