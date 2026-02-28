using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.WinUI.Services;

namespace neTiPx.WinUI.Views
{
    public sealed class ColorSchemeItem
    {
        public ColorSchemeItem(string displayName, string value)
        {
            DisplayName = displayName;
            Value = value;
        }

        public string DisplayName { get; }
        public string Value { get; }
    }

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
                new ThemeOptionItem("Dunkel", ThemeOption.Dark),
                new ThemeOptionItem("Custom", ThemeOption.Custom)
            };

            // Color Scheme Options initialisieren
            ColorSchemeCombo.ItemsSource = new[]
            {
                new ColorSchemeItem("Rot", "Red"),
                new ColorSchemeItem("Orange", "Orange"),
                new ColorSchemeItem("Gelb", "Yellow"),
                new ColorSchemeItem("Blau", "Blue"),
                new ColorSchemeItem("Grün", "Green"),
                new ColorSchemeItem("Braun", "Brown"),
                new ColorSchemeItem("Grau", "Gray")
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
                bool isCustom = item.Value == ThemeOption.Custom;
                
                // Custom Color Panel ein-/ausblenden und Spaltenbreiten anpassen
                if (isCustom)
                {
                    CustomColorPanel.Visibility = Visibility.Visible;
                    ThemeColumn.Width = new GridLength(1, GridUnitType.Star);
                    ColorColumn.Width = new GridLength(1, GridUnitType.Star);
                }
                else
                {
                    CustomColorPanel.Visibility = Visibility.Collapsed;
                    ThemeColumn.Width = new GridLength(1, GridUnitType.Star);
                    ColorColumn.Width = new GridLength(0);
                }

                // Nur für nicht-Custom Themes das Theme setzen
                if (!isCustom)
                {
                    App.ThemeService.SetThemeOption(item.Value);
                }
            }
        }

        private void ColorSchemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorSchemeCombo.SelectedItem is ColorSchemeItem item)
            {
                // TODO: Hier später die Custom Theme Farben anwenden
                // Für jetzt nur ein Platzhalter
            }
        }
    }
}
