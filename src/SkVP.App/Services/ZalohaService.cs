using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// Zálohy databázy (vrátane loga školy) do priečinka Dokumenty\SkVP\Zálohy. Súbory sú číslované za sebou:
/// SkVP_zaloha_0001_2026-10-23.zip, SkVP_zaloha_0002_… Pri prvom spustení dňa sa vytvorí automatická záloha.
/// </summary>
public sealed partial class ZalohaService
{
    private const string KlucPoslednaAutomaticka = "zaloha.posledna_automaticka";
    private const int PocetAutomatickych = 30;

    [GeneratedRegex(@"^SkVP_zaloha_(\d{4,})_", RegexOptions.IgnoreCase)]
    private static partial Regex VzorNazvu();

    /// <summary>Vytvorí zálohu a vráti cestu k nej.</summary>
    public string Zalohuj(bool automaticka = false)
    {
        Directory.CreateDirectory(Databaza.PriecinokZaloh);
        var cislo = DalsieCislo();
        var nazov = $"SkVP_zaloha_{cislo:0000}_{DateTime.Now:yyyy-MM-dd_HH-mm}{(automaticka ? "_auto" : "")}.zip";
        var ciel = Path.Combine(Databaza.PriecinokZaloh, nazov);
        var docasna = Path.Combine(Path.GetTempPath(), $"skvp_{Guid.NewGuid():N}.db");
        try
        {
            Databaza.Kopiruj(docasna);
            using var archiv = ZipFile.Open(ciel, ZipArchiveMode.Create);
            archiv.CreateEntryFromFile(docasna, "skvp.db", CompressionLevel.Optimal);
            if (NastaveniaSkoly.CestaLoga() is { } logo)
            {
                archiv.CreateEntryFromFile(logo, Path.GetFileName(logo), CompressionLevel.Optimal);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SkvpException($"Zálohu sa nepodarilo vytvoriť.\n\n{ex.Message}");
        }
        finally
        {
            try
            {
                File.Delete(docasna);
            }
            catch (IOException)
            {
                // dočasný súbor zmaže systém
            }
        }

        return ciel;
    }

    /// <summary>Automatická záloha raz za deň (pri spustení programu). Staré automatické zálohy nad 30 sa zmažú.</summary>
    public string? AutomatickaAkTreba()
    {
        var dnes = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (Databaza.Citaj(KlucPoslednaAutomaticka) == dnes)
        {
            return null;
        }

        var cesta = Zalohuj(automaticka: true);
        Databaza.Zapis(KlucPoslednaAutomaticka, dnes);
        foreach (var stara in Zalohy().Where(z => z.Name.Contains("_auto", StringComparison.OrdinalIgnoreCase)).Skip(PocetAutomatickych))
        {
            try
            {
                stara.Delete();
            }
            catch (IOException)
            {
                // zmaže sa nabudúce
            }
        }

        return cesta;
    }

    /// <summary>Zálohy od najnovšej.</summary>
    public List<FileInfo> Zalohy()
    {
        if (!Directory.Exists(Databaza.PriecinokZaloh))
        {
            return new List<FileInfo>();
        }

        return new DirectoryInfo(Databaza.PriecinokZaloh).GetFiles("SkVP_zaloha_*.zip")
            .OrderByDescending(f => Cislo(f.Name)).ThenByDescending(f => f.LastWriteTime).ToList();
    }

    private int DalsieCislo() => Zalohy().Select(f => Cislo(f.Name)).DefaultIfEmpty(0).Max() + 1;

    private static int Cislo(string nazov)
    {
        var zhoda = VzorNazvu().Match(nazov);
        return zhoda.Success && int.TryParse(zhoda.Groups[1].Value, out var c) ? c : 0;
    }
}
