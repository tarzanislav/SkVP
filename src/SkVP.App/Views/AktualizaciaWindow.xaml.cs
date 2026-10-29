using System.Windows;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.Views;

/// <summary>Ponuka novej verzie: novinky, potom stiahnutie, záloha databázy a spustenie inštalácie.</summary>
public partial class AktualizaciaWindow : Window
{
    private readonly DostupnaVerzia _verzia;

    /// <summary>Aktualizácia práve prebieha (dá sa zrušiť); null = okno čaká na rozhodnutie.</summary>
    private CancellationTokenSource? _priebeh;

    public AktualizaciaWindow(DostupnaVerzia verzia)
    {
        InitializeComponent();
        _verzia = verzia;
        Nadpis.Text = $"Je dostupná nová verzia SkVP {verzia.Verzia}";
        Podnadpis.Text = $"Máte nainštalovanú verziu {AktualizaciaService.AktualnaVerzia}"
                         + (verzia.Zverejnene is { } datum ? $". Nová verzia je zverejnená {datum.ToString("d. M. yyyy H:mm", Planovanie.Sk)}." : ".");
        Novinky.Text = BezMarkdownu(verzia.Novinky);
        Closing += (_, _) => _priebeh?.Cancel();
    }

    /// <summary>Novinky z GitHubu (Markdown) ako obyčajný text – bez značiek nadpisov a tučného písma a bez podpisov commitov.</summary>
    private static string BezMarkdownu(string text)
    {
        var riadky = text.ReplaceLineEndings("\n").Split('\n')
            .Where(riadok => !riadok.StartsWith("Co-Authored-By", StringComparison.OrdinalIgnoreCase))
            .Select(riadok => riadok.TrimStart('#', ' ').Replace("**", "", StringComparison.Ordinal));
        var cisty = string.Join(Environment.NewLine, riadky).Trim();
        return cisty.Length > 0 ? cisty : "Bez popisu zmien.";
    }

    private async void Aktualizovat_Click(object sender, RoutedEventArgs e)
    {
        bool hotovo;
        using (var priebeh = new CancellationTokenSource())
        {
            _priebeh = priebeh;
            UkazPriebeh(true);
            try
            {
                hotovo = await AktualizujAsync(priebeh.Token);
            }
            catch (OperationCanceledException)
            {
                hotovo = false; // zrušené tlačidlom alebo zatvorením okna
            }
            catch (SkvpException chyba)
            {
                Dialogy.Upozornenie(chyba.Message);
                hotovo = false;
            }
            finally
            {
                _priebeh = null;
            }
        }

        if (hotovo)
        {
            DialogResult = true;
            Application.Current.Shutdown();
        }
        else
        {
            UkazPriebeh(false);
        }
    }

    /// <summary>Stiahnutie, záloha a spustenie inštalácie. False = používateľ ju zastavil (bez zálohy alebo bez povolenia Windows).</summary>
    private async Task<bool> AktualizujAsync(CancellationToken zrusenie)
    {
        Stav.Text = "Sťahujem novú verziu…";
        var instalator = await AktualizaciaService.StiahniAsync(_verzia, new Progress<double>(podiel => Priebeh.Value = podiel * 100), zrusenie);

        Stav.Text = "Zálohujem databázu…";
        Priebeh.IsIndeterminate = true;
        try
        {
            await Task.Run(() => new ZalohaService().Zalohuj(), zrusenie);
        }
        catch (SkvpException chyba) when (Dialogy.Potvrd($"{chyba.Message}\n\nPokračovať v aktualizácii aj bez zálohy?"))
        {
            // pokračuje sa bez zálohy
        }
        catch (SkvpException)
        {
            return false;
        }

        Stav.Text = "Spúšťam inštaláciu…";
        if (AktualizaciaService.SpustiInstalator(instalator))
        {
            return true;
        }

        Dialogy.Upozornenie("Aktualizácia sa nespustila – Windows nedostal povolenie na inštaláciu. Môžete to skúsiť znova.");
        return false;
    }

    private void Neskor_Click(object sender, RoutedEventArgs e)
    {
        if (_priebeh is { } priebeh)
        {
            priebeh.Cancel();
        }
        else
        {
            DialogResult = false;
        }
    }

    private void UkazPriebeh(bool bezi)
    {
        Aktualizovat.IsEnabled = !bezi;
        Neskor.Content = bezi ? "Zrušiť" : "Neskôr";
        PanelPriebeh.Visibility = bezi ? Visibility.Visible : Visibility.Collapsed;
        Priebeh.IsIndeterminate = false;
        Priebeh.Value = 0;
    }
}
