using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Čo má celý dokument učebných osnov obsahovať.</summary>
public sealed record VolbyZostavy
{
    /// <summary>Vybrané oblasti (null = všetky).</summary>
    public IReadOnlyCollection<long>? Oblasti { get; init; }

    /// <summary>Úvodná strana: škola, logo, „Školský vzdelávací program – Učebné osnovy“.</summary>
    public bool TitulnaStrana { get; init; } = true;
    public bool Obsah { get; init; } = true;

    /// <summary>Oddeľujúca strana pred každou oblasťou (názov oblasti a zoznam jej predmetov).</summary>
    public bool OddelovacOblasti { get; init; } = true;

    /// <summary>Oddeľujúca strana pred každým predmetom (názov predmetu).</summary>
    public bool OddelovacPredmetu { get; init; }

    public bool CharakteristikyOblasti { get; init; } = true;

    /// <summary>Zaradiť aj predmety, ktorých osnova ešte nemá text (s poznámkou, že nie je spracovaná).</summary>
    public bool NespracovanePredmety { get; init; } = true;
}

/// <summary>Základné údaje o škole a programe (Nastavenia).</summary>
public sealed record UdajeSkVP(string Skola, string Adresa, string NazovProgramu, string Typ, string Obdobie, string? Logo)
{
    public const string KlucSkola = "skvp.skola";
    public const string KlucAdresa = "skvp.adresa";
    public const string KlucNazov = "skvp.nazov";
    public const string KlucObdobie = "skvp.obdobie";

    public const string PredvolenyNazov = "Školský vzdelávací program";

    public static UdajeSkVP Nacitaj() => new(
        Databaza.Citaj(KlucSkola, ""),
        Databaza.Citaj(KlucAdresa, ""),
        Databaza.Citaj(KlucNazov, PredvolenyNazov),
        Databaza.Citaj(UcebnyPlanService.KlucTypSkoly, ""),
        Databaza.Citaj(KlucObdobie, ""),
        NastaveniaSkoly.CestaLoga());
}

public sealed record KapitolaPredmetu(long Id, int Poradie, string Nazov, List<Blok> Obsah, StavOsnovy Stav)
{
    public string Cislo => Poradie.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record KapitolaOblasti(long Id, string Nazov, string Farba, List<Blok> Charakteristika, List<KapitolaPredmetu> Predmety);

/// <summary>Učebné osnovy zostavené do jedného celku – podklad pre náhľad, tlač, PDF a Word.</summary>
public sealed record CelyDokument(UdajeSkVP Udaje, VolbyZostavy Volby, List<KapitolaOblasti> Oblasti, DateTime Vytvorene)
{
    /// <summary>Texty úvodnej strany a hlavičky (dajú sa upraviť a vypnúť).</summary>
    public TitulnaStrana Titul { get; init; } = new();

    public const string Podtitul = "Učebné osnovy";

    public int PocetPredmetov => Oblasti.Sum(o => o.Predmety.Count);
}

public static class ZostavaOsnov
{
    /// <summary>Zostaví dokument z databázy (volať na vlákne používateľského rozhrania – načítava dokumenty WPF).</summary>
    public static CelyDokument Zostav(VolbyZostavy volby)
    {
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var oblasti = new List<KapitolaOblasti>();
        var index = 0;
        foreach (var oblast in sluzba.Oblasti())
        {
            index++;
            if (volby.Oblasti is not null && !volby.Oblasti.Contains(oblast.Id))
            {
                continue;
            }

            var predmety = new List<KapitolaPredmetu>();
            var poradie = 0;
            foreach (var predmet in sluzba.Deti(oblast.Id).Where(p => p.Typ == TypObjektu.Predmet))
            {
                poradie++;
                var stav = StavyOsnovy.Urci(predmet.Stav, predmet.DlzkaTextu);
                if (stav == StavOsnovy.Prazdna && !volby.NespracovanePredmety)
                {
                    continue;
                }

                predmety.Add(new KapitolaPredmetu(predmet.Id, poradie, predmet.Nazov, NacitajBloky(dokumenty, predmet.Id), stav));
            }

            var charakteristika = volby.CharakteristikyOblasti ? NacitajBloky(dokumenty, oblast.Id) : new List<Blok>();
            oblasti.Add(new KapitolaOblasti(oblast.Id, oblast.Nazov, oblast.Farba ?? UcebnyPlanService.FarbaOblasti(index - 1),
                charakteristika, predmety));
        }

        return new CelyDokument(UdajeSkVP.Nacitaj(), volby, oblasti, DateTime.Now) { Titul = TitulnaStrana.Nacitaj() };
    }

    private static List<Blok> NacitajBloky(DokumentService dokumenty, long id)
    {
        try
        {
            return dokumenty.Nacitaj(id).Bloky;
        }
        catch (SkvpException ex)
        {
            return new List<Blok> { new Odsek(new[] { new Usek(ex.Message, Kurziva: true) }) };
        }
    }

    /// <summary>Názov súboru pre export (bez nepovolených znakov).</summary>
    public static string NazovSuboru(CelyDokument dokument, string pripona)
    {
        var zaklad = $"Ucebne_osnovy_{dokument.Vytvorene:yyyy-MM-dd}";
        if (dokument.Udaje.Skola.Length > 0)
        {
            var skola = new string(dokument.Udaje.Skola.Where(c => !System.IO.Path.GetInvalidFileNameChars().Contains(c)).ToArray()).Trim();
            if (skola.Length > 0)
            {
                zaklad = $"Ucebne_osnovy_{skola}_{dokument.Vytvorene:yyyy-MM-dd}";
            }
        }

        return zaklad.Replace(' ', '_') + pripona;
    }
}
