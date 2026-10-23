using System.Windows;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>Úprava textov úvodnej strany zostavy – každý riadok sa dá prepísať aj vypnúť.</summary>
public partial class UvodnaStranaWindow : Window
{
    public UvodnaStranaWindow()
    {
        InitializeComponent();
        Zobraz(TitulnaStrana.Nacitaj());
    }

    private void Zobraz(TitulnaStrana t)
    {
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
    }

    private void AktualnyStupen_Click(object sender, RoutedEventArgs e)
    {
        Typ.Text = TitulnaStrana.AktualnyStupenGymnazia;
        TypZobrazit.IsChecked = true;
    }

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

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        new TitulnaStrana
        {
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
            PataBloky = Pata.Uloz(),
            PataZobrazit = PataZobrazit.IsChecked == true,
        }.Uloz();
        DialogResult = true;
    }
}
