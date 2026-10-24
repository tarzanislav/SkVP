using System.Windows;
using System.Windows.Controls;
using SkVP.Pomocne;

namespace SkVP.Views;

/// <summary>Úprava poznámky s diktovaním do textu (hlasové písanie Windows alebo prepis v počítači).</summary>
public partial class PoznamkaEditor : UserControl
{
    private readonly DiktovanieDoPola _diktovanie;

    public PoznamkaEditor()
    {
        InitializeComponent();
        _diktovanie = new DiktovanieDoPola(PoleText, Diktovat, IkonaDiktovania, TextDiktovania, StavDiktovania, Hlasitost);
        _diktovanie.UkazStav(DiktovanieDoPola.PopisSposobu());
        Unloaded += (_, _) => _diktovanie.ZrusNahravanie();
    }

    /// <summary>Kurzor do textu (nová poznámka).</summary>
    public void ZamerajText() => PoleText.Focus();

    private async void Diktovat_Click(object sender, RoutedEventArgs e)
    {
        DiktovanieDoPola.PripravModel();
        await _diktovanie.KlikAsync(novyRiadok: true);
    }

    private void NastavenieDiktovania_Click(object sender, RoutedEventArgs e)
    {
        _diktovanie.ZrusNahravanie();
        if (new DiktovanieWindow { Owner = Window.GetWindow(this) }.ShowDialog() == true)
        {
            _diktovanie.UkazStav(DiktovanieDoPola.PopisSposobu());
            DiktovanieDoPola.PripravModel();
        }
    }
}
