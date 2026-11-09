using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>
/// Vzhľad osnov – galéria šablón vzhľadu s náhľadom na skutočnom dokumente. Zvolená šablóna platí pre všetky
/// dokumenty (editor, PDF, Word, tlač); voliteľne sa zároveň uprace text dokumentov (s archiváciou pôvodných verzií).
/// </summary>
public partial class VzhladOsnovWindow : Window
{
    private readonly MainViewModel _hlavny;
    private readonly long? _dokument;
    private readonly DokumentService _dokumenty = new();
    private NacitanyDokument? _ukazka;
    private bool _pripravene;

    /// <param name="dokument">dokument, z ktorého sa okno otvorilo (ukáže sa v náhľade, dá sa upratať len on)</param>
    public VzhladOsnovWindow(MainViewModel hlavny, long? dokument = null)
    {
        _hlavny = hlavny;
        _dokument = dokument;
        InitializeComponent();

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
        NacitajUkazku();
    }

    /// <summary>Dokument na výber ukážky.</summary>
    public sealed record UkazkaDokumentu(long Id, string Nazov, string Farba, int DlzkaTextu = 0)
    {
        public override string ToString() => Nazov;
    }

    private SablonaVzhladu Vybrana => (Sablony.SelectedItem as SablonaVM)?.Sablona ?? VzhladDokumentov.Aktivna;

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

    private void Sablony_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sablony.SelectedItem is SablonaVM vm)
        {
            Editor.Sablona = vm.Sablona;
            AktualizujTlacidlo();
        }
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

        try
        {
            _ukazka = ukazka.Id == 0 ? null : _dokumenty.Nacitaj(ukazka.Id);
        }
        catch (SkvpException ex)
        {
            Zaznam.Upozornenie($"Vzhľad osnov: ukážku {ukazka.Nazov} sa nepodarilo načítať – {ex.Message}");
            _ukazka = null;
        }

        IReadOnlyList<Blok> bloky = _ukazka is { } nacitany && DokumentOsnovy.DlzkaTextu(nacitany.Bloky) > 0
            ? (Upratat.IsChecked == true && (_dokument is null || RozsahVsetky.IsChecked == true || ukazka.Id == _dokument)
                ? JednotnyVzhlad.Priprav(nacitany).Bloky
                : nacitany.Bloky)
            : UkazkovaOsnova();

        // miniatúry vo farbe oblasti dokumentu; výber šablóny zostane
        var vybrana = (Sablony.SelectedItem as SablonaVM)?.Sablona.Id ?? VzhladDokumentov.Aktivna.Id;
        var sablony = VzhladDokumentov.Vsetky.Select(s => new SablonaVM(s, ukazka.Farba, s.Id == VzhladDokumentov.Aktivna.Id)).ToList();
        Sablony.ItemsSource = sablony;
        var vm = sablony.First(s => s.Sablona.Id == vybrana);
        Sablony.SelectedItem = vm;
        Sablony.ScrollIntoView(vm);

        Editor.Farba = ukazka.Farba;
        Editor.Sablona = vm.Sablona;
        Editor.Nacitaj(bloky);
    }

    private void Pouzit_Click(object sender, RoutedEventArgs e)
    {
        var sablona = Vybrana;
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
        Zaznam.Info($"Vzhľad osnov: šablóna {sablona.Nazov}");
        _hlavny.Oznam(vysledok is null ? $"Vzhľad osnov: {sablona.Nazov}" : $"Vzhľad osnov: {sablona.Nazov} – {vysledok.Text}");
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

/// <summary>Šablóna v galérii – miniatúra listu vo farbe oblasti ukážky.</summary>
public sealed class SablonaVM(SablonaVzhladu sablona, string farba, bool zvolena)
{
    private static readonly Brush Atrament = VzhladDokumentov.Stetec("#1F2933");

    public SablonaVzhladu Sablona => sablona;
    public string Nazov => sablona.Nazov;
    public string Popis => sablona.Popis;
    public bool JeZvolena => zvolena;

    public string Pisma => sablona.PismoNadpisov == sablona.PismoTextu
        ? $"Písmo {sablona.PismoTextu} {sablona.VelkostTextu:0.#} b"
        : $"Písma {sablona.PismoNadpisov} a {sablona.PismoTextu} {sablona.VelkostTextu:0.#} b";

    public FontFamily PismoNadpisov => new(sablona.PismoNadpisov);
    public FontFamily PismoTextu => new(sablona.PismoTextu);
    public Brush FarbaNadpisu => sablona.FarbaNadpisu(1, farba) is { } f ? VzhladDokumentov.Stetec(f) : Atrament;
    public Brush Ozdoba => VzhladDokumentov.Stetec(sablona.FarbaOzdoby(farba));

    public Thickness HrubkaOzdoby => sablona.Ozdoba switch
    {
        OzdobaNadpisu.Ciara => new Thickness(0, 0, 0, 1.5),
        OzdobaNadpisu.Pasik or OzdobaNadpisu.Pas => new Thickness(4, 0, 0, 0),
        _ => new Thickness(0),
    };

    public Thickness OdsadenieNadpisu => sablona.Ozdoba switch
    {
        OzdobaNadpisu.Ciara => new Thickness(0, 0, 0, 3),
        OzdobaNadpisu.Pasik => new Thickness(8, 0, 0, 0),
        OzdobaNadpisu.Pas => new Thickness(8, 4, 6, 4),
        _ => new Thickness(0),
    };

    public Brush PodkladNadpisu => sablona.Ozdoba == OzdobaNadpisu.Pas ? VzhladDokumentov.Stetec(sablona.PodkladNadpisu(farba)) : Brushes.Transparent;
    public Brush PodkladHlavicky => VzhladDokumentov.Stetec(sablona.PodkladHlavicky(farba));
    public Brush PodkladPruhu => sablona.PodkladPruhu(farba) is { } p ? VzhladDokumentov.Stetec(p) : Brushes.Transparent;
    public Brush Linka => VzhladDokumentov.Stetec(sablona.Linka(farba));
    public Thickness LinkaBunky => sablona.Ram == RamTabulky.Vodorovny ? new Thickness(0, 0, 0, 1) : new Thickness(0.5);

    public Thickness HlavickaBunky => sablona.Ram == RamTabulky.Vodorovny ? new Thickness(0, 0, 0, 1.5) : LinkaBunky;

    public TextAlignment Zarovnanie => sablona.DoBloku ? TextAlignment.Justify : TextAlignment.Left;
}
