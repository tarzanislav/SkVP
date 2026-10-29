using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;

namespace SkVP.Pomocne;

/// <summary>
/// Hlásenia, otázky a okná na výber súborov – na jednom mieste. V tichom režime (samotest) sa žiadne okno neotvorí:
/// hlásenie sa zapíše do <see cref="TicheHlasenia"/> a vráti sa pripravená odpoveď.
/// </summary>
public static class Dialogy
{
    private const string Titulok = "SkVP";

    public static bool TichyRezim { get; set; }

    public static List<string> TicheHlasenia { get; } = new();

    /// <summary>Cesta, ktorú v tichom režime vráti okno Uložiť ako (podľa navrhnutého názvu); null = priečinok dokumentov.</summary>
    public static Func<string, string?>? TicheUlozenie { get; set; }

    /// <summary>Súbor, ktorý v tichom režime vráti okno Otvoriť.</summary>
    public static string? TichyVyberSuboru { get; set; }

    /// <summary>Súbory, ktoré v tichom režime vráti okno Otvoriť s viacerými súbormi.</summary>
    public static IReadOnlyList<string>? TicheSubory { get; set; }

    /// <summary>Priečinok, ktorý v tichom režime vráti výber priečinka.</summary>
    public static string? TichyPriecinok { get; set; }

    /// <summary>Okno, ku ktorému patria hlásenia: aktívne okno programu, inak viditeľné hlavné okno.</summary>
    public static Window? Vlastnik =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(okno => okno.IsActive)
        ?? (Application.Current?.MainWindow is { IsVisible: true } hlavne ? hlavne : null);

    public static void Info(string sprava) => Ukaz(sprava, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Upozornenie(string sprava) => Ukaz(sprava, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Chyba(string sprava, Exception? ex = null) =>
        Ukaz(ex is null ? sprava : $"{sprava}\n\nPodrobnosti: {ex.GetBaseException().Message}", MessageBoxButton.OK, MessageBoxImage.Error);

    /// <summary>Otázka Áno/Nie (v tichom režime vždy áno).</summary>
    public static bool Potvrd(string otazka) => Ukaz(otazka, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Okno Uložiť ako. Vráti cestu alebo null.</summary>
    public static string? UlozitAko(string titulok, string filter, string nazov, string? priecinok = null)
    {
        priecinok ??= Data.Databaza.PriecinokDokumentov;
        return VTichu($"[Uložiť] {nazov}", () => TicheUlozenie?.Invoke(nazov) ?? Path.Combine(priecinok, nazov), () =>
        {
            try
            {
                Directory.CreateDirectory(priecinok);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // priečinok si používateľ vyberie sám
            }

            var okno = new SaveFileDialog { Title = titulok, Filter = filter, FileName = nazov, InitialDirectory = priecinok, AddExtension = true, OverwritePrompt = true };
            return Vyber(okno) ? okno.FileName : null;
        });
    }

    /// <summary>Okno Otvoriť. Vráti cestu alebo null.</summary>
    public static string? Otvorit(string titulok, string filter) => VTichu($"[Otvoriť] {titulok}", () => TichyVyberSuboru, () =>
    {
        var okno = new OpenFileDialog { Title = titulok, Filter = filter, CheckFileExists = true };
        return Vyber(okno) ? okno.FileName : null;
    });

    /// <summary>Okno Otvoriť s výberom viacerých súborov (nič nevybraté = prázdny zoznam).</summary>
    public static IReadOnlyList<string> OtvoritViac(string titulok, string filter) =>
        VTichu($"[Otvoriť viac] {titulok}", () => TicheSubory ?? Array.Empty<string>(), () =>
        {
            var okno = new OpenFileDialog { Title = titulok, Filter = filter, CheckFileExists = true, Multiselect = true };
            return Vyber(okno) ? okno.FileNames : Array.Empty<string>();
        });

    /// <summary>Výber priečinka. Vráti cestu alebo null.</summary>
    public static string? VybratPriecinok(string titulok) => VTichu($"[Priečinok] {titulok}", () => TichyPriecinok, () =>
    {
        var okno = new OpenFolderDialog { Title = titulok };
        return Vyber(okno) ? okno.FolderName : null;
    });

    /// <summary>Otvorí súbor alebo priečinok v predvolenom programe Windows.</summary>
    public static void OtvorVSysteme(string cesta) => VTichu($"[Otvoriť v systéme] {cesta}", () => true, () =>
    {
        try
        {
            Process.Start(new ProcessStartInfo(cesta) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            Chyba($"Nepodarilo sa otvoriť:\n{cesta}", ex);
        }

        return true;
    });

    /// <summary>Vykoná akciu s kurzorom „čakajte“.</summary>
    public static void Cakaj(Action akcia)
    {
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            akcia();
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
    }

    private static MessageBoxResult Ukaz(string sprava, MessageBoxButton tlacidla, MessageBoxImage ikona) =>
        VTichu($"[{ikona}] {sprava}", () => tlacidla == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK,
            () => Vlastnik is { } vlastnik ? MessageBox.Show(vlastnik, sprava, Titulok, tlacidla, ikona) : MessageBox.Show(sprava, Titulok, tlacidla, ikona));

    /// <summary>V tichom režime zapíše hlásenie a vráti pripravenú odpoveď, inak ukáže okno.</summary>
    private static T VTichu<T>(string hlasenie, Func<T> tichaOdpoved, Func<T> okno)
    {
        if (!TichyRezim)
        {
            return okno();
        }

        TicheHlasenia.Add(hlasenie);
        return tichaOdpoved();
    }

    private static bool Vyber(CommonDialog okno) => okno.ShowDialog(Vlastnik) == true;
}
