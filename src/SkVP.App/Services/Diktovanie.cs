using System.IO;
using System.Net.Http;
using Microsoft.Data.Sqlite;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Ako sa diktovaný text prepíše.</summary>
public enum SposobDiktovania
{
    /// <summary>Hlasové písanie Windows (Win+H) – vie po slovensky, potrebuje internet (Microsoft Azure).</summary>
    Windows,

    /// <summary>Prepis v počítači (whisper.cpp) – bez internetu, nič neodchádza von; treba stiahnuť model.</summary>
    Offline,
}

/// <summary>Model na prepis reči (whisper.cpp, formát ggml); stiahnutý súbor sa overí podľa SHA-256.</summary>
public sealed record ModelHlasu(string Kluc, string NazovSk, string PopisSk, string Subor, string Url, string Sha256, int VelkostMb)
{
    public string Nazov => Preklad.T(NazovSk);
    public string Popis => Preklad.T(PopisSk);

    /// <summary>Súbor modelu stiahnutý programom SkVP.</summary>
    public string Cesta => Path.Combine(Diktovanie.PriecinokModelov, Subor);

    /// <summary>Ten istý model stiahnutý programom EviTA na tomto počítači – netreba ho sťahovať druhýkrát.</summary>
    public string CestaEvita => Path.Combine(Diktovanie.PriecinokModelovEvita, Subor);

    /// <summary>Súbor, ktorý sa použije: vlastný, inak z EviTA (ak nie je ani jeden, vlastný – na stiahnutie).</summary>
    public string CestaNaPouzitie => !File.Exists(Cesta) && File.Exists(CestaEvita) ? CestaEvita : Cesta;

    public bool JeZEvity => CestaNaPouzitie != Cesta;

    public bool JeStiahnuty => File.Exists(CestaNaPouzitie);
}

/// <summary>Nastavenia diktovania a správa modelov na prepis bez internetu.</summary>
public static class Diktovanie
{
    public const string KlucSposob = "diktovanie.sposob";
    public const string KlucModel = "diktovanie.model";

    /// <summary>Jazyk prepisu (slovenčina).</summary>
    public const string Jazyk = "sk";

    /// <summary>
    /// Úvodný text pre model – slovenský pravopis, interpunkcia a slovník školského vzdelávacieho programu. Je v tvare,
    /// aký má mať prepis (oblasť, dvojbodka, predmety oddelené čiarkou, bodka) – model ho napodobní.
    /// </summary>
    public const string Vyzva =
        "Jazyk a komunikácia: slovenský jazyk a literatúra, anglický jazyk, nemecký jazyk, ruský jazyk. " +
        "Matematika a práca s informáciami: matematika, informatika. Človek a príroda: fyzika, chémia, biológia. " +
        "Človek a spoločnosť: dejepis, geografia, občianska náuka. Človek a hodnoty: etická výchova, náboženská výchova. " +
        "Umenie a kultúra: umenie a kultúra. Zdravie a pohyb: telesná a športová výchova.";

    public static IReadOnlyList<ModelHlasu> Modely { get; } = new[]
    {
        new ModelHlasu("small", Preklad.N("Štandardný (odporúčaný)"),
            Preklad.N("dobrá presnosť po slovensky, prepis približne v čase nahrávky"),
            "ggml-small.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin",
            "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b", 488),
        new ModelHlasu("turbo", Preklad.N("Presnejší"),
            Preklad.N("najlepšia presnosť, prepis trvá dlhšie (výkonnejší počítač)"),
            "ggml-large-v3-turbo-q5_0.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin",
            "394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2", 574),
        new ModelHlasu("base", Preklad.N("Rýchly"),
            Preklad.N("rýchly aj na slabšom počítači, po slovensky menej presný"),
            "ggml-base.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin",
            "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe", 148),
    };

    /// <summary>Priečinok modelov programu EviTA (C:\ProgramData\EviTA\Modely) – ten istý model sa použije aj tu.</summary>
    public static string PriecinokModelovEvita =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EviTA", "Modely");

    /// <summary>Modely sú mimo priečinka s databázou, aby nezväčšovali zálohy.</summary>
    public static string PriecinokModelov =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(Databaza.Priecinok.TrimEnd('\\', '/'))) ?? Databaza.Priecinok, "Modely");

    public static SposobDiktovania Sposob
    {
        get => Nastavenie(KlucSposob) == "offline" ? SposobDiktovania.Offline : SposobDiktovania.Windows;
        set => Databaza.Zapis(KlucSposob, value == SposobDiktovania.Offline ? "offline" : "windows");
    }

    public static ModelHlasu Model
    {
        get
        {
            var kluc = Nastavenie(KlucModel);
            return Modely.FirstOrDefault(m => m.Kluc == kluc) ?? Modely[0];
        }

        set => Databaza.Zapis(KlucModel, value.Kluc);
    }

    /// <summary>Nastavenie diktovania; nedostupná databáza = predvolené (diktovanie nesmie zhodiť okno).</summary>
    private static string? Nastavenie(string kluc)
    {
        try
        {
            return Databaza.Citaj(kluc);
        }
        catch (SqliteException)
        {
            return null;
        }
    }

    /// <summary>Stiahne model a overí SHA-256; použiteľný súbor vznikne až po overení.</summary>
    public static async Task StiahniAsync(ModelHlasu model, IProgress<double>? priebeh, CancellationToken zrusenie)
    {
        VysledokStahovania vysledok;
        try
        {
            vysledok = await Stahovanie.StiahniAsync(model.Url,
                new CielStahovania(model.Cesta, model.Sha256, OdhadVelkosti: model.VelkostMb * 1024L * 1024L), priebeh, zrusenie);
        }
        catch (HttpRequestException ex)
        {
            throw new SkvpException(Preklad.F($"Model sa nepodarilo stiahnuť. Skontrolujte pripojenie k internetu.\n\n{ex.Message}"));
        }

        if (vysledok != VysledokStahovania.Overeny)
        {
            throw new SkvpException(Preklad.T("Stiahnutý model nemá správny kontrolný súčet (SHA-256) – nepoužije sa. Skúste to znova."));
        }
    }

    /// <summary>SHA-256 súboru sa zhoduje s očakávaným.</summary>
    public static Task<bool> OverAsync(string cesta, string sha256, CancellationToken zrusenie = default) =>
        Stahovanie.OverAsync(cesta, sha256, zrusenie);

    /// <summary>Vymaže vlastný súbor modelu (model z EviTA zostane – patrí jej).</summary>
    public static void Vymaz(ModelHlasu model)
    {
        PrepisHlasu.Zavri();
        File.Delete(model.Cesta);
    }
}
