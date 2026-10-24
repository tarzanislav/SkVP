using System.Windows.Threading;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Hlavné okno – prehľad dňa pri spustení a pripomienky udalostí počas práce (v oznámení dole v okne).</summary>
public sealed partial class MainViewModel
{
    private DispatcherTimer? _pripomienky;
    private readonly HashSet<string> _pripomenute = new();
    private DateTime _poslednaKontrola = DateTime.Now;

    /// <summary>Po spustení programu: prehľad dňa (ak je zapnutý) a sledovanie pripomienok.</summary>
    public void SpustiPlanovanie()
    {
        var nastavenia = NastaveniaKalendara.Nacitaj();
        if (nastavenia.PrehladPriSpusteni && !Dialogy.TichyRezim)
        {
            try
            {
                var prehlad = new KalendarService().Prehlad(DateTime.Today);
                if (!prehlad.JePrazdny)
                {
                    var okno = new Views.PrehladDnaWindow(prehlad) { Owner = Dialogy.Vlastnik };
                    okno.ShowDialog();
                    switch (okno.Otvorit)
                    {
                        case Views.PrehladDnaWindow.Ciel.Kalendar:
                            OtvorKalendar(null);
                            break;
                        case Views.PrehladDnaWindow.Ciel.Ulohy:
                            OtvorUlohy(null);
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // prehľad dňa sa ukáže nabudúce
            }
        }

        _pripomienky ??= new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _pripomienky.Tick -= SkontrolujPripomienky;
        _pripomienky.Tick += SkontrolujPripomienky;
        _pripomienky.Start();
    }

    /// <summary>Udalosti, ktorých čas pripomienky nastal od poslednej kontroly (každá sa pripomenie raz).</summary>
    public IReadOnlyList<VyskytVKalendari> NovePripomienky(DateTime teraz)
    {
        var vysledok = new List<VyskytVKalendari>();
        foreach (var v in new KalendarService().Vyskyty(teraz.Date, teraz.Date.AddDays(8), automaticke: false))
        {
            if (v.Zdroj == ZdrojKalendara.Udalost && !v.JeVybavena && KalendarService.CasPripomienky(v) is { } cas
                && cas <= teraz && cas > _poslednaKontrola.AddMinutes(-1) && _pripomenute.Add(v.Kluc))
            {
                vysledok.Add(v);
            }
        }

        _poslednaKontrola = teraz;
        return vysledok;
    }

    private void SkontrolujPripomienky(object? sender, EventArgs e)
    {
        try
        {
            if (!NastaveniaKalendara.Nacitaj().Upozornenia)
            {
                _poslednaKontrola = DateTime.Now;
                return;
            }

            var nove = NovePripomienky(DateTime.Now);
            if (nove.Count > 0)
            {
                var prvy = nove[0];
                var kedy = prvy.Zaciatok.Date == DateTime.Today ? prvy.CelyDen ? "dnes" : $"dnes o {prvy.Zaciatok:H:mm}" : Planovanie.TerminText(prvy.Zaciatok);
                Oznam($"Pripomienka: {prvy.Nazov} – {kedy}" + (nove.Count > 1 ? $" (a ďalšie: {nove.Count - 1})" : ""),
                    () => OtvorKalendar(prvy.Zaciatok), "Otvoriť v kalendári");
            }
        }
        catch (Exception)
        {
            // skúsi sa o minútu
        }
    }
}
