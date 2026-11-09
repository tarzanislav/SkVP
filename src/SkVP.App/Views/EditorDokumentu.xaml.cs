using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>
/// Editor textu osnovy na liste papiera. V náhľade je text len na čítanie, po zapnutí úprav sa objaví panel nástrojov.
/// Text vložený z Wordu sa očistí od cudzích písem a farieb, nadpisy sa rozpoznajú podľa veľkosti a hrúbky písma.
/// </summary>
public partial class EditorDokumentu : UserControl
{
    public static readonly DependencyProperty JeUpravaProperty = DependencyProperty.Register(
        nameof(JeUprava), typeof(bool), typeof(EditorDokumentu),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((EditorDokumentu)d).NastavRezim()));

    public static readonly DependencyProperty FarbaProperty = DependencyProperty.Register(
        nameof(Farba), typeof(string), typeof(EditorDokumentu),
        new PropertyMetadata("#3B5BDB", (d, _) => ((EditorDokumentu)d).NastavFarbu()));

    public static readonly DependencyProperty KompaktnyProperty = DependencyProperty.Register(
        nameof(Kompaktny), typeof(bool), typeof(EditorDokumentu), new PropertyMetadata(false, (d, _) => ((EditorDokumentu)d).NastavKompaktny()));

    /// <summary>Rozloženie strany celého dokumentu (napr. na šírku); null = A4 na výšku.</summary>
    private Sekcia? _uvodnaSekcia;

    private bool _nacitavam;
    private bool _menimStyl;
    private bool _nacitany;
    private SablonaVzhladu? _sablona;

    public EditorDokumentu()
    {
        InitializeComponent();
        DataObject.AddPastingHandler(Text, Vkladanie);
        Text.Document = DokumentOsnovy.Novy();
        NastavFarbu();
        // zmena šablóny vzhľadu v programe – otvorený dokument sa prekreslí
        Loaded += (_, _) =>
        {
            VzhladDokumentov.Zmenena -= PriZmeneVzhladu;
            VzhladDokumentov.Zmenena += PriZmeneVzhladu;
        };
        Unloaded += (_, _) => VzhladDokumentov.Zmenena -= PriZmeneVzhladu;
    }

    /// <summary>Šablóna vzhľadu len pre tento editor (náhľad šablóny); null = šablóna zvolená v programe.</summary>
    public SablonaVzhladu? Sablona
    {
        get => _sablona;
        set
        {
            _sablona = value;
            Prekresli();
        }
    }

    private void PriZmeneVzhladu()
    {
        if (_sablona is null)
        {
            Prekresli();
        }
    }

    /// <summary>Text sa zobrazí znova v aktuálnom vzhľade (šablóna, farba oblasti) – rozpracované zmeny zostanú.</summary>
    private void Prekresli()
    {
        if (!_nacitany)
        {
            return;
        }

        var zmeneny = JeZmeneny;
        var bloky = DokumentOsnovy.Rozober(Text.Document);
        if (_uvodnaSekcia is { } sekcia)
        {
            bloky.Insert(0, sekcia);
        }

        Nacitaj(bloky);
        JeZmeneny = zmeneny;
    }

    public bool JeUprava
    {
        get => (bool)GetValue(JeUpravaProperty);
        set => SetValue(JeUpravaProperty, value);
    }

    /// <summary>Menší list bez okrajov – na krátke texty v dialógových oknách.</summary>
    public bool Kompaktny
    {
        get => (bool)GetValue(KompaktnyProperty);
        set => SetValue(KompaktnyProperty, value);
    }

    private void NastavKompaktny()
    {
        if (!Kompaktny)
        {
            return;
        }

        Papier.MinHeight = 150;
        Papier.MaxWidth = double.PositiveInfinity;
        Papier.Padding = new Thickness(20, 16, 20, 16);
        PruhPapiera.Visibility = Visibility.Collapsed;
        PlochaStola.Margin = new Thickness(0, 8, 0, 8);
        Nastroje.Padding = new Thickness(0, 4, 0, 4);
    }

    public string Farba
    {
        get => (string)GetValue(FarbaProperty);
        set => SetValue(FarbaProperty, value);
    }

    /// <summary>Pri zapnutí úprav prázdneho dokumentu vloží nadpisy častí osnovy.</summary>
    public bool SablonaOsnovy { get; set; }

    /// <summary>Text pre prázdny dokument v náhľade.</summary>
    public string TextPrazdneho { get; set; } = "Zatiaľ bez textu. Zapnite Úpravu a začnite písať.";

    /// <summary>Dokument bol zmenený od posledného uloženia.</summary>
    public bool JeZmeneny { get; private set; }

    /// <summary>Používateľ zmenil text (na automatické uloženie).</summary>
    public event EventHandler? Zmenene;

    public FlowDocument Dokument => Text.Document;

    /// <summary>
    /// Zobrazí text (model načítaný z dokumentu Word) bez hluchých miest. List má veľkosť podľa rozloženia strany
    /// dokumentu (osnova na šírku = list na šírku); rozloženie sa pri uložení zachová.
    /// </summary>
    public void Nacitaj(IReadOnlyList<Blok> bloky)
    {
        _nacitavam = true;
        try
        {
            // krátke texty v dialógoch (napr. text dole na úvodnej strane) sa zobrazia presne, osnovy bez hluchých miest
            IReadOnlyList<Blok> upratane = Kompaktny ? bloky : DokumentOsnovy.Upratane(bloky);
            _uvodnaSekcia = upratane.FirstOrDefault() as Sekcia;
            NastavList(DokumentOsnovy.NajsirsiaStrana(upratane));
            var dokument = DokumentOsnovy.KDokumentu(_uvodnaSekcia is null ? upratane : upratane.Skip(1).ToList(), Farba, Sablona);
            Text.Document = dokument;
            JeZmeneny = false;
            _nacitany = true;
            Text.CaretPosition = dokument.ContentStart;
            AktualizujPrazdne();
        }
        finally
        {
            _nacitavam = false;
        }
    }

    /// <summary>Text na uloženie do dokumentu Word.</summary>
    public List<Blok> Uloz()
    {
        JeZmeneny = false;
        var bloky = DokumentOsnovy.Rozober(Text.Document);
        if (_uvodnaSekcia is { } sekcia)
        {
            bloky.Insert(0, sekcia);
        }

        return bloky;
    }

    /// <summary>Šírka a výška listu podľa veľkosti strany (najviac; na užšej obrazovke sa list zúži).</summary>
    private void NastavList(RozlozenieStrany rozlozenie)
    {
        if (Kompaktny)
        {
            return;
        }

        const double jednotiekNaMm = 96 / 25.4;
        Papier.MaxWidth = Math.Round(rozlozenie.SirkaMm * jednotiekNaMm);
        Papier.MinHeight = Math.Min(1000, Math.Round(rozlozenie.VyskaMm * jednotiekNaMm));
    }

    private bool JePrazdny =>
        Text.Document.Blocks.Count == 0 ||
        (Text.Document.Blocks.Count == 1 && Text.Document.Blocks.FirstBlock is Paragraph p && p.Inlines.Count == 0) ||
        new TextRange(Text.Document.ContentStart, Text.Document.ContentEnd).Text.Trim().Length == 0;

    private void NastavRezim()
    {
        var uprava = JeUprava;
        if (uprava && SablonaOsnovy && JePrazdny)
        {
            _nacitavam = true;
            Text.Document = DokumentOsnovy.KDokumentu(DokumentOsnovy.SablonaOsnovy(), Farba, Sablona);
            _nacitavam = false;
            JeZmeneny = false;
            if (Text.Document.Blocks.ElementAtOrDefault(1) is Paragraph prvy)
            {
                Text.CaretPosition = prvy.ContentStart;
            }
        }

        Text.IsReadOnly = !uprava;
        Text.IsReadOnlyCaretVisible = false;
        Nastroje.Visibility = uprava ? Visibility.Visible : Visibility.Collapsed;
        Text.Cursor = uprava ? Cursors.IBeam : Cursors.Arrow;
        AktualizujPrazdne();
        if (uprava)
        {
            Dispatcher.BeginInvoke(() => Text.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    /// <summary>Na užšom liste menšie okraje.</summary>
    private void Papier_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Kompaktny)
        {
            return;
        }

        var okraj = Math.Clamp((e.NewSize.Width - 420) / 5, 28, 76);
        Papier.Padding = new Thickness(okraj, 64, okraj - 4, 72);
        PruhPapiera.Margin = new Thickness(-(okraj - 30), 4, 0, 0);
    }

    private void NastavFarbu()
    {
        PruhPapiera.Background = FarbaNaStetec.Stetec(Farba);
        Text.SelectionBrush = FarbaNaStetec.Stetec(Farba, 0x55);
        Prekresli(); // nadpisy a tabuľky vo farbe oblasti
    }

    private void AktualizujPrazdne()
    {
        var prazdny = !JeUprava && JePrazdny;
        Prazdne.Text = TextPrazdneho;
        Prazdne.Visibility = prazdny ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Text_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_nacitavam || Text.IsReadOnly)
        {
            return;
        }

        JeZmeneny = true;
        Zmenene?.Invoke(this, EventArgs.Empty);
    }

    // ---- štýl odseku ----

    private List<Paragraph> VybraneOdseky()
    {
        var vysledok = new List<Paragraph>();
        var koniec = Text.Selection.End;
        var pozicia = Text.Selection.Start.Paragraph?.ContentStart ?? Text.Selection.Start;
        var kroky = 0;
        while (pozicia is not null && pozicia.CompareTo(koniec) <= 0 && kroky++ < 100000)
        {
            var odsek = pozicia.Paragraph;
            if (odsek is null)
            {
                pozicia = pozicia.GetNextInsertionPosition(LogicalDirection.Forward);
                continue;
            }

            if (!vysledok.Contains(odsek))
            {
                vysledok.Add(odsek);
            }

            pozicia = odsek.ContentEnd.GetNextInsertionPosition(LogicalDirection.Forward);
        }

        return vysledok;
    }

    private void Text_SelectionChanged(object sender, RoutedEventArgs e)
    {
        var odsek = Text.Selection.Start.Paragraph;
        var uroven = odsek is null ? 0 : DokumentOsnovy.UrovenNadpisu(odsek);
        _menimStyl = true;
        StylOdseku.SelectedIndex = uroven;
        _menimStyl = false;
    }

    private void StylOdseku_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_menimStyl || StylOdseku.SelectedItem is not ComboBoxItem { Tag: string tag } || Text.IsReadOnly)
        {
            return;
        }

        NastavStyl(int.Parse(tag, System.Globalization.CultureInfo.InvariantCulture));
    }

    private void NastavStyl(int uroven)
    {
        Text.BeginChange();
        try
        {
            foreach (var odsek in VybraneOdseky())
            {
                DokumentOsnovy.NastavNadpis(odsek, uroven, Farba, Sablona);
                if (uroven > 0)
                {
                    // nadpis má jednotný vzhľad – formát jednotlivých slov sa zruší
                    new TextRange(odsek.ContentStart, odsek.ContentEnd).ClearAllProperties();
                }
            }
        }
        finally
        {
            Text.EndChange();
        }

        Text.Focus();
    }

    private void Text_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Text.IsReadOnly)
        {
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None && ZaNadpisom())
        {
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        var uroven = e.Key switch
        {
            Key.D0 or Key.NumPad0 => 0,
            Key.D1 or Key.NumPad1 => 1,
            Key.D2 or Key.NumPad2 => 2,
            Key.D3 or Key.NumPad3 => 3,
            _ => -1,
        };
        if (uroven >= 0)
        {
            NastavStyl(uroven);
            e.Handled = true;
        }
    }

    /// <summary>Enter na konci nadpisu: nový odsek bežného textu (nie ďalší nadpis s pásom a farbou).</summary>
    private bool ZaNadpisom()
    {
        if (!Text.Selection.IsEmpty || Text.CaretPosition.Paragraph is not { } nadpis || DokumentOsnovy.UrovenNadpisu(nadpis) == 0
            || Text.CaretPosition.GetOffsetToPosition(nadpis.ContentEnd) != 0 || nadpis.Parent is not FlowDocument)
        {
            return false;
        }

        var novy = DokumentOsnovy.NovyOdsek(Sablona);
        Text.BeginChange();
        try
        {
            Text.Document.Blocks.InsertAfter(nadpis, novy);
        }
        finally
        {
            Text.EndChange();
        }

        Text.CaretPosition = novy.ContentStart;
        return true;
    }

    // ---- formát ----

    private void Vykonaj(RoutedUICommand prikaz)
    {
        prikaz.Execute(null, Text);
        Text.Focus();
    }

    private void Tucne_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.ToggleBold);
    private void Kurziva_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.ToggleItalic);
    private void Podciarknute_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.ToggleUnderline);
    private void Odrazky_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.ToggleBullets);
    private void Cislovanie_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.ToggleNumbering);
    private void ZmensitOdsadenie_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.DecreaseIndentation);
    private void ZvacsitOdsadenie_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.IncreaseIndentation);
    private void Vlavo_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.AlignLeft);
    private void Stred_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.AlignCenter);
    private void Blok_Click(object sender, RoutedEventArgs e) => Vykonaj(EditingCommands.AlignJustify);
    private void Spat_Click(object sender, RoutedEventArgs e) => Vykonaj(ApplicationCommands.Undo);
    private void Znova_Click(object sender, RoutedEventArgs e) => Vykonaj(ApplicationCommands.Redo);

    private void VycistitFormat_Click(object sender, RoutedEventArgs e)
    {
        Text.Selection.ClearAllProperties();
        Text.Focus();
    }

    // ---- tabuľky ----

    private void Tabulka_Click(object sender, RoutedEventArgs e)
    {
        if (TlacidloTabulky.ContextMenu is { } menu)
        {
            menu.PlacementTarget = TlacidloTabulky;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private void VlozitTabulku_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string rozmer } && rozmer.Split('x') is [var r, var s])
        {
            VlozTabulku(int.Parse(r, System.Globalization.CultureInfo.InvariantCulture), int.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public void VlozTabulku(int riadky, int stlpce)
    {
        var tabulka = DokumentOsnovy.NovaTabulka(Farba, Sablona);
        for (var s = 0; s < stlpce; s++)
        {
            tabulka.Columns.Add(new TableColumn());
        }

        var skupina = new TableRowGroup();
        for (var r = 0; r < riadky; r++)
        {
            skupina.Rows.Add(NovyRiadok(stlpce, r));
        }

        tabulka.RowGroups.Add(skupina);
        var dokument = Text.Document;
        var blok = BlokNaUrovniDokumentu(Text.CaretPosition);
        if (blok is null)
        {
            dokument.Blocks.Add(tabulka);
        }
        else
        {
            dokument.Blocks.InsertAfter(blok, tabulka);
        }

        if (tabulka.NextBlock is null)
        {
            dokument.Blocks.InsertAfter(tabulka, new Paragraph());
        }

        Text.CaretPosition = ((TableCell)skupina.Rows[0].Cells[0]).ContentStart;
        Text.Focus();
    }

    /// <summary>Nový riadok tabuľky (riadok 0 = hlavička).</summary>
    private TableRow NovyRiadok(int stlpce, int index)
    {
        var riadok = new TableRow();
        for (var s = 0; s < stlpce; s++)
        {
            riadok.Cells.Add(NovaBunka(index));
        }

        return riadok;
    }

    private TableCell NovaBunka(int riadok)
    {
        var bunka = new TableCell(new Paragraph { Margin = new Thickness(0) });
        DokumentOsnovy.StylBunky(bunka, riadok == 0, riadok, true, Farba, Sablona);
        return bunka;
    }

    private Block? BlokNaUrovniDokumentu(TextPointer pozicia)
    {
        DependencyObject? prvok = pozicia.Parent;
        Block? blok = null;
        while (prvok is TextElement element)
        {
            if (element is Block b && b.Parent is FlowDocument)
            {
                blok = b;
                break;
            }

            prvok = element.Parent;
        }

        return blok;
    }

    private TableCell? AktualnaBunka()
    {
        DependencyObject? prvok = Text.CaretPosition.Parent;
        while (prvok is TextElement element)
        {
            if (element is TableCell bunka)
            {
                return bunka;
            }

            prvok = element.Parent;
        }

        return null;
    }

    private void PridatRiadok_Click(object sender, RoutedEventArgs e)
    {
        if (AktualnaBunka() is { Parent: TableRow riadok } && riadok.Parent is TableRowGroup skupina)
        {
            var index = skupina.Rows.IndexOf(riadok) + 1;
            skupina.Rows.Insert(index, NovyRiadok(riadok.Cells.Count, Math.Max(1, index)));
        }
    }

    private void PridatStlpec_Click(object sender, RoutedEventArgs e)
    {
        if (AktualnaBunka() is not { Parent: TableRow riadok } bunka || riadok.Parent is not TableRowGroup { Parent: Table tabulka })
        {
            return;
        }

        var index = riadok.Cells.IndexOf(bunka) + 1;
        DokumentOsnovy.ZrusVolnyStlpec(tabulka);
        tabulka.Columns.Add(new TableColumn());
        foreach (var skupina in tabulka.RowGroups)
        {
            for (var i = 0; i < skupina.Rows.Count; i++)
            {
                var r = skupina.Rows[i];
                r.Cells.Insert(Math.Min(index, r.Cells.Count), NovaBunka(skupina == tabulka.RowGroups[0] ? i : Math.Max(1, i)));
            }
        }
    }

    private void OdstranitRiadok_Click(object sender, RoutedEventArgs e)
    {
        if (AktualnaBunka() is not { Parent: TableRow riadok } || riadok.Parent is not TableRowGroup { Parent: Table tabulka } skupina)
        {
            return;
        }

        skupina.Rows.Remove(riadok);
        if (tabulka.RowGroups.All(g => g.Rows.Count == 0))
        {
            Text.Document.Blocks.Remove(tabulka);
        }
    }

    private void OdstranitStlpec_Click(object sender, RoutedEventArgs e)
    {
        if (AktualnaBunka() is not { Parent: TableRow riadok } bunka || riadok.Parent is not TableRowGroup { Parent: Table tabulka })
        {
            return;
        }

        var index = riadok.Cells.IndexOf(bunka);
        DokumentOsnovy.ZrusVolnyStlpec(tabulka);
        foreach (var skupina in tabulka.RowGroups)
        {
            foreach (var r in skupina.Rows)
            {
                if (index < r.Cells.Count)
                {
                    r.Cells.RemoveAt(index);
                }
            }
        }

        if (tabulka.Columns.Count > 0)
        {
            tabulka.Columns.RemoveAt(tabulka.Columns.Count - 1);
        }

        if (tabulka.RowGroups.SelectMany(g => g.Rows).All(r => r.Cells.Count == 0))
        {
            Text.Document.Blocks.Remove(tabulka);
        }
    }

    // ---- vloženie textu z Wordu a iných programov ----

    private void Vkladanie(object sender, DataObjectPastingEventArgs e)
    {
        var data = e.SourceDataObject;
        if (data.GetDataPresent(DataFormats.XamlPackage) || !data.GetDataPresent(DataFormats.Rtf))
        {
            // text z tohto programu alebo čistý text – vloží sa bežne
            return;
        }

        try
        {
            if (data.GetData(DataFormats.Rtf) is not string rtf)
            {
                return;
            }

            var docasny = new FlowDocument();
            var rozsah = new TextRange(docasny.ContentStart, docasny.ContentEnd);
            using (var vstup = new MemoryStream(Encoding.Latin1.GetBytes(rtf)))
            {
                rozsah.Load(vstup, DataFormats.Rtf);
            }

            DokumentOsnovy.OcistiVlozene(docasny);
            var vystup = new MemoryStream();
            new TextRange(docasny.ContentStart, docasny.ContentEnd).Save(vystup, DataFormats.XamlPackage);
            vystup.Position = 0;
            var nove = new DataObject();
            nove.SetData(DataFormats.XamlPackage, vystup);
            e.DataObject = nove;
            e.FormatToApply = DataFormats.XamlPackage;
        }
        catch (Exception)
        {
            // ak sa formát nedá spracovať, vloží sa čistý text
            if (data.GetData(DataFormats.UnicodeText) is string text)
            {
                var nove = new DataObject();
                nove.SetData(DataFormats.UnicodeText, text);
                e.DataObject = nove;
                e.FormatToApply = DataFormats.UnicodeText;
            }
        }
    }
}
