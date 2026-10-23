using System.Windows.Controls;
using System.Windows.Input;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class PredmetView : UserControl
{
    private PrepojenieEditora? _prepojenie;

    public PredmetView()
    {
        InitializeComponent();
        Loaded += (_, _) => Prepoj();
        Unloaded += (_, _) => Odpoj();

        // pri prechode na iný predmet WPF použije to isté zobrazenie, mení sa len DataContext
        DataContextChanged += (_, _) =>
        {
            Odpoj();
            if (IsLoaded)
            {
                Prepoj();
            }
        };
    }

    private void Prepoj()
    {
        if (_prepojenie is null && DataContext is PredmetViewModel model)
        {
            _prepojenie = new PrepojenieEditora(Editor, model.Dokument);
        }
    }

    private void Odpoj()
    {
        _prepojenie?.Dispose();
        _prepojenie = null;
    }

    private void Nazov_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox pole)
        {
            pole.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }
}
