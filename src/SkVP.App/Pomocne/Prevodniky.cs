using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SkVP.Pomocne;

/// <summary>true → skryté, false → viditeľné.</summary>
public sealed class InverzBoolNaViditelnost : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is not Visibility.Visible;
}

/// <summary>Neprázdny text → viditeľné (s parametrom „opacne“ naopak).</summary>
public sealed class NieJePrazdne : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is string text && text.Length > 0) != (parameter as string == "opacne") ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Číslo 0 → viditeľné (s parametrom „opacne“ naopak).</summary>
public sealed class NulaNaViditelnost : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (value is 0) != (parameter as string == "opacne") ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Farba #RRGGBB → štetec; parameter je priehľadnosť v šestnástkovej sústave (napr. „22“).</summary>
public sealed class FarbaNaStetec : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Vyrovnavacia = new();

    public static SolidColorBrush Stetec(string? hex, byte alfa = 0xFF)
    {
        var kluc = (hex ?? "") + alfa.ToString("X2", CultureInfo.InvariantCulture);
        lock (Vyrovnavacia)
        {
            if (!Vyrovnavacia.TryGetValue(kluc, out var stetec))
            {
                Color farba;
                try
                {
                    farba = (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(hex) ? "#3B5BDB" : hex);
                }
                catch (FormatException)
                {
                    farba = Color.FromRgb(0x3B, 0x5B, 0xDB);
                }

                farba.A = alfa;
                stetec = new SolidColorBrush(farba);
                stetec.Freeze();
                Vyrovnavacia[kluc] = stetec;
            }

            return stetec;
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var alfa = parameter is string p && byte.TryParse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var a) ? a : (byte)0xFF;
        return Stetec(value as string, alfa);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Porovná hodnotu s parametrom (napr. aktívna sekcia menu).</summary>
public sealed class RovnaSa : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Equals(value?.ToString(), parameter?.ToString());

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? parameter : Binding.DoNothing;
}

/// <summary>Obráti hodnotu bool.</summary>
public sealed class InverzBool : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}
