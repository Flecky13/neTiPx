using System.Collections.ObjectModel;
using neTiPx.Helpers;

namespace neTiPx.Models;

/// <summary>
/// Profil für UNC-Pfad-Verwaltung mit Authentifizierung
/// </summary>
public sealed class UncPathProfile : ObservableObject
{
    private string _name = string.Empty;
    private bool _isDirty;
    private ObservableCollection<UncPathEntry> _uncPaths = [];

    public UncPathProfile()
    {
        _uncPaths.CollectionChanged += (s, e) => OnPropertyChanged(nameof(UncPaths));
    }

    /// <summary>
    /// Profilname (eindeutig)
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Gibt an, ob ungespeicherte Änderungen vorhanden sind
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    /// <summary>
    /// Alle UNC-Pfäde in diesem Profil
    /// </summary>
    public ObservableCollection<UncPathEntry> UncPaths
    {
        get => _uncPaths;
        set => SetProperty(ref _uncPaths, value);
    }

    /// <summary>
    /// Displayname mit Dirty-Indikator
    /// </summary>
    public string DisplayName => IsDirty ? $"{Name} *" : Name;

    /// <summary>
    /// Validiert das gesamte Profil
    /// </summary>
    public bool IsValid()
    {
        // Profilname erforderlich
        if (string.IsNullOrWhiteSpace(Name))
            return false;

        // Mindestens ein UNC-Pfad erforderlich
        if (UncPaths.Count == 0)
            return false;

        // Alle UNC-Pfade müssen gültig sein
        foreach (var entry in UncPaths)
        {
            if (!entry.IsValid())
                return false;
        }

        return true;
    }
}
