using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WinAlign = System.Windows.TextAlignment;

namespace SkVP.Services;

/// <summary>
/// Export celého dokumentu učebných osnov do Wordu (.docx) na ďalšie úpravy. Používa štýly Wordu (Nadpis 1, Nadpis 2 …),
/// takže obsah, navigácia a číslovanie fungujú ako v bežnom dokumente Wordu. Obsah dokumentu Word doplní pri otvorení.
/// </summary>
public static class DocxVystup
{
    private const string Atrament = "1F2933";
    private const string Siva = "6B7280";
    private const string Linka = "CBD2D9";
    private const string Podklad = "F3F4F6";

    private sealed class Stav
    {
        public NumberingDefinitionsPart Cislovanie = null!;
        public int DalsieCislo = 10;

        /// <summary>O koľko úrovní sa posunú nadpisy textu osnovy (v celom dokumente sú pod oblasťou a predmetom).</summary>
        public int PosunNadpisov = 2;
    }

    public static void Uloz(CelyDokument dokument, string cesta)
    {
        var priecinok = Path.GetDirectoryName(cesta);
        if (!string.IsNullOrEmpty(priecinok))
        {
            Directory.CreateDirectory(priecinok);
        }

        using var docx = WordprocessingDocument.Create(cesta, WordprocessingDocumentType.Document);
        var hlavna = docx.AddMainDocumentPart();
        hlavna.Document = new Document(new Body());
        var telo = hlavna.Document.Body!;

        var styly = hlavna.AddNewPart<StyleDefinitionsPart>();
        styly.Styles = Styly(osnova: false);

        var stav = new Stav { Cislovanie = hlavna.AddNewPart<NumberingDefinitionsPart>() };
        stav.Cislovanie.Numbering = ZakladCislovania();

        var nastavenia = hlavna.AddNewPart<DocumentSettingsPart>();
        nastavenia.Settings = new Settings(
            new DefaultTabStop { Val = 709 },
            new UpdateFieldsOnOpen { Val = true },
            new Compatibility(new CompatibilitySetting
            {
                Name = CompatSettingNameValues.CompatibilityMode,
                Uri = "http://schemas.microsoft.com/office/word",
                Val = "15",
            }));

        docx.PackageProperties.Title = $"{dokument.Udaje.NazovProgramu} – {CelyDokument.Podtitul}";
        docx.PackageProperties.Creator = dokument.Udaje.Skola;
        docx.PackageProperties.Subject = CelyDokument.Podtitul;

        if (dokument.Volby.TitulnaStrana)
        {
            TitulnaStrana(telo, dokument, hlavna);
        }

        var prvy = !dokument.Volby.TitulnaStrana;
        if (dokument.Volby.Obsah)
        {
            var obsah = Odsek("Obsah", "SkvpNadpisObsahu");
            if (!prvy)
            {
                obsah.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
            }

            telo.Append(obsah);
            telo.Append(PoleObsahu());
            prvy = false;
        }

        var volby = dokument.Volby;
        foreach (var oblast in dokument.Oblasti)
        {
            Oblast(telo, oblast, !prvy, volby.OddelovacOblasti, stav);
            prvy = false;
            for (var i = 0; i < oblast.Predmety.Count; i++)
            {
                var predmet = oblast.Predmety[i];
                if (volby.OddelovacPredmetu)
                {
                    OddelovacPredmetu(telo, oblast, predmet);
                }

                var novaStrana = volby.OddelovacPredmetu || volby.OddelovacOblasti || i > 0 || oblast.Charakteristika.Count > 0;
                Predmet(telo, oblast, predmet, novaStrana, stav);
            }
        }

        // hlavička a päta (nie na titulnej strane)
        var hlavicka = hlavna.AddNewPart<HeaderPart>();
        var textHlavicky = $"{dokument.Udaje.NazovProgramu} – {CelyDokument.Podtitul}";
        var odsekHlavicky = new Paragraph(
            new ParagraphProperties(new ParagraphStyleId { Val = "Header" },
                new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Linka, Space = 3 }),
                new Tabs(new TabStop { Val = TabStopValues.Right, Position = 9354 })),
            Beh(textHlavicky));
        if (dokument.Udaje.Skola.Length > 0)
        {
            odsekHlavicky.Append(new Run(new TabChar()), Beh(dokument.Udaje.Skola));
        }

        hlavicka.Header = new Header(odsekHlavicky);

        var pata = hlavna.AddNewPart<FooterPart>();
        var odsekPaty = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Footer" }, new Justification { Val = JustificationValues.Center }));
        odsekPaty.Append(Beh("Strana "));
        odsekPaty.Append(Pole("PAGE", "1"));
        odsekPaty.Append(Beh(" z "));
        odsekPaty.Append(Pole("NUMPAGES", "1"));
        pata.Footer = new Footer(odsekPaty);

        var sekcia = new SectionProperties(
            new HeaderReference { Type = HeaderFooterValues.Default, Id = hlavna.GetIdOfPart(hlavicka) },
            new FooterReference { Type = HeaderFooterValues.Default, Id = hlavna.GetIdOfPart(pata) },
            new PageSize { Width = 11906, Height = 16838 },
            new PageMargin { Top = 1474, Bottom = 1361, Left = 1361, Right = 1247, Header = 680, Footer = 624, Gutter = 0 });
        if (dokument.Volby.TitulnaStrana)
        {
            sekcia.Append(new TitlePage());
        }

        telo.Append(sekcia);
        hlavna.Document.Save();
    }

    // ---- časti dokumentu ----

    private static void TitulnaStrana(Body telo, CelyDokument dokument, MainDocumentPart hlavna)
    {
        var u = dokument.Udaje;
        if (dokument.Oblasti.Count > 0)
        {
            // pás farieb oblastí
            var pas = new Table(new TableProperties(
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
                new TableBorders(new TopBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None }),
                new TableLayout { Type = TableLayoutValues.Fixed }));
            var siet = new TableGrid();
            var sirka = 9298 / dokument.Oblasti.Count;
            foreach (var _ in dokument.Oblasti)
            {
                siet.Append(new GridColumn { Width = sirka.ToString(CultureInfo.InvariantCulture) });
            }

            pas.Append(siet);
            var riadok = new TableRow(new TableRowProperties(new TableRowHeight { Val = 140, HeightType = HeightRuleValues.Exact }));
            foreach (var oblast in dokument.Oblasti)
            {
                riadok.Append(new TableCell(
                    new TableCellProperties(new Shading { Val = ShadingPatternValues.Clear, Fill = Hex(oblast.Farba), Color = "auto" }),
                    new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "0", Line = "120", LineRule = LineSpacingRuleValues.Exact }))));
            }

            pas.Append(riadok);
            telo.Append(pas);
        }

        if (u.Logo is not null && File.Exists(u.Logo) && Logo(hlavna, u.Logo) is { } logo)
        {
            telo.Append(logo);
        }

        telo.Append(Odsek(u.Skola.Length > 0 ? u.Skola : " ", "SkvpSkola"));
        if (u.Adresa.Length > 0)
        {
            telo.Append(Odsek(u.Adresa, "SkvpAdresa"));
        }

        telo.Append(Odsek(u.NazovProgramu, "Title"));
        telo.Append(Odsek(CelyDokument.Podtitul, "Subtitle"));
        foreach (var r in new[] { u.Typ, u.Obdobie.Length > 0 ? "Platnosť: " + u.Obdobie : "" }.Where(r => r.Length > 0))
        {
            telo.Append(Odsek(r, "SkvpTitulUdaj"));
        }

        var prvy = true;
        foreach (var oblast in dokument.Oblasti)
        {
            var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "SkvpTitulOblast" }));
            if (prvy)
            {
                p.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { Before = "1700", After = "40" };
                prvy = false;
            }

            p.Append(Beh("■   ", farba: Hex(oblast.Farba)));
            p.Append(Beh(oblast.Nazov + "   "));
            p.Append(Beh(PdfVystup.PocetPredmetov(oblast.Predmety.Count), farba: Siva));
            telo.Append(p);
        }

        var datum = Odsek($"Vytvorené {dokument.Vytvorene.ToString("d. M. yyyy", CultureInfo.GetCultureInfo("sk-SK"))} v programe SkVP", "SkvpAdresa");
        datum.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { Before = "1100" };
        telo.Append(datum);
        telo.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
    }

    private static void Oblast(Body telo, KapitolaOblasti oblast, bool novaStrana, bool oddelovac, Stav stav)
    {
        var farba = Hex(oblast.Farba);
        var nadpis = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Heading1" },
            new ParagraphBorders(new LeftBorder { Val = BorderValues.Single, Size = 48, Color = farba, Space = 12 }),
            new Indentation { Left = "360" }));
        if (novaStrana)
        {
            nadpis.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
        }

        if (!oddelovac)
        {
            nadpis.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { Before = "0" };
        }

        nadpis.Append(Beh(oblast.Nazov, farba: farba));
        telo.Append(nadpis);

        var popis = Odsek($"Vzdelávacia oblasť  •  {PdfVystup.PocetPredmetov(oblast.Predmety.Count)}", "SkvpPodnadpisOblasti");
        popis.ParagraphProperties!.Indentation = new Indentation { Left = "360" };
        telo.Append(popis);

        if (oddelovac)
        {
            foreach (var predmet in oblast.Predmety)
            {
                var p = new Paragraph(new ParagraphProperties(
                    new ParagraphStyleId { Val = "SkvpZoznamPredmetov" },
                    new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Linka, Space = 4 })));
                p.Append(Beh(predmet.Cislo, tucne: true, farba: farba));
                p.Append(new Run(new TabChar()));
                p.Append(Beh(predmet.Nazov));
                telo.Append(p);
            }
        }

        if (oblast.Charakteristika.Count > 0)
        {
            var c = Odsek("Charakteristika vzdelávacej oblasti", "Heading3", farba);
            if (oddelovac && oblast.Predmety.Count > 0)
            {
                c.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
            }

            telo.Append(c);
            foreach (var prvok in Bloky(oblast.Charakteristika, farba, stav))
            {
                telo.Append(prvok);
            }
        }
    }

    private static void OddelovacPredmetu(Body telo, KapitolaOblasti oblast, KapitolaPredmetu predmet)
    {
        var farba = Hex(oblast.Farba);
        var cislo = Odsek(predmet.Cislo, "SkvpOddelovacCislo", farba);
        cislo.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
        telo.Append(cislo);
        var nazov = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "SkvpOddelovacNazov" },
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 24, Color = farba, Space = 10 })));
        nazov.Append(Beh(predmet.Nazov));
        telo.Append(nazov);
        telo.Append(Odsek(oblast.Nazov, "SkvpTitulUdaj", Siva));
    }

    private static void Predmet(Body telo, KapitolaOblasti oblast, KapitolaPredmetu predmet, bool novaStrana, Stav stav)
    {
        var farba = Hex(oblast.Farba);
        var oblastText = Odsek($"{oblast.Nazov}  •  {predmet.Cislo}", "SkvpOblastMala", farba);
        if (novaStrana)
        {
            oblastText.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
        }

        telo.Append(oblastText);

        var nadpis = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Heading2" },
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 12, Color = farba, Space = 6 })));
        nadpis.Append(Beh(predmet.Nazov));
        telo.Append(nadpis);

        if (predmet.Obsah.Count == 0)
        {
            telo.Append(Odsek("Učebná osnova predmetu zatiaľ nie je spracovaná.", "SkvpPoznamka"));
            return;
        }

        foreach (var prvok in Bloky(predmet.Obsah, farba, stav))
        {
            telo.Append(prvok);
        }
    }

    /// <summary>Logo školy na titulnú stranu (výška 2,6 cm, pomer strán zachovaný).</summary>
    private static Paragraph? Logo(MainDocumentPart hlavna, string cesta)
    {
        try
        {
            int sirkaPx, vyskaPx;
            using (var prud = File.OpenRead(cesta))
            {
                var snimka = System.Windows.Media.Imaging.BitmapFrame.Create(prud,
                    System.Windows.Media.Imaging.BitmapCreateOptions.DelayCreation, System.Windows.Media.Imaging.BitmapCacheOption.None);
                sirkaPx = snimka.PixelWidth;
                vyskaPx = snimka.PixelHeight;
            }

            if (sirkaPx <= 0 || vyskaPx <= 0)
            {
                return null;
            }

            var typ = Path.GetExtension(cesta).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ImagePartType.Jpeg,
                ".bmp" => ImagePartType.Bmp,
                ".gif" => ImagePartType.Gif,
                _ => ImagePartType.Png,
            };
            var cast = hlavna.AddImagePart(typ);
            using (var prud = File.OpenRead(cesta))
            {
                cast.FeedData(prud);
            }

            const long vyska = 936000; // 2,6 cm v EMU
            var sirka = vyska * sirkaPx / vyskaPx;
            var id = hlavna.GetIdOfPart(cast);
            var obrazok = new DocumentFormat.OpenXml.Drawing.Pictures.Picture(
                new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureProperties(
                    new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualDrawingProperties { Id = 0U, Name = "logo" },
                    new DocumentFormat.OpenXml.Drawing.Pictures.NonVisualPictureDrawingProperties()),
                new DocumentFormat.OpenXml.Drawing.Pictures.BlipFill(
                    new DocumentFormat.OpenXml.Drawing.Blip { Embed = id },
                    new DocumentFormat.OpenXml.Drawing.Stretch(new DocumentFormat.OpenXml.Drawing.FillRectangle())),
                new DocumentFormat.OpenXml.Drawing.Pictures.ShapeProperties(
                    new DocumentFormat.OpenXml.Drawing.Transform2D(
                        new DocumentFormat.OpenXml.Drawing.Offset { X = 0L, Y = 0L },
                        new DocumentFormat.OpenXml.Drawing.Extents { Cx = sirka, Cy = vyska }),
                    new DocumentFormat.OpenXml.Drawing.PresetGeometry(new DocumentFormat.OpenXml.Drawing.AdjustValueList())
                    { Preset = DocumentFormat.OpenXml.Drawing.ShapeTypeValues.Rectangle }));
            var kresba = new Drawing(new DocumentFormat.OpenXml.Drawing.Wordprocessing.Inline(
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.Extent { Cx = sirka, Cy = vyska },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = 1U, Name = "Logo školy" },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(
                    new DocumentFormat.OpenXml.Drawing.GraphicFrameLocks { NoChangeAspect = true }),
                new DocumentFormat.OpenXml.Drawing.Graphic(new DocumentFormat.OpenXml.Drawing.GraphicData(obrazok)
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
            return new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "400", After = "0" }), new Run(kresba));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<OpenXmlElement> Bloky(IReadOnlyList<Blok> bloky, string farba, Stav stav, int uroven = 0)
    {
        var vysledok = new List<OpenXmlElement>();
        foreach (var blok in bloky)
        {
            switch (blok)
            {
                case Nadpis n:
                    var stylNadpisu = "Heading" + Math.Min(9, n.Uroven + stav.PosunNadpisov).ToString(CultureInfo.InvariantCulture);
                    vysledok.Add(Odsek(n.Text, stylNadpisu, n.Uroven == 1 && stav.PosunNadpisov > 0 ? farba : null));
                    break;

                case Odsek o:
                    var p = new Paragraph(new ParagraphProperties());
                    if (o.Odsadenie > 0)
                    {
                        p.ParagraphProperties!.Indentation = new Indentation { Left = (340 * o.Odsadenie).ToString(CultureInfo.InvariantCulture) };
                    }

                    if (o.Zarovnanie != WinAlign.Left)
                    {
                        p.ParagraphProperties!.Justification = new Justification
                        {
                            Val = o.Zarovnanie switch
                            {
                                WinAlign.Center => JustificationValues.Center,
                                WinAlign.Right => JustificationValues.Right,
                                _ => JustificationValues.Both,
                            },
                        };
                    }

                    Text(p, o.Useky);
                    vysledok.Add(p);
                    break;

                case Zoznam z:
                    var cislo = NovyZoznam(stav, z.Cislovany);
                    foreach (var polozka in z.Polozky)
                    {
                        var prvy = true;
                        foreach (var vnutorny in polozka)
                        {
                            if (vnutorny is Zoznam vnoreny)
                            {
                                vysledok.AddRange(Bloky(new Blok[] { vnoreny }, farba, stav, uroven + 1));
                                continue;
                            }

                            var li = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "ListParagraph" }));
                            if (prvy)
                            {
                                li.ParagraphProperties!.NumberingProperties = new NumberingProperties(
                                    new NumberingLevelReference { Val = Math.Min(uroven, 2) },
                                    new NumberingId { Val = cislo });
                                prvy = false;
                            }
                            else
                            {
                                li.ParagraphProperties!.Indentation = new Indentation { Left = (360 * (uroven + 1) + 360).ToString(CultureInfo.InvariantCulture) };
                            }

                            if (vnutorny is Odsek textPolozky)
                            {
                                Text(li, textPolozky.Useky);
                            }

                            vysledok.Add(li);
                        }
                    }

                    break;

                case Tabulka t:
                    vysledok.Add(Tabulka(t, farba, stav));
                    if (stav.PosunNadpisov > 0)
                    {
                        // v celom dokumente malá medzera za tabuľkou
                        vysledok.Add(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { After = "60" })));
                    }

                    break;
            }
        }

        return vysledok;
    }

    private const int SirkaTextuTwip = 9298;

    private static Table Tabulka(Tabulka t, string farba, Stav stav)
    {
        var tabulka = new Table(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = Linka },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = Linka },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Linka },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = Linka },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = Linka },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = Linka }),
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "50", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 85, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 85, Type = TableWidthValues.Dxa })));

        var sirky = t.PomerneSirky.Select(x => (int)Math.Round(x * SirkaTextuTwip)).ToList();
        var siet = new TableGrid();
        foreach (var sirka in sirky)
        {
            siet.Append(new GridColumn { Width = sirka.ToString(CultureInfo.InvariantCulture) });
        }

        tabulka.Append(siet);

        // obsadenosť mriežky – kvôli bunkám zlúčeným zvislo (vMerge) treba vedieť, kde pokračujú
        var pokracovanie = new Dictionary<(int Riadok, int Stlpec), int>();
        for (var r = 0; r < t.Riadky.Count; r++)
        {
            var riadok = new TableRow();
            if (r == 0 && t.Riadky[0].Any(b => b.Hlavicka) && t.Riadky.Count > 2)
            {
                riadok.Append(new TableRowProperties(new TableHeader()));
            }

            var stlpec = 0;
            var index = 0;
            while (stlpec < t.PocetStlpcov)
            {
                if (pokracovanie.TryGetValue((r, stlpec), out var rozpatie))
                {
                    riadok.Append(Bunka(new List<OpenXmlElement>(), Sucet(sirky, stlpec, rozpatie), rozpatie, MergedCellValues.Continue, false));
                    stlpec += rozpatie;
                    continue;
                }

                if (index >= t.Riadky[r].Count)
                {
                    riadok.Append(Bunka(new List<OpenXmlElement>(), sirky[stlpec], 1, null, false));
                    stlpec++;
                    continue;
                }

                var b = t.Riadky[r][index++];
                var stlpce = Math.Clamp(b.Stlpce, 1, t.PocetStlpcov - stlpec);
                MergedCellValues? zvislo = null;
                if (b.Riadky > 1)
                {
                    zvislo = MergedCellValues.Restart;
                    for (var d = 1; d < b.Riadky && r + d < t.Riadky.Count; d++)
                    {
                        pokracovanie[(r + d, stlpec)] = stlpce;
                    }
                }

                var obsah = Bloky(b.Bloky.Where(x => x is not SkVP.Services.Tabulka).ToList(), farba, stav);
                riadok.Append(Bunka(obsah, Sucet(sirky, stlpec, stlpce), stlpce, zvislo, b.Hlavicka));
                stlpec += stlpce;
            }

            tabulka.Append(riadok);
        }

        return tabulka;
    }

    private static int Sucet(List<int> sirky, int od, int pocet) => sirky.Skip(od).Take(pocet).Sum();

    private static TableCell Bunka(List<OpenXmlElement> obsah, int sirka, int stlpce, MergedCellValues? zvislo, bool hlavicka)
    {
        var vlastnosti = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = sirka.ToString(CultureInfo.InvariantCulture) });
        if (stlpce > 1)
        {
            vlastnosti.Append(new GridSpan { Val = stlpce });
        }

        if (zvislo is not null)
        {
            vlastnosti.Append(new VerticalMerge { Val = zvislo.Value });
        }

        if (hlavicka)
        {
            vlastnosti.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = Podklad, Color = "auto" });
        }

        var bunka = new TableCell(vlastnosti);
        foreach (var prvok in obsah)
        {
            if (prvok is Paragraph odsek)
            {
                odsek.ParagraphProperties ??= new ParagraphProperties();
                if (odsek.ParagraphProperties.ParagraphStyleId is null)
                {
                    odsek.ParagraphProperties.PrependChild(new ParagraphStyleId { Val = "SkvpTabulka" });
                }
            }
        }

        if (obsah.Count == 0)
        {
            obsah.Add(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "SkvpTabulka" })));
        }

        bunka.Append(obsah);
        return bunka;
    }

    /// <summary>Osnova jedného predmetu ako samostatný dokument Word (ukladá sa do databázy a otvára vo Worde).</summary>
    public static byte[] Osnova(IReadOnlyList<Blok> bloky)
    {
        using var pamat = new MemoryStream();
        using (var docx = WordprocessingDocument.Create(pamat, WordprocessingDocumentType.Document))
        {
            var hlavna = docx.AddMainDocumentPart();
            hlavna.Document = new Document(new Body());
            var telo = hlavna.Document.Body!;
            hlavna.AddNewPart<StyleDefinitionsPart>().Styles = Styly(osnova: true);
            var stav = new Stav { Cislovanie = hlavna.AddNewPart<NumberingDefinitionsPart>(), PosunNadpisov = 0 };
            stav.Cislovanie.Numbering = ZakladCislovania();
            hlavna.AddNewPart<DocumentSettingsPart>().Settings = new Settings(
                new DefaultTabStop { Val = 709 },
                new Compatibility(new CompatibilitySetting
                {
                    Name = CompatSettingNameValues.CompatibilityMode,
                    Uri = "http://schemas.microsoft.com/office/word",
                    Val = "15",
                }));
            foreach (var prvok in Bloky(bloky, "3B5BDB", stav))
            {
                telo.Append(prvok);
            }

            if (!telo.HasChildren || telo.LastChild is Table)
            {
                // Word chce za tabuľkou na konci dokumentu ešte odsek
                telo.Append(new Paragraph());
            }

            telo.Append(new SectionProperties(
                new PageSize { Width = 11906, Height = 16838 },
                new PageMargin { Top = 1418, Bottom = 1418, Left = 1418, Right = 1418, Header = 709, Footer = 709, Gutter = 0 }));
            hlavna.Document.Save();
        }

        return pamat.ToArray();
    }

    private static void Text(Paragraph odsek, IReadOnlyList<Usek> useky)
    {
        foreach (var u in useky)
        {
            if (u.NovyRiadok)
            {
                odsek.Append(new Run(new Break()));
                continue;
            }

            odsek.Append(Beh(u.Text, u.Tucne, u.Kurziva, u.Podciarknute));
        }
    }

    // ---- pomocné prvky ----

    private static Run Beh(string text, bool tucne = false, bool kurziva = false, bool podciarknute = false, string? farba = null)
    {
        var beh = new Run();
        if (tucne || kurziva || podciarknute || farba is not null)
        {
            var vlastnosti = new RunProperties();
            if (tucne)
            {
                vlastnosti.Append(new Bold());
            }

            if (kurziva)
            {
                vlastnosti.Append(new Italic());
            }

            if (farba is not null)
            {
                vlastnosti.Append(new Color { Val = farba });
            }

            if (podciarknute)
            {
                vlastnosti.Append(new Underline { Val = UnderlineValues.Single });
            }

            beh.Append(vlastnosti);
        }

        beh.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return beh;
    }

    private static Paragraph Odsek(string text, string? styl, string? farba = null)
    {
        var vlastnosti = new ParagraphProperties();
        if (styl is not null)
        {
            vlastnosti.Append(new ParagraphStyleId { Val = styl });
        }

        return new Paragraph(vlastnosti, Beh(text, farba: farba));
    }

    private static IEnumerable<OpenXmlElement> Pole(string kod, string zobrazenie) => new OpenXmlElement[]
    {
        new Run(new FieldChar { FieldCharType = FieldCharValues.Begin }),
        new Run(new FieldCode($" {kod} ") { Space = SpaceProcessingModeValues.Preserve }),
        new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }),
        new Run(new Text(zobrazenie)),
        new Run(new FieldChar { FieldCharType = FieldCharValues.End }),
    };

    private static Paragraph PoleObsahu()
    {
        var p = new Paragraph();
        p.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.Begin, Dirty = true }));
        p.Append(new Run(new FieldCode(" TOC \\o \"1-2\" \\h \\z \\u ") { Space = SpaceProcessingModeValues.Preserve }));
        p.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.Separate }));
        p.Append(Beh("Obsah sa doplní pri otvorení dokumentu vo Worde (potvrďte aktualizáciu polí).", kurziva: true, farba: Siva));
        p.Append(new Run(new FieldChar { FieldCharType = FieldCharValues.End }));
        return p;
    }

    private static int NovyZoznam(Stav stav, bool cislovany)
    {
        var id = stav.DalsieCislo++;
        var cislovanie = stav.Cislovanie.Numbering!;
        var instancia = new NumberingInstance(new AbstractNumId { Val = cislovany ? 2 : 1 }) { NumberID = id };
        if (cislovany)
        {
            instancia.Append(new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 });
        }

        cislovanie.Append(instancia);
        return id;
    }

    private static Numbering ZakladCislovania()
    {
        AbstractNum Abstraktny(int id, bool cislovany)
        {
            var abstraktny = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = id };
            string[] odrazky = { "•", "◦", "▪" };
            string[] formaty = { "%1.", "%2)", "%3." };
            for (var i = 0; i < 3; i++)
            {
                var uroven = new Level { LevelIndex = i };
                uroven.Append(new StartNumberingValue { Val = 1 });
                uroven.Append(new NumberingFormat
                {
                    Val = cislovany ? (i == 1 ? NumberFormatValues.LowerLetter : NumberFormatValues.Decimal) : NumberFormatValues.Bullet,
                });
                uroven.Append(new LevelText { Val = cislovany ? formaty[i] : odrazky[i] });
                uroven.Append(new LevelJustification { Val = LevelJustificationValues.Left });
                uroven.Append(new PreviousParagraphProperties(new Indentation
                {
                    Left = (360 * (i + 1) + 360).ToString(CultureInfo.InvariantCulture),
                    Hanging = "300",
                }));
                abstraktny.Append(uroven);
            }

            return abstraktny;
        }

        return new Numbering(Abstraktny(1, false), Abstraktny(2, true));
    }

    private static Styles Styly(bool osnova)
    {
        var text = PismaDokumentu.Text;
        var nadpis = PismaDokumentu.Nadpis;

        Style Styl(string id, string meno, string? zaklad, StyleRunProperties beh, StyleParagraphProperties? odsek = null, int? uroven = null)
        {
            var styl = new Style { Type = StyleValues.Paragraph, StyleId = id };
            styl.Append(new StyleName { Val = meno });
            if (zaklad is not null)
            {
                styl.Append(new BasedOn { Val = zaklad });
                styl.Append(new NextParagraphStyle { Val = "Normal" });
            }

            styl.Append(new PrimaryStyle());
            odsek ??= new StyleParagraphProperties();
            if (uroven is not null)
            {
                odsek.KeepNext = new KeepNext();
                odsek.OutlineLevel = new OutlineLevel { Val = uroven };
            }

            styl.Append(odsek);
            styl.Append(beh);
            return styl;
        }

        StyleRunProperties Pismo(string meno, int polBody, bool tucne = false, bool kurziva = false, string? farba = null)
        {
            var b = new StyleRunProperties(new RunFonts { Ascii = meno, HighAnsi = meno, ComplexScript = meno, EastAsia = meno });
            if (tucne)
            {
                b.Append(new Bold());
            }

            if (kurziva)
            {
                b.Append(new Italic());
            }

            b.Append(new Color { Val = farba ?? Atrament });
            b.Append(new FontSize { Val = polBody.ToString(CultureInfo.InvariantCulture) });
            return b;
        }

        StyleParagraphProperties Medzery(int pred, int po) =>
            new(new SpacingBetweenLines { Before = pred.ToString(CultureInfo.InvariantCulture), After = po.ToString(CultureInfo.InvariantCulture) });

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        normal.Append(new StyleName { Val = "Normal" }, new PrimaryStyle(),
            new StyleParagraphProperties(new SpacingBetweenLines { After = "100", Line = "283", LineRule = LineSpacingRuleValues.Auto }),
            Pismo(text, 22));

        var styly = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = text, HighAnsi = text, ComplexScript = text, EastAsia = text },
                    new FontSize { Val = "22" }, new Languages { Val = "sk-SK" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new SpacingBetweenLines { After = "100" }))),
            normal,
            Styl("Title", "Title", "Normal", Pismo(nadpis, 68, true), Medzery(3400, 200)),
            Styl("Subtitle", "Subtitle", "Normal", Pismo(text, 48, kurziva: true, farba: "3B5BDB"), Medzery(0, 520)),
            osnova
                ? Styl("Heading1", "heading 1", "Normal", Pismo(nadpis, 28, true), Medzery(360, 120), 0)
                : Styl("Heading1", "heading 1", "Normal", Pismo(nadpis, 52, true), Medzery(2800, 200), 0),
            osnova
                ? Styl("Heading2", "heading 2", "Normal", Pismo(nadpis, 24, true), Medzery(240, 100), 1)
                : Styl("Heading2", "heading 2", "Normal", Pismo(nadpis, 38, true), Medzery(0, 280), 1),
            osnova
                ? Styl("Heading3", "heading 3", "Normal", Pismo(nadpis, 22, true, kurziva: true), Medzery(200, 80), 2)
                : Styl("Heading3", "heading 3", "Normal", Pismo(nadpis, 26, true), Medzery(280, 100), 2),
            Styl("Heading4", "heading 4", "Normal", Pismo(nadpis, 23, true), Medzery(200, 80), 3),
            Styl("Heading5", "heading 5", "Normal", Pismo(nadpis, 22, kurziva: true), Medzery(160, 60), 4),
            Styl("ListParagraph", "List Paragraph", "Normal", Pismo(text, 22), Medzery(0, 50)),
            Styl("Header", "header", "Normal", Pismo(nadpis, 17, farba: Siva), Medzery(0, 0)),
            Styl("Footer", "footer", "Normal", Pismo(nadpis, 17, farba: Siva), Medzery(0, 0)),
            Styl("SkvpSkola", "SkVP – škola", "Normal", Pismo(nadpis, 26, true), Medzery(360, 20)),
            Styl("SkvpAdresa", "SkVP – adresa", "Normal", Pismo(nadpis, 20, farba: Siva), Medzery(0, 0)),
            Styl("SkvpTitulUdaj", "SkVP – údaj titulnej strany", "Normal", Pismo(nadpis, 24), Medzery(0, 60)),
            Styl("SkvpTitulOblast", "SkVP – oblasť na titulnej strane", "Normal", Pismo(nadpis, 20), Medzery(0, 40)),
            Styl("SkvpNadpisObsahu", "SkVP – nadpis obsahu", "Normal", Pismo(nadpis, 48, true), Medzery(0, 280)),
            Styl("SkvpPodnadpisOblasti", "SkVP – podnadpis oblasti", "Normal", Pismo(nadpis, 20, farba: Siva), Medzery(0, 440)),
            Styl("SkvpOblastMala", "SkVP – oblasť nad predmetom", "Normal", Pismo(nadpis, 19, true), Medzery(0, 40)),
            Styl("SkvpPoznamka", "SkVP – poznámka", "Normal", Pismo(text, 22, kurziva: true, farba: Siva)),
            Styl("SkvpTabulka", "SkVP – text v tabuľke", "Normal", Pismo(text, 20), Medzery(0, 40)),
            Styl("SkvpZoznamPredmetov", "SkVP – predmet v zozname oblasti", "Normal", Pismo(text, 25),
                new StyleParagraphProperties(new Tabs(new TabStop { Val = TabStopValues.Left, Position = 851 }),
                    new SpacingBetweenLines { Before = "80", After = "80" })),
            Styl("SkvpOddelovacCislo", "SkVP – číslo na oddeľujúcej strane", "Normal", Pismo(nadpis, 144, true), Medzery(3600, 60)),
            Styl("SkvpOddelovacNazov", "SkVP – predmet na oddeľujúcej strane", "Normal", Pismo(nadpis, 60, true), Medzery(0, 200)),
            Styl("TOC1", "toc 1", "Normal", Pismo(nadpis, 21, true), Medzery(180, 40)),
            Styl("TOC2", "toc 2", "Normal", Pismo(text, 21),
                new StyleParagraphProperties(new SpacingBetweenLines { After = "30" }, new Indentation { Left = "400" })));
        return styly;
    }

    private static string Hex(string farba) => (farba ?? "3B5BDB").TrimStart('#').ToUpperInvariant();
}
