using System.Windows;
using SkVP.Pomocne;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>Poznámka v samostatnom okne (z kalendára alebo z panela pri predmete). Ukladá sa priebežne aj pri zatvorení.</summary>
public partial class PoznamkaWindow : Window
{
    private readonly PoznamkaEditorVM _model;

    public PoznamkaWindow(PoznamkaEditorVM model)
    {
        InitializeComponent();
        _model = model;
        DataContext = model;
        model.Zmenena += odstranena =>
        {
            Zmenene = true;
            if (odstranena && IsVisible)
            {
                Close();
            }
        };
        Closing += (_, _) => _model.Dispose();
        Loaded += (_, _) => Editor.ZamerajText();
    }

    /// <summary>Poznámka sa uložila alebo odstránila.</summary>
    public bool Zmenene { get; private set; }

    /// <summary>Otvorí poznámku v okne; vráti true, ak sa zmenila.</summary>
    public static bool Otvor(PoznamkaEditorVM model)
    {
        if (Dialogy.TichyRezim)
        {
            model.Dispose();
            return false;
        }

        var okno = new PoznamkaWindow(model) { Owner = Dialogy.Vlastnik };
        okno.ShowDialog();
        return okno.Zmenene;
    }

    private void Zavriet_Click(object sender, RoutedEventArgs e) => Close();
}
