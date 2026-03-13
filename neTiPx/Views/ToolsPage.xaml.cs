using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using neTiPx.Services;
using neTiPx.Views.Tools;

namespace neTiPx.Views
{
    public partial class ToolsPage : Page
    {
        private readonly PagesVisibilityService _pagesVisibilityService = new PagesVisibilityService();

        public ToolsPage()
        {
            InitializeComponent();

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
