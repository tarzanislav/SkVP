using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SkVP.Pomocne;

/// <summary>Prevodník len jedným smerom (z údajov do zobrazenia).</summary>
public abstract class JednosmernyPrevodnik : IValueConverter
{
    public abstract object Convert(object value, Type targetType, object parameter, CultureInfo culture);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Podmienka splnená → viditeľné; s parametrom „opacne“ naopak.</summary>
public abstract class PodmienkaNaViditelnost : JednosmernyPrevodnik
{
    protected abstract bool Plati(object value);

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        (Plati(value) ^ parameter is "opacne") ? Visibility.Visible : Visibility.Collapsed;
}

/// <summary>Neprázdny text → viditeľné (s parametrom „opacne“ naopak).</summary>
public sealed class NieJePrazdne : PodmienkaNaViditelnost
{
    protected override bool Plati(object value) => value is string { Length: > 0 };
}

/// <summary>Číslo 0 → viditeľné (s parametrom „opacne“ naopak).</summary>
public sealed class NulaNaViditelnost : PodmienkaNaViditelnost
{
    protected override bool Plati(object value) => value is 0;
}

/// <summary>true → skryté, false → viditeľné (aj späť).</summary>
public sealed class InverzBoolNaViditelnost : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not Visibility.Visible;
}

/// <summary>Obráti hodnotu bool (oboma smermi).</summary>
public sealed class InverzBool : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is not true;
}

/// <summary>Hodnota sa rovná parametru (napr. aktívna sekcia menu); späť: zaškrtnuté → parameter.</summary>
public sealed class RovnaSa : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value is true ? parameter : Binding.DoNothing;
}

/// <summary>
/// Farba #RRGGBB → pastelový štetec (<see cref="Pastel.Zjemni"/>). Parameter: priehľadnosť v šestnástkovej sústave
/// (napr. „22“ – jemný podklad) alebo „T“ – čitateľný odtieň na text a ikony. Štetce sa vytvoria raz a zdieľajú sa.
/// </summary>
public sealed class FarbaNaStetec : JednosmernyPrevodnik
{
    private static readonly Color Predvolena = Color.FromRgb(0x3B, 0x5B, 0xDB);
    private static readonly ConcurrentDictionary<(string Farba, int Druh), SolidColorBrush> Stetce = new();

    /// <summary>Pastelová farba s priehľadnosťou (jemné podklady sú o niečo sýtejšie, aby boli viditeľné).</summary>
    public static SolidColorBrush Stetec(string? hex, byte alfa = 0xFF) => Stetce.GetOrAdd((hex ?? "", alfa), kluc =>
    {
        var farba = Pastel.Zjemni(Farba(kluc.Farba));
        farba.A = alfa == 0xFF ? alfa : (byte)Math.Min(0xFF, alfa * 1.6);
        return Zmrazeny(farba);
    });

    /// <summary>Čitateľný odtieň farby na text – podľa svetlého alebo tmavého vzhľadu.</summary>
    public static SolidColorBrush Text(string? hex)
    {
        var tmavy = Pastel.JeTmavyVzhlad();
        return Stetce.GetOrAdd((hex ?? "", tmavy ? -2 : -1), kluc => Zmrazeny(Pastel.Text(Farba(kluc.Farba), tmavy)));
    }

    public override object Convert(object value, Type targetType, object parameter, CultureInfo culture) => parameter switch
    {
        "T" => Text(value as string),
        string p when byte.TryParse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var alfa) => Stetec(value as string, alfa),
        _ => Stetec(value as string),
    };

    private static Color Farba(string hex)
    {
        if (hex.Length == 0)
        {
            return Predvolena;
        }

        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch (FormatException)
        {
            return Predvolena;
        }
    }

    private static SolidColorBrush Zmrazeny(Color farba)
    {
        var stetec = new SolidColorBrush(farba);
        stetec.Freeze();
        return stetec;
    }
}
