using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using neTiPx.Core.Models;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel für UNC-Pfad-Profilverwaltung
/// </summary>
public sealed partial class UncPathViewModel : ObservableObject
{
    private static readonly IReadOnlyList<string> FixedDriveLetters = Enumerable
        .Range('A', 'Z' - 'A' + 1)
        .Select(c => $"{(char)c}:")
        .ToList();

    private readonly UncPathStore _uncPathStore = new();
    private readonly UncPathService _uncPathService = new();

    [ObservableProperty]
    private ObservableCollection<UncPathProfile> _uncPathProfiles = new();

    [ObservableProperty]
    private ObservableCollection<MountedUncConnection> _mountedConnections = new();

    [ObservableProperty]
    private UncPathProfile? _selectedProfile;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string _statusMessage = "Noch keine Aktion";

    [ObservableProperty]
    private bool _hasNameError;

    [ObservableProperty]
    private string _nameErrorMessage = string.Empty;

    private string _selectedProfileNameBackup = string.Empty;
    private UncPathProfile? _subscribedProfile;

    public UncPathViewModel()
    {
        // Daten werden NICHT hier geladen – die Page ruft LoadProfiles() erst
        // im Loaded-Event auf, wenn die gesamte UI bereits aufgebaut ist.
    }

    #region Properties

    public IReadOnlyList<string> DriveLetters => FixedDriveLetters;

    /// <summary>
    /// Gibt an, ob wir auf Windows sind (zeigt Laufwerksbuchstaben)
    /// </summary>
    public bool IsWindows => OperatingSystem.IsWindows();

    /// <summary>
    /// Gibt an, ob wir auf Linux/macOS sind (zeigt Mount-Points)
    /// </summary>
    public bool IsUnixLike => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    /// <summary>
    /// Label für das Laufwerk/Mount-Point-Feld
    /// </summary>
    public string MountTargetLabel => IsWindows ? "Laufwerksbuchstabe" : "Mount-Point";

    /// <summary>
    /// Platzhalter für das Laufwerk/Mount-Point-Feld
    /// </summary>
    public string MountTargetPlaceholder => IsWindows 
        ? "z.B. Z:" 
        : (OperatingSystem.IsMacOS() ? "/Volumes/share" : "/mnt/share");

    #endregion

    #region Methoden

    /// <summary>
    /// Lädt alle Profile aus der Speicherdatei. Wird von der Page im Loaded-Event aufgerufen.
    /// </summary>
    public void LoadProfiles()
    {
        ReloadProfilesAndSelect(null);
    }

    /// <summary>
    /// Lädt Profile neu aus XML und selektiert ein Profil per Namen (falls vorhanden).
    /// </summary>
    public void ReloadProfilesAndSelect(string? profileName)
    {
        UncPathProfiles = _uncPathStore.LoadProfiles();

        if (UncPathProfiles.Count == 0)
        {
            SelectedProfile = null;
            _ = RefreshMountedConnectionsInternalAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var match = UncPathProfiles.FirstOrDefault(p =>
                string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

            SelectedProfile = match ?? UncPathProfiles[0];
            _ = RefreshMountedConnectionsInternalAsync();
            return;
        }

        SelectedProfile = UncPathProfiles[0];
        _ = RefreshMountedConnectionsInternalAsync();
    }

    partial void OnSelectedProfileChanged(UncPathProfile? oldValue, UncPathProfile? newValue)
    {
        UnsubscribeProfileEvents(oldValue);
        SubscribeProfileEvents(newValue);
        _subscribedProfile = newValue;

        _selectedProfileNameBackup = newValue?.Name ?? string.Empty;
        
        // Validiere den Profilnamen beim Wechsel
        ValidateProfileName();
        
        // Aktualisiere alle Command-Zustände
        SaveProfileCommand?.NotifyCanExecuteChanged();
        ApplyProfileCommand?.NotifyCanExecuteChanged();
        AddUncPathCommand?.NotifyCanExecuteChanged();
        RemoveUncPathCommand?.NotifyCanExecuteChanged();
        CopyProfileCommand?.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Abonniert PropertyChanged-Events des Profils, um IsDirty bei Änderungen zu setzen.
    /// </summary>
    private void SubscribeProfileEvents(UncPathProfile? profile)
    {
        if (profile == null)
            return;

        profile.PropertyChanged += OnProfilePropertyChanged;
        profile.UncPaths.CollectionChanged += OnUncPathsCollectionChanged;

        foreach (var entry in profile.UncPaths)
            entry.PropertyChanged += OnUncPathEntryPropertyChanged;
    }

    /// <summary>
    /// Deabonniert PropertyChanged-Events des Profils.
    /// </summary>
    private void UnsubscribeProfileEvents(UncPathProfile? profile)
    {
        if (profile == null)
            return;

        profile.PropertyChanged -= OnProfilePropertyChanged;
        profile.UncPaths.CollectionChanged -= OnUncPathsCollectionChanged;

        foreach (var entry in profile.UncPaths)
            entry.PropertyChanged -= OnUncPathEntryPropertyChanged;
    }

    private void OnProfilePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is UncPathProfile profile && e.PropertyName != nameof(UncPathProfile.IsDirty))
            profile.IsDirty = true;
    }

    private void OnUncPathsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (SelectedProfile != null)
        {
            SelectedProfile.IsDirty = true;

            // Subscribe/Unsubscribe von UncPathEntry-Events
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is UncPathEntry entry)
                        entry.PropertyChanged -= OnUncPathEntryPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is UncPathEntry entry)
                        entry.PropertyChanged += OnUncPathEntryPropertyChanged;
                }
            }
        }
    }

    private void OnUncPathEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (SelectedProfile != null)
        {
            SelectedProfile.IsDirty = true;
            
            // Bei Namensänderung: Validierung durchführen
            if (e.PropertyName == nameof(UncPathProfile.Name))
            {
                ValidateProfileName();
                SaveProfileCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Fügt ein neues leeres Profil hinzu
    /// </summary>
    [RelayCommand]
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
        
        // Commands werden durch OnSelectedProfileChanged automatisch aktualisiert
    }

    /// <summary>
    /// Dupliziert das aktuell ausgewählte Profil
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyProfile))]
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

    private bool CanCopyProfile() => SelectedProfile != null;

    /// <summary>
    /// Löscht ein Profil
    /// </summary>
    [RelayCommand]
    private void DeleteProfile(UncPathProfile? profile)
    {
        if (profile == null)
            return;

        UncPathProfiles.Remove(profile);
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
    [RelayCommand(CanExecute = nameof(CanAddUncPath))]
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
        
        // Command-Zustand aktualisieren
        RemoveUncPathCommand?.NotifyCanExecuteChanged();
    }

    private bool CanAddUncPath() => SelectedProfile != null;

    /// <summary>
    /// Entfernt einen UNC-Pfad-Eintrag (wenn nicht der letzte)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveUncPath))]
    private void RemoveUncPath(UncPathEntry? entry)
    {
        if (SelectedProfile == null || entry == null || SelectedProfile.UncPaths.Count <= 1)
            return;

        SelectedProfile.UncPaths.Remove(entry);
        SelectedProfile.IsDirty = true;

        // Aktualisiere CanRemove auf allen Einträgen
        foreach (var e in SelectedProfile.UncPaths)
            e.CanRemove = SelectedProfile.UncPaths.Count > 1;
        
        // Command-Zustand aktualisieren
        RemoveUncPathCommand?.NotifyCanExecuteChanged();
        AddUncPathCommand?.NotifyCanExecuteChanged();
    }

    private bool CanRemoveUncPath(UncPathEntry? entry) => SelectedProfile?.UncPaths.Count > 1;

    /// <summary>
    /// Speichert das aktuelle Profil
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSaveProfile))]
    private void SaveProfile()
    {
        SaveCurrentProfileForProfileSwitch();
    }

    private bool CanSaveProfile()
    {
        if (SelectedProfile == null)
            return false;

        // Prüfe auf Namens-Duplikate
        var duplicate = UncPathProfiles.FirstOrDefault(p =>
            !ReferenceEquals(p, SelectedProfile) &&
            string.Equals(p.Name?.Trim(), SelectedProfile.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

        return duplicate == null && !string.IsNullOrWhiteSpace(SelectedProfile.Name);
    }

    public bool SaveCurrentProfileForProfileSwitch()
    {
        var profile = SelectedProfile;
        if (profile == null)
            return true;

        if (profile.UncPaths == null)
        {
            SetLastAction("Speichern fehlgeschlagen: UNC-Pfade konnten nicht gelesen werden.");
            return false;
        }

        if (_uncPathStore.SaveProfile(UncPathProfiles, profile, _selectedProfileNameBackup))
        {
            _selectedProfileNameBackup = profile.Name;
            profile.IsDirty = false;
            SetLastAction("Gespeichert");
            return true;
        }

        SetLastAction("Speichern fehlgeschlagen: Profilname bereits vorhanden oder Fehler beim Schreiben der Datei UNC-Profile.xml.");
        return false;
    }

    /// <summary>
    /// Wendet das aktuelle Profil an (mapped alle UNC-Pfade)
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApplyProfile))]
    private async System.Threading.Tasks.Task ApplyProfileAsync()
    {
        if (SelectedProfile == null || IsApplying)
            return;

        var validationErrors = CollectValidationErrors(SelectedProfile, markUncErrors: true);
        if (validationErrors.Count > 0)
        {
            SetLastAction($"Anwenden abgebrochen: {string.Join(" | ", validationErrors)}");
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
        }
        catch (Exception ex)
        {
            SetLastAction($"Fehler beim Anwenden: {ex.Message}");
        }
        finally
        {
            await RefreshMountedConnectionsInternalAsync();
            IsApplying = false;
        }
    }

    private bool CanApplyProfile() => SelectedProfile != null && !IsApplying;

    /// <summary>
    /// Trennt eine gemountete Verbindung
    /// </summary>
    [RelayCommand]
    private async System.Threading.Tasks.Task DisconnectMountedConnectionAsync(MountedUncConnection? connection)
    {
        if (connection == null)
            return;

        SetLastAction($"Trenne Verbindung: {connection.DisplayText}");

        try
        {
            var (success, message) = await _uncPathService.DisconnectMappedConnection(connection.DisconnectTarget);
            SetLastAction(success ? message : $"Fehler beim Trennen: {message}");
        }
        catch (Exception ex)
        {
            SetLastAction($"Fehler beim Trennen: {ex.Message}");
        }
        finally
        {
            await RefreshMountedConnectionsInternalAsync();
        }
    }

    /// <summary>
    /// Aktualisiert die Liste der gemounteten Verbindungen manuell
    /// </summary>
    [RelayCommand]
    private async System.Threading.Tasks.Task RefreshMountedConnectionsAsync()
    {
        SetLastAction("Aktualisiere verbundene UNC-Pfade...");
        await RefreshMountedConnectionsInternalAsync();
        SetLastAction($"{MountedConnections.Count} verbundene UNC-Pfade gefunden");
    }

    #endregion

    #region Validierung

    /// <summary>
    /// Sammelt alle Validierungsfehler eines Profils
    /// </summary>
    private List<string> CollectValidationErrors(UncPathProfile profile, bool markUncErrors)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Name))
            errors.Add("Profilname fehlt");

        if (profile.UncPaths.Count == 0)
        {
            errors.Add("Keine UNC-Pfade definiert");
            return errors;
        }

        // UNC-Pfad-Validierung
        foreach (var entry in profile.UncPaths)
        {
            if (string.IsNullOrWhiteSpace(entry.UncPath))
            {
                errors.Add("Ein UNC-Pfad ist leer");
                if (markUncErrors)
                    entry.HasUncPathError = true;
            }
            else if (!entry.IsValid())
            {
                errors.Add($"Ungültiger UNC-Pfad: {entry.UncPath}");
                if (markUncErrors)
                    entry.HasUncPathError = true;
            }
            else
            {
                if (markUncErrors)
                    entry.HasUncPathError = false;
            }
        }

        return errors;
    }

    #endregion

    #region Hilfsmethoden

    /// <summary>
    /// Aktualisiert die Liste der gemounteten Verbindungen (interne Implementierung)
    /// </summary>
    private async System.Threading.Tasks.Task RefreshMountedConnectionsInternalAsync()
    {
        try
        {
            var connections = await _uncPathService.GetMountedConnections();
            MountedConnections = new ObservableCollection<MountedUncConnection>(connections);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UncPathViewModel] Fehler beim Abrufen der gemounteten Verbindungen: {ex.Message}");
        }
    }

    /// <summary>
    /// Generiert einen eindeutigen Profilnamen
    /// </summary>
    private string GetUniqueProfileName(string baseName)
    {
        var candidate = baseName;
        var counter = 1;

        while (UncPathProfiles.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            candidate = $"{baseName} {counter}";
        }

        return candidate;
    }

    /// <summary>
    /// Validiert den Profilnamen auf Duplikate
    /// </summary>
    private void ValidateProfileName()
    {
        if (SelectedProfile == null)
        {
            HasNameError = false;
            NameErrorMessage = string.Empty;
            return;
        }

        var name = SelectedProfile.Name?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(name))
        {
            HasNameError = true;
            NameErrorMessage = "Profilname darf nicht leer sein";
            return;
        }

        var duplicate = UncPathProfiles.FirstOrDefault(p =>
            !ReferenceEquals(p, SelectedProfile) &&
            string.Equals(p.Name?.Trim(), name, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            HasNameError = true;
            NameErrorMessage = $"Ein Profil mit dem Namen '{name}' existiert bereits";
        }
        else
        {
            HasNameError = false;
            NameErrorMessage = string.Empty;
        }
    }

    /// <summary>
    /// Setzt die letzte Aktionsnachricht
    /// </summary>
    private void SetLastAction(string message)
    {
        StatusMessage = message;
        System.Diagnostics.Debug.WriteLine($"[UncPathViewModel] {message}");
    }

    #endregion
}
