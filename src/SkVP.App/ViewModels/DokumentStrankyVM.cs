using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Data;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>
/// Dokument Word na stránke (osnova predmetu, charakteristika oblasti): náhľad, úprava vo vstavanom editore
/// alebo vo Worde, automatické ukladanie a archivácia predchádzajúcej verzie.
/// </summary>
public sealed partial class DokumentStrankyVM : ObservableObject, IDisposable
{
    private readonly DokumentService _dokumenty = new();
    private readonly Func<string> _nazovSuboru;
    private bool _zarchivovane;

    public DokumentStrankyVM(MainViewModel hlavny, long objektId, Func<string> nazovSuboru, bool sablonaOsnovy)
    {
        Hlavny = hlavny;
        ObjektId = objektId;
        _nazovSuboru = nazovSuboru;
        SablonaOsnovy = sablonaOsnovy;
        _jeVoWorde = hlavny.JeVoWorde(objektId);
        hlavny.DokumentZmeneny += PriZmeneDokumentu;
        hlavny.DokumentPrepisany += PriPrepisaniDokumentu;
        hlavny.WordSkoncil += PriKonciWordu;
    }

    public MainViewModel Hlavny { get; }

    public long ObjektId { get; }

    public bool SablonaOsnovy { get; }

    [ObservableProperty]
    private bool _jeUprava;

    [ObservableProperty]
    private bool _jeVoWorde;

    [ObservableProperty]
    private string _stavUlozenia = "";

    [ObservableProperty]
    private IReadOnlyList<string> _varovania = Array.Empty<string>();

    public string VarovaniaText => Varovania.Count == 0 ? "" :
        $"Dokument obsahuje prvky, ktoré vstavaný editor nezobrazí: {string.Join(", ", Varovania)}." +
        (Word.JeNainstalovany ? " Na úpravu odporúčame Word." : "");

    partial void OnVarovaniaChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(VarovaniaText));

    /// <summary>Tlačidlo „Upraviť vo Worde“ – Word je nainštalovaný a v nastaveniach je zvolený.</summary>
    public bool MozeWord => Word.PouzivatWord;

    public int PocetVerzii => _dokumenty.Verzie(ObjektId).Count;

    /// <summary>Text z editora, ak bol zmenený (nastaví zobrazenie).</summary>
    public Func<List<Blok>?>? ZmenenyText { get; set; }

    /// <summary>Dokument sa zmenil zvonka (Word) – zobrazenie má text načítať znova.</summary>
    public event Action? Obnovit;

    public NacitanyDokument Nacitaj()
    {
        try
        {
            var nacitany = _dokumenty.Nacitaj(ObjektId);
            Varovania = nacitany.Varovania;
            return nacitany;
        }
        catch (SkvpException ex)
        {
            Dialogy.Upozornenie(ex.Message);
            return new NacitanyDokument(new List<Blok>(), Array.Empty<string>());
        }
    }

    /// <summary>Uloží text z editora do dokumentu Word v databáze (pred prvým uložením zarchivuje doterajšiu verziu).</summary>
    public void UlozZEditora(List<Blok> bloky)
    {
        try
        {
            if (_dokumenty.UlozBloky(ObjektId, bloky, _zarchivovane ? null : "Pred úpravou v programe"))
            {
                _zarchivovane = true;
                Hlavny.PoZmeneDokumentu(ObjektId, oznamit: false);
            }

            StavUlozenia = $"Uložené {DateTime.Now:H:mm}";
        }
        catch (Exception ex)
        {
            StavUlozenia = "Neuložené!";
            Dialogy.Chyba("Text sa nepodarilo uložiť.", ex);
        }
    }

    public void UlozTeraz()
    {
        if (ZmenenyText?.Invoke() is { } bloky)
        {
            UlozZEditora(bloky);
        }
    }

    [RelayCommand]
    private void PrepniUpravu()
    {
        if (JeUprava)
        {
            UlozTeraz();
            JeUprava = false;
            return;
        }

        if (JeVoWorde)
        {
            Dialogy.Info("Dokument je práve otvorený vo Worde. Dokončite úpravu vo Worde a zatvorte ho, potom ho môžete upravovať aj tu.");
            return;
        }

        if (Varovania.Count > 0 && !Dialogy.Potvrd(
                $"{VarovaniaText}\n\nPri uložení zo vstavaného editora by sa tieto prvky stratili (pôvodná verzia sa zachová v archíve).\n\nPokračovať v úprave v programe?"))
        {
            return;
        }

        JeUprava = true;
    }

    [RelayCommand]
    private void UpravitVoWorde()
    {
        UlozTeraz();
        JeUprava = false;
        try
        {
            Hlavny.UpravVoWorde(ObjektId, _nazovSuboru(), SablonaOsnovy);
            JeVoWorde = true;
            StavUlozenia = "Otvorené vo Worde";
        }
        catch (Exception ex)
        {
            Dialogy.Chyba("Word sa nepodarilo spustiť. Dokument môžete upraviť v programe.", ex);
        }
    }

    [RelayCommand]
    private void PrevziatZWordu() => Hlavny.SkontrolujWord(ObjektId);

    private void PriZmeneDokumentu(long id)
    {
        if (id == ObjektId && !JeUprava)
        {
            StavUlozenia = $"Prevzaté z Wordu {DateTime.Now:H:mm}";
            Obnovit?.Invoke();
        }
    }

    /// <summary>Program zmenil text (nový názov predmetu) – načítať znova aj v režime úprav (text bol pred zmenou uložený).</summary>
    private void PriPrepisaniDokumentu(long id)
    {
        if (id == ObjektId)
        {
            Obnovit?.Invoke();
            OnPropertyChanged(nameof(PocetVerzii));
        }
    }

    private void PriKonciWordu(long id)
    {
        if (id == ObjektId)
        {
            JeVoWorde = false;
        }
    }

    public void Dispose()
    {
        Hlavny.DokumentZmeneny -= PriZmeneDokumentu;
        Hlavny.DokumentPrepisany -= PriPrepisaniDokumentu;
        Hlavny.WordSkoncil -= PriKonciWordu;
    }
}
