using System.Text.Json;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>Údaje dodané s programom (JSON v programe – ŠVP, učebný plán, testovacia sada).</summary>
public static class ZabudovaneZdroje
{
    private static readonly JsonSerializerOptions Moznosti = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Načíta zabudovaný zdroj; ak v programe chýba, vyhodí chybu so zrozumiteľnou správou.</summary>
    public static T Json<T>(string zdroj, string chyba) where T : new()
    {
        using var prud = typeof(ZabudovaneZdroje).Assembly.GetManifestResourceStream(zdroj) ?? throw new SkvpException(chyba);
        return JsonSerializer.Deserialize<T>(prud, Moznosti) ?? new T();
    }
}
