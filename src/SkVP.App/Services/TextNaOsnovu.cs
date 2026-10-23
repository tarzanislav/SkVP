using System.Text.RegularExpressions;

namespace SkVP.Services;

/// <summary>
/// Prevod prečítaného textu PDF (riadky z rozpoznávania textu) na štruktúrovaný text osnovy – nadpisy, odseky, zoznamy
/// a jednoduché tabuľky – vlastnými prostriedkami, bez Wordu. Výsledok sa uloží ako dokument Word.
/// </summary>
public static partial class TextNaOsnovu
{
    private static readonly string[] ZnamyNadpis =
    {
        "Charakteristika predmetu", "Charakteristika vyučovacieho predmetu", "Ciele predmetu", "Ciele vyučovacieho predmetu",
        "Obsahový a výkonový štandard", "Obsahový štandard", "Výkonový štandard", "Rozpis učiva", "Rozpis učiva podľa ročníkov",
        "Metódy a formy práce", "Stratégie vyučovania", "Hodnotenie predmetu", "Hodnotenie a klasifikácia", "Učebné zdroje",
        "Učebné osnovy", "Kľúčové kompetencie", "Prierezové témy", "Tematické celky", "Učebné pomôcky", "Kritériá hodnotenia",
    };

    [GeneratedRegex(@"^\s*([•▪◦·*\-–—])\s+(.+)$")]
    private static partial Regex Odrazka();

    [GeneratedRegex(@"^\s*(\d{1,2}|[a-z])[.)]\s+(.+)$")]
    private static partial Regex Cislovanie();

    [GeneratedRegex(@"^\s*\d\.\s*ročník\b", RegexOptions.IgnoreCase)]
    private static partial Regex Rocnik();

    public static List<Blok> Rozober(string text)
    {
        var riadky = text.Replace("\r", "").Split('\n').Select(r => r.TrimEnd()).ToList();
        var bloky = new List<Blok>();
        var odsek = new List<string>();
        var polozky = new List<string>();
        var cislovany = false;
        var tabulka = new List<string[]>();

        void UkonciOdsek()
        {
            if (odsek.Count > 0)
            {
                bloky.Add(new Odsek(new[] { new Usek(string.Join(" ", odsek)) }));
                odsek.Clear();
            }
        }

        void UkonciZoznam()
        {
            if (polozky.Count > 0)
            {
                bloky.Add(new Zoznam(cislovany, polozky.Select(p => (IReadOnlyList<Blok>)new Blok[] { new Odsek(new[] { new Usek(p) }) }).ToList()));
                polozky.Clear();
            }
        }

        void UkonciTabulku()
        {
            if (tabulka.Count >= 2)
            {
                var stlpce = tabulka.Max(r => r.Length);
                var riadkyTabulky = tabulka.Select((r, i) => (IReadOnlyList<Bunka>)Enumerable.Range(0, stlpce)
                    .Select(s => new Bunka(new Blok[] { new Odsek(new[] { new Usek(s < r.Length ? r[s].Trim() : "", Tucne: i == 0) }) }, Hlavicka: i == 0))
                    .ToList()).ToList();
                bloky.Add(new Tabulka(riadkyTabulky, stlpce));
            }
            else
            {
                // jeden riadok s tabulátorom je obyčajný text
                odsek.AddRange(tabulka.Select(r => string.Join(" ", r.Select(x => x.Trim()))));
            }

            tabulka.Clear();
        }

        foreach (var riadok in riadky)
        {
            var orezany = riadok.Trim();
            if (orezany.Length == 0)
            {
                UkonciTabulku();
                UkonciOdsek();
                UkonciZoznam();
                continue;
            }

            if (riadok.Contains('\t') && riadok.Split('\t').Count(c => c.Trim().Length > 0) >= 2)
            {
                UkonciOdsek();
                UkonciZoznam();
                tabulka.Add(riadok.Split('\t').Where(c => c.Trim().Length > 0).ToArray());
                continue;
            }

            UkonciTabulku();
            if (JeNadpis(orezany, out var uroven))
            {
                UkonciOdsek();
                UkonciZoznam();
                bloky.Add(new Nadpis(uroven, orezany.TrimEnd(':')));
                continue;
            }

            var odrazka = Odrazka().Match(orezany);
            var cislo = Cislovanie().Match(orezany);
            if (odrazka.Success || cislo.Success)
            {
                UkonciOdsek();
                var noveCislovanie = !odrazka.Success;
                if (polozky.Count > 0 && noveCislovanie != cislovany)
                {
                    UkonciZoznam();
                }

                cislovany = noveCislovanie;
                polozky.Add((odrazka.Success ? odrazka.Groups[2].Value : cislo.Groups[2].Value).Trim());
                continue;
            }

            if (polozky.Count > 0 && odsek.Count == 0 && char.IsLower(orezany[0]))
            {
                // pokračovanie položky zoznamu na ďalšom riadku
                polozky[^1] += " " + orezany;
                continue;
            }

            UkonciZoznam();
            if (odsek.Count > 0 && KoniecVety(odsek[^1]) && char.IsUpper(orezany[0]) && odsek[^1].Length < 60)
            {
                UkonciOdsek();
            }

            odsek.Add(orezany);
        }

        UkonciTabulku();
        UkonciOdsek();
        UkonciZoznam();
        return bloky;
    }

    private static bool KoniecVety(string riadok) => riadok.EndsWith('.') || riadok.EndsWith(':') || riadok.EndsWith('!') || riadok.EndsWith('?');

    private static bool JeNadpis(string riadok, out int uroven)
    {
        uroven = 1;
        var bez = riadok.TrimEnd(':').Trim();
        if (ZnamyNadpis.Any(n => string.Equals(n, bez, StringComparison.CurrentCultureIgnoreCase)))
        {
            return true;
        }

        if (Rocnik().IsMatch(bez) && bez.Length < 40)
        {
            uroven = 2;
            return true;
        }

        // krátky riadok písaný veľkými písmenami
        var pismena = bez.Where(char.IsLetter).ToList();
        return bez.Length is >= 4 and <= 80 && pismena.Count >= 4 && pismena.All(char.IsUpper);
    }
}
