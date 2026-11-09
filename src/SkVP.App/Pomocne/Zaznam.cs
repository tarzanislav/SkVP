using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using SkVP.Data;
using SkVP.Services;

namespace SkVP.Pomocne;

/// <summary>
/// Záznam činnosti programu (log) – chyby s technickými podrobnosťami a dôležité kroky (spustenie, import, zostavy).
/// Súbor na každý deň v priečinku Záznamy pri dátach programu, uchová sa 30 dní. Zápis nikdy nezastaví program.
/// Záznamy sa dajú v Servise uložiť do jedného súboru ZIP a poslať na podporu.
/// </summary>
public static class Zaznam
{
    private const int UchovatDni = 30;
    private static readonly object Zamok = new();

    public static string Priecinok => Path.Combine(Databaza.Priecinok, "Záznamy");

    private static string Subor => Path.Combine(Priecinok, $"skvp-{DateTime.Now:yyyy-MM-dd}.log");

    public static void Info(string text) => Zapis("INFO", text);

    public static void Upozornenie(string text) => Zapis("POZOR", text);

    public static void Chyba(string text, Exception? ex = null) =>
        Zapis("CHYBA", ex is null ? text : $"{text}{Environment.NewLine}{ex}");

    /// <summary>Úvodný záznam pri spustení: verzia programu a prostredie (na rozlíšenie, kde chyba vznikla).</summary>
    public static void Spustenie()
    {
        Info($"Spustenie SkVP {AktualizaciaService.AktualnaVerzia} · {RuntimeInformation.OSDescription} · .NET {Environment.Version} · "
             + $"{RuntimeInformation.ProcessArchitecture} · jazyk {CultureInfo.CurrentUICulture.Name} · dáta {Databaza.Priecinok}");
        UpracStare();
    }

    /// <summary>
    /// Všetky záznamy a základné údaje o programe v jednom súbore ZIP (na poslanie podpore). Dokumenty ani databáza
    /// sa nepribaľujú.
    /// </summary>
    public static void UlozBalik(string cesta)
    {
        Info("Záznamy uložené na odoslanie");
        using var zip = ZipFile.Open(cesta, ZipArchiveMode.Create);
        lock (Zamok)
        {
            if (Directory.Exists(Priecinok))
            {
                foreach (var subor in Directory.GetFiles(Priecinok, "skvp-*.log").Order())
                {
                    // súbor môže mať otvorený iný proces SkVP – číta sa so zdieľaním
                    using var zdroj = new FileStream(subor, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var ciel = zip.CreateEntry(Path.GetFileName(subor)).Open();
                    zdroj.CopyTo(ciel);
                }
            }
        }

        using var info = new StreamWriter(zip.CreateEntry("o-programe.txt").Open(), Encoding.UTF8);
        info.WriteLine($"SkVP {AktualizaciaService.AktualnaVerzia}");
        info.WriteLine($"Windows: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        info.WriteLine($".NET: {Environment.Version}");
        info.WriteLine($"Dáta: {Databaza.Priecinok}");
        info.WriteLine($"Vytvorené: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private static void Zapis(string druh, string text)
    {
        try
        {
            var riadok = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{druh}] {text}{Environment.NewLine}";
            lock (Zamok)
            {
                Directory.CreateDirectory(Priecinok);
                File.AppendAllText(Subor, riadok, Encoding.UTF8);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Debug.WriteLine($"Záznam sa nezapísal: {ex.Message}");
        }
    }

    private static void UpracStare()
    {
        try
        {
            var hranica = DateTime.Now.AddDays(-UchovatDni);
            foreach (var subor in Directory.GetFiles(Priecinok, "skvp-*.log").Where(s => File.GetLastWriteTime(s) < hranica))
            {
                File.Delete(subor);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // staré záznamy sa vymažú nabudúce
        }
    }
}
