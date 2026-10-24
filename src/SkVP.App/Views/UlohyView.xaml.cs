using System.Windows.Controls;

namespace SkVP.Views;

public partial class UlohyView : UserControl
{
    public UlohyView()
    {
        InitializeComponent();
        Loaded += (_, _) => PoleNova.Focus();
    }
}
