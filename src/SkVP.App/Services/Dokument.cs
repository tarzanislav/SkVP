using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SkVP.Services;

// ---------------------------------------------------------------------------------------------
// Spoločný model textu osnovy. Súbor Word (.docx) sa načíta do tohto modelu, z neho sa zobrazí v editore,
// uloží späť do Wordu a vysádza do PDF a na tlač – všetky cesty idú cez rovnaký model, preto sedia.
// ---------------------------------------------------------------------------------------------

public abstract record Blok;

/// <summary>Nadpis v texte osnovy (úroveň 1–3).</summary>
public sealed record Nadpis(int Uroven, string Text) : Blok;

public sealed record Odsek(IReadOnlyList<Usek> Useky, int Odsadenie = 0, TextAlignment Zarovnanie = TextAlignment.Left) : Blok
{
    public bool JePrazdny => Useky.All(u => u.Text.Trim().Length == 0 && !u.NovyRiadok);
}

/// <summary>Úsek textu s rovnakým formátom (alebo zalomenie riadka).</summary>
public sealed record Usek(string Text, bool Tucne = false, bool Kurziva = false, bool Podciarknute = false, bool NovyRiadok = false);

public sealed record Zoznam(bool Cislovany, IReadOnlyList<IReadOnlyList<Blok>> Polozky) : Blok;

/// <summary>Bunka tabuľky – môže zaberať viac stĺpcov alebo riadkov (zlúčené bunky).</summary>
public sealed record Bunka(IReadOnlyList<Blok> Bloky, int Stlpce = 1, int Riadky = 1, bool Hlavicka = false);

/// <summary>Tabuľka; bunky pokryté zlúčenou bunkou z predchádzajúceho riadka sa v riadkoch neuvádzajú.</summary>
public sealed record Tabulka(IReadOnlyList<IReadOnlyList<Bunka>> Riadky, int PocetStlpcov, IReadOnlyList<double>? Sirky = null) : Blok
{
    /// <summary>Pomerné šírky stĺpcov (súčet 1).</summary>
    public IReadOnlyList<double> PomerneSirky
    {
        get
        {
            if (Sirky is { Count: > 0 } s && s.Count == PocetStlpcov && s.Sum() > 0)
            {
                var sucet = s.Sum();
                return s.Select(x => x / sucet).ToList();
            }

            return Enumerable.Repeat(1.0 / Math.Max(1, PocetStlpcov), PocetStlpcov).ToList();
        }
    }
}

/// <summary>Písma a veľkosti textu osnov – rovnaké v editore, v PDF aj vo Worde.</summary>
public static class PismaDokumentu
{
    private static string? _text;

    /// <summary>Písmo textu: Constantia (Windows), ak chýba, tak Georgia.</summary>
    public static string Text => _text ??= Najdi("Constantia", "Georgia", "Cambria", "Times New Roman");

    public const string Nadpis = "Segoe UI";

    public const double VelkostTextuDip = 15;

    public static double VelkostNadpisuDip(int uroven) => uroven switch
    {
        1 => 22,
        2 => 17.5,
        _ => 15,
    };

    private static string Najdi(params string[] mena)
    {
        try
        {
            var systemove = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return mena.FirstOrDefault(systemove.Contains) ?? mena[^1];
        }
        catch (Exception)
        {
            return mena[^1];
        }
    }
}

/// <summary>Prevod medzi modelom textu a dokumentom editora (FlowDocument).</summary>
public static class DokumentOsnovy
{
    private const string ZnackaNadpisu = "H";

    private static readonly SolidColorBrush Linka = Zmrazeny(Color.FromRgb(0xC5, 0xCC, 0xD6));
    private static readonly SolidColorBrush PodkladHlavicky = Zmrazeny(Color.FromRgb(0xF3, 0xF4, 0xF6));

    /// <summary>Nadpisy novej osnovy predmetu (podľa bežnej štruktúry učebných osnov ŠkVP).</summary>
    public static readonly string[] CastiOsnovy =
    {
        "Charakteristika predmetu",
        "Ciele predmetu",
        "Obsahový a výkonový štandard",
        "Rozpis učiva podľa ročníkov",
        "Metódy a formy práce",
        "Hodnotenie predmetu",
        "Učebné zdroje",
    };

    /// <summary>Nová osnova predmetu s pripravenými nadpismi častí.</summary>
    public static List<Blok> SablonaOsnovy()
    {
        var bloky = new List<Blok>();
        foreach (var cast in CastiOsnovy)
        {
            bloky.Add(new Nadpis(1, cast));
            bloky.Add(new Odsek(new[] { new Usek("") }));
        }

        return bloky;
    }

    public static FlowDocument Novy()
    {
        var dokument = new FlowDocument();
        Uprav(dokument);
        dokument.Blocks.Add(new Paragraph());
        return dokument;
    }

    /// <summary>Jednotné písmo a rozmery dokumentu v editore.</summary>
    public static void Uprav(FlowDocument dokument)
    {
        dokument.FontFamily = new FontFamily(PismaDokumentu.Text);
        dokument.FontSize = PismaDokumentu.VelkostTextuDip;
        dokument.LineHeight = double.NaN;
        dokument.PagePadding = new Thickness(0);
        dokument.TextAlignment = TextAlignment.Left;
    }

    /// <summary>Úroveň nadpisu odseku (0 = bežný text).</summary>
    public static int UrovenNadpisu(Paragraph odsek) =>
        odsek.Tag is string znacka && znacka.Length == 2 && znacka[0] == 'H' && char.IsDigit(znacka[1]) ? znacka[1] - '0' : 0;

    public static void NastavNadpis(Paragraph odsek, int uroven)
    {
        if (uroven <= 0)
        {
            odsek.Tag = null;
            odsek.ClearValue(TextElement.FontSizeProperty);
            odsek.ClearValue(TextElement.FontWeightProperty);
            odsek.ClearValue(TextElement.FontStyleProperty);
            odsek.ClearValue(TextElement.FontFamilyProperty);
            odsek.ClearValue(TextElement.ForegroundProperty);
            odsek.ClearValue(Block.MarginProperty);
            return;
        }

        uroven = Math.Clamp(uroven, 1, 3);
        odsek.Tag = ZnackaNadpisu + uroven;
        odsek.FontFamily = new FontFamily(PismaDokumentu.Nadpis);
        odsek.FontSize = PismaDokumentu.VelkostNadpisuDip(uroven);
        odsek.FontWeight = uroven == 3 ? FontWeights.SemiBold : FontWeights.Bold;
        odsek.FontStyle = uroven == 3 ? FontStyles.Italic : FontStyles.Normal;
        odsek.Margin = new Thickness(0, uroven == 1 ? 22 : 16, 0, 6);
    }

    // ---- model → editor ----

    public static FlowDocument KDokumentu(IReadOnlyList<Blok> bloky)
    {
        var dokument = new FlowDocument();
        Uprav(dokument);
        PridajBloky(dokument.Blocks, bloky);
        if (dokument.Blocks.Count == 0)
        {
            dokument.Blocks.Add(new Paragraph());
        }

        return dokument;
    }

    private static void PridajBloky(BlockCollection ciel, IReadOnlyList<Blok> bloky)
    {
        foreach (var blok in bloky)
        {
            switch (blok)
            {
                case Nadpis n:
                    var nadpis = new Paragraph(new Run(n.Text));
                    NastavNadpis(nadpis, n.Uroven);
                    ciel.Add(nadpis);
                    break;

                case Odsek o:
                    ciel.Add(Odsek(o));
                    break;

                case Zoznam z:
                    var zoznam = new List
                    {
                        MarkerStyle = z.Cislovany ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                        Margin = new Thickness(0, 0, 0, 8),
                    };
                    foreach (var polozka in z.Polozky)
                    {
                        var li = new ListItem();
                        PridajBloky(li.Blocks, polozka);
                        if (li.Blocks.Count == 0)
                        {
                            li.Blocks.Add(new Paragraph());
                        }

                        foreach (var p in li.Blocks.OfType<Paragraph>())
                        {
                            p.Margin = new Thickness(0, 0, 0, 2);
                        }

                        zoznam.ListItems.Add(li);
                    }

                    ciel.Add(zoznam);
                    break;

                case Tabulka t:
                    ciel.Add(Tabulka(t));
                    break;
            }
        }
    }

    private static Paragraph Odsek(Odsek o)
    {
        var odsek = new Paragraph { TextAlignment = o.Zarovnanie };
        if (o.Odsadenie > 0)
        {
            odsek.Margin = new Thickness(20 * o.Odsadenie, 0, 0, odsek.Margin.Bottom);
        }

        foreach (var u in o.Useky)
        {
            if (u.NovyRiadok)
            {
                odsek.Inlines.Add(new LineBreak());
                continue;
            }

            if (u.Text.Length == 0)
            {
                continue;
            }

            var beh = new Run(u.Text);
            if (u.Tucne)
            {
                beh.FontWeight = FontWeights.Bold;
            }

            if (u.Kurziva)
            {
                beh.FontStyle = FontStyles.Italic;
            }

            if (u.Podciarknute)
            {
                beh.TextDecorations = TextDecorations.Underline;
            }

            odsek.Inlines.Add(beh);
        }

        return odsek;
    }

    private static Table Tabulka(Tabulka t)
    {
        var tabulka = new Table { CellSpacing = 0, BorderBrush = Linka, BorderThickness = new Thickness(0.5), Margin = new Thickness(0, 6, 0, 10) };
        foreach (var sirka in t.PomerneSirky)
        {
            tabulka.Columns.Add(new TableColumn { Width = new GridLength(Math.Max(0.02, sirka), GridUnitType.Star) });
        }

        var skupina = new TableRowGroup();
        foreach (var r in t.Riadky)
        {
            var riadok = new TableRow();
            foreach (var b in r)
            {
                var bunka = new TableCell
                {
                    BorderBrush = Linka,
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(7, 4, 7, 4),
                    ColumnSpan = Math.Max(1, b.Stlpce),
                    RowSpan = Math.Max(1, b.Riadky),
                };
                if (b.Hlavicka)
                {
                    bunka.Background = PodkladHlavicky;
                }

                PridajBloky(bunka.Blocks, b.Bloky);
                if (bunka.Blocks.Count == 0)
                {
                    bunka.Blocks.Add(new Paragraph());
                }

                foreach (var p in bunka.Blocks.OfType<Paragraph>())
                {
                    if (UrovenNadpisu(p) == 0)
                    {
                        p.Margin = new Thickness(0);
                    }
                }

                riadok.Cells.Add(bunka);
            }

            skupina.Rows.Add(riadok);
        }

        tabulka.RowGroups.Add(skupina);
        return tabulka;
    }

    // ---- editor → model ----

    /// <summary>Rozloží dokument editora na model textu (na uloženie do Wordu).</summary>
    public static List<Blok> Rozober(FlowDocument dokument)
    {
        Vycisti(dokument);
        return Bloky(dokument.Blocks);
    }

    private static List<Blok> Bloky(IEnumerable<Block> bloky)
    {
        var vysledok = new List<Blok>();
        foreach (var blok in bloky)
        {
            switch (blok)
            {
                case Paragraph odsek:
                    var uroven = UrovenNadpisu(odsek);
                    if (uroven > 0)
                    {
                        var text = new TextRange(odsek.ContentStart, odsek.ContentEnd).Text.Trim();
                        if (text.Length > 0)
                        {
                            vysledok.Add(new Nadpis(uroven, text));
                        }
                    }
                    else
                    {
                        var useky = new List<Usek>();
                        Inlines(odsek.Inlines, useky, false);
                        var odsadenie = double.IsNaN(odsek.Margin.Left) ? 0 : (int)Math.Round(Math.Max(0, odsek.Margin.Left) / 20);
                        vysledok.Add(new Odsek(useky, odsadenie, odsek.TextAlignment));
                    }

                    break;

                case List zoznam:
                    var cislovany = zoznam.MarkerStyle is TextMarkerStyle.Decimal or TextMarkerStyle.LowerLatin
                        or TextMarkerStyle.UpperLatin or TextMarkerStyle.LowerRoman or TextMarkerStyle.UpperRoman;
                    var polozky = zoznam.ListItems.Select(p => (IReadOnlyList<Blok>)Bloky(p.Blocks)).ToList();
                    if (polozky.Count > 0)
                    {
                        vysledok.Add(new Zoznam(cislovany, polozky));
                    }

                    break;

                case Table tabulka:
                    var riadky = new List<IReadOnlyList<Bunka>>();
                    var prvy = true;
                    foreach (var skupina in tabulka.RowGroups)
                    {
                        foreach (var riadok in skupina.Rows)
                        {
                            riadky.Add(riadok.Cells.Select(b => new Bunka(Bloky(b.Blocks), Math.Max(1, b.ColumnSpan), Math.Max(1, b.RowSpan),
                                prvy && b.Background is not null)).ToList());
                            prvy = false;
                        }
                    }

                    var stlpce = Math.Max(tabulka.Columns.Count, riadky.Count == 0 ? 0 : riadky.Max(r => r.Sum(b => b.Stlpce)));
                    List<double>? sirky = null;
                    if (tabulka.Columns.Count == stlpce && tabulka.Columns.All(c => c.Width.IsStar || c.Width.IsAbsolute))
                    {
                        sirky = tabulka.Columns.Select(c => c.Width.Value).ToList();
                    }

                    if (riadky.Count > 0 && stlpce > 0)
                    {
                        vysledok.Add(new Tabulka(riadky, stlpce, sirky));
                    }

                    break;

                case Section sekcia:
                    vysledok.AddRange(Bloky(sekcia.Blocks));
                    break;
            }
        }

        // prázdne odseky na konci nemajú význam
        while (vysledok.Count > 0 && vysledok[^1] is Odsek { JePrazdny: true })
        {
            vysledok.RemoveAt(vysledok.Count - 1);
        }

        return vysledok;
    }

    private static void Inlines(InlineCollection inlines, List<Usek> useky, bool podciarknute)
    {
        foreach (var inline in inlines)
        {
            var podciarknuteTu = podciarknute || inline.TextDecorations?.Any(d => d.Location == TextDecorationLocation.Underline) == true;
            switch (inline)
            {
                case Run beh:
                    if (beh.Text.Length > 0)
                    {
                        useky.Add(new Usek(beh.Text.Replace("\r", "").Replace('\n', ' '),
                            beh.FontWeight.ToOpenTypeWeight() >= 600,
                            beh.FontStyle == FontStyles.Italic || beh.FontStyle == FontStyles.Oblique,
                            podciarknuteTu));
                    }

                    break;

                case LineBreak:
                    useky.Add(new Usek("", NovyRiadok: true));
                    break;

                case Span span:
                    Inlines(span.Inlines, useky, podciarknuteTu);
                    break;
            }
        }
    }

    // ---- pomocné ----

    /// <summary>Dĺžka vlastného textu (bez nadpisov) – podľa nej sa pozná nezačatá osnova.</summary>
    public static int DlzkaTextu(IEnumerable<Blok> bloky) => bloky.Sum(DlzkaTextu);

    private static int DlzkaTextu(Blok blok) => blok switch
    {
        Odsek o => o.Useky.Sum(u => u.Text.Trim().Length),
        Zoznam z => z.Polozky.Sum(p => p.Sum(DlzkaTextu)),
        Tabulka t => t.Riadky.Sum(r => r.Sum(b => b.Bloky.Sum(DlzkaTextu))),
        _ => 0,
    };

    /// <summary>Čistý text (na vyhľadávanie a kontrolu).</summary>
    public static string Text(IEnumerable<Blok> bloky)
    {
        var sb = new StringBuilder();
        foreach (var blok in bloky)
        {
            switch (blok)
            {
                case Nadpis n:
                    sb.AppendLine(n.Text);
                    break;
                case Odsek o:
                    sb.AppendLine(string.Concat(o.Useky.Select(u => u.NovyRiadok ? "\n" : u.Text)));
                    break;
                case Zoznam z:
                    foreach (var p in z.Polozky)
                    {
                        sb.Append(Text(p));
                    }

                    break;
                case Tabulka t:
                    foreach (var r in t.Riadky)
                    {
                        sb.AppendLine(string.Join("\t", r.Select(b => Text(b.Bloky).Trim())));
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    private static SolidColorBrush Zmrazeny(Color farba)
    {
        var stetec = new SolidColorBrush(farba);
        stetec.Freeze();
        return stetec;
    }

    // ---- čistenie ----

    /// <summary>Odstráni z dokumentu prvky, ktoré sa neukladajú (obrázky a ovládacie prvky z vloženého obsahu, odkazy).</summary>
    public static void Vycisti(FlowDocument dokument) => VycistiBloky(dokument.Blocks);

    private static void VycistiBloky(BlockCollection bloky)
    {
        foreach (var blok in bloky.ToList())
        {
            switch (blok)
            {
                case BlockUIContainer:
                    bloky.Remove(blok);
                    break;
                case Paragraph odsek:
                    VycistiInlines(odsek.Inlines);
                    break;
                case List zoznam:
                    foreach (var polozka in zoznam.ListItems)
                    {
                        VycistiBloky(polozka.Blocks);
                    }

                    break;
                case Table tabulka:
                    foreach (var skupina in tabulka.RowGroups)
                    {
                        foreach (var riadok in skupina.Rows)
                        {
                            foreach (var bunka in riadok.Cells)
                            {
                                VycistiBloky(bunka.Blocks);
                            }
                        }
                    }

                    break;
                case Section sekcia:
                    VycistiBloky(sekcia.Blocks);
                    break;
            }
        }
    }

    private static void VycistiInlines(InlineCollection inlines)
    {
        foreach (var inline in inlines.ToList())
        {
            switch (inline)
            {
                case InlineUIContainer or Figure or Floater:
                    inlines.Remove(inline);
                    break;
                case Hyperlink odkaz:
                    // odkaz sa zmení na obyčajný text
                    var span = new Span();
                    foreach (var vnutorny in odkaz.Inlines.ToList())
                    {
                        odkaz.Inlines.Remove(vnutorny);
                        span.Inlines.Add(vnutorny);
                    }

                    inlines.InsertBefore(odkaz, span);
                    inlines.Remove(odkaz);
                    VycistiInlines(span.Inlines);
                    break;
                case Span s:
                    VycistiInlines(s.Inlines);
                    break;
            }
        }
    }

    /// <summary>
    /// Očistí text vložený z Wordu: rozpozná nadpisy (podľa veľkosti a hrúbky písma), zruší cudzie písma, farby
    /// a medzery, tabuľkám dá jednotný rámik.
    /// </summary>
    public static void OcistiVlozene(FlowDocument dokument)
    {
        Vycisti(dokument);
        OcistiBloky(dokument.Blocks);
    }

    private static void OcistiBloky(BlockCollection bloky)
    {
        foreach (var blok in bloky.ToList())
        {
            switch (blok)
            {
                case Paragraph odsek:
                    var uroven = RozpoznajNadpis(odsek);
                    foreach (var prvok in Prvky(odsek.Inlines))
                    {
                        prvok.ClearValue(TextElement.FontFamilyProperty);
                        prvok.ClearValue(TextElement.FontSizeProperty);
                        prvok.ClearValue(TextElement.ForegroundProperty);
                        prvok.ClearValue(TextElement.BackgroundProperty);
                    }

                    odsek.ClearValue(TextElement.FontFamilyProperty);
                    odsek.ClearValue(TextElement.FontSizeProperty);
                    odsek.ClearValue(TextElement.ForegroundProperty);
                    odsek.ClearValue(TextElement.BackgroundProperty);
                    odsek.ClearValue(Block.LineHeightProperty);
                    odsek.ClearValue(Paragraph.TextIndentProperty);
                    odsek.ClearValue(Block.BorderBrushProperty);
                    odsek.ClearValue(Block.BorderThicknessProperty);
                    var vlavo = double.IsNaN(odsek.Margin.Left) ? 0 : Math.Max(0, odsek.Margin.Left);
                    odsek.Margin = new Thickness(vlavo, 0, 0, 8);
                    if (uroven > 0)
                    {
                        NastavNadpis(odsek, uroven);
                        foreach (var prvok in Prvky(odsek.Inlines))
                        {
                            prvok.ClearValue(TextElement.FontWeightProperty);
                        }
                    }

                    break;

                case List zoznam:
                    zoznam.ClearValue(TextElement.FontFamilyProperty);
                    zoznam.ClearValue(TextElement.FontSizeProperty);
                    zoznam.ClearValue(TextElement.ForegroundProperty);
                    foreach (var polozka in zoznam.ListItems)
                    {
                        OcistiBloky(polozka.Blocks);
                    }

                    break;

                case Table tabulka:
                    tabulka.CellSpacing = 0;
                    tabulka.BorderBrush = Linka;
                    tabulka.BorderThickness = new Thickness(0.5);
                    tabulka.ClearValue(TextElement.FontFamilyProperty);
                    tabulka.ClearValue(TextElement.FontSizeProperty);
                    foreach (var skupina in tabulka.RowGroups)
                    {
                        foreach (var riadok in skupina.Rows)
                        {
                            foreach (var bunka in riadok.Cells)
                            {
                                bunka.BorderBrush = Linka;
                                bunka.BorderThickness = new Thickness(0.5);
                                bunka.Padding = new Thickness(7, 4, 7, 4);
                                bunka.ClearValue(TextElement.FontFamilyProperty);
                                bunka.ClearValue(TextElement.FontSizeProperty);
                                bunka.ClearValue(TextElement.ForegroundProperty);
                                OcistiBloky(bunka.Blocks);
                                foreach (var o in bunka.Blocks.OfType<Paragraph>())
                                {
                                    o.Margin = new Thickness(0);
                                }
                            }
                        }
                    }

                    break;

                case Section sekcia:
                    sekcia.ClearValue(TextElement.FontFamilyProperty);
                    sekcia.ClearValue(TextElement.FontSizeProperty);
                    sekcia.ClearValue(TextElement.ForegroundProperty);
                    sekcia.ClearValue(TextElement.BackgroundProperty);
                    sekcia.ClearValue(Block.LineHeightProperty);
                    OcistiBloky(sekcia.Blocks);
                    break;
            }
        }
    }

    /// <summary>Nadpis z Wordu: krátky odsek s tučným a väčším písmom.</summary>
    private static int RozpoznajNadpis(Paragraph odsek)
    {
        var behy = Prvky(odsek.Inlines).OfType<Run>().Where(r => r.Text.Trim().Length > 0).ToList();
        if (behy.Count == 0)
        {
            return 0;
        }

        var text = string.Concat(behy.Select(r => r.Text)).Trim();
        if (text.Length > 140 || text.EndsWith('.') && text.Length > 60)
        {
            return 0;
        }

        var tucne = behy.All(r => r.FontWeight.ToOpenTypeWeight() >= 600);
        var velkost = behy.Max(r => r.FontSize) * 72 / 96; // v bodoch
        if (velkost >= 15.5)
        {
            return 1;
        }

        if (tucne && velkost >= 13.5)
        {
            return 1;
        }

        if (tucne && velkost >= 12.5)
        {
            return 2;
        }

        return 0;
    }

    private static IEnumerable<Inline> Prvky(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            yield return inline;
            if (inline is Span span)
            {
                foreach (var vnutorny in Prvky(span.Inlines))
                {
                    yield return vnutorny;
                }
            }
        }
    }
}
