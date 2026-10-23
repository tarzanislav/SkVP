using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>
/// Pri premenovaní predmetu alebo oblasti prenesie nový názov aj do textov dokumentov Word uložených v programe
/// (osnovy, charakteristiky oblastí, text úvodnej strany). Nahrádza sa len celý názov (nie časť slova a nie časť dlhšieho
/// názvu iného predmetu či oblasti, napr. „Matematika“ v „Matematika a práca s informáciami“). Formátovanie dokumentu
/// zostane; doterajšie znenie sa pred zmenou zarchivuje.
/// </summary>
public static class PremenovanieVDokumentoch
{
    public sealed record Vysledok(List<long> Zmenene, List<long> Vynechane);

    /// <summary>Nahradí názov vo všetkých aktuálnych dokumentoch okrem tých, ktoré sa práve upravujú vo Worde.</summary>
    public static Vysledok NahradVsade(string stary, string novy, IReadOnlyCollection<long> vynechat)
    {
        var vysledok = new Vysledok(new List<long>(), new List<long>());
        stary = stary.Trim();
        novy = novy.Trim();
        if (stary.Length == 0 || stary == novy)
        {
            return vysledok;
        }

        var chranene = DlhsieNazvy(stary);
        var dokumenty = new DokumentService();
        foreach (var id in IdDokumentov())
        {
            if (dokumenty.NacitajData(id) is not { Length: > 0 } data)
            {
                continue;
            }

            byte[]? nove;
            try
            {
                nove = Nahrad(data, stary, novy, chranene, out _);
            }
            catch (Exception)
            {
                continue; // poškodený dokument sa nemení
            }

            if (nove is null)
            {
                continue;
            }

            if (vynechat.Contains(id))
            {
                vysledok.Vynechane.Add(id);
                continue;
            }

            if (dokumenty.Uloz(id, nove, $"Pred premenovaním „{stary}“ na „{novy}“"))
            {
                vysledok.Zmenene.Add(id);
            }
        }

        return vysledok;
    }

    private static List<long> IdDokumentov()
    {
        var idcka = new List<long>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db,
            "SELECT d.objekt_id FROM dokument d JOIN objekt o ON o.id = d.objekt_id WHERE o.odstranene IS NULL ORDER BY d.objekt_id");
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            idcka.Add(citac.GetInt64(0));
        }

        return idcka;
    }

    /// <summary>Názvy iných predmetov a oblastí, ktoré starý názov obsahujú – v nich sa nič nenahrádza.</summary>
    private static List<string> DlhsieNazvy(string stary)
    {
        var nazvy = new List<string>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db, "SELECT DISTINCT nazov FROM objekt WHERE typ IN ($o, $p, $c) AND odstranene IS NULL",
            ("$o", TypObjektu.Oblast), ("$p", TypObjektu.Predmet), ("$c", TypObjektu.Cast));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            var nazov = citac.GetString(0);
            if (nazov.Length > stary.Length && nazov.Contains(stary, StringComparison.Ordinal))
            {
                nazvy.Add(nazov);
            }
        }

        return nazvy;
    }

    /// <summary>
    /// Nahradí celé výskyty starého názvu v dokumente Word (telo, hlavičky, päty). Vráti nové dáta, alebo null,
    /// ak sa názov v dokumente nenachádza.
    /// </summary>
    public static byte[]? Nahrad(byte[] data, string stary, string novy, IReadOnlyList<string> chranene, out int pocet)
    {
        pocet = 0;
        using var pamat = new MemoryStream();
        pamat.Write(data, 0, data.Length);
        pamat.Position = 0;
        using (var docx = WordprocessingDocument.Open(pamat, true))
        {
            var hlavna = docx.MainDocumentPart;
            if (hlavna?.Document is null)
            {
                return null;
            }

            var korene = new List<OpenXmlElement> { hlavna.Document };
            korene.AddRange(hlavna.HeaderParts.Select(h => h.Header).OfType<OpenXmlElement>());
            korene.AddRange(hlavna.FooterParts.Select(f => f.Footer).OfType<OpenXmlElement>());
            foreach (var koren in korene)
            {
                foreach (var odsek in koren.Descendants<Paragraph>().ToList())
                {
                    pocet += NahradVOdseku(odsek, stary, novy, chranene);
                }
            }

            if (pocet == 0)
            {
                return null;
            }

            hlavna.Document.Save();
            foreach (var h in hlavna.HeaderParts)
            {
                h.Header?.Save();
            }

            foreach (var f in hlavna.FooterParts)
            {
                f.Footer?.Save();
            }
        }

        return pamat.ToArray();
    }

    /// <summary>Nahradí názov v jednom odseku, aj keď je text rozdelený do viacerých behov (časť formátovania).</summary>
    public static int NahradVOdseku(Paragraph odsek, string stary, string novy, IReadOnlyList<string> chranene)
    {
        var texty = odsek.Descendants<Text>().Where(t => t.Ancestors<Paragraph>().FirstOrDefault() == odsek).ToList();
        if (texty.Count == 0)
        {
            return 0;
        }

        var spolu = string.Concat(texty.Select(t => t.Text));
        var zhody = Zhody(spolu, stary, chranene);
        for (var z = zhody.Count - 1; z >= 0; z--)
        {
            // od poslednej zhody – zmeny za ňou neposunú pozície skorších zhôd
            var zaciatok = zhody[z];
            var koniec = zaciatok + stary.Length;
            var pozicia = 0;
            var vlozene = false;
            foreach (var t in texty)
            {
                var text = t.Text;
                var od = pozicia;
                var po = pozicia + text.Length;
                pozicia = po;
                if (po <= zaciatok || od >= koniec)
                {
                    continue;
                }

                var a = Math.Max(zaciatok, od) - od;
                var b = Math.Min(koniec, po) - od;
                t.Text = text[..a] + (vlozene ? "" : novy) + text[b..];
                t.Space = SpaceProcessingModeValues.Preserve;
                vlozene = true;
            }
        }

        return zhody.Count;
    }

    /// <summary>Začiatky celých výskytov názvu (nie časť slova a nie časť chráneného dlhšieho názvu).</summary>
    public static List<int> Zhody(string text, string nazov, IReadOnlyList<string> chranene)
    {
        var zhody = new List<int>();
        var start = 0;
        while (start <= text.Length - nazov.Length)
        {
            var i = text.IndexOf(nazov, start, StringComparison.Ordinal);
            if (i < 0)
            {
                break;
            }

            var koniec = i + nazov.Length;
            var celeSlovo = (i == 0 || !char.IsLetterOrDigit(text[i - 1])) && (koniec == text.Length || !char.IsLetterOrDigit(text[koniec]));
            if (celeSlovo && !JeVChranenom(text, i, koniec, chranene))
            {
                zhody.Add(i);
                start = koniec;
            }
            else
            {
                start = i + 1;
            }
        }

        return zhody;
    }

    private static bool JeVChranenom(string text, int zaciatok, int koniec, IReadOnlyList<string> chranene)
    {
        foreach (var dlhsi in chranene)
        {
            var hladaj = Math.Max(0, koniec - dlhsi.Length);
            while (hladaj <= zaciatok)
            {
                var j = text.IndexOf(dlhsi, hladaj, StringComparison.Ordinal);
                if (j < 0 || j > zaciatok)
                {
                    break;
                }

                if (j + dlhsi.Length >= koniec)
                {
                    return true;
                }

                hladaj = j + 1;
            }
        }

        return false;
    }
}
