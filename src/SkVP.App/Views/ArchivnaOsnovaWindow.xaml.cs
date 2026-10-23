using System.IO;
using System.Windows;
using System.Windows.Media;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>
/// Osnova z archívu školského roka – len na čítanie, tak ako bola v danom roku. Dá sa otvoriť ako kópia vo Worde
/// alebo uložiť ako súbor; upravuje sa vždy aktuálna osnova v sekcii Osnovy.
/// </summary>
public partial class ArchivnaOsnovaWindow : Window
{
    private readonly byte[] _data;
    private readonly string _nazovSuboru;

    public ArchivnaOsnovaWindow(byte[] data, string nazov, string skolskyRok, string oblast, string farba, string popis)
    {
        _data = data;
        _nazovSuboru = ImportOsnov.BezpecnyNazov($"{nazov} – školský rok {skolskyRok.Replace('/', '-')}");
        InitializeComponent();
        Title = $"{nazov} – archív {skolskyRok}";
        Rok.Text = $"Archív školského roka {skolskyRok}  •  {oblast}";
        Rok.Foreground = FarbaNaStetec.Stetec(farba);
        Pozadie.Color = FarbaNaStetec.Stetec(farba, 0x16).Color;
        Nadpis.Text = nazov;
        Popis.Text = popis;
        Popis.Visibility = popis.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        Editor.Farba = farba;
        using var prud = new MemoryStream(data, false);
        Bloky = DocxCitac.Nacitaj(prud).Bloky;
        Editor.Nacitaj(Bloky);
    }

    /// <summary>Načítaný obsah osnovy (pre samotest).</summary>
    public IReadOnlyList<Blok> Bloky { get; }

    private void Otvorit_Click(object sender, RoutedEventArgs e)
    {
        var priecinok = Path.Combine(Path.GetTempPath(), "SkVP archív");
        Directory.CreateDirectory(priecinok);
        var cesta = Path.Combine(priecinok, _nazovSuboru + ".docx");
        try
        {
            if (File.Exists(cesta))
            {
                File.SetAttributes(cesta, FileAttributes.Normal);
            }

            File.WriteAllBytes(cesta, _data);
            File.SetAttributes(cesta, FileAttributes.ReadOnly);
        }
        catch (IOException)
        {
            // kópia je už otvorená vo Worde – otvorí sa tá istá
        }

        Dialogy.OtvorVSysteme(cesta);
    }

    private void UlozitAko_Click(object sender, RoutedEventArgs e)
    {
        var cesta = Dialogy.UlozitAko("Uložiť archivovanú osnovu", "Dokument Word (*.docx)|*.docx", _nazovSuboru + ".docx");
        if (cesta is not null)
        {
            File.WriteAllBytes(cesta, _data);
        }
    }
}
