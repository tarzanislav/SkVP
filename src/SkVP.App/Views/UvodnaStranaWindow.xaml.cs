using System.Windows;
using System.Windows.Media.Imaging;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>
/// Úprava úvodnej strany zostavy (každý riadok sa dá prepísať aj vypnúť, logo školy) a hlavičky ďalších strán
/// (text vľavo, názov školy a logo vpravo – každá časť sa dá vypnúť zvlášť alebo celá hlavička naraz).
/// </summary>
public partial class UvodnaStranaWindow : Window
{
    private bool _nacitane;

    /// <summary>Text hlavičky odvodený z nadpisu a podnadpisu – kým ho používateľ neprepíše, mení sa spolu s nimi.</summary>
    private string _automatickyText = "";

    /// <summary>Logo, ktoré sa uloží až tlačidlom Uložiť: null = bez zmeny, "" = odstrániť, inak cesta k novému obrázku.</summary>
    private string? _zmenaLoga;

    public UvodnaStranaWindow()
    {
        InitializeComponent();
        Zobraz(TitulnaStrana.Nacitaj());
        ZobrazLogo();
        _nacitane = true;
        ObnovUkazku();
    }

    /// <summary>Logo, ktoré bude platiť po uložení.</summary>
    private string? AktualneLogo => _zmenaLoga switch
    {
        null => NastaveniaSkoly.CestaLoga(),
        "" => null,
        var nove => nove,
    };

    private void Zobraz(TitulnaStrana t)
    {
        var nacitane = _nacitane;
        _nacitane = false;
        LogoZobrazit.IsChecked = t.LogoZobrazit;
        Skola.Text = t.Skola;
        SkolaZobrazit.IsChecked = t.SkolaZobrazit;
        Adresa.Text = t.Adresa;
        AdresaZobrazit.IsChecked = t.AdresaZobrazit;
        Nadpis.Text = t.Nadpis;
        NadpisZobrazit.IsChecked = t.NadpisZobrazit;
        Podnadpis.Text = t.Podnadpis;
        PodnadpisZobrazit.IsChecked = t.PodnadpisZobrazit;
        Typ.Text = t.Typ;
        TypZobrazit.IsChecked = t.TypZobrazit;
        Platnost.Text = t.Platnost;
        PlatnostZobrazit.IsChecked = t.PlatnostZobrazit;
        VlastneRiadky.Text = t.VlastneRiadky;
        OblastiZobrazit.IsChecked = t.OblastiZobrazit;
        Pata.Nacitaj(t.PataBloky);
        PataZobrazit.IsChecked = t.PataZobrazit;

        HlavickaZobrazit.IsChecked = t.HlavickaZobrazit;
        _automatickyText = t.Hlavicka;
        TextHlavicky.Text = t.TextHlavickyVysledny;
        TextHlavickyZobrazit.IsChecked = t.TextHlavickyZobrazit;
        SkolaVHlavickeZobrazit.IsChecked = t.SkolaVHlavickeZobrazit;
        LogoVHlavickeZobrazit.IsChecked = t.LogoVHlavickeZobrazit;
        _nacitane = nacitane;
        ObnovUkazku();
    }

    // ---- logo ----

    private void ZobrazLogo()
    {
        var obrazok = NacitajObrazok(AktualneLogo);
        Logo.Source = obrazok;
        UkazkaLogo.Source = obrazok;
        BezLoga.Visibility = obrazok is null ? Visibility.Visible : Visibility.Collapsed;
        OdstranitLogo.IsEnabled = obrazok is not null;
        ObnovUkazku();
    }

    private static BitmapImage? NacitajObrazok(string? cesta)
    {
        if (cesta is null)
        {
            return null;
        }

        try
        {
            var obrazok = new BitmapImage();
            obrazok.BeginInit();
            obrazok.CacheOption = BitmapCacheOption.OnLoad;
            obrazok.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            obrazok.UriSource = new Uri(cesta);
            obrazok.EndInit();
            obrazok.Freeze();
            return obrazok;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void VybratLogo_Click(object sender, RoutedEventArgs e)
    {
        var cesta = Dialogy.Otvorit("Vyberte logo školy", "Obrázky (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif");
        if (cesta is null)
        {
            return;
        }

        if (NastaveniaSkoly.Rozmer(cesta) is null)
        {
            Dialogy.Chyba("Súbor sa nedá načítať ako obrázok. Vyberte obrázok PNG, JPEG, BMP alebo GIF.");
            return;
        }

        _zmenaLoga = cesta;
        LogoZobrazit.IsChecked = true;
        ZobrazLogo();
    }

    private void OdstranitLogo_Click(object sender, RoutedEventArgs e)
    {
        _zmenaLoga = "";
        ZobrazLogo();
    }

    // ---- hlavička ----

    private void Nadpis_Zmena(object sender, RoutedEventArgs e)
    {
        if (!_nacitane)
        {
            return;
        }

        // text hlavičky, ktorý používateľ neprepísal, sleduje nadpis a podnadpis
        var novy = TitulnaStrana.SpojNazov(Nadpis.Text, Podnadpis.Text);
        if (TextHlavicky.Text.Trim() == _automatickyText || TextHlavicky.Text.Trim().Length == 0)
        {
            TextHlavicky.Text = novy;
        }

        _automatickyText = novy;
        ObnovUkazku();
    }

    private void TextHlavickyPodlaNadpisu_Click(object sender, RoutedEventArgs e)
    {
        TextHlavicky.Text = TitulnaStrana.SpojNazov(Nadpis.Text, Podnadpis.Text);
        TextHlavickyZobrazit.IsChecked = true;
    }

    private void Hlavicka_Zmena(object sender, RoutedEventArgs e) => ObnovUkazku();

    /// <summary>Ukážka hlavičky tak, ako bude na stranách (rovnaké pravidlá ako pri tlači).</summary>
    private void ObnovUkazku()
    {
        if (!_nacitane)
        {
            return;
        }

        var t = ZoZadania();
        var logo = AktualneLogo;
        var text = t.TextVHlavicke;
        var skola = t.SkolaVHlavicke;
        var sLogom = t.LogoVHlavicke(logo) is not null;
        UkazkaText.Text = text ?? "";
        UkazkaSkola.Text = skola ?? "";
        UkazkaSkola.Visibility = skola is null ? Visibility.Collapsed : Visibility.Visible;
        UkazkaLogo.Visibility = sLogom ? Visibility.Visible : Visibility.Collapsed;
        UkazkaLogo.Margin = new Thickness(skola is null ? 0 : 8, 0, 0, 0);
        var ma = t.MaHlavicku(logo);
        UkazkaHlavicky.Visibility = ma ? Visibility.Visible : Visibility.Hidden;
        UkazkaBezHlavicky.Visibility = ma ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- uloženie ----

    private void Predvolene_Click(object sender, RoutedEventArgs e)
    {
        var skola = Skola.Text;
        var adresa = Adresa.Text;
        var typ = Typ.Text;
        TitulnaStrana.Predvolene();
        var predvolene = TitulnaStrana.Nacitaj();
        predvolene.Skola = skola;
        predvolene.Adresa = adresa;
        predvolene.Typ = typ;
        Zobraz(predvolene);
    }

    /// <summary>Nastavenia úvodnej strany a hlavičky podľa okna (bez textu dole – ten sa číta z editora len pri uložení).</summary>
    private TitulnaStrana ZoZadania() => new()
    {
        LogoZobrazit = LogoZobrazit.IsChecked == true,
        Skola = Skola.Text,
        SkolaZobrazit = SkolaZobrazit.IsChecked == true,
        Adresa = Adresa.Text,
        AdresaZobrazit = AdresaZobrazit.IsChecked == true,
        Nadpis = Nadpis.Text,
        NadpisZobrazit = NadpisZobrazit.IsChecked == true,
        Podnadpis = Podnadpis.Text,
        PodnadpisZobrazit = PodnadpisZobrazit.IsChecked == true,
        Typ = Typ.Text,
        TypZobrazit = TypZobrazit.IsChecked == true,
        Platnost = Platnost.Text,
        PlatnostZobrazit = PlatnostZobrazit.IsChecked == true,
        VlastneRiadky = VlastneRiadky.Text,
        OblastiZobrazit = OblastiZobrazit.IsChecked == true,
        PataZobrazit = PataZobrazit.IsChecked == true,
        HlavickaZobrazit = HlavickaZobrazit.IsChecked == true,
        TextHlavicky = TextHlavicky.Text,
        TextHlavickyZobrazit = TextHlavickyZobrazit.IsChecked == true,
        SkolaVHlavickeZobrazit = SkolaVHlavickeZobrazit.IsChecked == true,
        LogoVHlavickeZobrazit = LogoVHlavickeZobrazit.IsChecked == true,
    };

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_zmenaLoga == "")
            {
                NastaveniaSkoly.OdstranLogo();
            }
            else if (_zmenaLoga is not null)
            {
                NastaveniaSkoly.NastavLogo(_zmenaLoga);
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Chyba(ex.Message);
            return;
        }

        var t = ZoZadania();
        t.PataBloky = Pata.Uloz();
        t.Uloz();
        DialogResult = true;
    }
}
