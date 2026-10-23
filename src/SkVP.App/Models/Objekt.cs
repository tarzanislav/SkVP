namespace SkVP.Models;

/// <summary>Druhy objektov v databáze. Ďalšie (súbor, téma, štandard, ročník …) pribudnú s ďalšími modulmi.</summary>
public static class TypObjektu
{
    public const string Oblast = "oblast";
    public const string Predmet = "predmet";

    /// <summary>Úvodné časti učebných osnov – všeobecná skupina pred vzdelávacími oblasťami (jediná).</summary>
    public const string Uvod = "uvod";

    /// <summary>Záverečné časti učebných osnov – všeobecná skupina za vzdelávacími oblasťami (jediná).</summary>
    public const string Zaver = "zaver";

    /// <summary>Úvodná alebo záverečná časť učebných osnov (dokument ako osnova predmetu).</summary>
    public const string Cast = "cast";

    /// <summary>Skupina, ktorá obsahuje dokumenty osnov – oblasť alebo úvodné/záverečné časti.</summary>
    public static bool JeSkupina(string typ) => typ is Oblast or Uvod or Zaver;

    public static string Nazov(string typ) => typ switch
    {
        Oblast => "Vzdelávacia oblasť",
        Predmet => "Predmet",
        Uvod => "Úvodné časti učebných osnov",
        Zaver => "Záverečné časti učebných osnov",
        Cast => "Časť učebných osnov",
        _ => typ,
    };
}

/// <summary>Stav spracovania učebnej osnovy predmetu.</summary>
public enum StavOsnovy
{
    /// <summary>Osnova zatiaľ nemá žiadny text.</summary>
    Prazdna,
    Rozpracovana,
    NaKontrolu,
    Hotova,
}

public static class StavyOsnovy
{
    public const string NaKontrolu = "na_kontrolu";
    public const string Hotova = "hotova";

    public static StavOsnovy Urci(string? stav, int dlzkaTextu) => stav switch
    {
        Hotova => StavOsnovy.Hotova,
        NaKontrolu => StavOsnovy.NaKontrolu,
        _ => dlzkaTextu > 0 ? StavOsnovy.Rozpracovana : StavOsnovy.Prazdna,
    };

    public static string Text(StavOsnovy stav) => stav switch
    {
        StavOsnovy.Prazdna => "Nezačatá",
        StavOsnovy.Rozpracovana => "Rozpracovaná",
        StavOsnovy.NaKontrolu => "Na kontrolu",
        StavOsnovy.Hotova => "Hotová",
        _ => "",
    };
}

/// <summary>Jeden záznam v databáze – oblasť, predmet a ďalšie.</summary>
public sealed class Objekt
{
    public long Id { get; init; }
    public string Typ { get; init; } = "";
    public long? RodicId { get; set; }
    public int Poradie { get; set; }
    public string Nazov { get; set; } = "";
    public string? Farba { get; set; }
    public string? Stav { get; set; }
    public int DlzkaTextu { get; set; }
    public DateTime Upravene { get; set; }
}

/// <summary>Druhy väzieb medzi objektmi.</summary>
public static class TypVazby
{
    public const string Suvisi = "suvisi";
    public const string NadvazujeNa = "nadvazuje";
    public const string Predpoklad = "predpoklad";
    public const string Medzipredmetovy = "medzipredmetovy";

    public static readonly (string Kod, string Nazov, string Spatny)[] Vsetky =
    {
        (Suvisi, "súvisí s", "súvisí s"),
        (NadvazujeNa, "nadväzuje na", "pokračuje v"),
        (Predpoklad, "je predpokladom pre", "predpokladá"),
        (Medzipredmetovy, "medzipredmetový vzťah s", "medzipredmetový vzťah s"),
    };

    public static string Nazov(string kod, bool spatny)
    {
        foreach (var (k, n, s) in Vsetky)
        {
            if (k == kod)
            {
                return spatny ? s : n;
            }
        }

        return kod;
    }
}

/// <summary>Objekt prepojený s iným objektom (z pohľadu jedného z nich).</summary>
public sealed record Suvislost(long VazbaId, Objekt Objekt, string TypVazby, bool Spatna, string? Poznamka)
{
    public string Popis => Models.TypVazby.Nazov(TypVazby, Spatna);
}
