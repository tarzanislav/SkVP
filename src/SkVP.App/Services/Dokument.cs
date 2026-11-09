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

/// <summary>
/// Tabuľka; bunky pokryté zlúčenou bunkou z predchádzajúceho riadka sa v riadkoch neuvádzajú.
/// <paramref name="Podiel"/> = šírka tabuľky ako časť šírky textu (úzka tabuľka z Wordu zostane úzka).
/// </summary>
public sealed record Tabulka(IReadOnlyList<IReadOnlyList<Bunka>> Riadky, int PocetStlpcov, IReadOnlyList<double>? Sirky = null, double Podiel = 1)
    : Blok
{
    /// <summary>Šírka tabuľky ako časť šírky textu (0,2 – 1).</summary>
    public double SirkaVTexte => double.IsFinite(Podiel) ? Math.Clamp(Podiel, 0.2, 1) : 1;

    /// <summary>
    /// Tabuľka s pravidelnou mriežkou: bunky sa kladú zľava do voľných políčok a zlúčenie (vodorovné aj zvislé) sa skráti
    /// tak, aby nezasahovalo do políčok obsadených inou bunkou. Tabuľky z Wordu bývajú nepravidelné (zlúčenia
    /// sa krížia, chýbajú alebo prebývajú bunky) – sadzba PDF by na nich zlyhala. Text sa nestratí: bunka, pre ktorú
    /// v riadku nezostalo miesto, sa pripojí k predchádzajúcej bunke riadka.
    /// </summary>
    public Tabulka Pravidelna()
    {
        var stlpcov = Math.Max(1, PocetStlpcov);
        var obsadene = new HashSet<(int Riadok, int Stlpec)>();
        var riadky = new List<IReadOnlyList<Bunka>>(Riadky.Count);
        var zmena = false;
        for (var r = 0; r < Riadky.Count; r++)
        {
            var riadok = new List<Bunka>(Riadky[r].Count);
            var stlpec = 0;
            foreach (var bunka in Riadky[r])
            {
                while (stlpec < stlpcov && obsadene.Contains((r, stlpec)))
                {
                    stlpec++;
                }

                if (stlpec >= stlpcov)
                {
                    zmena = true;
                    if (riadok.Count > 0)
                    {
                        riadok[^1] = riadok[^1] with { Bloky = riadok[^1].Bloky.Concat(bunka.Bloky).ToList() };
                    }
                    else if (riadky.Count > 0 && riadky[^1].Count > 0)
                    {
                        // celý riadok je pokrytý zlúčenými bunkami – text patrí do bunky nad ním
                        var predosly = riadky[^1].ToList();
                        predosly[^1] = predosly[^1] with { Bloky = predosly[^1].Bloky.Concat(bunka.Bloky).ToList() };
                        riadky[^1] = predosly;
                    }

                    continue;
                }

                var sirka = 1;
                while (sirka < bunka.Stlpce && stlpec + sirka < stlpcov && !obsadene.Contains((r, stlpec + sirka)))
                {
                    sirka++;
                }

                var vyska = 1;
                while (vyska < bunka.Riadky && r + vyska < Riadky.Count
                       && Enumerable.Range(stlpec, sirka).All(s => !obsadene.Contains((r + vyska, s))))
                {
                    vyska++;
                }

                for (var d = 0; d < vyska; d++)
                {
                    for (var s = 0; s < sirka; s++)
                    {
                        obsadene.Add((r + d, stlpec + s));
                    }
                }

                zmena |= sirka != bunka.Stlpce || vyska != bunka.Riadky;
                riadok.Add(sirka == bunka.Stlpce && vyska == bunka.Riadky ? bunka : bunka with { Stlpce = sirka, Riadky = vyska });
                stlpec += sirka;
            }

            riadky.Add(riadok);
        }

        return zmena || PocetStlpcov < 1 ? this with { Riadky = riadky, PocetStlpcov = stlpcov } : this;
    }

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

/// <summary>Veľkosť strany papiera v mm (šírka × výška – strana na šírku má väčšiu šírku).</summary>
public sealed record RozlozenieStrany(double SirkaMm, double VyskaMm)
{
    /// <summary>Tolerancia porovnania veľkostí papiera (mm) – Word zaokrúhľuje rozmery na twipy.</summary>
    private const double Tolerancia = 3;

    public static RozlozenieStrany A4NaVysku { get; } = new(210, 297);

    public static RozlozenieStrany A4NaSirku { get; } = new(297, 210);

    public bool NaSirku => SirkaMm > VyskaMm;

    public bool JeA4NaVysku => Zhodne(A4NaVysku);

    public bool Zhodne(RozlozenieStrany ine) =>
        Math.Abs(SirkaMm - ine.SirkaMm) < Tolerancia && Math.Abs(VyskaMm - ine.VyskaMm) < Tolerancia;

    /// <summary>Napr. „A4 na šírku“ alebo „216 × 279 mm“.</summary>
    public string Popis =>
        Zhodne(A4NaVysku) ? "A4 na výšku"
        : Zhodne(A4NaSirku) ? "A4 na šírku"
        : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{SirkaMm:0} × {VyskaMm:0} mm");

    /// <summary>Rozloženie z rozmerov v twipoch (1/1440 palca) – ako ich zapisuje Word.</summary>
    public static RozlozenieStrany ZTwipov(double sirka, double vyska) => new(Math.Round(sirka / 56.6929, 1), Math.Round(vyska / 56.6929, 1));
}

/// <summary>
/// Od tohto miesta platí iné rozloženie strany a začína nová strana. Ako prvý blok dokumentu určuje rozloženie celého
/// dokumentu (napr. osnova na šírku). Dokument A4 na výšku tento blok na začiatku nemá.
/// </summary>
public sealed record Sekcia(RozlozenieStrany Rozlozenie) : Blok;

/// <summary>Nová strana (zlom strany z Wordu).</summary>
public sealed record ZlomStrany : Blok;

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
    private const string ZnackaZlomu = "ZLOM";
    private const string ZnackaVolnehoStlpca = "VOLNY";
    private const string ZnackaSekcie = "SEKCIA:";

    private static readonly SolidColorBrush Linka = Zmrazeny(Color.FromRgb(0xC5, 0xCC, 0xD6));
    private static readonly SolidColorBrush PodkladHlavicky = Zmrazeny(Color.FromRgb(0xF3, 0xF4, 0xF6));
    private static readonly SolidColorBrush ZnackaText = Zmrazeny(Color.FromRgb(0x8A, 0x94, 0xA3));

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

                case ZlomStrany:
                    ciel.Add(Znacka(ZnackaZlomu, "nová strana"));
                    break;

                case Sekcia sekcia:
                    ciel.Add(Znacka(ZnackaSekcie + Kod(sekcia.Rozlozenie), $"nová strana – {sekcia.Rozlozenie.Popis}"));
                    break;
            }
        }
    }

    /// <summary>Úzka tabuľka bude na celú šírku (pred pridaním alebo odstránením stĺpca v editore).</summary>
    public static void ZrusVolnyStlpec(Table tabulka)
    {
        foreach (var stlpec in tabulka.Columns.Where(c => Equals(c.Tag, ZnackaVolnehoStlpca)).ToList())
        {
            tabulka.Columns.Remove(stlpec);
            tabulka.BorderThickness = new Thickness(0.5);
        }
    }

    /// <summary>Značka zlomu strany alebo novej časti dokumentu v editore (sivý riadok s čiarou).</summary>
    private static Paragraph Znacka(string tag, string text) => new(new Run(text))
    {
        Tag = tag,
        FontFamily = new FontFamily(PismaDokumentu.Nadpis),
        FontSize = 11,
        Foreground = ZnackaText,
        TextAlignment = TextAlignment.Center,
        BorderBrush = Linka,
        BorderThickness = new Thickness(0, 1, 0, 0),
        Padding = new Thickness(0, 3, 0, 0),
        Margin = new Thickness(0, 14, 0, 14),
    };

    private static string Kod(RozlozenieStrany r) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{r.SirkaMm:0.#}x{r.VyskaMm:0.#}");

    private static RozlozenieStrany? ZKodu(string kod)
    {
        var casti = kod.Split('x');
        return casti.Length == 2
               && double.TryParse(casti[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sirka)
               && double.TryParse(casti[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var vyska)
               && sirka > 0 && vyska > 0
            ? new RozlozenieStrany(sirka, vyska)
            : null;
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
        t = t.Pravidelna();
        var tabulka = new Table { CellSpacing = 0, BorderBrush = Linka, BorderThickness = new Thickness(0.5), Margin = new Thickness(0, 6, 0, 10) };
        var podiel = t.SirkaVTexte;
        foreach (var sirka in t.PomerneSirky)
        {
            tabulka.Columns.Add(new TableColumn { Width = new GridLength(Math.Max(0.02, sirka * podiel), GridUnitType.Star) });
        }

        if (podiel < 0.97)
        {
            // úzka tabuľka: zvyšok šírky je prázdny stĺpec bez buniek, rámik majú len bunky
            tabulka.Columns.Add(new TableColumn { Width = new GridLength(1 - podiel, GridUnitType.Star), Tag = ZnackaVolnehoStlpca });
            tabulka.BorderThickness = new Thickness(0);
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
                case Paragraph { Tag: ZnackaZlomu }:
                    vysledok.Add(new ZlomStrany());
                    break;

                case Paragraph { Tag: string znacka } when znacka.StartsWith(ZnackaSekcie, StringComparison.Ordinal):
                    if (ZKodu(znacka[ZnackaSekcie.Length..]) is { } rozlozenie)
                    {
                        vysledok.Add(new Sekcia(rozlozenie));
                    }

                    break;

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

                    var stlpceTabulky = tabulka.Columns.Where(c => !Equals(c.Tag, ZnackaVolnehoStlpca)).ToList();
                    var volne = tabulka.Columns.Where(c => Equals(c.Tag, ZnackaVolnehoStlpca) && c.Width.IsStar).Sum(c => c.Width.Value);
                    var stlpce = Math.Max(stlpceTabulky.Count, riadky.Count == 0 ? 0 : riadky.Max(r => r.Sum(b => b.Stlpce)));
                    List<double>? sirky = null;
                    if (stlpceTabulky.Count == stlpce && stlpceTabulky.All(c => c.Width.IsStar || c.Width.IsAbsolute))
                    {
                        sirky = stlpceTabulky.Select(c => c.Width.Value).ToList();
                    }

                    var podiel = sirky is { Count: > 0 } && volne > 0 && stlpceTabulky.All(c => c.Width.IsStar) ? sirky.Sum() / (sirky.Sum() + volne) : 1;
                    if (riadky.Count > 0 && stlpce > 0)
                    {
                        vysledok.Add(new Tabulka(riadky, stlpce, sirky, podiel));
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

    // ---- rozloženie strán a prázdne miesta ----

    /// <summary>Rozloženie strany na začiatku dokumentu (bez bloku <see cref="Sekcia"/> A4 na výšku).</summary>
    public static RozlozenieStrany Rozlozenie(IReadOnlyList<Blok> bloky) =>
        bloky.Count > 0 && bloky[0] is Sekcia sekcia ? sekcia.Rozlozenie : RozlozenieStrany.A4NaVysku;

    /// <summary>Najširšia strana dokumentu – podľa nej sa nastaví šírka listu v náhľade.</summary>
    public static RozlozenieStrany NajsirsiaStrana(IReadOnlyList<Blok> bloky) =>
        bloky.OfType<Sekcia>().Select(s => s.Rozlozenie).Append(Rozlozenie(bloky)).MaxBy(r => r.SirkaMm)!;

    /// <summary>Časti dokumentu s jedným rozložením strany (rozdelené blokmi <see cref="Sekcia"/>, tie sa vynechajú).</summary>
    public static List<(RozlozenieStrany Rozlozenie, List<Blok> Bloky)> Casti(IReadOnlyList<Blok> bloky)
    {
        var casti = new List<(RozlozenieStrany, List<Blok>)> { (Rozlozenie(bloky), new List<Blok>()) };
        foreach (var blok in bloky)
        {
            if (blok is Sekcia sekcia)
            {
                if (casti[^1].Item2.Count == 0)
                {
                    casti[^1] = (sekcia.Rozlozenie, casti[^1].Item2);
                }
                else
                {
                    casti.Add((sekcia.Rozlozenie, new List<Blok>()));
                }
            }
            else
            {
                casti[^1].Item2.Add(blok);
            }
        }

        return casti;
    }

    /// <summary>
    /// Bez hluchých miest: viac prázdnych odsekov za sebou sa zlúči do jedného, prázdne odseky na začiatku, na konci,
    /// pri nadpise a pri novej strane sa vynechajú, opakované alebo zbytočné zlomy strany tiež (napr. zlom hneď na začiatku).
    /// Text, tabuľky ani rozloženie strán sa nemenia.
    /// </summary>
    public static List<Blok> Upratane(IReadOnlyList<Blok> bloky)
    {
        static bool Prazdny(Blok b) => b is Odsek o && o.Useky.All(u => u.Text.Trim().Length == 0);
        static bool Hranica(Blok b) => b is ZlomStrany or Sekcia or Nadpis;

        var vysledok = new List<Blok>();
        void OdstranPrazdneNaKonci()
        {
            while (vysledok.Count > 0 && Prazdny(vysledok[^1]))
            {
                vysledok.RemoveAt(vysledok.Count - 1);
            }
        }

        foreach (var blok in bloky)
        {
            if (Prazdny(blok))
            {
                if (vysledok.Count > 0 && !Prazdny(vysledok[^1]) && !Hranica(vysledok[^1]))
                {
                    vysledok.Add(blok);
                }

                continue;
            }

            if (Hranica(blok))
            {
                OdstranPrazdneNaKonci();
            }

            switch (blok)
            {
                // zlom na začiatku, za iným zlomom alebo za novou časťou (tá sama začína stranou) nič nerobí
                case ZlomStrany when vysledok.Count == 0 || vysledok[^1] is ZlomStrany or Sekcia:
                    continue;

                case Sekcia sekcia when vysledok.Count == 0:
                    if (!sekcia.Rozlozenie.JeA4NaVysku)
                    {
                        vysledok.Add(sekcia);
                    }

                    continue;

                case Sekcia sekcia when sekcia.Rozlozenie.Zhodne(AktualneRozlozenie(vysledok)):
                    // rovnaké rozloženie ako doteraz = len nová strana
                    if (vysledok[^1] is not (ZlomStrany or Sekcia))
                    {
                        vysledok.Add(new ZlomStrany());
                    }

                    continue;

                case Sekcia sekcia when vysledok[^1] is ZlomStrany:
                    vysledok[^1] = sekcia;
                    continue;
            }

            vysledok.Add(blok);
        }

        // na konci nemajú význam prázdne odseky ani zlomy strán
        OdstranPrazdneNaKonci();
        while (vysledok.Count > 1 && vysledok[^1] is ZlomStrany or Sekcia)
        {
            vysledok.RemoveAt(vysledok.Count - 1);
            OdstranPrazdneNaKonci();
        }

        if (vysledok.Count == 1 && vysledok[0] is ZlomStrany)
        {
            vysledok.Clear();
        }

        return vysledok;
    }

    private static RozlozenieStrany AktualneRozlozenie(List<Blok> bloky) =>
        bloky.OfType<Sekcia>().LastOrDefault()?.Rozlozenie ?? RozlozenieStrany.A4NaVysku;

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
