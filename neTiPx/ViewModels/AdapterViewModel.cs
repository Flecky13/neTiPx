using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using TimersTimer = System.Timers.Timer;
using Microsoft.UI.Xaml.Media;
using neTiPx.Helpers;
using neTiPx.Models;
using neTiPx.Services;
using Windows.UI.Xaml;
using Windows.Foundation;

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
        private CancellationTokenSource? _networkChangeCts;
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

        // Adapter Down State
        private bool _isPrimaryAdapterDown;
        private bool _isSecondaryAdapterDown;

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
        private string _gatewayV6StatusText = "Unbekannt";
        private string _gatewayV6PingText = "Ping: -";
        private GatewayStatusKind _gatewayV6StatusKind = GatewayStatusKind.Unknown;
        private string _dns1V6StatusText = "Unbekannt";
        private string _dns1V6PingText = "Ping: -";
        private GatewayStatusKind _dns1V6StatusKind = GatewayStatusKind.Unknown;
        private string _dns2V6StatusText = "Unbekannt";
        private string _dns2V6PingText = "Ping: -";
        private GatewayStatusKind _dns2V6StatusKind = GatewayStatusKind.Unknown;
        private string _secondaryGatewayStatusText = "Unbekannt";
        private string _secondaryGatewayPingText = "Ping: -";
        private GatewayStatusKind _secondaryGatewayStatusKind = GatewayStatusKind.Unknown;
        private string _secondaryDns1StatusText = "Unbekannt";
        private string _secondaryDns1PingText = "Ping: -";
        private GatewayStatusKind _secondaryDns1StatusKind = GatewayStatusKind.Unknown;
        private string _secondaryDns2StatusText = "Unbekannt";
        private string _secondaryDns2PingText = "Ping: -";
        private GatewayStatusKind _secondaryDns2StatusKind = GatewayStatusKind.Unknown;
        private string _secondaryGatewayV6StatusText = "Unbekannt";
        private string _secondaryGatewayV6PingText = "Ping: -";
        private GatewayStatusKind _secondaryGatewayV6StatusKind = GatewayStatusKind.Unknown;
        private string _secondaryDns1V6StatusText = "Unbekannt";
        private string _secondaryDns1V6PingText = "Ping: -";
        private GatewayStatusKind _secondaryDns1V6StatusKind = GatewayStatusKind.Unknown;
        private string _secondaryDns2V6StatusText = "Unbekannt";
        private string _secondaryDns2V6PingText = "Ping: -";
        private GatewayStatusKind _secondaryDns2V6StatusKind = GatewayStatusKind.Unknown;

        private const int TrendSampleCount = 24;
        private const double TrendWidth = 146d;
        private const double TrendHeight = 22d;

        private readonly Queue<double> _gatewayTrendHistory = new Queue<double>();
        private readonly Queue<double> _dns1TrendHistory = new Queue<double>();
        private readonly Queue<double> _dns2TrendHistory = new Queue<double>();
        private readonly Queue<double> _gatewayV6TrendHistory = new Queue<double>();
        private readonly Queue<double> _dns1V6TrendHistory = new Queue<double>();
        private readonly Queue<double> _dns2V6TrendHistory = new Queue<double>();
        private readonly Queue<double> _secondaryGatewayTrendHistory = new Queue<double>();
        private readonly Queue<double> _secondaryDns1TrendHistory = new Queue<double>();
        private readonly Queue<double> _secondaryDns2TrendHistory = new Queue<double>();
        private readonly Queue<double> _secondaryGatewayV6TrendHistory = new Queue<double>();
        private readonly Queue<double> _secondaryDns1V6TrendHistory = new Queue<double>();
        private readonly Queue<double> _secondaryDns2V6TrendHistory = new Queue<double>();

        private bool _gatewayTrendPulse;
        private bool _dns1TrendPulse;
        private bool _dns2TrendPulse;
        private bool _gatewayV6TrendPulse;
        private bool _dns1V6TrendPulse;
        private bool _dns2V6TrendPulse;
        private bool _secondaryGatewayTrendPulse;
        private bool _secondaryDns1TrendPulse;
        private bool _secondaryDns2TrendPulse;
        private bool _secondaryGatewayV6TrendPulse;
        private bool _secondaryDns1V6TrendPulse;
        private bool _secondaryDns2V6TrendPulse;

        private PointCollection _gatewayTrendPoints = CreateFlatTrendPoints();
        private PointCollection _dns1TrendPoints = CreateFlatTrendPoints();
        private PointCollection _dns2TrendPoints = CreateFlatTrendPoints();
        private PointCollection _gatewayV6TrendPoints = CreateFlatTrendPoints();
        private PointCollection _dns1V6TrendPoints = CreateFlatTrendPoints();
        private PointCollection _dns2V6TrendPoints = CreateFlatTrendPoints();
        private PointCollection _secondaryGatewayTrendPoints = CreateFlatTrendPoints();
        private PointCollection _secondaryDns1TrendPoints = CreateFlatTrendPoints();
        private PointCollection _secondaryDns2TrendPoints = CreateFlatTrendPoints();
        private PointCollection _secondaryGatewayV6TrendPoints = CreateFlatTrendPoints();
        private PointCollection _secondaryDns1V6TrendPoints = CreateFlatTrendPoints();
        private PointCollection _secondaryDns2V6TrendPoints = CreateFlatTrendPoints();

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
                _pingTimer = new TimersTimer(System.TimeSpan.FromSeconds(1))
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

        public bool IsPrimaryAdapterDown
        {
            get => _isPrimaryAdapterDown;
            set => SetProperty(ref _isPrimaryAdapterDown, value);
        }

        public bool IsSecondaryAdapterDown
        {
            get => _isSecondaryAdapterDown;
            set => SetProperty(ref _isSecondaryAdapterDown, value);
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

        public string GatewayV6StatusText
        {
            get => _gatewayV6StatusText;
            set => SetProperty(ref _gatewayV6StatusText, value);
        }

        public string GatewayV6PingText
        {
            get => _gatewayV6PingText;
            set => SetProperty(ref _gatewayV6PingText, value);
        }

        public GatewayStatusKind GatewayV6StatusKind
        {
            get => _gatewayV6StatusKind;
            set => SetProperty(ref _gatewayV6StatusKind, value);
        }

        public string Dns1V6StatusText
        {
            get => _dns1V6StatusText;
            set => SetProperty(ref _dns1V6StatusText, value);
        }

        public string Dns1V6PingText
        {
            get => _dns1V6PingText;
            set => SetProperty(ref _dns1V6PingText, value);
        }

        public GatewayStatusKind Dns1V6StatusKind
        {
            get => _dns1V6StatusKind;
            set => SetProperty(ref _dns1V6StatusKind, value);
        }

        public string Dns2V6StatusText
        {
            get => _dns2V6StatusText;
            set => SetProperty(ref _dns2V6StatusText, value);
        }

        public string Dns2V6PingText
        {
            get => _dns2V6PingText;
            set => SetProperty(ref _dns2V6PingText, value);
        }

        public GatewayStatusKind Dns2V6StatusKind
        {
            get => _dns2V6StatusKind;
            set => SetProperty(ref _dns2V6StatusKind, value);
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

        public string SecondaryGatewayV6StatusText
        {
            get => _secondaryGatewayV6StatusText;
            set => SetProperty(ref _secondaryGatewayV6StatusText, value);
        }

        public string SecondaryGatewayV6PingText
        {
            get => _secondaryGatewayV6PingText;
            set => SetProperty(ref _secondaryGatewayV6PingText, value);
        }

        public GatewayStatusKind SecondaryGatewayV6StatusKind
        {
            get => _secondaryGatewayV6StatusKind;
            set => SetProperty(ref _secondaryGatewayV6StatusKind, value);
        }

        public string SecondaryDns1V6StatusText
        {
            get => _secondaryDns1V6StatusText;
            set => SetProperty(ref _secondaryDns1V6StatusText, value);
        }

        public string SecondaryDns1V6PingText
        {
            get => _secondaryDns1V6PingText;
            set => SetProperty(ref _secondaryDns1V6PingText, value);
        }

        public GatewayStatusKind SecondaryDns1V6StatusKind
        {
            get => _secondaryDns1V6StatusKind;
            set => SetProperty(ref _secondaryDns1V6StatusKind, value);
        }

        public string SecondaryDns2V6StatusText
        {
            get => _secondaryDns2V6StatusText;
            set => SetProperty(ref _secondaryDns2V6StatusText, value);
        }

        public string SecondaryDns2V6PingText
        {
            get => _secondaryDns2V6PingText;
            set => SetProperty(ref _secondaryDns2V6PingText, value);
        }

        public GatewayStatusKind SecondaryDns2V6StatusKind
        {
            get => _secondaryDns2V6StatusKind;
            set => SetProperty(ref _secondaryDns2V6StatusKind, value);
        }

        public PointCollection GatewayTrendPoints
        {
            get => _gatewayTrendPoints;
            set => SetProperty(ref _gatewayTrendPoints, value);
        }

        public PointCollection Dns1TrendPoints
        {
            get => _dns1TrendPoints;
            set => SetProperty(ref _dns1TrendPoints, value);
        }

        public PointCollection Dns2TrendPoints
        {
            get => _dns2TrendPoints;
            set => SetProperty(ref _dns2TrendPoints, value);
        }

        public PointCollection GatewayV6TrendPoints
        {
            get => _gatewayV6TrendPoints;
            set => SetProperty(ref _gatewayV6TrendPoints, value);
        }

        public PointCollection Dns1V6TrendPoints
        {
            get => _dns1V6TrendPoints;
            set => SetProperty(ref _dns1V6TrendPoints, value);
        }

        public PointCollection Dns2V6TrendPoints
        {
            get => _dns2V6TrendPoints;
            set => SetProperty(ref _dns2V6TrendPoints, value);
        }

        public PointCollection SecondaryGatewayTrendPoints
        {
            get => _secondaryGatewayTrendPoints;
            set => SetProperty(ref _secondaryGatewayTrendPoints, value);
        }

        public PointCollection SecondaryDns1TrendPoints
        {
            get => _secondaryDns1TrendPoints;
            set => SetProperty(ref _secondaryDns1TrendPoints, value);
        }

        public PointCollection SecondaryDns2TrendPoints
        {
            get => _secondaryDns2TrendPoints;
            set => SetProperty(ref _secondaryDns2TrendPoints, value);
        }

        public PointCollection SecondaryGatewayV6TrendPoints
        {
            get => _secondaryGatewayV6TrendPoints;
            set => SetProperty(ref _secondaryGatewayV6TrendPoints, value);
        }

        public PointCollection SecondaryDns1V6TrendPoints
        {
            get => _secondaryDns1V6TrendPoints;
            set => SetProperty(ref _secondaryDns1V6TrendPoints, value);
        }

        public PointCollection SecondaryDns2V6TrendPoints
        {
            get => _secondaryDns2V6TrendPoints;
            set => SetProperty(ref _secondaryDns2V6TrendPoints, value);
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

        public string PrimaryDns1AddressV6
        {
            get
            {
                try
                {
                    return PrimaryAdapterDns6List?.Count > 0 ? PrimaryAdapterDns6List[0] : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string PrimaryDns2AddressV6
        {
            get
            {
                try
                {
                    return PrimaryAdapterDns6List?.Count > 1 ? PrimaryAdapterDns6List[1] : string.Empty;
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

        public string SecondaryDns1AddressV6
        {
            get
            {
                try
                {
                    return SecondaryAdapterDns6List?.Count > 0 ? SecondaryAdapterDns6List[0] : string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public string SecondaryDns2AddressV6
        {
            get
            {
                try
                {
                    return SecondaryAdapterDns6List?.Count > 1 ? SecondaryAdapterDns6List[1] : string.Empty;
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

                // Adapter Down: nur Name und MAC anzeigen, Rest auf "Kein Link"
                if (adapter.OperationalStatus != OperationalStatus.Up)
                {
                    IsPrimaryAdapterDown = true;
                    IsPrimaryIpv6Available = true;
                    PrimaryAdapterIpV4List.Clear();
                    PrimaryAdapterIpV4List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoLink"));
                    PrimaryAdapterGateway = string.Empty;
                    PrimaryAdapterDns4List.Clear();
                    PrimaryAdapterIpV6List.Clear();
                    PrimaryAdapterIpV6List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoLink"));
                    PrimaryAdapterGateway6 = string.Empty;
                    PrimaryAdapterDns6List.Clear();
                    OnPropertyChanged(nameof(PrimaryDns1Address));
                    OnPropertyChanged(nameof(PrimaryDns2Address));
                    OnPropertyChanged(nameof(PrimaryDns1AddressV6));
                    OnPropertyChanged(nameof(PrimaryDns2AddressV6));
                    var noLink = LanguageManager.Instance.Lang("ADAPTER_INFO_NoLink");
                    PostGatewayStatus(noLink, noLink, GatewayStatusKind.Bad);
                    PostDns1Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostDns2Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostGatewayV6Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostDns1V6Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostDns2V6Status(noLink, noLink, GatewayStatusKind.Bad);
                    return;
                }

                IsPrimaryAdapterDown = false;
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
                    int prefix4 = GetCidrPrefix(addr.IPv4Mask);
                    PrimaryAdapterIpV4List.Add($"{addr.Address}/{prefix4}");
                }
                if (PrimaryAdapterIpV4List.Count == 0)
                {
                    PrimaryAdapterIpV4List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv4"));
                }

                // Add gateway IPv4
                var gateway4 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                PrimaryAdapterGateway = gateway4?.Address.ToString() ?? LanguageManager.Instance.Lang("ADAPTER_INFO_NoGateway");

                // Add all DNS4 addresses
                var dns4Addresses = ipProperties.DnsAddresses
                    .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var addr in dns4Addresses)
                {
                    PrimaryAdapterDns4List.Add(addr.ToString());
                }
                if (PrimaryAdapterDns4List.Count == 0)
                {
                    PrimaryAdapterDns4List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoDNS"));
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
                    PrimaryAdapterIpV6List.Add($"{addr.Address}/{addr.PrefixLength}");
                }
                var hasIpv6 = PrimaryAdapterIpV6List.Count > 0;
                IsPrimaryIpv6Available = hasIpv6;
                if (!hasIpv6)
                {
                    PrimaryAdapterIpV6List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv6"));
                }

                // Add gateway IPv6
                var gateway6 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
                PrimaryAdapterGateway6 = hasIpv6
                    ? gateway6?.Address.ToString() ?? LanguageManager.Instance.Lang("ADAPTER_INFO_NoGateway")
                    : LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv6");

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
                    PrimaryAdapterDns6List.Add(hasIpv6 ? LanguageManager.Instance.Lang("ADAPTER_INFO_NoDNS") : LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv6"));
                }
                OnPropertyChanged(nameof(PrimaryDns1AddressV6));
                OnPropertyChanged(nameof(PrimaryDns2AddressV6));
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

                // Adapter Down: nur Name und MAC anzeigen, Rest auf "Kein Link"
                if (adapter.OperationalStatus != OperationalStatus.Up)
                {
                    IsSecondaryAdapterDown = true;
                    IsSecondaryIpv6Available = true;
                    SecondaryAdapterIpV4List.Clear();
                    SecondaryAdapterIpV4List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoLink"));
                    SecondaryAdapterGateway = string.Empty;
                    SecondaryAdapterDns4List.Clear();
                    SecondaryAdapterIpV6List.Clear();
                    SecondaryAdapterIpV6List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoLink"));
                    SecondaryAdapterGateway6 = string.Empty;
                    SecondaryAdapterDns6List.Clear();
                    OnPropertyChanged(nameof(SecondaryDns1Address));
                    OnPropertyChanged(nameof(SecondaryDns2Address));
                    OnPropertyChanged(nameof(SecondaryDns1AddressV6));
                    OnPropertyChanged(nameof(SecondaryDns2AddressV6));
                    var noLink = LanguageManager.Instance.Lang("ADAPTER_INFO_NoLink");
                    PostSecondaryGatewayStatus(noLink, noLink, GatewayStatusKind.Bad);
                    PostSecondaryDns1Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostSecondaryDns2Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostSecondaryGatewayV6Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostSecondaryDns1V6Status(noLink, noLink, GatewayStatusKind.Bad);
                    PostSecondaryDns2V6Status(noLink, noLink, GatewayStatusKind.Bad);
                    return;
                }

                IsSecondaryAdapterDown = false;
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
                    int prefix4 = GetCidrPrefix(addr.IPv4Mask);
                    SecondaryAdapterIpV4List.Add($"{addr.Address}/{prefix4}");
                }
                if (SecondaryAdapterIpV4List.Count == 0)
                {
                    SecondaryAdapterIpV4List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv4"));
                }

                // Add gateway IPv4
                var gateway4 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                SecondaryAdapterGateway = gateway4?.Address.ToString() ?? LanguageManager.Instance.Lang("ADAPTER_INFO_NoGateway");

                // Add all DNS4 addresses
                var dns4Addresses = ipProperties.DnsAddresses
                    .Where(d => d.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                foreach (var addr in dns4Addresses)
                {
                    SecondaryAdapterDns4List.Add(addr.ToString());
                }
                if (SecondaryAdapterDns4List.Count == 0)
                {
                    SecondaryAdapterDns4List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoDNS"));
                }

                OnPropertyChanged(nameof(SecondaryDns1Address));
                OnPropertyChanged(nameof(SecondaryDns2Address));

                // Add all IPv6 addresses
                var ipv6Addresses = ipProperties.UnicastAddresses
                    .Where(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                        && !a.Address.IsIPv6LinkLocal);
                foreach (var addr in ipv6Addresses)
                {
                    SecondaryAdapterIpV6List.Add($"{addr.Address}/{addr.PrefixLength}");
                }
                var hasIpv6 = SecondaryAdapterIpV6List.Count > 0;
                IsSecondaryIpv6Available = hasIpv6;
                if (!hasIpv6)
                {
                    SecondaryAdapterIpV6List.Add(LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv6"));
                }

                // Add gateway IPv6
                var gateway6 = ipProperties.GatewayAddresses.FirstOrDefault(g =>
                    g.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);
                SecondaryAdapterGateway6 = hasIpv6
                    ? gateway6?.Address.ToString() ?? LanguageManager.Instance.Lang("ADAPTER_INFO_NoGateway")
                    : LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv6");

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
                    SecondaryAdapterDns6List.Add(hasIpv6 ? LanguageManager.Instance.Lang("ADAPTER_INFO_NoDNS") : LanguageManager.Instance.Lang("ADAPTER_INFO_NoIPv6"));
                }
                OnPropertyChanged(nameof(SecondaryDns1AddressV6));
                OnPropertyChanged(nameof(SecondaryDns2AddressV6));

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
            OnPropertyChanged(nameof(PrimaryDns1AddressV6));
            OnPropertyChanged(nameof(PrimaryDns2AddressV6));
        }

        private static int GetCidrPrefix(System.Net.IPAddress mask)
        {
            int bits = 0;
            foreach (byte b in mask.GetAddressBytes())
            {
                int v = b;
                while (v != 0) { bits += v & 1; v >>= 1; }
            }
            return bits;
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
            OnPropertyChanged(nameof(SecondaryDns1AddressV6));
            OnPropertyChanged(nameof(SecondaryDns2AddressV6));
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
                    isAdapterDown: IsAdapterOperationallyDown(SelectedAdapterPrimary),
                    gatewayAddress: PrimaryAdapterGateway,
                    dnsAddresses: PrimaryAdapterDns4List?.ToList(),
                    gatewayAddressV6: PrimaryAdapterGateway6,
                    dnsAddressesV6: PrimaryAdapterDns6List?.ToList(),
                    postGateway: PostGatewayStatus,
                    postDns1: PostDns1Status,
                    postDns2: PostDns2Status,
                    postGatewayV6: PostGatewayV6Status,
                    postDns1V6: PostDns1V6Status,
                    postDns2V6: PostDns2V6Status);

                await UpdateAdapterStatusAsync(
                    adapterKey: "NIC2",
                    isVisible: IsSecondaryAdapterSelected == Visibility.Visible,
                    isAdapterDown: IsAdapterOperationallyDown(SelectedAdapterSecondary),
                    gatewayAddress: SecondaryAdapterGateway,
                    dnsAddresses: SecondaryAdapterDns4List?.ToList(),
                    gatewayAddressV6: SecondaryAdapterGateway6,
                    dnsAddressesV6: SecondaryAdapterDns6List?.ToList(),
                    postGateway: PostSecondaryGatewayStatus,
                    postDns1: PostSecondaryDns1Status,
                    postDns2: PostSecondaryDns2Status,
                    postGatewayV6: PostSecondaryGatewayV6Status,
                    postDns1V6: PostSecondaryDns1V6Status,
                    postDns2V6: PostSecondaryDns2V6Status);
            }
            catch
            {
                // Fehler beim Status Check - ignorieren
            }
        }

        private async Task UpdateAdapterStatusAsync(
            string adapterKey,
            bool isVisible,
            bool isAdapterDown,
            string? gatewayAddress,
            System.Collections.Generic.List<string>? dnsAddresses,
            System.Action<string, string, GatewayStatusKind> postGateway,
            System.Action<string, string, GatewayStatusKind> postDns1,
            System.Action<string, string, GatewayStatusKind> postDns2,
            string? gatewayAddressV6,
            System.Collections.Generic.List<string>? dnsAddressesV6,
            System.Action<string, string, GatewayStatusKind> postGatewayV6,
            System.Action<string, string, GatewayStatusKind> postDns1V6,
            System.Action<string, string, GatewayStatusKind> postDns2V6)
        {
            if (!isVisible)
            {
                var notConfigured = LanguageManager.Instance.Lang("ADAPTER_STA_NotConfigured");
                postGateway(notConfigured, "Ping: -", GatewayStatusKind.Unknown);
                postDns1(notConfigured, "Ping: -", GatewayStatusKind.Unknown);
                postDns2(notConfigured, "Ping: -", GatewayStatusKind.Unknown);
                postGatewayV6(notConfigured, "Ping: -", GatewayStatusKind.Unknown);
                postDns1V6(notConfigured, "Ping: -", GatewayStatusKind.Unknown);
                postDns2V6(notConfigured, "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            if (isAdapterDown)
            {
                var noLink = LanguageManager.Instance.Lang("ADAPTER_INFO_NoLink");
                postGateway(noLink, noLink, GatewayStatusKind.Bad);
                postDns1(noLink, noLink, GatewayStatusKind.Bad);
                postDns2(noLink, noLink, GatewayStatusKind.Bad);
                postGatewayV6(noLink, noLink, GatewayStatusKind.Bad);
                postDns1V6(noLink, noLink, GatewayStatusKind.Bad);
                postDns2V6(noLink, noLink, GatewayStatusKind.Bad);
                return;
            }

            var gateway = NormalizeHostAddress(gatewayAddress);
            var dns1 = NormalizeHostAddress(dnsAddresses != null && dnsAddresses.Count > 0 ? dnsAddresses[0] : string.Empty);
            var dns2 = NormalizeHostAddress(dnsAddresses != null && dnsAddresses.Count > 1 ? dnsAddresses[1] : string.Empty);
            var gatewayV6 = NormalizeHostAddress(gatewayAddressV6);
            var dns1V6 = NormalizeHostAddress(dnsAddressesV6 != null && dnsAddressesV6.Count > 0 ? dnsAddressesV6[0] : string.Empty);
            var dns2V6 = NormalizeHostAddress(dnsAddressesV6 != null && dnsAddressesV6.Count > 1 ? dnsAddressesV6[1] : string.Empty);

            var checkGateway = _settingsService.GetCheckConnectionGateway();
            var checkDns1 = _settingsService.GetCheckConnectionDns1();
            var checkDns2 = _settingsService.GetCheckConnectionDns2();
            DebugLog($"{adapterKey} targets gw={gateway} dns1={dns1} dns2={dns2} gw6={gatewayV6} dns16={dns1V6} dns26={dns2V6} checks gw={checkGateway} dns1={checkDns1} dns2={checkDns2}");

            if (checkGateway)
            {
                await CheckHostStatusAsync(gateway, adapterKey + "-Gateway", (status, ping, kind) => postGateway(status, ping, kind));
            }
            else
            {
                var disabled = LanguageManager.Instance.Lang("ADAPTER_STA_Disabled");
                postGateway(disabled, "Ping: -", GatewayStatusKind.Unknown);
            }

            if (checkDns1)
            {
                await CheckHostStatusAsync(dns1, adapterKey + "-DNS1", (status, ping, kind) => postDns1(status, ping, kind));
            }
            else
            {
                var disabled = LanguageManager.Instance.Lang("ADAPTER_STA_Disabled");
                postDns1(disabled, "Ping: -", GatewayStatusKind.Unknown);
            }

            if (checkDns2)
            {
                await CheckHostStatusAsync(dns2, adapterKey + "-DNS2", (status, ping, kind) => postDns2(status, ping, kind));
            }
            else
            {
                var disabled = LanguageManager.Instance.Lang("ADAPTER_STA_Disabled");
                postDns2(disabled, "Ping: -", GatewayStatusKind.Unknown);
            }

            if (checkGateway)
            {
                await CheckHostStatusAsync(gatewayV6, adapterKey + "-GatewayV6", (status, ping, kind) => postGatewayV6(status, ping, kind));
            }
            else
            {
                var disabled = LanguageManager.Instance.Lang("ADAPTER_STA_Disabled");
                postGatewayV6(disabled, "Ping: -", GatewayStatusKind.Unknown);
            }

            if (checkDns1)
            {
                await CheckHostStatusAsync(dns1V6, adapterKey + "-DNS1V6", (status, ping, kind) => postDns1V6(status, ping, kind));
            }
            else
            {
                var disabled = LanguageManager.Instance.Lang("ADAPTER_STA_Disabled");
                postDns1V6(disabled, "Ping: -", GatewayStatusKind.Unknown);
            }

            if (checkDns2)
            {
                await CheckHostStatusAsync(dns2V6, adapterKey + "-DNS2V6", (status, ping, kind) => postDns2V6(status, ping, kind));
            }
            else
            {
                var disabled = LanguageManager.Instance.Lang("ADAPTER_STA_Disabled");
                postDns2V6(disabled, "Ping: -", GatewayStatusKind.Unknown);
            }
        }

        private static bool IsAdapterOperationallyDown(string? adapterName)
        {
            if (string.IsNullOrEmpty(adapterName))
                return false;
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(n => n.Name == adapterName);
            return adapter != null && adapter.OperationalStatus != OperationalStatus.Up;
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

        private static PointCollection CreateFlatTrendPoints()
        {
            var points = new PointCollection();
            var step = TrendWidth / (TrendSampleCount - 1);
            var centerY = TrendHeight / 2d;
            for (int i = 0; i < TrendSampleCount; i++)
            {
                points.Add(new Point(i * step, centerY));
            }
            return points;
        }

        private static bool TryExtractPingMs(string pingText, out long ms)
        {
            ms = 0;
            if (string.IsNullOrWhiteSpace(pingText))
            {
                return false;
            }

            long value = 0;
            bool foundDigit = false;
            foreach (var c in pingText)
            {
                if (char.IsDigit(c))
                {
                    foundDigit = true;
                    value = value * 10 + (c - '0');
                }
                else if (foundDigit)
                {
                    ms = value;
                    return true;
                }
            }

            if (foundDigit)
            {
                ms = value;
                return true;
            }

            return false;
        }

        private static double ResolveTrendSample(GatewayStatusKind statusKind, string pingText, ref bool pulseState)
        {
            if (TryExtractPingMs(pingText, out var ms))
            {
                // 0..300 ms auf sichtbaren Zeichenbereich mappen (niedrig = obere Linie)
                var clamped = System.Math.Clamp(ms, 0L, 300L);
                return 2d + (1d - (clamped / 300d)) * (TrendHeight - 4d);
            }

            pulseState = !pulseState;

            if (statusKind == GatewayStatusKind.Bad)
            {
                return pulseState ? 3d : TrendHeight - 2d;
            }

            if (statusKind == GatewayStatusKind.Warning)
            {
                return pulseState ? (TrendHeight / 2d) - 3d : (TrendHeight / 2d) + 3d;
            }

            if (statusKind == GatewayStatusKind.Good)
            {
                return pulseState ? (TrendHeight / 2d) - 1.5d : (TrendHeight / 2d) + 1.5d;
            }

            return TrendHeight / 2d;
        }

        private static PointCollection BuildTrendPoints(Queue<double> history)
        {
            var points = new PointCollection();
            if (history.Count == 0)
            {
                return CreateFlatTrendPoints();
            }

            var samples = history.ToArray();
            var step = TrendWidth / (TrendSampleCount - 1);
            var leading = TrendSampleCount - samples.Length;

            for (int i = 0; i < leading; i++)
            {
                points.Add(new Point(i * step, TrendHeight / 2d));
            }

            for (int i = 0; i < samples.Length; i++)
            {
                points.Add(new Point((i + leading) * step, samples[i]));
            }

            return points;
        }

        private static void PushTrendSample(Queue<double> history, double value)
        {
            history.Enqueue(value);
            while (history.Count > TrendSampleCount)
            {
                history.Dequeue();
            }
        }

        private void UpdateTrend(
            Queue<double> history,
            ref bool pulseState,
            GatewayStatusKind statusKind,
            string pingText,
            System.Action<PointCollection> setPoints)
        {
            var sample = ResolveTrendSample(statusKind, pingText, ref pulseState);
            PushTrendSample(history, sample);
            setPoints(BuildTrendPoints(history));
        }

        private async Task CheckHostStatusAsync(string address, string targetKind, System.Action<string, string, GatewayStatusKind> callback)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                DebugLog($"Skip {targetKind}: address empty");
                var notConfigured = LanguageManager.Instance.Lang("ADAPTER_STA_NotConfigured");
                callback(notConfigured, "Ping: -", GatewayStatusKind.Unknown);
                return;
            }

            // Nur valide IP-Adressen pingen, um Ausnahme-Spam bei ungültigen Host-Strings zu vermeiden.
            if (!IPAddress.TryParse(address, out var parsedAddress))
            {
                DebugLog($"Skip {targetKind}: invalid ip '{address}'");
                var notReachable = LanguageManager.Instance.Lang("ADAPTER_STA_NotReachable");
                var pingInvalid = LanguageManager.Instance.Lang("ADAPTER_STA_PingInvalidAddress");
                callback(notReachable, pingInvalid, GatewayStatusKind.Bad);
                return;
            }

            try
            {
                DebugLog($"Ping start {targetKind} -> {parsedAddress}");
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(parsedAddress, 1000);
                if (reply.Status == IPStatus.Success)
                {
                    var ms = reply.RoundtripTime;

                    // Hole die Schwellwerte aus den Einstellungen
                    int thresholdFast = _settingsService.GetPingThresholdFast();
                    int thresholdNormal = _settingsService.GetPingThresholdNormal();

                    string statusText;
                    GatewayStatusKind statusKind;
                    if (ms <= thresholdFast)
                    {
                        statusText = LanguageManager.Instance.Lang("ADAPTER_STA_Reachable");
                        statusKind = GatewayStatusKind.Good;
                    }
                    else if (ms <= thresholdNormal)
                    {
                        statusText = LanguageManager.Instance.Lang("ADAPTER_STA_Slow");
                        statusKind = GatewayStatusKind.Warning;
                    }
                    else
                    {
                        statusText = LanguageManager.Instance.Lang("ADAPTER_STA_VerySlow");
                        statusKind = GatewayStatusKind.Bad;
                    }
                    DebugLog($"Ping ok {targetKind} -> {parsedAddress} ({ms} ms)");
                    var pingMs = $"Ping: {ms} ms";
                    callback(statusText, pingMs, statusKind);
                }
                else
                {
                    DebugLog($"Ping no-reply {targetKind} -> {parsedAddress} status={reply.Status}");
                    var notReachable = LanguageManager.Instance.Lang("ADAPTER_STA_NotReachable");
                    var pingTimeout = LanguageManager.Instance.Lang("ADAPTER_STA_PingTimeout");
                    callback(notReachable, pingTimeout, GatewayStatusKind.Bad);
                }
            }
            catch (PingException)
            {
                DebugLog($"Ping exception {targetKind} -> {address}");
                var notReachable = LanguageManager.Instance.Lang("ADAPTER_STA_NotReachable");
                var pingFailed = LanguageManager.Instance.Lang("ADAPTER_STA_PingFailed");
                callback(notReachable, pingFailed, GatewayStatusKind.Bad);
            }
            catch
            {
                DebugLog($"Ping error {targetKind} -> {address}");
                var error = LanguageManager.Instance.Lang("ADAPTER_STA_Error");
                var pingError = LanguageManager.Instance.Lang("ADAPTER_STA_PingError");
                callback(error, pingError, GatewayStatusKind.Bad);
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
                UpdateTrend(_gatewayTrendHistory, ref _gatewayTrendPulse, statusKind, pingText, points => GatewayTrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                GatewayStatusText = statusText;
                GatewayPingText = pingText;
                GatewayStatusKind = statusKind;
                UpdateTrend(_gatewayTrendHistory, ref _gatewayTrendPulse, statusKind, pingText, points => GatewayTrendPoints = points);
            }, null);
        }

        private void PostDns1Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns1StatusText = statusText;
                Dns1PingText = pingText;
                Dns1StatusKind = statusKind;
                UpdateTrend(_dns1TrendHistory, ref _dns1TrendPulse, statusKind, pingText, points => Dns1TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns1StatusText = statusText;
                Dns1PingText = pingText;
                Dns1StatusKind = statusKind;
                UpdateTrend(_dns1TrendHistory, ref _dns1TrendPulse, statusKind, pingText, points => Dns1TrendPoints = points);
            }, null);
        }

        private void PostDns2Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns2StatusText = statusText;
                Dns2PingText = pingText;
                Dns2StatusKind = statusKind;
                UpdateTrend(_dns2TrendHistory, ref _dns2TrendPulse, statusKind, pingText, points => Dns2TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns2StatusText = statusText;
                Dns2PingText = pingText;
                Dns2StatusKind = statusKind;
                UpdateTrend(_dns2TrendHistory, ref _dns2TrendPulse, statusKind, pingText, points => Dns2TrendPoints = points);
            }, null);
        }

        private void PostGatewayV6Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                GatewayV6StatusText = statusText;
                GatewayV6PingText = pingText;
                GatewayV6StatusKind = statusKind;
                UpdateTrend(_gatewayV6TrendHistory, ref _gatewayV6TrendPulse, statusKind, pingText, points => GatewayV6TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                GatewayV6StatusText = statusText;
                GatewayV6PingText = pingText;
                GatewayV6StatusKind = statusKind;
                UpdateTrend(_gatewayV6TrendHistory, ref _gatewayV6TrendPulse, statusKind, pingText, points => GatewayV6TrendPoints = points);
            }, null);
        }

        private void PostDns1V6Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns1V6StatusText = statusText;
                Dns1V6PingText = pingText;
                Dns1V6StatusKind = statusKind;
                UpdateTrend(_dns1V6TrendHistory, ref _dns1V6TrendPulse, statusKind, pingText, points => Dns1V6TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns1V6StatusText = statusText;
                Dns1V6PingText = pingText;
                Dns1V6StatusKind = statusKind;
                UpdateTrend(_dns1V6TrendHistory, ref _dns1V6TrendPulse, statusKind, pingText, points => Dns1V6TrendPoints = points);
            }, null);
        }

        private void PostDns2V6Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                Dns2V6StatusText = statusText;
                Dns2V6PingText = pingText;
                Dns2V6StatusKind = statusKind;
                UpdateTrend(_dns2V6TrendHistory, ref _dns2V6TrendPulse, statusKind, pingText, points => Dns2V6TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                Dns2V6StatusText = statusText;
                Dns2V6PingText = pingText;
                Dns2V6StatusKind = statusKind;
                UpdateTrend(_dns2V6TrendHistory, ref _dns2V6TrendPulse, statusKind, pingText, points => Dns2V6TrendPoints = points);
            }, null);
        }

        private void PostSecondaryGatewayStatus(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryGatewayStatusText = statusText;
                SecondaryGatewayPingText = pingText;
                SecondaryGatewayStatusKind = statusKind;
                UpdateTrend(_secondaryGatewayTrendHistory, ref _secondaryGatewayTrendPulse, statusKind, pingText, points => SecondaryGatewayTrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryGatewayStatusText = statusText;
                SecondaryGatewayPingText = pingText;
                SecondaryGatewayStatusKind = statusKind;
                UpdateTrend(_secondaryGatewayTrendHistory, ref _secondaryGatewayTrendPulse, statusKind, pingText, points => SecondaryGatewayTrendPoints = points);
            }, null);
        }

        private void PostSecondaryDns1Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryDns1StatusText = statusText;
                SecondaryDns1PingText = pingText;
                SecondaryDns1StatusKind = statusKind;
                UpdateTrend(_secondaryDns1TrendHistory, ref _secondaryDns1TrendPulse, statusKind, pingText, points => SecondaryDns1TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryDns1StatusText = statusText;
                SecondaryDns1PingText = pingText;
                SecondaryDns1StatusKind = statusKind;
                UpdateTrend(_secondaryDns1TrendHistory, ref _secondaryDns1TrendPulse, statusKind, pingText, points => SecondaryDns1TrendPoints = points);
            }, null);
        }

        private void PostSecondaryDns2Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryDns2StatusText = statusText;
                SecondaryDns2PingText = pingText;
                SecondaryDns2StatusKind = statusKind;
                UpdateTrend(_secondaryDns2TrendHistory, ref _secondaryDns2TrendPulse, statusKind, pingText, points => SecondaryDns2TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryDns2StatusText = statusText;
                SecondaryDns2PingText = pingText;
                SecondaryDns2StatusKind = statusKind;
                UpdateTrend(_secondaryDns2TrendHistory, ref _secondaryDns2TrendPulse, statusKind, pingText, points => SecondaryDns2TrendPoints = points);
            }, null);
        }

        private void PostSecondaryGatewayV6Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryGatewayV6StatusText = statusText;
                SecondaryGatewayV6PingText = pingText;
                SecondaryGatewayV6StatusKind = statusKind;
                UpdateTrend(_secondaryGatewayV6TrendHistory, ref _secondaryGatewayV6TrendPulse, statusKind, pingText, points => SecondaryGatewayV6TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryGatewayV6StatusText = statusText;
                SecondaryGatewayV6PingText = pingText;
                SecondaryGatewayV6StatusKind = statusKind;
                UpdateTrend(_secondaryGatewayV6TrendHistory, ref _secondaryGatewayV6TrendPulse, statusKind, pingText, points => SecondaryGatewayV6TrendPoints = points);
            }, null);
        }

        private void PostSecondaryDns1V6Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryDns1V6StatusText = statusText;
                SecondaryDns1V6PingText = pingText;
                SecondaryDns1V6StatusKind = statusKind;
                UpdateTrend(_secondaryDns1V6TrendHistory, ref _secondaryDns1V6TrendPulse, statusKind, pingText, points => SecondaryDns1V6TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryDns1V6StatusText = statusText;
                SecondaryDns1V6PingText = pingText;
                SecondaryDns1V6StatusKind = statusKind;
                UpdateTrend(_secondaryDns1V6TrendHistory, ref _secondaryDns1V6TrendPulse, statusKind, pingText, points => SecondaryDns1V6TrendPoints = points);
            }, null);
        }

        private void PostSecondaryDns2V6Status(string statusText, string pingText, GatewayStatusKind statusKind)
        {
            if (_uiContext == null)
            {
                SecondaryDns2V6StatusText = statusText;
                SecondaryDns2V6PingText = pingText;
                SecondaryDns2V6StatusKind = statusKind;
                UpdateTrend(_secondaryDns2V6TrendHistory, ref _secondaryDns2V6TrendPulse, statusKind, pingText, points => SecondaryDns2V6TrendPoints = points);
                return;
            }

            _uiContext.Post(_ =>
            {
                SecondaryDns2V6StatusText = statusText;
                SecondaryDns2V6PingText = pingText;
                SecondaryDns2V6StatusKind = statusKind;
                UpdateTrend(_secondaryDns2V6TrendHistory, ref _secondaryDns2V6TrendPulse, statusKind, pingText, points => SecondaryDns2V6TrendPoints = points);
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

        public void RegisterNetworkChangeEvents()
        {
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            DebugLog("NetworkChange events registered");
        }

        public void UnregisterNetworkChangeEvents()
        {
            NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
            _networkChangeCts?.Cancel();
            _networkChangeCts = null;
            DebugLog("NetworkChange events unregistered");
        }

        private void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            DebugLog("NetworkAddressChanged fired");
            ScheduleAdapterInfoRefresh();
        }

        private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            DebugLog($"NetworkAvailabilityChanged fired available={e.IsAvailable}");
            ScheduleAdapterInfoRefresh();
        }

        private void ScheduleAdapterInfoRefresh()
        {
            _networkChangeCts?.Cancel();
            _networkChangeCts = new CancellationTokenSource();
            var token = _networkChangeCts.Token;
            Task.Delay(400, token).ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    PostRefreshAdapterInfo();
                }
            }, TaskScheduler.Default);
        }

        private void PostRefreshAdapterInfo()
        {
            if (_uiContext != null)
            {
                _uiContext.Post(_ =>
                {
                    if (!string.IsNullOrEmpty(SelectedAdapterPrimary))
                        UpdatePrimaryAdapterInfo();
                    if (!string.IsNullOrEmpty(SelectedAdapterSecondary))
                        UpdateSecondaryAdapterInfo();
                }, null);
            }
            else
            {
                if (!string.IsNullOrEmpty(SelectedAdapterPrimary))
                    UpdatePrimaryAdapterInfo();
                if (!string.IsNullOrEmpty(SelectedAdapterSecondary))
                    UpdateSecondaryAdapterInfo();
            }
        }
    }
}
