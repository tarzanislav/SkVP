using System.Text.Json.Serialization;
using SkVP.Data;
using SkVP.Pomocne;

namespace SkVP.Services;

/// <summary>Formát ukladanej zostavy (názvy sa ukladajú v nastaveniach – nemenia sa).</summary>
public enum FormatVystupu
{
    /// <summary>Bežné PDF (dá sa zašifrovať a chrániť heslom).</summary>
    Pdf,

    /// <summary>Archívne PDF/A-2b (ISO 19005-2).</summary>
    PdfA2b,

    /// <summary>Archívne PDF/A-3b s vloženým dokumentom Word.</summary>
    PdfA3b,

    /// <summary>PDF/X-4 pre tlačiarne (ISO 15930-7).</summary>
    PdfX4,

    /// <summary>Strany ako obrázky PNG.</summary>
    Png,

    /// <summary>Strany ako obrázky JPEG.</summary>
    Jpeg,

    /// <summary>Všetky strany v jednom obrázku TIFF.</summary>
    Tiff,
}

/// <summary>Vlastnosti formátov výstupu.</summary>
public static class FormatyVystupu
{
    public static bool JePdf(this FormatVystupu format) => format is FormatVystupu.Pdf or FormatVystupu.PdfA2b or FormatVystupu.PdfA3b or FormatVystupu.PdfX4;

    /// <summary>Formát podľa normy (PDF/A, PDF/X) – šifrovanie a heslo norma nepovoľuje.</summary>
    public static bool JeNorma(this FormatVystupu format) => format.JePdf() && format != FormatVystupu.Pdf;

    public static string Pripona(this FormatVystupu format) => format switch
    {
        FormatVystupu.Png => ".png",
        FormatVystupu.Jpeg => ".jpg",
        FormatVystupu.Tiff => ".tif",
        _ => ".pdf",
    };

    /// <summary>Krátky názov formátu.</summary>
    public static string Nazov(this FormatVystupu format) => format switch
    {
        FormatVystupu.PdfA2b => "PDF/A-2b",
        FormatVystupu.PdfA3b => "PDF/A-3b",
        FormatVystupu.PdfX4 => "PDF/X-4",
        FormatVystupu.Png => "PNG",
        FormatVystupu.Jpeg => "JPEG",
        FormatVystupu.Tiff => "TIFF",
        _ => "PDF",
    };

    /// <summary>Filter okna Uložiť ako.</summary>
    public static string Filter(this FormatVystupu format) => format switch
    {
        FormatVystupu.Png => Preklad.T("Obrázky PNG") + " (*.png)|*.png",
        FormatVystupu.Jpeg => Preklad.T("Obrázky JPEG") + " (*.jpg;*.jpeg)|*.jpg;*.jpeg",
        FormatVystupu.Tiff => Preklad.T("Obrázok TIFF") + " (*.tif;*.tiff)|*.tif;*.tiff",
        _ => "PDF (*.pdf)|*.pdf",
    };

    /// <summary>Na čo sa formát hodí – popis pod výberom formátu.</summary>
    public static string Popis(this FormatVystupu format) => format switch
    {
        FormatVystupu.PdfA2b => Preklad.T("Archívne PDF podľa normy ISO 19005-2 – písma aj farby sú v súbore, otvorí sa rovnako aj o desiatky rokov. Šifrovanie a heslo norma nepovoľuje."),
        FormatVystupu.PdfA3b => Preklad.T("Archívne PDF s prílohou – v PDF je vložený aj dokument Word s rovnakým obsahom na ďalšie úpravy. Šifrovanie a heslo norma nepovoľuje."),
        FormatVystupu.PdfX4 => Preklad.T("PDF pre tlačiareň a reklamné štúdio (ISO 15930-7) s farebným profilom pre ofsetovú tlač. Šifrovanie a heslo norma nepovoľuje."),
        FormatVystupu.Png => Preklad.T("Každá strana ako samostatný obrázok PNG (300 dpi), napr. na vloženie do Wordu. Text sa z obrázka nedá kopírovať."),
        FormatVystupu.Jpeg => Preklad.T("Každá strana ako samostatný obrázok JPEG (300 dpi) – menšie súbory, napr. na web alebo do e-mailu. Text sa z obrázka nedá kopírovať."),
        FormatVystupu.Tiff => Preklad.T("Všetky strany v jednom obrázku TIFF (300 dpi), napr. pre registratúru alebo archívny systém. Text sa z obrázka nedá kopírovať."),
        _ => Preklad.T("Bežné PDF – dá sa zašifrovať, chrániť heslom a obmedziť tlač či kopírovanie."),
    };
}

/// <summary>Súbor vytvorený zo zostavy (PDF alebo obrázok) – na uloženie alebo ako príloha e-mailu.</summary>
public sealed record SuborVystupu(string Nazov, byte[] Data, string Mime);

/// <summary>Súbor vložený do PDF/A-3b (dokument Word s rovnakým obsahom ako PDF).</summary>
/// <param name="Typ">typ MIME ako meno PDF (napr. „/application/pdf“)</param>
public sealed record PrilohaPdf(string Nazov, byte[] Data, string Typ, string Popis)
{
    private const string TypWord = "/application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public static PrilohaPdf Word(string nazov, byte[] data, string popis) => new(nazov, data, TypWord, popis);
}

/// <summary>
/// Voľby ukladania do súboru: formát (PDF, PDF/A, PDF/X, obrázky), zabezpečenie bežného PDF (šifrovanie, heslo
/// na otvorenie, zákazy) a rozmiestnenie a úpravy strán ako pri tlači. Text v PDF zostáva textom a písma sú vždy vložené.
/// Ukladajú sa ako JSON (kľúč <see cref="KlucNastavenia"/>), heslá nikdy.
/// </summary>
public sealed record VolbyPdf
{
    public const string KlucNastavenia = "tlac.pdf";

    public FormatVystupu Format { get; init; } = FormatVystupu.Pdf;

    /// <summary>Rozmiestnenie a úpravy strán (rovnaké voľby ako pri tlači).</summary>
    public VolbyTlace Rozvrh { get; init; } = new();

    public bool Sifrovat { get; init; }

    /// <summary>PDF sa otvorí len po zadaní hesla.</summary>
    public bool PouzitHeslo { get; init; }

    public bool ZakazTlace { get; init; }
    public bool ZakazKopirovania { get; init; }
    public bool ZakazUprav { get; init; }
    public bool ZakazPoznamok { get; init; }

    /// <summary>Heslo na otvorenie – len v pamäti do zatvorenia programu, do databázy sa neukladá.</summary>
    [JsonIgnore] public static string? Heslo { get; set; }

    /// <summary>Heslo na zrušenie zákazov (nepovinné) – tiež len v pamäti.</summary>
    [JsonIgnore] public static string? HesloVlastnika { get; set; }

    [JsonIgnore] public bool JePdf => Format.JePdf();

    /// <summary>Šifrovanie, heslo a zákazy sú možné len v bežnom PDF.</summary>
    [JsonIgnore] public bool MozeZabezpecit => Format == FormatVystupu.Pdf;

    [JsonIgnore] public bool MaZakazy => MozeZabezpecit && (ZakazTlace || ZakazKopirovania || ZakazUprav || ZakazPoznamok);

    /// <summary>Heslo na otvorenie sa naozaj použije.</summary>
    [JsonIgnore] public bool SHeslom => MozeZabezpecit && PouzitHeslo;

    /// <summary>PDF sa zašifruje (zvolené šifrovanie, heslo alebo niektorý zákaz).</summary>
    [JsonIgnore] public bool Zasifrovat => MozeZabezpecit && (Sifrovat || PouzitHeslo || MaZakazy);

    [JsonIgnore] public string Pripona => Format.Pripona();

    /// <summary>Strany treba poskladať nanovo; inak sa PDF uloží priamo z MigraDoc – aj s odkazmi a záložkami.</summary>
    [JsonIgnore]
    public bool ZlozitStrany => Rozvrh.Rozmiestnenie != Rozmiestnenie.Jedna || Rozvrh.OdtieneSivej || Rozvrh.Zosvetlit
                                || Rozvrh.Ramcek || Rozvrh.Dierovac || Rozvrh.Zvacsenie > 1.001;

    public static VolbyPdf Nacitaj() => Databaza.CitajJson<VolbyPdf>(KlucNastavenia)?.Opravene() ?? new VolbyPdf();

    /// <summary>Uloží voľby bez tlačiarne a počtu kópií (tie patria k tlači, nie k súboru).</summary>
    public void Uloz()
    {
        var volby = Opravene();
        Databaza.ZapisJson(KlucNastavenia, volby with { Rozvrh = volby.Rozvrh with { Tlaciaren = null, Kopie = 1 } });
    }

    private VolbyPdf Opravene() => this with
    {
        Format = Enum.IsDefined(Format) ? Format : FormatVystupu.Pdf,
        Rozvrh = (Rozvrh ?? new VolbyTlace()).Opravene(),
    };
}
