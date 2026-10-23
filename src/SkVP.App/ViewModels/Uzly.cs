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
        Typ = objekt.Typ;
        _nazov = objekt.Nazov;
        _farba = objekt.Farba ?? (JeOblast ? UcebnyPlanService.FarbaOblasti(index) : CastiOsnov.Farba(Typ));
        _aktivna = JeOblast || CastiOsnov.JeAktivna(Typ);
    }

    public long Id { get; }

    /// <summary>Vzdelávacia oblasť alebo úvodné/záverečné časti učebných osnov.</summary>
    public string Typ { get; }

    public bool JeOblast => Typ == TypObjektu.Oblast;

    /// <summary>Úvodné alebo záverečné časti učebných osnov.</summary>
    public bool JeCastiOsnov => !JeOblast;

    public bool JeUvod => Typ == TypObjektu.Uvod;

    /// <summary>Zapnutá skupina (oblasti sú vždy zapnuté; úvodné a záverečné časti sa zapínajú prepínačom).</summary>
    [ObservableProperty]
    private bool _aktivna;

    partial void OnAktivnaChanged(bool value)
    {
        // zapnutie a vypnutie úvodných/záverečných častí sa pamätá (oblasti sú vždy zapnuté)
        if (JeCastiOsnov)
        {
            CastiOsnov.NastavAktivnu(Typ, value);
        }
    }

    /// <summary>Charakteristika úvodných/záverečných častí (pevný text).</summary>
    public string Charakteristika => JeOblast ? "" : CastiOsnov.Charakteristika(Typ);

    /// <summary>Názov bloku v sekcii Osnovy veľkými písmenami („ÚVODNÉ ČASTI UČEBNÝCH OSNOV“).</summary>
    public string NazovBloku => Nazov.ToUpper(CultureInfo.GetCultureInfo("sk-SK"));

    /// <summary>„Vzdelávacia oblasť“, „Úvodné časti učebných osnov“.</summary>
    public string NazovDruhu => TypObjektu.Nazov(Typ);

    /// <summary>Jednotné číslo dokumentu skupiny: „predmet“, „úvodná časť“, „záverečná časť“.</summary>
    public string NazovDokumentu => JeOblast ? "predmet" : JeUvod ? "úvodná časť" : "záverečná časť";

    [ObservableProperty]
    private string _nazov;

    [ObservableProperty]
    private string _farba;

    [ObservableProperty]
    private bool _jeVybrana;

    public ObservableCollection<PredmetVM> Predmety { get; } = new();

    public int Pocet => Predmety.Count;
    public string PocetText => JeOblast ? PdfVystup.PocetPredmetov(Predmety.Count) : CastiOsnov.PocetCasti(Typ, Predmety.Count);
    public int PocetHotovych => Predmety.Count(p => p.Stav == StavOsnovy.Hotova);
    public int PocetNaKontrolu => Predmety.Count(p => p.Stav == StavOsnovy.NaKontrolu);
    public int PocetRozpracovanych => Predmety.Count(p => p.Stav == StavOsnovy.Rozpracovana);
    public int PocetNezacatych => Predmety.Count(p => p.Stav == StavOsnovy.Prazdna);
    public double Podiel => Predmety.Count == 0 ? 0 : (double)PocetHotovych / Predmety.Count;
    public string HotoveText => Predmety.Count == 0
        ? (JeOblast ? "Bez predmetov" : JeUvod ? "Bez úvodných častí" : "Bez záverečných častí")
        : JeOblast ? $"{PocetHotovych} z {Predmety.Count} osnov hotových" : $"{PocetHotovych} z {Predmety.Count} hotových";

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

    /// <summary>Profilácie a platnosť z úvodnej tabuľky osnovy (čítajú sa z dokumentu).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextProfilacii), nameof(TextPlatnosti), nameof(MaUdajeOsnovy))]
    private UdajeOsnovy _udajeOsnovy = UdajeOsnovy.Prazdne;

    public string TextProfilacii => UdajeOsnovy.Profilacie is { Length: > 0 } p ? $"Profilácie: {p}" : "";

    public string TextPlatnosti => UdajeOsnovy.Platnost is { Length: > 0 } p ? $"Platnosť: {p}" : "";

    public bool MaUdajeOsnovy => UdajeOsnovy.MaUdaje;

    /// <summary>Načíta profilácie a platnosť z dokumentu osnovy (aj po každej zmene dokumentu – synchronizácia).</summary>
    public void NacitajUdajeOsnovy() => UdajeOsnovy = DlzkaTextu > 0 ? UvodnaTabulka.Precitaj(Id) : UdajeOsnovy.Prazdne;

    /// <summary>Zaškrtnuté políčko „opraviť údaje“ – profilácie a platnosť sa dajú upraviť a zapíšu sa do osnovy.</summary>
    [ObservableProperty]
    private bool _upravaUdajov;

    [ObservableProperty]
    private string _upravaProfilacii = "";

    [ObservableProperty]
    private string _upravaPlatnosti = "";

    partial void OnUpravaUdajovChanged(bool value)
    {
        if (value)
        {
            UpravaProfilacii = UdajeOsnovy.Profilacie ?? "";
            UpravaPlatnosti = UdajeOsnovy.Platnost ?? "";
        }
    }

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
