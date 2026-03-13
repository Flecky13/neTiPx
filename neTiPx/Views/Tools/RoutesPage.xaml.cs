using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Models;
using neTiPx.Services;

namespace neTiPx.Views.Tools
{
    public sealed partial class RoutesPage : Page
    {
        private readonly NetworkConfigService _networkConfigService = new NetworkConfigService();
        private readonly List<RouteEntry> _allRoutes = new List<RouteEntry>();

        private SortColumn _sortColumn = SortColumn.Destination;
        private bool _sortAscending = true;

        public ObservableCollection<RouteEntry> Routes { get; } = new ObservableCollection<RouteEntry>();
        public ObservableCollection<RouteEntry> FilteredRoutes { get; } = new ObservableCollection<RouteEntry>();

        private enum SortColumn
        {
            Destination,
            SubnetMask,
            Gateway,
            Metric
        }

        public RoutesPage()
        {
            InitializeComponent();
            UpdateSortIndicators();
            Loaded += RoutesPage_Loaded;
        }

        private async void RoutesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadRoutesAsync();
        }

        private async Task LoadRoutesAsync()
        {
            RoutesStatusText.Text = "Wird eingelesen...";

            await Task.Yield();
            var (success, routes, error) = _networkConfigService.ReadAllPersistentRoutes();

            Routes.Clear();
            if (success)
            {
                _allRoutes.Clear();
                _allRoutes.AddRange(routes);

                foreach (var r in routes)
                {
                    Routes.Add(r);
                }

                ApplyFilterAndSort();
            }
            else
            {
                RoutesStatusText.Text = error ?? "Fehler beim Einlesen.";
            }
        }

        private void ApplyFilterAndSort()
        {
            IEnumerable<RouteEntry> query = _allRoutes;

            var filterText = DestinationFilterBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(filterText))
            {
                if (IPAddress.TryParse(filterText, out var destinationIp) && destinationIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
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
                RoutesStatusText.Text = $"{FilteredRoutes.Count} Eintrag/Einträge gefunden.";
            }
            else if (IPAddress.TryParse(filterText, out var parsed) && parsed.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                RoutesStatusText.Text = $"{FilteredRoutes.Count} passende Route(n) für Ziel {filterText}.";
            }
            else
            {
                RoutesStatusText.Text = "Ungültige Ziel-IP. Bitte eine gültige IPv4-Adresse eingeben.";
            }
        }

        private static IEnumerable<RouteEntry> FilterCandidateRoutesForDestination(IEnumerable<RouteEntry> routes, IPAddress destinationIp)
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
            if (!IPAddress.TryParse(destination, out var routeDestination) || routeDestination.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }

            if (!IPAddress.TryParse(subnetMask, out var routeMask) || routeMask.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
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
            if (!IPAddress.TryParse(subnetMask, out var maskIp) || maskIp.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
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

        private void DestinationFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            if (DestinationFilterBox != null)
            {
                DestinationFilterBox.Text = string.Empty;
            }

            ApplyFilterAndSort();
        }

        private void SortDestination_Click(object sender, RoutedEventArgs e)
        {
            ToggleSort(SortColumn.Destination);
        }

        private void SortSubnetMask_Click(object sender, RoutedEventArgs e)
        {
            ToggleSort(SortColumn.SubnetMask);
        }

        private void SortGateway_Click(object sender, RoutedEventArgs e)
        {
            ToggleSort(SortColumn.Gateway);
        }

        private void SortMetric_Click(object sender, RoutedEventArgs e)
        {
            ToggleSort(SortColumn.Metric);
        }

        private async void RefreshRoutes_Click(object sender, RoutedEventArgs e)
        {
            await LoadRoutesAsync();
        }

        private async void DeleteRoute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not RouteEntry route)
                return;

            if (!route.CanDeleteFromSystem)
                return;

            var dialog = new ContentDialog
            {
                Title = "Route löschen",
                Content = $"Soll die Route '{route.Destination} / {route.SubnetMask} via {route.Gateway}' dauerhaft entfernt werden?",
                PrimaryButtonText = "Löschen",
                CloseButtonText = "Abbrechen",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            var (success, error) = _networkConfigService.DeleteRoute(route);
            if (!success)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Fehler beim Löschen",
                    Content = error ?? "Route konnte nicht entfernt werden.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                };
                await errorDialog.ShowAsync();
                return;
            }

            await LoadRoutesAsync();
        }

        private async void AddRoute_Click(object sender, RoutedEventArgs e)
        {
            AddStatusText.Visibility = Visibility.Collapsed;

            var route = new RouteEntry
            {
                Destination = AddDestinationBox.Text?.Trim() ?? string.Empty,
                SubnetMask = AddSubnetBox.Text?.Trim() ?? string.Empty,
                Gateway = AddGatewayBox.Text?.Trim() ?? string.Empty,
                Metric = int.TryParse(AddMetricBox.Text?.Trim(), out var m) && m > 0 ? m : 1
            };

            var (success, error) = _networkConfigService.AddRouteStandalone(route);
            if (!success)
            {
                AddStatusText.Text = error ?? "Route konnte nicht hinzugefügt werden.";
                AddStatusText.Visibility = Visibility.Visible;
                return;
            }

            AddDestinationBox.Text = string.Empty;
            AddSubnetBox.Text = string.Empty;
            AddGatewayBox.Text = string.Empty;
            AddMetricBox.Text = string.Empty;

            await LoadRoutesAsync();
        }
    }
}
