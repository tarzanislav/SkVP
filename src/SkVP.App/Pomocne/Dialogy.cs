using System.Windows;
using System.Windows.Input;

namespace SkVP.Pomocne;

/// <summary>Jednotné hlásenia a otázky pre používateľa.</summary>
public static class Dialogy
{
    private const string Titulok = "SkVP";

    /// <summary>V tichom režime (samotest) sa nezobrazuje žiadne okno – hlásenia sa len zapíšu.</summary>
    public static bool TichyRezim { get; set; }

    public static List<string> TicheHlasenia { get; } = new();

    public static Window? Vlastnik
    {
        get
        {
            var aktivne = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
            if (aktivne is not null)
            {
                return aktivne;
            }

            var hlavne = Application.Current?.MainWindow;
            return hlavne is { IsVisible: true } ? hlavne : null;
        }
    }

    public static void Info(string sprava) => Ukaz(sprava, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Upozornenie(string sprava) => Ukaz(sprava, MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Chyba(string sprava, Exception? ex = null)
    {
        var text = ex is null ? sprava : $"{sprava}\n\nPodrobnosti: {ex.GetBaseException().Message}";
        Ukaz(text, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static bool Potvrd(string otazka) =>
        Ukaz(otazka, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Cesta, ktorú v tichom režime (samotest) vráti okno „Uložiť ako“ – null = priečinok exportov.</summary>
    public static Func<string, string?>? TicheUlozenie { get; set; }

    /// <summary>Súbor, ktorý v tichom režime vráti okno „Otvoriť“.</summary>
    public static string? TichyVyberSuboru { get; set; }

    /// <summary>Okno „Uložiť ako“. Vráti cestu alebo null.</summary>
    public static string? UlozitAko(string titulok, string filter, string nazov, string? priecinok = null)
    {
        priecinok ??= Data.Databaza.PriecinokDokumentov;
        if (TichyRezim)
        {
            TicheHlasenia.Add($"[Uložiť] {nazov}");
            return TicheUlozenie?.Invoke(nazov) ?? System.IO.Path.Combine(priecinok, nazov);
        }

        try
        {
            System.IO.Directory.CreateDirectory(priecinok);
        }
        catch (Exception)
        {
            // priečinok si používateľ vyberie sám
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = titulok,
            Filter = filter,
            FileName = nazov,
            InitialDirectory = priecinok,
            AddExtension = true,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog(Vlastnik) == true ? dialog.FileName : null;
    }

    /// <summary>Okno „Otvoriť“. Vráti cestu alebo null.</summary>
    public static string? Otvorit(string titulok, string filter)
    {
        if (TichyRezim)
        {
            TicheHlasenia.Add($"[Otvoriť] {titulok}");
            return TichyVyberSuboru;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog { Title = titulok, Filter = filter, CheckFileExists = true };
        return dialog.ShowDialog(Vlastnik) == true ? dialog.FileName : null;
    }

    /// <summary>Otvorí súbor alebo priečinok v predvolenom programe Windows.</summary>
    public static void OtvorVSysteme(string cesta)
    {
        if (TichyRezim)
        {
            TicheHlasenia.Add($"[Otvoriť v systéme] {cesta}");
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(cesta) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Chyba($"Nepodarilo sa otvoriť:\n{cesta}", ex);
        }
    }

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

    private static MessageBoxResult Ukaz(string sprava, MessageBoxButton tlacidla, MessageBoxImage ikona)
    {
        if (TichyRezim)
        {
            TicheHlasenia.Add($"[{ikona}] {sprava}");
            return tlacidla == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
        }

        var vlastnik = Vlastnik;
        return vlastnik is null
            ? MessageBox.Show(sprava, Titulok, tlacidla, ikona)
            : MessageBox.Show(vlastnik, sprava, Titulok, tlacidla, ikona);
    }
}
