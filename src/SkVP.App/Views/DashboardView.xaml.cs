using System.Windows.Controls;
using System.Windows.Input;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class DashboardView : UserControl
{
    public DashboardView() => InitializeComponent();

    private void Dlazdica_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: PredmetVM predmet } && DataContext is DashboardViewModel model)
        {
            model.Hlavny.OtvorPredmet(predmet);
        }
    }
}
