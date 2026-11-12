using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Sekcia Import a export (ako v EviTA): export osnov, import osnov z Wordu a PDF, oblasti a predmety z dokumentu.</summary>
public sealed partial class ImportExportViewModel : ObservableObject
{
    public ImportExportViewModel(MainViewModel hlavny)
    {
        Hlavny = hlavny;
    }

    public MainViewModel Hlavny { get; }

    public string Suhrn => $"V programe je {Hlavny.Oblasti.Count} oblastí a {Hlavny.PocetPredmetov} predmetov.";

    public string PriecinokOsnov => ImportOsnov.PriecinokOsnov;

    [RelayCommand]
    private void ExportovatOsnovy() =>
        new Views.ExportOsnovWindow(new ExportOsnovViewModel(Hlavny)) { Owner = Dialogy.Vlastnik }.ShowDialog();

    [RelayCommand]
    private void NastaveniaPdf() =>
        new Views.TlacWindow(null, false, "PDF", Views.RezimOknaTlace.NastaveniaPdf, null, null) { Owner = Dialogy.Vlastnik }.ShowDialog();

    /// <summary>Import osnov z Wordu alebo PDF – do existujúcich alebo nových predmetov.</summary>
    [RelayCommand]
    private void ImportovatOsnovy()
    {
        var subory = Dialogy.OtvoritViac("Osnovy na import", ImportOsnov.FilterSuborov);
        if (subory.Count == 0)
        {
            return;
        }

        var model = new ImportOsnovViewModel(Hlavny);
        model.PridajSubory(subory);
        new Views.ImportOsnovWindow(model) { Owner = Dialogy.Vlastnik }.ShowDialog();
        OnPropertyChanged(nameof(Suhrn));
    }

    /// <summary>Všetky osnovy z vybraného priečinka (Word a PDF) – súbory v priečinku sa len čítajú.</summary>
    [RelayCommand]
    private void ImportovatZPriecinka()
    {
        if (Dialogy.VybratPriecinok("Priečinok s osnovami na import") is not { } priecinok)
        {
            return;
        }

        var model = new ImportOsnovViewModel(Hlavny);
        model.PridajPriecinok(priecinok);
        if (model.Polozky.Count > 0)
        {
            new Views.ImportOsnovWindow(model) { Owner = Dialogy.Vlastnik }.ShowDialog();
        }

        OnPropertyChanged(nameof(Suhrn));
    }

    [RelayCommand]
    private void OtvoritPriecinokOsnov()
    {
        Directory.CreateDirectory(PriecinokOsnov);
        Dialogy.OtvorVSysteme(PriecinokOsnov);
    }

    /// <summary>Oblasti a predmety z dokumentu (Excel, Word, PDF) – program ich vyhľadá a doplní.</summary>
    [RelayCommand]
    private void ImportovatStrukturu()
    {
        var cesta = Dialogy.Otvorit("Dokument so vzdelávacími oblasťami a predmetmi", StrukturaOsnov.FilterImportu);
        if (cesta is null)
        {
            return;
        }

        try
        {
            UcebnyPlan? plan = null;
            Dialogy.Cakaj(() => plan = StrukturaOsnov.NacitajZoSuboru(cesta));
            var ukazka = string.Join("\n", plan!.Oblasti.Take(12).Select(o => $"• {o.Nazov} ({o.Predmety.Count})"))
                         + (plan.Oblasti.Count > 12 ? "\n…" : "");
            var nahradit = Hlavny.Oblasti.Count > 0 && Dialogy.Potvrd(
                $"V dokumente sa našlo {plan.Oblasti.Count} oblastí a {plan.PocetPredmetov} predmetov:\n\n{ukazka}\n\n" +
                "Áno – nahradiť doterajšie oblasti a predmety (pred tým sa urobí záloha).\n" +
                "Nie – doplniť chýbajúce oblasti a predmety k doterajším.");
            PouzitPlan(plan, nahradit, "Z dokumentu");
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Oblasti a predmety sa z dokumentu nepodarilo načítať.", ex);
        }
    }

    /// <summary>Vytvorí oblasti a predmety z rozpoznaného plánu (pred tým záloha).</summary>
    public VysledokImportu PouzitPlan(UcebnyPlan plan, bool nahradit, string zdroj)
    {
        new ZalohaService().Zalohuj();
        var vysledok = UcebnyPlanService.Vytvor(plan, nahradit);
        Hlavny.NacitajOblasti();
        OnPropertyChanged(nameof(Suhrn));
        Hlavny.Oznam($"{zdroj}: {vysledok.NoveOblasti} nových oblastí, {vysledok.NovePredmety} nových predmetov, {vysledok.Preskocene} už v programe boli.");
        return vysledok;
    }

    /// <summary>Oblasti a predmety z webovej stránky (online).</summary>
    [RelayCommand]
    private void ImportovatZWebu() => OtvoritImportStruktury(Views.ImportStrukturyWindow.Rezim.Web, "Z webovej stránky");

    /// <summary>Oblasti a predmety diktovaním (hlasové písanie Windows) alebo napísaným textom.</summary>
    [RelayCommand]
    private void DiktovatStrukturu() => OtvoritImportStruktury(Views.ImportStrukturyWindow.Rezim.Diktovanie, "Z diktovania");

    private void OtvoritImportStruktury(Views.ImportStrukturyWindow.Rezim rezim, string zdroj)
    {
        var okno = new Views.ImportStrukturyWindow(rezim) { Owner = Dialogy.Vlastnik };
        if (okno.ShowDialog() == true && okno.Plan is { } plan)
        {
            try
            {
                PouzitPlan(plan, okno.NahraditDoterajsie, zdroj);
            }
            catch (SkvpException ex)
            {
                Dialogy.Upozornenie(ex.Message);
            }
        }
    }

    /// <summary>Oblasti a predmety do Excelu (dá sa znova naimportovať), Wordu, PDF alebo obrázkov.</summary>
    [RelayCommand]
    private void ExportovatStrukturu(string? format)
    {
        var plan = StrukturaOsnov.Aktualna();
        try
        {
            switch (format)
            {
                case "xlsx":
                {
                    var cesta = Dialogy.UlozitAko("Oblasti a predmety do Excelu", "Tabuľka Excel (*.xlsx)|*.xlsx", "Oblasti_a_predmety.xlsx");
                    if (cesta is not null)
                    {
                        Dialogy.Cakaj(() => StrukturaOsnov.UlozExcel(plan, cesta));
                        Hlavny.Oznam($"Uložené: {Path.GetFileName(cesta)}");
                        Dialogy.OtvorVSysteme(cesta);
                    }

                    break;
                }

                case "json" or "xml" or "html":
                {
                    var (filter, pripona) = format switch
                    {
                        "json" => ("JSON (*.json)|*.json", ".json"),
                        "xml" => ("XML (*.xml)|*.xml", ".xml"),
                        _ => ("Webová stránka HTML (*.html)|*.html", ".html"),
                    };
                    var cesta = Dialogy.UlozitAko($"Oblasti a predmety do {format.ToUpperInvariant()}", filter, "Oblasti_a_predmety" + pripona);
                    if (cesta is not null)
                    {
                        switch (format)
                        {
                            case "json":
                                ImportStruktury.UlozJson(plan, cesta);
                                break;
                            case "xml":
                                ImportStruktury.UlozXml(plan, cesta);
                                break;
                            default:
                                ImportStruktury.UlozHtml(plan, cesta);
                                break;
                        }

                        Hlavny.Oznam($"Uložené: {Path.GetFileName(cesta)}");
                        Dialogy.OtvorVSysteme(cesta);
                    }

                    break;
                }

                case "docx":
                {
                    var cesta = Dialogy.UlozitAko("Oblasti a predmety do Wordu", "Dokument Word (*.docx)|*.docx", "Oblasti_a_predmety.docx");
                    if (cesta is not null)
                    {
                        Dialogy.Cakaj(() => StrukturaOsnov.UlozWord(plan, cesta));
                        Hlavny.Oznam($"Uložené: {Path.GetFileName(cesta)}");
                        Dialogy.OtvorVSysteme(cesta);
                    }

                    break;
                }

                default:
                {
                    // PDF, PDF/A, PDF/X a obrázky – okno s voľbami PDF a náhľadom
                    var vykreslenie = NahladDokumentu.Priprav(StrukturaOsnov.Dokument(plan));
                    if (Dialogy.TichyRezim)
                    {
                        var cesta = Dialogy.UlozitAko("PDF", "PDF|*.pdf", "Oblasti_a_predmety.pdf");
                        if (cesta is not null)
                        {
                            SuboryZostavy.Uloz(StrukturaOsnov.Dokument(plan), cesta, new VolbyPdf());
                        }

                        break;
                    }

                    new Views.TlacWindow(vykreslenie, false, "Oblasti a predmety", Views.RezimOknaTlace.Pdf,
                        () => StrukturaOsnov.Dokument(plan), "Oblasti_a_predmety.pdf")
                    {
                        Owner = Dialogy.Vlastnik,
                        Priloha = () => PrilohaPdf.Word("Oblasti_a_predmety.docx", DocxVystup.Osnova(StrukturaOsnov.Bloky(plan)), "Oblasti a predmety vo Worde"),
                    }.ShowDialog();
                    break;
                }
            }
        }
        catch (IOException ex)
        {
            Dialogy.Chyba("Súbor sa nepodarilo uložiť. Ak je otvorený v inom programe, zatvorte ho.", ex);
        }
    }
}

/// <summary>Predmet s voľbou, či sa exportuje.</summary>
public sealed partial class VolbaPredmetuExportu(PredmetVM predmet) : ObservableObject
{
    public PredmetVM Predmet { get; } = predmet;

    [ObservableProperty]
    private bool _zahrnut = true;
}

/// <summary>Oblasť s predmetmi na výber pri exporte.</summary>
public sealed partial class VolbaOblastiExportu : ObservableObject
{
    private bool _nastavujem;

    public VolbaOblastiExportu(OblastVM oblast)
    {
        Oblast = oblast;
        foreach (var predmet in oblast.Predmety)
        {
            var volba = new VolbaPredmetuExportu(predmet);
            volba.PropertyChanged += (_, _) =>
            {
                if (!_nastavujem)
                {
                    OnPropertyChanged(nameof(Zahrnut));
                }
            };
            Predmety.Add(volba);
        }
    }

    public OblastVM Oblast { get; }

    public ObservableCollection<VolbaPredmetuExportu> Predmety { get; } = new();

    /// <summary>Celá oblasť (true), nič (false), alebo len niektoré predmety (null).</summary>
    public bool? Zahrnut
    {
        get => Predmety.Count == 0 ? true : Predmety.All(p => p.Zahrnut) ? true : Predmety.Any(p => p.Zahrnut) ? null : false;
        set
        {
            _nastavujem = true;
            foreach (var p in Predmety)
            {
                p.Zahrnut = value != false;
            }

            _nastavujem = false;
            OnPropertyChanged();
        }
    }
}

/// <summary>Ako sa osnovy exportujú do súborov.</summary>
public enum SposobExportu
{
    JedenDokument,
    Jednotlivo,

    /// <summary>Rovnaké priečinky, názvy a formáty ako importované súbory.</summary>
    AkoPriImporte,
}

/// <summary>Okno exportu osnov: čo, s čím, ako (jeden dokument / každý predmet zvlášť / ako pri importe) a v akom formáte.</summary>
public sealed partial class ExportOsnovViewModel : ObservableObject
{
    public ExportOsnovViewModel(MainViewModel hlavny)
    {
        Hlavny = hlavny;
        // zapnuté úvodné časti, oblasti a zapnuté záverečné časti učebných osnov
        foreach (var oblast in hlavny.VsetkySkupiny.Where(o => o.Aktivna))
        {
            Oblasti.Add(new VolbaOblastiExportu(oblast));
        }
    }

    public MainViewModel Hlavny { get; }

    public ObservableCollection<VolbaOblastiExportu> Oblasti { get; } = new();

    public IReadOnlyList<KeyValuePair<FormatExportu, string>> Formaty { get; } =
        Enum.GetValues<FormatExportu>().Select(f => new KeyValuePair<FormatExportu, string>(f, ExportOsnov.Nazov(f))).ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JePdf))]
    private FormatExportu _format = FormatExportu.Pdf;

    /// <summary>Výsledok bude PDF (ponúknu sa nastavenia PDF).</summary>
    public bool JePdf => AkoPriImporte ? FormatImportu != FormatPodlaImportu.VsetkoWord : Format != FormatExportu.Word;

    /// <summary>false = všetky osnovy, true = len vybrané oblasti a predmety.</summary>
    [ObservableProperty]
    private bool _len;

    /// <summary>Jeden dokument, každý predmet zvlášť, alebo ako pri importe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JedenDokument), nameof(Jednotlivo), nameof(AkoPriImporte), nameof(InyAkoPriImporte), nameof(JePdf),
        nameof(InfoImportu))]
    private SposobExportu _sposob;

    public bool JedenDokument
    {
        get => Sposob == SposobExportu.JedenDokument;
        set => Nastav(value, SposobExportu.JedenDokument);
    }

    /// <summary>Každý predmet do samostatného súboru „05 Fyzika 2026“ v priečinku oblasti.</summary>
    public bool Jednotlivo
    {
        get => Sposob == SposobExportu.Jednotlivo;
        set => Nastav(value, SposobExportu.Jednotlivo);
    }

    /// <summary>Rovnaké priečinky, názvy a formáty ako importované súbory.</summary>
    public bool AkoPriImporte
    {
        get => Sposob == SposobExportu.AkoPriImporte;
        set => Nastav(value, SposobExportu.AkoPriImporte);
    }

    public bool InyAkoPriImporte => !AkoPriImporte;

    private void Nastav(bool zapnut, SposobExportu sposob)
    {
        if (zapnut)
        {
            Sposob = sposob;
        }
    }

    /// <summary>Formát súborov pri exporte ako pri importe.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormatAkoPovodne), nameof(FormatVsetkoWord), nameof(FormatVsetkoPdf), nameof(JePdf))]
    private FormatPodlaImportu _formatImportu;

    public bool FormatAkoPovodne
    {
        get => FormatImportu == FormatPodlaImportu.AkoPovodne;
        set
        {
            if (value)
            {
                FormatImportu = FormatPodlaImportu.AkoPovodne;
            }
        }
    }

    public bool FormatVsetkoWord
    {
        get => FormatImportu == FormatPodlaImportu.VsetkoWord;
        set
        {
            if (value)
            {
                FormatImportu = FormatPodlaImportu.VsetkoWord;
            }
        }
    }

    public bool FormatVsetkoPdf
    {
        get => FormatImportu == FormatPodlaImportu.VsetkoPdf;
        set
        {
            if (value)
            {
                FormatImportu = FormatPodlaImportu.VsetkoPdf;
            }
        }
    }

    /// <summary>Názvy súborov ako importované (inak nové „05 Fyzika 2026“).</summary>
    [ObservableProperty]
    private bool _povodneNazvy = true;

    /// <summary>Priečinok, z ktorého sa osnovy importovali (pre osnovy importované skôr, než si program pamätal cestu).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PovodnyPriecinokText), nameof(JePovodnyPriecinok))]
    private string? _povodnyPriecinok;

    public string PovodnyPriecinokText => PovodnyPriecinok ?? "nevybraný";

    public bool JePovodnyPriecinok => PovodnyPriecinok is not null;

    /// <summary>Koľko osnov si pamätá, odkiaľ boli importované.</summary>
    public string InfoImportu
    {
        get
        {
            if (!AkoPriImporte)
            {
                return "";
            }

            var predmety = Hlavny.VsetkySkupiny.SelectMany(o => o.Predmety).Select(p => InfoOsnovyService.Nacitaj(p.Id)).ToList();
            var sCestou = predmety.Count(i => i.CestaZdroja is { Length: > 0 });
            var lenNazov = predmety.Count(i => i.CestaZdroja is not { Length: > 0 } && i.Zdroj is { Length: > 0 });
            var bez = predmety.Count - sCestou - lenNazov;
            var casti = new List<string> { $"Priečinok aj názov súboru pozná {sCestou} z {predmety.Count} osnov." };
            if (lenNazov > 0)
            {
                casti.Add(Preklad.Pocet(lenNazov, "Pri {0} osnove importovanej", "Pri {0} osnovách importovaných", "Pri {0} osnovách importovaných")
                          + " v staršej verzii pozná len názov súboru – vyberte pôvodný priečinok importu a program podľa názvu nájde, " +
                          "v ktorom podpriečinku boli (inak pôjdu do priečinka oblasti).");
            }

            if (bez > 0)
            {
                casti.Add(Preklad.Pocet(bez, "{0} osnova nebola importovaná zo súboru – uloží sa", "{0} osnovy neboli importované zo súboru – uložia sa",
                    "{0} osnov nebolo importovaných zo súboru – uložia sa") + " do priečinka oblasti s novým názvom.");
            }

            return string.Join(" ", casti);
        }
    }

    [RelayCommand]
    private void VybratPovodnyPriecinok()
    {
        if (Dialogy.VybratPriecinok("Priečinok, z ktorého ste osnovy importovali (súbory sa len prečítajú)") is { } priecinok)
        {
            PovodnyPriecinok = priecinok;
        }
    }

    [RelayCommand]
    private void ZrusitPovodnyPriecinok() => PovodnyPriecinok = null;

    /// <summary>Údaje o škole v zostave (spoločné s Tlač a zostavy).</summary>
    public UdajeOSkoleVZostaveVM UdajeOSkole { get; } = new();

    [ObservableProperty] private bool _titulnaStrana = true;
    [ObservableProperty] private bool _obsah = true;
    [ObservableProperty] private bool _oddelovacOblasti = true;
    [ObservableProperty] private bool _oddelovacPredmetu;
    [ObservableProperty] private bool _charakteristiky = true;
    [ObservableProperty] private bool _nespracovane = true;

    [ObservableProperty]
    private string _stav = "";

    /// <summary>Výsledné súbory posledného exportu (samotest).</summary>
    public IReadOnlyList<string> Subory { get; private set; } = Array.Empty<string>();

    public VolbyZostavy Volby() => new()
    {
        Oblasti = Len ? Oblasti.Where(o => o.Zahrnut != false).Select(o => o.Oblast.Id).ToList() : null,
        Predmety = Len ? Oblasti.SelectMany(o => o.Predmety).Where(p => p.Zahrnut).Select(p => p.Predmet.Id).ToList() : null,
        TitulnaStrana = TitulnaStrana,
        Obsah = Obsah,
        OddelovacOblasti = OddelovacOblasti,
        OddelovacPredmetu = OddelovacPredmetu,
        CharakteristikyOblasti = Charakteristiky,
        NespracovanePredmety = Nespracovane,
    };

    [RelayCommand]
    private void NastaveniaPdf() =>
        new Views.TlacWindow(null, false, "PDF", Views.RezimOknaTlace.NastaveniaPdf, null, null) { Owner = Dialogy.Vlastnik }.ShowDialog();

    [RelayCommand]
    private void VybratVsetko()
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

    /// <summary>Spustí export. Vráti true, ak sa niečo uložilo.</summary>
    [RelayCommand]
    private void Exportovat() => Exportuj();

    public bool Exportuj()
    {
        var volby = Volby();
        if (Len && volby.Predmety!.Count == 0)
        {
            Dialogy.Upozornenie("Vyberte aspoň jeden predmet.");
            return false;
        }

        try
        {
            IReadOnlyList<string> subory = Array.Empty<string>();
            string? priecinokExportu = null;
            string? doplnok = null;
            if (AkoPriImporte)
            {
                var priecinok = Dialogy.VybratPriecinok("Prázdny priečinok na upravené osnovy (ako pri importe)");
                if (priecinok is null)
                {
                    return false;
                }

                if (Directory.Exists(priecinok) && Directory.EnumerateFileSystemEntries(priecinok).Any()
                    && !Dialogy.Potvrd("Vybraný priečinok nie je prázdny. Súbory v ňom sa neprepíšu – nové súbory s rovnakým názvom dostanú číslo, napr. „Fyzika (2).pdf“.\n\nPokračovať?"))
                {
                    return false;
                }

                VysledokExportuOsnov? vysledok = null;
                Dialogy.Cakaj(() => vysledok = ExportOsnov.UlozAkoPriImporte(volby, FormatImportu, PovodneNazvy, priecinok, PovodnyPriecinok));
                subory = vysledok!.Subory;
                priecinokExportu = priecinok;
                doplnok = vysledok.BezImportu > 0 ? $" ({vysledok.PodlaImportu} podľa importu, {vysledok.BezImportu} v priečinkoch oblastí)" : null;
            }
            else if (Jednotlivo)
            {
                var priecinok = Dialogy.VybratPriecinok("Priečinok na osnovy (každý predmet zvlášť)");
                if (priecinok is null)
                {
                    return false;
                }

                Dialogy.Cakaj(() => subory = ExportOsnov.UlozJednotlivo(volby, Format, priecinok));
            }
            else
            {
                var dokument = ZostavaOsnov.Zostav(volby);
                var cesta = Dialogy.UlozitAko("Export učebných osnov", ExportOsnov.Filter(Format),
                    Path.ChangeExtension(ZostavaOsnov.NazovSuboru(dokument, ".pdf"), ExportOsnov.Pripona(Format)));
                if (cesta is null)
                {
                    return false;
                }

                Dialogy.Cakaj(() => subory = ExportOsnov.Uloz(dokument, Format, cesta));
            }

            Subory = subory;
            Stav = (subory.Count == 1 ? $"Uložené: {subory[0]}" : $"Uložené súbory: {subory.Count}") + doplnok;
            Hlavny.Oznam(subory.Count == 1 ? $"Exportované: {Path.GetFileName(subory[0])}" : $"Exportované súbory: {subory.Count}");
            if (priecinokExportu is not null)
            {
                Dialogy.OtvorVSysteme(priecinokExportu);
            }
            else if (subory.Count == 1)
            {
                Dialogy.OtvorVSysteme(subory[0]);
            }
            else if (subory.Count > 1)
            {
                Dialogy.OtvorVSysteme(Jednotlivo ? Path.GetDirectoryName(Path.GetDirectoryName(subory[0]))! : Path.GetDirectoryName(subory[0])!);
            }

            return subory.Count > 0;
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
        catch (IOException ex)
        {
            Dialogy.Chyba("Súbor sa nepodarilo uložiť. Ak je otvorený v inom programe, zatvorte ho.", ex);
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Export sa nepodaril.", ex);
        }

        return false;
    }
}
