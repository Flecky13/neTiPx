using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Models;
using neTiPx.Services;
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
    public sealed partial class PingPage : Page
    {
        private readonly PingTargetsStore _pingTargetsStore = new PingTargetsStore();
        private readonly PingLogService _pingLogService = new PingLogService();
        private readonly SettingsService _settingsService = new SettingsService();

        private readonly Dictionary<PingTarget, CancellationTokenSource> _pingTimers = new Dictionary<PingTarget, CancellationTokenSource>();
        private readonly Dictionary<PingTarget, string> _lastValidTargets = new Dictionary<PingTarget, string>();
        private bool _isPingPageVisible = true;

        public ObservableCollection<PingTarget> PingTargets { get; } = new ObservableCollection<PingTarget>();

        public PingPage()
        {
            InitializeComponent();
            Loaded += PingPage_Loaded;
            Unloaded += PingPage_Unloaded;
            LoadPingTargets();
        }

        private void PingPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (BackgroundActiveCheckBox != null)
            {
                BackgroundActiveCheckBox.IsChecked = _settingsService.GetPingBackgroundActive();
            }

            _isPingPageVisible = true;
            UpdatePingingState();
        }

        private void PingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isPingPageVisible = false;
            UpdatePingingState();
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
                    DetermineAddressType(pingTarget);
                    PingTargets.Add(pingTarget);
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
            var shouldPingBeActive = _isPingPageVisible || (BackgroundActiveCheckBox?.IsChecked == true);

            foreach (var target in PingTargets)
            {
                if (!target.IsPingEnabled)
                {
                    continue;
                }

                var isCurrentlyPinging = _pingTimers.ContainsKey(target);

                if (shouldPingBeActive && !isCurrentlyPinging)
                {
                    StartPingingAsync(target);
                }
                else if (!shouldPingBeActive && isCurrentlyPinging)
                {
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
            }
        }

        private async Task ExecutePingAsync(PingTarget target)
        {
            try
            {
                var ipv4Task = PingAsync(target.Target, AddressFamily.InterNetwork);
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
                if (ipAddress.AddressFamily == AddressFamily.InterNetwork)
                {
                    target.ShowIPv4 = Visibility.Visible;
                    target.ShowIPv6 = Visibility.Collapsed;
                    target.ResponseTimeIpv6 = "ungültig";
                    target.StatusColorIpv6 = new SolidColorBrush(Colors.Gray);
                }
                else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    target.ShowIPv4 = Visibility.Collapsed;
                    target.ShowIPv6 = Visibility.Visible;
                    target.ResponseTimeIpv4 = "ungültig";
                    target.StatusColorIpv4 = new SolidColorBrush(Colors.Gray);
                }
            }
            else
            {
                target.ShowIPv4 = Visibility.Visible;
                target.ShowIPv6 = Visibility.Visible;
            }
        }

        private async Task<PingResult> PingAsync(string target, AddressFamily addressFamily)
        {
            try
            {
                using var ping = new Ping();

                if (IPAddress.TryParse(target, out var ipAddress))
                {
                    if (ipAddress.AddressFamily == addressFamily)
                    {
                        return new PingResult(await ping.SendPingAsync(ipAddress, 3000), ipAddress.ToString());
                    }
                    return new PingResult(null, string.Empty);
                }

                var hostEntry = await Dns.GetHostEntryAsync(target);
                if (hostEntry?.AddressList == null || hostEntry.AddressList.Length == 0)
                {
                    return new PingResult(null, string.Empty);
                }

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
                var statusColor = result.Reply.RoundtripTime switch
                {
                    < 50 => new SolidColorBrush(Colors.Green),
                    < 150 => new SolidColorBrush(Colors.Yellow),
                    _ => new SolidColorBrush(Colors.Orange)
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
                var statusColor = new SolidColorBrush(Colors.Red);

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
    }
}
