using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MdColor = MigraDoc.DocumentObjectModel.Color;

namespace SkVP.Services;

/// <summary>Deň do tlačovej zostavy kalendára (bez väzby na obrazovku).</summary>
public sealed record DenZostavy(DateTime Datum, bool Volno, bool Vikend, string? Sviatok, string? Prazdniny, string Meniny,
    IReadOnlyList<VyskytVKalendari> Vyskyty, IReadOnlyList<PoznamkaRiadok> Poznamky)
{
    public bool BezVyucovania => Volno || Vikend || Prazdniny is not null;
}

/// <summary>
/// Tlačové zostavy kalendára, úloh a poznámok (MigraDoc → tlač, PDF): mesiac na A4 na šírku, celý školský rok
/// na jednej strane (riadky = dni, stĺpce = mesiace september – august), zoznam za obdobie, úlohy a poznámky.
/// Farby sú zosvetlené ako na obrazovke, aby sa dalo dobre čítať aj pri čiernobielej tlači.
/// </summary>
public static class ZostavaPlanovania
{
    private static readonly MdColor Siva = new(0x6B, 0x72, 0x80);
    private static readonly MdColor Ciara = new(0xD5, 0xD9, 0xE0);
    private static readonly MdColor Vikend = new(0xF3, 0xF4, 0xF6);
    private static readonly MdColor Zelena = Svetla(Planovanie.FarbaPrazdnin, 0.72);
    private const string FarbaSviatku = "#E07A7A";
    private const string FarbaPoznamky = "#B8A44C";
    private static readonly MdColor Cervena = Tmava(FarbaSviatku);

    /// <summary>Farba zmiešaná s bielou (podiel bielej 0 – 1).</summary>
    private static MdColor Svetla(string hex, double biela)
    {
        var f = PdfVystup.Farba(hex);
        byte Mix(uint c) => (byte)Math.Round(c + (255 - c) * biela);
        return new MdColor(Mix(f.R), Mix(f.G), Mix(f.B));
    }

    /// <summary>Tmavší odtieň farby – čitateľný text.</summary>
    private static MdColor Tmava(string hex)
    {
        var f = PdfVystup.Farba(hex);
        byte Tmav(uint c) => (byte)Math.Round(c * 0.55);
        return new MdColor(Tmav(f.R), Tmav(f.G), Tmav(f.B));
    }

    private static Document Novy(string nazov, bool naSirku, out Section sekcia)
    {
        var doc = new Document();
        doc.Info.Title = nazov;
        var normal = doc.Styles[StyleNames.Normal]!;
        normal.Font.Name = PismaDokumentu.Nadpis;
        normal.Font.Size = 9;
        sekcia = doc.AddSection();
        sekcia.PageSetup.PageFormat = PageFormat.A4;
        sekcia.PageSetup.Orientation = naSirku ? Orientation.Landscape : Orientation.Portrait;
        sekcia.PageSetup.TopMargin = Unit.FromCentimeter(1.4);
        sekcia.PageSetup.BottomMargin = Unit.FromCentimeter(1.3);
        sekcia.PageSetup.LeftMargin = Unit.FromCentimeter(1.4);
        sekcia.PageSetup.RightMargin = Unit.FromCentimeter(1.4);

        var titul = TitulnaStrana.Nacitaj();
        doc.Info.Author = titul.AutorSuboru ?? "";
        var nadpis = sekcia.AddParagraph(nazov);
        nadpis.Format.Font.Size = 17;
        nadpis.Format.Font.Bold = true;
        var skola = string.Join("  ·  ", titul.UdajeOSkole().Take(1));
        var podnadpis = sekcia.AddParagraph(string.Join("  ·  ", new[] { skola, $"vytlačené {Planovanie.DatumText(DateTime.Today)}" }.Where(t => t.Length > 0)));
        podnadpis.Format.Font.Color = Siva;
        podnadpis.Format.SpaceAfter = Unit.FromPoint(8);

        var pata = sekcia.Footers.Primary.AddParagraph();
        pata.Format.Alignment = ParagraphAlignment.Right;
        pata.Format.Font.Size = 8;
        pata.Format.Font.Color = Siva;
        pata.AddText("SkVP  ·  strana ");
        pata.AddPageField();
        pata.AddText(" z ");
        pata.AddNumPagesField();
        return doc;
    }

    private static Table Tabulka(Section sekcia)
    {
        var tabulka = sekcia.AddTable();
        tabulka.Borders.Color = Ciara;
        tabulka.Borders.Width = 0.5;
        tabulka.TopPadding = Unit.FromPoint(2);
        tabulka.BottomPadding = Unit.FromPoint(2);
        tabulka.LeftPadding = Unit.FromPoint(3);
        tabulka.RightPadding = Unit.FromPoint(3);
        return tabulka;
    }

    private static void Zaznam(Paragraph odsek, VyskytVKalendari v, double velkost)
    {
        var text = odsek.AddFormattedText((v.JeUloha ? (v.JeVybavena ? "[x] " : "[ ] ") : "") + (v.CelyDen || v.ViacDni ? v.Nazov : $"{v.Zaciatok:H:mm} {v.Nazov}"));
        text.Font.Size = velkost;
        text.Font.Color = Tmava(v.Farba);
        text.Font.Bold = v.JeVlastny;
    }

    // ------------------------------------------------------------------ mesiac

    /// <summary>Mesiac ako mriežka 7 × 5/6 týždňov (A4 na šírku).</summary>
    public static Document Mesiac(DateTime mesiac, IReadOnlyList<DenZostavy> dni)
    {
        var prvy = new DateTime(mesiac.Year, mesiac.Month, 1);
        var doc = Novy("Kalendár – " + Preklad.VelkePrve(prvy.ToString("MMMM yyyy", Planovanie.Sk)), naSirku: true, out var sekcia);
        var tabulka = Tabulka(sekcia);
        var sirka = (29.7 - 2.8) / 7;
        for (var i = 0; i < 7; i++)
        {
            tabulka.AddColumn(Unit.FromCentimeter(sirka));
        }

        var hlavicka = tabulka.AddRow();
        hlavicka.HeadingFormat = true;
        hlavicka.Format.Font.Bold = true;
        hlavicka.Shading.Color = Vikend;
        string[] nazvy = { "pondelok", "utorok", "streda", "štvrtok", "piatok", "sobota", "nedeľa" };
        for (var i = 0; i < 7; i++)
        {
            hlavicka.Cells[i].AddParagraph(nazvy[i]);
        }

        var podlaDna = dni.ToDictionary(d => d.Datum.Date);
        var den = Planovanie.Pondelok(prvy);
        var tyzdnov = (int)Math.Ceiling((Planovanie.DenVTyzdni(prvy) + DateTime.DaysInMonth(prvy.Year, prvy.Month)) / 7.0);
        var vyska = Unit.FromCentimeter((21.0 - 2.7 - 2.6) / tyzdnov);
        for (var t = 0; t < tyzdnov; t++)
        {
            var riadok = tabulka.AddRow();
            riadok.Height = vyska;
            riadok.HeightRule = RowHeightRule.AtLeast;
            for (var i = 0; i < 7; i++, den = den.AddDays(1))
            {
                var bunka = riadok.Cells[i];
                var cislo = bunka.AddParagraph(den.Day.ToString(Planovanie.Sk));
                cislo.Format.Font.Bold = true;
                cislo.Format.Font.Size = 11;
                if (den.Month != prvy.Month)
                {
                    cislo.Format.Font.Color = Ciara;
                    continue;
                }

                if (!podlaDna.TryGetValue(den, out var d))
                {
                    continue;
                }

                if (d.Prazdniny is not null)
                {
                    bunka.Shading.Color = Zelena;
                }
                else if (d.Volno || d.Vikend)
                {
                    bunka.Shading.Color = Vikend;
                }

                if (d.Volno || d.Vikend)
                {
                    cislo.Format.Font.Color = Cervena;
                }

                if (d.Meniny.Length > 0)
                {
                    var meno = cislo.AddFormattedText("   " + d.Meniny);
                    meno.Font.Size = 7;
                    meno.Font.Bold = false;
                    meno.Font.Color = Siva;
                }

                foreach (var stitok in new[] { d.Sviatok, d.Prazdniny }.OfType<string>())
                {
                    var p = bunka.AddParagraph(stitok);
                    p.Format.Font.Size = 7;
                    p.Format.Font.Color = stitok == d.Sviatok ? Cervena : Tmava(Planovanie.FarbaPrazdnin);
                }

                foreach (var v in d.Vyskyty.Where(v => v.Zdroj != ZdrojKalendara.Prazdniny))
                {
                    var p = bunka.AddParagraph();
                    p.Format.SpaceBefore = Unit.FromPoint(1);
                    Zaznam(p, v, 7.5);
                }
            }
        }

        return doc;
    }

    // ------------------------------------------------------------------ školský rok

    /// <summary>Celý školský rok na jednej strane A4 na šírku: riadky 1 – 31, stĺpce september – august.</summary>
    public static Document SkolskyRok(string skolskyRok, IReadOnlyList<DenZostavy> dni)
    {
        var doc = Novy($"Školský rok {skolskyRok}", naSirku: true, out var sekcia);
        sekcia.PageSetup.TopMargin = Unit.FromCentimeter(1.0);
        var (od, _) = ArchivSkolskehoRoka.Obdobie(skolskyRok);
        var tabulka = Tabulka(sekcia);
        tabulka.TopPadding = 0.6;
        tabulka.BottomPadding = 0.6;
        tabulka.Format.Font.Size = 6.5;
        tabulka.AddColumn(Unit.FromCentimeter(0.6));
        var sirka = (29.7 - 2.8 - 0.6) / 12;
        for (var i = 0; i < 12; i++)
        {
            tabulka.AddColumn(Unit.FromCentimeter(sirka));
        }

        var hlavicka = tabulka.AddRow();
        hlavicka.Format.Font.Bold = true;
        hlavicka.Format.Font.Size = 8;
        hlavicka.Shading.Color = Vikend;
        for (var m = 0; m < 12; m++)
        {
            hlavicka.Cells[m + 1].AddParagraph(Preklad.VelkePrve(od.AddMonths(m).ToString("MMMM", Planovanie.Sk)));
        }

        var podlaDna = dni.ToDictionary(d => d.Datum.Date);
        for (var cislo = 1; cislo <= 31; cislo++)
        {
            var riadok = tabulka.AddRow();
            riadok.Cells[0].AddParagraph(cislo.ToString(Planovanie.Sk)).Format.Font.Bold = true;
            for (var m = 0; m < 12; m++)
            {
                var mesiac = od.AddMonths(m);
                if (cislo > DateTime.DaysInMonth(mesiac.Year, mesiac.Month))
                {
                    riadok.Cells[m + 1].Shading.Color = Ciara;
                    continue;
                }

                var datum = new DateTime(mesiac.Year, mesiac.Month, cislo);
                var bunka = riadok.Cells[m + 1];
                var p = bunka.AddParagraph();
                var nazovDna = p.AddFormattedText(datum.ToString("ddd", Planovanie.Sk).TrimEnd('.') + " ");
                nazovDna.Font.Color = Siva;
                if (!podlaDna.TryGetValue(datum, out var d))
                {
                    continue;
                }

                if (d.Prazdniny is not null)
                {
                    bunka.Shading.Color = Zelena;
                }
                else if (d.Volno || d.Vikend)
                {
                    bunka.Shading.Color = Vikend;
                    nazovDna.Font.Color = Cervena;
                }

                var prvy = d.Vyskyty.FirstOrDefault(v => v.JeVlastny) ?? d.Vyskyty.FirstOrDefault(v => v.Zdroj != ZdrojKalendara.Prazdniny);
                if (d.Sviatok is not null && prvy is null)
                {
                    var s = p.AddFormattedText(d.Sviatok);
                    s.Font.Color = Cervena;
                }
                else if (prvy is not null)
                {
                    Zaznam(p, prvy, 6.5);
                    var dalsie = d.Vyskyty.Count(v => v.Zdroj != ZdrojKalendara.Prazdniny) - 1;
                    if (dalsie > 0)
                    {
                        p.AddText($" +{dalsie}");
                    }
                }
            }
        }

        var legenda = sekcia.AddParagraph("Zelené: prázdniny  ·  sivé: víkend a sviatok  ·  tučné: vlastné udalosti a úlohy");
        legenda.Format.SpaceBefore = Unit.FromPoint(4);
        legenda.Format.Font.Size = 7.5;
        legenda.Format.Font.Color = Siva;
        return doc;
    }

    // ------------------------------------------------------------------ zoznam

    /// <summary>Zoznam udalostí, úloh a termínov po dňoch (A4 na výšku).</summary>
    public static Document Zoznam(DateTime od, DateTime po, IReadOnlyList<DenZostavy> dni)
    {
        var doc = Novy($"Kalendár {Planovanie.DatumText(od)} – {Planovanie.DatumText(po)}", naSirku: false, out var sekcia);
        var tabulka = Tabulka(sekcia);
        tabulka.Borders.Visible = false;
        tabulka.AddColumn(Unit.FromCentimeter(3.4));
        tabulka.AddColumn(Unit.FromCentimeter(2.8));
        tabulka.AddColumn(Unit.FromCentimeter(11.9));
        var nic = true;
        foreach (var d in dni.Where(d => d.Vyskyty.Count > 0 || d.Poznamky.Count > 0 || d.Sviatok is not null))
        {
            nic = false;
            var prvy = true;
            var zaznamy = d.Vyskyty.Select(v => (v.CasText, v.Nazov, Doplnok: string.Join("  ·  ", new[] { v.Typ, v.TykaSaText }.Where(t => t.Length > 0)), v.Farba, Vlastny: v.JeVlastny))
                .Concat(d.Poznamky.Select(p => ("poznámka", p.Nazov, p.Nahlad, FarbaPoznamky, true)))
                .ToList();
            if (zaznamy.Count == 0)
            {
                zaznamy.Add(("", d.Sviatok!, "", FarbaSviatku, false));
            }

            foreach (var (cas, nazov, doplnok, farba, vlastny) in zaznamy)
            {
                var riadok = tabulka.AddRow();
                riadok.Borders.Top.Visible = prvy;
                riadok.Borders.Top.Color = Ciara;
                if (prvy)
                {
                    var den = riadok.Cells[0].AddParagraph(Preklad.VelkePrve(d.Datum.ToString("ddd d. M.", Planovanie.Sk)));
                    den.Format.Font.Bold = true;
                    if (d.Sviatok is not null && nazov != d.Sviatok)
                    {
                        var s = riadok.Cells[0].AddParagraph(d.Sviatok);
                        s.Format.Font.Size = 7.5;
                        s.Format.Font.Color = Cervena;
                    }
                }

                riadok.Cells[1].AddParagraph(cas).Format.Font.Color = Siva;
                var n = riadok.Cells[2].AddParagraph();
                var t = n.AddFormattedText(nazov);
                t.Font.Bold = vlastny;
                t.Font.Color = Tmava(farba);
                if (doplnok.Length > 0)
                {
                    var p = riadok.Cells[2].AddParagraph(doplnok);
                    p.Format.Font.Size = 8;
                    p.Format.Font.Color = Siva;
                }

                prvy = false;
            }
        }

        if (nic)
        {
            sekcia.AddParagraph("V tomto období nie sú žiadne udalosti, úlohy ani termíny.");
        }

        return doc;
    }

    // ------------------------------------------------------------------ úlohy a poznámky

    /// <summary>Zoznam úloh zoskupený podľa termínu (A4 na výšku) – s políčkom na odškrtnutie (písmo PDF nemá znak zaškrtávacieho políčka).</summary>
    public static Document Ulohy(IReadOnlyList<UlohaRiadok> ulohy, string podnadpis)
    {
        var doc = Novy("Úlohy" + (podnadpis.Length > 0 ? " – " + podnadpis : ""), naSirku: false, out var sekcia);
        if (ulohy.Count == 0)
        {
            sekcia.AddParagraph("Žiadne úlohy.");
            return doc;
        }

        foreach (var skupina in ulohy.GroupBy(u => u.Skupina)) // zoznam je už zoradený podľa skupín
        {
            var nadpis = sekcia.AddParagraph($"{skupina.Key} ({skupina.Count()})");
            nadpis.Format.Font.Size = 11;
            nadpis.Format.Font.Bold = true;
            nadpis.Format.SpaceBefore = Unit.FromPoint(8);
            nadpis.Format.SpaceAfter = Unit.FromPoint(3);
            var tabulka = Tabulka(sekcia);
            tabulka.AddColumn(Unit.FromCentimeter(0.7));
            tabulka.AddColumn(Unit.FromCentimeter(9.6));
            tabulka.AddColumn(Unit.FromCentimeter(2.4));
            tabulka.AddColumn(Unit.FromCentimeter(5.4));
            foreach (var u in skupina)
            {
                var riadok = tabulka.AddRow();
                riadok.Cells[0].AddParagraph(u.Hotovo ? "[x]" : "[  ]");
                var text = riadok.Cells[1].AddParagraph();
                var t = text.AddFormattedText((u.Vysoka ? "! " : "") + u.Text);
                t.Font.Bold = u.Vysoka;
                if (u.MaPopis)
                {
                    var p = riadok.Cells[1].AddParagraph(u.Popis!);
                    p.Format.Font.Size = 8;
                    p.Format.Font.Color = Siva;
                }

                riadok.Cells[2].AddParagraph(u.Termin is { } termin ? Planovanie.DatumText(termin) : "");
                riadok.Cells[3].AddParagraph(u.Podrobnosti).Format.Font.Size = 8;
                if (u.PoTermine)
                {
                    riadok.Cells[2].Format.Font.Color = Cervena;
                    riadok.Cells[2].Format.Font.Bold = true;
                }
            }
        }

        return doc;
    }

    /// <summary>Poznámky – každá s názvom, textom, dňom a naviazaním (A4 na výšku).</summary>
    public static Document Poznamky(IReadOnlyList<PoznamkaRiadok> poznamky, string podnadpis)
    {
        var doc = Novy("Poznámky" + (podnadpis.Length > 0 ? " – " + podnadpis : ""), naSirku: false, out var sekcia);
        if (poznamky.Count == 0)
        {
            sekcia.AddParagraph("Žiadne poznámky.");
            return doc;
        }

        foreach (var p in poznamky)
        {
            var tabulka = Tabulka(sekcia);
            tabulka.Borders.Visible = false;
            tabulka.AddColumn(Unit.FromCentimeter(18.1));
            var riadok = tabulka.AddRow();
            riadok.Shading.Color = Svetla(p.Farba, 0.35);
            riadok.TopPadding = Unit.FromPoint(5);
            riadok.BottomPadding = Unit.FromPoint(5);
            var nazov = riadok.Cells[0].AddParagraph(p.Nazov);
            nazov.Format.Font.Bold = true;
            nazov.Format.Font.Size = 11;
            foreach (var r in p.Text.Replace("\r", "").Split('\n'))
            {
                riadok.Cells[0].AddParagraph(r);
            }

            var info = riadok.Cells[0].AddParagraph(string.Join("  ·  ", new[] { p.Podrobnosti, "zmenená " + p.ZmeneneText }.Where(t => t.Length > 0)));
            info.Format.Font.Size = 7.5;
            info.Format.Font.Color = Siva;
            info.Format.SpaceBefore = Unit.FromPoint(3);
            sekcia.AddParagraph().Format.SpaceAfter = Unit.FromPoint(4);
        }

        return doc;
    }
}
