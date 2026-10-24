using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>
/// Panel „Úlohy a poznámky“ pri predmete, časti osnov alebo oblasti: otvorené úlohy (rýchle pridanie, odškrtnutie),
/// najbližšie termíny v kalendári a poznámky – všetko, čo k nim patrí. Pri oblasti aj úlohy a poznámky jej predmetov.
/// </summary>
public sealed partial class PanelPlanovaniaVM : ObservableObject
{
    private readonly MainViewModel _hlavny;
    private readonly long _objekt;
    private readonly Func<IReadOnlyCollection<long>> _objekty;

    /// <param name="objekt">Predmet, časť alebo oblasť, ku ktorej sa nové záznamy naviažu.</param>
    /// <param name="objekty">Objekty, ktorých záznamy sa ukazujú (pri oblasti aj jej predmety).</param>
    public PanelPlanovaniaVM(MainViewModel hlavny, long objekt, Func<IReadOnlyCollection<long>> objekty)
    {
        _hlavny = hlavny;
        _objekt = objekt;
        _objekty = objekty;
        Obnov();
    }

    [ObservableProperty] private List<UlohaRiadok> _ulohy = new();
    [ObservableProperty] private List<PoznamkaRiadok> _poznamky = new();
    [ObservableProperty] private List<VyskytVKalendari> _terminy = new();
    [ObservableProperty] private int _pocetHotovych;
    [ObservableProperty] private string _novaUloha = "";

    /// <summary>Počet na prepínači panela (otvorené úlohy, termíny a poznámky).</summary>
    public int Pocet => Ulohy.Count + Terminy.Count + Poznamky.Count;

    public string HotoveText => PocetHotovych == 0 ? "" : PocetHotovych == 1 ? "a 1 hotová úloha" : $"a hotové úlohy: {PocetHotovych}";

    public void Obnov()
    {
        try
        {
            var objekty = _objekty();
            var ulohy = new UlohyService().Zoznam(patriK: objekty);
            Ulohy = ulohy.Where(u => !u.Hotovo).ToList();
            PocetHotovych = ulohy.Count - Ulohy.Count;
            Poznamky = new PoznamkyService().Zoznam(patriK: objekty);
            Terminy = new KalendarService().Najblizsie(objekty);
        }
        catch (Exception)
        {
            // panel ostane prázdny – osnova sa dá upravovať aj tak
        }

        OnPropertyChanged(nameof(Pocet));
        OnPropertyChanged(nameof(HotoveText));
    }

    private void PoZmene()
    {
        Obnov();
        _hlavny.ObnovPlanovanie();
    }

    [RelayCommand]
    private void PridatUlohu()
    {
        if (string.IsNullOrWhiteSpace(NovaUloha))
        {
            return;
        }

        try
        {
            new UlohyService().Pridaj(NovaUloha, tykaSa: new[] { _objekt });
            NovaUloha = "";
            PoZmene();
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    private void PrepnutHotovo(UlohaRiadok? uloha)
    {
        if (uloha is not null)
        {
            new UlohyService().NastavHotovo(uloha.Id, !uloha.Hotovo);
            PoZmene();
        }
    }

    [RelayCommand]
    private void OtvoritUlohu(UlohaRiadok? uloha) => _hlavny.OtvorUlohy(uloha?.Id);

    /// <summary>Všetky úlohy predmetu či oblasti v sekcii Úlohy (aj hotové).</summary>
    [RelayCommand]
    private void VsetkyUlohy()
    {
        _hlavny.OtvorUlohy(null);
        if (_hlavny.Stranka is UlohyViewModel ulohy && ulohy.Filtre.FirstOrDefault(f => f.Hodnota == _objekt) is { } filter)
        {
            ulohy.FilterObjektu = filter;
            ulohy.Filter = FilterUloh.Vsetky;
        }
    }

    [RelayCommand]
    private void NovaPoznamka()
    {
        if (Views.PoznamkaWindow.Otvor(new PoznamkaEditorVM(_hlavny, null, tykaSa: new[] { _objekt })))
        {
            PoZmene();
        }
    }

    [RelayCommand]
    private void OtvoritPoznamku(PoznamkaRiadok? poznamka)
    {
        if (poznamka is null)
        {
            return;
        }

        try
        {
            if (Views.PoznamkaWindow.Otvor(new PoznamkaEditorVM(_hlavny, poznamka.Id)))
            {
                PoZmene();
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    private void NovyTermin()
    {
        if (Views.UdalostWindow.Otvor(new UdalostEditorVM(_hlavny, null, DateTime.Today, new[] { _objekt })))
        {
            PoZmene();
        }
    }

    [RelayCommand]
    private void OtvoritTermin(VyskytVKalendari? termin)
    {
        if (termin?.Id is { } id && Views.UdalostWindow.Otvor(new UdalostEditorVM(_hlavny, id, null)))
        {
            PoZmene();
        }
    }
}
