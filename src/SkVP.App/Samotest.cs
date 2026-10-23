using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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

            // Kalendárium (meniny, sviatky) v dočasnom priečinku, dashboard bez sťahovania zo siete
            Environment.SetEnvironmentVariable("KALENDARIUM_DIR", Path.Combine(Databaza.Priecinok, "Kalendarium"));
            App.PripravKalendarium();
            DashboardViewModel.BezSiete = true;

            TestUcebnehoPlanu();
            TestObjektov();
            TestDokumentov();
            TestVlozeniaZWordu();
            TestZostav();
            TestLogaAHlavicky();
            TestPremenovania();
            TestVystupov();
            TestImportuAExportu();
            TestServisu();
            TestSpravyDatabazy();
            TestImportuStruktury();
            TestUdajovOrganizacie();
            TestJazykovychOsnov(aplikacia);
            TestUvodnejTabulky();
            TestZaloh();
            TestNoviniek();
            TestDashboarduDna();
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
        var behy = new List<Run>();
        void Zbieraj(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                if (inline is Run r)
                {
                    behy.Add(r);
                }
                else if (inline is Span sp)
                {
                    Zbieraj(sp.Inlines);
                }
            }
        }

        foreach (var p in vlozeny.Blocks.OfType<Paragraph>())
        {
            Zbieraj(p.Inlines);
        }

        var cervene = behy.Where(r => r.Text.Contains("známkou", StringComparison.Ordinal)).ToList();
        Over(cervene.Count > 0 && cervene.All(r => r.Foreground is not SolidColorBrush { Color: var c } || c != Colors.Red),
            "farba vloženého textu sa zrušila");
        var pisma = string.Join(", ", behy.Select(r => r.FontFamily.Source).Distinct());
        Over(behy.All(r => r.FontFamily.Source is not ("Arial" or "Comic Sans MS")), $"cudzie písmo vloženého textu sa zrušilo ({pisma})");
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

        // texty úvodnej strany – úprava a vypnutie riadkov
        var titul = TitulnaStrana.Nacitaj();
        Over(titul.PataZobrazit && DokumentOsnovy.Text(titul.PataNaStranu(new DateTime(2026, 10, 23))).Trim() == "Vytvorené 23. 10. 2026 v programe SkVP",
            "predvolený text dole na úvodnej strane");
        titul.Typ = TitulnaStrana.AktualnyStupenGymnazia;
        titul.PataZobrazit = false;
        titul.VlastneRiadky = "Schválené pedagogickou radou dňa 28. 8. 2026\n\nPrerokované so zriaďovateľom";
        titul.Podnadpis = "Učebné osnovy – testovacia verzia";
        titul.Uloz();
        var upravenyTitul = TitulnaStrana.Nacitaj();
        Over(!upravenyTitul.PataZobrazit && upravenyTitul.RiadkyPodNadpisom().Count() == 3 && upravenyTitul.Typ.Contains("ISCED 344", StringComparison.Ordinal),
            "uložené texty úvodnej strany");
        var sTitulom = ZostavaOsnov.Zostav(new VolbyZostavy { Oblasti = new[] { plne.Oblasti[4].Id } });
        var docxTitul = Path.Combine(export, "samotest_uvodna_strana.docx");
        DocxVystup.Uloz(sTitulom, docxTitul);
        var textTitulu = DokumentOsnovy.Text(DocxCitac.Nacitaj(docxTitul).Bloky);
        Over(!textTitulu.Contains("Vytvorené", StringComparison.Ordinal) && textTitulu.Contains("Schválené pedagogickou radou", StringComparison.Ordinal)
             && textTitulu.Contains("ISCED 344", StringComparison.Ordinal) && !textTitulu.Contains("ISCED 3A", StringComparison.Ordinal), "úvodná strana bez vypnutého riadku a s vlastnými textami");
        var vykreslenieTitulu = NahladDokumentu.Priprav(PdfVystup.Vytvor(sTitulom));
        UlozObrazokStrany(vykreslenieTitulu, 1, Path.Combine(export, "uvodna_strana_upravena.png"));

        // text dole na strane s tabuľkou (schválenie a podpisy)
        var sTabulkou = TitulnaStrana.Nacitaj();
        sTabulkou.PataZobrazit = true;
        sTabulkou.PataBloky = new List<Blok>
        {
            new Odsek(new[] { new Usek("Školský vzdelávací program bol prerokovaný v pedagogickej rade a schválený v roku {rok}.") }),
            new Tabulka(new IReadOnlyList<Bunka>[]
            {
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("Prerokované v pedagogickej rade", Tucne: true) }) }, Hlavicka: true),
                        new Bunka(new Blok[] { new Odsek(new[] { new Usek("Schválil riaditeľ školy", Tucne: true) }) }, Hlavicka: true) },
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("dňa {datum}") }) }),
                        new Bunka(new Blok[] { new Odsek(new[] { new Usek("podpis a pečiatka") }) }) },
            }, 2),
        };
        sTabulkou.Uloz();
        var nacitanaPata = TitulnaStrana.Nacitaj();
        Over(nacitanaPata.PataBloky.OfType<Tabulka>().Count() == 1, "text dole na úvodnej strane môže obsahovať tabuľku");
        var dokumentSTabulkou = ZostavaOsnov.Zostav(new VolbyZostavy { Oblasti = new[] { plne.Oblasti[4].Id } });
        var docxPata = Path.Combine(export, "samotest_uvodna_strana_tabulka.docx");
        DocxVystup.Uloz(dokumentSTabulkou, docxPata);
        OverDocx(File.ReadAllBytes(docxPata), "úvodná strana s tabuľkou");
        var textPaty = DokumentOsnovy.Text(DocxCitac.Nacitaj(docxPata).Bloky);
        Over(textPaty.Contains("Schválil riaditeľ školy", StringComparison.Ordinal) && textPaty.Contains($"schválený v roku {DateTime.Now.Year}", StringComparison.Ordinal),
            "tabuľka a doplnený rok na úvodnej strane");
        UlozObrazokStrany(NahladDokumentu.Priprav(PdfVystup.Vytvor(dokumentSTabulkou)), 1, Path.Combine(export, "uvodna_strana_tabulka.png"));
        TitulnaStrana.Predvolene();
        Over(TitulnaStrana.Nacitaj().PataZobrazit && TitulnaStrana.Nacitaj().Podnadpis == "Učebné osnovy"
             && TitulnaStrana.Nacitaj().PataBloky.Count == 1, "predvolené texty úvodnej strany");

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

    /// <summary>Logo školy na úvodnej strane a hlavička ďalších strán (text vľavo, škola a logo vpravo, vypínanie).</summary>
    private static void TestLogaAHlavicky()
    {
        var export = Databaza.PriecinokDokumentov;
        Directory.CreateDirectory(export);
        var zdrojLoga = Path.Combine(Databaza.Priecinok, "samotest_logo.png");
        VytvorSkusobneLogo(zdrojLoga, 360, 240);
        NastaveniaSkoly.NastavLogo(zdrojLoga);
        var logo = NastaveniaSkoly.CestaLoga();
        Over(logo is not null && NastaveniaSkoly.Rozmer(logo) == (360, 240), "logo školy je nastavené");

        var t = TitulnaStrana.Nacitaj();
        Over(t.LogoZobrazit && t.HlavickaZobrazit && t.TextHlavickyZobrazit && t.SkolaVHlavickeZobrazit && t.LogoVHlavickeZobrazit,
            "logo na úvodnej strane aj všetky časti hlavičky sú predvolene zapnuté");
        Over(t.TextHlavickyVysledny == "Školský vzdelávací program – Učebné osnovy", $"predvolený text hlavičky ({t.TextHlavickyVysledny})");
        t.Skola = "Gymnázium Ukážková 1, Vzorovo";
        t.Uloz();

        var idOblasti = new ObjektService().Oblasti()[4].Id;
        CelyDokument Zostava() => ZostavaOsnov.Zostav(new VolbyZostavy { Oblasti = new[] { idOblasti } });
        var hornyOkraj = new Int32Rect(400, 0, 394, 96);   // hlavička vpravo hore (nad textom strany)
        var oblastLoga = new Int32Rect(60, 112, 360, 160); // logo na úvodnej strane pod pásom farieb

        // všetko zapnuté
        var plna = Zostava();
        var vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(plna));
        Over(PixelyLoga(vykreslenie, 1, oblastLoga) > 500, "logo na úvodnej strane (PDF a tlač)");
        Over(PixelyLoga(vykreslenie, 2, hornyOkraj) > 100, "logo v hlavičke ďalších strán (PDF a tlač)");
        Over(PixelyLoga(vykreslenie, 1, hornyOkraj) == 0, "úvodná strana nemá hlavičku");
        PdfVystup.UlozPdf(plna, Path.Combine(export, "samotest_hlavicka_s_logom.pdf"));
        UlozObrazokStrany(vykreslenie, 1, Path.Combine(export, "uvodna_strana_logo.png"));
        UlozObrazokStrany(vykreslenie, 2, Path.Combine(export, "hlavicka_s_logom.png"));
        var docx = Path.Combine(export, "samotest_hlavicka_s_logom.docx");
        DocxVystup.Uloz(plna, docx);
        OverDocx(File.ReadAllBytes(docx), "hlavička s logom");
        var (text, obrazkyHlavicky, obrazkyTela, hlaviciek) = HlavickaWordu(docx);
        Over(hlaviciek == 1 && text.Contains("Školský vzdelávací program – Učebné osnovy", StringComparison.Ordinal)
             && text.Contains("Gymnázium Ukážková 1", StringComparison.Ordinal) && obrazkyHlavicky == 1,
            $"hlavička vo Worde: text, škola a logo ({text}; obrázkov {obrazkyHlavicky})");
        Over(obrazkyTela == 1, "logo na úvodnej strane vo Worde");

        // jednotlivé časti vypnuté: logo na úvodnej strane, logo a text v hlavičke; vlastný text hlavičky
        t = TitulnaStrana.Nacitaj();
        t.LogoZobrazit = false;
        t.LogoVHlavickeZobrazit = false;
        t.TextHlavicky = "ŠkVP – Učebné osnovy gymnázia";
        t.Uloz();
        Over(TitulnaStrana.Nacitaj().TextHlavickyVysledny == "ŠkVP – Učebné osnovy gymnázia", "vlastný text hlavičky sa uložil");
        var bezLoga = Zostava();
        vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(bezLoga));
        Over(PixelyLoga(vykreslenie, 1, oblastLoga) == 0, "vypnuté logo na úvodnej strane");
        Over(PixelyLoga(vykreslenie, 2, hornyOkraj) == 0, "vypnuté logo v hlavičke");
        UlozObrazokStrany(vykreslenie, 2, Path.Combine(export, "hlavicka_bez_loga.png"));
        DocxVystup.Uloz(bezLoga, docx);
        OverDocx(File.ReadAllBytes(docx), "hlavička bez loga");
        (text, obrazkyHlavicky, obrazkyTela, hlaviciek) = HlavickaWordu(docx);
        Over(hlaviciek == 1 && obrazkyHlavicky == 0 && obrazkyTela == 0 && text.Contains("ŠkVP – Učebné osnovy gymnázia", StringComparison.Ordinal),
            "Word: bez loga, s vlastným textom hlavičky");

        t = TitulnaStrana.Nacitaj();
        t.TextHlavickyZobrazit = false;
        t.LogoVHlavickeZobrazit = true;
        t.Uloz();
        var lenSkola = Zostava();
        DocxVystup.Uloz(lenSkola, docx);
        OverDocx(File.ReadAllBytes(docx), "hlavička bez textu");
        (text, obrazkyHlavicky, _, _) = HlavickaWordu(docx);
        Over(!text.Contains("Učebné osnovy", StringComparison.Ordinal) && text.Contains("Gymnázium Ukážková 1", StringComparison.Ordinal) && obrazkyHlavicky == 1,
            "vypnutý text hlavičky – zostane škola a logo");
        UlozObrazokStrany(NahladDokumentu.Priprav(PdfVystup.Vytvor(lenSkola)), 2, Path.Combine(export, "hlavicka_skola_a_logo.png"));

        // celá hlavička vypnutá naraz – nastavenie jednotlivých častí sa zachová
        t = TitulnaStrana.Nacitaj();
        t.HlavickaZobrazit = false;
        t.Uloz();
        var bezHlavicky = Zostava();
        vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(bezHlavicky));
        Over(PixelyLoga(vykreslenie, 2, hornyOkraj) == 0 && !bezHlavicky.Titul.MaHlavicku(logo), "celá hlavička vypnutá naraz");
        UlozObrazokStrany(vykreslenie, 2, Path.Combine(export, "hlavicka_vypnuta.png"));
        DocxVystup.Uloz(bezHlavicky, docx);
        OverDocx(File.ReadAllBytes(docx), "dokument bez hlavičky");
        Over(HlavickaWordu(docx).Hlaviciek == 0, "Word: dokument bez hlavičky");
        var znova = TitulnaStrana.Nacitaj();
        Over(!znova.HlavickaZobrazit && !znova.TextHlavickyZobrazit && znova.SkolaVHlavickeZobrazit && znova.LogoVHlavickeZobrazit,
            "po vypnutí celej hlavičky sa jednotlivé časti pamätajú");

        // predvolené: logo zostane, hlavička aj logo sú znova zapnuté a text sleduje nadpis
        TitulnaStrana.Predvolene();
        t = TitulnaStrana.Nacitaj();
        Over(NastaveniaSkoly.CestaLoga() is not null && t.LogoZobrazit && t.HlavickaZobrazit && t.TextHlavickyZobrazit
             && t.TextHlavickyVysledny == "Školský vzdelávací program – Učebné osnovy", "predvolená hlavička (logo zostalo)");
        t.Podnadpis = "Učebné osnovy 2026";
        t.TextHlavicky = t.Hlavicka;
        t.Uloz();
        Over(Databaza.Citaj("titul.hlavicka.text") is null, "text hlavičky rovnaký ako nadpis sa neukladá zvlášť");
        TitulnaStrana.Predvolene();
        UlozObrazokStrany(NahladDokumentu.Priprav(PdfVystup.Vytvor(Zostava())), 2, Path.Combine(export, "hlavicka_predvolena.png"));

        // upratať – testovacia sada neskôr nahrá údaje vymyslenej školy len do programu bez vlastnej školy a loga
        NastaveniaSkoly.OdstranLogo();
        Databaza.Zapis(UdajeSkVP.KlucSkola, "");
        Krok("logo a hlavička v poriadku");
    }

    /// <summary>Nový názov predmetu sa prenesie aj do textov dokumentov (celé výskyty, formátovanie zostane, archív).</summary>
    private static void TestPremenovania()
    {
        // nahradenie v dokumente Word – aj keď je názov rozdelený do behov s rôznym formátovaním
        var bloky = new List<Blok>
        {
            new Odsek(new[] { new Usek("Predmet "), new Usek("Fyz", Tucne: true), new Usek("ika nadväzuje na predmet Fyzika z 1. ročníka.") }),
            new Odsek(new[] { new Usek("Fyzikálne pokusy, Fyzika2, Matematika a práca s informáciami, Matematika.") }),
        };
        var data = DocxVystup.Osnova(bloky);
        var nove = PremenovanieVDokumentoch.Nahrad(data, "Fyzika", "Fyzika a astronómia", Array.Empty<string>(), out var pocet);
        var text = nove is null ? "" : DokumentOsnovy.Text(DocxCitac.Nacitaj(new MemoryStream(nove)).Bloky);
        Over(pocet == 2 && text.Contains("Predmet Fyzika a astronómia nadväzuje na predmet Fyzika a astronómia z 1. ročníka.", StringComparison.Ordinal)
             && text.Contains("Fyzikálne pokusy, Fyzika2", StringComparison.Ordinal), $"nahradenie celého názvu v dokumente ({pocet}): {text}");
        OverDocx(nove!, "dokument po premenovaní");
        var chranene = PremenovanieVDokumentoch.Nahrad(data, "Matematika", "Matematika 2", new[] { "Matematika a práca s informáciami" }, out var pocetM);
        var textM = chranene is null ? "" : DokumentOsnovy.Text(DocxCitac.Nacitaj(new MemoryStream(chranene)).Bloky);
        Over(pocetM == 1 && textM.Contains("Matematika a práca s informáciami, Matematika 2.", StringComparison.Ordinal),
            "názov oblasti, ktorý obsahuje názov predmetu, sa nezmení");
        Over(PremenovanieVDokumentoch.Nahrad(data, "Chémia", "Chémia 2", Array.Empty<string>(), out _) is null, "dokument bez názvu sa nemení");

        // jednorazová úprava: Konverzácia z nemeckého jazyka → Konverzácia v nemeckom jazyku (aj v textoch)
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var jazyk = sluzba.Oblasti()[0];
        // stará databáza: predmet má ešte pôvodný názov
        var stary = sluzba.Deti(jazyk.Id).First(p => p.Nazov == "Konverzácia v nemeckom jazyku");
        sluzba.Premenuj(stary.Id, "Konverzácia z nemeckého jazyka");
        var malOsnovu = dokumenty.Existuje(stary.Id);
        dokumenty.UlozBloky(stary.Id, new List<Blok>
        {
            new Odsek(new[] { new Usek("Konverzácia z nemeckého jazyka rozvíja ústny prejav.") }),
        }, null);
        var malCharakteristiku = dokumenty.Existuje(jazyk.Id);
        var charakteristika = dokumenty.Nacitaj(jazyk.Id).Bloky;
        dokumenty.UlozBloky(jazyk.Id, charakteristika.Append(new Odsek(new[] { new Usek("Patrí sem aj Konverzácia z nemeckého jazyka.") })).ToList(), null);
        Over(Migracie.PremenujPredmet("Konverzácia z nemeckého jazyka", "Konverzácia v nemeckom jazyku") == 1
             && sluzba.Nacitaj(stary.Id)!.Nazov == "Konverzácia v nemeckom jazyku", "premenovanie predmetu Konverzácia v nemeckom jazyku");
        Over(DokumentOsnovy.Text(dokumenty.Nacitaj(stary.Id).Bloky).Contains("Konverzácia v nemeckom jazyku rozvíja", StringComparison.Ordinal)
             && DokumentOsnovy.Text(dokumenty.Nacitaj(jazyk.Id).Bloky).Contains("Patrí sem aj Konverzácia v nemeckom jazyku.", StringComparison.Ordinal),
            "nový názov aj v osnove a charakteristike oblasti");
        Over(dokumenty.Verzie(stary.Id).FirstOrDefault()?.Dovod?.Contains("Pred premenovaním", StringComparison.Ordinal) == true,
            "pred premenovaním v texte sa zarchivovala verzia");
        Migracie.Spusti();
        Over(Migracie.PremenujPredmet("Konverzácia z nemeckého jazyka", "Konverzácia v nemeckom jazyku") == 0, "opakované premenovanie nič nezmení");
        if (malCharakteristiku)
        {
            dokumenty.UlozBloky(jazyk.Id, charakteristika, null);
        }
        else
        {
            using var db = Databaza.Otvor();
            Databaza.Vykonaj(db, "DELETE FROM verzia WHERE objekt_id = $id; DELETE FROM dokument WHERE objekt_id = $id;", ("$id", jazyk.Id));
        }

        if (!malOsnovu)
        {
            using var db = Databaza.Otvor();
            Databaza.Vykonaj(db, "DELETE FROM verzia WHERE objekt_id = $id; DELETE FROM dokument WHERE objekt_id = $id;", ("$id", stary.Id));
        }

        Krok("premenovanie v dokumentoch v poriadku");
    }

    /// <summary>Import osnov z Wordu a PDF (archivácia s rokom), oblasti a predmety z dokumentu, export osnov.</summary>
    private static void TestImportuAExportu()
    {
        var export = Databaza.PriecinokDokumentov;
        Directory.CreateDirectory(export);
        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var predmety = ImportOsnov.Predmety();

        // rozpoznanie predmetu
        Over(ImportOsnov.CistyNazov("05_Fyzika_2026") == "Fyzika", "čistý názov zo súboru");
        Over(ImportOsnov.Rozpoznaj("07 Fyzika 2025", "", predmety).Id is long, "predmet podľa názvu súboru");
        Over(ImportOsnov.Rozpoznaj("osnova", "Učebné osnovy\nPredmet: Chémia\nCharakteristika predmetu", predmety).Navrh == "Chémia", "predmet podľa textu dokumentu");
        Over(ImportOsnov.Rozpoznaj("Astronómia a vesmír", "", predmety).Id is null, "neznámy predmet → nový");

        // prevod textu PDF na osnovu
        var bloky = TextNaOsnovu.Rozober("CHARAKTERISTIKA PREDMETU\nPredmet rozvíja prírodovedné myslenie\nžiakov a ich zručnosti.\n\nCiele predmetu\n• pozorovať javy\n• merať veličiny\n\n1. ročník\nTéma\tObsah\tVýkon\nPohyb\trýchlosť\tvypočíta rýchlosť\n");
        Over(bloky.OfType<Nadpis>().Count() == 3 && bloky.OfType<Zoznam>().Single().Polozky.Count == 2 && bloky.OfType<Tabulka>().Count() == 1
             && bloky.OfType<Odsek>().Any(o => DokumentOsnovy.Text(new Blok[] { o }).Contains("myslenie žiakov", StringComparison.Ordinal)),
            $"text PDF → nadpisy, odseky, zoznam a tabuľka ({string.Join(", ", bloky.Select(b => b.GetType().Name))})");

        // import z Wordu do existujúceho predmetu – archivácia s rokom
        var fyzika = predmety.First(p => p.Nazov == "Fyzika");
        dokumenty.UlozBloky(fyzika.Id, new List<Blok> { new Odsek(new[] { new Usek("Pôvodná osnova fyziky.") }) }, null);
        var verziePred = dokumenty.Verzie(fyzika.Id).Count;
        var zdroj = Path.Combine(export, "07 Fyzika 2025.docx");
        File.WriteAllBytes(zdroj, DocxVystup.Osnova(new List<Blok> { new Nadpis(1, "Charakteristika predmetu"), new Odsek(new[] { new Usek("Nová osnova fyziky z roku 2025.") }) }));
        var subor = ImportOsnov.Nacitaj(zdroj, predmety);
        Over(subor.PredmetId == fyzika.Id && subor.Rok == 2025 && !subor.JePdf, "import: rozpoznaný predmet a rok zo súboru");
        var vysledok = ImportOsnov.Importuj(subor, fyzika.Id, 2025);
        var cislo = ImportOsnov.Cislo(fyzika.Id);
        Over(vysledok.Archivovane && dokumenty.Verzie(fyzika.Id).Count == verziePred + 1
             && dokumenty.Verzie(fyzika.Id)[0].Dovod!.Contains($"rok {DateTime.Now.Year}", StringComparison.Ordinal), "stará osnova je zarchivovaná s rokom a záznamom");
        Over(DokumentOsnovy.Text(dokumenty.Nacitaj(fyzika.Id).Bloky).Contains("z roku 2025", StringComparison.Ordinal), "nová osnova je uložená");
        Over(InfoOsnovyService.Nacitaj(fyzika.Id) is { Rok: 2025, Zdroj: "07 Fyzika 2025.docx" }, "rok zmeny osnov a zdroj importu");
        Over(File.Exists(Path.Combine(ImportOsnov.PriecinokOsnov, "2025", $"{cislo} Fyzika 2025.docx")), "kópia novej osnovy „číslo názov rok“");
        Over(File.Exists(Path.Combine(ImportOsnov.PriecinokOsnov, "Archív", $"{cislo} Fyzika – archív {DateTime.Now.Year}.docx")), "súbor archivovanej osnovy „… – archív rok“");
        Over(File.ReadAllBytes(zdroj).Length > 0 && File.Exists(zdroj), "zdrojový súbor zostal");

        // import do nového predmetu cez sprievodcu (bez okna)
        var model = new MainViewModel();
        var novy = Path.Combine(export, "Astronómia.docx");
        File.WriteAllBytes(novy, DocxVystup.Osnova(new List<Blok> { new Odsek(new[] { new Usek("Osnova astronómie.") }) }));
        var sprievodca = new ImportOsnovViewModel(model);
        sprievodca.PridajSubory(new[] { novy });
        var polozka = sprievodca.Polozky.Single();
        Over(polozka.JeNovy && polozka.NovyNazov == "Astronómia", "sprievodca navrhne nový predmet");
        polozka.NovaOblast = model.Oblasti.First(o => o.Nazov == "Človek a príroda");
        Over(sprievodca.Importuj() == 1 && model.Oblasti.First(o => o.Nazov == "Človek a príroda").Predmety.Any(p => p.Nazov == "Astronómia"),
            "import do nového predmetu");
        var astronomia = model.Oblasti.SelectMany(o => o.Predmety).First(p => p.Nazov == "Astronómia");

        // import z PDF (čítanie PDF a rozpoznávanie textu Windows)
        var pdf = Path.Combine(export, "Chémia osnova.pdf");
        PdfVystup.UlozPdf(ZostavaOsnov.Zostav(new VolbyZostavy
        {
            Predmety = new[] { fyzika.Id }, TitulnaStrana = false, Obsah = false, OddelovacOblasti = false, CharakteristikyOblasti = false,
        }), pdf);
        try
        {
            var zPdf = ImportOsnov.Nacitaj(pdf, predmety);
            Over(zPdf.JePdf && zPdf.Text.Length > 20 && zPdf.Docx.Length > 1000, $"PDF prevedené na Word bez Wordu ({zPdf.Text.Length} znakov)");
            OverDocx(zPdf.Docx, "osnova z PDF");
            Krok($"text z PDF: {zPdf.Text.Replace('\n', ' ')[..Math.Min(120, zPdf.Text.Length)]}");
        }
        catch (SkvpException ex)
        {
            Krok($"import z PDF: {ex.Message} (OCR na tomto počítači nie je – preskočené)");
        }

        // oblasti a predmety z dokumentu
        var plan = StrukturaOsnov.Aktualna();
        var xlsx = Path.Combine(export, "samotest_oblasti.xlsx");
        StrukturaOsnov.UlozExcel(plan, xlsx);
        var zExcelu = StrukturaOsnov.NacitajZoSuboru(xlsx);
        Over(zExcelu.Oblasti.Count == plan.Oblasti.Count && zExcelu.PocetPredmetov == plan.PocetPredmetov, "oblasti a predmety: Excel tam a späť");
        var docx = Path.Combine(export, "samotest_oblasti.docx");
        StrukturaOsnov.UlozWord(plan, docx);
        OverDocx(File.ReadAllBytes(docx), "oblasti a predmety vo Worde");
        var zWordu = StrukturaOsnov.NacitajZoSuboru(docx);
        Over(zWordu.Oblasti.Count == plan.Oblasti.Count && zWordu.PocetPredmetov == plan.PocetPredmetov,
            $"oblasti a predmety: Word tam a späť ({zWordu.Oblasti.Count}/{zWordu.PocetPredmetov})");
        var zTextu = StrukturaOsnov.ZTextu("Vzdelávacie oblasti a predmety\nJazyk a komunikácia\nSlovenský jazyk a literatúra\nAnglický jazyk\nMATEMATIKA A PRÁCA S INFORMÁCIAMI\nMatematika\nInformatika\n");
        Over(zTextu.Oblasti.Count == 2 && zTextu.PocetPredmetov == 4, $"oblasti a predmety z textu PDF ({zTextu.Oblasti.Count}/{zTextu.PocetPredmetov})");
        var zZoznamu = StrukturaOsnov.ZBlokov(new List<Blok>
        {
            new Nadpis(1, "Človek a príroda"),
            new Zoznam(false, new[] { (IReadOnlyList<Blok>)new Blok[] { new Odsek(new[] { new Usek("Fyzika") }) }, new Blok[] { new Odsek(new[] { new Usek("Chémia") }) } }),
            new Odsek(new[] { new Usek("Zdravie a pohyb", Tucne: true) }),
            new Zoznam(false, new[] { (IReadOnlyList<Blok>)new Blok[] { new Odsek(new[] { new Usek("Telesná a športová výchova") }) } }),
        });
        Over(zZoznamu.Oblasti.Count == 2 && zZoznamu.PocetPredmetov == 3, "oblasti a predmety z nadpisov a zoznamov Wordu");
        SuboryZostavy.Uloz(StrukturaOsnov.Dokument(plan), Path.Combine(export, "samotest_oblasti.pdf"), new VolbyPdf());

        // export osnov
        var exportModel = new ExportOsnovViewModel(model) { Len = true };
        foreach (var o in exportModel.Oblasti)
        {
            o.Zahrnut = o.Oblast.Nazov == "Človek a príroda";
        }

        var volby = exportModel.Volby();
        Over(volby.Predmety!.Count == model.Oblasti.First(o => o.Nazov == "Človek a príroda").Predmety.Count, "export: výber predmetov oblasti");
        var cesty = ExportOsnov.Uloz(ZostavaOsnov.Zostav(volby), FormatExportu.Word, Path.Combine(export, "export_priroda.docx"));
        OverDocx(File.ReadAllBytes(cesty[0]), "export osnov do Wordu");
        var jpg = ExportOsnov.Uloz(ZostavaOsnov.Zostav(volby with { TitulnaStrana = false, Obsah = false }), FormatExportu.Jpeg, Path.Combine(export, "export_priroda.jpg"));
        Over(jpg.Count > 1 && jpg.All(File.Exists), $"export osnov do JPEG ({jpg.Count} strán)");
        var priecinok = Path.Combine(export, "Export jednotlivo");
        var jednotlivo = ExportOsnov.UlozJednotlivo(volby with { NespracovanePredmety = true }, FormatExportu.Pdf, priecinok);
        Over(jednotlivo.Count == volby.Predmety.Count && jednotlivo.Any(c => Path.GetFileName(c) == $"{cislo} Fyzika 2025.pdf"),
            $"každý predmet zvlášť s rokom v názve ({jednotlivo.Count}): {string.Join(", ", jednotlivo.Select(Path.GetFileName).Take(4))}");

        // upratať – nový predmet a rok fyziky
        sluzba.Odstran(astronomia.Id);
        InfoOsnovyService.Uloz(fyzika.Id, InfoOsnovy.Prazdne);
        Krok("import a export v poriadku");
    }

    /// <summary>
    /// Import osnov oblasti Jazyk a komunikácia z priečinka: skratky v názvoch súborov, profilácia konverzácie v anglickom
    /// jazyku z úvodnej tabuľky, úroveň B1/B2 v názve predmetu. Na konci sa dáta obnovia zo zálohy.
    /// </summary>
    private static void TestJazykovychOsnov(App aplikacia)
    {
        var zaloha = new ZalohaService().Zalohuj();
        var priecinok = Path.Combine(Databaza.PriecinokDokumentov, "Moje osnovy");
        Directory.CreateDirectory(priecinok);
        byte[] Osnova(string profilacia) => DocxVystup.Osnova(new List<Blok>
        {
            new Tabulka(new IReadOnlyList<Bunka>[]
            {
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("Názov predmetu") }) }), new Bunka(new Blok[] { new Odsek(new[] { new Usek("Konverzácia v anglickom jazyku") }) }) },
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("Profilácia") }) }), new Bunka(new Blok[] { new Odsek(new[] { new Usek(profilacia) }) }) },
            }, 2),
            new Nadpis(1, "Charakteristika predmetu"),
            new Odsek(new[] { new Usek("Text osnovy.") }),
        });
        var subory = new Dictionary<string, string>
        {
            ["KAJ profilácia ANJ.docx"] = "anglický jazyk",
            ["KAJ šport.docx"] = "šport, informatika, STEAM",
            ["RUJ.docx"] = "", ["NEJ.docx"] = "", ["KRJ.docx"] = "", ["KNJ.docx"] = "",
            ["Jazykom k maturite RUJ.docx"] = "", ["Jazykom k maturite_NEJ.docx"] = "", ["Jazykom k maturite ANJ.docx"] = "",
            ["Reálie anglicky hovoriacich krajín.docx"] = "", ["ANJ.docx"] = "", ["SJL.docx"] = "", ["SSJ.docx"] = "",
        };
        foreach (var (nazov, profilacia) in subory)
        {
            File.WriteAllBytes(Path.Combine(priecinok, nazov), Osnova(profilacia));
        }

        var model = new MainViewModel();
        var sprievodca = new ImportOsnovViewModel(model);
        sprievodca.PridajPriecinok(priecinok);
        Over(sprievodca.Polozky.Count == subory.Count && sprievodca.Polozky.All(p => p.Subor is not null), $"import z priečinka: {sprievodca.Polozky.Count} súborov");
        string Navrh(string subor) => sprievodca.Polozky.Single(p => p.NazovSuboru == subor).NovyNazov;
        Over(Navrh("KAJ profilácia ANJ.docx") == "Konverzácia v anglickom jazyku - profilácia anglický jazyk – úroveň B2", $"KAJ – profilácia anglický jazyk ({Navrh("KAJ profilácia ANJ.docx")})");
        Over(Navrh("KAJ šport.docx") == "Konverzácia v anglickom jazyku - profilácia šport, informatika, steam – úroveň B2", $"KAJ – ostatné profilácie ({Navrh("KAJ šport.docx")})");
        Over(Navrh("RUJ.docx") == "Ruský jazyk – úroveň B1" && Navrh("KNJ.docx") == "Konverzácia v nemeckom jazyku – úroveň B1"
             && Navrh("KRJ.docx") == "Konverzácia v ruskom jazyku – úroveň B1" && Navrh("NEJ.docx") == "Nemecký jazyk – úroveň B1", "úroveň B1: RUJ, NEJ, KRJ, KNJ");
        Over(Navrh("Jazykom k maturite_NEJ.docx") == "Jazykom k maturite - nemecký jazyk – úroveň B2" && Navrh("ANJ.docx") == "Anglický jazyk – úroveň B2"
             && Navrh("Reálie anglicky hovoriacich krajín.docx") == "Reálie anglicky hovoriacich krajín – úroveň B2", "úroveň B2: Jazykom k maturite, ANJ, Reálie");
        Over(Navrh("SJL.docx") == "Slovenský jazyk a literatúra" && Navrh("SSJ.docx") == "Seminár zo slovenského jazyka a literatúry"
             && !sprievodca.Polozky.Single(p => p.NazovSuboru == "SJL.docx").CudziJazyk, "SJL a SSJ bez úrovne");
        var kaj = sprievodca.Polozky.Where(p => p.NazovSuboru.StartsWith("KAJ", StringComparison.Ordinal)).ToList();
        Over(kaj.Count(p => !p.JeNovy) == 1 && kaj.Count(p => p.JeNovy) == 1, "KAJ: jedna profilácia do existujúceho predmetu, druhá ako nový predmet");
        Over(sprievodca.Polozky.Count(p => p.NazovSuboru.StartsWith("Jazykom", StringComparison.Ordinal) && !p.JeNovy) == 1, "Jazykom k maturite: existujúci predmet sa použije raz");

        var okno = new ImportOsnovWindow(sprievodca);
        okno.Show();
        Pumpuj();
        Snimka(okno, "25_import_jazykovych_osnov");
        okno.Close();

        Over(sprievodca.Importuj() == subory.Count, "import všetkých jazykových osnov");
        var nazvy = new ObjektService().Deti(new ObjektService().Oblasti().First(o => o.Nazov == JazykovePravidla.Oblast).Id).Select(p => p.Nazov).ToList();
        Krok("predmety oblasti Jazyk a komunikácia: " + string.Join(" | ", nazvy));
        Over(nazvy.Contains("Konverzácia v anglickom jazyku - profilácia anglický jazyk – úroveň B2")
             && nazvy.Contains("Konverzácia v anglickom jazyku - profilácia šport, informatika, steam – úroveň B2")
             && nazvy.Contains("Jazykom k maturite - ruský jazyk – úroveň B2") && nazvy.Contains("Jazykom k maturite - anglický jazyk – úroveň B2")
             && nazvy.Contains("Reálie anglicky hovoriacich krajín – úroveň B2") && nazvy.Contains("Ruský jazyk – úroveň B1")
             && nazvy.Contains("Slovenský jazyk a literatúra") && !nazvy.Contains("Konverzácia v anglickom jazyku")
             && !nazvy.Contains("Reálie v anglickom jazyku") && !nazvy.Contains("Jazykom k maturite"), "názvy predmetov po importe");
        int Index(string zaciatok) => nazvy.FindIndex(n => n.StartsWith(zaciatok, StringComparison.Ordinal));
        Over(Index("Konverzácia v anglickom jazyku - profilácia šport") == Index("Konverzácia v anglickom jazyku - profilácia anglický") + 1,
            "obe konverzácie v anglickom jazyku sú pod sebou");
        Over(Index("Konverzácia v ruskom jazyku") == Index("Konverzácia v nemeckom jazyku") + 1
             && Index("Konverzácia v nemeckom jazyku") == Index("Konverzácia v anglickom jazyku - profilácia šport") + 1,
            "konverzácia v ruskom jazyku je pod ostatnými konverzáciami");
        var maturita = nazvy.Select((n, i) => (n, i)).Where(x => x.n.StartsWith("Jazykom k maturite", StringComparison.Ordinal)).Select(x => x.i).ToList();
        Over(maturita.Count == 3 && maturita[2] - maturita[0] == 2, "Jazykom k maturite – všetky tri pod sebou");
        var rusky = new ObjektService().Oblasti().SelectMany(o => new ObjektService().Deti(o.Id)).First(p => p.Nazov == "Ruský jazyk – úroveň B1");
        Over(InfoOsnovyService.Nacitaj(rusky.Id).Uroven == "B1", "úroveň je zaznamenaná pri osnove");

        // budúci import už premenovaného predmetu: rovnaký predmet, úroveň sa ponúkne z názvu
        var znova = new ImportOsnovViewModel(new MainViewModel());
        znova.PridajSubory(new[] { Path.Combine(priecinok, "RUJ.docx") });
        Over(!znova.Polozky[0].JeNovy && znova.Polozky[0].Uroven == "B1", "opakovaný import nájde predmet s úrovňou");

        new ZalohaService().Obnov(zaloha);
        Krok("jazykové osnovy v poriadku");
    }

    /// <summary>Profilácie a platnosť z úvodnej tabuľky osnovy – čítanie a zápis do dokumentu.</summary>
    private static void TestUvodnejTabulky()
    {
        var docx = DocxVystup.Osnova(new List<Blok> { TestovaciaSada.UvodnaTabulka("Fyzika"), new Nadpis(1, "Charakteristika predmetu") });
        var udaje = UvodnaTabulka.Precitaj(docx);
        Over(udaje.Profilacie == TestovaciaSada.ProfilacieSady && udaje.Platnost == "od 1.9.2026", $"úvodná tabuľka: {udaje}");
        var opravene = UvodnaTabulka.Zapis(docx, "šport, STEAM", "od 1.9.2027");
        OverDocx(opravene, "osnova po oprave úvodnej tabuľky");
        Over(UvodnaTabulka.Precitaj(opravene) is { Profilacie: "šport, STEAM", Platnost: "od 1.9.2027" }, "oprava sa zapísala do dokumentu");
        Over(DokumentOsnovy.Text(DocxCitac.Nacitaj(new MemoryStream(opravene)).Bloky).Contains("Názov predmetu", StringComparison.Ordinal),
            "ostatné riadky tabuľky zostali");

        // popis a hodnota v jednej bunke, platnosť bez popisu
        var jednaBunka = DocxVystup.Osnova(new List<Blok>
        {
            new Tabulka(new IReadOnlyList<Bunka>[]
            {
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("Profilácie: anglický jazyk, informatika") }) }) },
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("Učebné osnovy platné od 1. 9. 2026") }) }) },
            }, 1),
        });
        Over(UvodnaTabulka.Precitaj(jednaBunka) is { Profilacie: "anglický jazyk, informatika", Platnost: "od 1. 9. 2026" }, "údaje v jednej bunke a platnosť bez popisu");
        Over(UvodnaTabulka.Precitaj(UvodnaTabulka.Zapis(jednaBunka, "anglický jazyk", null)).Profilacie == "anglický jazyk", "oprava v jednej bunke");
        var bezTabulky = DocxVystup.Osnova(new List<Blok> { new Odsek(new[] { new Usek("Text.") }) });
        var chyba = false;
        try
        {
            UvodnaTabulka.Zapis(bezTabulky, "x", null);
        }
        catch (SkvpException)
        {
            chyba = true;
        }

        Over(!UvodnaTabulka.Precitaj(bezTabulky).MaUdaje && chyba, "osnova bez úvodnej tabuľky");
        Krok("úvodná tabuľka v poriadku");
    }

    /// <summary>Servis: záloha osnov vo Worde, obnova zo zálohy, kontrola databázy, export a import nastavení.</summary>
    private static void TestServisu()
    {
        var zipOsnov = ZalohaService.ZalohujOsnovy();
        using (var archiv = System.IO.Compression.ZipFile.OpenRead(zipOsnov))
        {
            Over(archiv.Entries.Count > 5 && archiv.Entries.Any(e => e.FullName.Contains("/Archív/", StringComparison.Ordinal))
                 && archiv.Entries.Any(e => e.FullName.StartsWith("03 Človek a príroda/", StringComparison.Ordinal)),
                $"záloha učebných osnov vo Worde ({archiv.Entries.Count} súborov)");
            var wordy = archiv.Entries.Where(e => e.FullName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)).ToList();
            Over(wordy.Count > 0 && wordy.All(w => archiv.GetEntry(w.FullName[..^5] + ".pdf") is { Length: > 1000 }),
                $"pri každom dokumente Word je v zálohe aj PDF ({wordy.Count})");
        }

        Over(ZalohaService.Skontroluj() == "Databáza je v poriadku.", "kontrola databázy");
        var zaloha = new ZalohaService().Zalohuj();
        var sluzba = new ObjektService();
        var navyse = sluzba.Pridaj(TypObjektu.Oblast, null, "Oblasť po zálohe");
        new ZalohaService().Obnov(zaloha);
        Over(new ObjektService().Oblasti().All(o => o.Id != navyse.Id), "obnova všetkých dát zo zálohy");
        Over(new ZalohaService().Zalohy().Count >= 2, "pred obnovou sa zálohoval súčasný stav");

        ZalohaService.Frekvencia = ZalohaService.Tyzdenne;
        Over(ZalohaService.Frekvencia == ZalohaService.Tyzdenne, "nastavenie pravidelnej zálohy");
        ZalohaService.Frekvencia = ZalohaService.Denne;

        // export a import nastavení
        var subor = Path.Combine(Databaza.PriecinokDokumentov, "samotest_nastavenia.json");
        Databaza.Zapis(UdajeSkVP.KlucAdresa, "Skúšobná 1, Vzorovo");
        NastaveniaProgramu.Export(subor);
        Databaza.Zapis(UdajeSkVP.KlucAdresa, "iná");
        var pocet = NastaveniaProgramu.Import(NastaveniaProgramu.Nacitaj(subor));
        Over(pocet > 3 && Databaza.Citaj(UdajeSkVP.KlucAdresa) == "Skúšobná 1, Vzorovo", $"export a import nastavení ({pocet})");
        Databaza.Zapis(UdajeSkVP.KlucAdresa, "");
        Krok("servis v poriadku");
    }

    /// <summary>Správa databázy: kontroly, údržba, oprava väzieb a záchrana dát.</summary>
    private static void TestSpravyDatabazy()
    {
        Over(DiagnostikaService.RychlaKontrola().Ok && DiagnostikaService.UplnaKontrola().Ok, "rýchla a úplná kontrola integrity");
        Over(DiagnostikaService.KontrolaVazieb().Ok, "kontrola väzieb");
        var dat = DiagnostikaService.KontrolaDat();
        Over(dat.Riadky.Count > 2, $"kontrola dát ({string.Join(" | ", dat.Riadky.Take(3))})");
        Over(DiagnostikaService.Optimalizuj().Ok && DiagnostikaService.ObnovIndexy().Ok && DiagnostikaService.Kompaktuj().Ok, "optimalizácia, indexy, kompaktovanie");
        Over(DiagnostikaService.VerziaSchemy() == Databaza.VerziaSchemy && DiagnostikaService.Prehlad().Count == 7
             && DiagnostikaService.PoslednaKontrola() is not null, "prehľad databázy");

        // chybná väzba (súvislosť na zmazaný predmet) – nájde sa a opraví
        var sluzba = new ObjektService();
        var predmety = sluzba.Oblasti().SelectMany(o => sluzba.Deti(o.Id)).Where(p => p.Typ == TypObjektu.Predmet).Take(1).ToList();
        if (predmety.Count == 1)
        {
            using (var db = Databaza.Otvor())
            {
                Databaza.Vykonaj(db, "PRAGMA foreign_keys = OFF;");
                Databaza.Vykonaj(db, "INSERT INTO vazba(z_id, do_id, typ, vytvorene) VALUES($z, 99999999, 'suvisi', $t)", ("$z", predmety[0].Id), ("$t", Databaza.Teraz()));
            }

            Over(!DiagnostikaService.KontrolaVazieb().Ok, "kontrola väzieb nájde chybnú súvislosť");
            DiagnostikaService.OpravVazby();
            Over(DiagnostikaService.KontrolaVazieb().Ok, "oprava väzieb");
        }

        var predmetovPred = sluzba.Oblasti().SelectMany(o => sluzba.Deti(o.Id)).Count();
        var zachrana = DiagnostikaService.Zachran();
        Over(zachrana.Ok && new ObjektService().Oblasti().SelectMany(o => new ObjektService().Deti(o.Id)).Count() == predmetovPred,
            $"záchrana dát ({zachrana.Riadky.LastOrDefault()})");
        Krok("správa databázy v poriadku");
    }

    /// <summary>Oblasti a predmety z HTML, XML, JSON a diktovaného textu, export a znova import.</summary>
    private static void TestImportuStruktury()
    {
        var html = ImportStruktury.ZHtml("""
            <html><head><title>x</title><style>p{}</style></head><body>
            <h1>Učebný plán</h1>
            <table><tr><th>Vzdelávacia oblasť</th><th>Predmety</th></tr>
            <tr><td><b>Jazyk a komunikácia</b></td><td>Slovenský jazyk a literatúra<br>Anglický jazyk</td></tr>
            <tr><td>Človek a príroda</td><td>Fyzika<br>Chémia&nbsp;<br>Biológia</td></tr></table></body></html>
            """);
        Over(html.Oblasti.Count == 2 && html.Oblasti[1].Predmety.SequenceEqual(new[] { "Fyzika", "Chémia", "Biológia" }), $"oblasti a predmety z HTML tabuľky ({html.PocetPredmetov})");
        var zoznam = ImportStruktury.ZHtml("<h2>Matematika a práca s informáciami</h2><ul><li>Matematika</li><li>Informatika</li></ul><h2>Zdravie a pohyb</h2><ul><li>Telesná a športová výchova</li></ul>");
        Over(zoznam.Oblasti.Count == 2 && zoznam.Oblasti[0].Predmety.Count == 2, "oblasti a predmety z nadpisov a zoznamov HTML");
        var xml = ImportStruktury.ZXml("""<ucebnyPlan><oblast nazov="Človek a spoločnosť"><predmet>Dejepis</predmet><predmet>Geografia</predmet></oblast><oblast><nazov>Umenie a kultúra</nazov><predmet>Umenie a kultúra</predmet></oblast></ucebnyPlan>""");
        Over(xml.Oblasti.Count == 2 && xml.Oblasti[1].Nazov == "Umenie a kultúra" && xml.PocetPredmetov == 3, "oblasti a predmety z XML");
        var json = ImportStruktury.ZJson("""{"oblasti":[{"nazov":"Človek a hodnoty","predmety":["Etická výchova",{"nazov":"Náboženská výchova"}]}]}""");
        var mapa = ImportStruktury.ZJson("""{"Zdravie a pohyb":["Telesná a športová výchova"]}""");
        Over(json.Oblasti[0].Predmety.Count == 2 && mapa.Oblasti[0].Nazov == "Zdravie a pohyb", "oblasti a predmety z JSON");
        var diktat = ImportStruktury.ZDiktovania("Človek a príroda dvojbodka fyzika čiarka chémia čiarka biológia bodka Matematika a práca s informáciami: matematika, informatika.");
        Over(diktat.Oblasti.Count == 2 && diktat.Oblasti[0].Nazov == "Človek a príroda" && diktat.Oblasti[0].Predmety.SequenceEqual(new[] { "Fyzika", "Chémia", "Biológia" })
             && diktat.Oblasti[1].Predmety.Count == 2, $"oblasti a predmety z diktovania ({string.Join(" | ", diktat.Oblasti.Select(o => o.Nazov + ": " + string.Join(", ", o.Predmety)))})");

        var plan = StrukturaOsnov.Aktualna();
        foreach (var (pripona, uloz) in new (string, Action<UcebnyPlan, string>)[] { (".json", ImportStruktury.UlozJson), (".xml", ImportStruktury.UlozXml), (".html", ImportStruktury.UlozHtml) })
        {
            var cesta = Path.Combine(Databaza.PriecinokDokumentov, "samotest_oblasti" + pripona);
            uloz(plan, cesta);
            var spat = StrukturaOsnov.NacitajZoSuboru(cesta);
            Over(spat.Oblasti.Count == plan.Oblasti.Count && spat.PocetPredmetov == plan.PocetPredmetov,
                $"export a import oblastí a predmetov {pripona} ({spat.Oblasti.Count} oblastí, {spat.PocetPredmetov} predmetov)");
        }

        // charakteristiky zo ŠVP: aktualizácia nahradí len nezmenené ukážkové texty, tlačidlo v Osnovách všetky
        var dokumenty = new DokumentService();
        var oblastiSvp = new ObjektService().Oblasti().Where(o => SvpGymnazia.Oblast(o.Nazov) is not null).Take(2).ToList();
        if (oblastiSvp.Count == 2)
        {
            var ukazkova = TestovaciaSada.Nacitaj().Oblasti.First(o => ImportOsnov.Kluc(o.Nazov) == ImportOsnov.Kluc(oblastiSvp[0].Nazov));
            dokumenty.UlozBloky(oblastiSvp[0].Id, TestovaciaSada.UkazkovaCharakteristika(ukazkova), null);
            dokumenty.UlozBloky(oblastiSvp[1].Id, new List<Blok> { new Odsek(new[] { new Usek("Vlastná charakteristika školy.") }) }, null);
            SvpGymnazia.PrevezmiVsetky(lenUkazkove: true);
            var prva = SvpGymnazia.JePrevzata(dokumenty.Nacitaj(oblastiSvp[0].Id).Bloky, SvpGymnazia.Oblast(oblastiSvp[0].Nazov)!);
            var druha = DokumentOsnovy.Text(dokumenty.Nacitaj(oblastiSvp[1].Id).Bloky).Contains("Vlastná charakteristika", StringComparison.Ordinal);
            SvpGymnazia.PrevezmiVsetky(lenUkazkove: false);
            Over(prva && druha && SvpGymnazia.JePrevzata(dokumenty.Nacitaj(oblastiSvp[1].Id).Bloky, SvpGymnazia.Oblast(oblastiSvp[1].Nazov)!)
                 && dokumenty.Verzie(oblastiSvp[1].Id)[0].Dovod == SvpGymnazia.DovodPrevzatia,
                "charakteristiky zo ŠVP: aktualizácia nahradí len ukážkové texty, prevzatie všetkých archivuje vlastné");
        }

        Over(ImportStruktury.Dekoduj(System.Text.Encoding.GetEncoding(1250).GetBytes("<html><head><meta charset=\"windows-1250\"></head><body>Človek a príroda</body></html>"), null)
             .Contains("Človek a príroda", StringComparison.Ordinal), "webová stránka v kódovaní windows-1250");
        Krok("import oblastí a predmetov v poriadku");
    }

    /// <summary>Údaje o organizácii: adresa po častiach, prevod staršej adresy, údaje v zostave.</summary>
    private static void TestUdajovOrganizacie()
    {
        var povodne = UdajeOrganizacie.Nacitaj();
        Over(UdajeOrganizacie.Prazdne with { Psc = "999 01" } is var x && x.ZAdresy("Školská 3, Vzorovo").Psc == ""
             && UdajeOrganizacie.SkontrolujPsc("97401", "Slovensko") is null && UdajeOrganizacie.SkontrolujPsc("1234", "Slovensko") is not null
             && UdajeOrganizacie.SkontrolujPsc("SW1A 1AA", "Spojené kráľovstvo") is null
             && UdajeOrganizacie.SkontrolujDic("2021145766") is null && UdajeOrganizacie.SkontrolujDic("123") is not null,
            "adresa bez PSČ, kontrola PSČ a DIČ");
        var stara = UdajeOrganizacie.Prazdne.ZAdresy("Hlavná 5, 811 01 Bratislava");
        Over(stara is { Ulica: "Hlavná 5", Psc: "811 01", Mesto: "Bratislava" } && stara.AdresaRiadok == "Hlavná 5, 811 01 Bratislava", "rozdelenie adresy na časti");
        var org = new UdajeOrganizacie("Spojená škola Vzorová", "Gymnázium", "Vzorová 1", "Vzorovo", "999 99", "Česko", "12345678", "2020999999", "skola@vzor.example", "+421 900 111 222");
        Over(org.AdresaRiadok == "Vzorová 1, 999 99 Vzorovo, Česko" && org.IcoDicRiadok.Contains("IČO: 12345678", StringComparison.Ordinal)
             && UdajeOrganizacie.SkontrolujIco("123") is not null && UdajeOrganizacie.SkontrolujEmail("zly") is not null, "údaje o organizácii");
        org.Uloz();
        var titul = TitulnaStrana.Nacitaj();
        titul.IcoDicZobrazit = true;
        titul.KontaktZobrazit = true;
        titul.SkolaVPateZobrazit = true;
        titul.UlozUdajeOSkole();
        var znova = TitulnaStrana.Nacitaj();
        Over(znova.ZlozkaNaStrane == "Gymnázium" && znova.RiadkyPodAdresou().Count() == 2 && znova.SkolaVHlavicke == "Spojená škola Vzorová – Gymnázium"
             && znova.TextVPate == "Spojená škola Vzorová – Gymnázium" && znova.Adresa == org.AdresaRiadok, "údaje o škole na úvodnej strane, v hlavičke a päte");
        var celok = ZostavaOsnov.Zostav(new VolbyZostavy { Obsah = false });
        var pdf = Path.Combine(Databaza.PriecinokDokumentov, "samotest_udaje_organizacie.pdf");
        PdfVystup.UlozPdf(celok, pdf);
        var docx = Path.Combine(Databaza.PriecinokDokumentov, "samotest_udaje_organizacie.docx");
        DocxVystup.Uloz(celok, docx);
        using (var word = WordprocessingDocument.Open(docx, false))
        {
            var text = word.MainDocumentPart!.Document.Body!.InnerText;
            var pata = string.Concat(word.MainDocumentPart.FooterParts.Select(f => f.Footer.InnerText));
            Over(text.Contains("IČO: 12345678", StringComparison.Ordinal) && text.Contains("Gymnázium", StringComparison.Ordinal)
                 && pata.Contains("Spojená škola Vzorová", StringComparison.Ordinal), "údaje o organizácii v dokumente Word (úvodná strana a päta)");
        }

        var vykreslenie = NahladDokumentu.Priprav(PdfVystup.Vytvor(celok));
        UlozObrazokStrany(vykreslenie, 1, Path.Combine(Databaza.PriecinokDokumentov, "organizacia_uvodna_strana.png"));
        UlozObrazokStrany(vykreslenie, 2, Path.Combine(Databaza.PriecinokDokumentov, "organizacia_hlavicka_pata.png"));

        // okno úvodnej strany: uloženie zachová údaje o škole v zostave (IČO, kontakt, päta) a adresa sa uloží aj po častiach
        var titulPred = new Dictionary<string, string?>();
        using (var db = Databaza.Otvor())
        using (var prikaz = Databaza.Prikaz(db, "SELECT kluc, hodnota FROM nastavenie WHERE kluc LIKE 'titul.%'"))
        using (var citac = prikaz.ExecuteReader())
        {
            while (citac.Read())
            {
                titulPred[citac.GetString(0)] = citac.IsDBNull(1) ? null : citac.GetString(1);
            }
        }

        var patuPred = TitulnaStrana.DataPaty();
        var adresaPred = Databaza.Citaj(UdajeSkVP.KlucAdresa);
        var oknoTitulu = new UvodnaStranaWindow();
        oknoTitulu.Show();
        Pumpuj();
        Snimka(oknoTitulu, "11c_uvodna_strana_udaje_skoly");
        oknoTitulu.Adresa.Text = "Nová 7, 821 08 Bratislava";
        oknoTitulu.UlozitAZavriet();
        var poOkne = TitulnaStrana.Nacitaj();
        Over(poOkne.IcoDicZobrazit && poOkne.KontaktZobrazit && poOkne.SkolaVPateZobrazit && poOkne.ZlozkaNaStrane == "Gymnázium"
             && UdajeOrganizacie.Nacitaj() is { Ulica: "Nová 7", Psc: "821 08", Mesto: "Bratislava", Ico: "12345678" },
            "okno úvodnej strany zachová údaje o škole v zostave a uloží adresu po častiach");
        // texty úvodnej strany späť (okno ich uložilo – napr. nadpis by inak prekryl názov programu testovacej sady)
        using (var db = Databaza.Otvor())
        {
            Databaza.Vykonaj(db, "DELETE FROM nastavenie WHERE kluc LIKE 'titul.%'");
            foreach (var (kluc, hodnota) in titulPred)
            {
                Databaza.Vykonaj(db, "INSERT INTO nastavenie(kluc, hodnota) VALUES($k, $h)", ("$k", kluc), ("$h", hodnota));
            }

            if (patuPred is null)
            {
                Databaza.Vykonaj(db, "DELETE FROM objekt WHERE typ = $t", ("$t", TitulnaStrana.TypPaty));
            }
        }

        Databaza.Zapis(UdajeSkVP.KlucAdresa, adresaPred);
        var bloky = StrukturaOsnov.Bloky(StrukturaOsnov.Aktualna());
        Over(DokumentOsnovy.Text(bloky).Contains("IČO: 12345678", StringComparison.Ordinal), "údaje o škole aj v exporte oblastí a predmetov");

        // export a import nastavení programu po skupinách
        var nastavenia = Path.Combine(Databaza.PriecinokDokumentov, "samotest_nastavenia_skupiny.json");
        NastaveniaProgramu.Export(nastavenia, new[] { "údaje o organizácii" });
        var len = NastaveniaProgramu.Nacitaj(nastavenia);
        Over(len.Nastavenia.Keys.All(k => NastaveniaProgramu.Skupina(k) == "údaje o organizácii") && len.Logo is null
             && !NastaveniaProgramu.Vytvor().Nastavenia.ContainsKey(DiagnostikaService.KlucPoslednaKontrola)
             && NastaveniaProgramu.Prehlad(NastaveniaProgramu.Vytvor())[0] == "údaje o organizácii",
            "export nastavení programu po skupinách (bez stavu tohto počítača)");
        Databaza.Zapis(UdajeOrganizacie.KlucTelefon, "0");
        Databaza.Zapis(UdajeSkVP.KlucNazov, "Iný program");
        NastaveniaProgramu.Import(NastaveniaProgramu.Nacitaj(nastavenia), new[] { "údaje o organizácii" });
        Over(Databaza.Citaj(UdajeOrganizacie.KlucTelefon) == "+421 900 111 222" && Databaza.Citaj(UdajeSkVP.KlucNazov) == "Iný program",
            "import len vybraných skupín nastavení");
        Databaza.Zapis(UdajeSkVP.KlucNazov, null);

        // späť pôvodné údaje a predvolené zobrazenie
        titul.IcoDicZobrazit = false;
        titul.KontaktZobrazit = false;
        titul.SkolaVPateZobrazit = false;
        titul.UlozUdajeOSkole();
        UdajeOrganizacie.Vymaz();
        povodne.Uloz();
        Krok("údaje o organizácii v poriadku");
    }

    /// <summary>Uloženie zostavy do PDF, PDF/A-2b, PDF/A-3b, PDF/X-4, PNG, JPEG, TIFF a zašifrovaného PDF.</summary>
    private static void TestVystupov()
    {
        var export = Databaza.PriecinokDokumentov;
        Directory.CreateDirectory(export);
        var idOblasti = new ObjektService().Oblasti()[4].Id;
        var volby = new VolbyZostavy { Oblasti = new[] { idOblasti } };
        var dokument = ZostavaOsnov.Zostav(volby);
        MigraDoc.DocumentObjectModel.Document Tvorba() => PdfVystup.Vytvor(ZostavaOsnov.Zostav(volby));
        var stran = NahladDokumentu.PocetStran(NahladDokumentu.Priprav(Tvorba()));

        string Text(byte[] data) => System.Text.Encoding.Latin1.GetString(data);
        var pdf = SuboryZostavy.Vytvor(Tvorba(), "skuska", new VolbyPdf());
        Over(pdf.Count == 1 && Text(pdf[0].Data).StartsWith("%PDF", StringComparison.Ordinal), "PDF");
        foreach (var (format, znak, popis) in new[]
                 {
                     (FormatVystupu.PdfA2b, "pdfaid:part>2", "PDF/A-2b"),
                     (FormatVystupu.PdfA3b, "/EmbeddedFiles", "PDF/A-3b s dokumentom Word"),
                     (FormatVystupu.PdfX4, "GTS_PDFX", "PDF/X-4"),
                 })
        {
            try
            {
                var subor = SuboryZostavy.Vytvor(Tvorba(), "skuska", new VolbyPdf { Format = format }, () => TlacViewModel.PrilohaWord(dokument));
                File.WriteAllBytes(Path.Combine(export, $"samotest_{format}.pdf"), subor[0].Data);
                Over(Text(subor[0].Data).Contains(znak, StringComparison.Ordinal), popis);
            }
            catch (SkvpException ex)
            {
                Krok($"{popis}: {ex.Message} (preskočené)");
            }
        }

        var png = SuboryZostavy.Vytvor(Tvorba(), "skuska", new VolbyPdf { Format = FormatVystupu.Png });
        Over(png.Count == stran && png.All(s => s.Mime == "image/png"), $"obrázky PNG ({png.Count})");
        var jpeg = SuboryZostavy.Vytvor(Tvorba(), "skuska", new VolbyPdf { Format = FormatVystupu.Jpeg });
        Over(jpeg.Count == stran && jpeg[0].Nazov.EndsWith(".jpg", StringComparison.Ordinal) && jpeg[0].Data[0] == 0xFF && jpeg[0].Data[1] == 0xD8,
            $"obrázky JPEG ({jpeg.Count})");
        File.WriteAllBytes(Path.Combine(export, "samotest_strana.jpg"), jpeg[0].Data);
        var tiff = SuboryZostavy.Vytvor(Tvorba(), "skuska", new VolbyPdf { Format = FormatVystupu.Tiff });
        Over(tiff.Count == 1 && tiff[0].Nazov.EndsWith(".tif", StringComparison.Ordinal), "viacstranový TIFF");
        var dve = SuboryZostavy.Vytvor(Tvorba(), "skuska", new VolbyPdf { Rozvrh = new VolbyTlace { Rozmiestnenie = Rozmiestnenie.Dve, OdtieneSivej = true } });
        File.WriteAllBytes(Path.Combine(export, "samotest_2_na_list_siva.pdf"), dve[0].Data);
        VolbyPdf.Heslo = "skvp";
        var sifrovane = SuboryZostavy.Vytvor(Tvorba(), "skuska", new VolbyPdf { PouzitHeslo = true, ZakazTlace = true });
        VolbyPdf.Heslo = null;
        Over(Text(sifrovane[0].Data).Contains("/Encrypt", StringComparison.Ordinal), "zašifrované PDF s heslom");
        Krok("výstupy tlače v poriadku");
    }

    /// <summary>Po vymazaní testovacej sady nesmú zostať údaje vymyslenej školy.</summary>
    private static void OverBezSkolySady(string popis, Dictionary<string, string?> predSadou)
    {
        var t = TitulnaStrana.Nacitaj();
        var zvysky = new List<string>();
        foreach (var (kluc, hodnota) in TestovaciaSada.NastaveniaSady)
        {
            // nastavenie musí mať hodnotu spred nahratia sady (ak bola rovnaká ako v sade, zostane)
            if (Databaza.Citaj(kluc) is var teraz && teraz == hodnota && teraz != predSadou.GetValueOrDefault(kluc))
            {
                zvysky.Add(kluc);
            }
        }

        if (NastaveniaSkoly.CestaLoga() is not null)
        {
            zvysky.Add("logo");
        }

        if (Databaza.Citaj(TestovaciaSada.KlucUdajeSkoly) is not null)
        {
            zvysky.Add(TestovaciaSada.KlucUdajeSkoly);
        }

        if (t.Skola.Length > 0 || t.Adresa.Length > 0 || t.Nadpis != UdajeSkVP.PredvolenyNazov || t.PataBloky.OfType<Tabulka>().Any()
            || t.TextHlavickyVysledny != "Školský vzdelávací program – Učebné osnovy")
        {
            zvysky.Add("úvodná strana");
        }

        Over(zvysky.Count == 0, $"{popis}: bez zvyškov vymyslenej školy ({string.Join(", ", zvysky)})");
    }

    /// <summary>Skúšobné logo – purpurový kruh s modrým štvorcom (farba sa dá nájsť na vykreslenej strane).</summary>
    private static void VytvorSkusobneLogo(string cesta, int sirka, int vyska)
    {
        var kresba = new DrawingVisual();
        using (var kontext = kresba.RenderOpen())
        {
            var purpurova = new SolidColorBrush(Color.FromRgb(200, 20, 120));
            kontext.DrawEllipse(purpurova, null, new Point(vyska / 2.0, vyska / 2.0), vyska / 2.0 - 4, vyska / 2.0 - 4);
            kontext.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 60, 200)), null, new Rect(vyska, vyska * 0.2, sirka - vyska - 4, vyska * 0.6));
        }

        var obrazok = new System.Windows.Media.Imaging.RenderTargetBitmap(sirka, vyska, 96, 96, PixelFormats.Pbgra32);
        obrazok.Render(kresba);
        var kodovac = new System.Windows.Media.Imaging.PngBitmapEncoder();
        kodovac.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(obrazok));
        using var subor = File.Create(cesta);
        kodovac.Save(subor);
    }

    /// <summary>Počet purpurových bodov skúšobného loga vo vybranej časti vykreslenej strany.</summary>
    private static int PixelyLoga(MigraDoc.Rendering.DocumentRenderer vykreslenie, int strana, Int32Rect oblast)
    {
        var kresba = NahladDokumentu.Strana(vykreslenie, strana, out var velkost);
        var obrazok = new System.Windows.Media.Imaging.RenderTargetBitmap((int)velkost.Width, (int)velkost.Height, 96, 96, PixelFormats.Pbgra32);
        obrazok.Render(kresba);
        var sirka = Math.Min(oblast.Width, obrazok.PixelWidth - oblast.X);
        var vyska = Math.Min(oblast.Height, obrazok.PixelHeight - oblast.Y);
        var body = new byte[sirka * vyska * 4];
        obrazok.CopyPixels(new Int32Rect(oblast.X, oblast.Y, sirka, vyska), body, sirka * 4, 0);
        var pocet = 0;
        for (var i = 0; i < body.Length; i += 4)
        {
            int b = body[i], g = body[i + 1], r = body[i + 2];
            if (r > 160 && g < 90 && b > 70 && b < 180)
            {
                pocet++;
            }
        }

        return pocet;
    }

    /// <summary>Text a obrázky hlavičky dokumentu Word, počet obrázkov v tele (logo úvodnej strany) a počet hlavičiek.</summary>
    private static (string Text, int ObrazkyHlavicky, int ObrazkyTela, int Hlaviciek) HlavickaWordu(string cesta)
    {
        using var docx = WordprocessingDocument.Open(cesta, false);
        var hlavna = docx.MainDocumentPart!;
        var hlavicky = hlavna.HeaderParts.ToList();
        var text = string.Join(" | ", hlavicky.SelectMany(h => h.Header!.Descendants<W.Paragraph>()).Select(p => p.InnerText).Where(x => x.Length > 0));
        return (text, hlavicky.Sum(h => h.ImageParts.Count()), hlavna.ImageParts.Count(), hlavicky.Count);
    }

    private static void TestZaloh()
    {
        var zalohy = new ZalohaService();
        int Cislo(string cesta) => int.Parse(Path.GetFileName(cesta).Substring("SkVP_zaloha_".Length, 4), System.Globalization.CultureInfo.InvariantCulture);
        var prva = zalohy.Zalohuj();
        var druha = zalohy.Zalohuj();
        Over(Cislo(druha) == Cislo(prva) + 1 && Path.GetFileName(prva).StartsWith("SkVP_zaloha_0", StringComparison.Ordinal), "zálohy sú číslované za sebou");
        using var archiv = System.IO.Compression.ZipFile.OpenRead(druha);
        Over(archiv.GetEntry("skvp.db") is { Length: > 10_000 }, "záloha obsahuje databázu");
        Over(zalohy.AutomatickaAkTreba() is not null && zalohy.AutomatickaAkTreba() is null, "automatická záloha raz za deň");
    }

    /// <summary>Sekcia Novinky: overené údaje, oficiálne zdroje, termíny, RSS, krížové potvrdenie, overenie na stránkach, kalendár.</summary>
    private static void TestNoviniek()
    {
        var data = NovinkyService.Zabudovane();
        var chyby = NovinkyService.Skontroluj(data, vyhodit: false);
        Over(chyby.Count == 0, "údaje Noviniek sú platné" + (chyby.Count > 0 ? ": " + string.Join("; ", chyby) : ""));
        Over(data.Polozky.Count >= 60 && data.Kategorie.Count == 6 && data.Stranky.Count >= 8,
            $"Novinky: {data.Polozky.Count} overených informácií v 6 témach, {data.Stranky.Count} odporúčaných stránok");
        foreach (var tema in new[] { "osnovy", "tematicke", "ciele", "maturita", "prijimacky", "skolskyrok" })
        {
            Over(data.Polozky.Count(p => p.Kategoria == tema) >= 3, $"Novinky: téma {tema} má aspoň 3 informácie");
        }

        Over(data.Polozky.SelectMany(p => p.Zdroje).All(z => NovinkyService.JeOficialna(z.Url)) && data.Stranky.All(s => NovinkyService.JeOficialna(s.Url)),
            "všetky zdroje Noviniek sú oficiálne stránky (žiadne médiá)");
        Over(NovinkyService.JeOficialna("https://www.minedu.sk/terminy-prazdnin/") && NovinkyService.JeOficialna("https://www2.nucem.sk/sk")
             && !NovinkyService.JeOficialna("https://www.sme.sk/minedu") && !NovinkyService.JeOficialna("https://minedu.sk.priklad.com/")
             && !NovinkyService.JeOficialna("ftp://minedu.sk/") && !NovinkyService.JeOficialna("javascript:alert(1)"),
            "zoznam oficiálnych domén odmieta médiá a podvrhnuté adresy");
        var viacZdrojov = data.Polozky.Count(p => p.Zdroje.Count >= 2);
        Krok($"Novinky: {viacZdrojov} z {data.Polozky.Count} informácií má aspoň dva oficiálne zdroje");

        PolozkaNoviniek P(string id) => data.Polozky.Single(p => p.Id == id);
        Over(P("ms-nahradny-april").TerminText == "6. – 9. 4. 2027" && P("pr-vianocne").TerminText == "23. 12. 2026 – 7. 1. 2027"
             && P("ms-zmena-eformy").TerminText == "do 13. 11. 2026" && P("ms-ec-mat").TerminText == "11. 3. 2027"
             && P("svp-dodatok-6").TerminText == "od 1. 9. 2026", "texty termínov (rozsah, lehota, účinnosť)");
        var den = new DateOnly(2026, 10, 23);
        Over(NovinkyService.Odpocet(P("pr-jesenne"), den) == "o 6 dní" && NovinkyService.Odpocet(P("ms-zber-udajov"), den) == "prebieha (do 31. 10.)"
             && NovinkyService.Odpocet(P("ms-zmena-eformy"), den) == "ešte 21 dní" && NovinkyService.Odpocet(P("sr-zaciatok"), den) == "uplynulo"
             && NovinkyService.Odpocet(P("pr-jesenne"), new DateOnly(2026, 10, 26)) == "o 3 dni",
            "odpočet termínov (o 6 dní, prebieha, ešte 21 dní, uplynulo)");
        var najblizsie = NovinkyService.NajblizsieTerminy(data, den, 5);
        Over(najblizsie.Count == 5 && najblizsie.Zip(najblizsie.Skip(1)).All(d => d.First.Zaciatok <= d.Second.Zaciatok)
             && najblizsie.All(p => p.Koniec >= den), "najbližšie termíny sú nadchádzajúce a zoradené");

        // RSS a krížové potvrdenie
        const string rss = """
            <?xml version="1.0" encoding="UTF-8"?>
            <rss version="2.0"><channel><title>Test</title>
            <item><title>Maturita 2027: elektronická maturita z matematiky a slovenčiny</title><link>https://www.minedu.sk/maturita-2027/</link>
              <description><![CDATA[<p>Elektronická maturita z matematiky a slovenského jazyka sa rozširuje na ďalšie školy.</p>]]></description>
              <pubDate>Tue, 20 Oct 2026 12:00:00 +0200</pubDate></item>
            <item><title>Maturita v novinách</title><link>https://www.noviny.sk/maturita</link><description>Médiá</description></item>
            <item><title>Športový deň ministerstva</title><link>https://www.minedu.sk/sport/</link><description>Beh</description></item>
            </channel></rss>
            """;
        const string atom = """
            <feed xmlns="http://www.w3.org/2005/Atom"><title>NIVaM</title>
            <entry><title>Elektronická maturita 2027 – generálna skúška z matematiky a slovenského jazyka</title>
              <link href="https://nivam.sk/maturita-2027/" /><summary>Informácie k elektronickej maturite z matematiky.</summary>
              <updated>2026-10-22T10:00:00Z</updated></entry>
            </feed>
            """;
        var spravy = NovinkyService.PrecitajRss(rss, "Ministerstvo").Concat(NovinkyService.PrecitajRss(atom, "NIVaM")).ToList();
        Over(spravy.Count == 3 && spravy.All(s => NovinkyService.JeOficialna(s.Odkaz)), "RSS a Atom: správy iba s odkazom na oficiálnu stránku");
        Over(spravy[0].Kategoria == "maturita" && spravy[1].Kategoria is null && spravy[2].Kategoria == "maturita"
             && spravy[0].Popis.StartsWith("Elektronická maturita", StringComparison.Ordinal) && spravy[0].Datum?.Day == 20,
            "správy sa zaradia do tém (nerelevantné bez témy), popis bez HTML, dátum");
        var porovnane = NovinkyService.Porovnaj(spravy.Where(s => s.Kategoria is not null).ToList());
        Over(porovnane.All(s => s.PotvrdeneInde.Count == 1), "krížové potvrdenie: o téme píšu dva oficiálne zdroje");
        Over(NovinkyService.TemaSpravy("Termíny prijímacích skúšok na stredné školy", "") == "prijimacky"
             && NovinkyService.TemaSpravy("Školské prázdniny v školskom roku 2027/2028", "") == "skolskyrok"
             && NovinkyService.TemaSpravy("Ministerstvo schválilo dodatok k štátnemu vzdelávaciemu programu", "") == "osnovy"
             && NovinkyService.TemaSpravy("Spolupráca s Francúzskom v jadrovom odvetví", "") is null
             && NovinkyService.TemaSpravy("Skvelý štart na Chemickú olympiádu", "Súťaž pre žiakov gymnázií a maturantov") is null
             && NovinkyService.TemaSpravy("Dvojzmenné vyučovanie sa končí", "Nová škola v školskom roku 2026/2027") is null
             && NovinkyService.TemaSpravy("Matematika je základná zručnosť", "Povinná maturita z matematiky od roku 2031") == "maturita",
            "témy správ podľa kľúčových slov (súťaže a výstavba škôl nie sú vzdelávací obsah)");

        // overenie údaja na zdrojovej stránke
        var zdroj = new ZdrojNovinky { Url = "https://www2.nucem.sk/x", Overit = new() { "9. marec 2027 (utorok)" } };
        var stranka = NovinkyService.TextZHtml("<html><script>var x='9. marec 2027';</script><p>9.&nbsp;marec\n 2027 (utorok) – SJL</p></html>");
        Over(NovinkyService.Vyhodnot(zdroj, (true, stranka, "HTML")).Stav == StavOverenia.Potvrdene
             && NovinkyService.Vyhodnot(zdroj, (true, NovinkyService.TextZHtml("<p>10. marec 2027</p>"), "HTML")).Stav == StavOverenia.Nenajdene
             && NovinkyService.Vyhodnot(zdroj, (true, null, "PDF")).Stav == StavOverenia.Dostupne
             && NovinkyService.Vyhodnot(zdroj, (false, null, "chyba")).Stav == StavOverenia.Nedostupne,
            "overenie údaja na stránke (potvrdené, zmenené, PDF, nedostupné)");

        // stiahnuté údaje: novšie sa použijú, neoficiálny zdroj sa odmietne
        var novsie = NovinkyService.Precitaj(System.Text.Json.JsonSerializer.Serialize(data));
        novsie.Aktualizovane = "2099-01-01";
        Databaza.Zapis(NovinkyService.KlucData, System.Text.Json.JsonSerializer.Serialize(novsie));
        Over(NovinkyService.Nacitaj().Aktualizovane == "2099-01-01", "novšie stiahnuté údaje Noviniek sa použijú");
        novsie.Polozky[0].Zdroje[0].Url = "https://www.dennik.sk/clanok";
        Databaza.Zapis(NovinkyService.KlucData, System.Text.Json.JsonSerializer.Serialize(novsie));
        Over(NovinkyService.Nacitaj().Aktualizovane == data.Aktualizovane, "stiahnuté údaje s neoficiálnym zdrojom sa odmietnu");
        Databaza.Zapis(NovinkyService.KlucData, null);

        // kalendár
        var ics = NovinkyViewModel.Kalendar(data.Polozky.Where(p => p.Druh == NovinkyService.DruhTermin), data);
        Over(ics.StartsWith("BEGIN:VCALENDAR", StringComparison.Ordinal) && ics.Contains("DTSTART;VALUE=DATE:20270406") && ics.Contains("DTEND;VALUE=DATE:20270410")
             && Regex.Matches(ics, "BEGIN:VEVENT").Count == data.Polozky.Count(p => p.Druh == NovinkyService.DruhTermin),
            "termíny do kalendára (.ics, celodenné udalosti)");

        // obrazovka bez siete
        var model = new NovinkyViewModel(new MainViewModel()) { Dnes = den };
        model.Obnov();
        Over(model.Bloky.Count == 5 && model.Najblizsie.Count > 0 && model.Terminovnik.Count >= 5 && model.Temy.Count == 6
             && !model.Bloky.Any(b => b.Kategoria.Id == NovinkyService.TemaSkolskyRok) && !model.Najblizsie.Any(p => p.Polozka.Kategoria == NovinkyService.TemaSkolskyRok),
            "obrazovka Novinky: 5 blokov (prázdniny sú na dashboarde), najbližšie termíny, termínovník po mesiacoch, filtre tém");
        model.VybranaTema = "maturita";
        Over(model.Bloky.Count == 1 && model.Bloky[0].Kategoria.Id == "maturita", "filter témy");
        model.VybranaTema = null;
        model.Hladat = "ematurita";
        Over(model.Bloky.SelectMany(b => b.Polozky).Any(p => p.Polozka.Id == "ms-ematurita"), "hľadanie v Novinkách (bez diakritiky)");
        model.Hladat = "";

        // skutočné overenie na oficiálnych stránkach (informatívne – bez siete sa preskočí)
        try
        {
            var uloha = Task.Run(() => NovinkyService.OverAsync(data));
            if (uloha.Wait(TimeSpan.FromMinutes(4)))
            {
                var vysledky = uloha.Result.Polozky.SelectMany(p => p.Value.Select(v => (p.Key, v))).ToList();
                var riadky = vysledky.Select(x => $"{x.Key}\t{x.v.Stav}\t{x.v.Url}\t{x.v.Sprava}").ToList();
                File.WriteAllLines(Path.Combine(Databaza.PriecinokDokumentov, "novinky_overenie.txt"), riadky, Encoding.UTF8);
                Krok($"overenie Noviniek na stránkach: {vysledky.Count(x => x.v.Stav == StavOverenia.Potvrdene)} potvrdených, " +
                     $"{vysledky.Count(x => x.v.Stav == StavOverenia.Dostupne)} dostupných, {vysledky.Count(x => x.v.Stav == StavOverenia.Nenajdene)} nenájdených, " +
                     $"{vysledky.Count(x => x.v.Stav == StavOverenia.Nedostupne)} nedostupných");
                foreach (var x in vysledky.Where(x => x.v.Stav is StavOverenia.Nenajdene))
                {
                    Krok($"  na kontrolu: {x.Key} – {x.v.Url} – {x.v.Sprava}");
                }
            }
            else
            {
                Krok("overenie Noviniek na stránkach: časový limit (preskočené)");
            }

            var rssUloha = Task.Run(() => NovinkyService.StiahniSpravyAsync(data));
            if (rssUloha.Wait(TimeSpan.FromMinutes(1)))
            {
                var r = rssUloha.Result;
                Krok($"RSS ministerstva a NIVaM: {r.Spravy.Count} správ k vzdelávaciemu obsahu, {r.Nerelevantnych} iných, " +
                     $"{r.Spravy.Count(s => s.PotvrdeneInde.Count > 0)} krížovo potvrdených" + (r.Chyby.Count > 0 ? " · " + string.Join(", ", r.Chyby) : ""));
                foreach (var s in r.Spravy.Take(8))
                {
                    Krok($"  [{s.Kategoria}] {s.Datum:d.M.yyyy} {s.Zdroj}: {s.Titulok}");
                }
            }
        }
        catch (Exception ex)
        {
            Krok($"overenie Noviniek na stránkach: {ex.GetBaseException().Message} (bez siete – preskočené)");
        }

        Krok("Novinky v poriadku");
    }

    /// <summary>Dashboard: Kalendárium (meniny, sviatky), výroky slávnych osobností a počasie.</summary>
    private static void TestDashboarduDna()
    {
        var kalendar = Kalendarium.KalendariumDb.Spolocna;
        Over(kalendar.Cesta.StartsWith(Databaza.Priecinok, StringComparison.OrdinalIgnoreCase), "Kalendárium samotestu je v dočasnom priečinku");
        Over(kalendar.Meniny(new DateOnly(2026, 10, 23)).Contains("Alojzia") && kalendar.SviatkyDna(new DateOnly(2026, 1, 1)).Any(s => s.DenPracovnehoPokoja),
            "Kalendárium: meniny (23. 10. Alojzia) a sviatky (1. 1.)");

        // pastelové farby
        Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
        double Jas(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;
        Over(Pastel.Zjemni(C("#3B5BDB")) == C("#7B93DB") && Jas(Pastel.Zjemni(C("#DC2626"))) > 0.5 && Pastel.Zjemni(C("#808080")) == C("#808080")
             && Jas(Pastel.Text(C("#7B93DB"), false)) < 0.4 && Jas(Pastel.Text(C("#7B93DB"), true)) > 0.65
             && UcebnyPlanService.FarbaOblasti(0) == "#7B93DB",
            "pastelové farby: zjemnenie sýtych farieb, čitateľný text vo svetlom aj tmavom vzhľade, paleta oblastí");

        var citaty = CitatyService.Zbierka;
        var zakazani = new[] { "Danko", "Kiska", "Schwarzenegger", "Slota", "Čaputová", "Harabin", "Dalajláma", "Fico", "Pellegrini", "Matovič", "Orbán", "Putin", "Trump" };
        Over(citaty.Citaty.Count > 1900 && citaty.Zdroj == "citaty-slavnych.sk", $"databáza výrokov: {citaty.Citaty.Count} výrokov z citaty-slavnych.sk");
        Over(!citaty.Citaty.Any(c => zakazani.Any(z => c.Autor.Contains(z, StringComparison.OrdinalIgnoreCase))), "v databáze nie sú výroky žijúcich politikov");
        Over(citaty.Citaty.Where(c => c.Autor is "Josef Stalin" or "Vladimír Iljič Lenin" or "Gustáv Husák").All(c => c.Skryty),
            "výroky diktátorov sú predvolene skryté");
        Over(citaty.Citaty.Select(c => c.Id).Distinct().Count() == citaty.Citaty.Count, "výroky majú jedinečné čísla");
        var dnes = CitatyService.CitatDna(new DateOnly(2026, 10, 23));
        Over(dnes is { Skryty: false, JeKratky: true } && CitatyService.CitatDna(new DateOnly(2026, 10, 23))?.Id == dnes.Id
             && CitatyService.CitatDna(new DateOnly(2026, 10, 24))?.Id != dnes.Id, "výrok dňa: krátky, rovnaký celý deň, zajtra iný");
        CitatyService.Skry(dnes!.Id);
        Over(!CitatyService.NaDashboard().Any(c => c.Id == dnes.Id) && CitatyService.Nahodny(null) is not null, "skrytý výrok sa nezobrazí");
        CitatyService.Odkry(dnes.Id);
        Over(CitatyService.NaDashboard().Any(c => c.Id == dnes.Id), "skrytie výroku sa dá vrátiť");
        Over(dnes.AutorText.StartsWith("— ", StringComparison.Ordinal) && dnes.TextVUvodzovkach.StartsWith('„'), "výrok s autorom v úvodzovkách");

        // počasie (údaje vo formáte Open-Meteo)
        const string miesta = """{"results":[{"name":"Banská Bystrica","latitude":48.73946,"longitude":19.15349,"country_code":"SK","country":"Slovensko","admin1":"Banskobystrický kraj","admin2":"Okres Banská Bystrica"},{"name":"Bez súradníc"}]}""";
        var najdene = PocasieService.PrecitajMiesta(miesta);
        Over(najdene.Count == 1 && najdene[0].Popis == "Banská Bystrica, Okres Banská Bystrica, Banskobystrický kraj, Slovensko", "počasie: vyhľadanie miesta");
        var predpoved = PocasieService.PrecitajPredpoved(VzorPocasia(), najdene[0], DateTime.Now);
        Over(predpoved.TeplotaText == "12 °C" && predpoved.Popis == "Polooblačno" && predpoved.Dni.Count == 5 && predpoved.Dni[0].Den == "dnes"
             && predpoved.Podrobnosti.Contains("vietor 14 km/h"), "počasie: aktuálny stav a predpoveď na 5 dní");
        var sluzba = new PocasieService();
        sluzba.UlozMiesto(najdene[0]);
        Databaza.Zapis(PocasieService.KlucData, System.Text.Json.JsonSerializer.Serialize(predpoved));
        Over(sluzba.ZVyrovnavacejPamate()?.TeplotaText == "12 °C", "počasie z vyrovnávacej pamäte (bez siete)");

        try
        {
            var uloha = Task.Run(async () =>
            {
                var m = (await sluzba.HladajAsync("Banská Bystrica")).First(x => x.KodKrajiny == "SK");
                return await new PocasieService().StiahniAsync(m);
            });
            if (uloha.Wait(TimeSpan.FromSeconds(40)))
            {
                Krok($"počasie zo siete: {uloha.Result.Miesto.Nazov} {uloha.Result.TeplotaText}, {uloha.Result.Popis}");
            }
        }
        catch (Exception ex)
        {
            Krok($"počasie zo siete: {ex.GetBaseException().Message} (preskočené)");
        }

        // na snímky dashboardu vzorové počasie
        sluzba.UlozMiesto(najdene[0]);
        Databaza.Zapis(PocasieService.KlucData, System.Text.Json.JsonSerializer.Serialize(predpoved with { Stiahnute = DateTime.Now }));

        var dashboard = new DashboardViewModel(new MainViewModel());
        dashboard.AktualizujDen(new DateOnly(2026, 10, 29));
        Over(dashboard.MeninyDnes.Length > 0 && dashboard.StavSkolskehoRoka.StartsWith("Dnes sú jesenné prázdniny", StringComparison.Ordinal)
             && dashboard.NajblizsiTermin.Length > 0, "dashboard: meniny, prázdniny v školskom roku, najbližší termín z Noviniek");
        dashboard.AktualizujDen(new DateOnly(2026, 11, 17));
        Over(dashboard.JeDnesVolno || dashboard.Sviatky.Count > 0, "dashboard: sviatok 17. 11.");
        dashboard.AktualizujDen();
        Over(dashboard.JePocasie && dashboard.Citat is not null, "dashboard: počasie a výrok dňa");

        // tabuľka prázdnin
        dashboard.DnesPrazdnin = new DateOnly(2026, 10, 23);
        dashboard.VybranyKrajPrazdnin = NovinkyViewModel.VsetkyKraje;
        dashboard.VybranyRok = "2026/2027";
        dashboard.ZostavPrazdniny();
        var riadky = dashboard.RiadkyPrazdnin;
        Over(dashboard.SkolskeRoky.SequenceEqual(new[] { "2026/2027", "2027/2028" }) && riadky.Count == 8
             && riadky[0].Nazov == "Jesenné" && riadky[0].Posledny == "28. 10. 2026 (st)" && riadky[0].Nastup == "2. 11. 2026 (po)"
             && riadky[0].Termin == "29. – 30. 10. 2026" && riadky[0].JeNajblizsi && riadky.Count(r => r.Nazov == "Jarné") == 3
             && riadky[^1].Nazov == "Letné" && dashboard.ObdobieVyucovania.Contains("1. 9. 2026"),
            "dashboard: tabuľka prázdnin 2026/2027 (posledný deň, termín, nástup, najbližšie prázdniny)");
        dashboard.VybranyKrajPrazdnin = "KE, PO";
        Over(dashboard.RiadkyPrazdnin.Count == 6 && dashboard.RiadkyPrazdnin.Single(r => r.Nazov == "Jarné").Kraje == "KE, PO",
            "dashboard: jarné prázdniny podľa kraja");
        dashboard.VybranyRok = "2027/2028";
        Over(dashboard.RiadkyPrazdnin.Count == 6 && dashboard.RiadkyPrazdnin[0].Termin == "28. – 29. 10. 2027", "dashboard: prázdniny 2027/2028");
        dashboard.VybranyKrajPrazdnin = NovinkyViewModel.VsetkyKraje;
        Krok("dashboard – dátum, meniny, sviatky, počasie a výroky v poriadku");
    }

    private static string VzorPocasia()
    {
        var json = """
            {"current":{"temperature_2m":12.3,"apparent_temperature":10.1,"relative_humidity_2m":71,"weather_code":2,"wind_speed_10m":14.2,"precipitation":0,"is_day":1},
             "daily":{"time":["D0","D1","D2","D3","D4"],"weather_code":[2,61,3,0,80],"temperature_2m_max":[14,11,9,13,12],
              "temperature_2m_min":[6,5,3,2,4],"precipitation_probability_max":[10,80,30,0,55]}}
            """;
        for (var i = 0; i < 5; i++)
        {
            json = json.Replace($"D{i}", DateTime.Today.AddDays(i).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }

        return json;
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
        SnimkyStranky(okno, "01a_dashboard", 3);

        // sekcia Novinky – všetky záložky
        model.OtvorNovinky();
        Pumpuj();
        Over(model.Stranka is NovinkyViewModel, "sekcia Novinky sa otvorí z menu");
        Snimka(okno, "01b_novinky");
        var zalozky = Najdi<System.Windows.Controls.TabControl>(okno.Obsah);
        Over(zalozky is { Items.Count: 4 }, "Novinky: 4 záložky (témy, termínovník, správy, odporúčané stránky)");
        string[] nazvyZaloziek = { "01c_novinky_temy", "01d_novinky_terminovnik", "01e_novinky_spravy", "01f_novinky_stranky" };
        for (var i = 0; i < 4; i++)
        {
            zalozky!.SelectedIndex = i;
            Pumpuj();
            SnimkyStranky(okno, nazvyZaloziek[i], i == 0 ? 8 : 4);
        }

        model.OtvorDashboard();
        Pumpuj();

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
        var uvodnaStrana = new UvodnaStranaWindow { Owner = okno };
        uvodnaStrana.Show();
        Pumpuj();
        Snimka(uvodnaStrana, "11b_okno_uvodna_strana");
        uvodnaStrana.Close();
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

        // okno tlače (ako v EviTA) – vlastný náhľad listu a voľby
        var vykreslenieOblasti = NahladDokumentu.Priprav(PdfVystup.Vytvor(ZostavaOsnov.Zostav(tlacOblasti.Volby())));
        var oknoTlace = new TlacWindow(vykreslenieOblasti, false, "samotest", RezimOknaTlace.Tlac,
            () => PdfVystup.Vytvor(ZostavaOsnov.Zostav(tlacOblasti.Volby())), null) { Owner = okno };
        oknoTlace.Show();
        Pumpuj();
        var stranOblasti = NahladDokumentu.PocetStran(vykreslenieOblasti);
        Over(oknoTlace.PocetListov == stranOblasti && oknoTlace.ObrazokListu(0) is not null, $"náhľad v okne tlače ukazuje dokument ({oknoTlace.PocetListov} listov)");
        Snimka(oknoTlace, "13b_okno_tlace");
        oknoTlace.NastavVolby(new VolbyTlace { Rozmiestnenie = Rozmiestnenie.Dve, Ramcek = true });
        Pumpuj();
        Over(oknoTlace.PocetListov == (stranOblasti + 1) / 2, "2 strany na list");
        Snimka(oknoTlace, "13c_okno_tlace_2_na_list");
        oknoTlace.NastavVolby(new VolbyTlace { Rozmiestnenie = Rozmiestnenie.Brozura, OdtieneSivej = true });
        Pumpuj();
        Over(oknoTlace.PocetListov == (stranOblasti + 3) / 4 * 2, "brožúra");
        Snimka(oknoTlace, "13d_okno_tlace_brozura");
        oknoTlace.NastavVolby(new VolbyTlace());
        oknoTlace.Close();
        var oknoPdf = new TlacWindow(vykreslenieOblasti, false, "samotest", RezimOknaTlace.Pdf,
            () => PdfVystup.Vytvor(ZostavaOsnov.Zostav(tlacOblasti.Volby())), "samotest.pdf") { Owner = okno };
        oknoPdf.Show();
        Pumpuj();
        Snimka(oknoPdf, "13e_okno_pdf");
        oknoPdf.Close();

        // Import a export, Servis
        model.OtvorImportExport();
        Pumpuj();
        Snimka(okno, "13f_import_export");
        var exportOkno = new ExportOsnovWindow(new ExportOsnovViewModel(model) { Len = true }) { Owner = okno };
        exportOkno.Show();
        Pumpuj();
        Snimka(exportOkno, "13g_okno_export");
        exportOkno.Close();
        var importModel = new ImportOsnovViewModel(model);
        var vzor = Path.Combine(Databaza.PriecinokDokumentov, "05 Biológia 2026.docx");
        File.WriteAllBytes(vzor, DocxVystup.Osnova(new List<Blok> { new Odsek(new[] { new Usek("Osnova biológie.") }) }));
        importModel.PridajSubory(new[] { vzor, Path.Combine(Databaza.PriecinokDokumentov, "Astronómia.docx") });
        var importOkno = new ImportOsnovWindow(importModel) { Owner = okno };
        importOkno.Show();
        Pumpuj();
        Snimka(importOkno, "13h_okno_import");
        importOkno.Close();
        var archivOkno = new ArchivOsnovyWindow(sjl.Id, sjl.Cislo, sjl.Nazov) { Owner = okno };
        archivOkno.Show();
        Pumpuj();
        Over(archivOkno.PocetVerzii > 0, "archív osnovy s verziami");
        Snimka(archivOkno, "13i_okno_archiv");
        archivOkno.Close();
        model.OtvorServis();
        Pumpuj();
        Snimka(okno, "13j_servis");
        model.OtvorArchiv();
        Pumpuj();
        Over(model.Stranka is ArchivViewModel { NemaArchivy: true } && model.JeArchiv, "sekcia Archív bez archivovaného roka");
        Snimka(okno, "13k_archiv_prazdny");
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
        model.OtvorNovinky();
        Pumpuj();
        Snimka(okno, "15b_tmavy_novinky");
        model.OtvorPredmet(sjl);
        Pumpuj();
        Snimka(okno, "16_tmavy_predmet");
        model.OtvorOblast(model.Oblasti[2]);
        Pumpuj();
        Snimka(okno, "17_tmavy_oblast");
        App.NastavTemu(NastaveniaSkoly.TemaSvetla);
        Pumpuj();

        // testovacia sada učebných osnov
        var sada = TestovaciaSada.Nacitaj();
        Over(sada.Predmety.Count == 43 && sada.Oblasti.Count == 7, "testovacia sada: 43 osnov a 7 charakteristík oblastí");
        var osnovaSjl = TestovaciaSada.Osnova(sada.Predmety.First(p => p.Nazov == "Slovenský jazyk a literatúra"));
        var dataSjl = DocxVystup.Osnova(osnovaSjl);
        OverDocx(dataSjl, "testovacia osnova SJL");
        PorovnajBloky(osnovaSjl, DocxCitac.Nacitaj(new MemoryStream(dataSjl)).Bloky, "testovacia osnova Word → program");
        var predSadou = TestovaciaSada.NastaveniaSady.ToDictionary(n => n.Kluc, n => Databaza.Citaj(n.Kluc));
        model.NahrajTestovaciuSadu();
        Pumpuj();
        Over(model.JeTestovaciaSada && TestovaciaSada.JeNainstalovana, "testovacia sada je nahratá (upozornenie v programe)");
        Over(model.Oblasti.SelectMany(o => o.Predmety).Count(p => p.Stav != StavOsnovy.Prazdna) == 43, "všetkých 43 predmetov má osnovu");
        Over(model.Oblasti.SelectMany(o => o.Predmety).Count(p => p.Stav == StavOsnovy.Hotova) >= 10, "testovacia sada obsahuje hotové osnovy");
        Over(new ObjektService().Suvislosti(model.Oblasti[2].Predmety.First(p => p.Nazov == "Seminár z biológie").Id).Count >= 2, "testovacia sada obsahuje súvislosti");
        var titulSady = TitulnaStrana.Nacitaj();
        Over(titulSady.Skola == TestovaciaSada.SkolaSady && titulSady.Adresa == TestovaciaSada.AdresaSady
             && titulSady.Nadpis == TestovaciaSada.ProgramSady && titulSady.TextHlavickyVysledny == TestovaciaSada.HlavickaSady
             && titulSady.RiadkyPodNadpisom().Count() >= 4 && titulSady.PataBloky.OfType<Tabulka>().Count() == 1,
            "testovacia sada: vymyslená škola, názov programu, texty úvodnej strany a hlavičky");
        Over(NastaveniaSkoly.Rozmer(NastaveniaSkoly.CestaLoga()) == (480, 480), "testovacia sada: logo školy");
        Snimka(okno, "19_sada_dashboard");
        model.OtvorOblast(model.Oblasti[2]);
        Pumpuj();
        Snimka(okno, "20_sada_oblast");
        model.OtvorPredmet(model.Oblasti[1].Predmety[0]);
        Pumpuj();
        Snimka(okno, "21_sada_matematika");
        model.OtvorPredmet(model.Oblasti[2].Predmety.First(p => p.Nazov == "Seminár z biológie"));
        Pumpuj();
        Snimka(okno, "22_sada_seminar");

        // oprava názvu predmetu priamo v riadku – zmení sa aj v texte osnovy
        var nemcina = model.Oblasti[0].Predmety.First(p => p.Nazov == "Konverzácia v nemeckom jazyku");
        var dokumentyUi = new DokumentService();
        var osnovaNj = dokumentyUi.Nacitaj(nemcina.Id).Bloky;
        dokumentyUi.UlozBloky(nemcina.Id, osnovaNj.Prepend(new Odsek(new[] { new Usek("Predmet Konverzácia v nemeckom jazyku sa vyučuje v 3. a 4. ročníku.") })).ToList(), null);
        model.OtvorOblast(model.Oblasti[0]);
        Pumpuj();
        var oblastJazyk = (OblastViewModel)model.Stranka!;
        oblastJazyk.ZacniPremenovanieCommand.Execute(nemcina);
        nemcina.NovyNazov = "Konverzácia v nemeckom jazyku (B1)";
        Pumpuj();
        Snimka(okno, "22b_oprava_nazvu_v_riadku");
        oblastJazyk.DokonciPremenovanie(nemcina, true);
        Pumpuj();
        Over(nemcina.Nazov == "Konverzácia v nemeckom jazyku (B1)" && new ObjektService().Nacitaj(nemcina.Id)!.Nazov == nemcina.Nazov,
            "oprava názvu predmetu v riadku");
        Over(DokumentOsnovy.Text(dokumentyUi.Nacitaj(nemcina.Id).Bloky).Contains("Predmet Konverzácia v nemeckom jazyku (B1) sa vyučuje", StringComparison.Ordinal),
            "nový názov sa prejavil aj v texte osnovy");
        Snimka(okno, "22c_po_oprave_nazvu");
        model.PremenujPredmet(nemcina, "Konverzácia v nemeckom jazyku");
        Over(DokumentOsnovy.Text(dokumentyUi.Nacitaj(nemcina.Id).Bloky).Contains("Predmet Konverzácia v nemeckom jazyku sa vyučuje", StringComparison.Ordinal),
            "vrátenie názvu späť aj v texte");

        // profilácie a platnosť pod názvom predmetu, oprava do dokumentu a synchronizácia po úprave osnovy
        model.OtvorOblast(model.Oblasti[2]);
        Pumpuj();
        var priroda = (OblastViewModel)model.Stranka!;
        var fyz = model.Oblasti[2].Predmety.First(p => p.Nazov == "Fyzika");
        Over(fyz.TextProfilacii == $"Profilácie: {TestovaciaSada.ProfilacieSady}" && fyz.TextPlatnosti == "Platnosť: od 1.9.2026",
            $"údaje osnovy pod názvom predmetu ({fyz.TextProfilacii} | {fyz.TextPlatnosti})");
        Snimka(okno, "22d_oblast_udaje_osnov");
        fyz.UpravaUdajov = true;
        fyz.UpravaPlatnosti = "od 1.9.2027";
        Pumpuj();
        Snimka(okno, "22e_oprava_udajov");
        priroda.UlozitUdajeCommand.Execute(fyz);
        Pumpuj();
        Over(!fyz.UpravaUdajov && fyz.TextPlatnosti == "Platnosť: od 1.9.2027"
             && UvodnaTabulka.Precitaj(fyz.Id).Platnost == "od 1.9.2027", "oprava údajov sa zapísala do osnovy");
        Over(dokumentyUi.Verzie(fyz.Id)[0].Dovod!.Contains("úvodnej tabuľke", StringComparison.Ordinal), "pred opravou údajov sa zarchivovala verzia");
        dokumentyUi.Uloz(fyz.Id, UvodnaTabulka.Zapis(dokumentyUi.NacitajData(fyz.Id)!, "informatika", null), null);
        model.PoZmeneDokumentu(fyz.Id, oznamit: true);
        Over(fyz.TextProfilacii == "Profilácie: informatika", "úprava osnovy sa premietne do údajov pod názvom");

        // archív osnov za školský rok
        var verziePred = dokumentyUi.Verzie(fyz.Id).Count;
        var archivRoka = ArchivSkolskehoRoka.Archivuj("2025/2026");
        Over(Directory.Exists(archivRoka.Priecinok) && archivRoka.Dokumentov >= 43
             && File.Exists(Path.Combine(archivRoka.Priecinok!, "Učebné osnovy 2025-2026.pdf"))
             && File.Exists(Path.Combine(archivRoka.Priecinok!, "Učebné osnovy 2025-2026.docx")),
            $"archív osnov za školský rok ({archivRoka.Dokumentov} dokumentov)");
        var wordyArchivu = Directory.GetFiles(archivRoka.Priecinok!, "*.docx", SearchOption.AllDirectories).Where(f => Path.GetDirectoryName(f) != archivRoka.Priecinok).ToList();
        Over(wordyArchivu.Count >= 43 && wordyArchivu.All(f => File.Exists(Path.ChangeExtension(f, ".pdf"))),
            $"v priečinku archívu roka je pri každom dokumente Word aj PDF ({wordyArchivu.Count})");
        var zostavaRoka = ArchivSkolskehoRoka.Zostava(archivRoka);
        Over(zostavaRoka.PocetPredmetov == model.PocetPredmetov && zostavaRoka.Titul.Platnost == "Archív školského roka 2025/2026"
             && zostavaRoka.Oblasti.Sum(o => o.Predmety.Count(p => p.Obsah.Count > 0)) >= 43,
            "celé osnovy roka zostavené zo snímky v programe (náhľad, tlač, PDF, Word)");
        var vykreslenieRoka = NahladDokumentu.Priprav(PdfVystup.Vytvor(zostavaRoka));
        UlozObrazokStrany(vykreslenieRoka, 1, Path.Combine(Databaza.PriecinokDokumentov, "archiv_roka_uvodna_strana.png"));
        Over(dokumentyUi.Verzie(fyz.Id).Count == verziePred + 1 && dokumentyUi.Verzie(fyz.Id)[0].Dovod == "Archív školského roka 2025/2026",
            "archív školského roka aj v archíve verzií");
        Over(ArchivSkolskehoRoka.Zoznam().First().SkolskyRok == "2025/2026", "záznam o archíve školského roka");
        TestSekcieArchiv(okno, model, archivRoka, fyz);
        var celaSada = ZostavaOsnov.Zostav(new VolbyZostavy());
        var zhustenie = PdfVystup.StupenZhustenia(celaSada);
        Over(zhustenie > 0 && zhustenie < PdfVystup.MaxZhustenie, $"úvodná strana sady sa zhustením zmestí na jednu stranu (stupeň {zhustenie})");
        var pdfSady = Path.Combine(Databaza.PriecinokDokumentov, "samotest_testovacia_sada.pdf");
        PdfVystup.UlozPdf(celaSada, pdfSady);
        var vykreslenieSady = NahladDokumentu.Priprav(PdfVystup.Vytvor(celaSada));
        Over(NahladDokumentu.PocetStran(vykreslenieSady) > 100, $"PDF testovacej sady ({NahladDokumentu.PocetStran(vykreslenieSady)} strán)");
        DocxVystup.Uloz(celaSada, Path.Combine(Databaza.PriecinokDokumentov, "samotest_testovacia_sada.docx"));
        var stranaOsnovy = Enumerable.Range(1, NahladDokumentu.PocetStran(vykreslenieSady)).Skip(8).First();
        UlozObrazokStrany(vykreslenieSady, stranaOsnovy, Path.Combine(Databaza.PriecinokDokumentov, "sada_strana_a.png"));
        UlozObrazokStrany(vykreslenieSady, stranaOsnovy + 1, Path.Combine(Databaza.PriecinokDokumentov, "sada_strana_b.png"));
        UlozObrazokStrany(vykreslenieSady, 1, Path.Combine(Databaza.PriecinokDokumentov, "sada_uvodna_strana.png"));
        UlozObrazokStrany(vykreslenieSady, 2, Path.Combine(Databaza.PriecinokDokumentov, "sada_hlavicka.png"));

        var zalohyPred = new ZalohaService().Zalohy().Count;
        model.Vymaz(vsetko: false);
        Over(!model.JeTestovaciaSada && model.Oblasti.Count == 7 && model.PocetPredmetov == 43, "vymazanie osnov – oblasti a predmety zostali");
        Over(model.Oblasti.SelectMany(o => o.Predmety).All(p => p.Stav == StavOsnovy.Prazdna) && new DokumentService().Verzie(sjl.Id).Count == 0, "osnovy aj archív sú vymazané");
        Over(new ZalohaService().Zalohy().Count == zalohyPred + 1, "pred vymazaním sa vytvorila záloha");
        Over(ArchivSkolskehoRoka.Zoznam().Count == 0, "vymazanie osnov odstráni aj archív školských rokov v programe");
        OverBezSkolySady("vymazanie osnov", predSadou);
        model.NahrajTestovaciuSadu();
        Over(TitulnaStrana.Nacitaj().Skola == TestovaciaSada.SkolaSady && NastaveniaSkoly.CestaLoga() is not null, "opätovné nahratie sady doplní školu aj logo");
        model.Vymaz(vsetko: true);
        Over(model.Oblasti.Count == 0 && !model.JeTestovaciaSada, "vymazanie všetkého");
        OverBezSkolySady("vymazanie všetkého", predSadou);

        // vlastné údaje, ktoré používateľ zadal počas skúšania, vymazanie sady nezmaže
        model.NahrajTestovaciuSadu();
        Databaza.Zapis(UdajeSkVP.KlucSkola, "Moja škola");
        model.Vymaz(vsetko: false);
        Over(Databaza.Citaj(UdajeSkVP.KlucSkola) == "Moja škola" && NastaveniaSkoly.CestaLoga() is null
             && string.IsNullOrEmpty(Databaza.Citaj(UdajeSkVP.KlucAdresa)) && Databaza.Citaj(TestovaciaSada.KlucUdajeSkoly) is null, "vymazanie sady ponechá vlastný názov školy, ostatné údaje sady zmaže");
        Databaza.Zapis(UdajeSkVP.KlucSkola, "");
        model.Vymaz(vsetko: true);
        model.NahrajTestovaciuSadu();
        Over(model.Oblasti.Count == 7 && model.PocetPredmetov == 43, "testovacia sada do prázdneho programu doplní aj oblasti a predmety");
        Pumpuj();
        Snimka(okno, "23_sada_po_obnoveni");

        // diktovanie oblastí a predmetov: hlasové písanie Windows alebo prepis bez internetu (whisper.cpp)
        Over(PrepisHlasu.Prekazka() is null && PrepisHlasu.KniznicaFunguje(), $"prepis hlasu bez internetu – knižnica whisper.cpp ({PrepisHlasu.Prekazka() ?? "v poriadku"})");
        Over(File.Exists(Path.Combine(AppContext.BaseDirectory, "LICENSE-whisper.cpp.txt")), "licencia whisper.cpp je priložená k programu");
        var diktovanieOkno = new ImportStrukturyWindow(ImportStrukturyWindow.Rezim.Diktovanie) { Owner = okno };
        diktovanieOkno.Show();
        diktovanieOkno.Vstup = "Človek a príroda: fyzika, chémia, biológia.\nZdravie a pohyb: telesná a športová výchova.";
        diktovanieOkno.Rozpoznaj();
        Pumpuj();
        Over(diktovanieOkno.Plan is { Oblasti.Count: 2 }, "okno diktovania rozpozná oblasti a predmety");
        Snimka(diktovanieOkno, "18e_diktovanie_oblasti");

        // prepis reči často dá namiesto dvojbodky čiarku alebo pomlčku
        var bezDvojbodky = ImportStruktury.ZDiktovania("Človek a príroda, fyzika, chémia, biológia. zdravie a pohyb – telesná a športová výchova");
        Over(bezDvojbodky.Oblasti.Count == 2 && bezDvojbodky.Oblasti[0].Nazov == "Človek a príroda" && bezDvojbodky.Oblasti[0].Predmety.Count == 3
             && bezDvojbodky.Oblasti[1].Nazov == "Zdravie a pohyb" && bezDvojbodky.Oblasti[1].Predmety.SequenceEqual(new[] { "Telesná a športová výchova" }),
            "diktovanie: oblasť rozpoznaná aj bez dvojbodky (čiarka, pomlčka, malé písmená)");
        var preklep = ImportStruktury.ZDiktovania("Človek a prírova, fyzika, chémia, biológia. Fyzika, chémia.");
        Over(preklep.Oblasti.Count >= 1 && preklep.Oblasti[0].Nazov == "Človek a príroda" && preklep.Oblasti[0].Predmety.Count >= 3
             && ImportStruktury.Vzdialenost("priroda", "prirova") == 1,
            "diktovanie: oblasť rozpoznaná aj pri chybe prepisu v jednom písmene („Človek a prírova“)");
        Over(PrepisHlasu.BezOzveny(Diktovanie.Vyzva) == "" && PrepisHlasu.BezOzveny("Človek a príroda: fyzika, chémia, biológia.").Length > 0
             && PrepisHlasu.Uprav("  Fyzika   [BLANK_AUDIO] ") == "Fyzika" && Math.Abs(PrepisHlasu.Prevzorkuj(new float[22050], 22050).Length - 16000) <= 1,
            "diktovanie: úprava prepisu (ozvena výzvy, ticho, prevzorkovanie na 16 kHz)");

        // hlasové písanie Windows: pole dostane fokus a stlačí sa Win+H
        var povodnySposob = Diktovanie.Sposob;
        var povodnyModel = Diktovanie.Model;
        Diktovanie.Sposob = SposobDiktovania.Windows;
        var hlaseniaPred = Dialogy.TicheHlasenia.Count;
        diktovanieOkno.Diktovat.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        Pumpuj();
        Over(Dialogy.TicheHlasenia.Skip(hlaseniaPred).Contains("[Hlasové písanie] Win+H"), "diktovanie: hlasové písanie Windows (Win+H)");

        // prepis v počítači bez internetu (whisper.cpp) – ak zostavenie pripravilo model a slovenskú nahrávku
        var nahravka = Environment.GetEnvironmentVariable("SKVP_SAMOTEST_HLAS");
        var suborModelu = Environment.GetEnvironmentVariable("SKVP_SAMOTEST_HLAS_MODEL");
        if (nahravka is not null && suborModelu is not null && File.Exists(nahravka) && File.Exists(suborModelu) && PrepisHlasu.Prekazka() is null)
        {
            var modelHlasu = Diktovanie.Modely.First(m => m.Kluc == "small");
            Directory.CreateDirectory(Diktovanie.PriecinokModelov);
            File.Copy(suborModelu, modelHlasu.Cesta, true);
            var overenie = Diktovanie.OverAsync(modelHlasu.Cesta, modelHlasu.Sha256);
            while (!overenie.IsCompleted)
            {
                Pumpuj();
                Thread.Sleep(50);
            }

            Over(overenie.Result, "diktovanie: model overený podľa SHA-256");
            Diktovanie.Sposob = SposobDiktovania.Offline;
            Diktovanie.Model = modelHlasu;
            diktovanieOkno.Vstup = "";
            var priprava = PrepisHlasu.PripravAsync(modelHlasu);
            var cas = System.Diagnostics.Stopwatch.StartNew();
            while (!priprava.IsCompleted && cas.Elapsed < TimeSpan.FromMinutes(1))
            {
                Pumpuj();
                Thread.Sleep(50);
            }

            var nacitanie = cas.Elapsed;
            var zvuk = PrepisHlasu.NacitajWav(nahravka);
            cas.Restart();
            var uloha = diktovanieOkno.PrepisAsync(zvuk);
            while (!uloha.IsCompleted && cas.Elapsed < TimeSpan.FromMinutes(4))
            {
                Pumpuj();
                Thread.Sleep(50);
            }

            diktovanieOkno.Rozpoznaj();
            Pumpuj();
            var prepis = diktovanieOkno.Vstup;
            Krok($"diktovanie bez internetu: model načítaný za {nacitanie.TotalSeconds:0.0} s, {zvuk.Length / 16000.0:0.0} s zvuku prepísaných za {cas.Elapsed.TotalSeconds:0} s: „{prepis.Trim()}“");
            var kluc = ImportOsnov.Kluc(prepis);
            var slova = new[] { "clovek a priroda", "fyzik", "chemi", "biologi", "zdravie a pohyb", "telesn", "sportov" }.Count(kluc.Contains);
            var plan = diktovanieOkno.Plan;
            Over(slova >= 5, $"diktovanie bez internetu: slovenský text rozpoznaný ({slova} zo 7 výrazov)");
            Over(plan is not null && plan.Oblasti.Any(o => o.Nazov == "Človek a príroda" && o.Predmety.Count >= 2),
                "diktovanie bez internetu: z prepisu sa rozpoznali oblasti a predmety (" +
                string.Join(" | ", plan?.Oblasti.Select(o => o.Nazov + ": " + string.Join(", ", o.Predmety)) ?? Array.Empty<string>()) + ")");
            Snimka(diktovanieOkno, "18g_diktovanie_prepis");
        }
        else
        {
            Krok("diktovanie bez internetu: prepis preskočený (chýba model alebo nahrávka)");
        }

        Diktovanie.Sposob = povodnySposob;
        Diktovanie.Model = povodnyModel;
        PrepisHlasu.Zavri();
        diktovanieOkno.Close();
        var nastavenieDiktovania = new DiktovanieWindow { Owner = okno };
        nastavenieDiktovania.Show();
        Pumpuj();
        Snimka(nastavenieDiktovania, "18f_nastavenie_diktovania");
        nastavenieDiktovania.Close();

        // prvé spustenie: údaje o škole, oblasti podľa ŠVP, predmety z priečinka osnov podľa úvodnej tabuľky
        model.Vymaz(vsetko: true);
        var uvod = new PrveSpustenieWindow();
        uvod.Show();
        uvod.Udaje.Nazov = "Gymnázium Skúšobné";
        uvod.Udaje.Ulica = "Školská 1";
        uvod.Udaje.Psc = "999 99";
        uvod.Udaje.Mesto = "Skúšobné";
        uvod.Udaje.Ico = "12345678";
        Pumpuj();
        Snimka(uvod, "18_prve_spustenie");
        uvod.ZobrazKrok2();
        Pumpuj();
        Snimka(uvod, "18b_prve_spustenie_volba");
        uvod.Zvol("svp");
        uvod.Zacat();
        Pumpuj();
        model.NacitajOblasti();
        var oblastiSvp = new ObjektService().Oblasti();
        Over(oblastiSvp.Count == 7 && oblastiSvp.Select(o => o.Nazov).SequenceEqual(SvpGymnazia.Oblasti.Select(o => o.Nazov)) && model.PocetPredmetov == 0
             && Databaza.Citaj(UdajeSkVP.KlucSkola) == "Gymnázium Skúšobné" && UdajeOrganizacie.Nacitaj().AdresaRiadok == "Školská 1, 999 99 Skúšobné",
            "prvé spustenie podľa ŠVP – 7 oblastí bez predmetov a údaje o škole");
        var dokumentySvp = new DokumentService();
        var prirodaSvp = oblastiSvp.First(o => o.Nazov == "Človek a príroda");
        var textPrirody = DokumentOsnovy.Text(dokumentySvp.Nacitaj(prirodaSvp.Id).Bloky);
        Over(textPrirody.Contains("Základnou charakteristikou vzdelávacej oblasti je hľadanie zákonitých súvislostí", StringComparison.Ordinal)
             && textPrirody.Contains("Štátny vzdelávací program pre gymnáziá", StringComparison.Ordinal), "charakteristika oblasti podľa ŠVP");

        var priecinokSvp = Path.Combine(Databaza.PriecinokDokumentov, "Osnovy školy");
        Directory.CreateDirectory(priecinokSvp);
        byte[] OsnovaSTabulkou(string predmet) => DocxVystup.Osnova(new List<Blok>
        {
            new Tabulka(new IReadOnlyList<Bunka>[]
            {
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("Názov predmetu") }) }), new Bunka(new Blok[] { new Odsek(new[] { new Usek(predmet) }) }) },
                new[] { new Bunka(new Blok[] { new Odsek(new[] { new Usek("Platnosť") }) }), new Bunka(new Blok[] { new Odsek(new[] { new Usek("od 1.9.2026") }) }) },
            }, 2),
            new Nadpis(1, "Charakteristika predmetu"),
            new Odsek(new[] { new Usek($"Osnova predmetu {predmet}.") }),
        });
        var subory = new[] { ("osnova_1.docx", "Fyzika"), ("osnova_2.docx", "Seminár z dejepisu"), ("osnova_3.docx", "Anglický jazyk"), ("osnova_4.docx", "Telesná a športová výchova") };
        foreach (var (subor, predmet) in subory)
        {
            File.WriteAllBytes(Path.Combine(priecinokSvp, subor), OsnovaSTabulkou(predmet));
        }

        // podpriečinok s názvom oblasti určí oblasť (aj keď ju názov predmetu neprezrádza), priečinok Archív sa preskočí
        var podpriecinok = Path.Combine(priecinokSvp, "06 Človek a hodnoty");
        Directory.CreateDirectory(podpriecinok);
        File.WriteAllBytes(Path.Combine(podpriecinok, "osnova_5.docx"), OsnovaSTabulkou("Projektové vyučovanie"));
        Directory.CreateDirectory(Path.Combine(priecinokSvp, "Archív"));
        File.WriteAllBytes(Path.Combine(priecinokSvp, "Archív", "stara_osnova.docx"), OsnovaSTabulkou("Fyzika"));

        var sprievodcaSvp = new ImportOsnovViewModel(model);
        sprievodcaSvp.PridajPriecinok(priecinokSvp);
        PolozkaImportu? Najdi(string zaciatok) => sprievodcaSvp.Polozky.FirstOrDefault(p => p.NovyNazov.StartsWith(zaciatok, StringComparison.Ordinal));
        Over(sprievodcaSvp.Polozky.Count == 5 && sprievodcaSvp.Polozky.All(p => p.JeNovy)
             && Najdi("Fyzika")?.NovaOblast?.Nazov == "Človek a príroda"
             && Najdi("Seminár z dejepisu")?.NovaOblast?.Nazov == "Človek a spoločnosť"
             && Najdi("Anglický jazyk")?.NovaOblast?.Nazov == "Jazyk a komunikácia"
             && Najdi("Telesná a športová výchova")?.NovaOblast?.Nazov == "Zdravie a pohyb"
             && Najdi("Projektové vyučovanie")?.NovaOblast?.Nazov == "Človek a hodnoty",
            "predmety z priečinka a podpriečinkov podľa úvodnej tabuľky a ich oblasti (" + string.Join(" | ", sprievodcaSvp.Polozky.Select(p => $"{p.NovyNazov} → {p.NovaOblast?.Nazov}")) + ")");
        var oknoSvp = new ImportOsnovWindow(sprievodcaSvp) { Owner = okno };
        oknoSvp.Show();
        Pumpuj();
        Snimka(oknoSvp, "18c_import_predmetov_svp");
        oknoSvp.Close();
        foreach (var p in sprievodcaSvp.Polozky.Where(p => p.CudziJazyk && p.Uroven is null))
        {
            p.Uroven = "B2";
        }

        Over(sprievodcaSvp.Importuj() == 5, "import osnov vytvorí predmety v oblastiach podľa ŠVP");
        model.NacitajOblasti();
        Over(model.PocetPredmetov == 5 && model.Oblasti.First(o => o.Nazov == "Človek a príroda").Predmety.Single().Nazov == "Fyzika", "predmety sú v správnych oblastiach");
        model.OtvorOblast(model.Oblasti.First(o => o.Nazov == "Človek a príroda"));
        Pumpuj();
        var oblastSvp = (OblastViewModel)model.Stranka!;
        Over(oblastSvp.MaCharakteristikuSvp && oblastSvp.CharakteristikaJeZoSvp, "charakteristika oblasti je zhodná so ŠVP");
        Snimka(okno, "18d_oblast_svp");

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

    /// <summary>Sekcia Archív: snímka roka, štatistiky, porovnanie rokov, hľadanie, archivovaná osnova a export štatistík.</summary>
    private static void TestSekcieArchiv(Window okno, MainViewModel model, ZaznamArchivuRoka prvy, PredmetVM fyz)
    {
        var polozky = ArchivSkolskehoRoka.Polozky(prvy.Id);
        var stat = ArchivSkolskehoRoka.Statistiky(polozky);
        Over(prvy.Predmetov == model.PocetPredmetov && stat.Predmety == model.PocetPredmetov && stat.Oblasti == model.Oblasti.Count
             && stat.Hotove + stat.NaKontrolu + stat.Rozpracovane + stat.Nezacate == stat.Predmety && stat.Znakov > 10_000,
            $"štatistiky archívu roka ({stat.Predmety} predmetov, {stat.Hotove} hotových, {stat.Normostrany} NS)");
        Over(stat.Hotove == model.Oblasti.SelectMany(o => o.Predmety).Count(p => p.Stav == StavOsnovy.Hotova), "počet hotových osnov v archíve");
        var fyzPolozka = polozky.First(p => p.ObjektId == fyz.Id);
        Over(fyzPolozka.Profilacie == "informatika" && fyzPolozka.Platnost == "od 1.9.2027" && fyzPolozka.MaDokument,
            "archív roka si pamätá profilácie a platnosť osnovy");
        Over(polozky.Count(p => !p.JePredmet) > 0, "archív roka obsahuje charakteristiky oblastí");
        if (ArchivSkolskehoRoka.Aktualny() != "2025/2026")
        {
            Over(stat.ZmeneneVRoku == 0, "v minulom školskom roku sa osnovy testovacej sady nemenili");
        }

        // druhý rok po zmene osnovy fyziky – porovnanie s predchádzajúcim rokom
        var dokumenty = new DokumentService();
        dokumenty.Uloz(fyz.Id, UvodnaTabulka.Zapis(dokumenty.NacitajData(fyz.Id)!, "šport, informatika", null), null);
        model.PoZmeneDokumentu(fyz.Id, oznamit: false);
        var rok = ArchivSkolskehoRoka.Aktualny();
        var druhy = ArchivSkolskehoRoka.Archivuj(rok);
        var porovnanie = ArchivSkolskehoRoka.Porovnaj(druhy);
        Over(porovnanie is { PredchadzajuciRok: "2025/2026", ZmeneneOsnovy: 1, NovePredmety: 0, OdstranenePredmety: 0 } && porovnanie.Zmenene.Contains(fyz.Id),
            $"porovnanie s predchádzajúcim rokom ({porovnanie?.ZmeneneOsnovy} zmenených)");
        Over(ArchivSkolskehoRoka.Statistiky(ArchivSkolskehoRoka.Polozky(druhy.Id)).ZmeneneVRoku > 0, "osnovy zmenené počas aktuálneho školského roka");

        model.OtvorArchivRoka(druhy.Id);
        Pumpuj();
        var archiv = (ArchivViewModel)model.Stranka!;
        Over(archiv.Archivy.Count == 2 && archiv.Vybrany?.Id == druhy.Id && archiv.Dlazdice.Count == 6 && archiv.MaPorovnanie
             && archiv.Oblasti.Count == model.Oblasti.Count, "sekcia Archív – vybraný rok so štatistikami");
        Snimka(okno, "22f_archiv");
        if (Najdi<ArchivView>(okno) is { } archivView && Najdi<System.Windows.Controls.ScrollViewer>(archivView) is { } posuv)
        {
            posuv.ScrollToVerticalOffset(560);
            Pumpuj();
            Snimka(okno, "22f2_archiv_statistiky");
            posuv.ScrollToVerticalOffset(1100);
            Pumpuj();
            Snimka(okno, "22f3_archiv_predmety");
            posuv.ScrollToVerticalOffset(0);
        }

        archiv.Hladat = "fyzika";
        Pumpuj();
        var najdene = archiv.Oblasti.SelectMany(o => o.Predmety).ToList();
        Over(najdene.Count > 0 && najdene.All(p => ImportOsnov.Kluc(p.Nazov).Contains("fyzika", StringComparison.Ordinal))
             && najdene.Count(p => p.Zmenena) == 1 && najdene.Single(p => p.Zmenena).Polozka.ObjektId == fyz.Id,
            $"hľadanie v archíve a značka zmenenej osnovy ({najdene.Count} nájdených)");
        Snimka(okno, "22g_archiv_hladanie");
        var predmetVArchive = najdene.Single(p => p.Zmenena);
        var oknoOsnovy = archiv.OknoOsnovy(predmetVArchive.Polozka, predmetVArchive.Oblast.Farba)!;
        oknoOsnovy.Show();
        Pumpuj();
        Over(oknoOsnovy.Bloky.Count > 3, "archivovaná osnova sa dá prečítať");
        Snimka(oknoOsnovy, "22h_archivna_osnova");
        oknoOsnovy.Close();
        archiv.Hladat = "";
        archiv.Vybrany = archiv.Archivy.First(a => a.Id == prvy.Id);
        Pumpuj();
        Over(!archiv.MaPorovnanie && archiv.MaPrehlad, "najstarší archivovaný rok nemá s čím porovnať");

        var excel = Path.Combine(Databaza.PriecinokDokumentov, "samotest_statistiky_archivu.xlsx");
        ArchivSkolskehoRoka.UlozStatistiky(druhy, excel);
        using (var zosit = new XLWorkbook(excel))
        {
            Over(zosit.Worksheet("Predmety").LastRowUsed()!.RowNumber() == model.PocetPredmetov + 1
                 && zosit.Worksheet("Štatistiky").Cell(5, 2).GetValue<int>() == model.PocetPredmetov
                 && zosit.Worksheet("Predmety").Column(10).CellsUsed().Any(c => c.GetString() == "zmenená"),
                "štatistiky archívu v Exceli");
        }

        // archívy zo staršej verzie programu (zoznam v nastaveniach): 2024/2025 má verzie „Archív školského roka 2024/2025“
        // v archíve verzií → prenesie sa so všetkými údajmi; 2023/2024 verzie nemá → len záznam s priečinkom
        var dokumentyPred = 0;
        foreach (var ob in model.Oblasti)
        {
            if (dokumenty.ArchivujAktualnu(ob.Id, "Archív školského roka 2024/2025"))
            {
                dokumentyPred++;
            }

            foreach (var pr in ob.Predmety)
            {
                if (dokumenty.ArchivujAktualnu(pr.Id, "Archív školského roka 2024/2025"))
                {
                    dokumentyPred++;
                }
            }
        }

        Databaza.Zapis("archiv.skolske_roky", $$"""
            [{"SkolskyRok":"2024/2025","Vytvorene":"{{DateTime.Now.ToString("s", System.Globalization.CultureInfo.InvariantCulture)}}","Priecinok":"C:\neexistuje","Dokumentov":{{dokumentyPred}}},
             {"SkolskyRok":"2023/2024","Vytvorene":"2024-06-30T10:00:00","Priecinok":"C:\neexistuje","Dokumentov":5}]
            """);
        ArchivSkolskehoRoka.PrenesStareZaznamy();
        var prenesene = ArchivSkolskehoRoka.Zoznam().SingleOrDefault(z => z.SkolskyRok == "2024/2025");
        var stary = ArchivSkolskehoRoka.Zoznam().SingleOrDefault(z => z.SkolskyRok == "2023/2024");
        Over(Databaza.Citaj("archiv.skolske_roky") is null && stary is { MaPrehlad: false }, "prenos archívu bez verzií zo staršej verzie programu");
        int ZnakyPredmetov(long id) => ArchivSkolskehoRoka.Polozky(id).Where(x => x.JePredmet).Sum(x => x.Dlzka);
        var statDruhy = ArchivSkolskehoRoka.Statistiky(ArchivSkolskehoRoka.Polozky(druhy.Id));
        var statPrenesene = prenesene is null ? null : ArchivSkolskehoRoka.Statistiky(ArchivSkolskehoRoka.Polozky(prenesene.Id));
        Over(prenesene is { MaPrehlad: true, Poznamka: ArchivSkolskehoRoka.DoplneneZVerzii } && prenesene.Predmetov == model.PocetPredmetov
             && prenesene.Dokumentov == dokumentyPred && ZnakyPredmetov(prenesene.Id) == ZnakyPredmetov(druhy.Id)
             && statPrenesene!.SProfilaciami == statDruhy.SProfilaciami && statPrenesene.Hotove == statDruhy.Hotove,
            $"archív zo staršej verzie programu prenesený so všetkými údajmi ({prenesene?.Predmetov} predmetov, {prenesene?.Dokumentov} dokumentov)");
        var fyzPrenesena = ArchivSkolskehoRoka.Polozky(prenesene!.Id).First(p => p.ObjektId == fyz.Id);
        Over(fyzPrenesena.Profilacie == "šport, informatika" && fyzPrenesena.Stav == fyz.Stav, "prenesený archív – údaje a stav osnovy");
        ArchivSkolskehoRoka.PrenesStareZaznamy();
        Over(ArchivSkolskehoRoka.Zoznam().Count == 4, "prenos starších archívov sa nezopakuje");
        archiv.Obnov(prenesene.Id);
        Pumpuj();
        Over(archiv.MaPrehlad && archiv.Dlazdice.Count == 6, "prenesený archív v sekcii Archív");
        Snimka(okno, "22i_archiv_preneseny");
        archiv.Obnov(stary!.Id);
        Pumpuj();
        Over(archiv.BezPrehladu && archiv.Archivy.Count == 4, "starší archív bez verzií – bez prehľadu predmetov");
        Snimka(okno, "22j_archiv_starsi");
        ArchivSkolskehoRoka.Odstran(stary.Id);
        ArchivSkolskehoRoka.Odstran(prenesene.Id);
        Over(ArchivSkolskehoRoka.Zoznam().Count == 2, "odstránenie archívu školského roka z programu");
    }

    /// <summary>Dlhá stránka po častiach: posúvač sa postupne posunie a uloží sa niekoľko snímok okna (…_1, …_2).</summary>
    private static void SnimkyStranky(Window okno, string nazov, int najviac = 6)
    {
        if (Najdi<System.Windows.Controls.ScrollViewer>(((MainWindow)okno).Obsah) is not { } posuvac)
        {
            return;
        }

        for (var i = 0; i < najviac; i++)
        {
            posuvac.ScrollToVerticalOffset(i * Math.Max(200, posuvac.ViewportHeight - 80));
            Pumpuj();
            Snimka(okno, $"{nazov}_{i + 1}");
            if (posuvac.VerticalOffset + posuvac.ViewportHeight >= posuvac.ExtentHeight - 1)
            {
                break;
            }
        }

        posuvac.ScrollToTop();
        Pumpuj();
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
