using System.Windows;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>
/// Náhľad jednotného vzhľadu dokumentu pred použitím: prepínanie nový / pôvodný vzhľad, súhrn zmien a upozornenie
/// na prvky, ktoré by sa stratili. Uloženie (s archiváciou pôvodnej verzie) robí volajúci po potvrdení.
/// </summary>
public partial class JednotnyVzhladWindow : Window
{
    private readonly NacitanyDokument _povodny;

    public JednotnyVzhladWindow(NacitanyDokument povodny, string nazov, string farba)
    {
        _povodny = povodny;
        Prepracovany = JednotnyVzhlad.Priprav(povodny);
        InitializeComponent();
        Title = $"Jednotný vzhľad – {nazov}";
        Nadpis.Text = nazov;
        Druh.Foreground = FarbaNaStetec.Text(farba);
        Pozadie.Color = FarbaNaStetec.Stetec(farba, 0x16).Color;
        Editor.Farba = farba;

        var p = Prepracovany;
        var zmeny = new List<string>
        {
            "rovnaké písma, nadpisy a tabuľky ako v osnovách programu",
            Preklad.Pocet(p.Nadpisov, "{0} nadpis časti rozpoznaný", "{0} nadpisy častí rozpoznané", "{0} nadpisov častí rozpoznaných"),
        };
        if (p.HluchychMiest > 0)
        {
            zmeny.Add(Preklad.Pocet(p.HluchychMiest, "{0} hluché miesto vynechané", "{0} hluché miesta vynechané", "{0} hluchých miest vynechaných"));
        }

        zmeny.Add($"strana {p.Rozlozenie.Popis} zostáva");
        Suhrn.Text = string.Join("  •  ", zmeny);
        if (p.Straty.Count > 0)
        {
            Straty.Text = $"Dokument obsahuje prvky, ktoré jednotný vzhľad nemá: {string.Join(", ", p.Straty)}. Po použití by v osnove chýbali "
                          + "(v archíve zostanú). Ak sú dôležité, upravte vzhľad radšej vo Worde.";
            PanelStrat.Visibility = Visibility.Visible;
        }

        Editor.Nacitaj(p.Bloky);
    }

    /// <summary>Dokument v jednotnom vzhľade (na uloženie).</summary>
    public PrepracovanyDokument Prepracovany { get; }

    private void Zobrazenie_Checked(object sender, RoutedEventArgs e)
    {
        if (Editor is null)
        {
            return; // počas vytvárania okna
        }

        Editor.Nacitaj(ZobrazitPovodny.IsChecked == true ? _povodny.Bloky : Prepracovany.Bloky);
    }

    private void Pouzit_Click(object sender, RoutedEventArgs e)
    {
        if (Prepracovany.Straty.Count > 0 && !Dialogy.Potvrd(
                $"Prvky dokumentu ({string.Join(", ", Prepracovany.Straty)}) v jednotnom vzhľade nebudú. Pôvodná verzia zostane v archíve.\n\nPoužiť jednotný vzhľad?"))
        {
            return;
        }

        DialogResult = true;
    }
}
