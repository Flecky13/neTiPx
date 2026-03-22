using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Services;
using System;

namespace neTiPx.Views
{
    public partial class MainPage : Page
    {
        private readonly PagesVisibilityService _pagesVisibilityService = new PagesVisibilityService();

        private static readonly LanguageManager _lm = LanguageManager.Instance;

        public MainPage()
        {
            InitializeComponent();
            Loaded += MainPage_Loaded;
            Unloaded += MainPage_Unloaded;
            _lm.LanguageChanged += OnLanguageChanged;
        }

        private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void UpdateLanguage()
        {
            if (NavItemAdapters != null) NavItemAdapters.Content = _lm.Lang("NAV_ADAPTERS");
            if (NavItemIpConfig != null) NavItemIpConfig.Content = _lm.Lang("NAV_IPCONFIG");
            if (NavItemTools != null) NavItemTools.Content = _lm.Lang("NAV_TOOLS");
            if (NavItemInfo != null) NavItemInfo.Content = _lm.Lang("NAV_INFO");
            if (NavItemSettings != null) NavItemSettings.Content = _lm.Lang("NAV_SETTINGS");
            if (CopyrightText != null) CopyrightText.Text = "© neTiPx by Pedro";
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            App.MainWindow.SetTitleBar(AppTitleBar);

            // Sprache anwenden
            UpdateLanguage();

            // Load and apply pages visibility
            ApplyMainPagesVisibility();

            var firstVisible = RootNavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Visibility == Visibility.Visible);

            if (firstVisible != null)
            {
                RootNavView.SelectedItem = firstVisible;
            }

            // Set initial min width based on pane state
            App.UpdateMinWidth(RootNavView.IsPaneOpen);

            // Set initial copyright text visibility
            CopyrightText.Visibility = RootNavView.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyMainPagesVisibility()
        {
            _pagesVisibilityService.EnsureConfigExists();
            var visibility = _pagesVisibilityService.ReadPagesVisibility();

            if (RootNavView?.MenuItems == null)
            {
                return;
            }

            foreach (var menuItem in RootNavView.MenuItems.OfType<NavigationViewItem>())
            {
                var tag = menuItem.Tag as string;
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                if (string.Equals(tag, "Adapters", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, "Info", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tag, "Settings", StringComparison.OrdinalIgnoreCase))
                {
                    menuItem.Visibility = Visibility.Visible;
                    continue;
                }

                var isVisible = visibility.ContainsKey(tag) && visibility[tag];
                menuItem.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
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

        public void RefreshVisibilityConfiguration()
        {
            ApplyMainPagesVisibility();

            if (ContentFrame?.Content is ToolsPage toolsPage)
            {
                toolsPage.RefreshVisibilityConfiguration();
            }

            if (RootNavView.SelectedItem is NavigationViewItem selectedItem
                && selectedItem.Visibility == Visibility.Visible)
            {
                return;
            }

            var firstVisible = RootNavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Visibility == Visibility.Visible);

            if (firstVisible != null)
            {
                RootNavView.SelectedItem = firstVisible;
            }
        }
    }
}
