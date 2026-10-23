using System.Windows;
using System.Windows.Threading;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;
using SkVP.ViewModels;
using SkVP.Views;

namespace SkVP;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        NastavTemu(NastaveniaSkoly.TemaSystem);
        DispatcherUnhandledException += NeosetrenaChyba;

        if (e.Args.Length == 3 && e.Args[0] == CitaniePdf.Parameter)
        {
            // pomocný proces na prečítanie PDF (import osnovy z PDF)
            CitaniePdf.SpustiPomocnyProces(e.Args[1], e.Args[2]);
            Shutdown(0);
            return;
        }

        if (e.Args.Contains("--samotest", StringComparer.OrdinalIgnoreCase))
        {
            Dialogy.TichyRezim = true;
            AppDomain.CurrentDomain.UnhandledException += (_, a) => Samotest.ZapisPriebeh("NEOŠETRENÁ VÝNIMKA: " + a.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (_, a) =>
            {
                Samotest.ZapisPriebeh("nepozorovaná výnimka úlohy: " + a.Exception);
                a.SetObserved();
            };
            Shutdown(Samotest.Spusti(this));
            return;
        }

        var prveSpustenie = !Databaza.Existuje;
        try
        {
            Databaza.Inicializuj();
            Services.Migracie.Spusti();
            Preklad.Nacitaj();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Databázu sa nepodarilo otvoriť.\n\n{Databaza.Cesta}\n\n{ex.GetBaseException().Message}\n\n" +
                            "Zálohy nájdete v priečinku Dokumenty\\SkVP\\Zálohy.", "SkVP", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        NastavTemu(Databaza.Citaj(NastaveniaSkoly.KlucTema, NastaveniaSkoly.TemaSystem));

        string? priecinokOsnov = null;
        if (prveSpustenie)
        {
            var uvod = new PrveSpustenieWindow();
            uvod.ShowDialog();
            priecinokOsnov = uvod.PriecinokOsnov;
        }

        try
        {
            // natrvalo sa mažú len objekty odstránené pred viac ako 60 dňami
            new ObjektService().VyprazdniKos(60);
        }
        catch (Exception)
        {
            // kôš sa vyprázdni nabudúce
        }

        var okno = new MainWindow();
        MainWindow = okno;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        okno.Show();

        okno.Dispatcher.InvokeAsync(okno.Model.ObnovNeprevzateZWordu, DispatcherPriority.ApplicationIdle);
        if (priecinokOsnov is not null)
        {
            // prvé spustenie podľa ŠVP: predmety z učebných osnov v priečinku (podľa názvu v úvodnej tabuľke)
            okno.Dispatcher.InvokeAsync(() =>
            {
                var sprievodca = new ImportOsnovViewModel(okno.Model);
                sprievodca.PridajPriecinok(priecinokOsnov);
                new ImportOsnovWindow(sprievodca) { Owner = okno }.ShowDialog();
                okno.Model.NacitajOblasti();
            }, DispatcherPriority.ApplicationIdle);
        }

        // rýchla kontrola databázy pri spustení (ako v EviTA), automatická záloha raz za deň a kontrola novej verzie
        Task.Run(() =>
        {
            try
            {
                if (!DiagnostikaService.RychlaKontrola().Ok)
                {
                    okno.Dispatcher.InvokeAsync(() => Dialogy.Upozornenie(
                        "Rýchla kontrola našla v databáze chybu. Otvorte Servis → Databáza: spustite úplnú kontrolu, " +
                        "obnovte dáta zo zálohy alebo použite záchranu dát."));
                }
            }
            catch (Exception)
            {
                // kontrola sa skúsi nabudúce
            }

            try
            {
                new ZalohaService().AutomatickaAkTreba();
            }
            catch (Exception)
            {
                // záloha sa skúsi nabudúce
            }
        });
        Exit += (_, _) =>
        {
            try
            {
                DiagnostikaService.Optimalizuj(); // štatistiky databázy pri zatvorení programu
            }
            catch (Exception)
            {
                // nevadí
            }
        };
        _ = okno.Model.SkontrolujAktualizaciuAsync();
    }

    /// <summary>Vzhľad programu: svetlý, tmavý alebo podľa nastavenia Windows (Fluent – Windows 11).</summary>
    public static void NastavTemu(string tema)
    {
        try
        {
#pragma warning disable WPF0001
            Current.ThemeMode = tema switch
            {
                NastaveniaSkoly.TemaSvetla => ThemeMode.Light,
                NastaveniaSkoly.TemaTmava => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
#pragma warning restore WPF0001
        }
        catch (Exception)
        {
            // vzhľad zostane predvolený
        }
    }

    private void NeosetrenaChyba(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        if (Dialogy.TichyRezim)
        {
            Samotest.Chyby.Add(e.Exception.ToString());
            e.Handled = true;
            return;
        }

        System.Windows.Input.Mouse.OverrideCursor = null;
        if (e.Exception is SkvpException chyba)
        {
            Dialogy.Upozornenie(chyba.Message);
        }
        else
        {
            Dialogy.Chyba("Nastala neočakávaná chyba. Program pokračuje ďalej.", e.Exception);
        }

        e.Handled = true;
    }
}
