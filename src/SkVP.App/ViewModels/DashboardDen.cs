using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>
/// Dashboard – horné panely: dátum, meniny a sviatky (Kalendárium spoločné s programom EviTA), stav školského roka
/// a najbližší termín zo sekcie Novinky, počasie (Open-Meteo) a výrok dňa (citaty-slavnych.sk).
/// </summary>
public sealed partial class DashboardViewModel
{
    public const string KlucDnes = "dashboard.dnes";
    public const string KlucPocasie = "dashboard.pocasie";
    public const string KlucCitat = "dashboard.citat";

    private static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");
    private readonly PocasieService _pocasieSluzba = new();
    private bool _panelyPripravene;

    /// <summary>Pri samoteste a v náhľade sa nič nesťahuje zo siete.</summary>
    public static bool BezSiete { get; set; }

    // ------------------------------------------------------------------ viditeľnosť panelov

    [ObservableProperty]
    private bool _ukazDnes = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaSpodnePanely))]
    private bool _ukazPocasie = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaSpodnePanely))]
    private bool _ukazCitat = true;

    /// <summary>Počasie a výrok dňa pod blokom Dnes.</summary>
    public bool MaSpodnePanely => UkazPocasie || UkazCitat;

    partial void OnUkazDnesChanged(bool value) => UlozPanel(KlucDnes, value);

    partial void OnUkazPocasieChanged(bool value)
    {
        UlozPanel(KlucPocasie, value);
        if (value && _panelyPripravene)
        {
            _ = NacitajPocasieAsync(vynutit: false);
        }
    }

    partial void OnUkazCitatChanged(bool value) => UlozPanel(KlucCitat, value);

    private void UlozPanel(string kluc, bool hodnota)
    {
        if (_panelyPripravene)
        {
            Databaza.Zapis(kluc, hodnota ? "1" : "0");
        }
    }

    private void PripravPanely()
    {
        UkazDnes = Databaza.Citaj(KlucDnes, "1") != "0";
        UkazPocasie = Databaza.Citaj(KlucPocasie, "1") != "0";
        UkazCitat = Databaza.Citaj(KlucCitat, "1") != "0";
        AktualizujDen();
        AktualizujCitat(CitatyService.CitatDna(DateOnly.FromDateTime(DateTime.Today)));
        PripravPocasie();
        PripravPrazdniny();
        PripravPlanovanie();
        _panelyPripravene = true;
        if (!BezSiete && !Dialogy.TichyRezim && UkazPocasie)
        {
            _ = NacitajPocasieAsync(vynutit: false);
        }
    }

    // ------------------------------------------------------------------ dátum, meniny, sviatky

    public string DatumVelky => Preklad.VelkePrve(DateTime.Today.ToString("d. MMMM yyyy", Sk));
    public string DenVTyzdni => Preklad.VelkePrve(DateTime.Today.ToString("dddd", Sk));

    public string TyzdenText =>
        $"{ISOWeek.GetWeekOfYear(DateTime.Today)}. týždeň · {DateTime.Today.DayOfYear}. deň roka";

    [ObservableProperty] private string _meninyDnes = "";
    [ObservableProperty] private string _meninyZajtra = "";
    [ObservableProperty] private List<string> _sviatky = new();
    [ObservableProperty] private bool _jeDnesVolno;
    [ObservableProperty] private string _najblizsiVolnyDen = "";
    [ObservableProperty] private string _stavSkolskehoRoka = "";
    [ObservableProperty] private string _najblizsiTermin = "";
    [ObservableProperty] private string _najblizsiTerminFarba = "#3B5BDB";

    public bool MaSviatky => Sviatky.Count > 0;

    /// <summary>Meniny, sviatky, najbližší deň pracovného pokoja; stav školského roka a najbližší termín z Noviniek.</summary>
    public void AktualizujDen() => AktualizujDen(DateOnly.FromDateTime(DateTime.Today));

    public void AktualizujDen(DateOnly dnes)
    {
        try
        {
            var kalendar = Kalendarium.KalendariumDb.Spolocna;
            var mena = kalendar.Meniny(dnes);
            MeninyDnes = mena.Count > 0 ? string.Join(", ", mena) : "dnes nemá meniny nikto";
            var zajtra = kalendar.Meniny(dnes.AddDays(1));
            MeninyZajtra = zajtra.Count > 0 ? string.Join(", ", zajtra) : "nikto";

            var sviatky = kalendar.SviatkyDna(dnes);
            JeDnesVolno = sviatky.Any(sv => sv.DenPracovnehoPokoja);
            Sviatky = sviatky.Select(sv => $"{Preklad.VelkePrve(sv.DruhText)}: {sv.Nazov}" +
                                           (sv.Druh != Kalendarium.DruhSviatku.PamatnyDen && !sv.DenPracovnehoPokoja ? " (nie je deň pracovného pokoja)" : ""))
                .ToList();

            var najblizsi = kalendar.NajblizsiDenPracovnehoPokoja(dnes.AddDays(JeDnesVolno ? 1 : 0));
            if (najblizsi is null)
            {
                NajblizsiVolnyDen = "";
            }
            else
            {
                var dni = najblizsi.Datum.DayNumber - dnes.DayNumber;
                var kedy = dni switch { 0 => "dnes", 1 => "zajtra", <= 4 => $"o {dni} dni", _ => $"o {dni} dní" };
                NajblizsiVolnyDen = $"Najbližší deň pracovného pokoja: {najblizsi.Nazov}, {najblizsi.Datum.ToDateTime(TimeOnly.MinValue).ToString("d. MMMM", Sk)} ({kedy})";
            }
        }
        catch (Exception)
        {
            MeninyDnes = "Kalendárium nie je dostupné";
            MeninyZajtra = "";
            Sviatky = new List<string>();
            JeDnesVolno = false;
            NajblizsiVolnyDen = "";
        }

        OnPropertyChanged(nameof(MaSviatky));

        try
        {
            var data = NovinkyService.Nacitaj();
            var kraj = Databaza.Citaj(NovinkyService.KlucKraj, NovinkyService.VsetkyKraje);
            bool JeVKraji(PolozkaNoviniek p) => p.Skupina is null || kraj == NovinkyService.VsetkyKraje || p.Skupina == kraj;
            var prazdniny = data.Polozky.FirstOrDefault(p => p.Kategoria == NovinkyService.TemaSkolskyRok && p.Druh == NovinkyService.DruhTermin
                                                              && p.Nazov.Contains("prázdniny", StringComparison.OrdinalIgnoreCase)
                                                              && p.Zaciatok <= dnes && p.Koniec >= dnes && JeVKraji(p));
            StavSkolskehoRoka = prazdniny is not null
                ? $"Dnes sú {prazdniny.Nazov.ToLower(Sk)} ({prazdniny.TerminText})"
                : JeDnesVolno || dnes.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? "" : $"{data.SkolskyRok} – vyučovací deň";
            var termin = NovinkyService.NajblizsieTerminy(data, dnes, 10, lenDolezite: true).FirstOrDefault(p => JeVKraji(p) && p.Zaciatok > dnes)
                         ?? NovinkyService.NajblizsieTerminy(data, dnes, 10).FirstOrDefault(JeVKraji);
            NajblizsiTermin = termin is null ? "" : $"{termin.Nazov} – {termin.TerminText} ({NovinkyService.Odpocet(termin, dnes)})";
            NajblizsiTerminFarba = termin is null ? "#3B5BDB" : data.Kategoria(termin.Kategoria)?.Farba ?? "#3B5BDB";
        }
        catch (Exception)
        {
            StavSkolskehoRoka = "";
            NajblizsiTermin = "";
        }
    }

    [RelayCommand]
    private void OtvorNovinky() => Hlavny.OtvorNovinky();

    // ------------------------------------------------------------------ počasie

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JePocasie))]
    private Pocasie? _pocasie;

    [ObservableProperty] private string _pocasieStav = "";
    [ObservableProperty] private bool _jeMiestoPocasia;
    [ObservableProperty] private bool _upravaMiesta;
    [ObservableProperty] private string _hladaneMiesto = "";
    [ObservableProperty] private List<MiestoPocasia> _najdeneMiesta = new();
    [ObservableProperty] private bool _hladaSa;

    public bool JePocasie => Pocasie is not null;

    private void PripravPocasie()
    {
        var miesto = _pocasieSluzba.NacitajMiesto();
        JeMiestoPocasia = miesto is not null;
        Pocasie = miesto is null ? null : _pocasieSluzba.ZVyrovnavacejPamate();
        PocasieStav = miesto is null
            ? (UdajeOrganizacie.Nacitaj().Mesto.Trim().Length > 0
                ? "Hľadám mesto z Údajov o organizácii…"
                : "Nastavte mesto alebo obec, pre ktorú sa má zobrazovať počasie (predvolene mesto z Údajov o organizácii).")
            : Pocasie is null ? "Načítavam počasie…" : "";
    }

    /// <summary>Stiahne počasie (najviac raz za 30 minút); bez nastaveného miesta skúsi mesto z Údajov o organizácii.</summary>
    public async Task NacitajPocasieAsync(bool vynutit)
    {
        try
        {
            var miesto = _pocasieSluzba.NacitajMiesto();
            if (miesto is null)
            {
                miesto = await _pocasieSluzba.MiestoOrganizacieAsync();
                if (miesto is null)
                {
                    PripravPocasie();
                    PocasieStav = "Nastavte mesto alebo obec, pre ktorú sa má zobrazovať počasie.";
                    return;
                }

                _pocasieSluzba.UlozMiesto(miesto);
                JeMiestoPocasia = true;
            }

            if (vynutit || _pocasieSluzba.TrebaObnovit(Pocasie))
            {
                Pocasie = await _pocasieSluzba.StiahniAsync(miesto);
            }

            PocasieStav = "";
        }
        catch (SkvpException ex)
        {
            PocasieStav = Pocasie is null ? ex.Message : "Zobrazené sú posledné stiahnuté údaje – nové sa nepodarilo stiahnuť.";
        }
        catch (Exception)
        {
            PocasieStav = "Počasie sa nepodarilo načítať.";
        }
    }

    [RelayCommand]
    private Task ObnovitPocasie() => NacitajPocasieAsync(vynutit: true);

    [RelayCommand]
    private void ZmenitMiesto()
    {
        HladaneMiesto = _pocasieSluzba.NacitajMiesto()?.Nazov ?? UdajeOrganizacie.Nacitaj().Mesto;
        NajdeneMiesta = new List<MiestoPocasia>();
        UpravaMiesta = true;
    }

    [RelayCommand]
    private void ZrusitUpravuMiesta() => UpravaMiesta = false;

    [RelayCommand]
    private async Task HladatMiesto()
    {
        HladaSa = true;
        try
        {
            NajdeneMiesta = await _pocasieSluzba.HladajAsync(HladaneMiesto);
            PocasieStav = NajdeneMiesta.Count == 0 ? $"Miesto „{HladaneMiesto.Trim()}“ sa nenašlo." : "";
        }
        catch (SkvpException ex)
        {
            PocasieStav = ex.Message;
        }
        finally
        {
            HladaSa = false;
        }
    }

    [RelayCommand]
    private async Task VybratMiesto(MiestoPocasia? miesto)
    {
        if (miesto is null)
        {
            return;
        }

        _pocasieSluzba.UlozMiesto(miesto);
        UpravaMiesta = false;
        JeMiestoPocasia = true;
        Pocasie = null;
        PocasieStav = "Načítavam počasie…";
        await NacitajPocasieAsync(vynutit: true);
    }

    // ------------------------------------------------------------------ výrok dňa

    [ObservableProperty] private Citat? _citat;
    [ObservableProperty] private string _citatText = "";
    [ObservableProperty] private string _citatAutor = "";
    [ObservableProperty] private string _citatPopis = "";

    public string CitatyZdroj => $"Zdroj: {CitatyService.Zbierka.Zdroj} · {CitatyService.Zbierka.Citaty.Count} výrokov · bez výrokov žijúcich politikov";

    public void AktualizujCitat(Citat? citat)
    {
        Citat = citat;
        CitatText = citat?.TextVUvodzovkach ?? "Žiadny výrok – všetky sú skryté.";
        CitatAutor = citat?.AutorText ?? "";
        CitatPopis = citat?.PopisText ?? "";
    }

    [RelayCommand]
    private void DalsiCitat() => AktualizujCitat(CitatyService.Nahodny(Citat?.Id));

    [RelayCommand]
    private void SkrytCitat()
    {
        if (Citat is { } c)
        {
            CitatyService.Skry(c.Id);
            AktualizujCitat(CitatyService.Nahodny(c.Id));
            Hlavny.Oznam("Výrok sa už nebude zobrazovať.", () =>
            {
                CitatyService.Odkry(c.Id);
                AktualizujCitat(c);
            });
        }
    }

    [RelayCommand]
    private void OtvoritZdrojCitatov() => Dialogy.OtvorVSysteme(CitatyService.ZdrojUrl);
}
