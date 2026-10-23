using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using ClosedXML.Excel;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>Archív osnov jedného školského roka.</summary>
public sealed record ZaznamArchivuRoka(long Id, string SkolskyRok, DateTime Vytvorene, string? Priecinok, int Dokumentov, int Predmetov, string? Poznamka)
{
    public bool MaPrehlad => Predmetov > 0;
}

/// <summary>Predmet (alebo charakteristika oblasti) v archíve školského roka – tak, ako bol v danom roku.</summary>
public sealed record PolozkaArchivuRoka(
    long Id, long? ObjektId, string Typ, string Oblast, string? Farba, int PoradieOblasti, int Cislo, string Nazov, StavOsnovy Stav,
    int Dlzka, string? Profilacie, string? Platnost, int? RokOsnovy, string? Uroven, bool ZmenenaVRoku, string? Sha)
{
    public bool JePredmet => Typ == TypObjektu.Predmet;
    public string CisloText => Cislo.ToString("00", CultureInfo.InvariantCulture);
    public bool MaDokument => Sha is not null;
}

/// <summary>Štatistiky archivovaného školského roka (počty predmetov podľa stavu, rozsah, úrovne jazykov).</summary>
public sealed record StatistikyArchivu(int Oblasti, int Predmety, int SOsnovou, int Hotove, int NaKontrolu, int Rozpracovane, int Nezacate,
    int ZmeneneVRoku, int Znakov, int UrovenB1, int UrovenB2, int SProfilaciami, int SPlatnostou)
{
    /// <summary>Normostrany (1 800 znakov).</summary>
    public double Normostrany => Math.Round(Znakov / 1800.0, 1);

    public int Percento(int pocet) => Predmety == 0 ? 0 : (int)Math.Round(100.0 * pocet / Predmety);
}

/// <summary>Porovnanie archívu s predchádzajúcim archivovaným školským rokom.</summary>
public sealed record PorovnanieRokov(string PredchadzajuciRok, int NovePredmety, int OdstranenePredmety, int ZmeneneOsnovy, int RovnakeOsnovy,
    IReadOnlySet<long> Nove, IReadOnlySet<long> Zmenene);

/// <summary>
/// Archivácia celých učebných osnov za školský rok (napr. 2025/2026): snímka všetkých predmetov a osnov v databáze
/// (sekcia Archív – prehliadanie a štatistiky roka), kópia do archívu verzií („Archív školského roka 2025/2026“)
/// a priečinok Dokumenty\SkVP\Archív školských rokov\2025-2026 (osnovy vo Worde po oblastiach, celé osnovy Word a PDF/A).
/// </summary>
public static class ArchivSkolskehoRoka
{
    public static string Priecinok => Path.Combine(Databaza.PriecinokDokumentov, "Archív školských rokov");

    /// <summary>Školský rok podľa dátumu: od septembra nový (23. 10. 2026 → 2026/2027).</summary>
    public static string Aktualny(DateTime? datum = null)
    {
        var d = datum ?? DateTime.Today;
        var zaciatok = d.Month >= 9 ? d.Year : d.Year - 1;
        return $"{zaciatok}/{zaciatok + 1}";
    }

    /// <summary>Školské roky na výber: aktuálny a päť predchádzajúcich.</summary>
    public static IReadOnlyList<string> Roky()
    {
        var zaciatok = int.Parse(Aktualny()[..4], CultureInfo.InvariantCulture);
        return Enumerable.Range(0, 6).Select(i => $"{zaciatok - i}/{zaciatok - i + 1}").ToList();
    }

    /// <summary>Začiatok a koniec školského roka (1. 9. – 31. 8.).</summary>
    public static (DateTime Od, DateTime Do) Obdobie(string skolskyRok)
    {
        var zaciatok = int.Parse(skolskyRok[..4], CultureInfo.InvariantCulture);
        return (new DateTime(zaciatok, 9, 1), new DateTime(zaciatok + 1, 9, 1).AddTicks(-1));
    }

    /// <summary>Archívy od najnovšieho školského roka.</summary>
    public static List<ZaznamArchivuRoka> Zoznam()
    {
        var zoznam = new List<ZaznamArchivuRoka>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db, """
            SELECT a.id, a.skolsky_rok, a.vytvorene, a.priecinok, a.poznamka,
                   (SELECT count(*) FROM archiv_polozka p WHERE p.archiv_id = a.id AND p.data IS NOT NULL),
                   (SELECT count(*) FROM archiv_polozka p WHERE p.archiv_id = a.id AND p.typ = 'predmet')
            FROM archiv_rok a ORDER BY a.skolsky_rok DESC, a.id DESC
            """);
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            zoznam.Add(new ZaznamArchivuRoka(citac.GetInt64(0), citac.GetString(1), Datum(citac.GetString(2)),
                citac.IsDBNull(3) ? null : citac.GetString(3), citac.GetInt32(5), citac.GetInt32(6), citac.IsDBNull(4) ? null : citac.GetString(4)));
        }

        return zoznam;
    }

    public static List<PolozkaArchivuRoka> Polozky(long archivId)
    {
        var polozky = new List<PolozkaArchivuRoka>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db, """
            SELECT id, objekt_id, typ, oblast, farba, poradie_oblasti, cislo, nazov, stav, dlzka, profilacie, platnost,
                   rok_osnovy, uroven, zmenena_v_roku, sha256
            FROM archiv_polozka WHERE archiv_id = $a ORDER BY poradie_oblasti, typ <> 'charakteristika', cislo
            """, ("$a", archivId));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            polozky.Add(new PolozkaArchivuRoka(
                citac.GetInt64(0), citac.IsDBNull(1) ? null : citac.GetInt64(1), citac.GetString(2), citac.GetString(3),
                citac.IsDBNull(4) ? null : citac.GetString(4), citac.GetInt32(5), citac.GetInt32(6), citac.GetString(7),
                Enum.TryParse<StavOsnovy>(citac.GetString(8), out var stav) ? stav : StavOsnovy.Prazdna, citac.GetInt32(9),
                citac.IsDBNull(10) ? null : citac.GetString(10), citac.IsDBNull(11) ? null : citac.GetString(11),
                citac.IsDBNull(12) ? null : citac.GetInt32(12), citac.IsDBNull(13) ? null : citac.GetString(13),
                citac.GetInt32(14) != 0, citac.IsDBNull(15) ? null : citac.GetString(15)));
        }

        return polozky;
    }

    /// <summary>Dokument osnovy z archívu (Word).</summary>
    public static byte[]? Data(long polozkaId)
    {
        using var db = Databaza.Otvor();
        return Databaza.Skalar(db, "SELECT data FROM archiv_polozka WHERE id = $id", ("$id", polozkaId)) as byte[];
    }

    /// <summary>Štatistiky z položiek archívu.</summary>
    public static StatistikyArchivu Statistiky(IReadOnlyList<PolozkaArchivuRoka> polozky)
    {
        var predmety = polozky.Where(p => p.JePredmet).ToList();
        string? Uroven(PolozkaArchivuRoka p) => p.Uroven ?? JazykovePravidla.UrovenZNazvu(p.Nazov);
        return new StatistikyArchivu(
            polozky.Select(p => p.PoradieOblasti).Distinct().Count(), predmety.Count, predmety.Count(p => p.MaDokument),
            predmety.Count(p => p.Stav == StavOsnovy.Hotova), predmety.Count(p => p.Stav == StavOsnovy.NaKontrolu),
            predmety.Count(p => p.Stav == StavOsnovy.Rozpracovana), predmety.Count(p => p.Stav == StavOsnovy.Prazdna),
            predmety.Count(p => p.ZmenenaVRoku), polozky.Sum(p => p.Dlzka),
            predmety.Count(p => Uroven(p) == "B1"), predmety.Count(p => Uroven(p) == "B2"),
            predmety.Count(p => p.Profilacie is { Length: > 0 }), predmety.Count(p => p.Platnost is { Length: > 0 }));
    }

    /// <summary>
    /// Archívy z verzie 0.1.37 (zoznam v nastaveniach, bez snímky predmetov) sa prenesú do tabuľky archívov,
    /// aby boli vidieť v sekcii Archív aspoň s priečinkom.
    /// </summary>
    public static void PrenesStareZaznamy()
    {
        const string kluc = "archiv.skolske_roky";
        if (Databaza.Citaj(kluc) is not { Length: > 0 } json)
        {
            return;
        }

        try
        {
            using var db = Databaza.Otvor();
            foreach (var stary in System.Text.Json.JsonSerializer.Deserialize<List<StaryZaznam>>(json) ?? new())
            {
                Databaza.Vykonaj(db, "INSERT INTO archiv_rok(skolsky_rok, vytvorene, priecinok, poznamka) VALUES($r, $v, $p, $n)",
                    ("$r", stary.SkolskyRok), ("$v", stary.Vytvorene.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)),
                    ("$p", stary.Priecinok), ("$n", "bez prehľadu predmetov (archív staršej verzie programu)"));
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // poškodený zoznam – priečinky archívov zostávajú na disku
        }

        Databaza.Zapis(kluc, null);
    }

    private sealed record StaryZaznam(string SkolskyRok, DateTime Vytvorene, string Priecinok, int Dokumentov);

    /// <summary>Porovnanie s predchádzajúcim archivovaným školským rokom (alebo null, ak taký nie je).</summary>
    public static PorovnanieRokov? Porovnaj(ZaznamArchivuRoka archiv)
    {
        var predchadzajuci = Zoznam().Where(z => string.CompareOrdinal(z.SkolskyRok, archiv.SkolskyRok) < 0)
            .OrderByDescending(z => z.SkolskyRok).ThenByDescending(z => z.Id).FirstOrDefault();
        if (predchadzajuci is null)
        {
            return null;
        }

        var teraz = Polozky(archiv.Id).Where(p => p.JePredmet && p.ObjektId is not null).ToList();
        var predtym = Polozky(predchadzajuci.Id).Where(p => p.JePredmet && p.ObjektId is not null).ToDictionary(p => p.ObjektId!.Value);
        var nove = teraz.Where(p => !predtym.ContainsKey(p.ObjektId!.Value)).Select(p => p.ObjektId!.Value).ToHashSet();
        var zmenene = teraz.Where(p => predtym.TryGetValue(p.ObjektId!.Value, out var s) && s.Sha != p.Sha).Select(p => p.ObjektId!.Value).ToHashSet();
        var teraziIds = teraz.Select(p => p.ObjektId!.Value).ToHashSet();
        return new PorovnanieRokov(predchadzajuci.SkolskyRok, nove.Count, predtym.Keys.Count(k => !teraziIds.Contains(k)), zmenene.Count,
            teraz.Count - nove.Count - zmenene.Count, nove, zmenene);
    }

    /// <summary>Štatistiky a zoznam predmetov archívu do zošita Excel (hárky Štatistiky, Oblasti, Predmety).</summary>
    public static void UlozStatistiky(ZaznamArchivuRoka archiv, string cesta)
    {
        var polozky = Polozky(archiv.Id);
        var s = Statistiky(polozky);
        var porovnanie = Porovnaj(archiv);
        using var zosit = new XLWorkbook();

        var h = zosit.Worksheets.Add("Štatistiky");
        h.Cell(1, 1).Value = $"Učebné osnovy – školský rok {archiv.SkolskyRok}";
        h.Cell(1, 1).Style.Font.Bold = true;
        h.Cell(1, 1).Style.Font.FontSize = 14;
        h.Cell(2, 1).Value = $"{Databaza.Citaj(UdajeSkVP.KlucSkola, "")}  •  archivované {archiv.Vytvorene.ToString("d. M. yyyy H:mm", CultureInfo.GetCultureInfo("sk-SK"))}";
        var riadky = new List<(string, XLCellValue, string)>
        {
            ("Vzdelávacie oblasti", s.Oblasti, ""),
            ("Predmety", s.Predmety, ""),
            ("Predmety s osnovou (dokument)", s.SOsnovou, $"{s.Percento(s.SOsnovou)} %"),
            ("Hotové osnovy", s.Hotove, $"{s.Percento(s.Hotove)} %"),
            ("Na kontrolu", s.NaKontrolu, $"{s.Percento(s.NaKontrolu)} %"),
            ("Rozpracované", s.Rozpracovane, $"{s.Percento(s.Rozpracovane)} %"),
            ("Nezačaté", s.Nezacate, $"{s.Percento(s.Nezacate)} %"),
            ("Zmenené počas školského roka", s.ZmeneneVRoku, $"{s.Percento(s.ZmeneneVRoku)} %"),
            ("Rozsah textu (znaky)", s.Znakov, ""),
            ("Rozsah textu (normostrany)", s.Normostrany, ""),
            ("Cudzie jazyky na úrovni B1", s.UrovenB1, ""),
            ("Cudzie jazyky na úrovni B2", s.UrovenB2, ""),
            ("Osnovy s profiláciami", s.SProfilaciami, ""),
            ("Osnovy s platnosťou", s.SPlatnostou, ""),
        };
        if (porovnanie is { } p)
        {
            riadky.Add(($"Nové predmety oproti {p.PredchadzajuciRok}", p.NovePredmety, ""));
            riadky.Add(($"Zmenené osnovy oproti {p.PredchadzajuciRok}", p.ZmeneneOsnovy, ""));
            riadky.Add(($"Osnovy bez zmeny oproti {p.PredchadzajuciRok}", p.RovnakeOsnovy, ""));
            riadky.Add(($"Odstránené predmety oproti {p.PredchadzajuciRok}", p.OdstranenePredmety, ""));
        }

        var r = 4;
        foreach (var (popis, hodnota, doplnok) in riadky)
        {
            h.Cell(r, 1).Value = popis;
            h.Cell(r, 2).Value = hodnota;
            h.Cell(r, 3).Value = doplnok;
            r++;
        }

        h.Column(1).Width = 44;
        h.Column(2).Width = 12;
        h.Column(3).Width = 10;

        var o = zosit.Worksheets.Add("Oblasti");
        string[] hlavickaOblasti = { "Vzdelávacia oblasť", "Predmety", "Hotové", "Na kontrolu", "Rozpracované", "Nezačaté", "Zmenené v roku", "Normostrany" };
        for (var i = 0; i < hlavickaOblasti.Length; i++)
        {
            o.Cell(1, i + 1).Value = hlavickaOblasti[i];
        }

        r = 2;
        foreach (var skupina in polozky.GroupBy(x => (x.PoradieOblasti, x.Oblast)))
        {
            var pr = skupina.Where(x => x.JePredmet).ToList();
            o.Cell(r, 1).Value = skupina.Key.Oblast;
            o.Cell(r, 2).Value = pr.Count;
            o.Cell(r, 3).Value = pr.Count(x => x.Stav == StavOsnovy.Hotova);
            o.Cell(r, 4).Value = pr.Count(x => x.Stav == StavOsnovy.NaKontrolu);
            o.Cell(r, 5).Value = pr.Count(x => x.Stav == StavOsnovy.Rozpracovana);
            o.Cell(r, 6).Value = pr.Count(x => x.Stav == StavOsnovy.Prazdna);
            o.Cell(r, 7).Value = pr.Count(x => x.ZmenenaVRoku);
            o.Cell(r, 8).Value = Math.Round(skupina.Sum(x => x.Dlzka) / 1800.0, 1);
            r++;
        }

        Format(o, hlavickaOblasti.Length);

        var t = zosit.Worksheets.Add("Predmety");
        string[] hlavicka = { "Oblasť", "Č.", "Predmet", "Stav", "Profilácie", "Platnosť", "Úroveň", "Normostrany", "Zmenená v roku", "Oproti min. roku" };
        for (var i = 0; i < hlavicka.Length; i++)
        {
            t.Cell(1, i + 1).Value = hlavicka[i];
        }

        r = 2;
        foreach (var x in polozky.Where(x => x.JePredmet))
        {
            t.Cell(r, 1).Value = x.Oblast;
            t.Cell(r, 2).Value = x.Cislo;
            t.Cell(r, 3).Value = x.Nazov;
            t.Cell(r, 4).Value = StavyOsnovy.Text(x.Stav);
            t.Cell(r, 5).Value = x.Profilacie ?? "";
            t.Cell(r, 6).Value = x.Platnost ?? "";
            t.Cell(r, 7).Value = x.Uroven ?? JazykovePravidla.UrovenZNazvu(x.Nazov) ?? "";
            t.Cell(r, 8).Value = Math.Round(x.Dlzka / 1800.0, 1);
            t.Cell(r, 9).Value = x.ZmenenaVRoku ? "áno" : "";
            t.Cell(r, 10).Value = porovnanie is null || x.ObjektId is not { } id ? ""
                : porovnanie.Nove.Contains(id) ? "nový" : porovnanie.Zmenene.Contains(id) ? "zmenená" : "bez zmeny";
            r++;
        }

        Format(t, hlavicka.Length);
        zosit.SaveAs(cesta);

        static void Format(IXLWorksheet harok, int stlpcov)
        {
            var hlavickaRiadok = harok.Range(1, 1, 1, stlpcov);
            hlavickaRiadok.Style.Font.Bold = true;
            hlavickaRiadok.Style.Fill.BackgroundColor = XLColor.FromHtml("#E7ECFB");
            harok.SheetView.FreezeRows(1);
            harok.Columns(1, stlpcov).AdjustToContents();
        }
    }

    /// <summary>Odstráni archív školského roka z programu (priečinok so súbormi zostane).</summary>
    public static void Odstran(long archivId)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, "DELETE FROM archiv_polozka WHERE archiv_id = $a; DELETE FROM archiv_rok WHERE id = $a;", ("$a", archivId));
    }

    /// <summary>Archivuje celé osnovy za školský rok. Vráti záznam archívu.</summary>
    public static ZaznamArchivuRoka Archivuj(string skolskyRok)
    {
        var ciel = Path.Combine(Priecinok, skolskyRok.Replace('/', '-'));
        if (Directory.Exists(ciel) && Directory.EnumerateFileSystemEntries(ciel).Any())
        {
            ciel += $" ({DateTime.Now:yyyy-MM-dd HH-mm})"; // opakovaná archivácia toho istého roka
        }

        Directory.CreateDirectory(ciel);
        var dovod = $"Archív školského roka {skolskyRok}";
        var (od, po) = Obdobie(skolskyRok);
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var teraz = DateTime.Now;

        long archivId;
        using (var db = Databaza.Otvor())
        {
            Databaza.Vykonaj(db, "INSERT INTO archiv_rok(skolsky_rok, vytvorene, priecinok) VALUES($r, $v, $p)",
                ("$r", skolskyRok), ("$v", Databaza.Teraz()), ("$p", ciel));
            archivId = Convert.ToInt64(Databaza.Skalar(db, "SELECT last_insert_rowid()"), CultureInfo.InvariantCulture);
        }

        var cisloOblasti = 0;
        foreach (var oblast in sluzba.Oblasti())
        {
            cisloOblasti++;
            var farba = oblast.Farba ?? UcebnyPlanService.FarbaOblasti(cisloOblasti - 1);
            var priecinokOblasti = Path.Combine(ciel, ImportOsnov.BezpecnyNazov($"{cisloOblasti:00} {oblast.Nazov}"));
            if (dokumenty.NacitajData(oblast.Id) is { Length: > 0 } charakteristika)
            {
                Directory.CreateDirectory(priecinokOblasti);
                File.WriteAllBytes(Path.Combine(priecinokOblasti, ImportOsnov.BezpecnyNazov($"00 Charakteristika – {oblast.Nazov}") + ".docx"), charakteristika);
                dokumenty.ArchivujAktualnu(oblast.Id, dovod);
                Zapis(archivId, oblast.Id, "charakteristika", oblast.Nazov, farba, cisloOblasti, 0, "Charakteristika vzdelávacej oblasti",
                    StavOsnovy.Rozpracovana, oblast.DlzkaTextu, UdajeOsnovy.Prazdne, InfoOsnovy.Prazdne, ZmenenaVRoku(oblast.Id, od, po), charakteristika);
            }

            var cislo = 0;
            foreach (var predmet in sluzba.Deti(oblast.Id).Where(p => p.Typ == TypObjektu.Predmet))
            {
                cislo++;
                var osnova = dokumenty.NacitajData(predmet.Id);
                var info = InfoOsnovyService.Nacitaj(predmet.Id);
                var stav = StavyOsnovy.Urci(predmet.Stav, predmet.DlzkaTextu);
                if (osnova is { Length: > 0 })
                {
                    Directory.CreateDirectory(priecinokOblasti);
                    var c = cislo.ToString("00", CultureInfo.InvariantCulture);
                    File.WriteAllBytes(Path.Combine(priecinokOblasti, ImportOsnov.BezpecnyNazov(InfoOsnovyService.NazovDokumentu(c, predmet.Nazov, info.Rok)) + ".docx"), osnova);
                    dokumenty.ArchivujAktualnu(predmet.Id, dovod);
                }

                Zapis(archivId, predmet.Id, TypObjektu.Predmet, oblast.Nazov, farba, cisloOblasti, cislo, predmet.Nazov, stav, predmet.DlzkaTextu,
                    UvodnaTabulka.Precitaj(osnova), info, ZmenenaVRoku(predmet.Id, od, po), osnova is { Length: > 0 } ? osnova : null);
            }
        }

        // celé osnovy v jednom dokumente – Word a archívne PDF/A-2b (bez farebného profilu Windows bežné PDF)
        var celok = ZostavaOsnov.Zostav(new VolbyZostavy());
        var nazovCelku = ImportOsnov.BezpecnyNazov($"Učebné osnovy {skolskyRok.Replace('/', '-')}");
        DocxVystup.Uloz(celok, Path.Combine(ciel, nazovCelku + ".docx"));
        try
        {
            SuboryZostavy.Uloz(PdfVystup.Vytvor(celok), Path.Combine(ciel, nazovCelku + ".pdf"), new VolbyPdf { Format = FormatVystupu.PdfA2b });
        }
        catch (SkvpException)
        {
            SuboryZostavy.Uloz(PdfVystup.Vytvor(celok), Path.Combine(ciel, nazovCelku + ".pdf"), new VolbyPdf());
        }

        return Zoznam().First(z => z.Id == archivId);
    }

    /// <summary>Celé osnovy roka v PDF (z priečinka archívu), alebo null.</summary>
    public static string? CelePdf(ZaznamArchivuRoka archiv) =>
        archiv.Priecinok is { } p && Directory.Exists(p) ? Directory.EnumerateFiles(p, "Učebné osnovy*.pdf").FirstOrDefault() : null;

    private static void Zapis(long archivId, long objektId, string typ, string oblast, string farba, int poradieOblasti, int cislo, string nazov,
        StavOsnovy stav, int dlzka, UdajeOsnovy udaje, InfoOsnovy info, bool zmenena, byte[]? data)
    {
        using var db = Databaza.Otvor();
        Databaza.Vykonaj(db, """
            INSERT INTO archiv_polozka(archiv_id, objekt_id, typ, oblast, farba, poradie_oblasti, cislo, nazov, stav, dlzka, profilacie, platnost,
                                       rok_osnovy, uroven, zmenena_v_roku, sha256, data)
            VALUES($a, $o, $t, $ob, $f, $po, $c, $n, $s, $d, $pr, $pl, $r, $u, $z, $sha, $data)
            """, ("$a", archivId), ("$o", objektId), ("$t", typ), ("$ob", oblast), ("$f", farba), ("$po", poradieOblasti), ("$c", cislo),
            ("$n", nazov), ("$s", stav.ToString()), ("$d", dlzka), ("$pr", udaje.Profilacie), ("$pl", udaje.Platnost), ("$r", info.Rok),
            ("$u", info.Uroven), ("$z", zmenena ? 1 : 0), ("$sha", data is null ? null : Convert.ToHexString(SHA256.HashData(data))), ("$data", data));
    }

    /// <summary>Zmenila sa osnova počas školského roka (úprava, import, obnova verzie)?</summary>
    private static bool ZmenenaVRoku(long objektId, DateTime od, DateTime po)
    {
        using var db = Databaza.Otvor();
        var upravene = Databaza.Skalar(db, "SELECT upravene FROM dokument WHERE objekt_id = $id", ("$id", objektId)) as string;
        if (upravene is not null && Datum(upravene) is var d && d >= od && d <= po)
        {
            return true;
        }

        return Convert.ToInt64(Databaza.Skalar(db, """
            SELECT count(*) FROM verzia WHERE objekt_id = $id AND vytvorene >= $od AND vytvorene <= $po AND (dovod IS NULL OR dovod NOT LIKE 'Archív školského roka%')
            """, ("$id", objektId), ("$od", od.ToString("s", CultureInfo.InvariantCulture)), ("$po", po.ToString("s", CultureInfo.InvariantCulture))) ?? 0L,
            CultureInfo.InvariantCulture) > 0;
    }

    private static DateTime Datum(string text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : DateTime.MinValue;
}
