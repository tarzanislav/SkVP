using System.ComponentModel;
using System.Windows.Controls;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class PoznamkyView : UserControl
{
    public PoznamkyView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is PoznamkyViewModel stary)
            {
                stary.PropertyChanged -= ZmenaModelu;
            }

            if (e.NewValue is PoznamkyViewModel novy)
            {
                novy.PropertyChanged += ZmenaModelu;
            }
        };
    }

    /// <summary>Pri novej poznámke ide kurzor rovno do textu.</summary>
    private void ZmenaModelu(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PoznamkyViewModel.Editor) && sender is PoznamkyViewModel { Editor.JeNova: true })
        {
            Dispatcher.BeginInvoke(new Action(Editor.ZamerajText), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}
