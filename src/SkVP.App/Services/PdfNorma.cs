using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// PDF podľa normy: archívne PDF/A-2b a PDF/A-3b (s vloženým dokumentom Word) a PDF/X-4 pre tlačiarne. Doplní farebný
/// profil a výstupný zámer, metadáta XMP s označením normy a odstráni, čo norma zakazuje (interpolácia obrázkov,
/// prázdne položky Info). Farebné profily sú súčasťou Windows (sRGB a RSWOP), program ich so sebou nenesie.
/// </summary>
public static partial class PdfNorma
{
    private static readonly string PriecinokProfilov =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "spool", "drivers", "color");

    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    private static readonly XNamespace Pdf = "http://ns.adobe.com/pdf/1.3/";
    private static readonly XNamespace XmpMM = "http://ns.adobe.com/xap/1.0/mm/";
    private static readonly XNamespace PdfaId = "http://www.aiim.org/pdfa/ns/id/";
    private static readonly XNamespace PdfxId = "http://www.npes.org/pdfx/ns/id/";

    /// <summary>Upraví PDF podľa normy a vráti obsah súboru.</summary>
    /// <param name="priloha">len PDF/A-3b: vložený súbor (null = bez prílohy)</param>
    public static byte[] Uloz(PdfDocument pdf, FormatVystupu format, string nazov, PrilohaPdf? priloha)
    {
        var archiv = format is FormatVystupu.PdfA2b or FormatVystupu.PdfA3b;
        pdf.Version = archiv ? 17 : 16;

        // v slovníku Info aj v XMP musí byť ten istý čas – na celé sekundy
        var cas = DateTime.Now;
        cas = cas.AddTicks(-(cas.Ticks % TimeSpan.TicksPerSecond));
        UpravInfo(pdf, nazov, cas);
        BezInterpolacie(pdf);

        var srgb = Profil(pdf, "sRGB Color Space Profile.icm", zloziek: 3);
        if (archiv)
        {
            VystupnyZamer(pdf, "/GTS_PDFA1", srgb, "sRGB IEC61966-2.1", "sRGB IEC61966-2.1");
        }
        else
        {
            VystupnyZamer(pdf, "/GTS_PDFX", Profil(pdf, "RSWOP.icm", zloziek: 4), "CGATS TR 001", "U.S. Web Coated (SWOP)");
            pdf.Info.Elements.SetName("/Trapped", "/False");
            pdf.Info.Elements.SetString("/GTS_PDFXVersion", "PDF/X-4");
            OznacFarbyRgb(pdf, srgb);
        }

        if (format == FormatVystupu.PdfA3b && priloha is not null)
        {
            VlozSubor(pdf, priloha, cas);
        }

        using var prud = new MemoryStream();
        pdf.Save(prud, false);
        if (pdf.Internals.Catalog.Elements["/Metadata"] is not PdfReference metadata)
        {
            throw new InvalidOperationException("PDF nemá metadáta.");
        }

        return NahradObjekt(prud.ToArray(), metadata.ObjectNumber, metadata.GenerationNumber, Metadata(pdf.Info, format, cas));
    }

    private static void UpravInfo(PdfDocument pdf, string nazov, DateTime cas)
    {
        pdf.Info.CreationDate = cas;
        pdf.Info.ModificationDate = cas;
        if (string.IsNullOrWhiteSpace(pdf.Info.Title))
        {
            pdf.Info.Title = nazov;
        }

        // prázdne položky by sa v XMP nezhodovali
        foreach (var kluc in new[] { "/Author", "/Subject", "/Keywords" })
        {
            if (pdf.Info.Elements.GetString(kluc).Length == 0)
            {
                pdf.Info.Elements.Remove(kluc);
            }
        }
    }

    private static void BezInterpolacie(PdfDocument pdf)
    {
        foreach (var slovnik in pdf.Internals.GetAllObjects().OfType<PdfDictionary>())
        {
            if (slovnik.Elements.GetName("/Subtype") == "/Image")
            {
                slovnik.Elements.Remove("/Interpolate");
            }
        }
    }

    /// <summary>Farebný profil ICC zo systému Windows ako prúd v PDF.</summary>
    private static PdfDictionary Profil(PdfDocument pdf, string subor, int zloziek)
    {
        var cesta = Path.Combine(PriecinokProfilov, subor);
        if (!File.Exists(cesta))
        {
            throw new SkvpException(Preklad.F($"V systéme Windows chýba farebný profil {subor} – PDF podľa normy sa nedá vytvoriť. Uložte bežné PDF."));
        }

        var profil = Pridaj(pdf, new PdfDictionary(pdf));
        profil.CreateStream(File.ReadAllBytes(cesta));
        profil.Elements.SetInteger("/N", zloziek);
        return profil;
    }

    /// <summary>Výstupný zámer (OutputIntent) – pre aké farebné podmienky je dokument určený.</summary>
    private static void VystupnyZamer(PdfDocument pdf, string druh, PdfDictionary profil, string podmienka, string popis)
    {
        var zamer = Pridaj(pdf, new PdfDictionary(pdf));
        zamer.Elements.SetName("/Type", "/OutputIntent");
        zamer.Elements.SetName("/S", druh);
        zamer.Elements.SetString("/OutputConditionIdentifier", podmienka);
        zamer.Elements.SetString("/OutputCondition", popis);
        zamer.Elements.SetString("/Info", popis);
        zamer.Elements.SetString("/RegistryName", "http://www.color.org");
        zamer.Elements.SetReference("/DestOutputProfile", profil);
        pdf.Internals.Catalog.Elements["/OutputIntents"] = Pole(pdf, zamer.Reference!);
    }

    /// <summary>
    /// PDF/X-4 s výstupom CMYK: farby RGB zo zostavy sa na každej strane označia profilom sRGB (predvolený farebný
    /// priestor aj skupina priehľadnosti – inak by PDFsharp pri ukladaní doplnil neoznačené DeviceRGB).
    /// </summary>
    private static void OznacFarbyRgb(PdfDocument pdf, PdfDictionary srgb)
    {
        var rgb = Pridaj(pdf, Pole(pdf, new PdfName("/ICCBased"), srgb.Reference!));
        foreach (var strana in pdf.Pages)
        {
            strana.TrimBox = strana.MediaBox;
            var zdroje = strana.Resources.Elements;
            if (zdroje.GetDictionary("/ColorSpace") is not { } priestory)
            {
                zdroje["/ColorSpace"] = priestory = new PdfDictionary(pdf);
            }

            priestory.Elements.SetReference("/DefaultRGB", rgb);
            var skupina = new PdfDictionary(pdf);
            skupina.Elements.SetName("/S", "/Transparency");
            skupina.Elements.SetReference("/CS", rgb);
            strana.Elements["/Group"] = skupina;
        }
    }

    /// <summary>Vložený súbor podľa PDF/A-3 (/AF a strom /EmbeddedFiles) – viditeľný ako príloha v prehliadači PDF.</summary>
    private static void VlozSubor(PdfDocument pdf, PrilohaPdf priloha, DateTime cas)
    {
        var parametre = new PdfDictionary(pdf);
        parametre.Elements.SetInteger("/Size", priloha.Data.Length);
        parametre.Elements.SetDateTime("/ModDate", cas);
        var subor = Pridaj(pdf, new PdfDictionary(pdf));
        subor.CreateStream(priloha.Data);
        subor.Elements.SetName("/Type", "/EmbeddedFile");
        subor.Elements.SetName("/Subtype", priloha.Typ);
        subor.Elements["/Params"] = parametre;

        var prudy = new PdfDictionary(pdf);
        prudy.Elements.SetReference("/F", subor);
        prudy.Elements.SetReference("/UF", subor);
        var specifikacia = Pridaj(pdf, new PdfDictionary(pdf));
        specifikacia.Elements.SetName("/Type", "/Filespec");
        specifikacia.Elements["/F"] = new PdfString(Ascii(priloha.Nazov));
        specifikacia.Elements["/UF"] = new PdfString(priloha.Nazov, PdfStringEncoding.Unicode);
        specifikacia.Elements["/Desc"] = new PdfString(priloha.Popis, PdfStringEncoding.Unicode);
        specifikacia.Elements.SetName("/AFRelationship", "/Source");
        specifikacia.Elements["/EF"] = prudy;

        var katalog = pdf.Internals.Catalog.Elements;
        katalog["/AF"] = Pole(pdf, specifikacia.Reference!);
        if (katalog.GetDictionary("/Names") is not { } mena)
        {
            katalog["/Names"] = mena = new PdfDictionary(pdf);
        }

        var strom = new PdfDictionary(pdf);
        strom.Elements["/Names"] = Pole(pdf, new PdfString(priloha.Nazov, PdfStringEncoding.Unicode), specifikacia.Reference!);
        mena.Elements["/EmbeddedFiles"] = strom;
    }

    private static T Pridaj<T>(PdfDocument pdf, T objekt) where T : PdfObject
    {
        pdf.Internals.AddObject(objekt);
        return objekt;
    }

    private static PdfArray Pole(PdfDocument pdf, params PdfItem[] polozky)
    {
        var pole = new PdfArray(pdf);
        foreach (var polozka in polozky)
        {
            pole.Elements.Add(polozka);
        }

        return pole;
    }

    /// <summary>Názov súboru len zo znakov ASCII (diakritika sa odstráni, ostatné znaky nahradí „_“).</summary>
    private static string Ascii(string text) =>
        string.Concat(text.Normalize(NormalizationForm.FormD)
            .Where(znak => CharUnicodeInfo.GetUnicodeCategory(znak) != UnicodeCategory.NonSpacingMark)
            .Select(znak => znak is >= ' ' and <= '~' ? znak : '_'));

    /// <summary>Metadáta XMP zhodné so slovníkom Info, s označením normy (pdfaid alebo pdfxid).</summary>
    private static string Metadata(PdfDocumentInformation info, FormatVystupu format, DateTime cas)
    {
        var datum = cas.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);
        var pdfX = format == FormatVystupu.PdfX4;
        static XElement Alt(XName meno, string text) =>
            new(meno, new XElement(Rdf + "Alt", new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), text)));

        var popis = new XElement(Rdf + "Description",
            new XAttribute(Rdf + "about", ""),
            new XAttribute(XNamespace.Xmlns + "dc", Dc),
            new XAttribute(XNamespace.Xmlns + "xmp", Xmp),
            new XAttribute(XNamespace.Xmlns + "pdf", Pdf),
            new XAttribute(XNamespace.Xmlns + "xmpMM", XmpMM),
            pdfX ? new XAttribute(XNamespace.Xmlns + "pdfxid", PdfxId) : new XAttribute(XNamespace.Xmlns + "pdfaid", PdfaId),
            new XElement(Dc + "format", "application/pdf"),
            Alt(Dc + "title", info.Title),
            info.Author.Length > 0 ? new XElement(Dc + "creator", new XElement(Rdf + "Seq", new XElement(Rdf + "li", info.Author))) : null,
            info.Subject.Length > 0 ? Alt(Dc + "description", info.Subject) : null,
            new XElement(Xmp + "CreatorTool", info.Creator),
            new XElement(Xmp + "CreateDate", datum),
            new XElement(Xmp + "ModifyDate", datum),
            new XElement(Xmp + "MetadataDate", datum),
            new XElement(Pdf + "Producer", info.Producer),
            info.Keywords.Length > 0 ? new XElement(Pdf + "Keywords", info.Keywords) : null,
            new XElement(XmpMM + "DocumentID", $"uuid:{Guid.NewGuid()}"),
            new XElement(XmpMM + "InstanceID", $"uuid:{Guid.NewGuid()}"),
            new XElement(XmpMM + "VersionID", "1"),
            new XElement(XmpMM + "RenditionClass", "default"),
            pdfX
                ? new object[] { new XElement(Pdf + "Trapped", "False"), new XElement(PdfxId + "GTS_PDFXVersion", "PDF/X-4") }
                : new object[] { new XElement(PdfaId + "part", format == FormatVystupu.PdfA3b ? "3" : "2"), new XElement(PdfaId + "conformance", "B") });
        var meta = new XElement(XName.Get("xmpmeta", "adobe:ns:meta/"), new XAttribute(XNamespace.Xmlns + "x", "adobe:ns:meta/"),
            new XElement(Rdf + "RDF", new XAttribute(XNamespace.Xmlns + "rdf", Rdf), popis));

        // rezerva medzier umožní neskôr upraviť metadáta na mieste; „w“ = paket sa smie prepisovať
        return "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n" + meta.ToString(SaveOptions.DisableFormatting)
               + "\n" + new string(' ', 1500) + "\n<?xpacket end=\"w\"?>";
    }

    /// <summary>
    /// Nová verzia jedného objektu prírastkovou aktualizáciou (ako pri podpisovaní PDF): objekt, tabuľka odkazov
    /// a trailer sa pripíšu na koniec súboru. PDFsharp zapisuje vlastné XMP bez označenia normy – takto ho nahradí.
    /// </summary>
    private static byte[] NahradObjekt(byte[] pdf, int cislo, int generacia, string xmp)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var zaciatok = text.LastIndexOf("trailer", StringComparison.Ordinal);
        var trailer = zaciatok < 0 ? Match.Empty : Trailer().Match(text, zaciatok);
        if (!trailer.Success)
        {
            throw new InvalidOperationException("PDF nemá klasickú tabuľku odkazov.");
        }

        var slovnik = trailer.Groups["slovnik"].Value;
        string Polozka(string kluc, string vzor) =>
            Regex.Match(slovnik, $@"/{kluc}\s*({vzor})", RegexOptions.Singleline) is { Success: true } m ? $"/{kluc} {m.Groups[1].Value}" : "";

        using var vystup = new MemoryStream(pdf.Length + xmp.Length + 512);
        vystup.Write(pdf);
        void Zapis(string ascii) => vystup.Write(Encoding.ASCII.GetBytes(ascii));
        if (pdf.Length > 0 && pdf[^1] != '\n')
        {
            Zapis("\n");
        }

        var data = Encoding.UTF8.GetBytes(xmp);
        var objekt = vystup.Position;
        Zapis($"{cislo} {generacia} obj\n<</Type/Metadata/Subtype/XML/Length {data.Length}>>\nstream\n");
        vystup.Write(data);
        Zapis("\nendstream\nendobj\n");
        var tabulka = vystup.Position;
        Zapis($"xref\n{cislo} 1\n{objekt:D10} {generacia:D5} n\r\n");
        Zapis("trailer\n<<" + Polozka("Size", @"\d+") + Polozka("Root", @"\d+\s+\d+\s+R") + Polozka("Info", @"\d+\s+\d+\s+R")
              + Polozka("ID", @"\[[^\]]*\]") + $"/Prev {trailer.Groups["xref"].Value}>>\nstartxref\n{tabulka}\n%%EOF\n");
        return vystup.ToArray();
    }

    /// <summary>Trailer: jeho slovník a poloha tabuľky odkazov za „startxref“ (medzi nimi môžu byť komentáre).</summary>
    [GeneratedRegex(@"\Gtrailer\s*<<(?<slovnik>.*?)>>(?:\s|%[^\r\n]*)*startxref\s+(?<xref>\d+)\s*%%EOF\s*\z", RegexOptions.Singleline)]
    private static partial Regex Trailer();
}
