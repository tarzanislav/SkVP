using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SkVP.Models;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>Výber objektu, s ktorým sa má predmet prepojiť (ľubovoľný predmet alebo oblasť).</summary>
public partial class VyberObjektuWindow : Window
{
    public sealed record Polozka(long Id, string Nazov, string Popis, string Farba, string Hladanie);

    public sealed record DruhVazby(string Kod, string Nazov);

    private readonly List<Polozka> _vsetky = new();

    public VyberObjektuWindow(MainViewModel hlavny, long vlastneId)
    {
        InitializeComponent();
        Druh.ItemsSource = Models.TypVazby.Vsetky.Select(t => new DruhVazby(t.Kod, char.ToUpper(t.Nazov[0], CultureInfo.CurrentCulture) + t.Nazov[1..])).ToList();
        foreach (var oblast in hlavny.Oblasti)
        {
            if (oblast.Id != vlastneId)
            {
                _vsetky.Add(new Polozka(oblast.Id, oblast.Nazov, "Vzdelávacia oblasť", oblast.Farba, Normalizuj(oblast.Nazov)));
            }

            foreach (var predmet in oblast.Predmety.Where(p => p.Id != vlastneId))
            {
                _vsetky.Add(new Polozka(predmet.Id, predmet.Nazov, $"{oblast.Nazov} • {predmet.Cislo}", oblast.Farba,
                    Normalizuj(predmet.Nazov + " " + oblast.Nazov)));
            }
        }

        Zoznam.ItemsSource = _vsetky;
        Loaded += (_, _) => Hladat.Focus();
    }

    public long? Vybrany { get; private set; }

    public string TypVazby { get; private set; } = Models.TypVazby.Suvisi;

    private void Hladat_TextChanged(object sender, TextChangedEventArgs e)
    {
        var hladane = Normalizuj(Hladat.Text);
        Zoznam.ItemsSource = hladane.Length == 0 ? _vsetky : _vsetky.Where(p => p.Hladanie.Contains(hladane, StringComparison.Ordinal)).ToList();
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Potvrd();

    private void Zoznam_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => Potvrd();

    private void Potvrd()
    {
        if (Zoznam.SelectedItem is not Polozka polozka)
        {
            return;
        }

        Vybrany = polozka.Id;
        TypVazby = (Druh.SelectedItem as DruhVazby)?.Kod ?? Models.TypVazby.Suvisi;
        DialogResult = true;
    }

    /// <summary>Text bez diakritiky a malými písmenami (vyhľadávanie „slovencina“ nájde „Slovenčina“).</summary>
    public static string Normalizuj(string text)
    {
        var rozlozeny = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(rozlozeny.Length);
        foreach (var c in rozlozeny)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
