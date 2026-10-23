using System.Text.RegularExpressions;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// Údaje o organizácii (Nastavenia → Údaje o organizácii, prvé spustenie): názov školy, organizačná zložka (pri spojenej
/// škole), adresa po častiach, IČO, DIČ, e-mail a telefón. Adresa sa zároveň skladá do jedného riadka
/// <see cref="UdajeSkVP.KlucAdresa"/> pre úvodnú stranu („Javorová 12, 974 01 Banská Bystrica“).
/// </summary>
public sealed partial record UdajeOrganizacie(
    string Nazov, string Zlozka, string Ulica, string Mesto, string Psc, string Stat, string Ico, string Dic, string Email, string Telefon)
{
    public const string KlucZlozka = "org.zlozka";
    public const string KlucUlica = "org.ulica";
    public const string KlucMesto = "org.mesto";
    public const string KlucPsc = "org.psc";
    public const string KlucStat = "org.stat";
    public const string KlucIco = "org.ico";
    public const string KlucDic = "org.dic";
    public const string KlucEmail = "org.email";
    public const string KlucTelefon = "org.telefon";

    public const string PredvolenyStat = "Slovensko";

    public static readonly string[] Kluce =
    {
        UdajeSkVP.KlucSkola, KlucZlozka, KlucUlica, KlucMesto, KlucPsc, KlucStat, KlucIco, KlucDic, KlucEmail, KlucTelefon,
    };

    public static UdajeOrganizacie Prazdne => new("", "", "", "", "", PredvolenyStat, "", "", "", "");

    public static UdajeOrganizacie Nacitaj()
    {
        var udaje = new UdajeOrganizacie(
            Databaza.Citaj(UdajeSkVP.KlucSkola, ""), Databaza.Citaj(KlucZlozka, ""), Databaza.Citaj(KlucUlica, ""),
            Databaza.Citaj(KlucMesto, ""), Databaza.Citaj(KlucPsc, ""), Databaza.Citaj(KlucStat, PredvolenyStat),
            Databaza.Citaj(KlucIco, ""), Databaza.Citaj(KlucDic, ""), Databaza.Citaj(KlucEmail, ""), Databaza.Citaj(KlucTelefon, ""));

        // staršie verzie mali adresu v jednom riadku – rozdelí sa na ulicu, PSČ a mesto
        if (!udaje.MaAdresu && Databaza.Citaj(UdajeSkVP.KlucAdresa, "").Trim() is { Length: > 0 } stara)
        {
            udaje = udaje.ZAdresy(stara);
        }

        return udaje;
    }

    public bool MaAdresu => Ulica.Length > 0 || Mesto.Length > 0 || Psc.Length > 0;

    /// <summary>Adresa v jednom riadku: „Ulica 12, 974 01 Mesto“ (štát len mimo Slovenska).</summary>
    public string AdresaRiadok
    {
        get
        {
            var obec = string.Join(" ", new[] { Psc.Trim(), Mesto.Trim() }.Where(t => t.Length > 0));
            var stat = Stat.Trim();
            var casti = new List<string> { Ulica.Trim(), obec };
            if (stat.Length > 0 && !JeSlovensko(stat))
            {
                casti.Add(stat);
            }

            return string.Join(", ", casti.Where(t => t.Length > 0));
        }
    }

    /// <summary>„IČO: 00160849  •  DIČ: 2021145766“ (len vyplnené).</summary>
    public string IcoDicRiadok => string.Join("  •  ", new[]
    {
        Ico.Trim().Length > 0 ? $"IČO: {Ico.Trim()}" : null,
        Dic.Trim().Length > 0 ? $"DIČ: {Dic.Trim()}" : null,
    }.Where(t => t is not null));

    /// <summary>„e-mail: …  •  tel.: …“ (len vyplnené).</summary>
    public string KontaktRiadok => string.Join("  •  ", new[]
    {
        Email.Trim().Length > 0 ? $"e-mail: {Email.Trim()}" : null,
        Telefon.Trim().Length > 0 ? $"tel.: {Telefon.Trim()}" : null,
    }.Where(t => t is not null));

    /// <summary>Názov školy a organizačnej zložky („Spojená škola – Gymnázium“).</summary>
    public string NazovSoZlozkou => string.Join(" – ", new[] { Nazov.Trim(), Zlozka.Trim() }.Where(t => t.Length > 0));

    public void Uloz()
    {
        Databaza.Zapis(UdajeSkVP.KlucSkola, Nazov.Trim());
        Databaza.Zapis(KlucZlozka, Zlozka.Trim());
        Databaza.Zapis(KlucUlica, Ulica.Trim());
        Databaza.Zapis(KlucMesto, Mesto.Trim());
        Databaza.Zapis(KlucPsc, Psc.Trim());
        Databaza.Zapis(KlucStat, Stat.Trim());
        Databaza.Zapis(KlucIco, Ico.Trim());
        Databaza.Zapis(KlucDic, Dic.Trim());
        Databaza.Zapis(KlucEmail, Email.Trim());
        Databaza.Zapis(KlucTelefon, Telefon.Trim());
        Databaza.Zapis(UdajeSkVP.KlucAdresa, AdresaRiadok);
    }

    /// <summary>Vymaže všetky údaje o organizácii (napr. vymyslenú školu testovacej sady).</summary>
    public static void Vymaz()
    {
        foreach (var kluc in Kluce.Append(UdajeSkVP.KlucAdresa))
        {
            Databaza.Zapis(kluc, null);
        }
    }

    [GeneratedRegex(@"^(?<ulica>.*?)[,;]\s*(?<psc>\d{3}\s?\d{2})\s+(?<mesto>[^,]+?)(?:,\s*(?<stat>.+))?$")]
    private static partial Regex VzorAdresy();

    /// <summary>Rozdelí adresu v jednom riadku („Ulica 12, 974 01 Mesto[, Štát]“).</summary>
    public UdajeOrganizacie ZAdresy(string adresa)
    {
        var m = VzorAdresy().Match(adresa.Trim());
        if (m.Success)
        {
            return this with
            {
                Ulica = m.Groups["ulica"].Value.Trim(),
                Psc = m.Groups["psc"].Value.Trim(),
                Mesto = m.Groups["mesto"].Value.Trim(),
                Stat = m.Groups["stat"].Success ? m.Groups["stat"].Value.Trim() : Stat,
            };
        }

        var casti = adresa.Split(',', 2, StringSplitOptions.TrimEntries);
        return casti.Length == 2 ? this with { Ulica = casti[0], Mesto = casti[1] } : this with { Ulica = adresa.Trim() };
    }

    private static bool JeSlovensko(string stat) =>
        ImportOsnov.Kluc(stat) is "slovensko" or "slovenska republika" or "sr" or "slovakia";

    /// <summary>Kontrola IČO (8 číslic) – vráti text chyby alebo null.</summary>
    public static string? SkontrolujIco(string ico) =>
        ico.Trim().Length == 0 || Regex.IsMatch(ico.Replace(" ", ""), @"^\d{8}$") ? null : "IČO má 8 číslic.";

    /// <summary>Kontrola e-mailu – vráti text chyby alebo null.</summary>
    public static string? SkontrolujEmail(string email) =>
        email.Trim().Length == 0 || Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$") ? null : "E-mailová adresa nie je v správnom tvare.";
}
