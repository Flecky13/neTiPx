using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using neTiPx.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace neTiPx.Views
{
    public partial class ToolsPage : Page
    {
        private readonly PingTargetsStore _pingTargetsStore = new PingTargetsStore();
        private readonly PingLogService _pingLogService = new PingLogService();
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly AdapterStore _adapterStore = new AdapterStore();
        private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();
        private const int WifiListBaseHeight = 260;
        private const int MainWindowMinHeight = 950;
        private AppWindow? _mainAppWindow;
        private string _wifiSortColumn = string.Empty;
        private bool _wifiSortAscending = true;
        public ObservableCollection<PingTarget> PingTargets { get; } = new ObservableCollection<PingTarget>();
        private readonly Dictionary<PingTarget, CancellationTokenSource> _pingTimers = new Dictionary<PingTarget, CancellationTokenSource>();
        private readonly Dictionary<PingTarget, string> _lastValidTargets = new Dictionary<PingTarget, string>();
        private bool _isPingPageVisible = true;
        private bool _isSyncingNetworkCalcInputs;
        private bool _isIpv6Mode = false;
        public ObservableCollection<NetworkDevice> NetworkDevices { get; } = new ObservableCollection<NetworkDevice>();
        private CancellationTokenSource? _networkScanCts;

        public ToolsPage()
        {
            InitializeComponent();

            Loaded += ToolsPage_Loaded;
            Unloaded += ToolsPage_Unloaded;

            // Ping-Ziele aus Config laden
            LoadPingTargets();

            // Standardmäßig PING-Panel anzeigen
            if (ToolsNavView != null && ToolsNavView.MenuItems.Count > 0)
            {
                ToolsNavView.SelectedItem = ToolsNavView.MenuItems[0];
            }
        }

        private void ToolsPage_Loaded(object sender, RoutedEventArgs e)
        {
            // Checkbox mit gespeichertem Wert initialisieren
            if (BackgroundActiveCheckBox != null)
            {
                BackgroundActiveCheckBox.IsChecked = _settingsService.GetPingBackgroundActive();
            }
            UpdatePingingState();

            _mainAppWindow = WindowHelper.GetAppWindow(App.MainWindow);
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed += MainAppWindow_Changed;
                UpdateWifiListHeight();
            }

            UpdateWifiHeaderLabels();

            PrefillNetworkScanRangesFromNic1();

            // Wenn WLAN-Panel sichtbar ist, initialen Scan durchführen
            if (WlanPanel != null && WlanPanel.Visibility == Visibility.Visible && DataContext is ToolsViewModel vm)
            {
                vm.ScanWifiCommand.Execute(null);
            }
        }

        private void ToolsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed -= MainAppWindow_Changed;
            }

            _isPingPageVisible = false;
            UpdatePingingState();
        }

        private void ToolsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            {
                // Alle Panels ausblenden
                if (PingPanel != null) PingPanel.Visibility = Visibility.Collapsed;
                if (WlanPanel != null) WlanPanel.Visibility = Visibility.Collapsed;
                if (NetworkCalculatorPanel != null) NetworkCalculatorPanel.Visibility = Visibility.Collapsed;
                if (NetworkScannerPanel != null) NetworkScannerPanel.Visibility = Visibility.Collapsed;

                // Ausgewähltes Panel anzeigen
                switch (tag)
                {
                    case "Ping":
                        if (PingPanel != null) PingPanel.Visibility = Visibility.Visible;
                        _isPingPageVisible = true;
                        break;
                    case "Wlan":
                        if (WlanPanel != null) WlanPanel.Visibility = Visibility.Visible;
                        _isPingPageVisible = false;
                        // Automatischen WLAN-Scan triggern
                        if (DataContext is ToolsViewModel vm)
                        {
                            vm.ScanWifiCommand.Execute(null);
                        }
                        break;
                    case "NetworkCalculator":
                        if (NetworkCalculatorPanel != null) NetworkCalculatorPanel.Visibility = Visibility.Visible;
                        _isPingPageVisible = false;
                        break;
                    case "NetworkScanner":
                        if (NetworkScannerPanel != null) NetworkScannerPanel.Visibility = Visibility.Visible;
                        _isPingPageVisible = false;
                        break;
                }

                UpdatePingingState();
            }
        }

        private void LoadPingTargets()
        {
            var savedTargets = _pingTargetsStore.ReadAll();

            foreach (var savedTarget in savedTargets)
            {
                if (!string.IsNullOrWhiteSpace(savedTarget.Target))
                {
                    var pingTarget = new PingTarget
                    {
                        Target = savedTarget.Target,
                        IntervalSeconds = Math.Clamp(savedTarget.IntervalSeconds, 1, 3600),
                        IsPingEnabled = savedTarget.IsEnabled,
                        Source = savedTarget.Source,
                        ResponseTimeIpv4 = string.Empty,
                        ResponseTimeIpv6 = string.Empty,
                        StatusColorIpv4 = new SolidColorBrush(Colors.Gray),
                        StatusColorIpv6 = new SolidColorBrush(Colors.Gray)
                    };

                    _lastValidTargets[pingTarget] = pingTarget.Target;

                    // Bestimme Adresstyp
                    DetermineAddressType(pingTarget);

                    PingTargets.Add(pingTarget);
                    // Pinging wird durch UpdatePingingState() im Loaded-Event gestartet
                }
            }
        }

        private void AddPingTarget_Click(object sender, RoutedEventArgs e)
        {
            var target = NewPingTargetTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            // Prüfen ob bereits vorhanden
            if (PingTargets.Any(p => p.Target.Equals(target, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var intervalSeconds = (int)PingIntervalNumberBox.Value;
            var pingTarget = new PingTarget
            {
                Target = target,
                IntervalSeconds = intervalSeconds,
                IsPingEnabled = true,
                Source = string.Empty,
                ResponseTimeIpv4 = string.Empty,
                ResponseTimeIpv6 = string.Empty,
                StatusColorIpv4 = new SolidColorBrush(Colors.Gray),
                StatusColorIpv6 = new SolidColorBrush(Colors.Gray)
            };

            _lastValidTargets[pingTarget] = pingTarget.Target;

            // Bestimme Adresstyp
            DetermineAddressType(pingTarget);

            PingTargets.Add(pingTarget);
            SavePingTargets();
            UpdatePingingState();

            NewPingTargetTextBox.Text = string.Empty;
        }

        private async void DeletePingTarget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PingTarget target)
            {
                var deleteConfirmed = await ConfirmLogDeleteActionAsync(target);
                if (!deleteConfirmed)
                {
                    return;
                }

                // Timer stoppen
                if (_pingTimers.TryGetValue(target, out var cts))
                {
                    cts.Cancel();
                    _pingTimers.Remove(target);
                }

                _lastValidTargets.Remove(target);

                PingTargets.Remove(target);
                SavePingTargets();
            }
        }

        private async Task<bool> ConfirmLogDeleteActionAsync(PingTarget target)
        {
            if (!_pingLogService.LogFileExists(target.Target))
            {
                return true;
            }

            var dialog = new ContentDialog
            {
                Title = "Log-Datei beim Löschen",
                Content = "Soll die zugehörige Log-Datei ebenfalls gelöscht werden?",
                PrimaryButtonText = "Ja",
                SecondaryButtonText = "Nein",
                CloseButtonText = "Abbrechen",
                DefaultButton = ContentDialogButton.Secondary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _pingLogService.TryDeleteLogFile(target.Target);
                return true;
            }

            if (result == ContentDialogResult.Secondary)
            {
                await SaveLogFileAsAndDeleteSourceAsync(target.Target);
                return true;
            }

            return false;
        }

        private async Task SaveLogFileAsAndDeleteSourceAsync(string target)
        {
            if (!_pingLogService.LogFileExists(target))
            {
                return;
            }

            var sourceLogPath = _pingLogService.GetLogFilePath(target);
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileName(sourceLogPath)
            };
            picker.FileTypeChoices.Add("Log-Datei", new List<string> { ".log" });

            var hwnd = Helpers.WindowHelper.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);

            var saveFile = await picker.PickSaveFileAsync();
            if (saveFile == null)
            {
                return;
            }

            _pingLogService.TryExportAndDeleteLogFile(target, saveFile.Path);
        }

        private void PingTargetTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.Tag is not PingTarget target)
            {
                return;
            }

            var newValue = textBox.Text.Trim();
            _lastValidTargets.TryGetValue(target, out var lastValidValue);

            if (string.IsNullOrWhiteSpace(newValue))
            {
                target.Target = lastValidValue ?? string.Empty;
                return;
            }

            var duplicateExists = PingTargets.Any(p => !ReferenceEquals(p, target) && p.Target.Equals(newValue, StringComparison.OrdinalIgnoreCase));
            if (duplicateExists)
            {
                target.Target = lastValidValue ?? string.Empty;
                return;
            }

            if (!string.Equals(target.Target, newValue, StringComparison.Ordinal))
            {
                target.Target = newValue;
            }

            _lastValidTargets[target] = target.Target;
            DetermineAddressType(target);
            SavePingTargets();
        }

        private void PingIntervalNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (sender.Tag is not PingTarget target)
            {
                return;
            }

            if (double.IsNaN(sender.Value) || double.IsInfinity(sender.Value))
            {
                sender.Value = target.IntervalSeconds;
                return;
            }

            var clamped = Math.Clamp((int)Math.Round(sender.Value), 1, 3600);
            if (target.IntervalSeconds != clamped)
            {
                target.IntervalSeconds = clamped;
                SavePingTargets();
            }

            if (Math.Abs(sender.Value - clamped) > double.Epsilon)
            {
                sender.Value = clamped;
            }
        }

        private async void StartPingingAsync(PingTarget target)
        {
            if (_pingTimers.ContainsKey(target))
            {
                return;
            }

            var cts = new CancellationTokenSource();
            _pingTimers[target] = cts;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await ExecutePingAsync(target);
                    await Task.Delay(TimeSpan.FromSeconds(target.IntervalSeconds), cts.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Timer wurde gestoppt
            }
        }

        private void StopPinging(PingTarget target)
        {
            if (_pingTimers.TryGetValue(target, out var cts))
            {
                cts.Cancel();
                _pingTimers.Remove(target);
            }

            target.ResponseTimeIpv4 = "Deaktiviert";
            target.StatusColorIpv4 = new SolidColorBrush(Colors.Gray);
            target.ResponseTimeIpv6 = "Deaktiviert";
            target.StatusColorIpv6 = new SolidColorBrush(Colors.Gray);
        }

        private void PingEnabledCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox || checkBox.Tag is not PingTarget target)
            {
                return;
            }

            var isEnabled = checkBox.IsChecked == true;
            target.IsPingEnabled = isEnabled;

            if (isEnabled)
            {
                DetermineAddressType(target);
                StartPingingAsync(target);
            }
            else
            {
                StopPinging(target);
            }

            SavePingTargets();
        }

        private void BackgroundActiveCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
            {
                return;
            }

            var isActive = checkBox.IsChecked == true;
            _settingsService.SetPingBackgroundActive(isActive);
            UpdatePingingState();
        }

        private void UpdatePingingState()
        {
            // Pingen soll aktiv sein wenn: Ping-Seite sichtbar ODER Checkbox aktiv
            var shouldPingBeActive = _isPingPageVisible || (BackgroundActiveCheckBox?.IsChecked == true);

            foreach (var target in PingTargets)
            {
                if (!target.IsPingEnabled)
                {
                    // Target ist manuell deaktiviert - respektiere das
                    continue;
                }

                var isCurrentlyPinging = _pingTimers.ContainsKey(target);

                if (shouldPingBeActive && !isCurrentlyPinging)
                {
                    // Starte Pingen
                    StartPingingAsync(target);
                }
                else if (!shouldPingBeActive && isCurrentlyPinging)
                {
                    // Stoppe Pingen
                    StopPinging(target);
                }
            }
        }

        private void OpenPingLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not PingTarget target)
            {
                return;
            }

            try
            {
                _pingLogService.OpenLogFile(target.Target);
            }
            catch
            {
                // Datei konnte nicht geöffnet werden.
            }
        }

        private async Task ExecutePingAsync(PingTarget target)
        {
            try
            {
                // IPv4 Ping
                var ipv4Task = PingAsync(target.Target, AddressFamily.InterNetwork);
                // IPv6 Ping
                var ipv6Task = PingAsync(target.Target, AddressFamily.InterNetworkV6);

                await Task.WhenAll(ipv4Task, ipv6Task);

                var ipv4Result = await ipv4Task;
                var ipv6Result = await ipv6Task;

                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdatePingResult(target, ipv4Result, ResponseType.IPv4);
                    UpdatePingResult(target, ipv6Result, ResponseType.IPv6);
                });
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (target.ShowIPv4 == Visibility.Visible)
                    {
                        target.ResponseTimeIpv4 = "Fehler";
                        target.StatusColorIpv4 = new SolidColorBrush(Colors.Red);
                        _pingLogService.AppendPingResult(target.Target, "IPv4", "Fehler", target.ResolvedAddressIpv4);
                    }

                    if (target.ShowIPv6 == Visibility.Visible)
                    {
                        target.ResponseTimeIpv6 = "Fehler";
                        target.StatusColorIpv6 = new SolidColorBrush(Colors.Red);
                        _pingLogService.AppendPingResult(target.Target, "IPv6", "Fehler", target.ResolvedAddressIpv6);
                    }
                });
            }
        }

        private void DetermineAddressType(PingTarget target)
        {
            if (IPAddress.TryParse(target.Target, out var ipAddress))
            {
                // Eindeutige IP-Adresse erkannt
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    // IPv4-Adresse
                    target.ShowIPv4 = Visibility.Visible;
                    target.ShowIPv6 = Visibility.Collapsed;
                    target.ResponseTimeIpv6 = "ungültig";
                    target.StatusColorIpv6 = new SolidColorBrush(Colors.Gray);
                }
                else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    // IPv6-Adresse
                    target.ShowIPv4 = Visibility.Collapsed;
                    target.ShowIPv6 = Visibility.Visible;
                    target.ResponseTimeIpv4 = "ungültig";
                    target.StatusColorIpv4 = new SolidColorBrush(Colors.Gray);
                }
            }
            else
            {
                // Hostname - beide anzeigen
                target.ShowIPv4 = Visibility.Visible;
                target.ShowIPv6 = Visibility.Visible;
            }
        }

        private async Task<PingResult> PingAsync(string target, AddressFamily addressFamily)
        {
            try
            {
                using var ping = new Ping();

                // Versuche direkt zu parsen, ob es eine IP-Adresse ist
                if (IPAddress.TryParse(target, out var ipAddress))
                {
                    // Direkte IP-Adresse - prüfe ob sie dem gewünschten AddressFamily entspricht
                    if (ipAddress.AddressFamily == addressFamily)
                    {
                        return new PingResult(await ping.SendPingAsync(ipAddress, 3000), ipAddress.ToString());
                    }
                    return new PingResult(null, string.Empty); // Adresse entspricht nicht dem gewünschten AddressFamily
                }

                // Versuche Hostname aufzulösen
                var hostEntry = await Dns.GetHostEntryAsync(target);
                if (hostEntry?.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    return new PingResult(null, string.Empty);
                }

                // Finde eine Adresse mit dem gewünschten AddressFamily
                var address = hostEntry.AddressList.FirstOrDefault(a => a.AddressFamily == addressFamily);
                if (address == null)
                {
                    return new PingResult(null, string.Empty);
                }

                return new PingResult(await ping.SendPingAsync(address, 3000), address.ToString());
            }
            catch
            {
                return new PingResult(null, string.Empty);
            }
        }

        private sealed class PingResult
        {
            public PingResult(PingReply? reply, string resolvedAddress)
            {
                Reply = reply;
                ResolvedAddress = resolvedAddress;
            }

            public PingReply? Reply { get; }

            public string ResolvedAddress { get; }
        }

        private enum ResponseType
        {
            IPv4,
            IPv6
        }

        private void UpdatePingResult(PingTarget target, PingResult result, ResponseType type)
        {
            if (!ShouldHandleResponseType(target, type))
            {
                return;
            }

            if (type == ResponseType.IPv4)
            {
                target.ResolvedAddressIpv4 = result.ResolvedAddress;
            }
            else
            {
                target.ResolvedAddressIpv6 = result.ResolvedAddress;
            }

            if (result.Reply != null && result.Reply.Status == IPStatus.Success)
            {
                var responseTimeStr = $"{result.Reply.RoundtripTime} ms";

                // Ampel-Farbe basierend auf Antwortzeit
                var statusColor = result.Reply.RoundtripTime switch
                {
                    < 50 => new SolidColorBrush(Colors.Green),    // Grün
                    < 150 => new SolidColorBrush(Colors.Yellow),  // Gelb
                    _ => new SolidColorBrush(Colors.Orange)       // Orange
                };

                if (type == ResponseType.IPv4)
                {
                    target.ResponseTimeIpv4 = responseTimeStr;
                    target.StatusColorIpv4 = statusColor;
                    target.PingCountIpv4++;
                    target.AddResponseTimeIpv4(result.Reply.RoundtripTime);
                    _pingLogService.AppendPingResult(target.Target, "IPv4", responseTimeStr, result.ResolvedAddress);
                }
                else
                {
                    target.ResponseTimeIpv6 = responseTimeStr;
                    target.StatusColorIpv6 = statusColor;
                    target.PingCountIpv6++;
                    target.AddResponseTimeIpv6(result.Reply.RoundtripTime);
                    _pingLogService.AppendPingResult(target.Target, "IPv6", responseTimeStr, result.ResolvedAddress);
                }
            }
            else
            {
                var statusColor = new SolidColorBrush(Colors.Red); // Rot für Fehler/Timeout

                if (type == ResponseType.IPv4)
                {
                    target.ResponseTimeIpv4 = "Timeout";
                    target.StatusColorIpv4 = statusColor;
                    target.PingCountIpv4++;
                    target.TimeoutCountIpv4++;
                    _pingLogService.AppendPingResult(target.Target, "IPv4", "Timeout", result.ResolvedAddress);
                }
                else
                {
                    target.ResponseTimeIpv6 = "Timeout";
                    target.StatusColorIpv6 = statusColor;
                    target.PingCountIpv6++;
                    target.TimeoutCountIpv6++;
                    _pingLogService.AppendPingResult(target.Target, "IPv6", "Timeout", result.ResolvedAddress);
                }
            }
        }

        private static bool ShouldHandleResponseType(PingTarget target, ResponseType type)
        {
            return type switch
            {
                ResponseType.IPv4 => target.ShowIPv4 == Visibility.Visible,
                ResponseType.IPv6 => target.ShowIPv6 == Visibility.Visible,
                _ => true
            };
        }

        private void SavePingTargets()
        {
            _pingTargetsStore.WriteAll(PingTargets.Select(target => new PingTargetsStore.PingTargetSettings
            {
                Target = target.Target,
                IntervalSeconds = target.IntervalSeconds,
                IsEnabled = target.IsPingEnabled,
                Source = target.Source
            }));
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
                // Formatiere die Netzwerk-Details
                string band = "";
                if (selectedNetwork.Frequency >= 2412 && selectedNetwork.Frequency <= 2484)
                    band = " (2.4 GHz)";
                else if (selectedNetwork.Frequency >= 5160 && selectedNetwork.Frequency <= 5885)
                    band = " (5 GHz)";
                else if (selectedNetwork.Frequency >= 5955 && selectedNetwork.Frequency <= 7115)
                    band = " (6 GHz - Wi-Fi 6E)";

                string securityIcon = selectedNetwork.IsSecured ? "�" : "🔒";

                // Spalte 1: Signal
                WifiDetailSignalStrength.Text = $"{selectedNetwork.SignalSymbol} {selectedNetwork.SignalStrengthPercent}%";
                WifiDetailQuality.Text = $"Qualität: {selectedNetwork.LinkQuality}%";
                WifiDetailRssi.Text = $"RSSI: {selectedNetwork.SignalStrengthDbm} dBm";

                // Spalte 2: Frequenz
                WifiDetailBand.Text = $"Band: {selectedNetwork.Band}{band}";
                WifiDetailChannel.Text = $"Kanal: {selectedNetwork.Channel}";
                WifiDetailFrequency.Text = $"{selectedNetwork.Frequency} MHz";

                // Spalte 3: Sicherheit & Standard
                WifiDetailSecurity.Text = $"{securityIcon} {selectedNetwork.SecurityType}";
                WifiDetailPhyType.Text = $"PHY: {selectedNetwork.PhyType}";
                WifiDetailNetworkType.Text = $"{selectedNetwork.NetworkType}";
            }
            else
            {
                // Felder zurücksetzen
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

        // Network Calculator Methoden
        private void NetworkCalcIpVersionRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (NetworkCalcIpv4RadioButton == null || NetworkCalcIpv6RadioButton == null)
            {
                return;
            }

            _isIpv6Mode = NetworkCalcIpv6RadioButton.IsChecked == true;

            // Toggle visibility
            NetworkCalcIpv4InputBorder.Visibility = _isIpv6Mode ? Visibility.Collapsed : Visibility.Visible;
            NetworkCalcIpv6InputBorder.Visibility = _isIpv6Mode ? Visibility.Visible : Visibility.Collapsed;
            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
            NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;

            // Clear error
            NetworkCalcErrorBar.IsOpen = false;

            // Trigger calculation for the active mode
            if (_isIpv6Mode)
            {
                TryCalculateIpv6NetworkAuto();
            }
            else
            {
                TryCalculateNetworkAuto();
            }
        }

        private void NetworkCalcIpAddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateIpScopeIndicator();
            TryCalculateNetworkAuto();
        }

        private void NetworkCalcIpv6AddressTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateIpv6ScopeIndicator();
            TryCalculateIpv6NetworkAuto();
        }

        private void NetworkCalcIpv6PrefixTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TryCalculateIpv6NetworkAuto();
        }

        private void NetworkCalcSubnetTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingNetworkCalcInputs)
            {
                return;
            }

            var subnetInput = NetworkCalcSubnetTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(subnetInput))
            {
                SetNetworkCalcInputText(() =>
                {
                    NetworkCalcCidrTextBox.Text = string.Empty;
                    NetworkCalcMaxHostsTextBox.Text = string.Empty;
                });
                TryCalculateNetworkAuto();
                return;
            }

            if (!TryParseSubnetMask(subnetInput, out var subnetMask))
            {
                return;
            }

            var prefixLength = CountBits(subnetMask);
            var maxHosts = CalculateUsableHosts(prefixLength);
            SetNetworkCalcInputText(() =>
            {
                NetworkCalcCidrTextBox.Text = prefixLength.ToString();
                NetworkCalcMaxHostsTextBox.Text = maxHosts.ToString();
            });

            TryCalculateNetworkAuto();
        }

        private void NetworkCalcCidrTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingNetworkCalcInputs)
            {
                return;
            }

            var cidrSuffixInput = NetworkCalcCidrTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(cidrSuffixInput))
            {
                SetNetworkCalcInputText(() =>
                {
                    NetworkCalcSubnetTextBox.Text = string.Empty;
                    NetworkCalcMaxHostsTextBox.Text = string.Empty;
                });
                TryCalculateNetworkAuto();
                return;
            }

            if (!TryParseCidrSuffix(cidrSuffixInput, out var prefixLength))
            {
                return;
            }

            var mask = PrefixToMask(prefixLength);
            var maxHosts = CalculateUsableHosts(prefixLength);
            SetNetworkCalcInputText(() =>
            {
                NetworkCalcSubnetTextBox.Text = UintToIp(mask).ToString();
                NetworkCalcMaxHostsTextBox.Text = maxHosts.ToString();
            });

            TryCalculateNetworkAuto();
        }

        private void NetworkCalcMaxHostsTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncingNetworkCalcInputs)
            {
                return;
            }

            var maxHostsInput = NetworkCalcMaxHostsTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(maxHostsInput))
            {
                SetNetworkCalcInputText(() =>
                {
                    NetworkCalcSubnetTextBox.Text = string.Empty;
                    NetworkCalcCidrTextBox.Text = string.Empty;
                });
                TryCalculateNetworkAuto();
                return;
            }

            if (!long.TryParse(maxHostsInput, out var maxHosts) || maxHosts < 0)
            {
                return;
            }

            if (!TryGetPrefixFromMaxHosts(maxHosts, out var prefixLength))
            {
                return;
            }

            var mask = PrefixToMask(prefixLength);
            SetNetworkCalcInputText(() =>
            {
                NetworkCalcCidrTextBox.Text = prefixLength.ToString();
                NetworkCalcSubnetTextBox.Text = UintToIp(mask).ToString();
                NetworkCalcMaxHostsTextBox.Text = CalculateUsableHosts(prefixLength).ToString();
            });

            TryCalculateNetworkAuto();
        }

        private void NetworkCalcHostsMinusButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetCurrentPrefix(out var currentPrefix))
            {
                currentPrefix = 24;
            }

            var nextPrefix = Math.Min(32, currentPrefix + 1);
            ApplyPrefixToNetworkCalcInputs(nextPrefix);
            TryCalculateNetworkAuto();
        }

        private void NetworkCalcHostsPlusButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetCurrentPrefix(out var currentPrefix))
            {
                currentPrefix = 24;
            }

            var nextPrefix = Math.Max(0, currentPrefix - 1);
            ApplyPrefixToNetworkCalcInputs(nextPrefix);
            TryCalculateNetworkAuto();
        }

        private void SetNetworkCalcInputText(Action updateAction)
        {
            _isSyncingNetworkCalcInputs = true;
            try
            {
                updateAction();
            }
            finally
            {
                _isSyncingNetworkCalcInputs = false;
            }
        }

        private bool TryGetCurrentPrefix(out int prefixLength)
        {
            if (TryParseCidrSuffix(NetworkCalcCidrTextBox.Text.Trim(), out prefixLength))
            {
                return true;
            }

            if (TryParseSubnetMask(NetworkCalcSubnetTextBox.Text.Trim(), out var subnetMask))
            {
                prefixLength = CountBits(subnetMask);
                return true;
            }

            var maxHostsInput = NetworkCalcMaxHostsTextBox.Text.Trim();
            if (long.TryParse(maxHostsInput, out var maxHosts) && maxHosts >= 0)
            {
                return TryGetPrefixFromMaxHosts(maxHosts, out prefixLength);
            }

            prefixLength = 0;
            return false;
        }

        private void ApplyPrefixToNetworkCalcInputs(int prefixLength)
        {
            var mask = PrefixToMask(prefixLength);
            var maxHosts = CalculateUsableHosts(prefixLength);

            SetNetworkCalcInputText(() =>
            {
                NetworkCalcCidrTextBox.Text = prefixLength.ToString();
                NetworkCalcSubnetTextBox.Text = UintToIp(mask).ToString();
                NetworkCalcMaxHostsTextBox.Text = maxHosts.ToString();
            });
        }

        private void TryCalculateNetworkAuto()
        {
            NetworkCalcErrorBar.IsOpen = false;

            if (!IPAddress.TryParse(NetworkCalcIpAddressTextBox.Text.Trim(), out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            {
                NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            var cidrInput = NetworkCalcCidrTextBox.Text.Trim();
            if (TryParseCidrSuffix(cidrInput, out var prefixFromCidr))
            {
                CalculateFromSuffix(ip.ToString(), prefixFromCidr.ToString());
                return;
            }

            var subnetInput = NetworkCalcSubnetTextBox.Text.Trim();
            if (TryParseSubnetMask(subnetInput, out var subnetMask))
            {
                var prefixFromSubnet = CountBits(subnetMask);
                CalculateFromSuffix(ip.ToString(), prefixFromSubnet.ToString());
                return;
            }

            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
        }

        private void TryCalculateIpv6NetworkAuto()
        {
            NetworkCalcErrorBar.IsOpen = false;

            var addressInput = NetworkCalcIpv6AddressTextBox.Text.Trim();
            var prefixInput = NetworkCalcIpv6PrefixTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(addressInput) || string.IsNullOrWhiteSpace(prefixInput))
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            if (!IPAddress.TryParse(addressInput, out var ipv6Address) || ipv6Address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            if (!int.TryParse(prefixInput, out var prefixLength) || prefixLength < 0 || prefixLength > 128)
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                CalculateIpv6Network(ipv6Address, prefixLength);
            }
            catch
            {
                NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateIpv6ScopeIndicator()
        {
            var input = NetworkCalcIpv6AddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                NetworkCalcIpv6ScopeTextBlock.Text = "IP-Bereich: -";
                return;
            }

            if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != AddressFamily.InterNetworkV6)
            {
                NetworkCalcIpv6ScopeTextBlock.Text = "IP-Bereich: ungültige IPv6-Adresse";
                return;
            }

            NetworkCalcIpv6ScopeTextBlock.Text = $"IP-Bereich: {GetIpv6ScopeLabel(ip)}";
        }

        private void CalculateIpv6Network(IPAddress ipv6Address, int prefixLength)
        {
            var addressBytes = ipv6Address.GetAddressBytes();
            var networkBytes = new byte[16];
            var lastBytes = new byte[16];

            // Calculate network address
            int fullBytes = prefixLength / 8;
            int remainingBits = prefixLength % 8;

            for (int i = 0; i < 16; i++)
            {
                if (i < fullBytes)
                {
                    networkBytes[i] = addressBytes[i];
                    lastBytes[i] = addressBytes[i];
                }
                else if (i == fullBytes && remainingBits > 0)
                {
                    byte mask = (byte)(0xFF << (8 - remainingBits));
                    networkBytes[i] = (byte)(addressBytes[i] & mask);
                    lastBytes[i] = (byte)(addressBytes[i] | ~mask);
                }
                else
                {
                    networkBytes[i] = 0;
                    lastBytes[i] = 0xFF;
                }
            }

            var networkAddress = new IPAddress(networkBytes);
            var firstAddress = new IPAddress(networkBytes);
            var lastAddress = new IPAddress(lastBytes);

            // Calculate address count
            var hostBits = 128 - prefixLength;
            string addressCount;
            if (hostBits > 63)
            {
                addressCount = $"2^{hostBits} (sehr groß)";
            }
            else
            {
                var count = System.Numerics.BigInteger.Pow(2, hostBits);
                addressCount = count.ToString("N0");
            }

            // Display results
            NetworkAddressIpv6.Text = networkAddress.ToString();
            PrefixLengthIpv6.Text = $"/{prefixLength}";
            FirstAddressIpv6.Text = firstAddress.ToString();
            LastAddressIpv6.Text = lastAddress.ToString();
            AddressCountIpv6.Text = addressCount;

            NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Visible;
        }

        private string GetIpv6ScopeLabel(IPAddress ipAddress)
        {
            var bytes = ipAddress.GetAddressBytes();

            // Unspecified address (::)
            if (bytes.All(b => b == 0))
            {
                return "Unspecified";
            }

            // Loopback (::1)
            if (bytes.Take(15).All(b => b == 0) && bytes[15] == 1)
            {
                return "Loopback";
            }

            // Link-local (fe80::/10)
            if (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80)
            {
                return "Link-Local";
            }

            // Unique local (fc00::/7)
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return "Unique Local (ULA)";
            }

            // Multicast (ff00::/8)
            if (bytes[0] == 0xFF)
            {
                return "Multicast";
            }

            // IPv4-mapped (::ffff:0:0/96)
            if (bytes.Take(10).All(b => b == 0) && bytes[10] == 0xFF && bytes[11] == 0xFF)
            {
                return "IPv4-mapped";
            }

            // Documentation (2001:db8::/32)
            if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0D && bytes[3] == 0xB8)
            {
                return "Dokumentationsbereich";
            }

            // Global unicast
            if ((bytes[0] & 0xE0) == 0x20)
            {
                return "Global Unicast";
            }

            return "Reserviert";
        }

        private void UpdateIpScopeIndicator()
        {
            var input = NetworkCalcIpAddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(input))
            {
                NetworkCalcIpScopeTextBlock.Text = "IP-Bereich: -";
                return;
            }

            if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            {
                NetworkCalcIpScopeTextBlock.Text = "IP-Bereich: ungültige IPv4-Adresse";
                return;
            }

            NetworkCalcIpScopeTextBlock.Text = $"IP-Bereich: {GetIpv4ScopeLabel(ip)}";
        }

        private string GetIpv4ScopeLabel(IPAddress ipAddress)
        {
            var octets = ipAddress.GetAddressBytes();
            var ip = (uint)((octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3]);

            if (ip == 0xFFFFFFFFu)
            {
                return "Broadcast";
            }

            if (ip == 0u)
            {
                return "Unspecified";
            }

            if (IsInIpv4Range(ip, 0x7F000000u, 8))
            {
                return "Loopback";
            }

            if (IsInIpv4Range(ip, 0xA9FE0000u, 16))
            {
                return "Zeroconf (Link-Local)";
            }

            if (IsInIpv4Range(ip, 0xE0000000u, 4))
            {
                return "Multicast";
            }

            if (IsInIpv4Range(ip, 0x0A000000u, 8) ||
                IsInIpv4Range(ip, 0xAC100000u, 12) ||
                IsInIpv4Range(ip, 0xC0A80000u, 16))
            {
                return "Privater Bereich";
            }

            if (IsInIpv4Range(ip, 0x64400000u, 10))
            {
                return "Shared Address Space (CGNAT)";
            }

            if (IsInIpv4Range(ip, 0xC0000200u, 24) ||
                IsInIpv4Range(ip, 0xC6336400u, 24) ||
                IsInIpv4Range(ip, 0xCB007100u, 24))
            {
                return "Dokumentationsbereich";
            }

            if (IsInIpv4Range(ip, 0xF0000000u, 4))
            {
                return "Reserviert";
            }

            return "Public Bereich";
        }

        private bool IsInIpv4Range(uint ip, uint network, int prefixLength)
        {
            var mask = PrefixToMask(prefixLength);
            return (ip & mask) == (network & mask);
        }

        private void CalculateNetwork_Click(object sender, RoutedEventArgs e)
        {
            NetworkCalcErrorBar.IsOpen = false;
            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;

            try
            {
                string ipAddressInput = NetworkCalcIpAddressTextBox.Text.Trim();
                string subnetInput = NetworkCalcSubnetTextBox.Text.Trim();
                string cidrSuffixInput = NetworkCalcCidrTextBox.Text.Trim();

                if (string.IsNullOrEmpty(ipAddressInput))
                {
                    ShowError("Bitte geben Sie eine IP-Adresse ein.");
                    return;
                }

                if (!string.IsNullOrEmpty(cidrSuffixInput))
                {
                    CalculateFromSuffix(ipAddressInput, cidrSuffixInput);
                }
                else if (!string.IsNullOrEmpty(subnetInput))
                {
                    CalculateFromSubnet(ipAddressInput, subnetInput);
                }
                else
                {
                    ShowError("Bitte geben Sie Subnetzmaske oder CIDR-Sufix ein.");
                    return;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Fehler: {ex.Message}");
            }
        }

        private void CalculateFromSuffix(string ipAddress, string cidrSuffix)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                throw new ArgumentException("Ungültige IP-Adresse.");

            if (!TryParseCidrSuffix(cidrSuffix, out var prefixLength))
                throw new ArgumentException("Ungültiges Präfix. Muss zwischen 0 und 32 liegen.");

            uint mask = PrefixToMask(prefixLength);
            uint networkAddress = IpToUint(ip) & mask;
            uint broadcastAddress = networkAddress | ~mask;

            var networkIp = UintToIp(networkAddress);
            var broadcastIp = UintToIp(broadcastAddress);
            var firstUsable = UintToIp(networkAddress + 1);
            var lastUsable = UintToIp(broadcastAddress - 1);
            var subnetMask = UintToIp(mask);
            var wildcard = UintToIp(~mask);

            long hostCount = broadcastAddress - networkAddress - 1;
            if (hostCount < 0) hostCount = 0;

            DisplayResults(networkIp, broadcastIp, firstUsable, lastUsable, subnetMask, wildcard, hostCount, prefixLength);
        }

        private void CalculateFromSubnet(string ipAddress, string subnetMask)
        {
            if (!IPAddress.TryParse(ipAddress, out var ip))
                throw new ArgumentException("Ungültige IP-Adresse.");

            if (!IPAddress.TryParse(subnetMask, out var subnet))
                throw new ArgumentException("Ungültige Subnetzmaske.");

            uint ipUint = IpToUint(ip);
            uint subnetUint = IpToUint(subnet);
            if (!IsValidSubnetMask(subnetUint))
            {
                throw new ArgumentException("Ungültige Subnetzmaske. Die Bits müssen zusammenhängend sein.");
            }

            uint networkAddress = ipUint & subnetUint;
            uint broadcastAddress = networkAddress | ~subnetUint;

            int prefixLength = CountBits(subnetUint);
            var networkIp = UintToIp(networkAddress);
            var broadcastIp = UintToIp(broadcastAddress);
            var firstUsable = UintToIp(networkAddress + 1);
            var lastUsable = UintToIp(broadcastAddress - 1);
            var wildcard = UintToIp(~subnetUint);

            long hostCount = broadcastAddress - networkAddress - 1;
            if (hostCount < 0) hostCount = 0;

            DisplayResults(networkIp, broadcastIp, firstUsable, lastUsable, UintToIp(subnetUint), wildcard, hostCount, prefixLength);
        }

        private void DisplayResults(IPAddress networkAddress, IPAddress broadcastAddress, IPAddress firstUsable,
                                   IPAddress lastUsable, IPAddress subnetMask, IPAddress wildcard, long hostCount, int prefixLength)
        {
            NetworkAddress.Text = networkAddress.ToString();
            BroadcastAddress.Text = broadcastAddress.ToString();
            FirstUsableIp.Text = firstUsable.ToString();
            LastUsableIp.Text = lastUsable.ToString();
            SubnetMask.Text = subnetMask.ToString();
            CidrNotation.Text = $"/{prefixLength}";
            HostCount.Text = hostCount.ToString("N0");
            WildcardMask.Text = wildcard.ToString();

            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Visible;
        }

        private uint IpToUint(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            return (uint)((bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3]);
        }

        private uint PrefixToMask(int prefixLength)
        {
            return prefixLength == 0 ? 0u : (0xFFFFFFFFu << (32 - prefixLength));
        }

        private bool TryParseCidrSuffix(string input, out int prefixLength)
        {
            var sanitized = input.Trim();
            if (sanitized.StartsWith("/", StringComparison.Ordinal))
            {
                sanitized = sanitized.Substring(1);
            }

            if (!int.TryParse(sanitized, out prefixLength))
            {
                return false;
            }

            return prefixLength >= 0 && prefixLength <= 32;
        }

        private long CalculateUsableHosts(int prefixLength)
        {
            var hostBits = 32 - prefixLength;
            if (hostBits <= 1)
            {
                return 0;
            }

            return (1L << hostBits) - 2;
        }

        private bool TryGetPrefixFromMaxHosts(long maxHosts, out int prefixLength)
        {
            for (int candidatePrefix = 32; candidatePrefix >= 0; candidatePrefix--)
            {
                var capacity = CalculateUsableHosts(candidatePrefix);
                if (capacity >= maxHosts)
                {
                    prefixLength = candidatePrefix;
                    return true;
                }
            }

            prefixLength = 0;
            return false;
        }

        private bool TryParseSubnetMask(string subnetMask, out uint subnetUint)
        {
            subnetUint = 0;
            if (!IPAddress.TryParse(subnetMask, out var subnet))
            {
                return false;
            }

            subnetUint = IpToUint(subnet);
            return IsValidSubnetMask(subnetUint);
        }

        private bool IsValidSubnetMask(uint subnetMask)
        {
            var seenZero = false;
            for (int i = 31; i >= 0; i--)
            {
                var isOne = ((subnetMask >> i) & 1u) == 1u;
                if (!isOne)
                {
                    seenZero = true;
                    continue;
                }

                if (seenZero)
                {
                    return false;
                }
            }

            return true;
        }

        private IPAddress UintToIp(uint value)
        {
            return new IPAddress(new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            });
        }

        private int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                count += (int)(value & 1);
                value >>= 1;
            }
            return count;
        }

        private void ShowError(string message)
        {
            NetworkCalcErrorBar.Title = "Fehler";
            NetworkCalcErrorBar.Message = message;
            NetworkCalcErrorBar.IsOpen = true;
            NetworkCalcIpv4ResultsBorder.Visibility = Visibility.Collapsed;
            NetworkCalcIpv6ResultsBorder.Visibility = Visibility.Collapsed;
        }

        // Network Scanner Methoden
        private async void NetworkScanStartButton_Click(object sender, RoutedEventArgs e)
        {
            var rangeInput = NetworkScanRangesTextBox.Text.Trim();

            NetworkScanErrorBar.IsOpen = false;

            if (string.IsNullOrWhiteSpace(rangeInput))
            {
                ShowScanError("Bitte geben Sie mindestens einen IP-Bereich ein.");
                return;
            }

            if (!TryParseNetworkScanRanges(rangeInput, out var ipList, out var parseError))
            {
                ShowScanError(parseError ?? "Ungültiger IP-Bereich.");
                return;
            }

            // Cancel previous scan
            _networkScanCts?.Cancel();
            _networkScanCts = new CancellationTokenSource();

            NetworkDevices.Clear();
            NetworkScanDetailsPanel.Visibility = Visibility.Collapsed;
            NetworkScanDetailsPlaceholderText.Visibility = Visibility.Visible;
            NetworkScanStartButton.IsEnabled = false;
            NetworkScanProgressRing.IsActive = true;
            NetworkScanStatusTextBlock.Text = $"Scanne {ipList.Count} Adressen...";

            try
            {
                await ScanNetworkRangeAsync(ipList, _networkScanCts.Token);
            }
            catch (OperationCanceledException)
            {
                NetworkScanStatusTextBlock.Text = "Scan abgebrochen";
            }
            catch (Exception ex)
            {
                ShowScanError($"Fehler beim Scannen: {ex.Message}");
            }
            finally
            {
                NetworkScanStartButton.IsEnabled = true;
                NetworkScanProgressRing.IsActive = false;
            }
        }

        private async Task ScanNetworkRangeAsync(IReadOnlyList<IPAddress> ipAddresses, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            var semaphore = new SemaphoreSlim(50); // Limit parallel operations

            foreach (var currentIp in ipAddresses)
            {
                tasks.Add(ScanSingleDeviceAsync(currentIp, semaphore, cancellationToken));
            }

            await Task.WhenAll(tasks);

            DispatcherQueue.TryEnqueue(() =>
            {
                NetworkScanCountTextBlock.Text = $"{NetworkDevices.Count} Gerät(e)";
                NetworkScanStatusTextBlock.Text = NetworkDevices.Count > 0
                    ? $"Scan abgeschlossen - {NetworkDevices.Count} Gerät(e) gefunden"
                    : "Scan abgeschlossen - Keine Geräte gefunden";
            });
        }

        private void PrefillNetworkScanRangesFromNic1()
        {
            if (NetworkScanRangesTextBox == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(NetworkScanRangesTextBox.Text))
            {
                return;
            }

            try
            {
                var adapterSettings = _adapterStore.ReadAdapters();
                if (string.IsNullOrWhiteSpace(adapterSettings.PrimaryAdapter))
                {
                    return;
                }

                var ipv4Config = _networkInfoService.GetIpv4Config(adapterSettings.PrimaryAdapter);
                var nicIp = ipv4Config?.IpAddresses.FirstOrDefault().IpAddress;
                if (string.IsNullOrWhiteSpace(nicIp) || !IPAddress.TryParse(nicIp, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
                {
                    return;
                }

                var bytes = ip.GetAddressBytes();
                NetworkScanRangesTextBox.Text = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.1-254";
            }
            catch
            {
                // Ignore prefill errors; manual input remains available.
            }
        }

        private bool TryParseNetworkScanRanges(string input, out List<IPAddress> ipAddresses, out string? errorMessage)
        {
            ipAddresses = new List<IPAddress>();
            errorMessage = null;

            var uniqueIps = new HashSet<uint>();
            var ranges = input.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ranges.Length == 0)
            {
                errorMessage = "Bitte geben Sie mindestens einen gültigen Bereich ein.";
                return false;
            }

            foreach (var rangeText in ranges)
            {
                if (!TryParseSingleNetworkRange(rangeText, out var startIp, out var endIp, out var rangeError))
                {
                    errorMessage = rangeError;
                    return false;
                }

                for (uint ipUint = startIp; ipUint <= endIp; ipUint++)
                {
                    uniqueIps.Add(ipUint);
                }
            }

            if (uniqueIps.Count == 0)
            {
                errorMessage = "Es wurden keine IP-Adressen aus der Eingabe erkannt.";
                return false;
            }

            if (uniqueIps.Count > 1024)
            {
                errorMessage = "IP-Bereich zu groß. Maximal 1024 Adressen.";
                return false;
            }

            ipAddresses = uniqueIps
                .OrderBy(v => v)
                .Select(UintToIp)
                .ToList();

            return true;
        }

        private bool TryParseSingleNetworkRange(string rangeText, out uint startIp, out uint endIp, out string? errorMessage)
        {
            startIp = 0;
            endIp = 0;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(rangeText))
            {
                errorMessage = "Leerer Bereich in der Eingabe gefunden.";
                return false;
            }

            var normalized = rangeText.Trim();
            if (!normalized.Contains('-'))
            {
                if (TryParseIpv4ToUint(normalized, out var singleIp))
                {
                    startIp = singleIp;
                    endIp = singleIp;
                    return true;
                }

                errorMessage = $"Ungültige IP-Adresse: {rangeText}";
                return false;
            }

            var parts = normalized.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                errorMessage = $"Ungültiger Bereich: {rangeText}";
                return false;
            }

            if (!TryParseIpv4ToUint(parts[0], out var parsedStartIp))
            {
                errorMessage = $"Ungültige Start-IP: {parts[0]}";
                return false;
            }

            uint parsedEndIp;
            if (parts[1].Contains('.'))
            {
                if (!TryParseIpv4ToUint(parts[1], out parsedEndIp))
                {
                    errorMessage = $"Ungültige End-IP: {parts[1]}";
                    return false;
                }
            }
            else
            {
                var firstIpBytes = UintToIp(parsedStartIp).GetAddressBytes();
                if (!byte.TryParse(parts[1], out var lastOctet))
                {
                    errorMessage = $"Ungültiger Endbereich: {parts[1]}";
                    return false;
                }

                parsedEndIp = IpToUint(new IPAddress(new byte[]
                {
                    firstIpBytes[0],
                    firstIpBytes[1],
                    firstIpBytes[2],
                    lastOctet
                }));
            }

            if (parsedStartIp > parsedEndIp)
            {
                errorMessage = $"Start-IP muss kleiner oder gleich End-IP sein: {rangeText}";
                return false;
            }

            startIp = parsedStartIp;
            endIp = parsedEndIp;
            return true;
        }

        private bool TryParseIpv4ToUint(string input, out uint ipUint)
        {
            ipUint = 0;
            if (!IPAddress.TryParse(input, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            ipUint = IpToUint(ip);
            return true;
        }

        private async Task ScanSingleDeviceAsync(IPAddress ipAddress, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ipAddress, 500);

                if (reply.Status == IPStatus.Success)
                {
                    var device = new NetworkDevice
                    {
                        IpAddress = ipAddress.ToString(),
                        MacAddress = "-",
                        Hostname = "-"
                    };

                    // Try to get MAC address, hostname, and scan ports in parallel
                    var macTask = Task.Run(() => GetMacAddressAsync(ipAddress.ToString()), cancellationToken);
                    var hostnameTask = Task.Run(() => GetHostnameAsync(ipAddress), cancellationToken);
                    var portsTask = Task.Run(() => ScanPortsAsync(ipAddress, cancellationToken), cancellationToken);

                    await Task.WhenAll(macTask, hostnameTask, portsTask);

                    device.MacAddress = macTask.Result;
                    device.Hostname = hostnameTask.Result;

                    var openPorts = portsTask.Result;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        foreach (var port in openPorts)
                        {
                            device.OpenPorts.Add(port);
                        }
                        NetworkDevices.Add(device);
                    });
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ignore individual failures
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task<string> GetMacAddressAsync(string ipAddress)
        {
            try
            {
                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "arp",
                    Arguments = $"-a {ipAddress}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processStartInfo);
                if (process == null) return "-";

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                // Parse ARP output for MAC address
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(ipAddress))
                    {
                        // Extract MAC address (format: xx-xx-xx-xx-xx-xx)
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in parts)
                        {
                            if (part.Length == 17 && part.Count(c => c == '-') == 5)
                            {
                                return part.ToUpper();
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return "-";
        }

        private async Task<string> GetHostnameAsync(IPAddress ipAddress)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(ipAddress);
                return hostEntry.HostName ?? "-";
            }
            catch
            {
                return "-";
            }
        }

        private async Task<List<string>> ScanPortsAsync(IPAddress ipAddress, CancellationToken cancellationToken)
        {
            var openPorts = new List<string>();
            var portsToScan = new Dictionary<int, string>();

            // Standard ports
            if (_settingsService.GetScanPortHttp()) portsToScan[80] = "HTTP";
            if (_settingsService.GetScanPortHttps()) portsToScan[443] = "HTTPS";
            if (_settingsService.GetScanPortFtp()) portsToScan[21] = "FTP";
            if (_settingsService.GetScanPortSsh()) portsToScan[22] = "SSH";
            if (_settingsService.GetScanPortSmb()) portsToScan[445] = "SMB";
            if (_settingsService.GetScanPortRdp()) portsToScan[3389] = "RDP";

            // Custom ports
            var custom1 = _settingsService.GetCustomPort1();
            if (custom1 > 0 && custom1 <= 65535) portsToScan[custom1] = $"Custom ({custom1})";

            var custom2 = _settingsService.GetCustomPort2();
            if (custom2 > 0 && custom2 <= 65535) portsToScan[custom2] = $"Custom ({custom2})";

            var custom3 = _settingsService.GetCustomPort3();
            if (custom3 > 0 && custom3 <= 65535) portsToScan[custom3] = $"Custom ({custom3})";

            var tasks = portsToScan.Select(kvp => CheckPortAsync(ipAddress, kvp.Key, kvp.Value, cancellationToken)).ToList();
            var results = await Task.WhenAll(tasks);

            foreach (var result in results.Where(r => r != null))
            {
                openPorts.Add(result!);
            }

            return openPorts;
        }

        private async Task<string?> CheckPortAsync(IPAddress ipAddress, int port, string portName, CancellationToken cancellationToken)
        {
            try
            {
                using var tcpClient = new System.Net.Sockets.TcpClient();
                var connectTask = tcpClient.ConnectAsync(ipAddress, port);
                var timeoutTask = Task.Delay(1000, cancellationToken);

                if (await Task.WhenAny(connectTask, timeoutTask) == connectTask && tcpClient.Connected)
                {
                    return portName;
                }
            }
            catch
            {
                // Port closed or unreachable
            }

            return null;
        }

        private void NetworkScanResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NetworkScanResultsListView.SelectedItem is NetworkDevice selectedDevice)
            {
                // Details anzeigen
                NetworkScanDetailsPanel.Visibility = Visibility.Visible;
                NetworkScanDetailsPlaceholderText.Visibility = Visibility.Collapsed;
                DeviceDetailsIpTextBlock.Text = selectedDevice.IpAddress;
                DeviceDetailsMacTextBlock.Text = selectedDevice.MacAddress;
                DeviceDetailsHostnameTextBlock.Text = selectedDevice.Hostname;

                // Ports anzeigen wenn vorhanden
                if (selectedDevice.OpenPorts != null && selectedDevice.OpenPorts.Count > 0)
                {
                    DeviceDetailsPortsPanel.Visibility = Visibility.Visible;
                    DeviceDetailsPortsItemsControl.ItemsSource = selectedDevice.OpenPorts;
                }
                else
                {
                    DeviceDetailsPortsPanel.Visibility = Visibility.Collapsed;
                    DeviceDetailsPortsItemsControl.ItemsSource = null;
                }
            }
            else
            {
                // Kein Gerät ausgewählt - Details ausblenden
                NetworkScanDetailsPanel.Visibility = Visibility.Collapsed;
                NetworkScanDetailsPlaceholderText.Visibility = Visibility.Visible;
            }
        }

        private void ShowScanError(string message)
        {
            NetworkScanErrorBar.Title = "Fehler";
            NetworkScanErrorBar.Message = message;
            NetworkScanErrorBar.IsOpen = true;
        }
    }
}
