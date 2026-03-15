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
