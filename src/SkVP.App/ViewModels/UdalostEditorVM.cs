using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>
/// Úprava udalosti v kalendári: názov, typ, deň a čas (alebo celý deň či viac dní), opakovanie, pripomienka, stav,
/// miesto, popis, „Týka sa“ (predmety, oblasti, časti osnov) a poznámky k udalosti (napr. zápis z porady).
/// </summary>
public sealed partial class UdalostEditorVM : ObservableObject
{
    private readonly MainViewModel _hlavny;
    private readonly KalendarService _sluzba = new();
    private readonly Guid _guid = Guid.NewGuid();

    public UdalostEditorVM(MainViewModel hlavny, long? id, DateTime? den, IEnumerable<long>? tykaSa = null)
    {
        _hlavny = hlavny;
        TykaSa = new TykaSaVM(hlavny);
        var udalost = id is { } i ? _sluzba.Nacitaj(i) ?? throw new SkvpException("Udalosť už neexistuje (bola odstránená).") : null;
        Typy = _sluzba.Typy().Where(t => t.Aktivny || t.Id == udalost?.TypId).ToList();
        if (udalost is null)
        {
            var zaciatok = (den ?? DateTime.Today).Date;
            _datum = zaciatok;
            _casZaciatku = "8:00";
            _casKonca = "9:00";
            _typ = Typy.FirstOrDefault();
            TykaSa.Nastav(tykaSa ?? Array.Empty<long>());
            return;
        }

        Id = udalost.Id;
        _guid = udalost.Guid;
        _nazov = udalost.Nazov;
        _popis = udalost.Popis ?? "";
        _miesto = udalost.Miesto ?? "";
        _typ = Typy.FirstOrDefault(t => t.Id == udalost.TypId) ?? Typy.FirstOrDefault();
        _celyDen = udalost.CelyDen;
        _datum = udalost.Zaciatok.Date;
        _casZaciatku = udalost.CelyDen ? "8:00" : udalost.Zaciatok.ToString("H:mm", CultureInfo.InvariantCulture);
        _koniec = udalost.Koniec is { } k && (udalost.CelyDen ? k.Date > udalost.Zaciatok.Date : k.Date != udalost.Zaciatok.Date) ? k.Date : null;
        _casKonca = udalost.Koniec is { } kk && !udalost.CelyDen ? kk.ToString("H:mm", CultureInfo.InvariantCulture) : "";
        _opakovanie = udalost.Opakovanie;
        _opakovanieDo = udalost.OpakovanieDo;
        _pripomienka = udalost.Pripomienka;
        _stav = udalost.Stav;
        TykaSa.Nastav(udalost.TykaSa);
        NacitajPoznamky();
    }

    public long Id { get; private set; }

    public bool JeNova => Id == 0;

    public string Titulok => JeNova ? "Nová udalosť" : "Udalosť";

    public List<TypUdalosti> Typy { get; }

    public IReadOnlyList<Volba<OpakovanieUdalosti>> Opakovania => ZoznamOpakovania;

    private static readonly IReadOnlyList<Volba<OpakovanieUdalosti>> ZoznamOpakovania =
        Enum.GetValues<OpakovanieUdalosti>().Select(o => new Volba<OpakovanieUdalosti>(o, KalendarService.NazovOpakovania(o))).ToList();

    public IReadOnlyList<Volba<PripomienkaUdalosti>> Pripomienky => ZoznamPripomienky;

    private static readonly IReadOnlyList<Volba<PripomienkaUdalosti>> ZoznamPripomienky =
        Enum.GetValues<PripomienkaUdalosti>().Select(p => new Volba<PripomienkaUdalosti>(p, KalendarService.NazovPripomienky(p))).ToList();

    public IReadOnlyList<Volba<StavUdalosti>> Stavy => ZoznamStavy;

    private static readonly IReadOnlyList<Volba<StavUdalosti>> ZoznamStavy =
        Enum.GetValues<StavUdalosti>().Select(s => new Volba<StavUdalosti>(s, KalendarService.NazovStavu(s))).ToList();

    public TykaSaVM TykaSa { get; }

    [ObservableProperty] private string _nazov = "";
    [ObservableProperty] private TypUdalosti? _typ;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeCas))]
    private bool _celyDen;

    public bool JeCas => !CelyDen;

    [ObservableProperty] private DateTime _datum;
    [ObservableProperty] private string _casZaciatku = "";

    /// <summary>Posledný deň udalosti cez viac dní (null = v ten istý deň).</summary>
    [ObservableProperty] private DateTime? _koniec;

    [ObservableProperty] private string _casKonca = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Opakuje))]
    private OpakovanieUdalosti _opakovanie;

    public bool Opakuje => Opakovanie != OpakovanieUdalosti.Ziadne;

    [ObservableProperty] private DateTime? _opakovanieDo;
    [ObservableProperty] private PripomienkaUdalosti _pripomienka = PripomienkaUdalosti.DenVopred;
    [ObservableProperty] private StavUdalosti _stav;
    [ObservableProperty] private string _miesto = "";
    [ObservableProperty] private string _popis = "";
    [ObservableProperty] private List<PoznamkaRiadok> _poznamky = new();

    private void NacitajPoznamky() => Poznamky = Id == 0 ? new List<PoznamkaRiadok>() : new PoznamkyService().Zoznam(patriK: new[] { Id });

    private static TimeSpan? Cas(string text) =>
        TimeSpan.TryParseExact((text ?? "").Trim().Replace('.', ':'), new[] { @"h\:mm", @"hh\:mm", "%h" }, CultureInfo.InvariantCulture, out var t)
        && t < TimeSpan.FromDays(1)
            ? t
            : null;

    /// <summary>Uloží udalosť. Vráti false pri chybe (hlásenie už zobrazilo).</summary>
    public bool Uloz()
    {
        try
        {
            DateTime zaciatok;
            DateTime? koniec;
            if (CelyDen)
            {
                zaciatok = Datum.Date;
                koniec = Koniec?.Date;
            }
            else
            {
                var od = Cas(CasZaciatku) ?? throw new SkvpException("Čas začiatku zadajte v tvare 8:00.");
                var dokedy = CasKonca.Trim().Length == 0 ? (TimeSpan?)null : Cas(CasKonca) ?? throw new SkvpException("Čas konca zadajte v tvare 9:30.");
                zaciatok = Datum.Date + od;
                koniec = dokedy is { } d ? (Koniec ?? Datum).Date + d : Koniec?.Date + od;
            }

            Id = _sluzba.Uloz(new Udalost
            {
                Id = Id,
                Guid = _guid,
                Nazov = Nazov,
                Popis = Popis,
                TypId = Typ?.Id,
                Zaciatok = zaciatok,
                Koniec = koniec,
                CelyDen = CelyDen,
                Opakovanie = Opakovanie,
                OpakovanieDo = OpakovanieDo,
                Pripomienka = Pripomienka,
                Stav = Stav,
                Miesto = Miesto,
                TykaSa = TykaSa.Id.ToList(),
            });
            return true;
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
            return false;
        }
    }

    /// <summary>Odstráni udalosť (dá sa vrátiť späť). Vráti true, ak bola odstránená.</summary>
    public bool Odstran()
    {
        if (JeNova)
        {
            return false;
        }

        var id = Id;
        Planovanie.Odstran(id);
        _hlavny.Oznam($"Udalosť „{Nazov}“ je odstránená.", () =>
        {
            Planovanie.Obnov(id);
            _hlavny.ObnovPlanovanie();
            (_hlavny.Stranka as KalendarViewModel)?.Obnov();
        });
        return true;
    }

    [RelayCommand]
    private void NovaPoznamka()
    {
        if (JeNova && !Uloz())
        {
            return;
        }

        Views.PoznamkaWindow.Otvor(new PoznamkaEditorVM(_hlavny, null, Datum, new[] { Id }));
        NacitajPoznamky();
    }

    [RelayCommand]
    private void OtvoritPoznamku(PoznamkaRiadok? poznamka)
    {
        if (poznamka is not null)
        {
            Views.PoznamkaWindow.Otvor(new PoznamkaEditorVM(_hlavny, poznamka.Id));
            NacitajPoznamky();
        }
    }
}
