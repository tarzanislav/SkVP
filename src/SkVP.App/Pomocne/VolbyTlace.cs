using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using SkVP.Data;

namespace SkVP.Pomocne;

/// <summary>Koľko strán zostavy pripadne na jednu stranu listu papiera (číselná hodnota je zároveň počet strán).</summary>
public enum Rozmiestnenie
{
    Jedna = 1,
    Dve = 2,
    Styri = 4,
    Osem = 8,

    /// <summary>Brožúra: po obojstrannej tlači sa listy preložia v strede a vznikne zošit A5.</summary>
    Brozura = 100,
}

/// <summary>
/// Voľby tlače a rozmiestnenia strán – spoločné pre tlačiareň, PDF aj obrázky. Ukladajú sa ako JSON
/// (kľúč <see cref="KlucNastavenia"/>), preto sa názvy vlastností nemenia.
/// </summary>
public sealed record VolbyTlace
{
    public const string KlucNastavenia = "tlac.volby";

    /// <summary>Priehľadnosť bieleho závoja pri zosvetlení (0 – 1).</summary>
    public const double SilaZosvetlenia = 0.45;

    public string? Tlaciaren { get; init; }
    public int Kopie { get; init; } = 1;
    public bool Obojstranne { get; init; }
    public Rozmiestnenie Rozmiestnenie { get; init; } = Rozmiestnenie.Jedna;

    /// <summary>Každá strana sa na liste zopakuje (2, 4 alebo 8 rovnakých strán – napr. na rozstrihanie).</summary>
    public bool OpakovatStranky { get; init; }

    /// <summary>Zväčšenie zmenšených strán (1 = bez zväčšenia, 1,3 = o 30 %) – odrežú sa len prázdne okraje strán.</summary>
    public double Zvacsenie { get; init; } = 1.0;

    /// <summary>Tenký rám okolo každej strany na liste.</summary>
    public bool Ramcek { get; init; }

    public bool OdtieneSivej { get; init; }

    /// <summary>Zosvetliť tlač (šetrí toner).</summary>
    public bool Zosvetlit { get; init; }

    /// <summary>Farebné podklady (hlavička tabuľky, striedanie riadkov …) sa netlačia, zostane biely papier.</summary>
    public bool BezPozadia { get; init; }

    /// <summary>Vynechať okraj na dierovanie (vľavo, na zadnej strane obojstrannej tlače vpravo).</summary>
    public bool Dierovac { get; init; }

    public double DierovacMm { get; init; } = 10;

    /// <summary>Brožúra sa tlačí vždy obojstranne (otáčanie po kratšej hrane).</summary>
    [JsonIgnore] public bool TlacitObojstranne => Obojstranne || Rozmiestnenie == Rozmiestnenie.Brozura;

    /// <summary>Počet strán zostavy na jednej strane listu.</summary>
    [JsonIgnore] public int StranNaList => Rozmiestnenie == Rozmiestnenie.Brozura ? 2 : (int)Rozmiestnenie;

    public static VolbyTlace Nacitaj() => (Databaza.CitajJson<VolbyTlace>(KlucNastavenia) ?? new VolbyTlace()).Opravene();

    public void Uloz()
    {
        try
        {
            Databaza.ZapisJson(KlucNastavenia, Opravene());
        }
        catch (SqliteException)
        {
            // voľby sa nezapamätajú – tlač funguje aj tak
        }
    }

    /// <summary>Voľby s hodnotami v povolenom rozsahu a bez volieb, ktoré pri danom rozmiestnení nemajú význam.</summary>
    public VolbyTlace Opravene()
    {
        var rozmiestnenie = Enum.IsDefined(Rozmiestnenie) ? Rozmiestnenie : Rozmiestnenie.Jedna;
        return this with
        {
            Rozmiestnenie = rozmiestnenie,
            Kopie = Math.Clamp(Kopie, 1, 999),
            Zvacsenie = VRozsahu(Zvacsenie, 1, 1.6, 1),
            DierovacMm = VRozsahu(DierovacMm, 0, 40, 10),
            OpakovatStranky = OpakovatStranky && rozmiestnenie is not (Rozmiestnenie.Jedna or Rozmiestnenie.Brozura),
            Dierovac = Dierovac && rozmiestnenie != Rozmiestnenie.Brozura,
        };
    }

    private static double VRozsahu(double hodnota, double min, double max, double nahradna) =>
        double.IsFinite(hodnota) ? Math.Clamp(hodnota, min, max) : nahradna;
}
