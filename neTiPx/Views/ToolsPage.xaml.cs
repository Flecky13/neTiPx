using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace neTiPx.Views
{
    public partial class ToolsPage : Page
    {
        public ToolsPage()
        {
            InitializeComponent();

            if (PingPanel != null)
            {
                PingPanel.Navigated += PingPanel_Navigated;
            }

            // Standardmäßig PING-Panel anzeigen
            if (ToolsNavView != null && ToolsNavView.MenuItems.Count > 0)
            {
                ToolsNavView.SelectedItem = ToolsNavView.MenuItems[0];
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
