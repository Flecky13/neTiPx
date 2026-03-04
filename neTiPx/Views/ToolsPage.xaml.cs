using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace neTiPx.Views
{
    public partial class ToolsPage : Page
    {
        public ToolsPage()
        {
            InitializeComponent();
            // Standardmäßig PING-Panel anzeigen
            if (ToolsNavView.MenuItems.Count > 0)
            {
                ToolsNavView.SelectedItem = ToolsNavView.MenuItems[0];
            }
        }

        private void ToolsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                // Alle Panels ausblenden
                PingPanel.Visibility = Visibility.Collapsed;
                WlanPanel.Visibility = Visibility.Collapsed;
                PlaceholderPanel.Visibility = Visibility.Collapsed;

                // Ausgewähltes Panel anzeigen
                switch (tag)
                {
                    case "Ping":
                        PingPanel.Visibility = Visibility.Visible;
                        break;
                    case "Wlan":
                        WlanPanel.Visibility = Visibility.Visible;
                        break;
                    case "Placeholder":
                        PlaceholderPanel.Visibility = Visibility.Visible;
                        break;
                }
            }
        }
    }
}
