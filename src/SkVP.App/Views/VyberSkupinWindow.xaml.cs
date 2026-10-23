using System.ComponentModel;
using System.Windows;

namespace SkVP.Views;

/// <summary>Výber skupín (napr. skupín nastavení programu na export alebo import) zaškrtnutím.</summary>
public partial class VyberSkupinWindow : Window
{
    public sealed class Polozka : INotifyPropertyChanged
    {
        private bool _vybrana = true;

        public Polozka(string nazov) => Nazov = nazov;

        public string Nazov { get; }

        public bool Vybrana
        {
            get => _vybrana;
            set
            {
                _vybrana = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Vybrana)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly List<Polozka> _polozky;

    public VyberSkupinWindow(string titulok, string popis, string tlacidlo, IEnumerable<string> skupiny)
    {
        InitializeComponent();
        Title = titulok;
        Nadpis.Text = titulok;
        Popis.Text = popis;
        Ok.Content = tlacidlo;
        _polozky = skupiny.Select(s => new Polozka(s)).ToList();
        Zoznam.ItemsSource = _polozky;
    }

    public IReadOnlyCollection<string> Vybrane => _polozky.Where(p => p.Vybrana).Select(p => p.Nazov).ToList();

    private void Vsetko_Click(object sender, RoutedEventArgs e) => _polozky.ForEach(p => p.Vybrana = true);

    private void Nic_Click(object sender, RoutedEventArgs e) => _polozky.ForEach(p => p.Vybrana = false);

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Vybrane.Count == 0)
        {
            Pomocne.Dialogy.Upozornenie("Vyberte aspoň jednu skupinu nastavení.");
            return;
        }

        DialogResult = true;
    }
}
