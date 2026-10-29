using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Threading;
using Microsoft.Win32;
using SkVP.Data;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>Microsoft Word na tomto počítači (nepovinný – bez neho sa upravuje vo vstavanom editore).</summary>
public static class Word
{
    public const string KlucRezimUpravy = "uprava.rezim";
    public const string RezimWord = "word";
    public const string RezimEditor = "editor";

    private static string? _cesta;
    private static bool _zistene;

    /// <summary>Cesta k WINWORD.EXE alebo null, ak Word nie je nainštalovaný.</summary>
    public static string? Cesta
    {
        get
        {
            if (!_zistene)
            {
                _cesta = Najdi();
                _zistene = true;
            }

            return _cesta;
        }
    }

    public static bool JeNainstalovany => Cesta is not null;

    /// <summary>Upravovať vo Worde: Word je nainštalovaný a používateľ to nevypol v nastaveniach.</summary>
    public static bool PouzivatWord => JeNainstalovany && Databaza.Citaj(KlucRezimUpravy, RezimWord) == RezimWord;

    private static string? Najdi()
    {
        try
        {
            foreach (var koren in new[] { Registry.LocalMachine, Registry.CurrentUser })
            {
                using var kluc = koren.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Winword.exe");
                if (kluc?.GetValue(null) is string cesta)
                {
                    cesta = cesta.Trim().Trim('"');
                    if (File.Exists(cesta))
                    {
                        return cesta;
                    }
                }
            }
        }
        catch (Exception)
        {
            // bez prístupu k registru sa Word nepoužije
        }

        return null;
    }
}

/// <summary>
/// Úprava dokumentu vo Worde: dokument z databázy sa uloží ako pracovná kópia s čitateľným názvom a otvorí vo Worde.
/// Po každom uložení vo Worde sa zmeny prevezmú späť do databázy. Ak program medzitým skončí, pri ďalšom spustení
/// sa neprevzaté zmeny ponúknu na obnovenie.
/// </summary>
public sealed class UpravaVoWorde : IDisposable
{
    private readonly FileSystemWatcher _sledovanie;
    private readonly DispatcherTimer _odklad;
    private string _poslednySha;

    private UpravaVoWorde(long objektId, string cesta, string sha)
    {
        ObjektId = objektId;
        Cesta = cesta;
        _poslednySha = sha;
        _odklad = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _odklad.Tick += (_, _) =>
        {
            _odklad.Stop();
            Skontroluj();
        };
        _sledovanie = new FileSystemWatcher(Path.GetDirectoryName(cesta)!, Path.GetFileName(cesta))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.CreationTime,
        };
        _sledovanie.Changed += (_, _) => Odloz();
        _sledovanie.Created += (_, _) => Odloz();
        _sledovanie.Renamed += (_, _) => Odloz();
        _sledovanie.EnableRaisingEvents = true;
    }

    public long ObjektId { get; }

    public string Cesta { get; }

    public DateTime Spustene { get; } = DateTime.Now;

    /// <summary>Word už mal súbor otvorený (zamknutý) – jeho odomknutie znamená koniec úpravy.</summary>
    public bool BolOtvoreny { get; set; }

    /// <summary>Vo Worde sa uložila nová verzia dokumentu (obsah súboru).</summary>
    public event Action<byte[]>? Ulozene;

    /// <summary>Priečinok pracovných kópií (Lokálne údaje aplikácií\SkVP\Úpravy vo Worde).</summary>
    public static string Priecinok =>
        Environment.GetEnvironmentVariable("SKVP_DATA_DIR") is { Length: > 0 }
            ? Path.Combine(Databaza.Priecinok, "UpravyVoWorde")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkVP", "Úpravy vo Worde");

    /// <summary>Pripraví pracovnú kópiu dokumentu a otvorí ju vo Worde.</summary>
    public static UpravaVoWorde Spusti(long objektId, string nazovSuboru, byte[] data, bool otvorit = true)
    {
        var priecinok = Path.Combine(Priecinok, objektId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(priecinok);
        var cesta = Path.Combine(priecinok, ImportOsnov.BezpecnyNazov(nazovSuboru, "osnova") + ".docx");
        foreach (var stary in Directory.GetFiles(priecinok, "*.docx"))
        {
            if (!string.Equals(stary, cesta, StringComparison.OrdinalIgnoreCase) && !JeOtvoreny(stary))
            {
                File.Delete(stary);
            }
        }

        if (!File.Exists(cesta) || !JeOtvoreny(cesta))
        {
            File.WriteAllBytes(cesta, data);
        }

        var uprava = new UpravaVoWorde(objektId, cesta, Convert.ToHexString(SHA256.HashData(data)));
        if (otvorit)
        {
            if (Dialogy.TichyRezim || Word.Cesta is null)
            {
                Dialogy.TicheHlasenia.Add($"[Word] {cesta}");
            }
            else
            {
                Process.Start(new ProcessStartInfo(Word.Cesta, $"\"{cesta}\"") { UseShellExecute = false });
            }
        }

        return uprava;
    }

    /// <summary>Je súbor ešte otvorený vo Worde (zamknutý)?</summary>
    public bool JeOtvorenyVoWorde => JeOtvoreny(Cesta);

    private static bool JeOtvoreny(string cesta)
    {
        if (!File.Exists(cesta))
        {
            return false;
        }

        try
        {
            using var _ = new FileStream(cesta, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private void Odloz()
    {
        // udalosti prichádzajú z iného vlákna
        _odklad.Dispatcher.BeginInvoke(() =>
        {
            _odklad.Stop();
            _odklad.Start();
        });
    }

    /// <summary>Prečíta pracovnú kópiu; ak sa zmenila, ohlási nový obsah.</summary>
    public void Skontroluj()
    {
        var data = PrecitajSubor(Cesta);
        if (data is null || data.Length == 0)
        {
            return;
        }

        var sha = Convert.ToHexString(SHA256.HashData(data));
        if (sha == _poslednySha)
        {
            return;
        }

        _poslednySha = sha;
        Ulozene?.Invoke(data);
    }

    private static byte[]? PrecitajSubor(string cesta)
    {
        for (var pokus = 0; pokus < 5; pokus++)
        {
            try
            {
                using var prud = new FileStream(cesta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var pamat = new MemoryStream();
                prud.CopyTo(pamat);
                return pamat.ToArray();
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                Thread.Sleep(200);
            }
        }

        return null;
    }

    /// <summary>Ukončí sledovanie; ak už Word súbor nemá otvorený, pracovná kópia sa zmaže.</summary>
    public void Dispose()
    {
        _odklad.Stop();
        _sledovanie.EnableRaisingEvents = false;
        _sledovanie.Dispose();
        try
        {
            if (!JeOtvoreny(Cesta))
            {
                Directory.Delete(Path.GetDirectoryName(Cesta)!, true);
            }
        }
        catch (Exception)
        {
            // pracovná kópia sa zmaže nabudúce
        }
    }

    /// <summary>Pracovné kópie, ktorých zmeny ešte nie sú v databáze (napr. program skončil počas úpravy).</summary>
    public static List<(long ObjektId, string Cesta, byte[] Data)> NajdiNeprevzate(DokumentService dokumenty)
    {
        var vysledok = new List<(long, string, byte[])>();
        if (!Directory.Exists(Priecinok))
        {
            return vysledok;
        }

        foreach (var priecinok in Directory.GetDirectories(Priecinok))
        {
            if (!long.TryParse(Path.GetFileName(priecinok), out var id))
            {
                continue;
            }

            foreach (var subor in Directory.GetFiles(priecinok, "*.docx").Where(s => !Path.GetFileName(s).StartsWith("~$", StringComparison.Ordinal)))
            {
                var data = PrecitajSubor(subor);
                if (data is null || data.Length == 0 || JeOtvoreny(subor))
                {
                    continue;
                }

                if (Convert.ToHexString(SHA256.HashData(data)) != dokumenty.Sha(id))
                {
                    vysledok.Add((id, subor, data));
                }
                else
                {
                    try
                    {
                        Directory.Delete(priecinok, true);
                    }
                    catch (Exception)
                    {
                        // nevadí
                    }
                }
            }
        }

        return vysledok;
    }
}
