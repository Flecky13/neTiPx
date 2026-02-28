using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.WinUI.Services;

namespace neTiPx.WinUI.Views
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Theme Options initialisieren
            ThemeCombo.ItemsSource = new[]
            {
                new ThemeOptionItem("System", ThemeOption.System),
                new ThemeOptionItem("Hell", ThemeOption.Light),
                new ThemeOptionItem("Dunkel", ThemeOption.Dark)
            };

            // Aktuelles Theme auswählen
            var currentTheme = App.ThemeService.CurrentTheme;
            for (int i = 0; i < ThemeCombo.Items.Count; i++)
            {
                if (ThemeCombo.Items[i] is ThemeOptionItem item && item.Value == currentTheme)
                {
                    ThemeCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is ThemeOptionItem item)
            {
                App.ThemeService.SetThemeOption(item.Value);
            }
        }
    }
}
