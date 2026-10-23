using System.Windows.Controls;

namespace SkVP.Views;

public partial class UdajeOrganizacieEditor : UserControl
{
    public UdajeOrganizacieEditor() => InitializeComponent();

    /// <summary>Kurzor do názvu školy (prvé spustenie).</summary>
    public void ZamerajNazov() => Nazov.Focus();
}
