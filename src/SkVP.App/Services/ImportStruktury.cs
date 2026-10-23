using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// Vzdelávacie oblasti a predmety z ďalších zdrojov: webová stránka (online), diktovaný alebo vložený text, HTML, XML
/// a JSON – a export do HTML, XML a JSON (dá sa znova naimportovať). Výsledok je vždy <see cref="UcebnyPlan"/>.
/// </summary>
public static partial class ImportStruktury
{
    public const string FilterSuborov =
        "Excel, Word, PDF, HTML, XML, JSON, text (*.xlsx;*.docx;*.pdf;*.html;*.htm;*.xml;*.json;*.txt)|*.xlsx;*.docx;*.pdf;*.html;*.htm;*.xml;*.json;*.txt";

    // ---- HTML ----

    [GeneratedRegex(@"<(script|style|head|noscript|template)\b.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NepotrebneTagy();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Komentare();

    [GeneratedRegex(@"<(/?)([a-zA-Z][a-zA-Z0-9]*)\b[^>]*?(/?)>", RegexOptions.Singleline)]
    private static partial Regex Tag();

    /// <summary>
    /// HTML: tabuľky (stĺpce oblasť – predmet), inak nadpisy a tučné riadky = oblasti, položky zoznamov a odseky = predmety.
    /// Rovnaké pravidlá ako pri Worde, takže funguje aj na stránke školy alebo exporte z iného programu.
    /// </summary>
    public static UcebnyPlan ZHtml(string html) => StrukturaOsnov.ZBlokov(BlokyZHtml(html));

    /// <summary>Jednoduchý prevod HTML na bloky (nadpisy, odseky, zoznamy, tabuľky) – bez externej knižnice.</summary>
    public static List<Blok> BlokyZHtml(string html)
    {
        html = Komentare().Replace(NepotrebneTagy().Replace(html, " "), " ");
        var bloky = new List<Blok>();
        var text = new StringBuilder();
        var useky = new List<Usek>();
        var tucne = 0;
        int? nadpis = null;
        List<IReadOnlyList<Blok>>? polozky = null;
        var cislovany = false;
        List<IReadOnlyList<Bunka>>? riadky = null;
        List<Bunka>? riadok = null;
        var hlavickaBunky = false;
        var vBunke = false;
        var vPolozke = false;

        void PridajText()
        {
            // zalomenie <br> (\u0002) zostane novým riadkom, ostatné medzery a zalomenia zdroja sa zlúčia
            var t = Regex.Replace(WebUtility.HtmlDecode(text.ToString()), @"[^\S\u0002]+", " ").Replace(" \u0002", "\u0002").Replace("\u0002 ", "\u0002").Replace('\u0002', '\n');
            text.Clear();
            if (t.Trim().Length > 0)
            {
                useky.Add(new Usek(t, Tucne: tucne > 0));
            }
        }

        string Uzavri()
        {
            PridajText();
            var t = string.Concat(useky.Select(u => u.Text)).Trim();
            var tucnyCely = useky.Count > 0 && useky.Where(u => u.Text.Trim().Length > 0).All(u => u.Tucne);
            useky.Clear();
            return tucnyCely && t.Length > 0 ? "\u0001" + t : t;
        }

        Blok? Odsek(string t) => t.Length == 0 ? null
            : t[0] == '\u0001' ? new Odsek(new[] { new Usek(t[1..], Tucne: true) })
            : new Odsek(new[] { new Usek(t) });

        void UzavriOdsek()
        {
            var t = Uzavri();
            if (t.Length == 0)
            {
                return;
            }

            if (vBunke || vPolozke)
            {
                // text bunky alebo položky sa zbiera ďalej
                useky.Add(new Usek(t.TrimStart('\u0001') + "\n", Tucne: t[0] == '\u0001'));
                return;
            }

            bloky.Add(nadpis is int u ? new Nadpis(u, t.TrimStart('\u0001')) : Odsek(t)!);
        }

        var pozicia = 0;
        foreach (Match m in Tag().Matches(html))
        {
            text.Append(html, pozicia, m.Index - pozicia);
            pozicia = m.Index + m.Length;
            var konci = m.Groups[1].Value == "/";
            var meno = m.Groups[2].Value.ToLowerInvariant();
            switch (meno)
            {
                case "b" or "strong" when !konci:
                    PridajText();
                    tucne++;
                    break;
                case "b" or "strong" when konci:
                    PridajText();
                    tucne = Math.Max(0, tucne - 1);
                    break;
                case "br":
                    text.Append('\u0002');
                    break;
                case "h1" or "h2" or "h3" or "h4" or "h5" or "h6":
                    UzavriOdsek();
                    nadpis = konci ? null : meno[1] - '0';
                    break;
                case "p" or "div" or "section" or "article" or "header" or "footer" or "main" or "dt" or "dd" or "caption":
                    UzavriOdsek();
                    break;
                case "ul" or "ol" when !konci:
                    UzavriOdsek();
                    polozky ??= new List<IReadOnlyList<Blok>>();
                    cislovany = meno == "ol";
                    break;
                case "ul" or "ol" when konci:
                    if (polozky is { Count: > 0 })
                    {
                        bloky.Add(new Zoznam(cislovany, polozky));
                    }

                    polozky = null;
                    break;
                case "li" when !konci:
                    vPolozke = true;
                    UzavriOdsek();
                    break;
                case "li" when konci:
                {
                    var t = Uzavri().TrimStart('\u0001').Trim();
                    vPolozke = false;
                    if (t.Length > 0)
                    {
                        (polozky ??= new()).Add(new Blok[] { new Odsek(new[] { new Usek(t.Replace("\n", " ").Trim()) }) });
                    }

                    break;
                }

                case "table" when !konci:
                    UzavriOdsek();
                    riadky = new();
                    break;
                case "table" when konci:
                    if (riadky is { Count: > 0 })
                    {
                        bloky.Add(new Tabulka(riadky, riadky.Max(r => r.Count)));
                    }

                    riadky = null;
                    break;
                case "tr" when !konci:
                    riadok = new();
                    break;
                case "tr" when konci:
                    if (riadok is { Count: > 0 })
                    {
                        riadky?.Add(riadok);
                    }

                    riadok = null;
                    break;
                case "td" or "th" when !konci:
                    PridajText();
                    useky.Clear();
                    vBunke = true;
                    hlavickaBunky = meno == "th";
                    if (hlavickaBunky)
                    {
                        tucne++;
                    }

                    break;
                case "td" or "th" when konci:
                {
                    var t = Uzavri().TrimStart('\u0001');
                    vBunke = false;
                    if (meno == "th")
                    {
                        tucne = Math.Max(0, tucne - 1);
                    }

                    var odseky = t.Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0)
                        .Select(x => (Blok)new Odsek(new[] { new Usek(x) })).ToList();
                    riadok?.Add(new Bunka(odseky, Hlavicka: hlavickaBunky));
                    break;
                }
            }
        }

        text.Append(html, pozicia, html.Length - pozicia);
        UzavriOdsek();
        if (polozky is { Count: > 0 })
        {
            bloky.Add(new Zoznam(cislovany, polozky));
        }

        return bloky;
    }

    // ---- XML ----

    /// <summary>
    /// XML: prvky „oblast“ (area) s názvom v atribúte alebo prvku „nazov“ (name) a v nich prvky „predmet“ (subject).
    /// Iný tvar: dve úrovne pod koreňom (oblasť → predmety), názov z atribútu alebo textu.
    /// </summary>
    public static UcebnyPlan ZXml(string xml)
    {
        XDocument dokument;
        try
        {
            dokument = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new SkvpException($"Súbor nie je platný XML.\n\n{ex.Message}");
        }

        static string Kluc(XName meno) => ImportOsnov.Kluc(meno.LocalName);
        static bool JeOblast(XElement e) => Kluc(e.Name) is var k && (k.StartsWith("oblast", StringComparison.Ordinal) || k is "area" or "vzdelavaciaoblast");
        static bool JePredmet(XElement e) => Kluc(e.Name) is var k && (k.StartsWith("predmet", StringComparison.Ordinal) || k is "subject" or "vyucovacipredmet");

        static string Nazov(XElement e)
        {
            var atribut = e.Attributes().FirstOrDefault(a => ImportOsnov.Kluc(a.Name.LocalName) is "nazov" or "name" or "title" or "nazev");
            if (atribut is not null)
            {
                return atribut.Value.Trim();
            }

            var prvok = e.Elements().FirstOrDefault(x => Kluc(x.Name) is "nazov" or "name" or "title" or "nazev");
            if (prvok is not null)
            {
                return prvok.Value.Trim();
            }

            return string.Concat(e.Nodes().OfType<XText>().Select(t => t.Value)).Trim();
        }

        var plan = new UcebnyPlan { Nazov = dokument.Root?.Attribute("nazov")?.Value, Typ = dokument.Root?.Attribute("typ")?.Value };
        var oblasti = dokument.Descendants().Where(JeOblast).ToList();
        if (oblasti.Count > 0)
        {
            foreach (var o in oblasti)
            {
                plan.Oblasti.Add(new OblastPlanu
                {
                    Nazov = Nazov(o),
                    Predmety = o.Descendants().Where(JePredmet).Select(Nazov).Where(n => n.Length > 0).ToList(),
                });
            }
        }
        else if (dokument.Root is { } koren)
        {
            foreach (var o in koren.Elements().Where(e => e.HasElements))
            {
                var predmety = o.Elements().Where(e => !(Kluc(e.Name) is "nazov" or "name" or "title")).Select(Nazov).Where(n => n.Length > 0).ToList();
                plan.Oblasti.Add(new OblastPlanu { Nazov = Nazov(o), Predmety = predmety });
            }
        }

        return Over(plan);
    }

    // ---- JSON ----

    /// <summary>
    /// JSON: {"oblasti": [{"nazov": "…", "predmety": ["…" | {"nazov": "…"}]}]} (aj anglické kľúče areas/name/subjects),
    /// pole oblastí alebo objekt {"Oblasť": ["predmet", …]}.
    /// </summary>
    public static UcebnyPlan ZJson(string json)
    {
        JsonDocument dokument;
        try
        {
            dokument = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException ex)
        {
            throw new SkvpException($"Súbor nie je platný JSON.\n\n{ex.Message}");
        }

        using (dokument)
        {
            static JsonElement? Vlastnost(JsonElement e, params string[] mena)
            {
                if (e.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                foreach (var p in e.EnumerateObject())
                {
                    if (mena.Contains(ImportOsnov.Kluc(p.Name)))
                    {
                        return p.Value;
                    }
                }

                return null;
            }

            static string Text(JsonElement e) => e.ValueKind switch
            {
                JsonValueKind.String => e.GetString()!.Trim(),
                JsonValueKind.Object => Vlastnost(e, "nazov", "name", "title", "nazev") is { ValueKind: JsonValueKind.String } n ? n.GetString()!.Trim() : "",
                _ => "",
            };

            static OblastPlanu Oblast(JsonElement e) => new()
            {
                Nazov = Text(e),
                Predmety = Vlastnost(e, "predmety", "subjects", "predmet", "subject") is { ValueKind: JsonValueKind.Array } p
                    ? p.EnumerateArray().Select(Text).Where(t => t.Length > 0).ToList()
                    : new List<string>(),
            };

            var koren = dokument.RootElement;
            var plan = new UcebnyPlan();
            if (Vlastnost(koren, "oblasti", "areas", "vzdelavacieoblasti", "vzdelavacie oblasti") is { ValueKind: JsonValueKind.Array } pole)
            {
                plan.Nazov = Vlastnost(koren, "nazov", "name") is { ValueKind: JsonValueKind.String } n ? n.GetString() : null;
                plan.Typ = Vlastnost(koren, "typ", "type") is { ValueKind: JsonValueKind.String } t ? t.GetString() : null;
                plan.Oblasti.AddRange(pole.EnumerateArray().Select(Oblast));
            }
            else if (koren.ValueKind == JsonValueKind.Array)
            {
                plan.Oblasti.AddRange(koren.EnumerateArray().Select(Oblast));
            }
            else if (koren.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in koren.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Array))
                {
                    plan.Oblasti.Add(new OblastPlanu { Nazov = p.Name.Trim(), Predmety = p.Value.EnumerateArray().Select(Text).Where(t => t.Length > 0).ToList() });
                }
            }

            return Over(plan);
        }
    }

    // ---- diktovaný alebo vložený text ----

    /// <summary>
    /// Diktovaný alebo napísaný text: „Jazyk a komunikácia: slovenský jazyk a literatúra, anglický jazyk. Matematika a práca
    /// s informáciami: matematika, informatika.“ – oblasť pred dvojbodkou (alebo slovom „predmety“), predmety oddelené
    /// čiarkou, bodkočiarkou alebo novým riadkom, oblasti bodkou alebo novým riadkom. Prvé písmeno sa zväčší.
    /// </summary>
    public static UcebnyPlan ZDiktovania(string text)
    {
        var riadky = new List<(string A, string B)>();
        var zname = UcebnyPlanService.Vstavany().Oblasti.Select(o => o.Nazov)
            .Concat(new ObjektService().Oblasti().Select(o => o.Nazov)).ToList();
        var normalizovany = Regex.Replace(text.Replace("\r", ""), @"\b(dvojbodka|dvojbodku)\b", ":", RegexOptions.IgnoreCase);
        normalizovany = Regex.Replace(normalizovany, @"\b(čiarka|čiarku)\b", ",", RegexOptions.IgnoreCase);
        normalizovany = Regex.Replace(normalizovany, @"\b(bodka|bodku|nový riadok|nova oblasť|nová oblasť|ďalšia oblasť)\b", "\n", RegexOptions.IgnoreCase);
        normalizovany = Regex.Replace(normalizovany, @"\s+(predmety|predmet)\s*[:\-–]?\s*", ":", RegexOptions.IgnoreCase);
        foreach (var veta in Regex.Split(normalizovany, @"(?<=[.!?])\s+|\n"))
        {
            var cast = veta.Trim().TrimEnd('.', '!', '?').Trim();
            if (cast.Length == 0)
            {
                continue;
            }

            var dvojbodka = cast.IndexOf(':');
            if (dvojbodka >= 0)
            {
                var oblast = Velke(Regex.Replace(cast[..dvojbodka], @"^\s*(vzdelávacia\s+)?oblasť\s+", "", RegexOptions.IgnoreCase).Trim());
                riadky.Add((Znama(oblast, zname), ""));
                foreach (var predmet in Rozdel(cast[(dvojbodka + 1)..]))
                {
                    riadky.Add(("", predmet));
                }
            }
            else if (zname.FirstOrDefault(z => ImportOsnov.Kluc(z) == ImportOsnov.Kluc(cast)) is { } znama)
            {
                riadky.Add((znama, ""));
            }
            else
            {
                foreach (var predmet in Rozdel(cast))
                {
                    riadky.Add(("", predmet));
                }
            }
        }

        if (riadky.Count == 0)
        {
            throw new SkvpException("V texte nie sú žiadne oblasti ani predmety. Diktujte napr. „Človek a príroda: fyzika, chémia, biológia.“");
        }

        return UcebnyPlanService.Rozober(riadky);

        static IEnumerable<string> Rozdel(string predmety) =>
            predmety.Split(',', ';', '\n').Select(p => Velke(p.Trim(' ', '•', '-', '–'))).Where(p => p.Length > 0);

        static string Znama(string oblast, IReadOnlyList<string> zname) =>
            zname.FirstOrDefault(z => ImportOsnov.Kluc(z) == ImportOsnov.Kluc(oblast)) ?? oblast;
    }

    private static string Velke(string text) => text.Length == 0 ? text : char.ToUpper(text[0], System.Globalization.CultureInfo.GetCultureInfo("sk-SK")) + text[1..];

    // ---- webová stránka ----

    private static readonly HttpClient Klient = new() { Timeout = TimeSpan.FromSeconds(40) };

    static ImportStruktury() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Stiahne webovú stránku alebo súbor (PDF, Word, Excel, JSON, XML, HTML) a rozpozná oblasti a predmety.</summary>
    public static async Task<UcebnyPlan> ZWebuAsync(string adresa, CancellationToken zrusenie = default)
    {
        adresa = adresa.Trim();
        if (!adresa.Contains("://", StringComparison.Ordinal))
        {
            adresa = "https://" + adresa;
        }

        if (!Uri.TryCreate(adresa, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new SkvpException("Zadajte adresu webovej stránky, napr. https://www.skola.sk/ucebny-plan.");
        }

        HttpResponseMessage odpoved;
        try
        {
            using var poziadavka = new HttpRequestMessage(HttpMethod.Get, uri);
            poziadavka.Headers.UserAgent.ParseAdd($"SkVP/{AktualizaciaService.AktualnaVerzia}");
            odpoved = await Klient.SendAsync(poziadavka, zrusenie);
            odpoved.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            throw new SkvpException($"Stránku sa nepodarilo načítať. Skontrolujte adresu a pripojenie k internetu.\n\n{ex.Message}");
        }
        catch (TaskCanceledException) when (!zrusenie.IsCancellationRequested)
        {
            throw new SkvpException("Stránka neodpovedá (vypršal čas). Skúste to znova neskôr.");
        }

        using (odpoved)
        {
            var typ = odpoved.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
            var pripona = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            var data = await odpoved.Content.ReadAsByteArrayAsync(zrusenie);
            if (typ.Contains("pdf") || pripona is ".pdf" or ".docx" or ".xlsx" || typ.Contains("officedocument"))
            {
                var priponaSuboru = typ.Contains("pdf") || pripona == ".pdf" ? ".pdf"
                    : typ.Contains("spreadsheet") || pripona == ".xlsx" ? ".xlsx" : ".docx";
                var docasny = Path.Combine(Path.GetTempPath(), $"skvp_web_{Guid.NewGuid():N}{priponaSuboru}");
                await File.WriteAllBytesAsync(docasny, data, zrusenie);
                try
                {
                    return await Task.Run(() => StrukturaOsnov.NacitajZoSuboru(docasny), zrusenie);
                }
                finally
                {
                    File.Delete(docasny);
                }
            }

            var text = Dekoduj(data, odpoved.Content.Headers.ContentType?.CharSet);
            return typ.Contains("json") || pripona == ".json" ? ZJson(text)
                : (typ.Contains("xml") && !typ.Contains("html")) || pripona == ".xml" ? ZXml(text)
                : typ.StartsWith("text/plain", StringComparison.Ordinal) ? ZDiktovania(text)
                : ZHtml(text);
        }
    }

    private static string Dekoduj(byte[] data, string? znakovaSada)
    {
        try
        {
            if (znakovaSada is { Length: > 0 })
            {
                return Encoding.GetEncoding(znakovaSada.Trim('"')).GetString(data);
            }
        }
        catch (ArgumentException)
        {
            // neznáma znaková sada – UTF-8
        }

        return Encoding.UTF8.GetString(data);
    }

    /// <summary>Súbor HTML, XML, JSON alebo text (ostatné formáty číta <see cref="StrukturaOsnov.NacitajZoSuboru"/>).</summary>
    public static UcebnyPlan? ZTextovehoSuboru(string cesta)
    {
        var pripona = Path.GetExtension(cesta).ToLowerInvariant();
        if (pripona is not (".html" or ".htm" or ".xml" or ".json" or ".txt"))
        {
            return null;
        }

        var text = File.ReadAllText(cesta);
        return pripona switch
        {
            ".html" or ".htm" => ZHtml(text),
            ".xml" => ZXml(text),
            ".json" => ZJson(text),
            _ => ZDiktovania(text),
        };
    }

    private static UcebnyPlan Over(UcebnyPlan plan)
    {
        plan.Oblasti.RemoveAll(o => o.Nazov.Length == 0 && o.Predmety.Count == 0);
        foreach (var o in plan.Oblasti.Where(o => o.Nazov.Length == 0))
        {
            o.Nazov = "Ďalšie predmety";
        }

        if (plan.Oblasti.Count == 0)
        {
            throw new SkvpException("V súbore sa nenašli žiadne vzdelávacie oblasti a predmety.");
        }

        return plan;
    }

    // ---- export ----

    private static readonly JsonSerializerOptions MoznostiJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void UlozJson(UcebnyPlan plan, string cesta) =>
        File.WriteAllText(cesta, JsonSerializer.Serialize(new
        {
            nazov = plan.Nazov,
            typ = plan.Typ,
            oblasti = plan.Oblasti.Select(o => new { nazov = o.Nazov, predmety = o.Predmety }),
        }, MoznostiJson), new UTF8Encoding(false));

    public static void UlozXml(UcebnyPlan plan, string cesta)
    {
        var koren = new XElement("ucebnyPlan");
        if (plan.Nazov is { Length: > 0 } nazov)
        {
            koren.SetAttributeValue("nazov", nazov);
        }

        if (plan.Typ is { Length: > 0 } typ)
        {
            koren.SetAttributeValue("typ", typ);
        }

        koren.Add(plan.Oblasti.Select(o => new XElement("oblast", new XAttribute("nazov", o.Nazov),
            o.Predmety.Select(p => new XElement("predmet", p)))));
        new XDocument(new XDeclaration("1.0", "utf-8", null), koren).Save(cesta);
    }

    public static void UlozHtml(UcebnyPlan plan, string cesta)
    {
        static string E(string? t) => WebUtility.HtmlEncode(t ?? "");
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>\n<html lang=\"sk\">\n<head>\n<meta charset=\"utf-8\">");
        html.AppendLine($"<title>{E(plan.Nazov ?? "Vzdelávacie oblasti a predmety")}</title>");
        html.AppendLine("<style>body{font-family:'Segoe UI',sans-serif;margin:32px;color:#1f2933}table{border-collapse:collapse}" +
                        "th,td{border:1px solid #c9d1dc;padding:6px 12px;text-align:left;vertical-align:top}th{background:#e7ecfb}</style>");
        html.AppendLine("</head>\n<body>");
        html.AppendLine($"<h1>{E(plan.Nazov ?? "Vzdelávacie oblasti a predmety")}</h1>");
        if (plan.Typ is { Length: > 0 })
        {
            html.AppendLine($"<p>{E(plan.Typ)}</p>");
        }

        html.AppendLine("<table>\n<tr><th>Vzdelávacia oblasť</th><th>Predmety</th></tr>");
        foreach (var o in plan.Oblasti)
        {
            html.AppendLine($"<tr><td><strong>{E(o.Nazov)}</strong></td><td>{string.Join("<br>", o.Predmety.Select(E))}</td></tr>");
        }

        html.AppendLine("</table>\n</body>\n</html>");
        File.WriteAllText(cesta, html.ToString(), new UTF8Encoding(false));
    }
}
