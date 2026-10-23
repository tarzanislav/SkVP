using System.Windows;
using System.Windows.Media;

namespace SkVP.Pomocne;

/// <summary>
/// Pastelové farby programu. Každá farba zobrazená cez <see cref="FarbaNaStetec"/> sa zjemní na pastelový odtieň
/// (aj farby oblastí uložené v starších verziách alebo vybraté používateľom); farebný text sa zobrazí v tmavšom
/// (v tmavom vzhľade svetlejšom) odtieni tej istej farby, aby bol dobre čitateľný. Výstupné dokumenty (Word, PDF)
/// používajú farby bez zmeny.
/// </summary>
public static class Pastel
{
    /// <summary>Pastelové farby vzdelávacích oblastí (poradie zodpovedá pôvodnej palete).</summary>
    public static readonly string[] Oblasti =
    {
        "#7B93DB", "#5DB5B0", "#7CBF8E", "#EDB06B", "#E58FA8", "#A592E0", "#EE9A7A", "#74AEE0", "#A9C76F", "#C38FD3",
    };

    /// <summary>Pôvodné sýte farby oblastí (verzie do 0.1.70) → pastelové.</summary>
    private static readonly Dictionary<string, string> ZoSytych = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#3B5BDB"] = "#7B93DB", ["#0C8599"] = "#5DB5B0", ["#2F9E44"] = "#7CBF8E", ["#E67700"] = "#EDB06B", ["#C2255C"] = "#E58FA8",
        ["#7048E8"] = "#A592E0", ["#D9480F"] = "#EE9A7A", ["#1971C2"] = "#74AEE0", ["#5C940D"] = "#A9C76F", ["#862E9C"] = "#C38FD3",
    };

    /// <summary>Pastelový odtieň farby: obmedzená sýtosť a svetlosť v pásme 0,60 – 0,82.</summary>
    public static Color Zjemni(Color farba)
    {
        var kluc = $"#{farba.R:X2}{farba.G:X2}{farba.B:X2}";
        if (ZoSytych.TryGetValue(kluc, out var mapovana))
        {
            var m = (Color)ColorConverter.ConvertFromString(mapovana);
            m.A = farba.A;
            return m;
        }

        var (h, s, l) = DoHsl(farba);
        if (s < 0.08)
        {
            return farba; // sivé tóny ostávajú
        }

        var vysledok = ZHsl(h, Math.Min(s, 0.62), Math.Clamp(l, 0.60, 0.82));
        vysledok.A = farba.A;
        return vysledok;
    }

    /// <summary>Čitateľný odtieň farby na text a ikony: tmavší vo svetlom vzhľade, svetlejší v tmavom.</summary>
    public static Color Text(Color farba, bool tmavyVzhlad)
    {
        var (h, s, _) = DoHsl(farba);
        var vysledok = s < 0.08 ? ZHsl(h, s, tmavyVzhlad ? 0.80 : 0.30) : ZHsl(h, Math.Min(s, 0.55), tmavyVzhlad ? 0.78 : 0.34);
        vysledok.A = 0xFF;
        return vysledok;
    }

    /// <summary>Je zapnutý tmavý vzhľad (podľa farby hlavného textu)?</summary>
    public static bool JeTmavyVzhlad()
    {
        if (Application.Current?.TryFindResource("TextFillColorPrimaryBrush") is SolidColorBrush text)
        {
            var c = text.Color;
            return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255 > 0.5;
        }

        return false;
    }

    private static (double H, double S, double L) DoHsl(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        if (max - min < 1e-9)
        {
            return (0, 0, l);
        }

        var d = max - min;
        var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
        var h = max == r ? (g - b) / d + (g < b ? 6 : 0) : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h / 6, s, l);
    }

    private static Color ZHsl(double h, double s, double l)
    {
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }

        double r, g, b;
        if (s < 1e-9)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = Hue(p, q, h + 1.0 / 3);
            g = Hue(p, q, h);
            b = Hue(p, q, h - 1.0 / 3);
        }

        return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
    }
}
