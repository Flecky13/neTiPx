using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        public ObservableCollection<PingTarget> PingTargets { get; } = new ObservableCollection<PingTarget>();
        private readonly Dictionary<PingTarget, CancellationTokenSource> _pingTimers = new Dictionary<PingTarget, CancellationTokenSource>();
        private readonly Dictionary<PingTarget, string> _lastValidTargets = new Dictionary<PingTarget, string>();
        private bool _isPingPageVisible = true;
        private bool _isSyncingNetworkCalcInputs;
        private bool _isIpv6Mode = false;

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

        }

        private void ToolsPage_Unloaded(object sender, RoutedEventArgs e)
        {
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
                        if (WlanPanel != null)
                        {
                            WlanPanel.Visibility = Visibility.Visible;
                            if (WlanPanel.Content == null)
                            {
                                WlanPanel.Navigate(typeof(WlanPage));
                            }
                        }
                        _isPingPageVisible = false;
                        break;
                    case "NetworkCalculator":
                        if (NetworkCalculatorPanel != null) NetworkCalculatorPanel.Visibility = Visibility.Visible;
                        _isPingPageVisible = false;
                        break;
                    case "NetworkScanner":
                        if (NetworkScannerPanel != null)
                        {
                            NetworkScannerPanel.Visibility = Visibility.Visible;
                            if (NetworkScannerPanel.Content == null)
                            {
                                NetworkScannerPanel.Navigate(typeof(NetworkScannerPage));
                            }
                        }
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
    }
}

