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
    private const string StylTabulkaSvetla = "SkvpTabulkaSvetla";

    private static readonly MdColor Atrament = Farba("#1F2933");
    private static readonly MdColor Siva = Farba("#6B7280");
    private static readonly MdColor Linka = Farba("#CBD2D9");

    // okraje strán zostavy (cm)
    private const double OkrajVlavoCm = 2.4;
    private const double OkrajVpravoCm = 2.2;

    /// <summary>Šírka textu strany A4 na výšku (úvodná strana, obsah, oblasti).</summary>
    private static readonly Unit SirkaTextu = SirkaTextuStrany(RozlozenieStrany.A4NaVysku);

    private static Unit SirkaTextuStrany(RozlozenieStrany r) => Unit.FromCentimeter(r.SirkaMm / 10 - OkrajVlavoCm - OkrajVpravoCm);

    /// <summary>
    /// Sadzba celého dokumentu po častiach (sekciách MigraDoc) s rôznym rozložením strany – napr. osnova na šírku medzi
    /// osnovami na výšku. Nová časť má vlastnú hlavičku a pätu pre svoju šírku a začína novou stranou.
    /// </summary>
    private sealed class Sadzba(Document doc, CelyDokument dokument)
    {
        public Section Sekcia { get; private set; } = null!;

        public RozlozenieStrany Rozlozenie { get; private set; } = RozlozenieStrany.A4NaVysku;

        public Unit SirkaTextu => SirkaTextuStrany(Rozlozenie);

        public void Zacni(RozlozenieStrany rozlozenie)
        {
            Rozlozenie = rozlozenie;
            Sekcia = doc.AddSection();
            NastavStranu(Sekcia, rozlozenie);
            HlavickaAPata(Sekcia, dokument, SirkaTextu);
        }

        /// <summary>Prepne rozloženie strany. True = začala sa nová časť (a tým aj nová strana).</summary>
        public bool Prepni(RozlozenieStrany rozlozenie)
        {
            if (rozlozenie.Zhodne(Rozlozenie))
            {
                return false;
            }

            Zacni(rozlozenie);
            return true;
        }
    }

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

    /// <summary>
    /// Ktoré osnovy a charakteristiky sa nedajú vysádzať (keď zlyhá celý dokument): každá sa skúsi vysádzať samostatne.
    /// Vráti napr. „02 Jazykom k maturite (Jazyk a komunikácia)“ a technickú príčinu.
    /// </summary>
    public static List<(string Nazov, Exception Chyba)> ChybneCasti(CelyDokument dokument)
    {
        var chybne = new List<(string, Exception)>();
        var volby = dokument.Volby with { TitulnaStrana = false, Obsah = false };
        void Skus(string nazov, KapitolaOblasti oblast)
        {
            try
            {
                new MigraDoc.Rendering.DocumentRenderer(Vytvor(dokument with { Volby = volby, Oblasti = new List<KapitolaOblasti> { oblast } })).PrepareDocument();
            }
            catch (Exception ex)
            {
                chybne.Add((nazov, ex));
            }
        }

        foreach (var oblast in dokument.Oblasti)
        {
            if (oblast.Charakteristika.Count > 0)
            {
                Skus($"charakteristika – {oblast.Nazov}", oblast with { Predmety = new List<KapitolaPredmetu>() });
            }

            foreach (var predmet in oblast.Predmety)
            {
                Skus($"{predmet.Cislo} {predmet.Nazov} ({oblast.Nazov})",
                    oblast with { Charakteristika = new List<Blok>(), Predmety = new List<KapitolaPredmetu> { predmet } });
            }
        }

        return chybne;
    }

    public static Document Vytvor(CelyDokument dokument)
    {
        var doc = new Document();
        doc.Info.Title = dokument.Titul.Hlavicka;
        doc.Info.Subject = dokument.Titul.Podnadpis;
        doc.Info.Author = dokument.Titul.AutorSuboru ?? "";
        Styly(doc);

        if (dokument.Volby.TitulnaStrana)
        {
            TitulnaStrana(doc, dokument, StupenZhustenia(dokument));
        }

        var sadzba = new Sadzba(doc, dokument);
        sadzba.Zacni(RozlozenieStrany.A4NaVysku);

        var prvy = true;
        if (dokument.Volby.Obsah)
        {
            ObsahDokumentu(sadzba.Sekcia, dokument);
            prvy = false;
        }

        var volby = dokument.Volby;
        foreach (var oblast in dokument.Oblasti)
        {
            // oblasť je vždy na výšku; nová časť sama začína novou stranou
            var prepnute = sadzba.Prepni(RozlozenieStrany.A4NaVysku);
            Oblast(sadzba, oblast, !prvy && !prepnute, volby.OddelovacOblasti);
            prvy = false;
            for (var i = 0; i < oblast.Predmety.Count; i++)
            {
                var predmet = oblast.Predmety[i];
                var obsah = DokumentOsnovy.Upratane(predmet.Obsah);

                // predmet (aj s oddeľujúcou stranou) má rozloženie strany svojej osnovy
                prepnute = sadzba.Prepni(DokumentOsnovy.Rozlozenie(obsah));
                if (volby.OddelovacPredmetu)
                {
                    OddelovacPredmetu(sadzba.Sekcia, oblast, predmet, !prepnute);
                    prepnute = false;
                }

                // bez oddeľujúcej strany oblasti prvý predmet pokračuje hneď pod nadpisom oblasti
                var novaStrana = !prepnute && (volby.OddelovacPredmetu || volby.OddelovacOblasti || i > 0 || oblast.Charakteristika.Count > 0);
                Predmet(sadzba, oblast, predmet, obsah, novaStrana);
            }
        }

        if (dokument.Oblasti.Count == 0)
        {
            var p = sadzba.Sekcia.AddParagraph("Dokument zatiaľ neobsahuje žiadne vzdelávacie oblasti.");
            p.Style = StylPoznamka;
        }

        return doc;
    }

    private static void Styly(Document doc)
    {
        var sablona = VzhladDokumentov.Aktivna;
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = sablona.PismoTextu;
        normal.Font.Size = sablona.VelkostTextu;
        normal.Font.Color = Farba(sablona.FarbaTextu);
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(sablona.MedzeraZaOdsekom);
        normal.ParagraphFormat.LineSpacingRule = LineSpacingRule.Multiple;
        normal.ParagraphFormat.LineSpacing = sablona.Riadkovanie;
        normal.ParagraphFormat.Alignment = sablona.DoBloku ? ParagraphAlignment.Justify : ParagraphAlignment.Left;

        var oblast = doc.Styles[StyleNames.Heading1]!;
        oblast.Font.Name = PismaDokumentu.Nadpis;
        oblast.Font.Size = 26;
        oblast.Font.Bold = true;
        oblast.ParagraphFormat.SpaceBefore = 0;
        oblast.ParagraphFormat.SpaceAfter = Unit.FromPoint(10);
        oblast.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        oblast.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        oblast.ParagraphFormat.KeepWithNext = true;
        oblast.ParagraphFormat.OutlineLevel = OutlineLevel.Level1;

        var predmet = doc.Styles.AddStyle(StylPredmet, StyleNames.Normal);
        predmet.Font.Name = PismaDokumentu.Nadpis;
        predmet.Font.Size = 19;
        predmet.Font.Bold = true;
        predmet.ParagraphFormat.SpaceAfter = Unit.FromPoint(14);
        predmet.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        predmet.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        predmet.ParagraphFormat.KeepWithNext = true;
        predmet.ParagraphFormat.OutlineLevel = OutlineLevel.Level2;

        var oblastMala = doc.Styles.AddStyle(StylOblastMala, StyleNames.Normal);
        oblastMala.Font.Name = PismaDokumentu.Nadpis;
        oblastMala.Font.Size = 9.5;
        oblastMala.Font.Bold = true;
        oblastMala.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        oblastMala.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        oblastMala.ParagraphFormat.KeepWithNext = true;

        var n1 = doc.Styles.AddStyle(StylN1, StyleNames.Normal);
        n1.Font.Name = sablona.PismoNadpisov;
        n1.Font.Size = sablona.Nadpis1;
        n1.Font.Bold = true;
        n1.ParagraphFormat.SpaceBefore = Unit.FromPoint(sablona.Nadpis1 * 1.05);
        n1.ParagraphFormat.SpaceAfter = Unit.FromPoint(6);
        n1.ParagraphFormat.LineSpacingRule = LineSpacingRule.Single;
        n1.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        n1.ParagraphFormat.KeepWithNext = true;
        n1.ParagraphFormat.OutlineLevel = OutlineLevel.Level3;

        var n2 = doc.Styles.AddStyle(StylN2, StylN1);
        n2.Font.Size = sablona.Nadpis2;
        n2.ParagraphFormat.SpaceBefore = Unit.FromPoint(sablona.Nadpis2 * 0.85);
        n2.ParagraphFormat.SpaceAfter = Unit.FromPoint(4);
        n2.ParagraphFormat.OutlineLevel = OutlineLevel.Level4;

        var n3 = doc.Styles.AddStyle(StylN3, StylN1);
        n3.Font.Size = sablona.Nadpis3;
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
        obsah1.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        obsah1.ParagraphFormat.TabStops.AddTabStop(SirkaTextu, TabAlignment.Right, TabLeader.Dots);

        var obsah2 = doc.Styles.AddStyle(StylObsah2, StyleNames.Normal);
        obsah2.Font.Size = 10.5;
        obsah2.ParagraphFormat.LeftIndent = Unit.FromCentimeter(0.7);
        obsah2.ParagraphFormat.SpaceAfter = Unit.FromPoint(1.5);
        obsah2.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        obsah2.ParagraphFormat.TabStops.AddTabStop(SirkaTextu, TabAlignment.Right, TabLeader.Dots);

        var poznamka = doc.Styles.AddStyle(StylPoznamka, StyleNames.Normal);
        poznamka.Font.Italic = true;
        poznamka.Font.Color = Siva;

        var tabulka = doc.Styles.AddStyle(StylTabulka, StyleNames.Normal);
        tabulka.Font.Size = Math.Round(sablona.VelkostTextu * 0.91, 1);
        tabulka.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        tabulka.ParagraphFormat.LineSpacing = Math.Max(1.05, sablona.Riadkovanie - 0.08);
        tabulka.ParagraphFormat.Alignment = ParagraphAlignment.Left;

        // text v tmavej hlavičke tabuľky
        var svetla = doc.Styles.AddStyle(StylTabulkaSvetla, StylTabulka);
        svetla.Font.Color = Farba(sablona.TextHlavicky ?? sablona.FarbaTextu);

        var hlavicka = doc.Styles[StyleNames.Header]!;
        hlavicka.Font.Name = PismaDokumentu.Nadpis;
        hlavicka.Font.Size = 8.5;
        hlavicka.Font.Color = Siva;
        hlavicka.ParagraphFormat.Alignment = ParagraphAlignment.Left;
        hlavicka.ParagraphFormat.TabStops.ClearAll();
        hlavicka.ParagraphFormat.TabStops.AddTabStop(SirkaTextu, TabAlignment.Right);

        var pata = doc.Styles[StyleNames.Footer]!;
        pata.Font.Name = PismaDokumentu.Nadpis;
        pata.Font.Size = 8.5;
        pata.Font.Color = Siva;
        pata.ParagraphFormat.Alignment = ParagraphAlignment.Center;
    }

    private static void NastavStranu(Section sekcia, RozlozenieStrany? rozlozenie = null)
    {
        // MigraDoc má rozmery strany vždy na výšku, strana na šírku je orientácia
        var r = rozlozenie ?? RozlozenieStrany.A4NaVysku;
        sekcia.PageSetup.PageWidth = Unit.FromMillimeter(Math.Min(r.SirkaMm, r.VyskaMm));
        sekcia.PageSetup.PageHeight = Unit.FromMillimeter(Math.Max(r.SirkaMm, r.VyskaMm));
        sekcia.PageSetup.Orientation = r.NaSirku ? Orientation.Landscape : Orientation.Portrait;
        sekcia.PageSetup.TopMargin = Unit.FromCentimeter(2.6);
        sekcia.PageSetup.BottomMargin = Unit.FromCentimeter(2.4);
        sekcia.PageSetup.LeftMargin = Unit.FromCentimeter(OkrajVlavoCm);
        sekcia.PageSetup.RightMargin = Unit.FromCentimeter(OkrajVpravoCm);
        sekcia.PageSetup.HeaderDistance = Unit.FromCentimeter(1.2);
        sekcia.PageSetup.FooterDistance = Unit.FromCentimeter(1.1);
    }

    /// <summary>Najväčší stupeň zhustenia úvodnej strany (0 = bežné rozloženie, 3 = najhustejšie).</summary>
    public const int MaxZhustenie = 3;

    /// <summary>
    /// Ako veľmi treba zhustiť úvodnú stranu, aby sa zmestila na jednu stranu (veľa riadkov, dlhý nadpis, tabuľka dole):
    /// úvodná strana sa skúšobne vysádza od bežného rozloženia po najhustejšie. Rovnaký stupeň použije aj export do Wordu.
    /// </summary>
    public static int StupenZhustenia(CelyDokument dokument)
    {
        for (var stupen = 0; stupen < MaxZhustenie; stupen++)
        {
            try
            {
                var skuska = new Document();
                Styly(skuska);
                TitulnaStrana(skuska, dokument, stupen);
                var vykreslenie = new DocumentRenderer(skuska);
                vykreslenie.PrepareDocument();
                if (vykreslenie.FormattedDocument!.PageCount <= 1)
                {
                    return stupen;
                }
            }
            catch (Exception)
            {
                return 0;
            }
        }

        return MaxZhustenie;
    }

    private static void TitulnaStrana(Document doc, CelyDokument dokument, int z)
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
            odsekLoga.Format.SpaceBefore = Unit.FromPoint(z >= 2 ? 12 : 22);
            odsekLoga.Format.SpaceAfter = 0;
            odsekLoga.Format.LineSpacingRule = LineSpacingRule.Single;
            var logo = odsekLoga.AddImage(cestaLoga);
            logo.LockAspectRatio = true;
            logo.Height = Unit.FromCentimeter(z >= 3 ? 1.8 : 2.6);
        }

        var skola = sekcia.AddParagraph(t.SkolaZobrazit && t.Skola.Length > 0 ? t.Skola : " ");
        skola.Format.Font.Name = PismaDokumentu.Nadpis;
        skola.Format.Font.Size = 13;
        skola.Format.Font.Bold = true;
        skola.Format.SpaceBefore = Unit.FromPoint(18);
        skola.Format.SpaceAfter = Unit.FromPoint(1);
        if (t.ZlozkaNaStrane is { } zlozka)
        {
            var odsekZlozky = sekcia.AddParagraph(zlozka);
            odsekZlozky.Format.Font.Name = PismaDokumentu.Nadpis;
            odsekZlozky.Format.Font.Size = 11;
            odsekZlozky.Format.SpaceAfter = Unit.FromPoint(1);
        }

        if (t.AdresaZobrazit && t.Adresa.Length > 0)
        {
            var adresa = sekcia.AddParagraph(t.Adresa);
            adresa.Format.Font.Name = PismaDokumentu.Nadpis;
            adresa.Format.Font.Size = 10;
            adresa.Format.Font.Color = Siva;
        }

        foreach (var riadokOrganizacie in t.RiadkyPodAdresou())
        {
            var odsek = sekcia.AddParagraph(riadokOrganizacie);
            odsek.Format.Font.Name = PismaDokumentu.Nadpis;
            odsek.Format.Font.Size = 9;
            odsek.Format.Font.Color = Siva;
        }

        // nadpis ostáva na rovnakom mieste, aj keď je vypnutý
        var program = sekcia.AddParagraph(t.NadpisZobrazit && t.Nadpis.Length > 0 ? t.Nadpis : " ");
        program.Format.Font.Name = PismaDokumentu.Nadpis;
        program.Format.Font.Size = z >= 3 ? 28 : 34;
        program.Format.Font.Bold = true;
        program.Format.LineSpacingRule = LineSpacingRule.Multiple;
        program.Format.LineSpacing = 1.0;
        program.Format.SpaceBefore = Unit.FromCentimeter(new[] { 6.5, 3.5, 2.0, 1.2 }[z]);
        program.Format.SpaceAfter = Unit.FromPoint(10);

        if (t.PodnadpisZobrazit && t.Podnadpis.Length > 0)
        {
            var podtitul = sekcia.AddParagraph(t.Podnadpis);
            podtitul.Format.Font.Name = PismaDokumentu.Text;
            podtitul.Format.Font.Size = 24;
            podtitul.Format.Font.Italic = true;
            podtitul.Format.Font.Color = Farba("#3B5BDB");
            podtitul.Format.SpaceAfter = Unit.FromPoint(z >= 2 ? 14 : 26);
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
            Pata(sekcia, t, dokument.Vytvorene, z);
            return;
        }

        // pri zhustení od stupňa 2 sú oblasti v dvoch stĺpcoch
        var stlpce = z >= 2 && dokument.Oblasti.Count > 3 ? 2 : 1;
        var riadkov = (dokument.Oblasti.Count + stlpce - 1) / stlpce;
        var zoznam = sekcia.AddTable();
        zoznam.Borders.Visible = false;
        zoznam.TopPadding = Unit.FromPoint(2.5);
        zoznam.BottomPadding = Unit.FromPoint(2.5);
        for (var s = 0; s < stlpce; s++)
        {
            zoznam.AddColumn(Unit.FromPoint(14));
            zoznam.AddColumn(Unit.FromPoint(SirkaTextu.Point / stlpce - 14));
        }

        for (var r = 0; r < riadkov; r++)
        {
            var riadok = zoznam.AddRow();
            if (r == 0)
            {
                riadok.TopPadding = Unit.FromCentimeter(new[] { 3.0, 1.5, 0.8, 0.6 }[z]);
            }

            for (var s = 0; s < stlpce; s++)
            {
                var i = s * riadkov + r;
                if (i >= dokument.Oblasti.Count)
                {
                    continue;
                }

                var oblast = dokument.Oblasti[i];
                var znacka = riadok.Cells[2 * s].AddParagraph();
                znacka.Format.SpaceAfter = 0;
                var stvorcek = znacka.AddFormattedText("■");
                stvorcek.Font.Name = PismaDokumentu.Nadpis;
                stvorcek.Font.Color = Farba(oblast.Farba);
                var nazov = riadok.Cells[2 * s + 1].AddParagraph($"{oblast.Nazov}   ");
                nazov.Format.Font.Name = PismaDokumentu.Nadpis;
                nazov.Format.Font.Size = 10;
                nazov.Format.SpaceAfter = 0;
                var pocet = nazov.AddFormattedText(oblast.PocetText);
                pocet.Font.Color = Siva;
            }
        }

        Pata(sekcia, t, dokument.Vytvorene, z);
    }

    /// <summary>Text dole na úvodnej strane (napr. „Vytvorené 23. 10. 2026 v programe SkVP“), ak je zapnutý.</summary>
    private static void Pata(Section sekcia, TitulnaStrana t, DateTime vytvorene, int z)
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
            datum.Format.SpaceBefore = Unit.FromCentimeter(new[] { 2.0, 1.2, 0.8, 0.5 }[z]);
            return;
        }

        // ľubovoľný obsah – odseky, zoznamy, tabuľka (napr. schválenie pedagogickou radou s podpismi)
        var medzera = sekcia.AddParagraph();
        medzera.Format.SpaceBefore = Unit.FromCentimeter(new[] { 1.6, 1.0, 0.6, 0.4 }[z]);
        medzera.Format.SpaceAfter = 0;
        Bloky(new CielSekcia(sekcia), bloky, Farba("#3B5BDB"), 0, false);
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

    private static void HlavickaAPata(Section sekcia, CelyDokument dokument, Unit sirkaTextu)
    {
        var t = dokument.Titul;
        var text = t.TextVHlavicke;
        var skola = t.SkolaVHlavicke;
        var logo = t.LogoVHlavicke(dokument.Udaje.Logo);
        if (text is not null || skola is not null || logo is not null)
        {
            Hlavicka(sekcia.Headers.Primary, text, skola, logo, sirkaTextu);
        }

        var pata = sekcia.Footers.Primary.AddParagraph();
        if (t.TextVPate is { } textPaty)
        {
            // vľavo škola / kontakt, vpravo číslo strany
            pata.Format.Alignment = ParagraphAlignment.Left;
            pata.Format.TabStops.ClearAll();
            pata.Format.TabStops.AddTabStop(sirkaTextu, TabAlignment.Right);
            pata.AddText(textPaty);
            pata.AddTab();
        }

        pata.AddText("Strana ");
        pata.AddPageField();
        pata.AddText(" z ");
        pata.AddNumPagesField();
    }

    /// <summary>Hlavička strany: vľavo text, vpravo názov školy a logo, pod tým tenká čiara. Vypnuté časti sa vynechajú.</summary>
    private static void Hlavicka(HeaderFooter hlavicka, string? text, string? skola, string? logo, Unit sirkaTextu)
    {
        var (sirkaLoga, vyskaLoga) = logo is null ? (0.0, 0.0) : RozmerLogaHlavicky(logo);
        var stlpecLoga = logo is null ? 0 : Unit.FromCentimeter(sirkaLoga + (skola is null ? 0 : 0.3)).Point;
        var zvysok = sirkaTextu.Point - stlpecLoga;

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

    private static void Oblast(Sadzba sadzba, KapitolaOblasti oblast, bool novaStrana, bool oddelovac)
    {
        var sekcia = sadzba.Sekcia;
        var farba = Farba(oblast.Farba);
        var strana = UvodnaStranaOblasti.Pre(oblast, VzhladDokumentov.Aktivna, SirkaTextu.Point);
        if (oddelovac)
        {
            // oddeľujúca strana: nadpis oblasti nižšie na strane (medzera hore na strane sa inak vynechá) – len toľko,
            // aby sa zoznam predmetov zmestil na jednu stranu
            Medzera(sekcia, Math.Max(0.05, Unit.FromPoint(strana.MedzeraHore).Centimeter), novaStrana);
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
        nadpis.Format.SpaceBefore = 0;
        if (!oddelovac)
        {
            nadpis.Format.Font.Size = 20;
        }

        nadpis.AddBookmark(Zalozka(oblast.Id));
        nadpis.AddText(oblast.Nazov);

        var popis = sekcia.AddParagraph($"{TypObjektu.Nazov(oblast.Typ)}  •  {oblast.PocetText}");
        popis.Format.Font.Name = PismaDokumentu.Nadpis;
        popis.Format.Font.Size = 10;
        popis.Format.Font.Color = Siva;
        popis.Format.LeftIndent = Unit.FromPoint(18);
        popis.Format.SpaceAfter = Unit.FromPoint(oddelovac ? strana.MedzeraPodPopisom : 14);

        if (oddelovac && oblast.Predmety.Count > 0)
        {
            // zoznam predmetov oblasti s číslom poradia a stranou
            var zoznam = sekcia.AddTable();
            zoznam.Borders.Visible = false;
            zoznam.TopPadding = Unit.FromPoint(strana.Odstup);
            zoznam.BottomPadding = Unit.FromPoint(strana.Odstup);
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
                cislo.Format.Font.Size = strana.Cislo;
                cislo.Format.Font.Bold = true;
                cislo.Format.Font.Color = farba;
                cislo.Format.SpaceAfter = 0;
                cislo.Format.LineSpacingRule = LineSpacingRule.Single;
                var nazov = r.Cells[1].AddParagraph(predmet.Nazov);
                nazov.Format.Font.Size = strana.Nazov;
                nazov.Format.SpaceAfter = 0;
                nazov.Format.LineSpacingRule = LineSpacingRule.Single;
                nazov.Format.SpaceBefore = Unit.FromPoint(Math.Max(0, (strana.Cislo - strana.Nazov) * 0.6));
                var cisloStrany = r.Cells[2].AddParagraph();
                cisloStrany.Format.Alignment = ParagraphAlignment.Right;
                cisloStrany.Format.Font.Name = PismaDokumentu.Nadpis;
                cisloStrany.Format.Font.Size = Math.Min(10, strana.Nazov);
                cisloStrany.Format.Font.Color = Siva;
                cisloStrany.Format.SpaceAfter = 0;
                cisloStrany.Format.LineSpacingRule = LineSpacingRule.Single;
                cisloStrany.Format.SpaceBefore = Unit.FromPoint(Math.Max(0, (strana.Cislo - Math.Min(10, strana.Nazov)) * 0.6));
                cisloStrany.AddPageRefField(Zalozka(predmet.Id));
            }
        }

        if (oblast.Charakteristika.Count > 0)
        {
            var c = sekcia.AddParagraph("Charakteristika vzdelávacej oblasti");
            c.Style = StylN1;
            c.Format.Font.Color = farba;
            c.Format.PageBreakBefore = oddelovac && oblast.Predmety.Count > 0;

            // charakteristika zostáva pod nadpisom oblasti na výšku – rozloženie strany jej dokumentu sa nepoužije
            Bloky(sadzba, DokumentOsnovy.Upratane(oblast.Charakteristika).Where(b => b is not Sekcia).ToList(), farba);
        }
    }

    /// <summary>Oddeľujúca strana pred predmetom: veľké číslo poradia a názov predmetu.</summary>
    private static void OddelovacPredmetu(Section sekcia, KapitolaOblasti oblast, KapitolaPredmetu predmet, bool novaStrana)
    {
        var farba = Farba(oblast.Farba);
        Medzera(sekcia, 7, novaStrana);
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

    private static void Predmet(Sadzba sadzba, KapitolaOblasti oblast, KapitolaPredmetu predmet, IReadOnlyList<Blok> obsah, bool novaStrana)
    {
        var sekcia = sadzba.Sekcia;
        var farba = Farba(oblast.Farba);
        var sablona = VzhladDokumentov.Aktivna;
        var oblastText = sekcia.AddParagraph($"{oblast.Nazov}  •  {predmet.Cislo}");
        oblastText.Style = StylOblastMala;
        oblastText.Format.PageBreakBefore = novaStrana;
        oblastText.Format.Font.Color = Farba(sablona.FarbaZahlavia(oblast.Farba));

        var nadpis = sekcia.AddParagraph();
        nadpis.Style = StylPredmet;
        nadpis.Format.Borders.Bottom.Width = Unit.FromPoint(1.5);
        nadpis.Format.Borders.Bottom.Color = Farba(sablona.FarbaPredmetu(oblast.Farba));
        nadpis.Format.Borders.DistanceFromBottom = Unit.FromPoint(6);
        nadpis.AddBookmark(Zalozka(predmet.Id));
        nadpis.AddText(predmet.Nazov);

        if (!obsah.Any(b => b is not Sekcia))
        {
            var p = sekcia.AddParagraph("Učebná osnova predmetu zatiaľ nie je spracovaná.");
            p.Style = StylPoznamka;
            return;
        }

        // úvodná časť už je nastavená (Prepni pred predmetom), ďalšie časti prepnú rozloženie v texte
        Bloky(sadzba, obsah.Count > 0 && obsah[0] is Sekcia ? obsah.Skip(1).ToList() : obsah, farba);
    }

    // ---- bloky textu ----

    private interface ICiel
    {
        /// <summary>Šírka textu, do ktorého sa sádza (body).</summary>
        double SirkaTextu { get; }

        Paragraph NovyOdsek();
        Table NovaTabulka();
    }

    /// <summary>Text strany – vždy do aktuálnej časti sadzby (blok <see cref="SkVP.Services.Sekcia"/> ju prepne).</summary>
    private sealed class CielSadzba(Sadzba sadzba) : ICiel
    {
        public Sadzba Sadzba => sadzba;
        public double SirkaTextu => sadzba.SirkaTextu.Point;
        public Paragraph NovyOdsek() => sadzba.Sekcia.AddParagraph();
        public Table NovaTabulka() => sadzba.Sekcia.AddTable();
    }

    /// <summary>Text do pevnej časti A4 na výšku (úvodná strana).</summary>
    private sealed class CielSekcia(Section sekcia) : ICiel
    {
        public double SirkaTextu => PdfVystup.SirkaTextu.Point;
        public Paragraph NovyOdsek() => sekcia.AddParagraph();
        public Table NovaTabulka() => sekcia.AddTable();
    }

    private sealed class CielBunka(Cell bunka, string styl = StylTabulka) : ICiel
    {
        public double SirkaTextu => bunka.Column.Width.Point;

        public Paragraph NovyOdsek()
        {
            var p = bunka.AddParagraph();
            p.Style = styl;
            return p;
        }

        public Table NovaTabulka() => throw new NotSupportedException();
    }

    private static void Bloky(Sadzba sadzba, IReadOnlyList<Blok> bloky, MdColor farba) =>
        Bloky(new CielSadzba(sadzba), bloky, farba, 0, false);

    private static void Bloky(ICiel ciel, IReadOnlyList<Blok> bloky, MdColor farba, int uroven, bool vBunke)
    {
        foreach (var blok in bloky)
        {
            switch (blok)
            {
                case Nadpis n:
                    var p = ciel.NovyOdsek();
                    p.Style = n.Uroven switch { 1 => StylN1, 2 => StylN2, _ => StylN3 };
                    var sablona = VzhladDokumentov.Aktivna;
                    if (sablona.FarbaNadpisu(n.Uroven, Hex(farba)) is { } farbaNadpisu)
                    {
                        p.Format.Font.Color = Farba(farbaNadpisu);
                    }

                    if (n.Uroven == 1 && !vBunke)
                    {
                        Ozdob(p.Format, sablona, Hex(farba));
                    }
                    else if (n.Uroven == 2 && !vBunke && sablona.Nadpis2Pasik)
                    {
                        p.Format.Borders.Left.Width = Unit.FromPoint(2.5);
                        p.Format.Borders.Left.Color = Farba(sablona.FarbaOzdoby(Hex(farba)));
                        p.Format.Borders.DistanceFromLeft = Unit.FromPoint(7);
                        p.Format.LeftIndent = Unit.FromPoint(3);
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
                        _ => VzhladDokumentov.Aktivna.DoBloku && !vBunke ? ParagraphAlignment.Justify : ParagraphAlignment.Left,
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
                    Tabulka(ciel.NovaTabulka(), t, farba, ciel.SirkaTextu);
                    ciel.NovyOdsek().Format.SpaceAfter = Unit.FromPoint(2);
                    break;

                case ZlomStrany when ciel is CielSadzba strana:
                    strana.Sadzba.Sekcia.AddPageBreak();
                    break;

                case Sekcia novaCast when ciel is CielSadzba strana:
                    if (!strana.Sadzba.Prepni(novaCast.Rozlozenie))
                    {
                        strana.Sadzba.Sekcia.AddPageBreak();
                    }

                    break;
            }
        }
    }

    private static void Tabulka(Table tabulka, Tabulka t, MdColor farba, double sirkaTextu)
    {
        // zlúčené bunky, ktoré sa krížia, by MigraDoc nevysádzal („GetMinMergedCell“) – mriežka sa najprv upraví
        t = t.Pravidelna();
        var sablona = VzhladDokumentov.Aktivna;
        var oblast = Hex(farba);
        var vodorovne = sablona.Ram == RamTabulky.Vodorovny;
        var akademicky = sablona.Ram == RamTabulky.Akademicky;
        var linka = Farba(sablona.Linka(oblast));
        // akademická tabuľka: čiary len nad prvým a pod posledným riadkom a pod hlavičkou (pri bunkách)
        tabulka.Borders.Width = Unit.FromPoint(akademicky ? 0 : sablona.HrubkaLinky);
        tabulka.Borders.Color = linka;
        if (vodorovne)
        {
            // len vodorovné čiary – zvislé sa skryjú pri všetkých bunkách
            tabulka.Borders.Left.Visible = false;
            tabulka.Borders.Right.Visible = false;
        }

        // rám tabuľky zarovno s okrajmi textu (bez nastaveného odsadenia MigraDoc posunie tabuľku doľava o vnútorný okraj bunky)
        tabulka.Rows.LeftIndent = Unit.FromPoint(0);
        tabulka.LeftPadding = Unit.FromPoint(vodorovne ? 5 : 4);
        tabulka.RightPadding = Unit.FromPoint(vodorovne ? 5 : 4);
        tabulka.TopPadding = Unit.FromPoint(vodorovne ? 4 : 3);
        tabulka.BottomPadding = Unit.FromPoint(vodorovne ? 3 : 2);
        foreach (var sirka in t.PomerneSirky)
        {
            tabulka.AddColumn(Unit.FromPoint(sirkaTextu * t.SirkaVTexte * sirka));
        }

        var riadky = new List<Row>();
        for (var r = 0; r < t.Riadky.Count; r++)
        {
            riadky.Add(tabulka.AddRow());
        }

        var sHlavickou = t.Riadky.Count > 0 && t.Riadky[0].Any(b => b.Hlavicka);
        if (sHlavickou)
        {
            riadky[0].HeadingFormat = t.Riadky.Count > 2;
        }

        var pruh = sablona.PodkladPruhu(oblast);

        foreach (var p in Mriezka(t))
        {
            var bunka = riadky[p.Riadok].Cells[p.Stlpec];
            if (p.Sirka > 1)
            {
                bunka.MergeRight = p.Sirka - 1;
            }

            if (p.Vyska > 1)
            {
                bunka.MergeDown = p.Vyska - 1;
            }

            // rozdelená zlúčená bunka – medzi jej časťami nie je čiara
            if (p.BezHorneho)
            {
                bunka.Borders.Top.Visible = false;
                bunka.Borders.Top.Width = 0;
            }

            if (p.BezDolneho)
            {
                bunka.Borders.Bottom.Visible = false;
                bunka.Borders.Bottom.Width = 0;
            }

            if (akademicky)
            {
                if (p.Riadok == 0 && !p.BezHorneho)
                {
                    bunka.Borders.Top.Width = Unit.FromPoint(sablona.HrubkaLinky);
                    bunka.Borders.Top.Color = linka;
                }

                if (p.Riadok + p.Vyska == t.Riadky.Count && !p.BezDolneho)
                {
                    bunka.Borders.Bottom.Width = Unit.FromPoint(sablona.HrubkaLinky);
                    bunka.Borders.Bottom.Color = linka;
                }
            }

            if (p.Bunka.Hlavicka)
            {
                bunka.Shading.Color = Farba(sablona.PodkladHlavicky(oblast));
                if (sablona.CiaraPodHlavickou && !p.BezDolneho)
                {
                    bunka.Borders.Bottom.Width = Unit.FromPoint(sablona.HrubkaPodHlavickou);
                    bunka.Borders.Bottom.Color = akademicky ? linka : Farba(sablona.FarbaOzdoby(oblast));
                }
            }
            else if (pruh is not null && p.Riadok > 0 && (p.Riadok - (sHlavickou ? 1 : 0)) % 2 == 1)
            {
                bunka.Shading.Color = Farba(pruh);
            }

            Bloky(new CielBunka(bunka, p.Bunka.Hlavicka && sablona.TextHlavicky is not null ? StylTabulkaSvetla : StylTabulka),
                p.Bunka.Bloky, farba, 0, true);
        }
    }

    /// <summary>Bunka tabuľky na svojom mieste v mriežke; bez horného/dolného okraja = časť rozdelenej zlúčenej bunky.</summary>
    internal sealed record Policko(int Riadok, int Stlpec, int Sirka, int Vyska, Bunka Bunka, bool BezHorneho = false, bool BezDolneho = false);

    /// <summary>
    /// Bunky pravidelnej tabuľky (<see cref="SkVP.Services.Tabulka.Pravidelna"/>) v mriežke pre MigraDoc. MigraDoc 6 vysádza
    /// tabuľku, len ak v každom riadku začína bunka bez zvislého zlúčenia alebo je v ňom voľné políčko – inak zlyhá
    /// („GetMinMergedCell: Unexpected problem #1“). Napr. riadok, v ktorom sú všetky bunky zlúčené nadol, alebo riadok
    /// celý pokrytý zlúčenými bunkami. Zvislé zlúčenie sa v takom riadku rozdelí na časti bez čiar medzi nimi –
    /// tabuľka vyzerá rovnako, text zostane v hornej časti.
    /// </summary>
    internal static List<Policko> Mriezka(Tabulka t)
    {
        var policka = new List<Policko>();
        var obsadene = new HashSet<(int, int)>();
        var stlpcov = Math.Max(1, t.PocetStlpcov);
        for (var r = 0; r < t.Riadky.Count; r++)
        {
            var stlpec = 0;
            foreach (var b in t.Riadky[r])
            {
                while (stlpec < stlpcov && obsadene.Contains((r, stlpec)))
                {
                    stlpec++;
                }

                if (stlpec >= stlpcov)
                {
                    break;
                }

                var sirka = Math.Clamp(b.Stlpce, 1, stlpcov - stlpec);
                var vyska = Math.Clamp(b.Riadky, 1, t.Riadky.Count - r);
                for (var d = 0; d < vyska; d++)
                {
                    for (var s = 0; s < sirka; s++)
                    {
                        obsadene.Add((r + d, stlpec + s));
                    }
                }

                policka.Add(new Policko(r, stlpec, sirka, vyska, b));
                stlpec += sirka;
            }
        }

        for (var r = 0; r < t.Riadky.Count; r++)
        {
            var vRiadku = policka.Where(p => p.Riadok <= r && r < p.Riadok + p.Vyska).ToList();
            if (vRiadku.Sum(p => p.Sirka) < stlpcov || vRiadku.Any(p => p.Riadok == r && p.Vyska == 1))
            {
                continue;
            }

            // prvé zlúčenie zľava sa rozdelí: časť nad riadkom, jeden riadok, časť pod ním
            var zlucena = vRiadku.MinBy(p => p.Stlpec)!;
            policka.Remove(zlucena);
            var nad = r > zlucena.Riadok;
            var pod = r + 1 < zlucena.Riadok + zlucena.Vyska;
            var prazdna = zlucena.Bunka with { Bloky = Array.Empty<Blok>(), Riadky = 1 };
            if (nad)
            {
                policka.Add(zlucena with { Vyska = r - zlucena.Riadok, BezDolneho = true });
            }

            policka.Add(zlucena with
            {
                Riadok = r, Vyska = 1, Bunka = nad ? prazdna : zlucena.Bunka,
                BezHorneho = nad || zlucena.BezHorneho, BezDolneho = pod || zlucena.BezDolneho,
            });
            if (pod)
            {
                policka.Add(zlucena with
                {
                    Riadok = r + 1, Vyska = zlucena.Riadok + zlucena.Vyska - r - 1, Bunka = prazdna, BezHorneho = true,
                });
            }
        }

        return policka;
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
    /// <summary>Šírka textu strany A4 na výšku (bez okrajov) v bodoch.</summary>
    internal static double SirkaTextuNaVysku => SirkaTextu.Point;

    /// <summary>Výška textu strany A4 na výšku (bez okrajov) v bodoch.</summary>
    internal static double VyskaTextu => Unit.FromMillimeter(297).Point - Unit.FromCentimeter(2.6).Point - Unit.FromCentimeter(2.4).Point;

    private static void Medzera(Section sekcia, double cm, bool novaStrana)
    {
        var medzera = sekcia.AddParagraph("\u00A0");
        medzera.Format.PageBreakBefore = novaStrana;
        medzera.Format.LineSpacingRule = LineSpacingRule.Exactly;
        medzera.Format.LineSpacing = Unit.FromCentimeter(cm);
        medzera.Format.SpaceAfter = 0;
        medzera.Format.SpaceBefore = 0;
    }

    public static string PocetPredmetov(int pocet) => Preklad.Pocet(pocet, "{0} predmet", "{0} predmety", "{0} predmetov");

    private static string Zalozka(long id) => "obj" + id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Farba MigraDoc → #RRGGBB.</summary>
    private static string Hex(MdColor c) => string.Create(CultureInfo.InvariantCulture, $"#{c.R:X2}{c.G:X2}{c.B:X2}");

    /// <summary>Ozdoba nadpisu časti osnovy podľa šablóny vzhľadu (čiara, pásik vľavo, farebný pás).</summary>
    private static void Ozdob(ParagraphFormat format, SablonaVzhladu sablona, string oblast)
    {
        var farba = Farba(sablona.FarbaOzdoby(oblast));
        switch (sablona.Ozdoba)
        {
            case OzdobaNadpisu.Ciara:
                format.Borders.Bottom.Width = Unit.FromPoint(1.2);
                format.Borders.Bottom.Color = farba;
                format.Borders.DistanceFromBottom = Unit.FromPoint(4);
                format.SpaceAfter = Unit.FromPoint(9);
                break;

            case OzdobaNadpisu.Pasik:
                format.Borders.Left.Width = Unit.FromPoint(4);
                format.Borders.Left.Color = farba;
                format.Borders.DistanceFromLeft = Unit.FromPoint(9);
                format.LeftIndent = Unit.FromPoint(4);
                break;

            case OzdobaNadpisu.Pas or OzdobaNadpisu.Karta:
                // karta: pásik vľavo v rovnakej farbe ako podklad – súvislá farebná plocha
                format.Shading.Color = Farba(sablona.PodkladNadpisu(oblast)!);
                format.Borders.Left.Width = Unit.FromPoint(4);
                format.Borders.Left.Color = farba;
                format.Borders.DistanceFromLeft = Unit.FromPoint(9);
                format.Borders.DistanceFromTop = Unit.FromPoint(4);
                format.Borders.DistanceFromBottom = Unit.FromPoint(4);
                format.LeftIndent = Unit.FromPoint(4);
                format.SpaceAfter = Unit.FromPoint(10);
                break;

            case OzdobaNadpisu.Nadciarka:
                format.Borders.Top.Width = Unit.FromPoint(2.5);
                format.Borders.Top.Color = farba;
                format.Borders.DistanceFromTop = Unit.FromPoint(6);
                format.SpaceBefore = Unit.FromPoint(sablona.Nadpis1 * 1.25);
                format.SpaceAfter = Unit.FromPoint(8);
                break;

            case OzdobaNadpisu.Ramik:
                format.Borders.Width = Unit.FromPoint(1);
                format.Borders.Color = farba;
                format.Borders.Distance = Unit.FromPoint(5);
                format.LeftIndent = Unit.FromPoint(6);
                format.RightIndent = Unit.FromPoint(6);
                format.SpaceAfter = Unit.FromPoint(10);
                break;
        }
    }

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
