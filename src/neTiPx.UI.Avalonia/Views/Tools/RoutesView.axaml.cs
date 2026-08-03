using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using neTiPx.Core.Helpers;
using neTiPx.Core.Models;
using neTiPx.UI.Avalonia.Helpers;
using neTiPx.UI.Avalonia.Services;
using neTiPx.UI.Avalonia.ViewModels;
using neTiPx.UI.Avalonia.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace neTiPx.UI.Avalonia.Views.Tools;

public partial class RoutesView : UserControl
{
    private static readonly LanguageManager _lm = LanguageManager.Instance;
    private static string T(string key) => _lm.Lang(key);
    private static string TF(string key, params object[] args) => string.Format(T(key), args);

    private readonly NetworkConfigService _networkConfigService = new();
    private readonly IpProfileStore _ipProfileStore = new();
    private readonly List<RouteEntry> _allRoutes = new();
    private readonly RouteProfileViewModel _routeProfileViewModel = new();

    private SortColumn _sortColumn = SortColumn.Destination;
    private bool _sortAscending = true;
    private RouteProfile? _subscribedProfile;
    private bool _updatingNameBox; // Flag um Loops zu vermeiden

    public ObservableCollection<RouteEntry> FilteredRoutes { get; } = new();

    private enum SortColumn
    {
        Destination,
        SubnetMask,
        Gateway,
        Metric
    }

    private enum RouteUsageDeleteDecision
    {
        Cancel,
        DeleteAndCleanup,
        SwitchToProfile
    }

    public RoutesView()
    {
        InitializeComponent();

        // System-Routen Tab initialisieren
        RoutesItemsControl.ItemsSource = FilteredRoutes;

        // Event-Handler für System-Routen registrieren
        RefreshRoutesButton.Click += RefreshRoutes_Click;
        DestinationFilterBox.TextChanged += DestinationFilterBox_TextChanged;
        ClearFilterButton.Click += ClearFilter_Click;

        SortDestinationButton.Click += (s, e) => ToggleSort(SortColumn.Destination);
        SortSubnetButton.Click += (s, e) => ToggleSort(SortColumn.SubnetMask);
        SortGatewayButton.Click += (s, e) => ToggleSort(SortColumn.Gateway);
        SortMetricButton.Click += (s, e) => ToggleSort(SortColumn.Metric);

        AddRouteButton.Click += AddRoute_Click;

        UpdateSortIndicators();

        // Routen-Profile Tab initialisieren
        InitializeRouteProfileTab();

        // Routen beim Laden abrufen
        Loaded += async (s, e) => await LoadRoutesAsync();
    }

    private void InitializeRouteProfileTab()
    {
        // Profil-Liste DataContext setzen
        RouteProfileListBox.ItemsSource = _routeProfileViewModel.RouteProfiles;
        RouteProfileListBox.SelectionChanged += RouteProfileListBox_SelectionChanged;

        // Buttons
        AddRouteProfileButton.Click += (s, e) => _routeProfileViewModel.AddProfileCommand.Execute(null);
        CopyRouteProfileButton.Click += (s, e) => _routeProfileViewModel.CopyProfileCommand.Execute(null);
        SaveRouteProfileButton.Click += (s, e) => _routeProfileViewModel.SaveProfileCommand.Execute(null);

        // Profilname-Textbox - TextChanged statt PropertyChanged verwenden
        RouteProfileNameBox.TextChanged += (s, e) =>
        {
            if (!_updatingNameBox && _routeProfileViewModel.SelectedProfile != null)
            {
                var newName = RouteProfileNameBox.Text ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[RoutesView] TextChanged: '{newName}', Current: '{_routeProfileViewModel.SelectedProfile.Name}'");
                _routeProfileViewModel.SelectedProfile.Name = newName;
            }
        };

        // Route hinzufügen Button
        AddRouteToProfileButton.Click += (s, e) => _routeProfileViewModel.AddRouteCommand.Execute(null);

        // ViewModel Property Changed Handler
        _routeProfileViewModel.PropertyChanged += RouteProfileViewModel_PropertyChanged;

        // Initial-Update
        UpdateRouteProfileUI();
        
        // Initiale Subscription zum ersten Profil (falls vorhanden)
        if (_routeProfileViewModel.SelectedProfile != null)
        {
            _routeProfileViewModel.SelectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
            _subscribedProfile = _routeProfileViewModel.SelectedProfile;
        }
    }

    private void RouteProfileListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RouteProfileListBox.SelectedItem is RouteProfile selected)
        {
            _routeProfileViewModel.SelectedProfile = selected;
        }
    }

    private void RouteProfileViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RouteProfileViewModel.SelectedProfile))
        {
            // Unsubscribe vom alten Profil
            if (_subscribedProfile != null)
            {
                _subscribedProfile.PropertyChanged -= SelectedProfile_PropertyChanged;
            }
            
            // Subscribe zum neuen Profil
            if (_routeProfileViewModel.SelectedProfile != null)
            {
                _routeProfileViewModel.SelectedProfile.PropertyChanged += SelectedProfile_PropertyChanged;
                _subscribedProfile = _routeProfileViewModel.SelectedProfile;
            }
            else
            {
                _subscribedProfile = null;
            }
            
            UpdateRouteProfileUI();
        }
        else if (e.PropertyName == nameof(RouteProfileViewModel.StatusMessage))
        {
            RouteProfileStatusText.Text = _routeProfileViewModel.StatusMessage;
        }
        else if (e.PropertyName == nameof(RouteProfileViewModel.HasNameError))
        {
            RouteProfileNameError.IsVisible = _routeProfileViewModel.HasNameError;
            RouteProfileNameError.Text = _routeProfileViewModel.NameErrorMessage;
        }
    }

    private void SelectedProfile_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[RoutesView] SelectedProfile PropertyChanged: {e.PropertyName}");
        if (e.PropertyName == nameof(RouteProfile.IsDirty))
        {
            // Button-Status aktualisieren wenn IsDirty sich ändert
            var canExecute = _routeProfileViewModel.SaveProfileCommand.CanExecute(null);
            System.Diagnostics.Debug.WriteLine($"[RoutesView] IsDirty changed, CanExecute={canExecute}");
            SaveRouteProfileButton.IsEnabled = canExecute;
        }
    }

    private void UpdateRouteProfileUI()
    {
        var hasProfile = _routeProfileViewModel.SelectedProfile != null;

        ProfileConfigBorder.IsEnabled = hasProfile;

        if (hasProfile)
        {
            _updatingNameBox = true;
            RouteProfileNameBox.Text = _routeProfileViewModel.SelectedProfile!.Name;
            _updatingNameBox = false;
            ProfileRoutesItemsControl.ItemsSource = _routeProfileViewModel.SelectedProfile.Routes;
        }
        else
        {
            _updatingNameBox = true;
            RouteProfileNameBox.Text = string.Empty;
            _updatingNameBox = false;
            ProfileRoutesItemsControl.ItemsSource = null;
        }

        // Command-States aktualisieren
        CopyRouteProfileButton.IsEnabled = _routeProfileViewModel.CopyProfileCommand.CanExecute(null);
        AddRouteToProfileButton.IsEnabled = _routeProfileViewModel.AddRouteCommand.CanExecute(null);
        SaveRouteProfileButton.IsEnabled = _routeProfileViewModel.SaveProfileCommand.CanExecute(null);
    }

    private void RemoveRouteButton_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is RouteEntry route)
        {
            button.Click -= RemoveRouteFromProfile_Click;
            button.Click += RemoveRouteFromProfile_Click;
        }
    }

    private async void RemoveRouteFromProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not RouteEntry route)
            return;

        var selectedRouteProfile = _routeProfileViewModel.SelectedProfile;
        if (selectedRouteProfile == null)
            return;

        var linkedIpProfiles = FindIpProfilesUsingRouteProfile(selectedRouteProfile.Name);
        if (linkedIpProfiles.Count > 0)
        {
            var decision = await ShowProfileRouteUsageDeleteDialog(selectedRouteProfile, route, linkedIpProfiles);

            if (decision == RouteUsageDeleteDecision.Cancel)
                return;

            if (decision == RouteUsageDeleteDecision.SwitchToProfile)
            {
                SwitchToIpConfigWithProfile(linkedIpProfiles[0].Name);
                return;
            }
        }

        _routeProfileViewModel.RemoveRouteCommand.Execute(route);

        if (linkedIpProfiles.Count > 0)
        {
            _routeProfileViewModel.SaveCurrentProfileForProfileSwitch();
            var updatedProfiles = RefreshLinkedIpProfiles(linkedIpProfiles);
            _routeProfileViewModel.StatusMessage =
                $"Route entfernt. {updatedProfiles} verknüpfte IP-Profil(e) aktualisiert.";
        }
    }

    private void DeleteRouteProfileButton_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.Click -= DeleteRouteProfile_Click;
            button.Click += DeleteRouteProfile_Click;
        }
    }

    private async void DeleteRouteProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not RouteProfile profile)
            return;

        var linkedIpProfiles = FindIpProfilesUsingRouteProfile(profile.Name);
        var confirmed = false;

        if (linkedIpProfiles.Count > 0)
        {
            var decision = await ShowRouteProfileUsageDeleteDialog(profile, linkedIpProfiles);

            if (decision == RouteUsageDeleteDecision.Cancel)
                return;

            if (decision == RouteUsageDeleteDecision.SwitchToProfile)
            {
                SwitchToIpConfigWithProfile(linkedIpProfiles[0].Name);
                return;
            }

            confirmed = true;
        }

        if (!confirmed)
        {
            confirmed = await ShowConfirmDialog(
                "Profil löschen",
                $"Möchten Sie das Routenprofil '{profile.Name}' wirklich löschen?");
        }

        if (!confirmed)
            return;

        _routeProfileViewModel.DeleteProfileCommand.Execute(profile);

        if (linkedIpProfiles.Count > 0)
        {
            var updated = CleanupIpProfilesAfterRouteProfileDelete(linkedIpProfiles);
            _routeProfileViewModel.StatusMessage =
                $"Routenprofil gelöscht. {updated} verknüpfte IP-Profil(e) bereinigt.";
        }

        UpdateRouteProfileUI();
    }

    private void DeleteButton_Loaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            // Event-Handler nur einmal hinzufügen
            button.Click -= DeleteRoute_Click;
            button.Click += DeleteRoute_Click;
        }
    }

    private async Task LoadRoutesAsync()
    {
        RoutesStatusText.Text = "Wird eingelesen...";

        await Task.Yield();
        LogHandler.LogSystemMessage(LogLevel.INFO, "Routes", "Routen werden geladen...");
        
        var (success, routes, error) = _networkConfigService.ReadAllPersistentRoutes();

        if (success)
        {
            _allRoutes.Clear();
            _allRoutes.AddRange(routes);

            LogHandler.LogSystemMessage(LogLevel.INFO, "Routes", $"{routes.Count} Route(n) geladen");
            ApplyFilterAndSort();
        }
        else
        {
            LogHandler.LogErrorMessage("Routes", $"Routen laden fehlgeschlagen: {error}");
            RoutesStatusText.Text = error ?? "Fehler beim Laden der Routen";
        }
    }

    private void ApplyFilterAndSort()
    {
        IEnumerable<RouteEntry> query = _allRoutes;

        var filterText = DestinationFilterBox?.Text?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(filterText))
        {
            if (IPAddress.TryParse(filterText, out var destinationIp) && 
                destinationIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                query = FilterCandidateRoutesForDestination(query, destinationIp);
            }
            else
            {
                query = Enumerable.Empty<RouteEntry>();
            }
        }

        query = SortRoutes(query);

        FilteredRoutes.Clear();
        foreach (var route in query)
        {
            FilteredRoutes.Add(route);
        }

        if (string.IsNullOrWhiteSpace(filterText))
        {
            RoutesStatusText.Text = TF("ROUTES_STATUS_FOUND_COUNT", FilteredRoutes.Count);
        }
        else if (IPAddress.TryParse(filterText, out var parsed) && 
                 parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            RoutesStatusText.Text = TF("ROUTES_STATUS_MATCHING_FOR_DESTINATION", FilteredRoutes.Count, filterText);
        }
        else
        {
            RoutesStatusText.Text = T("ROUTES_STATUS_INVALID_FILTER");
        }
    }

    private static IEnumerable<RouteEntry> FilterCandidateRoutesForDestination(
        IEnumerable<RouteEntry> routes, IPAddress destinationIp)
    {
        var candidates = routes
            .Select(route => new
            {
                Route = route,
                PrefixLength = TryGetPrefixLength(route.SubnetMask)
            })
            .Where(x => x.PrefixLength >= 0)
            .Where(x => RouteMatchesDestination(x.Route.Destination, x.Route.SubnetMask, destinationIp))
            .ToList();

        if (candidates.Count == 0)
        {
            return Enumerable.Empty<RouteEntry>();
        }

        var bestPrefix = candidates.Max(x => x.PrefixLength);
        var bestPrefixCandidates = candidates.Where(x => x.PrefixLength == bestPrefix).ToList();
        var bestMetric = bestPrefixCandidates.Min(x => x.Route.Metric > 0 ? x.Route.Metric : int.MaxValue);

        return bestPrefixCandidates
            .Where(x => (x.Route.Metric > 0 ? x.Route.Metric : int.MaxValue) == bestMetric)
            .Select(x => x.Route);
    }

    private IEnumerable<RouteEntry> SortRoutes(IEnumerable<RouteEntry> routes)
    {
        return _sortColumn switch
        {
            SortColumn.Destination => _sortAscending
                ? routes.OrderBy(r => r.Destination, StringComparer.OrdinalIgnoreCase)
                : routes.OrderByDescending(r => r.Destination, StringComparer.OrdinalIgnoreCase),
            SortColumn.SubnetMask => _sortAscending
                ? routes.OrderBy(r => r.SubnetMask, StringComparer.OrdinalIgnoreCase)
                : routes.OrderByDescending(r => r.SubnetMask, StringComparer.OrdinalIgnoreCase),
            SortColumn.Gateway => _sortAscending
                ? routes.OrderBy(r => r.Gateway, StringComparer.OrdinalIgnoreCase)
                : routes.OrderByDescending(r => r.Gateway, StringComparer.OrdinalIgnoreCase),
            SortColumn.Metric => _sortAscending
                ? routes.OrderBy(r => r.Metric)
                : routes.OrderByDescending(r => r.Metric),
            _ => routes
        };
    }

    private static bool RouteMatchesDestination(string destination, string subnetMask, IPAddress destinationIp)
    {
        if (!IPAddress.TryParse(destination, out var routeDestination) || 
            routeDestination.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        if (!IPAddress.TryParse(subnetMask, out var routeMask) || 
            routeMask.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var destBytes = destinationIp.GetAddressBytes();
        var routeBytes = routeDestination.GetAddressBytes();
        var maskBytes = routeMask.GetAddressBytes();

        for (int i = 0; i < 4; i++)
        {
            if ((destBytes[i] & maskBytes[i]) != (routeBytes[i] & maskBytes[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static int TryGetPrefixLength(string subnetMask)
    {
        if (!IPAddress.TryParse(subnetMask, out var maskIp) || 
            maskIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return -1;
        }

        int count = 0;
        foreach (var b in maskIp.GetAddressBytes())
        {
            for (int bit = 7; bit >= 0; bit--)
            {
                if ((b & (1 << bit)) != 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void ToggleSort(SortColumn column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            _sortAscending = true;
        }

        UpdateSortIndicators();
        ApplyFilterAndSort();
    }

    private void UpdateSortIndicators()
    {
        DestinationSortIndicator.Text = _sortColumn == SortColumn.Destination ? (_sortAscending ? "▲" : "▼") : string.Empty;
        SubnetSortIndicator.Text = _sortColumn == SortColumn.SubnetMask ? (_sortAscending ? "▲" : "▼") : string.Empty;
        GatewaySortIndicator.Text = _sortColumn == SortColumn.Gateway ? (_sortAscending ? "▲" : "▼") : string.Empty;
        MetricSortIndicator.Text = _sortColumn == SortColumn.Metric ? (_sortAscending ? "▲" : "▼") : string.Empty;
    }

    private void DestinationFilterBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilterAndSort();
    }

    private void ClearFilter_Click(object? sender, RoutedEventArgs e)
    {
        LogHandler.LogUserEvent("Routes", "ButtonClick", "FilterClear");
        if (DestinationFilterBox != null)
        {
            DestinationFilterBox.Text = string.Empty;
        }

        ApplyFilterAndSort();
    }

    private async void RefreshRoutes_Click(object? sender, RoutedEventArgs e)
    {
        LogHandler.LogUserEvent("Routes", "ButtonClick", "RoutesReload");
        await LoadRoutesAsync();
    }

    private async void DeleteRoute_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not RouteEntry route)
            return;

        LogHandler.LogUserEvent("Routes", "ButtonClick", "RouteDelete", new Dictionary<string, string?>
        {
            ["Destination"] = route.Destination,
            ["SubnetMask"] = route.SubnetMask,
            ["Gateway"] = route.Gateway
        });

        if (!route.CanDeleteFromSystem)
            return;

        var profilesUsingRoute = FindIpProfilesUsingRoute(route);
        if (profilesUsingRoute.Count > 0)
        {
            var decision = await ShowRouteUsageDeleteDialog(route, profilesUsingRoute);

            if (decision == RouteUsageDeleteDecision.Cancel)
                return;

            if (decision == RouteUsageDeleteDecision.SwitchToProfile)
            {
                SwitchToIpConfigWithProfile(profilesUsingRoute[0].Name);
                return;
            }
        }
        else
        {
            // Standard-Bestätigung, wenn keine Profil-Verwendung vorliegt.
            var result = await ShowConfirmDialog(
                "Route löschen",
                $"Möchten Sie diese ständige Route wirklich aus dem System entfernen?\n\n{route.Destination} / {route.SubnetMask} via {route.Gateway}");

            if (!result)
                return;
        }

        LogHandler.LogSystemMessage(LogLevel.INFO, "Routes", 
            $"Route löschen: {route.Destination} mask {route.SubnetMask} via {route.Gateway}");
        
        var (success, error) = _networkConfigService.DeleteRoute(route);
        if (!success)
        {
            LogHandler.LogErrorMessage("Routes", $"Route löschen fehlgeschlagen: {error}");
            await ShowErrorDialog("Fehler beim Löschen", error ?? "Route konnte nicht gelöscht werden");
            return;
        }

        if (profilesUsingRoute.Count > 0)
        {
            var updatedProfiles = RemoveRouteFromIpProfiles(route, profilesUsingRoute);
            LogHandler.LogSystemMessage(LogLevel.INFO, "Routes",
                $"Route zusätzlich aus {updatedProfiles} IP-Profil(en) entfernt");
        }

        LogHandler.LogSystemMessage(LogLevel.INFO, "Routes", "Route erfolgreich gelöscht");
        await LoadRoutesAsync();
    }

    private List<IpProfile> FindIpProfilesUsingRoute(RouteEntry route)
    {
        var profiles = _ipProfileStore.ReadAllProfiles();
        return profiles
            .Where(profile => profile.Routes.Any(profileRoute => RoutesAreEquivalent(profileRoute, route)))
            .ToList();
    }

    private List<IpProfile> FindIpProfilesUsingRouteProfile(string routeProfileName)
    {
        var profileName = routeProfileName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(profileName))
            return [];

        var profiles = _ipProfileStore.ReadAllProfiles();
        return profiles
            .Where(profile => string.Equals(
                profile.LinkedRouteProfileName?.Trim(),
                profileName,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private int RefreshLinkedIpProfiles(IEnumerable<IpProfile> profiles)
    {
        var updated = 0;

        foreach (var profile in profiles)
        {
            _ipProfileStore.SaveProfile(profile, profile.Name);
            updated++;
        }

        return updated;
    }

    private int CleanupIpProfilesAfterRouteProfileDelete(IEnumerable<IpProfile> profiles)
    {
        var updated = 0;

        foreach (var profile in profiles)
        {
            profile.LinkedRouteProfileName = string.Empty;
            profile.RoutesEnabled = false;
            profile.Routes.Clear();
            _ipProfileStore.SaveProfile(profile, profile.Name);
            updated++;
        }

        return updated;
    }

    private int RemoveRouteFromIpProfiles(RouteEntry route, IEnumerable<IpProfile> profiles)
    {
        var updatedProfiles = 0;

        foreach (var profile in profiles)
        {
            var matchingRoutes = profile.Routes
                .Where(profileRoute => RoutesAreEquivalent(profileRoute, route))
                .ToList();

            if (matchingRoutes.Count == 0)
                continue;

            foreach (var matchingRoute in matchingRoutes)
                profile.Routes.Remove(matchingRoute);

            _ipProfileStore.SaveProfile(profile, profile.Name);
            updatedProfiles++;
        }

        return updatedProfiles;
    }

    private static bool RoutesAreEquivalent(RouteEntry left, RouteEntry right)
    {
        return string.Equals((left.Destination ?? string.Empty).Trim(), (right.Destination ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals((left.SubnetMask ?? string.Empty).Trim(), (right.SubnetMask ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            && string.Equals((left.Gateway ?? string.Empty).Trim(), (right.Gateway ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
            && NormalizeMetric(left.Metric) == NormalizeMetric(right.Metric);
    }

    private static int NormalizeMetric(int metric) => metric > 0 ? metric : 1;

    private void SwitchToIpConfigWithProfile(string profileName)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window window)
            return;

        if (window.DataContext is not MainWindowViewModel mainWindowViewModel)
            return;

        mainWindowViewModel.NavigateToCommand.Execute("IpConfig");

        if (mainWindowViewModel.CurrentPage is IpConfigPage ipConfigPage)
        {
            var target = ipConfigPage.ViewModel.IpProfiles
                .FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                ipConfigPage.ViewModel.SelectedProfile = target;
            }
        }
    }

    private async Task<RouteUsageDeleteDecision> ShowRouteUsageDeleteDialog(RouteEntry route, List<IpProfile> profilesUsingRoute)
    {
        var decision = RouteUsageDeleteDecision.Cancel;
        Window? dialog = null;

        var profileList = string.Join("\n- ", profilesUsingRoute.Select(p => p.Name));
        var message =
            $"Diese Route wird verwendet:\n{route.Destination} / {route.SubnetMask} via {route.Gateway}\n\n" +
            $"Verwendet in {profilesUsingRoute.Count} IP-Profil(en):\n- {profileList}\n\n" +
            "Bitte wählen Sie eine Aktion:";

        dialog = new Window
        {
            Title = "Route wird verwendet",
            Width = 560,
            Height = 320,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Content = "Nein (Abbrechen)",
                                Width = 140,
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.Cancel;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Zum Profil wechseln",
                                Width = 160,
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.SwitchToProfile;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Ja (Löschen + Bereinigen)",
                                Width = 180,
                                Classes = { "accent" },
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.DeleteAndCleanup;
                                    dialog?.Close();
                                })
                            }
                        }
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }

        return decision;
    }

    private async Task<RouteUsageDeleteDecision> ShowProfileRouteUsageDeleteDialog(
        RouteProfile routeProfile,
        RouteEntry route,
        List<IpProfile> linkedIpProfiles)
    {
        var decision = RouteUsageDeleteDecision.Cancel;
        Window? dialog = null;

        var profileList = string.Join("\n- ", linkedIpProfiles.Select(p => p.Name));
        var message =
            $"Diese Profil-Route wird verwendet:\n{route.Destination} / {route.SubnetMask} via {route.Gateway}\n\n" +
            $"Routen-Profil: {routeProfile.Name}\n" +
            $"Verwendet in {linkedIpProfiles.Count} IP-Profil(en):\n- {profileList}\n\n" +
            "Bitte wählen Sie eine Aktion:";

        dialog = new Window
        {
            Title = "Profil-Route wird verwendet",
            Width = 620,
            Height = 340,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Content = "Nein (Abbrechen)",
                                Width = 140,
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.Cancel;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Zum Profil wechseln",
                                Width = 160,
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.SwitchToProfile;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Ja (Löschen + Bereinigen)",
                                Width = 180,
                                Classes = { "accent" },
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.DeleteAndCleanup;
                                    dialog?.Close();
                                })
                            }
                        }
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }

        return decision;
    }

    private async Task<RouteUsageDeleteDecision> ShowRouteProfileUsageDeleteDialog(
        RouteProfile routeProfile,
        List<IpProfile> linkedIpProfiles)
    {
        var decision = RouteUsageDeleteDecision.Cancel;
        Window? dialog = null;

        var profileList = string.Join("\n- ", linkedIpProfiles.Select(p => p.Name));
        var message =
            $"Das Routen-Profil '{routeProfile.Name}' wird verwendet.\n\n" +
            $"Verwendet in {linkedIpProfiles.Count} IP-Profil(en):\n- {profileList}\n\n" +
            "Bitte wählen Sie eine Aktion:";

        dialog = new Window
        {
            Title = "Routen-Profil wird verwendet",
            Width = 620,
            Height = 320,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Content = "Nein (Abbrechen)",
                                Width = 140,
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.Cancel;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Zum Profil wechseln",
                                Width = 160,
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.SwitchToProfile;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Ja (Löschen + Bereinigen)",
                                Width = 180,
                                Classes = { "accent" },
                                Command = new RelayCommand(() =>
                                {
                                    decision = RouteUsageDeleteDecision.DeleteAndCleanup;
                                    dialog?.Close();
                                })
                            }
                        }
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }

        return decision;
    }

    private async void AddRoute_Click(object? sender, RoutedEventArgs e)
    {
        AddStatusText.IsVisible = false;

        var route = new RouteEntry
        {
            Destination = AddDestinationBox.Text?.Trim() ?? string.Empty,
            SubnetMask = AddSubnetBox.Text?.Trim() ?? string.Empty,
            Gateway = AddGatewayBox.Text?.Trim() ?? string.Empty,
            Metric = int.TryParse(AddMetricBox.Text?.Trim(), out var m) && m > 0 ? m : 1
        };

        LogHandler.LogUserEvent("Routes", "ButtonClick", "RouteAdd", new Dictionary<string, string?>
        {
            ["Destination"] = route.Destination,
            ["SubnetMask"] = route.SubnetMask,
            ["Gateway"] = route.Gateway,
            ["Metric"] = route.Metric.ToString()
        });

        LogHandler.LogSystemMessage(LogLevel.INFO, "Routes", 
            $"Route hinzufügen: {route.Destination} mask {route.SubnetMask} via {route.Gateway} metric {route.Metric}");

        var (success, error) = _networkConfigService.AddRouteStandalone(route);
        if (!success)
        {
            LogHandler.LogErrorMessage("Routes", $"Route hinzufügen fehlgeschlagen: {error}");
            AddStatusText.Text = error ?? "Fehler beim Hinzufügen der Route";
            AddStatusText.IsVisible = true;
            return;
        }

        AddDestinationBox.Text = string.Empty;
        AddSubnetBox.Text = string.Empty;
        AddGatewayBox.Text = string.Empty;
        AddMetricBox.Text = string.Empty;

        await LoadRoutesAsync();
    }

    private async Task<bool> ShowConfirmDialog(string title, string message)
    {
        bool result = false;
        Window? dialog = null;

        dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Content = "Abbrechen",
                                Width = 100,
                                Command = new RelayCommand(() => 
                                {
                                    result = false;
                                    dialog?.Close();
                                })
                            },
                            new Button
                            {
                                Content = "Löschen",
                                Width = 100,
                                Classes = { "accent" },
                                Command = new RelayCommand(() => 
                                {
                                    result = true;
                                    dialog?.Close();
                                })
                            }
                        }
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }

        return result;
    }

    private async Task ShowErrorDialog(string title, string message)
    {
        Window? dialog = null;

        dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Background = GetThemeBrush("AppBackgroundBrush", Colors.White),
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Button
                    {
                        Content = "OK",
                        Width = 100,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Command = new RelayCommand(() => dialog?.Close())
                    }
                }
            }
        };

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            await dialog.ShowDialog(parentWindow);
        }
        else
        {
            dialog.Show();
        }
    }

    private static IBrush GetThemeBrush(string resourceKey, Color fallbackColor)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is IBrush brush)
        {
            return brush;
        }

        return new SolidColorBrush(fallbackColor);
    }

    // Einfache RelayCommand-Implementierung für Dialoge
    private class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

#pragma warning disable CS0067
        public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}

// Extension-Methode für FindDescendantOfType
internal static class ControlExtensions
{
    public static T? FindDescendantOfType<T>(this Control control) where T : Control
    {
        if (control is T result)
        {
            return result;
        }

        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is Control childControl)
                {
                    var found = FindDescendantOfType<T>(childControl);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
        }
        else if (control is Decorator decorator && decorator.Child is Control decoratorChild)
        {
            return FindDescendantOfType<T>(decoratorChild);
        }
        else if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
        {
            return FindDescendantOfType<T>(contentChild);
        }

        return null;
    }
}
