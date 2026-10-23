using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using SkVP.Models;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>
/// Výber objektu, s ktorým sa má predmet prepojiť (ľubovoľný predmet alebo oblasť), alebo výber viacerých predmetov,
/// oblastí a častí osnov, ktorých sa týka úloha, udalosť či poznámka („Týka sa“).
/// </summary>
public partial class VyberObjektuWindow : Window
{
    public sealed record Polozka(long Id, string Nazov, string Popis, string Farba, string Hladanie);

    public sealed record DruhVazby(string Kod, string Nazov);

    private readonly List<Polozka> _vsetky = new();
    private readonly HashSet<long> _vybrane = new();
    private readonly bool _viac;
    private bool _filtrujem;

    /// <summary>Výber viacerých objektov („Týka sa“) – predvolene označené sú doterajšie.</summary>
    public VyberObjektuWindow(MainViewModel hlavny, IEnumerable<long> vybrane) : this(hlavny, 0)
    {
        _viac = true;
        Tag = true;
        Title = "Týka sa…";
        Titulok.Text = "Čoho sa to týka";
        Vysvetlenie.Visibility = Visibility.Visible;
        VolbaDruhu.Visibility = Visibility.Collapsed;
        Ok.Content = "Vybrať";
        Zoznam.SelectionMode = SelectionMode.Multiple;
        _vybrane.UnionWith(vybrane);
        OznacVybrane();
    }

    public VyberObjektuWindow(MainViewModel hlavny, long vlastneId)
    {
        InitializeComponent();
        Druh.ItemsSource = Models.TypVazby.Vsetky.Select(t => new DruhVazby(t.Kod, char.ToUpper(t.Nazov[0], CultureInfo.CurrentCulture) + t.Nazov[1..])).ToList();
        // vzdelávacie oblasti a zapnuté úvodné/záverečné časti učebných osnov
        foreach (var oblast in hlavny.VsetkySkupiny.Where(o => o.Aktivna))
        {
            if (oblast.Id != vlastneId)
            {
                _vsetky.Add(new Polozka(oblast.Id, oblast.Nazov, oblast.NazovDruhu, oblast.Farba, Normalizuj(oblast.Nazov)));
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

    /// <summary>
    /// Vybrané objekty v poradí zoznamu (pri výbere viacerých). Doterajšie naviazania, ktoré v zozname nie sú
    /// (odstránený predmet, vypnuté časti osnov), zostanú zachované.
    /// </summary>
    public IReadOnlyList<long> Vybrane
    {
        get
        {
            var ponuknute = _vsetky.Select(p => p.Id).ToHashSet();
            return _vsetky.Where(p => _vybrane.Contains(p.Id)).Select(p => p.Id).Concat(_vybrane.Where(id => !ponuknute.Contains(id))).ToList();
        }
    }

    public string TypVazby { get; private set; } = Models.TypVazby.Suvisi;

    private void Hladat_TextChanged(object sender, TextChangedEventArgs e)
    {
        var hladane = Normalizuj(Hladat.Text);
        _filtrujem = true;
        Zoznam.ItemsSource = hladane.Length == 0 ? _vsetky : _vsetky.Where(p => p.Hladanie.Contains(hladane, StringComparison.Ordinal)).ToList();
        _filtrujem = false;
        OznacVybrane();
    }

    /// <summary>Pri výbere viacerých si okno pamätá označenie aj pri zmene hľadania.</summary>
    private void Zoznam_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_viac || _filtrujem)
        {
            return;
        }

        foreach (Polozka p in e.AddedItems)
        {
            _vybrane.Add(p.Id);
        }

        foreach (Polozka p in e.RemovedItems)
        {
            _vybrane.Remove(p.Id);
        }
    }

    private void OznacVybrane()
    {
        if (!_viac)
        {
            return;
        }

        _filtrujem = true;
        Zoznam.SelectedItems.Clear();
        foreach (var polozka in Zoznam.Items.OfType<Polozka>().Where(p => _vybrane.Contains(p.Id)))
        {
            Zoznam.SelectedItems.Add(polozka);
        }

        _filtrujem = false;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Potvrd();

    private void Zoznam_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_viac)
        {
            Potvrd();
        }
    }

    private void Potvrd()
    {
        if (_viac)
        {
            DialogResult = true;
            return;
        }

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
