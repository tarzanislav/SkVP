using System.Windows;
using System.Windows.Controls;

namespace SkVP.Views;

/// <summary>Lišta nad dokumentom: stav uloženia, Upraviť vo Worde, Úprava v programe, upozornenia.</summary>
public partial class ListaDokumentu : UserControl
{
    public static readonly DependencyProperty PopisProperty = DependencyProperty.Register(
        nameof(Popis), typeof(string), typeof(ListaDokumentu), new PropertyMetadata(""));

    public ListaDokumentu() => InitializeComponent();

    public string Popis
    {
        get => (string)GetValue(PopisProperty);
        set => SetValue(PopisProperty, value);
    }
}
