using CommunityToolkit.Mvvm.ComponentModel;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>
/// Zapínanie údajov o škole v zostavách (Tlač a zostavy, Export): na úvodnej strane názov, organizačná zložka, adresa,
/// IČO a DIČ, kontakt a logo; v hlavičke strán názov, zložka a logo; v päte strán názov školy a kontakt;
/// vo vlastnostiach súboru autor. Voľby sú spoločné (ukladajú sa hneď), takže platia rovnako pre tlač, PDF aj Word.
/// </summary>
public sealed partial class UdajeOSkoleVZostaveVM : ObservableObject
{
    private readonly TitulnaStrana _titul;
    private readonly Action? _priZmene;
    private readonly bool _nacitane;

    public UdajeOSkoleVZostaveVM(Action? priZmene = null)
    {
        _titul = TitulnaStrana.Nacitaj();
        _priZmene = priZmene;
        _skola = _titul.SkolaZobrazit;
        _zlozka = _titul.ZlozkaZobrazit;
        _adresa = _titul.AdresaZobrazit;
        _icoDic = _titul.IcoDicZobrazit;
        _kontakt = _titul.KontaktZobrazit;
        _logo = _titul.LogoZobrazit;
        _skolaVHlavicke = _titul.SkolaVHlavickeZobrazit;
        _zlozkaVHlavicke = _titul.ZlozkaVHlavickeZobrazit;
        _logoVHlavicke = _titul.LogoVHlavickeZobrazit;
        _skolaVPate = _titul.SkolaVPateZobrazit;
        _kontaktVPate = _titul.KontaktVPateZobrazit;
        _metadata = _titul.MetadataZobrazit;
        _nacitane = true;
    }

    // hodnoty z Nastavení → Údaje o organizácii (na zobrazenie pri voľbe)
    public string TextSkoly => Text(_titul.Skola);
    public string TextZlozky => Text(_titul.Zlozka);
    public string TextAdresy => Text(_titul.Adresa);
    public string TextIcoDic => Text(_titul.IcoDic);
    public string TextKontaktu => Text(_titul.Kontakt);
    public bool MaLogo => NastaveniaSkoly.CestaLoga() is not null;
    public string TextLoga => MaLogo ? "logo z Nastavení" : "nevyplnené – logo vyberiete v Nastaveniach";
    public bool ChybajuUdaje => _titul.Skola.Trim().Length == 0 || (_titul.Adresa.Trim().Length == 0 && _titul.IcoDic.Length == 0);

    private static string Text(string hodnota) => hodnota.Trim().Length > 0 ? hodnota.Trim() : "nevyplnené";

    [ObservableProperty] private bool _skola;
    [ObservableProperty] private bool _zlozka;
    [ObservableProperty] private bool _adresa;
    [ObservableProperty] private bool _icoDic;
    [ObservableProperty] private bool _kontakt;
    [ObservableProperty] private bool _logo;
    [ObservableProperty] private bool _skolaVHlavicke;
    [ObservableProperty] private bool _zlozkaVHlavicke;
    [ObservableProperty] private bool _logoVHlavicke;
    [ObservableProperty] private bool _skolaVPate;
    [ObservableProperty] private bool _kontaktVPate;
    [ObservableProperty] private bool _metadata;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_nacitane)
        {
            return;
        }

        _titul.SkolaZobrazit = Skola;
        _titul.ZlozkaZobrazit = Zlozka;
        _titul.AdresaZobrazit = Adresa;
        _titul.IcoDicZobrazit = IcoDic;
        _titul.KontaktZobrazit = Kontakt;
        _titul.LogoZobrazit = Logo;
        _titul.SkolaVHlavickeZobrazit = SkolaVHlavicke;
        _titul.ZlozkaVHlavickeZobrazit = ZlozkaVHlavicke;
        _titul.LogoVHlavickeZobrazit = LogoVHlavicke;
        _titul.SkolaVPateZobrazit = SkolaVPate;
        _titul.KontaktVPateZobrazit = KontaktVPate;
        _titul.MetadataZobrazit = Metadata;
        _titul.UlozUdajeOSkole();
        _priZmene?.Invoke();
    }
}
