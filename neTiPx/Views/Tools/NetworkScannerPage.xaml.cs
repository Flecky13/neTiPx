using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace neTiPx.Views
{
    public sealed partial class NetworkScannerPage : Page
    {
        private static readonly LanguageManager _lm = LanguageManager.Instance;
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly AdapterStore _adapterStore = new AdapterStore();
        private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();
        private readonly NetworkScanStore _networkScanStore = new NetworkScanStore();

        private string _networkScanSortColumn = string.Empty;
        private bool _networkScanSortAscending = true;
        private CancellationTokenSource? _networkScanCts;
        private CancellationTokenSource? _detailsLoadCts;
        private readonly HashSet<string> _detailsLoadedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _detailsLoadingIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<NetworkDevice> NetworkDevices { get; } = new ObservableCollection<NetworkDevice>();

        public NetworkScannerPage()
        {
            InitializeComponent();
            Loaded += NetworkScannerPage_Loaded;
            Unloaded += NetworkScannerPage_Unloaded;
        }

        private static string T(string key)
        {
            return _lm.Lang(key);
        }

        private void OnLanguageChanged(object? sender, EventArgs e)
        {
            UpdateLanguage();
            UpdateNetworkScanHeaderLabels();
            UpdateScanCountLabel();
        }

        private void UpdateLanguage()
        {
            if (NetworkScanTitleText != null) NetworkScanTitleText.Text = T("TOOLS_NET_SCAN");
            if (NetworkScanRangesLabelText != null) NetworkScanRangesLabelText.Text = T("NETSCAN_LABEL_RANGES");
            if (NetworkScanRangesTextBox != null) NetworkScanRangesTextBox.PlaceholderText = T("NETSCAN_PLACEHOLDER_RANGES");
            if (NetworkScanRangesHintText != null) NetworkScanRangesHintText.Text = T("NETSCAN_HINT_RANGES");
            if (NetworkScanStartButton != null) NetworkScanStartButton.Content = T("NETSCAN_BUTTON_START");
            if (NetworkScanFoundDevicesTitleText != null) NetworkScanFoundDevicesTitleText.Text = T("NETSCAN_FOUND_DEVICES");
            if (NetworkScanDetailsTitleText != null) NetworkScanDetailsTitleText.Text = T("NETSCAN_DETAILS_TITLE");
            if (NetworkScanDetailsPlaceholderText != null && NetworkScanDetailsPlaceholderText.Visibility == Visibility.Visible)
            {
                NetworkScanDetailsPlaceholderText.Text = T("NETSCAN_DETAILS_PLACEHOLDER");
            }

            if (DeviceDetailsIpLabelText != null) DeviceDetailsIpLabelText.Text = T("NETSCAN_LABEL_IP");
            if (DeviceDetailsMacLabelText != null) DeviceDetailsMacLabelText.Text = T("NETSCAN_LABEL_MAC");
            if (DeviceDetailsHostnameLabelText != null) DeviceDetailsHostnameLabelText.Text = T("NETSCAN_LABEL_HOSTNAME");
            if (DeviceDetailsPortsLabelText != null) DeviceDetailsPortsLabelText.Text = T("NETSCAN_LABEL_OPEN_PORTS");
            if (NetworkScanStatusTextBlock != null && string.IsNullOrWhiteSpace(NetworkScanStatusTextBlock.Text))
            {
                NetworkScanStatusTextBlock.Text = T("NETSCAN_STATUS_READY");
            }
        }

        private void UpdateScanCountLabel()
        {
            if (NetworkScanCountTextBlock != null)
            {
                NetworkScanCountTextBlock.Text = $"{T("NETSCAN_COUNT_FORMAT")}: {NetworkDevices.Count}";
            }
        }

        private void NetworkScannerPage_Loaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _lm.LanguageChanged += OnLanguageChanged;
            UpdateLanguage();
            UpdateNetworkScanHeaderLabels();
            UpdateScanCountLabel();
            LoadLastScanRanges();
        }

        private void NetworkScannerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _lm.LanguageChanged -= OnLanguageChanged;
            _networkScanCts?.Cancel();
            _detailsLoadCts?.Cancel();
        }

        private async void NetworkScanStartButton_Click(object sender, RoutedEventArgs e)
        {
            var rangeInput = NetworkScanRangesTextBox.Text.Trim();

            NetworkScanErrorBar.IsOpen = false;

            if (string.IsNullOrWhiteSpace(rangeInput))
            {
                ShowScanError(T("NETSCAN_ERROR_ENTER_RANGE"));
                return;
            }

            if (!TryParseNetworkScanRanges(rangeInput, out var ipList, out var parseError))
            {
                DebugLogger.Log(LogLevel.WARN, "NetScan", $"Eingabe ungültig: {parseError}");
                ShowScanError(parseError ?? T("NETSCAN_ERROR_INVALID_RANGE"));
                return;
            }

            DebugLogger.Log(LogLevel.INFO, "NetScan", $"Scan startet: Eingabe='{rangeInput}', IPs={ipList.Count}");

            // Bereiche speichern
            _networkScanStore.WriteLastScanRanges(rangeInput);

            _networkScanCts?.Cancel();
            _networkScanCts = new CancellationTokenSource();
            _detailsLoadCts?.Cancel();
            _detailsLoadedIps.Clear();
            _detailsLoadingIps.Clear();

            NetworkDevices.Clear();
            NetworkScanDetailsPanel.Visibility = Visibility.Collapsed;
            NetworkScanDetailsPlaceholderText.Visibility = Visibility.Visible;
            NetworkScanDetailsPlaceholderText.Text = T("NETSCAN_DETAILS_PLACEHOLDER");
            NetworkScanStartButton.IsEnabled = false;
            NetworkScanProgressRing.IsActive = true;
            NetworkScanStatusTextBlock.Text = $"{T("NETSCAN_STATUS_SCANNING")}: {ipList.Count}";

            try
            {
                await ScanNetworkRangeAsync(ipList, _networkScanCts.Token);
            }
            catch (OperationCanceledException)
            {
                DebugLogger.Log(LogLevel.WARN, "NetScan", "Scan vom Benutzer abgebrochen");
                NetworkScanStatusTextBlock.Text = T("NETSCAN_STATUS_CANCELED");
            }
            catch (Exception ex)
            {
                DebugLogger.Log(LogLevel.ERROR, "NetScan", "Scan fehlgeschlagen", ex);
                ShowScanError($"{T("NETSCAN_ERROR_SCAN_FAILED")}: {ex.Message}");
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
            var semaphore = new SemaphoreSlim(50);

            foreach (var currentIp in ipAddresses)
            {
                tasks.Add(ScanSingleDeviceAsync(currentIp, semaphore, cancellationToken));
            }

            await Task.WhenAll(tasks);

            DispatcherQueue.TryEnqueue(() =>
            {
                if (!string.IsNullOrEmpty(_networkScanSortColumn))
                {
                    ApplyNetworkScanSorting();
                }

                UpdateScanCountLabel();
                var statusMsg = NetworkDevices.Count > 0
                    ? $"{T("NETSCAN_STATUS_FINISHED_FOUND")} ({NetworkDevices.Count})"
                    : T("NETSCAN_STATUS_FINISHED_NONE");
                DebugLogger.Log(LogLevel.INFO, "NetScan", $"Scan abgeschlossen: {NetworkDevices.Count} Gerät(e) gefunden");
                NetworkScanStatusTextBlock.Text = statusMsg;
            });
        }

        private void LoadLastScanRanges()
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
                var savedRanges = _networkScanStore.ReadLastScanRanges();
                if (!string.IsNullOrWhiteSpace(savedRanges))
                {
                    NetworkScanRangesTextBox.Text = savedRanges;
                    return;
                }
            }
            catch
            {
            }

            // Fallback: NIC1-basiertes Prefill
            PrefillNetworkScanRangesFromNic1();
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
                errorMessage = T("NETSCAN_ERROR_ENTER_VALID_RANGE");
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
                errorMessage = T("NETSCAN_ERROR_NO_IPS");
                return false;
            }

            int maxScanHosts = _settingsService.GetNetworkScanMaxHosts();
            if (uniqueIps.Count > maxScanHosts)
            {
                errorMessage = string.Format(T("NETSCAN_ERROR_HOST_LIMIT"), maxScanHosts);
                return false;
            }

            ipAddresses = uniqueIps.OrderBy(v => v).Select(UintToIp).ToList();
            return true;
        }

        private bool TryParseSingleNetworkRange(string rangeText, out uint startIp, out uint endIp, out string? errorMessage)
        {
            startIp = 0;
            endIp = 0;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(rangeText))
            {
                errorMessage = T("NETSCAN_ERROR_EMPTY_RANGE");
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

                errorMessage = $"{T("NETSCAN_ERROR_INVALID_IP")}: {rangeText}";
                return false;
            }

            var parts = normalized.Split('-', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                errorMessage = $"{T("NETSCAN_ERROR_INVALID_RANGE_VALUE")}: {rangeText}";
                return false;
            }

            if (!TryParseIpv4ToUint(parts[0], out var parsedStartIp))
            {
                errorMessage = $"{T("NETSCAN_ERROR_INVALID_START_IP")}: {parts[0]}";
                return false;
            }

            uint parsedEndIp;
            if (parts[1].Contains('.'))
            {
                if (!TryParseIpv4ToUint(parts[1], out parsedEndIp))
                {
                    errorMessage = $"{T("NETSCAN_ERROR_INVALID_END_IP")}: {parts[1]}";
                    return false;
                }
            }
            else
            {
                var firstIpBytes = UintToIp(parsedStartIp).GetAddressBytes();
                if (!byte.TryParse(parts[1], out var lastOctet))
                {
                    errorMessage = $"{T("NETSCAN_ERROR_INVALID_END_RANGE")}: {parts[1]}";
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
                errorMessage = $"{T("NETSCAN_ERROR_START_GREATER_THAN_END")}: {rangeText}";
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

                    var macTask = Task.Run(() => GetMacAddressAsync(ipAddress.ToString()), cancellationToken);
                    var portsTask = Task.Run(() => ScanPortsAsync(ipAddress, cancellationToken), cancellationToken);

                    await Task.WhenAll(macTask, portsTask);

                    device.MacAddress = macTask.Result;
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
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "arp",
                    Arguments = $"-a {ipAddress}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    return T("NETSCAN_VALUE_OTHER_SUBNET");
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(ipAddress))
                    {
                        var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var part in parts)
                        {
                            if (part.Length == 17 && part.Count(c => c == '-') == 5)
                            {
                                return part.ToUpperInvariant();
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return T("NETSCAN_VALUE_OTHER_SUBNET");
        }

        private async Task<List<string>> ScanPortsAsync(IPAddress ipAddress, CancellationToken cancellationToken)
        {
            var openPorts = new List<string>();
            var portsToScan = new Dictionary<int, string>();

            if (_settingsService.GetScanPortHttp()) portsToScan[80] = "HTTP";
            if (_settingsService.GetScanPortHttps()) portsToScan[443] = "HTTPS";
            if (_settingsService.GetScanPortFtp()) portsToScan[21] = "FTP";
            if (_settingsService.GetScanPortSsh()) portsToScan[22] = "SSH";
            if (_settingsService.GetScanPortSmb()) portsToScan[445] = "SMB";
            if (_settingsService.GetScanPortRdp()) portsToScan[3389] = "RDP";

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
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                using var connectArgs = new SocketAsyncEventArgs
                {
                    RemoteEndPoint = new IPEndPoint(ipAddress, port)
                };

                var completion = new TaskCompletionSource<SocketError>(TaskCreationOptions.RunContinuationsAsynchronously);
                connectArgs.Completed += (_, args) => completion.TrySetResult(args.SocketError);

                if (!socket.ConnectAsync(connectArgs))
                {
                    completion.TrySetResult(connectArgs.SocketError);
                }

                var completedTask = await Task.WhenAny(completion.Task, Task.Delay(1000, cancellationToken));
                if (completedTask != completion.Task)
                {
                    return null;
                }

                if (await completion.Task == SocketError.Success)
                {
                    return portName;
                }
            }
            catch (OperationCanceledException)
            {
                // User requested cancellation.
            }
            catch
            {
            }

            return null;
        }

        private void NetworkScanResultsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NetworkScanResultsListView.SelectedItem is NetworkDevice selectedDevice)
            {
                NetworkScanDetailsPanel.Visibility = Visibility.Visible;
                NetworkScanDetailsPlaceholderText.Visibility = Visibility.Collapsed;
                ApplyDeviceDetailsToPanel(selectedDevice);
                _ = LoadDeviceDetailsForSelectionAsync(selectedDevice);
            }
            else
            {
                _detailsLoadCts?.Cancel();
                NetworkScanDetailsPanel.Visibility = Visibility.Collapsed;
                NetworkScanDetailsPlaceholderText.Visibility = Visibility.Visible;
                NetworkScanDetailsPlaceholderText.Text = T("NETSCAN_DETAILS_PLACEHOLDER");
            }
        }

        private void ApplyDeviceDetailsToPanel(NetworkDevice device)
        {
            DeviceDetailsIpTextBlock.Text = device.IpAddress;

            var detailsLoaded = _detailsLoadedIps.Contains(device.IpAddress);
            DeviceDetailsMacTextBlock.Text = device.MacAddress;
            DeviceDetailsHostnameTextBlock.Text = detailsLoaded ? device.Hostname : T("NETSCAN_VALUE_LOADING");

            if (device.OpenPorts != null && device.OpenPorts.Count > 0)
            {
                DeviceDetailsPortsPanel.Visibility = Visibility.Visible;
                DeviceDetailsPortsListView.ItemsSource = device.OpenPorts;
            }
            else
            {
                DeviceDetailsPortsPanel.Visibility = Visibility.Collapsed;
                DeviceDetailsPortsListView.ItemsSource = null;
            }
        }

        private async Task LoadDeviceDetailsForSelectionAsync(NetworkDevice selectedDevice)
        {
            if (string.IsNullOrWhiteSpace(selectedDevice.IpAddress))
            {
                return;
            }

            if (_detailsLoadedIps.Contains(selectedDevice.IpAddress) || _detailsLoadingIps.Contains(selectedDevice.IpAddress))
            {
                return;
            }

            if (!IPAddress.TryParse(selectedDevice.IpAddress, out var ipAddress) || ipAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                return;
            }

            _detailsLoadCts?.Cancel();
            _detailsLoadCts = new CancellationTokenSource();
            var cancellationToken = _detailsLoadCts.Token;

            _detailsLoadingIps.Add(selectedDevice.IpAddress);

            try
            {
                var hostnameTask = GetHostnameAsync(ipAddress, cancellationToken);
                await hostnameTask;
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var loadedHostname = hostnameTask.Result;

                DispatcherQueue.TryEnqueue(() =>
                {
                    selectedDevice.Hostname = loadedHostname;

                    _detailsLoadedIps.Add(selectedDevice.IpAddress);

                    if (NetworkScanResultsListView.SelectedItem is NetworkDevice currentSelection
                        && string.Equals(currentSelection.IpAddress, selectedDevice.IpAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        ApplyDeviceDetailsToPanel(selectedDevice);
                    }
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
            finally
            {
                _detailsLoadingIps.Remove(selectedDevice.IpAddress);
            }
        }

        private async Task<string> GetHostnameAsync(IPAddress ipAddress, CancellationToken cancellationToken)
        {
            try
            {
                var lookupTask = Dns.GetHostEntryAsync(ipAddress);
                var completedTask = await Task.WhenAny(lookupTask, Task.Delay(4000, cancellationToken));
                if (completedTask != lookupTask)
                {
                    return await GetHostnameViaNslookupAsync(ipAddress, cancellationToken);
                }

                var hostEntry = await lookupTask;
                var hostName = string.IsNullOrWhiteSpace(hostEntry.HostName) ? "-" : hostEntry.HostName.TrimEnd('.');
                if (!string.Equals(hostName, "-", StringComparison.Ordinal))
                {
                    return hostName;
                }

                return await GetHostnameViaNslookupAsync(ipAddress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return "-";
            }
            catch
            {
                return await GetHostnameViaNslookupAsync(ipAddress, cancellationToken);
            }
        }

        private async Task<string> GetHostnameViaNslookupAsync(IPAddress ipAddress, CancellationToken cancellationToken)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "nslookup",
                    Arguments = ipAddress.ToString(),
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                if (process == null)
                {
                    return "-";
                }

                var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var waitTask = process.WaitForExitAsync(cancellationToken);
                await Task.WhenAll(outputTask, waitTask);

                var output = outputTask.Result;
                if (string.IsNullOrWhiteSpace(output))
                {
                    return "-";
                }

                foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var line = rawLine.Trim();
                    if (line.Length == 0)
                    {
                        continue;
                    }

                    var colonMatch = Regex.Match(line, "^name\\s*:\\s*(.+)$", RegexOptions.IgnoreCase);
                    if (colonMatch.Success)
                    {
                        return colonMatch.Groups[1].Value.Trim().TrimEnd('.');
                    }

                    var equalsMatch = Regex.Match(line, "\\bname\\s*=\\s*(.+)$", RegexOptions.IgnoreCase);
                    if (equalsMatch.Success)
                    {
                        return equalsMatch.Groups[1].Value.Trim().TrimEnd('.');
                    }
                }
            }
            catch
            {
            }

            return "-";
        }

        private void ShowScanError(string message)
        {
            NetworkScanErrorBar.Title = T("NETSCAN_ERROR_TITLE");
            NetworkScanErrorBar.Message = message;
            NetworkScanErrorBar.IsOpen = true;
        }

        private void DeviceDetailsPortsListView_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is not ListView portsListView)
            {
                return;
            }

            var portInfo = (e.OriginalSource as FrameworkElement)?.DataContext as string
                ?? portsListView.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(portInfo))
            {
                return;
            }

            var selectedDevice = NetworkScanResultsListView?.SelectedItem as NetworkDevice;
            if (selectedDevice == null || string.IsNullOrWhiteSpace(selectedDevice.IpAddress))
            {
                return;
            }

            OpenPortConnection(selectedDevice.IpAddress, portInfo);
        }

        private void OpenPortConnection(string ipAddress, string portInfo)
        {
            var normalized = portInfo.Trim().ToUpperInvariant();

            try
            {
                var numberText = new string(portInfo.TakeWhile(char.IsDigit).ToArray());
                if (!string.IsNullOrWhiteSpace(numberText) && int.TryParse(numberText, out var numericPort))
                {
                    if (numericPort == 80 || numericPort == 8080 || numericPort == 8000)
                    {
                        Process.Start(new ProcessStartInfo($"http://{ipAddress}:{numericPort}") { UseShellExecute = true });
                        return;
                    }

                    if (numericPort == 443 || numericPort == 8443)
                    {
                        Process.Start(new ProcessStartInfo($"https://{ipAddress}:{numericPort}") { UseShellExecute = true });
                        return;
                    }

                    if (numericPort == 3389)
                    {
                        Process.Start("mstsc.exe", $"/v:{ipAddress}:{numericPort}");
                        return;
                    }
                }

                if (normalized.StartsWith("HTTP"))
                {
                    Process.Start(new ProcessStartInfo($"http://{ipAddress}") { UseShellExecute = true });
                }
                else if (normalized.StartsWith("HTTPS"))
                {
                    Process.Start(new ProcessStartInfo($"https://{ipAddress}") { UseShellExecute = true });
                }
                else if (normalized.StartsWith("RDP"))
                {
                    Process.Start("mstsc.exe", $"/v:{ipAddress}");
                }
                else if (normalized.StartsWith("FTP"))
                {
                    Process.Start(new ProcessStartInfo($"ftp://{ipAddress}") { UseShellExecute = true });
                }
                else if (normalized.StartsWith("SSH"))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo($"ssh://{ipAddress}") { UseShellExecute = true });
                    }
                    catch
                    {
                        ShowScanError($"{T("NETSCAN_ERROR_SSH_HINT")}: ssh {ipAddress}");
                    }
                }
                else if (normalized.StartsWith("SMB"))
                {
                    Process.Start(new ProcessStartInfo($"\\\\{ipAddress}") { UseShellExecute = true });
                }
                else if (normalized.StartsWith("CUSTOM"))
                {
                    var customPortText = new string(portInfo.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrWhiteSpace(customPortText) && int.TryParse(customPortText, out var customPort))
                    {
                        ShowScanError($"{T("NETSCAN_ERROR_CUSTOM_PORT_OPEN")}: {ipAddress}:{customPort}");
                    }
                }
                else
                {
                    ShowScanError($"{T("NETSCAN_ERROR_NO_ACTION")}: {portInfo}");
                }
            }
            catch (Exception ex)
            {
                ShowScanError($"{T("NETSCAN_ERROR_OPEN_CONNECTION")}: {ex.Message}");
            }
        }

        private void NetworkScanHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not string column)
            {
                return;
            }

            if (string.Equals(_networkScanSortColumn, column, StringComparison.OrdinalIgnoreCase))
            {
                _networkScanSortAscending = !_networkScanSortAscending;
            }
            else
            {
                _networkScanSortColumn = column;
                _networkScanSortAscending = true;
            }

            ApplyNetworkScanSorting();
            UpdateNetworkScanHeaderLabels();
        }

        private void UpdateNetworkScanHeaderLabels()
        {
            if (NetworkScanHeaderIp == null || NetworkScanHeaderMac == null || NetworkScanHeaderPorts == null)
            {
                return;
            }

            NetworkScanHeaderIp.Content = GetNetworkScanHeaderLabel(T("NETSCAN_HEADER_IP"), "ip");
            NetworkScanHeaderMac.Content = GetNetworkScanHeaderLabel(T("NETSCAN_HEADER_MAC"), "mac");
            NetworkScanHeaderPorts.Content = GetNetworkScanHeaderLabel(T("NETSCAN_HEADER_PORTS"), "ports");
        }

        private string GetNetworkScanHeaderLabel(string label, string column)
        {
            if (!string.Equals(_networkScanSortColumn, column, StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }

            return _networkScanSortAscending ? $"{label} ▲" : $"{label} ▼";
        }

        private void ApplyNetworkScanSorting()
        {
            if (NetworkDevices.Count <= 1)
            {
                return;
            }

            IEnumerable<NetworkDevice> ordered = _networkScanSortColumn switch
            {
                "ip" => _networkScanSortAscending
                    ? NetworkDevices.OrderBy(d => IPAddress.TryParse(d.IpAddress, out var ip) ? IpToUint(ip) : 0u)
                    : NetworkDevices.OrderByDescending(d => IPAddress.TryParse(d.IpAddress, out var ip) ? IpToUint(ip) : 0u),
                "mac" => _networkScanSortAscending
                    ? NetworkDevices.OrderBy(d => d.MacAddress)
                    : NetworkDevices.OrderByDescending(d => d.MacAddress),
                "ports" => _networkScanSortAscending
                    ? NetworkDevices.OrderBy(d => d.OpenPorts?.Count ?? 0)
                    : NetworkDevices.OrderByDescending(d => d.OpenPorts?.Count ?? 0),
                _ => _networkScanSortAscending
                    ? NetworkDevices.OrderBy(d => IPAddress.TryParse(d.IpAddress, out var ip) ? IpToUint(ip) : 0u)
                    : NetworkDevices.OrderByDescending(d => IPAddress.TryParse(d.IpAddress, out var ip) ? IpToUint(ip) : 0u)
            };

            var selected = NetworkScanResultsListView?.SelectedItem as NetworkDevice;
            var sorted = ordered.ToList();

            NetworkDevices.Clear();
            foreach (var device in sorted)
            {
                NetworkDevices.Add(device);
            }

            if (selected != null)
            {
                var selectedAfterSort = NetworkDevices.FirstOrDefault(d =>
                    string.Equals(d.IpAddress, selected.IpAddress, StringComparison.OrdinalIgnoreCase));

                if (selectedAfterSort != null && NetworkScanResultsListView != null)
                {
                    NetworkScanResultsListView.SelectedItem = selectedAfterSort;
                }
            }
        }

        private static uint IpToUint(IPAddress address)
        {
            var bytes = address.GetAddressBytes();
            if (bytes.Length != 4)
            {
                throw new ArgumentException("Nur IPv4 wird unterstützt", nameof(address));
            }

            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static IPAddress UintToIp(uint value)
        {
            return new IPAddress(new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            });
        }
    }
}
