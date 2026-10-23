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

/// <summary>
/// Farba #RRGGBB → pastelový štetec (<see cref="Pastel.Zjemni"/>). Parameter: priehľadnosť v šestnástkovej sústave
/// (napr. „22“ – jemný podklad) alebo „T“ – čitateľný odtieň na text a ikony.
/// </summary>
public sealed class FarbaNaStetec : IValueConverter
{
    private static readonly Dictionary<string, SolidColorBrush> Vyrovnavacia = new();

    private static Color Precitaj(string? hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(string.IsNullOrEmpty(hex) ? "#3B5BDB" : hex);
        }
        catch (FormatException)
        {
            return Color.FromRgb(0x3B, 0x5B, 0xDB);
        }
    }

    /// <summary>Pastelová farba s priehľadnosťou (jemné podklady sú o niečo sýtejšie, aby boli viditeľné).</summary>
    public static SolidColorBrush Stetec(string? hex, byte alfa = 0xFF)
    {
        var kluc = (hex ?? "") + alfa.ToString("X2", CultureInfo.InvariantCulture);
        lock (Vyrovnavacia)
        {
            if (!Vyrovnavacia.TryGetValue(kluc, out var stetec))
            {
                var farba = Pastel.Zjemni(Precitaj(hex));
                farba.A = alfa == 0xFF ? alfa : (byte)Math.Min(0xFF, alfa * 1.6);
                stetec = new SolidColorBrush(farba);
                stetec.Freeze();
                Vyrovnavacia[kluc] = stetec;
            }

            return stetec;
        }
    }

    /// <summary>Čitateľný odtieň farby na text (podľa svetlého alebo tmavého vzhľadu).</summary>
    public static SolidColorBrush Text(string? hex)
    {
        var tmavy = Pastel.JeTmavyVzhlad();
        var kluc = (hex ?? "") + (tmavy ? "Tt" : "Ts");
        lock (Vyrovnavacia)
        {
            if (!Vyrovnavacia.TryGetValue(kluc, out var stetec))
            {
                stetec = new SolidColorBrush(Pastel.Text(Precitaj(hex), tmavy));
                stetec.Freeze();
                Vyrovnavacia[kluc] = stetec;
            }

            return stetec;
        }
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is "T")
        {
            return Text(value as string);
        }

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
