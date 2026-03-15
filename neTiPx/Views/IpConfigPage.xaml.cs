using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using neTiPx.ViewModels;
using System;
using System.Diagnostics;
using System.Linq;
using System.Collections;

namespace neTiPx.Views
{
    public partial class IpConfigPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;

        private bool _isHandlingSelection;
        private bool _isPageLoaded;
        private bool _isWindowActive;
        private AppWindow? _mainAppWindow;

        public IpConfigPage()
        {
            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            _lm.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            if (IpConfigTitleText != null) IpConfigTitleText.Text = _lm.Lang("IPCONFIG_TITLE");
            if (IpConfigSubtitleText != null) IpConfigSubtitleText.Text = _lm.Lang("IPCONFIG_SUBTITLE");
            if (IpProfilesTitle != null) IpProfilesTitle.Text = _lm.Lang("IPCONFIG_IP_PROFILES");
            if (NewProfileButtonText != null) NewProfileButtonText.Text = _lm.Lang("IPCONFIG_NEW_PROFILE");
            if (ProfileSettingsTitle != null) ProfileSettingsTitle.Text = _lm.Lang("IPCONFIG_PROFILE_SETTINGS");

            SetToolTips();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[ReachabilityDebug][IpConfigPage] Loaded");

            UpdateLanguage();

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

            _lm.LanguageChanged -= OnLanguageChanged;

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
                return;

            if (shouldMonitor)
                viewModel.StartConnectionMonitoring();
            else
                viewModel.StopConnectionMonitoring();
        }

        private void SetToolTips()
        {
            if (ProfileListView != null)
            {
                foreach (var item in ProfileListView.Items)
                {
                    var container = ProfileListView.ContainerFromItem(item) as ListViewItem;

                    if (container != null)
                    {
                        var deleteButton = FindChildByName(container, "DeleteProfileButton") as Button;

                        if (deleteButton != null)
                        {
                            ToolTipService.SetToolTip(deleteButton, _lm.Lang("IPCONFIG_TOOLTIP_DELETE_PROFILE"));
                        }
                    }
                }
            }

            if (RoutesButton != null)
                ToolTipService.SetToolTip(RoutesButton, _lm.Lang("IPCONFIG_TOOLTIP_ROUTES"));

            if (GatewayTextBox != null)
                ToolTipService.SetToolTip(GatewayTextBox, _lm.Lang("IPCONFIG_TOOLTIP_GATEWAY"));

            if (Dns1TextBox != null)
                ToolTipService.SetToolTip(Dns1TextBox, _lm.Lang("IPCONFIG_TOOLTIP_DNS1"));

            if (Dns2TextBox != null)
                ToolTipService.SetToolTip(Dns2TextBox, _lm.Lang("IPCONFIG_TOOLTIP_DNS2"));

            if (IpAddressesRepeater != null && IpAddressesRepeater.ItemsSource is IEnumerable items)
            {
                int index = 0;

                foreach (var element in items)
                {
                    var container = IpAddressesRepeater.TryGetElement(index);

                    if (container != null)
                    {
                        var ipBox = FindChildByName(container, "IpAddressTextBox") as TextBox;
                        if (ipBox != null)
                            ToolTipService.SetToolTip(ipBox, _lm.Lang("IPCONFIG_TOOLTIP_IP"));

                        var subnetBox = FindChildByName(container, "SubnetMaskTextBox") as TextBox;
                        if (subnetBox != null)
                            ToolTipService.SetToolTip(subnetBox, _lm.Lang("IPCONFIG_TOOLTIP_SUBNET"));

                        var removeBtn = FindChildByName(container, "RemoveIpButton") as Button;
                        if (removeBtn != null)
                            ToolTipService.SetToolTip(removeBtn, _lm.Lang("IPCONFIG_TOOLTIP_REMOVE_IP"));
                    }

                    index++;
                }
            }

            if (ApplyButton != null)
                ToolTipService.SetToolTip(ApplyButton, _lm.Lang("IPCONFIG_TOOLTIP_APPLY"));

            if (SaveButton != null)
                ToolTipService.SetToolTip(SaveButton, _lm.Lang("IPCONFIG_TOOLTIP_SAVE"));
        }

        private FrameworkElement? FindChildByName(DependencyObject parent, string name)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is FrameworkElement fe && fe.Name == name)
                    return fe;

                var result = FindChildByName(child, name);

                if (result != null)
                    return result;
            }

            return null;
        }

        private async void ProfileListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isHandlingSelection || DataContext is not IpConfigViewModel viewModel)
                return;

            var currentProfile = viewModel.SelectedProfile;
            var nextProfile = e.AddedItems.OfType<IpProfile>().FirstOrDefault();

            if (nextProfile == null || ReferenceEquals(nextProfile, currentProfile))
                return;

            _isHandlingSelection = true;

            try
            {
                if (currentProfile?.IsDirty == true)
                {
                    var dialog = new ContentDialog
                    {
                        Title = _lm.Lang("IPCONFIG_UNSAVED_TITLE"),
                        Content = _lm.Lang("IPCONFIG_UNSAVED_CONTENT"),
                        PrimaryButtonText = _lm.Lang("IPCONFIG_SAVE"),
                        CloseButtonText = _lm.Lang("IPCONFIG_NO"),
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = this.XamlRoot
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
                return;

            var routeWindow = new RouteConfigWindow(viewModel.SelectedProfile, viewModel);

            routeWindow.Activate();
        }
    }
}