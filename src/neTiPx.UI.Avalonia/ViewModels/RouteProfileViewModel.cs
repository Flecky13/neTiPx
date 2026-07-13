using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using neTiPx.Core.Models;
using neTiPx.UI.Avalonia.Services;

namespace neTiPx.UI.Avalonia.ViewModels;

/// <summary>
/// ViewModel für Routen-Profilverwaltung
/// </summary>
public sealed partial class RouteProfileViewModel : ObservableObject
{
    private readonly RouteProfileStore _routeProfileStore = new();

    [ObservableProperty]
    private ObservableCollection<RouteProfile> _routeProfiles = [];

    [ObservableProperty]
    private RouteProfile? _selectedProfile;

    [ObservableProperty]
    private string _statusMessage = "Bereit";

    [ObservableProperty]
    private bool _hasNameError;

    [ObservableProperty]
    private string _nameErrorMessage = string.Empty;

    private RouteProfile? _subscribedProfile;
    private string _selectedProfileNameBackup = string.Empty;

    public RouteProfileViewModel()
    {
        // Profile beim Start laden
        LoadProfiles();
    }

    /// <summary>
    /// Lädt alle Profile aus der Speicherdatei
    /// </summary>
    public void LoadProfiles()
    {
        ReloadProfilesAndSelect(null);
    }

    /// <summary>
    /// Lädt Profile neu aus XML und selektiert ein Profil per Namen (falls vorhanden)
    /// </summary>
    public void ReloadProfilesAndSelect(string? profileName)
    {
        RouteProfiles = _routeProfileStore.LoadProfiles();

        if (RouteProfiles.Count == 0)
        {
            SelectedProfile = null;
            return;
        }

        if (!string.IsNullOrWhiteSpace(profileName))
        {
            var match = RouteProfiles.FirstOrDefault(p =>
                string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

            SelectedProfile = match ?? RouteProfiles[0];
            return;
        }

        SelectedProfile = RouteProfiles[0];
    }

    partial void OnSelectedProfileChanged(RouteProfile? value)
    {
        UnsubscribeProfileEvents(_subscribedProfile);
        SubscribeProfileEvents(value);
        _subscribedProfile = value;

        _selectedProfileNameBackup = value?.Name ?? string.Empty;
        OnPropertyChanged(nameof(SelectedProfile));
        RefreshCommandStates();
        ValidateCurrentProfile();
    }

    [RelayCommand]
    private void AddProfile()
    {
        var newProfile = new RouteProfile
        {
            Name = GetUniqueProfileName("Routen-Profil")
        };

        // Mindestens eine Route hinzufügen
        newProfile.Routes.Add(new RouteEntry
        {
            Destination = string.Empty,
            SubnetMask = "255.255.255.0",
            Gateway = string.Empty,
            Metric = 1
        });

        RouteProfiles.Add(newProfile);
        SelectedProfile = newProfile;
        
        // Neues Profil muss als dirty markiert werden, damit Speichern-Button aktiv ist
        newProfile.IsDirty = true;
        RefreshCommandStates();
        
        StatusMessage = $"Neues Profil '{newProfile.Name}' erstellt";
    }

    [RelayCommand(CanExecute = nameof(CanCopyOrDeleteProfile))]
    private void CopyProfile()
    {
        if (SelectedProfile == null)
            return;

        var newProfile = _routeProfileStore.DuplicateProfile(SelectedProfile, RouteProfiles);
        RouteProfiles.Add(newProfile);
        SelectedProfile = newProfile;
        
        // Kopiertes Profil muss als dirty markiert werden, damit Speichern-Button aktiv ist
        newProfile.IsDirty = true;
        RefreshCommandStates();
        
        StatusMessage = $"Profil '{newProfile.Name}' kopiert";
    }

    [RelayCommand]
    private void DeleteProfile(RouteProfile? profile)
    {
        if (profile == null || RouteProfiles.Count <= 1)
            return;

        var index = RouteProfiles.IndexOf(profile);
        _routeProfileStore.DeleteProfile(RouteProfiles, profile);

        // Nächstes Profil auswählen
        if (RouteProfiles.Count > 0)
        {
            var newIndex = Math.Min(index, RouteProfiles.Count - 1);
            SelectedProfile = RouteProfiles[newIndex];
        }

        StatusMessage = $"Profil '{profile.Name}' gelöscht";
    }

    [RelayCommand(CanExecute = nameof(CanAddRoute))]
    private void AddRoute()
    {
        if (SelectedProfile == null)
            return;

        var newRoute = new RouteEntry
        {
            Destination = string.Empty,
            SubnetMask = "255.255.255.0",
            Gateway = string.Empty,
            Metric = 1
        };

        SelectedProfile.Routes.Add(newRoute);
        SelectedProfile.IsDirty = true;
        StatusMessage = "Route hinzugefügt";
        RefreshCommandStates();
    }

    [RelayCommand]
    private void RemoveRoute(RouteEntry? route)
    {
        if (SelectedProfile == null || route == null || SelectedProfile.Routes.Count <= 1)
            return;

        SelectedProfile.Routes.Remove(route);
        SelectedProfile.IsDirty = true;
        StatusMessage = "Route entfernt";
        RefreshCommandStates();
    }

    [RelayCommand(CanExecute = nameof(CanSaveProfile))]
    private void SaveProfile()
    {
        System.Diagnostics.Debug.WriteLine($"[RouteProfileViewModel] SaveProfile() aufgerufen, CanSaveProfile={CanSaveProfile()}");
        SaveCurrentProfileForProfileSwitch();
    }

    public bool SaveCurrentProfileForProfileSwitch()
    {
        if (SelectedProfile == null)
            return false;

        if (!ValidateCurrentProfile())
        {
            StatusMessage = "Profil konnte nicht gespeichert werden: " + NameErrorMessage;
            return false;
        }

        var previousName = _selectedProfileNameBackup;
        var success = _routeProfileStore.SaveProfile(RouteProfiles, SelectedProfile, previousName);

        if (success)
        {
            _selectedProfileNameBackup = SelectedProfile.Name;
            SelectedProfile.IsDirty = false;
            StatusMessage = $"Profil '{SelectedProfile.Name}' gespeichert";
        }
        else
        {
            StatusMessage = "Fehler beim Speichern des Profils";
        }

        return success;
    }

    private bool ValidateCurrentProfile()
    {
        if (SelectedProfile == null)
            return false;

        // Profilname prüfen
        if (string.IsNullOrWhiteSpace(SelectedProfile.Name))
        {
            HasNameError = true;
            NameErrorMessage = "Profilname darf nicht leer sein";
            return false;
        }

        // Doppelte Namen prüfen
        var duplicate = RouteProfiles.FirstOrDefault(p =>
            !ReferenceEquals(p, SelectedProfile) &&
            string.Equals(p.Name, SelectedProfile.Name, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            HasNameError = true;
            NameErrorMessage = "Profilname bereits vergeben";
            return false;
        }

        HasNameError = false;
        NameErrorMessage = string.Empty;
        return true;
    }

    private bool CanCopyOrDeleteProfile() => SelectedProfile != null;
    private bool CanAddRoute() => SelectedProfile != null;
    private bool CanSaveProfile()
    {
        var canSave = SelectedProfile != null && SelectedProfile.IsDirty;
        System.Diagnostics.Debug.WriteLine($"[RouteProfileViewModel] CanSaveProfile: {canSave}, IsDirty={SelectedProfile?.IsDirty}, Profile={SelectedProfile?.Name}");
        return canSave;
    }

    private void RefreshCommandStates()
    {
        CopyProfileCommand.NotifyCanExecuteChanged();
        AddRouteCommand.NotifyCanExecuteChanged();
        SaveProfileCommand.NotifyCanExecuteChanged();
    }

    private void SubscribeProfileEvents(RouteProfile? profile)
    {
        if (profile == null)
            return;

        profile.PropertyChanged += SelectedProfile_PropertyChanged;
        profile.Routes.CollectionChanged += SelectedProfile_RoutesCollectionChanged;

        foreach (var route in profile.Routes)
            route.PropertyChanged += Route_PropertyChanged;
    }

    private void UnsubscribeProfileEvents(RouteProfile? profile)
    {
        if (profile == null)
            return;

        profile.PropertyChanged -= SelectedProfile_PropertyChanged;
        profile.Routes.CollectionChanged -= SelectedProfile_RoutesCollectionChanged;

        foreach (var route in profile.Routes)
            route.PropertyChanged -= Route_PropertyChanged;
    }

    private void SelectedProfile_RoutesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<RouteEntry>())
                item.PropertyChanged += Route_PropertyChanged;
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<RouteEntry>())
                item.PropertyChanged -= Route_PropertyChanged;
        }

        MarkSelectedProfileDirty();
        RefreshCommandStates();
    }

    private void SelectedProfile_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RouteProfile.IsDirty))
            return;

        if (e.PropertyName == nameof(RouteProfile.DisplayName))
            return;

        if (e.PropertyName == nameof(RouteProfile.Name))
        {
            ValidateCurrentProfile();
        }

        MarkSelectedProfileDirty();
        RefreshCommandStates();
    }

    private void Route_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MarkSelectedProfileDirty();
    }

    private void MarkSelectedProfileDirty()
    {
        if (SelectedProfile != null)
        {
            System.Diagnostics.Debug.WriteLine($"[RouteProfileViewModel] Markiere Profil '{SelectedProfile.Name}' als dirty");
            SelectedProfile.IsDirty = true;
            RefreshCommandStates();
        }
    }

    private string GetUniqueProfileName(string baseName)
    {
        var counter = 1;
        var candidateName = baseName;

        while (RouteProfiles.Any(p =>
            string.Equals(p.Name, candidateName, StringComparison.OrdinalIgnoreCase)))
        {
            counter++;
            candidateName = $"{baseName} {counter}";
        }

        return candidateName;
    }
}
