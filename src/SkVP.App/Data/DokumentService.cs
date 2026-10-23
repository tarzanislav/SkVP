using System.IO;
using System.Security.Cryptography;
using SkVP.Services;

namespace SkVP.Data;

/// <summary>Verzia dokumentu v archíve.</summary>
public sealed record VerziaDokumentu(long Id, DateTime Vytvorene, DateTime? PovodneUpravene, string? Dovod, int Velkost);

/// <summary>
/// Dokumenty Word (.docx) uložené priamo v databáze – osnovy predmetov a charakteristiky oblastí – a ich archív verzií.
/// Pri každej úprave sa doterajšia verzia najprv zarchivuje, takže sa žiadna staršia osnova nestratí.
/// </summary>
public sealed class DokumentService
{
    public byte[]? NacitajData(long objektId)
    {
        using var db = Databaza.Otvor();
        return Databaza.Skalar(db, "SELECT data FROM dokument WHERE objekt_id = $id", ("$id", objektId)) as byte[];
    }

    public bool Existuje(long objektId)
    {
        using var db = Databaza.Otvor();
        return Databaza.Skalar(db, "SELECT 1 FROM dokument WHERE objekt_id = $id", ("$id", objektId)) is not null;
    }

    /// <summary>Načíta dokument do modelu textu (prázdny, ak objekt dokument nemá).</summary>
    public NacitanyDokument Nacitaj(long objektId)
    {
        var data = NacitajData(objektId);
        if (data is null || data.Length == 0)
        {
            return new NacitanyDokument(new List<Blok>(), Array.Empty<string>());
        }

        using var prud = new MemoryStream(data, false);
        return DocxCitac.Nacitaj(prud);
    }

    public string? Sha(long objektId)
    {
        using var db = Databaza.Otvor();
        return Databaza.Skalar(db, "SELECT sha256 FROM dokument WHERE objekt_id = $id", ("$id", objektId)) as string;
    }

    /// <summary>
    /// Uloží dokument. Ak je zadaný dôvod archivácie, doterajšia verzia (ak sa líši) sa najprv uloží do archívu.
    /// Vráti false, ak sa obsah nezmenil.
    /// </summary>
    public bool Uloz(long objektId, byte[] data, string? dovodArchivacie)
    {
        // overenie, že je to platný dokument Word, a výpočet textu na vyhľadávanie
        NacitanyDokument nacitany;
        using (var prud = new MemoryStream(data, false))
        {
            nacitany = DocxCitac.Nacitaj(prud);
        }

        var sha = Convert.ToHexString(SHA256.HashData(data));
        using var db = Databaza.Otvor();
        using var transakcia = db.BeginTransaction();
        var doterajsi = Databaza.Skalar(db, "SELECT sha256 FROM dokument WHERE objekt_id = $id", ("$id", objektId)) as string;
        if (doterajsi == sha)
        {
            return false;
        }

        if (doterajsi is not null && dovodArchivacie is not null)
        {
            Databaza.Vykonaj(db, """
                INSERT INTO verzia(objekt_id, data, text, sha256, povodne_upravene, vytvorene, dovod)
                SELECT objekt_id, data, text, sha256, upravene, $c, $d FROM dokument WHERE objekt_id = $id
                """, ("$id", objektId), ("$c", Databaza.Teraz()), ("$d", dovodArchivacie));
        }

        var teraz = Databaza.Teraz();
        Databaza.Vykonaj(db, """
            INSERT INTO dokument(objekt_id, data, text, dlzka, sha256, upravene) VALUES($id, $data, $t, $dl, $sha, $c)
            ON CONFLICT(objekt_id) DO UPDATE SET data = excluded.data, text = excluded.text, dlzka = excluded.dlzka,
                sha256 = excluded.sha256, upravene = excluded.upravene;
            UPDATE objekt SET upravene = $c WHERE id = $id;
            """, ("$id", objektId), ("$data", data), ("$t", DokumentOsnovy.Text(nacitany.Bloky)),
            ("$dl", DokumentOsnovy.DlzkaTextu(nacitany.Bloky)), ("$sha", sha), ("$c", teraz));
        transakcia.Commit();
        return true;
    }

    /// <summary>Uloží kópiu aktuálneho dokumentu do archívu verzií (dokument sa nemení). Vráti false, ak dokument nie je.</summary>
    public bool ArchivujAktualnu(long objektId, string dovod)
    {
        using var db = Databaza.Otvor();
        return Databaza.Vykonaj(db, """
            INSERT INTO verzia(objekt_id, data, text, sha256, povodne_upravene, vytvorene, dovod)
            SELECT objekt_id, data, text, sha256, upravene, $c, $d FROM dokument WHERE objekt_id = $id
            """, ("$id", objektId), ("$c", Databaza.Teraz()), ("$d", dovod)) > 0;
    }

    /// <summary>Uloží text z editora programu ako dokument Word.</summary>
    public bool UlozBloky(long objektId, IReadOnlyList<Blok> bloky, string? dovodArchivacie) =>
        Uloz(objektId, DocxVystup.Osnova(bloky), dovodArchivacie);

    // ---- archív ----

    public List<VerziaDokumentu> Verzie(long objektId)
    {
        var vysledok = new List<VerziaDokumentu>();
        using var db = Databaza.Otvor();
        using var prikaz = Databaza.Prikaz(db,
            "SELECT id, vytvorene, povodne_upravene, dovod, length(data) FROM verzia WHERE objekt_id = $id ORDER BY id DESC", ("$id", objektId));
        using var citac = prikaz.ExecuteReader();
        while (citac.Read())
        {
            vysledok.Add(new VerziaDokumentu(
                citac.GetInt64(0),
                DateTime.Parse(citac.GetString(1), System.Globalization.CultureInfo.InvariantCulture),
                citac.IsDBNull(2) ? null : DateTime.Parse(citac.GetString(2), System.Globalization.CultureInfo.InvariantCulture),
                citac.IsDBNull(3) ? null : citac.GetString(3),
                citac.GetInt32(4)));
        }

        return vysledok;
    }

    public byte[]? DataVerzie(long verziaId)
    {
        using var db = Databaza.Otvor();
        return Databaza.Skalar(db, "SELECT data FROM verzia WHERE id = $id", ("$id", verziaId)) as byte[];
    }

    /// <summary>Vráti staršiu verziu ako aktuálnu (aktuálna sa pritom zarchivuje).</summary>
    public void ObnovVerziu(long objektId, long verziaId)
    {
        var data = DataVerzie(verziaId) ?? throw new SkvpException("Verzia sa v archíve nenašla.");
        Uloz(objektId, data, "Pred obnovením staršej verzie");
    }
}
