using System;
using System.Collections.ObjectModel;
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

        public ObservableCollection<RouteEntry> Routes { get; } = new ObservableCollection<RouteEntry>();

        public RoutesPage()
        {
            InitializeComponent();
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
                foreach (var r in routes)
                {
                    Routes.Add(r);
                }

                RoutesStatusText.Text = $"{routes.Count} Eintrag/Einträge gefunden.";
            }
            else
            {
                RoutesStatusText.Text = error ?? "Fehler beim Einlesen.";
            }
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
