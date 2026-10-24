using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SkVP.Pomocne;

/// <summary>
/// Pole dátumu po slovensky: WPF má text prázdneho poľa („Select a date“) len v niekoľkých jazykoch, preto ho program
/// nastaví sám pri každom poli dátumu. Formát dátumu (24. 10. 2026) určuje jazyk stránky (Language="sk-SK").
/// </summary>
public static class PoleDatumu
{
    public const string TextPrazdneho = "Vyberte dátum";

    public static void Zapni() =>
        EventManager.RegisterClassHandler(typeof(DatePicker), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) =>
        {
            if (s is DatePicker pole)
            {
                NastavText(pole);
            }
        }));

    private static void NastavText(DatePicker pole)
    {
        try
        {
            pole.ApplyTemplate();
            if (pole.Template?.FindName("PART_TextBox", pole) is DatePickerTextBox text)
            {
                text.ApplyTemplate();
                if (text.Template?.FindName("PART_Watermark", text) is ContentControl vodoznak)
                {
                    vodoznak.Content = TextPrazdneho;
                }
            }
        }
        catch (Exception)
        {
            // pole dátumu funguje aj s pôvodným textom
        }
    }
}
