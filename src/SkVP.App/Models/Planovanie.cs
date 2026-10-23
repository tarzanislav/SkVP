namespace SkVP.Models;

public enum PrioritaUlohy
{
    Nizka = 0,
    Normalna = 1,
    Vysoka = 2,
}

public enum OpakovanieUdalosti
{
    Ziadne = 0,
    Denne = 1,
    Tyzdenne = 2,
    Mesacne = 3,
    Rocne = 4,
}

public enum PripomienkaUdalosti
{
    Ziadna = 0,
    VDen = 1,
    DenVopred = 2,
    TyzdenVopred = 3,
}

public enum StavUdalosti
{
    Naplanovane = 0,
    Hotovo = 1,
    Zrusene = 2,
}

/// <summary>
/// Objekt, ku ktorému patrí úloha, udalosť alebo poznámka („Týka sa“) – predmet, oblasť, úvodná či záverečná časť,
/// ale aj iná udalosť alebo úloha. Väzba typu <see cref="TypVazby.Patri"/>.
/// </summary>
public sealed record Naviazanie(long Id, string Typ, string Nazov, string Farba, string Popis, bool Odstranene)
{
    public bool JeOsnova => Typ is TypObjektu.Predmet or TypObjektu.Cast || TypObjektu.JeSkupina(Typ);

    /// <summary>Krátky text do zoznamu: „Fyzika“, „Udalosť: Pedagogická rada“.</summary>
    public string Text => JeOsnova ? Nazov : $"{TypObjektu.Nazov(Typ)}: {Nazov}";

    public string Tip => Odstranene ? $"{Text} (odstránené)" : Popis.Length > 0 ? $"{Nazov} – {Popis}" : Nazov;
}

/// <summary>Typ udalosti v kalendári (pedagogická rada, predmetová komisia …) s farbou.</summary>
public sealed class TypUdalosti
{
    public long Id { get; init; }
    public string Nazov { get; set; } = "";
    public string Farba { get; set; } = "#94A3B8";
    public bool Aktivny { get; set; } = true;
    public int Poradie { get; set; }

    public override string ToString() => Nazov;
}

/// <summary>Úloha – voliteľne s termínom (potom je aj v kalendári), prioritou, zodpovedným a naviazaním na osnovy.</summary>
public sealed class Uloha
{
    public long Id { get; init; }
    public string Text { get; set; } = "";
    public string? Popis { get; set; }
    public DateTime? Termin { get; set; }
    public PrioritaUlohy Priorita { get; set; } = PrioritaUlohy.Normalna;
    public DateTime? Hotovo { get; set; }
    public string? Zodpovedny { get; set; }

    /// <summary>Odkiaľ úloha vznikla (navrhovaná úloha z osnov, novinka) – „kontrola:12“, „novinka:svp-2026“.</summary>
    public string? Zdroj { get; set; }

    public DateTime Vytvorene { get; init; }
    public List<long> TykaSa { get; set; } = new();
}

/// <summary>Udalosť v kalendári (porada, pedagogická rada, termín k osnovám …).</summary>
public sealed class Udalost
{
    public long Id { get; init; }
    public Guid Guid { get; init; } = Guid.NewGuid();
    public string Nazov { get; set; } = "";
    public string? Popis { get; set; }
    public long? TypId { get; set; }
    public DateTime Zaciatok { get; set; }
    public DateTime? Koniec { get; set; }
    public bool CelyDen { get; set; }
    public OpakovanieUdalosti Opakovanie { get; set; }
    public DateTime? OpakovanieDo { get; set; }
    public PripomienkaUdalosti Pripomienka { get; set; }
    public StavUdalosti Stav { get; set; }
    public string? Miesto { get; set; }
    public List<long> TykaSa { get; set; } = new();
}

/// <summary>Poznámka – lístok s farbou, voliteľne pre deň v kalendári a naviazaná na predmety, udalosť či úlohu.</summary>
public sealed class Poznamka
{
    public long Id { get; init; }
    public string Nazov { get; set; } = "";
    public string Text { get; set; } = "";
    public string Farba { get; set; } = "#FFF1A8";
    public bool Pripnuta { get; set; }
    public DateTime? Datum { get; set; }
    public DateTime Vytvorene { get; init; }
    public DateTime Zmenene { get; init; }
    public List<long> TykaSa { get; set; } = new();
}
