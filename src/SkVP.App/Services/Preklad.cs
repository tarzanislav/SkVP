using System.Globalization;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Jazyk programu: kód, názov v danom jazyku a kultúra (formáty dátumu a čísel).</summary>
public sealed record Jazyk(string Kod, string Nazov, string Kultura)
{
    public override string ToString() => Nazov;
}

/// <summary>
/// Preklad textov programu (rovnaký princíp ako v EviTA): zdrojom je slovenský text, ktorý je zároveň kľúčom do slovníka
/// daného jazyka. Zatiaľ je k dispozícii len slovenčina – ďalšie jazyky sa doplnia slovníkmi bez zmeny kódu.
/// </summary>
public static class Preklad
{
    public const string KlucJazyk = "program.jazyk";

    public static readonly IReadOnlyList<Jazyk> Jazyky = new Jazyk[]
    {
        new("sk", "Slovenčina", "sk-SK"),
    };

    private static Dictionary<string, string>? _slovnik;

    public static Jazyk Aktualny { get; private set; } = Jazyky[0];

    public static CultureInfo Kultura { get; private set; } = new("sk-SK");

    public static Jazyk Najdi(string? kod) =>
        Jazyky.FirstOrDefault(j => string.Equals(j.Kod, kod, StringComparison.OrdinalIgnoreCase)) ?? Jazyky[0];

    /// <summary>Nastaví jazyk podľa uložených nastavení.</summary>
    public static void Nacitaj() => Nastav(Databaza.Citaj(KlucJazyk));

    public static void Nastav(string? kod)
    {
        Aktualny = Najdi(kod);
        Kultura = new CultureInfo(Aktualny.Kultura);
        _slovnik = null; // slovníky ďalších jazykov sa doplnia neskôr
    }

    /// <summary>Preložený text (bez prekladu slovenský).</summary>
    /// <summary>Text na neskorší preklad (označenie pre slovník).</summary>
    public static string N(string text) => text;

    public static string T(string text) => _slovnik is not null && _slovnik.TryGetValue(text, out var p) ? p : text;

    /// <summary>Preložený text s doplnenými hodnotami.</summary>
    public static string F(FormattableString text) =>
        string.Format(Kultura, T(text.Format), text.GetArguments());

    /// <summary>Počet so slovom v správnom tvare: 1 strana, 2 – 4 strany, 5 strán (formát s {0}).</summary>
    public static string Pocet(int pocet, string jeden, string dva, string pat)
    {
        var vzor = pocet == 1 ? jeden : pocet is >= 2 and <= 4 ? dva : pat;
        return string.Format(Kultura, T(vzor), pocet);
    }
}
