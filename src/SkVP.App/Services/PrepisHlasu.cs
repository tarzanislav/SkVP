using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using SkVP.Data;

namespace SkVP.Services;

/// <summary>
/// Prepis reči do textu bez internetu: SkvpHlas.dll (whisper.cpp v1.9.5, MIT) zostavená v GitHub Actions zo zdrojového
/// kódu overeného commitu. Model je v programe naraz len jeden – načíta sa raz a zostane v pamäti, kým sa program
/// nezavrie alebo sa nezmení model. Prístup k nemu je len pod zámkom (knižnica nie je viacvláknová).
/// </summary>
public static class PrepisHlasu
{
    public const int VzorkovaFrekvencia = 16000;

    private const string Kniznica = "SkvpHlas.dll";
    private const int VelkostVystupu = 64 * 1024;

    /// <summary>Kratšia nahrávka sa neprepisuje (je to len klik alebo šum).</summary>
    private const int NajmenejVzoriek = VzorkovaFrekvencia / 4;

    // knižnica len z priečinka programu (nie z aktuálneho priečinka ani z PATH) – nedá sa podvrhnúť
    [DllImport(Kniznica, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode), DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    private static extern IntPtr skvp_hlas_otvor(string cesta);

    [DllImport(Kniznica, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    private static extern int skvp_hlas_prepis(IntPtr kontext, float[] zvuk, int vzoriek, byte[] jazyk, int vlakna, byte[] vyzva,
        byte[] vystup, int velkost);

    [DllImport(Kniznica, CallingConvention = CallingConvention.Cdecl), DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory)]
    private static extern void skvp_hlas_zavri(IntPtr kontext);

    private static readonly byte[] JazykUtf8 = Utf8(Diktovanie.Jazyk);
    private static readonly byte[] VyzvaUtf8 = Utf8(Diktovanie.Vyzva);
    private static readonly string KlucVyzvy = ImportOsnov.Kluc(Diktovanie.Vyzva);
    private static readonly string[] Smetie = { "[BLANK_AUDIO]", "[ Silence ]", "(ticho)", "[hudba]" };

    private static readonly SemaphoreSlim Zamok = new(1, 1);
    private static IntPtr _kontext;
    private static string? _cestaModelu;

    /// <summary>Počet vlákien prepisu – polovica logických jadier (zvyčajne fyzické jadrá), 2 až 8.</summary>
    public static int Vlakna => Math.Clamp(Environment.ProcessorCount / 2, 2, 8);

    /// <summary>Prečo sa prepis bez internetu nedá použiť (chýba knižnica alebo procesor nemá AVX2), inak null.</summary>
    public static string? Prekazka() =>
        !File.Exists(Path.Combine(AppContext.BaseDirectory, Kniznica))
            ? Preklad.T("Prepis bez internetu nie je v tejto inštalácii k dispozícii (chýba súčasť SkvpHlas.dll).")
            : !(Avx2.IsSupported && Fma.IsSupported)
                ? Preklad.T("Tento procesor nepodporuje inštrukcie AVX2 – prepis bez internetu by bol príliš pomalý. Použite hlasové písanie Windows.")
                : null;

    /// <summary>Knižnica sa dá načítať (samotest): otvorenie neexistujúceho modelu vráti chybu, program nespadne.</summary>
    public static bool KniznicaFunguje()
    {
        try
        {
            return skvp_hlas_otvor(Path.Combine(Path.GetTempPath(), "skvp_neexistujuci_model.bin")) == IntPtr.Zero;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return false;
        }
    }

    /// <summary>Prepíše zvuk (16 kHz mono) po slovensky. Model sa pri prvom použití načíta (niekoľko sekúnd).</summary>
    public static async Task<string> PrepisAsync(float[] zvuk, ModelHlasu model, CancellationToken zrusenie = default)
    {
        if (Prekazka() is { } prekazka)
        {
            throw new SkvpException(prekazka);
        }

        if (!model.JeStiahnuty)
        {
            throw new SkvpException(Preklad.T("Model na prepis nie je stiahnutý. Stiahnite ho v nastavení diktovania."));
        }

        if (zvuk.Length < NajmenejVzoriek)
        {
            return "";
        }

        return await PodZamkomAsync(() =>
        {
            var kontext = Nacitaj(model.CestaNaPouzitie)
                          ?? throw new SkvpException(Preklad.T("Model na prepis sa nepodarilo načítať. Stiahnite ho v nastavení diktovania znova."));
            var vystup = new byte[VelkostVystupu];
            var dlzka = skvp_hlas_prepis(kontext, zvuk, zvuk.Length, JazykUtf8, Vlakna, VyzvaUtf8, vystup, vystup.Length);
            return dlzka < 0
                ? throw new SkvpException(Preklad.T("Prepis sa nepodaril."))
                : BezOzveny(Uprav(Encoding.UTF8.GetString(vystup, 0, dlzka)));
        }, zrusenie);
    }

    /// <summary>
    /// Načíta model do pamäte vopred (pri otvorení okna s diktovaním), aby prepis po zastavení nahrávky začal hneď.
    /// Chyba sa tu neohlási – ukáže sa až pri prepise.
    /// </summary>
    public static Task PripravAsync(ModelHlasu model) =>
        Prekazka() is null && model.JeStiahnuty
            ? PodZamkomAsync(() =>
            {
                try
                {
                    return Nacitaj(model.CestaNaPouzitie) is not null;
                }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or SEHException)
                {
                    return false;
                }
            }, CancellationToken.None)
            : Task.CompletedTask;

    /// <summary>Uvoľní model z pamäte (pri zmene alebo vymazaní modelu).</summary>
    public static void Zavri()
    {
        Zamok.Wait();
        try
        {
            ZavriModel();
        }
        finally
        {
            Zamok.Release();
        }
    }

    /// <summary>
    /// Pri tichu model niekedy „zopakuje“ úvodný text (výzvu). Výsledok, ktorý je väčšou časťou výzvy, sa zahodí;
    /// kratší úsek (napr. jedna nadiktovaná oblasť s predmetmi, ktorá je aj vo výzve) zostane.
    /// </summary>
    public static string BezOzveny(string text)
    {
        var kluc = ImportOsnov.Kluc(text);
        var ozvena = kluc.Length >= KlucVyzvy.Length * 0.6 && KlucVyzvy.Contains(kluc, StringComparison.Ordinal);
        return ozvena ? "" : text;
    }

    /// <summary>Bez typických „halucinácií“ z ticha (titulky, značky) a s jednoduchými medzerami.</summary>
    public static string Uprav(string text)
    {
        foreach (var znacka in Smetie)
        {
            text = text.Replace(znacka, " ", StringComparison.OrdinalIgnoreCase);
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Zvuk zo súboru WAV (PCM 16 bit, ľubovoľná frekvencia a počet kanálov) ako 16 kHz mono.</summary>
    public static float[] NacitajWav(string cesta)
    {
        ReadOnlySpan<byte> subor = File.ReadAllBytes(cesta);
        if (subor.Length < 12 || !subor[..4].SequenceEqual("RIFF"u8) || !subor.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException("WAV");
        }

        int kanaly = 1, frekvencia = VzorkovaFrekvencia, bitov = 16;
        ReadOnlySpan<byte> data = default;
        var maData = false;
        for (var poloha = 12; poloha + 8 <= subor.Length;)
        {
            var druh = subor.Slice(poloha, 4);
            var uvedena = BinaryPrimitives.ReadInt32LittleEndian(subor[(poloha + 4)..]);
            var zostava = subor.Length - poloha - 8;
            var obsah = subor.Slice(poloha + 8, uvedena < 0 ? zostava : Math.Min(uvedena, zostava));
            if (druh.SequenceEqual("fmt "u8) && obsah.Length >= 16)
            {
                kanaly = BinaryPrimitives.ReadInt16LittleEndian(obsah[2..]);
                frekvencia = BinaryPrimitives.ReadInt32LittleEndian(obsah[4..]);
                bitov = BinaryPrimitives.ReadInt16LittleEndian(obsah[14..]);
            }
            else if (druh.SequenceEqual("data"u8))
            {
                data = obsah;
                maData = true;
            }

            // časti súboru majú párnu dĺžku (nepárna sa doplní bajtom)
            poloha += 8 + obsah.Length + (obsah.Length & 1);
        }

        if (!maData || bitov != 16 || kanaly < 1 || frekvencia < 1)
        {
            throw new InvalidDataException("WAV PCM 16");
        }

        var mono = new float[data.Length / (2 * kanaly)];
        var delitel = 32768f * kanaly;
        for (var i = 0; i < mono.Length; i++)
        {
            var sucet = 0;
            for (var k = 0; k < kanaly; k++)
            {
                sucet += BinaryPrimitives.ReadInt16LittleEndian(data[((i * kanaly + k) * 2)..]);
            }

            mono[i] = sucet / delitel;
        }

        return Prevzorkuj(mono, frekvencia);
    }

    /// <summary>Lineárne prevzorkovanie na 16 kHz.</summary>
    public static float[] Prevzorkuj(float[] zvuk, int frekvencia)
    {
        if (frekvencia == VzorkovaFrekvencia || zvuk.Length == 0)
        {
            return zvuk;
        }

        var krok = (double)frekvencia / VzorkovaFrekvencia;
        var vysledok = new float[(int)(zvuk.Length / krok)];
        for (var i = 0; i < vysledok.Length; i++)
        {
            var poloha = i * krok;
            var j = (int)poloha;
            var dalsia = Math.Min(j + 1, zvuk.Length - 1);
            vysledok[i] = zvuk[j] + (zvuk[dalsia] - zvuk[j]) * (float)(poloha - j);
        }

        return vysledok;
    }

    /// <summary>Práca s modelom mimo vlákna okna a pod zámkom.</summary>
    private static async Task<T> PodZamkomAsync<T>(Func<T> praca, CancellationToken zrusenie)
    {
        await Zamok.WaitAsync(zrusenie);
        try
        {
            return await Task.Run(praca, zrusenie);
        }
        finally
        {
            Zamok.Release();
        }
    }

    /// <summary>Načítaný model (iný sa najprv uvoľní); null = model sa nedá načítať. Volá sa len pod zámkom.</summary>
    private static IntPtr? Nacitaj(string cesta)
    {
        if (_kontext == IntPtr.Zero || _cestaModelu != cesta)
        {
            ZavriModel();
            _kontext = skvp_hlas_otvor(cesta);
            _cestaModelu = _kontext == IntPtr.Zero ? null : cesta;
        }

        return _kontext == IntPtr.Zero ? null : _kontext;
    }

    private static void ZavriModel()
    {
        if (_kontext != IntPtr.Zero)
        {
            skvp_hlas_zavri(_kontext);
            _kontext = IntPtr.Zero;
            _cestaModelu = null;
        }
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text + "\0");
}
