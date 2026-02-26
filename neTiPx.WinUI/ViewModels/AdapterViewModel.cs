using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using neTiPx.WinUI.Helpers;
using neTiPx.WinUI.Services;
using Windows.UI.Xaml;

namespace neTiPx.WinUI.ViewModels
{
    public sealed class AdapterViewModel : ObservableObject
    {
        private readonly ConfigStore _configStore = new ConfigStore();
        private readonly NetworkInfoService _networkInfoService = new NetworkInfoService();
        private string? _selectedAdapterPrimary;
        private string? _selectedAdapterSecondary;
        private bool _isLoading;

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

        public AdapterViewModel()
        {
            AdapterList = new ObservableCollection<string>();

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

            LoadAdapters();
            LoadSelectionFromConfig();
        }

        public ObservableCollection<string> AdapterList { get; }

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
            set
            {
                if (SetProperty(ref _selectedAdapterPrimary, value))
                {
                    SaveSelectionToConfig();
                    UpdatePrimaryAdapterInfo();
                    OnPropertyChanged(nameof(IsPrimaryAdapterSelected));
                }
            }
        }

        public string? SelectedAdapterSecondary
        {
            get => _selectedAdapterSecondary;
            set
            {
                if (SetProperty(ref _selectedAdapterSecondary, value))
                {
                    SaveSelectionToConfig();
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

        private void LoadAdapters()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => n.GetPhysicalAddress() != null && n.GetPhysicalAddress().GetAddressBytes().Length > 0)
                .Select(n => n.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            AdapterList.Clear();
            foreach (var adapter in adapters)
            {
                AdapterList.Add(adapter);
            }
        }

        private void LoadSelectionFromConfig()
        {
            _isLoading = true;
            try
            {
                var values = _configStore.ReadAll();
                if (values.TryGetValue("Adapter1", out var a1))
                {
                    SelectedAdapterPrimary = a1;
                }
                if (values.TryGetValue("Adapter2", out var a2))
                {
                    SelectedAdapterSecondary = a2;
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SaveSelectionToConfig()
        {
            if (_isLoading)
            {
                return;
            }

            var values = _configStore.ReadAll();
            values["Adapter1"] = SelectedAdapterPrimary ?? string.Empty;
            values["Adapter2"] = SelectedAdapterSecondary ?? string.Empty;
            _configStore.WriteAll(values);
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
            }
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
        }
    }
}
