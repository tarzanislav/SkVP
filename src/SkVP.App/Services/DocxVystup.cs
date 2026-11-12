using SkVP.Models;
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
    private const string StylTabulky = "SkvpTabulka";
    private const string StylTabulkySvetly = "SkvpTabulkaSvetla";

    private sealed class Stav
    {
        public NumberingDefinitionsPart Cislovanie = null!;
        public int DalsieCislo = 10;

        /// <summary>O koľko úrovní sa posunú nadpisy textu osnovy (v celom dokumente sú pod oblasťou a predmetom).</summary>
        public int PosunNadpisov = 2;

        /// <summary>Šírka textu aktuálnej strany v twipoch (podľa rozloženia strany a okrajov).</summary>
        public int SirkaTextu = SirkaTextuZostavy(RozlozenieStrany.A4NaVysku);
    }

    // okraje strán zostavy a samostatnej osnovy (twipy)
    private const int OkrajZostavyVlavo = 1361;
    private const int OkrajZostavyVpravo = 1247;
    private const int OkrajOsnovy = 1418;

    private static int Twipy(double mm) => (int)Math.Round(mm * 56.6929);

    private static int SirkaTextuZostavy(RozlozenieStrany r) => Twipy(r.SirkaMm) - OkrajZostavyVlavo - OkrajZostavyVpravo;

    /// <summary>Veľkosť strany pre vlastnosti časti dokumentu Word (pri strane na šírku aj s orientáciou).</summary>
    private static PageSize VelkostStrany(RozlozenieStrany r)
    {
        var velkost = new PageSize { Width = (UInt32Value)(uint)Twipy(r.SirkaMm), Height = (UInt32Value)(uint)Twipy(r.VyskaMm) };
        if (r.NaSirku)
        {
            velkost.Orient = PageOrientationValues.Landscape;
        }

        return velkost;
    }

    /// <summary>
    /// Časti celého dokumentu s rôznym rozložením strany (napr. osnova na šírku medzi osnovami na výšku). Každá časť
    /// má hlavičku a pätu pre svoju šírku; prepnutie ukončí doterajšiu časť a ďalší text začne na novej strane.
    /// </summary>
    private sealed class CastiZostavy(MainDocumentPart hlavna, CelyDokument dokument, Body telo, Stav stav)
    {
        private readonly Dictionary<string, (string? Hlavicka, string Pata)> _odkazy = new();
        private bool _prva = true;

        public RozlozenieStrany Rozlozenie { get; private set; } = RozlozenieStrany.A4NaVysku;

        /// <summary>Prepne rozloženie strany. True = začala sa nová časť (a tým aj nová strana).</summary>
        public bool Prepni(RozlozenieStrany nove)
        {
            if (nove.Zhodne(Rozlozenie))
            {
                return false;
            }

            telo.Append(new Paragraph(new ParagraphProperties(Vlastnosti())));
            Rozlozenie = nove;
            stav.SirkaTextu = SirkaTextuZostavy(nove);
            return true;
        }

        /// <summary>Vlastnosti aktuálnej časti: hlavička, päta, veľkosť strany a okraje.</summary>
        public SectionProperties Vlastnosti()
        {
            var kluc = $"{Twipy(Rozlozenie.SirkaMm)}x{Twipy(Rozlozenie.VyskaMm)}";
            if (!_odkazy.TryGetValue(kluc, out var odkazy))
            {
                var sirka = SirkaTextuZostavy(Rozlozenie);
                string? hlavicka = null;
                if (dokument.Titul.MaHlavicku(dokument.Udaje.Logo))
                {
                    var cast = hlavna.AddNewPart<HeaderPart>();
                    cast.Header = Hlavicka(cast, dokument.Titul, dokument.Udaje.Logo, sirka, (uint)(2 + _odkazy.Count));
                    hlavicka = hlavna.GetIdOfPart(cast);
                }

                var pata = hlavna.AddNewPart<FooterPart>();
                pata.Footer = Pata(dokument.Titul, sirka);
                odkazy = (hlavicka, hlavna.GetIdOfPart(pata));
                _odkazy[kluc] = odkazy;
            }

            var vlastnosti = new SectionProperties();
            if (odkazy.Hlavicka is not null)
            {
                vlastnosti.Append(new HeaderReference { Type = HeaderFooterValues.Default, Id = odkazy.Hlavicka });
            }

            vlastnosti.Append(
                new FooterReference { Type = HeaderFooterValues.Default, Id = odkazy.Pata },
                VelkostStrany(Rozlozenie),
                new PageMargin { Top = 1474, Bottom = 1361, Left = OkrajZostavyVlavo, Right = OkrajZostavyVpravo, Header = 680, Footer = 624, Gutter = 0 });
            if (_prva && dokument.Volby.TitulnaStrana)
            {
                vlastnosti.Append(new TitlePage());
            }

            _prva = false;
            return vlastnosti;
        }
    }

    /// <summary>Celý dokument ako obsah súboru Word (napr. príloha PDF/A-3b) – priamo v pamäti, bez dočasného súboru.</summary>
    public static byte[] Data(CelyDokument dokument)
    {
        using var pamat = new MemoryStream();
        Zapis(dokument, pamat);
        return pamat.ToArray();
    }

    public static void Uloz(CelyDokument dokument, string cesta)
    {
        var priecinok = Path.GetDirectoryName(cesta);
        if (!string.IsNullOrEmpty(priecinok))
        {
            Directory.CreateDirectory(priecinok);
        }

        using var subor = new FileStream(cesta, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        Zapis(dokument, subor);
    }

    private static void Zapis(CelyDokument dokument, Stream ciel)
    {
        using var docx = WordprocessingDocument.Create(ciel, WordprocessingDocumentType.Document);
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

        docx.PackageProperties.Title = dokument.Titul.Hlavicka;
        docx.PackageProperties.Creator = dokument.Titul.AutorSuboru ?? "";
        docx.PackageProperties.Subject = dokument.Titul.Podnadpis;

        if (dokument.Volby.TitulnaStrana)
        {
            TitulnaStrana(telo, dokument, hlavna, stav);
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
        var casti = new CastiZostavy(hlavna, dokument, telo, stav);
        foreach (var oblast in dokument.Oblasti)
        {
            // oblasť je vždy na výšku; nová časť sama začína novou stranou
            var prepnute = casti.Prepni(RozlozenieStrany.A4NaVysku);
            Oblast(telo, oblast, !prvy && !prepnute, volby.OddelovacOblasti, stav);
            prvy = false;
            for (var i = 0; i < oblast.Predmety.Count; i++)
            {
                var predmet = oblast.Predmety[i];
                var obsah = DokumentOsnovy.Upratane(predmet.Obsah);
                var castiPredmetu = DokumentOsnovy.Casti(obsah);
                prepnute = casti.Prepni(castiPredmetu[0].Rozlozenie);
                if (volby.OddelovacPredmetu)
                {
                    OddelovacPredmetu(telo, oblast, predmet, !prepnute);
                    prepnute = false;
                }

                var novaStrana = !prepnute && (volby.OddelovacPredmetu || volby.OddelovacOblasti || i > 0 || oblast.Charakteristika.Count > 0);
                Predmet(telo, oblast, predmet, novaStrana, castiPredmetu, casti, stav);
            }
        }

        // hlavička (ak má zapnutú aspoň jednu časť) a päta – nie na titulnej strane; vlastnosti poslednej časti sú v tele
        telo.Append(casti.Vlastnosti());
        hlavna.Document.Save();
    }

    /// <summary>Päta strán: vľavo škola a kontakt (ak sú zapnuté), vpravo číslo strany „Strana X z Y“.</summary>
    private static Footer Pata(SkVP.Services.TitulnaStrana titul, int sirkaTextu)
    {
        Paragraph odsek;
        if (titul.TextVPate is { } textPaty)
        {
            odsek = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Footer" },
                new Tabs(new TabStop { Val = TabStopValues.Right, Position = sirkaTextu }), new Justification { Val = JustificationValues.Left }));
            odsek.Append(Beh(textPaty));
            odsek.Append(new Run(new TabChar()));
        }
        else
        {
            odsek = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Footer" }, new Justification { Val = JustificationValues.Center }));
        }

        odsek.Append(Beh("Strana "));
        odsek.Append(Pole("PAGE", "1"));
        odsek.Append(Beh(" z "));
        odsek.Append(Pole("NUMPAGES", "1"));
        return new Footer(odsek);
    }

    // ---- časti dokumentu ----

    private static void TitulnaStrana(Body telo, CelyDokument dokument, MainDocumentPart hlavna, Stav stav)
    {
        var u = dokument.Udaje;

        // rovnaké zhustenie ako v PDF, aby sa úvodná strana zmestila na jednu stranu
        var z = PdfVystup.StupenZhustenia(dokument);
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

        var t = dokument.Titul;
        if (t.LogoNaUvodnejStrane(u.Logo) is { } cestaLoga && NastaveniaSkoly.Rozmer(cestaLoga) is { } rozmer)
        {
            var vyska = z >= 3 ? 1.8 : 2.6;
            var beh = Obrazok(hlavna, cestaLoga, vyska * rozmer.Sirka / rozmer.Vyska, vyska, 1U, "Logo školy");
            if (beh is not null)
            {
                telo.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = z >= 2 ? "240" : "400", After = "0" }), beh));
            }
        }

        telo.Append(Odsek(t.SkolaZobrazit && t.Skola.Length > 0 ? t.Skola : " ", "SkvpSkola"));
        if (t.ZlozkaNaStrane is { } zlozka)
        {
            telo.Append(Odsek(zlozka, "SkvpAdresa"));
        }

        if (t.AdresaZobrazit && t.Adresa.Length > 0)
        {
            telo.Append(Odsek(t.Adresa, "SkvpAdresa"));
        }

        foreach (var riadokOrganizacie in t.RiadkyPodAdresou())
        {
            telo.Append(Odsek(riadokOrganizacie, "SkvpAdresa"));
        }

        var nadpis = Odsek(t.NadpisZobrazit && t.Nadpis.Length > 0 ? t.Nadpis : " ", "Title");
        if (z > 0)
        {
            nadpis.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { Before = new[] { "3400", "1985", "1134", "680" }[z] };
            if (z >= 3)
            {
                foreach (var beh in nadpis.Elements<Run>())
                {
                    beh.PrependChild(new RunProperties(new FontSize { Val = "56" }));
                }
            }
        }

        telo.Append(nadpis);
        if (t.PodnadpisZobrazit && t.Podnadpis.Length > 0)
        {
            var podnadpis = Odsek(t.Podnadpis, "Subtitle");
            if (z >= 2)
            {
                podnadpis.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { After = "280" };
            }

            telo.Append(podnadpis);
        }

        foreach (var r in t.RiadkyPodNadpisom())
        {
            telo.Append(Odsek(r, "SkvpTitulUdaj"));
        }

        var prvy = true;
        foreach (var oblast in t.OblastiZobrazit ? dokument.Oblasti : new List<KapitolaOblasti>())
        {
            var p = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "SkvpTitulOblast" }));
            if (prvy)
            {
                p.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { Before = new[] { "1700", "850", "454", "340" }[z], After = "40" };
                prvy = false;
            }

            p.Append(Beh("■   ", farba: Hex(oblast.Farba)));
            p.Append(Beh(oblast.Nazov + "   "));
            p.Append(Beh(oblast.PocetText, farba: Siva));
            telo.Append(p);
        }

        var pata = t.PataNaStranu(dokument.Vytvorene);
        if (pata.Count > 0 && DokumentOsnovy.Text(pata).Trim().Length > 0)
        {
            if (SkVP.Services.TitulnaStrana.JeJednaVeta(pata))
            {
                var datum = Odsek(string.Concat(((Odsek)pata[0]).Useky.Select(u => u.Text)), "SkvpAdresa");
                datum.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { Before = new[] { "1100", "680", "454", "283" }[z] };
                telo.Append(datum);
            }
            else
            {
                telo.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = new[] { "900", "567", "340", "227" }[z], After = "0" })));
                foreach (var prvok in Bloky(pata, "3B5BDB", stav))
                {
                    telo.Append(prvok);
                }
            }
        }
        telo.Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
    }

    private static void Oblast(Body telo, KapitolaOblasti oblast, bool novaStrana, bool oddelovac, Stav stav)
    {
        var farba = Hex(oblast.Farba);
        var strana = UvodnaStranaOblasti.Pre(oblast, VzhladDokumentov.Aktivna, PdfVystup.SirkaTextuNaVysku);
        var nadpis = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Heading1" },
            new ParagraphBorders(new LeftBorder { Val = BorderValues.Single, Size = 48, Color = farba, Space = 12 }),
            new Indentation { Left = "360" }));
        if (novaStrana)
        {
            nadpis.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
        }

        // na oddeľujúcej strane medzera nad nadpisom len taká, aby sa zoznam predmetov zmestil na stranu
        var pred = oddelovac ? (int)Math.Round(Math.Min(2800, strana.MedzeraHore * 20 * 2800 / (UvodnaStranaOblasti.PlnaMedzera * 20))) : 0;
        nadpis.ParagraphProperties!.SpacingBetweenLines = new SpacingBetweenLines { Before = pred.ToString(CultureInfo.InvariantCulture) };

        nadpis.Append(Beh(oblast.Nazov, farba: farba));
        telo.Append(nadpis);

        var popis = Odsek($"{TypObjektu.Nazov(oblast.Typ)}  •  {oblast.PocetText}", "SkvpPodnadpisOblasti");
        popis.ParagraphProperties!.Indentation = new Indentation { Left = "360" };
        if (oddelovac)
        {
            popis.ParagraphProperties.SpacingBetweenLines = new SpacingBetweenLines
            {
                After = ((int)Math.Round(strana.MedzeraPodPopisom * 20)).ToString(CultureInfo.InvariantCulture),
            };
        }

        telo.Append(popis);

        if (oddelovac)
        {
            var odstup = ((int)Math.Round(strana.Odstup * 20 + 20)).ToString(CultureInfo.InvariantCulture);
            var velkost = ((int)Math.Round(strana.Nazov * 2)).ToString(CultureInfo.InvariantCulture);
            for (var i = 0; i < oblast.Predmety.Count; i++)
            {
                var predmet = oblast.Predmety[i];
                var vlastnosti = new ParagraphProperties(new ParagraphStyleId { Val = "SkvpZoznamPredmetov" });
                if (i < oblast.Predmety.Count - 1)
                {
                    // celý zoznam na jednej strane
                    vlastnosti.KeepNext = new KeepNext();
                }

                vlastnosti.ParagraphBorders = new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Linka, Space = 4 });
                vlastnosti.SpacingBetweenLines = new SpacingBetweenLines { Before = odstup, After = odstup, Line = "240", LineRule = LineSpacingRuleValues.Auto };
                var p = new Paragraph(vlastnosti);
                foreach (var beh in new[] { Beh(predmet.Cislo, tucne: true, farba: farba), new Run(new TabChar()), Beh(predmet.Nazov) })
                {
                    (beh.RunProperties ??= new RunProperties()).FontSize = new FontSize { Val = velkost };
                    p.Append(beh);
                }

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
            foreach (var prvok in Bloky(DokumentOsnovy.Upratane(oblast.Charakteristika).Where(b => b is not Sekcia).ToList(), farba, stav))
            {
                telo.Append(prvok);
            }
        }
    }

    private static void OddelovacPredmetu(Body telo, KapitolaOblasti oblast, KapitolaPredmetu predmet, bool novaStrana)
    {
        var farba = Hex(oblast.Farba);
        var cislo = Odsek(predmet.Cislo, "SkvpOddelovacCislo", farba);
        if (novaStrana)
        {
            cislo.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
        }

        telo.Append(cislo);
        var nazov = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "SkvpOddelovacNazov" },
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 24, Color = farba, Space = 10 })));
        nazov.Append(Beh(predmet.Nazov));
        telo.Append(nazov);
        telo.Append(Odsek(oblast.Nazov, "SkvpTitulUdaj", Siva));
    }

    private static void Predmet(Body telo, KapitolaOblasti oblast, KapitolaPredmetu predmet, bool novaStrana,
        List<(RozlozenieStrany Rozlozenie, List<Blok> Bloky)> obsah, CastiZostavy casti, Stav stav)
    {
        var farba = Hex(oblast.Farba);
        var sablona = VzhladDokumentov.Aktivna;
        var oblastText = Odsek($"{oblast.Nazov}  •  {predmet.Cislo}", "SkvpOblastMala", Bez(sablona.FarbaZahlavia(oblast.Farba)));
        if (novaStrana)
        {
            oblastText.ParagraphProperties!.PageBreakBefore = new PageBreakBefore();
        }

        telo.Append(oblastText);

        var nadpis = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Heading2" },
            new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 12, Color = Bez(sablona.FarbaPredmetu(oblast.Farba)), Space = 6 })));
        nadpis.Append(Beh(predmet.Nazov));
        telo.Append(nadpis);

        if (obsah.All(c => c.Bloky.Count == 0))
        {
            telo.Append(Odsek("Učebná osnova predmetu zatiaľ nie je spracovaná.", "SkvpPoznamka"));
            return;
        }

        foreach (var (rozlozenie, bloky) in obsah)
        {
            casti.Prepni(rozlozenie);
            foreach (var prvok in Bloky(bloky, farba, stav))
            {
                telo.Append(prvok);
            }
        }
    }

    /// <summary>
    /// Hlavička strán: tabuľka bez okrajov s čiarou dole – vľavo text, vpravo názov školy a logo. Vypnuté časti sa vynechajú.
    /// </summary>
    private static Header Hlavicka(HeaderPart cast, SkVP.Services.TitulnaStrana t, string? cestaLoga, int sirkaTextu, uint idObrazka)
    {
        const double twipovNaCm = 566.93;
        var text = t.TextVHlavicke;
        var skola = t.SkolaVHlavicke;
        var logo = t.LogoVHlavicke(cestaLoga);
        var (sirkaLoga, vyskaLoga) = logo is null ? (0.0, 0.0) : PdfVystup.RozmerLogaHlavicky(logo);
        var stlpecLoga = logo is null ? 0 : (int)Math.Round((sirkaLoga + (skola is null ? 0 : 0.3)) * twipovNaCm);
        var zvysok = sirkaTextu - stlpecLoga;

        var bunky = new List<(int Sirka, Paragraph Obsah)>();
        if (text is not null)
        {
            bunky.Add((skola is null ? zvysok : (int)(zvysok * 0.55), OdsekHlavicky(JustificationValues.Left, Beh(text))));
        }

        if (skola is not null)
        {
            bunky.Add((text is null ? zvysok : zvysok - (int)(zvysok * 0.55), OdsekHlavicky(JustificationValues.Right, Beh(skola))));
        }

        if (text is null && skola is null)
        {
            bunky.Add((zvysok, OdsekHlavicky(JustificationValues.Left)));
        }

        if (logo is not null)
        {
            var beh = Obrazok(cast, logo, sirkaLoga, vyskaLoga, idObrazka, "Logo školy v hlavičke");
            bunky.Add((stlpecLoga, beh is null ? OdsekHlavicky(JustificationValues.Right) : OdsekHlavicky(JustificationValues.Right, beh)));
        }

        var siet = new TableGrid();
        var riadok = new TableRow();
        foreach (var (sirka, obsah) in bunky)
        {
            siet.Append(new GridColumn { Width = sirka.ToString(CultureInfo.InvariantCulture) });
            riadok.Append(new TableCell(
                new TableCellProperties(
                    new TableCellWidth { Width = sirka.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }),
                obsah));
        }

        var tabulka = new Table(
            new TableProperties(
                new TableWidth { Width = sirkaTextu.ToString(CultureInfo.InvariantCulture), Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = Linka, Space = 0 },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None }),
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableCellMarginDefault(
                    new TopMargin { Width = "0", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 0, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 0, Type = TableWidthValues.Dxa })),
            siet,
            riadok);

        // hlavička vo Worde musí končiť odsekom – drobný prázdny odsek pod tabuľkou
        var koniec = new Paragraph(new ParagraphProperties(
            new ParagraphStyleId { Val = "Header" },
            new SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = LineSpacingRuleValues.Exact }));
        return new Header(tabulka, koniec);
    }

    private static Paragraph OdsekHlavicky(JustificationValues zarovnanie, params OpenXmlElement[] obsah)
    {
        var odsek = new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = "Header" }, new Justification { Val = zarovnanie }));
        odsek.Append(obsah);
        return odsek;
    }

    private static PartTypeInfo TypObrazka(string cesta) => Path.GetExtension(cesta).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => ImagePartType.Jpeg,
        ".bmp" => ImagePartType.Bmp,
        ".gif" => ImagePartType.Gif,
        _ => ImagePartType.Png,
    };

    /// <summary>Obrázok (logo) vložený do časti dokumentu ako beh textu s daným rozmerom v cm.</summary>
    private static Run? Obrazok(OpenXmlPart vlastnik, string cesta, double sirkaCm, double vyskaCm, uint cislo, string nazov)
    {
        try
        {
            var cast = vlastnik switch
            {
                MainDocumentPart m => m.AddImagePart(TypObrazka(cesta)),
                HeaderPart h => h.AddImagePart(TypObrazka(cesta)),
                _ => throw new ArgumentException("Nepodporovaná časť dokumentu.", nameof(vlastnik)),
            };
            using (var prud = File.OpenRead(cesta))
            {
                cast.FeedData(prud);
            }

            const long emuNaCm = 360000;
            var sirka = (long)Math.Round(sirkaCm * emuNaCm);
            var vyska = (long)Math.Round(vyskaCm * emuNaCm);
            var id = vlastnik.GetIdOfPart(cast);
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
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.DocProperties { Id = cislo, Name = nazov },
                new DocumentFormat.OpenXml.Drawing.Wordprocessing.NonVisualGraphicFrameDrawingProperties(
                    new DocumentFormat.OpenXml.Drawing.GraphicFrameLocks { NoChangeAspect = true }),
                new DocumentFormat.OpenXml.Drawing.Graphic(new DocumentFormat.OpenXml.Drawing.GraphicData(obrazok)
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
            return new Run(kresba);
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
                    var sablona = VzhladDokumentov.Aktivna;
                    var nadpis = Odsek(n.Text, stylNadpisu, sablona.FarbaNadpisu(n.Uroven, "#" + farba) is { } farbaNadpisu ? Bez(farbaNadpisu) : null);
                    if (n.Uroven == 1)
                    {
                        Ozdob(nadpis.ParagraphProperties!, sablona, "#" + farba);
                    }
                    else if (n.Uroven == 2 && sablona.Nadpis2Pasik)
                    {
                        nadpis.ParagraphProperties!.Append(new ParagraphBorders(
                            new LeftBorder { Val = BorderValues.Single, Size = 20, Color = Bez(sablona.FarbaOzdoby("#" + farba)), Space = 6 }));
                        nadpis.ParagraphProperties.Append(new Indentation { Left = "100" });
                    }

                    vysledok.Add(nadpis);
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

                case ZlomStrany:
                    vysledok.Add(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
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

    private static Table Tabulka(Tabulka t, string farba, Stav stav)
    {
        t = t.Pravidelna();
        var sablona = VzhladDokumentov.Aktivna;
        var oblast = "#" + farba;
        var linka = Bez(sablona.Linka(oblast));
        var hrubka = (uint)Math.Round(sablona.HrubkaLinky * 8);
        var zvisle = sablona.CiaraPodHlavickou ? BorderValues.Nil : BorderValues.Single;
        var vnutri = sablona.Ram == RamTabulky.Akademicky ? BorderValues.Nil : BorderValues.Single;
        var tabulka = new Table(new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = ((int)Math.Round(5000 * t.SirkaVTexte)).ToString(CultureInfo.InvariantCulture) },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = hrubka, Color = linka },
                new LeftBorder { Val = zvisle, Size = hrubka, Color = linka },
                new BottomBorder { Val = BorderValues.Single, Size = hrubka, Color = linka },
                new RightBorder { Val = zvisle, Size = hrubka, Color = linka },
                new InsideHorizontalBorder { Val = vnutri, Size = hrubka, Color = linka },
                new InsideVerticalBorder { Val = zvisle, Size = hrubka, Color = linka }),
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableCellMarginDefault(
                new TopMargin { Width = "50", Type = TableWidthUnitValues.Dxa },
                new TableCellLeftMargin { Width = 85, Type = TableWidthValues.Dxa },
                new BottomMargin { Width = "30", Type = TableWidthUnitValues.Dxa },
                new TableCellRightMargin { Width = 85, Type = TableWidthValues.Dxa })));

        var sirky = t.PomerneSirky.Select(x => (int)Math.Round(x * stav.SirkaTextu * t.SirkaVTexte)).ToList();
        var siet = new TableGrid();
        foreach (var sirka in sirky)
        {
            siet.Append(new GridColumn { Width = sirka.ToString(CultureInfo.InvariantCulture) });
        }

        tabulka.Append(siet);

        // obsadenosť mriežky – kvôli bunkám zlúčeným zvislo (vMerge) treba vedieť, kde pokračujú
        var pokracovanie = new Dictionary<(int Riadok, int Stlpec), int>();
        var sHlavickou = t.Riadky.Count > 0 && t.Riadky[0].Any(b => b.Hlavicka);
        var pruh = sablona.PodkladPruhu(oblast) is { } farbaPruhu ? Bez(farbaPruhu) : null;
        var podHlavickou = sablona.CiaraPodHlavickou
            ? new BottomBorder
            {
                Val = BorderValues.Single, Size = (uint)Math.Round(sablona.HrubkaPodHlavickou * 8),
                Color = sablona.Ram == RamTabulky.Akademicky ? linka : Bez(sablona.FarbaOzdoby(oblast)),
            }
            : null;
        var stylHlavicky = sablona.TextHlavicky is null ? StylTabulky : StylTabulkySvetly;
        for (var r = 0; r < t.Riadky.Count; r++)
        {
            var podklad = r > 0 && (r - (sHlavickou ? 1 : 0)) % 2 == 1 ? pruh : null;
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
                    riadok.Append(Bunka(new List<OpenXmlElement>(), Sucet(sirky, stlpec, rozpatie), rozpatie, MergedCellValues.Continue, null));
                    stlpec += rozpatie;
                    continue;
                }

                if (index >= t.Riadky[r].Count)
                {
                    riadok.Append(Bunka(new List<OpenXmlElement>(), sirky[stlpec], 1, null, podklad));
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
                var bunka = Bunka(obsah, Sucet(sirky, stlpec, stlpce), stlpce, zvislo, b.Hlavicka ? Bez(sablona.PodkladHlavicky(oblast)) : podklad,
                    b.Hlavicka ? stylHlavicky : StylTabulky);
                if (b.Hlavicka && podHlavickou is not null)
                {
                    // okraje bunky musia byť pred podkladom (poradie prvkov Wordu)
                    var vlastnosti = bunka.TableCellProperties!;
                    var okraje = new TableCellBorders((BottomBorder)podHlavickou.CloneNode(true));
                    if (vlastnosti.GetFirstChild<Shading>() is { } tien)
                    {
                        vlastnosti.InsertBefore(okraje, tien);
                    }
                    else
                    {
                        vlastnosti.Append(okraje);
                    }
                }

                riadok.Append(bunka);
                stlpec += stlpce;
            }

            tabulka.Append(riadok);
        }

        return tabulka;
    }

    private static int Sucet(List<int> sirky, int od, int pocet) => sirky.Skip(od).Take(pocet).Sum();

    /// <param name="podklad">farba podkladu bunky (hlavička, pruh) bez # alebo null</param>
    /// <param name="styl">štýl odsekov v bunke (text v tmavej hlavičke je biely)</param>
    private static TableCell Bunka(List<OpenXmlElement> obsah, int sirka, int stlpce, MergedCellValues? zvislo, string? podklad, string styl = StylTabulky)
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

        if (podklad is not null)
        {
            vlastnosti.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = podklad, Color = "auto" });
        }

        var bunka = new TableCell(vlastnosti);
        foreach (var prvok in obsah)
        {
            if (prvok is Paragraph odsek)
            {
                odsek.ParagraphProperties ??= new ParagraphProperties();
                if (odsek.ParagraphProperties.ParagraphStyleId is null)
                {
                    odsek.ParagraphProperties.PrependChild(new ParagraphStyleId { Val = styl });
                }
            }
        }

        if (obsah.Count == 0)
        {
            obsah.Add(new Paragraph(new ParagraphProperties(new ParagraphStyleId { Val = styl })));
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
            // každá časť s vlastným rozložením strany (osnova na šírku) – vlastnosti časti sú na jej konci
            var casti = DokumentOsnovy.Casti(bloky);
            for (var i = 0; i < casti.Count; i++)
            {
                var (rozlozenie, blokyCasti) = casti[i];
                stav.SirkaTextu = Twipy(rozlozenie.SirkaMm) - 2 * OkrajOsnovy;
                foreach (var prvok in Bloky(blokyCasti, "3B5BDB", stav))
                {
                    telo.Append(prvok);
                }

                var vlastnosti = new SectionProperties(
                    VelkostStrany(rozlozenie),
                    new PageMargin { Top = OkrajOsnovy, Bottom = OkrajOsnovy, Left = (uint)OkrajOsnovy, Right = (uint)OkrajOsnovy, Header = 709, Footer = 709, Gutter = 0 });
                if (i < casti.Count - 1)
                {
                    telo.Append(new Paragraph(new ParagraphProperties(vlastnosti)));
                    continue;
                }

                if (!telo.HasChildren || telo.LastChild is Table)
                {
                    // Word chce za tabuľkou na konci dokumentu ešte odsek
                    telo.Append(new Paragraph());
                }

                telo.Append(vlastnosti);
            }
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
        var sablona = VzhladDokumentov.Aktivna;
        var text = sablona.PismoTextu;
        var nadpis = sablona.PismoNadpisov;
        int Pol(double body) => (int)Math.Round(body * 2);
        string Twip(double body) => ((int)Math.Round(body * 20)).ToString(CultureInfo.InvariantCulture);
        var velkost = Pol(sablona.VelkostTextu);

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

        // nadpisy a údaje vľavo aj v šablóne s textom do bloku
        StyleParagraphProperties Medzery(int pred, int po) =>
            new(new SpacingBetweenLines { Before = pred.ToString(CultureInfo.InvariantCulture), After = po.ToString(CultureInfo.InvariantCulture) },
                new Justification { Val = JustificationValues.Left });

        var normalOdsek = new StyleParagraphProperties(new SpacingBetweenLines
        {
            After = Twip(sablona.MedzeraZaOdsekom),
            Line = ((int)Math.Round(240 * sablona.Riadkovanie)).ToString(CultureInfo.InvariantCulture),
            LineRule = LineSpacingRuleValues.Auto,
        });
        if (sablona.DoBloku)
        {
            normalOdsek.Append(new Justification { Val = JustificationValues.Both });
        }

        var normal = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        var farbaTextu = Bez(sablona.FarbaTextu);
        normal.Append(new StyleName { Val = "Normal" }, new PrimaryStyle(), normalOdsek, Pismo(text, velkost, farba: farbaTextu));

        var styly = new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = text, HighAnsi = text, ComplexScript = text, EastAsia = text },
                    new FontSize { Val = velkost.ToString(CultureInfo.InvariantCulture) }, new Languages { Val = "sk-SK" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(new SpacingBetweenLines { After = Twip(sablona.MedzeraZaOdsekom) }))),
            normal,
            Styl("Title", "Title", "Normal", Pismo(nadpis, 68, true), Medzery(3400, 200)),
            Styl("Subtitle", "Subtitle", "Normal", Pismo(text, 48, kurziva: true, farba: "3B5BDB"), Medzery(0, 520)),
            osnova
                ? Styl("Heading1", "heading 1", "Normal", Pismo(nadpis, Pol(sablona.Nadpis1), true), Medzery(Pol(sablona.Nadpis1) * 11, 120), 0)
                : Styl("Heading1", "heading 1", "Normal", Pismo(nadpis, 52, true), Medzery(2800, 200), 0),
            osnova
                ? Styl("Heading2", "heading 2", "Normal", Pismo(nadpis, Pol(sablona.Nadpis2), true), Medzery(Pol(sablona.Nadpis2) * 9, 80), 1)
                : Styl("Heading2", "heading 2", "Normal", Pismo(nadpis, 38, true), Medzery(0, 280), 1),
            osnova
                ? Styl("Heading3", "heading 3", "Normal", Pismo(nadpis, Pol(sablona.Nadpis3), true, kurziva: true), Medzery(200, 80), 2)
                : Styl("Heading3", "heading 3", "Normal", Pismo(nadpis, Pol(sablona.Nadpis1), true), Medzery(Pol(sablona.Nadpis1) * 11, 120), 2),
            Styl("Heading4", "heading 4", "Normal", Pismo(nadpis, Pol(sablona.Nadpis2), true), Medzery(Pol(sablona.Nadpis2) * 9, 80), 3),
            Styl("Heading5", "heading 5", "Normal", Pismo(nadpis, Pol(sablona.Nadpis3), true, kurziva: true), Medzery(160, 60), 4),
            Styl("ListParagraph", "List Paragraph", "Normal", Pismo(text, velkost, farba: farbaTextu), new StyleParagraphProperties(new SpacingBetweenLines { After = "50" })),
            Styl("Header", "header", "Normal", Pismo(nadpis, 17, farba: Siva), Medzery(0, 0)),
            Styl("Footer", "footer", "Normal", Pismo(nadpis, 17, farba: Siva), Medzery(0, 0)),
            Styl("SkvpSkola", "SkVP – škola", "Normal", Pismo(nadpis, 26, true), Medzery(360, 20)),
            Styl("SkvpAdresa", "SkVP – adresa", "Normal", Pismo(nadpis, 20, farba: Siva), Medzery(0, 0)),
            Styl("SkvpTitulUdaj", "SkVP – údaj titulnej strany", "Normal", Pismo(nadpis, 24), Medzery(0, 60)),
            Styl("SkvpTitulOblast", "SkVP – oblasť na titulnej strane", "Normal", Pismo(nadpis, 20), Medzery(0, 40)),
            Styl("SkvpNadpisObsahu", "SkVP – nadpis obsahu", "Normal", Pismo(nadpis, 48, true), Medzery(0, 280)),
            Styl("SkvpPodnadpisOblasti", "SkVP – podnadpis oblasti", "Normal", Pismo(nadpis, 20, farba: Siva), Medzery(0, 440)),
            Styl("SkvpOblastMala", "SkVP – oblasť nad predmetom", "Normal", Pismo(nadpis, 19, true), Medzery(0, 40)),
            Styl("SkvpPoznamka", "SkVP – poznámka", "Normal", Pismo(text, velkost, kurziva: true, farba: Siva)),
            Styl(StylTabulky, "SkVP – text v tabuľke", "Normal", Pismo(text, (int)Math.Round(velkost * 0.91), farba: farbaTextu), Medzery(0, 40)),
            Styl(StylTabulkySvetly, "SkVP – text v tmavej hlavičke tabuľky", StylTabulky,
                Pismo(text, (int)Math.Round(velkost * 0.91), farba: Bez(sablona.TextHlavicky ?? sablona.FarbaTextu)), Medzery(0, 40)),
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

    /// <summary>Farba #RRGGBB → RRGGBB (formát Wordu).</summary>
    private static string Bez(string farba) => farba.TrimStart('#').ToUpperInvariant();

    /// <summary>Ozdoba nadpisu časti osnovy podľa šablóny vzhľadu (čiara, pásik vľavo, farebný pás).</summary>
    private static void Ozdob(ParagraphProperties vlastnosti, SablonaVzhladu sablona, string oblast)
    {
        var farba = Bez(sablona.FarbaOzdoby(oblast));
        switch (sablona.Ozdoba)
        {
            case OzdobaNadpisu.Ciara:
                vlastnosti.Append(new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 10, Color = farba, Space = 4 }));
                break;

            case OzdobaNadpisu.Pasik:
                vlastnosti.Append(new ParagraphBorders(new LeftBorder { Val = BorderValues.Single, Size = 32, Color = farba, Space = 8 }));
                vlastnosti.Append(new Indentation { Left = "120" });
                break;

            case OzdobaNadpisu.Pas or OzdobaNadpisu.Karta:
                var podklad = Bez(sablona.PodkladNadpisu(oblast)!);
                vlastnosti.Append(new ParagraphBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = podklad, Space = 3 },
                    new LeftBorder { Val = BorderValues.Single, Size = 32, Color = farba, Space = 8 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = podklad, Space = 3 }));
                vlastnosti.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = podklad, Color = "auto" });
                vlastnosti.Append(new Indentation { Left = "120" });
                break;

            case OzdobaNadpisu.Nadciarka:
                vlastnosti.Append(new ParagraphBorders(new TopBorder { Val = BorderValues.Single, Size = 20, Color = farba, Space = 6 }));
                break;

            case OzdobaNadpisu.Ramik:
                vlastnosti.Append(new ParagraphBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 8, Color = farba, Space = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 8, Color = farba, Space = 6 },
                    new BottomBorder { Val = BorderValues.Single, Size = 8, Color = farba, Space = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 8, Color = farba, Space = 6 }));
                vlastnosti.Append(new Indentation { Left = "120", Right = "120" });
                break;
        }
    }
}
