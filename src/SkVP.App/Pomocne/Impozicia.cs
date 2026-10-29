using System.Windows;

namespace SkVP.Pomocne;

/// <summary>
/// Kreslenie jednej strany listu – implementujú ho cieľ WPF (tlač, náhľad, obrázky) aj PDF. Súradnice sú v jednotkách
/// cieľa (jednotky WPF alebo body PDF), počiatok vľavo hore.
/// </summary>
public interface IPlatnoListu
{
    /// <summary>Strana zostavy orezaná na bunku: najprv zmenšená mierkou, potom posunutá.</summary>
    void Strana(int cislo, Rect bunka, double mierka, Vector posun);

    /// <summary>Tenký čierny rám.</summary>
    void Ramcek(Rect obdlznik);

    /// <summary>Prerušovaná čiara, kde sa list brožúry prekladá.</summary>
    void Prelozenie(Point zaciatok, Point koniec);

    /// <summary>Biely priehľadný závoj cez celý list (zosvetlenie).</summary>
    void Zavoj(Rect list, double nepriehladnost);
}

/// <summary>
/// Impozícia – rozmiestnenie strán zostavy na listy A4: ktoré strany prídu na ktorú stranu listu a kam presne.
/// Počíta len geometriu, nič nekreslí sama, takže tlač, náhľad, PDF aj obrázky majú rovnaký výsledok.
/// Strany listov sú číslované od 0 v poradí tlače; pri obojstrannej tlači je každá nepárna zadná strana.
/// </summary>
public sealed class Impozicia
{
    private const double SirkaA4Mm = 210;
    private const double VyskaA4Mm = 297;
    private const double OdsadenieRamcekaMm = 3;

    /// <summary>Počet strán brožúry doplnený na násobok 4 (inak počet strán zostavy).</summary>
    private readonly int _miestBrozury;

    public Impozicia(int pocetStran, VolbyTlace volby, bool stranyNaSirku)
    {
        Volby = volby.Opravene();
        PocetStran = Math.Max(0, pocetStran);
        var naList = Volby.StranNaList;

        // pri 2 a 8 stranách na liste sú strany voči listu otočené o 90° – list má opačnú orientáciu ako strany
        ListNaSirku = stranyNaSirku ^ (naList is 2 or 8);
        var (pozdlzDlhsej, pozdlzKratsej) = naList switch
        {
            2 => (2, 1),
            4 => (2, 2),
            8 => (4, 2),
            _ => (1, 1),
        };
        (Stlpce, Riadky) = ListNaSirku ? (pozdlzDlhsej, pozdlzKratsej) : (pozdlzKratsej, pozdlzDlhsej);

        var brozura = Volby.Rozmiestnenie == Rozmiestnenie.Brozura;
        _miestBrozury = brozura ? (PocetStran + 3) / 4 * 4 : PocetStran;
        PocetStranListov = PocetStran == 0 ? 0
            : brozura ? _miestBrozury / 2
            : Volby.OpakovatStranky ? PocetStran
            : (PocetStran + naList - 1) / naList;
    }

    public VolbyTlace Volby { get; }
    public int PocetStran { get; }
    public int Stlpce { get; }
    public int Riadky { get; }
    public bool ListNaSirku { get; }

    /// <summary>Počet strán listov, ktoré sa vytlačia (pri obojstrannej tlači dve na jeden list papiera).</summary>
    public int PocetStranListov { get; }

    /// <summary>Strana listu je zadná – okraj na dierovanie má na opačnej hrane.</summary>
    public bool JeZadna(int index) => Volby.TlacitObojstranne && index % 2 == 1;

    /// <summary>Veľkosť listu A4 v jednotkách cieľa.</summary>
    public Size List(double jednotiekNaMm) => A4(ListNaSirku, jednotiekNaMm);

    /// <summary>List A4 v zvolenej orientácii a jednotkách.</summary>
    public static Size A4(bool naSirku, double jednotiekNaMm) => naSirku
        ? new Size(VyskaA4Mm * jednotiekNaMm, SirkaA4Mm * jednotiekNaMm)
        : new Size(SirkaA4Mm * jednotiekNaMm, VyskaA4Mm * jednotiekNaMm);

    /// <summary>Strany zostavy (od 1) v bunkách strany listu zľava doprava a zhora nadol; null = prázdna bunka.</summary>
    public int?[] StranyNaListe(int index)
    {
        int? Strana(int cislo) => cislo >= 1 && cislo <= PocetStran ? cislo : null;
        if (Volby.Rozmiestnenie == Rozmiestnenie.Brozura)
        {
            // protiľahlé strany preloženého zošita majú súčet vždy o 1 väčší ako počet miest;
            // predná strana listu má nižšie číslo vpravo, zadná vľavo
            var nizsia = Strana(index + 1);
            var vyssia = Strana(_miestBrozury - index);
            return index % 2 == 0 ? new[] { vyssia, nizsia } : new[] { nizsia, vyssia };
        }

        var bunky = new int?[Stlpce * Riadky];
        for (var i = 0; i < bunky.Length; i++)
        {
            bunky[i] = Strana(Volby.OpakovatStranky ? index + 1 : index * bunky.Length + i + 1);
        }

        return bunky;
    }

    /// <summary>Bunky strany listu v rovnakom poradí ako <see cref="StranyNaListe"/> (bez okraja na dierovanie).</summary>
    public Rect[] Bunky(int index, double jednotiekNaMm)
    {
        var list = List(jednotiekNaMm);
        var dierovac = Volby.Dierovac ? Volby.DierovacMm * jednotiekNaMm : 0;
        var x0 = JeZadna(index) ? 0 : dierovac;
        var sirka = (list.Width - dierovac) / Stlpce;
        var vyska = list.Height / Riadky;
        var bunky = new Rect[Stlpce * Riadky];
        for (var i = 0; i < bunky.Length; i++)
        {
            bunky[i] = new Rect(x0 + i % Stlpce * sirka, i / Stlpce * vyska, sirka, vyska);
        }

        return bunky;
    }

    /// <summary>
    /// Mierka a posun strany v bunke. Bez zväčšenia sa do bunky zmestí celá strana. Zväčšenie odreže len prázdne okraje –
    /// obsah strany sa nikdy neoreže – a stred sa pritom plynule presúva zo stredu strany na stred obsahu.
    /// </summary>
    /// <param name="obsah">časť strany s textom a obrázkami; prázdna = celá strana</param>
    public static (double Mierka, Vector Posun) Umiestnenie(Size strana, Rect obsah, Rect bunka, double zvacsenie)
    {
        if (obsah.IsEmpty || obsah.Width < 1 || obsah.Height < 1)
        {
            obsah = new Rect(strana);
        }

        var naStranu = Math.Min(bunka.Width / strana.Width, bunka.Height / strana.Height);
        var naObsah = Math.Min(bunka.Width / obsah.Width, bunka.Height / obsah.Height);
        var mierka = Math.Max(naStranu, Math.Min(naStranu * zvacsenie, naObsah));
        var podiel = naObsah > naStranu ? Math.Clamp((mierka - naStranu) / (naObsah - naStranu), 0, 1) : 0;

        var stredStrany = new Point(strana.Width / 2, strana.Height / 2);
        var stredObsahu = new Point(obsah.X + obsah.Width / 2, obsah.Y + obsah.Height / 2);
        var stred = stredStrany + (stredObsahu - stredStrany) * podiel;
        var stredBunky = new Point(bunka.X + bunka.Width / 2, bunka.Y + bunka.Height / 2);
        return (mierka, new Vector(stredBunky.X - stred.X * mierka, stredBunky.Y - stred.Y * mierka));
    }

    /// <summary>Nakreslí stranu listu na plátno.</summary>
    /// <param name="rozmerStrany">veľkosť strany zostavy a jej obsah (v jednotkách cieľa)</param>
    public void Nakresli(int index, double jednotiekNaMm, IPlatnoListu platno, Func<int, (Size Velkost, Rect Obsah)> rozmerStrany)
    {
        var strany = StranyNaListe(index);
        var bunky = Bunky(index, jednotiekNaMm);
        var odsadenie = OdsadenieRamcekaMm * jednotiekNaMm;
        for (var i = 0; i < bunky.Length; i++)
        {
            var bunka = bunky[i];
            if (strany[i] is int cislo)
            {
                var (velkost, obsah) = rozmerStrany(cislo);
                var (mierka, posun) = Umiestnenie(velkost, obsah, bunka, Volby.Zvacsenie);
                platno.Strana(cislo, bunka, mierka, posun);
            }

            // pri jednej strane na liste sa prázdny list neorámuje
            if (Volby.Ramcek && (strany[i] is not null || bunky.Length > 1))
            {
                platno.Ramcek(new Rect(bunka.X + odsadenie, bunka.Y + odsadenie,
                    Math.Max(1, bunka.Width - 2 * odsadenie), Math.Max(1, bunka.Height - 2 * odsadenie)));
            }
        }

        var list = List(jednotiekNaMm);
        if (Volby.Rozmiestnenie == Rozmiestnenie.Brozura && Volby.Ramcek)
        {
            platno.Prelozenie(new Point(list.Width / 2, 0), new Point(list.Width / 2, list.Height));
        }

        if (Volby.Zosvetlit)
        {
            platno.Zavoj(new Rect(list), VolbyTlace.SilaZosvetlenia);
        }
    }
}
