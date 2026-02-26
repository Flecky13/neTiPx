using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace neTiPx.WinUI.Views
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
            }
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is Services.ThemeOptionItem item)
            {
                App.ThemeService.SetThemeOption(item.Value);
            }
        }
    }
}
