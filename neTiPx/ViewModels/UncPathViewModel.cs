using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;

namespace neTiPx.ViewModels;

/// <summary>
/// ViewModel für UNC-Pfad-Profilverwaltung
/// </summary>
public sealed partial class UncPathViewModel : ObservableObject
{
    private readonly UncPathStore _uncPathStore = new();
    private readonly UncPathService _uncPathService = new();

    private ObservableCollection<UncPathProfile> _uncPathProfiles = [];
    private UncPathProfile? _selectedProfile;
    private string _selectedProfileNameBackup = string.Empty;
    private bool _isApplying;
    private string _statusMessage = string.Empty;
    private UncPathProfile? _subscribedProfile;

    public UncPathViewModel()
    {
        StatusMessage = "Noch keine Aktion";

        // Commands
        AddProfileCommand = new RelayCommand(AddProfile);
        CopyProfileCommand = new RelayCommand(CopyProfile, () => SelectedProfile != null);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile != null);
        AddUncPathCommand = new RelayCommand(AddUncPath, () => SelectedProfile != null);
        RemoveUncPathCommand = new RelayCommand<UncPathEntry>(RemoveUncPath, _ => SelectedProfile?.UncPaths.Count > 1);
        SaveProfileCommand = new RelayCommand(SaveProfile, () => SelectedProfile != null);
        ApplyProfileCommand = new RelayCommand(ApplyProfile, () => !IsApplying && SelectedProfile != null);

        // Profile laden
        LoadProfiles();
    }

    #region Properties

    public ObservableCollection<UncPathProfile> UncPathProfiles
    {
        get => _uncPathProfiles;
        set => SetProperty(ref _uncPathProfiles, value);
    }

    public UncPathProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                UnsubscribeProfileEvents(_subscribedProfile);
                SubscribeProfileEvents(value);
                _subscribedProfile = value;

                _selectedProfileNameBackup = value?.Name ?? string.Empty;
                OnPropertyChanged(nameof(SelectedProfile));
                RefreshCommandStates();
            }
        }
    }

    public bool IsApplying
    {
        get => _isApplying;
        set
        {
            if (SetProperty(ref _isApplying, value))
                (ApplyProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    #endregion

    #region Commands

    public ICommand AddProfileCommand { get; }
    public ICommand CopyProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand AddUncPathCommand { get; }
    public ICommand RemoveUncPathCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand ApplyProfileCommand { get; }

    #endregion

    #region Methoden

    /// <summary>
    /// Lädt alle Profile aus der Speicherdatei
    /// </summary>
    private void LoadProfiles()
    {
        UncPathProfiles = _uncPathStore.LoadProfiles();

        if (UncPathProfiles.Count > 0)
            SelectedProfile = UncPathProfiles[0];
    }

    /// <summary>
    /// Fügt ein neues leeres Profil hinzu
    /// </summary>
    private void AddProfile()
    {
        var newProfile = new UncPathProfile
        {
            Name = GetUniqueProfileName("UNC Profil"),
            IsDirty = true
        };

        // Mindestens ein leerer UNC-Pfad-Eintrag
        newProfile.UncPaths.Add(new UncPathEntry());

        UncPathProfiles.Add(newProfile);
        SelectedProfile = newProfile;
        SetLastAction("Neues UNC-Profil erstellt.");
    }

    /// <summary>
    /// Dupliziert das aktuell ausgewählte Profil
    /// </summary>
    private void CopyProfile()
    {
        if (SelectedProfile == null)
            return;

        var copiedProfile = _uncPathStore.DuplicateProfile(SelectedProfile);
        copiedProfile.IsDirty = true;

        UncPathProfiles.Add(copiedProfile);
        SelectedProfile = copiedProfile;
        SetLastAction("Profil wurde kopiert.");
    }

    /// <summary>
    /// Löscht das aktuell ausgewählte Profil
    /// </summary>
    private void DeleteProfile()
    {
        if (SelectedProfile == null)
            return;

        UncPathProfiles.Remove(SelectedProfile);
        _uncPathStore.SaveProfiles(UncPathProfiles);

        if (UncPathProfiles.Count > 0)
            SelectedProfile = UncPathProfiles[0];
        else
            SelectedProfile = null;

        SetLastAction("Profil wurde gelöscht.");
    }

    /// <summary>
    /// Fügt einen neuen UNC-Pfad-Eintrag zum aktuellen Profil hinzu
    /// </summary>
    private void AddUncPath()
    {
        if (SelectedProfile == null)
            return;

        var newEntry = new UncPathEntry
        {
            CanRemove = SelectedProfile.UncPaths.Count > 0
        };

        SelectedProfile.UncPaths.Add(newEntry);
        SelectedProfile.IsDirty = true;

        // Alle Einträge sollten mindestens 1 haben
        foreach (var entry in SelectedProfile.UncPaths)
            entry.CanRemove = SelectedProfile.UncPaths.Count > 1;

        RefreshCommandStates();
    }

    /// <summary>
    /// Entfernt einen UNC-Pfad-Eintrag (wenn nicht der letzte)
    /// </summary>
    private void RemoveUncPath(UncPathEntry? entry)
    {
        if (SelectedProfile == null || entry == null || SelectedProfile.UncPaths.Count <= 1)
            return;

        SelectedProfile.UncPaths.Remove(entry);
        SelectedProfile.IsDirty = true;

        // Aktualisiere CanRemove auf allen Einträgen
        foreach (var e in SelectedProfile.UncPaths)
            e.CanRemove = SelectedProfile.UncPaths.Count > 1;

        RefreshCommandStates();
    }

    /// <summary>
    /// Speichert das aktuelle Profil
    /// </summary>
    private void SaveProfile()
    {
        SaveCurrentProfileForProfileSwitch();
    }

    public bool SaveCurrentProfileForProfileSwitch()
    {
        var profile = SelectedProfile;
        if (profile == null)
            return true;

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            SetLastAction("Speichern fehlgeschlagen: Profilname fehlt.");
            return false;
        }

        if (profile.UncPaths == null)
        {
            SetLastAction("Speichern fehlgeschlagen: UNC-Pfade konnten nicht gelesen werden.");
            return false;
        }

        // Validiere alle UNC-Pfade und sammle Details
        bool hasErrors = false;
        var invalidEntries = new List<int>();
        for (int i = 0; i < profile.UncPaths.Count; i++)
        {
            var entry = profile.UncPaths[i];
            if (entry == null)
            {
                hasErrors = true;
                invalidEntries.Add(i + 1);
                continue;
            }

            entry.HasUncPathError = !UncPathEntry.ValidateUncPath(entry.UncPath);
            if (entry.HasUncPathError)
            {
                hasErrors = true;
                invalidEntries.Add(i + 1);
            }
        }

        if (hasErrors)
        {
            SetLastAction($"Speichern fehlgeschlagen: Ungueltige UNC-Pfade in Zeile(n) {string.Join(", ", invalidEntries)}. Format: \\\\server\\share");
            return false;
        }

        if (!profile.IsValid())
        {
            SetLastAction("Speichern fehlgeschlagen: Profil ist ungueltig.");
            return false;
        }

        if (_uncPathStore.SaveProfile(UncPathProfiles, profile, _selectedProfileNameBackup))
        {
            _selectedProfileNameBackup = profile.Name;

            if (!ReferenceEquals(SelectedProfile, profile))
            {
                SelectedProfile = profile;
            }

            SetLastAction("Profil erfolgreich gespeichert.");
            return true;
        }

        SetLastAction("Speichern fehlgeschlagen: Profilname bereits vorhanden oder Fehler beim Schreiben der Datei UNC-Profile.xml.");
        return false;
    }

    public void DiscardCurrentProfileChangesMarker()
    {
        if (SelectedProfile != null)
            SelectedProfile.IsDirty = false;
    }

    /// <summary>
    /// Wendet das aktuelle Profil an (mapped alle UNC-Pfade)
    /// </summary>
    private async void ApplyProfile()
    {
        if (SelectedProfile == null || IsApplying)
            return;

        if (string.IsNullOrWhiteSpace(SelectedProfile.Name))
        {
            SetLastAction("Anwenden abgebrochen: Profilname fehlt.");
            return;
        }

        var invalidEntries = SelectedProfile.UncPaths
            .Select((e, i) => new { Entry = e, Index = i + 1 })
            .Where(x => !UncPathEntry.ValidateUncPath(x.Entry.UncPath))
            .Select(x => x.Index)
            .ToList();

        if (invalidEntries.Count > 0)
        {
            SetLastAction($"Anwenden abgebrochen: Ungueltige UNC-Pfade in Zeile(n) {string.Join(", ", invalidEntries)}.");
            return;
        }

        IsApplying = true;
        SetLastAction("Verbinde UNC-Pfade...");

        try
        {
            var (success, message) = await _uncPathService.ApplyProfile(SelectedProfile);

            if (success)
                SetLastAction("Alle UNC-Pfade verbunden:\n" + message);
            else
                SetLastAction("Fehler beim Verbinden:\n" + message);

            // Optional: Profil automatisch speichern nach erfolgreicher Anwendung
            if (success && SelectedProfile != null)
                SaveProfile();
        }
        catch (Exception ex)
        {
            SetLastAction($"Fehler beim Anwenden: {ex.Message}");
        }
        finally
        {
            IsApplying = false;
        }
    }

    private void RefreshCommandStates()
    {
        (CopyProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddUncPathCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveUncPathCommand as RelayCommand<UncPathEntry>)?.RaiseCanExecuteChanged();
        (SaveProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ApplyProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void SubscribeProfileEvents(UncPathProfile? profile)
    {
        if (profile == null)
            return;

        profile.PropertyChanged += SelectedProfile_PropertyChanged;
        profile.UncPaths.CollectionChanged += SelectedProfile_UncPathsCollectionChanged;

        foreach (var entry in profile.UncPaths)
            entry.PropertyChanged += UncPathEntry_PropertyChanged;
    }

    private void UnsubscribeProfileEvents(UncPathProfile? profile)
    {
        if (profile == null)
            return;

        profile.PropertyChanged -= SelectedProfile_PropertyChanged;
        profile.UncPaths.CollectionChanged -= SelectedProfile_UncPathsCollectionChanged;

        foreach (var entry in profile.UncPaths)
            entry.PropertyChanged -= UncPathEntry_PropertyChanged;
    }

    private void SelectedProfile_UncPathsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<UncPathEntry>())
                item.PropertyChanged += UncPathEntry_PropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<UncPathEntry>())
                item.PropertyChanged -= UncPathEntry_PropertyChanged;
        }

        MarkSelectedProfileDirty();
        RefreshCommandStates();
    }

    private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UncPathProfile.IsDirty))
            return;

        if (e.PropertyName == nameof(UncPathProfile.DisplayName))
            return;

        MarkSelectedProfileDirty();
        RefreshCommandStates();
    }

    private void UncPathEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkSelectedProfileDirty();
        RefreshCommandStates();
    }

    private void MarkSelectedProfileDirty()
    {
        if (SelectedProfile != null && !SelectedProfile.IsDirty)
            SelectedProfile.IsDirty = true;
    }

    private void SetLastAction(string message)
    {
        StatusMessage = message;
    }

    /// <summary>
    /// Generiert einen eindeutigen Profilnamen
    /// </summary>
    private string GetUniqueProfileName(string baseName)
    {
        if (!UncPathProfiles.Any(p => p.Name == baseName))
            return baseName;

        int index = 2;
        while (UncPathProfiles.Any(p => p.Name == $"{baseName} #{index}"))
            index++;

        return $"{baseName} #{index}";
    }

    #endregion
}
