using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Shapes;
using SkVP.Data;
using SkVP.Services;

namespace SkVP.Pomocne;

/// <summary>
/// Diktovanie do textového poľa: hlasové písanie Windows, alebo nahrávka z mikrofónu a prepis v počítači. Jedno
/// tlačidlo prepína Diktovať ↔ Zastaviť a prepísať; stav a hlasitosť sa ukazujú pri ňom.
/// </summary>
public sealed class DiktovanieDoPola(TextBox pole, Button tlacidlo, TextBlock ikona, TextBlock napis, TextBlock stav, Rectangle hlasitost)
{
    private const string IkonaMikrofonu = "";
    private const string IkonaZastavenia = "";
    private const double SirkaHlasitosti = 90;

    private Mikrofon? _mikrofon;
    private bool _prepisuje;

    /// <summary>Ako sa práve diktuje – text pri tlačidle Diktovať.</summary>
    public static string PopisSposobu()
    {
        if (Diktovanie.Sposob == SposobDiktovania.Offline)
        {
            var model = Diktovanie.Model;
            return model.JeStiahnuty
                ? Preklad.F($"Prepis v počítači bez internetu (model {model.Nazov}) – kliknite Diktovať, hovorte a potom Zastaviť a prepísať.")
                : Preklad.T("Prepis bez internetu: model ešte nie je stiahnutý – otvorte nastavenie diktovania (ozubené koliesko).");
        }

        return HlasovePisanie.Slovencina is null
            ? Preklad.T("Hlasové písanie Windows (Win+H) – pre slovenčinu pridajte v Nastaveniach Windows slovenskú klávesnicu (Čas a jazyk → Jazyk a oblasť).")
            : Preklad.T("Hlasové písanie Windows (Win+H) – potrebuje internet. Bez internetu: ozubené koliesko → Prepis v počítači.");
    }

    /// <summary>Pri prepise bez internetu načíta model do pamäte vopred – prepis potom začne bez čakania.</summary>
    public static void PripravModel()
    {
        if (Diktovanie.Sposob == SposobDiktovania.Offline)
        {
            _ = PrepisHlasu.PripravAsync(Diktovanie.Model);
        }
    }

    /// <param name="uspech">true = zelená, false = upozornenie, null = bežný text</param>
    public void UkazStav(string text, bool? uspech = null)
    {
        stav.Text = text;
        stav.SetResourceReference(TextBlock.ForegroundProperty, uspech switch
        {
            true => "SystemFillColorSuccessBrush",
            false => "SystemFillColorCautionBrush",
            null => "TextFillColorSecondaryBrush",
        });
    }

    /// <summary>Klik na tlačidlo: hlasové písanie Windows, alebo začatie či ukončenie nahrávky s prepisom.</summary>
    public async Task KlikAsync(bool novyRiadok = false)
    {
        if (_prepisuje)
        {
            return;
        }

        if (Diktovanie.Sposob == SposobDiktovania.Windows)
        {
            var poSlovensky = HlasovePisanie.Spusti(pole);
            UkazStav(poSlovensky
                    ? Preklad.T("Hovorte – Windows píše text na miesto kurzora. Ukončíte znova klávesmi Win+H alebo kliknutím mimo.")
                    : Preklad.T("Hovorte… Pre slovenčinu pridajte v Nastaveniach Windows slovenskú klávesnicu (Čas a jazyk → Jazyk a oblasť)."),
                poSlovensky ? null : false);
        }
        else if (_mikrofon is { } mikrofon)
        {
            var zvuk = mikrofon.Stop();
            ZrusNahravanie();
            await PrepisAVlozAsync(zvuk, novyRiadok);
        }
        else
        {
            ZacniNahravat();
        }
    }

    /// <summary>Prepíše zvuk modelom v počítači a vloží text na miesto kurzora (aj samotest s nahrávkou WAV).</summary>
    public async Task PrepisAVlozAsync(float[] zvuk, bool novyRiadok = false)
    {
        _prepisuje = true;
        tlacidlo.IsEnabled = false;
        UkazStav(Preklad.F($"Prepisujem {zvuk.Length / (double)PrepisHlasu.VzorkovaFrekvencia:0} s nahrávky…"));
        var cas = Stopwatch.StartNew();
        try
        {
            var text = await PrepisHlasu.PrepisAsync(zvuk, Diktovanie.Model);
            if (string.IsNullOrWhiteSpace(text))
            {
                UkazStav(Preklad.T("Nič som nezachytil – hovorte bližšie k mikrofónu alebo skontrolujte, či nie je stlmený."), false);
                return;
            }

            VlozText(text, novyRiadok);
            UkazStav(Preklad.F($"Hotovo za {cas.Elapsed.TotalSeconds:0} s – text je vložený na miesto kurzora."), true);
        }
        catch (SkvpException ex)
        {
            UkazStav(ex.Message, false);
        }
        catch (Exception ex)
        {
            UkazStav(Preklad.F($"Prepis sa nepodaril: {ex.GetBaseException().Message}"), false);
        }
        finally
        {
            _prepisuje = false;
            tlacidlo.IsEnabled = true;
        }
    }

    /// <summary>Ukončí nahrávanie bez prepisu a vráti tlačidlo do pôvodného stavu.</summary>
    public void ZrusNahravanie()
    {
        _mikrofon?.Dispose();
        _mikrofon = null;
        hlasitost.Width = 0;
        ikona.Text = IkonaMikrofonu;
        napis.Text = Preklad.T("Diktovať");
    }

    private void ZacniNahravat()
    {
        var prekazka = PrepisHlasu.Prekazka()
                       ?? (Diktovanie.Model.JeStiahnuty ? null : Preklad.T("Model na prepis nie je stiahnutý – otvorte nastavenie diktovania (ozubené koliesko)."));
        if (prekazka is not null)
        {
            UkazStav(prekazka, false);
            return;
        }

        try
        {
            _mikrofon = new Mikrofon();
            _mikrofon.Uroven += uroven => hlasitost.Dispatcher.BeginInvoke(() => hlasitost.Width = 4 + uroven * SirkaHlasitosti);
            _mikrofon.Start();
            ikona.Text = IkonaZastavenia;
            napis.Text = Preklad.T("Zastaviť a prepísať");
            UkazStav(Preklad.T("Nahrávam… hovorte, potom kliknite Zastaviť a prepísať."));
        }
        catch (SkvpException ex)
        {
            ZrusNahravanie();
            UkazStav(ex.Message, false);
        }
    }

    /// <summary>Vloží text na miesto kurzora – s medzerou (alebo novým riadkom), ak pred ním nie je medzera.</summary>
    private void VlozText(string text, bool novyRiadok)
    {
        var kurzor = Math.Clamp(pole.CaretIndex, 0, pole.Text.Length);
        var predKurzorom = kurzor > 0 ? pole.Text[kurzor - 1] : '\n';
        var vlozit = (char.IsWhiteSpace(predKurzorom) ? "" : novyRiadok ? Environment.NewLine : " ") + text.Trim();
        pole.Text = pole.Text.Insert(kurzor, vlozit);
        pole.CaretIndex = kurzor + vlozit.Length;
        pole.Focus();
    }
}
