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
            new ZalohaService().Zalohuj();
            var vysledok = UcebnyPlanService.Vytvor(plan, nahradit);
            Hlavny.NacitajOblasti();
            OnPropertyChanged(nameof(Suhrn));
            Hlavny.Oznam($"Z dokumentu: {vysledok.NoveOblasti} nových oblastí, {vysledok.NovePredmety} nových predmetov, {vysledok.Preskocene} už v programe boli.");
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
                        Priloha = () => new SuboryZostavy.Priloha("Oblasti_a_predmety.docx", DocxVystup.Osnova(StrukturaOsnov.Bloky(plan)),
                            "/application/vnd.openxmlformats-officedocument.wordprocessingml.document", "Oblasti a predmety vo Worde"),
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

/// <summary>Okno exportu osnov: čo, s čím, ako (jeden dokument / každý predmet zvlášť) a v akom formáte.</summary>
public sealed partial class ExportOsnovViewModel : ObservableObject
{
    public ExportOsnovViewModel(MainViewModel hlavny)
    {
        Hlavny = hlavny;
        foreach (var oblast in hlavny.Oblasti)
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

    public bool JePdf => Format != FormatExportu.Word;

    /// <summary>false = všetky osnovy, true = len vybrané oblasti a predmety.</summary>
    [ObservableProperty]
    private bool _len;

    /// <summary>Každý predmet do samostatného súboru (inak jeden dokument).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JedenDokument))]
    private bool _jednotlivo;

    public bool JedenDokument => !Jednotlivo;

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
    public bool Exportovat()
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
            if (Jednotlivo)
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
            Stav = subory.Count == 1 ? $"Uložené: {subory[0]}" : $"Uložené súbory: {subory.Count}";
            Hlavny.Oznam(subory.Count == 1 ? $"Exportované: {Path.GetFileName(subory[0])}" : $"Exportované súbory: {subory.Count}");
            if (subory.Count == 1)
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
