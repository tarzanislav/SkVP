using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Stránka, ktorá má pred odchodom niečo uložiť (rozpísaný text osnovy).</summary>
public interface IUkladaPredOdchodom
{
    void UlozTeraz();
}

public static class Sekcie
{
    public const string Dashboard = "dashboard";
    public const string Osnovy = "osnovy";
    public const string Kalendar = "kalendar";
    public const string Ulohy = "ulohy";
    public const string Poznamky = "poznamky";
    public const string Tlac = "tlac";
    public const string ImportExport = "import";
    public const string Archiv = "archiv";
    public const string Novinky = "novinky";
    public const string Servis = "servis";
    public const string Nastavenia = "nastavenia";
}

/// <summary>Hlavné okno: menu, oblasti s predmetmi, prechod medzi stránkami a spoločné akcie.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly ObjektService _sluzba = new();
    private readonly DispatcherTimer _casovacOznamenia = new() { Interval = TimeSpan.FromSeconds(9) };
    private Action? _vratitSpat;

    public MainViewModel()
    {
        _casovacOznamenia.Tick += (_, _) =>
        {
            _casovacOznamenia.Stop();
            Oznamenie = null;
            _vratitSpat = null;
        };
        NacitajOblasti();
        NacitajUdaje();
        OtvorDashboard();
    }

    /// <summary>V programe je testovacia sada učebných osnov (ukážkové dáta).</summary>
    [ObservableProperty]
    private bool _jeTestovaciaSada;

    /// <summary>Vymazanie údajov (testovacej sady alebo všetkého) so zálohou pred vymazaním.</summary>
    [RelayCommand]
    public void VymazatUdaje()
    {
        var okno = new Views.VymazanieWindow { Owner = Dialogy.Vlastnik };
        if (!Dialogy.TichyRezim && okno.ShowDialog() != true)
        {
            return;
        }

        Vymaz(okno.VsetkoVrataneOblasti);
    }

    public void Vymaz(bool vsetko)
    {
        (Stranka as IUkladaPredOdchodom)?.UlozTeraz();
        UkonciUpravyVoWorde();
        try
        {
            Dialogy.Cakaj(() =>
            {
                new ZalohaService().Zalohuj();
                if (vsetko)
                {
                    TestovaciaSada.VymazVsetko();
                }
                else
                {
                    TestovaciaSada.VymazOsnovy();
                }
            });
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Údaje sa nepodarilo vymazať.", ex);
            return;
        }

        if (vsetko && !Dialogy.TichyRezim)
        {
            new Views.PrveSpustenieWindow { Owner = Dialogy.Vlastnik }.ShowDialog();
        }

        NacitajOblasti();
        NacitajUdaje();
        OtvorDashboard();
        Oznam(vsetko
            ? "Všetky údaje sú vymazané. Záloha pred vymazaním je v priečinku záloh."
            : "Osnovy sú vymazané, oblasti a predmety zostali. Záloha pred vymazaním je v priečinku záloh.");
    }

    /// <summary>Nahrá testovaciu sadu učebných osnov.</summary>
    public void NahrajTestovaciuSadu()
    {
        (Stranka as IUkladaPredOdchodom)?.UlozTeraz();
        try
        {
            TestovaciaSada.Vysledok? vysledok = null;
            Dialogy.Cakaj(() => vysledok = TestovaciaSada.Nainstaluj());
            NacitajOblasti();
            OtvorDashboard();
            Oznam($"Testovacia sada je nahratá: {vysledok!.Osnovy} osnov, {vysledok.Charakteristiky} charakteristík oblastí, {vysledok.Suvislosti} súvislostí.");
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Testovaciu sadu sa nepodarilo nahrať.", ex);
        }
    }

    public ObservableCollection<OblastVM> Oblasti { get; } = new();

    [ObservableProperty]
    private object? _stranka;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeDashboard), nameof(JeOsnovy), nameof(JeTlac), nameof(JeNastavenia), nameof(JeImportExport), nameof(JeArchiv), nameof(JeServis), nameof(JeNovinky),
        nameof(JeKalendar), nameof(JeUlohy), nameof(JePoznamky))]
    private string _sekcia = Sekcie.Dashboard;

    public bool JeDashboard => Sekcia == Sekcie.Dashboard;
    public bool JeOsnovy => Sekcia == Sekcie.Osnovy;
    public bool JeTlac => Sekcia == Sekcie.Tlac;
    public bool JeNastavenia => Sekcia == Sekcie.Nastavenia;
    public bool JeImportExport => Sekcia == Sekcie.ImportExport;
    public bool JeArchiv => Sekcia == Sekcie.Archiv;
    public bool JeNovinky => Sekcia == Sekcie.Novinky;
    public bool JeServis => Sekcia == Sekcie.Servis;
    public bool JeKalendar => Sekcia == Sekcie.Kalendar;
    public bool JeUlohy => Sekcia == Sekcie.Ulohy;
    public bool JePoznamky => Sekcia == Sekcie.Poznamky;

    [ObservableProperty]
    private string _skola = "";

    [ObservableProperty]
    private string _nazovProgramu = UdajeSkVP.PredvolenyNazov;

    [ObservableProperty]
    private string? _oznamenie;

    [ObservableProperty]
    private bool _maVratitSpat;

    [ObservableProperty]
    private DostupnaVerzia? _novaVerzia;

    public string VerziaText => $"Verzia {AktualizaciaService.AktualnaVerzia}";

    public string PodtitulMenu => Skola.Length > 0 ? Skola : NazovProgramu;

    partial void OnSkolaChanged(string value) => OnPropertyChanged(nameof(PodtitulMenu));

    partial void OnNazovProgramuChanged(string value) => OnPropertyChanged(nameof(PodtitulMenu));

    public int PocetPredmetov => Oblasti.Sum(o => o.Pocet);

    // ---- načítanie ----

    /// <summary>Úvodné časti učebných osnov (skupina pred oblasťami).</summary>
    [ObservableProperty]
    private OblastVM? _uvodneCasti;

    /// <summary>Záverečné časti učebných osnov (skupina za oblasťami).</summary>
    [ObservableProperty]
    private OblastVM? _zaverecneCasti;

    /// <summary>Všetky skupiny v poradí dokumentu: úvodné časti, oblasti, záverečné časti (aj vypnuté).</summary>
    public IEnumerable<OblastVM> VsetkySkupiny =>
        new[] { UvodneCasti }.Concat(Oblasti).Append(ZaverecneCasti).OfType<OblastVM>();

    private OblastVM NacitajSkupinu(string typ)
    {
        var objekt = CastiOsnov.Skupina(_sluzba, typ);
        var skupina = new OblastVM(objekt, 0);
        foreach (var cast in _sluzba.Deti(objekt.Id).Where(p => p.Typ == TypObjektu.Cast))
        {
            skupina.Predmety.Add(new PredmetVM(cast, skupina));
        }

        skupina.Obnov();
        return skupina;
    }

    /// <summary>Zapne alebo vypne úvodné/záverečné časti učebných osnov (prepínač v sekcii Osnovy).</summary>
    public void NastavAktivnuSkupinu(OblastVM skupina, bool aktivna)
    {
        if (skupina.JeOblast)
        {
            return;
        }

        skupina.Aktivna = aktivna;
        Oznam(aktivna
            ? $"{skupina.Nazov} sú zapnuté – zaradia sa do tlače, exportu, archívu a záloh."
            : $"{skupina.Nazov} sú vypnuté – dokumenty zostávajú uložené, do tlače a exportu sa nezaradia.");
    }

    public void NacitajOblasti()
    {
        var vybrana = VsetkySkupiny.FirstOrDefault(o => o.JeVybrana)?.Id;
        UvodneCasti = NacitajSkupinu(TypObjektu.Uvod);
        ZaverecneCasti = NacitajSkupinu(TypObjektu.Zaver);
        UvodneCasti.JeVybrana = UvodneCasti.Id == vybrana;
        ZaverecneCasti.JeVybrana = ZaverecneCasti.Id == vybrana;
        Oblasti.Clear();
        var index = 0;
        foreach (var objekt in _sluzba.Oblasti())
        {
            var oblast = new OblastVM(objekt, index++) { JeVybrana = objekt.Id == vybrana };
            foreach (var predmet in _sluzba.Deti(objekt.Id).Where(p => p.Typ == TypObjektu.Predmet))
            {
                oblast.Predmety.Add(new PredmetVM(predmet, oblast));
            }

            oblast.Obnov();
            Oblasti.Add(oblast);
        }

        OnPropertyChanged(nameof(PocetPredmetov));
        JeTestovaciaSada = TestovaciaSada.JeNainstalovana;
        ObnovPlanovanie();
    }

    public void NacitajUdaje()
    {
        var udaje = UdajeSkVP.Nacitaj();
        Skola = udaje.Skola;
        NazovProgramu = udaje.NazovProgramu;
    }

    public OblastVM? NajdiOblast(long id) => VsetkySkupiny.FirstOrDefault(o => o.Id == id);

    public PredmetVM? NajdiPredmet(long id) => VsetkySkupiny.SelectMany(o => o.Predmety).FirstOrDefault(p => p.Id == id);

    // ---- navigácia ----

    /// <summary>
    /// Prechod na inú stránku: rozpísané zmeny doterajšej stránky sa uložia ešte pred vytvorením novej, aby nová stránka
    /// načítala aktuálne údaje (napr. kalendár práve upravenú úlohu).
    /// </summary>
    private void Prejdi(string sekcia, Func<object> vytvorStranku, OblastVM? oblast = null)
    {
        (Stranka as IUkladaPredOdchodom)?.UlozTeraz();
        var nova = vytvorStranku();
        (Stranka as IDisposable)?.Dispose();
        foreach (var o in VsetkySkupiny)
        {
            o.JeVybrana = o == oblast;
        }

        Sekcia = sekcia;
        Stranka = nova;
    }

    [RelayCommand]
    public void OtvorDashboard() => Prejdi(Sekcie.Dashboard, () => new DashboardViewModel(this));

    [RelayCommand]
    public void OtvorOsnovy() => Prejdi(Sekcie.Osnovy, () => new OsnovyViewModel(this));

    [RelayCommand]
    public void OtvorOblast(OblastVM? oblast)
    {
        if (oblast is { Aktivna: false })
        {
            Oznam($"{oblast.Nazov} sú vypnuté – zapnite ich prepínačom v sekcii Osnovy.");
            return;
        }

        if (oblast is not null)
        {
            Prejdi(Sekcie.Osnovy, () => new OblastViewModel(this, oblast), oblast);
        }
    }

    [RelayCommand]
    public void OtvorPredmet(PredmetVM? predmet)
    {
        if (predmet is not null)
        {
            Prejdi(Sekcie.Osnovy, () => new PredmetViewModel(this, predmet), predmet.Oblast);
        }
    }

    [RelayCommand]
    public void OtvorTlac(OblastVM? oblast) => Prejdi(Sekcie.Tlac, () => new TlacViewModel(this, oblast));

    [RelayCommand]
    public void OtvorNastavenia() => Prejdi(Sekcie.Nastavenia, () => new NastaveniaViewModel(this));

    [RelayCommand]
    public void OtvorImportExport() => Prejdi(Sekcie.ImportExport, () => new ImportExportViewModel(this));

    [RelayCommand]
    public void OtvorArchiv() => Prejdi(Sekcie.Archiv, () => new ArchivViewModel(this, null));

    /// <summary>Sekcia Archív s vybraným archívom školského roka.</summary>
    public void OtvorArchivRoka(long archivId) => Prejdi(Sekcie.Archiv, () => new ArchivViewModel(this, archivId));

    [RelayCommand]
    public void OtvorNovinky() => Prejdi(Sekcie.Novinky, () => new NovinkyViewModel(this));

    [RelayCommand]
    public void OtvorServis() => Prejdi(Sekcie.Servis, () => new ServisViewModel(this));

    /// <summary>Kalendár na mesiaci s dátumom (predvolene dnes).</summary>
    [RelayCommand]
    public void OtvorKalendar(DateTime? datum) => Prejdi(Sekcie.Kalendar, () => new KalendarViewModel(this, datum ?? DateTime.Today));

    /// <summary>Úlohy, voliteľne s vybranou úlohou.</summary>
    [RelayCommand]
    public void OtvorUlohy(long? vybrana) => Prejdi(Sekcie.Ulohy, () => new UlohyViewModel(this, vybrana));

    /// <summary>Poznámky, voliteľne s vybranou poznámkou.</summary>
    [RelayCommand]
    public void OtvorPoznamky(long? vybrana) => Prejdi(Sekcie.Poznamky, () => new PoznamkyViewModel(this, vybrana));

    /// <summary>Otvorí ľubovoľný objekt: oblasť alebo časti osnov, predmet, úlohu, poznámku alebo udalosť (v kalendári).</summary>
    public void OtvorObjekt(long id)
    {
        if (NajdiOblast(id) is { } oblast)
        {
            OtvorOblast(oblast);
        }
        else if (NajdiPredmet(id) is { } predmet)
        {
            if (!predmet.Oblast.Aktivna)
            {
                Oznam($"{predmet.Oblast.Nazov} sú vypnuté – zapnite ich prepínačom v sekcii Osnovy.");
                return;
            }

            OtvorPredmet(predmet);
        }
        else if (_sluzba.Nacitaj(id) is { } objekt)
        {
            switch (objekt.Typ)
            {
                case TypObjektu.Uloha:
                    OtvorUlohy(id);
                    break;
                case TypObjektu.Poznamka:
                    OtvorPoznamky(id);
                    break;
                case TypObjektu.Udalost:
                    OtvorKalendar(new KalendarService().Nacitaj(id)?.Zaciatok);
                    break;
            }
        }
    }

    // ---- kalendár, úlohy, poznámky: počty v menu a odznaky pri predmetoch ----

    [ObservableProperty]
    private int _pocetOtvorenychUloh;

    [ObservableProperty]
    private int _pocetUlohPoTermine;

    [ObservableProperty]
    private int _pocetPoznamok;

    /// <summary>Počet dnešných udalostí a úloh s dnešným termínom.</summary>
    [ObservableProperty]
    private int _pocetDnes;

    /// <summary>Prepočíta počty v menu a odznaky otvorených úloh pri predmetoch a oblastiach.</summary>
    public void ObnovPlanovanie()
    {
        try
        {
            var ulohy = new UlohyService();
            var (otvorene, poTermine, _) = ulohy.Pocty();
            PocetOtvorenychUloh = otvorene;
            PocetUlohPoTermine = poTermine;
            PocetPoznamok = new PoznamkyService().Pocet();
            PocetDnes = new KalendarService().Vyskyty(DateTime.Today, DateTime.Today, automaticke: false).Count(v => !v.JeVybavena);
            var podlaObjektov = ulohy.OtvorenePodlaObjektov();
            foreach (var skupina in VsetkySkupiny)
            {
                foreach (var predmet in skupina.Predmety)
                {
                    predmet.OtvoreneUlohy = podlaObjektov.GetValueOrDefault(predmet.Id);
                }

                skupina.OtvoreneUlohy = podlaObjektov.GetValueOrDefault(skupina.Id) + skupina.Predmety.Sum(p => p.OtvoreneUlohy);
            }
        }
        catch (Exception)
        {
            // počty sa obnovia nabudúce
        }
    }

    // ---- oblasti ----

    public OblastVM? PridajOblast(string nazov)
    {
        if (string.IsNullOrWhiteSpace(nazov))
        {
            return null;
        }

        var objekt = _sluzba.Pridaj(TypObjektu.Oblast, null, nazov, UcebnyPlanService.FarbaOblasti(Oblasti.Count));
        var oblast = new OblastVM(objekt, Oblasti.Count);
        oblast.Obnov();
        Oblasti.Add(oblast);
        return oblast;
    }

    [RelayCommand]
    private void PosunOblastHore(OblastVM? oblast) => PosunOblast(oblast, -1);

    [RelayCommand]
    private void PosunOblastDole(OblastVM? oblast) => PosunOblast(oblast, +1);

    private void PosunOblast(OblastVM? oblast, int smer)
    {
        if (oblast is null || !_sluzba.Posun(oblast.Id, smer))
        {
            return;
        }

        var index = Oblasti.IndexOf(oblast);
        Oblasti.Move(index, index + smer);
    }

    public void PremenujOblast(OblastVM oblast, string nazov)
    {
        nazov = nazov.Trim();
        if (nazov.Length == 0 || nazov == oblast.Nazov)
        {
            return;
        }

        var stary = oblast.Nazov;
        (Stranka as IUkladaPredOdchodom)?.UlozTeraz();
        _sluzba.Premenuj(oblast.Id, nazov);
        oblast.Nazov = nazov;
        PrenesNazovDoDokumentov(stary, nazov, "Oblasť");
    }

    /// <summary>
    /// Nový názov predmetu či oblasti sa prenesie aj do textov dokumentov (osnovy, charakteristiky, úvodná strana).
    /// Dokumenty otvorené vo Worde sa nemenia – to by prepísalo rozpracované úpravy.
    /// </summary>
    private void PrenesNazovDoDokumentov(string stary, string novy, string co)
    {
        PremenovanieVDokumentoch.Vysledok vysledok;
        try
        {
            vysledok = PremenovanieVDokumentoch.NahradVsade(stary, novy, _upravyVoWorde.Keys.ToList());
        }
        catch (Exception ex)
        {
            Dialogy.Chyba($"{co} má nový názov, ale v textoch dokumentov sa ho nepodarilo zmeniť.", ex);
            return;
        }

        foreach (var id in vysledok.Zmenene)
        {
            PoZmeneDokumentu(id, oznamit: false);
            DokumentPrepisany?.Invoke(id);
        }

        var sprava = vysledok.Zmenene.Count switch
        {
            0 => "",
            1 => $"Názov „{novy}“ je zmenený aj v texte 1 dokumentu (predchádzajúce znenie je v archíve verzií).",
            _ => $"Názov „{novy}“ je zmenený aj v textoch dokumentov: {vysledok.Zmenene.Count} (predchádzajúce znenie je v archíve verzií).",
        };
        if (vysledok.Vynechane.Count > 0)
        {
            var nazvy = string.Join(", ", vysledok.Vynechane.Select(id => _sluzba.Nacitaj(id)?.Nazov ?? "?"));
            sprava = (sprava + $" Dokument otvorený vo Worde ({nazvy}) ostal bez zmeny – starý názov v ňom opravte vo Worde.").Trim();
        }

        if (sprava.Length > 0)
        {
            Oznam(sprava);
        }
    }

    public void NastavFarbuOblasti(OblastVM oblast, string farba)
    {
        _sluzba.NastavFarbu(oblast.Id, farba);
        oblast.Farba = farba;
    }

    [RelayCommand]
    private void OdstranOblast(OblastVM? oblast)
    {
        if (oblast is null || !Dialogy.Potvrd(
                $"Odstrániť vzdelávaciu oblasť „{oblast.Nazov}“ aj s jej predmetmi ({oblast.Pocet})?\n\nOdstránenie sa dá hneď vrátiť späť."))
        {
            return;
        }

        _sluzba.Odstran(oblast.Id);
        var index = Oblasti.IndexOf(oblast);
        Oblasti.Remove(oblast);
        OnPropertyChanged(nameof(PocetPredmetov));
        if (Stranka is OblastViewModel or PredmetViewModel)
        {
            OtvorOsnovy();
        }

        Oznam($"Oblasť „{oblast.Nazov}“ je odstránená.", () =>
        {
            _sluzba.Obnov(oblast.Id);
            Oblasti.Insert(Math.Min(index, Oblasti.Count), oblast);
            OnPropertyChanged(nameof(PocetPredmetov));
        });
    }

    // ---- predmety ----

    public PredmetVM? PridajPredmet(OblastVM oblast, string nazov)
    {
        if (string.IsNullOrWhiteSpace(nazov))
        {
            return null;
        }

        var objekt = _sluzba.Pridaj(oblast.JeOblast ? TypObjektu.Predmet : TypObjektu.Cast, oblast.Id, nazov);
        var predmet = new PredmetVM(objekt, oblast);
        oblast.Predmety.Add(predmet);
        oblast.Obnov();
        OnPropertyChanged(nameof(PocetPredmetov));
        return predmet;
    }

    [RelayCommand]
    private void PosunPredmetHore(PredmetVM? predmet) => PosunPredmet(predmet, -1);

    [RelayCommand]
    private void PosunPredmetDole(PredmetVM? predmet) => PosunPredmet(predmet, +1);

    public void PosunPredmet(PredmetVM? predmet, int smer)
    {
        if (predmet is null || !_sluzba.Posun(predmet.Id, smer))
        {
            return;
        }

        var zoznam = predmet.Oblast.Predmety;
        var index = zoznam.IndexOf(predmet);
        zoznam.Move(index, index + smer);
        predmet.Oblast.Obnov();
    }

    /// <summary>Presun predmetu na iné miesto alebo do inej oblasti (ťahanie myšou).</summary>
    public void PresunPredmet(PredmetVM predmet, OblastVM cielova, int pozicia)
    {
        var povodna = predmet.Oblast;
        if (povodna.JeOblast != cielova.JeOblast || (povodna.JeCastiOsnov && povodna != cielova))
        {
            // predmety a úvodné/záverečné časti sa medzi sebou nepresúvajú
            Oznam(povodna.JeOblast
                ? "Predmet sa nedá presunúť medzi úvodné alebo záverečné časti učebných osnov."
                : "Úvodné a záverečné časti učebných osnov sa nedajú presunúť do vzdelávacej oblasti.");
            return;
        }

        var staryIndex = povodna.Predmety.IndexOf(predmet);
        if (povodna == cielova)
        {
            if (pozicia > staryIndex)
            {
                pozicia--;
            }

            pozicia = Math.Clamp(pozicia, 0, povodna.Predmety.Count - 1);
            if (pozicia == staryIndex)
            {
                return;
            }
        }

        _sluzba.Presun(predmet.Id, cielova.Id, pozicia);
        povodna.Predmety.Remove(predmet);
        pozicia = Math.Clamp(pozicia, 0, cielova.Predmety.Count);
        cielova.Predmety.Insert(pozicia, predmet);
        predmet.Oblast = cielova;
        povodna.Obnov();
        if (cielova != povodna)
        {
            cielova.Obnov();
            Oznam($"Predmet „{predmet.Nazov}“ je presunutý do oblasti {cielova.Nazov}.");
        }
    }

    public void PremenujPredmet(PredmetVM predmet, string nazov)
    {
        nazov = nazov.Trim();
        if (nazov.Length == 0 || nazov == predmet.Nazov)
        {
            return;
        }

        var stary = predmet.Nazov;
        (Stranka as IUkladaPredOdchodom)?.UlozTeraz();
        _sluzba.Premenuj(predmet.Id, nazov);
        predmet.Nazov = nazov;
        PrenesNazovDoDokumentov(stary, nazov, "Predmet");
    }

    [RelayCommand]
    private void OdstranPredmet(PredmetVM? predmet)
    {
        if (predmet is null)
        {
            return;
        }

        var oblast = predmet.Oblast;
        var index = oblast.Predmety.IndexOf(predmet);
        _sluzba.Odstran(predmet.Id);
        oblast.Predmety.Remove(predmet);
        oblast.Obnov();
        OnPropertyChanged(nameof(PocetPredmetov));
        if (Stranka is PredmetViewModel { Predmet: var otvoreny } && otvoreny == predmet)
        {
            OtvorOblast(oblast);
        }

        Oznam(oblast.JeOblast ? $"Predmet „{predmet.Nazov}“ je odstránený." : $"Časť „{predmet.Nazov}“ je odstránená.", () =>
        {
            _sluzba.Obnov(predmet.Id);
            oblast.Predmety.Insert(Math.Min(index, oblast.Predmety.Count), predmet);
            oblast.Obnov();
            OnPropertyChanged(nameof(PocetPredmetov));
        });
    }

    public void NastavStav(PredmetVM predmet, string? stav)
    {
        _sluzba.NastavStav(predmet.Id, stav);
        predmet.StavKod = stav;
        predmet.Oblast.Obnov();
        if (stav != StavyOsnovy.NaKontrolu)
        {
            return;
        }

        // ponuka (nič sa nevytvára samo): rovnaká úloha ako navrhovaná v Úlohách, naviazaná na predmet
        var navrh = NavrhyUloh.KontrolaOsnovy(predmet.Id, predmet.Nazov, predmet.Oblast.Nazov, predmet.Oblast.Farba);
        if (!new UlohyService().Zdroje().Contains(navrh.Kluc))
        {
            Oznam($"Osnova „{predmet.Nazov}“ je na kontrolu.", () =>
            {
                NavrhyUloh.Prevezmi(navrh);
                ObnovPlanovanie();
                Oznam($"Úloha „{navrh.Text}“ je v Úlohách – doplňte termín a zodpovedného.");
            }, "Vytvoriť úlohu");
        }
    }

    // ---- dokumenty a úprava vo Worde ----

    private readonly Dictionary<long, UpravaVoWorde> _upravyVoWorde = new();
    private readonly HashSet<long> _zarchivovaneVoWorde = new();
    private DispatcherTimer? _kontrolaWordu;

    /// <summary>Dokument objektu sa zmenil (prevzatý z Wordu).</summary>
    public event Action<long>? DokumentZmeneny;

    /// <summary>Text dokumentu sa zmenil v programe (napr. nový názov predmetu) – zobrazenie aj editor ho majú načítať znova.</summary>
    public event Action<long>? DokumentPrepisany;

    /// <summary>Dokument sa prepísal v programe (napr. charakteristika zo ŠVP) – stránky ho načítajú znova.</summary>
    public void OznamPrepisanie(long objektId)
    {
        PoZmeneDokumentu(objektId, oznamit: false);
        DokumentPrepisany?.Invoke(objektId);
    }

    /// <summary>Úprava dokumentu vo Worde sa skončila (Word dokument zatvoril).</summary>
    public event Action<long>? WordSkoncil;

    public bool JeVoWorde(long objektId) => _upravyVoWorde.ContainsKey(objektId);

    public bool NiecoJeVoWorde => _upravyVoWorde.Count > 0;

    /// <summary>Otvorí dokument objektu vo Worde (pracovná kópia, zmeny sa preberajú automaticky).</summary>
    public void UpravVoWorde(long objektId, string nazovSuboru, bool sablonaOsnovy)
    {
        if (_upravyVoWorde.TryGetValue(objektId, out var bezi))
        {
            if (!Dialogy.TichyRezim && Word.Cesta is not null)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Word.Cesta, $"\"{bezi.Cesta}\"") { UseShellExecute = false });
            }

            return;
        }

        var data = new DokumentService().NacitajData(objektId)
                   ?? DocxVystup.Osnova(sablonaOsnovy ? DokumentOsnovy.SablonaOsnovy() : new List<Blok>());
        var uprava = UpravaVoWorde.Spusti(objektId, nazovSuboru, data);
        uprava.Ulozene += novy => PrevezmiZWordu(objektId, novy);
        _upravyVoWorde[objektId] = uprava;
        if (_kontrolaWordu is null)
        {
            _kontrolaWordu = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _kontrolaWordu.Tick += (_, _) => KontrolaWordu();
        }

        _kontrolaWordu.Start();
    }

    public void SkontrolujWord(long objektId)
    {
        if (_upravyVoWorde.TryGetValue(objektId, out var uprava))
        {
            uprava.Skontroluj();
        }
    }

    private void PrevezmiZWordu(long objektId, byte[] data)
    {
        try
        {
            var dovod = _zarchivovaneVoWorde.Contains(objektId) ? null : "Pred úpravou vo Worde";
            if (new DokumentService().Uloz(objektId, data, dovod))
            {
                _zarchivovaneVoWorde.Add(objektId);
                PoZmeneDokumentu(objektId, oznamit: true);
            }
        }
        catch (Exception ex)
        {
            Oznam($"Zmeny z Wordu sa nepodarilo prevziať: {ex.Message}");
        }
    }

    /// <summary>Po uložení dokumentu obnoví stav predmetu (dĺžka textu, dátum úpravy) a dá vedieť stránkam.</summary>
    public void PoZmeneDokumentu(long objektId, bool oznamit)
    {
        if (NajdiPredmet(objektId) is { } predmet && _sluzba.Nacitaj(objektId) is { } objekt)
        {
            predmet.DlzkaTextu = objekt.DlzkaTextu;
            predmet.Upravene = objekt.Upravene;
            predmet.NacitajUdajeOsnovy(); // profilácie a platnosť podľa upraveného dokumentu
            predmet.Oblast.Obnov();
        }

        if (oznamit)
        {
            DokumentZmeneny?.Invoke(objektId);
        }
    }

    private void KontrolaWordu()
    {
        foreach (var (id, uprava) in _upravyVoWorde.ToList())
        {
            if (uprava.JeOtvorenyVoWorde)
            {
                uprava.BolOtvoreny = true;
                continue;
            }

            // Word sa spúšťa niekoľko sekúnd – koniec úpravy sa uzná, až keď bol súbor aspoň raz otvorený
            if (!uprava.BolOtvoreny && DateTime.Now - uprava.Spustene < TimeSpan.FromSeconds(90))
            {
                continue;
            }

            uprava.Skontroluj();
            uprava.Dispose();
            _upravyVoWorde.Remove(id);
            _zarchivovaneVoWorde.Remove(id);
            WordSkoncil?.Invoke(id);
            var nazov = NajdiPredmet(id)?.Nazov ?? NajdiOblast(id)?.Nazov ?? "dokument";
            Oznam($"Úprava vo Worde je ukončená – {nazov} je uložený v programe.");
        }

        if (_upravyVoWorde.Count == 0)
        {
            _kontrolaWordu?.Stop();
        }
    }

    /// <summary>Pri zatvorení programu prevezme posledné zmeny z Wordu.</summary>
    public void UkonciUpravyVoWorde()
    {
        foreach (var uprava in _upravyVoWorde.Values.ToList())
        {
            uprava.Skontroluj();
            uprava.Dispose();
        }

        _upravyVoWorde.Clear();
    }

    /// <summary>Zmeny z Wordu, ktoré sa neprevzali (program skončil počas úpravy) – ponúkne ich obnovenie.</summary>
    public void ObnovNeprevzateZWordu()
    {
        var dokumenty = new DokumentService();
        foreach (var (id, cesta, data) in UpravaVoWorde.NajdiNeprevzate(dokumenty))
        {
            var nazov = NajdiPredmet(id)?.Nazov ?? NajdiOblast(id)?.Nazov;
            if (nazov is null)
            {
                continue;
            }

            if (Dialogy.Potvrd($"Našli sa zmeny dokumentu „{nazov}“ z Wordu, ktoré program ešte neprevzal:\n{cesta}\n\nPrevziať ich teraz? (Doterajšia verzia sa zarchivuje.)"))
            {
                try
                {
                    dokumenty.Uloz(id, data, "Pred prevzatím neuložených zmien z Wordu");
                    PoZmeneDokumentu(id, oznamit: true);
                }
                catch (Exception ex)
                {
                    Dialogy.Chyba("Zmeny sa nepodarilo prevziať.", ex);
                    continue;
                }
            }

            try
            {
                System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(cesta)!, true);
            }
            catch (Exception)
            {
                // pracovná kópia zostane, nevadí
            }
        }
    }

    // ---- oznámenie s možnosťou vrátiť späť ----

    /// <summary>Oznámenie v spodnej lište, voliteľne s tlačidlom (predvolene „Vrátiť späť“).</summary>
    public void Oznam(string text, Action? vratitSpat = null, string textAkcie = "Vrátiť späť")
    {
        _casovacOznamenia.Stop();
        Oznamenie = text;
        _vratitSpat = vratitSpat;
        MaVratitSpat = vratitSpat is not null;
        TextAkcie = textAkcie;
        _casovacOznamenia.Start();
    }

    [ObservableProperty]
    private string _textAkcie = "Vrátiť späť";

    [RelayCommand]
    private void VratitSpat()
    {
        var akcia = _vratitSpat;
        _vratitSpat = null;
        _casovacOznamenia.Stop();
        Oznamenie = null;
        akcia?.Invoke();
    }

    [RelayCommand]
    private void ZavrietOznamenie()
    {
        _casovacOznamenia.Stop();
        Oznamenie = null;
        _vratitSpat = null;
    }

    // ---- aktualizácia programu ----

    public string NovaVerziaText => NovaVerzia is null ? "" : $"Je dostupná nová verzia SkVP {NovaVerzia.Verzia}.";

    partial void OnNovaVerziaChanged(DostupnaVerzia? value) => OnPropertyChanged(nameof(NovaVerziaText));

    public async Task SkontrolujAktualizaciuAsync()
    {
        if (!AktualizaciaService.KontrolovatPriSpusteni)
        {
            return;
        }

        try
        {
            NovaVerzia = await AktualizaciaService.SkontrolujAsync();
        }
        catch (Exception)
        {
            // bez internetu sa nová verzia skontroluje nabudúce
        }
    }

    [RelayCommand]
    private void Aktualizovat()
    {
        if (NovaVerzia is null)
        {
            return;
        }

        (Stranka as IUkladaPredOdchodom)?.UlozTeraz();
        new Views.AktualizaciaWindow(NovaVerzia) { Owner = Dialogy.Vlastnik }.ShowDialog();
    }
}
