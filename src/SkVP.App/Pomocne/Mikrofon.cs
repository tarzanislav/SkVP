using System.Runtime.InteropServices;
using SkVP.Data;
using SkVP.Services;

namespace SkVP.Pomocne;

/// <summary>
/// Nahrávanie z mikrofónu cez waveIn (winmm.dll) v tvare, aký potrebuje prepis: 16 kHz, mono, 16 bit. Windows
/// plní kruh bufferov po 100 ms a po každom naplnenom dá signál – vlákno nahrávania len čaká, nič nezisťuje dookola.
/// Zvuk zostáva len v pamäti počítača.
/// </summary>
public sealed class Mikrofon : IDisposable
{
    private const int WaveMapper = -1;
    private const int CallbackEvent = 0x00050000;
    private const uint WhdrDone = 0x1;
    private const int Bufferov = 8;
    private const int VzoriekBuffera = PrepisHlasu.VzorkovaFrekvencia / 10;
    private const int BajtovBuffera = VzoriekBuffera * sizeof(short);
    private static readonly int VelkostHlavicky = Marshal.SizeOf<WaveHdr>();

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    private struct WaveFormatEx
    {
        public ushort FormatTag;
        public ushort Channels;
        public uint SamplesPerSec;
        public uint AvgBytesPerSec;
        public ushort BlockAlign;
        public ushort BitsPerSample;
        public ushort Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHdr
    {
        public IntPtr Data;
        public uint BufferLength;
        public uint BytesRecorded;
        public IntPtr User;
        public uint Flags;
        public uint Loops;
        public IntPtr Next;
        public IntPtr Reserved;
    }

    // systémové knižnice len zo System32 – podvrhnutá winmm.dll v inom priečinku sa nenačíta
    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInGetNumDevs();

    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInOpen(out IntPtr zariadenie, int id, ref WaveFormatEx format, IntPtr spatne, IntPtr instancia, int priznaky);

    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInPrepareHeader(IntPtr zariadenie, IntPtr hlavicka, int velkost);

    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInUnprepareHeader(IntPtr zariadenie, IntPtr hlavicka, int velkost);

    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInAddBuffer(IntPtr zariadenie, IntPtr hlavicka, int velkost);

    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInStart(IntPtr zariadenie);

    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInReset(IntPtr zariadenie);

    [DllImport("winmm.dll"), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int waveInClose(IntPtr zariadenie);

    private readonly AutoResetEvent _naplneny = new(false);
    private readonly List<short> _zaznam = new();
    private IntPtr _zariadenie;
    private IntPtr _hlavicky;
    private IntPtr _data;
    private Thread? _vlakno;
    private volatile bool _nahrava;

    /// <summary>Hlasitosť posledných 100 ms (0 – 1) – na ukazovateľ pri tlačidle. Volá sa z vlákna nahrávania.</summary>
    public event Action<double>? Uroven;

    public static bool MaZariadenie => waveInGetNumDevs() > 0;

    /// <summary>Dĺžka nahrávky v sekundách.</summary>
    public double Sekund
    {
        get
        {
            lock (_zaznam)
            {
                return _zaznam.Count / (double)PrepisHlasu.VzorkovaFrekvencia;
            }
        }
    }

    public void Start()
    {
        if (_nahrava)
        {
            return;
        }

        if (!MaZariadenie)
        {
            throw new SkvpException(Preklad.T("V počítači nie je mikrofón (alebo je vypnutý)."));
        }

        var format = new WaveFormatEx
        {
            FormatTag = 1, // PCM
            Channels = 1,
            SamplesPerSec = PrepisHlasu.VzorkovaFrekvencia,
            AvgBytesPerSec = PrepisHlasu.VzorkovaFrekvencia * sizeof(short),
            BlockAlign = sizeof(short),
            BitsPerSample = 16,
        };
        var kod = waveInOpen(out _zariadenie, WaveMapper, ref format, _naplneny.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CallbackEvent);
        if (kod != 0)
        {
            _zariadenie = IntPtr.Zero;
            throw new SkvpException(Preklad.F($"Mikrofón sa nepodarilo otvoriť (kód {kod}). Skontrolujte v Nastaveniach Windows → Ochrana súkromia → Mikrofón, či majú klasické aplikácie povolený prístup."));
        }

        try
        {
            _data = Marshal.AllocHGlobal(Bufferov * BajtovBuffera);
            _hlavicky = Marshal.AllocHGlobal(Bufferov * VelkostHlavicky);
            // najprv všetky hlavičky vyplniť – pri chybe sa potom uvoľňujú len platné hlavičky
            for (var i = 0; i < Bufferov; i++)
            {
                Marshal.StructureToPtr(new WaveHdr { Data = _data + i * BajtovBuffera, BufferLength = BajtovBuffera }, Hlavicka(i), false);
            }

            for (var i = 0; i < Bufferov; i++)
            {
                Over(waveInPrepareHeader(_zariadenie, Hlavicka(i), VelkostHlavicky));
                Over(waveInAddBuffer(_zariadenie, Hlavicka(i), VelkostHlavicky));
            }

            Over(waveInStart(_zariadenie));
        }
        catch
        {
            Uvolni();
            throw;
        }

        _nahrava = true;
        _vlakno = new Thread(Nahravaj) { IsBackground = true, Name = "SkVP mikrofón" };
        _vlakno.Start();
    }

    /// <summary>Ukončí nahrávanie a vráti celý zvuk (16 kHz mono, float −1 až 1).</summary>
    public float[] Stop()
    {
        if (!_nahrava)
        {
            return Array.Empty<float>();
        }

        Zastav();
        lock (_zaznam)
        {
            var zvuk = new float[_zaznam.Count];
            for (var i = 0; i < zvuk.Length; i++)
            {
                zvuk[i] = _zaznam[i] / 32768f;
            }

            return zvuk;
        }
    }

    public void Dispose()
    {
        // ak vlákno neodpovedá, signál aj pamäť zostanú – môže ich ešte používať
        if (Zastav())
        {
            _naplneny.Dispose();
        }
    }

    private IntPtr Hlavicka(int index) => _hlavicky + index * VelkostHlavicky;

    /// <summary>Chyba pri spúšťaní: nahrávka sa nezačne (inak by bola potichu prázdna).</summary>
    private static void Over(int kod)
    {
        if (kod != 0)
        {
            throw new SkvpException(Preklad.F($"Nahrávanie z mikrofónu sa nepodarilo spustiť (kód {kod}). Skúste iný mikrofón alebo reštartujte program."));
        }
    }

    /// <summary>
    /// Vlákno nahrávania: po signále prevezme naplnené buffery v poradí, v akom ich Windows plní (kruhovo od prvého),
    /// a hneď ich vráti na ďalšie nahrávanie.
    /// </summary>
    private void Nahravaj()
    {
        var vzorky = new short[VzoriekBuffera];
        var nasledujuci = 0;
        while (_nahrava)
        {
            _naplneny.WaitOne(250);
            while (_nahrava)
            {
                var adresa = Hlavicka(nasledujuci);
                var hlavicka = Marshal.PtrToStructure<WaveHdr>(adresa);
                if ((hlavicka.Flags & WhdrDone) == 0)
                {
                    break;
                }

                var pocet = (int)Math.Min(hlavicka.BytesRecorded / sizeof(short), VzoriekBuffera);
                Marshal.Copy(hlavicka.Data, vzorky, 0, pocet);
                ReadOnlySpan<short> usek = vzorky.AsSpan(0, pocet);
                lock (_zaznam)
                {
                    _zaznam.AddRange(usek);
                }

                Uroven?.Invoke(Hlasitost(usek));
                hlavicka.Flags &= ~WhdrDone;
                hlavicka.BytesRecorded = 0;
                Marshal.StructureToPtr(hlavicka, adresa, false);
                waveInAddBuffer(_zariadenie, adresa, VelkostHlavicky);
                nasledujuci = (nasledujuci + 1) % Bufferov;
            }
        }
    }

    /// <summary>Efektívna hodnota úseku, zosilnená pre ukazovateľ (bežná reč je okolo polovice).</summary>
    private static double Hlasitost(ReadOnlySpan<short> usek)
    {
        if (usek.IsEmpty)
        {
            return 0;
        }

        double sucet = 0;
        foreach (var vzorka in usek)
        {
            sucet += (double)vzorka * vzorka;
        }

        return Math.Min(1, Math.Sqrt(sucet / usek.Length) / 32768 * 4);
    }

    /// <summary>Zastaví vlákno a uvoľní zariadenie. False = vlákno neskončilo včas a pamäť bufferov sa neuvoľnila.</summary>
    private bool Zastav()
    {
        _nahrava = false;
        if (_vlakno is { } vlakno)
        {
            _naplneny.Set();
            if (!vlakno.Join(2000))
            {
                return false;
            }

            _vlakno = null;
        }

        Uvolni();
        return true;
    }

    private void Uvolni()
    {
        if (_zariadenie != IntPtr.Zero)
        {
            waveInReset(_zariadenie);
            if (_hlavicky != IntPtr.Zero)
            {
                for (var i = 0; i < Bufferov; i++)
                {
                    waveInUnprepareHeader(_zariadenie, Hlavicka(i), VelkostHlavicky);
                }
            }

            waveInClose(_zariadenie);
            _zariadenie = IntPtr.Zero;
        }

        foreach (var blok in new[] { _hlavicky, _data })
        {
            if (blok != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(blok);
            }
        }

        _hlavicky = _data = IntPtr.Zero;
    }
}
