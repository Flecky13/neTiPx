using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.ViewModels;
using System.Diagnostics;
using System.Linq;

namespace neTiPx.Views
{
    public partial class IpConfigPage : Page
    {
        private bool _isHandlingSelection;
        private bool _isPageLoaded;
        private bool _isWindowActive;
        private AppWindow? _mainAppWindow;

        public IpConfigPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[ReachabilityDebug][IpConfigPage] Loaded");

            _isPageLoaded = true;
            _mainAppWindow = WindowHelper.GetAppWindow(App.MainWindow);
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed += MainAppWindow_Changed;
            }

            App.MainWindow.Activated += MainWindow_Activated;
            _isWindowActive = _mainAppWindow?.IsVisible == true;

            if (DataContext is IpConfigViewModel viewModel)
            {
                ProfileListView.SelectedItem = viewModel.SelectedProfile;
            }

            UpdateMonitoringState();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[ReachabilityDebug][IpConfigPage] Unloaded");

            _isPageLoaded = false;
            App.MainWindow.Activated -= MainWindow_Activated;
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed -= MainAppWindow_Changed;
            }

            if (DataContext is IpConfigViewModel viewModel)
            {
                viewModel.StopConnectionMonitoring();
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
            Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] WindowActivated state={args.WindowActivationState}");
            UpdateMonitoringState();
        }

        private void MainAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidVisibilityChange)
            {
                Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] WindowVisibility changed isVisible={sender.IsVisible}");
                UpdateMonitoringState();
            }
        }

        private void UpdateMonitoringState()
        {
            var isWindowVisible = _mainAppWindow?.IsVisible ?? false;
            var shouldMonitor = _isPageLoaded && _isWindowActive && isWindowVisible;
            Debug.WriteLine($"[ReachabilityDebug][IpConfigPage] MonitoringState loaded={_isPageLoaded} active={_isWindowActive} visible={isWindowVisible} => shouldMonitor={shouldMonitor}");

            if (DataContext is not IpConfigViewModel viewModel)
            {
                return;
            }

            if (shouldMonitor)
            {
                viewModel.StartConnectionMonitoring();
            }
            else
            {
                viewModel.StopConnectionMonitoring();
            }
        }

        private async void ProfileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isHandlingSelection || DataContext is not IpConfigViewModel viewModel)
            {
                return;
            }

            var currentProfile = viewModel.SelectedProfile;
            var nextProfile = e.AddedItems.OfType<IpProfile>().FirstOrDefault();

            if (nextProfile == null || ReferenceEquals(nextProfile, currentProfile))
            {
                return;
            }

            _isHandlingSelection = true;
            try
            {
                if (currentProfile?.IsDirty == true)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Ungespeicherte Änderungen",
                        Content = "Änderungen speichern, bevor das Profil verlassen wird?",
                        PrimaryButtonText = "Speichern",
                        CloseButtonText = "Nein",
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        if (!viewModel.SaveCurrentProfileForProfileSwitch())
                        {
                            ProfileListView.SelectedItem = currentProfile;
                            return;
                        }

                        currentProfile.IsDirty = false;
                    }
                    else
                    {
                        currentProfile.IsDirty = false;
                        viewModel.DiscardCurrentProfileChangesMarker();
                    }
                }

                viewModel.SelectedProfile = nextProfile;
                ProfileListView.SelectedItem = nextProfile;
            }
            finally
            {
                _isHandlingSelection = false;
            }
        }

        private void RoutesButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not IpConfigViewModel viewModel || viewModel.SelectedProfile == null)
            {
                return;
            }

            var routeWindow = new RouteConfigWindow(viewModel.SelectedProfile, viewModel);
            routeWindow.Activate();
        }
    }
}
