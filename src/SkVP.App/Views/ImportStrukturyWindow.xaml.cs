using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>
/// Vzdelávacie oblasti a predmety z webovej stránky (online) alebo z diktovaného / napísaného textu – s náhľadom toho,
/// čo program rozpoznal. Výsledok sa doplní k doterajším oblastiam alebo ich nahradí.
/// </summary>
public partial class ImportStrukturyWindow : Window
{
    public enum Rezim
    {
        Web,
        Diktovanie,
    }

    public sealed record OblastNahladu(string Nazov, IReadOnlyList<string> Predmety)
    {
        public string PocetText => $"  ({Predmety.Count})";
    }

    private readonly Rezim _rezim;
    private readonly DispatcherTimer _odklad = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public ImportStrukturyWindow(Rezim rezim)
    {
        _rezim = rezim;
        InitializeComponent();
        _odklad.Tick += (_, _) =>
        {
            _odklad.Stop();
            Rozpoznaj();
        };
        if (rezim == Rezim.Web)
        {
            Nadpis.Text = "Oblasti a predmety z webovej stránky";
            Popis.Text = "Zadajte adresu stránky (napr. učebný plán na webe školy) alebo priamo súboru PDF, Word, Excel, JSON či XML na internete. " +
                         "Program stránku načíta a vyhľadá v nej oblasti a predmety (tabuľka, nadpisy, zoznamy).";
            PanelDiktovania.Visibility = Visibility.Collapsed;
            PanelTextu.Visibility = Visibility.Collapsed;
            StlpecTextu.Width = new GridLength(0);
            Loaded += (_, _) => Adresa.Focus();
        }
        else
        {
            Nadpis.Text = "Diktovanie oblastí a predmetov";
            Popis.Text = "Kliknite na Diktovať a hovorte, napr. „Človek a príroda dvojbodka fyzika čiarka chémia čiarka biológia bodka“. " +
                         "Oblasť je pred dvojbodkou, predmety oddeľte čiarkou, ďalšiu oblasť začnite bodkou alebo novým riadkom. Text môžete aj napísať alebo vložiť.";
            PanelWebu.Visibility = Visibility.Collapsed;
            StavDiktovania.Text = HlasovePisanie.Slovencina is null
                ? "Pre diktovanie po slovensky pridajte v Nastaveniach Windows slovenskú klávesnicu (Čas a jazyk → Jazyk a oblasť)."
                : "Diktovanie používa hlasové písanie Windows (Win+H) – potrebuje internet.";
            Loaded += (_, _) => Text.Focus();
        }

        Aktualizuj(null, "");
    }

    /// <summary>Rozpoznané oblasti a predmety (null = nič).</summary>
    public UcebnyPlan? Plan { get; private set; }

    /// <summary>Nahradiť doterajšie oblasti (inak doplniť).</summary>
    public bool NahraditDoterajsie { get; private set; }

    /// <summary>Text na rozpoznanie (diktovanie, samotest).</summary>
    public string Vstup
    {
        get => Text.Text;
        set => Text.Text = value;
    }

    public string AdresaStranky
    {
        get => Adresa.Text;
        set => Adresa.Text = value;
    }

    private void Text_TextChanged(object sender, TextChangedEventArgs e)
    {
        _odklad.Stop();
        _odklad.Start();
    }

    /// <summary>Rozpozná oblasti a predmety z textu hneď (bez čakania).</summary>
    public void Rozpoznaj()
    {
        if (Text.Text.Trim().Length == 0)
        {
            Aktualizuj(null, "");
            return;
        }

        try
        {
            Aktualizuj(ImportStruktury.ZDiktovania(Text.Text), "");
        }
        catch (SkvpException ex)
        {
            Aktualizuj(null, ex.Message);
        }
    }

    private void Diktovat_Click(object sender, RoutedEventArgs e)
    {
        var slovencina = HlasovePisanie.Spusti(Text);
        StavDiktovania.Text = slovencina
            ? "Hovorte – Windows píše text do poľa. Ukončíte znova klávesmi Win+H alebo kliknutím mimo."
            : "Hovorte… Pre slovenčinu pridajte v Nastaveniach Windows slovenskú klávesnicu.";
    }

    private async void Nacitat_Click(object sender, RoutedEventArgs e) => await NacitajAsync();

    /// <summary>Načíta webovú stránku a rozpozná oblasti a predmety.</summary>
    public async Task NacitajAsync()
    {
        Nacitat.IsEnabled = false;
        Aktualizuj(null, "Načítavam stránku…");
        try
        {
            Aktualizuj(await ImportStruktury.ZWebuAsync(Adresa.Text), "");
        }
        catch (SkvpException ex)
        {
            Aktualizuj(null, ex.Message);
        }
        catch (Exception ex)
        {
            Aktualizuj(null, $"Stránku sa nepodarilo spracovať: {ex.GetBaseException().Message}");
        }
        finally
        {
            Nacitat.IsEnabled = true;
        }
    }

    private void Aktualizuj(UcebnyPlan? plan, string sprava)
    {
        Plan = plan;
        Nahlad.ItemsSource = plan?.Oblasti.Select(o => new OblastNahladu(o.Nazov, o.Predmety)).ToList();
        Chyba.Text = plan is null && sprava.Length == 0
            ? _rezim == Rezim.Web ? "Zadajte adresu a kliknite na Načítať." : "Zatiaľ nič – diktujte alebo napíšte text."
            : sprava;
        Chyba.Visibility = Chyba.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Suhrn.Text = plan is null ? "" : $"Oblastí: {plan.Oblasti.Count}, predmetov: {plan.PocetPredmetov}";
        Doplnit.IsEnabled = Nahradit.IsEnabled = plan is { Oblasti.Count: > 0 };
    }

    private void Doplnit_Click(object sender, RoutedEventArgs e)
    {
        NahraditDoterajsie = false;
        DialogResult = true;
    }

    private void Nahradit_Click(object sender, RoutedEventArgs e)
    {
        if (!Dialogy.Potvrd("Nahradiť doterajšie oblasti a predmety týmito? Pred zmenou sa urobí záloha."))
        {
            return;
        }

        NahraditDoterajsie = true;
        DialogResult = true;
    }
}
