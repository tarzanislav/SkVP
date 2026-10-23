using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using SkVP.Data;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;
using SkVP.ViewModels;
using SkVP.Views;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace SkVP;

/// <summary>
/// Automatická kontrola programu (parameter --samotest, spúšťa ju zostavenie na GitHube). Pracuje v dočasnom priečinku,
/// skutočných dát sa nedotkne. Vyskúša databázu, import tabuľky, dokumenty Word, zostavy a otvorí všetky obrazovky
/// (snímky obrazovky sa uložia do priečinka Export).
/// </summary>
internal static class Samotest
{
    public static List<string> Chyby { get; } = new();

    private static readonly StringBuilder Zaznam = new();
    private static string? _priebeznyZaznam;

    public static int Spusti(App aplikacia)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SKVP_DATA_DIR")))
        {
            Environment.SetEnvironmentVariable("SKVP_DATA_DIR", Path.Combine(Path.GetTempPath(), "SkVP_samotest_" + Guid.NewGuid().ToString("N")));
        }

        Directory.CreateDirectory(Databaza.Priecinok);
        _priebeznyZaznam = Path.Combine(Databaza.Priecinok, "samotest_priebeh.log");
        File.Delete(_priebeznyZaznam);
        var vysledok = 1;
        try
        {
            Krok($"priečinok: {Databaza.Priecinok}");
            if (File.Exists(Databaza.Cesta))
            {
                File.Delete(Databaza.Cesta);
            }

            Databaza.Inicializuj();
            Databaza.Inicializuj(); // druhé spustenie nesmie nič pokaziť
            Krok("databáza vytvorená");

            TestUcebnehoPlanu();
            TestObjektov();
            TestDokumentov();
            TestVlozeniaZWordu();
            TestZostav();
            TestZaloh();
            TestObrazoviek(aplikacia);

            if (Chyby.Count > 0)
            {
                throw new InvalidOperationException("Neošetrené chyby v rozhraní:\n" + string.Join("\n---\n", Chyby));
            }

            Krok("SAMOTEST OK");
            vysledok = 0;
        }
        catch (Exception ex)
        {
            Krok("CHYBA: " + ex);
        }

        foreach (var hlasenie in Dialogy.TicheHlasenia)
        {
            Zaznam.AppendLine("  hlásenie: " + hlasenie.Replace('\n', ' '));
        }

        File.WriteAllText(Path.Combine(Databaza.Priecinok, "samotest.log"), Zaznam.ToString(), Encoding.UTF8);
        return vysledok;
    }

    // ---------------------------------------------------------------------------------------

    private static void TestUcebnehoPlanu()
    {
        var plan = UcebnyPlanService.Vstavany();
        Over(plan.Oblasti.Count == 7, $"vstavaný plán: 7 oblastí (je {plan.Oblasti.Count})");
        Over(plan.PocetPredmetov == 43, $"vstavaný plán: 43 predmetov (je {plan.PocetPredmetov})");
        Over(plan.Typ == "Gymnázium, ISCED 3A", "vstavaný plán: typ školy");

        // tabuľka Excel v rovnakom tvare ako tabuľka školy (nadpisy a oblasti cez zlúčené bunky A:B)
        var cesta = Path.Combine(Databaza.Priecinok, "ucebny_plan_test.xlsx");
        using (var zosit = new XLWorkbook())
        {
            var harok = zosit.AddWorksheet("Vzdelávacie oblasti a predmety");
            var r = 2;
            harok.Cell(r, 1).Value = "Vzdelávacie oblasti a predmety";
            harok.Range(r, 1, r, 2).Merge();
            r++;
            harok.Cell(r, 1).Value = "Gymnázium, ISCED 3A";
            harok.Range(r, 1, r, 2).Merge();
            r += 2;
            foreach (var oblast in plan.Oblasti)
            {
                harok.Cell(r, 1).Value = oblast.Nazov;
                harok.Range(r, 1, r, 2).Merge();
                r++;
                foreach (var predmet in oblast.Predmety)
                {
                    harok.Cell(r++, 2).Value = predmet;
                }
            }

            zosit.SaveAs(cesta);
        }

        var nacitany = UcebnyPlanService.NacitajZExcelu(cesta);
        Over(nacitany.Oblasti.Count == 7 && nacitany.PocetPredmetov == 43, "import z Excelu: 7 oblastí, 43 predmetov");
        Over(nacitany.Typ == "Gymnázium, ISCED 3A" && nacitany.Nazov == "Vzdelávacie oblasti a predmety", "import z Excelu: nadpisy tabuľky");
        Over(nacitany.Oblasti[4].Nazov == "Umenie a kultúra" && nacitany.Oblasti[4].Predmety.Single() == "Umenie a kultúra", "oblasť s jedným predmetom rovnakého názvu");

        var prvy = UcebnyPlanService.Vytvor(plan, nahradit: false);
        Over(prvy.NoveOblasti == 7 && prvy.NovePredmety == 43, "vytvorenie oblastí a predmetov");
        var druhy = UcebnyPlanService.Vytvor(nacitany, nahradit: false);
        Over(druhy.NoveOblasti == 0 && druhy.NovePredmety == 0 && druhy.Preskocene == 43, "opakovaný import nič nezdvojí");
        Over(Databaza.Citaj(UcebnyPlanService.KlucTypSkoly) == "Gymnázium, ISCED 3A", "typ školy uložený v nastaveniach");

        var oblasti = new ObjektService().Oblasti();
        Over(oblasti.Select(o => o.Farba).Distinct().Count() == 7, "každá oblasť má vlastnú farbu");
        Over(oblasti[0].Nazov == "Jazyk a komunikácia" && oblasti[6].Nazov == "Zdravie a pohyb", "poradie oblastí podľa tabuľky");
    }

    private static void TestObjektov()
    {
        var sluzba = new ObjektService();
        var jazyk = sluzba.Oblasti()[0];
        var predmety = sluzba.Deti(jazyk.Id);
        Over(predmety.Count == 9 && predmety[0].Nazov == "Slovenský jazyk a literatúra", "predmety prvej oblasti v poradí tabuľky");

        var novy = sluzba.Pridaj(TypObjektu.Predmet, jazyk.Id, "  Francúzsky jazyk  ");
        Over(novy.Nazov == "Francúzsky jazyk" && sluzba.Deti(jazyk.Id).Last().Id == novy.Id, "pridaný predmet je na konci oblasti");
        Over(sluzba.Posun(novy.Id, -1), "posun vyššie");
        Over(sluzba.Deti(jazyk.Id)[^2].Id == novy.Id, "predmet je o miesto vyššie");
        Over(!sluzba.Posun(predmety[0].Id, -1), "prvý predmet sa vyššie posunúť nedá");
        Over(sluzba.Posun(novy.Id, +1) && !sluzba.Posun(novy.Id, +1), "posun nižšie a koniec zoznamu");
        var poradie = sluzba.Deti(jazyk.Id).Select(p => p.Poradie).ToList();
        Over(poradie.SequenceEqual(Enumerable.Range(0, poradie.Count)), "poradie je súvislé 0, 1, 2 …");

        var matematika = sluzba.Oblasti()[1];
        sluzba.Presun(novy.Id, matematika.Id, 1);
        Over(sluzba.Deti(matematika.Id)[1].Id == novy.Id && sluzba.Deti(jazyk.Id).All(p => p.Id != novy.Id), "presun predmetu do inej oblasti");
        sluzba.Premenuj(novy.Id, "Francúzsky jazyk 2");
        Over(sluzba.Nacitaj(novy.Id)!.Nazov == "Francúzsky jazyk 2", "premenovanie");
        var prazdny = false;
        try
        {
            sluzba.Premenuj(novy.Id, "   ");
        }
        catch (SkvpException)
        {
            prazdny = true;
        }

        Over(prazdny, "prázdny názov sa nedá uložiť");

        sluzba.Odstran(novy.Id);
        Over(sluzba.Deti(matematika.Id).All(p => p.Id != novy.Id), "odstránenie (kôš)");
        sluzba.Obnov(novy.Id);
        Over(sluzba.Deti(matematika.Id)[1].Id == novy.Id, "vrátenie odstráneného späť na pôvodné miesto");
        sluzba.Odstran(novy.Id);

        // väzby – čokoľvek s čímkoľvek
        var fyzika = sluzba.Deti(sluzba.Oblasti()[2].Id)[0];
        var matPredmet = sluzba.Deti(matematika.Id)[0];
        var vazba = sluzba.Prepoj(fyzika.Id, matPredmet.Id, TypVazby.Predpoklad);
        sluzba.Prepoj(fyzika.Id, matPredmet.Id, TypVazby.Predpoklad); // druhý raz sa nezdvojí
        sluzba.Prepoj(fyzika.Id, sluzba.Oblasti()[3].Id, TypVazby.Medzipredmetovy);
        var zFyziky = sluzba.Suvislosti(fyzika.Id);
        var zMatematiky = sluzba.Suvislosti(matPredmet.Id);
        Over(zFyziky.Count == 2, "fyzika má 2 súvislosti");
        Over(zMatematiky.Count == 1 && zMatematiky[0].Spatna && zMatematiky[0].Objekt.Id == fyzika.Id, "súvislosť vidno aj z druhej strany");
        Over(zMatematiky[0].Popis == "predpokladá" && zFyziky.First(s => s.VazbaId == vazba).Popis == "je predpokladom pre", "názov väzby z oboch strán");
        sluzba.ZrusVazbu(vazba);
        Over(sluzba.Suvislosti(matPredmet.Id).Count == 0, "zrušenie väzby");
        var samSoSebou = false;
        try
        {
            sluzba.Prepoj(fyzika.Id, fyzika.Id, TypVazby.Suvisi);
        }
        catch (SkvpException)
        {
            samSoSebou = true;
        }

        Over(samSoSebou, "objekt sa nedá prepojiť sám so sebou");
    }

    /// <summary>Ukážková osnova: nadpisy, formát písma, vnorené odrážky, číslovanie, tabuľka so zlúčenými bunkami.</summary>
    private static List<Blok> UkazkovaOsnova() => new()
    {
        new Nadpis(1, "Charakteristika predmetu"),
        new Odsek(new[]
        {
            new Usek("Predmet rozvíja "), new Usek("komunikačné", Tucne: true), new Usek(" a "),
            new Usek("čitateľské", Kurziva: true), new Usek(" kompetencie žiakov – "), new Usek("ľščťžýáíéúäôň", Podciarknute: true),
            new Usek(".", NovyRiadok: false), new Usek("", NovyRiadok: true), new Usek("Druhý riadok odseku."),
        }),
        new Nadpis(2, "Ciele predmetu"),
        new Zoznam(false, new IReadOnlyList<Blok>[]
        {
            new Blok[] { new Odsek(new[] { new Usek("rozvíjať slovnú zásobu") }) },
            new Blok[]
            {
                new Odsek(new[] { new Usek("čítať s porozumením") }),
                new Zoznam(true, new IReadOnlyList<Blok>[]
                {
                    new Blok[] { new Odsek(new[] { new Usek("umelecké texty") }) },
                    new Blok[] { new Odsek(new[] { new Usek("odborné texty") }) },
                }),
            },
            new Blok[] { new Odsek(new[] { new Usek("písať jasne a výstižne") }) },
        }),
        new Nadpis(1, "Obsahový a výkonový štandard"),
        new Tabulka(new IReadOnlyList<Bunka>[]
        {
            new[]
            {
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Tematický celok", Tucne: true) }) }, Hlavicka: true),
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Obsahový štandard", Tucne: true) }) }, Hlavicka: true),
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Výkonový štandard", Tucne: true) }) }, Hlavicka: true),
            },
            new[]
            {
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Jazyk a reč") }) }, Riadky: 2),
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("jazykový systém") }) }),
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Žiak vie vysvetliť pojem jazyk.") }) }),
            },
            new[]
            {
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("spisovný jazyk a nárečia") }) }, Stlpce: 2),
            },
        }, 3, new[] { 2.0, 3.0, 4.0 }),
        new Nadpis(3, "Poznámka"),
        new Odsek(new[] { new Usek("Text zarovnaný do bloku.") }, 1, TextAlignment.Justify),
    };

    private static void TestDokumentov()
    {
        var osnova = UkazkovaOsnova();
        var data = DocxVystup.Osnova(osnova);
        Over(data.Length > 2000, $"osnova uložená ako dokument Word ({data.Length} B)");
        OverDocx(data, "osnova predmetu");

        var nacitany = DocxCitac.Nacitaj(new MemoryStream(data)).Bloky;
        PorovnajBloky(osnova, nacitany, "Word → program");

        // editor: model → FlowDocument → model
        var dokument = DokumentOsnovy.KDokumentu(osnova);
        PorovnajBloky(osnova, DokumentOsnovy.Rozober(dokument), "editor programu");

        // dokument Word so slovenskými štýlmi (ako zo slovenského Wordu)
        var slovensky = DocxSoSlovenskymiStylmi();
        var zoSlovenskeho = DocxCitac.Nacitaj(new MemoryStream(slovensky)).Bloky;
        Over(zoSlovenskeho.OfType<Nadpis>().Select(n => (n.Uroven, n.Text)).SequenceEqual(new[] { (1, "Ciele"), (2, "Kompetencie") }),
            "nadpisy so slovenskými štýlmi Wordu (Nadpis 1, Nadpis 2)");
        Over(zoSlovenskeho.OfType<Zoznam>().Single() is { Cislovany: true, Polozky.Count: 2 }, "číslovaný zoznam zo slovenského Wordu");

        // uloženie do databázy a archív verzií
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var predmet = sluzba.Deti(sluzba.Oblasti()[0].Id)[0];
        Over(dokumenty.Uloz(predmet.Id, data, "Pred úpravou"), "uloženie osnovy do databázy");
        Over(!dokumenty.Uloz(predmet.Id, data, "Pred úpravou"), "rovnaký obsah sa druhý raz neuloží");
        Over(dokumenty.Verzie(predmet.Id).Count == 0, "prvá verzia nemá čo archivovať");
        Over(sluzba.Nacitaj(predmet.Id)!.DlzkaTextu > 100, "dĺžka textu osnovy sa zapísala");
        var upravena = osnova.Append(new Odsek(new[] { new Usek("Doplnený odsek.") })).ToList();
        Over(dokumenty.UlozBloky(predmet.Id, upravena, "Pred úpravou v programe"), "uloženie upravenej osnovy");
        var verzie = dokumenty.Verzie(predmet.Id);
        Over(verzie.Count == 1 && verzie[0].Dovod == "Pred úpravou v programe", "predchádzajúca verzia je v archíve");
        Over(dokumenty.Nacitaj(predmet.Id).Bloky.Count == osnova.Count + 1, "načítanie aktuálnej verzie");
        dokumenty.ObnovVerziu(predmet.Id, verzie[0].Id);
        Over(dokumenty.Nacitaj(predmet.Id).Bloky.Count == osnova.Count && dokumenty.Verzie(predmet.Id).Count == 2, "obnovenie staršej verzie (aktuálna sa zarchivovala)");

        var poskodeny = false;
        try
        {
            dokumenty.Uloz(predmet.Id, Encoding.UTF8.GetBytes("toto nie je Word"), null);
        }
        catch (SkvpException)
        {
            poskodeny = true;
        }

        Over(poskodeny, "poškodený súbor sa do databázy neuloží");

        // druhý predmet a charakteristika oblasti
        dokumenty.UlozBloky(sluzba.Deti(sluzba.Oblasti()[0].Id)[1].Id, osnova.Take(4).ToList(), null);
        dokumenty.UlozBloky(sluzba.Oblasti()[0].Id, new List<Blok>
        {
            new Odsek(new[] { new Usek("Vzdelávacia oblasť Jazyk a komunikácia rozvíja jazykové kompetencie žiakov.") }),
        }, null);
        new ObjektService().NastavStav(sluzba.Deti(sluzba.Oblasti()[0].Id)[1].Id, StavyOsnovy.Hotova);
    }

    private static void TestVlozeniaZWordu()
    {
        // text skopírovaný z Wordu príde ako RTF – nadpisy sa rozpoznajú, cudzie písma a farby sa zrušia
        var zdroj = new FlowDocument();
        var nadpis = new Paragraph(new Run("Hodnotenie predmetu")) { FontSize = 16 * 96 / 72.0, FontWeight = FontWeights.Bold, FontFamily = new FontFamily("Arial") };
        var text = new Paragraph(new Run("Žiak je hodnotený známkou.") { Foreground = Brushes.Red, FontFamily = new FontFamily("Comic Sans MS") });
        zdroj.Blocks.Add(nadpis);
        zdroj.Blocks.Add(text);
        string rtf;
        using (var pamat = new MemoryStream())
        {
            new TextRange(zdroj.ContentStart, zdroj.ContentEnd).Save(pamat, DataFormats.Rtf);
            rtf = Encoding.Latin1.GetString(pamat.ToArray());
        }

        var vlozeny = new FlowDocument();
        using (var pamat = new MemoryStream(Encoding.Latin1.GetBytes(rtf)))
        {
            new TextRange(vlozeny.ContentStart, vlozeny.ContentEnd).Load(pamat, DataFormats.Rtf);
        }

        DokumentOsnovy.OcistiVlozene(vlozeny);
        var bloky = DokumentOsnovy.Rozober(vlozeny);
        Over(bloky.FirstOrDefault() is Nadpis { Uroven: 1, Text: "Hodnotenie predmetu" }, "vložený nadpis z Wordu sa rozpoznal");
        var odsek = vlozeny.Blocks.OfType<Paragraph>().Last();
        Over(odsek.Inlines.FirstInline is Run beh && beh.ReadLocalValue(TextElement.ForegroundProperty) == DependencyProperty.UnsetValue,
            "farba vloženého textu sa zrušila");
    }

    private static void TestZostav()
    {
        var plne = ZostavaOsnov.Zostav(new VolbyZostavy { OddelovacPredmetu = true });
        Over(plne.Oblasti.Count == 7 && plne.PocetPredmetov == 43, "celý dokument: 7 oblastí, 43 predmetov");
        Over(plne.Oblasti[0].Predmety[0].Obsah.Count > 5 && plne.Oblasti[0].Charakteristika.Count == 1, "celý dokument obsahuje osnovy a charakteristiku");
        Over(plne.Oblasti[0].Predmety[2].Cislo == "03", "číslo poradia predmetu 01, 02, 03 …");

        var export = Databaza.PriecinokDokumentov;
        Directory.CreateDirectory(export);
        var pdf = Path.Combine(export, "samotest_cele_osnovy.pdf");
        PdfVystup.UlozPdf(plne, pdf);
        var vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(plne));
        var strany = NahladDokumentu.PocetStran(vykreslenie);
        Over(new FileInfo(pdf).Length > 20_000 && strany >= 2 + 7 + 43 * 2, $"PDF celých osnov ({strany} strán)");
        Over(NahladDokumentu.Vytlac(vykreslenie, "samotest"), "tlač (tichý režim)");

        var jednaOblast = ZostavaOsnov.Zostav(new VolbyZostavy
        {
            Oblasti = new[] { plne.Oblasti[0].Id },
            TitulnaStrana = false,
            Obsah = false,
            OddelovacOblasti = false,
            NespracovanePredmety = false,
        });
        Over(jednaOblast.Oblasti.Count == 1 && jednaOblast.PocetPredmetov == 2, "zostava jednej oblasti len so spracovanými osnovami");
        PdfVystup.UlozPdf(jednaOblast, Path.Combine(export, "samotest_jedna_oblast.pdf"));
        var vybrane = ZostavaOsnov.Zostav(new VolbyZostavy { Oblasti = new[] { plne.Oblasti[1].Id, plne.Oblasti[6].Id } });
        Over(vybrane.Oblasti.Select(o => o.Nazov).SequenceEqual(new[] { "Matematika a práca s informáciami", "Zdravie a pohyb" }), "zostava vybraných oblastí");

        var docx = Path.Combine(export, "samotest_cele_osnovy.docx");
        DocxVystup.Uloz(plne, docx);
        OverDocx(File.ReadAllBytes(docx), "celé osnovy vo Worde");
        var spat = DocxCitac.Nacitaj(docx).Bloky;
        Over(spat.OfType<Nadpis>().Count(n => n.Uroven == 2) >= 43, "export do Wordu: predmety ako Nadpis 2");

        // náhľady strán ako obrázky (kontrola vzhľadu)
        for (var i = 1; i <= Math.Min(strany, 6); i++)
        {
            UlozObrazokStrany(vykreslenie, i, Path.Combine(export, $"strana_{i:00}.png"));
        }

        var predmetovaStrana = Enumerable.Range(1, strany).FirstOrDefault(s => s > 10) ;
        if (predmetovaStrana > 0)
        {
            UlozObrazokStrany(vykreslenie, Math.Min(strany, 12), Path.Combine(export, "strana_osnova.png"));
        }

        Krok("zostavy v poriadku");
    }

    private static void TestZaloh()
    {
        var zalohy = new ZalohaService();
        var prva = zalohy.Zalohuj();
        var druha = zalohy.Zalohuj();
        Over(Path.GetFileName(prva).StartsWith("SkVP_zaloha_0001_", StringComparison.Ordinal) &&
             Path.GetFileName(druha).StartsWith("SkVP_zaloha_0002_", StringComparison.Ordinal), "zálohy sú číslované za sebou");
        using var archiv = System.IO.Compression.ZipFile.OpenRead(druha);
        Over(archiv.GetEntry("skvp.db") is { Length: > 10_000 }, "záloha obsahuje databázu");
        Over(zalohy.AutomatickaAkTreba() is not null && zalohy.AutomatickaAkTreba() is null, "automatická záloha raz za deň");
    }

    private static void TestObrazoviek(App aplikacia)
    {
        var okno = new MainWindow { Width = 1440, Height = 900, WindowStartupLocation = WindowStartupLocation.Manual, Left = 0, Top = 0 };
        aplikacia.MainWindow = okno;
        okno.Show();
        Pumpuj();
        var model = okno.Model;
        Over(model.Oblasti.Count == 7 && model.PocetPredmetov == 43, "hlavné okno: 7 oblastí, 43 predmetov");
        Snimka(okno, "01_dashboard");

        model.OtvorOsnovy();
        Pumpuj();
        Snimka(okno, "02_osnovy");

        var jazyk = model.Oblasti[0];
        model.OtvorOblast(jazyk);
        Pumpuj();
        Snimka(okno, "03_oblast");

        // pridanie a presun predmetu cez rozhranie
        var oblastModel = (OblastViewModel)model.Stranka!;
        oblastModel.NovyPredmet = "Latinský jazyk";
        oblastModel.PridajPredmetCommand.Execute(null);
        Over(jazyk.Predmety.Last().Nazov == "Latinský jazyk" && jazyk.Predmety.Last().Cislo == "10", "pridanie predmetu v rozhraní (číslo 10)");
        model.PosunPredmetHoreCommand.Execute(jazyk.Predmety.Last());
        Over(jazyk.Predmety[^2].Nazov == "Latinský jazyk" && jazyk.Predmety[^2].Cislo == "09", "posun predmetu vyššie v rozhraní");
        model.PosunPredmetDoleCommand.Execute(jazyk.Predmety[^2]);
        Over(jazyk.Predmety[^1].Nazov == "Latinský jazyk", "posun predmetu nižšie v rozhraní");
        var latinsky = jazyk.Predmety[^1];
        model.PresunPredmet(latinsky, jazyk, 0);
        Over(jazyk.Predmety[0] == latinsky && latinsky.Cislo == "01" && new ObjektService().Deti(jazyk.Id)[0].Id == latinsky.Id, "presun predmetu ťahaním na začiatok");
        model.PresunPredmet(latinsky, model.Oblasti[3], 1);
        Over(model.Oblasti[3].Predmety[1] == latinsky && latinsky.Oblast == model.Oblasti[3], "presun predmetu do inej oblasti");
        model.OdstranPredmetCommand.Execute(latinsky);
        Over(!model.Oblasti[3].Predmety.Contains(latinsky) && model.MaVratitSpat, "odstránenie s možnosťou vrátiť späť");
        model.VratitSpatCommand.Execute(null);
        Over(model.Oblasti[3].Predmety.Contains(latinsky), "vrátenie späť");
        model.OdstranPredmetCommand.Execute(latinsky);
        Pumpuj();
        Snimka(okno, "04_oblast_po_uprave");

        // charakteristika oblasti
        if (Najdi<OblastView>(okno) is { } oblastView)
        {
            oblastView.UkazCharakteristiku();
            Pumpuj();
            Snimka(okno, "05_oblast_charakteristika");
        }

        // osnova predmetu – náhľad
        var sjl = jazyk.Predmety[0];
        model.OtvorPredmet(sjl);
        Pumpuj();
        Snimka(okno, "06_predmet_nahlad");
        var predmetModel = (PredmetViewModel)model.Stranka!;
        Over(predmetModel.Predmet == sjl && sjl.Stav == StavOsnovy.Rozpracovana, "osnova predmetu je rozpracovaná");

        // úprava v programe – text sa automaticky uloží
        predmetModel.Dokument.PrepniUpravuCommand.Execute(null);
        Pumpuj();
        Over(predmetModel.Dokument.JeUprava, "zapnutie úprav");
        if (Najdi<EditorDokumentu>(okno) is { } editor)
        {
            editor.Dokument.Blocks.Add(new Paragraph(new Run("Doplnené v samoteste.")));
            Pumpuj();
            Snimka(okno, "07_predmet_uprava");
            predmetModel.Dokument.PrepniUpravuCommand.Execute(null);
            Pumpuj();
            var text = new DokumentService().Nacitaj(sjl.Id).Bloky;
            Over(DokumentOsnovy.Text(text).Contains("Doplnené v samoteste.", StringComparison.Ordinal), "úprava v programe sa uložila do dokumentu Word");
            Over(new DokumentService().Verzie(sjl.Id)[0].Dovod == "Pred úpravou v programe", "pred úpravou sa zarchivovala verzia");
        }

        // prázdna osnova – úprava vloží šablónu častí osnovy
        var prazdny = jazyk.Predmety[5];
        model.OtvorPredmet(prazdny);
        Pumpuj();
        Snimka(okno, "08_predmet_prazdny");
        var prazdnyModel = (PredmetViewModel)model.Stranka!;
        prazdnyModel.Dokument.PrepniUpravuCommand.Execute(null);
        Pumpuj();
        if (Najdi<EditorDokumentu>(okno) is { } editor2)
        {
            Over(DokumentOsnovy.Rozober(editor2.Dokument).OfType<Nadpis>().Count() == DokumentOsnovy.CastiOsnovy.Length, "šablóna osnovy s nadpismi častí");
        }

        prazdnyModel.Dokument.PrepniUpravuCommand.Execute(null);
        Over(!new DokumentService().Existuje(prazdny.Id), "nezmenená šablóna sa neuloží");

        // úprava vo Worde – simulácia: Word uloží zmenený súbor do pracovnej kópie
        model.OtvorPredmet(sjl);
        Pumpuj();
        var wordModel = (PredmetViewModel)model.Stranka!;
        model.UpravVoWorde(sjl.Id, $"{sjl.Cislo} {sjl.Nazov}", true);
        Over(model.JeVoWorde(sjl.Id), "úprava vo Worde sa začala");
        var pracovne = Directory.GetFiles(Path.Combine(UpravaVoWorde.Priecinok, sjl.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)), "*.docx").Single();
        Over(Path.GetFileName(pracovne) == "01 Slovenský jazyk a literatúra.docx", "pracovná kópia má čitateľný názov");
        var zWordu = UkazkovaOsnova().Append(new Odsek(new[] { new Usek("Zmena urobená vo Worde.") })).ToList();
        File.WriteAllBytes(pracovne, DocxVystup.Osnova(zWordu));
        CakajNa(() => DokumentOsnovy.Text(new DokumentService().Nacitaj(sjl.Id).Bloky).Contains("Zmena urobená vo Worde.", StringComparison.Ordinal), 8);
        Over(DokumentOsnovy.Text(new DokumentService().Nacitaj(sjl.Id).Bloky).Contains("Zmena urobená vo Worde.", StringComparison.Ordinal),
            "zmena uložená vo Worde sa prevzala do databázy");
        Over(new DokumentService().Verzie(sjl.Id)[0].Dovod == "Pred úpravou vo Worde", "pred prevzatím z Wordu sa zarchivovala verzia");
        model.OtvorPredmet(sjl);
        Pumpuj();
        Snimka(okno, "09_predmet_vo_worde");
        model.UkonciUpravyVoWorde();

        // súvislosti
        var suvislostiModel = (PredmetViewModel)model.Stranka!;
        suvislostiModel.Prepoj(model.Oblasti[1].Predmety[0].Id, TypVazby.Medzipredmetovy);
        suvislostiModel.Prepoj(model.Oblasti[3].Id, TypVazby.Suvisi);
        Over(suvislostiModel.Suvislosti.Count == 2, "súvislosti predmetu v rozhraní");
        Pumpuj();
        Snimka(okno, "10_predmet_suvislosti");
        var vyber = new VyberObjektuWindow(model, sjl.Id) { Owner = okno };
        vyber.Show();
        Pumpuj();
        Snimka(vyber, "11_okno_prepojit");
        vyber.Close();

        // tlač a zostavy
        model.OtvorTlac(null);
        Pumpuj();
        var tlac = (TlacViewModel)model.Stranka!;
        if (tlac.PocetStran == 0)
        {
            tlac.Obnovit();
        }

        Pumpuj();
        Over(tlac.PocetStran > 50, $"náhľad celých osnov ({tlac.PocetStran} strán)");
        Snimka(okno, "12_tlac");
        model.OtvorTlac(jazyk);
        Pumpuj();
        var tlacOblasti = (TlacViewModel)model.Stranka!;
        tlacOblasti.Obnovit();
        Pumpuj();
        Over(tlacOblasti.Len && tlacOblasti.Oblasti.Count(o => o.Zahrnut) == 1, "tlač jednej oblasti z jej stránky");
        Snimka(okno, "13_tlac_oblasti");
        tlacOblasti.UlozitPdfCommand.Execute(null);
        tlacOblasti.UlozitWordCommand.Execute(null);

        model.OtvorNastavenia();
        Pumpuj();
        Snimka(okno, "14_nastavenia");

        // tmavý vzhľad
        App.NastavTemu(NastaveniaSkoly.TemaTmava);
        model.OtvorDashboard();
        Pumpuj();
        Snimka(okno, "15_tmavy_dashboard");
        model.OtvorPredmet(sjl);
        Pumpuj();
        Snimka(okno, "16_tmavy_predmet");
        model.OtvorOblast(model.Oblasti[2]);
        Pumpuj();
        Snimka(okno, "17_tmavy_oblast");
        App.NastavTemu(NastaveniaSkoly.TemaSvetla);
        Pumpuj();

        var uvod = new PrveSpustenieWindow();
        uvod.Show();
        Pumpuj();
        Snimka(uvod, "18_prve_spustenie");
        uvod.Close();

        okno.Close();
        Pumpuj();
        Krok("obrazovky v poriadku");
    }

    // ---------------------------------------------------------------------------------------

    private static byte[] DocxSoSlovenskymiStylmi()
    {
        using var pamat = new MemoryStream();
        using (var docx = WordprocessingDocument.Create(pamat, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var hlavna = docx.AddMainDocumentPart();
            var styly = hlavna.AddNewPart<StyleDefinitionsPart>();
            styly.Styles = new W.Styles(
                new W.Style(new W.StyleName { Val = "heading 1" }) { Type = W.StyleValues.Paragraph, StyleId = "Nadpis1" },
                new W.Style(new W.StyleName { Val = "heading 2" }) { Type = W.StyleValues.Paragraph, StyleId = "Nadpis2" });
            var cislovanie = hlavna.AddNewPart<NumberingDefinitionsPart>();
            cislovanie.Numbering = new W.Numbering(
                new W.AbstractNum(new W.Level(new W.NumberingFormat { Val = W.NumberFormatValues.Decimal }, new W.LevelText { Val = "%1." }) { LevelIndex = 0 })
                { AbstractNumberId = 5 },
                new W.NumberingInstance(new W.AbstractNumId { Val = 5 }) { NumberID = 7 });
            W.Paragraph Odsek(string text, string? styl = null, int? zoznam = null)
            {
                var vlastnosti = new W.ParagraphProperties();
                if (styl is not null)
                {
                    vlastnosti.Append(new W.ParagraphStyleId { Val = styl });
                }

                if (zoznam is not null)
                {
                    vlastnosti.Append(new W.NumberingProperties(new W.NumberingLevelReference { Val = 0 }, new W.NumberingId { Val = zoznam }));
                }

                return new W.Paragraph(vlastnosti, new W.Run(new W.Text(text)));
            }

            hlavna.Document = new W.Document(new W.Body(
                Odsek("Ciele", "Nadpis1"),
                Odsek("Úvodný text."),
                Odsek("Kompetencie", "Nadpis2"),
                Odsek("prvá", zoznam: 7),
                Odsek("druhá", zoznam: 7)));
            hlavna.Document.Save();
        }

        return pamat.ToArray();
    }

    private static void OverDocx(byte[] data, string popis)
    {
        using var docx = WordprocessingDocument.Open(new MemoryStream(data), false);
        var chyby = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2016).Validate(docx).Take(10).ToList();
        foreach (var chyba in chyby)
        {
            Krok($"  OpenXML {popis}: {chyba.Description} ({chyba.Path?.XPath})");
        }

        Over(chyby.Count == 0, $"{popis}: dokument Word je platný (kontrola OpenXML)");
    }

    private static void PorovnajBloky(IReadOnlyList<Blok> povodne, IReadOnlyList<Blok> nove, string popis)
    {
        Over(povodne.Count == nove.Count, $"{popis}: počet blokov {povodne.Count} = {nove.Count}");
        for (var i = 0; i < povodne.Count; i++)
        {
            var a = povodne[i];
            var b = nove[i];
            Over(a.GetType() == b.GetType(), $"{popis}: blok {i} je {a.GetType().Name} (je {b.GetType().Name})");
            switch (a, b)
            {
                case (Nadpis na, Nadpis nb):
                    Over(na.Uroven == nb.Uroven && na.Text == nb.Text, $"{popis}: nadpis „{na.Text}“");
                    break;
                case (Odsek oa, Odsek ob):
                    Over(TextOdseku(oa) == TextOdseku(ob), $"{popis}: text odseku „{TextOdseku(ob)}“");
                    Over(oa.Useky.Where(u => u.Tucne).Select(u => u.Text).SequenceEqual(ob.Useky.Where(u => u.Tucne).Select(u => u.Text)), $"{popis}: tučné písmo");
                    Over(oa.Useky.Where(u => u.Kurziva).Select(u => u.Text).SequenceEqual(ob.Useky.Where(u => u.Kurziva).Select(u => u.Text)), $"{popis}: kurzíva");
                    Over(oa.Useky.Where(u => u.Podciarknute).Select(u => u.Text).SequenceEqual(ob.Useky.Where(u => u.Podciarknute).Select(u => u.Text)), $"{popis}: podčiarknutie");
                    Over(oa.Zarovnanie == ob.Zarovnanie && oa.Odsadenie == ob.Odsadenie, $"{popis}: zarovnanie a odsadenie");
                    break;
                case (Zoznam za, Zoznam zb):
                    Over(za.Cislovany == zb.Cislovany && za.Polozky.Count == zb.Polozky.Count, $"{popis}: zoznam ({za.Polozky.Count} položky)");
                    for (var p = 0; p < za.Polozky.Count; p++)
                    {
                        PorovnajBloky(za.Polozky[p], zb.Polozky[p], popis + " / položka");
                    }

                    break;
                case (Tabulka ta, Tabulka tb):
                    Over(ta.PocetStlpcov == tb.PocetStlpcov && ta.Riadky.Count == tb.Riadky.Count, $"{popis}: tabuľka {ta.Riadky.Count}×{ta.PocetStlpcov}");
                    for (var r = 0; r < ta.Riadky.Count; r++)
                    {
                        Over(ta.Riadky[r].Count == tb.Riadky[r].Count, $"{popis}: tabuľka riadok {r} má {ta.Riadky[r].Count} buniek (má {tb.Riadky[r].Count})");
                        for (var c = 0; c < ta.Riadky[r].Count; c++)
                        {
                            var x = ta.Riadky[r][c];
                            var y = tb.Riadky[r][c];
                            Over(x.Stlpce == y.Stlpce && x.Riadky == y.Riadky, $"{popis}: zlúčenie bunky [{r},{c}] {x.Stlpce}×{x.Riadky} (je {y.Stlpce}×{y.Riadky})");
                            Over(DokumentOsnovy.Text(x.Bloky).Trim() == DokumentOsnovy.Text(y.Bloky).Trim(), $"{popis}: text bunky [{r},{c}]");
                        }
                    }

                    var sa = ta.PomerneSirky;
                    var sb = tb.PomerneSirky;
                    Over(sa.Zip(sb).All(z => Math.Abs(z.First - z.Second) < 0.02), $"{popis}: šírky stĺpcov tabuľky");
                    break;
            }
        }
    }

    private static string TextOdseku(Odsek o) => string.Concat(o.Useky.Select(u => u.NovyRiadok ? "⏎" : u.Text));

    private static void UlozObrazokStrany(MigraDoc.Rendering.DocumentRenderer vykreslenie, int strana, string cesta)
    {
        var kresba = NahladDokumentu.Strana(vykreslenie, strana, out var velkost);
        var obrazok = new System.Windows.Media.Imaging.RenderTargetBitmap((int)velkost.Width, (int)velkost.Height, 96, 96, PixelFormats.Pbgra32);
        obrazok.Render(kresba);
        var kodovac = new System.Windows.Media.Imaging.PngBitmapEncoder();
        kodovac.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(obrazok));
        using var subor = File.Create(cesta);
        kodovac.Save(subor);
    }

    private static T? Najdi<T>(DependencyObject koren) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(koren); i++)
        {
            var dieta = VisualTreeHelper.GetChild(koren, i);
            if (dieta is T hladany)
            {
                return hladany;
            }

            if (Najdi<T>(dieta) is { } vnoreny)
            {
                return vnoreny;
            }
        }

        return null;
    }

    private static void CakajNa(Func<bool> podmienka, int sekund)
    {
        var koniec = DateTime.Now.AddSeconds(sekund);
        while (DateTime.Now < koniec && !podmienka())
        {
            Pumpuj();
            Thread.Sleep(150);
        }
    }

    private static void Pumpuj()
    {
        for (var i = 0; i < 3; i++)
        {
            var ramec = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => ramec.Continue = false));
            Dispatcher.PushFrame(ramec);
        }
    }

    private static void Snimka(Window okno, string nazov)
    {
        try
        {
            if (okno.Content is not FrameworkElement obsah || obsah.ActualWidth < 1 || obsah.ActualHeight < 1)
            {
                return;
            }

            var sirka = (int)Math.Ceiling(obsah.ActualWidth);
            var vyska = (int)Math.Ceiling(obsah.ActualHeight);
            var kresba = new DrawingVisual();
            using (var kontext = kresba.RenderOpen())
            {
                var oblast = new Rect(0, 0, sirka, vyska);
                var pozadie = okno.Background is SolidColorBrush { Color.A: > 0 } p ? p : (Brush)(Application.Current.TryFindResource("SolidBackgroundFillColorBaseBrush") ?? Brushes.White);
                kontext.DrawRectangle(pozadie, null, oblast);
                kontext.DrawRectangle(new VisualBrush(obsah), null, oblast);
            }

            var obrazok = new System.Windows.Media.Imaging.RenderTargetBitmap(sirka, vyska, 96, 96, PixelFormats.Pbgra32);
            obrazok.Render(kresba);
            var kodovac = new System.Windows.Media.Imaging.PngBitmapEncoder();
            kodovac.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(obrazok));
            var priecinok = Databaza.PriecinokDokumentov;
            Directory.CreateDirectory(priecinok);
            using var subor = File.Create(Path.Combine(priecinok, nazov + ".png"));
            kodovac.Save(subor);
        }
        catch (Exception ex)
        {
            Krok($"snímka {nazov}: {ex.Message}");
        }
    }

    private static void Over(bool podmienka, string popis)
    {
        if (!podmienka)
        {
            throw new InvalidOperationException("Neplatí: " + popis);
        }

        Krok("ok – " + popis);
    }

    internal static void ZapisPriebeh(string text) => Krok(text);

    private static void Krok(string text)
    {
        var riadok = $"{DateTime.Now:HH:mm:ss} {text}";
        Zaznam.AppendLine(riadok);
        if (_priebeznyZaznam is not null)
        {
            try
            {
                File.AppendAllText(_priebeznyZaznam, riadok + Environment.NewLine, Encoding.UTF8);
            }
            catch (Exception)
            {
                // priebežný záznam nie je nevyhnutný
            }
        }
    }
}
