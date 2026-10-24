using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Nastavenia – kalendár a úlohy: prehľad dňa pri spustení, pripomienky počas práce, navrhované úlohy.</summary>
public sealed partial class NastaveniaViewModel
{
    public bool PrehladPriSpusteni
    {
        get => NastaveniaKalendara.Nacitaj().PrehladPriSpusteni;
        set => ZmenKalendar(n => n with { PrehladPriSpusteni = value });
    }

    public bool Upozornenia
    {
        get => NastaveniaKalendara.Nacitaj().Upozornenia;
        set => ZmenKalendar(n => n with { Upozornenia = value });
    }

    public bool NavrhovatUlohy
    {
        get => NavrhyUloh.Zapnute;
        set
        {
            NavrhyUloh.Zapnute = value;
            OnPropertyChanged();
        }
    }

    private void ZmenKalendar(Func<NastaveniaKalendara, NastaveniaKalendara> zmena)
    {
        zmena(NastaveniaKalendara.Nacitaj()).Uloz();
        OnPropertyChanged(nameof(PrehladPriSpusteni));
        OnPropertyChanged(nameof(Upozornenia));
    }
}
