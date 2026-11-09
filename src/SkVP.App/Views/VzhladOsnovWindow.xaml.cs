using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>
/// Vzhľad osnov – galéria šablón vzhľadu (podľa skupín) s náhľadom na skutočnom dokumente a prispôsobenie vybranej
/// šablóny (farba, písma, veľkosť, zarovnanie). Zvolená šablóna platí pre všetky dokumenty (editor, PDF, Word, tlač);
/// voliteľne sa zároveň uprace text dokumentov (s archiváciou pôvodných verzií).
/// </summary>
public partial class VzhladOsnovWindow : Window
{
    private const string VsetkyZobrazit = "Všetky";

    /// <summary>Písma Windows, ktoré sa dajú vložiť do PDF – ponuka pri prispôsobení.</summary>
    private static readonly string[] PonukaPisiem =
    {
        "Segoe UI", "Calibri", "Candara", "Corbel", "Trebuchet MS", "Franklin Gothic Medium", "Verdana", "Tahoma", "Arial",
        "Constantia", "Georgia", "Palatino Linotype", "Times New Roman",
    };

    private readonly MainViewModel _hlavny;
    private readonly long? _dokument;
    private readonly DokumentService _dokumenty = new();
    private readonly List<FarbaVolba> _farby;
    private string _skupina = VsetkyZobrazit;
    private SablonaVzhladu _vybrana = VzhladDokumentov.Aktivna;
    private SablonaVzhladu? _upravena;
    private string _farbaUkazky = VzhladDokumentov.PredvolenaOblast;
    private bool _pripravene;
    private bool _nastavujem;

    /// <param name="dokument">dokument, z ktorého sa okno otvorilo (ukáže sa v náhľade, dá sa upratať len on)</param>
    public VzhladOsnovWindow(MainViewModel hlavny, long? dokument = null)
    {
        _hlavny = hlavny;
        _dokument = dokument;
        InitializeComponent();

        // ponuky prispôsobenia
        _farby = FarbaVolba.Ponuka(Prisposob_Zmena);
        Farby.ItemsSource = _farby;
        var pisma = PonukaPisiem.Where(VzhladDokumentov.JePouzitelne).ToList();
        PismoNadpisov.ItemsSource = pisma;
        PismoTextu.ItemsSource = pisma;
        Velkost.ItemsSource = Enumerable.Range(0, 8).Select(i => 9.5 + i * 0.5)
            .Select(v => new ComboBoxItem { Content = $"{v.ToString("0.#", CultureInfo.CurrentCulture)} b", Tag = v }).ToList();

        // skupiny šablón
        foreach (var skupina in VzhladDokumentov.Skupiny.Prepend(VsetkyZobrazit))
        {
            var filter = new RadioButton
            {
                Content = skupina, GroupName = "Skupina", IsChecked = skupina == VsetkyZobrazit, Padding = new Thickness(11, 3, 11, 4),
                Margin = new Thickness(0, 0, 6, 6), Style = TryFindResource(typeof(ToggleButton)) as Style,
            };
            filter.Checked += (_, _) =>
            {
                _skupina = skupina;
                ObnovGaleriu();
            };
            Filtre.Children.Add(filter);
        }

        var ukazky = new List<UkazkaDokumentu> { new(0, "Ukážková osnova", VzhladDokumentov.PredvolenaOblast) };
        foreach (var skupina in hlavny.VsetkySkupiny)
        {
            if (skupina.JeOblast)
            {
                ukazky.Add(new UkazkaDokumentu(skupina.Id, $"Charakteristika – {skupina.Nazov}", skupina.Farba));
            }

            ukazky.AddRange(skupina.Predmety.Select(p => new UkazkaDokumentu(p.Id, $"{skupina.Nazov} – {p.Nazov}", skupina.Farba, p.DlzkaTextu)));
        }

        Ukazky.ItemsSource = ukazky;
        Ukazky.SelectedItem = ukazky.FirstOrDefault(u => u.Id == dokument)
                              ?? ukazky.FirstOrDefault(u => u.DlzkaTextu > 0)
                              ?? ukazky[0];
        _pripravene = true;
        NastavPrisposobenie(_vybrana);
        NacitajUkazku();
    }

    /// <summary>Dokument na výber ukážky.</summary>
    public sealed record UkazkaDokumentu(long Id, string Nazov, string Farba, int DlzkaTextu = 0)
    {
        public override string ToString() => Nazov;
    }

    /// <summary>Šablóna, ktorá sa použije – upravená alebo vybraná v galérii.</summary>
    public SablonaVzhladu Vysledna => _upravena ?? _vybrana;

    // ---- galéria ----

    /// <summary>Miniatúry šablón vybranej skupiny vo farbe oblasti ukážky; výber zostane.</summary>
    private void ObnovGaleriu()
    {
        var vsetky = VzhladDokumentov.VGalerii;
        var vyber = vsetky.Where(s => _skupina == VsetkyZobrazit || s.Skupina == _skupina).ToList();
        var aktivna = VzhladDokumentov.Aktivna.Id;
        var sablony = vyber.Select(s => new SablonaVM(s, _farbaUkazky, s.Id == aktivna)).ToList();
        _nastavujem = true;
        try
        {
            Sablony.ItemsSource = sablony;
            Sablony.SelectedItem = sablony.FirstOrDefault(s => s.Sablona.Id == _vybrana.Id);
        }
        finally
        {
            _nastavujem = false;
        }

        if (Sablony.SelectedItem is { } vybrana)
        {
            Sablony.ScrollIntoView(vybrana);
        }

        PocetSablon.Text = _skupina == VsetkyZobrazit
            ? Preklad.Pocet(vsetky.Count, "{0} šablóna vzhľadu", "{0} šablóny vzhľadu", "{0} šablón vzhľadu")
            : $"{_skupina}: {Preklad.Pocet(vyber.Count, "{0} šablóna", "{0} šablóny", "{0} šablón")} z {vsetky.Count}";
    }

    private void Sablony_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_nastavujem || Sablony.SelectedItem is not SablonaVM vm)
        {
            return;
        }

        _vybrana = vm.Sablona;
        _upravena = null;
        NastavPrisposobenie(_vybrana);
        ZobrazSablonu();
    }

    private void ZobrazSablonu()
    {
        Editor.Sablona = Vysledna;
        StitokVlastnej.Visibility = _upravena is null ? Visibility.Collapsed : Visibility.Visible;
        AktualizujTlacidlo();
    }

    // ---- prispôsobenie ----

    /// <summary>Ovládače prispôsobenia podľa šablóny (bez vytvorenia vlastnej).</summary>
    private void NastavPrisposobenie(SablonaVzhladu sablona)
    {
        _nastavujem = true;
        try
        {
            var farba = sablona.Farba == FarbaNadpisov.Akcent ? sablona.Akcent : null;
            foreach (var volba in _farby)
            {
                volba.Vybrana = sablona.Farba == FarbaNadpisov.Tmava ? volba.Hex == FarbaVolba.Grafitova
                    : string.Equals(volba.Hex, farba, StringComparison.OrdinalIgnoreCase);
            }

            PismoNadpisov.SelectedItem = PismoNadpisov.Items.Cast<string>().FirstOrDefault(p => p == sablona.PismoNadpisov);
            PismoTextu.SelectedItem = PismoTextu.Items.Cast<string>().FirstOrDefault(p => p == sablona.PismoTextu);
            Velkost.SelectedItem = Velkost.Items.Cast<ComboBoxItem>().MinBy(i => Math.Abs((double)i.Tag - sablona.VelkostTextu));
            DoBloku.IsChecked = sablona.DoBloku;
        }
        finally
        {
            _nastavujem = false;
        }
    }

    private void Prisposob_Zmena(object sender, RoutedEventArgs e)
    {
        if (_nastavujem || !_pripravene)
        {
            return;
        }

        // farba, na ktorú sa práve kliklo (predchádzajúca môže byť ešte označená)
        var farba = sender as FarbaVolba ?? _farby.FirstOrDefault(f => f.Vybrana);
        _upravena = VzhladDokumentov.Prisposob(_vybrana,
            farba is null ? _vybrana.Farba == FarbaNadpisov.Akcent ? _vybrana.Akcent : null : farba.Hex,
            PismoNadpisov.SelectedItem as string ?? _vybrana.PismoNadpisov,
            PismoTextu.SelectedItem as string ?? _vybrana.PismoTextu,
            Velkost.SelectedItem is ComboBoxItem { Tag: double velkost } ? velkost : _vybrana.VelkostTextu,
            DoBloku.IsChecked == true);
        ZobrazSablonu();
    }

    private void Povodna_Click(object sender, RoutedEventArgs e)
    {
        _upravena = null;
        NastavPrisposobenie(_vybrana);
        ZobrazSablonu();
    }

    // ---- ukážka a upratanie ----

    private void Ukazky_SelectionChanged(object sender, SelectionChangedEventArgs e) => NacitajUkazku();

    private void Upratat_Zmena(object sender, RoutedEventArgs e)
    {
        if (!_pripravene)
        {
            return;
        }

        var upratat = Upratat.IsChecked == true;
        PanelRozsahu.Visibility = upratat && _dokument is not null ? Visibility.Visible : Visibility.Collapsed;
        InfoUpratania.Visibility = upratat ? Visibility.Visible : Visibility.Collapsed;
        AktualizujTlacidlo();
        NacitajUkazku();
    }

    private void AktualizujTlacidlo() =>
        PouzitText.Text = Upratat.IsChecked == true && _dokument is not null && RozsahTento.IsChecked == true
            ? "Použiť a upratať tento dokument"
            : Upratat.IsChecked == true ? "Použiť a upratať všetky dokumenty" : "Použiť pre všetky dokumenty";

    /// <summary>Vybraný dokument v náhľade (po upratovaní, ak je zapnuté) a miniatúry šablón vo farbe jeho oblasti.</summary>
    private void NacitajUkazku()
    {
        if (!_pripravene || Ukazky.SelectedItem is not UkazkaDokumentu ukazka)
        {
            return;
        }

        NacitanyDokument? nacitany;
        try
        {
            nacitany = ukazka.Id == 0 ? null : _dokumenty.Nacitaj(ukazka.Id);
        }
        catch (SkvpException ex)
        {
            Zaznam.Upozornenie($"Vzhľad osnov: ukážku {ukazka.Nazov} sa nepodarilo načítať – {ex.Message}");
            nacitany = null;
        }

        IReadOnlyList<Blok> bloky = nacitany is not null && DokumentOsnovy.DlzkaTextu(nacitany.Bloky) > 0
            ? (Upratat.IsChecked == true && (_dokument is null || RozsahVsetky.IsChecked == true || ukazka.Id == _dokument)
                ? JednotnyVzhlad.Priprav(nacitany).Bloky
                : nacitany.Bloky)
            : UkazkovaOsnova();

        _farbaUkazky = ukazka.Farba;
        ObnovGaleriu();
        Editor.Farba = ukazka.Farba;
        Editor.Sablona = Vysledna;
        Editor.Nacitaj(bloky);
        ZobrazSablonu();
    }

    private void Pouzit_Click(object sender, RoutedEventArgs e)
    {
        var sablona = Vysledna;
        VysledokUpratania? vysledok = null;
        if (Upratat.IsChecked == true)
        {
            var dokumenty = _dokument is long jeden && RozsahTento.IsChecked == true
                ? new List<long> { jeden }
                : _hlavny.VsetkySkupiny.SelectMany(s => s.Predmety.Select(p => p.Id).Prepend(s.Id)).ToList();
            if (dokumenty.Count > 1 && !Dialogy.Potvrd(
                    $"Upratať text všetkých dokumentov osnov ({dokumenty.Count})?\n\nNadpisy písané tučne alebo veľkými písmenami sa zmenia na skutočné nadpisy, " +
                    "vynechajú sa prázdne riadky a zbytočné zlomy strán, tabuľky s tučným prvým riadkom dostanú hlavičku.\n\n" +
                    "Pôvodné verzie sa uložia do archívu dokumentov – dajú sa obnoviť tlačidlom Archív pri dokumente."))
            {
                return;
            }

            try
            {
                Dialogy.Cakaj(() => vysledok = JednotnyVzhlad.Uprac(dokumenty, _hlavny.JeVoWorde, _hlavny.OznamPrepisanie));
            }
            catch (Exception ex)
            {
                Dialogy.Chyba("Text dokumentov sa nepodarilo upratať celý. Upratané dokumenty zostali uložené, ostatné sú bez zmeny.", ex);
            }
        }

        VzhladDokumentov.Nastav(sablona);
        var nazov = sablona.Id == VzhladDokumentov.IdVlastnej ? $"{sablona.Nazov} ({sablona.Popis.TrimEnd('.')})" : sablona.Nazov;
        Zaznam.Info($"Vzhľad osnov: šablóna {nazov}");
        _hlavny.Oznam(vysledok is null ? $"Vzhľad osnov: {nazov}" : $"Vzhľad osnov: {nazov} – {vysledok.Text}");
        DialogResult = true;
    }

    /// <summary>Ukážka, keď vybraný dokument ešte nemá text – nadpisy, odsek, zoznam a tabuľka s hlavičkou.</summary>
    public static List<Blok> UkazkovaOsnova()
    {
        static Odsek T(string text, bool tucne = false) => new(new[] { new Usek(text, Tucne: tucne) });
        static Bunka B(string text, bool hlavicka = false) => new(new Blok[] { T(text) }, Hlavicka: hlavicka);
        return new List<Blok>
        {
            new Nadpis(1, "Charakteristika predmetu"),
            T("Predmet rozvíja komunikačné kompetencie žiakov, ich čitateľskú gramotnosť a schopnosť kriticky pracovať s informáciami. " +
              "Žiaci sa učia formulovať vlastné názory, argumentovať a rešpektovať názory iných."),
            new Nadpis(1, "Ciele predmetu"),
            T("Žiak:"),
            new Zoznam(false, new IReadOnlyList<Blok>[]
            {
                new Blok[] { T("vyhľadá, porovná a overí informácie z viacerých zdrojov,") },
                new Blok[] { T("vytvorí súvislý text podľa komunikačnej situácie,") },
                new Blok[] { T("zhodnotí vlastnú prácu a navrhne jej zlepšenie.") },
            }),
            new Nadpis(1, "Rozpis učiva podľa ročníkov"),
            new Nadpis(2, "1. ročník"),
            new Tabulka(new IReadOnlyList<Bunka>[]
            {
                new[] { B("Tematický celok", true), B("Výkonový štandard", true), B("Hodiny", true) },
                new[] { B("Práca s textom"), B("Žiak určí tému a hlavnú myšlienku textu."), B("12") },
                new[] { B("Sloh a komunikácia"), B("Žiak napíše úvahu s jasnou argumentáciou."), B("10") },
                new[] { B("Jazyk a reč"), B("Žiak správne používa spisovné tvary slov."), B("8") },
                new[] { B("Literatúra"), B("Žiak porovná dve literárne diela."), B("14") },
            }, 3, new double[] { 0.3, 0.55, 0.15 }),
            new Nadpis(1, "Hodnotenie predmetu"),
            T("Žiak je hodnotený priebežne – ústne, písomne aj za projekty. Výsledné hodnotenie zohľadňuje aj aktivitu a pokrok žiaka."),
        };
    }
}

/// <summary>Farba na výber pri prispôsobení šablóny (null = farby vzdelávacích oblastí).</summary>
public sealed class FarbaVolba(string? hex, string nazov, Brush stetec, Action<object, RoutedEventArgs> priVybere) : ObservableObject
{
    public const string Grafitova = "#374151";

    private bool _vybrana;

    public string? Hex => hex;
    public string Nazov => nazov;
    public Brush Stetec => stetec;

    public bool Vybrana
    {
        get => _vybrana;
        set
        {
            if (SetProperty(ref _vybrana, value) && value)
            {
                priVybere(this, new RoutedEventArgs());
            }
        }
    }

    /// <summary>Farby oblastí (dúha pastelových farieb) a sýte farby, na ktorých je biely text dobre čitateľný.</summary>
    public static List<FarbaVolba> Ponuka(Action<object, RoutedEventArgs> priVybere)
    {
        var duha = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        for (var i = 0; i < Pastel.Oblasti.Length; i++)
        {
            duha.GradientStops.Add(new GradientStop(VzhladDokumentov.ZHex(Pastel.Oblasti[i]), i / (double)(Pastel.Oblasti.Length - 1)));
        }

        duha.Freeze();
        var volby = new List<FarbaVolba> { new(null, "Farby vzdelávacích oblastí", duha, priVybere) };
        volby.AddRange(new (string Hex, string Nazov)[]
        {
            ("#0E7C7B", "tyrkysová"), ("#1D5D9B", "oceánska modrá"), ("#2E5E9E", "modrá"), ("#4338CA", "indigová"),
            ("#6A48B0", "fialová"), ("#A23B72", "malinová"), ("#C2503B", "korálová"), ("#B7791F", "jantárová"),
            ("#23784B", "smaragdová"), ("#5B7B2A", "olivová"), ("#7A5230", "hnedá"), ("#7A2E35", "bordová"), (Grafitova, "grafitová"),
        }.Select(f => new FarbaVolba(f.Hex, f.Nazov, VzhladDokumentov.Stetec(f.Hex), priVybere)));
        return volby;
    }
}

/// <summary>Šablóna v galérii – miniatúra listu vo farbe oblasti ukážky.</summary>
public sealed class SablonaVM(SablonaVzhladu sablona, string farba, bool zvolena)
{
    public SablonaVzhladu Sablona => sablona;
    public string Nazov => sablona.Nazov;
    public string Popis => sablona.Popis;
    public bool JeZvolena => zvolena;

    public string Pisma => sablona.PismoNadpisov == sablona.PismoTextu
        ? $"{sablona.Skupina} · {sablona.PismoTextu} {sablona.VelkostTextu:0.#} b"
        : $"{sablona.Skupina} · {sablona.PismoNadpisov} a {sablona.PismoTextu} {sablona.VelkostTextu:0.#} b";

    public FontFamily PismoNadpisov => new(sablona.PismoNadpisov);
    public FontFamily PismoTextu => new(sablona.PismoTextu);
    public Brush FarbaTextu => VzhladDokumentov.Stetec(sablona.FarbaTextu);
    public Brush FarbaNadpisu => VzhladDokumentov.Stetec(sablona.FarbaNadpisu(1, farba) ?? sablona.FarbaTextu);
    public Brush Ozdoba => VzhladDokumentov.Stetec(sablona.FarbaOzdoby(farba));

    public Thickness HrubkaOzdoby => sablona.Ozdoba switch
    {
        OzdobaNadpisu.Ciara => new Thickness(0, 0, 0, 1.5),
        OzdobaNadpisu.Pasik or OzdobaNadpisu.Pas or OzdobaNadpisu.Karta => new Thickness(4, 0, 0, 0),
        OzdobaNadpisu.Nadciarka => new Thickness(0, 2.5, 0, 0),
        OzdobaNadpisu.Ramik => new Thickness(1.2),
        _ => new Thickness(0),
    };

    public Thickness OdsadenieNadpisu => sablona.Ozdoba switch
    {
        OzdobaNadpisu.Ciara => new Thickness(0, 0, 0, 3),
        OzdobaNadpisu.Pasik => new Thickness(8, 0, 0, 0),
        OzdobaNadpisu.Pas or OzdobaNadpisu.Karta => new Thickness(8, 4, 6, 4),
        OzdobaNadpisu.Nadciarka => new Thickness(0, 5, 0, 0),
        OzdobaNadpisu.Ramik => new Thickness(7, 3, 6, 3),
        _ => new Thickness(0),
    };

    public Brush PodkladNadpisu => sablona.PodkladNadpisu(farba) is { } p ? VzhladDokumentov.Stetec(p) : Brushes.Transparent;
    public Brush PodkladHlavicky => VzhladDokumentov.Stetec(sablona.PodkladHlavicky(farba));
    public Brush TextHlavicky => VzhladDokumentov.Stetec(sablona.TextHlavicky ?? sablona.FarbaTextu);
    public Brush PodkladPruhu => sablona.PodkladPruhu(farba) is { } p ? VzhladDokumentov.Stetec(p) : Brushes.Transparent;
    public Brush Linka => VzhladDokumentov.Stetec(sablona.Linka(farba));

    public Thickness LinkaBunky => sablona.Ram switch
    {
        RamTabulky.Vodorovny => new Thickness(0, 0, 0, 1),
        RamTabulky.Akademicky => new Thickness(0),
        _ => new Thickness(0.5),
    };

    public Thickness HlavickaBunky => sablona.Ram switch
    {
        RamTabulky.Vodorovny => new Thickness(0, 0, 0, 1.5),
        RamTabulky.Akademicky => new Thickness(0, 0, 0, 0.8),
        _ => LinkaBunky,
    };

    /// <summary>Čiary nad a pod celou tabuľkou (akademická tabuľka).</summary>
    public Thickness RamMiniTabulky => sablona.Ram == RamTabulky.Akademicky ? new Thickness(0, 1.5, 0, 1.5) : new Thickness(0);

    public TextAlignment Zarovnanie => sablona.DoBloku ? TextAlignment.Justify : TextAlignment.Left;
}
