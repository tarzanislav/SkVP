using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>Prehľad dňa pri spustení: dnešné udalosti a termíny, pripomienky na najbližšie dni a úlohy po termíne.</summary>
public partial class PrehladDnaWindow : Window
{
    public enum Ciel
    {
        Nic,
        Kalendar,
        Ulohy,
    }

    public PrehladDnaWindow(PrehladDna prehlad)
    {
        InitializeComponent();
        Nadpis.Text = PocasieService.VelkePrve(DateTime.Today.ToString("dddd d. MMMM yyyy", Planovanie.Sk));
        Skupina("Dnes", prehlad.Dnes.Select(v => (v.Farba, $"{v.CasText}  ·  {v.Nazov}")));
        Skupina("Pripomienky na najbližšie dni", prehlad.Pripomienky.Select(v => (v.Farba, $"{Planovanie.TerminText(v.Zaciatok)}  ·  {v.Nazov}")));
        Skupina("Úlohy po termíne", prehlad.PoTermine.Select(u => ("#E07A7A", $"{u.TerminText}  ·  {u.Text}")));
    }

    public Ciel Otvorit { get; private set; }

    private void Skupina(string nazov, IEnumerable<(string Farba, string Text)> riadky)
    {
        var zoznam = riadky.ToList();
        if (zoznam.Count == 0)
        {
            return;
        }

        Obsah.Children.Add(new TextBlock { Text = $"{nazov} ({zoznam.Count})", FontWeight = FontWeights.SemiBold, FontSize = 14.5, Margin = new Thickness(0, 10, 0, 6) });
        foreach (var (farba, text) in zoznam)
        {
            var riadok = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var pasik = new Rectangle { Width = 4, RadiusX = 2, RadiusY = 2, Margin = new Thickness(0, 1, 10, 1), Fill = FarbaNaStetec.Stetec(farba) };
            DockPanel.SetDock(pasik, Dock.Left);
            riadok.Children.Add(pasik);
            riadok.Children.Add(new TextBlock { Text = text, FontSize = 13.5, TextWrapping = TextWrapping.Wrap });
            Obsah.Children.Add(riadok);
        }
    }

    private void Kalendar_Click(object sender, RoutedEventArgs e)
    {
        Otvorit = Ciel.Kalendar;
        Close();
    }

    private void Ulohy_Click(object sender, RoutedEventArgs e)
    {
        Otvorit = Ciel.Ulohy;
        Close();
    }
}
