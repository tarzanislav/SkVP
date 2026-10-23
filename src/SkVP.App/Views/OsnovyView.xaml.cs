using System.Windows.Controls;
using System.Windows.Input;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class OsnovyView : UserControl
{
    public OsnovyView() => InitializeComponent();

    private void NovaOblast_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is OsnovyViewModel model)
        {
            model.PridajOblastCommand.Execute(null);
            e.Handled = true;
        }
    }
}
