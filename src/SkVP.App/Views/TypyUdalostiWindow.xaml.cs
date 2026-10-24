using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>Správa typov udalostí: názov, farba (pastelová paleta oblastí), pridanie a odstránenie.</summary>
public partial class TypyUdalostiWindow : Window
{
    private readonly KalendarService _sluzba = new();

    public TypyUdalostiWindow()
    {
        InitializeComponent();
        Nacitaj();
    }

    private void Nacitaj() => Zoznam.ItemsSource = _sluzba.Typy(lenAktivne: true);

    private void Uloz(TypUdalosti typ)
    {
        try
        {
            _sluzba.UlozTyp(typ.Id, typ.Nazov, typ.Farba);
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }

        Nacitaj();
    }

    private void Nazov_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: TypUdalosti typ })
        {
            Uloz(typ);
        }
    }

    /// <summary>Ďalšia farba z palety (kliknutím sa farby striedajú dookola).</summary>
    private void Farba_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TypUdalosti typ })
        {
            return;
        }

        var paleta = UcebnyPlanService.Paleta;
        var index = paleta.ToList().FindIndex(f => string.Equals(f, typ.Farba, StringComparison.OrdinalIgnoreCase));
        typ.Farba = paleta[(index + 1) % paleta.Count];
        Uloz(typ);
    }

    private void Odstranit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: TypUdalosti typ } || !Dialogy.Potvrd($"Odstrániť typ udalosti „{typ.Nazov}“?"))
        {
            return;
        }

        try
        {
            if (!_sluzba.OdstranTyp(typ.Id))
            {
                Dialogy.Info($"Typ „{typ.Nazov}“ používajú udalosti – zostáva pri nich, ale v ponuke sa už neukáže.");
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }

        Nacitaj();
    }

    private void Pridat_Click(object sender, RoutedEventArgs e) => Pridaj();

    private void NovyNazov_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Pridaj();
        }
    }

    private void Pridaj()
    {
        if (string.IsNullOrWhiteSpace(NovyNazov.Text))
        {
            return;
        }

        var paleta = UcebnyPlanService.Paleta;
        Uloz(new TypUdalosti { Nazov = NovyNazov.Text, Farba = paleta[_sluzba.Typy().Count % paleta.Count] });
        NovyNazov.Text = "";
    }

    private void Zavriet_Click(object sender, RoutedEventArgs e) => Close();
}
