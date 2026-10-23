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
        "testovacia_sada", "migracia.", "zaloha.posledna_automaticka", "tlac.priecinok", "skvp.logo", "aktualizacia.", "archiv.",
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

    public static void Export(string cesta) => File.WriteAllText(cesta, JsonSerializer.Serialize(Vytvor(), Moznosti));

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
    public static int Import(Subor subor)
    {
        var pocet = 0;
        foreach (var (kluc, hodnota) in subor.Nastavenia)
        {
            if (Prenasat(kluc))
            {
                Databaza.Zapis(kluc, hodnota);
                pocet++;
            }
        }

        if (subor.Logo is { Length: > 0 } logo && subor.LogoPripona is { Length: > 0 } pripona)
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

        if (subor.TextDoleNaStrane is { Length: > 0 } pata)
        {
            TitulnaStrana.UlozDataPaty(Convert.FromBase64String(pata));
        }

        return pocet;
    }
}
