using System.Windows;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>Export učebných osnov s voľbami (ako v EviTA).</summary>
public partial class ExportOsnovWindow : Window
{
    public ExportOsnovWindow(ExportOsnovViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}
