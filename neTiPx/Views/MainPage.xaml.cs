using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace neTiPx.Views
{
    public partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            App.MainWindow.SetTitleBar(AppTitleBar);

            if (RootNavView.MenuItems.Count > 0)
            {
                RootNavView.SelectedItem = RootNavView.MenuItems[0];
            }

            // Set initial min width based on pane state
            App.UpdateMinWidth(RootNavView.IsPaneOpen);
            
            // Set initial copyright text visibility
            CopyrightText.Visibility = RootNavView.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RootNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is not NavigationViewItem selectedItem)
            {
                return;
            }

            var tag = selectedItem.Tag?.ToString();
            switch (tag)
            {
                case "Adapters":
                    ContentFrame.Navigate(typeof(AdapterPage));
                    break;
                case "IpConfig":
                    ContentFrame.Navigate(typeof(IpConfigPage));
                    break;
                case "Tools":
                    ContentFrame.Navigate(typeof(ToolsPage));
                    break;
                case "Info":
                    ContentFrame.Navigate(typeof(InfoPage));
                    break;
                case "Settings":
                    ContentFrame.Navigate(typeof(SettingsPage));
                    break;
            }
        }

        private void RootNavView_PaneOpening(NavigationView sender, object args)
        {
            App.UpdateMinWidth(true);
            CopyrightText.Visibility = Visibility.Visible;
        }

        private void RootNavView_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        {
            App.UpdateMinWidth(false);
            CopyrightText.Visibility = Visibility.Collapsed;
        }
    }
}
