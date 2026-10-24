using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Udalosť na výber pri poznámke („Poznámka k udalosti“).</summary>
public sealed record VolbaUdalosti(long Id, string Text)
{
    public override string ToString() => Text;
}

/// <summary>
/// Úprava jednej poznámky – v sekcii Poznámky aj v samostatnom okne (z kalendára, z panela pri predmete).
/// Ukladá sa priebežne chvíľu po poslednej zmene a pri zatvorení; nová poznámka vznikne, keď sa do nej niečo napíše.
/// </summary>
public sealed partial class PoznamkaEditorVM : ObservableObject, IDisposable
{
    private static readonly VolbaUdalosti BezUdalosti = new(0, "(žiadna)");

    private readonly MainViewModel _hlavny;
    private readonly PoznamkyService _sluzba = new();
    private readonly DispatcherTimer _ukladanie = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private readonly bool _nacitavam;

    /// <param name="id">Upravovaná poznámka alebo null pre novú.</param>
    /// <param name="datum">Deň v kalendári pre novú poznámku.</param>
    /// <param name="tykaSa">Predmety, oblasti, udalosť … pre novú poznámku.</param>
    public PoznamkaEditorVM(MainViewModel hlavny, long? id, DateTime? datum = null, IEnumerable<long>? tykaSa = null)
    {
        _hlavny = hlavny;
        _nacitavam = true;
        TykaSa = new TykaSaVM(hlavny, Zmena);
        var poznamka = id is { } i ? _sluzba.Nacitaj(i) ?? throw new SkvpException("Poznámka už neexistuje (bola odstránená).") : null;
        var naviazania = (poznamka?.TykaSa ?? tykaSa?.ToList() ?? new List<long>()).Select(Planovanie.Objekt).OfType<Naviazanie>().ToList();
        var udalost = naviazania.FirstOrDefault(n => n.Typ == TypObjektu.Udalost);
        Udalosti = new[] { BezUdalosti }
            .Concat(new KalendarService().UdalostiNaVyber().Select(u => new VolbaUdalosti(u.Id, $"{Planovanie.DatumText(u.Zaciatok)}  {u.Nazov}")))
            .ToList();
        _udalost = udalost is null ? BezUdalosti : Udalosti.FirstOrDefault(u => u.Id == udalost.Id) ?? BezUdalosti;
        TykaSa.Nastav(naviazania.Where(n => n.Typ != TypObjektu.Udalost));
        if (poznamka is null)
        {
            _farba = PoznamkyService.Farby[0];
            _datum = datum?.Date;
        }
        else
        {
            Id = poznamka.Id;
            _nazov = poznamka.Nazov;
            _text = poznamka.Text;
            _farba = poznamka.Farba;
            _pripnuta = poznamka.Pripnuta;
            _datum = poznamka.Datum;
            Info = $"Vytvorená {Planovanie.DatumCasText(poznamka.Vytvorene)}  ·  zmenená {Planovanie.DatumCasText(poznamka.Zmenene)}";
        }

        _ukladanie.Tick += (_, _) => Uloz();
        _nacitavam = false;
    }

    /// <summary>Id poznámky (0 = nová, ešte neuložená).</summary>
    public long Id { get; private set; }

    public bool JeNova => Id == 0;

    public string Titulok => JeNova ? "Nová poznámka" : $"Poznámka – {Nazov}";

    public IReadOnlyList<string> Farby => PoznamkyService.Farby;

    public IReadOnlyList<VolbaUdalosti> Udalosti { get; }

    public TykaSaVM TykaSa { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Titulok))]
    private string _nazov = "";

    [ObservableProperty]
    private string _text = "";

    [ObservableProperty]
    private string _farba;

    [ObservableProperty]
    private bool _pripnuta;

    [ObservableProperty]
    private DateTime? _datum;

    [ObservableProperty]
    private VolbaUdalosti _udalost;

    [ObservableProperty]
    private string _info = "Poznámka sa uloží automaticky, keď do nej niečo napíšete.";

    /// <summary>Po uložení (false) alebo odstránení (true) – zoznam sa obnoví.</summary>
    public event Action<bool>? Zmenena;

    partial void OnNazovChanged(string value) => Zmena();
    partial void OnTextChanged(string value) => Zmena();
    partial void OnFarbaChanged(string value) => Zmena();
    partial void OnPripnutaChanged(bool value) => Zmena();
    partial void OnDatumChanged(DateTime? value) => Zmena();
    partial void OnUdalostChanged(VolbaUdalosti value) => Zmena();

    private bool _zmenene;

    private void Zmena()
    {
        if (_nacitavam)
        {
            return;
        }

        _zmenene = true;
        _ukladanie.Stop();
        _ukladanie.Start();
    }

    [RelayCommand]
    private void NastavFarbu(string? farba)
    {
        if (farba is not null)
        {
            Farba = farba;
        }
    }

    [RelayCommand]
    private void Dnes() => Datum = DateTime.Today;

    [RelayCommand]
    private void BezDna() => Datum = null;

    /// <summary>Uloží zmeny (prázdnu novú poznámku nie). Vráti false pri chybe.</summary>
    public bool Uloz()
    {
        _ukladanie.Stop();
        if (!_zmenene)
        {
            return true;
        }

        if (JeNova && Nazov.Trim().Length == 0 && Text.Trim().Length == 0)
        {
            return true;
        }

        _zmenene = false;
        try
        {
            var ciele = TykaSa.Id.ToList();
            if (Udalost.Id > 0)
            {
                ciele.Add(Udalost.Id);
            }

            Id = _sluzba.Uloz(new Poznamka
            {
                Id = Id, Nazov = Nazov, Text = Text, Farba = Farba, Pripnuta = Pripnuta, Datum = Datum, TykaSa = ciele,
            });
            Info = $"Uložená {DateTime.Now:H:mm}";
            OnPropertyChanged(nameof(JeNova));
            OnPropertyChanged(nameof(Titulok));
            Zmenena?.Invoke(false);
            return true;
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
            return false;
        }
    }

    [RelayCommand]
    private void Odstranit()
    {
        _ukladanie.Stop();
        _zmenene = false;
        if (JeNova)
        {
            Zmenena?.Invoke(true);
            return;
        }

        var id = Id;
        var nazov = Nazov;
        Planovanie.Odstran(id);
        Zmenena?.Invoke(true);
        _hlavny.ObnovPlanovanie();
        _hlavny.Oznam($"Poznámka „{nazov}“ je odstránená.", () =>
        {
            Planovanie.Obnov(id);
            _hlavny.ObnovPlanovanie();
            (_hlavny.Stranka as PoznamkyViewModel)?.Obnov(id);
        });
    }

    /// <summary>Skopíruje text poznámky do schránky – napr. na vloženie do osnovy (Ctrl+V v editore osnovy).</summary>
    [RelayCommand]
    private void Kopirovat()
    {
        var text = string.Join(Environment.NewLine, new[] { Nazov.Trim(), Text.Trim() }.Where(t => t.Length > 0).Distinct());
        if (text.Length == 0)
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
            _hlavny.Oznam("Text poznámky je v schránke – vložte ho napr. do osnovy klávesmi Ctrl+V.");
        }
        catch (Exception)
        {
            // schránku práve používa iný program
        }
    }

    public void Dispose()
    {
        Uloz();
        _ukladanie.Stop();
    }
}

/// <summary>Sekcia Poznámky: nástenka lístkov (pripnuté hore), hľadanie, filter „Týka sa“ a úprava vybranej poznámky.</summary>
public sealed partial class PoznamkyViewModel : ObservableObject, IUkladaPredOdchodom, IDisposable
{
    private readonly PoznamkyService _sluzba = new();
    private bool _menimVyber;

    public PoznamkyViewModel(MainViewModel hlavny, long? vybrana = null)
    {
        Hlavny = hlavny;
        Filtre = FilterOsnov.Volby(hlavny);
        _filterObjektu = Filtre[0];
        Obnov(vybrana);
        if (vybrana is null && Zoznam.Count > 0)
        {
            Vybrana = Zoznam[0];
        }
    }

    public MainViewModel Hlavny { get; }

    public IReadOnlyList<Volba<long?>> Filtre { get; }

    [ObservableProperty]
    private Volba<long?> _filterObjektu;

    partial void OnFilterObjektuChanged(Volba<long?> value) => Obnov(Editor?.Id);

    [ObservableProperty]
    private string _hladat = "";

    partial void OnHladatChanged(string value) => Obnov(Editor?.Id);

    [ObservableProperty]
    private List<PoznamkaRiadok> _zoznam = new();

    [ObservableProperty]
    private PoznamkaRiadok? _vybrana;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeEditor))]
    private PoznamkaEditorVM? _editor;

    public bool JeEditor => Editor is not null;

    [ObservableProperty]
    private string _suhrn = "";

    public string TextPrazdneho => Hladat.Trim().Length > 0 || FilterObjektu.Hodnota is not null
        ? "Takéto poznámky nie sú."
        : "Zatiaľ žiadne poznámky. Vytvorte prvú tlačidlom Nová poznámka.";

    /// <summary>Načíta zoznam a vyberie poznámku (alebo ponechá rozpísanú novú).</summary>
    public void Obnov(long? vybrat = null)
    {
        try
        {
            Zoznam = _sluzba.Zoznam(Hladat, FilterOsnov.Objekty(Hlavny, FilterObjektu));
            var spolu = _sluzba.Pocet();
            Suhrn = Zoznam.Count == spolu ? Preklad.Pocet(spolu, "{0} poznámka", "{0} poznámky", "{0} poznámok") : $"Zobrazené: {Zoznam.Count} z {spolu}";
        }
        catch (Exception ex)
        {
            Zoznam = new List<PoznamkaRiadok>();
            Suhrn = $"Poznámky sa nepodarilo načítať: {ex.GetBaseException().Message}";
        }

        OnPropertyChanged(nameof(TextPrazdneho));
        _menimVyber = true;
        Vybrana = vybrat is { } id ? Zoznam.FirstOrDefault(p => p.Id == id) : null;
        _menimVyber = false;
        if (vybrat is { } v && (Editor is null || Editor.Id != v) && Vybrana is not null)
        {
            OtvorVEditore(v);
        }
    }

    partial void OnVybranaChanged(PoznamkaRiadok? value)
    {
        if (!_menimVyber && value is not null && Editor?.Id != value.Id)
        {
            OtvorVEditore(value.Id);
        }
    }

    private void OtvorVEditore(long? id, DateTime? datum = null)
    {
        if (Editor is { } predosly)
        {
            predosly.Zmenena -= PoZmene;
            predosly.Dispose();
        }

        try
        {
            Editor = new PoznamkaEditorVM(Hlavny, id, datum, FilterObjektu.Hodnota is { } objekt && id is null ? new[] { objekt } : null);
            Editor.Zmenena += PoZmene;
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
            Editor = null;
        }
    }

    private void PoZmene(bool odstranena)
    {
        if (odstranena)
        {
            Editor = null;
            Obnov();
            return;
        }

        Obnov(Editor?.Id);
        Hlavny.ObnovPlanovanie();
    }

    [RelayCommand]
    private void NovaPoznamka()
    {
        Vybrana = null;
        OtvorVEditore(null);
    }

    [RelayCommand]
    private void PrepnutPripnutie(PoznamkaRiadok? poznamka)
    {
        if (poznamka is null)
        {
            return;
        }

        Editor?.Uloz();
        _sluzba.NastavPripnutie(poznamka.Id, !poznamka.Pripnuta);
        Obnov(Editor?.Id);
    }

    private string PodnadpisTlace => FilterObjektu.Hodnota is null ? "" : FilterObjektu.Nazov.Trim();

    /// <summary>Vytlačí zobrazené poznámky (podľa filtra a hľadania).</summary>
    [RelayCommand]
    private void Tlacit()
    {
        UlozTeraz();
        TlacPlanovania.Poznamky(Zoznam, PodnadpisTlace, pdf: false);
    }

    [RelayCommand]
    private void UlozitPdf()
    {
        UlozTeraz();
        TlacPlanovania.Poznamky(Zoznam, PodnadpisTlace, pdf: true);
    }

    public void UlozTeraz() => Editor?.Uloz();

    public void Dispose() => Editor?.Dispose();
}
