using System.Globalization;
using System.IO;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Document = MigraDoc.DocumentObjectModel.Document;
using MigraDoc.Rendering;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>Na čo sa okno tlače používa.</summary>
public enum RezimOknaTlace
{
    Tlac,

    /// <summary>Uloženie zostavy do súboru (PDF, PDF/A, PDF/X, obrázky) so zabezpečením PDF a náhľadom.</summary>
    Pdf,

    /// <summary>Len nastavenia PDF bez zostavy (napr. pre export v časti Import a export).</summary>
    NastaveniaPdf,
}

/// <summary>
/// Okno tlače SkVP s náhľadom listu: tlačiareň, kópie, obojstranná tlač, 1/2/4/8 strán na list, opakovanie strán,
/// brožúra, zväčšenie, rámik, odtiene sivej, zosvetlenie, bez pozadia a dierovač. V režime súboru ukladá PDF, PDF/A-2b,
/// PDF/A-3b, PDF/X-4 a obrázky PNG, JPEG a TIFF. Voľby sa zapamätajú.
/// </summary>
public partial class TlacWindow : Window
{
    /// <summary>Kľúč nastavenia: priečinok, kam sa naposledy ukladalo.</summary>
    private const string KlucPriecinok = "tlac.priecinok";

    /// <summary>Rozlíšenie strán v odtieňoch sivej v náhľade (stačí menšie – je rýchlejší).</summary>
    private const double DpiNahladu = 110;

    private readonly DocumentRenderer? _vykreslenie;
    private readonly bool _stranyNaSirku;
    private readonly string _nazovUlohy;
    private readonly RezimOknaTlace _rezim;
    private readonly Func<Document>? _tvorba;
    private readonly string? _nazovSuboru;
    private readonly (RadioButton Volba, Rozmiestnenie Hodnota)[] _rozmiestnenia;
    private readonly DispatcherTimer _oneskorenie = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private DocumentRenderer? _vykreslenieBezPozadia;
    private ListyTlace? _listy;
    private int _list;
    private bool _nacitavam = true;

    /// <param name="vykreslenie">pripravená zostava pre náhľad; null = len nastavenia PDF</param>
    /// <param name="tvorba">nová zostava (na uloženie do súboru a pre voľbu Bez pozadia)</param>
    public TlacWindow(DocumentRenderer? vykreslenie, bool naSirku, string nazovUlohy, RezimOknaTlace rezim, Func<Document>? tvorba, string? nazovSuboru)
    {
        _vykreslenie = vykreslenie;
        _stranyNaSirku = naSirku;
        _nazovUlohy = nazovUlohy;
        _rezim = rezim;
        _tvorba = tvorba;
        _nazovSuboru = nazovSuboru;
        InitializeComponent();
        _rozmiestnenia = new[]
        {
            (Jedna, Rozmiestnenie.Jedna), (Dve, Rozmiestnenie.Dve), (Styri, Rozmiestnenie.Styri), (Osem, Rozmiestnenie.Osem),
            (Brozura, Rozmiestnenie.Brozura),
        };

        Title = rezim switch
        {
            RezimOknaTlace.Pdf => Preklad.F($"Uložiť ako PDF – {nazovUlohy}"),
            RezimOknaTlace.NastaveniaPdf => Preklad.T("Nastavenia PDF"),
            _ => Preklad.F($"Vytlačiť – {nazovUlohy}"),
        };
        if (rezim != RezimOknaTlace.Tlac)
        {
            SekciaTlaciarne.Visibility = Visibility.Collapsed;
            SekciaPdf.Visibility = Visibility.Visible;
            IkonaHlavneho.Text = "";
            TextHlavneho.Text = rezim == RezimOknaTlace.Pdf ? Preklad.T("Uložiť PDF…") : Preklad.T("Uložiť nastavenia");
        }

        if (vykreslenie is null)
        {
            UsporiadajBezNahladu();
        }

        _oneskorenie.Tick += (_, _) =>
        {
            _oneskorenie.Stop();
            Obnov();
        };
        NacitajVolby();
        _nacitavam = false;
        Obnov();
    }

    /// <summary>Príloha PDF/A-3b (dokument Word s rovnakým obsahom).</summary>
    public Func<PrilohaPdf?>? Priloha { get; init; }

    /// <summary>Počet strán listov, ktoré sa vytlačia (aj pre samotest).</summary>
    public int PocetListov => _listy?.Pocet ?? 0;

    /// <summary>Nastavenie volieb z kódu (samotest).</summary>
    public void NastavVolby(VolbyTlace volby)
    {
        _nacitavam = true;
        ZobrazVolby(volby);
        _nacitavam = false;
        Obnov();
    }

    /// <summary>Strana listu ako obrázok (náhľad, samotest); null = taká strana nie je.</summary>
    public DrawingImage? ObrazokListu(int index)
    {
        if (_listy is null || index < 0 || index >= _listy.Pocet)
        {
            return null;
        }

        var obrazok = new DrawingImage(_listy.List(index).Drawing);
        obrazok.Freeze();
        return obrazok;
    }

    // ---- voľby v okne ----

    /// <summary>Voľby rozmiestnenia a tlače z okna (opravené do povoleného rozsahu).</summary>
    private VolbyTlace Volby => new VolbyTlace
    {
        Tlaciaren = (Tlaciarne.SelectedItem as ComboBoxItem)?.Tag as string,
        Kopie = int.TryParse(Kopie.Text.Trim(), out var kopie) ? kopie : 1,
        Obojstranne = Zapnute(Obojstranne),
        Rozmiestnenie = _rozmiestnenia.Where(r => Zapnute(r.Volba)).Select(r => r.Hodnota).DefaultIfEmpty(Rozmiestnenie.Jedna).First(),
        OpakovatStranky = Zapnute(Opakovat),
        Zvacsenie = Zvacsenie.Value / 100.0,
        Ramcek = Zapnute(Ramcek),
        OdtieneSivej = Zapnute(Siva),
        Zosvetlit = Zapnute(Zosvetlit),
        BezPozadia = Zapnute(BezPozadia),
        Dierovac = Zapnute(Dierovac),
        DierovacMm = double.TryParse(DierovacMm.Text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var mm) ? mm : 10,
    }.Opravene();

    /// <summary>Voľby súboru z okna (formát, zabezpečenie) s rozmiestnením strán bez tlačiarne a kópií.</summary>
    private VolbyPdf VolbyPdfZOkna => new()
    {
        Format = (FormatSuboru.SelectedItem as ComboBoxItem)?.Tag is string tag && Enum.TryParse<FormatVystupu>(tag, out var format) ? format : FormatVystupu.Pdf,
        Rozvrh = Volby with { Tlaciaren = null, Kopie = 1 },
        Sifrovat = Zapnute(Sifrovat),
        PouzitHeslo = Zapnute(PouzitHeslo),
        ZakazTlace = Zapnute(ZakazTlace),
        ZakazKopirovania = Zapnute(ZakazKopirovania),
        ZakazUprav = Zapnute(ZakazUprav),
        ZakazPoznamok = Zapnute(ZakazPoznamok),
    };

    private static bool Zapnute(ToggleButton prepinac) => prepinac.IsChecked == true;

    private void NacitajVolby()
    {
        VolbyTlace volby;
        if (_rezim == RezimOknaTlace.Tlac)
        {
            volby = VolbyTlace.Nacitaj();
            NaplnTlaciarne(volby.Tlaciaren);
        }
        else
        {
            var pdf = VolbyPdf.Nacitaj();
            FormatSuboru.SelectedItem = FormatSuboru.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == pdf.Format.ToString())
                                        ?? FormatSuboru.Items[0];
            Sifrovat.IsChecked = pdf.Sifrovat;
            PouzitHeslo.IsChecked = pdf.PouzitHeslo;
            ZakazTlace.IsChecked = pdf.ZakazTlace;
            ZakazKopirovania.IsChecked = pdf.ZakazKopirovania;
            ZakazUprav.IsChecked = pdf.ZakazUprav;
            ZakazPoznamok.IsChecked = pdf.ZakazPoznamok;
            HesloPole.Password = VolbyPdf.Heslo ?? "";
            HesloVlastnikaPole.Password = VolbyPdf.HesloVlastnika ?? "";
            volby = pdf.Rozvrh;
        }

        Kopie.Text = volby.Kopie.ToString(CultureInfo.InvariantCulture);
        ZobrazVolby(volby);
    }

    /// <summary>Rozmiestnenie a úprava strán do ovládacích prvkov.</summary>
    private void ZobrazVolby(VolbyTlace volby)
    {
        Obojstranne.IsChecked = volby.Obojstranne;
        (_rozmiestnenia.FirstOrDefault(r => r.Hodnota == volby.Rozmiestnenie).Volba ?? Jedna).IsChecked = true;
        Opakovat.IsChecked = volby.OpakovatStranky;
        Zvacsenie.Value = Math.Round(volby.Zvacsenie * 100);
        Ramcek.IsChecked = volby.Ramcek;
        Siva.IsChecked = volby.OdtieneSivej;
        Zosvetlit.IsChecked = volby.Zosvetlit;
        BezPozadia.IsChecked = volby.BezPozadia;
        Dierovac.IsChecked = volby.Dierovac;
        DierovacMm.Text = volby.DierovacMm.ToString("0.#", CultureInfo.CurrentCulture);
    }

    private void NaplnTlaciarne(string? ulozena)
    {
        var (nazvy, predvolena) = TlacoveFronty.Zoznam();
        var polozky = nazvy.Select(nazov => new ComboBoxItem
        {
            Content = nazov == predvolena ? Preklad.F($"{nazov} (predvolená)") : nazov,
            Tag = nazov,
        }).ToList();
        polozky.ForEach(polozka => Tlaciarne.Items.Add(polozka));
        Tlaciarne.SelectedItem = polozky.Find(p => (string)p.Tag == ulozena) ?? polozky.Find(p => (string)p.Tag == predvolena) ?? polozky.FirstOrDefault();
    }

    /// <summary>Len nastavenia (bez náhľadu): užšie okno, voľby na celú šírku a súhrn nad tlačidlami.</summary>
    private void UsporiadajBezNahladu()
    {
        SekciaNahladu.Visibility = Visibility.Collapsed;
        PanelVolieb.Width = double.NaN;
        DockPanel.SetDock(PanelVolieb, Dock.Top);
        DockPanel.SetDock(Tlacidla, Dock.Bottom);
        Tlacidla.HorizontalAlignment = HorizontalAlignment.Right;
        Suhrn.Margin = new Thickness(0, 0, 0, 10);
        (MinWidth, MinHeight, Width, Height) = (360, 420, 440, 760);
    }

    private void Volba_Changed(object sender, RoutedEventArgs e)
    {
        if (!_nacitavam)
        {
            _oneskorenie.Stop();
            _oneskorenie.Start();
        }
    }

    private void Zvacsenie_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ZvacsenieText is not null)
        {
            ZvacsenieText.Text = $"{Zvacsenie.Value:0} %";
        }

        Volba_Changed(sender, e);
    }

    // ---- náhľad a súhrn ----

    /// <summary>Prispôsobí okno zmeneným voľbám: dostupné voľby, popisy, náhľad listu a súhrn.</summary>
    private void Obnov()
    {
        var volby = Volby;
        var mriezka = volby.Rozmiestnenie is not (Rozmiestnenie.Jedna or Rozmiestnenie.Brozura);
        var brozura = volby.Rozmiestnenie == Rozmiestnenie.Brozura;
        Opakovat.IsEnabled = mriezka;
        Obojstranne.IsEnabled = !brozura;
        Dierovac.IsEnabled = !brozura;
        DierovacMm.IsEnabled = !brozura && Zapnute(Dierovac);
        BezPozadia.IsEnabled = _tvorba is not null || _vykreslenie is null;
        HesloPole.IsEnabled = Zapnute(PouzitHeslo);

        var subor = _rezim == RezimOknaTlace.Tlac ? null : VolbyPdfZOkna;
        if (subor is null)
        {
            ZobrazProfil();
        }
        else
        {
            Zabezpecenie.Visibility = subor.MozeZabezpecit ? Visibility.Visible : Visibility.Collapsed;
            InfoPisma.Visibility = subor.JePdf ? Visibility.Visible : Visibility.Collapsed;
            PopisFormatu.Text = subor.Format.Popis();
            if (_rezim == RezimOknaTlace.Pdf)
            {
                TextHlavneho.Text = subor.JePdf ? Preklad.T("Uložiť PDF…") : Preklad.T("Uložiť obrázky…");
            }
        }

        if (_vykreslenie is null)
        {
            Suhrn.Text = Preklad.T("Nastavenia platia pre každé PDF zo SkVP (zostavy, export).");
            return;
        }

        _listy = new ListyTlace(Vykreslenie(volby), _stranyNaSirku, volby, DpiNahladu);
        _list = Math.Clamp(_list, 0, Math.Max(0, _listy.Pocet - 1));
        UkazList();
        Suhrn.Text = TextSuhrnu(_listy, volby, subor);
    }

    /// <summary>Napr. „12 strán zostavy → 6 listov papiera × 2 · obojstranne · A4 na šírku“.</summary>
    private static string TextSuhrnu(ListyTlace listy, VolbyTlace volby, VolbyPdf? subor)
    {
        var text = Preklad.Pocet(listy.Impozicia.PocetStran, "{0} strana zostavy", "{0} strany zostavy", "{0} strán zostavy") + " → ";
        if (subor is not null)
        {
            return text
                   + (subor.JePdf
                       ? Preklad.Pocet(listy.Pocet, "{0} strana PDF", "{0} strany PDF", "{0} strán PDF")
                       : Preklad.Pocet(listy.Pocet, "{0} obrázok", "{0} obrázky", "{0} obrázkov"))
                   + (subor.Format != FormatVystupu.Pdf ? " · " + subor.Format.Nazov() : "")
                   + (subor.Zasifrovat ? " · " + Preklad.T("zašifrované") : "")
                   + (subor.SHeslom ? " · " + Preklad.T("s heslom") : "");
        }

        var obojstranne = volby.TlacitObojstranne;
        return text
               + Preklad.Pocet(obojstranne ? (listy.Pocet + 1) / 2 : listy.Pocet, "{0} list papiera", "{0} listy papiera", "{0} listov papiera")
               + (volby.Kopie > 1 ? $" × {volby.Kopie}" : "")
               + (obojstranne ? " · " + Preklad.T("obojstranne") : "")
               + " · A4 " + (listy.NaSirku ? Preklad.T("na šírku") : Preklad.T("na výšku"));
    }

    /// <summary>Zostava na tlač; pri voľbe Bez pozadia sa (ak sa dá) pripraví znova bez farebných podkladov.</summary>
    private DocumentRenderer Vykreslenie(VolbyTlace volby) =>
        volby.BezPozadia && _tvorba is not null
            ? _vykreslenieBezPozadia ??= NahladDokumentu.Priprav(PozadieDokumentu.Odstran(_tvorba()))
            : _vykreslenie!;

    private void UkazList()
    {
        var obrazok = ObrazokListu(_list);
        Nahlad.Source = obrazok;
        if (_listy is null || obrazok is null)
        {
            CisloListu.Text = "";
            Predosly.IsEnabled = Dalsi.IsEnabled = false;
            return;
        }

        Nahlad.Width = _listy.Velkost.Width;
        Nahlad.Height = _listy.Velkost.Height;
        CisloListu.Text = _listy.Impozicia.Volby.TlacitObojstranne
            ? Preklad.F($"list {_list / 2 + 1} z {(_listy.Pocet + 1) / 2}, {(_list % 2 == 0 ? Preklad.T("predná strana") : Preklad.T("zadná strana"))}")
            : Preklad.F($"list {_list + 1} z {_listy.Pocet}");
        Predosly.IsEnabled = _list > 0;
        Dalsi.IsEnabled = _list < _listy.Pocet - 1;
    }

    private void Predosly_Click(object sender, RoutedEventArgs e) => Listuj(-1);

    private void Dalsi_Click(object sender, RoutedEventArgs e) => Listuj(+1);

    private void Listuj(int smer)
    {
        _list = Math.Clamp(_list + smer, 0, Math.Max(0, PocetListov - 1));
        UkazList();
    }

    // ---- obojstranná tlač ----

    private void ZobrazProfil() =>
        ProfilText.Text = ProfilObojstrannej.Pre(Volby.Tlaciaren) is { } profil
            ? Preklad.F($"Obojstranne: {profil.Popis}")
            : Preklad.T("Obojstranná tlač na tejto tlačiarni ešte nie je nastavená.");

    private void NastavitObojstranne_Click(object sender, RoutedEventArgs e)
    {
        if (Volby.Tlaciaren is not { } tlaciaren)
        {
            Dialogy.Upozornenie(Preklad.T("Nie je nainštalovaná žiadna tlačiareň."));
            return;
        }

        new ObojstrannaWindow(tlaciaren) { Owner = this }.ShowDialog();
        ZobrazProfil();
    }

    /// <summary>
    /// Profil obojstrannej tlače (jednostranne null). Pri prvom použití tlačiarne ponúkne sprievodcu so skúšobnými
    /// listami; false = sprievodca sa nedokončil a tlačiť sa nemá.
    /// </summary>
    private bool ZistiProfil(VolbyTlace volby, out ProfilObojstrannej? profil)
    {
        profil = volby.TlacitObojstranne ? ProfilObojstrannej.Pre(volby.Tlaciaren) : null;
        if (!volby.TlacitObojstranne || profil is not null)
        {
            return true;
        }

        if (Dialogy.Potvrd(Preklad.F($"Obojstranná tlač na tlačiarni „{volby.Tlaciaren}“ ešte nie je nastavená. Každá tlačiareň otáča list inak a zadná strana by mohla vyjsť hore nohami.\n\nSpustiť sprievodcu? Vytlačí 2 skúšobné listy a opýta sa, ako vyšli.\n\n(Nie = tlačiť s predvoleným nastavením.)")))
        {
            var sprievodca = new ObojstrannaWindow(volby.Tlaciaren!) { Owner = this };
            sprievodca.ShowDialog();
            ZobrazProfil();
            if (sprievodca.Profil is null)
            {
                return false;
            }

            profil = sprievodca.Profil;
        }

        profil ??= new ProfilObojstrannej();
        return true;
    }

    // ---- hlavné tlačidlo ----

    private void Vytlacit_Click(object sender, RoutedEventArgs e)
    {
        if (_rezim != RezimOknaTlace.Tlac)
        {
            if (PotvrdVolbyPdf() is { } pdf && (_rezim == RezimOknaTlace.NastaveniaPdf || _tvorba is null || UlozSubor(pdf) is not null))
            {
                DialogResult = true;
            }

            return;
        }

        var volby = Volby;
        if (volby.Tlaciaren is null)
        {
            Dialogy.Upozornenie(Preklad.T("Nie je nainštalovaná žiadna tlačiareň."));
            return;
        }

        try
        {
            if (ZistiProfil(volby, out var profil) && Vytlac(volby, profil))
            {
                volby.Uloz();
                DialogResult = true;
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
        catch (Exception ex)
        {
            Dialogy.Chyba(Preklad.F($"Tlač na „{volby.Tlaciaren}“ sa nepodarila."), ex);
        }
    }

    /// <summary>
    /// Pošle listy na tlačiareň podľa plánu tlačových úloh. Pri ručnom otáčaní sa pred zadnými stranami počká na vloženie
    /// listov; false = používateľ tlač zadných strán zrušil.
    /// </summary>
    private bool Vytlac(VolbyTlace volby, ProfilObojstrannej? profil)
    {
        var listy = new ListyTlace(Vykreslenie(volby), _stranyNaSirku, volby);
        var ulohy = PlanTlace.Ulohy(listy.Pocet, volby.Kopie, volby.TlacitObojstranne, listy.NaSirku, profil);
        using var server = new LocalPrintServer();
        using var fronta = TlacoveFronty.Otvor(server, volby.Tlaciaren!);
        foreach (var (uloha, poradie) in ulohy.Select((uloha, i) => (uloha, i)))
        {
            if (poradie > 0 && !Dialogy.Potvrd(Preklad.F($"Predné strany ({uloha.Strany.Count} listov) sa tlačia.\n\nKeď sú vytlačené, vezmite listy z tlačiarne, nemeňte ich poradie a vložte ich do zásobníka tak, ako pri skúšobných listoch (čistou stranou na tlač). Potom pokračujte – vytlačia sa zadné strany.")))
            {
                return false;
            }

            var strany = uloha.Strany.Select(strana => (Func<Visual>)(() => Kresba(listy, strana))).ToArray();
            var tiket = TlacoveFronty.Tiket(fronta, listy.NaSirku, uloha.Duplex, uloha.Kopie, volby.OdtieneSivej);
            var nazov = poradie == 0 ? _nazovUlohy : _nazovUlohy + " – " + Preklad.T("zadné strany");
            Dialogy.Cakaj(() => TlacoveFronty.Posli(fronta, tiket, listy.Velkost, strany, nazov));
        }

        return true;
    }

    private static DrawingVisual Kresba(ListyTlace listy, StranaUlohy strana) => strana.List switch
    {
        null => listy.Prazdny(),
        int index when strana.Otocena => listy.Otoceny(index),
        int index => listy.List(index),
    };

    // ---- uloženie do súboru ----

    /// <summary>Overí heslo, zapamätá heslá (len v pamäti) a uloží voľby súboru. Null = chýba heslo.</summary>
    private VolbyPdf? PotvrdVolbyPdf()
    {
        var volby = VolbyPdfZOkna;
        if (volby.SHeslom && HesloPole.Password.Length == 0)
        {
            Dialogy.Upozornenie(Preklad.T("Zadajte heslo na otvorenie PDF."));
            HesloPole.Focus();
            return null;
        }

        VolbyPdf.Heslo = volby.SHeslom ? HesloPole.Password : null;
        VolbyPdf.HesloVlastnika = HesloVlastnikaPole.Password.Length > 0 ? HesloVlastnikaPole.Password : null;
        volby.Uloz();
        return volby;
    }

    /// <summary>Okno Uložiť ako, uloženie súborov a otvorenie výsledku. Vráti cestu k (prvému) súboru alebo null.</summary>
    private string? UlozSubor(VolbyPdf volby)
    {
        var priecinok = Databaza.Citaj(KlucPriecinok);
        var cesta = Dialogy.UlozitAko(volby.JePdf ? Preklad.T("Uložiť ako PDF") : Preklad.T("Uložiť ako obrázok"), volby.Format.Filter(),
            Path.ChangeExtension(_nazovSuboru ?? "SkVP.pdf", volby.Pripona), Directory.Exists(priecinok) ? priecinok : null);
        if (cesta is null)
        {
            return null;
        }

        try
        {
            IReadOnlyList<string> subory = Array.Empty<string>();
            Dialogy.Cakaj(() => subory = SuboryZostavy.Uloz(_tvorba!(), cesta, volby, Priloha));
            Databaza.Zapis(KlucPriecinok, Path.GetDirectoryName(cesta));
            if (subory.Count == 1)
            {
                Dialogy.OtvorVSysteme(subory[0]);
            }
            else if (subory.Count > 1)
            {
                Dialogy.Info(Preklad.F($"Uložené obrázky: {subory.Count}\n{Path.GetDirectoryName(subory[0])}"));
            }

            return subory.Count > 0 ? subory[0] : cesta;
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            Dialogy.Chyba(Preklad.T("Súbor sa nepodarilo uložiť."), ex);
            return null;
        }
    }
}
