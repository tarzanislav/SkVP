using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>
/// Sprievodca obojstrannou tlačou pre jednu tlačiareň: vytlačí skúšobné listy (číslo, šípka HORE, krúžky dierovača),
/// podľa odpovedí zistí, či tlačiareň otáča list sama, či treba zadnú stranu otočiť o 180° a pri ručnom otáčaní aj
/// poradie listov, a výsledok uloží pre túto tlačiareň.
/// </summary>
public partial class ObojstrannaWindow : Window
{
    private enum Faza
    {
        Uvod,
        OtazkyDuplexu,
        RucnePredne,
        RucneZadne,
        OtazkaRucneho,
        Hotovo,
    }

    /// <summary>Čo fáza ukazuje: texty, možnosti (vybraná je prvá), druhá otázka a nápis hlavného tlačidla.</summary>
    private sealed record Obrazovka(string Nadpis, string Text, string Tlacidlo, string[] Moznosti, string? DruhaOtazka = null, string[]? DruheMoznosti = null);

    private readonly string _tlaciaren;
    private Faza _faza;

    public ObojstrannaWindow(string tlaciaren)
    {
        _tlaciaren = tlaciaren;
        InitializeComponent();
        Tlaciaren.Text = Preklad.F($"Tlačiareň: {tlaciaren}");
        Ukaz(Faza.Uvod);
    }

    /// <summary>Uložené nastavenie (po dokončení sprievodcu).</summary>
    public ProfilObojstrannej? Profil { get; private set; }

    private Obrazovka Obsah(Faza faza) => faza switch
    {
        Faza.Uvod => new(
            Preklad.T("Nastavenie obojstrannej tlače"),
            Preklad.T("Každá tlačiareň otáča list pri obojstrannej tlači trochu inak – zadná strana môže vyjsť hore nohami alebo v inom poradí. Sprievodca vytlačí skúšobné listy s číslom, šípkou HORE a krúžkami ako po dierovači a opýta sa, ako vyšli. SkVP si to zapamätá a pri každej obojstrannej tlači (aj 2 – 8 strán na list a brožúra) zadnú stranu podľa toho upraví."),
            Preklad.T("Vytlačiť skúšobné listy"),
            [Preklad.T("Tlačiareň tlačí obojstranne sama (automatický duplex)"), Preklad.T("Tlačiareň obojstranne netlačí – listy budem otáčať ručne")]),
        Faza.OtazkyDuplexu => new(
            Preklad.T("Ako vyšli skúšobné listy?"),
            Preklad.T("Vytlačili sa 2 listy. Vezmite list s číslom 1 (na výšku) a otočte ho ako stránku v knihe – okolo ľavej hrany. Čo je na zadnej strane?"),
            Preklad.T("Uložiť"),
            [
                Preklad.T("Číslo 2 stojí rovno – šípky na oboch stranách ukazujú hore a krúžky sa proti svetlu prekrývajú"),
                Preklad.T("Číslo 2 je hore nohami"),
                Preklad.T("Zadná strana je prázdna – číslo 2 je na samostatnom liste"),
            ],
            Preklad.T("Teraz list s číslom 3 (na šírku) – otočte ho tiež okolo ľavej (kratšej) hrany. Čo je na zadnej strane?"),
            [Preklad.T("Číslo 4 stojí rovno"), Preklad.T("Číslo 4 je hore nohami"), Preklad.T("Zadná strana je prázdna – číslo 4 je na samostatnom liste")]),
        Faza.RucnePredne => new(
            Preklad.T("Ručné otáčanie listov – 1. časť"),
            Preklad.T("Vytlačia sa predné strany 2 skúšobných listov (čísla 1 a 3)."),
            Preklad.T("Vytlačiť predné strany"),
            []),
        Faza.RucneZadne => new(
            Preklad.T("Ručné otáčanie listov – 2. časť"),
            Preklad.T("Vezmite oba vytlačené listy z tlačiarne, nemeňte ich poradie a vložte ich do zásobníka tak, aby sa tlačilo na druhú, čistú stranu (tak, ako to budete robiť vždy). Potom vytlačte zadné strany."),
            Preklad.T("Vytlačiť zadné strany"),
            []),
        Faza.OtazkaRucneho => new(
            Preklad.T("Ako vyšli skúšobné listy?"),
            Preklad.T("Vezmite list s číslom 1 a otočte ho ako stránku v knihe – okolo ľavej hrany. Čo je na zadnej strane?"),
            Preklad.T("Uložiť"),
            [
                Preklad.T("Číslo 2, stojí rovno"),
                Preklad.T("Číslo 2, ale hore nohami"),
                Preklad.T("Číslo 4, stojí rovno (poradie listov je obrátené)"),
                Preklad.T("Číslo 4, hore nohami"),
                Preklad.T("Nič – zadné strany sa vytlačili na tú istú stranu ako predné (listy treba vložiť obrátene)"),
            ]),
        _ => new(
            Preklad.T("Hotovo"),
            Preklad.F($"Nastavenie obojstrannej tlače pre túto tlačiareň je uložené ({Profil?.Popis}). Môžete ho kedykoľvek zopakovať tlačidlom „Nastaviť…“ v okne tlače."),
            Preklad.T("Zavrieť"),
            []),
    };

    private void Ukaz(Faza faza)
    {
        _faza = faza;
        var obsah = Obsah(faza);
        Nadpis.Text = obsah.Nadpis;
        Text.Text = obsah.Text;
        Hlavne.Content = obsah.Tlacidlo;
        Napln(Moznosti, obsah.Moznosti);
        Napln(Moznosti2, obsah.DruheMoznosti ?? []);
        Moznosti2Nadpis.Text = obsah.DruhaOtazka ?? "";
        Moznosti2Nadpis.Visibility = obsah.DruhaOtazka is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void Napln(Panel panel, IReadOnlyList<string> moznosti)
    {
        panel.Children.Clear();
        for (var i = 0; i < moznosti.Count; i++)
        {
            panel.Children.Add(new RadioButton
            {
                Content = new TextBlock { Text = moznosti[i], TextWrapping = TextWrapping.Wrap },
                IsChecked = i == 0,
                GroupName = panel.Name,
                Margin = new Thickness(0, 3, 0, 3),
            });
        }
    }

    private static int Vybrana(Panel panel) => panel.Children.OfType<RadioButton>().ToList().FindIndex(r => r.IsChecked == true);

    private void Hlavne_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Dalsia() is { } dalsia)
            {
                Ukaz(dalsia);
            }
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
        catch (Exception ex)
        {
            Dialogy.Chyba(Preklad.T("Skúšobné listy sa nepodarilo vytlačiť."), ex);
        }
    }

    /// <summary>Vykoná krok aktuálnej fázy a vráti nasledujúcu (null = sprievodca sa zatvára).</summary>
    private Faza? Dalsia()
    {
        switch (_faza)
        {
            case Faza.Uvod when Vybrana(Moznosti) == 1:
                return Faza.RucnePredne;

            case Faza.Uvod:
                // duplex: list 1/2 na výšku a list 3/4 na šírku, každý ako samostatná obojstranná úloha
                Vytlac(naSirku: false, duplex: true, 1, 2);
                Vytlac(naSirku: true, duplex: true, 3, 4);
                return Faza.OtazkyDuplexu;

            case Faza.OtazkyDuplexu:
                if (ProfilObojstrannej.ZDuplexu(Vybrana(Moznosti), Vybrana(Moznosti2)) is { } duplex)
                {
                    return Uloz(duplex);
                }

                Dialogy.Info(Preklad.T("Tlačiareň obojstranne netlačí (alebo je duplex vypnutý v jej nastavení). Nastavíme ručné otáčanie listov."));
                return Faza.RucnePredne;

            case Faza.RucnePredne:
                Vytlac(naSirku: false, duplex: false, 1, 3);
                return Faza.RucneZadne;

            case Faza.RucneZadne:
                Vytlac(naSirku: false, duplex: false, 2, 4);
                return Faza.OtazkaRucneho;

            case Faza.OtazkaRucneho:
                if (ProfilObojstrannej.ZRucneho(Vybrana(Moznosti)) is { } rucne)
                {
                    return Uloz(rucne);
                }

                Dialogy.Info(Preklad.T("Vložte nabudúce listy do zásobníka opačnou stranou. Skúsime to znova."));
                return Faza.RucnePredne;

            default:
                DialogResult = true;
                return null;
        }
    }

    private Faza Uloz(ProfilObojstrannej profil)
    {
        ProfilObojstrannej.Uloz(_tlaciaren, profil);
        Profil = profil;
        return Faza.Hotovo;
    }

    /// <summary>Skúšobné strany ako jedna tlačová úloha (párne čísla sú zadné strany).</summary>
    private void Vytlac(bool naSirku, bool duplex, params int[] cisla)
    {
        using var server = new LocalPrintServer();
        using var fronta = TlacoveFronty.Otvor(server, _tlaciaren);
        var velkost = Impozicia.A4(naSirku, NahladDokumentu.JednotiekNaMm);
        var strany = cisla.Select(cislo => (Func<Visual>)(() => SkusobnyList.Strana(cislo, velkost))).ToArray();
        Dialogy.Cakaj(() => TlacoveFronty.Posli(fronta, TlacoveFronty.Tiket(fronta, naSirku, duplex), velkost, strany,
            Preklad.T("SkVP – skúšobný list obojstrannej tlače")));
    }
}
