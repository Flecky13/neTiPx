using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Text;
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
    public sealed class LanguageComboItem
    {
        public LanguageComboItem(string displayName, string code)
        {
            DisplayName = displayName;
            Code = code;
        }

        public string DisplayName { get; set; }
        public string Code { get; }
    }

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
        private readonly PagesVisibilityService _pagesVisibilityService = new PagesVisibilityService();
        private List<ColorTheme> _colorThemes = new();
        private List<string> _adapterList = new();
        private string _pingLogFolderPath = string.Empty;
        private bool _isLoading = true;
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private bool _isUpdatingLanguageCombo = false;

        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
            Unloaded += SettingsPage_Unloaded;
            _lm.LanguageChanged += OnLanguageChanged;
            _themeService = new ThemeSettingsService();
            _settingsService = new SettingsService();
            _colorThemeApplier = new ColorThemeApplier();
            _adapterStore = new AdapterStore();
            _autostartService = new AutostartService();
            _pingLogService = new PingLogService();
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoading = true;

            // Apply settings sections visibility based on page visibility
            ApplySettingsSectionsVisibility();

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
            if (HoverWindowStateCombo != null
                && HoverWindowDelayCombo != null
                && HoverWindowVerticalAnchorCombo != null
                && HoverWindowRightOffsetNumberBox != null
                && HoverWindowVerticalOffsetNumberBox != null)
            {
                bool hoverEnabled = _settingsService.GetHoverWindowEnabled();
                int hoverDelay = _settingsService.GetHoverWindowDelaySeconds();
                string hoverVerticalAnchor = _settingsService.GetHoverWindowVerticalAnchor();
                int hoverRightOffset = _settingsService.GetHoverWindowRightOffsetPixels();
                int hoverVerticalOffset = _settingsService.GetHoverWindowVerticalOffsetPixels();

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

                HoverWindowVerticalAnchorCombo.SelectedIndex = string.Equals(hoverVerticalAnchor, "Top", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                HoverWindowRightOffsetNumberBox.Value = hoverRightOffset;
                HoverWindowVerticalOffsetNumberBox.Value = hoverVerticalOffset;

                UpdateHoverWindowVerticalOffsetLabel();
                UpdateHoverWindowControlsEnabled(hoverEnabled);
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

            // Netzwerkscanner-Einstellungen laden
            if (ScanPortHttpCheckBox != null)
            {
                ScanPortHttpCheckBox.IsChecked = _settingsService.GetScanPortHttp();
            }
            if (ScanPortHttpsCheckBox != null)
            {
                ScanPortHttpsCheckBox.IsChecked = _settingsService.GetScanPortHttps();
            }
            if (ScanPortFtpCheckBox != null)
            {
                ScanPortFtpCheckBox.IsChecked = _settingsService.GetScanPortFtp();
            }
            if (ScanPortSshCheckBox != null)
            {
                ScanPortSshCheckBox.IsChecked = _settingsService.GetScanPortSsh();
            }
            if (ScanPortSmbCheckBox != null)
            {
                ScanPortSmbCheckBox.IsChecked = _settingsService.GetScanPortSmb();
            }
            if (ScanPortRdpCheckBox != null)
            {
                ScanPortRdpCheckBox.IsChecked = _settingsService.GetScanPortRdp();
            }
            if (CustomPort1NumberBox != null)
            {
                CustomPort1NumberBox.Value = _settingsService.GetCustomPort1();
            }
            if (CustomPort2NumberBox != null)
            {
                CustomPort2NumberBox.Value = _settingsService.GetCustomPort2();
            }
            if (CustomPort3NumberBox != null)
            {
                CustomPort3NumberBox.Value = _settingsService.GetCustomPort3();
            }

            // Sprache laden
            LoadLanguageCombo();

            _isLoading = false;

            UpdateLanguage();
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

        private async void OpenPagesVisibilityConfigText_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _pagesVisibilityService.EnsureConfigExists();
            var entries = _pagesVisibilityService.ReadXmlManagedEntries();

            var editorsPanel = new StackPanel { Spacing = 8 };
            var checkBoxes = new Dictionary<string, CheckBox>(StringComparer.OrdinalIgnoreCase);

            var mainPageKeys = new[] { "IpConfig", "Tools" };
            var toolPageKeys = new[] { "Ping", "Wlan", "NetworkCalculator", "NetworkScanner" };

            AddVisibilityGroup(editorsPanel, _lm.Lang("DIALOG_MAIN_PAGES"), mainPageKeys, entries, checkBoxes);
            AddVisibilityGroup(editorsPanel, _lm.Lang("DIALOG_TOOLS"), toolPageKeys, entries, checkBoxes);

            foreach (var entry in entries
                .Where(e2 => !mainPageKeys.Contains(e2.Key, StringComparer.OrdinalIgnoreCase)
                             && !toolPageKeys.Contains(e2.Key, StringComparer.OrdinalIgnoreCase))
                .OrderBy(e2 => e2.Key, StringComparer.OrdinalIgnoreCase))
            {
                var checkBox = new CheckBox
                {
                    Content = GetPageVisibilityDisplayLabel(entry.Key),
                    IsChecked = entry.Value,
                    Tag = entry.Key
                };

                checkBoxes[entry.Key] = checkBox;
                editorsPanel.Children.Add(checkBox);
            }

            ApplyToolsDependency(checkBoxes, toolPageKeys);

            var dialog = new ContentDialog
            {
                Title = _lm.Lang("DIALOG_PAGES_VISIBILITY"),
                Content = new ScrollViewer
                {
                    Content = editorsPanel,
                    MaxHeight = 420
                },
                PrimaryButtonText = _lm.Lang("DIALOG_CLOSE"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();

            var updatedEntries = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in checkBoxes)
            {
                updatedEntries[pair.Key] = pair.Value.IsChecked == true;
            }

            _pagesVisibilityService.SaveXmlManagedEntries(updatedEntries);
            ApplySettingsSectionsVisibility();
            RefreshAppVisibilityConfiguration();
        }

        private static void ApplyToolsDependency(Dictionary<string, CheckBox> checkBoxes, IEnumerable<string> toolPageKeys)
        {
            if (!checkBoxes.TryGetValue("Tools", out var toolsMainCheckBox))
            {
                return;
            }

            var toolKeys = toolPageKeys.Where(k => checkBoxes.ContainsKey(k)).ToList();
            var isUpdating = false;

            void SyncFromToolsMain(bool isChecked)
            {
                foreach (var toolKey in toolKeys)
                {
                    checkBoxes[toolKey].IsChecked = isChecked;
                }
            }

            toolsMainCheckBox.Checked += (_, _) =>
            {
                if (isUpdating)
                {
                    return;
                }

                isUpdating = true;
                SyncFromToolsMain(true);
                isUpdating = false;
            };

            toolsMainCheckBox.Unchecked += (_, _) =>
            {
                if (isUpdating)
                {
                    return;
                }

                isUpdating = true;
                SyncFromToolsMain(false);
                isUpdating = false;
            };

            foreach (var toolKey in toolKeys)
            {
                var toolCheckBox = checkBoxes[toolKey];
                toolCheckBox.Checked += (_, _) =>
                {
                    if (isUpdating)
                    {
                        return;
                    }

                    isUpdating = true;
                    toolsMainCheckBox.IsChecked = true;
                    isUpdating = false;
                };

                toolCheckBox.Unchecked += (_, _) =>
                {
                    if (isUpdating)
                    {
                        return;
                    }

                    var anyToolCheckedNow = toolKeys.Any(k => checkBoxes[k].IsChecked == true);
                    if (anyToolCheckedNow)
                    {
                        return;
                    }

                    isUpdating = true;
                    toolsMainCheckBox.IsChecked = false;
                    isUpdating = false;
                };
            }

            // Ensure initial state also respects dependency.
            var anyToolChecked = toolKeys.Any(k => checkBoxes[k].IsChecked == true);
            if (anyToolChecked)
            {
                toolsMainCheckBox.IsChecked = true;
            }
            else if (toolsMainCheckBox.IsChecked == false)
            {
                SyncFromToolsMain(false);
            }
        }

        private static void AddVisibilityGroup(
            StackPanel parent,
            string title,
            IEnumerable<string> keys,
            Dictionary<string, bool> source,
            Dictionary<string, CheckBox> target)
        {
            var availableKeys = keys
                .Where(k => source.ContainsKey(k))
                .ToList();

            if (availableKeys.Count == 0)
            {
                return;
            }

            parent.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 6, 0, 0)
            });

            foreach (var key in availableKeys)
            {
                var checkBox = new CheckBox
                {
                    Content = GetPageVisibilityDisplayLabel(key),
                    IsChecked = source[key],
                    Tag = key
                };

                target[key] = checkBox;
                parent.Children.Add(checkBox);
            }
        }

        private static string GetPageVisibilityDisplayLabel(string pageKey)
        {
            return pageKey switch
            {
                "IpConfig" => "IP-Konfiguration",
                "Tools" => "Tools (Hauptseite)",
                "Info" => "Info",
                "Settings" => "Einstellungen",
                "Ping" => "Tool: Ping",
                "Wlan" => "Tool: WLAN",
                "NetworkCalculator" => "Tool: Netzwerk-Rechner",
                "NetworkScanner" => "Tool: Netzwerkscanner",
                _ => pageKey
            };
        }

        private static void RefreshAppVisibilityConfiguration()
        {
            if (App.MainWindow?.Content is Frame rootFrame
                && rootFrame.Content is MainPage mainPage)
            {
                mainPage.RefreshVisibilityConfiguration();
            }
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

            if (HoverWindowDelayCombo == null
                || HoverWindowVerticalAnchorCombo == null
                || HoverWindowRightOffsetNumberBox == null
                || HoverWindowVerticalOffsetNumberBox == null)
                return;

            bool isActive = HoverWindowStateCombo.SelectedIndex == 0;

            // Hover Window aktivieren/deaktivieren
            _settingsService.SetHoverWindowEnabled(isActive);

            UpdateHoverWindowControlsEnabled(isActive);
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

        private void HoverWindowVerticalAnchorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || _settingsService == null || HoverWindowVerticalAnchorCombo == null)
                return;

            _settingsService.SetHoverWindowVerticalAnchor(HoverWindowVerticalAnchorCombo.SelectedIndex == 0 ? "Top" : "Bottom");
            UpdateHoverWindowVerticalOffsetLabel();
        }

        private void HoverWindowRightOffsetNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoading || _settingsService == null || double.IsNaN(sender.Value))
                return;

            _settingsService.SetHoverWindowRightOffsetPixels((int)Math.Round(sender.Value));
        }

        private void HoverWindowVerticalOffsetNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoading || _settingsService == null || double.IsNaN(sender.Value))
                return;

            _settingsService.SetHoverWindowVerticalOffsetPixels((int)Math.Round(sender.Value));
        }

        private void UpdateHoverWindowControlsEnabled(bool isEnabled)
        {
            HoverWindowDelayCombo.IsEnabled = isEnabled;
            HoverWindowVerticalAnchorCombo.IsEnabled = isEnabled;
            HoverWindowRightOffsetNumberBox.IsEnabled = isEnabled;
            HoverWindowVerticalOffsetNumberBox.IsEnabled = isEnabled;
        }

        private void UpdateHoverWindowVerticalOffsetLabel()
        {
            if (HoverWindowVerticalOffsetLabel == null || HoverWindowVerticalAnchorCombo == null)
            {
                return;
            }

            HoverWindowVerticalOffsetLabel.Text = HoverWindowVerticalAnchorCombo.SelectedIndex == 0
                ? _lm.Lang("SETTINGS_OFFSET_TOP")
                : _lm.Lang("SETTINGS_OFFSET_BOTTOM");
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

        // Netzwerkscanner Port-Scanning Event-Handler
        private void ScanPortHttpCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortHttp(true);
        }

        private void ScanPortHttpCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortHttp(false);
        }

        private void ScanPortHttpsCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortHttps(true);
        }

        private void ScanPortHttpsCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortHttps(false);
        }

        private void ScanPortFtpCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortFtp(true);
        }

        private void ScanPortFtpCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortFtp(false);
        }

        private void ScanPortSshCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortSsh(true);
        }

        private void ScanPortSshCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortSsh(false);
        }

        private void ScanPortSmbCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortSmb(true);
        }

        private void ScanPortSmbCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortSmb(false);
        }

        private void ScanPortRdpCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortRdp(true);
        }

        private void ScanPortRdpCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isLoading || _settingsService == null)
                return;

            _settingsService.SetScanPortRdp(false);
        }

        private void CustomPort1NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoading || _settingsService == null)
                return;

            if (!double.IsNaN(args.NewValue))
            {
                _settingsService.SetCustomPort1((int)args.NewValue);
            }
        }

        private void CustomPort2NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoading || _settingsService == null)
                return;

            if (!double.IsNaN(args.NewValue))
            {
                _settingsService.SetCustomPort2((int)args.NewValue);
            }
        }

        private void CustomPort3NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_isLoading || _settingsService == null)
                return;

            if (!double.IsNaN(args.NewValue))
            {
                _settingsService.SetCustomPort3((int)args.NewValue);
            }
        }

        private void LoadLanguageCombo()
        {
            if (LanguageCombo == null)
                return;

            var items = new List<LanguageComboItem>
            {
                new LanguageComboItem(_lm.Lang("SETTINGS_LANGUAGE_SYSTEM"), "System")
            };

            foreach (var code in _lm.GetAvailableLanguages())
            {
                var displayName = code.ToUpperInvariant();
                items.Add(new LanguageComboItem(displayName, code));
            }

            LanguageCombo.ItemsSource      = items;
            LanguageCombo.DisplayMemberPath = nameof(LanguageComboItem.DisplayName);

            var saved = _settingsService.GetLanguageCode();
            var selected = items.FirstOrDefault(i =>
                string.Equals(i.Code, saved, StringComparison.OrdinalIgnoreCase))
                ?? items.FirstOrDefault();

            LanguageCombo.SelectedItem = selected;
        }

        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || _isUpdatingLanguageCombo || LanguageCombo?.SelectedItem is not LanguageComboItem item)
                return;

            _settingsService.SetLanguageCode(item.Code);
            _lm.LoadLanguage(item.Code);
        }

        private void UpdateLanguage()
        {
            if (SettingsTitleText != null) SettingsTitleText.Text = _lm.Lang("SETTINGS_TITLE");
            if (SettingsSubtitlePreText != null) SettingsSubtitlePreText.Text = _lm.Lang("SETTINGS_SUBTITLE_PRE");
            if (SettingsSubtitleLinkText != null) SettingsSubtitleLinkText.Text = _lm.Lang("SETTINGS_SUBTITLE_LINK");
            if (AdapterCardTitle != null) AdapterCardTitle.Text = _lm.Lang("SETTINGS_ADAPTER_CARD");
            if (AdapterCardDesc != null) AdapterCardDesc.Text = _lm.Lang("SETTINGS_ADAPTER_DESC");
            if (PrimaryAdapterLabel != null) PrimaryAdapterLabel.Text = _lm.Lang("SETTINGS_PRIMARY_ADAPTER");
            if (SecondaryAdapterLabel != null) SecondaryAdapterLabel.Text = _lm.Lang("SETTINGS_SECONDARY_ADAPTER");
            if (AdaptersSettingsUnavailable != null) AdaptersSettingsUnavailable.Text = _lm.Lang("SETTINGS_NO_SETTINGS");
            if (PingLogCardTitle != null) PingLogCardTitle.Text = _lm.Lang("SETTINGS_PING_LOG");
            if (PingLogCardDesc != null) PingLogCardDesc.Text = _lm.Lang("SETTINGS_PING_LOG_DESC");
            if (PingLogFolderLabel != null) PingLogFolderLabel.Text = _lm.Lang("SETTINGS_LOG_FOLDER");
            if (PingSettingsUnavailable != null) PingSettingsUnavailable.Text = _lm.Lang("SETTINGS_NO_SETTINGS");
            if (HoverWindowCardTitle != null) HoverWindowCardTitle.Text = _lm.Lang("SETTINGS_HOVER_WINDOW");
            if (HoverDisplayLabel != null) HoverDisplayLabel.Text = _lm.Lang("SETTINGS_DISPLAY");
            if (HoverDelayLabel != null) HoverDelayLabel.Text = _lm.Lang("SETTINGS_DELAYED");
            if (HoverPositionLabel != null) HoverPositionLabel.Text = _lm.Lang("SETTINGS_POSITION");
            if (HoverRightOffsetLabel != null) HoverRightOffsetLabel.Text = _lm.Lang("SETTINGS_OFFSET_RIGHT");
            if (ConnectionStatusCardTitle != null) ConnectionStatusCardTitle.Text = _lm.Lang("SETTINGS_CONNECTION_STATUS");
            if (PingFastLabel != null) PingFastLabel.Text = _lm.Lang("SETTINGS_FAST_MS");
            if (PingNormalLabel != null) PingNormalLabel.Text = _lm.Lang("SETTINGS_NORMAL");
            if (PingSlowLabel != null) PingSlowLabel.Text = _lm.Lang("SETTINGS_SLOW_MS");
            if (ConnectionStatusSettingsUnavailable != null) ConnectionStatusSettingsUnavailable.Text = _lm.Lang("SETTINGS_NO_SETTINGS");
            if (NetScannerCardTitle != null) NetScannerCardTitle.Text = _lm.Lang("SETTINGS_NET_SCANNER");
            if (NetScannerCardDesc != null) NetScannerCardDesc.Text = _lm.Lang("SETTINGS_PORT_SCANNING");
            if (NetworkScannerSettingsUnavailable != null) NetworkScannerSettingsUnavailable.Text = _lm.Lang("SETTINGS_NO_SETTINGS");
            if (PlaceholderMidRightTitle != null) PlaceholderMidRightTitle.Text = _lm.Lang("SETTINGS_PLACEHOLDER");
            if (PlaceholderMidRightDesc != null) PlaceholderMidRightDesc.Text = _lm.Lang("SETTINGS_PLACEHOLDER_DESC");
            if (ColorSchemeCardTitle != null) ColorSchemeCardTitle.Text = _lm.Lang("SETTINGS_COLORSCHEME");
            if (ColorSchemeCardDesc != null) ColorSchemeCardDesc.Text = _lm.Lang("SETTINGS_COLORSCHEME_DESC");
            if (AutostartCardTitle != null) AutostartCardTitle.Text = _lm.Lang("SETTINGS_AUTOSTART");
            if (LanguageCardTitle != null) LanguageCardTitle.Text = _lm.Lang("SETTINGS_LANGUAGE");
            if (LanguageCardDesc != null) LanguageCardDesc.Text = _lm.Lang("SETTINGS_LANGUAGE_DESC");
            UpdateHoverWindowVerticalOffsetLabel();
            // Sprach-Combo-Beschriftung aktualisieren
            if (LanguageCombo?.ItemsSource is List<LanguageComboItem> items && items.Count > 0)
            {
                _isUpdatingLanguageCombo = true;
                try
                {
                    items[0].DisplayName = _lm.Lang("SETTINGS_LANGUAGE_SYSTEM");
                    // Binding neu anstoßen: Quelle neu setzen
                    LanguageCombo.ItemsSource = null;
                    LanguageCombo.ItemsSource = items;
                    var currentCode = _settingsService.GetLanguageCode();
                    LanguageCombo.SelectedItem = items.FirstOrDefault(i =>
                        string.Equals(i.Code, currentCode, StringComparison.OrdinalIgnoreCase))
                        ?? items.FirstOrDefault();
                }
                finally
                {
                    _isUpdatingLanguageCombo = false;
                }
            }
        }

        private void ApplySettingsSectionsVisibility()
        {
            _pagesVisibilityService.EnsureConfigExists();
            var visibility = _pagesVisibilityService.ReadPagesVisibility();

            // Control visibility of settings content and unavailable messages
            if (AdaptersSettingsContent != null && AdaptersSettingsUnavailable != null)
            {
                var isVisible = visibility.ContainsKey("Adapters") && visibility["Adapters"];
                AdaptersSettingsContent.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                AdaptersSettingsUnavailable.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            }

            if (ConnectionStatusSettingsContent != null && ConnectionStatusSettingsUnavailable != null)
            {
                var isVisible = visibility.ContainsKey("Adapters") && visibility["Adapters"];
                ConnectionStatusSettingsContent.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                ConnectionStatusSettingsUnavailable.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            }

            if (PingSettingsContent != null && PingSettingsUnavailable != null)
            {
                var isVisible = visibility.ContainsKey("Ping") && visibility["Ping"];
                PingSettingsContent.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                PingSettingsUnavailable.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            }

            if (NetworkScannerSettingsContent != null && NetworkScannerSettingsUnavailable != null)
            {
                var isVisible = visibility.ContainsKey("NetworkScanner") && visibility["NetworkScanner"];
                NetworkScannerSettingsContent.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                NetworkScannerSettingsUnavailable.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
            }
        }

    }
}
