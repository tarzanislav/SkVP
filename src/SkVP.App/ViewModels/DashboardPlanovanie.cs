using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Dashboard – kalendár na najbližší týždeň, otvorené úlohy s rýchlym pridaním a pripnuté poznámky.</summary>
public sealed partial class DashboardViewModel
{
    public const string KlucKalendar = "dashboard.kalendar";
    public const string KlucUlohy = "dashboard.ulohy";
    public const string KlucPoznamky = "dashboard.poznamky";
    private const int UlohNaPaneli = 6;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaPlanovanie))]
    private bool _ukazKalendar = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaPlanovanie))]
    private bool _ukazUlohy = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaPlanovanie))]
    private bool _ukazPoznamky = true;

    public bool MaPlanovanie => UkazKalendar || UkazUlohy || UkazPoznamky;

    partial void OnUkazKalendarChanged(bool value) => UlozPanel(KlucKalendar, value);
    partial void OnUkazUlohyChanged(bool value) => UlozPanel(KlucUlohy, value);
    partial void OnUkazPoznamkyChanged(bool value) => UlozPanel(KlucPoznamky, value);

    /// <summary>Dnes a najbližších 7 dní: vlastné udalosti, úlohy s termínom a termíny školského roka (bez prázdnin).</summary>
    [ObservableProperty] private List<VyskytVKalendari> _najblizsieVKalendari = new();
    [ObservableProperty] private string _kalendarSuhrn = "";
    [ObservableProperty] private List<UlohaRiadok> _ulohyNaPaneli = new();
    [ObservableProperty] private string _ulohySuhrn = "";
    [ObservableProperty] private string _dalsieUlohy = "";
    [ObservableProperty] private string _novaUloha = "";
    [ObservableProperty] private List<PoznamkaRiadok> _pripnutePoznamky = new();

    private void PripravPlanovanie()
    {
        UkazKalendar = Databaza.Citaj(KlucKalendar, "1") != "0";
        UkazUlohy = Databaza.Citaj(KlucUlohy, "1") != "0";
        UkazPoznamky = Databaza.Citaj(KlucPoznamky, "1") != "0";
        AktualizujPlanovanie();
    }

    public void AktualizujPlanovanie()
    {
        try
        {
            var dnes = DateTime.Today;
            NajblizsieVKalendari = new KalendarService().Vyskyty(dnes, dnes.AddDays(7))
                .Where(v => v.Zdroj != ZdrojKalendara.Prazdniny && !v.JeVybavena)
                .Take(8)
                .ToList();
            var dnesne = NajblizsieVKalendari.Count(v => v.Zaciatok.Date <= dnes && v.PoslednyDen >= dnes);
            KalendarSuhrn = NajblizsieVKalendari.Count == 0
                ? "Na najbližší týždeň nie je nič naplánované."
                : dnesne == 0 ? "Dnes nič – najbližšie dni:" : $"Dnes: {dnesne}, najbližšie dni:";

            var otvorene = new UlohyService().Zoznam(ajHotove: false);
            UlohyNaPaneli = otvorene.OrderBy(u => Array.IndexOf(UlohyService.PoradieSkupin, u.Skupina)).Take(UlohNaPaneli).ToList();
            var poTermine = otvorene.Count(u => u.PoTermine);
            UlohySuhrn = otvorene.Count == 0
                ? "Žiadne otvorené úlohy."
                : $"Otvorené: {otvorene.Count}" + (poTermine > 0 ? $"  ·  po termíne: {poTermine}" : "");
            DalsieUlohy = otvorene.Count > UlohNaPaneli ? $"a ďalšie ({otvorene.Count - UlohNaPaneli}) →" : "";
            PripnutePoznamky = new PoznamkyService().Pripnute(4);
        }
        catch (Exception ex)
        {
            KalendarSuhrn = $"Kalendár sa nepodarilo načítať: {ex.GetBaseException().Message}";
        }
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
            new UlohyService().Pridaj(NovaUloha);
            NovaUloha = "";
            AktualizujPlanovanie();
            Hlavny.ObnovPlanovanie();
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
            AktualizujPlanovanie();
            Hlavny.ObnovPlanovanie();
        }
    }

    [RelayCommand]
    private void OtvoritVyskyt(VyskytVKalendari? v)
    {
        if (v is { JeUloha: true, Id: { } uloha })
        {
            Hlavny.OtvorUlohy(uloha);
        }
        else
        {
            Hlavny.OtvorKalendar(v?.Zaciatok);
        }
    }

    [RelayCommand]
    private void OtvoritPoznamku(PoznamkaRiadok? poznamka) => Hlavny.OtvorPoznamky(poznamka?.Id);
}
