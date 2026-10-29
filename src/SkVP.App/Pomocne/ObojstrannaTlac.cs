using System.Text.Json.Serialization;
using SkVP.Data;
using SkVP.Services;

namespace SkVP.Pomocne;

/// <summary>Ako tlačiareň tlačí obojstranne.</summary>
public enum RezimObojstrannej
{
    /// <summary>Tlačiareň otáča list sama (duplex).</summary>
    Automaticky = 0,

    /// <summary>Najprv sa vytlačia predné strany, používateľ vloží listy späť a vytlačia sa zadné strany.</summary>
    Rucne = 1,
}

/// <summary>
/// Ako obojstranne tlačí jedna konkrétna tlačiareň – zistí to sprievodca so skúšobnými listami. Tlačiarne otáčajú
/// list rôzne, preto sa zadná strana podľa potreby otočí o 180° a pri ručnom otáčaní sa obráti aj poradie listov.
/// Profily všetkých tlačiarní sa ukladajú spolu (kľúč <see cref="KlucNastavenia"/>, názov tlačiarne → profil).
/// </summary>
public sealed record ProfilObojstrannej
{
    public const string KlucNastavenia = "tlac.obojstranne";

    public RezimObojstrannej Rezim { get; init; }

    /// <summary>Zadné strany listov na výšku (1 alebo 4 strany na list) otočiť o 180°.</summary>
    public bool OtocitNaVysku { get; init; }

    /// <summary>Zadné strany listov na šírku (2 alebo 8 strán na list, brožúra) otočiť o 180°.</summary>
    public bool OtocitNaSirku { get; init; }

    /// <summary>Ručné otáčanie: zadné strany tlačiť od posledného listu.</summary>
    public bool ObratitPoradie { get; init; }

    public bool Otocit(bool listNaSirku) => listNaSirku ? OtocitNaSirku : OtocitNaVysku;

    /// <summary>Krátky popis do okna tlače.</summary>
    [JsonIgnore]
    public string Popis => Rezim == RezimObojstrannej.Rucne
        ? Preklad.T("ručné otáčanie listov") + (ObratitPoradie ? ", " + Preklad.T("obrátené poradie") : "")
        : Preklad.T("tlačiareň otáča sama") + (OtocitNaVysku || OtocitNaSirku ? ", " + Preklad.T("zadné strany otočené") : "");

    /// <summary>Profil tlačiarne, alebo null, ak ešte nebol nastavený sprievodcom.</summary>
    public static ProfilObojstrannej? Pre(string? tlaciaren) =>
        tlaciaren is not null && Profily().TryGetValue(tlaciaren, out var profil) ? profil : null;

    public static void Uloz(string tlaciaren, ProfilObojstrannej profil)
    {
        var profily = Profily();
        profily[tlaciaren] = profil;
        Databaza.ZapisJson(KlucNastavenia, profily);
    }

    private static Dictionary<string, ProfilObojstrannej> Profily()
    {
        var profily = new Dictionary<string, ProfilObojstrannej>(StringComparer.OrdinalIgnoreCase);
        foreach (var (tlaciaren, profil) in Databaza.CitajJson<Dictionary<string, ProfilObojstrannej>>(KlucNastavenia) ?? new())
        {
            profily[tlaciaren] = profil;
        }

        return profily;
    }

    /// <summary>
    /// Profil podľa odpovedí po skúšobných listoch duplexu – pre list na výšku a na šírku: 0 = zadná strana stojí rovno,
    /// 1 = je hore nohami, 2 = je na samostatnom liste. Null = tlačiareň obojstranne netlačí.
    /// </summary>
    public static ProfilObojstrannej? ZDuplexu(int naVysku, int naSirku) =>
        naVysku == 2 || naSirku == 2
            ? null
            : new ProfilObojstrannej { Rezim = RezimObojstrannej.Automaticky, OtocitNaVysku = naVysku == 1, OtocitNaSirku = naSirku == 1 };

    /// <summary>
    /// Profil podľa odpovede po ručnom otáčaní – čo je na zadnej strane listu 1: 0 = číslo 2 rovno, 1 = číslo 2 hore nohami,
    /// 2 = číslo 4 rovno, 3 = číslo 4 hore nohami. Iná odpoveď (zadná strana prázdna) = null, listy boli vložené zle.
    /// </summary>
    public static ProfilObojstrannej? ZRucneho(int odpoved)
    {
        if (odpoved is < 0 or > 3)
        {
            return null;
        }

        var otocit = odpoved % 2 == 1;
        return new ProfilObojstrannej { Rezim = RezimObojstrannej.Rucne, OtocitNaVysku = otocit, OtocitNaSirku = otocit, ObratitPoradie = odpoved >= 2 };
    }
}

/// <summary>Strana tlačovej úlohy: index strany listu (null = prázdna strana na doplnenie) a otočenie o 180°.</summary>
public readonly record struct StranaUlohy(int? List, bool Otocena);

/// <summary>Jedna tlačová úloha pre tlačiareň.</summary>
public sealed record TlacovaUloha(IReadOnlyList<StranaUlohy> Strany, bool Duplex, int Kopie);

/// <summary>Rozdelenie strán listov do tlačových úloh – nezávislé od tlačiarne a kreslenia.</summary>
public static class PlanTlace
{
    /// <summary>
    /// Tlačové úlohy. Jednostranne je to jedna úloha so všetkými stranami. Obojstranne sa počet strán doplní na párny
    /// (ďalšia kópia nezačne na zadnej strane) a zadné strany sa podľa profilu otočia; tlačiareň s duplexom dostane jednu
    /// úlohu, pri ručnom otáčaní sú dve – predné strany všetkých kópií a po vložení listov zadné strany.
    /// </summary>
    public static IReadOnlyList<TlacovaUloha> Ulohy(int stranListov, int kopie, bool obojstranne, bool listNaSirku, ProfilObojstrannej? profil)
    {
        if (stranListov <= 0)
        {
            return [];
        }

        if (!obojstranne)
        {
            return [new TlacovaUloha(Enumerable.Range(0, stranListov).Select(i => new StranaUlohy(i, false)).ToArray(), false, kopie)];
        }

        profil ??= new ProfilObojstrannej();
        var otocitZadne = profil.Otocit(listNaSirku);
        var listov = (stranListov + 1) / 2;
        StranaUlohy Strana(int i) => new(i < stranListov ? i : null, otocitZadne && i % 2 == 1);

        if (profil.Rezim == RezimObojstrannej.Automaticky)
        {
            return [new TlacovaUloha(Enumerable.Range(0, 2 * listov).Select(Strana).ToArray(), true, kopie)];
        }

        var poradieListov = Enumerable.Repeat(Enumerable.Range(0, listov), kopie).SelectMany(l => l).ToArray();
        var predne = poradieListov.Select(l => Strana(2 * l)).ToArray();
        var zadne = poradieListov.Select(l => Strana(2 * l + 1)).ToArray();
        if (profil.ObratitPoradie)
        {
            Array.Reverse(zadne);
        }

        return [new TlacovaUloha(predne, false, 1), new TlacovaUloha(zadne, false, 1)];
    }
}
