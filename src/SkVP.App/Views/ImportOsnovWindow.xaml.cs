using System.Windows;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>Sprievodca importom učebných osnov z Wordu a PDF.</summary>
public partial class ImportOsnovWindow : Window
{
    public ImportOsnovWindow(ImportOsnovViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}
