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
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
        private const int WifiListBaseHeight = 240;
        private const int MainWindowMinHeight = 950;
        private AppWindow? _mainAppWindow;
        public ObservableCollection<PingTarget> PingTargets { get; } = new ObservableCollection<PingTarget>();
        private readonly Dictionary<PingTarget, CancellationTokenSource> _pingTimers = new Dictionary<PingTarget, CancellationTokenSource>();
        private readonly Dictionary<PingTarget, string> _lastValidTargets = new Dictionary<PingTarget, string>();
        private bool _isPingPageVisible = true;

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
                if (PlaceholderPanel != null) PlaceholderPanel.Visibility = Visibility.Collapsed;

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
                    case "Placeholder":
                        if (PlaceholderPanel != null) PlaceholderPanel.Visibility = Visibility.Visible;
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

        private void WifiNetworksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WifiDetailsTextBlock == null)
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

                string securityIcon = selectedNetwork.IsSecured ? "🔒" : "🔓";

                var details = $"{selectedNetwork.SignalSymbol} {selectedNetwork.SSID}\n" +
                    $"\n" +
                    $"BSSID: {selectedNetwork.BSSID}\n" +
                    $"Signal: {selectedNetwork.SignalStrengthPercent}% ({selectedNetwork.SignalStrengthDbm} dBm)\n" +
                    $"Qualität: {selectedNetwork.LinkQuality}%\n" +
                    $"\n" +
                    $"Kanal: {selectedNetwork.Channel}\n" +
                    $"Frequenz: {selectedNetwork.Frequency} MHz{band}\n" +
                    $"Sicherheit: {securityIcon} {(selectedNetwork.IsSecured ? "Gesichert" : "Offen")}\n" +
                    $"\n" +
                    $"Typ: {selectedNetwork.PhyType}\n" +
                    $"Netzwerk: {selectedNetwork.NetworkType}";

                WifiDetailsTextBlock.Text = details;
            }
            else
            {
                WifiDetailsTextBlock.Text = "Wählen Sie ein Netzwerk aus...";
            }
        }
    }
}
