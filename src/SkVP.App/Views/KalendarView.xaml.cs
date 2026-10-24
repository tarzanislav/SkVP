using System.Windows;
using System.Windows.Controls;

namespace SkVP.Views;

public partial class KalendarView : UserControl
{
    public KalendarView() => InitializeComponent();

    /// <summary>Po výbere akcie sa ponuka zatvorí.</summary>
    private void ZavrietPonuku(object sender, RoutedEventArgs e) => PrepinacAkcii.IsChecked = false;
}
