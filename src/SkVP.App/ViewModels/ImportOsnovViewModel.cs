using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Kam sa osnova naimportuje: existujúci predmet, alebo nový predmet (PredmetId = null).</summary>
public sealed record CielImportu(long? PredmetId, string Popis)
{
    public override string ToString() => Popis;
}

/// <summary>Jeden súbor v sprievodcovi importom osnov.</summary>
public sealed partial class PolozkaImportu : ObservableObject
{
    public PolozkaImportu(string cesta) => Cesta = cesta;

    /// <summary>Voľby úrovne cudzieho jazyka (prázdna hodnota = bez úrovne).</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> Urovne { get; } = new[]
    {
        new KeyValuePair<string, string>("B1", "B1"),
        new KeyValuePair<string, string>("B2", "B2"),
        new KeyValuePair<string, string>("", "bez úrovne"),
    };

    /// <summary>Názov existujúceho predmetu podľa id (nastaví sprievodca) – pri ručnej zmene predmetu sa podľa neho doplní názov.</summary>
    public Func<long, string?>? NazovPredmetu { get; set; }

    /// <summary>Sprievodca práve nastavuje návrh (zmena predmetu nemení názov).</summary>
    public bool Nastavuje { get; set; }

    partial void OnCielChanged(CielImportu? value)
    {
        if (Nastavuje || value?.PredmetId is not long id || NazovPredmetu?.Invoke(id) is not { } nazov)
        {
            return;
        }

        // používateľ vybral iný predmet – názov a úroveň podľa neho
        Zaklad = JazykovePravidla.BezUrovne(nazov);
        CudziJazyk = JazykovePravidla.JeCudziJazyk(Zaklad);
        NovyNazov = nazov;
        Uroven = CudziJazyk ? JazykovePravidla.UrovenZNazvu(nazov) : "";
        NovyNazov = nazov;
    }

    /// <summary>Názov predmetu bez úrovne (z pravidiel, rozpoznaného predmetu alebo súboru).</summary>
    public string Zaklad { get; set; } = "";

    /// <summary>Cudzí jazyk – sprievodca sa pýta na úroveň.</summary>
    [ObservableProperty]
    private bool _cudziJazyk;

    /// <summary>Úroveň cudzieho jazyka: B1, B2, "" = bez úrovne, null = ešte nezvolená.</summary>
    [ObservableProperty]
    private string? _uroven;

    /// <summary>Po zmene úrovne sa názov predmetu doplní („Ruský jazyk – úroveň B1“).</summary>
    partial void OnUrovenChanged(string? value)
    {
        if (Zaklad.Length > 0)
        {
            NovyNazov = JazykovePravidla.NazovSUrovnou(Zaklad, value);
        }
    }

    public string Cesta { get; }

    public string NazovSuboru => System.IO.Path.GetFileName(Cesta);

    public SuborImportu? Subor { get; set; }

    public string Druh => Subor is null ? "" : Subor.JePdf ? "PDF → Word" : "Word";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeNovy))]
    private CielImportu? _ciel;

    public bool JeNovy => Ciel?.PredmetId is null;

    [ObservableProperty]
    private OblastVM? _novaOblast;

    /// <summary>Názov predmetu po importe – nový predmet sa tak nazve, existujúci sa naň premenuje.</summary>
    [ObservableProperty]
    private string _novyNazov = "";

    [ObservableProperty]
    private string _rok = DateTime.Today.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [ObservableProperty]
    private bool _zahrnut = true;

    [ObservableProperty]
    private string _stav = "";

    public bool MaChybu => Subor is null;
}

/// <summary>
/// Sprievodca importom osnov z Wordu a PDF: pri každom súbore program navrhne predmet (podľa názvu súboru alebo textu),
/// rok zmeny osnov a pri neznámom predmete nový predmet v oblasti. Pred importom sa urobí záloha a doterajšie osnovy
/// sa zarchivujú.
/// </summary>
public sealed partial class ImportOsnovViewModel : ObservableObject
{
    private readonly List<(long Id, string Nazov)> _predmety;

    public ImportOsnovViewModel(MainViewModel hlavny, long? predmetId = null)
    {
        Hlavny = hlavny;
        _predmetId = predmetId;
        _predmety = ImportOsnov.Predmety();
        Ciele.Add(NovyPredmet);
        foreach (var oblast in hlavny.Oblasti)
        {
            foreach (var predmet in oblast.Predmety)
            {
                Ciele.Add(new CielImportu(predmet.Id, $"{predmet.Cislo} {predmet.Nazov}  ({oblast.Nazov})"));
            }
        }
    }

    private readonly long? _predmetId;

    public static readonly CielImportu NovyPredmet = new(null, "➕ Nový predmet…");

    public MainViewModel Hlavny { get; }

    public ObservableCollection<CielImportu> Ciele { get; } = new();

    public ObservableCollection<OblastVM> Oblasti => Hlavny.Oblasti;

    public ObservableCollection<PolozkaImportu> Polozky { get; } = new();

    [ObservableProperty]
    private string _suhrn = "";

    /// <summary>Načíta súbory (Word hneď, PDF sa prečíta rozpoznávaním textu) a navrhne predmety.</summary>
    public void PridajSubory(IEnumerable<string> cesty)
    {
        foreach (var cesta in cesty)
        {
            if (Polozky.Any(p => string.Equals(p.Cesta, cesta, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var polozka = new PolozkaImportu(cesta);
            try
            {
                SuborImportu? subor = null;
                Dialogy.Cakaj(() => subor = ImportOsnov.Nacitaj(cesta, _predmety));
                polozka.Subor = subor;
                NastavCiel(polozka, subor!);
                if (subor!.Rok is int rok)
                {
                    polozka.Rok = rok.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                polozka.Stav = (polozka.JeNovy ? "Predmet sa nenašiel – vytvorí sa nový" : "Predmet rozpoznaný")
                               + (polozka.CudziJazyk && polozka.Uroven is null ? " • zvoľte úroveň jazyka" : "");
            }
            catch (SkvpException ex)
            {
                polozka.Zahrnut = false;
                polozka.Stav = ex.Message;
            }
            catch (Exception ex)
            {
                polozka.Zahrnut = false;
                polozka.Stav = "Súbor sa nepodarilo prečítať: " + ex.GetBaseException().Message;
            }

            Polozky.Add(polozka);
        }

        ObnovSuhrn();
    }

    /// <summary>
    /// Predmet, názov a úroveň pre súbor. Jazykové osnovy podľa pravidiel (skratky, profilácia, úroveň); predmet, ktorý už
    /// patrí inému súboru v zozname, sa znova nepoužije – napr. druhá profilácia konverzácie sa stane novým predmetom.
    /// </summary>
    private void NastavCiel(PolozkaImportu polozka, SuborImportu subor)
    {
        polozka.Nastavuje = true;
        polozka.NazovPredmetu = id => _predmety.FirstOrDefault(p => p.Id == id).Nazov;
        var obsadene = Polozky.Select(p => p.Ciel?.PredmetId).OfType<long>().ToHashSet();
        long? id;
        if (_predmetId is long pevny)
        {
            id = pevny;
        }
        else if (subor.Jazyk is { } jazyk)
        {
            id = ImportOsnov.Kandidati(jazyk, _predmety).Select(p => (long?)p.Id).FirstOrDefault(i => i is long x && !obsadene.Contains(x));
        }
        else
        {
            id = subor.PredmetId is long i && !obsadene.Contains(i) ? i : null;
        }

        polozka.Ciel = Ciele.FirstOrDefault(c => c.PredmetId == id && id is not null) ?? NovyPredmet;
        var existujuci = polozka.Ciel.PredmetId is long e ? _predmety.First(p => p.Id == e).Nazov : null;
        polozka.Zaklad = subor.Jazyk?.Zaklad ?? JazykovePravidla.BezUrovne(existujuci ?? subor.NavrhNazvu);
        polozka.CudziJazyk = subor.Jazyk?.CudziJazyk ?? JazykovePravidla.JeCudziJazyk(polozka.Zaklad);
        polozka.NovyNazov = subor.Jazyk is { } j ? JazykovePravidla.NazovSUrovnou(j.Zaklad, j.Uroven) : existujuci ?? subor.NavrhNazvu;
        polozka.Uroven = subor.Jazyk is { } r ? r.Uroven ?? (r.CudziJazyk ? null : "")
            : !polozka.CudziJazyk ? "" : existujuci is not null ? JazykovePravidla.UrovenZNazvu(existujuci) : null;
        if (polozka.Uroven is null && !polozka.CudziJazyk)
        {
            polozka.Uroven = "";
        }

        // oblasť nového predmetu: podpriečinok s názvom oblasti („03 Človek a príroda“), jazykový predmet do oblasti
        // Jazyk a komunikácia, ostatné podľa učebného plánu, ŠVP a kľúčových slov
        var priecinokOblasti = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(polozka.Cesta) ?? "");
        var oblastZPriecinka = Hlavny.Oblasti.FirstOrDefault(o => ImportOsnov.Kluc(ImportOsnov.CistyNazov(priecinokOblasti)) == ImportOsnov.Kluc(o.Nazov))?.Nazov;
        var oblastPredmetu = oblastZPriecinka
                             ?? (subor.Jazyk is not null ? JazykovePravidla.Oblast : SvpGymnazia.OblastPredmetu(polozka.NovyNazov));
        polozka.NovaOblast = (oblastPredmetu is not null
                                 ? Hlavny.Oblasti.FirstOrDefault(o => ImportOsnov.Kluc(o.Nazov) == ImportOsnov.Kluc(oblastPredmetu))
                                 : null)
                             ?? Hlavny.Oblasti.FirstOrDefault();
        polozka.Nastavuje = false;
    }

    /// <summary>
    /// Všetky osnovy z priečinka aj jeho podpriečinkov (Word a PDF) – súbory sa len čítajú. Podpriečinok s názvom oblasti
    /// („03 Človek a príroda“) určí oblasť nového predmetu; priečinky Archív a dočasné súbory Wordu sa preskočia.
    /// </summary>
    public void PridajPriecinok(string priecinok)
    {
        var subory = System.IO.Directory.EnumerateFiles(priecinok, "*", new System.IO.EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Where(f => ImportOsnov.Pripony.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant())
                        && !System.IO.Path.GetFileName(f).StartsWith("~$", StringComparison.Ordinal)
                        && !System.IO.Path.GetRelativePath(priecinok, f).Split(System.IO.Path.DirectorySeparatorChar)
                            .SkipLast(1).Any(c => ImportOsnov.Kluc(c).StartsWith("archiv", StringComparison.Ordinal)))
            .OrderBy(f => System.IO.Path.GetRelativePath(priecinok, f), StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (subory.Count == 0)
        {
            Dialogy.Upozornenie("V priečinku nie sú žiadne osnovy vo Worde (.docx) ani PDF.");
            return;
        }

        PridajSubory(subory);
    }

    [RelayCommand]
    private void PridatPriecinok()
    {
        if (Dialogy.VybratPriecinok("Priečinok s osnovami na import") is { } priecinok)
        {
            PridajPriecinok(priecinok);
        }
    }

    private void ObnovSuhrn()
    {
        var spolu = Polozky.Count(p => p.Subor is not null);
        Suhrn = $"Súborov: {Polozky.Count}, pripravené na import: {spolu}. Doterajšie osnovy sa pred importom zarchivujú (s rokom archivácie).";
    }

    [RelayCommand]
    private void PridatSubory() => PridajSubory(Dialogy.OtvoritViac("Osnovy na import", ImportOsnov.FilterSuborov));

    [RelayCommand]
    private void Odobrat(PolozkaImportu? polozka)
    {
        if (polozka is not null)
        {
            Polozky.Remove(polozka);
            ObnovSuhrn();
        }
    }

    /// <summary>Výsledky posledného importu (samotest).</summary>
    public List<VysledokImportuOsnovy> Vysledky { get; } = new();

    /// <summary>Naimportuje zaškrtnuté súbory. Vráti počet naimportovaných osnov.</summary>
    [RelayCommand]
    private void Importovat() => Importuj();

    public int Importuj()
    {
        var naImport = Polozky.Where(p => p.Zahrnut && p.Subor is not null).ToList();
        if (naImport.Count == 0)
        {
            Dialogy.Upozornenie("Nie je vybraný žiadny súbor na import.");
            return 0;
        }

        foreach (var p in naImport)
        {
            if (!int.TryParse(p.Rok.Trim(), out var r) || r < 1950 || r > 2200)
            {
                Dialogy.Upozornenie($"{p.NazovSuboru}: zadajte rok zmeny osnov (napr. {DateTime.Today.Year}).");
                return 0;
            }

            if (p.JeNovy && (p.NovaOblast is null || p.NovyNazov.Trim().Length == 0))
            {
                Dialogy.Upozornenie($"{p.NazovSuboru}: zadajte názov nového predmetu a oblasť.");
                return 0;
            }

            if (p.CudziJazyk && p.Uroven is null)
            {
                Dialogy.Upozornenie($"{p.NazovSuboru}: zvoľte úroveň cudzieho jazyka (B1, B2 alebo bez úrovne).");
                return 0;
            }

            if (p.NovyNazov.Trim().Length == 0)
            {
                Dialogy.Upozornenie($"{p.NazovSuboru}: zadajte názov predmetu.");
                return 0;
            }
        }

        try
        {
            new ZalohaService().Zalohuj();
        }
        catch (Exception ex)
        {
            if (!Dialogy.Potvrd($"Zálohu sa nepodarilo urobiť ({ex.GetBaseException().Message}). Pokračovať v importe aj tak? Doterajšie osnovy sa zarchivujú v programe."))
            {
                return 0;
            }
        }

        Vysledky.Clear();
        var pocet = 0;
        foreach (var p in naImport)
        {
            try
            {
                long id;
                if (p.JeNovy)
                {
                    var novy = Hlavny.PridajPredmet(p.NovaOblast!, p.NovyNazov.Trim())
                               ?? throw new SkvpException("Nový predmet sa nepodarilo pridať.");
                    id = novy.Id;

                    // príbuzné predmety pod seba (napr. konverzácie v jednom bloku)
                    var oblast = p.NovaOblast!;
                    var ostatne = oblast.Predmety.Where(x => x != novy).Select(x => x.Nazov).ToList();
                    if (JazykovePravidla.Pozicia(ostatne, p.Subor!.Jazyk?.ZaraditZa) is int pozicia && pozicia < ostatne.Count)
                    {
                        Hlavny.PresunPredmet(novy, oblast, pozicia);
                    }
                }
                else
                {
                    id = p.Ciel!.PredmetId!.Value;
                    if (Hlavny.NajdiPredmet(id) is { } predmet && predmet.Nazov != p.NovyNazov.Trim())
                    {
                        // napr. „Ruský jazyk“ → „Ruský jazyk – úroveň B1“ (aj v textoch dokumentov)
                        Hlavny.PremenujPredmet(predmet, p.NovyNazov.Trim());
                    }
                }

                var vysledok = ImportOsnov.Importuj(p.Subor!, id, int.Parse(p.Rok.Trim(), System.Globalization.CultureInfo.InvariantCulture), p.Uroven);
                Vysledky.Add(vysledok);
                Hlavny.PoZmeneDokumentu(id, oznamit: true);
                p.Stav = !vysledok.Zmenene ? "Bez zmeny – osnova je rovnaká ako doteraz"
                    : vysledok.Archivovane ? "Naimportované, doterajšia osnova je v archíve" : "Naimportované";
                p.Zahrnut = false;
                pocet++;
            }
            catch (SkvpException ex)
            {
                p.Stav = ex.Message;
            }
            catch (Exception ex)
            {
                p.Stav = "Import sa nepodaril: " + ex.GetBaseException().Message;
            }
        }

        Suhrn = $"Naimportované osnovy: {pocet} z {naImport.Count}. Kópie sú v priečinku {ImportOsnov.PriecinokOsnov}.";
        Hlavny.Oznam($"Naimportované osnovy: {pocet}.");
        return pocet;
    }
}
