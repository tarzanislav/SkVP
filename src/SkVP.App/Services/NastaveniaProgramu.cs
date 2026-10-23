using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// Export a import nastavení programu do súboru JSON – škola, logo, program, úvodná strana a hlavička (aj text dole na
/// strane), vzhľad, jazyk, úprava osnov, voľby tlače a PDF, zálohovanie. Takto sa nastavenia prenesú na iný počítač.
/// Oblasti, predmety ani osnovy sa neprenášajú (na to je záloha v Servise alebo Import a export).
/// </summary>
public static class NastaveniaProgramu
{
    public const string Filter = "Nastavenia SkVP (*.json)|*.json";

    /// <summary>Kľúče, ktoré sa neprenášajú (stav tohto počítača, testovacia sada, jednorazové úpravy).</summary>
    private static readonly string[] Vynechat =
    {
        "testovacia_sada", "migracia.", "zaloha.posledna_automaticka", "tlac.priecinok", "skvp.logo", "aktualizacia.", "Aktualizacie.PoslednaKontrola", "archiv.", "kontrola.",
        // vyrovnávacie pamäte (stiahnuté údaje) – na inom počítači sa stiahnu znova
        "novinky.data", "novinky.spravy", "novinky.overenie", "novinky.historia", "pocasie.data",
    };

    public sealed class Subor
    {
        public string Program { get; set; } = "SkVP";
        public string Verzia { get; set; } = "";
        public DateTime Vytvorene { get; set; } = DateTime.Now;
        public Dictionary<string, string?> Nastavenia { get; set; } = new();

        /// <summary>Logo školy (prípona a obsah v base64).</summary>
        public string? LogoPripona { get; set; }
        public string? Logo { get; set; }

        /// <summary>Text dole na úvodnej strane (dokument Word v base64).</summary>
        public string? TextDoleNaStrane { get; set; }
    }

    private static readonly JsonSerializerOptions Moznosti = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static bool Prenasat(string kluc) => !Vynechat.Any(v => kluc.StartsWith(v, StringComparison.OrdinalIgnoreCase));

    public static Subor Vytvor()
    {
        var subor = new Subor { Verzia = AktualizaciaService.AktualnaVerzia.ToString() };
        using (var db = Databaza.Otvor())
        using (var prikaz = Databaza.Prikaz(db, "SELECT kluc, hodnota FROM nastavenie ORDER BY kluc"))
        using (var citac = prikaz.ExecuteReader())
        {
            while (citac.Read())
            {
                var kluc = citac.GetString(0);
                if (Prenasat(kluc))
                {
                    subor.Nastavenia[kluc] = citac.IsDBNull(1) ? null : citac.GetString(1);
                }
            }
        }

        if (NastaveniaSkoly.CestaLoga() is { } logo)
        {
            subor.LogoPripona = Path.GetExtension(logo);
            subor.Logo = Convert.ToBase64String(File.ReadAllBytes(logo));
        }

        if (TitulnaStrana.DataPaty() is { Length: > 0 } pata)
        {
            subor.TextDoleNaStrane = Convert.ToBase64String(pata);
        }

        return subor;
    }

    /// <summary>Skupina nastavenia podľa kľúča (na prehľad, čo súbor nastavení obsahuje).</summary>
    public static string Skupina(string kluc) => kluc switch
    {
        _ when kluc == UdajeSkVP.KlucSkola || kluc == UdajeSkVP.KlucAdresa || kluc.StartsWith("org.", StringComparison.Ordinal) => "údaje o organizácii",
        _ when kluc.StartsWith("skvp.", StringComparison.Ordinal) => "školský vzdelávací program (názov, druh školy, platnosť)",
        _ when kluc.StartsWith("titul.", StringComparison.Ordinal) => "úvodná strana, hlavička, päta a údaje o škole v zostavách",
        "tlac.volby" => "voľby tlače a zostáv",
        "tlac.pdf" => "nastavenia PDF (šifrovanie, heslo, viac strán na list, brožúra…)",
        "tlac.obojstranne" => "obojstranná tlač",
        _ when kluc.StartsWith("vzhlad.", StringComparison.Ordinal) => "vzhľad",
        _ when kluc.StartsWith("program.jazyk", StringComparison.Ordinal) => "jazyk programu",
        _ when kluc.StartsWith("uprava.", StringComparison.Ordinal) => "úprava osnov (Word / editor)",
        _ when kluc.StartsWith("zaloha.", StringComparison.Ordinal) => "zálohovanie (frekvencia, počet, priečinok)",
        _ when kluc.StartsWith("Aktualizacie.", StringComparison.Ordinal) => "aktualizácie",
        _ when kluc.StartsWith("diktovanie.", StringComparison.Ordinal) => "diktovanie (spôsob a model reči)",
        _ when kluc.StartsWith("osnovy.", StringComparison.Ordinal) => SkupinaCasti,
        _ when kluc.StartsWith("dashboard.", StringComparison.Ordinal) || kluc.StartsWith("pocasie.", StringComparison.Ordinal)
               || kluc.StartsWith("citaty.", StringComparison.Ordinal) => "dashboard (bloky, miesto počasia, skryté výroky)",
        _ when kluc.StartsWith("novinky.", StringComparison.Ordinal) => "Novinky a prázdniny (kraj)",
        _ => "ďalšie nastavenia",
    };

    public const string SkupinaLogo = "logo školy";
    public const string SkupinaCasti = "úvodné a záverečné časti učebných osnov (zapnuté / vypnuté)";
    public const string SkupinaPata = "text dole na úvodnej strane";

    /// <summary>Prehľad obsahu súboru nastavení: skupiny v poradí (s logom a textom dole na úvodnej strane).</summary>
    private static readonly string[] PoradieSkupin =
    {
        "údaje o organizácii", SkupinaLogo, "školský vzdelávací program (názov, druh školy, platnosť)",
        "úvodná strana, hlavička, päta a údaje o škole v zostavách", SkupinaPata, "voľby tlače a zostáv",
        "nastavenia PDF (šifrovanie, heslo, viac strán na list, brožúra…)", "obojstranná tlač", "vzhľad", "jazyk programu",
        SkupinaCasti, "úprava osnov (Word / editor)", "diktovanie (spôsob a model reči)", "dashboard (bloky, miesto počasia, skryté výroky)",
        "Novinky a prázdniny (kraj)", "zálohovanie (frekvencia, počet, priečinok)", "aktualizácie",
    };

    public static IReadOnlyList<string> Prehlad(Subor subor)
    {
        var skupiny = subor.Nastavenia.Keys.Select(Skupina).Distinct().ToList();
        if (subor.Logo is { Length: > 0 })
        {
            skupiny.Insert(Math.Min(1, skupiny.Count), SkupinaLogo);
        }

        if (subor.TextDoleNaStrane is { Length: > 0 })
        {
            skupiny.Add(SkupinaPata);
        }

        return skupiny.OrderBy(g => Array.IndexOf(PoradieSkupin, g) is var i && i >= 0 ? i : 99).ToList();
    }

    public static void Export(string cesta, IReadOnlyCollection<string>? skupiny = null)
    {
        var subor = Vytvor();
        if (skupiny is not null)
        {
            subor.Nastavenia = subor.Nastavenia.Where(n => skupiny.Contains(Skupina(n.Key))).ToDictionary(n => n.Key, n => n.Value);
            if (!skupiny.Contains(SkupinaLogo))
            {
                subor.Logo = null;
                subor.LogoPripona = null;
            }

            if (!skupiny.Contains(SkupinaPata))
            {
                subor.TextDoleNaStrane = null;
            }
        }

        File.WriteAllText(cesta, JsonSerializer.Serialize(subor, Moznosti));
    }

    public static Subor Nacitaj(string cesta)
    {
        try
        {
            var subor = JsonSerializer.Deserialize<Subor>(File.ReadAllText(cesta));
            if (subor is null || subor.Program != "SkVP")
            {
                throw new SkvpException("Súbor neobsahuje nastavenia programu SkVP.");
            }

            return subor;
        }
        catch (JsonException)
        {
            throw new SkvpException("Súbor nie je platný súbor nastavení SkVP.");
        }
    }

    /// <summary>Zapíše nastavenia zo súboru (doterajšie s rovnakým kľúčom sa prepíšu). Vráti počet nastavení.</summary>
    public static int Import(Subor subor, IReadOnlyCollection<string>? skupiny = null)
    {
        var pocet = 0;
        foreach (var (kluc, hodnota) in subor.Nastavenia)
        {
            // priečinok záloh z iného počítača sa prevezme, len ak na tomto počítači existuje
            if (kluc == Databaza.KlucPriecinokZaloh && hodnota is { Length: > 0 } && !Directory.Exists(hodnota))
            {
                continue;
            }

            if (Prenasat(kluc) && (skupiny is null || skupiny.Contains(Skupina(kluc))))
            {
                Databaza.Zapis(kluc, hodnota);
                pocet++;
            }
        }

        if (subor.Logo is { Length: > 0 } logo && subor.LogoPripona is { Length: > 0 } pripona && (skupiny is null || skupiny.Contains(SkupinaLogo)))
        {
            var docasne = Path.Combine(Path.GetTempPath(), $"skvp_logo_{Guid.NewGuid():N}{pripona}");
            try
            {
                File.WriteAllBytes(docasne, Convert.FromBase64String(logo));
                NastaveniaSkoly.NastavLogo(docasne);
            }
            finally
            {
                File.Delete(docasne);
            }
        }

        if (subor.TextDoleNaStrane is { Length: > 0 } pata && (skupiny is null || skupiny.Contains(SkupinaPata)))
        {
            TitulnaStrana.UlozDataPaty(Convert.FromBase64String(pata));
        }

        return pocet;
    }
}
