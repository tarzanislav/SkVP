using System.IO;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Údaje z úvodnej tabuľky osnovy (profilácie a platnosť). Null = údaj sa v tabuľke nenašiel.</summary>
public sealed record UdajeOsnovy(string? Profilacie, string? Platnost)
{
    public static readonly UdajeOsnovy Prazdne = new(null, null);

    public bool MaUdaje => Profilacie is { Length: > 0 } || Platnost is { Length: > 0 };
}

/// <summary>
/// Úvodná tabuľka na začiatku dokumentu osnovy: riadok s profiláciami („Profilácia: šport, informatika, anglický jazyk,
/// STEAM“) a platnosťou („od 1.9.2026“). Údaje sa čítajú priamo z dokumentu Word a pri oprave sa do neho aj zapíšu,
/// takže sú vždy zhodné s osnovou. Hodnota je v bunke vedľa popisu, alebo v tej istej bunke za dvojbodkou.
/// </summary>
public static partial class UvodnaTabulka
{
    private const string Profil = "profilac";
    private const string Platnost = "platnost";

    [GeneratedRegex(@"\bod\s*\d{1,2}\.\s*\d{1,2}\.\s*\d{4}", RegexOptions.IgnoreCase)]
    private static partial Regex DatumOd();

    /// <summary>Prečíta profilácie a platnosť z dokumentu Word.</summary>
    public static UdajeOsnovy Precitaj(byte[]? docx)
    {
        if (docx is not { Length: > 0 })
        {
            return UdajeOsnovy.Prazdne;
        }

        try
        {
            using var prud = new MemoryStream(docx, false);
            using var dokument = WordprocessingDocument.Open(prud, false);
            if (Tabulka(dokument) is not { } tabulka)
            {
                return UdajeOsnovy.Prazdne;
            }

            var profilacie = Najdi(tabulka, Profil)?.Hodnota;
            var platnost = Najdi(tabulka, Platnost)?.Hodnota;
            if (platnost is null)
            {
                // platnosť bez popisu – text „od 1.9.2026“ kdekoľvek v tabuľke
                var text = string.Join(" ", tabulka.Descendants<TableCell>().Select(TextBunky));
                platnost = DatumOd().Match(text) is { Success: true } m ? m.Value : null;
            }

            return new UdajeOsnovy(Upravit(profilacie), Upravit(platnost));
        }
        catch (Exception)
        {
            return UdajeOsnovy.Prazdne;
        }
    }

    /// <summary>
    /// Názov predmetu z úvodnej tabuľky („Názov predmetu | Fyzika“, „Predmet: Fyzika“), alebo null. Podľa neho sa pri
    /// importe z priečinka rozpozná alebo vytvorí predmet.
    /// </summary>
    public static string? NazovPredmetu(byte[]? docx)
    {
        if (docx is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            using var prud = new MemoryStream(docx, false);
            using var dokument = WordprocessingDocument.Open(prud, false);
            if (Tabulka(dokument) is not { } tabulka)
            {
                return null;
            }

            var miesto = Najdi(tabulka, "nazov predmetu") ?? Najdi(tabulka, "nazov vyucovacieho predmetu") ?? Najdi(tabulka, "vyucovaci predmet")
                         ?? Najdi(tabulka, "ucebny predmet") ?? Najdi(tabulka, "predmet");
            return Upravit(miesto?.Hodnota);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Údaje osnovy predmetu z databázy.</summary>
    public static UdajeOsnovy Precitaj(long objektId) => Precitaj(new DokumentService().NacitajData(objektId));

    /// <summary>
    /// Zapíše opravené profilácie a platnosť do úvodnej tabuľky dokumentu a vráti nový dokument. Ak sa niektorý údaj
    /// v tabuľke nenájde, vyhodí chybu so zrozumiteľnou správou (dokument sa nemení).
    /// </summary>
    public static byte[] Zapis(byte[] docx, string? profilacie, string? platnost)
    {
        using var pamat = new MemoryStream();
        pamat.Write(docx, 0, docx.Length);
        pamat.Position = 0;
        using (var dokument = WordprocessingDocument.Open(pamat, true))
        {
            var tabulka = Tabulka(dokument) ?? throw new SkvpException("Na začiatku osnovy nie je úvodná tabuľka – údaje upravte priamo v dokumente.");
            if (profilacie is not null)
            {
                Nastav(tabulka, Profil, profilacie, "profilácie");
            }

            if (platnost is not null)
            {
                Nastav(tabulka, Platnost, platnost, "platnosť");
            }

            dokument.MainDocumentPart!.Document.Save();
        }

        return pamat.ToArray();
    }

    /// <summary>Úvodná tabuľka: prvá tabuľka dokumentu, ak je medzi prvými blokmi (pred hlavným textom).</summary>
    private static Table? Tabulka(WordprocessingDocument dokument)
    {
        var telo = dokument.MainDocumentPart?.Document?.Body;
        if (telo is null)
        {
            return null;
        }

        var poradie = 0;
        foreach (var prvok in telo.ChildElements)
        {
            if (prvok is Table tabulka)
            {
                return tabulka;
            }

            if (prvok is Paragraph p && p.InnerText.Trim().Length > 0 && ++poradie > 8)
            {
                return null; // tabuľka nie je na začiatku dokumentu
            }
        }

        return null;
    }

    private sealed record Miesto(TableCell Bunka, string Hodnota, bool VTejIstejBunke, string Popis);

    /// <summary>Bunka s popisom (napr. „Profilácia:“) a jej hodnota – v ďalšej bunke riadka alebo za dvojbodkou.</summary>
    private static Miesto? Najdi(Table tabulka, string kluc)
    {
        foreach (var riadok in tabulka.Elements<TableRow>())
        {
            var bunky = riadok.Elements<TableCell>().ToList();
            for (var i = 0; i < bunky.Count; i++)
            {
                var text = TextBunky(bunky[i]);
                var k = ImportOsnov.Kluc(text);
                if (!k.StartsWith(kluc, StringComparison.Ordinal))
                {
                    continue;
                }

                var dvojbodka = text.IndexOf(':');
                if (dvojbodka >= 0 && text[(dvojbodka + 1)..].Trim().Length > 0)
                {
                    return new Miesto(bunky[i], text[(dvojbodka + 1)..].Trim(), true, text[..dvojbodka].Trim());
                }

                if (i + 1 < bunky.Count)
                {
                    return new Miesto(bunky[i + 1], TextBunky(bunky[i + 1]).Trim(), false, text.Trim().TrimEnd(':'));
                }

                return new Miesto(bunky[i], "", true, text.Trim().TrimEnd(':'));
            }
        }

        return null;
    }

    private static void Nastav(Table tabulka, string kluc, string hodnota, string nazov)
    {
        var miesto = Najdi(tabulka, kluc)
                     ?? throw new SkvpException($"V úvodnej tabuľke osnovy sa nenašiel riadok „{(kluc == Profil ? "Profilácia" : "Platnosť")}“ – {nazov} upravte priamo v dokumente.");
        PrepisBunku(miesto.Bunka, miesto.VTejIstejBunke ? $"{miesto.Popis}: {hodnota.Trim()}" : hodnota.Trim());
    }

    /// <summary>Nový text bunky so zachovaním formátovania prvého behu a odseku.</summary>
    private static void PrepisBunku(TableCell bunka, string text)
    {
        var odseky = bunka.Elements<Paragraph>().ToList();
        var odsek = odseky.FirstOrDefault() ?? bunka.AppendChild(new Paragraph());
        foreach (var dalsi in odseky.Skip(1))
        {
            dalsi.Remove();
        }

        var vzor = odsek.Descendants<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true) as RunProperties;
        foreach (var beh in odsek.Elements().Where(e => e is not ParagraphProperties).ToList())
        {
            beh.Remove();
        }

        var novy = new Run();
        if (vzor is not null)
        {
            novy.Append(vzor);
        }

        novy.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        odsek.Append(novy);
    }

    private static string TextBunky(TableCell bunka) =>
        string.Join(" ", bunka.Elements<Paragraph>().Select(p => p.InnerText.Trim()).Where(t => t.Length > 0));

    private static string? Upravit(string? hodnota)
    {
        if (hodnota is null)
        {
            return null;
        }

        hodnota = Regex.Replace(hodnota, @"\s+", " ").Trim().TrimEnd('.', ';', ',');
        return hodnota.Length == 0 ? null : hodnota;
    }
}
