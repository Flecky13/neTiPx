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
using neTiPx.Services;
using System.Globalization;

namespace neTiPx.Views
{
    public sealed partial class RouteConfigDialogContent : UserControl, INotifyPropertyChanged
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private readonly Func<RouteEntry, (bool success, string? message)>? _deleteRouteFromSystem;
        private readonly Func<RouteEntry, (bool success, string? message)>? _addRouteToSystem;
        private readonly Func<(bool success, List<RouteEntry> routes, string? error)>? _reloadSystemRoutes;
        private string _systemRoutesStatus = string.Empty;
        private bool _isSystemRoutesLoading;
        private bool _isRefreshingMarkers;

        public ObservableCollection<RouteEntry> Routes { get; }
        public ObservableCollection<RouteEntry> SystemRoutes { get; }

        private const int MaxRoutes = 8;

        public string DeleteSystemRouteToolTip { get; private set; } = string.Empty;
        public string ProfileBadgeText { get; private set; } = string.Empty;
        public string AddDestinationPlaceholder { get; private set; } = string.Empty;
        public string AddDestinationToolTip { get; private set; } = string.Empty;
        public string AddSubnetPlaceholder { get; private set; } = string.Empty;
        public string AddSubnetToolTip { get; private set; } = string.Empty;
        public string AddGatewayPlaceholder { get; private set; } = string.Empty;
        public string AddGatewayToolTip { get; private set; } = string.Empty;
        public string AddMetricPlaceholder { get; private set; } = string.Empty;
        public string AddMetricToolTip { get; private set; } = string.Empty;
        public string RemoveProfileRouteToolTip { get; private set; } = string.Empty;
        public string ApplyProfileRouteToolTip { get; private set; } = string.Empty;

        public bool CanAddRoute => Routes.Count < MaxRoutes;

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
            Func<RouteEntry, (bool success, string? message)>? addRouteToSystem = null,
            Func<(bool success, List<RouteEntry> routes, string? error)>? reloadSystemRoutes = null)
        {
            Routes = new ObservableCollection<RouteEntry>(sourceRoutes.Select(CloneRoute));
            SystemRoutes = new ObservableCollection<RouteEntry>();
            _deleteRouteFromSystem = deleteRouteFromSystem;
            _addRouteToSystem = addRouteToSystem;
            _reloadSystemRoutes = reloadSystemRoutes;
            _lm.LanguageChanged += OnLanguageChanged;
            InitializeComponent();
            UpdateLanguage();

            Routes.CollectionChanged += Routes_CollectionChanged;
            Routes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(CanAddRoute));
            foreach (var route in Routes)
            {
                route.PropertyChanged += ProfileRoute_PropertyChanged;
            }

            Loaded += RouteConfigDialogContent_Loaded;
            Unloaded += RouteConfigDialogContent_Unloaded;
        }

        private static string T(string key)
        {
            return _lm.Lang(key);
        }

        private void RouteConfigDialogContent_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
            RefreshLoadedStatusText();
        }

        private void UpdateLanguage()
        {
            if (DialogTitleText != null) DialogTitleText.Text = T("ROUTECFG_TITLE");
            if (DialogSubtitleText != null) DialogSubtitleText.Text = T("ROUTECFG_SUBTITLE");
            if (SystemSectionTitleText != null) SystemSectionTitleText.Text = T("ROUTES_SECTION_SYSTEM");
            if (ReloadSystemRoutesButtonText != null) ReloadSystemRoutesButtonText.Text = T("ROUTES_REFRESH");
            if (ReloadSystemRoutesButton != null) ToolTipService.SetToolTip(ReloadSystemRoutesButton, T("ROUTES_TOOLTIP_REFRESH"));

            if (SystemHeaderDestinationText != null) SystemHeaderDestinationText.Text = T("ROUTES_HEADER_DESTINATION");
            if (SystemHeaderSubnetText != null) SystemHeaderSubnetText.Text = T("ROUTES_HEADER_SUBNET");
            if (SystemHeaderGatewayText != null) SystemHeaderGatewayText.Text = T("ROUTES_HEADER_GATEWAY");
            if (SystemHeaderMetricText != null) SystemHeaderMetricText.Text = T("ROUTES_HEADER_METRIC");
            if (SystemHeaderActionText != null) SystemHeaderActionText.Text = T("ROUTES_HEADER_ACTION");

            if (ProfileRoutesSectionTitleText != null) ProfileRoutesSectionTitleText.Text = T("ROUTECFG_SECTION_PROFILE_ROUTES");
            if (AddRouteButtonText != null) AddRouteButtonText.Text = T("ROUTES_ADD_BUTTON");
            if (AddRouteButton != null) ToolTipService.SetToolTip(AddRouteButton, T("ROUTES_ADD_BUTTON_TOOLTIP"));

            if (ProfileHeaderDestinationText != null) ProfileHeaderDestinationText.Text = T("ROUTES_HEADER_DESTINATION");
            if (ProfileHeaderSubnetText != null) ProfileHeaderSubnetText.Text = T("ROUTES_HEADER_SUBNET");
            if (ProfileHeaderGatewayText != null) ProfileHeaderGatewayText.Text = T("ROUTES_HEADER_GATEWAY");
            if (ProfileHeaderMetricText != null) ProfileHeaderMetricText.Text = T("ROUTES_HEADER_METRIC");
            if (ProfileHeaderActionText != null) ProfileHeaderActionText.Text = T("ROUTES_HEADER_ACTION");

            DeleteSystemRouteToolTip = T("ROUTECFG_TOOLTIP_DELETE_SYSTEM_ROUTE");
            ProfileBadgeText = T("ROUTECFG_BADGE_PROFILE");
            AddDestinationPlaceholder = T("ROUTES_ADD_DEST_PLACEHOLDER");
            AddDestinationToolTip = T("ROUTES_ADD_DEST_TOOLTIP");
            AddSubnetPlaceholder = T("ROUTES_ADD_SUBNET_PLACEHOLDER");
            AddSubnetToolTip = T("ROUTES_ADD_SUBNET_TOOLTIP");
            AddGatewayPlaceholder = T("ROUTES_ADD_GATEWAY_PLACEHOLDER");
            AddGatewayToolTip = T("ROUTES_ADD_GATEWAY_TOOLTIP");
            AddMetricPlaceholder = T("ROUTES_ADD_METRIC_PLACEHOLDER");
            AddMetricToolTip = T("ROUTES_ADD_METRIC_TOOLTIP");
            RemoveProfileRouteToolTip = T("ROUTECFG_TOOLTIP_REMOVE_PROFILE_ROUTE");
            ApplyProfileRouteToolTip = T("ROUTECFG_TOOLTIP_APPLY_PROFILE_ROUTE");

            OnPropertyChanged(nameof(DeleteSystemRouteToolTip));
            OnPropertyChanged(nameof(ProfileBadgeText));
            OnPropertyChanged(nameof(AddDestinationPlaceholder));
            OnPropertyChanged(nameof(AddDestinationToolTip));
            OnPropertyChanged(nameof(AddSubnetPlaceholder));
            OnPropertyChanged(nameof(AddSubnetToolTip));
            OnPropertyChanged(nameof(AddGatewayPlaceholder));
            OnPropertyChanged(nameof(AddGatewayToolTip));
            OnPropertyChanged(nameof(AddMetricPlaceholder));
            OnPropertyChanged(nameof(AddMetricToolTip));
            OnPropertyChanged(nameof(RemoveProfileRouteToolTip));
            OnPropertyChanged(nameof(ApplyProfileRouteToolTip));

            if (string.IsNullOrWhiteSpace(SystemRoutesStatus))
            {
                SystemRoutesStatus = T("ROUTECFG_STATUS_NOT_LOADED");
            }
        }

        private void RefreshLoadedStatusText()
        {
            if (_isSystemRoutesLoading)
            {
                return;
            }

            if (_reloadSystemRoutes == null)
            {
                SystemRoutesStatus = T("ROUTECFG_STATUS_RELOAD_UNAVAILABLE");
                return;
            }

            SystemRoutesStatus = SystemRoutes.Count == 0
                ? T("ROUTECFG_STATUS_NONE_FOUND")
                : $"{SystemRoutes.Count} {T("ROUTECFG_STATUS_LOADED_COUNT")}";
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
            if (Routes.Count >= MaxRoutes)
            {
                return;
            }

            Routes.Add(new RouteEntry { Metric = 1 });
            OnPropertyChanged(nameof(CanAddRoute));
        }

        private async void RemoveRoute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not RouteEntry route)
            {
                return;
            }

            bool hasContent = !string.IsNullOrWhiteSpace(route.Destination)
                || !string.IsNullOrWhiteSpace(route.SubnetMask)
                || !string.IsNullOrWhiteSpace(route.Gateway);

            if (hasContent && _deleteRouteFromSystem != null)
            {
                var (success, message) = _deleteRouteFromSystem(CloneRoute(route));
                if (!success)
                {
                    var dialog = new ContentDialog
                    {
                        Title = T("ROUTECFG_DIALOG_REMOVE_FAILED_TITLE"),
                        Content = message ?? T("ROUTECFG_DIALOG_REMOVE_FAILED_CONTENT"),
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
                        Title = T("ROUTECFG_DIALOG_INFO_TITLE"),
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
            await ReloadSystemRoutesInternalAsync(showErrorDialog: false);
        }

        private async void ApplyRoute_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not RouteEntry route)
            {
                return;
            }

            if (_addRouteToSystem == null)
            {
                return;
            }

            var routeToApply = CloneRoute(route);
            routeToApply.Destination = routeToApply.Destination?.Trim() ?? string.Empty;
            routeToApply.SubnetMask = routeToApply.SubnetMask?.Trim() ?? string.Empty;
            routeToApply.Gateway = routeToApply.Gateway?.Trim() ?? string.Empty;
            routeToApply.Metric = routeToApply.Metric > 0 ? routeToApply.Metric : 1;

            var (success, message) = _addRouteToSystem(routeToApply);
            if (!success)
            {
                var dialog = new ContentDialog
                {
                    Title = T("ROUTECFG_DIALOG_APPLY_FAILED_TITLE"),
                    Content = message ?? T("ROUTECFG_DIALOG_APPLY_FAILED_CONTENT"),
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
                    Title = T("ROUTECFG_DIALOG_INFO_TITLE"),
                    Content = message,
                    CloseButtonText = "OK",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };

                await infoDialog.ShowAsync();
            }

            await ReloadSystemRoutesInternalAsync(showErrorDialog: false);
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
                SystemRoutesStatus = T("ROUTECFG_STATUS_RELOAD_UNAVAILABLE");
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
                        Title = T("ROUTECFG_DIALOG_RELOAD_FAILED_TITLE"),
                        Content = result.error ?? T("ROUTECFG_DIALOG_UNKNOWN_ERROR"),
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
                ? T("ROUTECFG_STATUS_NONE_FOUND")
                : $"{SystemRoutes.Count} {T("ROUTECFG_STATUS_LOADED_COUNT")}";

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
                    Title = T("ROUTECFG_DIALOG_REMOVE_FAILED_TITLE"),
                    Content = message ?? T("ROUTECFG_DIALOG_REMOVE_FAILED_CONTENT"),
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
                ? T("ROUTECFG_STATUS_NONE_FOUND")
                : $"{SystemRoutes.Count} {T("ROUTECFG_STATUS_LOADED_COUNT")}";

            if (!string.IsNullOrWhiteSpace(message))
            {
                var infoDialog = new ContentDialog
                {
                    Title = T("ROUTECFG_DIALOG_INFO_TITLE"),
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
            if (e.PropertyName == nameof(RouteEntry.ShowDeleteButton)
                || e.PropertyName == nameof(RouteEntry.ShowApplyButton))
            {
                return;
            }

            RefreshSystemRouteMarkers();
        }

        private void RefreshSystemRouteMarkers()
        {
            if (_isRefreshingMarkers)
            {
                return;
            }

            _isRefreshingMarkers = true;
            try
            {
                foreach (var systemRoute in SystemRoutes)
                {
                    var isProfileMatch = Routes.Any(profileRoute => RoutesEqual(profileRoute, systemRoute));
                    systemRoute.IsProfileMatch = isProfileMatch;
                    systemRoute.CanDeleteFromSystem = !isProfileMatch;
                }

                foreach (var profileRoute in Routes)
                {
                    bool isEmpty = string.IsNullOrWhiteSpace(profileRoute.Destination)
                        && string.IsNullOrWhiteSpace(profileRoute.SubnetMask)
                        && string.IsNullOrWhiteSpace(profileRoute.Gateway);
                    bool existsInSystem = SystemRoutes.Any(sysRoute => RoutesEqual(profileRoute, sysRoute));
                    profileRoute.ShowDeleteButton = isEmpty || existsInSystem;
                    profileRoute.ShowApplyButton = !isEmpty && !existsInSystem;
                }
            }
            finally
            {
                _isRefreshingMarkers = false;
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
