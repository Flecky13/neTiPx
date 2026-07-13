using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace neTiPx.Core.Models;

/// <summary>
/// Profil für vorkonfigurierte Routen
/// </summary>
public sealed partial class RouteProfile : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    public ObservableCollection<RouteEntry> Routes { get; } = new();

    public string DisplayName => IsDirty ? $"{Name} *" : Name;

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(DisplayName));
    }

    /// <summary>
    /// Validiert das Profil
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return false;

        // Mindestens eine Route erforderlich
        if (Routes.Count == 0)
            return false;

        // Alle Routen müssen gültige Werte haben
        foreach (var route in Routes)
        {
            if (string.IsNullOrWhiteSpace(route.Destination) ||
                string.IsNullOrWhiteSpace(route.SubnetMask) ||
                string.IsNullOrWhiteSpace(route.Gateway))
            {
                return false;
            }
        }

        return true;
    }
}
