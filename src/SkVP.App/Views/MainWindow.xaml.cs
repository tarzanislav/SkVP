using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class MainWindow : Window
{
    public const string FormatPredmetu = "SkVP.Predmet";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    public MainViewModel Model => (MainViewModel)DataContext;

    protected override void OnClosing(CancelEventArgs e)
    {
        (Model.Stranka as IUkladaPredOdchodom)?.UlozTeraz();
        Model.UkonciUpravyVoWorde();
        base.OnClosing(e);
    }

    // predmet pretiahnutý myšou na oblasť v ľavom paneli sa presunie do tej oblasti
    private void Oblast_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(FormatPredmetu) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Oblast_Drop(object sender, DragEventArgs e)
    {
        if (sender is Button { Tag: OblastVM oblast } && e.Data.GetData(FormatPredmetu) is PredmetVM predmet)
        {
            Model.PresunPredmet(predmet, oblast, oblast.Predmety.Count);
            e.Handled = true;
        }
    }
}
