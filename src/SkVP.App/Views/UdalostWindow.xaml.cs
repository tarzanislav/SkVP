using System.Windows;
using SkVP.Pomocne;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>Nová alebo upravovaná udalosť v kalendári.</summary>
public partial class UdalostWindow : Window
{
    private readonly UdalostEditorVM _model;

    public UdalostWindow(UdalostEditorVM model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        Odstranit.Visibility = model.JeNova ? Visibility.Collapsed : Visibility.Visible;
        Loaded += (_, _) => PoleNazov.Focus();
    }

    /// <summary>Otvorí okno udalosti; vráti true, ak sa udalosť uložila alebo odstránila.</summary>
    public static bool Otvor(UdalostEditorVM model)
    {
        if (Dialogy.TichyRezim)
        {
            return model.Uloz();
        }

        return new UdalostWindow(model) { Owner = Dialogy.Vlastnik }.ShowDialog() == true;
    }

    private void Ulozit_Click(object sender, RoutedEventArgs e)
    {
        if (_model.Uloz())
        {
            DialogResult = true;
        }
    }

    private void Odstranit_Click(object sender, RoutedEventArgs e)
    {
        if (_model.Odstran())
        {
            DialogResult = true;
        }
    }
}
