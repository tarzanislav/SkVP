using System.IO;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>Nastavenie diktovania: hlasové písanie Windows alebo prepis v počítači (výber, stiahnutie a vymazanie modelu).</summary>
public partial class DiktovanieWindow : Window
{
    /// <summary>Riadok zoznamu modelov; zmena výberu hneď prispôsobí tlačidlá okna.</summary>
    public sealed class VolbaModelu(ModelHlasu model, Action priZmeneVyberu) : ObservableObject
    {
        private bool _vybrany;

        public ModelHlasu Model { get; } = model;
        public string Nazov => Model.Nazov;
        public string Velkost => $"· {Model.VelkostMb} MB";
        public string Popis => Model.Popis;

        public string Stav => Model.JeZEvity ? Preklad.T("stiahnutý v programe EviTA – použije sa aj tu")
            : Model.JeStiahnuty ? Preklad.T("stiahnutý")
            : "";

        public bool Vybrany
        {
            get => _vybrany;
            set
            {
                if (SetProperty(ref _vybrany, value))
                {
                    priZmeneVyberu();
                }
            }
        }

        /// <summary>Po stiahnutí alebo vymazaní modelu.</summary>
        public void ObnovStav() => OnPropertyChanged(nameof(Stav));
    }

    private readonly List<VolbaModelu> _modely;
    private CancellationTokenSource? _stahovanie;

    public DiktovanieWindow()
    {
        InitializeComponent();
        var ulozeny = Diktovanie.Model;
        _modely = Diktovanie.Modely.Select(m => new VolbaModelu(m, ObnovTlacidla)).ToList();
        _modely.First(m => m.Model == ulozeny).Vybrany = true;
        Modely.ItemsSource = _modely;

        (Diktovanie.Sposob == SposobDiktovania.Offline ? Offline : Windows).IsChecked = true;
        Prekazka.Text = PrepisHlasu.Prekazka() ?? "";
        var slovencina = HlasovePisanie.Slovencina is not null;
        SlovencinaInfo.Text = slovencina
            ? Preklad.T("Slovenčina je v systéme nastavená.")
            : Preklad.T("Slovenská klávesnica v systéme nie je – hlasové písanie by rozpoznávalo iný jazyk.");
        SlovencinaInfo.SetResourceReference(TextBlock.ForegroundProperty, slovencina ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush");
        Closing += (_, _) => _stahovanie?.Cancel();
        ObnovTlacidla();
    }

    private ModelHlasu VybranyModel => _modely.Find(m => m.Vybrany)?.Model ?? Diktovanie.Modely[0];

    private bool PrepisVPocitaci => Offline.IsChecked == true;

    private void ObnovTlacidla()
    {
        var model = VybranyModel;
        SekciaModelov.IsEnabled = PrepisVPocitaci && PrepisHlasu.Prekazka() is null;
        Stiahnut.IsEnabled = _stahovanie is null && !model.JeStiahnuty;
        Vymazat.IsEnabled = _stahovanie is null && File.Exists(model.Cesta);
    }

    private void Sposob_Click(object sender, RoutedEventArgs e) => ObnovTlacidla();

    private async void Stiahnut_Click(object sender, RoutedEventArgs e) => await StiahnutAsync();

    /// <summary>Stiahne vybraný model s priebehom; zatvorením okna sa sťahovanie zruší.</summary>
    private async Task StiahnutAsync()
    {
        var model = VybranyModel;
        using var zrusenie = new CancellationTokenSource();
        _stahovanie = zrusenie;
        Priebeh.Value = 0;
        Priebeh.Visibility = Visibility.Visible;
        StavStahovania.Text = Preklad.F($"Sťahuje sa {model.Nazov} ({model.VelkostMb} MB)…");
        ObnovTlacidla();
        try
        {
            var priebeh = new Progress<double>(podiel =>
            {
                Priebeh.Value = podiel;
                StavStahovania.Text = Preklad.F($"Sťahuje sa {model.Nazov}: {podiel * 100:0} % z {model.VelkostMb} MB");
            });
            await Diktovanie.StiahniAsync(model, priebeh, zrusenie.Token);
            StavStahovania.Text = Preklad.T("Model je stiahnutý a overený (SHA-256). Diktovanie funguje aj bez internetu.");
        }
        catch (OperationCanceledException)
        {
            StavStahovania.Text = Preklad.T("Sťahovanie je zrušené.");
        }
        catch (Exception ex)
        {
            StavStahovania.Text = ex is SkvpException ? ex.Message : ex.GetBaseException().Message;
        }
        finally
        {
            _stahovanie = null;
            Priebeh.Visibility = Visibility.Collapsed;
            ObnovStavy();
        }
    }

    private void Vymazat_Click(object sender, RoutedEventArgs e)
    {
        var model = VybranyModel;
        if (!Dialogy.Potvrd(Preklad.F($"Vymazať model {model.Nazov} ({model.VelkostMb} MB) z počítača?")))
        {
            return;
        }

        try
        {
            Diktovanie.Vymaz(model);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogy.Chyba(Preklad.T("Model sa nepodarilo vymazať."), ex);
        }

        ObnovStavy();
    }

    private void ObnovStavy()
    {
        _modely.ForEach(m => m.ObnovStav());
        ObnovTlacidla();
    }

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        var offline = PrepisVPocitaci;
        if (offline && PrepisHlasu.Prekazka() is { } prekazka)
        {
            Dialogy.Upozornenie(prekazka);
            return;
        }

        var model = VybranyModel;
        Diktovanie.Sposob = offline ? SposobDiktovania.Offline : SposobDiktovania.Windows;
        if (Diktovanie.Model != model)
        {
            PrepisHlasu.Zavri();
            Diktovanie.Model = model;
        }

        if (offline && !model.JeStiahnuty)
        {
            Dialogy.Info(Preklad.T("Vybraný model ešte nie je stiahnutý – stiahnite ho tlačidlom Stiahnuť vybraný model, inak diktovanie nebude fungovať."));
        }

        DialogResult = true;
    }
}
