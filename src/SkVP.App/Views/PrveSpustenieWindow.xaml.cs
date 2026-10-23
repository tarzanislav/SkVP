using System.Windows;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>Prvé spustenie: názov školy a východisková štruktúra oblastí a predmetov.</summary>
public partial class PrveSpustenieWindow : Window
{
    private readonly UcebnyPlan _gymnazium = UcebnyPlanService.Vstavany();

    public PrveSpustenieWindow()
    {
        InitializeComponent();
        PopisGymnazia.Text = $"{_gymnazium.Typ} – {_gymnazium.Oblasti.Count} vzdelávacích oblastí a {_gymnazium.PocetPredmetov} predmetov z tabuľky školy.";
        TestovaciaSada.IsChecked = Services.TestovaciaSada.ZvolenaPriInstalacii;
        Loaded += (_, _) => Skola.Focus();
    }

    private void Zacat_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Skola.Text.Trim().Length > 0)
            {
                Databaza.Zapis(UdajeSkVP.KlucSkola, Skola.Text.Trim());
            }

            if (VolbaGymnazium.IsChecked == true)
            {
                UcebnyPlanService.Vytvor(_gymnazium, nahradit: false);
                if (TestovaciaSada.IsChecked == true)
                {
                    Dialogy.Cakaj(() => Services.TestovaciaSada.Nainstaluj());
                }
            }
            else if (VolbaExcel.IsChecked == true)
            {
                var cesta = Dialogy.Otvorit("Tabuľka so vzdelávacími oblasťami a predmetmi", "Tabuľky Excel (*.xlsx)|*.xlsx");
                if (cesta is null)
                {
                    return;
                }

                UcebnyPlanService.Vytvor(UcebnyPlanService.NacitajZExcelu(cesta), nahradit: false);
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
            return;
        }

        DialogResult = true;
    }
}
