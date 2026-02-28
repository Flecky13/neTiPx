using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.WinUI.Models;
using neTiPx.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace neTiPx.WinUI.Views
{
    public sealed class ColorSchemeItem
    {
        public ColorSchemeItem(string displayName, ColorTheme theme)
        {
            DisplayName = displayName;
            Theme = theme;
        }

        public string DisplayName { get; }
        public ColorTheme Theme { get; }
    }

    public partial class SettingsPage : Page
    {
        private readonly ThemeSettingsService _themeService;
        private readonly SettingsService _settingsService;
        private readonly ColorThemeApplier _colorThemeApplier;
        private List<ColorTheme> _colorThemes = new();

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            _themeService = new ThemeSettingsService();
            _settingsService = new SettingsService();
            _colorThemeApplier = new ColorThemeApplier();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Color Themes laden
            _colorThemes = _themeService.LoadThemes();
            var colorSchemeItems = new List<ColorSchemeItem>();
            foreach (var theme in _colorThemes)
            {
                colorSchemeItems.Add(new ColorSchemeItem(theme.Name, theme));
            }
            ColorSchemeCombo.ItemsSource = colorSchemeItems;

            var savedColorTheme = _settingsService.GetColorTheme();
            ColorSchemeItem? selectedColor = null;

            if (savedColorTheme != null)
            {
                selectedColor = colorSchemeItems.FirstOrDefault(item =>
                    string.Equals(item.DisplayName, savedColorTheme.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (selectedColor != null)
            {
                ColorSchemeCombo.SelectedItem = selectedColor;
                _colorThemeApplier.Apply(selectedColor.Theme);
            }
            else
            {
                var defaultBlue = colorSchemeItems.FirstOrDefault(item =>
                    string.Equals(item.DisplayName, "Blau", StringComparison.OrdinalIgnoreCase));

                if (defaultBlue != null)
                {
                    ColorSchemeCombo.SelectedItem = defaultBlue;
                    _colorThemeApplier.Apply(defaultBlue.Theme);
                    _settingsService.SetColorTheme(defaultBlue.Theme);
                }
            }
        }

        private void ColorSchemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorSchemeCombo.SelectedItem is ColorSchemeItem item)
            {
                _colorThemeApplier.Apply(item.Theme);
                _settingsService.SetColorTheme(item.Theme);
            }
        }
    }
}
