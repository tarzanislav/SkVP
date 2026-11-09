using System.Globalization;
using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkVP.Data;
using WinAlign = System.Windows.TextAlignment;

namespace SkVP.Services;

/// <summary>Výsledok načítania dokumentu Word.</summary>
public sealed record NacitanyDokument(List<Blok> Bloky, IReadOnlyList<string> Varovania);

/// <summary>
/// Načítanie dokumentu Word (.docx) do modelu textu bez Wordu: nadpisy (podľa štýlov Wordu aj slovenských názvov),
/// odseky s tučným písmom, kurzívou a podčiarknutím, odrážky a číslovanie (aj vnorené), tabuľky so zlúčenými bunkami.
/// Prvky, ktoré model nepozná (obrázky, textové polia …), sa vynechajú a vrátia sa ako varovanie.
/// </summary>
public sealed class DocxCitac
{
    private readonly Dictionary<string, int> _urovneStylov = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (bool Tucne, bool Kurziva, bool Podciarknute)> _formatStylov = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Dictionary<int, bool>> _cislovane = new();
    private readonly HashSet<string> _varovania = new();

    public static NacitanyDokument Nacitaj(string cesta)
    {
        try
        {
            using var prud = new FileStream(cesta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Nacitaj(prud);
        }
        catch (IOException ex)
        {
            throw new SkvpException($"Dokument sa nedá otvoriť.\n\n{ex.Message}");
        }
    }

    public static NacitanyDokument Nacitaj(Stream prud)
    {
        // časti dokumentu sa čítajú až pri prvom použití – poškodené XML vo vnútri súboru sa prejaví až tu
        try
        {
            using var docx = WordprocessingDocument.Open(prud, false);
            var citac = new DocxCitac();
            citac.PripravStyly(docx.MainDocumentPart?.StyleDefinitionsPart?.Styles);
            citac.PripravCislovanie(docx.MainDocumentPart?.NumberingDefinitionsPart?.Numbering);
            var telo = docx.MainDocumentPart?.Document?.Body;
            var bloky = telo is null ? new List<Blok>() : citac.Telo(telo);
            while (bloky.Count > 0 && bloky[^1] is Odsek { JePrazdny: true })
            {
                bloky.RemoveAt(bloky.Count - 1);
            }

            return new NacitanyDokument(bloky, citac._varovania.ToList());
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or FileFormatException or InvalidDataException or System.Xml.XmlException)
        {
            throw new SkvpException($"Súbor nie je platný dokument Word (.docx).\n\n{ex.Message}");
        }
    }

    // ---- štýly a číslovanie ----

    private void PripravStyly(Styles? styly)
    {
        if (styly is null)
        {
            return;
        }

        var vsetky = styly.Elements<Style>().Where(s => s.StyleId?.Value is not null).ToDictionary(s => s.StyleId!.Value!, StringComparer.OrdinalIgnoreCase);
        foreach (var (id, styl) in vsetky)
        {
            var uroven = UrovenStylu(styl, vsetky, 0);
            if (uroven > 0)
            {
                _urovneStylov[id] = uroven;
            }

            var beh = styl.StyleRunProperties;
            _formatStylov[id] = (Zapnute(beh?.Bold), Zapnute(beh?.Italic), beh?.Underline?.Val?.Value is { } u && u != UnderlineValues.None);
        }
    }

    private static int UrovenStylu(Style styl, Dictionary<string, Style> vsetky, int hlbka)
    {
        var meno = (styl.StyleName?.Val?.Value ?? "").Trim().ToLowerInvariant();
        var id = (styl.StyleId?.Value ?? "").ToLowerInvariant();
        foreach (var predpona in new[] { "heading ", "nadpis ", "heading", "nadpis" })
        {
            foreach (var kandidat in new[] { meno, id })
            {
                if (kandidat.StartsWith(predpona, StringComparison.Ordinal) &&
                    int.TryParse(kandidat[predpona.Length..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 9)
                {
                    return Math.Min(n, 3);
                }
            }
        }

        if (meno is "title" or "názov" or "nazov" || id == "title")
        {
            return 1;
        }

        if (styl.StyleParagraphProperties?.OutlineLevel?.Val?.Value is int obrys && obrys is >= 0 and <= 8)
        {
            return Math.Min(obrys + 1, 3);
        }

        if (hlbka < 5 && styl.BasedOn?.Val?.Value is { } zaklad && vsetky.TryGetValue(zaklad, out var rodic))
        {
            return UrovenStylu(rodic, vsetky, hlbka + 1);
        }

        return 0;
    }

    private void PripravCislovanie(Numbering? cislovanie)
    {
        if (cislovanie is null)
        {
            return;
        }

        var abstraktne = new Dictionary<int, Dictionary<int, bool>>();
        foreach (var a in cislovanie.Elements<AbstractNum>())
        {
            if (a.AbstractNumberId?.Value is not int id)
            {
                continue;
            }

            var urovne = new Dictionary<int, bool>();
            foreach (var l in a.Elements<Level>())
            {
                var format = l.NumberingFormat?.Val?.Value;
                urovne[l.LevelIndex?.Value ?? 0] = format is not null && format != NumberFormatValues.Bullet && format != NumberFormatValues.None;
            }

            abstraktne[id] = urovne;
        }

        foreach (var n in cislovanie.Elements<NumberingInstance>())
        {
            if (n.NumberID?.Value is int numId && n.AbstractNumId?.Val?.Value is int absId && abstraktne.TryGetValue(absId, out var urovne))
            {
                _cislovane[numId] = urovne;
            }
        }
    }

    // ---- telo dokumentu ----

    private sealed record PolozkaZoznamu(int NumId, int Uroven, Odsek Text);

    /// <summary>Šírka textu aktuálnej časti dokumentu v twipoch (na šírku tabuliek); A4 na výšku s okrajmi 2,5 cm.</summary>
    private double _sirkaTextu = 9070;

    /// <summary>Hĺbka vnorenia v tabuľkách – zlomy strán a šírka tabuľky platia len mimo tabuliek.</summary>
    private int _vTabulke;

    /// <summary>
    /// Telo dokumentu po častiach (sekciách Wordu). Vlastnosti časti sú na jej konci – v poslednom odseku časti,
    /// pri poslednej časti priamo v tele. Iné rozloženie strany ako A4 na výšku sa zapíše blokom <see cref="Sekcia"/>.
    /// </summary>
    private List<Blok> Telo(Body telo)
    {
        var casti = new List<(List<OpenXmlElement> Prvky, SectionProperties? Vlastnosti)> { (new List<OpenXmlElement>(), null) };
        foreach (var prvok in telo.ChildElements)
        {
            if (prvok is SectionProperties koniec)
            {
                casti[^1] = (casti[^1].Prvky, koniec);
                continue;
            }

            casti[^1].Prvky.Add(prvok);
            if (prvok is Paragraph { ParagraphProperties.SectionProperties: { } vlastnosti })
            {
                casti[^1] = (casti[^1].Prvky, vlastnosti);
                casti.Add((new List<OpenXmlElement>(), null));
            }
        }

        var bloky = new List<Blok>();
        RozlozenieStrany? predchadzajuce = null;
        foreach (var (prvky, vlastnosti) in casti)
        {
            if (prvky.Count == 0 && predchadzajuce is not null)
            {
                continue;
            }

            var rozlozenie = Rozlozenie(vlastnosti);
            if (predchadzajuce is null ? !rozlozenie.JeA4NaVysku : !rozlozenie.Zhodne(predchadzajuce))
            {
                bloky.Add(new Sekcia(rozlozenie));
            }

            predchadzajuce = rozlozenie;
            bloky.AddRange(Bloky(prvky));
        }

        return bloky;
    }

    /// <summary>Veľkosť strany časti (a šírka jej textu); bez údajov A4 na výšku.</summary>
    private RozlozenieStrany Rozlozenie(SectionProperties? vlastnosti)
    {
        var velkost = vlastnosti?.GetFirstChild<PageSize>();
        double sirka = velkost?.Width?.Value ?? 11906;
        double vyska = velkost?.Height?.Value ?? 16838;
        if (sirka <= 0 || vyska <= 0)
        {
            (sirka, vyska) = (11906, 16838);
        }

        // orientácia „na šírku“ s rozmermi na výšku (staršie programy) – rozmery sa vymenia
        if (velkost?.Orient?.Value == PageOrientationValues.Landscape && sirka < vyska)
        {
            (sirka, vyska) = (vyska, sirka);
        }

        var okraje = vlastnosti?.GetFirstChild<PageMargin>();
        var vlavo = (double?)okraje?.Left?.Value ?? 1418;
        var vpravo = (double?)okraje?.Right?.Value ?? 1418;
        _sirkaTextu = Math.Max(1000, sirka - vlavo - vpravo);
        return RozlozenieStrany.ZTwipov(sirka, vyska);
    }

    private List<Blok> Bloky(IEnumerable<OpenXmlElement> prvky)
    {
        var vysledok = new List<Blok>();
        var polozky = new List<PolozkaZoznamu>();

        void UzavriZoznam()
        {
            if (polozky.Count > 0)
            {
                vysledok.AddRange(PostavZoznamy(polozky, 0, 0, polozky.Count));
                polozky.Clear();
            }
        }

        foreach (var prvok in prvky)
        {
            switch (prvok)
            {
                case Paragraph p:
                    var uroven = UrovenNadpisu(p);
                    var numPr = p.ParagraphProperties?.NumberingProperties;
                    var numId = numPr?.NumberingId?.Val?.Value;
                    if (uroven == 0 && numId is int id && id != 0)
                    {
                        polozky.Add(new PolozkaZoznamu(id, numPr!.NumberingLevelReference?.Val?.Value ?? 0, Odsek(p)));
                        continue;
                    }

                    UzavriZoznam();
                    var zlom = _vTabulke == 0 ? ZlomVOdseku(p) : MiestoZlomu.Ziadny;
                    if (zlom == MiestoZlomu.Pred)
                    {
                        vysledok.Add(new ZlomStrany());
                    }

                    if (zlom != MiestoZlomu.Ziadny && Odsek(p).JePrazdny)
                    {
                        // odsek len so zlomom strany
                        if (zlom == MiestoZlomu.Za)
                        {
                            vysledok.Add(new ZlomStrany());
                        }

                        break;
                    }

                    if (uroven > 0)
                    {
                        var text = string.Concat(Useky(p).Where(u => !u.NovyRiadok).Select(u => u.Text)).Trim();
                        if (text.Length > 0)
                        {
                            vysledok.Add(new Nadpis(uroven, text));
                        }
                    }
                    else
                    {
                        var odsek = Odsek(p);
                        // prázdny odsek hneď za tabuľkou je len medzera (Word ho za tabuľku pridáva sám)
                        if (!(odsek.JePrazdny && vysledok.Count > 0 && vysledok[^1] is SkVP.Services.Tabulka))
                        {
                            vysledok.Add(odsek);
                        }
                    }

                    if (zlom == MiestoZlomu.Za)
                    {
                        vysledok.Add(new ZlomStrany());
                    }

                    break;

                case Table t:
                    UzavriZoznam();
                    if (Tabulka(t) is { } tabulka)
                    {
                        vysledok.Add(tabulka);
                    }

                    break;

                case SdtBlock sdt:
                    UzavriZoznam();
                    if (sdt.SdtContentBlock is { } obsah)
                    {
                        vysledok.AddRange(Bloky(obsah.ChildElements));
                    }

                    break;

                case SectionProperties or BookmarkStart or BookmarkEnd or ProofError:
                    break;

                default:
                    if (prvok.LocalName is "customXml" && prvok.ChildElements.Count > 0)
                    {
                        UzavriZoznam();
                        vysledok.AddRange(Bloky(prvok.ChildElements));
                    }

                    break;
            }
        }

        UzavriZoznam();
        return vysledok;
    }

    private enum MiestoZlomu
    {
        Ziadny,
        Pred,
        Za,
    }

    /// <summary>Zlom strany v odseku: pred odsekom (vlastnosť odseku alebo zlom pred prvým textom), inak za odsekom.</summary>
    private static MiestoZlomu ZlomVOdseku(Paragraph p)
    {
        if (Zapnute(p.ParagraphProperties?.PageBreakBefore))
        {
            return MiestoZlomu.Pred;
        }

        var predZlomom = false;
        foreach (var prvok in p.Descendants())
        {
            switch (prvok)
            {
                case Break { Type.Value: var druh } when druh == BreakValues.Page:
                    return predZlomom ? MiestoZlomu.Za : MiestoZlomu.Pred;
                case Text t when t.Text.Trim().Length > 0:
                    predZlomom = true;
                    break;
            }
        }

        return MiestoZlomu.Ziadny;
    }

    /// <summary>Z po sebe idúcich položiek zoznamu (s úrovňami) postaví vnorené zoznamy.</summary>
    private List<Blok> PostavZoznamy(List<PolozkaZoznamu> polozky, int uroven, int od, int po)
    {
        var vysledok = new List<Blok>();
        var i = od;
        while (i < po)
        {
            var cislovany = JeCislovany(polozky[i].NumId, Math.Max(uroven, polozky[i].Uroven));
            var zoznam = new List<IReadOnlyList<Blok>>();
            while (i < po && polozky[i].Uroven <= uroven)
            {
                var obsah = new List<Blok> { polozky[i].Text };
                var j = i + 1;
                while (j < po && polozky[j].Uroven > uroven)
                {
                    j++;
                }

                if (j > i + 1)
                {
                    obsah.AddRange(PostavZoznamy(polozky, uroven + 1, i + 1, j));
                }

                zoznam.Add(obsah);
                i = j;
                if (i < po && polozky[i].Uroven <= uroven && JeCislovany(polozky[i].NumId, uroven) != cislovany)
                {
                    break;
                }
            }

            if (zoznam.Count == 0)
            {
                // zoznam začína hlbšou úrovňou – vnorí sa pod prázdnu položku
                zoznam.Add(PostavZoznamy(polozky, uroven + 1, i, po));
                i = po;
            }

            vysledok.Add(new Zoznam(cislovany, zoznam));
        }

        return vysledok;
    }

    private bool JeCislovany(int numId, int uroven) =>
        _cislovane.TryGetValue(numId, out var urovne) && urovne.TryGetValue(uroven, out var c) && c;

    private int UrovenNadpisu(Paragraph p)
    {
        var vlastnosti = p.ParagraphProperties;
        if (vlastnosti?.ParagraphStyleId?.Val?.Value is { } styl && _urovneStylov.TryGetValue(styl, out var uroven))
        {
            return uroven;
        }

        if (vlastnosti?.OutlineLevel?.Val?.Value is int obrys && obrys is >= 0 and <= 8)
        {
            return Math.Min(obrys + 1, 3);
        }

        return 0;
    }

    private Odsek Odsek(Paragraph p)
    {
        var vlastnosti = p.ParagraphProperties;
        var zarovnanie = vlastnosti?.Justification?.Val?.Value switch
        {
            var j when j == JustificationValues.Center => WinAlign.Center,
            var j when j == JustificationValues.Right || j == JustificationValues.End => WinAlign.Right,
            var j when j == JustificationValues.Both || j == JustificationValues.Distribute => WinAlign.Justify,
            _ => WinAlign.Left,
        };
        var odsadenie = 0;
        var vlavo = vlastnosti?.Indentation?.Left?.Value ?? vlastnosti?.Indentation?.Start?.Value;
        if (vlavo is not null && int.TryParse(vlavo, NumberStyles.Integer, CultureInfo.InvariantCulture, out var twipy) && vlastnosti?.NumberingProperties is null)
        {
            odsadenie = Math.Clamp((int)Math.Round(twipy / 340.0), 0, 8);
        }

        return new Odsek(Useky(p), odsadenie, zarovnanie);
    }

    private List<Usek> Useky(OpenXmlElement odsek)
    {
        var styl = odsek is Paragraph p ? p.ParagraphProperties?.ParagraphStyleId?.Val?.Value : null;
        var zaklad = styl is not null && _formatStylov.TryGetValue(styl, out var f) ? f : (false, false, false);
        var useky = new List<Usek>();
        PridajUseky(odsek, useky, zaklad);
        return useky;
    }

    private void PridajUseky(OpenXmlElement rodic, List<Usek> useky, (bool Tucne, bool Kurziva, bool Podciarknute) zaklad)
    {
        foreach (var prvok in rodic.ChildElements)
        {
            switch (prvok)
            {
                case Run beh:
                    var v = beh.RunProperties;
                    var stylBehu = v?.RunStyle?.Val?.Value is { } rs && _formatStylov.TryGetValue(rs, out var fs) ? fs : zaklad;
                    var tucne = v?.Bold is null ? stylBehu.Tucne || zaklad.Tucne : Zapnute(v.Bold);
                    var kurziva = v?.Italic is null ? stylBehu.Kurziva || zaklad.Kurziva : Zapnute(v.Italic);
                    var podciarknute = v?.Underline is null ? stylBehu.Podciarknute || zaklad.Podciarknute
                        : v.Underline.Val?.Value is { } u && u != UnderlineValues.None;
                    foreach (var cast in beh.ChildElements)
                    {
                        switch (cast)
                        {
                            case Text t:
                                Pridaj(useky, t.Text, tucne, kurziva, podciarknute);
                                break;
                            case TabChar:
                                Pridaj(useky, "\t", tucne, kurziva, podciarknute);
                                break;
                            case Break zlom when zlom.Type?.Value != BreakValues.Page && zlom.Type?.Value != BreakValues.Column:
                                useky.Add(new Usek("", NovyRiadok: true));
                                break;
                            case CarriageReturn:
                                useky.Add(new Usek("", NovyRiadok: true));
                                break;
                            case NoBreakHyphen:
                                Pridaj(useky, "-", tucne, kurziva, podciarknute);
                                break;
                            case Drawing or Picture or EmbeddedObject:
                                _varovania.Add("obrázky a objekty");
                                break;
                        }
                    }

                    break;

                case Hyperlink or SimpleField or InsertedRun or SdtRun:
                    PridajUseky(prvok, useky, zaklad);
                    break;

                case DeletedRun:
                    _varovania.Add("sledované zmeny (zobrazujú sa prijaté)");
                    break;

                default:
                    if (prvok.LocalName is "sdtContent" or "customXml" or "fldSimple" or "ins" or "smartTag")
                    {
                        PridajUseky(prvok, useky, zaklad);
                    }
                    else if (prvok.LocalName is "AlternateContent")
                    {
                        _varovania.Add("textové polia a tvary");
                    }

                    break;
            }
        }
    }

    private static void Pridaj(List<Usek> useky, string text, bool tucne, bool kurziva, bool podciarknute)
    {
        if (text.Length == 0)
        {
            return;
        }

        // susedné úseky s rovnakým formátom sa spoja
        if (useky.Count > 0 && useky[^1] is { NovyRiadok: false } posledny &&
            posledny.Tucne == tucne && posledny.Kurziva == kurziva && posledny.Podciarknute == podciarknute)
        {
            useky[^1] = posledny with { Text = posledny.Text + text };
            return;
        }

        useky.Add(new Usek(text, tucne, kurziva, podciarknute));
    }

    private Tabulka? Tabulka(Table t)
    {
        _vTabulke++;
        try
        {
            return NacitajTabulku(t);
        }
        finally
        {
            _vTabulke--;
        }
    }

    private Tabulka? NacitajTabulku(Table t)
    {
        var sirkaTextu = _sirkaTextu;
        var vnorena = _vTabulke > 1;
        var siet = t.GetFirstChild<TableGrid>()?.Elements<GridColumn>()
            .Select(g => double.TryParse(g.Width?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var w) ? w : 0).ToList() ?? new List<double>();

        // najprv bunky s pozíciou stĺpca a zlúčením zvislo
        var riadky = new List<List<(int Stlpec, int Sirka, string? Zvislo, TableCell Bunka)>>();
        foreach (var riadok in t.Elements<TableRow>())
        {
            var bunky = new List<(int, int, string?, TableCell)>();
            var stlpec = riadok.TableRowProperties?.GetFirstChild<GridBefore>()?.Val?.Value ?? 0;
            foreach (var bunka in riadok.Elements<TableCell>())
            {
                var vlastnosti = bunka.TableCellProperties;
                var sirka = Math.Max(1, vlastnosti?.GridSpan?.Val?.Value ?? 1);
                string? zvislo = null;
                if (vlastnosti?.VerticalMerge is { } vm)
                {
                    zvislo = vm.Val?.Value == MergedCellValues.Restart ? "zaciatok" : "pokracuje";
                }

                bunky.Add((stlpec, sirka, zvislo, bunka));
                stlpec += sirka;
            }

            riadky.Add(bunky);
        }

        if (riadky.Count == 0)
        {
            return null;
        }

        var pocetStlpcov = Math.Max(siet.Count, riadky.Max(r => r.Count == 0 ? 0 : r[^1].Item1 + r[^1].Item2));
        if (pocetStlpcov == 0)
        {
            return null;
        }

        var hlavicka = t.Elements<TableRow>().FirstOrDefault()?.TableRowProperties?.GetFirstChild<TableHeader>() is not null;

        // políčka pokryté zvislým zlúčením zhora; bunky sa zapíšu na svoje miesto v mriežke – vynechané stĺpce
        // (gridBefore, chýbajúce bunky) sa doplnia prázdnymi bunkami, aby sa ďalšie bunky neposunuli
        var pokryte = new HashSet<(int Riadok, int Stlpec)>();
        var vysledok = new List<IReadOnlyList<Bunka>>();
        for (var r = 0; r < riadky.Count; r++)
        {
            var bunky = new List<Bunka>();
            var pozicia = 0;
            foreach (var (stlpec, sirka, zvislo, bunka) in riadky[r])
            {
                if (zvislo == "pokracuje" && pokryte.Contains((r, stlpec)))
                {
                    continue;
                }

                // voľné políčka pred bunkou
                while (pozicia < stlpec)
                {
                    if (pokryte.Contains((r, pozicia)))
                    {
                        pozicia++;
                        continue;
                    }

                    var od = pozicia;
                    while (pozicia < stlpec && !pokryte.Contains((r, pozicia)))
                    {
                        pozicia++;
                    }

                    bunky.Add(new Bunka(new List<Blok>(), pozicia - od));
                }

                var vyska = 1;
                if (zvislo == "zaciatok")
                {
                    while (r + vyska < riadky.Count && riadky[r + vyska].Any(b => b.Stlpec == stlpec && b.Zvislo == "pokracuje"))
                    {
                        vyska++;
                    }
                }

                for (var d = 1; d < vyska; d++)
                {
                    for (var s = 0; s < sirka; s++)
                    {
                        pokryte.Add((r + d, stlpec + s));
                    }
                }

                var tienovana = bunka.TableCellProperties?.Shading?.Fill?.Value is { } fill && fill is not ("auto" or "FFFFFF" or "ffffff");
                bunky.Add(new Bunka(Bloky(bunka.ChildElements), sirka, vyska, r == 0 && (hlavicka || tienovana)));
                pozicia = Math.Max(pozicia, stlpec + sirka);
            }

            vysledok.Add(bunky);
        }

        // šírka tabuľky voči šírke textu (úzka tabuľka zostane úzka); takmer celá šírka = celá šírka
        var podiel = vnorena || siet.Sum() <= 0 ? 1 : siet.Sum() / sirkaTextu;
        return new Tabulka(vysledok, pocetStlpcov, siet.Count == pocetStlpcov && siet.Sum() > 0 ? siet : null,
            podiel >= 0.93 ? 1 : Math.Max(0.2, podiel)).Pravidelna();
    }

    private static bool Zapnute(OnOffType? prvok) => prvok is not null && (prvok.Val is null || prvok.Val.Value);
}
