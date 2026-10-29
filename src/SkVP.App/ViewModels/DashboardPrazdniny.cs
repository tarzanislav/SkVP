using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SkVP.Data;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Riadok tabuľky prázdnin na dashboarde.</summary>
public sealed class RiadokPrazdninVM
{
    private static readonly CultureInfo Sk = CultureInfo.GetCultureInfo("sk-SK");

    public RiadokPrazdninVM(PolozkaNoviniek polozka, DateOnly dnes, bool najblizsi)
    {
        Polozka = polozka;
        var druh = polozka.Nazov.Split(' ')[0];
        Nazov = druh;
        Farba = druh switch
        {
            "Jesenné" => "#F2C38B",
            "Vianočné" => "#9FC3E7",
            "Polročné" => "#C9B6E4",
            "Jarné" => "#A8D8A8",
            "Veľkonočné" => "#F4B6C2",
            "Letné" => "#F7D774",
            _ => "#B8C4D6",
        };
        Kraje = polozka.Skupina ?? "";
        Posledny = DenText(NovinkyService.Datum(polozka.Posledny));
        Nastup = DenText(NovinkyService.Datum(polozka.Nastup));
        Termin = polozka.TerminText;
        Odpocet = NovinkyService.Odpocet(polozka, dnes);
        JeMinule = polozka.Koniec < dnes;
        Prebieha = !JeMinule && polozka.Zaciatok <= dnes;
        JeNajblizsi = najblizsi;
    }

    public PolozkaNoviniek Polozka { get; }
    public string Nazov { get; }
    public string Kraje { get; }
    public bool MaKraje => Kraje.Length > 0;
    public string Posledny { get; }
    public string Termin { get; }
    public string Nastup { get; }
    public string Odpocet { get; }
    public string Farba { get; }
    public bool JeMinule { get; }
    public bool Prebieha { get; }
    public bool JeNajblizsi { get; }

    /// <summary>Zvýraznený riadok – práve prebiehajúce alebo najbližšie prázdniny.</summary>
    public bool Zvyrazneny => Prebieha || JeNajblizsi;

    private static string DenText(DateOnly? datum) =>
        datum is { } d ? $"{NovinkyService.Datum(d)} ({d.ToString("ddd", Sk).TrimEnd('.')})" : "";
}

/// <summary>Dashboard – tabuľka prázdnin a organizácia školského roka (téma presunutá zo sekcie Novinky).</summary>
public sealed partial class DashboardViewModel
{
    public const string KlucPrazdniny = "dashboard.prazdniny";

    private List<PolozkaNoviniek> _skolskyRok = new();

    [ObservableProperty]
    private bool _ukazPrazdniny = true;

    partial void OnUkazPrazdninyChanged(bool value) => UlozPanel(KlucPrazdniny, value);

    public List<string> SkolskeRoky { get; private set; } = new();
    public List<string> KrajePrazdnin { get; private set; } = new();

    [ObservableProperty] private string _vybranyRok = "";
    [ObservableProperty] private string _vybranyKrajPrazdnin = NovinkyService.VsetkyKraje;
    [ObservableProperty] private List<RiadokPrazdninVM> _riadkyPrazdnin = new();
    [ObservableProperty] private string _obdobieVyucovania = "";

    public string ZdrojPrazdnin => "Zdroj: Pedagogicko-organizačné pokyny MŠVVaM SR a termíny školských prázdnin na minedu.sk (údaje sekcie Novinky)";

    partial void OnVybranyRokChanged(string value) => ZostavPrazdniny();

    partial void OnVybranyKrajPrazdninChanged(string value)
    {
        if (_panelyPripravene)
        {
            Databaza.Zapis(NovinkyService.KlucKraj, value);
        }

        ZostavPrazdniny();
    }

    /// <summary>Školský rok, do ktorého dátum patrí (september – august).</summary>
    public static string SkolskyRok(DateOnly datum)
    {
        var rok = datum.Month >= 9 ? datum.Year : datum.Year - 1;
        return $"{rok}/{rok + 1}";
    }

    private void PripravPrazdniny()
    {
        UkazPrazdniny = Databaza.Citaj(KlucPrazdniny, "1") != "0";
        try
        {
            _skolskyRok = NovinkyService.Nacitaj().Polozky.Where(p => p.Kategoria == NovinkyService.TemaSkolskyRok).ToList();
        }
        catch (Exception)
        {
            _skolskyRok = new List<PolozkaNoviniek>();
        }

        var prazdniny = _skolskyRok.Where(JePrazdniny).ToList();
        SkolskeRoky = prazdniny.Select(p => SkolskyRok(p.Zaciatok!.Value)).Distinct().Order().ToList();
        KrajePrazdnin = new[] { NovinkyService.VsetkyKraje }.Concat(prazdniny.Select(p => p.Skupina).OfType<string>().Distinct().Order()).ToList();
        var kraj = Databaza.Citaj(NovinkyService.KlucKraj, NovinkyService.VsetkyKraje);
        _vybranyKrajPrazdnin = KrajePrazdnin.Contains(kraj) ? kraj : NovinkyService.VsetkyKraje;
        var terajsi = SkolskyRok(DnesPrazdnin);
        _vybranyRok = SkolskeRoky.Contains(terajsi) ? terajsi : SkolskeRoky.FirstOrDefault() ?? "";
        OnPropertyChanged(nameof(SkolskeRoky));
        OnPropertyChanged(nameof(KrajePrazdnin));
        OnPropertyChanged(nameof(VybranyRok));
        OnPropertyChanged(nameof(VybranyKrajPrazdnin));
        ZostavPrazdniny();
    }

    /// <summary>Dátum, ku ktorému sa tabuľka zostavuje (samotest ho nastaví).</summary>
    public DateOnly DnesPrazdnin { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    private static bool JePrazdniny(PolozkaNoviniek p) =>
        p.Druh == NovinkyService.DruhTermin && p.Zaciatok is not null && p.Nazov.Contains("prázdniny", StringComparison.OrdinalIgnoreCase);

    public void ZostavPrazdniny()
    {
        var dnes = DnesPrazdnin;
        var vRoku = _skolskyRok.Where(p => JePrazdniny(p) && SkolskyRok(p.Zaciatok!.Value) == VybranyRok
                                           && (VybranyKrajPrazdnin == NovinkyService.VsetkyKraje || p.Skupina is null || p.Skupina == VybranyKrajPrazdnin))
            .OrderBy(p => p.Zaciatok).ThenBy(p => p.Skupina).ToList();
        var najblizsi = vRoku.FirstOrDefault(p => p.Zaciatok > dnes);
        RiadkyPrazdnin = vRoku.Select(p => new RiadokPrazdninVM(p, dnes, p == najblizsi && !vRoku.Any(x => x.Zaciatok <= dnes && x.Koniec >= dnes))).ToList();

        var zaciatok = _skolskyRok.FirstOrDefault(p => p.Id.StartsWith("sr-zaciatok", StringComparison.Ordinal) && p.Zaciatok is { } z && SkolskyRok(z) == VybranyRok);
        var koniec = _skolskyRok.FirstOrDefault(p => p.Id.StartsWith("sr-koniec", StringComparison.Ordinal) && p.Zaciatok is { } z && SkolskyRok(z) == VybranyRok);
        var polrok = _skolskyRok.FirstOrDefault(p => p.Id.StartsWith("sr-polrok", StringComparison.Ordinal) && p.Zaciatok is { } z && SkolskyRok(z) == VybranyRok);
        ObdobieVyucovania = zaciatok is not null && koniec is not null
            ? $"Vyučovanie: {zaciatok.TerminText} – {koniec.TerminText}" + (polrok is not null ? $"  ·  prvý polrok sa končí {polrok.TerminText}" : "")
            : "Obdobie vyučovania určí ministerstvo v pedagogicko-organizačných pokynoch na tento školský rok.";
    }
}
