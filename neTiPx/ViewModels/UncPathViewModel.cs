using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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
    private static readonly IReadOnlyList<string> FixedDriveLetters = Enumerable
        .Range('A', 'Z' - 'A' + 1)
        .Select(c => $"{(char)c}:")
        .ToList();

    private readonly UncPathStore _uncPathStore = new();
    private readonly UncPathService _uncPathService = new();

    private ObservableCollection<UncPathProfile> _uncPathProfiles = [];
    private ObservableCollection<MountedUncConnection> _mountedConnections = [];
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
        ApplyProfileCommand = new RelayCommand(ApplyProfile, CanApplyProfile);
        DisconnectMountedConnectionCommand = new RelayCommand<MountedUncConnection>(DisconnectMountedConnection, c => c != null);
        // Daten werden NICHT hier geladen – die Page ruft LoadProfiles() erst
        // im Loaded-Event auf, wenn die gesamte UI bereits aufgebaut ist.
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

    public IReadOnlyList<string> DriveLetters => FixedDriveLetters;

    public ObservableCollection<MountedUncConnection> MountedConnections
    {
        get => _mountedConnections;
        set => SetProperty(ref _mountedConnections, value);
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
    public ICommand DisconnectMountedConnectionCommand { get; }

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
            _ = RefreshMountedConnectionsAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var match = UncPathProfiles.FirstOrDefault(p =>
                string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

            SelectedProfile = match ?? UncPathProfiles[0];
            _ = RefreshMountedConnectionsAsync();
            return;
        }

        SelectedProfile = UncPathProfiles[0];
        _ = RefreshMountedConnectionsAsync();
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

        if (profile.UncPaths == null)
        {
            SetLastAction("Speichern fehlgeschlagen: UNC-Pfade konnten nicht gelesen werden.");
            return false;
        }

        if (_uncPathStore.SaveProfile(UncPathProfiles, profile, _selectedProfileNameBackup))
        {
            _selectedProfileNameBackup = profile.Name;

            if (!ReferenceEquals(SelectedProfile, profile))
            {
                SelectedProfile = profile;
            }

            SetLastAction("Gespeichert");

            RefreshCommandStates();
            return true;
        }

        SetLastAction("Speichern fehlgeschlagen: Profilname bereits vorhanden oder Fehler beim Schreiben der Datei UNC-Profile.xml.");
        RefreshCommandStates();
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
            await RefreshMountedConnectionsAsync();
            IsApplying = false;
            RefreshCommandStates();
        }
    }

    private async void DisconnectMountedConnection(MountedUncConnection? connection)
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
            await RefreshMountedConnectionsAsync();
        }
    }

    public async Task RefreshMountedConnectionsAsync()
    {
        try
        {
            var connections = await _uncPathService.GetMountedConnections();
            MountedConnections = new ObservableCollection<MountedUncConnection>(connections);
        }
        catch
        {
            MountedConnections = [];
        }
    }

    private bool CanApplyProfile()
    {
        if (IsApplying || SelectedProfile == null)
            return false;

        if (SelectedProfile.IsDirty)
            return false;

        return CollectValidationErrors(SelectedProfile, markUncErrors: false).Count == 0;
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
        if (e.PropertyName == nameof(UncPathEntry.CanRemove) ||
            e.PropertyName == nameof(UncPathEntry.HasUncPathError))
        {
            return;
        }

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

    private bool TryValidateDriveLetters(UncPathProfile profile, out string message)
    {
        message = string.Empty;

        var missingRows = new List<int>();
        var duplicateRows = new List<int>();
        var invalidRows = new List<int>();
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < profile.UncPaths.Count; i++)
        {
            var row = i + 1;
            var rawDrive = profile.UncPaths[i].DriveLetter?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(rawDrive))
            {
                missingRows.Add(row);
                continue;
            }

            if (rawDrive.Length != 2 || rawDrive[1] != ':' || !char.IsLetter(rawDrive[0]))
            {
                invalidRows.Add(row);
                continue;
            }

            var drive = rawDrive.ToUpperInvariant();

            if (seen.ContainsKey(drive))
            {
                duplicateRows.Add(row);
                duplicateRows.Add(seen[drive]);
            }
            else
            {
                seen[drive] = row;
            }
        }

        duplicateRows = duplicateRows.Distinct().OrderBy(v => v).ToList();

        if (missingRows.Count > 0)
        {
            message = $"Laufwerksbuchstabe fehlt in Zeile(n) {string.Join(", ", missingRows)}.";
            return false;
        }

        if (invalidRows.Count > 0)
        {
            message = $"Laufwerksbuchstabe ungueltig in Zeile(n) {string.Join(", ", invalidRows)}. Erlaubt ist A: bis Z:.";
            return false;
        }

        if (duplicateRows.Count > 0)
        {
            message = $"Laufwerksbuchstabe doppelt gewaehlt in Zeile(n) {string.Join(", ", duplicateRows)}.";
            return false;
        }

        return true;
    }

    private List<string> CollectValidationErrors(UncPathProfile profile, bool markUncErrors)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(profile.Name))
            errors.Add("Profilname fehlt.");

        if (profile.UncPaths.Count == 0)
        {
            errors.Add("Mindestens ein UNC-Pfad ist erforderlich.");
            return errors;
        }

        var missingUncRows = new List<int>();
        var invalidUncRows = new List<int>();

        for (int i = 0; i < profile.UncPaths.Count; i++)
        {
            var entry = profile.UncPaths[i];
            var row = i + 1;
            var isMissing = string.IsNullOrWhiteSpace(entry.UncPath);
            var isInvalid = !isMissing && !UncPathEntry.ValidateUncPath(entry.UncPath);

            if (markUncErrors)
                entry.HasUncPathError = isMissing || isInvalid;

            if (isMissing)
                missingUncRows.Add(row);
            else if (isInvalid)
                invalidUncRows.Add(row);
        }

        if (missingUncRows.Count > 0)
            errors.Add($"UNC-Pfad fehlt in Zeile(n) {string.Join(", ", missingUncRows)}.");

        if (invalidUncRows.Count > 0)
            errors.Add($"UNC-Pfad ungueltig in Zeile(n) {string.Join(", ", invalidUncRows)}. Format: \\\\server\\share");

        if (!TryValidateDriveLetters(profile, out var driveValidationMessage))
            errors.Add(driveValidationMessage);

        return errors;
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
