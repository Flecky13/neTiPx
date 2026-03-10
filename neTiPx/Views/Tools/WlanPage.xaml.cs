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

        private void WlanPage_Loaded(object sender, RoutedEventArgs e)
        {
            _mainAppWindow = WindowHelper.GetAppWindow(App.MainWindow);
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed += MainAppWindow_Changed;
                UpdateWifiListHeight();
            }

            UpdateWifiHeaderLabels();

            if (DataContext is ToolsViewModel vm)
            {
                vm.ScanWifiCommand.Execute(null);
            }
        }

        private void WlanPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed -= MainAppWindow_Changed;
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
            WifiHeaderSignal.Content = GetWifiHeaderLabel("Signal", "signal");
            WifiHeaderBand.Content = GetWifiHeaderLabel("Band", "band");
            WifiHeaderBssid.Content = GetWifiHeaderLabel("BSSID", "bssid");
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
                WifiDetailQuality.Text = $"Qualität: {selectedNetwork.LinkQuality}%";
                WifiDetailRssi.Text = $"RSSI: {selectedNetwork.SignalStrengthDbm} dBm";

                WifiDetailBand.Text = $"Band: {selectedNetwork.Band}{band}";
                WifiDetailChannel.Text = $"Kanal: {selectedNetwork.Channel}";
                WifiDetailFrequency.Text = $"{selectedNetwork.Frequency} MHz";

                WifiDetailSecurity.Text = $"Verschlüsselung: {selectedNetwork.SecurityType}";
                WifiDetailPhyType.Text = $"PHY: {selectedNetwork.PhyType}";
                WifiDetailNetworkType.Text = $"{selectedNetwork.NetworkType}";
            }
            else
            {
                WifiDetailSignalStrength.Text = "Stärke: --";
                WifiDetailQuality.Text = "Qualität: --";
                WifiDetailRssi.Text = "RSSI: -- dBm";
                WifiDetailBand.Text = "Band: --";
                WifiDetailChannel.Text = "Kanal: --";
                WifiDetailFrequency.Text = "Frequenz: --";
                WifiDetailSecurity.Text = "Verschlüsselung: --";
                WifiDetailPhyType.Text = "PHY-Typ: --";
                WifiDetailNetworkType.Text = "Netzwerk: --";
            }
        }
    }
}
