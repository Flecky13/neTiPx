using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using neTiPx.ViewModels;
using Windows.Graphics;

namespace neTiPx.Views
{
    public sealed class RouteConfigWindow : Window
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private const int FixedWindowWidth = 690;
        private const int FixedWindowHeight = 900;
        private readonly IpProfile _profile;
        private readonly IpConfigViewModel _viewModel;
        private readonly NetworkConfigService _networkConfigService = new NetworkConfigService();
        private readonly RouteConfigDialogContent _content;
        private Button? _cancelButton;
        private Button? _applyButton;

        public RouteConfigWindow(IpProfile profile, IpConfigViewModel viewModel)
        {
            _profile = profile;
            _viewModel = viewModel;
            _content = new RouteConfigDialogContent(profile.Routes, DeleteRouteImmediately, AddRouteImmediately, ReloadSystemRoutes);
            _lm.LanguageChanged += OnLanguageChanged;
            Closed += RouteConfigWindow_Closed;

            Content = CreateLayout();
            UpdateLanguage();

            ConfigureWindow();
        }

        private static string T(string key)
        {
            return _lm.Lang(key);
        }

        private void RouteConfigWindow_Closed(object sender, WindowEventArgs args)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            Title = T("ROUTECFG_WINDOW_TITLE");
            if (_cancelButton != null)
            {
                _cancelButton.Content = T("ROUTECFG_BUTTON_CANCEL");
            }

            if (_applyButton != null)
            {
                _applyButton.Content = T("ROUTECFG_BUTTON_APPLY");
            }
        }

        private (bool success, string? message) DeleteRouteImmediately(RouteEntry route)
        {
            var result = _networkConfigService.RemoveRoute(_profile, route);
            if (!result.success)
            {
                return result;
            }

            var existingRoute = _profile.Routes.FirstOrDefault(r =>
                string.Equals(r.Destination?.Trim(), route.Destination?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.SubnetMask?.Trim(), route.SubnetMask?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.Gateway?.Trim(), route.Gateway?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                r.Metric == route.Metric);

            if (existingRoute != null)
            {
                _profile.Routes.Remove(existingRoute);
            }

            _viewModel.RevalidateProfile();
            return (true, result.error);
        }

        private (bool success, string? message) AddRouteImmediately(RouteEntry route)
        {
            var sanitizedRoute = new RouteEntry
            {
                Destination = route.Destination?.Trim() ?? string.Empty,
                SubnetMask = route.SubnetMask?.Trim() ?? string.Empty,
                Gateway = route.Gateway?.Trim() ?? string.Empty,
                Metric = route.Metric > 0 ? route.Metric : 1
            };

            var result = _networkConfigService.AddRoute(_profile, sanitizedRoute);
            if (!result.success)
            {
                return result;
            }

            var existingRoute = _profile.Routes.FirstOrDefault(r =>
                string.Equals(r.Destination?.Trim(), sanitizedRoute.Destination, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.SubnetMask?.Trim(), sanitizedRoute.SubnetMask, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.Gateway?.Trim(), sanitizedRoute.Gateway, StringComparison.OrdinalIgnoreCase) &&
                (r.Metric > 0 ? r.Metric : 1) == sanitizedRoute.Metric);

            if (existingRoute == null)
            {
                _profile.Routes.Add(new RouteEntry
                {
                    Destination = sanitizedRoute.Destination,
                    SubnetMask = sanitizedRoute.SubnetMask,
                    Gateway = sanitizedRoute.Gateway,
                    Metric = sanitizedRoute.Metric
                });
            }

            _viewModel.RevalidateProfile();
            return (true, result.error);
        }

        private (bool success, System.Collections.Generic.List<RouteEntry> routes, string? error) ReloadSystemRoutes()
        {
            var result = _networkConfigService.ReadStaticRoutes(_profile);
            if (!string.IsNullOrWhiteSpace(result.debugInfo))
            {
                Debug.WriteLine("[RouteConfig][Refresh]\n" + result.debugInfo);
            }

            return (result.success, result.routes, result.error);
        }

        private UIElement CreateLayout()
        {
            var root = new Grid
            {
                Background = GetBrush("AppBackgroundBrush"),
                RequestedTheme = GetMainWindowTheme()
            };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentBorder = new Border
            {
                Padding = new Thickness(0),
                Child = _content
            };
            Grid.SetRow(contentBorder, 0);
            root.Children.Add(contentBorder);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 0, 16, 16)
            };

            _cancelButton = new Button
            {
                Content = T("ROUTECFG_BUTTON_CANCEL"),
                MinWidth = 120,
                Style = GetStyle("PrimaryAction")
            };
            _cancelButton.Click += (_, _) => Close();

            _applyButton = new Button
            {
                Content = T("ROUTECFG_BUTTON_APPLY"),
                MinWidth = 120,
                Style = GetStyle("AccentButtonStyle")
            };

            // Für helle Farbprofile (Weiß/Prinzessin) wird der Button-Text explizit weiß gesetzt,
            // da der globale implizite TextBlock-Style (AppTextBrush) dort sonst den dunklen Text durchdrückt.
            // Bei dunklen Profilen ergibt sich weiß automatisch über den AccentButtonStyle.
            if (GetMainWindowTheme() == ElementTheme.Light)
            {
                _applyButton.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255));
            }

            _applyButton.Click += ApplyButton_Click;

            buttonPanel.Children.Add(_cancelButton);
            buttonPanel.Children.Add(_applyButton);

            Grid.SetRow(buttonPanel, 1);
            root.Children.Add(buttonPanel);

            return root;
        }

        private static Style? GetStyle(string key)
        {
            return Application.Current.Resources.TryGetValue(key, out var value) ? value as Style : null;
        }

        private static Brush? GetBrush(string key)
        {
            return Application.Current.Resources.TryGetValue(key, out var value) ? value as Brush : null;
        }

        private static ElementTheme GetMainWindowTheme()
        {
            if (App.MainWindow.Content is FrameworkElement mainRoot)
            {
                return mainRoot.RequestedTheme;
            }

            return ElementTheme.Default;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            var routes = _content.GetSanitizedRoutes();

            _profile.Routes.Clear();
            foreach (var route in routes)
            {
                _profile.Routes.Add(route);
            }

            _viewModel.RevalidateProfile();
            Close();
        }

        private void ConfigureWindow()
        {
            var appWindow = WindowHelper.GetAppWindow(this);
            appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.SetBorderAndTitleBar(true, true);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
            }

            appWindow.Resize(new SizeInt32(FixedWindowWidth, FixedWindowHeight));

            var mainWindow = WindowHelper.GetAppWindow(App.MainWindow);
            var x = mainWindow.Position.X + Math.Max(40, (mainWindow.Size.Width - FixedWindowWidth) / 2);
            var y = mainWindow.Position.Y + Math.Max(40, (mainWindow.Size.Height - FixedWindowHeight) / 2);
            appWindow.Move(new PointInt32(x, y));
        }
    }
}
