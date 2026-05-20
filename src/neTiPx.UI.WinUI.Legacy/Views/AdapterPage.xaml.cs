using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using neTiPx.Helpers;
using neTiPx.Services;
using neTiPx.ViewModels;
using System;
using System.Diagnostics;

namespace neTiPx.Views
{
    public partial class AdapterPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private bool _isPageLoaded;
        private bool _isWindowActive;
        private AppWindow? _mainAppWindow;

        public AdapterPage()
        {
            InitializeComponent();
            Loaded += AdapterPage_Loaded;
            Unloaded += AdapterPage_Unloaded;
            _lm.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            if (AdapterTitleText != null) AdapterTitleText.Text = _lm.Lang("ADAPTER_TITLE");
            if (AdapterSubtitleText != null) AdapterSubtitleText.Text = _lm.Lang("ADAPTER_SUBTITLE");
            if (Nic1Title != null) Nic1Title.Text = _lm.Lang("ADAPTER_NIC1");
            if (LblName1 != null) LblName1.Text = _lm.Lang("ADAPTER_LBL_NAME");
            if (LblMac1 != null) LblMac1.Text = "MAC: ";
            if (LblIpv4Info1 != null) LblIpv4Info1.Text = _lm.Lang("ADAPTER_LBL_IPV4_INFO");
            if (LblIpv4_1 != null) LblIpv4_1.Text = "IPv4: ";
            if (LblGateway1 != null) LblGateway1.Text = "Gateway:";
            if (LblDns4_1 != null) LblDns4_1.Text = "DNS4: ";
            if (LblIpv6Info1 != null) LblIpv6Info1.Text = _lm.Lang("ADAPTER_LBL_IPV6_INFO");
            if (LblIpv6_1 != null) LblIpv6_1.Text = "IPv6: ";
            if (LblGateway6_1 != null) LblGateway6_1.Text = "Gateway 6: ";
            if (LblDns6_1 != null) LblDns6_1.Text = "DNS6: ";
            if (Nic2Title != null) Nic2Title.Text = _lm.Lang("ADAPTER_NIC2");
            if (LblName2 != null) LblName2.Text = _lm.Lang("ADAPTER_LBL_NAME");
            if (LblMac2 != null) LblMac2.Text = "MAC:";
            if (LblIpv4Info2 != null) LblIpv4Info2.Text = _lm.Lang("ADAPTER_LBL_IPV4_INFO");
            if (LblIpv4_2 != null) LblIpv4_2.Text = "IPv4: ";
            if (LblGateway2 != null) LblGateway2.Text = "Gateway:";
            if (LblDns4_2 != null) LblDns4_2.Text = "DNS4: ";
            if (LblIpv6Info2 != null) LblIpv6Info2.Text = _lm.Lang("ADAPTER_LBL_IPV6_INFO");
            if (LblIpv6_2 != null) LblIpv6_2.Text = "IPv6: ";
            if (LblGateway6_2 != null) LblGateway6_2.Text = "Gateway 6: ";
            if (LblDns6_2 != null) LblDns6_2.Text = "DNS6: ";
            if (StatusNic1 != null) StatusNic1.Text = _lm.Lang("ADAPTER_STATUS_NIC1");
            if (StatusNic2 != null) StatusNic2.Text = _lm.Lang("ADAPTER_STATUS_NIC2");
        }

        private void AdapterPage_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[ReachabilityDebug][AdapterPage] Loaded");
            UpdateLanguage();

            _isPageLoaded = true;
            _mainAppWindow = WindowHelper.GetAppWindow(App.MainWindow);
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed += MainAppWindow_Changed;
            }

            App.MainWindow.Activated += MainWindow_Activated;
            _isWindowActive = _mainAppWindow?.IsVisible == true;
            UpdateMonitoringState();

            if (DataContext is AdapterViewModel vm)
            {
                vm.RegisterNetworkChangeEvents();
            }
        }

        private void AdapterPage_Unloaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("[ReachabilityDebug][AdapterPage] Unloaded");
            _lm.LanguageChanged -= OnLanguageChanged;

            _isPageLoaded = false;
            App.MainWindow.Activated -= MainWindow_Activated;

            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed -= MainAppWindow_Changed;
            }

            if (DataContext is AdapterViewModel viewModel)
            {
                viewModel.StopConnectionMonitoring();
                viewModel.UnregisterNetworkChangeEvents();
            }
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
            Debug.WriteLine($"[ReachabilityDebug][AdapterPage] WindowActivated state={args.WindowActivationState}");
            UpdateMonitoringState();
        }

        private void MainAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidVisibilityChange)
            {
                Debug.WriteLine($"[ReachabilityDebug][AdapterPage] WindowVisibility changed isVisible={sender.IsVisible}");
                UpdateMonitoringState();
            }
        }

        private void UpdateMonitoringState()
        {
            var isWindowVisible = _mainAppWindow?.IsVisible ?? false;
            var shouldMonitor = _isPageLoaded && _isWindowActive && isWindowVisible;

            Debug.WriteLine($"[ReachabilityDebug][AdapterPage] MonitoringState loaded={_isPageLoaded} active={_isWindowActive} visible={isWindowVisible} => shouldMonitor={shouldMonitor}");

            if (DataContext is not AdapterViewModel viewModel)
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
    }
}