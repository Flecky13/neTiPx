using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
using System.Threading;
using System.Threading.Tasks;

namespace neTiPx.Views
{
    public sealed partial class NetworkScannerPage : Page
    {
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly AdapterStore _adapterStore = new AdapterStore();
        private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();
        private readonly NetworkScanStore _networkScanStore = new NetworkScanStore();

        private string _networkScanSortColumn = string.Empty;
        private bool _networkScanSortAscending = true;
        private CancellationTokenSource? _networkScanCts;

        public ObservableCollection<NetworkDevice> NetworkDevices { get; } = new ObservableCollection<NetworkDevice>();

        public NetworkScannerPage()
        {
            InitializeComponent();
            Loaded += NetworkScannerPage_Loaded;
            Unloaded += NetworkScannerPage_Unloaded;
        }

        private void NetworkScannerPage_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateNetworkScanHeaderLabels();
            LoadLastScanRanges();
        }

        private void NetworkScannerPage_Unloaded(object sender, RoutedEventArgs e)
        {
            _networkScanCts?.Cancel();
        }

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

            // Bereiche speichern
            _networkScanStore.WriteLastScanRanges(rangeInput);

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

                NetworkScanCountTextBlock.Text = $"{NetworkDevices.Count} Gerät(e)";
                NetworkScanStatusTextBlock.Text = NetworkDevices.Count > 0
                    ? $"Scan abgeschlossen - {NetworkDevices.Count} Gerät(e) gefunden"
                    : "Scan abgeschlossen - Keine Geräte gefunden";
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
                    return "Anderes Subnetz";
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

            return "Anderes Subnetz";
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
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(ipAddress, port);
                var timeoutTask = Task.Delay(1000, cancellationToken);

                if (await Task.WhenAny(connectTask, timeoutTask) == connectTask && tcpClient.Connected)
                {
                    return portName;
                }
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
                DeviceDetailsIpTextBlock.Text = selectedDevice.IpAddress;
                DeviceDetailsMacTextBlock.Text = selectedDevice.MacAddress;
                DeviceDetailsHostnameTextBlock.Text = selectedDevice.Hostname;

                if (selectedDevice.OpenPorts != null && selectedDevice.OpenPorts.Count > 0)
                {
                    DeviceDetailsPortsPanel.Visibility = Visibility.Visible;
                    DeviceDetailsPortsListView.ItemsSource = selectedDevice.OpenPorts;
                }
                else
                {
                    DeviceDetailsPortsPanel.Visibility = Visibility.Collapsed;
                    DeviceDetailsPortsListView.ItemsSource = null;
                }
            }
            else
            {
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
                        ShowScanError($"SSH-Verbindung: ssh {ipAddress}");
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
                        ShowScanError($"Custom-Port offen: {ipAddress}:{customPort}");
                    }
                }
                else
                {
                    ShowScanError($"Keine Startaktion definiert für: {portInfo}");
                }
            }
            catch (Exception ex)
            {
                ShowScanError($"Fehler beim Öffnen der Verbindung: {ex.Message}");
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

            NetworkScanHeaderIp.Content = GetNetworkScanHeaderLabel("IP-Adresse", "ip");
            NetworkScanHeaderMac.Content = GetNetworkScanHeaderLabel("MAC-Adresse", "mac");
            NetworkScanHeaderPorts.Content = GetNetworkScanHeaderLabel("Open Ports", "ports");
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
