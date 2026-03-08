using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace neTiPx.Views
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
        private readonly AutostartService _autostartService;
        private readonly PingLogService _pingLogService;
        private List<ColorTheme> _colorThemes = new();
        private List<string> _adapterList = new();
        private string _pingLogFolderPath = string.Empty;
        private bool _isLoading = true;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            _themeService = new ThemeSettingsService();
            _settingsService = new SettingsService();
            _colorThemeApplier = new ColorThemeApplier();
            _adapterStore = new AdapterStore();
            _autostartService = new AutostartService();
            _pingLogService = new PingLogService();
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

            // Hover Window Einstellungen laden
            if (HoverWindowStateCombo != null && HoverWindowDelayCombo != null)
            {
                bool hoverEnabled = _settingsService.GetHoverWindowEnabled();
                int hoverDelay = _settingsService.GetHoverWindowDelaySeconds();

                HoverWindowStateCombo.SelectedIndex = hoverEnabled ? 0 : 1;

                // Verzögerung basierend auf Sekunden setzen
                HoverWindowDelayCombo.SelectedIndex = hoverDelay switch
                {
                    1 => 0,
                    2 => 1,
                    3 => 2,
                    4 => 3,
                    _ => 0
                };

                // Verzögerung ComboBox aktivieren/deaktivieren basierend auf Hover-Status
                HoverWindowDelayCombo.IsEnabled = hoverEnabled;
            }

            // Verbindungsstatus-Einstellungen laden
            if (CheckGatewayCheckBox != null && CheckDns1CheckBox != null && CheckDns2CheckBox != null)
            {
                CheckGatewayCheckBox.IsChecked = _settingsService.GetCheckConnectionGateway();
                CheckDns1CheckBox.IsChecked = _settingsService.GetCheckConnectionDns1();
                CheckDns2CheckBox.IsChecked = _settingsService.GetCheckConnectionDns2();
            }

            // Ping-Schwellwert-Einstellungen laden
            if (PingThresholdFastTextBox != null)
            {
                PingThresholdFastTextBox.Text = _settingsService.GetPingThresholdFast().ToString();
            }
            if (PingThresholdNormalTextBox != null)
            {
                PingThresholdNormalTextBox.Text = _settingsService.GetPingThresholdNormal().ToString();
            }

            if (AutostartCheckBox != null)
            {
                AutostartCheckBox.IsChecked = _autostartService.IsEnabled();
            }

            if (CloseToTrayCheckBox != null)
            {
                CloseToTrayCheckBox.IsChecked = _settingsService.GetCloseToTrayOnClose();
            }

            _pingLogFolderPath = _pingLogService.GetLogFolderPath();
            UpdatePingLogFolderPathDisplay();

            _isLoading = false;
        }

        private async void SelectPingLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");

            var hwnd = WindowHelper.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var selectedFolder = await picker.PickSingleFolderAsync();
            if (selectedFolder == null)
            {
                return;
            }

            _settingsService.SetPingLogFolderPath(selectedFolder.Path);
            _pingLogFolderPath = selectedFolder.Path;
            UpdatePingLogFolderPathDisplay();
        }

        private void ResetPingLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.SetPingLogFolderPath(string.Empty);
            _pingLogFolderPath = _pingLogService.GetLogFolderPath();
            UpdatePingLogFolderPathDisplay();
        }

        private void PingLogFolderPathContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePingLogFolderPathDisplay();
        }

        private void UpdatePingLogFolderPathDisplay()
        {
            if (PingLogFolderPathTextBlock == null)
            {
                return;
            }

            var fullPath = string.IsNullOrWhiteSpace(_pingLogFolderPath)
                ? _pingLogService.GetLogFolderPath()
                : _pingLogFolderPath;

            if (string.IsNullOrWhiteSpace(fullPath))
            {
                PingLogFolderPathTextBlock.Text = string.Empty;
                ToolTipService.SetToolTip(PingLogFolderPathTextBlock, null);
                return;
            }

            const int baseLength = 32;
            var containerWidth = PingLogFolderPathContainer?.ActualWidth ?? 0;

            // Dynamischer Anteil: mit wachsender Breite werden mehr Zeichen angezeigt.
            // 254px gilt als Basisbreite (minimale Fensterbreite), danach +1 Zeichen je 8px.
            var dynamicPart = containerWidth > 254
                ? (int)Math.Floor((containerWidth - 254) / 8.0)
                : 0;

            var maxLength = Math.Max(baseLength, baseLength + dynamicPart);

            Debug.WriteLine($"[PingLogPath] containerWidth={containerWidth:F1}, baseLength={baseLength}, dynamicPart={dynamicPart}, maxLength={maxLength}, fullPathLength={fullPath.Length}");

            PingLogFolderPathTextBlock.Text = fullPath.Length <= maxLength
                ? fullPath
                : fullPath.Substring(fullPath.Length - maxLength, maxLength);
            ToolTipService.SetToolTip(PingLogFolderPathTextBlock, fullPath);
        }

        private void ColorSchemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading)
                return;

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

        private void HoverWindowStateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            if (HoverWindowDelayCombo == null)
                return;

            bool isActive = HoverWindowStateCombo.SelectedIndex == 0;

            // Hover Window aktivieren/deaktivieren
            _settingsService.SetHoverWindowEnabled(isActive);

            // Verzögerung ComboBox aktivieren/deaktivieren
            HoverWindowDelayCombo.IsEnabled = isActive;
        }

        private void HoverWindowDelayCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            int delaySeconds = HoverWindowDelayCombo.SelectedIndex switch
            {
                0 => 1,
                1 => 2,
                2 => 3,
                3 => 4,
                _ => 1
            };

            _settingsService.SetHoverWindowDelaySeconds(delaySeconds);
        }

        private void CheckGatewayCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCheckConnectionGateway(true);
        }

        private void CheckGatewayCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCheckConnectionGateway(false);
        }

        private void CheckDns1CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCheckConnectionDns1(true);
        }

        private void CheckDns1CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCheckConnectionDns1(false);
        }

        private void CheckDns2CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCheckConnectionDns2(true);
        }

        private void CheckDns2CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCheckConnectionDns2(false);
        }

        private void PingThresholdFastTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            if (PingThresholdFastTextBox != null && int.TryParse(PingThresholdFastTextBox.Text, out int value))
            {
                if (value >= 1 && value <= 200)
                {
                    _settingsService.SetPingThresholdFast(value);
                }
            }
        }

        private void PingThresholdNormalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            if (PingThresholdNormalTextBox != null && int.TryParse(PingThresholdNormalTextBox.Text, out int value))
            {
                if (value >= 1 && value <= 200)
                {
                    _settingsService.SetPingThresholdNormal(value);
                }
            }
        }

        private void AutostartCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
                return;

            _autostartService.SetEnabled(true);
        }

        private void AutostartCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading)
                return;

            _autostartService.SetEnabled(false);
        }

        private void CloseToTrayCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCloseToTrayOnClose(true);
        }

        private void CloseToTrayCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetCloseToTrayOnClose(false);
        }

    }
}
