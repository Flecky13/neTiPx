using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Models;

namespace neTiPx.Views
{
    public sealed partial class RouteConfigDialogContent : UserControl
    {
        public ObservableCollection<RouteEntry> Routes { get; }

        public RouteConfigDialogContent(IEnumerable<RouteEntry> sourceRoutes)
        {
            Routes = new ObservableCollection<RouteEntry>(sourceRoutes.Select(CloneRoute));
            InitializeComponent();
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

        private void RemoveRoute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not RouteEntry route)
            {
                return;
            }

            Routes.Remove(route);
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
