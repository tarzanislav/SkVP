using System.Windows.Controls;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class TlacView : UserControl
{
    private bool _prisposobene;

    public TlacView()
    {
        InitializeComponent();

        // náhľad sa na začiatku prispôsobí šírke okna
        Nahlad.SizeChanged += (_, e) =>
        {
            if (!_prisposobene && e.NewSize.Width > 100 && DataContext is TlacViewModel model)
            {
                _prisposobene = true;
                model.Zvacsenie = Math.Round(Math.Clamp((e.NewSize.Width - 90) / 794, 0.3, 1.0), 2);
            }
        };
        Loaded += (_, _) =>
        {
            if (DataContext is TlacViewModel { PocetStran: 0 } model)
            {
                Dispatcher.BeginInvoke(model.Obnovit, System.Windows.Threading.DispatcherPriority.Background);
            }
        };
    }
}
