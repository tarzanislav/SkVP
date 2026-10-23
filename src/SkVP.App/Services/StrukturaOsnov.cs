using System.IO;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>
/// Import a export vzdelávacích oblastí a predmetov z/do dokumentu: Excel (stĺpec A oblasť, B predmet), Word (tabuľka
/// alebo nadpisy a zoznamy) a PDF (prečítaný text). Export aj do PDF, PDF/A, PDF/X a obrázkov (cez voľby PDF).
/// </summary>
public static class StrukturaOsnov
{
    public const string FilterImportu = ImportStruktury.FilterSuborov;

    /// <summary>Načíta oblasti a predmety zo súboru – program ich sám vyhľadá v tabuľke alebo v texte.</summary>
    public static UcebnyPlan NacitajZoSuboru(string cesta)
    {
        if (ImportStruktury.ZTextovehoSuboru(cesta) is { } plan)
        {
            return plan;
        }

        return Path.GetExtension(cesta).ToLowerInvariant() switch
        {
            ".xlsx" => UcebnyPlanService.NacitajZExcelu(cesta),
            ".docx" => ZWordu(cesta),
            ".pdf" => ZTextu(Task.Run(() => CitaniePdf.PrecitajAsync(cesta)).GetAwaiter().GetResult().Text),
            _ => throw new SkvpException("Podporované sú súbory Excel (.xlsx), Word (.docx), PDF, HTML, XML, JSON a text (.txt)."),
        };
    }

    /// <summary>Word: tabuľka (prvé dva stĺpce = oblasť, predmet), inak nadpisy/tučné riadky = oblasti a zoznamy = predmety.</summary>
    public static UcebnyPlan ZWordu(string cesta)
    {
        NacitanyDokument dokument;
        try
        {
            using var prud = new FileStream(cesta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            dokument = DocxCitac.Nacitaj(prud);
        }
        catch (IOException ex)
        {
            throw new SkvpException($"Súbor sa nedá otvoriť. Ak je otvorený vo Worde, zatvorte ho.\n\n{ex.Message}");
        }

        return ZBlokov(dokument.Bloky);
    }

    public static UcebnyPlan ZBlokov(IReadOnlyList<Blok> bloky)
    {
        var riadky = new List<(string A, string B)>();
        var tabulky = bloky.OfType<Tabulka>().ToList();
        if (tabulky.Count > 0)
        {
            foreach (var tabulka in tabulky)
            {
                var poslednaOblast = "";
                foreach (var riadok in tabulka.Riadky)
                {
                    var a = riadok.Count > 0 ? DokumentOsnovy.Text(riadok[0].Bloky).Trim() : "";
                    var b = riadok.Count > 1 ? DokumentOsnovy.Text(riadok[1].Bloky).Trim() : "";
                    if (riadok.Count > 0 && riadok[0].Hlavicka || JeHlavickaTabulky(a, b))
                    {
                        continue;
                    }

                    // predmety v jednej bunke pod sebou
                    var predmety = b.Replace("\r", "").Split('\n').Select(x => x.Trim(' ', '•', '-', '–')).Where(x => x.Length > 0).ToList();
                    if (a.Length > 0)
                    {
                        poslednaOblast = a;
                    }

                    if (predmety.Count == 0 && a.Length > 0)
                    {
                        riadky.Add((a, ""));
                    }

                    foreach (var predmet in predmety)
                    {
                        riadky.Add((poslednaOblast, predmet));
                    }
                }
            }
        }
        else
        {
            foreach (var blok in bloky)
            {
                switch (blok)
                {
                    case Nadpis n when n.Text.Trim().Length > 0:
                        riadky.Add((n.Text.Trim(), ""));
                        break;
                    case Zoznam z:
                        riadky.AddRange(z.Polozky.Select(p => ("", DokumentOsnovy.Text(p).Trim())).Where(r => r.Item2.Length > 0));
                        break;
                    case Odsek o when DokumentOsnovy.Text(new[] { o }).Trim() is { Length: > 0 } text:
                        var tucny = o.Useky.Where(u => u.Text.Trim().Length > 0).All(u => u.Tucne);
                        riadky.Add(tucny || text.EndsWith(':') ? (text.TrimEnd(':'), "") : ("", text));
                        break;
                }
            }
        }

        return UcebnyPlanService.Rozober(riadky);
    }

    /// <summary>PDF (prečítaný text): riadok s tabulátorom = oblasť a predmet, známe oblasti, riadky s dvojbodkou alebo veľkými písmenami = oblasti.</summary>
    public static UcebnyPlan ZTextu(string text)
    {
        var zname = ZnameOblasti();
        var riadky = new List<(string A, string B)>();
        var poslednaOblast = "";
        foreach (var surovy in text.Replace("\r", "").Split('\n'))
        {
            var riadok = surovy.Trim();
            if (riadok.Length == 0)
            {
                continue;
            }

            if (riadok.Contains('\t'))
            {
                var casti = riadok.Split('\t').Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
                if (casti.Length >= 2 && !JeHlavickaTabulky(casti[0], casti[1]))
                {
                    if (!casti[0].Any(char.IsDigit))
                    {
                        poslednaOblast = casti[0];
                    }

                    riadky.Add((poslednaOblast, casti[1]));
                    continue;
                }

                riadok = string.Join(" ", casti);
            }

            var bezDvojbodky = riadok.TrimEnd(':').Trim();
            var pismena = bezDvojbodky.Where(char.IsLetter).ToList();
            if (zname.Contains(ImportOsnov.Kluc(bezDvojbodky)) || riadok.EndsWith(':') || pismena.Count >= 4 && pismena.All(char.IsUpper))
            {
                poslednaOblast = bezDvojbodky;
                riadky.Add((bezDvojbodky, ""));
            }
            else if (poslednaOblast.Length == 0)
            {
                riadky.Add((riadok, "")); // nadpis dokumentu pred prvou oblasťou
            }
            else
            {
                riadky.Add(("", riadok.TrimStart('•', '-', '–', ' ')));
            }
        }

        return UcebnyPlanService.Rozober(riadky);
    }

    private static bool JeHlavickaTabulky(string a, string b) =>
        ImportOsnov.Kluc(a) is "vzdelavacia oblast" or "oblast" or "vzdelavacie oblasti"
        && ImportOsnov.Kluc(b) is "predmet" or "predmety" or "vyucovaci predmet" or "vyucovacie predmety";

    private static HashSet<string> ZnameOblasti()
    {
        var zname = new HashSet<string>(StringComparer.Ordinal);
        foreach (var oblast in new ObjektService().Oblasti())
        {
            zname.Add(ImportOsnov.Kluc(oblast.Nazov));
        }

        foreach (var oblast in UcebnyPlanService.Vstavany().Oblasti)
        {
            zname.Add(ImportOsnov.Kluc(oblast.Nazov));
        }

        return zname;
    }

    // ---- export ----

    /// <summary>Aktuálne oblasti a predmety programu.</summary>
    public static UcebnyPlan Aktualna()
    {
        var sluzba = new ObjektService();
        return new UcebnyPlan
        {
            Nazov = "Vzdelávacie oblasti a predmety",
            Typ = Databaza.Citaj(UcebnyPlanService.KlucTypSkoly),
            Oblasti = sluzba.Oblasti().Select(o => new OblastPlanu
            {
                Nazov = o.Nazov,
                Predmety = sluzba.Deti(o.Id).Where(p => p.Typ == TypObjektu.Predmet).Select(p => p.Nazov).ToList(),
            }).ToList(),
        };
    }

    /// <summary>Excel v tvare, ktorý sa dá znova naimportovať (oblasť v stĺpci A, predmety pod ňou v stĺpci B).</summary>
    public static void UlozExcel(UcebnyPlan plan, string cesta)
    {
        using var zosit = new XLWorkbook();
        var harok = zosit.Worksheets.Add("Oblasti a predmety");
        var r = 1;
        harok.Cell(r++, 1).Value = plan.Nazov ?? "Vzdelávacie oblasti a predmety";
        if (!string.IsNullOrWhiteSpace(plan.Typ))
        {
            harok.Cell(r++, 1).Value = plan.Typ;
        }

        foreach (var riadok in TitulnaStrana.Nacitaj().UdajeOSkole())
        {
            harok.Cell(r++, 1).Value = riadok;
        }

        harok.Cell(1, 1).Style.Font.Bold = true;
        harok.Cell(1, 1).Style.Font.FontSize = 14;
        r++;
        foreach (var oblast in plan.Oblasti)
        {
            harok.Cell(r, 1).Value = oblast.Nazov;
            harok.Cell(r, 1).Style.Font.Bold = true;
            r++;
            foreach (var predmet in oblast.Predmety)
            {
                harok.Cell(r++, 2).Value = predmet;
            }
        }

        harok.Column(1).Width = 42;
        harok.Column(2).Width = 48;
        zosit.SaveAs(cesta);
    }

    /// <summary>Text oblastí a predmetov ako bloky (tabuľka) – pre Word.</summary>
    public static List<Blok> Bloky(UcebnyPlan plan)
    {
        var riadky = new List<IReadOnlyList<Bunka>>
        {
            new[]
            {
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Vzdelávacia oblasť", Tucne: true) }) }, Hlavicka: true),
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Predmet", Tucne: true) }) }, Hlavicka: true),
            },
        };
        foreach (var oblast in plan.Oblasti)
        {
            var predmety = oblast.Predmety.Count == 0 ? new List<string> { "" } : oblast.Predmety;
            for (var i = 0; i < predmety.Count; i++)
            {
                riadky.Add(new[]
                {
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek(i == 0 ? oblast.Nazov : "", Tucne: true) }) }),
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek(predmety[i]) }) }),
                });
            }
        }

        var bloky = new List<Blok> { new Nadpis(1, plan.Nazov ?? "Vzdelávacie oblasti a predmety") };
        foreach (var riadok in TitulnaStrana.Nacitaj().UdajeOSkole())
        {
            bloky.Add(new Odsek(new[] { new Usek(riadok) }));
        }

        if (!string.IsNullOrWhiteSpace(plan.Typ))
        {
            bloky.Add(new Odsek(new[] { new Usek(plan.Typ!) }));
        }

        bloky.Add(new Tabulka(riadky, 2, new[] { 4.5, 5.5 }));
        return bloky;
    }

    public static void UlozWord(UcebnyPlan plan, string cesta) => File.WriteAllBytes(cesta, DocxVystup.Osnova(Bloky(plan)));

    /// <summary>Dokument na PDF a obrázky: oblasti s farebnou značkou a ich predmety s číslami.</summary>
    public static Document Dokument(UcebnyPlan plan)
    {
        var doc = new Document();
        doc.Info.Title = plan.Nazov ?? "Vzdelávacie oblasti a predmety";
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = PismaDokumentu.Text;
        normal.Font.Size = 11;
        var sekcia = doc.AddSection();
        sekcia.PageSetup.PageFormat = PageFormat.A4;
        sekcia.PageSetup.TopMargin = Unit.FromCentimeter(2.2);
        sekcia.PageSetup.LeftMargin = Unit.FromCentimeter(2.4);
        sekcia.PageSetup.RightMargin = Unit.FromCentimeter(2.2);
        var nadpis = sekcia.AddParagraph(plan.Nazov ?? "Vzdelávacie oblasti a predmety");
        nadpis.Format.Font.Name = PismaDokumentu.Nadpis;
        nadpis.Format.Font.Size = 20;
        nadpis.Format.Font.Bold = true;
        nadpis.Format.SpaceAfter = Unit.FromPoint(4);
        var titul = TitulnaStrana.Nacitaj();
        doc.Info.Author = titul.AutorSuboru ?? "";
        foreach (var riadok in titul.UdajeOSkole())
        {
            var udaj = sekcia.AddParagraph(riadok);
            udaj.Format.Font.Color = Colors.Gray;
            udaj.Format.Font.Size = 10;
        }

        var podnadpis = sekcia.AddParagraph(plan.Typ ?? "");
        podnadpis.Format.Font.Color = Colors.Gray;
        podnadpis.Format.SpaceAfter = Unit.FromPoint(16);

        var sluzba = new ObjektService();
        var farby = sluzba.Oblasti().ToDictionary(o => o.Nazov, o => o.Farba ?? "#3B5BDB");
        var index = 0;
        foreach (var oblast in plan.Oblasti)
        {
            var farba = farby.TryGetValue(oblast.Nazov, out var f) ? f : UcebnyPlanService.FarbaOblasti(index);
            index++;
            var p = sekcia.AddParagraph();
            p.Format.SpaceBefore = Unit.FromPoint(10);
            p.Format.SpaceAfter = Unit.FromPoint(3);
            p.Format.KeepWithNext = true;
            var znacka = p.AddFormattedText("■  ");
            znacka.Font.Color = PdfVystup.Farba(farba);
            var nazov = p.AddFormattedText(oblast.Nazov);
            nazov.Font.Bold = true;
            nazov.Font.Name = PismaDokumentu.Nadpis;
            nazov.Font.Size = 12.5;
            for (var i = 0; i < oblast.Predmety.Count; i++)
            {
                var predmet = sekcia.AddParagraph($"{i + 1:00}   {oblast.Predmety[i]}");
                predmet.Format.LeftIndent = Unit.FromCentimeter(0.8);
                predmet.Format.SpaceAfter = Unit.FromPoint(1.5);
            }
        }

        return doc;
    }
}
