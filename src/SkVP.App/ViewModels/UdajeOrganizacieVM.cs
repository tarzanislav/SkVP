using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Formulár údajov o organizácii (Nastavenia, prvé spustenie). Pri každej zmene zavolá <c>priZmene</c>.</summary>
public sealed partial class UdajeOrganizacieVM : ObservableObject
{
    private readonly Action<UdajeOrganizacie>? _priZmene;
    private readonly bool _nacitane;

    public UdajeOrganizacieVM(UdajeOrganizacie udaje, Action<UdajeOrganizacie>? priZmene = null)
    {
        _nazov = udaje.Nazov;
        _zlozka = udaje.Zlozka;
        _ulica = udaje.Ulica;
        _mesto = udaje.Mesto;
        _psc = udaje.Psc;
        _stat = udaje.Stat;
        _ico = udaje.Ico;
        _dic = udaje.Dic;
        _email = udaje.Email;
        _telefon = udaje.Telefon;
        _priZmene = priZmene;
        _nacitane = true;
    }

    [ObservableProperty] private string _nazov;
    [ObservableProperty] private string _zlozka;
    [ObservableProperty] private string _ulica;
    [ObservableProperty] private string _mesto;
    [ObservableProperty] private string _psc;
    [ObservableProperty] private string _stat;
    [ObservableProperty] private string _ico;
    [ObservableProperty] private string _dic;
    [ObservableProperty] private string _email;
    [ObservableProperty] private string _telefon;

    public UdajeOrganizacie Udaje => new(Nazov, Zlozka, Ulica, Mesto, Psc, Stat, Ico, Dic, Email, Telefon);

    public string? ChybaIco => UdajeOrganizacie.SkontrolujIco(Ico);
    public string? ChybaEmail => UdajeOrganizacie.SkontrolujEmail(Email);
    public bool MaChybuIco => ChybaIco is not null;
    public bool MaChybuEmail => ChybaEmail is not null;

    public IReadOnlyList<string> Staty { get; } = new[] { "Slovensko", "Česko", "Maďarsko", "Poľsko", "Rakúsko", "Ukrajina" };

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is nameof(Ico))
        {
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(ChybaIco)));
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(MaChybuIco)));
        }

        if (e.PropertyName is nameof(Email))
        {
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(ChybaEmail)));
            base.OnPropertyChanged(new PropertyChangedEventArgs(nameof(MaChybuEmail)));
        }

        if (_nacitane && e.PropertyName is nameof(Nazov) or nameof(Zlozka) or nameof(Ulica) or nameof(Mesto) or nameof(Psc) or nameof(Stat)
                or nameof(Ico) or nameof(Dic) or nameof(Email) or nameof(Telefon))
        {
            _priZmene?.Invoke(Udaje);
        }
    }
}
