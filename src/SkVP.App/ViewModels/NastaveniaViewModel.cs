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
        Organizacia = new UdajeOrganizacieVM(UdajeOrganizacie.Nacitaj(), u =>
        {
            u.Uloz();
            Hlavny.NacitajUdaje();
        });
        _nazovProgramu = udaje.NazovProgramu;
        _typ = udaje.Typ;
        _obdobie = udaje.Obdobie;
        _tema = Databaza.Citaj(NastaveniaSkoly.KlucTema, NastaveniaSkoly.TemaSystem);
        _kontrolovatAktualizacie = new AktualizaciaService().KontrolovatPriSpusteni();
        _upravaVoWorde = Databaza.Citaj(Word.KlucRezimUpravy, Word.RezimWord) == Word.RezimWord;
        NacitajLogo();
        _jazyk = Preklad.Aktualny;
        _nacitane = true;
    }

    public IReadOnlyList<Jazyk> Jazyky => Preklad.Jazyky;

    [ObservableProperty]
    private Jazyk _jazyk;

    partial void OnJazykChanged(Jazyk value)
    {
        if (_nacitane && value is not null)
        {
            Databaza.Zapis(Preklad.KlucJazyk, value.Kod);
            Preklad.Nastav(value.Kod);
        }
    }

    /// <summary>Výber skupín nastavení (v tichom režime samotestu všetky).</summary>
    private static IReadOnlyCollection<string>? VyberSkupiny(string titulok, string popis, string tlacidlo, IReadOnlyList<string> skupiny)
    {
        if (Dialogy.TichyRezim)
        {
            return skupiny;
        }

        var okno = new Views.VyberSkupinWindow(titulok, popis, tlacidlo, skupiny) { Owner = Dialogy.Vlastnik };
        return okno.ShowDialog() == true ? okno.Vybrane : null;
    }

    [RelayCommand]
    private void ExportovatNastavenia()
    {
        var skupiny = VyberSkupiny("Export nastavení programu",
            "Ktoré nastavenia programu sa majú uložiť do súboru? Databáza (oblasti, predmety, osnovy a ich archív) sa neukladá – na to je záloha v sekcii Servis.",
            "Exportovať…", NastaveniaProgramu.Prehlad(NastaveniaProgramu.Vytvor()));
        if (skupiny is null)
        {
            return;
        }

        var cesta = Dialogy.UlozitAko("Exportovať nastavenia programu", NastaveniaProgramu.Filter, $"SkVP_nastavenia_{DateTime.Now:yyyy-MM-dd}.json");
        if (cesta is null)
        {
            return;
        }

        try
        {
            NastaveniaProgramu.Export(cesta, skupiny);
            Hlavny.Oznam($"Nastavenia sú uložené: {System.IO.Path.GetFileName(cesta)}");
        }
        catch (System.IO.IOException ex)
        {
            Dialogy.Chyba("Nastavenia sa nepodarilo uložiť.", ex);
        }
    }

    [RelayCommand]
    private void ImportovatNastavenia()
    {
        var cesta = Dialogy.Otvorit("Importovať nastavenia", NastaveniaProgramu.Filter);
        if (cesta is null)
        {
            return;
        }

        try
        {
            var subor = NastaveniaProgramu.Nacitaj(cesta);
            var skupiny = VyberSkupiny("Import nastavení programu",
                $"Súbor {System.IO.Path.GetFileName(cesta)} (SkVP {subor.Verzia}, {subor.Vytvorene:d. M. yyyy}). Vyberte, ktoré nastavenia sa majú prevziať – " +
                "doterajšie sa prepíšu (pred tým sa urobí záloha databázy). Oblasti, predmety ani osnovy sa nemenia.",
                "Importovať", NastaveniaProgramu.Prehlad(subor));
            if (skupiny is null)
            {
                return;
            }

            new ZalohaService().Zalohuj();
            var pocet = NastaveniaProgramu.Import(subor, skupiny);
            Preklad.Nacitaj();
            App.NastavTemu(Databaza.Citaj(NastaveniaSkoly.KlucTema, NastaveniaSkoly.TemaSystem));
            Hlavny.NacitajUdaje();
            Hlavny.NacitajOblasti(); // zapnuté/vypnuté úvodné a záverečné časti učebných osnov
            Hlavny.Oznam($"Nastavenia sú importované ({pocet}).");
            Hlavny.OtvorNastavenia();
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    public MainViewModel Hlavny { get; }

    /// <summary>Čo sa pri exporte nastavení programu uloží (podľa terajších nastavení).</summary>
    public string ObsahNastaveni => string.Join(", ", NastaveniaProgramu.Prehlad(NastaveniaProgramu.Vytvor()));

    /// <summary>Údaje o organizácii – názov, zložka, adresa, IČO, DIČ, e-mail, telefón (ukladajú sa hneď).</summary>
    public UdajeOrganizacieVM Organizacia { get; }

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
