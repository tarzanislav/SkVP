using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Nastavenia: škola, program, vzhľad, učebný plán, zálohy a aktualizácie.</summary>
public sealed partial class NastaveniaViewModel : ObservableObject
{
    private readonly bool _nacitane;

    public NastaveniaViewModel(MainViewModel hlavny)
    {
        Hlavny = hlavny;
        var udaje = UdajeSkVP.Nacitaj();
        _skola = udaje.Skola;
        _adresa = udaje.Adresa;
        _nazovProgramu = udaje.NazovProgramu;
        _typ = udaje.Typ;
        _obdobie = udaje.Obdobie;
        _tema = Databaza.Citaj(NastaveniaSkoly.KlucTema, NastaveniaSkoly.TemaSystem);
        _kontrolovatAktualizacie = new AktualizaciaService().KontrolovatPriSpusteni();
        _upravaVoWorde = Databaza.Citaj(Word.KlucRezimUpravy, Word.RezimWord) == Word.RezimWord;
        NacitajLogo();
        _nacitane = true;
    }

    public MainViewModel Hlavny { get; }

    [ObservableProperty]
    private string _skola;

    [ObservableProperty]
    private string _adresa;

    [ObservableProperty]
    private string _nazovProgramu;

    [ObservableProperty]
    private string _typ;

    [ObservableProperty]
    private string _obdobie;

    [ObservableProperty]
    private string _tema;

    [ObservableProperty]
    private bool _kontrolovatAktualizacie;

    /// <summary>Osnovy upravovať vo Worde (ak je nainštalovaný), inak vo vstavanom editore.</summary>
    [ObservableProperty]
    private bool _upravaVoWorde;

    public bool WordJeNainstalovany => Word.JeNainstalovany;

    public string StavWordu => Word.JeNainstalovany
        ? "Microsoft Word je na tomto počítači nainštalovaný."
        : "Microsoft Word na tomto počítači nie je – osnovy sa upravujú vo vstavanom editore programu.";

    partial void OnUpravaVoWordeChanged(bool value)
    {
        if (_nacitane)
        {
            Databaza.Zapis(Word.KlucRezimUpravy, value ? Word.RezimWord : Word.RezimEditor);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaLogo))]
    private BitmapImage? _logo;

    public bool MaLogo => Logo is not null;

    [ObservableProperty]
    private string _stavAktualizacie = "";

    public string PriecinokDat => Databaza.Priecinok;
    public string PriecinokZaloh => Databaza.PriecinokZaloh;
    public string Verzia => AktualizaciaService.AktualnaVerzia.ToString();

    public string PoslednaZaloha
    {
        get
        {
            var posledna = new ZalohaService().Zalohy().FirstOrDefault();
            return posledna is null ? "Zatiaľ žiadna záloha." : $"Posledná záloha: {posledna.Name} ({posledna.LastWriteTime:d. M. yyyy H:mm})";
        }
    }

    partial void OnSkolaChanged(string value) => Zapis(UdajeSkVP.KlucSkola, value.Trim());
    partial void OnAdresaChanged(string value) => Zapis(UdajeSkVP.KlucAdresa, value.Trim());
    partial void OnNazovProgramuChanged(string value) =>
        Zapis(UdajeSkVP.KlucNazov, string.IsNullOrWhiteSpace(value) ? UdajeSkVP.PredvolenyNazov : value.Trim());
    partial void OnTypChanged(string value) => Zapis(UcebnyPlanService.KlucTypSkoly, value.Trim());
    partial void OnObdobieChanged(string value) => Zapis(UdajeSkVP.KlucObdobie, value.Trim());

    partial void OnTemaChanged(string value)
    {
        if (!_nacitane)
        {
            return;
        }

        Databaza.Zapis(NastaveniaSkoly.KlucTema, value);
        App.NastavTemu(value);
    }

    partial void OnKontrolovatAktualizacieChanged(bool value)
    {
        if (_nacitane)
        {
            new AktualizaciaService().NastavKontroluPriSpusteni(value);
        }
    }

    private void Zapis(string kluc, string hodnota)
    {
        if (!_nacitane)
        {
            return;
        }

        Databaza.Zapis(kluc, hodnota);
        Hlavny.NacitajUdaje();
    }

    private void NacitajLogo()
    {
        var cesta = NastaveniaSkoly.CestaLoga();
        if (cesta is null)
        {
            Logo = null;
            return;
        }

        try
        {
            var obrazok = new BitmapImage();
            obrazok.BeginInit();
            obrazok.CacheOption = BitmapCacheOption.OnLoad;
            obrazok.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            obrazok.UriSource = new Uri(cesta);
            obrazok.EndInit();
            obrazok.Freeze();
            Logo = obrazok;
        }
        catch (Exception)
        {
            Logo = null;
        }
    }

    [RelayCommand]
    private void VybratLogo()
    {
        var cesta = Dialogy.Otvorit("Vyberte logo školy", "Obrázky (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif");
        if (cesta is null)
        {
            return;
        }

        try
        {
            NastaveniaSkoly.NastavLogo(cesta);
            NacitajLogo();
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    private void OdstranitLogo()
    {
        NastaveniaSkoly.OdstranLogo();
        NacitajLogo();
    }

    [RelayCommand]
    private void NacitatZExcelu()
    {
        var cesta = Dialogy.Otvorit("Tabuľka so vzdelávacími oblasťami a predmetmi", "Tabuľky Excel (*.xlsx)|*.xlsx");
        if (cesta is null)
        {
            return;
        }

        try
        {
            var plan = UcebnyPlanService.NacitajZExcelu(cesta);
            var nahradit = Hlavny.Oblasti.Count > 0 && Dialogy.Potvrd(
                $"V tabuľke je {plan.Oblasti.Count} oblastí a {plan.PocetPredmetov} predmetov.\n\n" +
                "Áno – nahradiť doterajšie oblasti a predmety (dajú sa obnoviť zo zálohy).\n" +
                "Nie – doplniť chýbajúce oblasti a predmety k doterajším.");
            if (nahradit)
            {
                new ZalohaService().Zalohuj();
            }

            var vysledok = UcebnyPlanService.Vytvor(plan, nahradit);
            Hlavny.NacitajOblasti();
            Hlavny.Oznam($"Načítané: {vysledok.NoveOblasti} nových oblastí, {vysledok.NovePredmety} nových predmetov.");
            Typ = Databaza.Citaj(UcebnyPlanService.KlucTypSkoly, Typ);
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    private void Zalohovat()
    {
        try
        {
            var cesta = new ZalohaService().Zalohuj();
            OnPropertyChanged(nameof(PoslednaZaloha));
            Hlavny.Oznam($"Záloha je uložená: {System.IO.Path.GetFileName(cesta)}");
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    private void OtvoritZalohy()
    {
        System.IO.Directory.CreateDirectory(Databaza.PriecinokZaloh);
        Dialogy.OtvorVSysteme(Databaza.PriecinokZaloh);
    }

    [RelayCommand]
    private void OtvoritData() => Dialogy.OtvorVSysteme(Databaza.Priecinok);

    [RelayCommand]
    private void NahratTestovaciuSadu()
    {
        if (Dialogy.Potvrd("Do predmetov učebného plánu gymnázia sa nahrajú ukážkové učebné osnovy (chýbajúce oblasti a predmety sa doplnia).\n\n" +
                           "Osnovy predmetov s rovnakým názvom sa nahradia – doterajšia verzia sa zarchivuje.\n\nPokračovať?"))
        {
            Hlavny.NahrajTestovaciuSadu();
        }
    }

    [RelayCommand]
    private void VymazatUdaje() => Hlavny.VymazatUdaje();

    [RelayCommand]
    private async Task SkontrolovatAktualizacie()
    {
        StavAktualizacie = "Kontrolujem…";
        try
        {
            var nova = await new AktualizaciaService().SkontrolujAsync();
            Hlavny.NovaVerzia = nova;
            StavAktualizacie = nova is null ? "Máte najnovšiu verziu." : $"Je dostupná verzia {nova.Verzia}.";
        }
        catch (SkvpException ex)
        {
            StavAktualizacie = ex.Message;
        }
    }
}
