using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using neTiPx.Services;
using neTiPx.Views.Tools;
using System;

namespace neTiPx.Views
{
    public partial class ToolsPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private readonly PagesVisibilityService _pagesVisibilityService = new PagesVisibilityService();

        public ToolsPage()
        {
            InitializeComponent();
            Loaded += ToolsPage_Loaded;
            Unloaded += ToolsPage_Unloaded;
            _lm.LanguageChanged += OnLanguageChanged;

            if (PingPanel != null)
            {
                PingPanel.Navigated += PingPanel_Navigated;
            }

            // Zuerst die Config laden und apply
            ApplyPagesVisibility();

            // Standardmäßig die erste sichtbare Tool-Seite anzeigen
            if (ToolsNavView != null)
            {
                var firstVisible = ToolsNavView.MenuItems
                    .OfType<NavigationViewItem>()
                    .FirstOrDefault(item => item.Visibility == Visibility.Visible);

                if (firstVisible != null)
                {
                    ToolsNavView.SelectedItem = firstVisible;
                }
            }
        }

        private void ToolsPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateLanguage();
        }

        private void ToolsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            if (ToolsTitleText != null) ToolsTitleText.Text = _lm.Lang("TOOLS_TITLE");
            if (ToolsSubtitleText != null) ToolsSubtitleText.Text = _lm.Lang("TOOLS_SUBTITLE");
            if (ToolsNavPing != null) ToolsNavPing.Content = _lm.Lang("TOOLS_PING");
            if (ToolsNavWlan != null) ToolsNavWlan.Content = _lm.Lang("TOOLS_WLAN");
            if (ToolsNavCalculator != null) ToolsNavCalculator.Content = _lm.Lang("TOOLS_NET_CALC");
            if (ToolsNavScanner != null) ToolsNavScanner.Content = _lm.Lang("TOOLS_NET_SCAN");
            if (ToolsNavRoutes != null) ToolsNavRoutes.Content = _lm.Lang("TOOLS_ROUTES");
            if (ToolsNavLogViewer != null) ToolsNavLogViewer.Content = _lm.Lang("TOOLS_LOG_VIEWER");
                    if (ToolsNavUncPath != null) ToolsNavUncPath.Content = _lm.Lang("TOOLS_UNC_PATH");
        }

        public void RefreshVisibilityConfiguration()
        {
            ApplyPagesVisibility();

            if (ToolsNavView == null)
            {
                return;
            }

            if (ToolsNavView.SelectedItem is NavigationViewItem selectedItem
                && selectedItem.Visibility == Visibility.Visible)
            {
                return;
            }

            var firstVisible = ToolsNavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Visibility == Visibility.Visible);

            if (firstVisible != null)
            {
                ToolsNavView.SelectedItem = firstVisible;
            }
        }

        private void ApplyPagesVisibility()
        {
            _pagesVisibilityService.EnsureConfigExists();
            var visibility = _pagesVisibilityService.ReadPagesVisibility();

            if (ToolsNavView?.MenuItems == null)
            {
                return;
            }

            foreach (var menuItem in ToolsNavView.MenuItems.OfType<NavigationViewItem>())
            {
                var tag = menuItem.Tag as string;
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                var isVisible = visibility.ContainsKey(tag) && visibility[tag];
                menuItem.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ToolsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                // Alle Panels ausblenden
                if (PingPanel != null) PingPanel.Visibility = Visibility.Collapsed;
                if (WlanPanel != null) WlanPanel.Visibility = Visibility.Collapsed;
                if (NetworkCalculatorPanel != null) NetworkCalculatorPanel.Visibility = Visibility.Collapsed;
                if (NetworkScannerPanel != null) NetworkScannerPanel.Visibility = Visibility.Collapsed;
                if (RoutesPanel != null) RoutesPanel.Visibility = Visibility.Collapsed;
                if (LogViewerPanel != null) LogViewerPanel.Visibility = Visibility.Collapsed;
                                if (UncPathPanel != null) UncPathPanel.Visibility = Visibility.Collapsed;
                if (ToolsContentScrollViewer != null) ToolsContentScrollViewer.Visibility = Visibility.Visible;

                // Ausgewähltes Panel anzeigen
                switch (tag)
                {
                    case "Ping":
                        if (PingPanel != null)
                        {
                            PingPanel.Visibility = Visibility.Visible;
                            if (PingPanel.Content == null)
                            {
                                PingPanel.Navigate(typeof(PingPage));
                            }
                        }
                        SetPingPageHostActiveState(true);
                        break;
                    case "Wlan":
                        if (WlanPanel != null)
                        {
                            WlanPanel.Visibility = Visibility.Visible;
                            if (WlanPanel.Content == null)
                            {
                                WlanPanel.Navigate(typeof(WlanPage));
                            }
                        }
                        SetPingPageHostActiveState(false);
                        break;
                    case "NetworkCalculator":
                        if (NetworkCalculatorPanel != null)
                        {
                            NetworkCalculatorPanel.Visibility = Visibility.Visible;
                            if (NetworkCalculatorPanel.Content == null)
                            {
                                NetworkCalculatorPanel.Navigate(typeof(NetworkCalculatorPage));
                            }
                        }
                        SetPingPageHostActiveState(false);
                        break;
                    case "NetworkScanner":
                        if (NetworkScannerPanel != null)
                        {
                            NetworkScannerPanel.Visibility = Visibility.Visible;
                            if (NetworkScannerPanel.Content == null)
                            {
                                NetworkScannerPanel.Navigate(typeof(NetworkScannerPage));
                            }
                        }
                        SetPingPageHostActiveState(false);
                        break;
                    case "Routes":
                        if (RoutesPanel != null)
                        {
                            RoutesPanel.Visibility = Visibility.Visible;
                            if (RoutesPanel.Content == null)
                            {
                                RoutesPanel.Navigate(typeof(RoutesPage));
                            }
                        }
                        SetPingPageHostActiveState(false);
                        break;
                    case "LogViewer":
                        if (ToolsContentScrollViewer != null)
                        {
                            ToolsContentScrollViewer.Visibility = Visibility.Collapsed;
                        }
                        if (LogViewerPanel != null)
                        {
                            LogViewerPanel.Visibility = Visibility.Visible;
                            if (LogViewerPanel.Content == null)
                            {
                                LogViewerPanel.Navigate(typeof(LogViewerPage));
                            }
                        }
                        SetPingPageHostActiveState(false);
                        break;
                    case "UncPath":
                        if (UncPathPanel != null)
                        {
                            UncPathPanel.Visibility = Visibility.Visible;
                            if (UncPathPanel.Content == null)
                            {
                                UncPathPanel.Navigate(typeof(UncPathPage));
                            }
                        }
                        SetPingPageHostActiveState(false);
                        break;
                }
            }
        }

        private void PingPanel_Navigated(object sender, NavigationEventArgs e)
        {
            if (e.Content is PingPage pingPage)
            {
                pingPage.SetHostPingTabActive(PingPanel?.Visibility == Visibility.Visible);
            }
        }

        private void SetPingPageHostActiveState(bool isActive)
        {
            if (PingPanel?.Content is PingPage pingPage)
            {
                pingPage.SetHostPingTabActive(isActive);
            }
        }
    }
}
