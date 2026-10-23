using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SkVP.ViewModels;

namespace SkVP.Views;

public partial class OblastView : UserControl
{
    private Point _zaciatokTahania;
    private PrepojenieEditora? _prepojenie;

    public OblastView()
    {
        InitializeComponent();
        Unloaded += (_, _) =>
        {
            _prepojenie?.Dispose();
            _prepojenie = null;
        };
        PreviewKeyDown += Klavesy;

        // pri prechode na inú oblasť WPF použije to isté zobrazenie, mení sa len DataContext
        DataContextChanged += (_, _) =>
        {
            _prepojenie?.Dispose();
            _prepojenie = null;
            if (ZalozkaPredmety is not null)
            {
                ZalozkaPredmety.IsChecked = true;
            }
        };
    }

    private OblastViewModel? Model => DataContext as OblastViewModel;

    private void Zalozka_Checked(object sender, RoutedEventArgs e)
    {
        if (PanelPredmety is null || PanelCharakteristika is null)
        {
            return;
        }

        var charakteristika = ZalozkaCharakteristika.IsChecked == true;
        PanelPredmety.Visibility = charakteristika ? Visibility.Collapsed : Visibility.Visible;
        PanelCharakteristika.Visibility = charakteristika ? Visibility.Visible : Visibility.Collapsed;
        if (charakteristika && _prepojenie is null && Model is { } model)
        {
            _prepojenie = new PrepojenieEditora(Editor, model.Dokument);
        }
    }

    /// <summary>Otvorí záložku charakteristiky (aj pre samotest).</summary>
    public void UkazCharakteristiku() => ZalozkaCharakteristika.IsChecked = true;

    private void Nazov_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is TextBox pole)
        {
            pole.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void NovyPredmet_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Model is { } model)
        {
            model.PridajPredmetCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ---- poradie klávesmi Alt+↑ / Alt+↓ (predmet, v ktorom je zameranie) ----

    private void Klavesy(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt || Model is not { } model)
        {
            return;
        }

        var kluc = e.Key == Key.System ? e.SystemKey : e.Key;
        if (kluc is not (Key.Up or Key.Down) || (Keyboard.FocusedElement as FrameworkElement)?.DataContext is not PredmetVM predmet)
        {
            return;
        }

        model.Hlavny.PosunPredmet(predmet, kluc == Key.Up ? -1 : 1);
        e.Handled = true;
        Dispatcher.BeginInvoke(() => ZamerajPredmet(predmet), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ZamerajPredmet(PredmetVM predmet)
    {
        if (ZoznamPredmetov.ItemContainerGenerator.ContainerFromItem(predmet) is ContentPresenter obal &&
            obal.ContentTemplate?.FindName("Otvorit", obal) is Button tlacidlo)
        {
            tlacidlo.Focus();
        }
    }

    // ---- premenovanie v zozname ----

    private void Premenovanie_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: PredmetVM predmet } || Model is not { } model)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            model.DokonciPremenovanie(predmet, true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            model.DokonciPremenovanie(predmet, false);
            e.Handled = true;
        }
    }

    private void Premenovanie_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { DataContext: PredmetVM predmet } && Model is { } model)
        {
            model.DokonciPremenovanie(predmet, true);
        }
    }

    private void Premenovanie_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is TextBox pole && pole.IsVisible)
        {
            pole.Dispatcher.BeginInvoke(() =>
            {
                pole.Focus();
                pole.SelectAll();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    // ---- presúvanie myšou ----

    private void Riadok_MouseDown(object sender, MouseButtonEventArgs e) => _zaciatokTahania = e.GetPosition(this);

    private void Riadok_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement { Tag: PredmetVM predmet } riadok || predmet.Premenuva)
        {
            return;
        }

        var posun = e.GetPosition(this) - _zaciatokTahania;
        if (Math.Abs(posun.Y) < SystemParameters.MinimumVerticalDragDistance * 2 &&
            Math.Abs(posun.X) < SystemParameters.MinimumHorizontalDragDistance * 2)
        {
            return;
        }

        var data = new DataObject(MainWindow.FormatPredmetu, predmet);
        riadok.Opacity = 0.5;
        try
        {
            DragDrop.DoDragDrop(riadok, data, DragDropEffects.Move);
        }
        finally
        {
            riadok.Opacity = 1;
        }
    }

    private void Zoznam_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(MainWindow.FormatPredmetu) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Zoznam_Drop(object sender, DragEventArgs e)
    {
        if (Model is not { } model || e.Data.GetData(MainWindow.FormatPredmetu) is not PredmetVM predmet)
        {
            return;
        }

        // cieľ: riadok pod myšou – horná polovica = pred neho, dolná = za neho
        var pozicia = model.Oblast.Predmety.Count;
        var prvok = e.OriginalSource as DependencyObject;
        while (prvok is not null && prvok is not ContentPresenter { Content: PredmetVM })
        {
            prvok = VisualTreeHelper.GetParent(prvok);
        }

        if (prvok is ContentPresenter { Content: PredmetVM cielovy } obal)
        {
            pozicia = model.Oblast.Predmety.IndexOf(cielovy);
            if (e.GetPosition(obal).Y > obal.ActualHeight / 2)
            {
                pozicia++;
            }
        }

        model.Hlavny.PresunPredmet(predmet, model.Oblast, pozicia);
        e.Handled = true;
    }
}
