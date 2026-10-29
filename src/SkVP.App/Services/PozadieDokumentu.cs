using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;

namespace SkVP.Services;

/// <summary>
/// Voľba „Bez pozadia“ pri tlači a PDF: z dokumentu zmiznú farebné podklady (hlavička tabuľky, striedanie riadkov,
/// zvýraznené dni kalendára, výplne rámčekov). Zostane biely papier – text, čiary, obrázky a logo sa nemenia.
/// </summary>
public static class PozadieDokumentu
{
    /// <summary>Odstráni podklady priamo v dokumente a vráti ten istý dokument.</summary>
    public static Document Odstran(Document dokument)
    {
        foreach (var styl in dokument.Styles.OfType<Style>())
        {
            Skry(styl.Values.ParagraphFormat);
        }

        foreach (var sekcia in dokument.Sections.OfType<Section>())
        {
            var obsah = new List<DocumentElements?> { sekcia.Values.Elements };
            foreach (var cast in HlavickyAPaty(sekcia))
            {
                Skry(cast.Values.Format);
                obsah.Add(cast.Values.Elements);
            }

            foreach (var prvok in obsah.SelectMany(VsetkyPrvky))
            {
                BezPodkladu(prvok);
            }
        }

        return dokument;
    }

    private static IEnumerable<HeaderFooter> HlavickyAPaty(Section sekcia)
    {
        foreach (var skupina in new[] { sekcia.Values.Headers, sekcia.Values.Footers })
        {
            if (skupina is null)
            {
                continue;
            }

            foreach (var cast in new[] { skupina.Values.Primary, skupina.Values.FirstPage, skupina.Values.EvenPage })
            {
                if (cast is not null)
                {
                    yield return cast;
                }
            }
        }
    }

    /// <summary>Prvky vrátane vnorených – obsah buniek tabuliek a textových rámcov.</summary>
    private static IEnumerable<DocumentObject> VsetkyPrvky(DocumentElements? prvky)
    {
        if (prvky is null)
        {
            yield break;
        }

        foreach (var prvok in prvky.OfType<DocumentObject>())
        {
            yield return prvok;
            var vnorene = prvok switch
            {
                Table tabulka => tabulka.Rows.OfType<Row>().SelectMany(r => r.Cells.OfType<Cell>()).SelectMany(b => VsetkyPrvky(b.Values.Elements)),
                TextFrame ramec => VsetkyPrvky(ramec.Elements),
                _ => Enumerable.Empty<DocumentObject>(),
            };
            foreach (var vnoreny in vnorene)
            {
                yield return vnoreny;
            }
        }
    }

    private static void BezPodkladu(DocumentObject prvok)
    {
        switch (prvok)
        {
            case Paragraph odsek:
                Skry(odsek.Values.Format);
                break;

            case TextFrame ramec:
                ramec.FillFormat.Visible = false;
                break;

            case Table tabulka:
                Skry(tabulka.Values.Shading, tabulka.Values.Format);
                foreach (var stlpec in tabulka.Columns.OfType<Column>())
                {
                    Skry(stlpec.Values.Shading, stlpec.Values.Format);
                }

                foreach (var riadok in tabulka.Rows.OfType<Row>())
                {
                    Skry(riadok.Values.Shading, riadok.Values.Format);
                    foreach (var bunka in riadok.Cells.OfType<Cell>())
                    {
                        Skry(bunka.Values.Shading, bunka.Values.Format);
                    }
                }

                break;
        }
    }

    private static void Skry(Shading? podklad, ParagraphFormat? format)
    {
        Skry(podklad);
        Skry(format);
    }

    private static void Skry(ParagraphFormat? format) => Skry(format?.Values.Shading);

    private static void Skry(Shading? podklad)
    {
        if (podklad is not null)
        {
            podklad.Visible = false;
        }
    }
}
