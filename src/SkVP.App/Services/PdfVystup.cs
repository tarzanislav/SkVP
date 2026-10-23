using System.Globalization;
using System.IO;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SkVP.Models;
using MdColor = MigraDoc.DocumentObjectModel.Color;
using WinAlign = System.Windows.TextAlignment;

namespace SkVP.Services;

/// <summary>
/// Vysádzanie celého dokumentu učebných osnov (MigraDoc). Rovnaký dokument sa zobrazuje v náhľade, tlačí a ukladá
/// do PDF – vytlačené osnovy vyzerajú presne ako PDF. Oblasti majú svoju farbu ako záložky v šanóne.
/// </summary>
public static class PdfVystup
{
    private const string StylPredmet = "SkvpPredmet";
    private const string StylOblastMala = "SkvpOblastMala";
    private const string StylN1 = "SkvpN1";
    private const string StylN2 = "SkvpN2";
    private const string StylN3 = "SkvpN3";
    private const string StylObsah1 = "SkvpObsah1";
    private const string StylObsah2 = "SkvpObsah2";
    private const string StylPoznamka = "SkvpPoznamka";
    private const string StylTabulka = "SkvpTabulka";

    private static readonly MdColor Atrament = Farba("#1F2933");
    private static readonly MdColor Siva = Farba("#6B7280");
    private static readonly MdColor Linka = Farba("#CBD2D9");
    private static readonly MdColor Podklad = Farba("#F3F4F6");

    private static readonly Unit SirkaTextu = Unit.FromCentimeter(21.0 - 2.4 - 2.2);

    public static void UlozPdf(CelyDokument dokument, string cesta)
    {
        var vykreslenie = new PdfDocumentRenderer { Document = Vytvor(dokument) };
        vykreslenie.RenderDocument();
        var priecinok = Path.GetDirectoryName(cesta);
        if (!string.IsNullOrEmpty(priecinok))
        {
            Directory.CreateDirectory(priecinok);
        }

        vykreslenie.PdfDocument.Save(cesta);
    }

    public static Document Vytvor(CelyDokument dokument)
    {
        var doc = new Document();
        doc.Info.Title = dokument.Titul.Hlavicka;
        doc.Info.Subject = dokument.Titul.Podnadpis;
        doc.Info.Author = dokument.Udaje.Skola;
        Styly(doc);

        if (dokument.Volby.TitulnaStrana)
        {
            TitulnaStrana(doc, dokument);
        }

        var sekcia = doc.AddSection();
        NastavStranu(sekcia);
        HlavickaAPata(sekcia, dokument);

        var prvy = true;
        if (dokument.Volby.Obsah)
        {
            ObsahDokumentu(sekcia, dokument);
            prvy = false;
        }

        var volby = dokument.Volby;
        foreach (var oblast in dokument.Oblasti)
        {
            Oblast(sekcia, oblast, !prvy, volby.OddelovacOblasti);
            prvy = false;
            for (var i = 0; i < oblast.Predmety.Count; i++)
            {
                var predmet = oblast.Predmety[i];
                if (volby.OddelovacPredmetu)
                {
                    OddelovacPredmetu(sekcia, oblast, predmet);
                }

                // bez oddeľujúcej strany oblasti prvý predmet pokračuje hneď pod nadpisom oblasti
                var novaStrana = volby.OddelovacPredmetu || volby.OddelovacOblasti || i > 0 || oblast.Charakteristika.Count > 0;
                Predmet(sekcia, oblast, predmet, novaStrana);
            }
        }

        if (dokument.Oblasti.Count == 0)
        {
            var p = sekcia.AddParagraph("Dokument zatiaľ neobsahuje žiadne vzdelávacie oblasti.");
            p.Style = StylPoznamka;
        }

        return doc;
    }

    private static void Styly(Document doc)
    {
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = PismaDokumentu.Text;
        normal.Font.Size = 11;
        normal.Font.Color = Atrament;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = 1.18;

        var oblast = doc.Styles[StyleNames.Heading1]!;
        oblast.Font.Name = PismaDokumentu.Nadpis;
        oblast.Font.Size = 26;
        oblast.Font.Bold = true;
        oblast.ParagraphFormat.SpaceBefore = Unit.FromCentimeter(5);
        oblast.ParagraphFormat.SpaceAfter = Unit.FromPoint(10);
        oblast.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        oblast.ParagraphFormat.KeepWithNext = true;
        oblast.ParagraphFormat.OutlineLevel = OutlineLevel.Level1;

        var predmet = doc.Styles.AddStyle(StylPredmet, StyleNames.Normal);
        predmet.Font.Name = PismaDokumentu.Nadpis;
        predmet.Font.Size = 19;
        predmet.Font.Bold = true;
        predmet.ParagraphFormat.SpaceAfter = Unit.FromPoint(14);
        predmet.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        predmet.ParagraphFormat.KeepWithNext = true;
        predmet.ParagraphFormat.OutlineLevel = OutlineLevel.Level2;

        var oblastMala = doc.Styles.AddStyle(StylOblastMala, StyleNames.Normal);
        oblastMala.Font.Name = PismaDokumentu.Nadpis;
        oblastMala.Font.Size = 9.5;
        oblastMala.Font.Bold = true;
        oblastMala.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        oblastMala.ParagraphFormat.KeepWithNext = true;

        var n1 = doc.Styles.AddStyle(StylN1, StyleNames.Normal);
        n1.Font.Name = PismaDokumentu.Nadpis;
        n1.Font.Size = 13;
        n1.Font.Bold = true;
        n1.ParagraphFormat.SpaceBefore = Unit.FromPoint(14);
        n1.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
        n1.ParagraphFormat.KeepWithNext = true;
        n1.ParagraphFormat.OutlineLevel = OutlineLevel.Level3;

        var n2 = doc.Styles.AddStyle(StylN2, StylN1);
        n2.Font.Size = 11.5;
        n2.ParagraphFormat.SpaceBefore = Unit.FromPoint(10);
        n2.ParagraphFormat.OutlineLevel = OutlineLevel.Level4;

        var n3 = doc.Styles.AddStyle(StylN3, StylN1);
        n3.Font.Size = 11;
        n3.Font.Bold = false;
        n3.Font.Italic = true;
        n3.ParagraphFormat.SpaceBefore = Unit.FromPoint(8);
        n3.ParagraphFormat.OutlineLevel = OutlineLevel.BodyText;

        var obsah1 = doc.Styles.AddStyle(StylObsah1, StyleNames.Normal);
        obsah1.Font.Name = PismaDokumentu.Nadpis;
        obsah1.Font.Size = 10.5;
        obsah1.Font.Bold = true;
        obsah1.ParagraphFormat.SpaceBefore = Unit.FromPoint(9);
        obsah1.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        obsah1.ParagraphFormat.TabStops.AddTabStop(SirkaTextu, TabAlignment.Right, TabLeader.Dots);

        var obsah2 = doc.Styles.AddStyle(StylObsah2, StyleNames.Normal);
        obsah2.Font.Size = 10.5;
        obsah2.ParagraphFormat.LeftIndent = Unit.FromCentimeter(0.7);
        obsah2.ParagraphFormat.SpaceAfter = Unit.FromPoint(1.5);
        obsah2.ParagraphFormat.TabStops.AddTabStop(SirkaTextu, TabAlignment.Right, TabLeader.Dots);

        var poznamka = doc.Styles.AddStyle(StylPoznamka, StyleNames.Normal);
        poznamka.Font.Italic = true;
        poznamka.Font.Color = Siva;

        var tabulka = doc.Styles.AddStyle(StylTabulka, StyleNames.Normal);
        tabulka.Font.Size = 10;
        tabulka.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        tabulka.ParagraphFormat.LineSpacing = 1.1;

        var hlavicka = doc.Styles[StyleNames.Header]!;
        hlavicka.Font.Name = PismaDokumentu.Nadpis;
        hlavicka.Font.Size = 8.5;
        hlavicka.Font.Color = Siva;
        hlavicka.ParagraphFormat.TabStops.ClearAll();
        hlavicka.ParagraphFormat.TabStops.AddTabStop(SirkaTextu, TabAlignment.Right);

        var pata = doc.Styles[StyleNames.Footer]!;
        pata.Font.Name = PismaDokumentu.Nadpis;
        pata.Font.Size = 8.5;
        pata.Font.Color = Siva;
        pata.ParagraphFormat.Alignment = ParagraphAlignment.Center;
    }

    private static void NastavStranu(Section sekcia)
    {
        sekcia.PageSetup.PageFormat = PageFormat.A4;
        sekcia.PageSetup.Orientation = Orientation.Portrait;
        sekcia.PageSetup.TopMargin = Unit.FromCentimeter(2.6);
        sekcia.PageSetup.BottomMargin = Unit.FromCentimeter(2.4);
        sekcia.PageSetup.LeftMargin = Unit.FromCentimeter(2.4);
        sekcia.PageSetup.RightMargin = Unit.FromCentimeter(2.2);
        sekcia.PageSetup.HeaderDistance = Unit.FromCentimeter(1.2);
        sekcia.PageSetup.FooterDistance = Unit.FromCentimeter(1.1);
    }

    private static void TitulnaStrana(Document doc, CelyDokument dokument)
    {
        var sekcia = doc.AddSection();
        NastavStranu(sekcia);
        var u = dokument.Udaje;

        // pás farieb oblastí – záložky šanónu
        if (dokument.Oblasti.Count > 0)
        {
            var pas = sekcia.AddTable();
            pas.Borders.Visible = false;
            var sirka = SirkaTextu.Point / dokument.Oblasti.Count;
            foreach (var _ in dokument.Oblasti)
            {
                pas.AddColumn(Unit.FromPoint(sirka));
            }

            var riadok = pas.AddRow();
            riadok.Height = Unit.FromPoint(7);
            riadok.HeightRule = RowHeightRule.Exactly;
            for (var i = 0; i < dokument.Oblasti.Count; i++)
            {
                riadok.Cells[i].Shading.Color = Farba(dokument.Oblasti[i].Farba);
            }
        }

        var t = dokument.Titul;
        if (t.LogoNaUvodnejStrane(u.Logo) is { } cestaLoga)
        {
            var odsekLoga = sekcia.AddParagraph();
            odsekLoga.Format.SpaceBefore = Unit.FromPoint(22);
            odsekLoga.Format.SpaceAfter = 0;
            odsekLoga.Format.LineSpacingRule = LineSpacingRule.Single;
            var logo = odsekLoga.AddImage(cestaLoga);
            logo.LockAspectRatio = true;
            logo.Height = Unit.FromCentimeter(2.6);
        }

        var skola = sekcia.AddParagraph(t.SkolaZobrazit && t.Skola.Length > 0 ? t.Skola : " ");
        skola.Format.Font.Name = PismaDokumentu.Nadpis;
        skola.Format.Font.Size = 13;
        skola.Format.Font.Bold = true;
        skola.Format.SpaceBefore = Unit.FromPoint(18);
        skola.Format.SpaceAfter = Unit.FromPoint(1);
        if (t.AdresaZobrazit && t.Adresa.Length > 0)
        {
            var adresa = sekcia.AddParagraph(t.Adresa);
            adresa.Format.Font.Name = PismaDokumentu.Nadpis;
            adresa.Format.Font.Size = 10;
            adresa.Format.Font.Color = Siva;
        }

        // nadpis ostáva na rovnakom mieste, aj keď je vypnutý
        var program = sekcia.AddParagraph(t.NadpisZobrazit && t.Nadpis.Length > 0 ? t.Nadpis : " ");
        program.Format.Font.Name = PismaDokumentu.Nadpis;
        program.Format.Font.Size = 34;
        program.Format.Font.Bold = true;
        program.Format.LineSpacingRule = LineSpacingRule.Multiple;
        program.Format.LineSpacing = 1.0;
        program.Format.SpaceBefore = Unit.FromCentimeter(6.5);
        program.Format.SpaceAfter = Unit.FromPoint(10);

        if (t.PodnadpisZobrazit && t.Podnadpis.Length > 0)
        {
            var podtitul = sekcia.AddParagraph(t.Podnadpis);
            podtitul.Format.Font.Name = PismaDokumentu.Text;
            podtitul.Format.Font.Size = 24;
            podtitul.Format.Font.Italic = true;
            podtitul.Format.Font.Color = Farba("#3B5BDB");
            podtitul.Format.SpaceAfter = Unit.FromPoint(26);
        }

        foreach (var riadok in t.RiadkyPodNadpisom())
        {
            var p = sekcia.AddParagraph(riadok);
            p.Format.Font.Name = PismaDokumentu.Nadpis;
            p.Format.Font.Size = 12;
            p.Format.SpaceAfter = Unit.FromPoint(3);
        }

        // zoznam oblastí s farebnou značkou
        if (!t.OblastiZobrazit)
        {
            Pata(sekcia, t, dokument.Vytvorene);
            return;
        }

        var zoznam = sekcia.AddTable();
        zoznam.Borders.Visible = false;
        zoznam.TopPadding = Unit.FromPoint(2.5);
        zoznam.BottomPadding = Unit.FromPoint(2.5);
        zoznam.AddColumn(Unit.FromPoint(14));
        zoznam.AddColumn(Unit.FromPoint(SirkaTextu.Point - 14));
        var prvy = true;
        foreach (var oblast in dokument.Oblasti)
        {
            var r = zoznam.AddRow();
            if (prvy)
            {
                r.TopPadding = Unit.FromCentimeter(3);
                prvy = false;
            }

            var znacka = r.Cells[0].AddParagraph();
            znacka.Format.SpaceAfter = 0;
            var stvorcek = znacka.AddFormattedText("■");
            stvorcek.Font.Name = PismaDokumentu.Nadpis;
            stvorcek.Font.Color = Farba(oblast.Farba);
            var nazov = r.Cells[1].AddParagraph($"{oblast.Nazov}   ");
            nazov.Format.Font.Name = PismaDokumentu.Nadpis;
            nazov.Format.Font.Size = 10;
            nazov.Format.SpaceAfter = 0;
            var pocet = nazov.AddFormattedText(PocetPredmetov(oblast.Predmety.Count));
            pocet.Font.Color = Siva;
        }

        Pata(sekcia, t, dokument.Vytvorene);
    }

    /// <summary>Text dole na úvodnej strane (napr. „Vytvorené 23. 10. 2026 v programe SkVP“), ak je zapnutý.</summary>
    private static void Pata(Section sekcia, TitulnaStrana t, DateTime vytvorene)
    {
        var bloky = t.PataNaStranu(vytvorene);
        if (bloky.Count == 0 || DokumentOsnovy.Text(bloky).Trim().Length == 0)
        {
            return;
        }

        if (SkVP.Services.TitulnaStrana.JeJednaVeta(bloky))
        {
            var datum = sekcia.AddParagraph(string.Concat(((Odsek)bloky[0]).Useky.Select(u => u.Text)));
            datum.Format.Font.Name = PismaDokumentu.Nadpis;
            datum.Format.Font.Size = 8.5;
            datum.Format.Font.Color = Siva;
            datum.Format.SpaceBefore = Unit.FromCentimeter(2);
            return;
        }

        // ľubovoľný obsah – odseky, zoznamy, tabuľka (napr. schválenie pedagogickou radou s podpismi)
        var medzera = sekcia.AddParagraph();
        medzera.Format.SpaceBefore = Unit.FromCentimeter(1.6);
        medzera.Format.SpaceAfter = 0;
        Bloky(sekcia, bloky, Farba("#3B5BDB"), 0);
    }

    /// <summary>Výška loga v hlavičke strán a jeho najväčšia šírka (pri širokom logu).</summary>
    public const double VyskaLogaHlavickyCm = 0.8;
    public const double MaxSirkaLogaHlavickyCm = 3.5;

    /// <summary>Rozmer loga v hlavičke v centimetroch (pomer strán zachovaný).</summary>
    public static (double Sirka, double Vyska) RozmerLogaHlavicky(string logo)
    {
        var (px, py) = NastaveniaSkoly.Rozmer(logo) ?? (1, 1);
        var vyska = VyskaLogaHlavickyCm;
        var sirka = vyska * px / py;
        if (sirka > MaxSirkaLogaHlavickyCm)
        {
            sirka = MaxSirkaLogaHlavickyCm;
            vyska = sirka * py / px;
        }

        return (sirka, vyska);
    }

    private static void HlavickaAPata(Section sekcia, CelyDokument dokument)
    {
        var t = dokument.Titul;
        var text = t.TextVHlavicke;
        var skola = t.SkolaVHlavicke;
        var logo = t.LogoVHlavicke(dokument.Udaje.Logo);
        if (text is not null || skola is not null || logo is not null)
        {
            Hlavicka(sekcia.Headers.Primary, text, skola, logo);
        }

        var pata = sekcia.Footers.Primary.AddParagraph();
        pata.AddText("Strana ");
        pata.AddPageField();
        pata.AddText(" z ");
        pata.AddNumPagesField();
    }

    /// <summary>Hlavička strany: vľavo text, vpravo názov školy a logo, pod tým tenká čiara. Vypnuté časti sa vynechajú.</summary>
    private static void Hlavicka(HeaderFooter hlavicka, string? text, string? skola, string? logo)
    {
        var (sirkaLoga, vyskaLoga) = logo is null ? (0.0, 0.0) : RozmerLogaHlavicky(logo);
        var stlpecLoga = logo is null ? 0 : Unit.FromCentimeter(sirkaLoga + (skola is null ? 0 : 0.3)).Point;
        var zvysok = SirkaTextu.Point - stlpecLoga;

        var tabulka = hlavicka.AddTable();
        tabulka.LeftPadding = 0;
        tabulka.RightPadding = 0;
        tabulka.TopPadding = 0;
        tabulka.BottomPadding = Unit.FromPoint(3);
        var bunky = new List<Action<Cell>>();
        if (text is not null)
        {
            tabulka.AddColumn(Unit.FromPoint(skola is null ? zvysok : zvysok * 0.55));
            bunky.Add(c => OdsekHlavicky(c, ParagraphAlignment.Left).AddText(text));
        }

        if (skola is not null)
        {
            tabulka.AddColumn(Unit.FromPoint(text is null ? zvysok : zvysok * 0.45));
            bunky.Add(c => OdsekHlavicky(c, ParagraphAlignment.Right).AddText(skola));
        }

        if (text is null && skola is null)
        {
            // samotné logo je vpravo
            tabulka.AddColumn(Unit.FromPoint(zvysok));
            bunky.Add(_ => { });
        }

        if (logo is not null)
        {
            tabulka.AddColumn(Unit.FromPoint(stlpecLoga));
            bunky.Add(c =>
            {
                var obrazok = OdsekHlavicky(c, ParagraphAlignment.Right).AddImage(logo);
                obrazok.LockAspectRatio = true;
                obrazok.Width = Unit.FromCentimeter(sirkaLoga);
                obrazok.Height = Unit.FromCentimeter(vyskaLoga);
            });
        }

        var riadok = tabulka.AddRow();
        riadok.VerticalAlignment = VerticalAlignment.Center;
        riadok.Borders.Bottom.Width = Unit.FromPoint(0.5);
        riadok.Borders.Bottom.Color = Linka;
        for (var i = 0; i < bunky.Count; i++)
        {
            bunky[i](riadok.Cells[i]);
        }
    }

    private static Paragraph OdsekHlavicky(Cell bunka, ParagraphAlignment zarovnanie)
    {
        var odsek = bunka.AddParagraph();
        odsek.Style = StyleNames.Header;
        odsek.Format.Alignment = zarovnanie;
        odsek.Format.SpaceBefore = 0;
        odsek.Format.SpaceAfter = 0;
        odsek.Format.LineSpacingRule = LineSpacingRule.Single;
        return odsek;
    }

    private static void ObsahDokumentu(Section sekcia, CelyDokument dokument)
    {
        var nadpis = sekcia.AddParagraph("Obsah");
        nadpis.Format.Font.Name = PismaDokumentu.Nadpis;
        nadpis.Format.Font.Size = 24;
        nadpis.Format.Font.Bold = true;
        nadpis.Format.SpaceAfter = Unit.FromPoint(14);

        foreach (var oblast in dokument.Oblasti)
        {
            var p = sekcia.AddParagraph();
            p.Style = StylObsah1;
            var znacka = p.AddFormattedText("■  ");
            znacka.Font.Color = Farba(oblast.Farba);
            p.AddText(oblast.Nazov);
            p.AddTab();
            p.AddPageRefField(Zalozka(oblast.Id));

            foreach (var predmet in oblast.Predmety)
            {
                var r = sekcia.AddParagraph();
                r.Style = StylObsah2;
                var cislo = r.AddFormattedText(predmet.Cislo + "   ");
                cislo.Font.Color = Siva;
                r.AddText(predmet.Nazov);
                r.AddTab();
                r.AddPageRefField(Zalozka(predmet.Id));
            }
        }
    }

    private static void Oblast(Section sekcia, KapitolaOblasti oblast, bool novaStrana, bool oddelovac)
    {
        var farba = Farba(oblast.Farba);
        if (oddelovac)
        {
            // oddeľujúca strana: nadpis oblasti nižšie na strane (medzera hore na strane sa inak vynechá)
            Medzera(sekcia, 4.5, novaStrana);
            novaStrana = false;
        }

        var nadpis = sekcia.AddParagraph();
        nadpis.Style = StyleNames.Heading1;
        nadpis.Format.PageBreakBefore = novaStrana;
        nadpis.Format.Font.Color = farba;
        nadpis.Format.Borders.Left.Width = Unit.FromPoint(6);
        nadpis.Format.Borders.Left.Color = farba;
        nadpis.Format.Borders.DistanceFromLeft = Unit.FromPoint(12);
        nadpis.Format.LeftIndent = Unit.FromPoint(18);
        if (!oddelovac)
        {
            nadpis.Format.SpaceBefore = 0;
            nadpis.Format.Font.Size = 20;
        }

        nadpis.AddBookmark(Zalozka(oblast.Id));
        nadpis.AddText(oblast.Nazov);

        var popis = sekcia.AddParagraph($"Vzdelávacia oblasť  •  {PocetPredmetov(oblast.Predmety.Count)}");
        popis.Format.Font.Name = PismaDokumentu.Nadpis;
        popis.Format.Font.Size = 10;
        popis.Format.Font.Color = Siva;
        popis.Format.LeftIndent = Unit.FromPoint(18);
        popis.Format.SpaceAfter = Unit.FromPoint(oddelovac ? 26 : 14);

        if (oddelovac && oblast.Predmety.Count > 0)
        {
            // zoznam predmetov oblasti s číslom poradia a stranou
            var zoznam = sekcia.AddTable();
            zoznam.Borders.Visible = false;
            zoznam.TopPadding = Unit.FromPoint(4);
            zoznam.BottomPadding = Unit.FromPoint(4);
            zoznam.AddColumn(Unit.FromPoint(46));
            zoznam.AddColumn(Unit.FromPoint(SirkaTextu.Point - 46 - 40));
            zoznam.AddColumn(Unit.FromPoint(40));
            foreach (var predmet in oblast.Predmety)
            {
                var r = zoznam.AddRow();
                r.Borders.Bottom.Width = Unit.FromPoint(0.5);
                r.Borders.Bottom.Color = Linka;
                var cislo = r.Cells[0].AddParagraph(predmet.Cislo);
                cislo.Format.Font.Name = PismaDokumentu.Nadpis;
                cislo.Format.Font.Size = 15;
                cislo.Format.Font.Bold = true;
                cislo.Format.Font.Color = farba;
                cislo.Format.SpaceAfter = 0;
                var nazov = r.Cells[1].AddParagraph(predmet.Nazov);
                nazov.Format.Font.Size = 12.5;
                nazov.Format.SpaceAfter = 0;
                nazov.Format.SpaceBefore = Unit.FromPoint(2);
                var strana = r.Cells[2].AddParagraph();
                strana.Format.Alignment = ParagraphAlignment.Right;
                strana.Format.Font.Name = PismaDokumentu.Nadpis;
                strana.Format.Font.Size = 10;
                strana.Format.Font.Color = Siva;
                strana.Format.SpaceBefore = Unit.FromPoint(3);
                strana.AddPageRefField(Zalozka(predmet.Id));
            }
        }

        if (oblast.Charakteristika.Count > 0)
        {
            var c = sekcia.AddParagraph("Charakteristika vzdelávacej oblasti");
            c.Style = StylN1;
            c.Format.Font.Color = farba;
            c.Format.PageBreakBefore = oddelovac && oblast.Predmety.Count > 0;
            Bloky(sekcia, oblast.Charakteristika, farba, 0);
        }
    }

    /// <summary>Oddeľujúca strana pred predmetom: veľké číslo poradia a názov predmetu.</summary>
    private static void OddelovacPredmetu(Section sekcia, KapitolaOblasti oblast, KapitolaPredmetu predmet)
    {
        var farba = Farba(oblast.Farba);
        Medzera(sekcia, 7, novaStrana: true);
        var cislo = sekcia.AddParagraph(predmet.Cislo);
        cislo.Format.Font.Name = PismaDokumentu.Nadpis;
        cislo.Format.Font.Size = 72;
        cislo.Format.Font.Bold = true;
        cislo.Format.Font.Color = farba;
        cislo.Format.LineSpacingRule = LineSpacingRule.Single;
        cislo.Format.SpaceAfter = Unit.FromPoint(4);

        var nazov = sekcia.AddParagraph(predmet.Nazov);
        nazov.Format.Font.Name = PismaDokumentu.Nadpis;
        nazov.Format.Font.Size = 30;
        nazov.Format.Font.Bold = true;
        nazov.Format.LineSpacingRule = LineSpacingRule.Multiple;
        nazov.Format.LineSpacing = 1.0;
        nazov.Format.SpaceAfter = Unit.FromPoint(12);
        nazov.Format.Borders.Bottom.Width = Unit.FromPoint(3);
        nazov.Format.Borders.Bottom.Color = farba;
        nazov.Format.Borders.DistanceFromBottom = Unit.FromPoint(10);

        var o = sekcia.AddParagraph(oblast.Nazov);
        o.Format.Font.Name = PismaDokumentu.Nadpis;
        o.Format.Font.Size = 12;
        o.Format.Font.Color = Siva;
        o.Format.SpaceBefore = Unit.FromPoint(8);
    }

    private static void Predmet(Section sekcia, KapitolaOblasti oblast, KapitolaPredmetu predmet, bool novaStrana)
    {
        var farba = Farba(oblast.Farba);
        var oblastText = sekcia.AddParagraph($"{oblast.Nazov}  •  {predmet.Cislo}");
        oblastText.Style = StylOblastMala;
        oblastText.Format.PageBreakBefore = novaStrana;
        oblastText.Format.Font.Color = farba;

        var nadpis = sekcia.AddParagraph();
        nadpis.Style = StylPredmet;
        nadpis.Format.Borders.Bottom.Width = Unit.FromPoint(1.5);
        nadpis.Format.Borders.Bottom.Color = farba;
        nadpis.Format.Borders.DistanceFromBottom = Unit.FromPoint(6);
        nadpis.AddBookmark(Zalozka(predmet.Id));
        nadpis.AddText(predmet.Nazov);

        if (predmet.Obsah.Count == 0)
        {
            var p = sekcia.AddParagraph("Učebná osnova predmetu zatiaľ nie je spracovaná.");
            p.Style = StylPoznamka;
            return;
        }

        Bloky(sekcia, predmet.Obsah, farba, 0);
    }

    // ---- bloky textu ----

    private interface ICiel
    {
        Paragraph NovyOdsek();
        Table NovaTabulka();
    }

    private sealed class CielSekcia(Section sekcia) : ICiel
    {
        public Paragraph NovyOdsek() => sekcia.AddParagraph();
        public Table NovaTabulka() => sekcia.AddTable();
    }

    private sealed class CielBunka(Cell bunka) : ICiel
    {
        public Paragraph NovyOdsek()
        {
            var p = bunka.AddParagraph();
            p.Style = StylTabulka;
            return p;
        }

        public Table NovaTabulka() => throw new NotSupportedException();
    }

    private static void Bloky(Section sekcia, IReadOnlyList<Blok> bloky, MdColor farba, int uroven) =>
        Bloky(new CielSekcia(sekcia), bloky, farba, uroven, false);

    private static void Bloky(ICiel ciel, IReadOnlyList<Blok> bloky, MdColor farba, int uroven, bool vBunke)
    {
        foreach (var blok in bloky)
        {
            switch (blok)
            {
                case Nadpis n:
                    var p = ciel.NovyOdsek();
                    p.Style = n.Uroven switch { 1 => StylN1, 2 => StylN2, _ => StylN3 };
                    if (n.Uroven == 1)
                    {
                        p.Format.Font.Color = farba;
                    }

                    p.AddText(n.Text);
                    break;

                case Odsek o:
                    var odsek = ciel.NovyOdsek();
                    Text(odsek, o.Useky);
                    if (o.Odsadenie > 0)
                    {
                        odsek.Format.LeftIndent = Unit.FromCentimeter(0.6 * o.Odsadenie);
                    }

                    odsek.Format.Alignment = o.Zarovnanie switch
                    {
                        WinAlign.Center => ParagraphAlignment.Center,
                        WinAlign.Right => ParagraphAlignment.Right,
                        WinAlign.Justify => ParagraphAlignment.Justify,
                        _ => ParagraphAlignment.Left,
                    };
                    break;

                case Zoznam z:
                    for (var i = 0; i < z.Polozky.Count; i++)
                    {
                        var prvy = true;
                        foreach (var vnutorny in z.Polozky[i])
                        {
                            if (prvy && vnutorny is Odsek polozka)
                            {
                                var li = ciel.NovyOdsek();
                                Text(li, polozka.Useky);
                                var odsadenie = Unit.FromCentimeter(0.65 * (uroven + 1));
                                li.Format.LeftIndent = odsadenie;
                                li.Format.FirstLineIndent = Unit.FromCentimeter(-0.5);
                                li.Format.SpaceAfter = Unit.FromPoint(2.5);
                                li.Format.ListInfo = new ListInfo
                                {
                                    ListType = (z.Cislovany, uroven) switch
                                    {
                                        (true, 0) => ListType.NumberList1,
                                        (true, 1) => ListType.NumberList2,
                                        (true, _) => ListType.NumberList3,
                                        (false, 0) => ListType.BulletList1,
                                        (false, 1) => ListType.BulletList2,
                                        _ => ListType.BulletList3,
                                    },
                                    ContinuePreviousList = i > 0,
                                    NumberPosition = Unit.FromCentimeter(0.65 * uroven + 0.15),
                                };
                                prvy = false;
                            }
                            else if (vnutorny is Zoznam vnoreny)
                            {
                                Bloky(ciel, new Blok[] { vnoreny }, farba, uroven + 1, vBunke);
                            }
                            else
                            {
                                var pred = ciel.NovyOdsek();
                                if (vnutorny is Odsek dalsi)
                                {
                                    Text(pred, dalsi.Useky);
                                }

                                pred.Format.LeftIndent = Unit.FromCentimeter(0.65 * (uroven + 1));
                                prvy = false;
                            }
                        }
                    }

                    break;

                case Tabulka t when !vBunke:
                    Tabulka(ciel.NovaTabulka(), t, farba);
                    ciel.NovyOdsek().Format.SpaceAfter = Unit.FromPoint(2);
                    break;
            }
        }
    }

    private static void Tabulka(Table tabulka, Tabulka t, MdColor farba)
    {
        tabulka.Borders.Width = Unit.FromPoint(0.5);
        tabulka.Borders.Color = Linka;
        tabulka.LeftPadding = Unit.FromPoint(4);
        tabulka.RightPadding = Unit.FromPoint(4);
        tabulka.TopPadding = Unit.FromPoint(3);
        tabulka.BottomPadding = Unit.FromPoint(2);
        foreach (var sirka in t.PomerneSirky)
        {
            tabulka.AddColumn(Unit.FromPoint(SirkaTextu.Point * sirka));
        }

        var riadky = new List<Row>();
        for (var r = 0; r < t.Riadky.Count; r++)
        {
            riadky.Add(tabulka.AddRow());
        }

        // obsadené políčka mriežky (zlúčené bunky z predchádzajúcich riadkov)
        var obsadene = new HashSet<(int, int)>();
        for (var r = 0; r < t.Riadky.Count; r++)
        {
            var riadok = riadky[r];
            if (r == 0 && t.Riadky[0].Any(b => b.Hlavicka))
            {
                riadok.HeadingFormat = t.Riadky.Count > 2;
            }

            var stlpec = 0;
            foreach (var b in t.Riadky[r])
            {
                while (stlpec < t.PocetStlpcov && obsadene.Contains((r, stlpec)))
                {
                    stlpec++;
                }

                if (stlpec >= t.PocetStlpcov)
                {
                    break;
                }

                var stlpce = Math.Clamp(b.Stlpce, 1, t.PocetStlpcov - stlpec);
                var vyska = Math.Clamp(b.Riadky, 1, t.Riadky.Count - r);
                var bunka = riadok.Cells[stlpec];
                if (stlpce > 1)
                {
                    bunka.MergeRight = stlpce - 1;
                }

                if (vyska > 1)
                {
                    bunka.MergeDown = vyska - 1;
                }

                for (var d = 0; d < vyska; d++)
                {
                    for (var s = 0; s < stlpce; s++)
                    {
                        obsadene.Add((r + d, stlpec + s));
                    }
                }

                if (b.Hlavicka)
                {
                    bunka.Shading.Color = Podklad;
                }

                Bloky(new CielBunka(bunka), b.Bloky, farba, 0, true);
                stlpec += stlpce;
            }
        }
    }

    private static void Text(Paragraph odsek, IReadOnlyList<Usek> useky)
    {
        foreach (var u in useky)
        {
            if (u.NovyRiadok)
            {
                odsek.AddLineBreak();
                continue;
            }

            var format = TextFormat.NotBold;
            if (u.Tucne)
            {
                format = TextFormat.Bold;
            }

            if (u.Kurziva)
            {
                format |= TextFormat.Italic;
            }

            if (u.Podciarknute)
            {
                format |= TextFormat.Underline;
            }

            if (format == TextFormat.NotBold)
            {
                odsek.AddText(u.Text);
            }
            else
            {
                odsek.AddFormattedText(u.Text, format);
            }
        }
    }

    // ---- pomocné ----

    /// <summary>Prázdny odsek pevnej výšky – posunie obsah nižšie aj na začiatku strany.</summary>
    private static void Medzera(Section sekcia, double cm, bool novaStrana)
    {
        var medzera = sekcia.AddParagraph("\u00A0");
        medzera.Format.PageBreakBefore = novaStrana;
        medzera.Format.LineSpacingRule = LineSpacingRule.Exactly;
        medzera.Format.LineSpacing = Unit.FromCentimeter(cm);
        medzera.Format.SpaceAfter = 0;
        medzera.Format.SpaceBefore = 0;
    }

    public static string PocetPredmetov(int pocet) => pocet switch
    {
        1 => "1 predmet",
        >= 2 and <= 4 => $"{pocet} predmety",
        _ => $"{pocet} predmetov",
    };

    private static string Zalozka(long id) => "obj" + id.ToString(CultureInfo.InvariantCulture);

    public static MdColor Farba(string hex)
    {
        hex = (hex ?? "").TrimStart('#');
        if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hodnota))
        {
            return new MdColor((byte)(hodnota >> 16), (byte)(hodnota >> 8), (byte)hodnota);
        }

        return new MdColor(0x3B, 0x5B, 0xDB);
    }
}
