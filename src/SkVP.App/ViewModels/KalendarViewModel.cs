using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kalendarium;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

public enum RezimKalendara
{
    Mesiac,
    Tyzden,
    Zoznam,
    SkolskyRok,
}

/// <summary>Jeden deň v kalendári (bunka mesiaca, stĺpec týždňa, deň zoznamu alebo štvorček školského roka).</summary>
public sealed partial class DenKalendara : ObservableObject
{
    public const int NajviacVBunke = 4;

    public required DateTime Datum { get; init; }
    public bool MimoMesiaca { get; init; }
    public bool Dnes { get; init; }
    public bool Vikend { get; init; }

    /// <summary>Deň pracovného pokoja (štátny sviatok).</summary>
    public bool Volno { get; init; }

    public string? Sviatok { get; init; }
    public string Meniny { get; init; } = "";

    /// <summary>Názov prázdnin, ak je v tento deň voľno (podľa kraja).</summary>
    public string? Prazdniny { get; init; }

    public List<VyskytVKalendari> Vyskyty { get; init; } = new();
    public List<PoznamkaRiadok> Poznamky { get; init; } = new();

    [ObservableProperty]
    private bool _vybrany;

    public int Cislo => Datum.Day;
    public string NazovDna => PocasieService.VelkePrve(Datum.ToString("dddd", Planovanie.Sk));
    public string KratkyNazovDna => Datum.ToString("ddd", Planovanie.Sk).TrimEnd('.');
    public string DatumText => $"{Datum.Day}. {Datum.Month}.";
    public string DlhyDatum => PocasieService.VelkePrve(Datum.ToString("dddd d. MMMM yyyy", Planovanie.Sk));
    public bool MaSviatok => !string.IsNullOrEmpty(Sviatok);
    public bool JePrazdniny => Prazdniny is not null;
    public bool MaPoznamky => Poznamky.Count > 0;
    public bool MaVyskyty => Vyskyty.Count > 0;
    public bool Zvyraznit => Volno || Vikend;

    /// <summary>Bez vyučovania: víkend, sviatok alebo prázdniny.</summary>
    public bool BezVyucovania => Volno || Vikend || JePrazdniny;

    /// <summary>V bunke sa neopakujú prázdniny (deň je podfarbený) – ukážu sa vo výpise dňa.</summary>
    public List<VyskytVKalendari> Zobrazene => Vyskyty.Where(v => v.Zdroj != ZdrojKalendara.Prazdniny).Take(NajviacVBunke).ToList();

    public int Dalsie => Math.Max(0, Vyskyty.Count(v => v.Zdroj != ZdrojKalendara.Prazdniny) - NajviacVBunke);
    public string DalsieText => Dalsie > 0 ? $"+ {Dalsie} ďalšie" : "";
    public string PoznamkyTip => string.Join("\n", Poznamky.Select(p => "• " + p.Nazov));

    /// <summary>Farba bodky v prehľade školského roka (prvá vlastná udalosť alebo úloha dňa).</summary>
    public string? FarbaBodky => Vyskyty.FirstOrDefault(v => v.JeVlastny)?.Farba;

    public string Tip => string.Join("\n", new[] { DlhyDatum, Prazdniny ?? "", Sviatok ?? "" }
        .Concat(Vyskyty.Where(v => v.Zdroj != ZdrojKalendara.Prazdniny).Select(v => "• " + v.KratkyText)).Where(t => t.Length > 0));
}

public sealed record TyzdenKalendara(int Cislo, List<DenKalendara> Dni);

/// <summary>Mesiac v prehľade školského roka: názov a 42 políčok (prázdne pred prvým a za posledným dňom).</summary>
public sealed record MiniMesiac(string Nazov, List<DenKalendara?> Dni);

public sealed record PolozkaLegendy(string Nazov, string Farba);

/// <summary>
/// Kalendár: mesiac, týždeň, zoznam na 60 dní a celý školský rok (september – august) so sviatkami, menami, prázdninami
/// podľa kraja a automatickými termínmi. Vpravo je vybraný deň so všetkým, čo sa v ňom deje, a s akciami.
/// </summary>
public sealed partial class KalendarViewModel : ObservableObject
{
    private const int DniZoznamu = 60;

    private readonly KalendarService _sluzba = new();
    private readonly bool _pripravene;

    public KalendarViewModel(MainViewModel hlavny, DateTime datum)
    {
        Hlavny = hlavny;
        _aktualnyDatum = datum.Date;
        _rezim = Enum.TryParse<RezimKalendara>(Databaza.Citaj(KlucRezim), out var r) ? r : RezimKalendara.Mesiac;
        var nastavenia = _sluzba.Nastavenia;
        _ukazNovinky = nastavenia.Novinky;
        _ukazSkolskyRok = nastavenia.SkolskyRok;
        _ukazOsnovy = nastavenia.Osnovy;
        _ukazZalohy = nastavenia.Zalohy;
        _ukazArchiv = nastavenia.Archiv;
        try
        {
            Kraje = new[] { NovinkyService.VsetkyKraje }
                .Concat(NovinkyService.Nacitaj().Polozky.Where(KalendarService.JePrazdniny).Select(p => p.Skupina).OfType<string>().Distinct().Order())
                .ToList();
        }
        catch (Exception)
        {
            Kraje = new List<string> { NovinkyService.VsetkyKraje };
        }

        _kraj = Kraje.Contains(_sluzba.Kraj) ? _sluzba.Kraj : NovinkyService.VsetkyKraje;
        _pripravene = true;
        Obnov();
    }

    public const string KlucRezim = "kalendar.rezim";

    public MainViewModel Hlavny { get; }

    public string[] NazvyDni { get; } = { "Po", "Ut", "St", "Št", "Pi", "So", "Ne" };

    // ------------------------------------------------------------------ režim a obdobie

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeMesiac), nameof(JeTyzden), nameof(JeZoznam), nameof(JeSkolskyRok))]
    private RezimKalendara _rezim;

    public bool JeMesiac { get => Rezim == RezimKalendara.Mesiac; set { if (value) Rezim = RezimKalendara.Mesiac; } }
    public bool JeTyzden { get => Rezim == RezimKalendara.Tyzden; set { if (value) Rezim = RezimKalendara.Tyzden; } }
    public bool JeZoznam { get => Rezim == RezimKalendara.Zoznam; set { if (value) Rezim = RezimKalendara.Zoznam; } }
    public bool JeSkolskyRok { get => Rezim == RezimKalendara.SkolskyRok; set { if (value) Rezim = RezimKalendara.SkolskyRok; } }

    partial void OnRezimChanged(RezimKalendara value)
    {
        Databaza.Zapis(KlucRezim, value.ToString());
        Obnov();
    }

    [ObservableProperty]
    private DateTime _aktualnyDatum;

    partial void OnAktualnyDatumChanged(DateTime value)
    {
        OnPropertyChanged(nameof(SkocNa));
        Obnov();
    }

    /// <summary>Výber dátumu „Skočiť na“.</summary>
    public DateTime? SkocNa
    {
        get => AktualnyDatum;
        set
        {
            if (value is { } d && d.Date != AktualnyDatum)
            {
                AktualnyDatum = d.Date;
            }
        }
    }

    [ObservableProperty] private string _titulok = "";
    [ObservableProperty] private string _suhrn = "";
    [ObservableProperty] private List<TyzdenKalendara> _tyzdne = new();
    [ObservableProperty] private List<DenKalendara> _dniTyzdna = new();
    [ObservableProperty] private List<DenKalendara> _dniZoznamu = new();
    [ObservableProperty] private List<MiniMesiac> _mesiace = new();
    [ObservableProperty] private List<PolozkaLegendy> _legenda = new();

    // ------------------------------------------------------------------ čo zobraziť (uloží sa do nastavení kalendára)

    [ObservableProperty] private bool _ukazNovinky;
    [ObservableProperty] private bool _ukazSkolskyRok;
    [ObservableProperty] private bool _ukazOsnovy;
    [ObservableProperty] private bool _ukazZalohy;
    [ObservableProperty] private bool _ukazArchiv;

    [ObservableProperty]
    private bool _ukazVybavene = Databaza.Citaj("kalendar.vybavene", "1") != "0";

    partial void OnUkazNovinkyChanged(bool value) => ZmenaZobrazenia();
    partial void OnUkazSkolskyRokChanged(bool value) => ZmenaZobrazenia();
    partial void OnUkazOsnovyChanged(bool value) => ZmenaZobrazenia();
    partial void OnUkazZalohyChanged(bool value) => ZmenaZobrazenia();
    partial void OnUkazArchivChanged(bool value) => ZmenaZobrazenia();

    partial void OnUkazVybaveneChanged(bool value)
    {
        Databaza.Zapis("kalendar.vybavene", value ? "1" : "0");
        Obnov();
    }

    private void ZmenaZobrazenia()
    {
        if (!_pripravene)
        {
            return;
        }

        _sluzba.Nastavenia = _sluzba.Nastavenia with
        {
            Novinky = UkazNovinky, SkolskyRok = UkazSkolskyRok, Osnovy = UkazOsnovy, Zalohy = UkazZalohy, Archiv = UkazArchiv,
        };
        _sluzba.Nastavenia.Uloz();
        Obnov();
    }

    public List<string> Kraje { get; }

    /// <summary>Kraj pre jarné prázdniny (spoločný s dashboardom).</summary>
    [ObservableProperty]
    private string _kraj;

    partial void OnKrajChanged(string value)
    {
        Databaza.Zapis(NovinkyService.KlucKraj, value);
        _sluzba.Kraj = value;
        Obnov();
    }

    // ------------------------------------------------------------------ vybraný deň

    [ObservableProperty]
    private DenKalendara? _vybranyDen;

    partial void OnVybranyDenChanged(DenKalendara? oldValue, DenKalendara? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Vybrany = false;
        }

        if (newValue is not null)
        {
            newValue.Vybrany = true;
        }
    }

    [RelayCommand]
    private void VybratDen(DenKalendara? den)
    {
        if (den is not null)
        {
            VybranyDen = den;
        }
    }

    // ------------------------------------------------------------------ načítanie

    public void Obnov()
    {
        if (!_pripravene)
        {
            return;
        }

        var vybrany = VybranyDen?.Datum ?? AktualnyDatum;
        try
        {
            switch (Rezim)
            {
                case RezimKalendara.Mesiac:
                    NacitajMesiac();
                    break;
                case RezimKalendara.Tyzden:
                    NacitajTyzden();
                    break;
                case RezimKalendara.Zoznam:
                    NacitajZoznam();
                    break;
                default:
                    NacitajSkolskyRok();
                    break;
            }
        }
        catch (Exception ex)
        {
            Suhrn = $"Kalendár sa nepodarilo načítať: {ex.GetBaseException().Message}";
        }

        VybranyDen = VsetkyDni().FirstOrDefault(d => d.Datum == vybrany.Date) ?? VsetkyDni().FirstOrDefault(d => d.Datum == AktualnyDatum)
            ?? Dni(vybrany.Date, vybrany.Date, null).FirstOrDefault();
        NacitajLegendu();
    }

    private IEnumerable<DenKalendara> VsetkyDni() => Rezim switch
    {
        RezimKalendara.Mesiac => Tyzdne.SelectMany(t => t.Dni),
        RezimKalendara.Tyzden => DniTyzdna,
        RezimKalendara.Zoznam => DniZoznamu,
        _ => Mesiace.SelectMany(m => m.Dni).OfType<DenKalendara>(),
    };

    public static DateTime Pondelok(DateTime datum) => datum.Date.AddDays(-(((int)datum.DayOfWeek + 6) % 7));

    private static string MesiacRok(DateTime datum) => PocasieService.VelkePrve(datum.ToString("MMMM yyyy", Planovanie.Sk));

    private void NacitajMesiac()
    {
        var prvy = new DateTime(AktualnyDatum.Year, AktualnyDatum.Month, 1);
        var od = Pondelok(prvy);
        var dni = Dni(od, od.AddDays(41), prvy.Month);
        Tyzdne = Enumerable.Range(0, 6).Select(t => new TyzdenKalendara(KalendarService.CisloTyzdna(od.AddDays(t * 7)), dni.Skip(t * 7).Take(7).ToList())).ToList();
        Titulok = MesiacRok(prvy);
        var pocet = dni.Where(d => !d.MimoMesiaca).SelectMany(d => d.Vyskyty).Where(v => v.JeVlastny).Select(v => v.Kluc).Distinct().Count();
        Suhrn = $"{Titulok}: {Tvar(pocet, "udalosť alebo úloha", "udalosti a úlohy", "udalostí a úloh")}. Dvojklikom na deň pridáte udalosť, pravým tlačidlom ďalšie možnosti.";
    }

    private void NacitajTyzden()
    {
        var od = Pondelok(AktualnyDatum);
        DniTyzdna = Dni(od, od.AddDays(6), null);
        Titulok = $"{od.Day}. {od.Month}. – {Planovanie.DatumText(od.AddDays(6))}  ·  {KalendarService.CisloTyzdna(od)}. týždeň";
        Suhrn = $"V týždni: {Tvar(DniTyzdna.SelectMany(d => d.Vyskyty).Count(v => v.JeVlastny), "udalosť alebo úloha", "udalosti a úlohy", "udalostí a úloh")}.";
    }

    private void NacitajZoznam()
    {
        var od = AktualnyDatum.Date;
        var po = od.AddDays(DniZoznamu - 1);
        DniZoznamu = Dni(od, po, null).Where(d => d.MaVyskyty || d.MaSviatok || d.MaPoznamky).ToList();
        Titulok = $"{Planovanie.DatumText(od)} – {Planovanie.DatumText(po)}";
        Suhrn = $"Zoznam na {DniZoznamu} dní: {Tvar(DniZoznamu.Sum(d => d.Vyskyty.Count), "záznam", "záznamy", "záznamov")}.";
    }

    private void NacitajSkolskyRok()
    {
        var rok = ArchivSkolskehoRoka.Aktualny(AktualnyDatum);
        var (od, _) = ArchivSkolskehoRoka.Obdobie(rok);
        var dni = Dni(od, od.AddYears(1).AddDays(-1), null).ToDictionary(d => d.Datum);
        Mesiace = Enumerable.Range(0, 12).Select(i =>
        {
            var prvy = od.AddMonths(i);
            var posun = ((int)prvy.DayOfWeek + 6) % 7;
            var policka = Enumerable.Range(0, 42).Select(p =>
            {
                var d = prvy.AddDays(p - posun);
                return d.Month == prvy.Month ? dni[d] : null;
            }).ToList();
            return new MiniMesiac(MesiacRok(prvy), policka);
        }).ToList();
        Titulok = $"Školský rok {rok}";
        var vyucovacie = dni.Values.Count(d => !d.BezVyucovania && d.Datum >= od && d.Datum < new DateTime(od.Year + 1, 7, 1));
        Suhrn = $"Školský rok {rok}: približne {vyucovacie} vyučovacích dní (bez víkendov, sviatkov a prázdnin). Kliknutím na deň ho zobrazíte vpravo, dvojklikom otvoríte mesiac.";
    }

    private static string Tvar(int n, string jeden, string dvaAzStyri, string viac) =>
        $"{n} " + (n == 1 ? jeden : n is >= 2 and <= 4 ? dvaAzStyri : viac);

    private List<DenKalendara> Dni(DateTime od, DateTime po, int? mesiac)
    {
        var vyskyty = _sluzba.Vyskyty(od, po).Where(v => UkazVybavene || !v.JeVybavena).ToList();
        var prazdniny = _sluzba.DniPrazdnin(od, po);
        var sviatky = new Dictionary<DateOnly, List<SviatokDna>>();
        try
        {
            for (var rok = od.Year; rok <= po.Year; rok++)
            {
                foreach (var s in KalendariumDb.Spolocna.Sviatky(rok))
                {
                    if (!sviatky.TryGetValue(s.Datum, out var zoznam))
                    {
                        sviatky[s.Datum] = zoznam = new List<SviatokDna>();
                    }

                    zoznam.Add(s);
                }
            }
        }
        catch (Exception)
        {
            // kalendár funguje aj bez Kalendária
        }

        var poznamky = new List<PoznamkaRiadok>();
        try
        {
            poznamky = new PoznamkyService().PreDni(od, po);
        }
        catch (Exception)
        {
            // bez poznámok
        }

        var dnes = DateTime.Today;
        var dni = new List<DenKalendara>();
        for (var d = od.Date; d <= po.Date; d = d.AddDays(1))
        {
            var den = DateOnly.FromDateTime(d);
            var sviatkyDna = sviatky.TryGetValue(den, out var s) ? s : new List<SviatokDna>();
            var meniny = "";
            try
            {
                meniny = string.Join(", ", KalendariumDb.Spolocna.Meniny(den));
            }
            catch (Exception)
            {
                // bez menín
            }

            var datum = d;
            dni.Add(new DenKalendara
            {
                Datum = d,
                MimoMesiaca = mesiac is { } m && d.Month != m,
                Dnes = d == dnes,
                Vikend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                Volno = sviatkyDna.Any(x => x.DenPracovnehoPokoja),
                Sviatok = sviatkyDna.Count == 0 ? null : string.Join(", ", sviatkyDna.Select(x => x.Nazov)),
                Meniny = meniny,
                Prazdniny = prazdniny.TryGetValue(d, out var p) ? p : null,
                Vyskyty = vyskyty.Where(v => v.Zaciatok.Date <= datum && v.PoslednyDen >= datum).ToList(),
                Poznamky = poznamky.Where(x => x.Datum?.Date == datum).ToList(),
            });
        }

        return dni;
    }

    private void NacitajLegendu()
    {
        var legenda = _sluzba.Typy(lenAktivne: true).Select(t => new PolozkaLegendy(t.Nazov, t.Farba)).ToList();
        legenda.Add(new PolozkaLegendy("úloha", Planovanie.FarbaUlohy));
        if (UkazSkolskyRok)
        {
            legenda.Add(new PolozkaLegendy("prázdniny", "#8CCB98"));
        }

        Legenda = legenda;
    }

    // ------------------------------------------------------------------ navigácia

    [RelayCommand]
    private void Predchadzajuce() => AktualnyDatum = Rezim switch
    {
        RezimKalendara.Mesiac => new DateTime(AktualnyDatum.Year, AktualnyDatum.Month, 1).AddMonths(-1),
        RezimKalendara.Tyzden => AktualnyDatum.AddDays(-7),
        RezimKalendara.Zoznam => AktualnyDatum.AddDays(-DniZoznamu),
        _ => AktualnyDatum.AddYears(-1),
    };

    [RelayCommand]
    private void Nasledujuce() => AktualnyDatum = Rezim switch
    {
        RezimKalendara.Mesiac => new DateTime(AktualnyDatum.Year, AktualnyDatum.Month, 1).AddMonths(1),
        RezimKalendara.Tyzden => AktualnyDatum.AddDays(7),
        RezimKalendara.Zoznam => AktualnyDatum.AddDays(DniZoznamu),
        _ => AktualnyDatum.AddYears(1),
    };

    [RelayCommand]
    private void Dnes()
    {
        VybranyDen = null;
        if (AktualnyDatum == DateTime.Today)
        {
            Obnov();
        }
        else
        {
            AktualnyDatum = DateTime.Today;
        }
    }

    /// <summary>Týždeň s daným dňom („+ N ďalšie“ v mesiaci).</summary>
    [RelayCommand]
    private void ZobrazTyzden(DenKalendara? den)
    {
        if (den is null)
        {
            return;
        }

        VybranyDen = den;
        AktualnyDatum = den.Datum;
        Rezim = RezimKalendara.Tyzden;
    }

    /// <summary>Mesiac s daným dňom (dvojklik v prehľade školského roka).</summary>
    [RelayCommand]
    private void ZobrazMesiac(DenKalendara? den)
    {
        if (den is null)
        {
            return;
        }

        VybranyDen = den;
        AktualnyDatum = den.Datum;
        Rezim = RezimKalendara.Mesiac;
    }

    // ------------------------------------------------------------------ udalosti, úlohy, poznámky

    private DateTime DenPreNove(DenKalendara? den) => (den ?? VybranyDen)?.Datum ?? AktualnyDatum;

    [RelayCommand]
    private void NovaUdalost(DenKalendara? den)
    {
        if (Views.UdalostWindow.Otvor(new UdalostEditorVM(Hlavny, null, DenPreNove(den))))
        {
            Obnov();
            Hlavny.ObnovPlanovanie();
        }
    }

    [RelayCommand]
    private void NovaUloha(DenKalendara? den)
    {
        // rýchle pridanie v Úlohách s predvyplneným termínom
        var datum = DenPreNove(den);
        Hlavny.OtvorUlohy(null);
        if (Hlavny.Stranka is UlohyViewModel ulohy)
        {
            ulohy.NovyTermin = datum;
        }
    }

    [RelayCommand]
    private void NovaPoznamka(DenKalendara? den)
    {
        if (Views.PoznamkaWindow.Otvor(new PoznamkaEditorVM(Hlavny, null, DenPreNove(den))))
        {
            Obnov();
            Hlavny.ObnovPlanovanie();
        }
    }

    /// <summary>Otvorí záznam: udalosť na úpravu, úlohu v Úlohách, platnosť osnovy v osnove, automatický termín v detaile dňa.</summary>
    [RelayCommand]
    private void OtvoritVyskyt(VyskytVKalendari? v)
    {
        switch (v)
        {
            case { Zdroj: ZdrojKalendara.Udalost, Id: { } id }:
                if (Views.UdalostWindow.Otvor(new UdalostEditorVM(Hlavny, id, null)))
                {
                    Obnov();
                    Hlavny.ObnovPlanovanie();
                }

                break;
            case { Zdroj: ZdrojKalendara.Uloha, Id: { } uloha }:
                Hlavny.OtvorUlohy(uloha);
                break;
            case { Zdroj: ZdrojKalendara.Osnova, Id: { } predmet }:
                Hlavny.OtvorObjekt(predmet);
                break;
            case { Zdroj: ZdrojKalendara.Archiv }:
                Hlavny.OtvorArchiv();
                break;
            case { } automaticky:
                VybranyDen = VsetkyDni().FirstOrDefault(d => d.Datum == automaticky.Zaciatok.Date) ?? VybranyDen;
                break;
        }
    }

    [RelayCommand]
    private void PrepnutHotovo(VyskytVKalendari? v)
    {
        if (v is { Zdroj: ZdrojKalendara.Uloha, Id: { } uloha })
        {
            new UlohyService().NastavHotovo(uloha, !v.JeVybavena);
        }
        else if (v is { Zdroj: ZdrojKalendara.Udalost, Id: { } udalost })
        {
            _sluzba.NastavStav(udalost, v.JeVybavena ? StavUdalosti.Naplanovane : StavUdalosti.Hotovo);
        }
        else
        {
            return;
        }

        Obnov();
        Hlavny.ObnovPlanovanie();
    }

    [RelayCommand]
    private void OtvoritZdroj(VyskytVKalendari? v)
    {
        if (v?.Odkaz is { Length: > 0 } odkaz)
        {
            Dialogy.OtvorVSysteme(odkaz);
        }
    }

    /// <summary>Automatický termín ako vlastná udalosť (napr. s pripomienkou alebo s naviazaním na predmety).</summary>
    [RelayCommand]
    private void PrevziatDoKalendara(VyskytVKalendari? v)
    {
        if (v is null || !v.JeAutomaticky)
        {
            return;
        }

        var typ = _sluzba.Typy(lenAktivne: true).FirstOrDefault(t => string.Equals(t.Nazov, v.Typ, StringComparison.CurrentCultureIgnoreCase));
        var model = new UdalostEditorVM(Hlavny, null, v.Zaciatok)
        {
            Nazov = v.Nazov,
            Popis = string.Join("\n\n", new[] { v.Popis ?? "", v.Odkaz ?? "" }.Where(t => t.Length > 0)),
            CelyDen = true,
            Koniec = v.ViacDni ? v.PoslednyDen : null,
            Pripomienka = PripomienkaUdalosti.TyzdenVopred,
        };
        if (typ is not null)
        {
            model.Typ = model.Typy.FirstOrDefault(t => t.Id == typ.Id) ?? model.Typ;
        }

        if (Views.UdalostWindow.Otvor(model))
        {
            Obnov();
            Hlavny.ObnovPlanovanie();
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
            if (Views.PoznamkaWindow.Otvor(new PoznamkaEditorVM(Hlavny, poznamka.Id)))
            {
                Obnov();
                Hlavny.ObnovPlanovanie();
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    private void Typy()
    {
        new Views.TypyUdalostiWindow { Owner = Dialogy.Vlastnik }.ShowDialog();
        Obnov();
    }

    // ------------------------------------------------------------------ export a tlač

    /// <summary>Obdobie aktuálneho zobrazenia (na tlač a export).</summary>
    public (DateTime Od, DateTime Do) Obdobie()
    {
        var prvy = new DateTime(AktualnyDatum.Year, AktualnyDatum.Month, 1);
        var zaciatokRoka = ArchivSkolskehoRoka.Obdobie(ArchivSkolskehoRoka.Aktualny(AktualnyDatum)).Od;
        return Rezim switch
        {
            RezimKalendara.Mesiac => (prvy, prvy.AddMonths(1).AddDays(-1)),
            RezimKalendara.Tyzden => (Pondelok(AktualnyDatum), Pondelok(AktualnyDatum).AddDays(6)),
            RezimKalendara.Zoznam => (AktualnyDatum.Date, AktualnyDatum.Date.AddDays(DniZoznamu - 1)),
            _ => (zaciatokRoka, zaciatokRoka.AddYears(1).AddDays(-1)),
        };
    }

    /// <summary>Export do Outlooku a Google Kalendára: od začiatku zobrazeného obdobia na rok dopredu.</summary>
    [RelayCommand]
    private void ExportIcs()
    {
        var (od, _) = Obdobie();
        var cesta = Dialogy.UlozitAko("Export kalendára pre Outlook alebo Google Kalendár", "Kalendár iCalendar (*.ics)|*.ics", $"SkVP kalendar {od:yyyy-MM}.ics");
        if (cesta is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(cesta, _sluzba.Ics(od, od.AddYears(1).AddDays(-1), automaticke: true), new UTF8Encoding(false));
            Hlavny.Oznam($"Kalendár na rok od {Planovanie.DatumText(od)} je uložený: {Path.GetFileName(cesta)} – otvorí ho Outlook aj Google Kalendár (Import).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogy.Chyba("Kalendár sa nepodarilo uložiť.", ex);
        }
    }

    [RelayCommand]
    private void Tlacit() => TlacPlanovania.Kalendar(this, pdf: false);

    [RelayCommand]
    private void UlozitPdf() => TlacPlanovania.Kalendar(this, pdf: true);

    /// <summary>Služba s rovnakým nastavením zobrazenia (tlač).</summary>
    public KalendarService Sluzba => _sluzba;

    /// <summary>Dni zobrazeného obdobia (tlač).</summary>
    public List<DenKalendara> DniObdobia(DateTime od, DateTime po) => Dni(od, po, null);
}
