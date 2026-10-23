using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;
using SkVP.Views;

namespace SkVP.ViewModels;

/// <summary>Predmet v archíve školského roka (riadok v zozname).</summary>
public sealed class PolozkaArchivuVM
{
    public PolozkaArchivuVM(PolozkaArchivuRoka polozka, OblastArchivuVM oblast, bool nova, bool zmenena)
    {
        Polozka = polozka;
        Oblast = oblast;
        Nova = nova;
        Zmenena = zmenena;
    }

    public PolozkaArchivuRoka Polozka { get; }
    public OblastArchivuVM Oblast { get; }
    public string Cislo => Polozka.CisloText;
    public string Nazov => Polozka.Nazov;
    public StavOsnovy Stav => Polozka.Stav;
    public string StavText => StavyOsnovy.Text(Polozka.Stav);
    public bool MaDokument => Polozka.MaDokument;

    /// <summary>Nová oproti predchádzajúcemu archivovanému roku.</summary>
    public bool Nova { get; }

    /// <summary>Iná osnova ako v predchádzajúcom archivovanom roku.</summary>
    public bool Zmenena { get; }

    public bool ZmenenaVRoku => Polozka.ZmenenaVRoku;

    public string Udaje => string.Join("  •  ", new[]
    {
        Polozka.Profilacie is { Length: > 0 } p ? $"Profilácie: {p}" : null,
        Polozka.Platnost is { Length: > 0 } pl ? $"Platnosť: {pl}" : null,
        Polozka.Dlzka > 0 ? (Polozka.Dlzka / 1800.0).ToString("0.0", CultureInfo.GetCultureInfo("sk-SK")) + " NS" : null,
    }.Where(t => t is not null));

    public bool MaUdaje => Udaje.Length > 0;
}

/// <summary>Vzdelávacia oblasť v archíve školského roka.</summary>
public sealed class OblastArchivuVM
{
    public OblastArchivuVM(string nazov, string farba, PolozkaArchivuRoka? charakteristika)
    {
        Nazov = nazov;
        Farba = farba;
        Charakteristika = charakteristika;
    }

    public string Nazov { get; }
    public string Farba { get; }
    public PolozkaArchivuRoka? Charakteristika { get; }
    public bool MaCharakteristiku => Charakteristika is not null;
    public List<PolozkaArchivuVM> Predmety { get; } = new();
    public int Pocet => Predmety.Count;
    public int PocetHotovych => Predmety.Count(p => p.Stav == StavOsnovy.Hotova);
    public double Podiel => Pocet == 0 ? 0 : (double)PocetHotovych / Pocet;
    public string HotoveText => $"{PocetHotovych} z {Pocet} hotových";
}

/// <summary>Dlaždica štatistiky (číslo, popis, doplnok).</summary>
public sealed record DlazdicaStatistiky(string Hodnota, string Popis, string Doplnok, string Farba, string Ikona);

/// <summary>
/// Sekcia Archív: prehliadanie učebných osnov archivovaných za školský rok – zoznam rokov, štatistiky roka (stav osnov,
/// rozsah, zmeny počas roka, úrovne jazykov, porovnanie s predchádzajúcim rokom), predmety po oblastiach a čítanie
/// archivovanej osnovy. Archív sa nedá upravovať – je to záznam, ako osnovy v danom roku vyzerali.
/// </summary>
public sealed partial class ArchivViewModel : ObservableObject
{
    private List<OblastArchivuVM> _vsetkyOblasti = new();

    public ArchivViewModel(MainViewModel hlavny, long? archivId)
    {
        Hlavny = hlavny;
        Obnov(archivId);
    }

    public MainViewModel Hlavny { get; }

    public ObservableCollection<ZaznamArchivuRoka> Archivy { get; } = new();

    public bool MaArchivy => Archivy.Count > 0;
    public bool NemaArchivy => Archivy.Count == 0;

    public IReadOnlyList<string> SkolskeRoky { get; } = ArchivSkolskehoRoka.Roky();

    [ObservableProperty]
    private string _skolskyRok = ArchivSkolskehoRoka.Aktualny();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(JeVybrany), nameof(Nadpis), nameof(PopisArchivu), nameof(MaPrehlad), nameof(BezPrehladu))]
    private ZaznamArchivuRoka? _vybrany;

    public bool JeVybrany => Vybrany is not null;
    public bool MaPrehlad => Vybrany?.MaPrehlad == true;
    public bool BezPrehladu => Vybrany is not null && !Vybrany.MaPrehlad;

    public string Nadpis => Vybrany is null ? "" : $"Učebné osnovy {Vybrany.SkolskyRok}";

    public string PopisArchivu => Vybrany is null ? "" :
        $"Archivované {Vybrany.Vytvorene.ToString("d. MMMM yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))}"
        + (Vybrany.MaPrehlad ? $"  •  predmetov: {Vybrany.Predmetov}  •  dokumentov: {Vybrany.Dokumentov}" : "")
        + (Vybrany.Poznamka is { Length: > 0 } p ? $"  •  {p}" : "");

    [ObservableProperty]
    private StatistikyArchivu? _statistiky;

    public ObservableCollection<DlazdicaStatistiky> Dlazdice { get; } = new();

    public ObservableCollection<OblastArchivuVM> Oblasti { get; } = new();

    [ObservableProperty]
    private PorovnanieRokov? _porovnanie;

    public bool MaPorovnanie => Porovnanie is not null;

    public string TextPorovnania => Porovnanie is not { } p ? "" :
        $"Oproti školskému roku {p.PredchadzajuciRok}: nové predmety {p.NovePredmety}, zmenené osnovy {p.ZmeneneOsnovy}, " +
        $"bez zmeny {p.RovnakeOsnovy}, odstránené predmety {p.OdstranenePredmety}.";

    // šírky úsekov pruhu stavu osnov (hotové, na kontrolu, rozpracované, nezačaté)
    public GridLength SirkaHotove => Sirka(Statistiky?.Hotove);
    public GridLength SirkaNaKontrolu => Sirka(Statistiky?.NaKontrolu);
    public GridLength SirkaRozpracovane => Sirka(Statistiky?.Rozpracovane);
    public GridLength SirkaNezacate => Sirka(Statistiky?.Nezacate);

    private static GridLength Sirka(int? pocet) => new(pocet ?? 0, GridUnitType.Star);

    public string LegendaStavu => Statistiky is not { } s ? "" :
        $"hotové {s.Hotove} ({s.Percento(s.Hotove)} %)   na kontrolu {s.NaKontrolu} ({s.Percento(s.NaKontrolu)} %)   " +
        $"rozpracované {s.Rozpracovane} ({s.Percento(s.Rozpracovane)} %)   nezačaté {s.Nezacate} ({s.Percento(s.Nezacate)} %)";

    [ObservableProperty]
    private string _hladat = "";

    partial void OnHladatChanged(string value) => Filtruj();

    partial void OnVybranyChanged(ZaznamArchivuRoka? value) => NacitajDetail();

    partial void OnPorovnanieChanged(PorovnanieRokov? value)
    {
        OnPropertyChanged(nameof(MaPorovnanie));
        OnPropertyChanged(nameof(TextPorovnania));
    }

    partial void OnStatistikyChanged(StatistikyArchivu? value)
    {
        OnPropertyChanged(nameof(SirkaHotove));
        OnPropertyChanged(nameof(SirkaNaKontrolu));
        OnPropertyChanged(nameof(SirkaRozpracovane));
        OnPropertyChanged(nameof(SirkaNezacate));
        OnPropertyChanged(nameof(LegendaStavu));
    }

    public void Obnov(long? vybrat = null)
    {
        var predtym = vybrat ?? Vybrany?.Id;
        Archivy.Clear();
        foreach (var zaznam in ArchivSkolskehoRoka.Zoznam())
        {
            Archivy.Add(zaznam);
        }

        OnPropertyChanged(nameof(MaArchivy));
        OnPropertyChanged(nameof(NemaArchivy));
        Vybrany = Archivy.FirstOrDefault(a => a.Id == predtym) ?? Archivy.FirstOrDefault();
        if (Vybrany is null)
        {
            NacitajDetail();
        }
    }

    private void NacitajDetail()
    {
        Dlazdice.Clear();
        _vsetkyOblasti = new();
        if (Vybrany is not { } archiv)
        {
            Statistiky = null;
            Porovnanie = null;
            Filtruj();
            return;
        }

        var polozky = ArchivSkolskehoRoka.Polozky(archiv.Id);
        var s = ArchivSkolskehoRoka.Statistiky(polozky);
        Statistiky = s;
        Porovnanie = ArchivSkolskehoRoka.Porovnaj(archiv);

        Dlazdice.Add(new DlazdicaStatistiky(s.Predmety.ToString(CultureInfo.InvariantCulture), "predmetov",
            $"v {s.Oblasti} oblastiach, {s.SOsnovou} s osnovou", "#3B5BDB", ""));
        Dlazdice.Add(new DlazdicaStatistiky($"{s.Percento(s.Hotove)} %", "hotových osnov", $"{s.Hotove} z {s.Predmety}", "#2F9E44", ""));
        Dlazdice.Add(new DlazdicaStatistiky(s.NaKontrolu.ToString(CultureInfo.InvariantCulture), "na kontrolu",
            $"rozpracované {s.Rozpracovane}, nezačaté {s.Nezacate}", "#F08C00", ""));
        Dlazdice.Add(new DlazdicaStatistiky(s.ZmeneneVRoku.ToString(CultureInfo.InvariantCulture), "zmenených v roku",
            "úpravy, importy a obnovy osnov", "#AE3EC9", ""));
        Dlazdice.Add(new DlazdicaStatistiky(s.Normostrany.ToString("0.#", CultureInfo.GetCultureInfo("sk-SK")), "normostrán",
            $"{s.Znakov.ToString("N0", CultureInfo.GetCultureInfo("sk-SK"))} znakov textu", "#1098AD", ""));
        Dlazdice.Add(new DlazdicaStatistiky($"{s.UrovenB1} / {s.UrovenB2}", "jazyky B1 / B2",
            $"profilácie v {s.SProfilaciami}, platnosť v {s.SPlatnostou} osnovách", "#E8590C", ""));

        OblastArchivuVM? oblast = null;
        foreach (var p in polozky)
        {
            if (oblast is null || oblast.Nazov != p.Oblast)
            {
                oblast = new OblastArchivuVM(p.Oblast,
                    p.Farba ?? (p.JeCast ? CastiOsnov.Farba(p.PoradieOblasti == 0 ? TypObjektu.Uvod : TypObjektu.Zaver) : UcebnyPlanService.FarbaOblasti(p.PoradieOblasti - 1)),
                    p.Typ == "charakteristika" ? p : null);
                _vsetkyOblasti.Add(oblast);
            }

            if (p.JeDokumentOsnov)
            {
                var id = p.ObjektId ?? 0;
                oblast.Predmety.Add(new PolozkaArchivuVM(p, oblast, Porovnanie?.Nove.Contains(id) == true, Porovnanie?.Zmenene.Contains(id) == true));
            }
        }

        Filtruj();
    }

    private void Filtruj()
    {
        Oblasti.Clear();
        var kluc = ImportOsnov.Kluc(Hladat ?? "");
        foreach (var oblast in _vsetkyOblasti)
        {
            if (kluc.Length == 0 || ImportOsnov.Kluc(oblast.Nazov).Contains(kluc, StringComparison.Ordinal))
            {
                Oblasti.Add(oblast);
                continue;
            }

            var vyber = new OblastArchivuVM(oblast.Nazov, oblast.Farba, oblast.Charakteristika);
            vyber.Predmety.AddRange(oblast.Predmety.Where(p => ImportOsnov.Kluc(p.Nazov).Contains(kluc, StringComparison.Ordinal)));
            if (vyber.Pocet > 0)
            {
                Oblasti.Add(vyber);
            }
        }
    }

    // ---- akcie ----

    [RelayCommand]
    private void Vybrat(ZaznamArchivuRoka? zaznam)
    {
        if (zaznam is not null)
        {
            Vybrany = zaznam;
        }
    }

    /// <summary>Archivuje celé osnovy za vybraný školský rok a zobrazí ho.</summary>
    [RelayCommand]
    private void ArchivovatRok()
    {
        if (Spusti(SkolskyRok, Hlavny) is { } zaznam)
        {
            Obnov(zaznam.Id);
        }
    }

    /// <summary>Spoločná archivácia školského roka (sekcie Archív a Servis). Vráti záznam, alebo null.</summary>
    public static ZaznamArchivuRoka? Spusti(string skolskyRok, MainViewModel hlavny)
    {
        if (!Dialogy.Potvrd($"Archivovať celé učebné osnovy za školský rok {skolskyRok}?\n\n" +
                            "Všetky osnovy a charakteristiky oblastí sa uložia do archívu v programe (sekcia Archív – prehľad so štatistikami roka), " +
                            $"do archívu verzií a do priečinka {ArchivSkolskehoRoka.Priecinok} (Word po oblastiach a celé osnovy v archívnom PDF/A). " +
                            "Osnovy sa nemenia."))
        {
            return null;
        }

        try
        {
            ZaznamArchivuRoka? zaznam = null;
            Dialogy.Cakaj(() => zaznam = ArchivSkolskehoRoka.Archivuj(skolskyRok));
            hlavny.Oznam($"Osnovy školského roka {skolskyRok} sú archivované ({zaznam!.Predmetov} predmetov, {zaznam.Dokumentov} dokumentov).");
            return zaznam;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SkvpException)
        {
            Dialogy.Chyba("Osnovy sa nepodarilo archivovať.", ex);
            return null;
        }
    }

    /// <summary>Okno s archivovanou osnovou (len na čítanie); v tichom režime samotestu sa okno len vytvorí.</summary>
    public ArchivnaOsnovaWindow? OknoOsnovy(PolozkaArchivuRoka polozka, string farba)
    {
        if (Vybrany is null || ArchivSkolskehoRoka.Data(polozka.Id) is not { Length: > 0 } data)
        {
            Dialogy.Upozornenie("Táto osnova v danom školskom roku nemala dokument.");
            return null;
        }

        var nazov = polozka.JeDokumentOsnov
            ? InfoOsnovyService.NazovDokumentu(polozka.CisloText, polozka.Nazov, polozka.RokOsnovy)
            : $"Charakteristika – {polozka.Oblast}";
        return new ArchivnaOsnovaWindow(data, nazov, Vybrany.SkolskyRok, polozka.Oblast, farba,
            string.Join("  •  ", new[] { polozka.JeDokumentOsnov ? StavyOsnovy.Text(polozka.Stav) : null, polozka.Profilacie is { Length: > 0 } p ? $"Profilácie: {p}" : null,
                polozka.Platnost is { Length: > 0 } pl ? $"Platnosť: {pl}" : null }.Where(t => t is not null)))
        {
            Owner = Dialogy.Vlastnik,
        };
    }

    [RelayCommand]
    private void OtvoritOsnovu(PolozkaArchivuVM? polozka)
    {
        if (polozka is not null && OknoOsnovy(polozka.Polozka, polozka.Oblast.Farba) is { } okno && !Dialogy.TichyRezim)
        {
            okno.ShowDialog();
        }
    }

    [RelayCommand]
    private void OtvoritCharakteristiku(OblastArchivuVM? oblast)
    {
        if (oblast?.Charakteristika is { } c && OknoOsnovy(c, oblast.Farba) is { } okno && !Dialogy.TichyRezim)
        {
            okno.ShowDialog();
        }
    }

    [RelayCommand]
    private void OtvoritPriecinok()
    {
        if (Vybrany?.Priecinok is { } p && Directory.Exists(p))
        {
            Dialogy.OtvorVSysteme(p);
        }
        else
        {
            Dialogy.Upozornenie("Priečinok archívu sa nenašiel (bol presunutý alebo zmazaný). Osnovy sú však uložené aj v programe.");
        }
    }

    /// <summary>Náhľad a tlač celých osnov roka zo snímky v programe (okno tlače ako v Tlač a zostavy).</summary>
    [RelayCommand]
    private void NahladRoka()
    {
        if (Vybrany is not { MaPrehlad: true } v)
        {
            return;
        }

        MigraDoc.Rendering.DocumentRenderer? vykreslenie = null;
        Dialogy.Cakaj(() => vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(ArchivSkolskehoRoka.Zostava(v))));
        if (!Dialogy.TichyRezim)
        {
            NahladDokumentu.Vytlac(vykreslenie!, $"Učebné osnovy {v.SkolskyRok}", () => PdfVystup.Vytvor(ArchivSkolskehoRoka.Zostava(v)));
        }
    }

    /// <summary>Celé osnovy roka zo snímky v programe do PDF (PDF/A, obrázky…) alebo Wordu.</summary>
    [RelayCommand]
    private void UlozitRok(string? format)
    {
        if (Vybrany is not { MaPrehlad: true } v)
        {
            return;
        }

        var nazov = ImportOsnov.BezpecnyNazov($"Učebné osnovy {v.SkolskyRok.Replace('/', '-')}");
        try
        {
            if (format == "docx")
            {
                var cesta = Dialogy.UlozitAko("Uložiť osnovy roka do Wordu", "Dokument Word (*.docx)|*.docx", nazov + ".docx");
                if (cesta is not null)
                {
                    Dialogy.Cakaj(() => DocxVystup.Uloz(ArchivSkolskehoRoka.Zostava(v), cesta));
                    Hlavny.Oznam($"Uložené: {Path.GetFileName(cesta)}");
                    Dialogy.OtvorVSysteme(cesta);
                }

                return;
            }

            if (Dialogy.TichyRezim)
            {
                if (Dialogy.UlozitAko("PDF", "PDF|*.pdf", nazov + ".pdf") is { } pdf)
                {
                    SuboryZostavy.Uloz(PdfVystup.Vytvor(ArchivSkolskehoRoka.Zostava(v)), pdf, new VolbyPdf());
                }

                return;
            }

            MigraDoc.Rendering.DocumentRenderer? vykreslenie = null;
            Dialogy.Cakaj(() => vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(ArchivSkolskehoRoka.Zostava(v))));
            new TlacWindow(vykreslenie, false, $"Učebné osnovy {v.SkolskyRok}", RezimOknaTlace.Pdf,
                () => PdfVystup.Vytvor(ArchivSkolskehoRoka.Zostava(v)), nazov + ".pdf") { Owner = Dialogy.Vlastnik }.ShowDialog();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SkvpException)
        {
            Dialogy.Chyba("Osnovy roka sa nepodarilo uložiť.", ex);
        }
    }

    [RelayCommand]
    private void OtvoritPdf()
    {
        if (Vybrany is { } v && ArchivSkolskehoRoka.CelePdf(v) is { } pdf)
        {
            Dialogy.OtvorVSysteme(pdf);
        }
        else
        {
            Dialogy.Upozornenie("PDF celých osnov sa v priečinku archívu nenašlo.");
        }
    }

    /// <summary>Štatistiky a zoznam predmetov roka do Excelu (napr. pre vedenie školy).</summary>
    [RelayCommand]
    private void ExportovatStatistiky()
    {
        if (Vybrany is not { } v)
        {
            return;
        }

        var cesta = Dialogy.UlozitAko("Uložiť štatistiky archívu", "Zošit Excel (*.xlsx)|*.xlsx",
            ImportOsnov.BezpecnyNazov($"Štatistiky osnov {v.SkolskyRok.Replace('/', '-')}") + ".xlsx");
        if (cesta is null)
        {
            return;
        }

        try
        {
            ArchivSkolskehoRoka.UlozStatistiky(v, cesta);
            Hlavny.Oznam("Štatistiky archívu sú uložené.");
            Dialogy.OtvorVSysteme(cesta);
        }
        catch (IOException ex)
        {
            Dialogy.Chyba("Štatistiky sa nepodarilo uložiť (nie je súbor otvorený v Exceli?).", ex);
        }
    }

    [RelayCommand]
    private void OdstranitArchiv()
    {
        if (Vybrany is not { } v || !Dialogy.Potvrd(
                $"Odstrániť archív školského roka {v.SkolskyRok} z programu?\n\nPriečinok so súbormi Word a PDF na disku zostane, " +
                "verzie v archíve osnov predmetov tiež. Prehľad a štatistiky tohto roka sa už v sekcii Archív nezobrazia."))
        {
            return;
        }

        ArchivSkolskehoRoka.Odstran(v.Id);
        Vybrany = null;
        Obnov();
    }
}
