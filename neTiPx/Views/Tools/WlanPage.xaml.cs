using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Helpers;
using neTiPx.Services;
using neTiPx.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace neTiPx.Views
{
    public sealed partial class WlanPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private const int WifiListBaseHeight = 260;
        private const int MainWindowMinHeight = 950;

        private AppWindow? _mainAppWindow;
        private string _wifiSortColumn = string.Empty;
        private bool _wifiSortAscending = true;

        public WlanPage()
        {
            InitializeComponent();
            Loaded += WlanPage_Loaded;
            Unloaded += WlanPage_Unloaded;
        }

        private static string T(string key)
        {
            return _lm.Lang(key);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
            UpdateWifiHeaderLabels();
            RefreshSelectedWifiDetails();
        }

        private void WlanPage_Loaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _lm.LanguageChanged += OnLanguageChanged;

            _mainAppWindow = WindowHelper.GetAppWindow(App.MainWindow);
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed += MainAppWindow_Changed;
                UpdateWifiListHeight();
            }

            UpdateLanguage();
            UpdateWifiHeaderLabels();

            if (DataContext is ToolsViewModel vm)
            {
                vm.ScanWifiCommand.Execute(null);
            }
        }

        private void WlanPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;

            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed -= MainAppWindow_Changed;
            }
        }

        private void UpdateLanguage()
        {
            if (WlanTitleText != null) WlanTitleText.Text = T("WLAN_TITLE");
            if (WlanScanTitleText != null) WlanScanTitleText.Text = T("WLAN_SCAN_TITLE");
            if (WlanScanButton != null) WlanScanButton.Content = T("WLAN_SCAN_BUTTON");

            if (WlanDetailsTitleText != null) WlanDetailsTitleText.Text = T("WLAN_DETAILS_TITLE");
            if (WifiDetailSignalSectionText != null) WifiDetailSignalSectionText.Text = T("WLAN_DETAILS_SECTION_SIGNAL");
            if (WifiDetailFrequencySectionText != null) WifiDetailFrequencySectionText.Text = T("WLAN_DETAILS_SECTION_FREQUENCY");
            if (WifiDetailSecuritySectionText != null) WifiDetailSecuritySectionText.Text = T("WLAN_DETAILS_SECTION_SECURITY");

            if (WifiNetworksListBox?.SelectedItem is not WifiNetwork)
            {
                ResetWifiDetailsTexts();
            }
        }

        private void MainAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange)
            {
                DispatcherQueue.TryEnqueue(UpdateWifiListHeight);
            }
        }

        private void UpdateWifiListHeight()
        {
            if (WifiNetworksListBox == null || _mainAppWindow == null)
            {
                return;
            }

            int deltaHeight = Math.Max(0, _mainAppWindow.Size.Height - MainWindowMinHeight);
            int networkListHeight = WifiListBaseHeight + deltaHeight;
            WifiNetworksListBox.MaxHeight = networkListHeight;
        }

        private void WifiHeader_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ToolsViewModel vm || sender is not Button button || button.Tag is not string column)
            {
                return;
            }

            if (string.Equals(_wifiSortColumn, column, StringComparison.OrdinalIgnoreCase))
            {
                _wifiSortAscending = !_wifiSortAscending;
            }
            else
            {
                _wifiSortColumn = column;
                _wifiSortAscending = true;
            }

            ApplyWifiSorting(vm);
            UpdateWifiHeaderLabels();
        }

        private void UpdateWifiHeaderLabels()
        {
            if (WifiHeaderStrength == null || WifiHeaderSsid == null || WifiHeaderSignal == null || WifiHeaderBand == null || WifiHeaderBssid == null)
            {
                return;
            }

            WifiHeaderStrength.Content = GetWifiHeaderLabel("📶", "strength");
            WifiHeaderSsid.Content = GetWifiHeaderLabel("SSID", "ssid");
            WifiHeaderSignal.Content = GetWifiHeaderLabel(T("WLAN_HEADER_SIGNAL"), "signal");
            WifiHeaderBand.Content = GetWifiHeaderLabel(T("WLAN_HEADER_BAND"), "band");
            WifiHeaderBssid.Content = GetWifiHeaderLabel(T("WLAN_HEADER_BSSID"), "bssid");
        }

        private string GetWifiHeaderLabel(string label, string column)
        {
            if (!string.Equals(_wifiSortColumn, column, StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }

            return _wifiSortAscending ? $"{label} ▲" : $"{label} ▼";
        }

        private void ApplyWifiSorting(ToolsViewModel vm)
        {
            if (vm.WifiNetworks.Count <= 1)
            {
                return;
            }

            IEnumerable<WifiNetwork> ordered = _wifiSortColumn switch
            {
                "strength" => _wifiSortAscending
                    ? vm.WifiNetworks.OrderBy(n => n.SignalStrengthPercent)
                    : vm.WifiNetworks.OrderByDescending(n => n.SignalStrengthPercent),
                "signal" => _wifiSortAscending
                    ? vm.WifiNetworks.OrderBy(n => n.SignalStrengthPercent)
                    : vm.WifiNetworks.OrderByDescending(n => n.SignalStrengthPercent),
                "band" => _wifiSortAscending
                    ? vm.WifiNetworks.OrderBy(n => n.Band)
                    : vm.WifiNetworks.OrderByDescending(n => n.Band),
                "bssid" => _wifiSortAscending
                    ? vm.WifiNetworks.OrderBy(n => n.BSSID)
                    : vm.WifiNetworks.OrderByDescending(n => n.BSSID),
                _ => _wifiSortAscending
                    ? vm.WifiNetworks.OrderBy(n => n.SSID)
                    : vm.WifiNetworks.OrderByDescending(n => n.SSID)
            };

            var selected = WifiNetworksListBox?.SelectedItem as WifiNetwork;
            var sorted = ordered.ToList();

            vm.WifiNetworks.Clear();
            foreach (var network in sorted)
            {
                vm.WifiNetworks.Add(network);
            }

            if (selected != null)
            {
                var selectedAfterSort = vm.WifiNetworks.FirstOrDefault(n =>
                    string.Equals(n.BSSID, selected.BSSID, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(n.SSID, selected.SSID, StringComparison.OrdinalIgnoreCase));

                if (selectedAfterSort != null && WifiNetworksListBox != null)
                {
                    WifiNetworksListBox.SelectedItem = selectedAfterSort;
                }
            }
        }

        private void WifiNetworksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateWifiDetailsFromCurrentSelection();
        }

        private void UpdateWifiDetailsFromCurrentSelection()
        {
            if (WifiDetailSignalStrength == null || WifiDetailQuality == null || WifiDetailRssi == null ||
                WifiDetailBand == null || WifiDetailChannel == null || WifiDetailFrequency == null ||
                WifiDetailSecurity == null || WifiDetailPhyType == null || WifiDetailNetworkType == null)
            {
                return;
            }

            if (WifiNetworksListBox?.SelectedItem is WifiNetwork selectedNetwork)
            {
                string band = string.Empty;
                if (selectedNetwork.Frequency >= 2412 && selectedNetwork.Frequency <= 2484)
                {
                    band = " (2.4 GHz)";
                }
                else if (selectedNetwork.Frequency >= 5160 && selectedNetwork.Frequency <= 5885)
                {
                    band = " (5 GHz)";
                }
                else if (selectedNetwork.Frequency >= 5955 && selectedNetwork.Frequency <= 7115)
                {
                    band = " (6 GHz - Wi-Fi 6E)";
                }

                WifiDetailSignalStrength.Text = $"{selectedNetwork.SignalSymbol} {selectedNetwork.SignalStrengthPercent}%";
                WifiDetailQuality.Text = $"{T("WLAN_DETAILS_QUALITY_VALUE")} {selectedNetwork.LinkQuality}%";
                WifiDetailRssi.Text = $"RSSI: {selectedNetwork.SignalStrengthDbm} dBm";

                WifiDetailBand.Text = $"{T("WLAN_DETAILS_BAND_VALUE")} {selectedNetwork.Band}{band}";
                WifiDetailChannel.Text = $"{T("WLAN_DETAILS_CHANNEL_VALUE")} {selectedNetwork.Channel}";
                WifiDetailFrequency.Text = $"{T("WLAN_DETAILS_FREQUENCY_VALUE")} {selectedNetwork.Frequency} MHz";

                WifiDetailSecurity.Text = $"{T("WLAN_DETAILS_SECURITY_VALUE")} {selectedNetwork.SecurityType}";
                WifiDetailPhyType.Text = $"PHY-Typ: {selectedNetwork.PhyType}";
                WifiDetailNetworkType.Text = $"{T("WLAN_DETAILS_NETWORK_VALUE")} {selectedNetwork.NetworkType}";
            }
            else
            {
                ResetWifiDetailsTexts();
            }
        }

        private void RefreshSelectedWifiDetails()
        {
            UpdateWifiDetailsFromCurrentSelection();
        }

        private void ResetWifiDetailsTexts()
        {
            if (WifiDetailSignalStrength == null || WifiDetailQuality == null || WifiDetailRssi == null ||
                WifiDetailBand == null || WifiDetailChannel == null || WifiDetailFrequency == null ||
                WifiDetailSecurity == null || WifiDetailPhyType == null || WifiDetailNetworkType == null)
            {
                return;
            }

            WifiDetailSignalStrength.Text = T("WLAN_DETAILS_SIGNAL_EMPTY");
            WifiDetailQuality.Text = T("WLAN_DETAILS_QUALITY_EMPTY");
            WifiDetailRssi.Text = "RSSI: -- dBm";
            WifiDetailBand.Text = T("WLAN_DETAILS_BAND_EMPTY");
            WifiDetailChannel.Text = T("WLAN_DETAILS_CHANNEL_EMPTY");
            WifiDetailFrequency.Text = T("WLAN_DETAILS_FREQUENCY_EMPTY");
            WifiDetailSecurity.Text = T("WLAN_DETAILS_SECURITY_EMPTY");
            WifiDetailPhyType.Text = "PHY-Typ: --";
            WifiDetailNetworkType.Text = T("WLAN_DETAILS_NETWORK_EMPTY");
        }
    }
}
