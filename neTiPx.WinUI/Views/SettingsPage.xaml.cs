using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.WinUI.Models;
using neTiPx.WinUI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;

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
        private readonly AdapterStore _adapterStore;
        private List<ColorTheme> _colorThemes = new();
        private List<string> _adapterList = new();
        private bool _isLoading = false;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            _themeService = new ThemeSettingsService();
            _settingsService = new SettingsService();
            _colorThemeApplier = new ColorThemeApplier();
            _adapterStore = new AdapterStore();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;

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

            // Adapter-Liste laden
            LoadAdapters();

            // Gespeicherte Adapter laden
            var adapterSettings = _adapterStore.ReadAdapters();
            if (!string.IsNullOrWhiteSpace(adapterSettings.PrimaryAdapter))
            {
                PrimaryAdapterCombo.SelectedItem = adapterSettings.PrimaryAdapter;
            }
            if (!string.IsNullOrWhiteSpace(adapterSettings.SecondaryAdapter))
            {
                SecondaryAdapterCombo.SelectedItem = adapterSettings.SecondaryAdapter;
            }

            _isLoading = false;
        }

        private void ColorSchemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorSchemeCombo.SelectedItem is ColorSchemeItem item)
            {
                _colorThemeApplier.Apply(item.Theme);
                _settingsService.SetColorTheme(item.Theme);
            }
        }

        private void LoadAdapters()
        {
            _adapterList = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => n.GetPhysicalAddress() != null && n.GetPhysicalAddress().GetAddressBytes().Length > 0)
                .Select(n => n.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            PrimaryAdapterCombo.ItemsSource = _adapterList;
            SecondaryAdapterCombo.ItemsSource = _adapterList;
        }

        private void PrimaryAdapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
                return;

            var settings = _adapterStore.ReadAdapters();
            settings.PrimaryAdapter = (string?)PrimaryAdapterCombo.SelectedItem ?? string.Empty;
            _adapterStore.WriteAdapters(settings);
        }

        private void SecondaryAdapterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
                return;

            var settings = _adapterStore.ReadAdapters();
            settings.SecondaryAdapter = (string?)SecondaryAdapterCombo.SelectedItem ?? string.Empty;
            _adapterStore.WriteAdapters(settings);
        }
    }
}
