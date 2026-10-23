using System.Windows;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>
/// Prvé spustenie v dvoch krokoch: 1. údaje o škole (organizácii) a logo, 2. s čím začať – vzdelávacie oblasti podľa
/// Štátneho vzdelávacieho programu pre gymnáziá (predmety z priečinka s osnovami), testovacia sada, učebný plán gymnázia,
/// tabuľka Excel alebo prázdny program.
/// </summary>
public partial class PrveSpustenieWindow : Window
{
    private readonly UcebnyPlan _gymnazium = UcebnyPlanService.Vstavany();
    private readonly UdajeOrganizacieVM _organizacia = new(UdajeOrganizacie.Nacitaj());

    public PrveSpustenieWindow()
    {
        InitializeComponent();
        Organizacia.DataContext = _organizacia;
        PopisGymnazia.Text = $"{_gymnazium.Typ} – {_gymnazium.Oblasti.Count} vzdelávacích oblastí a {_gymnazium.PocetPredmetov} predmetov z tabuľky školy, osnovy doplníte sami.";
        if (Services.TestovaciaSada.ZvolenaPriInstalacii)
        {
            VolbaTestovacia.IsChecked = true;
        }

        Loaded += (_, _) => Organizacia.ZamerajNazov();
    }

    /// <summary>Priečinok s učebnými osnovami na import po spustení (voľba podľa ŠVP), alebo null.</summary>
    public string? PriecinokOsnov { get; private set; }

    /// <summary>Údaje o organizácii vo formulári (samotest).</summary>
    public UdajeOrganizacieVM Udaje => _organizacia;

    /// <summary>Druhý krok (samotest).</summary>
    public void ZobrazKrok2() => Zobraz(2);

    private void Zobraz(int krok)
    {
        Krok1.Visibility = krok == 1 ? Visibility.Visible : Visibility.Collapsed;
        Krok2.Visibility = krok == 2 ? Visibility.Visible : Visibility.Collapsed;
        Spat.Visibility = krok == 2 ? Visibility.Visible : Visibility.Collapsed;
        Dalej.Content = krok == 2 ? "Začať" : "Ďalej";
        Krok.Text = $"Krok {krok} z 2";
    }

    private void Spat_Click(object sender, RoutedEventArgs e) => Zobraz(1);

    private void Dalej_Click(object sender, RoutedEventArgs e)
    {
        if (Krok1.Visibility == Visibility.Visible)
        {
            if (_organizacia.Nazov.Trim().Length == 0
                && !Dialogy.Potvrd("Názov školy nie je vyplnený. Pokračovať aj tak? Doplníte ho neskôr v Nastaveniach."))
            {
                return;
            }

            Zobraz(2);
            return;
        }

        Zacat();
    }

    private void Logo_Click(object sender, RoutedEventArgs e)
    {
        var cesta = Dialogy.Otvorit("Logo školy", "Obrázky (*.png;*.jpg;*.jpeg;*.svg;*.bmp)|*.png;*.jpg;*.jpeg;*.svg;*.bmp");
        if (cesta is null)
        {
            return;
        }

        try
        {
            NastaveniaSkoly.NastavLogo(cesta);
            StavLoga.Text = $"Logo: {System.IO.Path.GetFileName(cesta)}";
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    private void Priecinok_Click(object sender, RoutedEventArgs e)
    {
        if (Dialogy.VybratPriecinok("Priečinok s učebnými osnovami (Word alebo PDF)") is { } priecinok)
        {
            PriecinokOsnov = priecinok;
            Priecinok.Text = $"Priečinok s učebnými osnovami: {priecinok}";
        }
    }

    /// <summary>Uloží údaje o škole a vytvorí východiskovú štruktúru podľa voľby.</summary>
    public void Zacat()
    {
        try
        {
            _organizacia.Udaje.Uloz();
            if (VolbaSvp.IsChecked == true)
            {
                SvpGymnazia.VytvorOblasti();
            }
            else if (VolbaTestovacia.IsChecked == true)
            {
                UcebnyPlanService.Vytvor(_gymnazium, nahradit: false);
                Dialogy.Cakaj(() => Services.TestovaciaSada.Nainstaluj());
            }
            else if (VolbaGymnazium.IsChecked == true)
            {
                UcebnyPlanService.Vytvor(_gymnazium, nahradit: false);
                SvpGymnazia.VytvorOblasti(); // charakteristiky oblastí zo ŠVP
            }
            else if (VolbaExcel.IsChecked == true)
            {
                var cesta = Dialogy.Otvorit("Tabuľka so vzdelávacími oblasťami a predmetmi", "Tabuľky Excel (*.xlsx)|*.xlsx");
                if (cesta is null)
                {
                    return;
                }

                UcebnyPlanService.Vytvor(UcebnyPlanService.NacitajZExcelu(cesta), nahradit: false);
            }

            if (VolbaSvp.IsChecked != true)
            {
                PriecinokOsnov = null;
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
            return;
        }

        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            Close(); // okno nebolo otvorené ako dialóg (samotest)
        }
    }

    /// <summary>Voľba štartu (samotest): svp, testovacia, gymnazium, prazdny.</summary>
    public void Zvol(string volba)
    {
        (volba switch
        {
            "testovacia" => VolbaTestovacia,
            "gymnazium" => VolbaGymnazium,
            "prazdny" => VolbaPrazdny,
            _ => VolbaSvp,
        }).IsChecked = true;
    }
}
