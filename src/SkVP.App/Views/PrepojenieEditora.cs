using System.Windows.Threading;
using SkVP.ViewModels;

namespace SkVP.Views;

/// <summary>
/// Prepojí editor s dokumentom stránky: načíta text, po zmenách ho o chvíľu sám uloží a po prevzatí zmien
/// z Wordu text znova načíta.
/// </summary>
public sealed class PrepojenieEditora : IDisposable
{
    private readonly EditorDokumentu _editor;
    private readonly DokumentStrankyVM _dokument;
    private readonly DispatcherTimer _ukladanie = new() { Interval = TimeSpan.FromSeconds(1.5) };

    public PrepojenieEditora(EditorDokumentu editor, DokumentStrankyVM dokument)
    {
        _editor = editor;
        _dokument = dokument;
        _editor.SablonaOsnovy = dokument.SablonaOsnovy;
        _editor.Nacitaj(dokument.Nacitaj().Bloky);
        _dokument.ZmenenyText = () => _editor.JeZmeneny ? _editor.Uloz() : null;
        _dokument.Obnovit += Obnov;
        _editor.Zmenene += PriZmene;
        _ukladanie.Tick += (_, _) =>
        {
            _ukladanie.Stop();
            _dokument.UlozTeraz();
        };
    }

    private void PriZmene(object? sender, EventArgs e)
    {
        _dokument.StavUlozenia = "Ukladám…";
        _ukladanie.Stop();
        _ukladanie.Start();
    }

    private void Obnov() => _editor.Nacitaj(_dokument.Nacitaj().Bloky);

    public void Dispose()
    {
        _ukladanie.Stop();
        _dokument.UlozTeraz();
        _dokument.Obnovit -= Obnov;
        _editor.Zmenene -= PriZmene;
        _dokument.ZmenenyText = null;
    }
}
