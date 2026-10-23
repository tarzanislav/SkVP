using System.Windows.Controls;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class TlacView : UserControl
{
    public TlacView()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is TlacViewModel { PocetStran: 0 } model)
            {
                Dispatcher.BeginInvoke(model.Obnovit, System.Windows.Threading.DispatcherPriority.Background);
            }
        };
    }
}
