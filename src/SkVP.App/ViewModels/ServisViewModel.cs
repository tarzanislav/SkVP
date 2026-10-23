using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Záloha v zozname (súbor ZIP).</summary>
public sealed record ZalohaVM(FileInfo Subor)
{
    public string Nazov => Subor.Name;

    public string Popis => $"{Subor.LastWriteTime.ToString("d. M. yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))}  •  {Velkost(Subor.Length)}"
                           + (Subor.Name.Contains("_auto", StringComparison.OrdinalIgnoreCase) ? "  •  automatická" : "")
                           + (Subor.Name.StartsWith("SkVP_osnovy_", StringComparison.OrdinalIgnoreCase) ? "  •  osnovy vo Worde" : "");

    public static string Velkost(long bajty) => bajty >= 1024 * 1024
        ? $"{bajty / 1024.0 / 1024.0:0.0} MB"
        : $"{Math.Max(1, bajty / 1024)} kB";
}

/// <summary>
/// Sekcia Servis (ako v EviTA): zálohy všetkých dát a učebných osnov, pravidelná záloha, obnova zo zálohy, kontrola
/// a údržba databázy, testovacia sada a vymazanie údajov.
/// </summary>
public sealed partial class ServisViewModel : ObservableObject
{
    private readonly bool _nacitane;

    public ServisViewModel(MainViewModel hlavny)
    {
        Hlavny = hlavny;
        _frekvencia = ZalohaService.Frekvencia;
        _ponechat = ZalohaService.Ponechat.ToString(CultureInfo.InvariantCulture);
        _priecinokZaloh = Databaza.PriecinokZaloh;
        Obnov();
        _nacitane = true;
    }

    public MainViewModel Hlavny { get; }

    public ObservableCollection<ZalohaVM> Zalohy { get; } = new();

    [ObservableProperty]
    private ZalohaVM? _vybrana;

    [ObservableProperty]
    private string _stavZaloh = "";

    [ObservableProperty]
    private string _frekvencia;

    [ObservableProperty]
    private string _ponechat;

    [ObservableProperty]
    private string _priecinokZaloh;

    [ObservableProperty]
    private string _protokol = "";

    public string CestaDatabazy => Databaza.Cesta;

    public string Velkost => File.Exists(Databaza.Cesta) ? ZalohaVM.Velkost(new FileInfo(Databaza.Cesta).Length) : "";

    public string Pocty
    {
        get
        {
            using var db = Databaza.Otvor();
            long Pocet(string sql) => Convert.ToInt64(Databaza.Skalar(db, sql) ?? 0L, CultureInfo.InvariantCulture);
            return $"Oblasti: {Pocet("SELECT count(*) FROM objekt WHERE typ = 'oblast' AND odstranene IS NULL")}  •  " +
                   $"predmety: {Pocet("SELECT count(*) FROM objekt WHERE typ = 'predmet' AND odstranene IS NULL")}  •  " +
                   $"dokumenty: {Pocet("SELECT count(*) FROM dokument")}  •  archivované verzie: {Pocet("SELECT count(*) FROM verzia")}";
        }
    }

    public bool JeTestovaciaSada => TestovaciaSada.JeNainstalovana;

    public void Obnov()
    {
        Zalohy.Clear();
        foreach (var subor in new ZalohaService().Zalohy())
        {
            Zalohy.Add(new ZalohaVM(subor));
        }

        if (Directory.Exists(Databaza.PriecinokZaloh))
        {
            foreach (var subor in new DirectoryInfo(Databaza.PriecinokZaloh).GetFiles("SkVP_osnovy_*.zip").OrderByDescending(f => f.LastWriteTime))
            {
                Zalohy.Add(new ZalohaVM(subor));
            }
        }

        var posledna = new ZalohaService().Posledna();
        StavZaloh = posledna is null
            ? "Zatiaľ nie je žiadna záloha."
            : $"Posledná záloha: {posledna.LastWriteTime.ToString("d. M. yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))} ({posledna.Name}). Záloh: {Zalohy.Count}.";
        OnPropertyChanged(nameof(Velkost));
        OnPropertyChanged(nameof(Pocty));
        OnPropertyChanged(nameof(JeTestovaciaSada));
    }

    private void Zapis(string text) => Protokol = $"{DateTime.Now:HH:mm:ss}  {text}\n" + Protokol;

    partial void OnFrekvenciaChanged(string value)
    {
        if (_nacitane)
        {
            ZalohaService.Frekvencia = value;
        }
    }

    partial void OnPonechatChanged(string value)
    {
        if (_nacitane && int.TryParse(value, out var n))
        {
            ZalohaService.Ponechat = n;
        }
    }

    [RelayCommand]
    private void VybratPriecinokZaloh()
    {
        if (Dialogy.VybratPriecinok("Priečinok na zálohy (aj USB kľúč, sieťový disk, OneDrive)") is { } priecinok)
        {
            Databaza.Zapis(Databaza.KlucPriecinokZaloh, priecinok);
            PriecinokZaloh = Databaza.PriecinokZaloh;
            Zapis($"Priečinok záloh: {priecinok}");
            Obnov();
        }
    }

    [RelayCommand]
    private void PredvolenyPriecinokZaloh()
    {
        Databaza.Zapis(Databaza.KlucPriecinokZaloh, null);
        PriecinokZaloh = Databaza.PriecinokZaloh;
        Obnov();
    }

    /// <summary>Záloha všetkých dát (databáza s osnovami, archívom, nastaveniami a logom).</summary>
    [RelayCommand]
    public void ZalohovatVsetko()
    {
        try
        {
            string? cesta = null;
            Dialogy.Cakaj(() => cesta = new ZalohaService().Zalohuj());
            Zapis($"Záloha všetkých dát: {Path.GetFileName(cesta)}");
            Hlavny.Oznam($"Záloha je uložená: {Path.GetFileName(cesta)}");
            Obnov();
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    /// <summary>Záloha učebných osnov vo Worde (ZIP s priečinkami oblastí a archívom).</summary>
    [RelayCommand]
    public void ZalohovatOsnovy()
    {
        try
        {
            string? cesta = null;
            Dialogy.Cakaj(() => cesta = ZalohaService.ZalohujOsnovy());
            Zapis($"Záloha učebných osnov vo Worde: {Path.GetFileName(cesta)}");
            Hlavny.Oznam($"Osnovy sú zálohované: {Path.GetFileName(cesta)}");
            Obnov();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Dialogy.Chyba("Zálohu osnov sa nepodarilo vytvoriť.", ex);
        }
    }

    [RelayCommand]
    private void OtvoritZalohy()
    {
        Directory.CreateDirectory(Databaza.PriecinokZaloh);
        Dialogy.OtvorVSysteme(Databaza.PriecinokZaloh);
    }

    [RelayCommand]
    private void OtvoritData() => Dialogy.OtvorVSysteme(Databaza.Priecinok);

    [RelayCommand]
    private void ObnovitVybranu()
    {
        if (Vybrana is null)
        {
            Dialogy.Upozornenie("Vyberte zálohu v zozname.");
            return;
        }

        ObnovZo(Vybrana.Subor.FullName);
    }

    [RelayCommand]
    private void ObnovitZoSuboru()
    {
        if (Dialogy.Otvorit("Záloha SkVP", "Záloha SkVP (*.zip)|*.zip") is { } cesta)
        {
            ObnovZo(cesta);
        }
    }

    private void ObnovZo(string zip)
    {
        if (Path.GetFileName(zip).StartsWith("SkVP_osnovy_", StringComparison.OrdinalIgnoreCase))
        {
            Dialogy.Info("Toto je záloha učebných osnov vo Worde – súbory sa otvárajú priamo (napr. dvojklikom na ZIP). Na obnovu programu vyberte zálohu SkVP_zaloha_….");
            return;
        }

        if (!Dialogy.Potvrd($"Obnoviť všetky dáta zo zálohy {Path.GetFileName(zip)}?\n\nSúčasné dáta sa nahradia. Predtým sa ešte zálohujú, takže sa dajú vrátiť. Program sa potom spustí znova."))
        {
            return;
        }

        try
        {
            Dialogy.Cakaj(() => new ZalohaService().Obnov(zip));
            Zapis($"Obnovené zo zálohy {Path.GetFileName(zip)}");
            if (Dialogy.TichyRezim)
            {
                Hlavny.NacitajOblasti();
                Obnov();
                return;
            }

            Dialogy.Info("Dáta sú obnovené. Program sa teraz spustí znova.");
            if (Environment.ProcessPath is { } program)
            {
                Process.Start(new ProcessStartInfo(program) { UseShellExecute = true });
            }

            System.Windows.Application.Current.Shutdown();
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
        }
    }

    [RelayCommand]
    public void Skontrolovat()
    {
        var vysledok = ZalohaService.Skontroluj();
        Zapis(vysledok);
        Dialogy.Info(vysledok);
    }

    [RelayCommand]
    private void Zmensit()
    {
        (long Pred, long Po) velkost = default;
        Dialogy.Cakaj(() => velkost = ZalohaService.Zmensi());
        Zapis($"Databáza zmenšená: {ZalohaVM.Velkost(velkost.Pred)} → {ZalohaVM.Velkost(velkost.Po)}");
        OnPropertyChanged(nameof(Velkost));
    }

    [RelayCommand]
    private void NahratTestovaciuSadu()
    {
        if (Dialogy.Potvrd("Do predmetov učebného plánu gymnázia sa nahrajú ukážkové učebné osnovy (chýbajúce oblasti a predmety sa doplnia) a údaje vymyslenej školy.\n\n" +
                           "Osnovy predmetov s rovnakým názvom sa nahradia – doterajšia verzia sa zarchivuje.\n\nPokračovať?"))
        {
            Hlavny.NahrajTestovaciuSadu();
            Obnov();
        }
    }

    [RelayCommand]
    private void VymazatUdaje()
    {
        Hlavny.VymazatUdaje();
        Obnov();
    }
}
