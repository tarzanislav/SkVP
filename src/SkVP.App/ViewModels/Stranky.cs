using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Dashboard – stav osnov po oblastiach a naposledy upravené predmety.</summary>
public sealed partial class DashboardViewModel : ObservableObject
{
    public DashboardViewModel(MainViewModel hlavny)
    {
        Hlavny = hlavny;
        var vsetky = hlavny.Oblasti.SelectMany(o => o.Predmety).ToList();
        PocetPredmetov = vsetky.Count;
        PocetHotovych = vsetky.Count(p => p.Stav == StavOsnovy.Hotova);
        PocetRozpracovanych = vsetky.Count(p => p.Stav is StavOsnovy.Rozpracovana or StavOsnovy.NaKontrolu);
        PocetNezacatych = vsetky.Count(p => p.Stav == StavOsnovy.Prazdna);
        Naposledy = vsetky.Where(p => p.DlzkaTextu > 0 || p.StavKod is not null)
            .OrderByDescending(p => p.Upravene).Take(6).ToList();
    }

    public MainViewModel Hlavny { get; }

    public ObservableCollection<OblastVM> Oblasti => Hlavny.Oblasti;

    public string Datum
    {
        get
        {
            var text = DateTime.Today.ToString("dddd d. MMMM yyyy", CultureInfo.GetCultureInfo("sk-SK"));
            return char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];
        }
    }

    public string Pozdrav => DateTime.Now.Hour switch
    {
        < 10 => "Dobré ráno",
        < 18 => "Dobrý deň",
        _ => "Dobrý večer",
    };

    public int PocetOblasti => Hlavny.Oblasti.Count;
    public int PocetPredmetov { get; }
    public int PocetHotovych { get; }
    public int PocetRozpracovanych { get; }
    public int PocetNezacatych { get; }
    public double Podiel => PocetPredmetov == 0 ? 0 : (double)PocetHotovych / PocetPredmetov;
    public string PodielText => PocetPredmetov == 0 ? "–" : $"{Math.Round(Podiel * 100):0} %";
    public IReadOnlyList<PredmetVM> Naposledy { get; }
    public bool NicNebolo => Naposledy.Count == 0;
}

/// <summary>Prehľad všetkých vzdelávacích oblastí (sekcia Osnovy).</summary>
public sealed partial class OsnovyViewModel : ObservableObject
{
    public OsnovyViewModel(MainViewModel hlavny) => Hlavny = hlavny;

    public MainViewModel Hlavny { get; }

    public ObservableCollection<OblastVM> Oblasti => Hlavny.Oblasti;

    [ObservableProperty]
    private string _novaOblast = "";

    [RelayCommand]
    private void PridajOblast()
    {
        if (Hlavny.PridajOblast(NovaOblast) is not null)
        {
            NovaOblast = "";
        }
    }
}

/// <summary>Vzdelávacia oblasť so zoznamom predmetov v poradí.</summary>
public sealed partial class OblastViewModel : ObservableObject, IUkladaPredOdchodom, IDisposable
{
    public OblastViewModel(MainViewModel hlavny, OblastVM oblast)
    {
        Hlavny = hlavny;
        Oblast = oblast;
        _nazov = oblast.Nazov;
        Dokument = new DokumentStrankyVM(hlavny, oblast.Id, () => $"Charakteristika – {Oblast.Nazov}", sablonaOsnovy: false);
    }

    /// <summary>Charakteristika vzdelávacej oblasti (dokument Word).</summary>
    public DokumentStrankyVM Dokument { get; }

    public MainViewModel Hlavny { get; }

    public OblastVM Oblast { get; }

    public IReadOnlyList<string> Paleta => UcebnyPlanService.Paleta;

    [ObservableProperty]
    private string _nazov;

    [ObservableProperty]
    private string _novyPredmet = "";

    /// <summary>Záložka: 0 = predmety, 1 = charakteristika oblasti.</summary>
    [ObservableProperty]
    private int _zalozka;

    partial void OnNazovChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            Hlavny.PremenujOblast(Oblast, value);
        }
    }

    [RelayCommand]
    private void PridajPredmet()
    {
        if (Hlavny.PridajPredmet(Oblast, NovyPredmet) is not null)
        {
            NovyPredmet = "";
        }
    }

    [RelayCommand]
    private void NastavFarbu(string? farba)
    {
        if (farba is not null)
        {
            Hlavny.NastavFarbuOblasti(Oblast, farba);
        }
    }

    [RelayCommand]
    private void ZacniPremenovanie(PredmetVM? predmet)
    {
        if (predmet is null)
        {
            return;
        }

        foreach (var p in Oblast.Predmety)
        {
            p.Premenuva = false;
        }

        predmet.NovyNazov = predmet.Nazov;
        predmet.Premenuva = true;
    }

    public void DokonciPremenovanie(PredmetVM predmet, bool ulozit)
    {
        if (!predmet.Premenuva)
        {
            return;
        }

        predmet.Premenuva = false;
        if (ulozit)
        {
            try
            {
                Hlavny.PremenujPredmet(predmet, predmet.NovyNazov);
            }
            catch (SkvpException ex)
            {
                Dialogy.Upozornenie(ex.Message);
            }
        }
    }

    public void UlozTeraz() => Dokument.UlozTeraz();

    public void Dispose() => Dokument.Dispose();
}

/// <summary>Spojenie s iným objektom zobrazené pri predmete.</summary>
public sealed record SuvislostVM(Suvislost Suvislost, string Farba, string Nadradeny)
{
    public string Nazov => Suvislost.Objekt.Nazov;
    public string Popis => Suvislost.Popis;
    public string TypObjektu => Models.TypObjektu.Nazov(Suvislost.Objekt.Typ);
}

/// <summary>Učebná osnova predmetu – náhľad, úprava, stav a súvislosti.</summary>
public sealed partial class PredmetViewModel : ObservableObject, IUkladaPredOdchodom, IDisposable
{
    private readonly ObjektService _sluzba = new();

    public PredmetViewModel(MainViewModel hlavny, PredmetVM predmet)
    {
        Hlavny = hlavny;
        Predmet = predmet;
        _nazov = predmet.Nazov;
        _vybranyStav = Stavy.First(s => s.Kod == predmet.StavKod);
        Dokument = new DokumentStrankyVM(hlavny, predmet.Id, () => $"{Predmet.Cislo} {Predmet.Nazov}", sablonaOsnovy: true);
        NacitajSuvislosti();
    }

    /// <summary>Učebná osnova predmetu (dokument Word).</summary>
    public DokumentStrankyVM Dokument { get; }

    public MainViewModel Hlavny { get; }

    public PredmetVM Predmet { get; }

    public OblastVM Oblast => Predmet.Oblast;

    public sealed record VolbaStavu(string? Kod, string Nazov);

    public static IReadOnlyList<VolbaStavu> Stavy { get; } = new[]
    {
        new VolbaStavu(null, "Podľa obsahu (nezačatá / rozpracovaná)"),
        new VolbaStavu(StavyOsnovy.NaKontrolu, "Na kontrolu"),
        new VolbaStavu(StavyOsnovy.Hotova, "Hotová"),
    };

    [ObservableProperty]
    private string _nazov;

    [ObservableProperty]
    private VolbaStavu _vybranyStav;

    [ObservableProperty]
    private bool _zobrazitSuvislosti = true;

    public ObservableCollection<SuvislostVM> Suvislosti { get; } = new();

    public PredmetVM? Predchadzajuci => Poloha(-1);
    public PredmetVM? Dalsi => Poloha(+1);

    private PredmetVM? Poloha(int posun)
    {
        var index = Oblast.Predmety.IndexOf(Predmet) + posun;
        return index >= 0 && index < Oblast.Predmety.Count ? Oblast.Predmety[index] : null;
    }

    partial void OnNazovChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        try
        {
            Hlavny.PremenujPredmet(Predmet, value);
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    partial void OnVybranyStavChanged(VolbaStavu value)
    {
        if (value.Kod != Predmet.StavKod)
        {
            Hlavny.NastavStav(Predmet, value.Kod);
        }
    }

    public void UlozTeraz() => Dokument.UlozTeraz();

    public void Dispose() => Dokument.Dispose();

    private bool MaPredchadzajuci() => Predchadzajuci is not null;

    private bool MaDalsi() => Dalsi is not null;

    [RelayCommand(CanExecute = nameof(MaPredchadzajuci))]
    private void OtvorPredchadzajuci() => Hlavny.OtvorPredmet(Predchadzajuci);

    [RelayCommand(CanExecute = nameof(MaDalsi))]
    private void OtvorDalsi() => Hlavny.OtvorPredmet(Dalsi);

    // ---- súvislosti ----

    public void NacitajSuvislosti()
    {
        Suvislosti.Clear();
        foreach (var s in _sluzba.Suvislosti(Predmet.Id))
        {
            var oblast = s.Objekt.Typ == TypObjektu.Oblast ? Hlavny.NajdiOblast(s.Objekt.Id) : Hlavny.NajdiPredmet(s.Objekt.Id)?.Oblast;
            Suvislosti.Add(new SuvislostVM(s, oblast?.Farba ?? "#6B7280",
                s.Objekt.Typ == TypObjektu.Predmet ? oblast?.Nazov ?? "" : "Vzdelávacia oblasť"));
        }
    }

    [RelayCommand]
    private void PridatSuvislost()
    {
        var okno = new Views.VyberObjektuWindow(Hlavny, Predmet.Id) { Owner = Dialogy.Vlastnik };
        if (okno.ShowDialog() == true && okno.Vybrany is { } ciel)
        {
            _sluzba.Prepoj(Predmet.Id, ciel, okno.TypVazby);
            NacitajSuvislosti();
        }
    }

    public void Prepoj(long ciel, string typ)
    {
        _sluzba.Prepoj(Predmet.Id, ciel, typ);
        NacitajSuvislosti();
    }

    [RelayCommand]
    private void OdstranitSuvislost(SuvislostVM? suvislost)
    {
        if (suvislost is null)
        {
            return;
        }

        _sluzba.ZrusVazbu(suvislost.Suvislost.VazbaId);
        NacitajSuvislosti();
    }

    [RelayCommand]
    private void OtvoritSuvislost(SuvislostVM? suvislost)
    {
        if (suvislost is null)
        {
            return;
        }

        if (suvislost.Suvislost.Objekt.Typ == TypObjektu.Oblast)
        {
            Hlavny.OtvorOblast(Hlavny.NajdiOblast(suvislost.Suvislost.Objekt.Id));
        }
        else
        {
            Hlavny.OtvorPredmet(Hlavny.NajdiPredmet(suvislost.Suvislost.Objekt.Id));
        }
    }
}
