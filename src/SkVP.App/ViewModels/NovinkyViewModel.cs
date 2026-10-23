using System.Globalization;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Zdroj informácie s výsledkom posledného overenia.</summary>
public sealed class ZdrojNovinkyVM
{
    public ZdrojNovinkyVM(ZdrojNovinky zdroj, VysledokOverenia? overenie)
    {
        Zdroj = zdroj;
        Overenie = overenie;
    }

    public ZdrojNovinky Zdroj { get; }
    public VysledokOverenia? Overenie { get; }
    public string Nazov => Zdroj.Nazov;
    public string Url => Zdroj.Url;
    public string Domena => Zdroj.Domena;
    public bool MaOverovanyUdaj => Zdroj.Overit is { Count: > 0 };

    public string Ikona => Overenie?.Stav switch
    {
        StavOverenia.Potvrdene => "\uE73E",
        StavOverenia.Dostupne => "\uE8A7",
        StavOverenia.Nenajdene => "\uE7BA",
        StavOverenia.Nedostupne => "\uE711",
        _ => "\uE71B",
    };

    public string Farba => Overenie?.Stav switch
    {
        StavOverenia.Potvrdene => "#16A34A",
        StavOverenia.Dostupne => "#0284C7",
        StavOverenia.Nenajdene => "#D97706",
        StavOverenia.Nedostupne => "#DC2626",
        _ => "#64748B",
    };

    public string Popis => Overenie is null
        ? $"{Nazov}\n{Url}\nOverené pri príprave údajov; pre kontrolu na stránke použite „Overiť na zdrojoch“."
        : $"{Nazov}\n{Url}\n{Overenie.Sprava}";
}

/// <summary>Jedna informácia sekcie Novinky v zozname.</summary>
public sealed class PolozkaNovinkyVM
{
    private static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");

    public PolozkaNovinkyVM(PolozkaNoviniek polozka, KategoriaNoviniek kategoria, List<VysledokOverenia>? overenie, DateOnly dnes)
    {
        Polozka = polozka;
        Kategoria = kategoria;
        Zdroje = polozka.Zdroje.Select(z => new ZdrojNovinkyVM(z, overenie?.FirstOrDefault(o => o.Url == z.Url))).ToList();
        Odpocet = NovinkyService.Odpocet(polozka, dnes);
        // termín je minulý po poslednom dni; novinka s dátumom účinnosti (len „od“) neuplynie
        JeMinule = polozka.Druh == NovinkyService.DruhTermin ? polozka.Koniec is { } k && k < dnes : polozka.DatumDo is { } d && d < dnes;
        JeBlizko = !JeMinule && polozka.Zaciatok is { } z && z.DayNumber - dnes.DayNumber <= 14;
        Prebieha = !JeMinule && polozka.Zaciatok is { } zz && zz <= dnes && polozka.DatumOd is not null;
    }

    public PolozkaNoviniek Polozka { get; }
    public KategoriaNoviniek Kategoria { get; }
    public List<ZdrojNovinkyVM> Zdroje { get; }
    public string Nazov => Polozka.Nazov;
    public string Text => Polozka.Text;
    public string Farba => Kategoria.Farba;
    public string KategoriaNazov => Kategoria.Nazov;
    public string Ikona => Kategoria.Ikona;
    public bool Dolezite => Polozka.Dolezite;
    public string? Poznamka => Polozka.Poznamka;
    public bool MaPoznamku => !string.IsNullOrWhiteSpace(Polozka.Poznamka);
    public string? Skupina => Polozka.Skupina;
    public bool MaSkupinu => !string.IsNullOrWhiteSpace(Polozka.Skupina);
    public string TerminText => Polozka.TerminText;
    public bool MaTermin => TerminText.Length > 0;
    public string Odpocet { get; }
    public bool MaOdpocet => Odpocet.Length > 0 && Polozka.Druh == NovinkyService.DruhTermin;
    public bool JeMinule { get; }
    public bool JeBlizko { get; }
    public bool Prebieha { get; }

    public string DruhText => Polozka.Druh switch
    {
        NovinkyService.DruhTermin => "Termín",
        NovinkyService.DruhOdporucanie => "Odporúčanie",
        _ => "Novinka",
    };

    /// <summary>Farba štítka druhu – termíny, novinky a odporúčania sa farebne odlišujú.</summary>
    public string DruhFarba => Polozka.Druh switch
    {
        NovinkyService.DruhTermin => "#D97706",
        NovinkyService.DruhOdporucanie => "#059669",
        _ => "#2563EB",
    };

    public string DruhIkona => Polozka.Druh switch
    {
        NovinkyService.DruhTermin => "\uE787",
        NovinkyService.DruhOdporucanie => "\uE82F",
        _ => "\uE789",
    };

    public int PocetPotvrdenych => Zdroje.Count(z => z.Overenie?.Stav == StavOverenia.Potvrdene);

    /// <summary>„2 oficiálne zdroje · overené 23. 10. 2026“.</summary>
    public string ZdrojeText
    {
        get
        {
            var pocet = Zdroje.Count;
            var text = pocet switch
            {
                1 => "1 oficiálny zdroj",
                <= 4 => $"{pocet} oficiálne zdroje",
                _ => $"{pocet} oficiálnych zdrojov",
            };
            if (NovinkyService.Datum(Polozka.Overene) is { } d)
            {
                text += $" · overené {NovinkyService.Datum(d)}";
            }

            return text;
        }
    }

    /// <summary>Deň a mesiac pre termínovník („9.“ a „mar“).</summary>
    public string Den => Polozka.Zaciatok is { } z ? (Polozka.DatumOd is null ? "do " : "") + $"{z.Day}." : "";
    public string MesiacSkratka => Polozka.Zaciatok is { } z ? z.ToString("MMM", Sk).TrimEnd('.') : "";
    public string DenVTyzdni => Polozka.Zaciatok is { } z ? z.ToString("dddd", Sk) : "";

    /// <summary>Text na hľadanie (bez diakritiky).</summary>
    public string Hladany => ImportOsnov.BezDiakritiky($"{Nazov} {Text} {Poznamka} {KategoriaNazov} {Skupina} {TerminText}").ToLowerInvariant();
}

/// <summary>Blok jednej témy (farebná karta so zoznamom informácií).</summary>
public sealed class BlokNoviniekVM
{
    public BlokNoviniekVM(KategoriaNoviniek kategoria, List<PolozkaNovinkyVM> polozky)
    {
        Kategoria = kategoria;
        Polozky = polozky;
    }

    public KategoriaNoviniek Kategoria { get; }
    public List<PolozkaNovinkyVM> Polozky { get; }
    public string Nazov => Kategoria.Nazov;
    public string Popis => Kategoria.Popis;
    public string Farba => Kategoria.Farba;
    public string Ikona => Kategoria.Ikona;
    public int Pocet => Polozky.Count;
    public string PocetText => Pocet switch { 1 => "1 informácia", <= 4 => $"{Pocet} informácie", _ => $"{Pocet} informácií" };
}

/// <summary>Mesiac v termínovníku.</summary>
public sealed record MesiacTerminovnikaVM(string Nazov, List<PolozkaNovinkyVM> Polozky);

/// <summary>Filter témy (farebný prepínač).</summary>
public sealed partial class FilterTemyVM : ObservableObject
{
    public FilterTemyVM(string? id, string nazov, string farba, string ikona, int pocet)
    {
        Id = id;
        Nazov = nazov;
        Farba = farba;
        Ikona = ikona;
        Pocet = pocet;
    }

    public string? Id { get; }
    public string Nazov { get; }
    public string Farba { get; }
    public string Ikona { get; }
    public int Pocet { get; }

    [ObservableProperty]
    private bool _vybrany;
}

/// <summary>Správa z RSS kanála oficiálnej stránky.</summary>
public sealed class SpravaVM
{
    public SpravaVM(SpravaZoZdroja sprava, KategoriaNoviniek? kategoria)
    {
        Sprava = sprava;
        Kategoria = kategoria;
    }

    public SpravaZoZdroja Sprava { get; }
    public KategoriaNoviniek? Kategoria { get; }
    public string Titulok => Sprava.Titulok;
    public string Popis => Sprava.Popis;
    public bool MaPopis => Sprava.Popis.Length > 0;
    public string Odkaz => Sprava.Odkaz;
    public string Zdroj => Sprava.Zdroj;
    public string Farba => Kategoria?.Farba ?? "#64748B";
    public string KategoriaNazov => Kategoria?.Nazov ?? "";
    public string DatumText => Sprava.Datum is { } d ? NovinkyService.Datum(DateOnly.FromDateTime(d)) : "";
    public bool JePotvrdena => Sprava.PotvrdeneInde.Count > 0;
    public string PotvrdeneText => JePotvrdena ? "Téma potvrdená aj na: " + string.Join(", ", Sprava.PotvrdeneInde) : "";
    public string Hladany => ImportOsnov.BezDiakritiky($"{Titulok} {Popis} {Zdroj}").ToLowerInvariant();
}

/// <summary>
/// Sekcia Novinky: overené informácie z oficiálnych stránok (učebné osnovy a ŠVP, tematické plány, cieľové požiadavky,
/// maturita, prijímacie skúšky, prázdniny a školský rok), termínovník, aktuálne správy z RSS kanálov ministerstva a NIVaM
/// a zoznam odporúčaných stránok.
/// </summary>
public sealed partial class NovinkyViewModel : ObservableObject
{
    public const string VsetkyKraje = "Všetky kraje";

    private DataNoviniek _data;
    private OverenieNoviniek? _overenie;
    private SpravyZoZdrojov? _spravy;
    private List<PolozkaNovinkyVM> _vsetky = new();
    private bool _nacitane;

    public NovinkyViewModel(MainViewModel hlavny)
    {
        Hlavny = hlavny;
        _data = NovinkyService.Nacitaj();
        _overenie = NovinkyService.UlozeneOverenie();
        _spravy = NovinkyService.UlozeneSpravy();
        Kraje = new[] { VsetkyKraje }.Concat(_data.Polozky.Select(p => p.Skupina).OfType<string>().Distinct().Order()).ToList();
        var kraj = Databaza.Citaj(NovinkyService.KlucKraj, VsetkyKraje);
        _vybranyKraj = Kraje.Contains(kraj) ? kraj : VsetkyKraje;
        Obnov();
        _nacitane = true;

        if (!Dialogy.TichyRezim && NovinkyService.TrebaObnovitSpravy(_spravy))
        {
            _ = StiahniSpravyAsync(tichy: true);
        }
    }

    public MainViewModel Hlavny { get; }

    public DateOnly Dnes { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string Podnadpis => $"Školský rok {_data.SkolskyRok} · overené údaje k {(_data.DatumAktualizacie is { } d ? NovinkyService.Datum(d) : "–")} · " +
                               "čerpané výlučne z oficiálnych stránok ministerstva školstva a jeho organizácií (nie z médií)";

    public List<FilterTemyVM> Temy { get; private set; } = new();
    public List<BlokNoviniekVM> Bloky { get; private set; } = new();
    public List<PolozkaNovinkyVM> Najblizsie { get; private set; } = new();
    public List<MesiacTerminovnikaVM> Terminovnik { get; private set; } = new();
    public List<SpravaVM> Spravy { get; private set; } = new();
    public List<OdporucanaStranka> Stranky => _data.Stranky;
    public List<string> Kraje { get; }

    public bool NicSaNenaslo => Bloky.Count == 0;
    public bool ZiadneSpravy => Spravy.Count == 0;
    public int PocetPoloziek => _data.Polozky.Count;

    public string SpravyStav => _spravy is null
        ? "Správy zo stránok ministerstva a NIVaM sa zatiaľ nestiahli – kliknite na „Aktualizovať“."
        : $"Stiahnuté {_spravy.Stiahnute.ToString("d. M. yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))} · {Spravy.Count} správ k vzdelávaciemu obsahu" +
          (_spravy.Nerelevantnych > 0 ? $" (ďalších {_spravy.Nerelevantnych} správ o iných témach je vynechaných)" : "") +
          (_spravy.Chyby.Count > 0 ? " · " + string.Join(", ", _spravy.Chyby) : "");

    public string OverenieStav
    {
        get
        {
            if (_overenie is null)
            {
                return "Údaje boli overené pri príprave na uvedených oficiálnych stránkach. Tlačidlom „Overiť na zdrojoch“ program skontroluje, či ich stránky stále uvádzajú.";
            }

            var vsetky = _overenie.Polozky.Values.SelectMany(v => v).ToList();
            return $"Overené na zdrojoch {_overenie.Cas.ToString("d. M. yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))}: " +
                   $"{vsetky.Count(v => v.Stav == StavOverenia.Potvrdene)} údajov potvrdených, {vsetky.Count(v => v.Stav == StavOverenia.Dostupne)} dokumentov dostupných" +
                   (vsetky.Count(v => v.Stav == StavOverenia.Nenajdene) is > 0 and var n ? $", {n} na kontrolu (údaj sa mohol zmeniť)" : "") +
                   (vsetky.Count(v => v.Stav == StavOverenia.Nedostupne) is > 0 and var m ? $", {m} nedostupných" : "") + ".";
        }
    }

    public bool MaNaKontrolu => _overenie?.Polozky.Values.Any(v => v.Any(o => o.Stav == StavOverenia.Nenajdene)) == true;

    [ObservableProperty]
    private string _hladat = "";

    [ObservableProperty]
    private bool _ajUplynule;

    [ObservableProperty]
    private string _vybranyKraj;

    [ObservableProperty]
    private string? _vybranaTema;

    [ObservableProperty]
    private bool _pracuje;

    [ObservableProperty]
    private string _stavPrace = "";

    partial void OnHladatChanged(string value) => Obnov();
    partial void OnAjUplynuleChanged(bool value) => Obnov();
    partial void OnVybranaTemaChanged(string? value) => Obnov();

    partial void OnVybranyKrajChanged(string value)
    {
        if (_nacitane)
        {
            Databaza.Zapis(NovinkyService.KlucKraj, value);
        }

        Obnov();
    }

    [RelayCommand]
    private void VybratTemu(string? id) => VybranaTema = VybranaTema == id ? null : id;

    [RelayCommand]
    private void OtvoritOdkaz(string? adresa)
    {
        // otvárajú sa iba oficiálne stránky (zoznam domén v NovinkyService)
        if (NovinkyService.JeOficialna(adresa))
        {
            Dialogy.OtvorVSysteme(adresa!);
        }
    }

    /// <summary>Stiahne novšie overené údaje z repozitára programu a aktuálne správy z RSS kanálov.</summary>
    [RelayCommand]
    private async Task Aktualizovat()
    {
        if (Pracuje)
        {
            return;
        }

        Pracuje = true;
        var sprava = new List<string>();
        try
        {
            StavPrace = "Hľadám novšie overené údaje…";
            try
            {
                if (await NovinkyService.AktualizujUdajeAsync())
                {
                    _data = NovinkyService.Nacitaj();
                    sprava.Add($"Overené údaje sú aktualizované (stav k {NovinkyService.Datum(_data.DatumAktualizacie!.Value)}).");
                }
                else
                {
                    sprava.Add("Overené údaje sú aktuálne.");
                }
            }
            catch (SkvpException ex)
            {
                sprava.Add(ex.Message);
            }

            StavPrace = "Sťahujem správy z oficiálnych stránok…";
            await StiahniSpravyAsync(tichy: true);
            sprava.Add($"Správy: {Spravy.Count} k vzdelávaciemu obsahu.");
        }
        finally
        {
            Pracuje = false;
            StavPrace = "";
            Obnov();
        }

        Hlavny.Oznam(string.Join(" ", sprava));
    }

    /// <summary>Načíta zdrojové stránky a skontroluje, či na nich údaje stále sú.</summary>
    [RelayCommand]
    private async Task OveritZdroje()
    {
        if (Pracuje)
        {
            return;
        }

        Pracuje = true;
        try
        {
            _overenie = await NovinkyService.OverAsync(_data, new Progress<string>(t => StavPrace = t));
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Overenie na zdrojoch sa nepodarilo.", ex);
        }
        finally
        {
            Pracuje = false;
            StavPrace = "";
            Obnov();
        }

        Hlavny.Oznam(OverenieStav);
    }

    /// <summary>Uloží termíny do kalendára (.ics – Outlook, Google Kalendár, kalendár Windows).</summary>
    [RelayCommand]
    private void UlozitDoKalendara()
    {
        var terminy = _vsetky.Where(p => p.Polozka.Druh == NovinkyService.DruhTermin && !p.JeMinule).ToList();
        var cesta = Dialogy.UlozitAko("Uložiť termíny do kalendára", "Kalendár iCalendar (*.ics)|*.ics", $"Terminy {_data.SkolskyRok.Replace('/', '-')}.ics");
        if (cesta is null)
        {
            return;
        }

        File.WriteAllText(cesta, Kalendar(terminy.Select(t => t.Polozka), _data), new UTF8Encoding(false));
        Hlavny.Oznam($"Uložených {terminy.Count} termínov do kalendára: {Path.GetFileName(cesta)}");
    }

    /// <summary>Termíny vo formáte iCalendar (celodenné udalosti, posledný deň vrátane).</summary>
    public static string Kalendar(IEnumerable<PolozkaNoviniek> terminy, DataNoviniek data)
    {
        static string Esc(string t) => t.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r", "").Replace("\n", "\\n");
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//SkVP//Novinky//SK\r\nCALSCALE:GREGORIAN\r\n");
        var teraz = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        foreach (var t in terminy)
        {
            if (t.Zaciatok is not { } od || t.Koniec is not { } dokedy)
            {
                continue;
            }

            var kategoria = data.Kategoria(t.Kategoria)?.Nazov ?? "";
            var popis = t.Text + (t.Poznamka is { Length: > 0 } p ? "\n\nPoznámka: " + p : "") + "\n\nZdroje: " + string.Join("; ", t.Zdroje.Select(z => z.Url));
            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append($"UID:{t.Id}@skvp-novinky\r\nDTSTAMP:{teraz}\r\n");
            sb.Append($"DTSTART;VALUE=DATE:{od:yyyyMMdd}\r\nDTEND;VALUE=DATE:{dokedy.AddDays(1):yyyyMMdd}\r\n");
            sb.Append($"SUMMARY:{Esc(t.Nazov)}\r\nCATEGORIES:{Esc(kategoria)}\r\nDESCRIPTION:{Esc(popis)}\r\n");
            if (t.Zdroje.FirstOrDefault() is { } zdroj)
            {
                sb.Append($"URL:{zdroj.Url}\r\n");
            }

            sb.Append("END:VEVENT\r\n");
        }

        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    private async Task StiahniSpravyAsync(bool tichy)
    {
        try
        {
            _spravy = await NovinkyService.StiahniSpravyAsync(_data);
        }
        catch (Exception ex) when (tichy)
        {
            _ = ex; // správy sa skúsia stiahnuť nabudúce
        }

        Obnov();
    }

    /// <summary>Zostaví bloky, termínovník a správy podľa filtrov.</summary>
    public void Obnov()
    {
        var hladane = ImportOsnov.BezDiakritiky(Hladat.Trim()).ToLowerInvariant();
        var predvolenaKategoria = new KategoriaNoviniek { Id = "", Nazov = "Ostatné" };
        _vsetky = _data.Polozky
            .Select(p => new PolozkaNovinkyVM(p, _data.Kategoria(p.Kategoria) ?? predvolenaKategoria,
                _overenie?.Polozky.TryGetValue(p.Id, out var o) == true ? o : null, Dnes))
            .ToList();

        bool PodlaKraja(PolozkaNovinkyVM p) => VybranyKraj == VsetkyKraje || !p.MaSkupinu || p.Skupina == VybranyKraj;
        bool PodlaHladania(PolozkaNovinkyVM p) => hladane.Length == 0 || p.Hladany.Contains(hladane, StringComparison.Ordinal);

        var viditelne = _vsetky.Where(p => PodlaKraja(p) && PodlaHladania(p) && (AjUplynule || !p.JeMinule)).ToList();

        Temy = new[] { new FilterTemyVM(null, "Všetky témy", "#475569", "\uE80F", viditelne.Count) { Vybrany = VybranaTema is null } }
            .Concat(_data.Kategorie.Select(k => new FilterTemyVM(k.Id, k.Nazov, k.Farba, k.Ikona, viditelne.Count(p => p.Polozka.Kategoria == k.Id))
                { Vybrany = VybranaTema == k.Id }))
            .ToList();

        var podlaTemy = viditelne.Where(p => VybranaTema is null || p.Polozka.Kategoria == VybranaTema).ToList();

        // v bloku: najprv dôležité novinky, potom termíny podľa dátumu, odporúčania na koniec
        Bloky = _data.Kategorie
            .Select(k => new BlokNoviniekVM(k, podlaTemy.Where(p => p.Polozka.Kategoria == k.Id)
                .OrderBy(p => p.Polozka.Druh == NovinkyService.DruhOdporucanie ? 2 : p.Polozka.Druh == NovinkyService.DruhNovinka ? 0 : 1)
                .ThenByDescending(p => p.Polozka.Druh == NovinkyService.DruhNovinka && p.Dolezite)
                .ThenBy(p => p.Polozka.Zaciatok ?? DateOnly.MaxValue)
                .ToList()))
            .Where(b => b.Pocet > 0)
            .ToList();

        Najblizsie = _vsetky.Where(p => p.Polozka.Druh == NovinkyService.DruhTermin && !p.JeMinule && PodlaKraja(p) && p.Dolezite)
            .OrderBy(p => p.Polozka.Zaciatok).Take(6).ToList();

        var sk = CultureInfo.GetCultureInfo("sk-SK");
        Terminovnik = podlaTemy.Where(p => p.Polozka.Druh == NovinkyService.DruhTermin && p.Polozka.Zaciatok is not null)
            .OrderBy(p => p.Polozka.Zaciatok).ThenBy(p => p.Polozka.Koniec)
            .GroupBy(p => (p.Polozka.Zaciatok!.Value.Year, p.Polozka.Zaciatok!.Value.Month))
            .Select(g => new MesiacTerminovnikaVM(
                PocasieService.VelkePrve(new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy", sk)), g.ToList()))
            .ToList();

        Spravy = (_spravy?.Spravy ?? new List<SpravaZoZdroja>())
            .Select(s => new SpravaVM(s, s.Kategoria is null ? null : _data.Kategoria(s.Kategoria)))
            .Where(s => (VybranaTema is null || s.Sprava.Kategoria == VybranaTema) && (hladane.Length == 0 || s.Hladany.Contains(hladane, StringComparison.Ordinal)))
            .ToList();

        OnPropertyChanged(nameof(Temy));
        OnPropertyChanged(nameof(Bloky));
        OnPropertyChanged(nameof(Najblizsie));
        OnPropertyChanged(nameof(Terminovnik));
        OnPropertyChanged(nameof(Spravy));
        OnPropertyChanged(nameof(NicSaNenaslo));
        OnPropertyChanged(nameof(ZiadneSpravy));
        OnPropertyChanged(nameof(SpravyStav));
        OnPropertyChanged(nameof(OverenieStav));
        OnPropertyChanged(nameof(MaNaKontrolu));
        OnPropertyChanged(nameof(Podnadpis));
        OnPropertyChanged(nameof(Stranky));
    }
}
