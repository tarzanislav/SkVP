using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using SkVP.Data;
using SkVP.Models;

namespace SkVP.Services;

/// <summary>
/// Testovacia sada učebných osnov: ukážkové osnovy všetkých predmetov učebného plánu gymnázia (podľa ŠVP a návrhy osnov
/// školských predmetov). Slúži na vyskúšanie programu; dá sa kedykoľvek vymazať a začať pracovať s reálnymi osnovami.
/// </summary>
public static class TestovaciaSada
{
    public const string KlucNainstalovana = "testovacia_sada";

    private const string DovodArchivacie = "Pred nahratím testovacej sady";

    /// <summary>Záznam o údajoch vymyslenej školy, ktoré zapísala testovacia sada (aby sa dali vymazať bez zvyškov).</summary>
    public const string KlucUdajeSkoly = "testovacia_sada.skola";

    // ---- vymyslená škola testovacej sady (nikde neexistuje) ----

    public const string SkolaSady = "Gymnázium Javorová";
    public const string AdresaSady = "Javorová 21, 999 01 Hviezdany";
    public const string IcoSady = "12345678";
    public const string DicSady = "2020123456";
    public const string EmailSady = "skola@gymnazium-javorova.example";
    public const string TelefonSady = "+421 999 123 456";
    public const string ProgramSady = "Školský vzdelávací program „Hviezdy na dosah“";
    public const string ObdobieSady = "od 1. septembra 2026";
    public const string RiadkySady = "Schválené pedagogickou radou dňa 28. augusta 2026\nPrerokované v rade školy dňa 29. augusta 2026";
    public const string HlavickaSady = "ŠkVP „Hviezdy na dosah“ – Učebné osnovy";

    /// <summary>Nastavenia, ktoré testovacia sada zapíše (kľúč → hodnota).</summary>
    public static IReadOnlyList<(string Kluc, string Hodnota)> NastaveniaSady => new[]
    {
        (UdajeSkVP.KlucSkola, SkolaSady),
        (UdajeSkVP.KlucAdresa, AdresaSady),
        (UdajeOrganizacie.KlucUlica, "Javorová 21"),
        (UdajeOrganizacie.KlucPsc, "999 01"),
        (UdajeOrganizacie.KlucMesto, "Hviezdany"),
        (UdajeOrganizacie.KlucStat, UdajeOrganizacie.PredvolenyStat),
        (UdajeOrganizacie.KlucIco, IcoSady),
        (UdajeOrganizacie.KlucDic, DicSady),
        (UdajeOrganizacie.KlucEmail, EmailSady),
        (UdajeOrganizacie.KlucTelefon, TelefonSady),
        (UdajeSkVP.KlucNazov, ProgramSady),
        (UdajeSkVP.KlucObdobie, ObdobieSady),
        (UcebnyPlanService.KlucTypSkoly, TitulnaStrana.AktualnyStupenGymnazia),
        ("titul.riadky", RiadkySady),
        ("titul.hlavicka.text", HlavickaSady),
    };

    /// <summary>Text dole na úvodnej strane: prerokovanie a podpisy (vymyslené mená).</summary>
    public static List<Blok> PataSady() => new()
    {
        Odsek("Školský vzdelávací program bol prerokovaný v pedagogickej rade a v rade školy. Zostava vytvorená {datum}."),
        new Tabulka(new IReadOnlyList<Bunka>[]
        {
            new[]
            {
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Vypracovala", Tucne: true) }) }, Hlavicka: true),
                new Bunka(new Blok[] { new Odsek(new[] { new Usek("Schválil", Tucne: true) }) }, Hlavicka: true),
            },
            new[]
            {
                new Bunka(new Blok[] { Odsek("Mgr. Lucia Hviezdna, koordinátorka ŠkVP") }),
                new Bunka(new Blok[] { Odsek("PaedDr. Oskar Javorský, riaditeľ školy") }),
            },
        }, 2),
    };

    private sealed class ZaznamSkoly
    {
        /// <summary>Kľúč → pôvodná hodnota (null = nebola) a hodnota zapísaná sadou.</summary>
        public Dictionary<string, string?[]> Nastavenia { get; set; } = new();
        public string? HashLoga { get; set; }
        public string? TextPaty { get; set; }
    }

    public sealed class Sada
    {
        public string Popis { get; set; } = "";
        public List<OblastSady> Oblasti { get; set; } = new();
        public List<PredmetSady> Predmety { get; set; } = new();
    }

    public sealed class OblastSady
    {
        public string Nazov { get; set; } = "";
        public List<string> Charakteristika { get; set; } = new();
    }

    public sealed class PredmetSady
    {
        public string Nazov { get; set; } = "";
        public string Oblast { get; set; } = "";
        public string Zdroj { get; set; } = "";
        public List<string> Charakteristika { get; set; } = new();
        public List<string> Ciele { get; set; } = new();
        public List<RocnikSady> Rocniky { get; set; } = new();
        public List<string> Metody { get; set; } = new();
        public List<string> Hodnotenie { get; set; } = new();
        public List<string> Zdroje { get; set; } = new();
        public string? Stav { get; set; }
        public List<SuvislostSady> Suvisi { get; set; } = new();
        public string? Poznamka { get; set; }

        public bool ZoSvp => Zdroj == "ŠVP";
    }

    public sealed class RocnikSady
    {
        public string Rocnik { get; set; } = "";
        public List<CelokSady> Celky { get; set; } = new();
    }

    public sealed class CelokSady
    {
        public string Nazov { get; set; } = "";
        public List<string> Obsah { get; set; } = new();
        public List<string> Vykon { get; set; } = new();
    }

    public sealed class SuvislostSady
    {
        public string Predmet { get; set; } = "";
        public string Typ { get; set; } = TypVazby.Suvisi;
    }

    public sealed record Vysledok(int Osnovy, int Charakteristiky, int Suvislosti);

    /// <summary>Je v databáze nahratá testovacia sada (zobrazí sa upozornenie s možnosťou ju vymazať).</summary>
    public static bool JeNainstalovana => Databaza.Citaj(KlucNainstalovana) == "1";

    /// <summary>Inštalátor ponúkol testovaciu sadu a používateľ ju zaškrtol.</summary>
    public static bool ZvolenaPriInstalacii
    {
        get
        {
            try
            {
                using var kluc = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\tarzanislav\SkVP");
                return kluc?.GetValue("TestovaciaSada") is int hodnota && hodnota == 1;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public static Sada Nacitaj()
    {
        using var prud = typeof(TestovaciaSada).Assembly.GetManifestResourceStream("SkVP.TestovaciaSada.json")
                         ?? throw new SkvpException("V programe chýba testovacia sada učebných osnov.");
        return JsonSerializer.Deserialize<Sada>(prud, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Sada();
    }

    /// <summary>
    /// Nahrá testovaciu sadu: doplní oblasti a predmety učebného plánu gymnázia (ak chýbajú) a do predmetov s rovnakým
    /// názvom uloží ukážkové osnovy. Doterajšie osnovy sa pritom zarchivujú.
    /// </summary>
    public static Vysledok Nainstaluj()
    {
        var sada = Nacitaj();
        UcebnyPlanService.Vytvor(UcebnyPlanService.Vstavany(), nahradit: false);

        var sluzba = new ObjektService();
        var dokumenty = new DokumentService();
        var oblasti = sluzba.Oblasti();
        var predmety = oblasti.SelectMany(o => sluzba.Deti(o.Id)).Where(p => p.Typ == TypObjektu.Predmet)
            .GroupBy(p => p.Nazov, StringComparer.CurrentCultureIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.CurrentCultureIgnoreCase);

        var charakteristiky = 0;
        foreach (var oblastSady in sada.Oblasti)
        {
            if (oblasti.FirstOrDefault(o => string.Equals(o.Nazov, oblastSady.Nazov, StringComparison.CurrentCultureIgnoreCase)) is { } oblast)
            {
                dokumenty.UlozBloky(oblast.Id, CharakteristikaOblasti(oblastSady), DovodArchivacie);
                charakteristiky++;
            }
        }

        var osnovy = 0;
        foreach (var predmetSady in sada.Predmety)
        {
            if (!predmety.TryGetValue(predmetSady.Nazov, out var predmet))
            {
                continue;
            }

            dokumenty.UlozBloky(predmet.Id, Osnova(predmetSady), DovodArchivacie);
            sluzba.NastavStav(predmet.Id, predmetSady.Stav);
            osnovy++;
        }

        var suvislosti = 0;
        foreach (var predmetSady in sada.Predmety)
        {
            if (!predmety.TryGetValue(predmetSady.Nazov, out var predmet))
            {
                continue;
            }

            foreach (var s in predmetSady.Suvisi)
            {
                if (predmety.TryGetValue(s.Predmet, out var ciel) && ciel.Id != predmet.Id)
                {
                    sluzba.Prepoj(predmet.Id, ciel.Id, s.Typ);
                    suvislosti++;
                }
            }
        }

        NahrajSkolu();
        Databaza.Zapis(KlucNainstalovana, "1");
        return new Vysledok(osnovy, charakteristiky, suvislosti);
    }

    /// <summary>
    /// Údaje vymyslenej školy: názov, adresa, logo, názov programu, texty úvodnej strany a hlavičky. Zapíšu sa len do programu,
    /// v ktorom ešte nie je vlastná škola ani logo; čo sa zapísalo, sa zaznamená, aby sa to dalo vymazať bez zvyškov.
    /// </summary>
    private static void NahrajSkolu()
    {
        if (Databaza.Citaj(KlucUdajeSkoly) is not null
            || Databaza.Citaj(UdajeSkVP.KlucSkola, "").Trim().Length > 0
            || NastaveniaSkoly.CestaLoga() is not null)
        {
            return;
        }

        var zaznam = new ZaznamSkoly();
        foreach (var (kluc, hodnota) in NastaveniaSady)
        {
            zaznam.Nastavenia[kluc] = new[] { Databaza.Citaj(kluc), hodnota };
            Databaza.Zapis(kluc, hodnota);
        }

        var docasne = Path.Combine(Path.GetTempPath(), $"SkVP_logo_sady_{Guid.NewGuid():N}.png");
        try
        {
            VytvorLogo(docasne);
            NastaveniaSkoly.NastavLogo(docasne);
            zaznam.HashLoga = HashSuboru(NastaveniaSkoly.CestaLoga());
        }
        catch (Exception)
        {
            // bez loga sa testovacia sada zaobíde
        }
        finally
        {
            try
            {
                File.Delete(docasne);
            }
            catch (IOException)
            {
            }
        }

        var pata = PataSady();
        TitulnaStrana.UlozPatu(pata, DovodArchivacie);
        zaznam.TextPaty = DokumentOsnovy.Text(TitulnaStrana.Nacitaj().PataBloky);
        Databaza.Zapis(KlucUdajeSkoly, JsonSerializer.Serialize(zaznam));
    }

    /// <summary>
    /// Vymaže údaje vymyslenej školy, ktoré zapísala testovacia sada. Čo používateľ medzitým zmenil (vlastný názov školy,
    /// iné logo, iný text), zostane; ostatné sa vráti na stav pred nahratím sady.
    /// </summary>
    public static void VymazSkolu()
    {
        if (Databaza.Citaj(KlucUdajeSkoly) is not { Length: > 0 } json)
        {
            return;
        }

        ZaznamSkoly? zaznam;
        try
        {
            zaznam = JsonSerializer.Deserialize<ZaznamSkoly>(json);
        }
        catch (JsonException)
        {
            zaznam = null;
        }

        if (zaznam is not null)
        {
            foreach (var (kluc, hodnoty) in zaznam.Nastavenia)
            {
                if (hodnoty.Length == 2 && Databaza.Citaj(kluc) == hodnoty[1])
                {
                    if (hodnoty[0] is null)
                    {
                        using var db = Databaza.Otvor();
                        Databaza.Vykonaj(db, "DELETE FROM nastavenie WHERE kluc = $k", ("$k", kluc));
                    }
                    else
                    {
                        Databaza.Zapis(kluc, hodnoty[0]);
                    }
                }
            }

            if (zaznam.HashLoga is not null && HashSuboru(NastaveniaSkoly.CestaLoga()) == zaznam.HashLoga)
            {
                NastaveniaSkoly.OdstranLogo();
            }

            var pata = TitulnaStrana.Nacitaj().PataBloky;
            if (zaznam.TextPaty is not null && DokumentOsnovy.Text(pata) == zaznam.TextPaty)
            {
                using var db = Databaza.Otvor();
                Databaza.Vykonaj(db, "DELETE FROM objekt WHERE typ = $t", ("$t", TitulnaStrana.TypPaty));
            }
        }

        using (var db = Databaza.Otvor())
        {
            Databaza.Vykonaj(db, "DELETE FROM nastavenie WHERE kluc = $k", ("$k", KlucUdajeSkoly));
        }
    }

    private static string? HashSuboru(string? cesta) =>
        cesta is not null && File.Exists(cesta) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(cesta))) : null;

    /// <summary>Logo vymyslenej školy: tmavomodrý kruh so zlatým lemom, hviezdou a písmenami GJ (kreslí sa v programe).</summary>
    public static void VytvorLogo(string cesta)
    {
        Exception? chyba = null;
        var vlakno = new Thread(() =>
        {
            try
            {
                const int rozmer = 480;
                var stred = new Point(rozmer / 2.0, rozmer / 2.0);
                var modra = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A));
                var zlata = new SolidColorBrush(Color.FromRgb(0xF2, 0xB8, 0x24));
                var kresba = new DrawingVisual();
                using (var k = kresba.RenderOpen())
                {
                    k.DrawEllipse(zlata, null, stred, rozmer / 2.0 - 4, rozmer / 2.0 - 4);
                    k.DrawEllipse(modra, null, stred, rozmer / 2.0 - 26, rozmer / 2.0 - 26);
                    k.DrawEllipse(null, new Pen(zlata, 5), stred, rozmer / 2.0 - 46, rozmer / 2.0 - 46);

                    // päťcípa hviezda
                    var body = new PointCollection();
                    for (var i = 0; i < 10; i++)
                    {
                        var polomer = i % 2 == 0 ? 112.0 : 46.0;
                        var uhol = Math.PI / 5 * i - Math.PI / 2;
                        body.Add(new Point(stred.X + polomer * Math.Cos(uhol), stred.Y - 52 + polomer * Math.Sin(uhol)));
                    }

                    var hviezda = new StreamGeometry();
                    using (var g = hviezda.Open())
                    {
                        g.BeginFigure(body[0], true, true);
                        g.PolyLineTo(body.Skip(1).ToList(), true, true);
                    }

                    k.DrawGeometry(zlata, null, hviezda);

                    var pismo = new FormattedText("GJ", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 112, Brushes.White, 1.0);
                    k.DrawText(pismo, new Point(stred.X - pismo.Width / 2, stred.Y + 52));
                }

                var obrazok = new RenderTargetBitmap(rozmer, rozmer, 96, 96, PixelFormats.Pbgra32);
                obrazok.Render(kresba);
                var kodovac = new PngBitmapEncoder();
                kodovac.Frames.Add(BitmapFrame.Create(obrazok));
                using var subor = File.Create(cesta);
                kodovac.Save(subor);
            }
            catch (Exception ex)
            {
                chyba = ex;
            }
        });
        vlakno.SetApartmentState(ApartmentState.STA);
        vlakno.Start();
        vlakno.Join();
        if (chyba is not null)
        {
            throw new SkvpException("Logo testovacej sady sa nepodarilo vytvoriť: " + chyba.Message);
        }
    }

    /// <summary>Vymaže všetky osnovy, ich archív (aj archív školských rokov v programe), súvislosti a údaje vymyslenej školy; oblasti a predmety zostanú.</summary>
    public static void VymazOsnovy()
    {
        VymazSkolu();
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        Databaza.Vykonaj(db, """
            DELETE FROM archiv_polozka;
            DELETE FROM archiv_rok;
            DELETE FROM verzia;
            DELETE FROM dokument;
            DELETE FROM vazba;
            UPDATE objekt SET stav = NULL;
            DELETE FROM nastavenie WHERE kluc = $k;
            """, ("$k", KlucNainstalovana));
        transakcia.Commit();
    }

    /// <summary>
    /// Vymaže všetko – oblasti, predmety, osnovy, archív aj súvislosti a údaje vymyslenej školy testovacej sady.
    /// Vlastné nastavenia (škola, logo, vzhľad) zostanú.
    /// </summary>
    public static void VymazVsetko()
    {
        VymazSkolu();
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        Databaza.Vykonaj(db, """
            DELETE FROM archiv_polozka;
            DELETE FROM archiv_rok;
            DELETE FROM verzia;
            DELETE FROM dokument;
            DELETE FROM vazba;
            DELETE FROM objekt;
            DELETE FROM nastavenie WHERE kluc = $k OR kluc LIKE 'osnovy.%';
            """, ("$k", KlucNainstalovana));
        transakcia.Commit();
        using var dbVacuum = Databaza.Otvor();
        Databaza.Vykonaj(dbVacuum, "VACUUM");
    }

    // ---- tvorba textu osnovy ----

    /// <summary>Charakteristika oblasti: doslovné znenie Štátneho vzdelávacieho programu pre gymnáziá (inak text sady).</summary>
    public static List<Blok> CharakteristikaOblasti(OblastSady oblast)
    {
        if (SvpGymnazia.Oblast(oblast.Nazov) is { } svp)
        {
            return SvpGymnazia.Charakteristika(svp);
        }

        return UkazkovaCharakteristika(oblast);
    }

    /// <summary>Ukážková charakteristika oblasti zo starších verzií sady (pred prevzatím zo ŠVP).</summary>
    public static List<Blok> UkazkovaCharakteristika(OblastSady oblast)
    {
        var bloky = oblast.Charakteristika.Select(t => (Blok)Odsek(t)).ToList();
        bloky.Add(Poznamka());
        return bloky;
    }

    /// <summary>Profilácie vymyslenej školy v úvodnej tabuľke osnov.</summary>
    public const string ProfilacieSady = "šport, informatika, anglický jazyk, STEAM";

    public const string PlatnostSady = "od 1.9.2026";

    /// <summary>Úvodná tabuľka na začiatku osnovy: predmet, profilácie, platnosť (ako v osnovách školy).</summary>
    public static Tabulka UvodnaTabulka(string predmet)
    {
        Bunka Popis(string text) => new(new Blok[] { new Odsek(new[] { new Usek(text, Tucne: true) }) }, Hlavicka: false);
        Bunka Hodnota(string text) => new(new Blok[] { new Odsek(new[] { new Usek(text) }) });
        return new Tabulka(new IReadOnlyList<Bunka>[]
        {
            new[] { Popis("Názov predmetu"), Hodnota(predmet) },
            new[] { Popis("Profilácia"), Hodnota(ProfilacieSady) },
            new[] { Popis("Platnosť"), Hodnota(PlatnostSady) },
            new[] { Popis("Stupeň vzdelania"), Hodnota("úplné stredné všeobecné vzdelanie (ISCED 344)") },
        }, 2, new[] { 3.0, 7.0 });
    }

    public static List<Blok> Osnova(PredmetSady p)
    {
        var bloky = new List<Blok> { UvodnaTabulka(p.Nazov), new Nadpis(1, "Charakteristika predmetu") };
        bloky.AddRange(p.Charakteristika.Select(t => (Blok)Odsek(t)));
        bloky.Add(new Odsek(new[]
        {
            new Usek(p.ZoSvp
                ? "Učebná osnova vychádza zo vzdelávacieho štandardu štátneho vzdelávacieho programu pre gymnáziá so štvorročným vzdelávacím programom."
                : "Predmet je zaradený do školského vzdelávacieho programu z disponibilných hodín školy; učebnú osnovu navrhla škola v nadväznosti na štátny vzdelávací program.",
                Kurziva: true),
        }));

        bloky.Add(new Nadpis(1, "Ciele predmetu"));
        bloky.Add(Odsek("Cieľom predmetu je viesť žiakov k tomu, aby vedeli:"));
        bloky.Add(Zoznam(p.Ciele));

        bloky.Add(new Nadpis(1, "Obsahový a výkonový štandard"));
        foreach (var rocnik in p.Rocniky)
        {
            bloky.Add(new Nadpis(2, rocnik.Rocnik));
            var riadky = new List<IReadOnlyList<Bunka>>
            {
                new[]
                {
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek("Tematický celok", Tucne: true) }) }, Hlavicka: true),
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek("Obsahový štandard", Tucne: true) }) }, Hlavicka: true),
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek("Výkonový štandard – žiak vie, dokáže", Tucne: true) }) }, Hlavicka: true),
                },
            };
            foreach (var celok in rocnik.Celky)
            {
                riadky.Add(new[]
                {
                    new Bunka(new Blok[] { new Odsek(new[] { new Usek(celok.Nazov, Tucne: true) }) }),
                    new Bunka(new Blok[] { Zoznam(celok.Obsah) }),
                    new Bunka(new Blok[] { Zoznam(celok.Vykon) }),
                });
            }

            bloky.Add(new Tabulka(riadky, 3, new[] { 2.2, 3.9, 3.9 }));
        }

        if (p.Metody.Count > 0)
        {
            bloky.Add(new Nadpis(1, "Metódy a formy práce"));
            bloky.Add(Zoznam(p.Metody));
        }

        if (p.Hodnotenie.Count > 0)
        {
            bloky.Add(new Nadpis(1, "Hodnotenie predmetu"));
            bloky.AddRange(p.Hodnotenie.Select(t => (Blok)Odsek(t)));
        }

        if (p.Zdroje.Count > 0)
        {
            bloky.Add(new Nadpis(1, "Učebné zdroje"));
            bloky.Add(Zoznam(p.Zdroje));
        }

        bloky.Add(Poznamka());
        return bloky;
    }

    private static Odsek Odsek(string text) => new(new[] { new Usek(text) });

    private static Zoznam Zoznam(IEnumerable<string> polozky) =>
        new(false, polozky.Select(t => (IReadOnlyList<Blok>)new Blok[] { Odsek(t) }).ToList());

    private static Odsek Poznamka() => new(new[]
    {
        new Usek("Testovacia sada – ukážkový text na vyskúšanie programu SkVP, nie oficiálny dokument školy.", Kurziva: true),
    });
}
