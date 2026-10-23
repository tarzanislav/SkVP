using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>
/// Zálohy databázy (vrátane loga školy) do priečinka Dokumenty\SkVP\Zálohy. Súbory sú číslované za sebou:
/// SkVP_zaloha_0001_2026-10-23.zip, SkVP_zaloha_0002_… Pri prvom spustení dňa sa vytvorí automatická záloha.
/// </summary>
public sealed partial class ZalohaService
{
    private const string KlucPoslednaAutomaticka = "zaloha.posledna_automaticka";
    public const string KlucFrekvencia = "zaloha.frekvencia";
    public const string KlucPonechat = "zaloha.ponechat";

    public const string Denne = "denne";
    public const string Tyzdenne = "tyzdenne";
    public const string Vypnute = "vypnute";

    /// <summary>Ako často sa zálohuje automaticky (pri spustení programu): denne, týždenne alebo vypnuté.</summary>
    public static string Frekvencia
    {
        get => Databaza.Citaj(KlucFrekvencia) is Tyzdenne or Vypnute ? Databaza.Citaj(KlucFrekvencia)! : Denne;
        set => Databaza.Zapis(KlucFrekvencia, value);
    }

    /// <summary>Deň poslednej automatickej zálohy (null = ešte nebola).</summary>
    public static DateTime? PoslednaAutomaticka =>
        DateTime.TryParseExact(Databaza.Citaj(KlucPoslednaAutomaticka), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    /// <summary>Koľko automatických záloh sa ponechá (staršie sa zmažú).</summary>
    public static int Ponechat
    {
        get => int.TryParse(Databaza.Citaj(KlucPonechat), out var n) ? Math.Clamp(n, 3, 999) : 30;
        set => Databaza.Zapis(KlucPonechat, Math.Clamp(value, 3, 999).ToString(CultureInfo.InvariantCulture));
    }

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

    /// <summary>
    /// Automatická záloha pri spustení programu podľa nastavenia (raz za deň alebo raz za týždeň). Staršie automatické
    /// zálohy nad nastavený počet sa zmažú.
    /// </summary>
    public string? AutomatickaAkTreba()
    {
        var dnes = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var posledna = Databaza.Citaj(KlucPoslednaAutomaticka);
        if (Frekvencia == Vypnute || posledna == dnes)
        {
            return null;
        }

        if (Frekvencia == Tyzdenne && DateTime.TryParseExact(posledna, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var datum)
            && (DateTime.Today - datum).TotalDays < 7)
        {
            return null;
        }

        var cesta = Zalohuj(automaticka: true);
        Databaza.Zapis(KlucPoslednaAutomaticka, dnes);
        foreach (var stara in Zalohy().Where(z => z.Name.Contains("_auto", StringComparison.OrdinalIgnoreCase)).Skip(Ponechat))
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

    /// <summary>Posledná záloha (alebo null).</summary>
    public FileInfo? Posledna() => Zalohy().FirstOrDefault();

    /// <summary>
    /// Obnoví databázu zo zálohy (ZIP zo SkVP). Najprv overí, že záloha je v poriadku a nie je z novšej verzie programu,
    /// a zálohuje súčasný stav. Program sa potom musí spustiť znova.
    /// </summary>
    public void Obnov(string zip)
    {
        var docasna = Path.Combine(Path.GetTempPath(), $"skvp_obnova_{Guid.NewGuid():N}.db");
        string? logo = null;
        byte[]? dataLoga = null;
        try
        {
            using (var archiv = ZipFile.OpenRead(zip))
            {
                var db = archiv.GetEntry("skvp.db") ?? throw new SkvpException("Súbor nie je záloha SkVP (chýba v ňom databáza).");
                db.ExtractToFile(docasna, true);
                if (archiv.Entries.FirstOrDefault(e => e.Name.StartsWith("logo_skoly", StringComparison.OrdinalIgnoreCase)) is { } polozkaLoga)
                {
                    logo = polozkaLoga.Name;
                    using var prud = polozkaLoga.Open();
                    using var pamat = new MemoryStream();
                    prud.CopyTo(pamat);
                    dataLoga = pamat.ToArray();
                }
            }

            OverZalohu(docasna);
            Zalohuj();
            foreach (var pripona in new[] { "-wal", "-shm" })
            {
                File.Delete(Databaza.Cesta + pripona);
            }

            File.Copy(docasna, Databaza.Cesta, true);
            if (logo is not null && dataLoga is not null)
            {
                File.WriteAllBytes(Path.Combine(Databaza.Priecinok, logo), dataLoga);
            }

            Databaza.Inicializuj();
        }
        catch (InvalidDataException)
        {
            throw new SkvpException("Súbor nie je platný archív ZIP.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SkvpException($"Zálohu sa nepodarilo obnoviť.\n\n{ex.Message}");
        }
        finally
        {
            try
            {
                File.Delete(docasna);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void OverZalohu(string cesta)
    {
        using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={cesta};Mode=ReadOnly;Pooling=False");
        db.Open();
        var verzia = Convert.ToInt32(Databaza.Skalar(db, "PRAGMA user_version;") ?? 0L, CultureInfo.InvariantCulture);
        if (verzia > Databaza.VerziaSchemy)
        {
            throw new SkvpException("Zálohu vytvorila novšia verzia programu SkVP. Najprv nainštalujte najnovšiu verziu.");
        }

        if (Databaza.Skalar(db, "PRAGMA integrity_check;") as string != "ok")
        {
            throw new SkvpException("Záloha je poškodená a nedá sa obnoviť. Vyberte inú zálohu.");
        }
    }

    /// <summary>Kontrola databázy (integrita). Vráti text výsledku.</summary>
    public static string Skontroluj()
    {
        using var db = Databaza.Otvor();
        var vysledok = Databaza.Skalar(db, "PRAGMA integrity_check;") as string;
        return vysledok == "ok" ? "Databáza je v poriadku." : $"Databáza má chyby: {vysledok}. Obnovte ju zo zálohy.";
    }

    /// <summary>Zmenší databázu (uvoľní nepoužité miesto). Vráti veľkosť pred a po v bajtoch.</summary>
    public static (long Pred, long Po) Zmensi()
    {
        var pred = new FileInfo(Databaza.Cesta).Length;
        using (var db = Databaza.Otvor())
        {
            Databaza.Vykonaj(db, "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;");
        }

        return (pred, new FileInfo(Databaza.Cesta).Length);
    }

    /// <summary>
    /// Záloha učebných osnov vo Worde a PDF (ZIP): aktuálne osnovy „01 Jazyk a komunikácia\01 Slovenský jazyk a literatúra 2026.docx“
    /// a vedľa nich rovnaký dokument v PDF, charakteristiky oblastí a celý archív starších verzií („Archív\… – archív 2025.docx“ + .pdf).
    /// Dá sa otvoriť aj bez programu.
    /// </summary>
    public static string ZalohujOsnovy(string? ciel = null)
    {
        Directory.CreateDirectory(Databaza.PriecinokZaloh);
        ciel ??= Path.Combine(Databaza.PriecinokZaloh, $"SkVP_osnovy_{DateTime.Now:yyyy-MM-dd_HH-mm}.zip");
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        if (File.Exists(ciel))
        {
            File.Delete(ciel);
        }

        var udaje = UdajeSkVP.Nacitaj();
        var titul = TitulnaStrana.Nacitaj();
        using var archiv = ZipFile.Open(ciel, ZipArchiveMode.Create);
        var pouzite = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Pridaj(string cesta, byte[] data, Func<byte[]?> pdf)
        {
            var zaklad = cesta[..^5];
            for (var i = 2; !pouzite.Add(zaklad + ".docx"); i++)
            {
                zaklad = $"{cesta[..^5]} ({i})";
            }

            using (var prud = archiv.CreateEntry(zaklad + ".docx", CompressionLevel.Optimal).Open())
            {
                prud.Write(data);
            }

            if (pdf() is { Length: > 0 } dataPdf)
            {
                using var prud = archiv.CreateEntry(zaklad + ".pdf", CompressionLevel.Optimal).Open();
                prud.Write(dataPdf);
            }
        }

        var cisloOblasti = 0;
        // zálohujú sa aj vypnuté úvodné a záverečné časti učebných osnov (nič sa nestratí)
        foreach (var oblast in CastiOsnov.SkupinyVPoradi(sluzba, ajVypnute: true))
        {
            var jeOblast = oblast.Typ == TypObjektu.Oblast;
            if (jeOblast)
            {
                cisloOblasti++;
            }

            var farba = oblast.Farba ?? (jeOblast ? UcebnyPlanService.FarbaOblasti(cisloOblasti - 1) : CastiOsnov.Farba(oblast.Typ));
            var priecinok = ImportOsnov.BezpecnyNazov(jeOblast ? $"{cisloOblasti:00} {oblast.Nazov}" : CastiOsnov.Priecinok(oblast.Typ));
            if (jeOblast && dokumenty.NacitajData(oblast.Id) is { Length: > 0 } charakteristika)
            {
                Pridaj($"{priecinok}/00 Charakteristika – {ImportOsnov.BezpecnyNazov(oblast.Nazov)}.docx", charakteristika,
                    () => PdfDokumentu(charakteristika, udaje, titul, oblast.Nazov, farba, null, 0, StavOsnovy.Rozpracovana));
            }

            var cislo = 0;
            foreach (var predmet in CastiOsnov.Dokumenty(sluzba, oblast))
            {
                cislo++;
                var c = cislo.ToString("00", CultureInfo.InvariantCulture);
                var poradie = cislo;
                if (dokumenty.NacitajData(predmet.Id) is { Length: > 0 } osnova)
                {
                    var rok = InfoOsnovyService.Nacitaj(predmet.Id).Rok;
                    var stav = StavyOsnovy.Urci(predmet.Stav, predmet.DlzkaTextu);
                    Pridaj($"{priecinok}/{ImportOsnov.BezpecnyNazov(InfoOsnovyService.NazovDokumentu(c, predmet.Nazov, rok))}.docx", osnova,
                        () => PdfDokumentu(osnova, udaje, titul, oblast.Nazov, farba, predmet, poradie, stav));
                }

                foreach (var verzia in dokumenty.Verzie(predmet.Id))
                {
                    if (dokumenty.DataVerzie(verzia.Id) is { Length: > 0 } stara)
                    {
                        Pridaj($"{priecinok}/Archív/{ImportOsnov.BezpecnyNazov(InfoOsnovyService.NazovArchivu(c, predmet.Nazov, verzia.Vytvorene.Year))} {verzia.Vytvorene:yyyy-MM-dd HH-mm}.docx", stara,
                            () => PdfDokumentu(stara, udaje, titul, oblast.Nazov, farba, predmet, poradie, StavOsnovy.Rozpracovana));
                    }
                }
            }
        }

        return ciel;
    }

    /// <summary>
    /// Dokument osnovy (alebo charakteristiky oblasti) ako PDF – rovnaký vzhľad ako pri tlači, bez úvodnej strany.
    /// Dokument, ktorý sa nedá prečítať, sa vynechá (Word v zálohe zostane).
    /// </summary>
    public static byte[]? PdfDokumentu(byte[] docx, UdajeSkVP udaje, TitulnaStrana titul, string oblast, string farba, Objekt? predmet, int poradie, StavOsnovy stav)
    {
        try
        {
            List<Blok> bloky;
            using (var prud = new MemoryStream(docx, false))
            {
                bloky = DocxCitac.Nacitaj(prud).Bloky;
            }

            var predmety = predmet is null
                ? new List<KapitolaPredmetu>()
                : new List<KapitolaPredmetu> { new(predmet.Id, poradie, predmet.Nazov, bloky, stav) };
            var volby = new VolbyZostavy
            {
                TitulnaStrana = false,
                Obsah = false,
                OddelovacOblasti = false,
                OddelovacPredmetu = false,
                CharakteristikyOblasti = predmet is null,
                NespracovanePredmety = true,
            };
            var celok = new CelyDokument(udaje, volby,
                new List<KapitolaOblasti> { new(0, oblast, farba, predmet is null ? bloky : new List<Blok>(), predmety) }, DateTime.Now) { Titul = titul };
            return SuboryZostavy.Vytvor(PdfVystup.Vytvor(celok), predmet?.Nazov ?? oblast, new VolbyPdf()).FirstOrDefault()?.Data;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return null;
        }
    }

    private int DalsieCislo() => Zalohy().Select(f => Cislo(f.Name)).DefaultIfEmpty(0).Max() + 1;

    private static int Cislo(string nazov)
    {
        var zhoda = VzorNazvu().Match(nazov);
        return zhoda.Success && int.TryParse(zhoda.Groups[1].Value, out var c) ? c : 0;
    }
}
