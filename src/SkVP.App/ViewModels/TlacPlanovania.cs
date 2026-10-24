using MigraDoc.DocumentObjectModel;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>Tlač a PDF kalendára, úloh a poznámok cez okno tlače SkVP (náhľad, tlačiareň, voľby PDF).</summary>
public static class TlacPlanovania
{
    /// <summary>Zobrazené obdobie kalendára: mesiac, školský rok, alebo týždeň či zoznam ako zoznam po dňoch.</summary>
    public static void Kalendar(KalendarViewModel kalendar, bool pdf)
    {
        var (od, po) = kalendar.Obdobie();
        var dni = kalendar.DniObdobia(od, po)
            .Select(d => new DenZostavy(d.Datum, d.Volno, d.Vikend, d.Sviatok, d.Prazdniny, d.Meniny, d.Vyskyty, d.Poznamky))
            .ToList();
        var (tvorba, naSirku, nazov) = kalendar.Rezim switch
        {
            RezimKalendara.Mesiac => ((Func<Document>)(() => ZostavaPlanovania.Mesiac(od, dni)), true, $"Kalendar {od:yyyy-MM}"),
            RezimKalendara.SkolskyRok => (() => ZostavaPlanovania.SkolskyRok(ArchivSkolskehoRoka.Aktualny(od), dni), true,
                $"Skolsky rok {ArchivSkolskehoRoka.Aktualny(od).Replace('/', '-')}"),
            _ => (() => ZostavaPlanovania.Zoznam(od, po, dni), false, $"Kalendar {od:yyyy-MM-dd}"),
        };
        Otvor(tvorba, naSirku, "Kalendár", nazov + ".pdf", pdf);
    }

    public static void Ulohy(IReadOnlyList<UlohaRiadok> ulohy, string podnadpis, bool pdf) =>
        Otvor(() => ZostavaPlanovania.Ulohy(ulohy, podnadpis), false, "Úlohy", "Ulohy.pdf", pdf);

    public static void Poznamky(IReadOnlyList<PoznamkaRiadok> poznamky, string podnadpis, bool pdf) =>
        Otvor(() => ZostavaPlanovania.Poznamky(poznamky, podnadpis), false, "Poznámky", "Poznamky.pdf", pdf);

    private static void Otvor(Func<Document> tvorba, bool naSirku, string nazovUlohy, string subor, bool pdf)
    {
        try
        {
            if (Dialogy.TichyRezim)
            {
                // samotest: dokument sa zostaví a uloží bez okna
                var cesta = Dialogy.UlozitAko("PDF", "PDF|*.pdf", subor);
                if (cesta is not null)
                {
                    SuboryZostavy.Uloz(tvorba(), cesta, new VolbyPdf());
                }

                return;
            }

            var vykreslenie = NahladDokumentu.Priprav(tvorba());
            new Views.TlacWindow(vykreslenie, naSirku, nazovUlohy, pdf ? Views.RezimOknaTlace.Pdf : Views.RezimOknaTlace.Tlac, tvorba, pdf ? subor : null)
            {
                Owner = Dialogy.Vlastnik,
            }.ShowDialog();
        }
        catch (Exception ex)
        {
            Dialogy.Chyba($"{nazovUlohy} sa nepodarilo pripraviť na tlač.", ex);
        }
    }
}
