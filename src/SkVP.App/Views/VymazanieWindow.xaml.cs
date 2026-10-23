using System.Windows;
using SkVP.Pomocne;

namespace SkVP.Views;

/// <summary>Voľba, čo vymazať: len osnovy (testovaciu sadu), alebo všetko vrátane oblastí a predmetov.</summary>
public partial class VymazanieWindow : Window
{
    public VymazanieWindow() => InitializeComponent();

    public bool VsetkoVrataneOblasti => Vsetko.IsChecked == true;

    private void Vymazat_Click(object sender, RoutedEventArgs e)
    {
        var text = VsetkoVrataneOblasti
            ? "Naozaj vymazať všetky oblasti, predmety, osnovy a ich archív?"
            : "Naozaj vymazať všetky osnovy, charakteristiky oblastí, archív verzií a súvislosti?";
        if (Dialogy.Potvrd(text + "\n\nPred vymazaním sa vytvorí záloha."))
        {
            DialogResult = true;
        }
    }
}
