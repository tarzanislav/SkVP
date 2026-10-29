using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using SkVP.Data;
using SkVP.Services;

namespace SkVP.Pomocne;

/// <summary>Tlačiarne Windows: zoznam, otvorenie podľa názvu, tlačový lístok a odoslanie strán.</summary>
public static class TlacoveFronty
{
    private static readonly EnumeratedPrintQueueTypes[] Druhy = { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections };

    /// <summary>Názvy tlačiarní (zoradené) a predvolená tlačiareň. Bez služby zaraďovača tlače je zoznam prázdny.</summary>
    public static (IReadOnlyList<string> Nazvy, string? Predvolena) Zoznam()
    {
        try
        {
            using var server = new LocalPrintServer();
            var nazvy = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var fronta in server.GetPrintQueues(Druhy))
            {
                using (fronta)
                {
                    nazvy.Add(fronta.FullName);
                }
            }

            using var predvolena = LocalPrintServer.GetDefaultPrintQueue();
            return (nazvy.ToList(), predvolena?.FullName);
        }
        catch (Exception)
        {
            // služba zaraďovača tlače nebeží alebo zoznam nie je dostupný
            return (Array.Empty<string>(), null);
        }
    }

    /// <summary>Tlačiareň podľa celého názvu (lokálna alebo pripojená zo siete); platí, kým je otvorený server.</summary>
    public static PrintQueue Otvor(LocalPrintServer server, string nazov)
    {
        PrintQueue? hladana = null;
        foreach (var fronta in server.GetPrintQueues(Druhy))
        {
            if (hladana is null && fronta.FullName == nazov)
            {
                hladana = fronta;
            }
            else
            {
                fronta.Dispose();
            }
        }

        return hladana ?? throw new SkvpException(Preklad.F($"Tlačiareň „{nazov}“ sa nenašla."));
    }

    /// <summary>
    /// Tlačový lístok: A4, orientácia, obojstranne (pri liste na šírku po kratšej hrane), kópie a prípadne sivá.
    /// Vychádza z nastavení tlačiarne a zosúladí sa s jej možnosťami; orientácia sa pri tom nesmie zmeniť.
    /// </summary>
    public static PrintTicket Tiket(PrintQueue fronta, bool listNaSirku, bool obojstranne, int kopie = 1, bool siva = false)
    {
        var zaklad = fronta.UserPrintTicket ?? fronta.DefaultPrintTicket;
        var poziadavka = zaklad.Clone();
        poziadavka.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
        poziadavka.PageOrientation = listNaSirku ? PageOrientation.Landscape : PageOrientation.Portrait;
        poziadavka.CopyCount = kopie;
        poziadavka.Collation = Collation.Collated;
        poziadavka.Duplexing = (obojstranne, listNaSirku) switch
        {
            (false, _) => Duplexing.OneSided,
            (true, true) => Duplexing.TwoSidedShortEdge,
            (true, false) => Duplexing.TwoSidedLongEdge,
        };
        if (siva)
        {
            poziadavka.OutputColor = OutputColor.Grayscale;
        }

        try
        {
            var overeny = fronta.MergeAndValidatePrintTicket(zaklad, poziadavka).ValidatedPrintTicket;
            overeny.PageOrientation = poziadavka.PageOrientation;
            return overeny;
        }
        catch (Exception)
        {
            // ovládač lístok neoverí – použije sa požadovaný
            return poziadavka;
        }
    }

    /// <summary>Pošle strany (kreslia sa až pri tlači) ako jednu tlačovú úlohu.</summary>
    public static void Posli(PrintQueue fronta, PrintTicket tiket, Size velkost, IReadOnlyList<Func<Visual>> strany, string nazovUlohy)
    {
        fronta.CurrentJobSettings.Description = nazovUlohy;
        PrintQueue.CreateXpsDocumentWriter(fronta).Write(new Strankovac(strany, velkost), tiket);
    }

    /// <summary>Strany rovnakej veľkosti pre zapisovač XPS.</summary>
    private sealed class Strankovac(IReadOnlyList<Func<Visual>> strany, Size velkost) : DocumentPaginator
    {
        public override bool IsPageCountValid => true;

        public override int PageCount => strany.Count;

        public override Size PageSize { get; set; } = velkost;

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            var plocha = new Rect(PageSize);
            return new DocumentPage(strany[pageNumber](), PageSize, plocha, plocha);
        }
    }
}
