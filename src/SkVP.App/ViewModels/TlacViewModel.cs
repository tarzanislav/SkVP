using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MigraDoc.Rendering;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Oblasť s voľbou, či patrí do zostavy.</summary>
public sealed partial class VolbaOblasti : ObservableObject
{
    public VolbaOblasti(OblastVM oblast, bool zahrnut)
    {
        Oblast = oblast;
        _zahrnut = zahrnut;
    }

    public OblastVM Oblast { get; }

    [ObservableProperty]
    private bool _zahrnut;
}

/// <summary>Jedna strana náhľadu – vykreslí sa, až keď je na obrazovke.</summary>
public sealed class StranaNahladu(DocumentRenderer vykreslenie, int cislo)
{
    private ImageSource? _obrazok;

    public int Cislo => cislo;

    public ImageSource Obrazok => _obrazok ??= NahladDokumentu.Obrazok(vykreslenie, cislo);
}

/// <summary>Tlač a zostavy: spojenie osnov (všetky, jedna alebo vybrané oblasti) do jedného dokumentu.</summary>
public sealed partial class TlacViewModel : ObservableObject
{
    private readonly DispatcherTimer _odklad = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private DocumentRenderer? _vykreslenie;
    private CelyDokument? _dokument;
    private bool _nacitane;

    public TlacViewModel(MainViewModel hlavny, OblastVM? oblast)
    {
        Hlavny = hlavny;
        foreach (var o in hlavny.Oblasti)
        {
            var volba = new VolbaOblasti(o, oblast is null || o == oblast);
            volba.PropertyChanged += (_, _) => Zmena();
            Oblasti.Add(volba);
        }

        _len = oblast is not null;
        _titulnaStrana = oblast is null;
        _obsah = oblast is null;
        _odklad.Tick += (_, _) =>
        {
            _odklad.Stop();
            Obnovit();
        };
        _nacitane = true;
    }

    public MainViewModel Hlavny { get; }

    public ObservableCollection<VolbaOblasti> Oblasti { get; } = new();

    public ObservableCollection<StranaNahladu> Strany { get; } = new();

    /// <summary>true = len vybrané oblasti, false = všetky.</summary>
    [ObservableProperty]
    private bool _len;

    [ObservableProperty]
    private bool _titulnaStrana;

    [ObservableProperty]
    private bool _obsah;

    [ObservableProperty]
    private bool _oddelovacOblasti = true;

    [ObservableProperty]
    private bool _oddelovacPredmetu;

    [ObservableProperty]
    private bool _charakteristiky = true;

    [ObservableProperty]
    private bool _nespracovane = true;

    [ObservableProperty]
    private bool _otvoritPoUlozeni = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SirkaStrany), nameof(VyskaStrany))]
    private double _zvacsenie = 0.75;

    /// <summary>Rozmer strany A4 v náhľade.</summary>
    public double SirkaStrany => 793.7 * Zvacsenie;
    public double VyskaStrany => 1122.5 * Zvacsenie;

    [ObservableProperty]
    private string _popis = "";

    public bool MaLogo => NastaveniaSkoly.CestaLoga() is not null;

    partial void OnLenChanged(bool value) => Zmena();
    partial void OnTitulnaStranaChanged(bool value) => Zmena();
    partial void OnObsahChanged(bool value) => Zmena();
    partial void OnOddelovacOblastiChanged(bool value) => Zmena();
    partial void OnOddelovacPredmetuChanged(bool value) => Zmena();
    partial void OnCharakteristikyChanged(bool value) => Zmena();
    partial void OnNespracovaneChanged(bool value) => Zmena();

    private void Zmena()
    {
        if (!_nacitane)
        {
            return;
        }

        _odklad.Stop();
        _odklad.Start();
    }

    public VolbyZostavy Volby() => new()
    {
        Oblasti = Len ? Oblasti.Where(o => o.Zahrnut).Select(o => o.Oblast.Id).ToList() : null,
        TitulnaStrana = TitulnaStrana,
        Obsah = Obsah,
        OddelovacOblasti = OddelovacOblasti,
        OddelovacPredmetu = OddelovacPredmetu,
        CharakteristikyOblasti = Charakteristiky,
        NespracovanePredmety = Nespracovane,
    };

    /// <summary>Úprava úvodnej strany (texty, logo) a hlavičky ďalších strán – každú časť sa dá prepísať aj vypnúť.</summary>
    [RelayCommand]
    private void UpravitUvodnuStranu()
    {
        var okno = new Views.UvodnaStranaWindow { Owner = Dialogy.Vlastnik };
        if (okno.ShowDialog() == true)
        {
            Hlavny.NacitajUdaje();
            OnPropertyChanged(nameof(MaLogo));
            TitulnaStrana = true;
            Obnovit();
        }
    }

    [RelayCommand]
    private void VybratVsetky()
    {
        foreach (var o in Oblasti)
        {
            o.Zahrnut = true;
        }
    }

    [RelayCommand]
    private void ZrusitVyber()
    {
        foreach (var o in Oblasti)
        {
            o.Zahrnut = false;
        }
    }

    /// <summary>Zostaví dokument a pripraví náhľad strán.</summary>
    [RelayCommand]
    public void Obnovit()
    {
        try
        {
            Dialogy.Cakaj(() =>
            {
                _dokument = ZostavaOsnov.Zostav(Volby());
                _vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(_dokument));
            });
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Dokument sa nepodarilo zostaviť.", ex);
            return;
        }

        Strany.Clear();
        var pocet = NahladDokumentu.PocetStran(_vykreslenie!);
        for (var i = 1; i <= pocet; i++)
        {
            Strany.Add(new StranaNahladu(_vykreslenie!, i));
        }

        OnPropertyChanged(nameof(PocetStran));
        var oblasti = _dokument!.Oblasti.Count;
        Popis = $"{PocetStranText(pocet)}  •  {OblastiText(oblasti)}  •  {PdfVystup.PocetPredmetov(_dokument.PocetPredmetov)}";
    }

    public int PocetStran => Strany.Count;

    [RelayCommand]
    private void Tlacit()
    {
        if (_vykreslenie is null)
        {
            Obnovit();
        }

        if (_vykreslenie is not null)
        {
            NahladDokumentu.Vytlac(_vykreslenie, $"{_dokument?.Titul.Podnadpis ?? Services.TitulnaStrana.PredvolenyPodnadpis} – SkVP");
        }
    }

    [RelayCommand]
    private void UlozitPdf()
    {
        var dokument = ZostavaOsnov.Zostav(Volby());
        var cesta = Dialogy.UlozitAko("Uložiť učebné osnovy do PDF", "Dokument PDF (*.pdf)|*.pdf", ZostavaOsnov.NazovSuboru(dokument, ".pdf"));
        if (cesta is null)
        {
            return;
        }

        Uloz(cesta, () => PdfVystup.UlozPdf(dokument, cesta));
    }

    [RelayCommand]
    private void UlozitWord()
    {
        var dokument = ZostavaOsnov.Zostav(Volby());
        var cesta = Dialogy.UlozitAko("Uložiť učebné osnovy do Wordu", "Dokument Word (*.docx)|*.docx", ZostavaOsnov.NazovSuboru(dokument, ".docx"));
        if (cesta is null)
        {
            return;
        }

        Uloz(cesta, () => DocxVystup.Uloz(dokument, cesta));
    }

    private void Uloz(string cesta, Action ulozenie)
    {
        try
        {
            Dialogy.Cakaj(ulozenie);
        }
        catch (IOException ex)
        {
            Dialogy.Chyba("Súbor sa nepodarilo uložiť. Ak je otvorený v inom programe, zatvorte ho a skúste znova.", ex);
            return;
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Súbor sa nepodarilo uložiť.", ex);
            return;
        }

        Hlavny.Oznam($"Uložené: {Path.GetFileName(cesta)}");
        if (OtvoritPoUlozeni)
        {
            Dialogy.OtvorVSysteme(cesta);
        }
    }

    [RelayCommand]
    private void Priblizit() => Zvacsenie = Math.Min(1.6, Math.Round(Zvacsenie + 0.1, 2));

    [RelayCommand]
    private void Oddialit() => Zvacsenie = Math.Max(0.3, Math.Round(Zvacsenie - 0.1, 2));

    public static string PocetStranText(int pocet) => pocet switch
    {
        1 => "1 strana",
        >= 2 and <= 4 => $"{pocet} strany",
        _ => $"{pocet} strán",
    };

    public static string OblastiText(int pocet) => pocet switch
    {
        1 => "1 oblasť",
        >= 2 and <= 4 => $"{pocet} oblasti",
        _ => $"{pocet} oblastí",
    };
}
