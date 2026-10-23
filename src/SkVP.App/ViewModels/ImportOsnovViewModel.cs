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
                var id = _predmetId ?? subor!.PredmetId;
                polozka.Ciel = Ciele.FirstOrDefault(c => c.PredmetId == id && id is not null) ?? NovyPredmet;
                polozka.NovyNazov = subor!.NavrhNazvu;
                polozka.NovaOblast = Hlavny.Oblasti.FirstOrDefault();
                if (subor.Rok is int rok)
                {
                    polozka.Rok = rok.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                polozka.Stav = polozka.JeNovy ? "Predmet sa nenašiel – vytvorí sa nový" : "Predmet rozpoznaný";
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
    public int Importovat()
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
                }
                else
                {
                    id = p.Ciel!.PredmetId!.Value;
                }

                var vysledok = ImportOsnov.Importuj(p.Subor!, id, int.Parse(p.Rok.Trim(), System.Globalization.CultureInfo.InvariantCulture));
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
