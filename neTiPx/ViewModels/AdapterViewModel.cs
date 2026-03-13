using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TimersTimer = System.Timers.Timer;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using Windows.UI.Xaml;

namespace neTiPx.ViewModels
{
    public sealed class AdapterViewModel : ObservableObject
    {
        private readonly AdapterStore _adapterStore = new AdapterStore();
        private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly TimersTimer? _pingTimer;
        private readonly SynchronizationContext? _uiContext;
        private readonly string _debugInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private long _debugTickCounter;
        private bool _isMonitoringActive;
        private string? _selectedAdapterPrimary;
        private string? _selectedAdapterSecondary;

        // Primary Adapter Properties
        private string? _primaryAdapterName;
        private string? _primaryAdapterMac;
        private string? _primaryAdapterGateway;
        private string? _primaryAdapterGateway6;
        private Visibility _isPrimaryAdapterSelected = Visibility.Collapsed;
        private bool _isPrimaryIpv6Available;

        // Secondary Adapter Properties
        private string? _secondaryAdapterName;
        private string? _secondaryAdapterMac;
        private string? _secondaryAdapterGateway;
        private string? _secondaryAdapterGateway6;
        private Visibility _isSecondaryAdapterSelected = Visibility.Collapsed;
        private bool _isSecondaryIpv6Available;

        // Connection Status Properties
        private string _gatewayStatusText = "Unbekannt";
        private string _gatewayPingText = "Ping: -";
        private GatewayStatusKind _gatewayStatusKind = GatewayStatusKind.Unknown;
        private string _dns1StatusText = "Unbekannt";
        private string _dns1PingText = "Ping: -";
        private GatewayStatusKind _dns1StatusKind = GatewayStatusKind.Unknown;
        private string _dns2StatusText = "Unbekannt";
        private string _dns2PingText = "Ping: -";
        private GatewayStatusKind _dns2StatusKind = GatewayStatusKind.Unknown;
        private string _secondaryGatewayStatusText = "Unbekannt";
        private string _secondaryGatewayPingText = "Ping: -";
        private GatewayStatusKind _secondaryGatewayStatusKind = GatewayStatusKind.Unknown;
        private string _secondaryDns1StatusText = "Unbekannt";
        private string _secondaryDns1PingText = "Ping: -";
        private GatewayStatusKind _secondaryDns1StatusKind = GatewayStatusKind.Unknown;
        private string _secondaryDns2StatusText = "Unbekannt";
        private string _secondaryDns2PingText = "Ping: -";
        private GatewayStatusKind _secondaryDns2StatusKind = GatewayStatusKind.Unknown;

        public AdapterViewModel()
        {
            // Initialize collections for Primary Adapter
            PrimaryAdapterIpV4List = new ObservableCollection<string>();
            PrimaryAdapterDns4List = new ObservableCollection<string>();
            PrimaryAdapterIpV6List = new ObservableCollection<string>();
            PrimaryAdapterDns6List = new ObservableCollection<string>();

            // Initialize collections for Secondary Adapter
            SecondaryAdapterIpV4List = new ObservableCollection<string>();
            SecondaryAdapterDns4List = new ObservableCollection<string>();
            SecondaryAdapterIpV6List = new ObservableCollection<string>();
            SecondaryAdapterDns6List = new ObservableCollection<string>();

            LoadSelectionFromConfig();

            _uiContext = SynchronizationContext.Current;
            try
            {
                _pingTimer = new TimersTimer(System.TimeSpan.FromSeconds(5))
                {
                    AutoReset = true
                };
                _pingTimer.Elapsed += async (_, _) => await UpdateStatusAsync();
            }
            catch
            {
                // Fehler beim Erstellen des Timers
            }
        }

        // Primary Adapter Collections
        public ObservableCollection<string> PrimaryAdapterIpV4List { get; }
        public ObservableCollection<string> PrimaryAdapterDns4List { get; }
        public ObservableCollection<string> PrimaryAdapterIpV6List { get; }
        public ObservableCollection<string> PrimaryAdapterDns6List { get; }

        // Secondary Adapter Collections
        public ObservableCollection<string> SecondaryAdapterIpV4List { get; }
        public ObservableCollection<string> SecondaryAdapterDns4List { get; }
        public ObservableCollection<string> SecondaryAdapterIpV6List { get; }
        public ObservableCollection<string> SecondaryAdapterDns6List { get; }

        public string? SelectedAdapterPrimary
        {
            get => _selectedAdapterPrimary;
            private set
            {
                if (SetProperty(ref _selectedAdapterPrimary, value))
                {
                    UpdatePrimaryAdapterInfo();
                    OnPropertyChanged(nameof(IsPrimaryAdapterSelected));
                }
            }
        }

        public string? SelectedAdapterSecondary
        {
            get => _selectedAdapterSecondary;
            private set
            {
                if (SetProperty(ref _selectedAdapterSecondary, value))
                {
                    UpdateSecondaryAdapterInfo();
                    OnPropertyChanged(nameof(IsSecondaryAdapterSelected));
                }
            }
        }

        public Visibility IsPrimaryAdapterSelected
        {
            get => _isPrimaryAdapterSelected;
            set => SetProperty(ref _isPrimaryAdapterSelected, value);
        }

        public bool IsPrimaryIpv6Available
        {
            get => _isPrimaryIpv6Available;
            set => SetProperty(ref _isPrimaryIpv6Available, value);
        }

        public Visibility IsSecondaryAdapterSelected
        {
            get => _isSecondaryAdapterSelected;
            set => SetProperty(ref _isSecondaryAdapterSelected, value);
        }

        public bool IsSecondaryIpv6Available
        {
            get => _isSecondaryIpv6Available;
            set => SetProperty(ref _isSecondaryIpv6Available, value);
        }

        // Primary Adapter Info Properties
        public string? PrimaryAdapterName
        {
            get => _primaryAdapterName;
            set => SetProperty(ref _primaryAdapterName, value);
        }

        public string? PrimaryAdapterMac
        {
            get => _primaryAdapterMac;
            set => SetProperty(ref _primaryAdapterMac, value);
        }

        public string? PrimaryAdapterGateway
        {
            get => _primaryAdapterGateway;
            set => SetProperty(ref _primaryAdapterGateway, value);
        }

        public string? PrimaryAdapterGateway6
        {
            get => _primaryAdapterGateway6;
            set => SetProperty(ref _primaryAdapterGateway6, value);
        }

        // Secondary Adapter Info Properties
        public string? SecondaryAdapterName
        {
            get => _secondaryAdapterName;
            set => SetProperty(ref _secondaryAdapterName, value);
        }

        public string? SecondaryAdapterMac
        {
            get => _secondaryAdapterMac;
            set => SetProperty(ref _secondaryAdapterMac, value);
        }

        public string? SecondaryAdapterGateway
        {
            get => _secondaryAdapterGateway;
            set => SetProperty(ref _secondaryAdapterGateway, value);
        }

        public string? SecondaryAdapterGateway6
        {
            get => _secondaryAdapterGateway6;
            set => SetProperty(ref _secondaryAdapterGateway6, value);
        }

        // Connection Status Properties
        public string GatewayStatusText
        {
            get => _gatewayStatusText;
            set => SetProperty(ref _gatewayStatusText, value);
        }

        public string GatewayPingText
        {
            get => _gatewayPingText;
            set => SetProperty(ref _gatewayPingText, value);
        }

        public GatewayStatusKind GatewayStatusKind
        {
            get => _gatewayStatusKind;
            set => SetProperty(ref _gatewayStatusKind, value);
        }

        public string Dns1StatusText
        {
            get => _dns1StatusText;
            set => SetProperty(ref _dns1StatusText, value);
        }

        public string Dns1PingText
        {
            get => _dns1PingText;
            set => SetProperty(ref _dns1PingText, value);
        }

        public GatewayStatusKind Dns1StatusKind
        {
            get => _dns1StatusKind;
            set => SetProperty(ref _dns1StatusKind, value);
        }

        public string Dns2StatusText
        {
            get => _dns2StatusText;
            set => SetProperty(ref _dns2StatusText, value);
        }

        public string Dns2PingText
        {
            get => _dns2PingText;
            set => SetProperty(ref _dns2PingText, value);
        }

        public GatewayStatusKind Dns2StatusKind
        {
            get => _dns2StatusKind;
            set => SetProperty(ref _dns2StatusKind, value);
        }

        public string SecondaryGatewayStatusText
        {
            get => _secondaryGatewayStatusText;
            set => SetProperty(ref _secondaryGatewayStatusText, value);
        }

        public string SecondaryGatewayPingText
        {
            get => _secondaryGatewayPingText;
            set => SetProperty(ref _secondaryGatewayPingText, value);
        }

        public GatewayStatusKind SecondaryGatewayStatusKind
        {
            get => _secondaryGatewayStatusKind;
            set => SetProperty(ref _secondaryGatewayStatusKind, value);
        }

        public string SecondaryDns1StatusText
        {
            get => _secondaryDns1StatusText;
            set => SetProperty(ref _secondaryDns1StatusText, value);
        }

        public string SecondaryDns1PingText
        {
            get => _secondaryDns1PingText;
            set => SetProperty(ref _secondaryDns1PingText, value);
        }

        public GatewayStatusKind SecondaryDns1StatusKind
        {
            get => _secondaryDns1StatusKind;
            set => SetProperty(ref _secondaryDns1StatusKind, value);
        }

        public string SecondaryDns2StatusText
        {
            get => _secondaryDns2StatusText;
            set => SetProperty(ref _secondaryDns2StatusText, value);
        }

        public string SecondaryDns2PingText
        {
            get => _secondaryDns2PingText;
            set => SetProperty(ref _secondaryDns2PingText, value);
        }

        public GatewayStatusKind SecondaryDns2StatusKind
        {
            get => _secondaryDns2StatusKind;
            set => SetProperty(ref _secondaryDns2StatusKind, value);
        }

        public bool ShowGatewayStatus => _settingsService.GetCheckConnectionGateway();
        public bool ShowDns1Status => _settingsService.GetCheckConnectionDns1();
        public bool ShowDns2Status => _settingsService.GetCheckConnectionDns2();

        public bool ShowConnectionQualityIndicator =>
            ShowGatewayStatus || ShowDns1Status || ShowDns2Status;

        public string PrimaryDns1Address
        {
            get
            {
                try
                {
                    return PrimaryAdapterDns4List?.Count > 0 ? PrimaryAdapterDns4List[0] : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string PrimaryDns2Address
        {
            get
            {
                try
                {
                    return PrimaryAdapterDns4List?.Count > 1 ? PrimaryAdapterDns4List[1] : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string SecondaryDns1Address
        {
            get
            {
                try
                {
                    return SecondaryAdapterDns4List?.Count > 0 ? SecondaryAdapterDns4List[0] : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string SecondaryDns2Address
        {
            get
            {
                try
                {
                    return SecondaryAdapterDns4List?.Count > 1 ? SecondaryAdapterDns4List[1] : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        private void LoadSelectionFromConfig()
        {
            var adapterSettings = _adapterStore.ReadAdapters();
            SelectedAdapterPrimary = adapterSettings.PrimaryAdapter;
            SelectedAdapterSecondary = adapterSettings.SecondaryAdapter;
        }

        private void UpdatePrimaryAdapterInfo()
        {
            if (string.IsNullOrEmpty(SelectedAdapterPrimary))
            {
                IsPrimaryAdapterSelected = Visibility.Collapsed;
                IsPrimaryIpv6Available = false;
                ClearPrimaryAdapterInfo();
                return;
            }

            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name == SelectedAdapterPrimary);

            if (adapter != null)
            {
                IsPrimaryAdapterSelected = Visibility.Visible;
                PrimaryAdapterName = adapter.Name;
                PrimaryAdapterMac = adapter.GetPhysicalAddress().ToString();

                var ipProperties = adapter.GetIPProperties();

                // Clear collections
                PrimaryAdapterIpV4List.Clear();
                PrimaryAdapterDns4List.Clear();
                PrimaryAdapterIpV6List.Clear();
                PrimaryAdapterDns6List.Clear();

                // Add all IPv4 addresses
                var ipv4Addresses = ipProperties.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var addr in ipv4Addresses)
                {
                    PrimaryAdapterIpV4List.Add(addr.Address.ToString());
                }
                if (PrimaryAdapterIpV4List.Count == 0)
                {
                    PrimaryAdapterIpV4List.Add("Keine IPv4 konfiguriert");
                }

                // Add gateway IPv4
                var gateway4 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                PrimaryAdapterGateway = gateway4?.Address.ToString() ?? "Keine Gateway konfiguriert";

                // Add all DNS4 addresses
                var dns4Addresses = ipProperties.DnsAddresses
                    .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var addr in dns4Addresses)
                {
                    PrimaryAdapterDns4List.Add(addr.ToString());
                }
                if (PrimaryAdapterDns4List.Count == 0)
                {
                    PrimaryAdapterDns4List.Add("Keine DNS konfiguriert");
                }

                // Notify DNS properties changed
                OnPropertyChanged(nameof(PrimaryDns1Address));
                OnPropertyChanged(nameof(PrimaryDns2Address));

                // Add all IPv6 addresses
                var ipv6Addresses = ipProperties.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        && !a.Address.IsIPv6LinkLocal);
                foreach (var addr in ipv6Addresses)
                {
                    PrimaryAdapterIpV6List.Add(addr.Address.ToString());
                }
                var hasIpv6 = PrimaryAdapterIpV6List.Count > 0;
                IsPrimaryIpv6Available = hasIpv6;
                if (!hasIpv6)
                {
                    PrimaryAdapterIpV6List.Add("Keine IPv6 konfiguriert");
                }

                // Add gateway IPv6
                var gateway6 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
                PrimaryAdapterGateway6 = hasIpv6
                    ? gateway6?.Address.ToString() ?? "Keine Gateway konfiguriert"
                    : "Keine IPv6 konfiguriert";

                // Add all DNS6 addresses
                var dns6Addresses = ipProperties.DnsAddresses
                    .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        && !d.IsIPv6SiteLocal);
                foreach (var addr in dns6Addresses)
                {
                    PrimaryAdapterDns6List.Add(addr.ToString());
                }
                if (PrimaryAdapterDns6List.Count == 0)
                {
                    PrimaryAdapterDns6List.Add(hasIpv6 ? "Keine DNS konfiguriert" : "Keine IPv6 konfiguriert");
                }
                // Update connection status after loading adapter info
                UpdateStatusAsync().ConfigureAwait(false);            }
            else
            {
                IsPrimaryAdapterSelected = Visibility.Collapsed;
                IsPrimaryIpv6Available = false;
                ClearPrimaryAdapterInfo();
            }
        }

        private void UpdateSecondaryAdapterInfo()
        {
            if (string.IsNullOrEmpty(SelectedAdapterSecondary))
            {
                IsSecondaryAdapterSelected = Visibility.Collapsed;
                IsSecondaryIpv6Available = false;
                ClearSecondaryAdapterInfo();
                return;
            }

            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name == SelectedAdapterSecondary);

            if (adapter != null)
            {
                IsSecondaryAdapterSelected = Visibility.Visible;
                SecondaryAdapterName = adapter.Name;
                SecondaryAdapterMac = adapter.GetPhysicalAddress().ToString();

                var ipProperties = adapter.GetIPProperties();

                // Clear collections
                SecondaryAdapterIpV4List.Clear();
                SecondaryAdapterDns4List.Clear();
                SecondaryAdapterIpV6List.Clear();
                SecondaryAdapterDns6List.Clear();

                // Add all IPv4 addresses
                var ipv4Addresses = ipProperties.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var addr in ipv4Addresses)
                {
                    SecondaryAdapterIpV4List.Add(addr.Address.ToString());
                }
                if (SecondaryAdapterIpV4List.Count == 0)
                {
                    SecondaryAdapterIpV4List.Add("Keine IPv4 konfiguriert");
                }

                // Add gateway IPv4
                var gateway4 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                SecondaryAdapterGateway = gateway4?.Address.ToString() ?? "Keine Gateway konfiguriert";

                // Add all DNS4 addresses
                var dns4Addresses = ipProperties.DnsAddresses
                    .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var addr in dns4Addresses)
                {
                    SecondaryAdapterDns4List.Add(addr.ToString());
                }
                if (SecondaryAdapterDns4List.Count == 0)
                {
                    SecondaryAdapterDns4List.Add("Keine DNS konfiguriert");
                }

                OnPropertyChanged(nameof(SecondaryDns1Address));
                OnPropertyChanged(nameof(SecondaryDns2Address));

                // Add all IPv6 addresses
                var ipv6Addresses = ipProperties.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        && !a.Address.IsIPv6LinkLocal);
                foreach (var addr in ipv6Addresses)
                {
                    SecondaryAdapterIpV6List.Add(addr.Address.ToString());
                }
                var hasIpv6 = SecondaryAdapterIpV6List.Count > 0;
                IsSecondaryIpv6Available = hasIpv6;
                if (!hasIpv6)
                {
                    SecondaryAdapterIpV6List.Add("Keine IPv6 konfiguriert");
                }

                // Add gateway IPv6
                var gateway6 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
                SecondaryAdapterGateway6 = hasIpv6
                    ? gateway6?.Address.ToString() ?? "Keine Gateway konfiguriert"
                    : "Keine IPv6 konfiguriert";

                // Add all DNS6 addresses
                var dns6Addresses = ipProperties.DnsAddresses
                    .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        && !d.IsIPv6SiteLocal);
                foreach (var addr in dns6Addresses)
                {
                    SecondaryAdapterDns6List.Add(addr.ToString());
                }
                if (SecondaryAdapterDns6List.Count == 0)
                {
                    SecondaryAdapterDns6List.Add(hasIpv6 ? "Keine DNS konfiguriert" : "Keine IPv6 konfiguriert");
                }

                UpdateStatusAsync().ConfigureAwait(false);
            }
            else
            {
                IsSecondaryAdapterSelected = Visibility.Collapsed;
                IsSecondaryIpv6Available = false;
                ClearSecondaryAdapterInfo();
            }
        }

        private void ClearPrimaryAdapterInfo()
        {
            PrimaryAdapterName = null;
            PrimaryAdapterMac = null;
            PrimaryAdapterGateway = null;
            PrimaryAdapterGateway6 = null;
            IsPrimaryIpv6Available = false;

            PrimaryAdapterIpV4List.Clear();
            PrimaryAdapterDns4List.Clear();
            PrimaryAdapterIpV6List.Clear();
            PrimaryAdapterDns6List.Clear();

            OnPropertyChanged(nameof(PrimaryDns1Address));
            OnPropertyChanged(nameof(PrimaryDns2Address));
        }

        private void ClearSecondaryAdapterInfo()
        {
            SecondaryAdapterName = null;
            SecondaryAdapterMac = null;
            SecondaryAdapterGateway = null;
            SecondaryAdapterGateway6 = null;
            IsSecondaryIpv6Available = false;

            SecondaryAdapterIpV4List.Clear();
            SecondaryAdapterDns4List.Clear();
            SecondaryAdapterIpV6List.Clear();
            SecondaryAdapterDns6List.Clear();

            OnPropertyChanged(nameof(SecondaryDns1Address));
            OnPropertyChanged(nameof(SecondaryDns2Address));
        }

        private async Task UpdateStatusAsync()
        {
            try
            {
                var tick = Interlocked.Increment(ref _debugTickCounter);
                DebugLog($"Tick#{tick} start timerEnabled={_pingTimer?.Enabled} monitoringActive={_isMonitoringActive} primary='{SelectedAdapterPrimary ?? "-"}' visible={IsPrimaryAdapterSelected}");

                if (!_isMonitoringActive)
                {
                    DebugLog($"Tick#{tick} skipped because monitoring is inactive");
                    return;
                }

                await UpdateAdapterStatusAsync(
                    adapterKey: "NIC1",
                    isVisible: IsPrimaryAdapterSelected == Visibility.Visible,
                    gatewayAddress: PrimaryAdapterGateway,
                    dnsAddresses: PrimaryAdapterDns4List?.ToList(),
                    postGateway: PostGatewayStatus,
                    postDns1: PostDns1Status,
                    postDns2: PostDns2Status);

                await UpdateAdapterStatusAsync(
                    adapterKey: "NIC2",
                    isVisible: IsSecondaryAdapterSelected == Visibility.Visible,
                    gatewayAddress: SecondaryAdapterGateway,
                    dnsAddresses: SecondaryAdapterDns4List?.ToList(),
                    postGateway: PostSecondaryGatewayStatus,
                    postDns1: PostSecondaryDns1Status,
                    postDns2: PostSecondaryDns2Status);
            }
            catch
            {
                // Fehler beim Status Check - ignorieren
            }
        }

        private async Task UpdateAdapterStatusAsync(
            string adapterKey,
            bool isVisible,
            string? gatewayAddress,
            System.Collections.Generic.List<string>? dnsAddresses,
            System.Action<string, string, GatewayStatusKind> postGateway,
            System.Action<string, string, GatewayStatusKind> postDns1,
            System.Action<string, string, GatewayStatusKind> postDns2)
        {
            if (!isVisible)
            {
                postGateway("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                postDns1("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                postDns2("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            var gateway = NormalizeHostAddress(gatewayAddress);
            var dns1 = NormalizeHostAddress(dnsAddresses != null && dnsAddresses.Count > 0 ? dnsAddresses[0] : string.Empty);
            var dns2 = NormalizeHostAddress(dnsAddresses != null && dnsAddresses.Count > 1 ? dnsAddresses[1] : string.Empty);

            var checkGateway = _settingsService.GetCheckConnectionGateway();
            var checkDns1 = _settingsService.GetCheckConnectionDns1();
            var checkDns2 = _settingsService.GetCheckConnectionDns2();
            DebugLog($"{adapterKey} targets gw={gateway} dns1={dns1} dns2={dns2} checks gw={checkGateway} dns1={checkDns1} dns2={checkDns2}");

            if (checkGateway)
            {
                await CheckHostStatusAsync(gateway, adapterKey + "-Gateway", (status, ping, kind) => postGateway(status, ping, kind));
            }
            else
            {
                postGateway("Deaktiviert", "Ping: -", GatewayStatusKind.Unknown);
            }

            if (checkDns1)
            {
                await CheckHostStatusAsync(dns1, adapterKey + "-DNS1", (status, ping, kind) => postDns1(status, ping, kind));
            }
            else
            {
                postDns1("Deaktiviert", "Ping: -", GatewayStatusKind.Unknown);
            }

            if (checkDns2)
            {
                await CheckHostStatusAsync(dns2, adapterKey + "-DNS2", (status, ping, kind) => postDns2(status, ping, kind));
            }
            else
            {
                postDns2("Deaktiviert", "Ping: -", GatewayStatusKind.Unknown);
            }
        }

        private static string NormalizeHostAddress(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return string.Empty;
            }

            var candidate = address.Trim();

            if (candidate.Contains(','))
            {
                candidate = candidate.Split(',')[0].Trim();
            }
            else if (candidate.Contains(';'))
            {
                candidate = candidate.Split(';')[0].Trim();
            }
            else if (candidate.Contains(' '))
            {
                candidate = candidate.Split(' ', System.StringSplitOptions.RemoveEmptyEntries)[0].Trim();
            }

            return candidate;
        }

        private async Task CheckHostStatusAsync(string address, string targetKind, System.Action<string, string, GatewayStatusKind> callback)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                DebugLog($"Skip {targetKind}: address empty");
                callback("Nicht konfiguriert", "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            try
            {
                DebugLog($"Ping start {targetKind} -> {address}");
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(address, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    var ms = reply.RoundtripTime;

                    // Hole die Schwellwerte aus den Einstellungen
                    int thresholdFast = _settingsService.GetPingThresholdFast();
                    int thresholdNormal = _settingsService.GetPingThresholdNormal();

                    var statusText = ms <= thresholdFast ? "Erreichbar" : ms <= thresholdNormal ? "Langsam" : "Sehr langsam";
                    var statusKind = ms <= thresholdFast ? GatewayStatusKind.Good :
                                   ms <= thresholdNormal ? GatewayStatusKind.Warning : GatewayStatusKind.Bad;
                    DebugLog($"Ping ok {targetKind} -> {address} ({ms} ms)");
                    callback(statusText, $"Ping: {ms} ms", statusKind);
                }
                else
                {
                    DebugLog($"Ping no-reply {targetKind} -> {address} status={reply.Status}");
                    callback("Nicht erreichbar", "Ping: timeout", GatewayStatusKind.Bad);
                }
            }
            catch (PingException)
            {
                DebugLog($"Ping exception {targetKind} -> {address}");
                callback("Nicht erreichbar", "Ping: fehlgeschlagen", GatewayStatusKind.Bad);
            }
            catch
            {
                DebugLog($"Ping error {targetKind} -> {address}");
                callback("Fehler", "Ping: Fehler", GatewayStatusKind.Bad);
            }
        }

        private void DebugLog(string message)
        {
            Debug.WriteLine($"[ReachabilityDebug][AdapterPage][VM:{_debugInstanceId}][T:{Environment.CurrentManagedThreadId}] {DateTime.Now:HH:mm:ss.fff} {message}");
        }

        private void PostGatewayStatus(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                GatewayStatusText = statusText;
                GatewayPingText = pingText;
                GatewayStatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                GatewayStatusText = statusText;
                GatewayPingText = pingText;
                GatewayStatusKind = statusKind;
            }, null);
        }

        private void PostDns1Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns1StatusText = statusText;
                Dns1PingText = pingText;
                Dns1StatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns1StatusText = statusText;
                Dns1PingText = pingText;
                Dns1StatusKind = statusKind;
            }, null);
        }

        private void PostDns2Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns2StatusText = statusText;
                Dns2PingText = pingText;
                Dns2StatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns2StatusText = statusText;
                Dns2PingText = pingText;
                Dns2StatusKind = statusKind;
            }, null);
        }

        private void PostSecondaryGatewayStatus(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryGatewayStatusText = statusText;
                SecondaryGatewayPingText = pingText;
                SecondaryGatewayStatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryGatewayStatusText = statusText;
                SecondaryGatewayPingText = pingText;
                SecondaryGatewayStatusKind = statusKind;
            }, null);
        }

        private void PostSecondaryDns1Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryDns1StatusText = statusText;
                SecondaryDns1PingText = pingText;
                SecondaryDns1StatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryDns1StatusText = statusText;
                SecondaryDns1PingText = pingText;
                SecondaryDns1StatusKind = statusKind;
            }, null);
        }

        private void PostSecondaryDns2Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryDns2StatusText = statusText;
                SecondaryDns2PingText = pingText;
                SecondaryDns2StatusKind = statusKind;
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryDns2StatusText = statusText;
                SecondaryDns2PingText = pingText;
                SecondaryDns2StatusKind = statusKind;
            }, null);
        }

            public void StartConnectionMonitoring()
            {
                _isMonitoringActive = true;

                if (_pingTimer != null && !_pingTimer.Enabled)
                {
                    DebugLog("StartConnectionMonitoring");
                    _pingTimer.Start();
                    UpdateStatusAsync().ConfigureAwait(false);
                }
                else
                {
                    DebugLog("StartConnectionMonitoring ignored (timer already active)");
                }
            }

            public void StopConnectionMonitoring()
            {
                _isMonitoringActive = false;

                if (_pingTimer != null && _pingTimer.Enabled)
                {
                    DebugLog("StopConnectionMonitoring");
                    _pingTimer.Stop();
                }
                else
                {
                    DebugLog("StopConnectionMonitoring ignored (timer already stopped)");
                }
            }
    }
}
