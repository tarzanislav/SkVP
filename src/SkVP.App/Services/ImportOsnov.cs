using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>Súbor pripravený na import osnovy – obsah ako dokument Word a rozpoznaný predmet.</summary>
public sealed class SuborImportu
{
    public required string Cesta { get; init; }
    public string NazovSuboru => Path.GetFileName(Cesta);
    public bool JePdf { get; init; }

    /// <summary>Obsah ako dokument Word (pri PDF prevedený vlastnými prostriedkami programu).</summary>
    public required byte[] Docx { get; init; }

    public required string Text { get; init; }
    public long? PredmetId { get; init; }

    /// <summary>Názov nového predmetu, ak sa v programe nenašiel (z názvu súboru alebo textu).</summary>
    public string NavrhNazvu { get; init; } = "";

    /// <summary>Rok zmeny osnov z názvu súboru (napr. „05 Fyzika 2026.docx“).</summary>
    public int? Rok { get; init; }

    /// <summary>Jazykový predmet podľa pravidiel oblasti Jazyk a komunikácia (skratky, profilácia, úroveň) alebo null.</summary>
    public JazykovyPredmet? Jazyk { get; init; }
}

/// <summary>Výsledok importu jednej osnovy.</summary>
public sealed record VysledokImportuOsnovy(long PredmetId, string Nazov, bool Archivovane, bool Zmenene, string? Subor);

/// <summary>
/// Import učebnej osnovy z Wordu (DOCX) alebo PDF do predmetu (existujúceho alebo nového). Zdrojový súbor sa len číta.
/// PDF sa prevedie na text osnovy (nadpisy, odseky, zoznamy, tabuľky) bez Wordu. Doterajšia osnova sa najprv zarchivuje
/// so záznamom, kedy a v ktorom roku sa to stalo („05 Fyzika – archív 2026“); nová osnova dostane rok zmeny
/// („05 Fyzika 2026“) a kópia sa uloží do priečinka Osnovy.
/// </summary>
public static partial class ImportOsnov
{
    public static readonly string[] Pripony = { ".docx", ".pdf" };

    public const string FilterSuborov = "Osnovy vo Worde alebo PDF (*.docx;*.pdf)|*.docx;*.pdf|Word (*.docx)|*.docx|PDF (*.pdf)|*.pdf";

    /// <summary>Priečinok s importovanými osnovami („05 Fyzika 2026.docx“) a archívom („Archív\05 Fyzika – archív 2026.docx“).</summary>
    public static string PriecinokOsnov => Path.Combine(Databaza.PriecinokDokumentov, "Osnovy");

    [GeneratedRegex(@"\b(19[5-9]\d|20\d\d)\b")]
    private static partial Regex RokVTexte();

    [GeneratedRegex(@"^\s*\d{1,3}[\s_.\-]+")]
    private static partial Regex CisloNaZaciatku();

    /// <summary>Prečíta súbor (Word alebo PDF) a rozpozná predmet.</summary>
    public static SuborImportu Nacitaj(string cesta, IReadOnlyList<(long Id, string Nazov)> predmety)
    {
        var pripona = Path.GetExtension(cesta).ToLowerInvariant();
        byte[] docx;
        string text;
        IReadOnlyList<Blok> bloky;
        if (pripona == ".docx")
        {
            try
            {
                using var prud = new FileStream(cesta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var pamat = new MemoryStream();
                prud.CopyTo(pamat);
                docx = pamat.ToArray();
            }
            catch (IOException ex)
            {
                throw new SkvpException($"Súbor {Path.GetFileName(cesta)} sa nedá otvoriť: {ex.Message}");
            }

            using var citanie = new MemoryStream(docx, false);
            bloky = DocxCitac.Nacitaj(citanie).Bloky;
            text = DokumentOsnovy.Text(bloky);
        }
        else if (pripona == ".pdf")
        {
            text = Task.Run(() => CitaniePdf.PrecitajAsync(cesta)).GetAwaiter().GetResult().Text;
            if (text.Trim().Length == 0)
            {
                throw new SkvpException($"V súbore {Path.GetFileName(cesta)} sa nenašiel žiadny text.");
            }

            bloky = TextNaOsnovu.Rozober(text);
            docx = DocxVystup.Osnova(bloky);
        }
        else if (pripona == ".doc")
        {
            throw new SkvpException($"{Path.GetFileName(cesta)} je v starom formáte Word 97–2003 (.doc). Otvorte ho vo Worde, uložte ako .docx a naimportujte znova.");
        }
        else
        {
            throw new SkvpException($"{Path.GetFileName(cesta)}: podporované sú dokumenty Word (.docx) a PDF.");
        }

        var jazyk = JazykovePravidla.Rozpoznaj(Path.GetFileName(cesta), text, bloky);
        var (id, navrh) = jazyk is null
            ? Rozpoznaj(Path.GetFileNameWithoutExtension(cesta), text, predmety)
            : (Kandidati(jazyk, predmety).Select(p => (long?)p.Id).FirstOrDefault(), JazykovePravidla.NazovSUrovnou(jazyk.Zaklad, jazyk.Uroven));
        return new SuborImportu
        {
            Cesta = cesta,
            JePdf = pripona == ".pdf",
            Docx = docx,
            Text = text,
            PredmetId = id,
            NavrhNazvu = navrh,
            Jazyk = jazyk,
            Rok = RokVTexte().Match(Path.GetFileNameWithoutExtension(cesta)) is { Success: true } m ? int.Parse(m.Value, CultureInfo.InvariantCulture) : null,
        };
    }

    /// <summary>
    /// Predmety programu, do ktorých patrí jazyková osnova – v poradí: rovnaký názov s úrovňou, rovnaký názov bez úrovne,
    /// doterajší názov (napr. „Konverzácia v anglickom jazyku“ pred rozdelením podľa profilácie).
    /// </summary>
    public static List<(long Id, string Nazov)> Kandidati(JazykovyPredmet jazyk, IReadOnlyList<(long Id, string Nazov)> predmety)
    {
        var vysledok = new List<(long Id, string Nazov)>();
        void Pridaj(IEnumerable<(long Id, string Nazov)> zhody)
        {
            foreach (var z in zhody)
            {
                if (vysledok.All(v => v.Id != z.Id))
                {
                    vysledok.Add(z);
                }
            }
        }

        var cely = Kluc(JazykovePravidla.NazovSUrovnou(jazyk.Zaklad, jazyk.Uroven));
        var zaklad = Kluc(jazyk.Zaklad);
        Pridaj(predmety.Where(p => Kluc(p.Nazov) == cely));
        Pridaj(predmety.Where(p => Kluc(JazykovePravidla.BezUrovne(p.Nazov)) == zaklad));
        foreach (var alias in jazyk.Aliasy)
        {
            Pridaj(predmety.Where(p => Kluc(JazykovePravidla.BezUrovne(p.Nazov)) == Kluc(alias)));
        }

        return vysledok;
    }

    /// <summary>
    /// Predmet podľa názvu súboru („05 Fyzika 2026.docx“ → Fyzika) alebo podľa textu dokumentu (najdlhší názov predmetu
    /// v úvode dokumentu). Ak sa nenájde, navrhne sa názov nového predmetu z názvu súboru.
    /// </summary>
    public static (long? Id, string Navrh) Rozpoznaj(string nazovSuboru, string text, IReadOnlyList<(long Id, string Nazov)> predmety)
    {
        var zoSuboru = CistyNazov(nazovSuboru);
        var kluc = Kluc(zoSuboru);
        if (predmety.FirstOrDefault(p => Kluc(p.Nazov) == kluc || Kluc(JazykovePravidla.BezUrovne(p.Nazov)) == kluc) is { Id: > 0 } presny)
        {
            return (presny.Id, presny.Nazov);
        }

        var uvod = Kluc(string.Join(" ", text.Replace("\r", "").Split('\n').Where(r => r.Trim().Length > 0).Take(15)));
        var vTexte = predmety.Where(p => Kluc(p.Nazov).Length > 2 && ObsahujeSlovo(uvod, Kluc(p.Nazov)))
            .OrderByDescending(p => p.Nazov.Length).FirstOrDefault();
        if (vTexte.Id > 0)
        {
            return (vTexte.Id, vTexte.Nazov);
        }

        var vSubore = predmety.Where(p => Kluc(p.Nazov).Length > 2 && ObsahujeSlovo(kluc, Kluc(p.Nazov)))
            .OrderByDescending(p => p.Nazov.Length).FirstOrDefault();
        return vSubore.Id > 0 ? (vSubore.Id, vSubore.Nazov) : (null, zoSuboru);
    }

    /// <summary>Názov zo súboru bez čísla poradia, roku a podčiarkovníkov: „05_Fyzika_2026“ → „Fyzika“.</summary>
    public static string CistyNazov(string nazovSuboru)
    {
        var nazov = nazovSuboru.Replace('_', ' ');
        nazov = CisloNaZaciatku().Replace(nazov, "");
        nazov = RokVTexte().Replace(nazov, "");
        nazov = Regex.Replace(nazov, @"\s*[–\-]\s*archív\s*$", "", RegexOptions.IgnoreCase);
        nazov = Regex.Replace(nazov, @"\s{2,}", " ").Trim(' ', '-', '–', '.', '(', ')');
        return nazov.Length > 0 ? char.ToUpper(nazov[0], CultureInfo.CurrentCulture) + nazov[1..] : nazov;
    }

    /// <summary>Text na porovnanie: malé písmená bez diakritiky.</summary>
    public static string Kluc(string text)
    {
        var rozlozeny = text.ToLower(CultureInfo.GetCultureInfo("sk-SK")).Normalize(NormalizationForm.FormD);
        var vysledok = new StringBuilder();
        foreach (var znak in rozlozeny)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(znak) != UnicodeCategory.NonSpacingMark)
            {
                vysledok.Append(char.IsLetterOrDigit(znak) ? znak : ' ');
            }
        }

        return Regex.Replace(vysledok.ToString(), @"\s+", " ").Trim();
    }

    private static bool ObsahujeSlovo(string text, string hladany) =>
        $" {text} ".Contains($" {hladany} ", StringComparison.Ordinal);

    /// <summary>Predmety programu (id a názov) na rozpoznanie.</summary>
    public static List<(long Id, string Nazov)> Predmety()
    {
        var sluzba = new ObjektService();
        return sluzba.Oblasti().SelectMany(o => sluzba.Deti(o.Id)).Where(p => p.Typ == TypObjektu.Predmet).Select(p => (p.Id, p.Nazov)).ToList();
    }

    /// <summary>Číslo predmetu v oblasti (01, 02 …).</summary>
    public static string Cislo(long predmetId)
    {
        var sluzba = new ObjektService();
        if (sluzba.Nacitaj(predmetId) is { RodicId: long rodic })
        {
            var index = sluzba.Deti(rodic).FindIndex(p => p.Id == predmetId);
            if (index >= 0)
            {
                return (index + 1).ToString("00", CultureInfo.InvariantCulture);
            }
        }

        return "00";
    }

    /// <summary>
    /// Uloží osnovu do predmetu: doterajšia sa zarchivuje (v databáze aj ako súbor „… – archív rok.docx“), nová dostane rok
    /// zmeny osnov a jej kópia sa uloží ako „číslo názov rok.docx“.
    /// </summary>
    public static VysledokImportuOsnovy Importuj(SuborImportu subor, long predmetId, int rok, string? uroven = null)
    {
        var sluzba = new ObjektService();
        var predmet = sluzba.Nacitaj(predmetId) ?? throw new SkvpException("Predmet sa nenašiel.");
        var dokumenty = new DokumentService();
        var cislo = Cislo(predmetId);
        var teraz = DateTime.Now;
        var archivovane = false;
        if (dokumenty.NacitajData(predmetId) is { Length: > 0 } stare)
        {
            var priecinokArchivu = Path.Combine(PriecinokOsnov, "Archív");
            Directory.CreateDirectory(priecinokArchivu);
            File.WriteAllBytes(VolnaCesta(priecinokArchivu, InfoOsnovyService.NazovArchivu(cislo, predmet.Nazov, teraz.Year), ".docx"), stare);
            archivovane = true;
        }

        var dovod = $"Archivované {teraz.ToString("d. M. yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))} (rok {teraz.Year}) – pred importom novej osnovy zo súboru {subor.NazovSuboru}";
        var zmenene = dokumenty.Uloz(predmetId, subor.Docx, dovod);
        InfoOsnovyService.Uloz(predmetId, new InfoOsnovy(rok, teraz, subor.NazovSuboru, uroven is { Length: > 0 } ? uroven : null));

        string? kopia = null;
        try
        {
            var priecinok = Path.Combine(PriecinokOsnov, rok.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(priecinok);
            kopia = Path.Combine(priecinok, BezpecnyNazov(InfoOsnovyService.NazovDokumentu(cislo, predmet.Nazov, rok)) + ".docx");
            File.WriteAllBytes(kopia, subor.Docx);
        }
        catch (IOException)
        {
            kopia = null; // kópia v priečinku Osnovy je len pomôcka – osnova je uložená v databáze
        }

        return new VysledokImportuOsnovy(predmetId, predmet.Nazov, archivovane && zmenene, zmenene, kopia);
    }

    private static string VolnaCesta(string priecinok, string nazov, string pripona)
    {
        var zaklad = BezpecnyNazov(nazov);
        var cesta = Path.Combine(priecinok, zaklad + pripona);
        for (var i = 2; File.Exists(cesta); i++)
        {
            cesta = Path.Combine(priecinok, $"{zaklad} ({i}){pripona}");
        }

        return cesta;
    }

    public static string BezpecnyNazov(string nazov) =>
        new string(nazov.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
}
