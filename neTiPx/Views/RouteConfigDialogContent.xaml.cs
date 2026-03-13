using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Models;

namespace neTiPx.Views
{
    public sealed partial class RouteConfigDialogContent : UserControl, INotifyPropertyChanged
    {
        private readonly Func<RouteEntry, (bool success, string? message)>? _deleteRouteFromSystem;
        private readonly Func<(bool success, List<RouteEntry> routes, string? error)>? _reloadSystemRoutes;
        private string _systemRoutesStatus = "Noch nicht eingelesen.";
        private bool _isSystemRoutesLoading;

        public ObservableCollection<RouteEntry> Routes { get; }
        public ObservableCollection<RouteEntry> SystemRoutes { get; }

        public string SystemRoutesStatus
        {
            get => _systemRoutesStatus;
            set
            {
                if (!string.Equals(_systemRoutesStatus, value, StringComparison.Ordinal))
                {
                    _systemRoutesStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public RouteConfigDialogContent(
            IEnumerable<RouteEntry> sourceRoutes,
            Func<RouteEntry, (bool success, string? message)>? deleteRouteFromSystem = null,
            Func<(bool success, List<RouteEntry> routes, string? error)>? reloadSystemRoutes = null)
        {
            Routes = new ObservableCollection<RouteEntry>(sourceRoutes.Select(CloneRoute));
            SystemRoutes = new ObservableCollection<RouteEntry>();
            _deleteRouteFromSystem = deleteRouteFromSystem;
            _reloadSystemRoutes = reloadSystemRoutes;
            InitializeComponent();

            Routes.CollectionChanged += Routes_CollectionChanged;
            foreach (var route in Routes)
            {
                route.PropertyChanged += ProfileRoute_PropertyChanged;
            }

            Loaded += RouteConfigDialogContent_Loaded;
        }

        public List<RouteEntry> GetRoutes()
        {
            return Routes.Select(CloneRoute).ToList();
        }

        public List<RouteEntry> GetSanitizedRoutes()
        {
            return Routes
                .Select(route => new RouteEntry
                {
                    Destination = route.Destination?.Trim() ?? string.Empty,
                    SubnetMask = route.SubnetMask?.Trim() ?? string.Empty,
                    Gateway = route.Gateway?.Trim() ?? string.Empty,
                    Metric = route.Metric > 0 ? route.Metric : 1
                })
                .Where(route =>
                    !string.IsNullOrWhiteSpace(route.Destination) ||
                    !string.IsNullOrWhiteSpace(route.SubnetMask) ||
                    !string.IsNullOrWhiteSpace(route.Gateway))
                .ToList();
        }

        private void AddRoute_Click(object sender, RoutedEventArgs e)
        {
            Routes.Add(new RouteEntry { Metric = 1 });
        }

        private async void RemoveRoute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not RouteEntry route)
            {
                return;
            }

            if (_deleteRouteFromSystem != null)
            {
                var (success, message) = _deleteRouteFromSystem(CloneRoute(route));
                if (!success)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Route konnte nicht geloescht werden",
                        Content = message ?? "Unbekannter Fehler beim Entfernen der Route aus dem System.",
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = XamlRoot
                    };

                    await dialog.ShowAsync();
                    return;
                }

                if (!string.IsNullOrWhiteSpace(message))
                {
                    var infoDialog = new ContentDialog
                    {
                        Title = "Hinweis",
                        Content = message,
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = XamlRoot
                    };

                    await infoDialog.ShowAsync();
                }
            }

            Routes.Remove(route);
            RefreshSystemRouteMarkers();
        }

        private async void ReloadSystemRoutes_Click(object sender, RoutedEventArgs e)
        {
            await ReloadSystemRoutesInternalAsync(showErrorDialog: true);
        }

        private async System.Threading.Tasks.Task ReloadSystemRoutesInternalAsync(bool showErrorDialog)
        {
            if (_isSystemRoutesLoading)
            {
                return;
            }

            if (_reloadSystemRoutes == null)
            {
                SystemRoutesStatus = "Einlesen aktuell nicht verfuegbar.";
                return;
            }

            _isSystemRoutesLoading = true;

            var result = _reloadSystemRoutes();
            if (!result.success)
            {
                if (showErrorDialog)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Routen konnten nicht eingelesen werden",
                        Content = result.error ?? "Unbekannter Fehler.",
                        CloseButtonText = "OK",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = XamlRoot
                    };

                    await dialog.ShowAsync();
                }

                _isSystemRoutesLoading = false;
                return;
            }

            SystemRoutes.Clear();
            foreach (var route in result.routes)
            {
                SystemRoutes.Add(CloneRoute(route));
            }

            RefreshSystemRouteMarkers();

            SystemRoutesStatus = SystemRoutes.Count == 0
                ? "Keine ständigen Routen im System gefunden."
                : $"{SystemRoutes.Count} ständige Route(n) eingelesen.";

            _isSystemRoutesLoading = false;
        }

        private async void RemoveSystemRoute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not RouteEntry route)
            {
                return;
            }

            if (_deleteRouteFromSystem == null)
            {
                return;
            }

            var (success, message) = _deleteRouteFromSystem(CloneRoute(route));
            if (!success)
            {
                var dialog = new ContentDialog
                {
                    Title = "Route konnte nicht geloescht werden",
                    Content = message ?? "Unbekannter Fehler beim Entfernen der Route aus dem System.",
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };

                await dialog.ShowAsync();
                return;
            }

            SystemRoutes.Remove(route);
            RefreshSystemRouteMarkers();
            SystemRoutesStatus = SystemRoutes.Count == 0
                ? "Keine ständigen Routen im System gefunden."
                : $"{SystemRoutes.Count} ständige Route(n) geladen.";

            if (!string.IsNullOrWhiteSpace(message))
            {
                var infoDialog = new ContentDialog
                {
                    Title = "Hinweis",
                    Content = message,
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };

                await infoDialog.ShowAsync();
            }
        }

        private async void RouteConfigDialogContent_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= RouteConfigDialogContent_Loaded;
            await ReloadSystemRoutesInternalAsync(showErrorDialog: false);
        }

        private void Routes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems.OfType<RouteEntry>())
                {
                    oldItem.PropertyChanged -= ProfileRoute_PropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems.OfType<RouteEntry>())
                {
                    newItem.PropertyChanged += ProfileRoute_PropertyChanged;
                }
            }

            RefreshSystemRouteMarkers();
        }

        private void ProfileRoute_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            RefreshSystemRouteMarkers();
        }

        private void RefreshSystemRouteMarkers()
        {
            foreach (var systemRoute in SystemRoutes)
            {
                var isProfileMatch = Routes.Any(profileRoute => RoutesEqual(profileRoute, systemRoute));
                systemRoute.IsProfileMatch = isProfileMatch;
                systemRoute.CanDeleteFromSystem = !isProfileMatch;
            }
        }

        private static bool RoutesEqual(RouteEntry left, RouteEntry right)
        {
            var leftMetric = left.Metric > 0 ? left.Metric : 1;
            var rightMetric = right.Metric > 0 ? right.Metric : 1;

            return string.Equals(left.Destination?.Trim(), right.Destination?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.SubnetMask?.Trim(), right.SubnetMask?.Trim(), StringComparison.OrdinalIgnoreCase)
                && string.Equals(left.Gateway?.Trim(), right.Gateway?.Trim(), StringComparison.OrdinalIgnoreCase)
                && leftMetric == rightMetric;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static RouteEntry CloneRoute(RouteEntry route)
        {
            return new RouteEntry
            {
                Destination = route.Destination,
                SubnetMask = route.SubnetMask,
                Gateway = route.Gateway,
                Metric = route.Metric > 0 ? route.Metric : 1
            };
        }
    }
}
