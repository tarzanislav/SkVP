using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Vzdelávacia oblasť podľa Štátneho vzdelávacieho programu: kapitola, názov, predmety a charakteristika.</summary>
public sealed class OblastSvp
{
    public string Kapitola { get; set; } = "";
    public string Nazov { get; set; } = "";
    public List<string> Predmety { get; set; } = new();
    public List<string> Charakteristika { get; set; } = new();
    public List<string> Poznamky { get; set; } = new();
}

/// <summary>Štátny vzdelávací program (zdroj, vydavateľ, adresa a vzdelávacie oblasti).</summary>
public sealed class DokumentSvp
{
    public string Nazov { get; set; } = "";
    public string Vydavatel { get; set; } = "";
    public string Url { get; set; } = "";
    public List<OblastSvp> Oblasti { get; set; } = new();
}

/// <summary>
/// Vzdelávacie oblasti a ich charakteristiky podľa Štátneho vzdelávacieho programu pre gymnáziá (ŠPÚ, kapitola 4 –
/// doslovné znenie, Tab. 1 – predmety pre štvorročné a päťročné štúdium). Použije sa pri prvom spustení bez testovacej
/// sady, v testovacej sade a na prevzatie charakteristiky oblasti (sekcia Osnovy → oblasť → Charakteristika podľa ŠVP).
/// </summary>
public static class SvpGymnazia
{
    private static DokumentSvp? _dokument;

    public static DokumentSvp Dokument
    {
        get
        {
            if (_dokument is null)
            {
                _dokument = ZabudovaneZdroje.Json<DokumentSvp>("SkVP.SvpGymnazia.json", "V programe chýba Štátny vzdelávací program pre gymnáziá.");
            }

            return _dokument;
        }
    }

    public static IReadOnlyList<OblastSvp> Oblasti => Dokument.Oblasti;

    /// <summary>Oblasť ŠVP podľa názvu (bez ohľadu na veľkosť písmen a diakritiku), alebo null.</summary>
    public static OblastSvp? Oblast(string nazov) =>
        Oblasti.FirstOrDefault(o => ImportOsnov.Kluc(o.Nazov) == ImportOsnov.Kluc(nazov));

    private static readonly (string Oblast, string[] Slova)[] SlovaOblasti =
    {
        ("Jazyk a komunikácia", new[] { "jazyk", "literatur", "konverzac", "realie", "sloh", "komunikac" }),
        ("Matematika a práca s informáciami", new[] { "matemat", "informat", "technik", "programov", "robotik", "algoritm", "statistik" }),
        ("Človek a príroda", new[] { "fyzik", "chemi", "biologi", "prirodoved", "astronom", "ekologi", "laborator", "prirod" }),
        ("Človek a spoločnosť", new[] { "dejepis", "dejin", "geograf", "obcian", "financn", "ekonom", "spolocensk", "psycholog", "sociolog", "filozof", "pravn", "regional" }),
        ("Človek a hodnoty", new[] { "etick", "nabozen", "hodnot" }),
        ("Umenie a kultúra", new[] { "umeni", "kultur", "hudob", "vytvar", "dramat", "film", "dizajn" }),
        ("Zdravie a pohyb", new[] { "telesn", "sport", "zdrav", "pohyb", "plavan", "lyzov" }),
    };

    /// <summary>
    /// Vzdelávacia oblasť, do ktorej patrí predmet: podľa učebného plánu gymnázia, zoznamu predmetov ŠVP a kľúčových slov
    /// („Seminár z fyziky“ → Človek a príroda, „Konverzácia v anglickom jazyku“ → Jazyk a komunikácia). Null = nevie sa.
    /// </summary>
    public static string? OblastPredmetu(string predmet)
    {
        var kluc = ImportOsnov.Kluc(JazykovePravidla.BezUrovne(predmet));
        foreach (var oblast in UcebnyPlanService.Vstavany().Oblasti)
        {
            if (oblast.Predmety.Any(p => ImportOsnov.Kluc(p) == kluc))
            {
                return Oblast(oblast.Nazov)?.Nazov ?? oblast.Nazov;
            }
        }

        foreach (var oblast in Oblasti)
        {
            if (oblast.Predmety.Any(p => ImportOsnov.Kluc(p) == kluc))
            {
                return oblast.Nazov;
            }
        }

        // jazyky skôr (Konverzácia v ruskom jazyku), potom ostatné slová podľa poradia oblastí
        foreach (var (oblast, slova) in SlovaOblasti.Skip(1).Append(SlovaOblasti[0]))
        {
            // „Informatika v anglickom jazyku“ patrí k informatike, preto sa jazyky skúšajú až nakoniec
            if (slova.Any(sl => kluc.Contains(sl, StringComparison.Ordinal)))
            {
                return oblast;
            }
        }

        return null;
    }

    /// <summary>Text charakteristiky oblasti ako dokument: odseky ŠVP, poznámky pod čiarou a zdroj.</summary>
    public static List<Blok> Charakteristika(OblastSvp oblast)
    {
        var bloky = oblast.Charakteristika.Select(t => (Blok)new Odsek(new[] { new Usek(t) })).ToList();
        foreach (var poznamka in oblast.Poznamky)
        {
            bloky.Add(new Odsek(new[] { new Usek(poznamka, Kurziva: true) }));
        }

        bloky.Add(new Odsek(new[] { new Usek($"Zdroj: {Dokument.Nazov}, kapitola {oblast.Kapitola} {oblast.Nazov}. {Dokument.Vydavatel}.", Kurziva: true) }));
        return bloky;
    }

    /// <summary>Je dokument charakteristiky oblasti prevzatý zo ŠVP (bez úprav)?</summary>
    public static bool JePrevzata(IReadOnlyList<Blok> bloky, OblastSvp oblast) =>
        DokumentOsnovy.Text(bloky).Trim() == DokumentOsnovy.Text(Charakteristika(oblast)).Trim();

    /// <summary>
    /// Vytvorí vzdelávacie oblasti podľa ŠVP (chýbajúce) s charakteristikami. Predmety sa nevytvárajú – doplnia sa
    /// z osnov (import z priečinka) alebo ručne. Vráti počet nových oblastí.
    /// </summary>
    public static int VytvorOblasti()
    {
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var existujuce = sluzba.Oblasti();
        var nove = 0;
        foreach (var oblast in Oblasti)
        {
            var objekt = existujuce.FirstOrDefault(o => ImportOsnov.Kluc(o.Nazov) == ImportOsnov.Kluc(oblast.Nazov));
            if (objekt is null)
            {
                objekt = sluzba.Pridaj(TypObjektu.Oblast, null, oblast.Nazov, UcebnyPlanService.FarbaOblasti(existujuce.Count + nove));
                nove++;
            }

            if (!dokumenty.Existuje(objekt.Id))
            {
                dokumenty.UlozBloky(objekt.Id, Charakteristika(oblast), null);
            }
        }

        if (Databaza.Citaj(UcebnyPlanService.KlucTypSkoly) is not { Length: > 0 })
        {
            Databaza.Zapis(UcebnyPlanService.KlucTypSkoly, TitulnaStrana.AktualnyStupenGymnazia);
        }

        return nove;
    }

    public const string DovodPrevzatia = "Pred prevzatím charakteristiky zo Štátneho vzdelávacieho programu";

    /// <summary>
    /// Prevezme charakteristiky všetkých oblastí, ktoré sú v ŠVP (doterajšie texty sa zarchivujú). Pri
    /// <paramref name="lenUkazkove"/> len tie, ktoré sú ešte nezmenenou ukážkovou charakteristikou testovacej sady
    /// (aktualizácia programu), alebo chýbajú. Vráti počet prevzatých.
    /// </summary>
    public static int PrevezmiVsetky(bool lenUkazkove)
    {
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var ukazkove = lenUkazkove
            ? TestovaciaSada.Nacitaj().Oblasti.ToDictionary(o => ImportOsnov.Kluc(o.Nazov), o => DokumentOsnovy.Text(TestovaciaSada.UkazkovaCharakteristika(o)).Trim())
            : new Dictionary<string, string>();
        var pocet = 0;
        foreach (var oblast in sluzba.Oblasti())
        {
            if (Oblast(oblast.Nazov) is not { } svp)
            {
                continue;
            }

            var existuje = dokumenty.Existuje(oblast.Id);
            var text = existuje ? DokumentOsnovy.Text(dokumenty.Nacitaj(oblast.Id).Bloky).Trim() : "";
            if (existuje && JePrevzata(dokumenty.Nacitaj(oblast.Id).Bloky, svp))
            {
                continue;
            }

            if (lenUkazkove && existuje && text.Length > 0 && !(ukazkove.TryGetValue(ImportOsnov.Kluc(oblast.Nazov), out var u) && u == text))
            {
                continue; // vlastný text školy sa bez opýtania nemení
            }

            dokumenty.UlozBloky(oblast.Id, Charakteristika(svp), DovodPrevzatia);
            pocet++;
        }

        return pocet;
    }

    /// <summary>Nahradí charakteristiku oblasti textom ŠVP (doterajšia sa zarchivuje). Vráti false, ak oblasť nie je v ŠVP.</summary>
    public static bool PrevezmiCharakteristiku(long oblastId, string nazovOblasti)
    {
        if (Oblast(nazovOblasti) is not { } oblast)
        {
            return false;
        }

        new DokumentService().UlozBloky(oblastId, Charakteristika(oblast), DovodPrevzatia);
        return true;
    }
}
