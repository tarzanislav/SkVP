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

    /// <summary>Zhrnutie stavu osnov v jednej vete (so správnymi tvarmi slov).</summary>
    public string Zhrnutie => PocetPredmetov == 0
        ? "Zatiaľ nie sú zadané žiadne predmety. Pridajte vzdelávacie oblasti a predmety v sekcii Osnovy."
        : $"Stav {PocetPredmetov} osnov: {Tvar(PocetHotovych, "hotová", "hotové", "hotových")}, " +
          $"{Tvar(PocetRozpracovanych, "rozpracovaná", "rozpracované", "rozpracovaných")} a " +
          $"{Tvar(PocetNezacatych, "nezačatá", "nezačaté", "nezačatých")}.";

    private static string Tvar(int pocet, string jeden, string dvaAzStyri, string viac) =>
        $"{pocet} " + (pocet == 1 ? jeden : pocet is >= 2 and <= 4 ? dvaAzStyri : viac);
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
        foreach (var predmet in oblast.Predmety)
        {
            predmet.NacitajUdajeOsnovy();
        }
    }

    /// <summary>Oblasť je v Štátnom vzdelávacom programe pre gymnáziá – charakteristika sa dá prevziať.</summary>
    public bool MaCharakteristikuSvp => SvpGymnazia.Oblast(Oblast.Nazov) is not null;

    /// <summary>Charakteristika oblasti je doslovne podľa ŠVP.</summary>
    public bool CharakteristikaJeZoSvp =>
        SvpGymnazia.Oblast(Oblast.Nazov) is { } svp && new DokumentService() is var d && d.Existuje(Oblast.Id) && SvpGymnazia.JePrevzata(d.Nacitaj(Oblast.Id).Bloky, svp);

    /// <summary>Nahradí charakteristiku oblasti oficiálnym znením ŠVP (doterajšia sa zarchivuje).</summary>
    [RelayCommand]
    private void CharakteristikaZoSvp()
    {
        if (!Dialogy.Potvrd($"Nahradiť charakteristiku oblasti {Oblast.Nazov} znením zo Štátneho vzdelávacieho programu pre gymnáziá?\n\n" +
                            "Doterajší text sa zarchivuje (dá sa obnoviť v archíve)."))
        {
            return;
        }

        Dokument.UlozTeraz();
        if (SvpGymnazia.PrevezmiCharakteristiku(Oblast.Id, Oblast.Nazov))
        {
            Hlavny.OznamPrepisanie(Oblast.Id);
            OnPropertyChanged(nameof(CharakteristikaJeZoSvp));
            Hlavny.Oznam($"Charakteristika oblasti {Oblast.Nazov} je podľa Štátneho vzdelávacieho programu.");
        }
    }

    /// <summary>Zapíše opravené profilácie a platnosť do úvodnej tabuľky osnovy (doterajšia verzia sa zarchivuje).</summary>
    [RelayCommand]
    private void UlozitUdaje(PredmetVM? predmet)
    {
        if (predmet is null)
        {
            return;
        }

        if (Hlavny.JeVoWorde(predmet.Id))
        {
            Dialogy.Upozornenie("Osnova je práve otvorená vo Worde – údaje opravte vo Worde, alebo Word najprv zatvorte.");
            return;
        }

        var dokumenty = new DokumentService();
        if (dokumenty.NacitajData(predmet.Id) is not { Length: > 0 } data)
        {
            Dialogy.Upozornenie("Predmet zatiaľ nemá dokument osnovy.");
            return;
        }

        string? Hodnota(string? povodna, string nova) => povodna is null && nova.Trim().Length == 0 ? null : nova.Trim();
        try
        {
            var nove = UvodnaTabulka.Zapis(data, Hodnota(predmet.UdajeOsnovy.Profilacie, predmet.UpravaProfilacii),
                Hodnota(predmet.UdajeOsnovy.Platnost, predmet.UpravaPlatnosti));
            dokumenty.Uloz(predmet.Id, nove, "Pred opravou profilácií a platnosti v úvodnej tabuľke");
            Hlavny.PoZmeneDokumentu(predmet.Id, oznamit: true);
            predmet.UpravaUdajov = false;
            Hlavny.Oznam($"Údaje osnovy „{predmet.Nazov}“ sú opravené aj v dokumente.");
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    private void ZrusitUpravuUdajov(PredmetVM? predmet)
    {
        if (predmet is not null)
        {
            predmet.UpravaUdajov = false;
        }
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
        _vybranyStav = VsetkyStavy.First(s => s.Kod == predmet.StavKod);
        _zobrazitSuvislosti = new ObjektService().Suvislosti(predmet.Id).Count > 0;
        Dokument = new DokumentStrankyVM(hlavny, predmet.Id,
            () => InfoOsnovyService.NazovDokumentu(Predmet.Cislo, Predmet.Nazov, InfoOsnovyService.Nacitaj(Predmet.Id).Rok), sablonaOsnovy: true);
        NacitajSuvislosti();
        NacitajInfo();
        predmet.NacitajUdajeOsnovy();
    }

    /// <summary>Rok zmeny osnov a import („Osnovy zmenené v roku 2026 • importované …“).</summary>
    [ObservableProperty]
    private string _poznamkaOsnovy = "";

    [ObservableProperty]
    private int _pocetArchivovanych;

    private void NacitajInfo()
    {
        PoznamkaOsnovy = InfoOsnovyService.Nacitaj(Predmet.Id).Poznamka;
        PocetArchivovanych = new DokumentService().Verzie(Predmet.Id).Count;
    }

    /// <summary>Archív starších verzií osnovy (s rokom archivácie v názve).</summary>
    [RelayCommand]
    private void Archiv()
    {
        Dokument.UlozTeraz();
        var okno = new Views.ArchivOsnovyWindow(Predmet.Id, Predmet.Cislo, Predmet.Nazov) { Owner = Dialogy.Vlastnik };
        okno.ShowDialog();
        if (okno.Obnovene)
        {
            Hlavny.PoZmeneDokumentu(Predmet.Id, oznamit: true);
        }

        NacitajInfo();
    }

    /// <summary>Nová osnova tohto predmetu z Wordu alebo PDF (doterajšia sa zarchivuje).</summary>
    [RelayCommand]
    private void ImportovatOsnovu()
    {
        var subory = Dialogy.OtvoritViac("Nová osnova predmetu " + Predmet.Nazov, ImportOsnov.FilterSuborov);
        if (subory.Count == 0)
        {
            return;
        }

        Dokument.UlozTeraz();
        var model = new ImportOsnovViewModel(Hlavny, Predmet.Id);
        model.PridajSubory(subory.Take(1));
        new Views.ImportOsnovWindow(model) { Owner = Dialogy.Vlastnik }.ShowDialog();
        NacitajInfo();
    }

    /// <summary>Učebná osnova predmetu (dokument Word).</summary>
    public DokumentStrankyVM Dokument { get; }

    public MainViewModel Hlavny { get; }

    public PredmetVM Predmet { get; }

    public OblastVM Oblast => Predmet.Oblast;

    public sealed record VolbaStavu(string? Kod, string Nazov);

    private static readonly IReadOnlyList<VolbaStavu> VsetkyStavy = new[]
    {
        new VolbaStavu(null, "Podľa obsahu (nezačatá / rozpracovaná)"),
        new VolbaStavu(StavyOsnovy.NaKontrolu, "Na kontrolu"),
        new VolbaStavu(StavyOsnovy.Hotova, "Hotová"),
    };

    public IReadOnlyList<VolbaStavu> Stavy => VsetkyStavy;

    [ObservableProperty]
    private string _nazov;

    [ObservableProperty]
    private VolbaStavu _vybranyStav;

    [ObservableProperty]
    private bool _zobrazitSuvislosti;

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
