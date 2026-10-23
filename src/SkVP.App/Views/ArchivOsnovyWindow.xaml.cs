using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>
/// Archív osnovy predmetu: všetky staršie verzie s rokom archivácie v názve („05 Fyzika – archív 2026“), dátumom a dôvodom.
/// Verziu možno otvoriť, uložiť ako súbor alebo obnoviť (terajšia sa pritom zarchivuje).
/// </summary>
public partial class ArchivOsnovyWindow : Window
{
    public sealed record PolozkaArchivu(VerziaDokumentu Verzia, string Nazov, string Popis);

    private readonly long _objektId;
    private readonly string _cislo;
    private readonly string _nazov;

    public ArchivOsnovyWindow(long objektId, string cislo, string nazov)
    {
        _objektId = objektId;
        _cislo = cislo;
        _nazov = nazov;
        InitializeComponent();
        Nacitaj();
    }

    /// <summary>Obnovená verzia (aby stránka predmetu načítala text znova).</summary>
    public bool Obnovene { get; private set; }

    public int PocetVerzii => Zoznam.Items.Count;

    private void Nacitaj()
    {
        var info = InfoOsnovyService.Nacitaj(_objektId);
        var sk = CultureInfo.GetCultureInfo("sk-SK");
        Nadpis.Text = $"Archív osnovy – {InfoOsnovyService.NazovDokumentu(_cislo, _nazov, info.Rok)}";
        Aktualna.Text = (info.Poznamka.Length > 0 ? info.Poznamka + ". " : "")
                        + "Staršie osnovy sa nikdy nemažú – pred každou zmenou, úpravou alebo importom sa doterajšia verzia uloží sem.";
        Zoznam.ItemsSource = new DokumentService().Verzie(_objektId).Select(v => new PolozkaArchivu(v,
            InfoOsnovyService.NazovArchivu(_cislo, _nazov, v.Vytvorene.Year),
            $"Archivované {v.Vytvorene.ToString("d. M. yyyy H:mm", sk)}"
            + (v.PovodneUpravene is DateTime p ? $"  •  verzia z {p.ToString("d. M. yyyy", sk)}" : "")
            + (v.Dovod is { Length: > 0 } d ? $"\n{d}" : "")
            + $"  •  {Math.Max(1, v.Velkost / 1024)} kB")).ToList();
        if (Zoznam.Items.Count > 0)
        {
            Zoznam.SelectedIndex = 0;
        }

        Zoznam_SelectionChanged(this, null!);
    }

    private PolozkaArchivu? Vybrana => Zoznam.SelectedItem as PolozkaArchivu;

    private void Zoznam_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        Otvorit.IsEnabled = UlozitAko.IsEnabled = Obnovit.IsEnabled = Vybrana is not null;
    }

    private byte[]? Data() => Vybrana is { } v ? new DokumentService().DataVerzie(v.Verzia.Id) : null;

    private void Otvorit_Click(object sender, RoutedEventArgs e)
    {
        if (Vybrana is not { } v || Data() is not { } data)
        {
            return;
        }

        var priecinok = Path.Combine(Path.GetTempPath(), "SkVP archív");
        Directory.CreateDirectory(priecinok);
        var cesta = Path.Combine(priecinok, ImportOsnov.BezpecnyNazov(v.Nazov) + $" ({v.Verzia.Id}).docx");
        if (!File.Exists(cesta))
        {
            File.WriteAllBytes(cesta, data);
            File.SetAttributes(cesta, FileAttributes.ReadOnly);
        }
        Dialogy.OtvorVSysteme(cesta);
    }

    private void UlozitAko_Click(object sender, RoutedEventArgs e)
    {
        if (Vybrana is not { } v || Data() is not { } data)
        {
            return;
        }

        var cesta = Dialogy.UlozitAko("Uložiť archivovanú osnovu", "Dokument Word (*.docx)|*.docx", ImportOsnov.BezpecnyNazov(v.Nazov) + ".docx");
        if (cesta is not null)
        {
            File.WriteAllBytes(cesta, data);
        }
    }

    private void Obnovit_Click(object sender, RoutedEventArgs e)
    {
        if (Vybrana is not { } v || !Dialogy.Potvrd($"Obnoviť „{v.Nazov}“?\n\nTerajšia osnova sa pritom zarchivuje, nič sa nestratí."))
        {
            return;
        }

        new DokumentService().ObnovVerziu(_objektId, v.Verzia.Id);
        Obnovene = true;
        Nacitaj();
    }
}
