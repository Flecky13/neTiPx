using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace neTiPx.Views
{
    public sealed partial class PingPage : Page, INotifyPropertyChanged
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private readonly PingTargetsStore _pingTargetsStore = new PingTargetsStore();
        private readonly PingLogService _pingLogService = new PingLogService();
        private readonly SettingsService _settingsService = new SettingsService();

        private readonly Dictionary<PingTarget, CancellationTokenSource> _pingTimers = new Dictionary<PingTarget, CancellationTokenSource>();
        private readonly Dictionary<PingTarget, string> _lastValidTargets = new Dictionary<PingTarget, string>();
        private bool _isPingPageVisible;
        private bool _isPageLoaded;
        private bool _isWindowActive;
        private bool _isHostPingTabActive = true;
        private AppWindow? _mainAppWindow;
        private readonly long _visibilityChangedToken;

        private string _pingResponsePlaceholder = "-- ms";
        private string _pingIpv4ReachabilityTooltip = "";
        private string _pingIpv6ReachabilityTooltip = "";
        private string _deletePingTooltip = "";
        private string _openLogTooltip = "";
        private string _pingEnabledTooltip = "";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PingResponsePlaceholder
        {
            get => _pingResponsePlaceholder;
            private set => SetField(ref _pingResponsePlaceholder, value);
        }

        public string PingIpv4ReachabilityTooltip
        {
            get => _pingIpv4ReachabilityTooltip;
            private set => SetField(ref _pingIpv4ReachabilityTooltip, value);
        }

        public string PingIpv6ReachabilityTooltip
        {
            get => _pingIpv6ReachabilityTooltip;
            private set => SetField(ref _pingIpv6ReachabilityTooltip, value);
        }

        public string DeletePingTooltip
        {
            get => _deletePingTooltip;
            private set => SetField(ref _deletePingTooltip, value);
        }

        public string OpenLogTooltip
        {
            get => _openLogTooltip;
            private set => SetField(ref _openLogTooltip, value);
        }

        public string PingEnabledTooltip
        {
            get => _pingEnabledTooltip;
            private set => SetField(ref _pingEnabledTooltip, value);
        }

        public ObservableCollection<PingTarget> PingTargets { get; } = new ObservableCollection<PingTarget>();

        public PingPage()
        {
            InitializeComponent();
            Loaded += PingPage_Loaded;
            Unloaded += PingPage_Unloaded;
            _visibilityChangedToken = RegisterPropertyChangedCallback(VisibilityProperty, PingPage_VisibilityChanged);
            LoadPingTargets();
            UpdateLanguage();
        }

        private static string T(string key)
        {
            return _lm.Lang(key);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
        }

        private void PingPage_Loaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _lm.LanguageChanged += OnLanguageChanged;

            if (BackgroundActiveCheckBox != null)
            {
                BackgroundActiveCheckBox.IsChecked = _settingsService.GetPingBackgroundActive();
            }

            _isPageLoaded = true;
            _isPingPageVisible = Visibility == Visibility.Visible;
            _mainAppWindow = WindowHelper.GetAppWindow(App.MainWindow);
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed += MainAppWindow_Changed;
            }

            App.MainWindow.Activated += MainWindow_Activated;
            _isWindowActive = _mainAppWindow?.IsVisible == true;
            UpdatePingingState();
        }

        private void PingPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _isPageLoaded = false;
            _isPingPageVisible = false;
            _lm.LanguageChanged -= OnLanguageChanged;

            App.MainWindow.Activated -= MainWindow_Activated;
            if (_mainAppWindow != null)
            {
                _mainAppWindow.Changed -= MainAppWindow_Changed;
            }

            UpdatePingingState();
        }

        private void UpdateLanguage()
        {
            if (PingTitleText != null) PingTitleText.Text = T("PING_TITLE");
            if (ConfigTitleText != null) ConfigTitleText.Text = T("PING_CONFIG_TITLE");
            if (BackgroundActiveText != null) BackgroundActiveText.Text = T("PING_CONFIG_BACKGROUND_ACTIVE");

            if (NewPingTargetTextBox != null)
            {
                NewPingTargetTextBox.Header = T("PING_TARGET_HEADER");
                NewPingTargetTextBox.PlaceholderText = T("PING_TARGET_PLACEHOLDER");
            }

            if (PingIntervalNumberBox != null)
            {
                PingIntervalNumberBox.Header = T("PING_INTERVAL_HEADER");
            }

            if (AddPingTargetButton != null)
            {
                AddPingTargetButton.Content = T("PING_ADD_BUTTON");
            }

            if (HeaderAddressText != null) HeaderAddressText.Text = T("PING_HEADER_ADDRESS");
            if (HeaderIntervalText != null) HeaderIntervalText.Text = T("PING_HEADER_INTERVAL");
            if (HeaderIpv4Text != null) HeaderIpv4Text.Text = "IPv4";
            if (HeaderIpv6Text != null) HeaderIpv6Text.Text = "IPv6";
            if (HeaderDeleteText != null) HeaderDeleteText.Text = T("PING_HEADER_DELETE");
            if (HeaderLogText != null) HeaderLogText.Text = T("PING_HEADER_LOG");
            if (HeaderActiveText != null) HeaderActiveText.Text = T("PING_HEADER_ACTIVE");

            PingResponsePlaceholder = T("PING_RESPONSE_PLACEHOLDER");
            PingIpv4ReachabilityTooltip = T("PING_TOOLTIP_IPV4_REACHABILITY");
            PingIpv6ReachabilityTooltip = T("PING_TOOLTIP_IPV6_REACHABILITY");
            DeletePingTooltip = T("PING_TOOLTIP_DELETE");
            OpenLogTooltip = T("PING_TOOLTIP_OPEN_LOG");
            PingEnabledTooltip = T("PING_TOOLTIP_ENABLED");
        }

        private bool SetField<TField>(ref TField field, TField value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<TField>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private void PingPage_VisibilityChanged(DependencyObject sender, DependencyProperty dependencyProperty)
        {
            _isPingPageVisible = Visibility == Visibility.Visible;
            UpdatePingingState();
        }

        private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            _isWindowActive = args.WindowActivationState != WindowActivationState.Deactivated;
            UpdatePingingState();
        }

        private void MainAppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidVisibilityChange)
            {
                UpdatePingingState();
            }
        }

        public void SetHostPingTabActive(bool isActive)
        {
            _isHostPingTabActive = isActive;
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
                DebugLogger.Log(LogLevel.WARN, "Ping", $"Ziel bereits vorhanden, kein Duplikat: {target}");
                return;
            }

            var intervalSeconds = (int)PingIntervalNumberBox.Value;
            DebugLogger.Log(LogLevel.INFO, "Ping", $"Ziel hinzugefügt: {target}, Intervall: {intervalSeconds}s");
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
                DebugLogger.Log(LogLevel.INFO, "Ping", $"Ziel löschen angefordert: {target.Target}");
                var deleteConfirmed = await ConfirmLogDeleteActionAsync(target);
                if (!deleteConfirmed)
                {
                    DebugLogger.Log(LogLevel.INFO, "Ping", $"Ziel löschen abgebrochen: {target.Target}");
                    return;
                }

                DebugLogger.Log(LogLevel.INFO, "Ping", $"Ziel gelöscht: {target.Target}");

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
                Title = T("PING_DIALOG_DELETE_LOG_TITLE"),
                Content = T("PING_DIALOG_DELETE_LOG_CONTENT"),
                PrimaryButtonText = T("PING_DIALOG_YES"),
                SecondaryButtonText = T("PING_DIALOG_NO"),
                CloseButtonText = T("PING_DIALOG_CANCEL"),
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
            picker.FileTypeChoices.Clear();
            picker.FileTypeChoices.Add(T("PING_LOG_FILETYPE"), new List<string> { ".log" });

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

            target.ResponseTimeIpv4 = T("PING_STATUS_DISABLED");
            target.StatusColorIpv4 = new SolidColorBrush(Colors.Gray);
            target.ResetTrendIpv4();
            target.ResponseTimeIpv6 = T("PING_STATUS_DISABLED");
            target.StatusColorIpv6 = new SolidColorBrush(Colors.Gray);
            target.ResetTrendIpv6();
        }

        private void PingEnabledCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox || checkBox.Tag is not PingTarget target)
            {
                return;
            }

            var isEnabled = checkBox.IsChecked == true;
            target.IsPingEnabled = isEnabled;
            DebugLogger.Log(LogLevel.INFO, "Ping", $"Ping {(isEnabled ? "aktiviert" : "deaktiviert")}: {target.Target}");

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
            DebugLogger.Log(LogLevel.INFO, "Ping", $"Hintergrund-Ping {(isActive ? "aktiviert" : "deaktiviert")}");
            _settingsService.SetPingBackgroundActive(isActive);
            UpdatePingingState();
        }

        private void UpdatePingingState()
        {
            var isWindowVisible = _mainAppWindow?.IsVisible ?? false;
            var isBackgroundActive = BackgroundActiveCheckBox?.IsChecked == true;
            var shouldPingByFocus = _isPageLoaded && _isHostPingTabActive && _isPingPageVisible && _isWindowActive && isWindowVisible;
            var shouldPingBeActive = isBackgroundActive || shouldPingByFocus;

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
                        target.ResponseTimeIpv4 = T("PING_STATUS_ERROR");
                        target.StatusColorIpv4 = new SolidColorBrush(Colors.Red);
                        target.UpdateTrendIpv4(null, isBad: true);
                        _pingLogService.AppendPingResult(target.Target, "IPv4", T("PING_STATUS_ERROR"), target.ResolvedAddressIpv4);
                    }

                    if (target.ShowIPv6 == Visibility.Visible)
                    {
                        target.ResponseTimeIpv6 = T("PING_STATUS_ERROR");
                        target.StatusColorIpv6 = new SolidColorBrush(Colors.Red);
                        target.UpdateTrendIpv6(null, isBad: true);
                        _pingLogService.AppendPingResult(target.Target, "IPv6", T("PING_STATUS_ERROR"), target.ResolvedAddressIpv6);
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
                    target.ResponseTimeIpv6 = T("PING_STATUS_INVALID");
                    target.StatusColorIpv6 = new SolidColorBrush(Colors.Gray);
                    target.ResetTrendIpv6();
                }
                else if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    target.ShowIPv4 = Visibility.Collapsed;
                    target.ShowIPv6 = Visibility.Visible;
                    target.ResponseTimeIpv4 = T("PING_STATUS_INVALID");
                    target.StatusColorIpv4 = new SolidColorBrush(Colors.Gray);
                    target.ResetTrendIpv4();
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
                    target.UpdateTrendIpv4(result.Reply.RoundtripTime, isBad: false);
                    _pingLogService.AppendPingResult(target.Target, "IPv4", responseTimeStr, result.ResolvedAddress);
                }
                else
                {
                    target.ResponseTimeIpv6 = responseTimeStr;
                    target.StatusColorIpv6 = statusColor;
                    target.PingCountIpv6++;
                    target.AddResponseTimeIpv6(result.Reply.RoundtripTime);
                    target.UpdateTrendIpv6(result.Reply.RoundtripTime, isBad: false);
                    _pingLogService.AppendPingResult(target.Target, "IPv6", responseTimeStr, result.ResolvedAddress);
                }
            }
            else
            {
                var statusColor = new SolidColorBrush(Colors.Red);

                if (type == ResponseType.IPv4)
                {
                    target.ResponseTimeIpv4 = T("PING_STATUS_TIMEOUT");
                    target.StatusColorIpv4 = statusColor;
                    target.PingCountIpv4++;
                    target.TimeoutCountIpv4++;
                    target.UpdateTrendIpv4(null, isBad: true);
                    _pingLogService.AppendPingResult(target.Target, "IPv4", T("PING_STATUS_TIMEOUT"), result.ResolvedAddress);
                }
                else
                {
                    target.ResponseTimeIpv6 = T("PING_STATUS_TIMEOUT");
                    target.StatusColorIpv6 = statusColor;
                    target.PingCountIpv6++;
                    target.TimeoutCountIpv6++;
                    target.UpdateTrendIpv6(null, isBad: true);
                    _pingLogService.AppendPingResult(target.Target, "IPv6", T("PING_STATUS_TIMEOUT"), result.ResolvedAddress);
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
