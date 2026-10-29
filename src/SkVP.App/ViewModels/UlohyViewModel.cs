using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

public enum FilterUloh
{
    Otvorene,
    Hotove,
    Vsetky,
}

/// <summary>Voľba so zobrazeným názvom (priorita, filter predmetu).</summary>
public sealed record Volba<T>(T Hodnota, string Nazov)
{
    public override string ToString() => Nazov;
}

/// <summary>
/// Sekcia Úlohy: zoznam zoskupený podľa termínu (po termíne, dnes, tento týždeň, neskôr, bez termínu, hotové),
/// rýchle pridanie, filter podľa predmetu či oblasti a hľadanie, navrhované úlohy z osnov a podrobnosti vybranej úlohy,
/// ktoré sa ukladajú priebežne.
/// </summary>
public sealed partial class UlohyViewModel : ObservableObject, IUkladaPredOdchodom, IDisposable
{
    private readonly UlohyService _sluzba = new();
    private readonly DispatcherTimer _ukladanie = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private bool _nacitavam;
    private bool _zmenene;

    public UlohyViewModel(MainViewModel hlavny, long? vybrana = null)
    {
        Hlavny = hlavny;
        TykaSa = new TykaSaVM(hlavny, () => Zmena(hned: true));
        Filtre = FilterOsnov.Volby(hlavny);
        _filterObjektu = Filtre[0];
        _ukladanie.Tick += (_, _) => Obnov(); // Obnov najprv uloží rozpísané zmeny
        try
        {
            _vsetkyNavrhy = NavrhyUloh.VsetkyNavrhy();
        }
        catch (Exception)
        {
            _vsetkyNavrhy = new List<NavrhUlohy>();
        }

        Obnov();
        if (vybrana is { } id)
        {
            if (!Zoznam.Any(u => u.Id == id))
            {
                Filter = FilterUloh.Vsetky;
            }

            Vybrana = Zoznam.FirstOrDefault(u => u.Id == id);
        }
    }

    public MainViewModel Hlavny { get; }

    public IReadOnlyList<Volba<PrioritaUlohy>> Priority => ZoznamPriority;

    private static readonly IReadOnlyList<Volba<PrioritaUlohy>> ZoznamPriority = new[]
    {
        new Volba<PrioritaUlohy>(PrioritaUlohy.Vysoka, "vysoká"),
        new Volba<PrioritaUlohy>(PrioritaUlohy.Normalna, "normálna"),
        new Volba<PrioritaUlohy>(PrioritaUlohy.Nizka, "nízka"),
    };

    // ------------------------------------------------------------------ zoznam a filtre

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeOtvorene), nameof(JeHotove), nameof(JeVsetky))]
    private FilterUloh _filter = FilterUloh.Otvorene;

    public bool JeOtvorene { get => Filter == FilterUloh.Otvorene; set { if (value) Filter = FilterUloh.Otvorene; } }
    public bool JeHotove { get => Filter == FilterUloh.Hotove; set { if (value) Filter = FilterUloh.Hotove; } }
    public bool JeVsetky { get => Filter == FilterUloh.Vsetky; set { if (value) Filter = FilterUloh.Vsetky; } }

    partial void OnFilterChanged(FilterUloh value) => Obnov();

    /// <summary>Filter „Týka sa“: všetko, skupina (aj s predmetmi) alebo predmet.</summary>
    public IReadOnlyList<Volba<long?>> Filtre { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PopisPridania))]
    private Volba<long?> _filterObjektu;

    partial void OnFilterObjektuChanged(Volba<long?> value) => Obnov();

    [ObservableProperty]
    private string _hladat = "";

    partial void OnHladatChanged(string value) => Obnov();

    [ObservableProperty]
    private List<UlohaRiadok> _zoznam = new();

    /// <summary>Zoznam zoskupený podľa termínu (hlavičky skupín).</summary>
    [ObservableProperty]
    private ICollectionView? _zobrazenie;

    [ObservableProperty]
    private string _suhrn = "";

    [ObservableProperty]
    private bool _suHotove;

    [ObservableProperty]
    private string _textPrazdneho = "";

    public void Obnov()
    {
        UlozTeraz(); // rozpísané zmeny vybranej úlohy sa pred novým načítaním uložia
        var vybrana = Vybrana?.Id;
        try
        {
            var vsetky = _sluzba.Zoznam(patriK: FilterOsnov.Objekty(Hlavny, FilterObjektu));
            if (Hladat.Trim().Length > 0)
            {
                vsetky = vsetky.Where(u => Planovanie.Vyhovuje(Hladat, u.Text, u.Popis, u.Zodpovedny, u.TykaSaText)).ToList();
            }

            Zoznam = (Filter switch
            {
                FilterUloh.Otvorene => vsetky.Where(u => !u.Hotovo),
                FilterUloh.Hotove => vsetky.Where(u => u.Hotovo),
                _ => vsetky,
            }).ToList();
            var otvorene = vsetky.Count(u => !u.Hotovo);
            var poTermine = vsetky.Count(u => u.PoTermine);
            var hotove = vsetky.Count(u => u.Hotovo);
            SuHotove = hotove > 0;
            Suhrn = $"otvorené {otvorene}  ·  po termíne {poTermine}  ·  hotové {hotove}";
            TextPrazdneho = Hladat.Trim().Length > 0 || FilterObjektu.Hodnota is not null
                ? "Takéto úlohy nie sú."
                : Filter == FilterUloh.Hotove ? "Zatiaľ nie sú žiadne hotové úlohy." : "Žiadne otvorené úlohy. Napíšte novú úlohu do poľa vyššie.";
        }
        catch (Exception ex)
        {
            Zoznam = new List<UlohaRiadok>();
            Suhrn = $"Úlohy sa nepodarilo načítať: {ex.GetBaseException().Message}";
        }

        // nový zoznam zruší výber v zozname – rozpísané polia sa pritom nemajú vyprázdniť (kurzor zostane, kde bol)
        _nacitavam = true;
        var pohlad = CollectionViewSource.GetDefaultView(Zoznam);
        pohlad.GroupDescriptions.Add(new PropertyGroupDescription(nameof(UlohaRiadok.Skupina)));
        Zobrazenie = pohlad;
        Vybrana = vybrana is { } id ? Zoznam.FirstOrDefault(u => u.Id == id) : null;
        _nacitavam = false;
        NacitajNavrhy();
        Hlavny.ObnovPlanovanie();
    }

    // ------------------------------------------------------------------ rýchle pridanie

    [ObservableProperty]
    private string _novyText = "";

    [ObservableProperty]
    private DateTime? _novyTermin;

    [ObservableProperty]
    private PrioritaUlohy _novaPriorita = PrioritaUlohy.Normalna;

    /// <summary>Nová úloha sa naviaže na predmet alebo oblasť z filtra.</summary>
    public string PopisPridania => FilterObjektu.Hodnota is null
        ? "Čo treba urobiť? Napíšte a stlačte Enter"
        : $"Nová úloha pre {FilterObjektu.Nazov.Trim()} – napíšte a stlačte Enter";

    [RelayCommand]
    private void Pridat()
    {
        if (string.IsNullOrWhiteSpace(NovyText))
        {
            return;
        }

        try
        {
            var tykaSa = FilterObjektu.Hodnota is { } objekt ? new[] { objekt } : Array.Empty<long>();
            var id = _sluzba.Pridaj(NovyText, NovyTermin, NovaPriorita, tykaSa);
            NovyText = "";
            NovyTermin = null;
            NovaPriorita = PrioritaUlohy.Normalna;
            if (Filter == FilterUloh.Hotove)
            {
                Filter = FilterUloh.Otvorene;
            }

            Hladat = "";
            Obnov();
            Vybrana = Zoznam.FirstOrDefault(u => u.Id == id);
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    // ------------------------------------------------------------------ vybraná úloha (ukladá sa priebežne)

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeVybrana))]
    private UlohaRiadok? _vybrana;

    public bool JeVybrana => Vybrana is not null;

    [ObservableProperty] private string _text = "";
    [ObservableProperty] private string _popis = "";
    [ObservableProperty] private DateTime? _termin;
    [ObservableProperty] private PrioritaUlohy _priorita = PrioritaUlohy.Normalna;
    [ObservableProperty] private bool _hotovo;
    [ObservableProperty] private string _zodpovedny = "";
    [ObservableProperty] private string _infoText = "";

    public TykaSaVM TykaSa { get; }

    partial void OnVybranaChanging(UlohaRiadok? value)
    {
        if (!_nacitavam)
        {
            UlozTeraz();
        }
    }

    partial void OnVybranaChanged(UlohaRiadok? value)
    {
        if (value is null && _nacitavam)
        {
            return;
        }

        var predtym = _nacitavam;
        _nacitavam = true;
        Text = value?.Text ?? "";
        Popis = value?.Popis ?? "";
        Termin = value?.Termin;
        Priorita = value?.Priorita ?? PrioritaUlohy.Normalna;
        Hotovo = value?.Hotovo ?? false;
        Zodpovedny = value?.Zodpovedny ?? "";
        TykaSa.Nastav(value?.TykaSa ?? Array.Empty<Naviazanie>());
        InfoText = value is null
            ? ""
            : $"Vytvorená {Planovanie.DatumCasText(value.Vytvorene)}" +
              (value.DatumHotovo is { } h ? $"  ·  hotová {Planovanie.DatumCasText(h)}" : "") +
              (value.Termin is not null ? "  ·  úloha s termínom je aj v kalendári" : "");
        _nacitavam = predtym;
        _zmenene = false;
    }

    // text sa uloží chvíľu po dopísaní, ostatné zmeny hneď
    partial void OnTextChanged(string value) => Zmena(hned: false);
    partial void OnPopisChanged(string value) => Zmena(hned: false);
    partial void OnTerminChanged(DateTime? value) => Zmena(hned: true);
    partial void OnPrioritaChanged(PrioritaUlohy value) => Zmena(hned: true);
    partial void OnHotovoChanged(bool value) => Zmena(hned: true);
    partial void OnZodpovednyChanged(string value) => Zmena(hned: false);

    private void Zmena(bool hned)
    {
        if (_nacitavam || Vybrana is null)
        {
            return;
        }

        _zmenene = true;
        _ukladanie.Stop();
        if (hned)
        {
            Obnov();
        }
        else
        {
            _ukladanie.Start();
        }
    }

    /// <summary>Uloží rozpísané zmeny vybranej úlohy (aj pri odchode zo sekcie).</summary>
    public void UlozTeraz()
    {
        _ukladanie.Stop();
        if (!_zmenene || Vybrana is not { } u)
        {
            return;
        }

        _zmenene = false;
        if (string.IsNullOrWhiteSpace(Text))
        {
            return; // prázdny text sa neuloží – úloha si ponechá pôvodný
        }

        try
        {
            _sluzba.Uloz(new Uloha
            {
                Id = u.Id,
                Text = Text,
                Popis = Popis,
                Termin = Termin,
                Priorita = Priorita,
                Hotovo = Hotovo ? u.DatumHotovo ?? DateTime.Now : null,
                Zodpovedny = Zodpovedny,
                TykaSa = TykaSa.Id.ToList(),
            });
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    public void Dispose()
    {
        UlozTeraz();
        _ukladanie.Stop();
    }

    [RelayCommand]
    private void Odstranit()
    {
        if (Vybrana is not { } u)
        {
            return;
        }

        UlozTeraz();
        Planovanie.Odstran(u.Id);
        _nacitavam = true;
        Vybrana = null;
        _nacitavam = false;
        Obnov();
        Hlavny.Oznam($"Úloha „{u.Text}“ je odstránená.", () =>
        {
            Planovanie.Obnov(u.Id);
            Obnov();
        });
    }

    [RelayCommand]
    private void PrepnutHotovo(UlohaRiadok? uloha)
    {
        if (uloha is null)
        {
            return;
        }

        UlozTeraz();
        _sluzba.NastavHotovo(uloha.Id, !uloha.Hotovo);
        Obnov();
    }

    [RelayCommand]
    private void OdstranitHotove()
    {
        if (!SuHotove)
        {
            return;
        }

        UlozTeraz();
        var odstranene = _sluzba.OdstranHotove();
        Obnov();
        Hlavny.Oznam(odstranene.Count == 1 ? "Odstránená 1 hotová úloha." : $"Odstránené hotové úlohy: {odstranene.Count}.", () =>
        {
            foreach (var id in odstranene)
            {
                Planovanie.Obnov(id);
            }

            Obnov();
        });
    }

    [RelayCommand] private void TerminDnes() => Termin = DateTime.Today;
    [RelayCommand] private void TerminZajtra() => Termin = DateTime.Today.AddDays(1);
    [RelayCommand] private void TerminOTyzden() => Termin = DateTime.Today.AddDays(7);
    [RelayCommand] private void TerminZrusit() => Termin = null;

    /// <summary>Otvorí kalendár na dni termínu úlohy.</summary>
    [RelayCommand]
    private void OtvoritVKalendari()
    {
        if (Termin is { } t)
        {
            Hlavny.OtvorKalendar(t); // rozpísané zmeny uloží prechod na inú stránku
        }
    }

    private string PodnadpisTlace => string.Join(" · ", new[]
    {
        FilterObjektu.Hodnota is null ? "" : FilterObjektu.Nazov.Trim(),
        Filter switch { FilterUloh.Otvorene => "otvorené", FilterUloh.Hotove => "hotové", _ => "" },
    }.Where(t => t.Length > 0));

    /// <summary>Vytlačí zobrazený zoznam úloh (podľa filtra a hľadania).</summary>
    [RelayCommand]
    private void Tlacit()
    {
        UlozTeraz();
        TlacPlanovania.Ulohy(Zoznam, PodnadpisTlace, pdf: false);
    }

    [RelayCommand]
    private void UlozitPdf()
    {
        UlozTeraz();
        TlacPlanovania.Ulohy(Zoznam, PodnadpisTlace, pdf: true);
    }

    // ------------------------------------------------------------------ navrhované úlohy z osnov

    private readonly List<NavrhUlohy> _vsetkyNavrhy;

    [ObservableProperty]
    private List<NavrhUlohy> _navrhy = new();

    /// <summary>Zoznam návrhov je rozbalený (predvolene zbalený – vidno len počet, zoznam úloh má viac miesta).</summary>
    [ObservableProperty]
    private bool _ukazNavrhy = Databaza.Citaj(KlucRozbaleneNavrhy, "0") == "1";

    [ObservableProperty]
    private int _pocetSkrytych;

    private const string KlucRozbaleneNavrhy = "ulohy.navrhy.rozbalene";

    partial void OnUkazNavrhyChanged(bool value) => Databaza.Zapis(KlucRozbaleneNavrhy, value ? "1" : "0");

    private void NacitajNavrhy()
    {
        try
        {
            Navrhy = NavrhyUloh.Zapnute ? NavrhyUloh.BezVybavenych(_vsetkyNavrhy) : new List<NavrhUlohy>();
            PocetSkrytych = NavrhyUloh.Skryte().Count;
        }
        catch (Exception)
        {
            Navrhy = new List<NavrhUlohy>();
        }
    }

    [RelayCommand]
    private void PrevziatNavrh(NavrhUlohy? navrh)
    {
        if (navrh is null)
        {
            return;
        }

        var id = NavrhyUloh.Prevezmi(navrh);
        if (Filter == FilterUloh.Hotove)
        {
            Filter = FilterUloh.Otvorene;
        }

        Obnov();
        Vybrana = Zoznam.FirstOrDefault(u => u.Id == id);
        Hlavny.Oznam($"Úloha „{navrh.Text}“ je pridaná – doplňte termín a zodpovedného.");
    }

    [RelayCommand]
    private void SkrytNavrh(NavrhUlohy? navrh)
    {
        if (navrh is null)
        {
            return;
        }

        NavrhyUloh.Skry(navrh.Kluc);
        NacitajNavrhy();
        Hlavny.Oznam("Návrh je skrytý.", () =>
        {
            NavrhyUloh.Skry(navrh.Kluc, skryt: false);
            NacitajNavrhy();
        });
    }

    [RelayCommand]
    private void ZobrazitSkryteNavrhy()
    {
        NavrhyUloh.ZobrazSkryte();
        NacitajNavrhy();
    }
}
