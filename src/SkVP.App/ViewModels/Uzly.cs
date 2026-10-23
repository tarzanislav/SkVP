using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SkVP.Models;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Vzdelávacia oblasť v menu a na stránkach programu.</summary>
public sealed partial class OblastVM : ObservableObject
{
    public OblastVM(Objekt objekt, int index)
    {
        Id = objekt.Id;
        _nazov = objekt.Nazov;
        _farba = objekt.Farba ?? UcebnyPlanService.FarbaOblasti(index);
    }

    public long Id { get; }

    [ObservableProperty]
    private string _nazov;

    [ObservableProperty]
    private string _farba;

    [ObservableProperty]
    private bool _jeVybrana;

    public ObservableCollection<PredmetVM> Predmety { get; } = new();

    public int Pocet => Predmety.Count;
    public string PocetText => PdfVystup.PocetPredmetov(Predmety.Count);
    public int PocetHotovych => Predmety.Count(p => p.Stav == StavOsnovy.Hotova);
    public int PocetNaKontrolu => Predmety.Count(p => p.Stav == StavOsnovy.NaKontrolu);
    public int PocetRozpracovanych => Predmety.Count(p => p.Stav == StavOsnovy.Rozpracovana);
    public int PocetNezacatych => Predmety.Count(p => p.Stav == StavOsnovy.Prazdna);
    public double Podiel => Predmety.Count == 0 ? 0 : (double)PocetHotovych / Predmety.Count;
    public string HotoveText => Predmety.Count == 0 ? "Bez predmetov" : $"{PocetHotovych} z {Predmety.Count} osnov hotových";

    /// <summary>Prepočíta čísla poradia predmetov a súhrny (po presune, pridaní, zmene stavu).</summary>
    public void Obnov()
    {
        for (var i = 0; i < Predmety.Count; i++)
        {
            Predmety[i].Poradie = i + 1;
            Predmety[i].JePrvy = i == 0;
            Predmety[i].JePosledny = i == Predmety.Count - 1;
        }

        OnPropertyChanged(nameof(Pocet));
        OnPropertyChanged(nameof(PocetText));
        OnPropertyChanged(nameof(PocetHotovych));
        OnPropertyChanged(nameof(PocetNaKontrolu));
        OnPropertyChanged(nameof(PocetRozpracovanych));
        OnPropertyChanged(nameof(PocetNezacatych));
        OnPropertyChanged(nameof(Podiel));
        OnPropertyChanged(nameof(HotoveText));
    }
}

/// <summary>Predmet v zozname oblasti.</summary>
public sealed partial class PredmetVM : ObservableObject
{
    public PredmetVM(Objekt objekt, OblastVM oblast)
    {
        Id = objekt.Id;
        _oblast = oblast;
        _nazov = objekt.Nazov;
        _stavKod = objekt.Stav;
        _dlzkaTextu = objekt.DlzkaTextu;
        _upravene = objekt.Upravene;
    }

    public long Id { get; }

    [ObservableProperty]
    private OblastVM _oblast;

    [ObservableProperty]
    private string _nazov;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Cislo))]
    private int _poradie;

    [ObservableProperty]
    private bool _jePrvy;

    [ObservableProperty]
    private bool _jePosledny;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stav), nameof(StavText), nameof(UpraveneText))]
    private string? _stavKod;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Stav), nameof(StavText), nameof(UpraveneText))]
    private int _dlzkaTextu;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpraveneText))]
    private DateTime _upravene;

    /// <summary>Premenovanie priamo v zozname.</summary>
    [ObservableProperty]
    private bool _premenuva;

    [ObservableProperty]
    private string _novyNazov = "";

    public string Cislo => Poradie.ToString("00", CultureInfo.InvariantCulture);

    public StavOsnovy Stav => StavyOsnovy.Urci(StavKod, DlzkaTextu);

    public string StavText => StavyOsnovy.Text(Stav);

    public string UpraveneText =>
        Upravene == DateTime.MinValue || Stav == StavOsnovy.Prazdna ? "" :
        Upravene.Date == DateTime.Today ? $"dnes {Upravene:H:mm}" :
        Upravene.Date == DateTime.Today.AddDays(-1) ? $"včera {Upravene:H:mm}" :
        Upravene.ToString("d. M. yyyy", CultureInfo.GetCultureInfo("sk-SK"));
}
