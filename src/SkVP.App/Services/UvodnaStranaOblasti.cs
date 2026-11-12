namespace SkVP.Services;

/// <summary>
/// Rozmery úvodnej (oddeľujúcej) strany vzdelávacej oblasti – nadpis oblasti a zoznam jej predmetov s číslami strán.
/// Zoznam sa musí zmestiť na jednu stranu: najprv sa zmenší voľné miesto nad nadpisom, potom písmo a riadky zoznamu.
/// Rovnaké rozmery používa PDF aj Word. Všetky hodnoty sú v bodoch.
/// </summary>
/// <param name="MedzeraHore">voľné miesto nad nadpisom oblasti</param>
/// <param name="MedzeraPodPopisom">medzera pod riadkom „Vzdelávacia oblasť • 12 predmetov“</param>
/// <param name="Cislo">veľkosť písma čísla predmetu</param>
/// <param name="Nazov">veľkosť písma názvu predmetu</param>
/// <param name="Odstup">vnútorný okraj riadka zoznamu hore a dole</param>
public sealed record UvodnaStranaOblasti(double MedzeraHore, double MedzeraPodPopisom, double Cislo, double Nazov, double Odstup)
{
    /// <summary>Medzera nad nadpisom, keď je miesta dosť (4,5 cm + 5 cm).</summary>
    public const double PlnaMedzera = 269;

    /// <summary>Najmenšia medzera nad nadpisom, kým sa ešte nezmenšuje písmo zoznamu.</summary>
    private const double NajmensiaMedzera = 28;

    /// <summary>Rezerva na nepresnosť odhadu (zalamovanie slov, písma so širšími znakmi).</summary>
    private const double Rezerva = 0.93;

    /// <summary>Šírka stĺpcov s číslom predmetu a číslom strany.</summary>
    private const double SirkaCisiel = 86;

    /// <summary>Stupne zmenšenia zoznamu: písmo čísla, písmo názvu, okraj riadka, medzera pod popisom.</summary>
    private static readonly (double Cislo, double Nazov, double Odstup, double PodPopisom)[] Stupne =
    {
        (15, 12.5, 4, 26),
        (13.5, 11.5, 3, 20),
        (12.5, 10.5, 2, 14),
        (11.5, 9.5, 1.2, 10),
        (10.5, 8.5, 0.6, 8),
    };

    /// <summary>Rozmery strany pre oblasť – najväčšie, pri ktorých sa zoznam predmetov zmestí na jednu stranu.</summary>
    /// <param name="sirkaTextu">šírka textu strany (body)</param>
    public static UvodnaStranaOblasti Pre(KapitolaOblasti oblast, SablonaVzhladu sablona, double sirkaTextu)
    {
        var vyska = PdfVystup.VyskaTextu * Rezerva;
        foreach (var (cislo, nazov, odstup, podPopisom) in Stupne)
        {
            var obsah = Hlavicka(podPopisom) + oblast.Predmety.Sum(p => Riadok(p.Nazov, cislo, nazov, odstup, sablona, sirkaTextu));
            if (obsah + PlnaMedzera <= vyska)
            {
                return new UvodnaStranaOblasti(PlnaMedzera, podPopisom, cislo, nazov, odstup);
            }

            if (obsah + NajmensiaMedzera <= vyska)
            {
                return new UvodnaStranaOblasti(Math.Floor(vyska - obsah), podPopisom, cislo, nazov, odstup);
            }
        }

        // veľmi veľa predmetov – najmenšie písmo bez medzery (zoznam pokračuje na ďalšej strane)
        var posledny = Stupne[^1];
        return new UvodnaStranaOblasti(0, posledny.PodPopisom, posledny.Cislo, posledny.Nazov, posledny.Odstup);
    }

    /// <summary>Výška nadpisu oblasti (26 b) s medzerou za ním a riadka s popisom.</summary>
    private static double Hlavicka(double podPopisom) => 26 * 1.25 + 10 + 10 * 1.3 + podPopisom;

    /// <summary>Odhad výšky riadka zoznamu – názov sa zalomí podľa šírky stĺpca a priemernej šírky znakov písma.</summary>
    private static double Riadok(string nazov, double cislo, double velkost, double odstup, SablonaVzhladu sablona, double sirkaTextu)
    {
        var sirokePismo = sablona.PismoTextu is "Verdana" or "Tahoma" or "Trebuchet MS" or "Arial" or "Segoe UI";
        var znakovNaRiadok = Math.Max(10, (sirkaTextu - SirkaCisiel) / (velkost * (sirokePismo ? 0.56 : 0.5)));
        var riadkov = Math.Max(1, Math.Ceiling(nazov.Length * 1.08 / znakovNaRiadok));
        var vyskaRiadka = velkost * 1.2 * Math.Max(1, sablona.Riadkovanie);
        return Math.Max(cislo * 1.25, riadkov * vyskaRiadka + 2) + 2 * odstup + 1;
    }
}
