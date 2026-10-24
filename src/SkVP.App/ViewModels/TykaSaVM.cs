using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkVP.Models;
using SkVP.Pomocne;
using SkVP.Services;

namespace SkVP.ViewModels;

/// <summary>
/// Pole „Týka sa“ pri úlohe, udalosti a poznámke: čipy predmetov, oblastí a častí osnov vo farbe oblasti.
/// Kliknutím na čip sa otvorí osnova, krížikom sa naviazanie zruší, „Vybrať…“ otvorí výber viacerých.
/// </summary>
public sealed partial class TykaSaVM : ObservableObject
{
    private readonly MainViewModel _hlavny;
    private readonly Action? _zmena;

    public TykaSaVM(MainViewModel hlavny, Action? zmena = null)
    {
        _hlavny = hlavny;
        _zmena = zmena;
    }

    public ObservableCollection<Naviazanie> Polozky { get; } = new();

    public bool JePrazdne => Polozky.Count == 0;

    public IReadOnlyList<long> Id => Polozky.Select(p => p.Id).ToList();

    /// <summary>Nastaví naviazania bez oznámenia zmeny (pri načítaní záznamu).</summary>
    public void Nastav(IEnumerable<Naviazanie> naviazania)
    {
        Polozky.Clear();
        foreach (var n in naviazania)
        {
            Polozky.Add(n);
        }

        OnPropertyChanged(nameof(JePrazdne));
    }

    public void Nastav(IEnumerable<long> id) => Nastav(id.Select(Planovanie.Objekt).OfType<Naviazanie>());

    [RelayCommand]
    private void Vybrat()
    {
        var okno = new Views.VyberObjektuWindow(_hlavny, Id) { Owner = Dialogy.Vlastnik };
        if (okno.ShowDialog() != true)
        {
            return;
        }

        Nastav(okno.Vybrane);
        _zmena?.Invoke();
    }

    [RelayCommand]
    private void Odobrat(Naviazanie? naviazanie)
    {
        if (naviazanie is not null && Polozky.Remove(naviazanie))
        {
            OnPropertyChanged(nameof(JePrazdne));
            _zmena?.Invoke();
        }
    }

    [RelayCommand]
    private void Otvorit(Naviazanie? naviazanie)
    {
        if (naviazanie is { Odstranene: false })
        {
            _hlavny.OtvorObjekt(naviazanie.Id);
        }
    }
}

/// <summary>Filter „Týka sa“ v zoznamoch úloh a poznámok: všetko, skupina osnov (aj s predmetmi) alebo jeden predmet.</summary>
public static class FilterOsnov
{
    public static IReadOnlyList<Volba<long?>> Volby(MainViewModel hlavny) =>
        new[] { new Volba<long?>(null, "Všetky predmety a oblasti") }
            .Concat(hlavny.VsetkySkupiny.Where(s => s.Aktivna).SelectMany(s =>
                new[] { new Volba<long?>(s.Id, s.Nazov) }.Concat(s.Predmety.Select(p => new Volba<long?>(p.Id, "      " + p.Nazov)))))
            .ToList();

    /// <summary>Objekty, podľa ktorých sa filtruje (null = bez filtra).</summary>
    public static IReadOnlyCollection<long>? Objekty(MainViewModel hlavny, Volba<long?> volba) =>
        volba.Hodnota is not { } id ? null
        : hlavny.NajdiOblast(id) is { } skupina ? skupina.Predmety.Select(p => p.Id).Append(id).ToList()
        : new[] { id };
}
