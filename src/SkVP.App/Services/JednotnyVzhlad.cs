using System.Text.RegularExpressions;

namespace SkVP.Services;

/// <summary>Dokument prepracovaný do jednotného vzhľadu a čo sa pri tom zmenilo.</summary>
/// <param name="Nadpisov">koľko odsekov sa rozpoznalo ako nadpis časti osnovy</param>
/// <param name="HluchychMiest">koľko nadbytočných prázdnych riadkov a zlomov strán sa vynechalo</param>
/// <param name="Straty">prvky dokumentu, ktoré jednotný vzhľad nemá (obrázky, textové polia …) a stratili by sa</param>
public sealed record PrepracovanyDokument(List<Blok> Bloky, int Nadpisov, int HluchychMiest, IReadOnlyList<string> Straty)
{
    public RozlozenieStrany Rozlozenie => DokumentOsnovy.Rozlozenie(Bloky);
}

/// <summary>
/// Jednotný vzhľad dokumentu osnovy – rovnaký ako osnovy vytvorené programom (testovacia sada): písma, nadpisy častí
/// vo farbe oblasti, tabuľky s jemným rámom, bez hluchých miest. Text, tabuľky, zoznamy aj rozloženie strany
/// (napr. osnova na šírku) zostanú; nadpisy písané ako tučný text alebo veľkými písmenami („1. CHARAKTERISTIKA
/// PREDMETU“) sa zmenia na skutočné nadpisy.
/// </summary>
public static partial class JednotnyVzhlad
{
    /// <summary>Najdlhší text, ktorý môže byť nadpisom (dlhší je bežný odsek).</summary>
    private const int NajdlhsiNadpis = 80;

    /// <summary>Začiatky názvov častí učebnej osnovy (malé písmená bez diakritiky).</summary>
    private static readonly string[] CastiOsnovy =
    {
        "charakteristika", "ciel", "ciele", "obsah", "obsahovy standard", "vykonovy standard", "obsahovy a vykonovy",
        "rozpis uciva", "rozpis", "tematicke celky", "tematicky", "ucebne zdroje", "ucebne pomocky", "hodnotenie", "kriteria",
        "metody", "formy", "strategie", "strategia", "prierezove temy", "klucove kompetencie", "kompetencie", "pozadavky",
        "vzdelavacie standardy", "casovy rozsah", "organizacne formy", "didakticke", "zaradenie predmetu", "poznamky",
    };

    /// <summary>Pripraví jednotný vzhľad dokumentu (nič neukladá).</summary>
    public static PrepracovanyDokument Priprav(NacitanyDokument povodny)
    {
        var upratane = DokumentOsnovy.Upratane(povodny.Bloky);
        var nadpisov = 0;
        var bloky = new List<Blok>(upratane.Count);
        foreach (var blok in upratane)
        {
            switch (blok)
            {
                case Odsek or Zoznam when AkoNadpis(blok) is { } nadpis:
                    bloky.Add(nadpis);
                    nadpisov++;
                    break;

                case Tabulka tabulka:
                    bloky.Add(SHlavickou(tabulka));
                    break;

                default:
                    bloky.Add(blok);
                    break;
            }
        }

        // pri nových nadpisoch netreba prázdne riadky
        var vysledok = DokumentOsnovy.Upratane(bloky);
        return new PrepracovanyDokument(vysledok, nadpisov, Math.Max(0, Hluche(povodny.Bloky) - Hluche(vysledok)), povodny.Varovania);
    }

    /// <summary>Prázdne riadky a zlomy strán (na súhrn, koľko hluchých miest zmizlo).</summary>
    private static int Hluche(IEnumerable<Blok> bloky) =>
        bloky.Count(b => b is ZlomStrany || (b is Odsek o && o.Useky.All(u => u.Text.Trim().Length == 0)));

    /// <summary>
    /// Odsek (alebo jediná položka číslovaného zoznamu), ktorý je v skutočnosti nadpisom: krátky, bez bodky na konci,
    /// celý tučný alebo veľkými písmenami. Názov časti osnovy (Charakteristika, Ciele, Hodnotenie …) je nadpis 1,
    /// iný zvýraznený riadok nadpis 2 (napr. ročník).
    /// </summary>
    public static Nadpis? AkoNadpis(Blok blok)
    {
        var (odsek, vZozname) = blok switch
        {
            Odsek o => (o, false),
            Zoznam { Polozky: [[Odsek o]] } => (o, true),
            _ => ((Odsek?)null, false),
        };
        if (odsek is null || odsek.Useky.Any(u => u.NovyRiadok))
        {
            return null;
        }

        var celyText = string.Concat(odsek.Useky.Select(u => u.Text)).Trim();
        var text = Cislovanie().Replace(celyText, "").Trim();
        if (text.Length is < 3 || celyText.Length > NajdlhsiNadpis || text[^1] is '.' or ',' or ';' or ':' || !text.Any(char.IsLetter))
        {
            return null;
        }

        var tucny = odsek.Useky.Where(u => u.Text.Trim().Length > 0).All(u => u.Tucne);
        var velkymi = text.Where(char.IsLetter).All(char.IsUpper);
        var kluc = ImportOsnov.Kluc(text);
        var castOsnovy = CastiOsnovy.Any(c => kluc.StartsWith(c, StringComparison.Ordinal));
        if (castOsnovy && (tucny || velkymi || vZozname))
        {
            return new Nadpis(1, Pekny(text, velkymi));
        }

        // zvýraznený riadok v texte (nie položka zoznamu – tá je obyčajne naozaj zoznam)
        return !vZozname && (tucny || velkymi) && celyText.Length <= 60 ? new Nadpis(2, Pekny(celyText, velkymi)) : null;
    }

    /// <summary>Nadpis písaný veľkými písmenami sa prepíše ako veta („CIELE PREDMETU“ → „Ciele predmetu“).</summary>
    private static string Pekny(string text, bool velkymi)
    {
        if (!velkymi)
        {
            return text;
        }

        var male = text.ToLower(Planovanie.Sk);
        var prve = male.IndexOf(male.FirstOrDefault(char.IsLetter));
        return prve < 0 ? male : male[..prve] + char.ToUpper(male[prve], Planovanie.Sk) + male[(prve + 1)..];
    }

    /// <summary>
    /// Tabuľka s tučným prvým riadkom dostane podfarbenú hlavičku – okrem tabuliek „popis – hodnota“ (prvý stĺpec
    /// tučný v každom riadku, napr. úvodná tabuľka s názvom predmetu).
    /// </summary>
    private static Tabulka SHlavickou(Tabulka tabulka)
    {
        if (tabulka.Riadky.Count < 2 || tabulka.Riadky[0].Count == 0 || tabulka.Riadky[0].Any(b => b.Hlavicka))
        {
            return tabulka;
        }

        static bool Tucna(Bunka b)
        {
            var useky = b.Bloky.OfType<Odsek>().SelectMany(o => o.Useky).Where(u => u.Text.Trim().Length > 0).ToList();
            return useky.Count > 0 && useky.All(u => u.Tucne);
        }

        var popisHodnota = tabulka.Riadky.Skip(1).All(r => r.Count > 0 && Tucna(r[0]));
        if (popisHodnota || !tabulka.Riadky[0].All(Tucna))
        {
            return tabulka;
        }

        var riadky = tabulka.Riadky.ToList();
        riadky[0] = riadky[0].Select(b => b with { Hlavicka = true }).ToList();
        return tabulka with { Riadky = riadky };
    }

    /// <summary>Číslovanie na začiatku nadpisu („1.“, „2.1“, „IV.“, „a)“).</summary>
    [GeneratedRegex(@"^((\d+(\.\d+)*[.)]?|([IVX]+|[a-z])[.)])\s+)+", RegexOptions.CultureInvariant)]
    private static partial Regex Cislovanie();
}
