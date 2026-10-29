using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Input;

namespace SkVP.Pomocne;

/// <summary>
/// Hlasové písanie Windows (Win+H): pole dostane fokus a slovenskú klávesnicu (ak je v systéme) a program stlačí Win+H.
/// Windows potom prepisuje reč priamo do poľa – rozpoznávanie robí Microsoft cez internet.
/// </summary>
public static class HlasovePisanie
{
    private const uint VstupKlavesnice = 1;
    private const uint KlavesPustena = 0x0002;
    private const ushort KlavesWin = 0x5B;
    private const ushort KlavesH = 0x48;

    /// <summary>INPUT z user32 – únia s myšou určuje veľkosť štruktúry na 32 aj 64 bitoch.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint SendInput(uint pocet, Input[] vstupy, int velkost);

    /// <summary>Slovenská klávesnica v systéme – s ňou hlasové písanie rozpoznáva po slovensky.</summary>
    public static CultureInfo? Slovencina =>
        InputLanguageManager.Current.AvailableInputLanguages?.OfType<CultureInfo>().FirstOrDefault(c => c.TwoLetterISOLanguageName == "sk");

    /// <summary>Spustí hlasové písanie do poľa. Vráti false, ak v systéme nie je slovenčina (diktovať sa dá aj tak).</summary>
    public static bool Spusti(TextBox pole)
    {
        var slovencina = Slovencina;
        if (slovencina is not null)
        {
            InputLanguageManager.SetInputLanguage(pole, slovencina);
            InputLanguageManager.SetRestoreInputLanguage(pole, true);
        }

        pole.Focus();
        Keyboard.Focus(pole);
        if (Dialogy.TichyRezim)
        {
            Dialogy.TicheHlasenia.Add("[Hlasové písanie] Win+H");
        }
        else
        {
            // Win dole, H dole, H hore, Win hore
            Input[] vstupy = [Klaves(KlavesWin, false), Klaves(KlavesH, false), Klaves(KlavesH, true), Klaves(KlavesWin, true)];
            SendInput((uint)vstupy.Length, vstupy, Marshal.SizeOf<Input>());
        }

        return slovencina is not null;
    }

    private static Input Klaves(ushort kod, bool pustit) => new()
    {
        Type = VstupKlavesnice,
        Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = kod, Flags = pustit ? KlavesPustena : 0 } },
    };
}
